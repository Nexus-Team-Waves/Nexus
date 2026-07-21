import { HomeIcon, PlusIcon, ListIcon } from './icons'

/** The three primary destinations. Kept as a union so routing stays type-safe. */
export type Screen = 'home' | 'submit' | 'claims'

/**
 * Persistent bottom navigation (thumb-reachable on a phone). The active item is highlighted in
 * brand colour; each item is a full tap target with an icon above its label.
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
