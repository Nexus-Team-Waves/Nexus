import { useEffect, useState } from 'react'

/**
 * Tracks browser connectivity.
 *
 * IMPORTANT CAVEAT: `navigator.onLine` is a weak signal. It reports whether the device
 * has *a* network connection — not whether the MEMS API is actually reachable. A user on
 * hotel wifi with a captive portal, or on a mobile signal that has dropped out, reads as
 * "online" while every request fails.
 *
 * So treat this as a UI hint only ("you appear to be offline"), never as the gate that
 * decides whether a claim is safe to submit. The offline queue is what makes submission
 * safe, and it is used on every submission regardless of what this hook says.
 */
export function useOnlineStatus(): boolean {
  const [isOnline, setIsOnline] = useState<boolean>(() => navigator.onLine)

  useEffect(() => {
    const handleOnline = () => setIsOnline(true)
    const handleOffline = () => setIsOnline(false)

    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)

    // Cleanup on unmount, otherwise listeners leak across remounts.
    return () => {
      window.removeEventListener('online', handleOnline)
      window.removeEventListener('offline', handleOffline)
    }
  }, [])

  return isOnline
}
