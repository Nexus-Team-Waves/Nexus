import { useEffect, useState } from 'react'
import { formatRs, shortCategory } from '../../types/domain'
import { ApiError, decideClaim, getLineReceiptPdf, getReceiptImage, postClaim } from '../../api/client'
import LineTimeline from '../../components/LineTimeline'
import type { ClaimDto, ClaimLineDto, DecisionAction, LineDecision, Role } from '../../types/api'

/**
 * Review one claim. In an approval stage, the approver decides every PENDING line (approve /
 * reduce / reject-with-reason) and submits them together — the server then advances the claim or
 * returns it (docs/entitlement-rules.md § Approval flow). Lines locked by an earlier round
 * (approved before a sibling was rejected and resubmitted) are shown read-only and never
 * re-decided. For a completed claim, Finance records the manual SAP posting reference instead.
 */
interface LineChoice {
  action: DecisionAction
  amount: string // for 'reduce'
  reason: string // for 'reject'
  comment: string // optional note, visible to the employee and later stages
}

export default function ClaimReviewScreen({
  claim,
  role,
  onBack,
  onUpdated,
}: {
  claim: ClaimDto
  role: Role
  onBack: () => void
  onUpdated: (updated: ClaimDto) => void
}) {
  const posting = claim.stage === 'Completed' && !claim.posted

  // Stage capability: the Line Manager approves in full or rejects — never reduces
  // (docs/entitlement-rules.md § Approval flow). The server enforces this too; hiding the
  // button just keeps the UI honest about what this role can do.
  const actions: DecisionAction[] =
    role === 'LineManager' ? ['approve', 'reject'] : ['approve', 'reduce', 'reject']

  // The Top-Level Approver ADJUSTS rather than reduces: any amount up to the ORIGINAL claim,
  // including increasing back above an earlier reduction (decision 2026-07-23).
  const isTopLevel = role === 'TopLevel'

  // Only pending lines get a decision; locked lines were decided in an earlier round.
  const pendingLines = claim.lines.filter((l) => l.status === 'Pending')

  const [choices, setChoices] = useState<Record<string, LineChoice>>(() =>
    Object.fromEntries(pendingLines.map((l) => [l.lineId, { action: 'approve', amount: String(l.currentAmount), reason: '', comment: '' }])),
  )
  // Admin/HR only: forward the claim directly to the Top-Level Approver, skipping Finance
  // review (Finance still records the SAP posting at the end).
  const [forwardToTopLevel, setForwardToTopLevel] = useState(false)
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
    for (const line of pendingLines) {
      const c = choices[line.lineId]!
      const comment = c.comment.trim() || undefined
      if (c.action === 'reduce') {
        const amount = Number.parseFloat(c.amount)
        // ED may adjust up to the original claim (inclusive); other stages strictly below the
        // carried amount — mirrors the server's rule.
        const invalid = isTopLevel
          ? !Number.isFinite(amount) || amount <= 0 || amount > line.claimedAmount
          : !Number.isFinite(amount) || amount <= 0 || amount >= line.currentAmount
        if (invalid) {
          setError(isTopLevel
            ? `${shortCategory(line.category)}: the adjusted amount must be between 0 and the claimed ${formatRs(line.claimedAmount)}.`
            : `${shortCategory(line.category)}: a reduced amount must be between 0 and ${formatRs(line.currentAmount)}.`)
          return
        }
        // Changing an amount always needs a stated reason (server enforces this too).
        if (!comment) {
          setError(`${shortCategory(line.category)}: a comment is required when changing the amount.`)
          return
        }
        // Adjusting to exactly the carried amount is just an approval — send it as one so the
        // audit trail reads naturally.
        if (isTopLevel && amount === line.currentAmount) {
          decisions.push({ lineId: line.lineId, action: 'approve', comment })
        } else {
          decisions.push({ lineId: line.lineId, action: 'reduce', amount, comment })
        }
      } else if (c.action === 'reject') {
        if (!c.reason.trim()) { setError(`${shortCategory(line.category)}: a rejection needs a reason.`); return }
        decisions.push({ lineId: line.lineId, action: 'reject', reason: c.reason.trim(), comment })
      } else {
        decisions.push({ lineId: line.lineId, action: 'approve', comment })
      }
    }

    setBusy(true)
    try {
      const updated = await decideClaim(claim.id, decisions, role === 'AdminHr' && forwardToTopLevel)
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
            {claim.lines.map((l, index) => (
              <div key={l.lineId} className="review-line">
                <div className="review-line-head">
                  <strong>{shortCategory(l.category)}</strong>
                  <span>{formatRs(l.approvedAmount ?? 0)}</span>
                </div>
                <p className="claim-card-meta">{l.beneficiaryKind === 'Self' ? 'Self' : 'Dependant'}</p>
                <ReceiptRow claimId={claim.id} line={l} itemNumber={index + 1} />
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
            {claim.lines.map((line, index) => {
              // Locked = approved/reduced in an earlier round; never re-decided (2026-07-23).
              if (line.locked) {
                return (
                  <div key={line.lineId} className="review-line review-line--locked">
                    <div className="review-line-head">
                      <strong>{shortCategory(line.category)}</strong>
                      <span>{formatRs(line.currentAmount)}</span>
                    </div>
                    <p className="claim-card-meta">
                      {line.beneficiaryKind === 'Self' ? 'Self' : 'Dependant'} · decided in an
                      earlier round — {line.status === 'Reduced' ? 'reduced to' : 'approved at'} {formatRs(line.currentAmount)}
                    </p>
                    <ReceiptRow claimId={claim.id} line={line} itemNumber={index + 1} />
                    <LineTimeline events={line.events} />
                  </div>
                )
              }

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

                  <ReceiptRow claimId={claim.id} line={line} itemNumber={index + 1} />
                  <LineTimeline events={line.events} />

                  <div className="seg">
                    {actions.map((a) => (
                      <button
                        key={a}
                        type="button"
                        className={`seg-btn ${c.action === a ? 'seg-btn--on seg-btn--' + a : ''}`}
                        onClick={() => setChoice(line.lineId, { action: a })}
                      >
                        {a === 'approve' ? 'Approve' : a === 'reduce' ? (isTopLevel ? 'Adjust' : 'Reduce') : 'Reject'}
                      </button>
                    ))}
                  </div>

                  {c.action === 'reduce' && (
                    <>
                      <input
                        className="review-input" type="number" min="0" step="0.01" inputMode="decimal"
                        value={c.amount} onChange={(e) => setChoice(line.lineId, { amount: e.target.value })}
                        aria-label={isTopLevel ? 'Adjusted amount' : 'Reduced amount'}
                      />
                      {isTopLevel && (
                        <p className="claim-card-meta">
                          Any amount up to the claimed {formatRs(line.claimedAmount)} — you may also
                          restore an earlier reduction.
                        </p>
                      )}
                    </>
                  )}
                  {c.action === 'reject' && (
                    <input
                      className="review-input" type="text" placeholder="Reason for rejection"
                      value={c.reason} onChange={(e) => setChoice(line.lineId, { reason: e.target.value })}
                      aria-label="Rejection reason"
                    />
                  )}
                  <input
                    className="review-input" type="text" maxLength={500}
                    required={c.action === 'reduce'}
                    placeholder={c.action === 'reduce'
                      ? 'Comment (required when changing the amount)'
                      : 'Comment (optional — visible to the employee and later stages)'}
                    value={c.comment} onChange={(e) => setChoice(line.lineId, { comment: e.target.value })}
                    aria-label="Line comment"
                  />
                </div>
              )
            })}
          </div>

          {role === 'AdminHr' && (
            <label className="escalate-row">
              <input
                type="checkbox"
                checked={forwardToTopLevel}
                onChange={(e) => setForwardToTopLevel(e.target.checked)}
              />
              <span>
                Forward directly to the Top-Level Approver (skips Finance review; Finance still
                records the SAP posting)
              </span>
            </label>
          )}

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

/**
 * One line's receipt controls for approvers: toggle previews of ALL the line's images and
 * download the per-line PDF (one PDF with every image). Images are fetched as Blobs because a
 * plain <img src="/api/..."> cannot carry the Authorization header the API requires.
 */
function ReceiptRow({ claimId, line, itemNumber }: { claimId: string; line: ClaimLineDto; itemNumber: number }) {
  const [imageUrls, setImageUrls] = useState<string[] | null>(null)
  const [busy, setBusy] = useState(false)
  const [note, setNote] = useState<string | null>(null)

  // Object URLs hold browser memory until revoked; release the old ones whenever they are
  // replaced or the component unmounts.
  useEffect(() => {
    return () => { if (imageUrls) for (const url of imageUrls) URL.revokeObjectURL(url) }
  }, [imageUrls])

  async function toggleImages() {
    if (imageUrls) { setImageUrls(null); return } // hide (cleanup above revokes the URLs)
    if (line.receiptIds.length === 0) return
    setBusy(true)
    setNote(null)
    try {
      const blobs = await Promise.all(line.receiptIds.map((id) => getReceiptImage(id)))
      setImageUrls(blobs.map((b) => URL.createObjectURL(b)))
    } catch (e) {
      setNote(e instanceof ApiError ? e.message : 'Could not load the receipt images.')
    } finally {
      setBusy(false)
    }
  }

  async function downloadPdf() {
    setBusy(true)
    setNote(null)
    try {
      const blob = await getLineReceiptPdf(claimId, line.lineId)
      // Standard "download a Blob" dance: temporary object URL on a temporary <a download>.
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = `claim-${claimId.replaceAll('-', '').slice(0, 8)}-item-${itemNumber}-receipt.pdf`
      document.body.appendChild(anchor)
      anchor.click()
      anchor.remove()
      URL.revokeObjectURL(url)
    } catch (e) {
      setNote(e instanceof ApiError ? e.message : 'Could not download the receipt PDF.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="receipt-row">
      <div className="receipt-actions">
        {line.receiptIds.length > 0 ? (
          <button type="button" className="btn btn--link" onClick={toggleImages} disabled={busy}>
            {imageUrls
              ? 'Hide receipts'
              : `View receipts (${line.receiptIds.length})`}
          </button>
        ) : (
          <span className="claim-card-meta">Receipt image not stored (legacy claim)</span>
        )}
        <button type="button" className="btn btn--link" onClick={downloadPdf} disabled={busy}>
          Download PDF
        </button>
      </div>
      {note && <p className="claim-card-meta" role="alert">{note}</p>}
      {imageUrls?.map((url, i) => (
        // eslint-disable-next-line react/no-array-index-key -- stable order per fetch
        <img key={i} className="receipt-preview" src={url} alt={`Receipt ${i + 1} of item ${itemNumber}`} />
      ))}
    </div>
  )
}
