import { useMemo, useRef, useState } from 'react'
import { CameraIcon, PlusIcon, UploadIcon } from '../../components/icons'
import { CLAIM_CATEGORIES, formatRs } from '../../types/domain'
import { ApiError, editClaim, submitClaim } from '../../api/client'
import type { ClaimDto, DependantDto, SubmitClaimLine, SubmitClaimRequest } from '../../types/api'

/**
 * Submit (or edit) a claim, posting straight to the API. A claim is one or more items; each item is
 * one expense for the employee or a dependant, in one category (docs/entitlement-rules.md
 * § Claim structure). Editing is only offered for claims the server marks editable.
 */
interface DraftItem {
  key: string
  beneficiary: string // 'Self' or a dependant id
  category: string
  amount: string
  receipt: string
}

const today = (): string => new Date().toISOString().slice(0, 10)
const uuid = (): string => crypto.randomUUID()

function blankItem(): DraftItem {
  return { key: uuid(), beneficiary: 'Self', category: 'Opd', amount: '', receipt: '' }
}

function itemsFromClaim(claim: ClaimDto): DraftItem[] {
  return claim.lines.map((l) => ({
    key: l.lineId,
    beneficiary: l.beneficiaryKind === 'Self' ? 'Self' : (l.dependantId ?? 'Self'),
    category: l.category,
    amount: String(l.claimedAmount),
    receipt: l.receiptReference ?? '',
  }))
}

export default function SubmitScreen({
  dependants,
  editing,
  onDone,
}: {
  dependants: DependantDto[]
  editing: ClaimDto | null
  onDone: () => void
}) {
  const [items, setItems] = useState<DraftItem[]>(() => (editing ? itemsFromClaim(editing) : [blankItem()]))
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const total = useMemo(
    () => items.reduce((sum, it) => sum + (Number.parseFloat(it.amount) || 0), 0),
    [items],
  )

  const patch = (key: string, next: Partial<DraftItem>) => {
    setItems((prev) => prev.map((it) => (it.key === key ? { ...it, ...next } : it)))
    setError(null)
  }
  const addItem = () => setItems((prev) => [...prev, blankItem()])
  const removeItem = (key: string) =>
    setItems((prev) => (prev.length > 1 ? prev.filter((it) => it.key !== key) : prev))

  function toLine(item: DraftItem, index: number): SubmitClaimLine | string {
    const amount = Number.parseFloat(item.amount)
    if (!Number.isFinite(amount) || amount <= 0) return `Item ${index + 1}: enter an amount greater than zero.`

    const base: SubmitClaimLine = {
      lineId: uuid(),
      beneficiaryKind: 'Self',
      category: item.category,
      expenseDate: today(),
      claimedAmount: amount,
      receiptReference: item.receipt || undefined,
    }
    if (item.beneficiary === 'Self') return base

    const dependant = dependants.find((d) => d.id === item.beneficiary)
    if (!dependant) return `Item ${index + 1}: choose a valid person.`
    return { ...base, beneficiaryKind: 'Dependant', dependantId: dependant.id, dependantRelationship: dependant.relationship }
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
            canRemove={items.length > 1}
            index={index}
            onChange={(next) => patch(item.key, next)}
            onRemove={() => removeItem(item.key)}
          />
        ))}
      </div>

      <button type="button" className="btn btn--outline btn--block add-item" onClick={addItem}>
        <PlusIcon size={18} /> Add another item
      </button>

      <div className="total-row">
        <span>Total claimed</span>
        <strong>{formatRs(total)}</strong>
      </div>

      <button type="button" className="btn btn--primary btn--block" onClick={submit} disabled={busy}>
        {busy ? 'Saving…' : editing ? 'Save changes' : 'Submit claim'}
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
  onRemove: () => void
}) {
  const { item, dependants, canRemove, index, onChange, onRemove } = props
  const fileInput = useRef<HTMLInputElement | null>(null)

  return (
    <fieldset className="item-card">
      {canRemove && (
        <div className="item-head">
          <span className="item-num">Item {index + 1}</span>
          <button type="button" className="btn btn--link" onClick={onRemove}>Remove</button>
        </div>
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
        <span>Receipt</span>
        <div className="receipt-btns">
          <button type="button" className="btn btn--outline" onClick={() => fileInput.current?.click()}>
            <CameraIcon size={18} /> Take photo
          </button>
          <button type="button" className="btn btn--outline" onClick={() => fileInput.current?.click()}>
            <UploadIcon size={18} /> Upload
          </button>
          <input
            ref={fileInput} type="file" accept="image/*" hidden
            onChange={(e) => onChange({ receipt: e.target.files?.[0]?.name ?? '' })}
          />
        </div>
        {item.receipt && <p className="receipt-name">Attached: {item.receipt}</p>}
      </div>
    </fieldset>
  )
}
