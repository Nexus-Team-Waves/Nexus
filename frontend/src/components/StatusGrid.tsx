import StatusCard from './StatusCardComponent'
import type { HealthResponse } from '../api/client'

type CardState = 'ok' | 'warn' | 'error' | 'pending'

interface StatusGridProps {
  isOnline: boolean
  queuedCount?: number
  health: HealthResponse | null
  healthError: string | null
}

export default function StatusGrid({
  isOnline,
  queuedCount,
  health,
  healthError,
}: StatusGridProps) {
  return (
    <section className="status-grid">
      <StatusCard
        label="User Status Card"
        value="running"
        state="ok"
        detail="some detail"
      />

      <StatusCard
        label="PWA shell"
        value="running"
        state="error"
        detail="React + TypeScript, offline-capable"
      />

      <StatusCard
        label="Network"
        value={isOnline ? 'online' : 'offline'}
        state={isOnline ? 'ok' : 'warn'}
        detail={
          isOnline
            ? 'Browser reports connectivity'
            : 'Claims will queue locally'
        }
      />

      <StatusCard
        label="MEMS API"
        value={
          health
            ? health.status
            : healthError
              ? 'unreachable'
              : 'checking...'
        }
        state={
          health
            ? 'ok'
            : healthError
              ? 'error'
              : 'pending'
        }
        detail={
          health
            ? `${health.service} · ${health.environment}`
            : healthError
              ? 'Start the API: dotnet run --project backend/Api'
              : 'Contacting /api/health'
        }
      />

      <StatusCard
        label="Offline queue"
        value={
          queuedCount === undefined
            ? 'reading...'
            : `${queuedCount} claim(s)`
        }
        state="ok"
        detail="Dexie over IndexedDB"
      />
    </section>
  )
}