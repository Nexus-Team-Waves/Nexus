import type { LineStatus, OverallStatus } from '../types/api'

/**
 * Small coloured pill for a claim's overall status or one line's status. Colour is paired with
 * a text label (never colour alone) so it stays legible to colour-blind users.
 */
const META: Record<OverallStatus | LineStatus, { label: string; tone: string }> = {
  InReview: { label: 'In review', tone: 'review' },
  PartiallyApproved: { label: 'Partially approved', tone: 'info' },
  Approved: { label: 'Approved', tone: 'ok' },
  ActionNeeded: { label: 'Action needed', tone: 'danger' },
  Posted: { label: 'Posted to SAP', tone: 'ok' },
  // Per-line statuses:
  Pending: { label: 'Pending', tone: 'review' },
  Reduced: { label: 'Reduced', tone: 'info' },
  Rejected: { label: 'Rejected', tone: 'danger' },
}

export default function StatusPill({ status }: { status: OverallStatus | LineStatus }) {
  const m = META[status]
  return <span className={`pill pill--${m.tone}`}>{m.label}</span>
}
