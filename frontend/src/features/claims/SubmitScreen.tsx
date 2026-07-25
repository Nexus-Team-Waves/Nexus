import { useMemo, useRef, useState } from 'react'
import { CameraIcon, PlusIcon, UploadIcon } from '../../components/icons'
import { CLAIM_CATEGORIES, formatRs } from '../../types/domain'
import { ApiError, editClaim, submitClaim, uploadReceipt } from '../../api/client'
import { uuid } from '../../utils/uuid'
import type { ClaimDto, DependantDto, SubmitClaimLine, SubmitClaimRequest } from '../../types/api'

/**
 * Submit (or edit) a claim, posting straight to the API. A claim is one or more items; each item is
 * one expense for the employee or a dependant, in one category (docs/entitlement-rules.md
 * § Claim structure). Editing is only offered for claims the server marks editable.
 */
/** One uploaded receipt image attached to a draft item. */
interface DraftReceipt {
  id: string
  name: string
}

interface DraftItem {
  key: string // the server's lineId when editing — MUST round-trip so the server can match lines
  beneficiary: string // 'Self' or a dependant id
  category: string
  amount: string
  expenseDate: string // the BILL date the user enters (not the submission date)
  receipts: DraftReceipt[] // uploaded images, 1–5 per item
  uploadingCount: number // images currently in flight — submit is blocked while > 0
  comment: string // optional note for the approvers on this line
  /**
   * Line already Approved/Reduced by the chain (claim was returned because ANOTHER line was
   * rejected). Locked items are shown read-only and sent back unchanged — the server rejects
   * any modification to them.
   */
  locked: boolean
  decidedAmount: number | null // the approved amount, for the locked read-only display
  rejectionReason: string | null // why this line was rejected, shown while re-editing it
}

/** Keep in sync with ReceiptService.MaxSizeBytes and the validator's 5-image cap on the server. */
const MAX_RECEIPT_BYTES = 5 * 1024 * 1024
const MAX_IMAGES_PER_ITEM = 5

const today = (): string => new Date().toISOString().slice(0, 10)

function blankItem(): DraftItem {
  return {
    key: uuid(), beneficiary: 'Self', category: 'Opd', amount: '', expenseDate: today(),
    receipts: [], uploadingCount: 0, comment: '',
    locked: false, decidedAmount: null, rejectionReason: null,
  }
}

function itemsFromClaim(claim: ClaimDto): DraftItem[] {
  return claim.lines.map((l) => {
    // receiptReference stores the joined original filenames in id order; fall back to a
    // generic label if the counts ever disagree.
    const names = (l.receiptReference ?? '').split(', ').filter(Boolean)
    return {
      key: l.lineId,
      beneficiary: l.beneficiaryKind === 'Self' ? 'Self' : (l.dependantId ?? 'Self'),
      category: l.category,
      amount: String(l.claimedAmount),
      expenseDate: l.expenseDate,
      receipts: l.receiptIds.map((id, i) => ({ id, name: names[i] ?? `Image ${i + 1}` })),
      uploadingCount: 0,
      comment: '',
      locked: l.locked,
      decidedAmount: l.approvedAmount,
      rejectionReason: l.status === 'Rejected' ? l.rejectionReason : null,
    }
  })
}

export default function SubmitScreen({
  dependants,
  claims,
  editing,
  remaining,
  onDone,
}: {
  dependants: DependantDto[]
  /** The employee's existing claims — used to warn about a possibly duplicated bill. */
  claims: ClaimDto[]
  editing: ClaimDto | null
  /** Remaining entitlement this year, shown in the summary panel. */
  remaining?: number
  onDone: () => void
}) {
  const [items, setItems] = useState<DraftItem[]>(() => (editing ? itemsFromClaim(editing) : [blankItem()]))
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // A returned claim resubmits only its rejected items: approved items are locked, and lines
  // cannot be added or removed (the server enforces both).
  const returnedEdit = editing?.stage === 'ReturnedToEmployee'

  // Images still uploading mean the line's receipt list is incomplete — hold submit until done.
  const anyUploading = items.some((it) => it.uploadingCount > 0)

  const total = useMemo(
    () => items.reduce((sum, it) => sum + (Number.parseFloat(it.amount) || 0), 0),
    [items],
  )

  // `next` may be a plain patch or an updater over the CURRENT item — the updater form is what
  // the concurrent image uploads need (each completion appends to the latest receipts list).
  const patch = (key: string, next: Partial<DraftItem> | ((it: DraftItem) => Partial<DraftItem>)) => {
    setItems((prev) => prev.map((it) =>
      it.key === key ? { ...it, ...(typeof next === 'function' ? next(it) : next) } : it))
    setError(null)
  }
  const addItem = () => setItems((prev) => [...prev, blankItem()])
  const removeItem = (key: string) =>
    setItems((prev) => (prev.length > 1 ? prev.filter((it) => it.key !== key) : prev))

  function toLine(item: DraftItem, index: number): SubmitClaimLine | string {
    const amount = Number.parseFloat(item.amount)
    // Locked items skip validation: they are sent back exactly as stored (the server compares
    // them field-by-field), and pre-image-upload locked lines may legitimately have no images.
    if (!item.locked) {
      if (!Number.isFinite(amount) || amount <= 0) return `Item ${index + 1}: enter an amount greater than zero.`
      if (!item.expenseDate) return `Item ${index + 1}: enter the date of the bill.`
      if (item.expenseDate > today()) return `Item ${index + 1}: the bill date cannot be in the future.`
      if (item.receipts.length === 0) return `Item ${index + 1}: attach at least one receipt image.`
    }

    const base: SubmitClaimLine = {
      // The server matches resubmitted lines to stored lines by this id — round-trip it.
      lineId: item.key,
      beneficiaryKind: 'Self',
      category: item.category,
      expenseDate: item.expenseDate || today(),
      claimedAmount: amount,
      receiptReference: item.receipts.map((r) => r.name).join(', ') || undefined,
      receiptIds: item.receipts.map((r) => r.id),
      comment: item.comment.trim() || undefined,
    }
    if (item.beneficiary === 'Self') return base

    const dependant = dependants.find((d) => d.id === item.beneficiary)
    if (dependant) return { ...base, beneficiaryKind: 'Dependant', dependantId: dependant.id, dependantRelationship: dependant.relationship }
    if (item.locked)
      // A locked dependant line whose dependant is no longer in the coverage list must still
      // round-trip untouched — rebuild it from the stored values.
      return { ...base, beneficiaryKind: 'Dependant', dependantId: item.beneficiary }
    return `Item ${index + 1}: choose a valid person.`
  }

  /**
   * Duplicate-bill guard (decision 2026-07-23): a bill with the same date + amount + category
   * as a line on any OTHER claim of this employee is probably being entered twice. Warn and
   * let the user decide — never block (two identical bills can be legitimate).
   */
  function duplicateWarnings(): string[] {
    const warnings: string[] = []
    items.forEach((item, index) => {
      if (item.locked) return
      const amount = Number.parseFloat(item.amount)
      for (const claim of claims) {
        if (claim.id === editing?.id) continue // don't match a claim against itself while editing
        for (const line of claim.lines) {
          if (line.expenseDate === item.expenseDate
            && line.claimedAmount === amount
            && line.category === item.category) {
            warnings.push(
              `Item ${index + 1}: a ${item.category} bill dated ${item.expenseDate} for ${formatRs(amount)} `
              + `already exists on your claim submitted ${claim.submissionDate}.`)
          }
        }
      }
    })
    return warnings
  }

  async function submit() {
    setError(null)
    const lines: SubmitClaimLine[] = []
    for (let i = 0; i < items.length; i++) {
      const result = toLine(items[i]!, i)
      if (typeof result === 'string') { setError(result); return }
      lines.push(result)
    }

    const duplicates = duplicateWarnings()
    if (duplicates.length > 0
      && !window.confirm(`Possible duplicate bill(s):\n\n${duplicates.join('\n')}\n\nSubmit anyway?`)) {
      return
    }

    // employeeId is set from the auth token server-side; the value here is a placeholder.
    const payload: SubmitClaimRequest = {
      claimId: editing?.id ?? uuid(),
      employeeId: '',
      submissionDate: today(),
      lines,
    }

    setBusy(true)
    try {
      if (editing) await editClaim(editing.id, payload)
      else await submitClaim(payload)
      onDone()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Could not save the claim. Please try again.')
    } finally {
      setBusy(false)
    }
  }

  const editableCount = items.filter((it) => !it.locked).length

  return (
    <div className="screen-pad">
      <h1 className="screen-title">{editing ? 'Edit claim' : 'Submit a claim'}</h1>

      {error && (
        <p className="banner banner--error" role="alert">
          {error}
        </p>
      )}

      {/* Two panes on desktop (items | sticky summary); one column on mobile. */}
      <div className="submit-layout">
        <div className="submit-main">
          <div className="items">
            {items.map((item, index) => (
              <ItemCard
                key={item.key}
                item={item}
                dependants={dependants}
                canRemove={items.length > 1 && !returnedEdit}
                index={index}
                onChange={(next) => patch(item.key, next)}
                onUploadError={(message) => setError(message)}
                onRemove={() => removeItem(item.key)}
              />
            ))}
          </div>

          {!returnedEdit && (
            <button type="button" className="btn btn--outline btn--block add-item" onClick={addItem}>
              <PlusIcon size={18} /> Add another item
            </button>
          )}
        </div>

        <aside className="submit-side">
          <div className="summary-card">
            <h2 className="summary-title">Summary</h2>
            <dl className="summary-rows">
              <div className="summary-row">
                <dt>Items</dt>
                <dd>{items.length}{returnedEdit ? ` (${editableCount} editable)` : ''}</dd>
              </div>
              <div className="summary-row">
                <dt>Total claimed</dt>
                <dd className="summary-total">{formatRs(total)}</dd>
              </div>
              {remaining != null && (
                <div className="summary-row">
                  <dt>Entitlement left</dt>
                  <dd>{formatRs(remaining)}</dd>
                </div>
              )}
            </dl>

            {remaining != null && total > remaining && (
              <p className="banner banner--info summary-note">
                This claim exceeds your remaining entitlement — the excess may be treated as an
                advance or reduced during approval.
              </p>
            )}

            <button type="button" className="btn btn--primary btn--block" onClick={submit} disabled={busy || anyUploading}>
              {busy ? 'Saving…' : anyUploading ? 'Uploading receipt…' : editing ? 'Save changes' : 'Submit claim'}
            </button>
            {editing && (
              <button type="button" className="btn btn--link btn--block" onClick={onDone}>
                Cancel
              </button>
            )}
            <p className="summary-hint">
              Each item needs its bill date and 1–{MAX_IMAGES_PER_ITEM} receipt images.
            </p>
          </div>
        </aside>
      </div>
    </div>
  )
}

function ItemCard(props: {
  item: DraftItem
  dependants: DependantDto[]
  canRemove: boolean
  index: number
  onChange: (next: Partial<DraftItem> | ((it: DraftItem) => Partial<DraftItem>)) => void
  onUploadError: (message: string) => void
  onRemove: () => void
}) {
  const { item, dependants, canRemove, index, onChange, onUploadError, onRemove } = props
  const fileInput = useRef<HTMLInputElement | null>(null)

  // A locked item was already approved by the chain — show it read-only. The server rejects
  // any change to it, so there is nothing useful to let the user touch.
  if (item.locked) {
    const label = item.beneficiary === 'Self'
      ? 'Self'
      : (dependants.find((d) => d.id === item.beneficiary)?.label ?? 'Dependant')
    return (
      <fieldset className="item-card item-card--locked">
        <div className="item-head">
          <span className="item-num">Item {index + 1}</span>
          <span className="receipt-name">Approved — locked</span>
        </div>
        <p className="claim-card-meta">
          {label} · {item.category} · {item.expenseDate} · claimed {formatRs(Number.parseFloat(item.amount) || 0)}
          {item.decidedAmount != null ? ` · approved ${formatRs(item.decidedAmount)}` : ''}
          {item.receipts.length > 0 ? ` · ${item.receipts.length} receipt image(s)` : ''}
        </p>
      </fieldset>
    )
  }

  /**
   * Upload the chosen images right away (not at submit time): the server stores the bytes and
   * returns receipt ids, which are what the claim line actually carries. Uploads run one after
   * another; each completion appends via the updater form of onChange so no result is lost.
   * Requires connectivity — like submit itself today.
   */
  async function handleFilesSelected(e: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(e.target.files ?? [])
    e.target.value = '' // reset so picking the same files again re-triggers this handler
    if (files.length === 0) return

    const room = MAX_IMAGES_PER_ITEM - item.receipts.length - item.uploadingCount
    if (files.length > room) {
      onUploadError(`Item ${index + 1}: at most ${MAX_IMAGES_PER_ITEM} receipt images per item (${room > 0 ? `${room} more allowed` : 'limit reached'}).`)
      return
    }
    if (files.some((f) => f.size > MAX_RECEIPT_BYTES)) {
      onUploadError(`Item ${index + 1}: each receipt image must be 5 MB or smaller.`)
      return
    }

    onChange((it) => ({ uploadingCount: it.uploadingCount + files.length }))
    for (const file of files) {
      try {
        const result = await uploadReceipt(file)
        onChange((it) => ({
          receipts: [...it.receipts, { id: result.receiptId, name: result.fileName }],
          uploadingCount: it.uploadingCount - 1,
        }))
      } catch (err) {
        onChange((it) => ({ uploadingCount: it.uploadingCount - 1 }))
        onUploadError(err instanceof ApiError
          ? `Item ${index + 1}: ${err.message}`
          : `Item ${index + 1}: could not upload a receipt. Check your connection and try again.`)
      }
    }
  }

  return (
    <fieldset className="item-card">
      {canRemove && (
        <div className="item-head">
          <span className="item-num">Item {index + 1}</span>
          <button type="button" className="btn btn--link" onClick={onRemove}>Remove</button>
        </div>
      )}

      {item.rejectionReason && (
        <p className="banner banner--error">
          Rejected: {item.rejectionReason} — fix this item and resubmit.
        </p>
      )}

      {/* Field pairs sit side-by-side when the card is wide enough (desktop). */}
      <div className="field-row">
        <label className="field">
          <span>For</span>
          <select value={item.beneficiary} onChange={(e) => onChange({ beneficiary: e.target.value })}>
            <option value="Self">Self</option>
            {dependants.map((d) => (
              <option key={d.id} value={d.id}>{d.label}</option>
            ))}
          </select>
        </label>

        <label className="field">
          <span>Category</span>
          <select value={item.category} onChange={(e) => onChange({ category: e.target.value })}>
            {CLAIM_CATEGORIES.map((c) => (
              <option key={c.value} value={c.value}>{c.label}</option>
            ))}
          </select>
        </label>
      </div>

      <div className="field-row">
        <label className="field">
          <span>Claimed amount (PKR)</span>
          <input
            type="number" min="0" step="0.01" inputMode="decimal" placeholder="0"
            value={item.amount}
            onChange={(e) => onChange({ amount: e.target.value })}
          />
        </label>

        <label className="field">
          {/* The BILL's date — distinct from the claim's submission date. */}
          <span>Date of bill (required)</span>
          <input
            type="date" required max={today()}
            value={item.expenseDate}
            onChange={(e) => onChange({ expenseDate: e.target.value })}
          />
        </label>
      </div>

      <div className="field">
        <span>Receipts (1–{MAX_IMAGES_PER_ITEM} images, required)</span>
        <div className="receipt-btns">
          <button type="button" className="btn btn--outline" onClick={() => fileInput.current?.click()}>
            <CameraIcon size={18} /> Take photo
          </button>
          <button type="button" className="btn btn--outline" onClick={() => fileInput.current?.click()}>
            <UploadIcon size={18} /> Upload
          </button>
          <input
            // JPEG/PNG only: they are what the server-side PDF renderer (PdfSharp) can embed.
            ref={fileInput} type="file" accept="image/jpeg,image/png" multiple hidden
            onChange={handleFilesSelected}
          />
        </div>
        {item.receipts.length > 0 && (
          <div className="receipt-chips">
            {item.receipts.map((r) => (
              <span key={r.id} className="receipt-chip">
                {r.name}
                <button
                  type="button" className="receipt-chip-remove" aria-label={`Remove ${r.name}`}
                  onClick={() => onChange((it) => ({ receipts: it.receipts.filter((x) => x.id !== r.id) }))}
                >
                  ×
                </button>
              </span>
            ))}
          </div>
        )}
        {item.uploadingCount > 0 && (
          <p className="receipt-name">Uploading {item.uploadingCount} image(s)…</p>
        )}
      </div>

      <label className="field">
        <span>Note for approvers (optional)</span>
        <input
          type="text" maxLength={500} placeholder="e.g. follow-up visit for the same illness"
          value={item.comment}
          onChange={(e) => onChange({ comment: e.target.value })}
        />
      </label>
    </fieldset>
  )
}
