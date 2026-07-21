/**
 * The claim payload shape the frontend builds and queues. Mirrors the backend's
 * SubmitClaimRequest / SubmitClaimLine DTOs (Mems.Application) so that, once the claims API
 * endpoint exists, the sync engine can POST this object as-is.
 */

import type {
  BeneficiaryKind,
  ClaimCategory,
  DependantRelationship,
} from './domain'

export interface ClaimLineInput {
  /** Client-generated UUID for the line (offline traceability). */
  lineId: string
  beneficiaryKind: BeneficiaryKind
  /** Present only when beneficiaryKind === 'Dependant'. */
  dependantId?: string
  dependantRelationship?: DependantRelationship
  category: ClaimCategory
  /** ISO-8601 date (yyyy-mm-dd) the expense was incurred. */
  expenseDate: string
  claimedAmount: number
  /** Filename / pointer to the receipt. The bytes are not modelled in this mockup. */
  receiptReference?: string
}

export interface ClaimPayload {
  employeeId: string
  /** ISO-8601 date the claim was submitted (client local date). */
  submissionDate: string
  lines: ClaimLineInput[]
}
