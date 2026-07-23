import { formatRs } from '../types/domain'
import type { ClaimLineEventDto } from '../types/api'

/**
 * One claim line's audit trail: who submitted/approved/reduced/rejected it, at which stage,
 * with amounts, rejection reasons, and free-text comments. Shown to the employee AND to every
 * approver so line-wise changes and comments travel through the whole chain.
 */
export default function LineTimeline({ events }: { events: ClaimLineEventDto[] }) {
  if (events.length === 0) return null
  return (
    <details className="line-timeline">
      <summary>Changes &amp; comments ({events.length})</summary>
      <ul className="history-list">
        {events.map((e, i) => (
          // eslint-disable-next-line react/no-array-index-key -- append-only, stable order
          <li key={i}>
            <span className="history-stage">{e.actorRole}</span>
            <span>
              {describe(e)}
              {e.comment ? <em className="line-comment"> — “{e.comment}”</em> : null}
            </span>
          </li>
        ))}
      </ul>
    </details>
  )
}

function describe(e: ClaimLineEventDto): string {
  switch (e.action) {
    case 'Submitted': return `${e.actorName} submitted for ${formatRs(e.amount ?? 0)}`
    case 'Resubmitted': return `${e.actorName} edited and resubmitted for ${formatRs(e.amount ?? 0)}`
    case 'Approved': return `${e.actorName} approved ${formatRs(e.amount ?? 0)}`
    case 'Reduced': return `${e.actorName} reduced to ${formatRs(e.amount ?? 0)}`
    case 'Rejected': return `${e.actorName} rejected${e.reason ? `: ${e.reason}` : ''}`
    default: return `${e.actorName} — ${e.action}`
  }
}
