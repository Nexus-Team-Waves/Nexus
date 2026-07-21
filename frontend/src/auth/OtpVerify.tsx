import { useEffect, useMemo, useRef, useState } from 'react'
import BrandMark from '../components/BrandMark'

/**
 * Sign-in step 2: enter the 6-digit code. Six single-character boxes with auto-advance and
 * backspace-to-previous — the standard OTP interaction, which is far less fiddly on a phone than
 * one long field. A 5-minute countdown mirrors the code's validity (docs/CLAUDE.md §10).
 */
const CODE_LENGTH = 6
const EXPIRY_SECONDS = 5 * 60

export default function OtpVerify({
  email,
  onVerify,
  onResend,
  onBack,
}: {
  email: string
  onVerify: (code: string) => Promise<boolean>
  onResend: () => void
  onBack: () => void
}) {
  const [digits, setDigits] = useState<string[]>(() => Array(CODE_LENGTH).fill(''))
  const [error, setError] = useState<string | null>(null)
  const [secondsLeft, setSecondsLeft] = useState(EXPIRY_SECONDS)
  const inputs = useRef<Array<HTMLInputElement | null>>([])

  // Tick the expiry countdown down to zero.
  useEffect(() => {
    if (secondsLeft <= 0) return
    const timer = setInterval(() => setSecondsLeft((s) => Math.max(0, s - 1)), 1000)
    return () => clearInterval(timer)
  }, [secondsLeft])

  const code = digits.join('')
  const complete = code.length === CODE_LENGTH && /^\d{6}$/.test(code)
  const mmss = useMemo(() => {
    const m = Math.floor(secondsLeft / 60)
    const s = secondsLeft % 60
    return `${m}:${s.toString().padStart(2, '0')}`
  }, [secondsLeft])

  function setDigit(index: number, value: string) {
    const char = value.replace(/\D/g, '').slice(-1) // keep only the last typed digit
    setDigits((prev) => {
      const next = [...prev]
      next[index] = char
      return next
    })
    setError(null)
    if (char && index < CODE_LENGTH - 1) inputs.current[index + 1]?.focus()
  }

  function handleKeyDown(index: number, e: React.KeyboardEvent<HTMLInputElement>) {
    // Backspace on an empty box jumps to the previous one, so deleting feels natural.
    if (e.key === 'Backspace' && !digits[index] && index > 0) {
      inputs.current[index - 1]?.focus()
    }
  }

  const [busy, setBusy] = useState(false)
  async function submit() {
    if (!complete || busy) return
    setBusy(true)
    try {
      const ok = await onVerify(code)
      if (!ok) setError('That code did not work. Please try again.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="auth">
      <BrandMark />

      <div className="auth-body">
        <h1 className="auth-title">Enter the code</h1>
        <p className="auth-sub">
          Sent to {email}. Expires in {mmss}.
        </p>

        <div className="otp" role="group" aria-label="6-digit code">
          {digits.map((digit, i) => (
            <input
              // eslint-disable-next-line react/no-array-index-key -- fixed-length positional inputs
              key={i}
              ref={(el) => {
                inputs.current[i] = el
              }}
              className="otp-box"
              type="text"
              inputMode="numeric"
              autoComplete={i === 0 ? 'one-time-code' : 'off'}
              maxLength={1}
              value={digit}
              onChange={(e) => setDigit(i, e.target.value)}
              onKeyDown={(e) => handleKeyDown(i, e)}
              aria-label={`Digit ${i + 1}`}
            />
          ))}
        </div>

        {error && (
          <p className="banner banner--error" role="alert">
            {error}
          </p>
        )}
        <p className="hint">Demo: enter any 6 digits.</p>

        <button
          type="button"
          className="btn btn--primary btn--block"
          disabled={!complete || busy}
          onClick={submit}
        >
          {busy ? 'Signing in…' : 'Verify and sign in'}
        </button>
        <button
          type="button"
          className="btn btn--outline btn--block"
          onClick={() => {
            setDigits(Array(CODE_LENGTH).fill(''))
            setSecondsLeft(EXPIRY_SECONDS)
            setError(null)
            onResend()
            inputs.current[0]?.focus()
          }}
        >
          Resend code
        </button>

        <button type="button" className="btn btn--link auth-back" onClick={onBack}>
          Use a different email
        </button>
      </div>
    </div>
  )
}
