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
  NotificationDto,
  ReceiptUploadResponse,
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

/** Shared "turn a non-2xx response into an ApiError" step for the binary helpers below. */
async function throwApiError(response: Response): Promise<never> {
  let detail = ''
  try {
    const body = await response.json()
    detail = body?.detail ?? body?.title ?? ''
  } catch {
    detail = await response.text().catch(() => '')
  }
  throw new ApiError(detail || `Request failed (${response.status})`, response.status)
}

/**
 * Upload one receipt image (multipart/form-data). Deliberately does NOT set a Content-Type
 * header — the browser must set it itself so the multipart boundary is included.
 */
export async function uploadReceipt(file: File): Promise<ReceiptUploadResponse> {
  const form = new FormData()
  form.append('file', file)
  const headers: Record<string, string> = {}
  if (token) headers.Authorization = `Bearer ${token}`

  const response = await fetch(`${BASE_URL}/api/receipts`, { method: 'POST', body: form, headers })
  if (!response.ok) await throwApiError(response)
  return (await response.json()) as ReceiptUploadResponse
}

/**
 * Fetch a binary endpoint as a Blob. A plain <img src> / <a href> cannot carry the bearer
 * token, so binary content is fetched here and turned into an object URL by the caller.
 */
async function requestBlob(path: string): Promise<Blob> {
  const headers: Record<string, string> = {}
  if (token) headers.Authorization = `Bearer ${token}`
  const response = await fetch(`${BASE_URL}${path}`, { headers })
  if (!response.ok) await throwApiError(response)
  return response.blob()
}

/** The stored receipt image (owner or any approver). */
export const getReceiptImage = (receiptId: string) => requestBlob(`/api/receipts/${receiptId}`)

/** One claim line's details + receipt image as a PDF (approvers only). */
export const getLineReceiptPdf = (claimId: string, lineId: string) =>
  requestBlob(`/api/approvals/${claimId}/lines/${lineId}/receipt.pdf`)

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
export const decideClaim = (id: string, lines: LineDecision[], forwardToTopLevel = false) =>
  request<ClaimDto>(`/api/approvals/${id}/decide`, { method: 'POST', body: JSON.stringify({ lines, forwardToTopLevel }) })
export const postClaim = (id: string, sapReference: string) =>
  request<ClaimDto>(`/api/approvals/${id}/post`, { method: 'POST', body: JSON.stringify({ sapReference }) })

// ---- Notifications ----
export const getNotifications = () => request<NotificationDto[]>('/api/notifications')
export const markNotificationsRead = () =>
  request<{ ok: boolean }>('/api/notifications/read-all', { method: 'POST' })

// ---- Web Push (approvers get stage alerts even with the browser closed) ----
export const getVapidPublicKey = () => request<{ publicKey: string }>('/api/push/vapid-public-key')
export const subscribePush = (endpoint: string, p256dh: string, auth: string) =>
  request<{ ok: boolean }>('/api/push/subscribe', { method: 'POST', body: JSON.stringify({ endpoint, p256dh, auth }) })
export const unsubscribePush = (endpoint: string) =>
  request<{ ok: boolean }>('/api/push/subscribe', { method: 'DELETE', body: JSON.stringify({ endpoint }) })
