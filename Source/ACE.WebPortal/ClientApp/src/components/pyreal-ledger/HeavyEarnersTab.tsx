import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import type { LedgerSuspectRow } from '../../types/pyrealLedger'
import { accountPath } from './ledgerUtils'
import {
  EmptyState,
  ErrorState,
  LINK_CLASS,
  LoadingState,
  Money,
  Panel,
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
import { ReviewCell, ShowReviewedToggle, ThresholdsControl, useSuspectRows } from './ReviewShared'

const OTHER = 'Other'

interface Group {
  source: string
  total: number
  users: number
  rows: LedgerSuspectRow[]
}

function groupBySource(rows: LedgerSuspectRow[]): Group[] {
  const map = new Map<string, Group>()
  for (const r of rows) {
    const key = r.topSource ?? OTHER
    let g = map.get(key)
    if (!g) {
      g = { source: key, total: r.topSource ? r.topSourceTotal : 0, users: r.topSource ? r.topSourceUsers : 0, rows: [] }
      map.set(key, g)
    }
    g.rows.push(r)
  }
  const groups = [...map.values()]
  for (const g of groups) g.rows.sort((a, b) => b.topSourceAmount - a.topSourceAmount)
  groups.sort((a, b) => {
    if (a.source === OTHER) return 1
    if (b.source === OTHER) return -1
    return b.total - a.total
  })
  return groups
}

/** Heavy earners: neutral list of accounts that earned a lot from one source, in one hour, or round the clock. */
export default function HeavyEarnersTab({ days }: { days: number }) {
  const navigate = useNavigate()
  const [showReviewed, setShowReviewed] = useState(false)
  const { rows: all, loading, error, failure, mark, unmark } = useSuspectRows(days)

  const heavy = all.filter(r => (r.highlights?.length ?? 0) > 0)
  const reviewedCount = heavy.filter(r => r.reviewed).length
  const groups = groupBySource(heavy.filter(r => showReviewed || !r.reviewed))

  return (
    <div className="space-y-4">
      <ThresholdsControl keys={['minAmount', 'sharePct', 'activePct', 'peakTimes']} />

      <Panel
        title="Heavy earners"
        help="Grouped by the single source each account earned the most from. A high share of a source says as much about the source as about the player."
        actions={<ShowReviewedToggle count={reviewedCount} checked={showReviewed} onChange={setShowReviewed} />}
      >
        <p className="px-4 pt-3 text-xs text-neutral-400">
          Accounts that earned unusually much from one source, in one hour, or round the clock. This points at a generous source as
          often as at a player: use it to decide what to rebalance, not whom to blame.
        </p>
        {failure && <ErrorState message={failure} />}
        {loading ? (
          <LoadingState />
        ) : error ? (
          <ErrorState message={error} />
        ) : groups.length === 0 ? (
          <EmptyState
            message={
              heavy.length > 0 ? 'Only reviewed accounts here. Turn on "Show reviewed" to see them.' : 'No heavy earners in this period.'
            }
          />
        ) : (
          <TableScroll>
            <table className={TABLE_CLASS}>
              <thead className={THEAD_CLASS}>
                <tr className="border-b border-neutral-800">
                  <th className={TH_CLASS}>Account</th>
                  <th className={TH_CLASS}>Highlights</th>
                  <th className={TH_NUM_CLASS} title="Earned minus spent, moves between players excluded">Created</th>
                  <th className={TH_NUM_CLASS} title="Share of this source that went to this account">Share of source</th>
                  <th className={TH_NUM_CLASS} title="The most this account earned in a single hour">Best hour</th>
                  <th className={TH_NUM_CLASS} title="Hours with activity, out of the hours in the period">Active hours</th>
                  <th className={TH_CLASS}>Review</th>
                </tr>
              </thead>
              <tbody className={TBODY_CLASS}>
                {groups.map(g => (
                  <GroupRows key={g.source} group={g} days={days} onOpen={id => navigate(accountPath(id, days))} mark={mark} unmark={unmark} />
                ))}
              </tbody>
            </table>
          </TableScroll>
        )}
      </Panel>
    </div>
  )
}

function GroupRows({
  group,
  days,
  onOpen,
  mark,
  unmark,
}: {
  group: Group
  days: number
  onOpen: (accountId: number) => void
  mark: (accountId: number, note: string) => Promise<void>
  unmark: (accountId: number) => Promise<void>
}) {
  const isOther = group.source === OTHER
  return (
    <>
      <tr className="bg-neutral-900/80 border-t border-neutral-800">
        <td colSpan={7} className="px-3 py-2 text-xs">
          <span className="font-semibold text-neutral-100">{group.source}</span>
          {!isOther && (
            <span className="ml-3 text-neutral-400">
              earned <Money value={group.total} /> across {group.users.toLocaleString()} account{group.users === 1 ? '' : 's'}
            </span>
          )}
        </td>
      </tr>
      {group.rows.map(row => (
        <tr
          key={row.accountId}
          className={`${ROW_CLICK_CLASS} align-top ${row.reviewed ? 'opacity-60' : ''}`}
          onClick={() => onOpen(row.accountId)}
        >
          <td className={`${TD_CLASS} min-w-[140px]`}>
            <Link to={accountPath(row.accountId, days)} className={LINK_CLASS} onClick={e => e.stopPropagation()}>
              {row.accountName || `#${row.accountId}`}
            </Link>
            <div className="text-[11px] text-neutral-500 max-w-[220px] truncate" title={row.characters}>
              {row.characters || '-'}
            </div>
          </td>
          <td className={`${TD_CLASS} min-w-[260px]`}>
            <ul className="space-y-0.5 text-neutral-200">
              {row.highlights.map((h, i) => (
                <li key={i}>{h}</li>
              ))}
            </ul>
          </td>
          <td className={TD_NUM_CLASS}>
            <Money value={row.created} signed />
          </td>
          <td className={TD_NUM_CLASS}>
            {row.topSource ? (
              <span title={`${row.topSourceAmount.toLocaleString()} of ${row.topSourceTotal.toLocaleString()} pyreals`}>
                {row.topSourceSharePct.toLocaleString(undefined, { maximumFractionDigits: 1 })}%
              </span>
            ) : (
              <span className="text-neutral-600">-</span>
            )}
          </td>
          <td className={TD_NUM_CLASS}>
            <Money value={row.peakHourAmount} />
          </td>
          <td className={`${TD_NUM_CLASS} tabular-nums`}>
            {row.activeHours.toLocaleString()} of {row.windowHours.toLocaleString()}
          </td>
          <td className={`${TD_CLASS} min-w-[200px]`}>
            <ReviewCell row={row} onMark={mark} onUnmark={unmark} />
          </td>
        </tr>
      ))}
    </>
  )
}
