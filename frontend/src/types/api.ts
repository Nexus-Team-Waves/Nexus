/**
 * TypeScript mirror of the backend API DTOs (Mems.Application.Workflow.ApprovalContracts).
 * These are the shapes crossing the wire — keep them in sync with the C# records.
 */

export type Role = 'Employee' | 'LineManager' | 'AdminHr' | 'Finance' | 'TopLevel'

export interface ApiUser {
  email: string
  displayName: string
  role: Role
  employeeId: string | null
}

export interface AuthResponse {
  token: string
  user: ApiUser
}

export interface DependantDto {
  id: string
  label: string
  relationship: string
}

export interface EntitlementDto {
  year: number
  firstName: string
  gradeLabel: string
  cap: number
  consumed: number
  remaining: number
  dependants: DependantDto[]
}

export type LineStatus = 'Pending' | 'Approved' | 'Reduced' | 'Rejected'
export type OverallStatus = 'InReview' | 'PartiallyApproved' | 'Approved' | 'ActionNeeded' | 'Posted' | 'Rejected'

/** One per-line audit entry: a submit/decision plus its amount, reason, and comment. */
export interface ClaimLineEventDto {
  stageLabel: string
  action: 'Submitted' | 'Resubmitted' | 'Approved' | 'Reduced' | 'Rejected'
  actorRole: string
  actorName: string
  amount: number | null
  reason: string | null
  comment: string | null
  at: string
}

export interface ClaimLineDto {
  lineId: string
  beneficiaryKind: 'Self' | 'Dependant'
  dependantId: string | null
  dependantRelationship: string | null
  category: string
  expenseDate: string
  claimedAmount: number
  currentAmount: number
  approvedAmount: number | null
  status: LineStatus
  rejectionReason: string | null
  receiptReference: string | null
  /** Ids of the stored receipt images (1–5); empty on claims from before image upload existed. */
  receiptIds: string[]
  /**
   * Decided in an earlier round and locked: the employee cannot edit it on resubmit and no
   * stage decides it again (decision 2026-07-23).
   */
  locked: boolean
  /** Line-wise audit trail: submits, decisions with amounts/reasons, and comments. */
  events: ClaimLineEventDto[]
}

export interface ApprovalEventDto {
  stage: string
  actorRole: string
  actorName: string
  summary: string
  at: string
}

export interface ClaimDto {
  id: string
  employeeId: string
  submissionDate: string
  stage: string
  stageLabel: string
  overallStatus: OverallStatus
  posted: boolean
  sapReference: string | null
  totalClaimed: number
  totalApproved: number
  editable: boolean
  lines: ClaimLineDto[]
  history: ApprovalEventDto[]
}

/** Outbound: a claim submission / edit. */
export interface SubmitClaimLine {
  lineId: string
  beneficiaryKind: 'Self' | 'Dependant'
  dependantId?: string
  dependantRelationship?: string
  category: string
  expenseDate: string
  claimedAmount: number
  /** Display string for the receipts (joined original filenames). */
  receiptReference?: string
  /** Ids returned by POST /api/receipts — the stored images (1–5, required by the server). */
  receiptIds?: string[]
  /** Optional note for the approvers on this line. */
  comment?: string
}

/** Response from POST /api/receipts (multipart image upload). */
export interface ReceiptUploadResponse {
  receiptId: string
  fileName: string
  sizeBytes: number
}
export interface SubmitClaimRequest {
  claimId: string
  employeeId: string
  submissionDate: string
  lines: SubmitClaimLine[]
}

/** Outbound: an approver's per-line decision. */
export type DecisionAction = 'approve' | 'reduce' | 'reject'
export interface LineDecision {
  lineId: string
  action: DecisionAction
  amount?: number
  reason?: string
  /** Optional note on this line, visible to the employee and later stages. */
  comment?: string
}

/** One in-app notification for the signed-in user. */
export interface NotificationDto {
  id: string
  claimId: string
  title: string
  body: string
  createdAt: string
  read: boolean
}
