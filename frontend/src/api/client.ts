/**
 * Typed MEMS API client.
 * ---------------------------------------------------------------------------
 * The frontend talks ONLY to the MEMS API — never to SAP B1 or any database
 * directly (CLAUDE.md §7/§13). Every call goes through this module so that
 * auth headers, error shape, and the base URL live in exactly one place.
 */

/**
 * In dev, this is empty and Vite proxies /api to the .NET API (see vite.config.ts).
 * In production, set VITE_API_BASE_URL at build time to the real API host.
 */
const BASE_URL: string = import.meta.env['VITE_API_BASE_URL'] ?? ''

/** Shape of the API's health response. Mirrors HealthResponse in the .NET API. */
export interface HealthResponse {
  status: string
  service: string
  environment: string
  utcTime: string
}

/**
 * Thrown for any non-2xx response. Carries the HTTP status so callers can
 * distinguish "you are offline" from "the server said no".
 */
export class ApiError extends Error {
  /** HTTP status code of the failing response. */
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

/**
 * Thin fetch wrapper. Deliberately small — it is not a framework.
 *
 * Note it does NOT retry. Retry policy belongs to the offline sync engine, which
 * knows about idempotency keys and backoff; a blind retry here could duplicate a
 * non-idempotent request.
 */
async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
  })

  if (!response.ok) {
    // Read the body as text: an error response may be ProblemDetails JSON or plain
    // text from a proxy, and we must not assume it parses as JSON.
    const detail = await response.text().catch(() => '')
    throw new ApiError(
      `Request to ${path} failed with ${response.status}. ${detail}`.trim(),
      response.status,
    )
  }

  return (await response.json()) as T
}

/** Liveness probe. Used by the shell to show API reachability. */
export function getHealth(): Promise<HealthResponse> {
  return request<HealthResponse>('/api/health')
}
