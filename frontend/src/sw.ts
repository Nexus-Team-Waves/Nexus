/// <reference lib="webworker" />
/**
 * MEMS custom service worker (injectManifest build).
 *
 * This hand-written worker REPRODUCES everything the previous generateSW config gave us, then
 * adds Web Push. If you touch this file, keep all four guarantees:
 *   1. The app shell is precached (offline claim entry keeps working).
 *   2. Caches are versioned per build and stale ones purged on activation.
 *   3. New builds take over immediately (autoUpdate behaviour).
 *   4. /api is NEVER served from any cache (CLAUDE.md §9) — there is deliberately no fetch
 *      handler or runtime cache for API requests; they always hit the network.
 */
import { clientsClaim } from 'workbox-core'
import { cleanupOutdatedCaches, createHandlerBoundToURL, precacheAndRoute } from 'workbox-precaching'
import { NavigationRoute, registerRoute } from 'workbox-routing'

declare let self: ServiceWorkerGlobalScope & {
  __WB_MANIFEST: Array<{ url: string; revision: string | null } | string>
}

self.skipWaiting()
clientsClaim()
cleanupOutdatedCaches()

// Precache the built app shell; the manifest is injected at build time.
precacheAndRoute(self.__WB_MANIFEST)

// Client-side navigations fall back to the shell — but never for /api URLs.
registerRoute(new NavigationRoute(createHandlerBoundToURL('index.html'), { denylist: [/^\/api/] }))

/**
 * Web Push: the server sends { title, body, claimId } when a claim enters this approver's
 * queue. Showing a notification is required (userVisibleOnly) and is the whole point — the
 * approver hears about new claims even when the browser is closed.
 */
self.addEventListener('push', (event) => {
  const data = (() => {
    try { return event.data?.json() as { title?: string; body?: string; claimId?: string } | undefined }
    catch { return undefined }
  })()
  if (!data) return
  event.waitUntil(self.registration.showNotification(data.title ?? 'MEMS', {
    body: data.body ?? '',
    icon: '/favicon.svg',
    data: { claimId: data.claimId },
  }))
})

// Clicking the notification focuses an open MEMS tab, or opens the app.
self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clients) => {
      const existing = clients.find((c): c is WindowClient => 'focus' in c)
      return existing ? existing.focus() : self.clients.openWindow('/')
    }),
  )
})
