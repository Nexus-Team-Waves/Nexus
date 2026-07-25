import { HomeIcon, PlusIcon, ListIcon } from './icons'
import BrandMark from './BrandMark'

/** The three primary destinations. Kept as a union so routing stays type-safe. */
export type Screen = 'home' | 'submit' | 'claims'

/**
 * Persistent bottom navigation (thumb-reachable on a phone). On desktop (≥900px) CSS
 * re-lays this same element out as a branded left sidebar — the BrandMark below is hidden
 * on mobile (`.nav-brand { display: none }`) and shown only in the sidebar.
 */
export default function BottomNav({
  active,
  onNavigate,
}: {
  active: Screen
  onNavigate: (screen: Screen) => void
}) {
  return (
    <nav className="bottom-nav" aria-label="Primary">
      <div className="nav-brand">
        <BrandMark />
      </div>
      <NavItem label="Home" active={active === 'home'} onClick={() => onNavigate('home')}>
        <HomeIcon />
      </NavItem>
      <NavItem label="Submit" active={active === 'submit'} onClick={() => onNavigate('submit')}>
        <PlusIcon />
      </NavItem>
      <NavItem label="Claims" active={active === 'claims'} onClick={() => onNavigate('claims')}>
        <ListIcon />
      </NavItem>
    </nav>
  )
}

function NavItem(props: {
  label: string
  active: boolean
  onClick: () => void
  children: React.ReactNode
}) {
  return (
    <button
      type="button"
      className={`nav-item ${props.active ? 'nav-item--active' : ''}`}
      aria-current={props.active ? 'page' : undefined}
      onClick={props.onClick}
    >
      {props.children}
      <span>{props.label}</span>
    </button>
  )
}
