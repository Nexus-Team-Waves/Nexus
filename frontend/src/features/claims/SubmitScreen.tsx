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
  onDone,
}: {
  dependants: DependantDto[]
  /** The employee's existing claims — used to warn about a possibly duplicated bill. */
  claims: ClaimDto[]
  editing: ClaimDto | null
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

  async function submit() {
    setError(null)
    const lines: SubmitClaimLine[] = []
    for (let i = 0; i < items.length; i++) {
      const result = toLine(items[i]!, i)
      if (typeof result === 'string') { setError(result); return }
      lines.push(result)
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

  return (
    <div className="screen-pad">
      <h1 className="screen-title">{editing ? 'Edit claim' : 'Submit a claim'}</h1>

      {error && (
        <p className="banner banner--error" role="alert">
          {error}
        </p>
      )}

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

      <div className="total-row">
        <span>Total claimed</span>
        <strong>{formatRs(total)}</strong>
      </div>

      <button type="button" className="btn btn--primary btn--block" onClick={submit} disabled={busy || anyUploading}>
        {busy ? 'Saving…' : anyUploading ? 'Uploading receipt…' : editing ? 'Save changes' : 'Submit claim'}
      </button>
      {editing && (
        <button type="button" className="btn btn--link btn--block" onClick={onDone}>
          Cancel
        </button>
      )}
    </div>
  )
}

function ItemCard(props: {
  item: DraftItem
  dependants: DependantDto[]
  canRemove: boolean
  index: number
  onChange: (next: Partial<DraftItem>) => void
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
          {label} · {item.category} · claimed {formatRs(Number.parseFloat(item.amount) || 0)}
          {item.decidedAmount != null ? ` · approved ${formatRs(item.decidedAmount)}` : ''}
        </p>
      </fieldset>
    )
  }

  /**
   * Upload the chosen image right away (not at submit time): the server stores the bytes and
   * returns a receiptId, which is what the claim line actually carries. Requires connectivity —
   * like submit itself today. (A future offline sync engine must also queue the image blob.)
   */
  async function handleFileSelected(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = '' // reset so picking the same file again re-triggers this handler
    if (!file) return

    if (file.size > MAX_RECEIPT_BYTES) {
      onUploadError(`Item ${index + 1}: the receipt image must be 5 MB or smaller.`)
      return
    }

    onChange({ receipt: file.name, receiptId: '', uploading: true })
    try {
      const result = await uploadReceipt(file)
      onChange({ receipt: result.fileName, receiptId: result.receiptId, uploading: false })
    } catch (err) {
      onChange({ receipt: '', receiptId: '', uploading: false })
      onUploadError(err instanceof ApiError
        ? `Item ${index + 1}: ${err.message}`
        : `Item ${index + 1}: could not upload the receipt. Check your connection and try again.`)
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

      <label className="field">
        <span>Claimed amount (PKR)</span>
        <input
          type="number" min="0" step="0.01" inputMode="decimal" placeholder="0"
          value={item.amount}
          onChange={(e) => onChange({ amount: e.target.value })}
        />
      </label>

      <div className="field">
        <span>Receipt (required)</span>
        <div className="receipt-btns">
          <button type="button" className="btn btn--outline" onClick={() => fileInput.current?.click()}>
            <CameraIcon size={18} /> Take photo
          </button>
          <button type="button" className="btn btn--outline" onClick={() => fileInput.current?.click()}>
            <UploadIcon size={18} /> Upload
          </button>
          <input
            // JPEG/PNG only: they are what the server-side PDF renderer (PdfSharp) can embed.
            ref={fileInput} type="file" accept="image/jpeg,image/png" hidden
            onChange={handleFileSelected}
          />
        </div>
        {item.uploading && <p className="receipt-name">Uploading {item.receipt}…</p>}
        {!item.uploading && item.receiptId && <p className="receipt-name">Attached: {item.receipt}</p>}
        {!item.uploading && !item.receiptId && item.receipt && (
          <p className="receipt-name">Re-attach: {item.receipt}</p>
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
