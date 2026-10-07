import { useState, type ReactNode } from 'react'
import { AlertTriangle, ChevronDown, ChevronRight, Info } from 'lucide-react'
import {
  FLAG_INFO,
  SEVERITY_CLASSES,
  SEVERITY_LABEL,
  flagInfo,
  formatCompact,
  formatFull,
  formatSigned,
  sourceHelp,
} from './ledgerUtils'

/** A pyreal amount: compact text, full value on hover. */
export function Money({ value, signed, className = '' }: { value: number | null | undefined; signed?: boolean; className?: string }) {
  if (value === null || value === undefined) return <span className="text-neutral-600">-</span>
  const text = signed ? formatSigned(value) : formatCompact(value)
  return (
    <span className={`tabular-nums ${className}`} title={`${formatFull(value)} pyreals`}>
      {text}
    </span>
  )
}

/** Signed money colored green for gains, red for losses. */
export function NetMoney({ value }: { value: number }) {
  const color = value > 0 ? 'text-emerald-400' : value < 0 ? 'text-red-400' : 'text-neutral-500'
  return <Money value={value} signed className={color} />
}

/** Currency surplus: positive means more currency left than the ledger saw arrive (suspicious), so it is amber. */
export function SurplusMoney({ value }: { value: number }) {
  const color = value > 0 ? 'text-amber-300' : 'text-neutral-500'
  return <Money value={value} signed className={color} />
}

export function FlagBadge({ flag, count }: { flag: string; count?: number }) {
  const info = flagInfo(flag)
  return (
    <span
      className={`inline-flex items-center gap-1 px-1.5 py-0.5 rounded-md border text-[10px] font-bold whitespace-nowrap ${SEVERITY_CLASSES[info.severity]}`}
      title={`${info.label} (${SEVERITY_LABEL[info.severity]}): ${info.explanation}`}
    >
      {info.label}
      {count !== undefined && count > 1 && <span className="opacity-70">x{count.toLocaleString()}</span>}
    </span>
  )
}

export function FlagBadgeList({ flags }: { flags: Record<string, number> | null | undefined }) {
  const entries = Object.entries(flags ?? {}).filter(([, c]) => c > 0)
  if (entries.length === 0) return <span className="text-neutral-600">-</span>
  const ordered = FLAG_INFO.map(f => f.flag)
  entries.sort(([a], [b]) => {
    const ia = ordered.indexOf(a)
    const ib = ordered.indexOf(b)
    return (ia < 0 ? 99 : ia) - (ib < 0 ? 99 : ib)
  })
  return (
    <div className="flex flex-wrap gap-1">
      {entries.map(([flag, count]) => (
        <FlagBadge key={flag} flag={flag} count={count} />
      ))}
    </div>
  )
}

export function MultiplierBadge({ multiplier }: { multiplier: number | null | undefined }) {
  if (multiplier === null || multiplier === undefined) return <span className="text-neutral-600">-</span>
  const bad = multiplier > 1
  return (
    <span
      className={`inline-flex items-center gap-1 px-1.5 py-0.5 rounded-md border text-[10px] font-bold tabular-nums ${
        bad ? 'bg-red-600/20 text-red-300 border-red-500/40' : 'bg-neutral-800 text-neutral-400 border-neutral-700'
      }`}
      title={
        bad
          ? 'This vendor pays MORE than the item is worth. That is a pricing bug that prints money - fix the vendor.'
          : 'Normal: the vendor pays at most the item value.'
      }
    >
      {bad && <AlertTriangle className="w-3 h-3" />}
      {multiplier.toLocaleString(undefined, { maximumFractionDigits: 3 })}x
    </span>
  )
}

export function SourceLabel({ source }: { source: string }) {
  const unattributed = source === 'Unattributed'
  return (
    <span
      className={`whitespace-nowrap ${unattributed ? 'text-amber-300 font-semibold' : 'text-neutral-200'}`}
      title={sourceHelp(source)}
    >
      {source}
    </span>
  )
}

export function LoadingState() {
  return (
    <div className="flex items-center justify-center p-12">
      <div className="w-10 h-10 border-4 border-blue-600/20 border-t-blue-600 rounded-full animate-spin" />
    </div>
  )
}

export function ErrorState({ message }: { message: string }) {
  return <div className="p-8 text-center text-red-400 text-sm">{message}</div>
}

export function EmptyState({ message }: { message: string }) {
  return <div className="p-10 text-center text-neutral-500 text-sm">{message}</div>
}

/** A titled card that holds a table or other content. */
export function Panel({
  title,
  help,
  actions,
  children,
  className = '',
}: {
  title: ReactNode
  help?: ReactNode
  actions?: ReactNode
  children: ReactNode
  className?: string
}) {
  return (
    <section className={`rounded-2xl border border-neutral-800 bg-neutral-900/40 overflow-hidden ${className}`}>
      <header className="px-4 py-3 border-b border-neutral-800 flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 className="text-sm font-bold text-white">{title}</h2>
          {help && <p className="text-xs text-neutral-500 mt-1 max-w-3xl leading-relaxed">{help}</p>}
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </header>
      {children}
    </section>
  )
}

/** Horizontal scroll container for wide tables. */
export function TableScroll({ children }: { children: ReactNode }) {
  return <div className="overflow-x-auto">{children}</div>
}

export const TABLE_CLASS = 'w-full text-left text-xs'
export const THEAD_CLASS = 'bg-neutral-950 text-[10px] uppercase tracking-wider text-neutral-500'
export const TH_CLASS = 'px-3 py-2 font-bold whitespace-nowrap'
export const TH_NUM_CLASS = 'px-3 py-2 font-bold whitespace-nowrap text-right'
export const TD_CLASS = 'px-3 py-2'
export const TD_NUM_CLASS = 'px-3 py-2 text-right whitespace-nowrap'
export const TBODY_CLASS = 'divide-y divide-neutral-800/80'
export const ROW_CLICK_CLASS = 'hover:bg-neutral-800/40 text-neutral-300 cursor-pointer'
export const ROW_CLASS = 'hover:bg-neutral-800/30 text-neutral-300'
export const LINK_CLASS = 'text-blue-400 hover:text-blue-300 hover:underline font-medium'

export function StatCard({ label, value, hint, tone = 'default' }: { label: string; value: ReactNode; hint?: ReactNode; tone?: 'default' | 'good' | 'bad' | 'warn' }) {
  const toneClass =
    tone === 'bad' ? 'text-red-300' : tone === 'good' ? 'text-emerald-300' : tone === 'warn' ? 'text-amber-300' : 'text-white'
  return (
    <div className="rounded-xl border border-neutral-800 bg-neutral-900/60 p-4 min-w-0">
      <div className="text-[10px] font-bold text-neutral-500 uppercase tracking-widest">{label}</div>
      <div className={`text-xl font-black mt-1 truncate ${toneClass}`}>{value}</div>
      {hint && <div className="text-[11px] text-neutral-500 mt-1 leading-snug">{hint}</div>}
    </div>
  )
}

export function InfoNote({ children, tone = 'info' }: { children: ReactNode; tone?: 'info' | 'warn' }) {
  const cls =
    tone === 'warn'
      ? 'border-amber-500/30 bg-amber-500/10 text-amber-200'
      : 'border-blue-500/20 bg-blue-500/5 text-neutral-300'
  const Icon = tone === 'warn' ? AlertTriangle : Info
  return (
    <div className={`flex items-start gap-2 rounded-xl border px-3 py-2 text-xs leading-relaxed ${cls}`}>
      <Icon className="w-4 h-4 shrink-0 mt-0.5 opacity-80" />
      <div>{children}</div>
    </div>
  )
}

/** Collapsible plain-English legend of every flag type. */
export function FlagLegend({ defaultOpen = false }: { defaultOpen?: boolean }) {
  const [open, setOpen] = useState(defaultOpen)
  return (
    <div className="rounded-xl border border-neutral-800 bg-neutral-900/60">
      <button
        type="button"
        onClick={() => setOpen(o => !o)}
        className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold text-neutral-300 hover:text-white"
      >
        {open ? <ChevronDown className="w-4 h-4" /> : <ChevronRight className="w-4 h-4" />}
        What do the flags mean?
      </button>
      {open && (
        <ul className="px-3 pb-3 space-y-2">
          {FLAG_INFO.map(f => (
            <li key={f.flag} className="flex flex-col sm:flex-row sm:items-start gap-1 sm:gap-3 text-xs">
              <div className="sm:w-40 shrink-0 flex items-center gap-2">
                <FlagBadge flag={f.flag} />
                <span className="text-[10px] text-neutral-500 uppercase tracking-wider">{SEVERITY_LABEL[f.severity]}</span>
              </div>
              <p className="text-neutral-400 leading-relaxed">{f.explanation}</p>
            </li>
          ))}
          <li className="text-[11px] text-neutral-500 pt-1">
            Amount = pyreals gained (+) or lost (-). Expected / actual are the bank balances the ledger compared.
          </li>
        </ul>
      )}
    </div>
  )
}

/** Small segmented toggle. */
export function Segmented<T extends string>({
  value,
  options,
  onChange,
}: {
  value: T
  options: { value: T; label: string }[]
  onChange: (v: T) => void
}) {
  return (
    <div className="inline-flex rounded-lg border border-neutral-800 bg-neutral-950 p-0.5">
      {options.map(o => (
        <button
          key={o.value}
          type="button"
          onClick={() => onChange(o.value)}
          className={`px-3 py-1 rounded-md text-xs font-semibold transition-colors ${
            value === o.value ? 'bg-blue-600 text-white' : 'text-neutral-400 hover:text-neutral-200'
          }`}
        >
          {o.label}
        </button>
      ))}
    </div>
  )
}
