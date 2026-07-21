/**
 * Minimal inline-SVG icon set. Inline (not an icon font or image) so icons inherit `color`,
 * stay crisp at any size, and add no dependency (CLAUDE.md §13). Each takes an optional size.
 */
import type { CSSProperties } from 'react'

interface IconProps {
  size?: number
  style?: CSSProperties
}

const base = (size: number): React.SVGProps<SVGSVGElement> => ({
  width: size,
  height: size,
  viewBox: '0 0 24 24',
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 1.8,
  strokeLinecap: 'round' as const,
  strokeLinejoin: 'round' as const,
})

export function HomeIcon({ size = 22, style }: IconProps) {
  return (
    <svg {...base(size)} style={style} aria-hidden="true">
      <path d="M3 10.5 12 4l9 6.5" />
      <path d="M5 9.5V20h14V9.5" />
    </svg>
  )
}

export function PlusIcon({ size = 22, style }: IconProps) {
  return (
    <svg {...base(size)} style={style} aria-hidden="true">
      <path d="M12 5v14M5 12h14" />
    </svg>
  )
}

export function ListIcon({ size = 22, style }: IconProps) {
  return (
    <svg {...base(size)} style={style} aria-hidden="true">
      <path d="M8 6h13M8 12h13M8 18h13" />
      <circle cx="3.5" cy="6" r="1.1" fill="currentColor" stroke="none" />
      <circle cx="3.5" cy="12" r="1.1" fill="currentColor" stroke="none" />
      <circle cx="3.5" cy="18" r="1.1" fill="currentColor" stroke="none" />
    </svg>
  )
}

export function BellIcon({ size = 22, style }: IconProps) {
  return (
    <svg {...base(size)} style={style} aria-hidden="true">
      <path d="M18 8a6 6 0 1 0-12 0c0 7-3 9-3 9h18s-3-2-3-9" />
      <path d="M13.7 21a2 2 0 0 1-3.4 0" />
    </svg>
  )
}

export function StethoscopeIcon({ size = 22, style }: IconProps) {
  return (
    <svg {...base(size)} style={style} aria-hidden="true">
      <path d="M6 3v5a4 4 0 0 0 8 0V3" />
      <path d="M10 15v1a5 5 0 0 0 10 0v-2" />
      <circle cx="20" cy="12" r="2" />
    </svg>
  )
}

export function PersonIcon({ size = 22, style }: IconProps) {
  return (
    <svg {...base(size)} style={style} aria-hidden="true">
      <circle cx="12" cy="8" r="4" />
      <path d="M4 21c0-4 3.6-6 8-6s8 2 8 6" />
    </svg>
  )
}

export function CameraIcon({ size = 20, style }: IconProps) {
  return (
    <svg {...base(size)} style={style} aria-hidden="true">
      <path d="M4 8h3l1.5-2h7L17 8h3a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V9a1 1 0 0 1 1-1Z" />
      <circle cx="12" cy="13" r="3.2" />
    </svg>
  )
}

export function UploadIcon({ size = 20, style }: IconProps) {
  return (
    <svg {...base(size)} style={style} aria-hidden="true">
      <path d="M12 16V5m0 0 4 4m-4-4-4 4" />
      <path d="M5 19h14" />
    </svg>
  )
}
