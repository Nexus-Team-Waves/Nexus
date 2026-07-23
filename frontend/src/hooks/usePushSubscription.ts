import { useEffect } from 'react'
import { getVapidPublicKey, subscribePush } from '../api/client'
import type { ApiUser } from '../types/api'

/**
 * Registers a Web Push subscription for APPROVER roles so stage alerts arrive even when the
 * browser is closed (decision 2026-07-23). Best-effort by design:
 * - In `npm run dev` the service worker is disabled → this quietly does nothing. Test push with
 *   `npm run build && npm run preview`.
 * - If the user declines the permission prompt, they still get in-app toasts and the bell.
 */
export function usePushSubscription(user: ApiUser | null): void {
  useEffect(() => {
    if (!user || user.role === 'Employee') return
    if (!('serviceWorker' in navigator) || !('PushManager' in window) || !('Notification' in window)) return

    let cancelled = false
    void (async () => {
      try {
        const registration = await navigator.serviceWorker.getRegistration()
        if (!registration || cancelled) return

        const permission = await Notification.requestPermission()
        if (permission !== 'granted' || cancelled) return

        const { publicKey } = await getVapidPublicKey()
        const subscription = await registration.pushManager.subscribe({
          userVisibleOnly: true,
          applicationServerKey: urlBase64ToUint8Array(publicKey),
        })

        const json = subscription.toJSON()
        if (json.endpoint && json.keys?.['p256dh'] && json.keys?.['auth']) {
          await subscribePush(json.endpoint, json.keys['p256dh'], json.keys['auth'])
        }
      } catch {
        // Push is an enhancement — never let it break sign-in or the queue.
      }
    })()
    return () => { cancelled = true }
  }, [user])
}

/** The Push API wants the VAPID public key as bytes; the server sends base64url. */
function urlBase64ToUint8Array(base64Url: string): Uint8Array<ArrayBuffer> {
  const padding = '='.repeat((4 - (base64Url.length % 4)) % 4)
  const base64 = (base64Url + padding).replace(/-/g, '+').replace(/_/g, '/')
  const raw = window.atob(base64)
  const output = new Uint8Array(raw.length)
  for (let i = 0; i < raw.length; i++) output[i] = raw.charCodeAt(i)
  return output
}
