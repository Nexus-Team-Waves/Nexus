import { useCallback, useEffect, useState } from 'react'
import { useAuth } from './auth/useAuth'
import SignIn from './auth/SignIn'
import OtpVerify from './auth/OtpVerify'
import BottomNav, { type Screen } from './components/BottomNav'
import HomeScreen from './features/home/HomeScreen'
import SubmitScreen from './features/claims/SubmitScreen'
import ClaimsScreen from './features/claims/ClaimsScreen'
import ApprovalQueueScreen from './features/approvals/ApprovalQueueScreen'
import ClaimReviewScreen from './features/approvals/ClaimReviewScreen'
import { getApprovalQueue, getEntitlement, listMyClaims } from './api/client'
import type { ApiUser, ClaimDto, EntitlementDto, Role } from './types/api'
import './App.css'

const ROLE_LABEL: Record<Role, string> = {
  Employee: 'Employee',
  LineManager: 'Line Manager',
  AdminHr: 'Admin/HR',
  Finance: 'Finance',
  TopLevel: 'Top-Level Approver',
}

/**
 * MEMS shell. Not signed in → the email-OTP flow. Signed in → the employee app (Home/Submit/Claims)
 * or, for an approver role, the approvals app (Queue/Review). All data is live from the API.
 */
function App() {
  const auth = useAuth()

  if (auth.loading) {
    return <div className="app-frame"><p className="centered-note">Loading…</p></div>
  }
  if (auth.status === 'signed-out') {
    return <div className="app-frame"><SignIn onRequestCode={auth.requestCode} error={auth.error} /></div>
  }
  if (auth.status === 'awaiting-code') {
    return (
      <div className="app-frame">
        <OtpVerify email={auth.email ?? ''} onVerify={auth.verify} onResend={auth.resend} onBack={auth.signOut} />
      </div>
    )
  }

  const user = auth.user!
  return (
    <div className="app-frame">
      {user.role === 'Employee'
        ? <EmployeeApp onSignOut={auth.signOut} />
        : <ApproverApp user={user} onSignOut={auth.signOut} />}
    </div>
  )
}

/** Employee experience: dashboard, submit/edit, claims list. */
function EmployeeApp({ onSignOut }: { onSignOut: () => void }) {
  const [screen, setScreen] = useState<Screen>('home')
  const [editing, setEditing] = useState<ClaimDto | null>(null)
  const [entitlement, setEntitlement] = useState<EntitlementDto | null>(null)
  const [claims, setClaims] = useState<ClaimDto[]>([])
  const [loading, setLoading] = useState(true)

  const refresh = useCallback(async () => {
    const [ent, list] = await Promise.all([getEntitlement(), listMyClaims()])
    setEntitlement(ent)
    setClaims(list)
    setLoading(false)
  }, [])

  useEffect(() => { void refresh() }, [refresh])

  if (loading || !entitlement) return <p className="centered-note">Loading…</p>

  const onNavigate = (next: Screen) => {
    if (next === 'submit') setEditing(null)
    setScreen(next)
  }
  const afterSubmit = async () => { setEditing(null); await refresh(); setScreen('claims') }

  return (
    <>
      <div className="app-scroll">
        {screen === 'home' && (
          <HomeScreen
            entitlement={entitlement}
            recent={claims.slice(0, 3)}
            onNavigate={(s) => (s === 'submit' ? onNavigate('submit') : setScreen('claims'))}
            onOpenClaim={() => setScreen('claims')}
            onSignOut={onSignOut}
          />
        )}
        {screen === 'submit' && (
          <SubmitScreen dependants={entitlement.dependants} editing={editing} onDone={afterSubmit} />
        )}
        {screen === 'claims' && (
          <ClaimsScreen claims={claims} onEdit={(c) => { setEditing(c); setScreen('submit') }} />
        )}
      </div>
      <BottomNav active={screen} onNavigate={onNavigate} />
    </>
  )
}

/** Approver experience: the role's queue and the per-claim review screen. */
function ApproverApp({ user, onSignOut }: { user: ApiUser; onSignOut: () => void }) {
  const [queue, setQueue] = useState<ClaimDto[]>([])
  const [reviewing, setReviewing] = useState<ClaimDto | null>(null)
  const [loading, setLoading] = useState(true)

  const refresh = useCallback(async () => {
    setQueue(await getApprovalQueue())
    setLoading(false)
  }, [])

  useEffect(() => { void refresh() }, [refresh])

  if (loading) return <p className="centered-note">Loading…</p>

  return (
    <div className="app-scroll">
      {reviewing ? (
        <ClaimReviewScreen
          claim={reviewing}
          onBack={() => setReviewing(null)}
          onUpdated={async () => { setReviewing(null); await refresh() }}
        />
      ) : (
        <ApprovalQueueScreen
          roleLabel={ROLE_LABEL[user.role]}
          displayName={user.displayName}
          claims={queue}
          onOpen={(c) => setReviewing(c)}
          onSignOut={onSignOut}
        />
      )}
    </div>
  )
}

export default App
