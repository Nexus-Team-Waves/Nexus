import { useState } from 'react'
import { formatRs, shortCategory } from '../../types/domain'
import { ApiError, decideClaim, postClaim } from '../../api/client'
import type { ClaimDto, DecisionAction, LineDecision } from '../../types/api'

/**
 * Review one claim. In an approval stage, the approver decides every line (approve / reduce /
 * reject-with-reason) and submits them together — the server then advances the claim or returns it
 * (docs/entitlement-rules.md § Approval flow). For a completed claim, Finance records the manual
 * SAP posting reference instead (docs/CLAUDE.md §7).
 */
interface LineChoice {
  action: DecisionAction
  amount: string // for 'reduce'
  reason: string // for 'reject'
}

export default function ClaimReviewScreen({
  claim,
  onBack,
  onUpdated,
}: {
  claim: ClaimDto
  onBack: () => void
  onUpdated: (updated: ClaimDto) => void
}) {
  const posting = claim.stage === 'Completed' && !claim.posted

  const [choices, setChoices] = useState<Record<string, LineChoice>>(() =>
    Object.fromEntries(claim.lines.map((l) => [l.lineId, { action: 'approve', amount: String(l.currentAmount), reason: '' }])),
  )
  const [sapRef, setSapRef] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const setChoice = (lineId: string, next: Partial<LineChoice>) => {
    setChoices((prev) => ({ ...prev, [lineId]: { ...prev[lineId]!, ...next } }))
    setError(null)
  }

  async function submitDecision() {
    setError(null)
    const decisions: LineDecision[] = []
    for (const line of claim.lines) {
      const c = choices[line.lineId]!
      if (c.action === 'reduce') {
        const amount = Number.parseFloat(c.amount)
        if (!Number.isFinite(amount) || amount <= 0 || amount >= line.currentAmount) {
          setError(`${shortCategory(line.category)}: a reduced amount must be between 0 and ${formatRs(line.currentAmount)}.`)
          return
        }
        decisions.push({ lineId: line.lineId, action: 'reduce', amount })
      } else if (c.action === 'reject') {
        if (!c.reason.trim()) { setError(`${shortCategory(line.category)}: a rejection needs a reason.`); return }
        decisions.push({ lineId: line.lineId, action: 'reject', reason: c.reason.trim() })
      } else {
        decisions.push({ lineId: line.lineId, action: 'approve' })
      }
    }

    setBusy(true)
    try {
      const updated = await decideClaim(claim.id, decisions)
      onUpdated(updated)
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Could not submit the decision.')
    } finally {
      setBusy(false)
    }
  }

  async function submitPosting() {
    if (!sapRef.trim()) { setError('Enter the SAP reference number.'); return }
    setBusy(true)
    setError(null)
    try {
      const updated = await postClaim(claim.id, sapRef.trim())
      onUpdated(updated)
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Could not record the posting.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="screen-pad">
      <button type="button" className="btn btn--link back-link" onClick={onBack}>← Back to queue</button>
      <h1 className="screen-title">{posting ? 'Record SAP posting' : 'Review claim'}</h1>
      <p className="panel-sub">
        {claim.employeeId} · submitted {claim.submissionDate} · stage: {claim.stageLabel}
      </p>

      {error && <p className="banner banner--error" role="alert">{error}</p>}

      {posting ? (
        <>
          <div className="review-lines">
            {claim.lines.map((l) => (
              <div key={l.lineId} className="review-line">
                <div className="review-line-head">
                  <strong>{shortCategory(l.category)}</strong>
                  <span>{formatRs(l.approvedAmount ?? 0)}</span>
                </div>
                <p className="claim-card-meta">{l.beneficiaryKind === 'Self' ? 'Self' : 'Dependant'}</p>
              </div>
            ))}
          </div>
          <div className="total-row">
            <span>Approved total to post</span>
            <strong>{formatRs(claim.totalApproved)}</strong>
          </div>
          <label className="field">
            <span>SAP document reference</span>
            <input type="text" placeholder="e.g. AR-2026-000123" value={sapRef} onChange={(e) => setSapRef(e.target.value)} />
          </label>
          <button type="button" className="btn btn--primary btn--block" onClick={submitPosting} disabled={busy}>
            {busy ? 'Recording…' : 'Mark posted'}
          </button>
        </>
      ) : (
        <>
          <div className="review-lines">
            {claim.lines.map((line) => {
              const c = choices[line.lineId]!
              return (
                <div key={line.lineId} className="review-line">
                  <div className="review-line-head">
                    <strong>{shortCategory(line.category)}</strong>
                    <span>{formatRs(line.currentAmount)}</span>
                  </div>
                  <p className="claim-card-meta">
                    {line.beneficiaryKind === 'Self' ? 'Self' : 'Dependant'}
                    {line.claimedAmount !== line.currentAmount ? ` · originally ${formatRs(line.claimedAmount)}` : ''}
                  </p>

                  <div className="seg">
                    {(['approve', 'reduce', 'reject'] as DecisionAction[]).map((a) => (
                      <button
                        key={a}
                        type="button"
                        className={`seg-btn ${c.action === a ? 'seg-btn--on seg-btn--' + a : ''}`}
                        onClick={() => setChoice(line.lineId, { action: a })}
                      >
                        {a === 'approve' ? 'Approve' : a === 'reduce' ? 'Reduce' : 'Reject'}
                      </button>
                    ))}
                  </div>

                  {c.action === 'reduce' && (
                    <input
                      className="review-input" type="number" min="0" step="0.01" inputMode="decimal"
                      value={c.amount} onChange={(e) => setChoice(line.lineId, { amount: e.target.value })}
                      aria-label="Reduced amount"
                    />
                  )}
                  {c.action === 'reject' && (
                    <input
                      className="review-input" type="text" placeholder="Reason for rejection"
                      value={c.reason} onChange={(e) => setChoice(line.lineId, { reason: e.target.value })}
                      aria-label="Rejection reason"
                    />
                  )}
                </div>
              )
            })}
          </div>

          <button type="button" className="btn btn--primary btn--block" onClick={submitDecision} disabled={busy}>
            {busy ? 'Submitting…' : 'Submit decision'}
          </button>
        </>
      )}

      {claim.history.length > 0 && (
        <details className="history" open>
          <summary>History ({claim.history.length})</summary>
          <ul className="history-list">
            {claim.history.map((h, i) => (
              // eslint-disable-next-line react/no-array-index-key -- append-only, stable order
              <li key={i}>
                <span className="history-stage">{h.stage}</span>
                <span>{h.actorName}: {h.summary}</span>
              </li>
            ))}
          </ul>
        </details>
      )}
    </div>
  )
}
