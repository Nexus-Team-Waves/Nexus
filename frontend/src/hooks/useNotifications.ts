import { useCallback, useEffect, useRef, useState } from 'react'
import { getNotifications, markNotificationsRead } from '../api/client'
import type { NotificationDto } from '../types/api'

/** A transient on-screen toast for a just-arrived notification. */
export interface NotificationToast {
  id: string
  title: string
  body: string
}

const POLL_MS = 30_000
const TOAST_MS = 6_000

/**
 * Polls the API for the signed-in user's notifications (~every 30s) and surfaces new ones as
 * transient toasts. Optionally also raises a browser/system notification when the tab is in the
 * background — used for employees only; approvers get real Web Push via the service worker, and
 * doubling up would show the same alert twice.
 */
export function useNotifications(options: {
  enabled: boolean
  systemNotifications: boolean
  /** Called when new notifications arrive — lets the caller refresh its claim/queue lists. */
  onNew?: () => void
}) {
  const { enabled, systemNotifications, onNew } = options
  const [items, setItems] = useState<NotificationDto[]>([])
  const [toasts, setToasts] = useState<NotificationToast[]>([])
  // Ids we have already surfaced; null until the first poll (the first batch is history, not news).
  const seenIds = useRef<Set<string> | null>(null)
  const onNewRef = useRef(onNew)
  onNewRef.current = onNew

  const refresh = useCallback(async () => {
    let fetched: NotificationDto[]
    try {
      fetched = await getNotifications()
    } catch {
      return // offline / API down — try again next tick
    }
    setItems(fetched)

    if (seenIds.current === null) {
      seenIds.current = new Set(fetched.map((n) => n.id))
      return
    }
    const fresh = fetched.filter((n) => !n.read && !seenIds.current!.has(n.id))
    for (const n of fetched) seenIds.current.add(n.id)
    if (fresh.length === 0) return

    setToasts((prev) => [...prev, ...fresh.map((n) => ({ id: n.id, title: n.title, body: n.body }))])
    // Auto-dismiss each new toast after a few seconds.
    for (const n of fresh) {
      window.setTimeout(() => setToasts((prev) => prev.filter((t) => t.id !== n.id)), TOAST_MS)
    }

    // System notification only when the tab is hidden — in the foreground the toast suffices.
    if (systemNotifications && document.hidden && 'Notification' in window && Notification.permission === 'granted') {
      for (const n of fresh) new Notification(n.title, { body: n.body })
    }

    onNewRef.current?.()
  }, [systemNotifications])

  useEffect(() => {
    if (!enabled) return
    void refresh()
    const timer = window.setInterval(() => void refresh(), POLL_MS)
    return () => window.clearInterval(timer)
  }, [enabled, refresh])

  const markAllRead = useCallback(async () => {
    try {
      await markNotificationsRead()
      setItems((prev) => prev.map((n) => ({ ...n, read: true })))
    } catch {
      // Best effort — the badge just stays until the next successful call.
    }
  }, [])

  const unreadCount = items.filter((n) => !n.read).length
  return { items, unreadCount, toasts, markAllRead, refresh }
}
