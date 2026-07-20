import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'

// MEMS PWA build config.
// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      // 'autoUpdate' ships a new service worker as soon as one is available. Claim entry
      // must never be pinned to a stale build holding outdated entitlement logic.
      registerType: 'autoUpdate',

      workbox: {
        // Precache the app shell only. The shell is what makes offline claim entry work:
        // the UI loads with no network, and claims queue locally in IndexedDB via Dexie.
        globPatterns: ['**/*.{js,css,html,svg,png,ico,woff2}'],

        // CRITICAL (CLAUDE.md §9): the service worker must never serve /api from cache.
        // Entitlement balances and approval status must always be fresh — a cached balance
        // would let someone submit against an entitlement they have already spent.
        // There is deliberately NO runtimeCaching entry for /api.
        navigateFallback: 'index.html',
        navigateFallbackDenylist: [/^\/api/],

        // Workbox revisions every precached asset by content hash, so the cache is
        // versioned per build and stale entries are purged on activation (§9).
        cleanupOutdatedCaches: true,
      },

      manifest: {
        name: 'MEMS — Medical Entitlement Management',
        short_name: 'MEMS',
        description: 'Submit and track medical entitlement claims.',
        theme_color: '#0f766e',
        background_color: '#ffffff',
        display: 'standalone',
        start_url: '/',
        icons: [
          // TODO: replace with real MEMS icons (192px + 512px PNG) before any pilot.
          { src: 'favicon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' },
        ],
      },

      devOptions: {
        // Keep the SW off in dev — it caches aggressively and makes hot-reload confusing.
        // Test PWA/offline behaviour with `npm run build && npm run preview`.
        enabled: false,
      },
    }),
  ],

  server: {
    port: 5173,
    proxy: {
      // Dev-only convenience: the frontend calls /api/* on its own origin and Vite
      // forwards to the .NET API (port from backend/Api/Properties/launchSettings.json).
      // Production points at a real host via VITE_API_BASE_URL instead.
      '/api': {
        target: 'http://localhost:5046',
        changeOrigin: true,
        // The .NET dev HTTPS certificate is self-signed; accept it in dev only.
        secure: false,
      },
    },
  },
})
