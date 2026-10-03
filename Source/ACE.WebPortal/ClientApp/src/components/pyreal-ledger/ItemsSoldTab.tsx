import { Fragment } from 'react'
import { Link } from 'react-router-dom'
import { ChevronDown, ChevronRight } from 'lucide-react'
import type { LedgerItemSellerRow, LedgerItemSoldRow } from '../../types/pyrealLedger'
import { accountPath, characterPath, formatDateTime, useLedgerFetch } from './ledgerUtils'
import {
  EmptyState,
  ErrorState,
  LINK_CLASS,
  LoadingState,
  Money,
  Panel,
  ROW_CLASS,
  ROW_CLICK_CLASS,
  TABLE_CLASS,
  TBODY_CLASS,
  TD_CLASS,
  TD_NUM_CLASS,
  THEAD_CLASS,
  TH_CLASS,
  TH_NUM_CLASS,
  TableScroll,
} from './LedgerShared'

const LIMIT = 200

function sharePct(part: number, total: number): number {
  return total > 0 ? Math.round((part / total) * 100) : 0
}

export default function ItemsSoldTab({
  days,
  openWcid,
  onToggle,
}: {
  days: number
  openWcid: number | null
  onToggle: (wcid: number | null) => void
}) {
  const { data, loading, error } = useLedgerFetch<LedgerItemSoldRow[]>(`/items?days=${days}&limit=${LIMIT}`)
  const rows = data ?? []
  const openMissing = openWcid !== null && !loading && !error && !rows.some(r => r.wcid === openWcid)

  return (
    <Panel
      title="Items sold to vendors"
      help="A character selling far more of one item than could plausibly drop for them is the sign of a duplicated stack. Compare the top seller's quantity with everyone else's. Click an item to see every character who sold it."
    >
      {loading ? (
        <LoadingState />
      ) : error ? (
        <ErrorState message={error} />
      ) : rows.length === 0 ? (
        <EmptyState message="No items sold to vendors in this period." />
      ) : (
        <TableScroll>
          <table className={TABLE_CLASS}>
            <thead className={THEAD_CLASS}>
              <tr className="border-b border-neutral-800">
                <th className={TH_CLASS}>Item</th>
                <th className={TH_CLASS}>WCID</th>
                <th className={TH_NUM_CLASS}>Quantity sold</th>
                <th className={TH_NUM_CLASS}>Paid</th>
                <th className={TH_NUM_CLASS}>Characters</th>
                <th className={TH_NUM_CLASS}>Accounts</th>
                <th className={TH_CLASS} title="The character who sold the most of this item, and their share of the total quantity">Top seller</th>
              </tr>
            </thead>
            <tbody className={TBODY_CLASS}>
              {rows.map(row => {
                const open = openWcid === row.wcid
                const share = sharePct(row.topSellerUnits, row.units)
                // One seller holding most of the volume while others sold it too is the pattern to look at.
                const lopsided = share >= 50 && row.characters > 1
                return (
                  <Fragment key={row.wcid}>
                    <tr className={`${ROW_CLICK_CLASS} ${open ? 'bg-neutral-800/40' : ''}`} onClick={() => onToggle(open ? null : row.wcid)}>
                      <td className={`${TD_CLASS} whitespace-nowrap`}>
                        <span className="inline-flex items-center gap-1.5 text-neutral-100 font-medium">
                          {open ? <ChevronDown className="w-3.5 h-3.5" /> : <ChevronRight className="w-3.5 h-3.5 text-neutral-500" />}
                          {row.itemName || `WCID ${row.wcid}`}
                        </span>
                      </td>
                      <td className={`${TD_CLASS} font-mono text-neutral-500`}>{row.wcid}</td>
                      <td className={`${TD_NUM_CLASS} font-semibold`}>
                        <span title={row.units.toLocaleString()}>{row.units.toLocaleString()}</span>
                      </td>
                      <td className={TD_NUM_CLASS}>
                        <Money value={row.payout} />
                      </td>
                      <td className={TD_NUM_CLASS}>{row.characters.toLocaleString()}</td>
                      <td className={TD_NUM_CLASS}>{row.accounts.toLocaleString()}</td>
                      <td className={`${TD_CLASS} whitespace-nowrap`}>
                        {row.topSeller && row.topSellerCharId ? (
                          <span className="inline-flex items-center gap-2">
                            <Link to={characterPath(row.topSellerCharId, days)} className={LINK_CLASS} onClick={e => e.stopPropagation()}>
                              {row.topSeller}
                            </Link>
                            <span
                              className={`px-1.5 py-0.5 rounded-md border text-[10px] font-bold tabular-nums ${
                                lopsided ? 'bg-amber-500/15 text-amber-300 border-amber-500/30' : 'bg-neutral-800 text-neutral-400 border-neutral-700'
                              }`}
                              title={`${row.topSellerUnits.toLocaleString()} of ${row.units.toLocaleString()} sold`}
                            >
                              {share}%
                            </span>
                          </span>
                        ) : (
                          <span className="text-neutral-600">-</span>
                        )}
                      </td>
                    </tr>
                    {open && (
                      <tr>
                        <td colSpan={7} className="bg-neutral-950/60 px-3 py-3">
                          <ItemSellers wcid={row.wcid} totalUnits={row.units} days={days} />
                        </td>
                      </tr>
                    )}
                  </Fragment>
                )
              })}
            </tbody>
          </table>
          {openMissing && openWcid !== null && (
            <div className="border-t border-neutral-800 px-3 py-3">
              <div className="text-xs text-neutral-400 mb-2">WCID {openWcid} is not in the top {LIMIT} items for this period. Its sellers:</div>
              <ItemSellers wcid={openWcid} totalUnits={null} days={days} />
            </div>
          )}
        </TableScroll>
      )}
    </Panel>
  )
}

function ItemSellers({ wcid, totalUnits, days }: { wcid: number; totalUnits: number | null; days: number }) {
  const { data, loading, error } = useLedgerFetch<LedgerItemSellerRow[]>(`/items/${wcid}?days=${days}`)
  const rows = data ?? []

  if (loading) return <LoadingState />
  if (error) return <ErrorState message={error} />
  if (rows.length === 0) return <EmptyState message="No sellers recorded for this item in this period." />

  const total = totalUnits ?? rows.reduce((s, r) => s + r.units, 0)

  return (
    <div className="rounded-xl border border-neutral-800 overflow-hidden">
      <div className="px-3 py-2 text-[10px] font-bold text-neutral-500 uppercase tracking-widest border-b border-neutral-800">
        Who sold this item
      </div>
      <TableScroll>
        <table className={TABLE_CLASS}>
          <thead className={THEAD_CLASS}>
            <tr className="border-b border-neutral-800">
              <th className={TH_CLASS}>Character</th>
              <th className={TH_CLASS}>Account</th>
              <th className={TH_NUM_CLASS}>Quantity</th>
              <th className={TH_NUM_CLASS}>Share</th>
              <th className={TH_NUM_CLASS}>Paid</th>
              <th className={TH_CLASS}>Vendors</th>
              <th className={TH_CLASS}>First seen</th>
              <th className={TH_CLASS}>Last seen</th>
            </tr>
          </thead>
          <tbody className={TBODY_CLASS}>
            {rows.map(r => (
              <tr key={r.charId} className={ROW_CLASS}>
                <td className={`${TD_CLASS} whitespace-nowrap`}>
                  <Link to={characterPath(r.charId, days)} className={LINK_CLASS}>
                    {r.charName || `#${r.charId}`}
                  </Link>
                </td>
                <td className={`${TD_CLASS} whitespace-nowrap`}>
                  <Link to={accountPath(r.accountId, days)} className={LINK_CLASS}>
                    {r.accountName || `#${r.accountId}`}
                  </Link>
                </td>
                <td className={`${TD_NUM_CLASS} font-semibold`}>{r.units.toLocaleString()}</td>
                <td className={`${TD_NUM_CLASS} text-neutral-400`}>{sharePct(r.units, total)}%</td>
                <td className={TD_NUM_CLASS}>
                  <Money value={r.payout} />
                </td>
                <td className={`${TD_CLASS} text-neutral-400 max-w-[240px] truncate`} title={r.vendors}>
                  {r.vendors || '-'}
                </td>
                <td className={`${TD_CLASS} whitespace-nowrap text-neutral-400`}>{formatDateTime(r.firstHourUtc)}</td>
                <td className={`${TD_CLASS} whitespace-nowrap text-neutral-400`}>{formatDateTime(r.lastHourUtc)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </TableScroll>
    </div>
  )
}
