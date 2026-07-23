import StatusPill from '../../components/StatusPill'
import LineTimeline from '../../components/LineTimeline'
import { claimPrimaryLabel, formatRs, shortCategory } from '../../types/domain'
import type { ClaimDto } from '../../types/api'

/**
 * "Your claims" — the employee's full list from the API. Each card shows the derived status, its
 * items, where it is in the approval chain, and the append-only history. Editable claims (all lines
 * still pending, or returned after a rejection) get an Edit action.
 */
export default function ClaimsScreen({
  claims,
  onEdit,
}: {
  claims: ClaimDto[]
  onEdit: (claim: ClaimDto) => void
}) {
  return (
    <div className="screen-pad">
      <h1 className="screen-title">Your claims</h1>

      {claims.length === 0 ? (
        <p className="muted-note">No claims yet.</p>
      ) : (
        <div className="claim-cards">
          {claims.map((claim) => (
            <article key={claim.id} className="claim-card">
              <div className="claim-card-top">
                <div>
                  <p className="claim-card-title">
                    {claimPrimaryLabel(claim.lines.length, claim.lines[0]?.category ?? '')}
                  </p>
                  <p className="claim-card-meta">
                    {claim.submissionDate} · {claim.posted ? 'Posted' : claim.stageLabel}
                  </p>
                </div>
                <div className="claim-card-right">
                  <StatusPill status={claim.overallStatus} />
                  <p className="claim-card-total">{formatRs(claim.totalClaimed)}</p>
                </div>
              </div>

              <ul className="claim-line-list">
                {claim.lines.map((l) => (
                  <li key={l.lineId} className="claim-line-item">
                    <div className="claim-line-row">
                      <span>
                        {l.beneficiaryKind === 'Self' ? 'Self' : 'Dependant'} · {shortCategory(l.category)}
                        {l.status === 'Rejected' && l.rejectionReason ? ` — ${l.rejectionReason}` : ''}
                      </span>
                      <span className="claim-line-amounts">
                        {/* A reduced line shows what changed: claimed → approved. */}
                        {l.status === 'Reduced' && l.approvedAmount != null
                          ? <>
                              <s>{formatRs(l.claimedAmount)}</s> {formatRs(l.approvedAmount)}
                            </>
                          : formatRs(l.claimedAmount)}
                        {' '}
                        <StatusPill status={l.status} />
                      </span>
                    </div>
                    <LineTimeline events={l.events} />
                  </li>
                ))}
              </ul>

              {claim.sapReference && (
                <p className="sap-ref">SAP reference: {claim.sapReference}</p>
              )}

              {claim.history.length > 0 && (
                <details className="history">
                  <summary>History ({claim.history.length})</summary>
                  <ul className="history-list">
                    {claim.history.map((h, i) => (
                      // eslint-disable-next-line react/no-array-index-key -- append-only, stable order
                      <li key={i}>
                        <span className="history-stage">{h.stage}</span>
                        <span>{h.summary}</span>
                      </li>
                    ))}
                  </ul>
                </details>
              )}

              {claim.editable && (
                <div className="claim-card-actions">
                  <button type="button" className="btn btn--outline btn--sm" onClick={() => onEdit(claim)}>
                    Edit &amp; resubmit
                  </button>
                </div>
              )}
            </article>
          ))}
        </div>
      )}
    </div>
  )
}
