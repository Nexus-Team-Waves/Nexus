/** The MEMS wordmark: a rounded blue tile with "M", optionally followed by the name. */
export default function BrandMark({ showName = true }: { showName?: boolean }) {
  return (
    <div className="brand">
      <span className="brand-tile" aria-hidden="true">
        M
      </span>
      {showName && <span className="brand-name">MEMS</span>}
    </div>
  )
}
