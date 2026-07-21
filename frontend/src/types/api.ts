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
export type OverallStatus = 'InReview' | 'PartiallyApproved' | 'Approved' | 'ActionNeeded' | 'Posted'

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
  receiptReference?: string
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
}
