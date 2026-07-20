import { useEffect, useState } from 'react'
import { useLiveQuery } from 'dexie-react-hooks'
import { getHealth, type HealthResponse } from './api/client'
import { useOnlineStatus } from './hooks/useOnlineStatus'
import { db } from './offline/db'
import './App.css'

//import  StatusCard  from './components/StatusCardComponent'

import StatusGrid from './components/StatusGrid'

/**
 * MEMS application shell — scaffold.
 *
 * This is intentionally a status page, not a feature. It proves the three pieces of the
 * architecture are wired together and talking:
 *   1. the React PWA renders,
 *   2. it can reach the .NET API (and degrades honestly when it cannot),
 *   3. the Dexie/IndexedDB offline queue is live and readable.
 *
 * Real screens go under src/features/{claims,approvals,entitlements}.
 */
function App() {
  const isOnline = useOnlineStatus()
  const [health, setHealth] = useState<HealthResponse | null>(null)
  const [healthError, setHealthError] = useState<string | null>(null)

  // useLiveQuery re-renders automatically whenever the underlying Dexie table changes —
  // no manual subscription or polling. Returns undefined on the first render, before the
  // IndexedDB read resolves, which is why the count below guards against that.
  const queuedCount = useLiveQuery(() => db.claims.count())

  useEffect(() => {
    let cancelled = false

    getHealth()
      .then((result) => {
        if (cancelled) return
        setHealth(result)
        setHealthError(null)
      })
      .catch((error: unknown) => {
        if (cancelled) return
        setHealth(null)
        setHealthError(error instanceof Error ? error.message : 'Unknown error')
      })

    // Guard against setting state after unmount (e.g. React 18+ StrictMode double-invoke).
    return () => {
      cancelled = true
    }
  }, [])

  return (
    <main className="mems-shell">
      <header>
        <h1>MEMS</h1>
        <p className="subtitle">Medical Entitlement Management System</p>
        <p className="scaffold-note">
          Scaffold build — architecture wired, business logic not yet implemented.
        </p>
      </header>


<StatusGrid
  isOnline={isOnline}
  queuedCount={queuedCount}
  health={health}
  healthError={healthError}
/>


      {/* <section className="status-grid">

        <StatusCard 
        label ="User Status Card"
        value="running"
        state="ok"
        detail="some detail"
        />

        <StatusCard
          label="PWA shell"
          value="running"
          state="ok"
          detail="React + TypeScript, offline-capable"
        />

        <StatusCard
          label="Network"
          value={isOnline ? 'online' : 'offline'}
          state={isOnline ? 'ok' : 'warn'}
          detail={isOnline ? 'Browser reports connectivity' : 'Claims will queue locally'}
        />

        

        <StatusCard
          label="Offline queue"
          value={queuedCount === undefined ? 'reading…' : `${queuedCount} claim(s)`}
          state="ok"
          detail="Dexie over IndexedDB"
        />
      </section> */}

      <footer>
        <p>
          Next: confirm entitlement rules and SAP mapping — see <code>/docs</code>.
        </p>
      </footer>
    </main>
  )
}


// type CardState = 'ok' | 'warn' | 'error' | 'pending'

// /** Small presentational card. Kept in this file until a second screen needs it. */
// function StatusCard(props: {
//   label: string
//   value: string
//   state: CardState
//   detail: string
// }) {
//   return (
//     <article className={`status-card status-card--${props.state}`}>
//       <h2>{props.label}</h2>
//       <p className="status-value">{props.value}</p>
//       <p className="status-detail">{props.detail}</p>
//     </article>
//   )
// }

export default App
