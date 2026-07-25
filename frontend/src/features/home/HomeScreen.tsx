import { ListIcon, PersonIcon, PlusIcon, StethoscopeIcon } from '../../components/icons'
import StatusPill from '../../components/StatusPill'
import NotificationsBell from '../../components/NotificationsBell'
import { claimPrimaryLabel, formatRs } from '../../types/domain'
import type { ClaimDto, EntitlementDto, NotificationDto } from '../../types/api'

/**
 * Home: greeting, remaining entitlement, the two primary actions, and a short "Recent" list —
 * all from the live API (`/api/entitlement`, `/api/claims`).
 */
export default function HomeScreen({
  entitlement,
  recent,
  notifications,
  unreadCount,
  onMarkAllRead,
  onNavigate,
  onOpenClaim,
  onSignOut,
}: {
  entitlement: EntitlementDto
  recent: ClaimDto[]
  notifications: NotificationDto[]
  unreadCount: number
  onMarkAllRead: () => void
  onNavigate: (screen: 'submit' | 'claims') => void
  onOpenClaim: (id: string) => void
  onSignOut: () => void
}) {
  const usedPct = entitlement.cap > 0
    ? Math.min(100, Math.round((entitlement.consumed / entitlement.cap) * 100))
    : 0

  return (
    <div className="screen-pad">
      <header className="home-head">
        <div>
          <h1 className="greeting">Hello, {entitlement.firstName}</h1>
          <p className="greeting-sub">{entitlement.gradeLabel}</p>
        </div>
        <NotificationsBell items={notifications} unreadCount={unreadCount} onMarkAllRead={onMarkAllRead} />
      </header>

      <section className="balance-card">
        <p className="balance-label">Remaining this year</p>
        <p className="balance-value">{formatRs(entitlement.remaining)}</p>
        <div className="bar" role="progressbar" aria-valuenow={usedPct} aria-valuemin={0} aria-valuemax={100}>
          <div className="bar-fill" style={{ width: `${usedPct}%` }} />
        </div>
        <p className="balance-foot">
          {formatRs(entitlement.consumed)} used of {formatRs(entitlement.cap)}
        </p>
      </section>

      {/* At-a-glance figures behind the hero number. */}
      <div className="stat-row">
        <div className="stat">
          <span className="stat-label">Used</span>
          <span className="stat-value">{formatRs(entitlement.consumed)}</span>
        </div>
        <div className="stat">
          <span className="stat-label">Annual cap</span>
          <span className="stat-value">{formatRs(entitlement.cap)}</span>
        </div>
        <div className="stat">
          <span className="stat-label">Utilised</span>
          <span className="stat-value">{usedPct}%</span>
        </div>
      </div>

      <div className="action-row">
        <button type="button" className="btn btn--primary" onClick={() => onNavigate('submit')}>
          <PlusIcon size={18} /> Submit a claim
        </button>
        <button type="button" className="btn btn--outline" onClick={() => onNavigate('claims')}>
          <ListIcon size={18} /> Your claims
        </button>
      </div>

      <h2 className="section-title">Recent</h2>
      {recent.length === 0 ? (
        <p className="muted-note">No claims yet. Tap “Submit a claim” to add your first.</p>
      ) : (
        <ul className="recent-list">
          {recent.map((claim) => (
            <li key={claim.id}>
              <button type="button" className="recent-row" onClick={() => onOpenClaim(claim.id)}>
                <span className="recent-icon" aria-hidden="true">
                  {claim.lines.length > 1 ? <PersonIcon size={20} /> : <StethoscopeIcon size={20} />}
                </span>
                <span className="recent-label">
                  {claimPrimaryLabel(claim.lines.length, claim.lines[0]?.category ?? '')}
                </span>
                <StatusPill status={claim.overallStatus} />
              </button>
            </li>
          ))}
        </ul>
      )}

      <button type="button" className="btn btn--link sign-out" onClick={onSignOut}>
        Sign out
      </button>
    </div>
  )
}
