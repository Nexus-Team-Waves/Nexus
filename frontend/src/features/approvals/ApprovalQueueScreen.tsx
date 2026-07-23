import { claimPrimaryLabel, formatRs } from '../../types/domain'
import NotificationsBell from '../../components/NotificationsBell'
import type { ClaimDto, NotificationDto } from '../../types/api'

/**
 * An approver's queue: the claims awaiting their stage (Finance also sees completed claims that
 * still need SAP posting). Each row opens the review screen.
 */
export default function ApprovalQueueScreen({
  roleLabel,
  displayName,
  claims,
  notifications,
  unreadCount,
  onMarkAllRead,
  onOpen,
  onSignOut,
}: {
  roleLabel: string
  displayName: string
  claims: ClaimDto[]
  notifications: NotificationDto[]
  unreadCount: number
  onMarkAllRead: () => void
  onOpen: (claim: ClaimDto) => void
  onSignOut: () => void
}) {
  return (
    <div className="screen-pad">
      <header className="home-head">
        <div>
          <h1 className="greeting">Approvals</h1>
          <p className="greeting-sub">{roleLabel} · {displayName}</p>
        </div>
        <div className="head-actions">
          <NotificationsBell items={notifications} unreadCount={unreadCount} onMarkAllRead={onMarkAllRead} />
          <button type="button" className="btn btn--link" onClick={onSignOut}>Sign out</button>
        </div>
      </header>

      {claims.length === 0 ? (
        <p className="muted-note">Nothing is awaiting you right now.</p>
      ) : (
        <div className="claim-cards">
          {claims.map((claim) => {
            const posting = claim.stage === 'Completed' && !claim.posted
            // Amount in play at this stage = sum of the current line amounts.
            const inPlay = claim.lines.reduce((s, l) => s + (posting ? (l.approvedAmount ?? 0) : l.currentAmount), 0)
            return (
              <button key={claim.id} type="button" className="queue-row" onClick={() => onOpen(claim)}>
                <div>
                  <p className="claim-card-title">
                    {claimPrimaryLabel(claim.lines.length, claim.lines[0]?.category ?? '')}
                  </p>
                  <p className="claim-card-meta">
                    {claim.employeeId} · {claim.submissionDate} · {claim.lines.length} item(s)
                  </p>
                </div>
                <div className="claim-card-right">
                  <span className={`pill pill--${posting ? 'info' : 'review'}`}>
                    {posting ? 'To post' : 'To review'}
                  </span>
                  <p className="claim-card-total">{formatRs(inPlay)}</p>
                </div>
              </button>
            )
          })}
        </div>
      )}
    </div>
  )
}
