import { useCallback, useEffect, useState } from 'react'
import { ApiError, getMe, getToken, requestCode as apiRequestCode, setToken, verifyCode } from '../api/client'
import type { ApiUser } from '../types/api'

/**
 * Email-OTP auth backed by the real API. On load, an existing token is validated via /api/me so a
 * refresh keeps the session. The OTP itself is a mock server-side (any 6-digit code) until the real
 * email provider is wired (docs/CLAUDE.md §10) — this hook doesn't care either way.
 */
export type AuthStatus = 'signed-out' | 'awaiting-code' | 'signed-in'

function message(e: unknown): string {
  return e instanceof ApiError ? e.message : 'Something went wrong. Please try again.'
}

export interface Auth {
  status: AuthStatus
  /** True while we validate a stored token on first load. */
  loading: boolean
  user: ApiUser | null
  email: string | null
  error: string | null
  requestCode: (email: string) => Promise<void>
  verify: (code: string) => Promise<boolean>
  resend: () => Promise<void>
  signOut: () => void
}

export function useAuth(): Auth {
  const [user, setUser] = useState<ApiUser | null>(null)
  const [pendingEmail, setPendingEmail] = useState<string | null>(null)
  const [loading, setLoading] = useState<boolean>(() => Boolean(getToken()))
  const [error, setError] = useState<string | null>(null)

  // Restore a session from a stored token.
  useEffect(() => {
    if (!getToken()) return
    let cancelled = false
    getMe()
      .then((u) => { if (!cancelled) { setUser(u); setLoading(false) } })
      .catch(() => { if (!cancelled) { setToken(null); setLoading(false) } })
    return () => { cancelled = true }
  }, [])

  const requestCode = useCallback(async (email: string) => {
    setError(null)
    try {
      await apiRequestCode(email)
      setPendingEmail(email.trim())
    } catch (e) {
      setError(message(e))
    }
  }, [])

  const verify = useCallback(async (code: string): Promise<boolean> => {
    if (!pendingEmail) return false
    setError(null)
    try {
      const result = await verifyCode(pendingEmail, code)
      setToken(result.token)
      setUser(result.user)
      setPendingEmail(null)
      return true
    } catch (e) {
      setError(message(e))
      return false
    }
  }, [pendingEmail])

  const resend = useCallback(async () => {
    if (pendingEmail) await apiRequestCode(pendingEmail)
  }, [pendingEmail])

  const signOut = useCallback(() => {
    setToken(null)
    setUser(null)
    setPendingEmail(null)
    setError(null)
  }, [])

  const status: AuthStatus = user ? 'signed-in' : pendingEmail ? 'awaiting-code' : 'signed-out'

  return { status, loading, user, email: user?.email ?? pendingEmail, error, requestCode, verify, resend, signOut }
}
