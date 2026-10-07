import type { LedgerFetchState } from './ledgerUtils'
import { flagInfo, sortFlagsBySeverity, SEVERITY_CLASSES } from './ledgerUtils'
import type { LedgerOverview } from '../../types/pyrealLedger'
import {
  EmptyState,
  ErrorState,
  LoadingState,
  Money,
  NetMoney,
  Panel,
  ROW_CLASS,
  SourceLabel,
  StatCard,
  TABLE_CLASS,
  TBODY_CLASS,
  TD_CLASS,
  TD_NUM_CLASS,
  THEAD_CLASS,
  TH_CLASS,
  TH_NUM_CLASS,
  TableScroll,
} from './LedgerShared'

export default function OverviewTab({
  overview,
  onFlagClick,
}: {
  overview: LedgerFetchState<LedgerOverview>
  onFlagClick: (flag: string) => void
}) {
  const { data, loading, error } = overview
  if (loading) return <LoadingState />
  if (error) return <ErrorState message={error} />
  if (!data) return <EmptyState message="No data." />

  const bank = [...(data.bankTotals ?? [])].sort((a, b) => b.amountIn + b.amountOut - (a.amountIn + a.amountOut))
  const bankIn = bank.reduce((s, r) => s + r.amountIn, 0)
  const bankOut = bank.reduce((s, r) => s + r.amountOut, 0)
  const maxBank = Math.max(1, ...bank.map(r => Math.max(r.amountIn, r.amountOut)))

  const itemSources = new Map<string, { source: string; inValue: number; outValue: number; inUnits: number; outUnits: number }>()
  for (const it of data.itemTotals ?? []) {
    const row = itemSources.get(it.source) ?? { source: it.source, inValue: 0, outValue: 0, inUnits: 0, outUnits: 0 }
    if (it.direction === 'in') {
      row.inValue += it.value
      row.inUnits += it.units
    } else {
      row.outValue += it.value
      row.outUnits += it.units
    }
    itemSources.set(it.source, row)
  }
  const items = [...itemSources.values()].sort((a, b) => b.inValue + b.outValue - (a.inValue + a.outValue))
  const itemIn = items.reduce((s, r) => s + r.inValue, 0)
  const itemOut = items.reduce((s, r) => s + r.outValue, 0)

  const flags = sortFlagsBySeverity(data.flagCounts ?? [])

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <StatCard label="Bank credits" value={<Money value={bankIn} />} hint="Pyreals added to banks" tone="good" />
        <StatCard label="Bank debits" value={<Money value={bankOut} />} hint="Pyreals removed from banks" />
        <StatCard label="Net change" value={<NetMoney value={bankIn - bankOut} />} hint="Positive = more pyreals in the economy" />
        <StatCard
          label="Flag events"
          value={flags.reduce((s, f) => s + f.count, 0).toLocaleString()}
          hint="See the breakdown below"
          tone={flags.some(f => ['critical', 'high'].includes(flagInfo(f.flag).severity) && f.count > 0) ? 'bad' : 'default'}
        />
      </div>

      <Panel title="Flags" help="How many times each flag fired and the pyreals involved. Click one to list those events.">
        {flags.length === 0 ? (
          <EmptyState message="No flags in this period." />
        ) : (
          <div className="p-3 grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-2">
            {flags.map(f => {
              const info = flagInfo(f.flag)
              return (
                <button
                  key={f.flag}
                  type="button"
                  onClick={() => onFlagClick(f.flag)}
                  title={info.explanation}
                  className={`text-left rounded-xl border px-3 py-2 hover:brightness-125 transition ${SEVERITY_CLASSES[info.severity]}`}
                >
                  <div className="text-xs font-bold">{info.label}</div>
                  <div className="flex items-baseline justify-between gap-2 mt-1">
                    <span className="text-lg font-black text-white">{f.count.toLocaleString()}</span>
                    <NetMoney value={f.amount} />
                  </div>
                </button>
              )
            })}
          </div>
        )}
      </Panel>

      <Panel title="Bank by source" help="Where banked pyreals came from (in) and went to (out). Hover a source name for what it means.">
        {bank.length === 0 ? (
          <EmptyState message="No bank activity in this period." />
        ) : (
          <TableScroll>
            <table className={TABLE_CLASS}>
              <thead className={THEAD_CLASS}>
                <tr className="border-b border-neutral-800">
                  <th className={TH_CLASS}>Source</th>
                  <th className={TH_NUM_CLASS}>In</th>
                  <th className={TH_NUM_CLASS}>Out</th>
                  <th className={TH_NUM_CLASS}>Net</th>
                  <th className={TH_NUM_CLASS}>Events</th>
                  <th className={`${TH_CLASS} w-1/4 hidden md:table-cell`}>Share</th>
                </tr>
              </thead>
              <tbody className={TBODY_CLASS}>
                {bank.map(r => (
                  <tr key={r.source} className={ROW_CLASS}>
                    <td className={TD_CLASS}>
                      <SourceLabel source={r.source} />
                    </td>
                    <td className={TD_NUM_CLASS}>
                      <Money value={r.amountIn} className="text-emerald-300" />
                    </td>
                    <td className={TD_NUM_CLASS}>
                      <Money value={r.amountOut} className="text-neutral-400" />
                    </td>
                    <td className={TD_NUM_CLASS}>
                      <NetMoney value={r.amountIn - r.amountOut} />
                    </td>
                    <td className={TD_NUM_CLASS}>{r.events.toLocaleString()}</td>
                    <td className={`${TD_CLASS} hidden md:table-cell`}>
                      <div className="space-y-0.5">
                        <div className="h-1.5 rounded-full bg-emerald-500/70" style={{ width: `${(r.amountIn / maxBank) * 100}%` }} />
                        <div className="h-1.5 rounded-full bg-red-500/60" style={{ width: `${(r.amountOut / maxBank) * 100}%` }} />
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TableScroll>
        )}
      </Panel>

      <Panel
        title="Currency items by source"
        help="Coins, trade notes and peas at face value. In = currency players received; out = currency they gave up (deposited, sold, gave away)."
      >
        {items.length === 0 ? (
          <EmptyState message="No currency item movement in this period." />
        ) : (
          <TableScroll>
            <table className={TABLE_CLASS}>
              <thead className={THEAD_CLASS}>
                <tr className="border-b border-neutral-800">
                  <th className={TH_CLASS}>Source</th>
                  <th className={TH_NUM_CLASS}>Value in</th>
                  <th className={TH_NUM_CLASS}>Units in</th>
                  <th className={TH_NUM_CLASS}>Value out</th>
                  <th className={TH_NUM_CLASS}>Units out</th>
                </tr>
              </thead>
              <tbody className={TBODY_CLASS}>
                {items.map(r => (
                  <tr key={r.source} className={ROW_CLASS}>
                    <td className={TD_CLASS}>
                      <SourceLabel source={r.source} />
                    </td>
                    <td className={TD_NUM_CLASS}>
                      <Money value={r.inValue} className="text-emerald-300" />
                    </td>
                    <td className={`${TD_NUM_CLASS} text-neutral-500`}>{r.inUnits.toLocaleString()}</td>
                    <td className={TD_NUM_CLASS}>
                      <Money value={r.outValue} className="text-neutral-400" />
                    </td>
                    <td className={`${TD_NUM_CLASS} text-neutral-500`}>{r.outUnits.toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
              <tfoot className="border-t border-neutral-700 text-neutral-200 font-semibold">
                <tr>
                  <td className={TD_CLASS}>Total</td>
                  <td className={TD_NUM_CLASS}>
                    <Money value={itemIn} />
                  </td>
                  <td />
                  <td className={TD_NUM_CLASS}>
                    <Money value={itemOut} />
                  </td>
                  <td className={`${TD_NUM_CLASS} text-[11px]`} title="Value out minus value in, for this period only.">
                    Net out this period <NetMoney value={itemOut - itemIn} />
                  </td>
                </tr>
              </tfoot>
            </table>
          </TableScroll>
        )}
      </Panel>
    </div>
  )
}
