/**
 * Typed MEMS API client.
 * ---------------------------------------------------------------------------
 * The frontend talks ONLY to the MEMS API (CLAUDE.md §7/§13). Every call goes through this module,
 * so the base URL, the bearer token, and the error shape live in exactly one place.
 *
 * In dev the base URL is empty and Vite proxies /api to the .NET API (vite.config.ts). The bearer
 * token (the user's email, in this demo) is kept in localStorage and sent on every request.
 */
import type {
  ApiUser,
  AuthResponse,
  ClaimDto,
  EntitlementDto,
  LineDecision,
  SubmitClaimRequest,
} from '../types/api'

const BASE_URL: string = import.meta.env['VITE_API_BASE_URL'] ?? ''
const TOKEN_KEY = 'mems.token'

let token: string | null = localStorage.getItem(TOKEN_KEY)

export function setToken(value: string | null): void {
  token = value
  if (value) localStorage.setItem(TOKEN_KEY, value)
  else localStorage.removeItem(TOKEN_KEY)
}
export function getToken(): string | null {
  return token
}

/** Non-2xx responses throw this. Carries the status and a server-provided detail message. */
export class ApiError extends Error {
  readonly status: number
  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(init?.headers as Record<string, string> | undefined),
  }
  if (token) headers.Authorization = `Bearer ${token}`

  const response = await fetch(`${BASE_URL}${path}`, { ...init, headers })

  if (!response.ok) {
    // Error bodies are problem-details JSON ({ title, detail }) or plain text.
    let detail = ''
    try {
      const body = await response.json()
      detail = body?.detail ?? body?.title ?? ''
    } catch {
      detail = await response.text().catch(() => '')
    }
    throw new ApiError(detail || `Request failed (${response.status})`, response.status)
  }

  // 204 or empty body → undefined.
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

// ---- Auth ----
export const requestCode = (email: string) =>
  request<{ sent: boolean }>('/api/auth/request-code', { method: 'POST', body: JSON.stringify({ email }) })

export const verifyCode = (email: string, code: string) =>
  request<AuthResponse>('/api/auth/verify', { method: 'POST', body: JSON.stringify({ email, code }) })

// ---- Me / entitlement ----
export const getMe = () => request<ApiUser>('/api/me')
export const getEntitlement = (year = 2026) => request<EntitlementDto>(`/api/entitlement?year=${year}`)

// ---- Claims (employee) ----
export const listMyClaims = () => request<ClaimDto[]>('/api/claims')
export const getClaim = (id: string) => request<ClaimDto>(`/api/claims/${id}`)
export const submitClaim = (payload: SubmitClaimRequest) =>
  request<ClaimDto>('/api/claims', { method: 'POST', body: JSON.stringify(payload) })
export const editClaim = (id: string, payload: SubmitClaimRequest) =>
  request<ClaimDto>(`/api/claims/${id}`, { method: 'PUT', body: JSON.stringify(payload) })

// ---- Approvals (approver) ----
export const getApprovalQueue = () => request<ClaimDto[]>('/api/approvals')
export const decideClaim = (id: string, lines: LineDecision[]) =>
  request<ClaimDto>(`/api/approvals/${id}/decide`, { method: 'POST', body: JSON.stringify({ lines }) })
export const postClaim = (id: string, sapReference: string) =>
  request<ClaimDto>(`/api/approvals/${id}/post`, { method: 'POST', body: JSON.stringify({ sapReference }) })
