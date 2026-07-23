import { useState } from 'react'
import { BellIcon } from './icons'
import type { NotificationDto } from '../types/api'
import type { NotificationToast } from '../hooks/useNotifications'

/**
 * Bell button with an unread badge and a dropdown of recent notifications. Opening the panel
 * marks everything read (matching how people expect a bell to behave).
 */
export default function NotificationsBell({
  items,
  unreadCount,
  onMarkAllRead,
}: {
  items: NotificationDto[]
  unreadCount: number
  onMarkAllRead: () => void
}) {
  const [open, setOpen] = useState(false)

  const toggle = () => {
    const next = !open
    setOpen(next)
    if (next && unreadCount > 0) onMarkAllRead()
  }

  return (
    <div className="bell-wrap">
      <button type="button" className="icon-btn" aria-label={`Notifications (${unreadCount} unread)`} onClick={toggle}>
        <BellIcon />
        {unreadCount > 0 && <span className="bell-badge">{unreadCount > 9 ? '9+' : unreadCount}</span>}
      </button>

      {open && (
        <div className="bell-panel" role="dialog" aria-label="Notifications">
          {items.length === 0 ? (
            <p className="muted-note">No notifications yet.</p>
          ) : (
            <ul className="bell-list">
              {items.map((n) => (
                <li key={n.id} className={n.read ? '' : 'bell-item--unread'}>
                  <strong>{n.title}</strong>
                  <span>{n.body}</span>
                  <time>{new Date(n.createdAt).toLocaleString()}</time>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  )
}

/** Transient toasts for notifications that just arrived while the app is open. */
export function ToastStack({ toasts }: { toasts: NotificationToast[] }) {
  if (toasts.length === 0) return null
  return (
    <div className="toast-stack" aria-live="polite">
      {toasts.map((t) => (
        <div key={t.id} className="toast">
          <strong>{t.title}</strong>
          <span>{t.body}</span>
        </div>
      ))}
    </div>
  )
}
