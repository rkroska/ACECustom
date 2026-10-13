import { formatCompact, formatFull, formatPct, formatSigned, STATUS_CLASSES, STATUS_LABEL, statusOf } from './economyUtils'

/** An amount: compact text, exact value on hover. */
export function Amount({ value, signed, className = '' }: { value: number | null | undefined; signed?: boolean; className?: string }) {
  if (value === null || value === undefined) return <span className="text-neutral-600">-</span>
  return (
    <span className={`tabular-nums ${className}`} title={formatFull(value)}>
      {signed ? formatSigned(value) : formatCompact(value)}
    </span>
  )
}

export function NetAmount({ value }: { value: number }) {
  const color = value > 0 ? 'text-emerald-400' : value < 0 ? 'text-red-400' : 'text-neutral-500'
  return <Amount value={value} signed className={color} />
}

export function StatusPill({ status }: { status: string }) {
  const s = statusOf(status)
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-md border text-[10px] font-bold uppercase tracking-wider ${STATUS_CLASSES[s]}`}>
      {STATUS_LABEL[s]}
    </span>
  )
}

/** A change value like "+1.2M (+3.4%)", or a note when there is no history. */
export function ChangeText({ change }: { change: { amount: number; pct: number } | null }) {
  if (!change) return <span className="text-neutral-600">needs a snapshot that old</span>
  const color = change.amount > 0 ? 'text-amber-300' : change.amount < 0 ? 'text-blue-300' : 'text-neutral-400'
  return (
    <span className={`tabular-nums ${color}`} title={`${formatFull(change.amount)} (${formatPct(change.pct, 2)})`}>
      {formatSigned(change.amount)} ({formatPct(change.pct)})
    </span>
  )
}
