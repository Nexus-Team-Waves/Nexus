/**
 * TypeScript mirror of the MEMS backend domain (Mems.Domain).
 * ---------------------------------------------------------------------------
 * These are the SAME concepts the C# domain enforces — kept in sync by hand for now.
 * The frontend uses them only to build a valid claim payload and to DISPLAY status; the
 * authoritative rules (entitlement amounts, approval routing, cap enforcement) live on the
 * server and must never be trusted from here. Anything computed in this file is for the UI.
 *
 * String-literal unions (not TS `enum`s) are used deliberately: they serialise to readable
 * values in the Dexie queue and on the wire, and match the C# enum member names 1:1.
 */

// ---- Grades & entitlement (docs/entitlement-rules.md § Salary & entitlement base) ----

export type GradeBand = 'MSenior' | 'MStandard' | 'Executive'

/** Human labels for the three covered bands. */
export const GRADE_BAND_LABEL: Record<GradeBand, string> = {
  MSenior: 'Grade M4+',
  MStandard: 'Grade M1–M3',
  Executive: 'Grade E1–E5',
}

/** Annual entitlement factor applied to Basic Monthly Salary. Display-only mirror of policy. */
export const ENTITLEMENT_FACTOR: Record<GradeBand, number> = {
  MSenior: 1,
  MStandard: 1.5,
  Executive: 2,
}

/** Per-day room-rate cap by band (fixed policy figure) — used to hint the user on the form. */
export const ROOM_RATE_CAP_PER_DAY: Record<GradeBand, number> = {
  MSenior: 8000,
  MStandard: 6000,
  Executive: 3000,
}

// ---- Claim structure (docs/entitlement-rules.md § Claim structure) ----

export type ClaimCategory =
  | 'Opd'
  | 'Hospitalization'
  | 'Maternity'
  | 'Dental'
  | 'Consultation'

/** Ordered list with labels, for <select> options and display. */
export const CLAIM_CATEGORIES: ReadonlyArray<{ value: ClaimCategory; label: string }> = [
  { value: 'Opd', label: 'OPD (out-patient)' },
  { value: 'Hospitalization', label: 'Hospitalization' },
  { value: 'Maternity', label: 'Maternity' },
  { value: 'Dental', label: 'Dental (limited)' },
  { value: 'Consultation', label: 'Doctor / Hakim / Homeopath' },
]

/** Short label for compact rows (lists, chips). */
export const CATEGORY_SHORT: Record<ClaimCategory, string> = {
  Opd: 'OPD',
  Hospitalization: 'Hospitalization',
  Maternity: 'Maternity',
  Dental: 'Dental',
  Consultation: 'Consultation',
}

/** Short category label for an arbitrary server-provided category string. */
export function shortCategory(category: string): string {
  return (CATEGORY_SHORT as Record<string, string>)[category] ?? category
}

/** "OPD" for a single-line claim, or "3 items" for several. */
export function claimPrimaryLabel(lineCount: number, firstCategory: string): string {
  return lineCount === 1 ? shortCategory(firstCategory) : `${lineCount} items`
}

export type BeneficiaryKind = 'Self' | 'Dependant'
export type DependantRelationship = 'Spouse' | 'Child'

/** Per-line decision status. */
export type ClaimLineStatus = 'Pending' | 'Approved' | 'PartiallyApproved' | 'Rejected'

/** Derived, claim-level status. Never stored — computed from the line statuses. */
export type ClaimOverallStatus = 'InReview' | 'Approved' | 'PartiallyApproved' | 'ActionNeeded'

export const OVERALL_STATUS_LABEL: Record<ClaimOverallStatus, string> = {
  InReview: 'In review',
  Approved: 'Approved',
  PartiallyApproved: 'Partially approved',
  ActionNeeded: 'Action needed',
}

// ---- Helpers ----

/**
 * Format a Rupee amount for display as "Rs 34,500". Plain thousands grouping (not Intl currency,
 * which renders sub-continental lakh/crore grouping that reads oddly here). Whole amounts show no
 * decimals; fractional amounts show up to two.
 */
export function formatRs(amount: number): string {
  return `Rs ${amount.toLocaleString('en-US', { maximumFractionDigits: 2 })}`
}

/**
 * Derive the overall claim status from its line statuses, mirroring Claim.OverallStatus in the
 * C# domain EXACTLY (precedence matters):
 *   any Rejected → Action needed; else all Approved → Approved;
 *   else any Pending → In review; else → Partially approved.
 */
export function deriveOverallStatus(
  lineStatuses: ReadonlyArray<ClaimLineStatus>,
): ClaimOverallStatus {
  if (lineStatuses.some((s) => s === 'Rejected')) return 'ActionNeeded'
  if (lineStatuses.every((s) => s === 'Approved')) return 'Approved'
  if (lineStatuses.some((s) => s === 'Pending')) return 'InReview'
  return 'PartiallyApproved'
}

/** Whole days between an expense date and the submission date (informational indicator). */
export function daysElapsed(expenseDateIso: string, submissionDateIso: string): number {
  const oneDayMs = 24 * 60 * 60 * 1000
  const expense = new Date(expenseDateIso).getTime()
  const submitted = new Date(submissionDateIso).getTime()
  return Math.max(0, Math.floor((submitted - expense) / oneDayMs))
}
