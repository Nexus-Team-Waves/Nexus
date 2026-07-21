import { useState } from 'react'
import BrandMark from '../components/BrandMark'

/**
 * Sign-in step 1: enter the official work email; we "send" a code and move to code entry. The demo
 * accounts below let a presenter jump straight into any role without typing (each maps to a seeded
 * user on the server).
 */
const DEMO_ACCOUNTS: ReadonlyArray<{ label: string; email: string }> = [
  { label: 'Employee', email: 'ayesha@waves.com.pk' },
  { label: 'Line Manager', email: 'manager@waves.com.pk' },
  { label: 'Admin/HR', email: 'hr@waves.com.pk' },
  { label: 'Finance', email: 'finance@waves.com.pk' },
  { label: 'Director', email: 'ed@waves.com.pk' },
]

export default function SignIn({
  onRequestCode,
  error,
}: {
  onRequestCode: (email: string) => Promise<void>
  error: string | null
}) {
  const [email, setEmail] = useState('')
  const [busy, setBusy] = useState(false)
  const trimmed = email.trim()
  const looksValid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmed)

  async function send(value: string) {
    setBusy(true)
    try {
      await onRequestCode(value)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="auth">
      <BrandMark />

      <div className="auth-body">
        <h1 className="auth-title">Sign in</h1>
        <p className="auth-sub">Use your official work email.</p>

        {error && (
          <p className="banner banner--error" role="alert">
            {error}
          </p>
        )}

        <form
          onSubmit={(e) => {
            e.preventDefault()
            if (looksValid && !busy) void send(trimmed)
          }}
        >
          <label className="field">
            <span>Email</span>
            <input
              type="email"
              inputMode="email"
              autoComplete="email"
              placeholder="name@waves.com.pk"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </label>

          <button type="submit" className="btn btn--primary btn--block" disabled={!looksValid || busy}>
            {busy ? 'Sending…' : 'Send code'}
          </button>
        </form>

        <p className="demo-hint">Demo accounts — tap to sign in as:</p>
        <div className="demo-chips">
          {DEMO_ACCOUNTS.map((a) => (
            <button
              key={a.email}
              type="button"
              className="demo-account"
              disabled={busy}
              onClick={() => { setEmail(a.email); void send(a.email) }}
            >
              {a.label}
            </button>
          ))}
        </div>
      </div>
    </div>
  )
}
