import type { LedgerFlagRow } from '../../types/pyrealLedger'
import { FLAG_INFO, SEVERITY_LABEL, useLedgerFetch } from './ledgerUtils'
import { EmptyState, ErrorState, FlagLegend, LoadingState, Panel } from './LedgerShared'
import FlagsTable from './FlagsTable'

const LIMIT = 200

export default function FlagsTab({
  days,
  flag,
  onFlagChange,
}: {
  days: number
  flag: string
  onFlagChange: (flag: string) => void
}) {
  const qs = new URLSearchParams({ days: String(days), limit: String(LIMIT) })
  if (flag) qs.set('flag', flag)
  const { data, loading, error } = useLedgerFetch<LedgerFlagRow[]>(`/flags?${qs.toString()}`)
  const rows = data ?? []

  return (
    <div className="space-y-4">
      <FlagLegend defaultOpen />
      <Panel
        title="Flag events"
        help={`Every time the ledger saw pyreals appear or vanish in a way that does not add up. Newest first, up to ${LIMIT} rows.`}
        actions={
          <select
            value={flag}
            onChange={e => onFlagChange(e.target.value)}
            className="px-3 py-1.5 rounded-lg bg-neutral-950 border border-neutral-800 text-xs text-white focus:outline-none focus:border-blue-500/50"
            aria-label="Filter by flag type"
          >
            <option value="">All flag types</option>
            {FLAG_INFO.map(f => (
              <option key={f.flag} value={f.flag}>
                {f.label} ({SEVERITY_LABEL[f.severity]})
              </option>
            ))}
          </select>
        }
      >
        {loading ? (
          <LoadingState />
        ) : error ? (
          <ErrorState message={error} />
        ) : rows.length === 0 ? (
          <EmptyState message={flag ? 'No flags of this type in this period.' : 'No flags in this period.'} />
        ) : (
          <>
            {rows.length >= LIMIT && (
              <div className="px-4 py-2 text-[11px] text-amber-300/90 border-b border-neutral-800">
                Showing the newest {LIMIT}. Narrow the period or pick a flag type to see older ones.
              </div>
            )}
            <FlagsTable rows={rows} days={days} />
          </>
        )}
      </Panel>
    </div>
  )
}
