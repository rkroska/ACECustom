import { Link, useNavigate } from 'react-router-dom'
import type { LedgerEarnerRow, LedgerEarnerScope } from '../../types/pyrealLedger'
import { accountPath, characterPath, sourceHelp, useLedgerFetch } from './ledgerUtils'
import {
  EmptyState,
  ErrorState,
  LINK_CLASS,
  LoadingState,
  Money,
  NetMoney,
  Panel,
  ROW_CLICK_CLASS,
  Segmented,
  TABLE_CLASS,
  TBODY_CLASS,
  TD_CLASS,
  TD_NUM_CLASS,
  THEAD_CLASS,
  TH_CLASS,
  TH_NUM_CLASS,
  TableScroll,
} from './LedgerShared'

export default function EarnersTab({
  days,
  by,
  onByChange,
}: {
  days: number
  by: LedgerEarnerScope
  onByChange: (by: LedgerEarnerScope) => void
}) {
  const { data, loading, error } = useLedgerFetch<LedgerEarnerRow[]>(`/earners?days=${days}&by=${by}&limit=100`)
  const navigate = useNavigate()
  const rows = data ?? []
  const pathFor = (row: LedgerEarnerRow) => (by === 'account' ? accountPath(row.id, days) : characterPath(row.id, days))

  return (
    <Panel
      title="Top earners"
      help="Who gained the most banked pyreals in this period, and where it came from. Big earners are not cheaters by default - check whether the sources make sense (an unusual vendor, Unattributed, or lots of Transfer in from other accounts)."
      actions={
        <Segmented<LedgerEarnerScope>
          value={by}
          onChange={onByChange}
          options={[
            { value: 'account', label: 'By account' },
            { value: 'character', label: 'By character' },
          ]}
        />
      }
    >
      {loading ? (
        <LoadingState />
      ) : error ? (
        <ErrorState message={error} />
      ) : rows.length === 0 ? (
        <EmptyState message="Nobody gained pyreals in this period." />
      ) : (
        <TableScroll>
          <table className={TABLE_CLASS}>
            <thead className={THEAD_CLASS}>
              <tr className="border-b border-neutral-800">
                <th className={TH_CLASS}>#</th>
                <th className={TH_CLASS}>{by === 'account' ? 'Account' : 'Character'}</th>
                {by === 'character' && <th className={TH_CLASS}>Account</th>}
                <th className={TH_NUM_CLASS}>Bank in</th>
                <th className={TH_NUM_CLASS}>Bank out</th>
                <th className={TH_NUM_CLASS}>Net</th>
                <th className={TH_CLASS}>Main sources (in)</th>
              </tr>
            </thead>
            <tbody className={TBODY_CLASS}>
              {rows.map((row, i) => {
                const top = [...(row.bySource ?? [])]
                  .filter(s => s.amountIn > 0)
                  .sort((a, b) => b.amountIn - a.amountIn)
                  .slice(0, 4)
                return (
                  <tr key={row.id} className={ROW_CLICK_CLASS} onClick={() => navigate(pathFor(row))}>
                    <td className={`${TD_CLASS} text-neutral-600`}>{i + 1}</td>
                    <td className={`${TD_CLASS} whitespace-nowrap`}>
                      <Link to={pathFor(row)} className={LINK_CLASS} onClick={e => e.stopPropagation()}>
                        {row.name || `#${row.id}`}
                      </Link>
                    </td>
                    {by === 'character' && (
                      <td className={`${TD_CLASS} text-neutral-400 whitespace-nowrap`}>{row.accountName || '-'}</td>
                    )}
                    <td className={TD_NUM_CLASS}>
                      <Money value={row.bankIn} className="text-emerald-300" />
                    </td>
                    <td className={TD_NUM_CLASS}>
                      <Money value={row.bankOut} className="text-neutral-400" />
                    </td>
                    <td className={`${TD_NUM_CLASS} font-semibold`}>
                      <NetMoney value={row.net} />
                    </td>
                    <td className={TD_CLASS}>
                      <div className="flex flex-wrap gap-1">
                        {top.length === 0 && <span className="text-neutral-600">-</span>}
                        {top.map(s => {
                          const share = row.bankIn > 0 ? Math.round((s.amountIn / row.bankIn) * 100) : 0
                          return (
                            <span
                              key={s.source}
                              title={`${sourceHelp(s.source)}: ${s.amountIn.toLocaleString()} pyreals in ${s.events.toLocaleString()} events`}
                              className={`px-1.5 py-0.5 rounded-md border text-[10px] whitespace-nowrap ${
                                s.source === 'Unattributed'
                                  ? 'bg-amber-500/15 border-amber-500/30 text-amber-300'
                                  : 'bg-neutral-800 border-neutral-700 text-neutral-300'
                              }`}
                            >
                              {s.source} {share}%
                            </span>
                          )
                        })}
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </TableScroll>
      )}
    </Panel>
  )
}
