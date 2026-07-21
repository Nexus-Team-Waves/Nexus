/**
 * MEMS offline store (Dexie over IndexedDB).
 * ---------------------------------------------------------------------------
 * WHY THIS EXISTS
 * Frontline staff submit claims on unstable networks. Rather than fail a submission
 * when the network is down, we write it to a local queue in the browser and push it
 * to the API later. The user's claim is safe the moment it hits this queue.
 *
 * THE RULE THAT KEEPS THIS SAFE
 * Every queued claim carries a client-generated UUID (`clientId`). That id travels to
 * the server as an idempotency key. If we retry — because the network dropped mid-request
 * and we genuinely do not know whether the server got it — the server recognises the id
 * and returns the original result instead of creating a second claim. Without this, a
 * flaky connection silently produces duplicate claims, which in a financial system means
 * double-posting to SAP. This is the single most important invariant in the offline path.
 *
 * NOTE FOR REVIEWERS: this file defines the queue envelope. The claim *body* is now modelled
 * as `ClaimPayload` (src/types/claim.ts), which mirrors the backend SubmitClaimRequest DTO —
 * the entitlement rules that determine those fields were agreed on 2026-07-21
 * (/docs/entitlement-rules.md).
 */

import Dexie, { type EntityTable } from 'dexie'
import type { ClaimPayload } from '../types/claim'

/**
 * Lifecycle of a queued claim:
 *
 *   pending ──► syncing ──► synced        (happy path)
 *      ▲           │
 *      └───────────┤ (network failed — retry later)
 *                  │
 *                  ├──► conflict          (server disagrees; a human must look)
 *                  └──► failed            (rejected outright; do not retry blindly)
 */
export type ClaimSyncStatus = 'pending' | 'syncing' | 'synced' | 'conflict' | 'failed'

export interface QueuedClaim {
  /** Client-generated UUID. The idempotency key — see file header. Never reassigned. */
  clientId: string

  /** Server-assigned id, populated only after a successful sync. */
  serverId?: string

  status: ClaimSyncStatus

  /** The claim itself — mirrors the backend SubmitClaimRequest DTO. */
  payload: ClaimPayload

  /** ISO-8601 UTC. When the user pressed submit — NOT when it reached the server. */
  createdAt: string

  /** ISO-8601 UTC of the last sync attempt, for backoff and diagnostics. */
  lastAttemptAt?: string

  /** How many times we have tried to push this. Drives exponential backoff. */
  attempts: number

  /**
   * Last error, for display and debugging.
   * MUST NOT contain PII or medical detail (CLAUDE.md §10) — store a code/message,
   * never an echo of the claim body.
   */
  lastError?: string
}

const DB_NAME = 'mems'

class MemsDatabase extends Dexie {
  /** The outbound claim queue. */
  claims!: EntityTable<QueuedClaim, 'clientId'>

  constructor() {
    super(DB_NAME)

    /**
     * Schema version 1.
     *
     * Dexie migrations are append-only: to change the schema, add a NEW `.version(n)`
     * block — never edit an existing one. Users have live databases on their devices
     * holding unsynced claims; rewriting version 1 would corrupt or drop them.
     *
     * Indexed fields are listed below. `clientId` is the primary key (`&` = unique).
     * `status` and `createdAt` are indexed because the sync engine queries by status
     * and drains oldest-first. `payload` is not indexed — you cannot query it.
     */
    this.version(1).stores({
      claims: '&clientId, status, createdAt',
    })
  }
}

export const db = new MemsDatabase()

/**
 * Enqueue a claim for submission. This is what the claim form calls — online or offline,
 * the path is identical. That uniformity is the point: there is no separate "offline mode"
 * to get wrong, and the happy path is exercised constantly rather than only when the
 * network fails.
 *
 * @returns the clientId, which is the claim's stable identity from here on.
 */
export async function enqueueClaim(payload: ClaimPayload): Promise<string> {
  const clientId = crypto.randomUUID()

  await db.claims.add({
    clientId,
    status: 'pending',
    payload,
    createdAt: new Date().toISOString(),
    attempts: 0,
  })

  return clientId
}

/** Claims still needing to reach the server, oldest first. */
export function pendingClaims(): Promise<QueuedClaim[]> {
  return db.claims.where('status').anyOf('pending', 'conflict', 'failed').sortBy('createdAt')
}
