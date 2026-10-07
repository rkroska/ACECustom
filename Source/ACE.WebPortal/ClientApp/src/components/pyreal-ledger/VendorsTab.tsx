import { Fragment } from 'react'
import { Link } from 'react-router-dom'
import { ChevronDown, ChevronRight } from 'lucide-react'
import type { LedgerVendorRow, LedgerVendorSellerRow } from '../../types/pyrealLedger'
import { accountPath, characterPath, formatDateTime, useLedgerFetch } from './ledgerUtils'
import {
  EmptyState,
  ErrorState,
  InfoNote,
  LINK_CLASS,
  LoadingState,
  Money,
  MultiplierBadge,
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

export default function VendorsTab({
  days,
  openWcid,
  onToggle,
}: {
  days: number
  openWcid: number | null
  onToggle: (wcid: number | null) => void
}) {
  const { data, loading, error } = useLedgerFetch<LedgerVendorRow[]>(`/vendors?days=${days}`)
  const rows = data ?? []
  const overpaying = rows.filter(r => (r.buyPriceNow ?? 0) > 1)

  return (
    <div className="space-y-4">
      {overpaying.length > 0 && (
        <InfoNote tone="warn">
          {overpaying.length === 1 ? '1 vendor currently pays' : `${overpaying.length} vendors currently pay`} more than items are
          worth (multiplier above 1x): {overpaying.map(v => v.vendorName).join(', ')}. Anyone selling to them is printing money -
          fix the vendor's buy price first, then look at who used it.
        </InfoNote>
      )}
      <Panel
        title="Vendor payouts"
        help="Pyreals paid out by each vendor for items players sold. The multiplier is what the vendor pays right now relative to item value - normal is 1x or less. Click a vendor to see who sold to it."
      >
        {loading ? (
          <LoadingState />
        ) : error ? (
          <ErrorState message={error} />
        ) : rows.length === 0 ? (
          <EmptyState message="No vendor sales in this period." />
        ) : (
          <TableScroll>
            <table className={TABLE_CLASS}>
              <thead className={THEAD_CLASS}>
                <tr className="border-b border-neutral-800">
                  <th className={TH_CLASS}>Vendor</th>
                  <th className={TH_CLASS}>WCID</th>
                  <th className={TH_CLASS} title="What the vendor pays now, relative to item value. Above 1x prints money.">Buys at</th>
                  <th className={TH_NUM_CLASS}>Total paid</th>
                  <th className={TH_NUM_CLASS}>Sales</th>
                  <th className={TH_NUM_CLASS}>Characters</th>
                  <th className={TH_NUM_CLASS}>Accounts</th>
                </tr>
              </thead>
              <tbody className={TBODY_CLASS}>
                {rows.map(row => {
                  const open = openWcid === row.vendorWcid
                  return (
                    <Fragment key={row.vendorWcid}>
                      <tr className={`${ROW_CLICK_CLASS} ${open ? 'bg-neutral-800/40' : ''}`} onClick={() => onToggle(open ? null : row.vendorWcid)}>
                        <td className={`${TD_CLASS} whitespace-nowrap`}>
                          <span className="inline-flex items-center gap-1.5 text-neutral-100 font-medium">
                            {open ? <ChevronDown className="w-3.5 h-3.5" /> : <ChevronRight className="w-3.5 h-3.5 text-neutral-500" />}
                            {row.vendorName || 'Unknown vendor'}
                          </span>
                        </td>
                        <td className={`${TD_CLASS} font-mono text-neutral-500`}>{row.vendorWcid}</td>
                        <td className={TD_CLASS}>
                          <MultiplierBadge multiplier={row.buyPriceNow} />
                        </td>
                        <td className={`${TD_NUM_CLASS} font-semibold`}>
                          <Money value={row.payout} />
                        </td>
                        <td className={TD_NUM_CLASS}>{row.sales.toLocaleString()}</td>
                        <td className={TD_NUM_CLASS}>{row.characters.toLocaleString()}</td>
                        <td className={TD_NUM_CLASS}>{row.accounts.toLocaleString()}</td>
                      </tr>
                      {open && (
                        <tr>
                          <td colSpan={7} className="bg-neutral-950/60 px-3 py-3">
                            <VendorSellers wcid={row.vendorWcid} days={days} />
                          </td>
                        </tr>
                      )}
                    </Fragment>
                  )
                })}
              </tbody>
            </table>
          </TableScroll>
        )}
      </Panel>
    </div>
  )
}

function VendorSellers({ wcid, days }: { wcid: number; days: number }) {
  const { data, loading, error } = useLedgerFetch<LedgerVendorSellerRow[]>(`/vendors/${wcid}?days=${days}`)
  const rows = data ?? []

  if (loading) return <LoadingState />
  if (error) return <ErrorState message={error} />
  if (rows.length === 0) return <EmptyState message="No sellers recorded for this vendor in this period." />

  return (
    <div className="rounded-xl border border-neutral-800 overflow-hidden">
      <div className="px-3 py-2 text-[10px] font-bold text-neutral-500 uppercase tracking-widest border-b border-neutral-800">
        Who sold to this vendor
      </div>
      <TableScroll>
        <table className={TABLE_CLASS}>
          <thead className={THEAD_CLASS}>
            <tr className="border-b border-neutral-800">
              <th className={TH_CLASS}>Character</th>
              <th className={TH_CLASS}>Account</th>
              <th className={TH_NUM_CLASS}>Paid</th>
              <th className={TH_NUM_CLASS}>Sales</th>
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
                <td className={`${TD_NUM_CLASS} font-semibold`}>
                  <Money value={r.payout} />
                </td>
                <td className={TD_NUM_CLASS}>{r.sales.toLocaleString()}</td>
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
