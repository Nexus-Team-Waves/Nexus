type CardState = 'ok' | 'warn' | 'error' | 'pending'

interface StatusCardProps {
  label: string
  value: string
  state: CardState
  detail: string
}

export default function StatusCardComponent({
  label,
  value,
  state,
  detail,
}: StatusCardProps) {
  return (
    <article className={`status-card status-card--${state}`}>
      <h2>{label}</h2>

      <p className="status-value">
        {value}
      </p>

      <p className="status-detail">
        {detail}
      </p>
    </article>
  )
}