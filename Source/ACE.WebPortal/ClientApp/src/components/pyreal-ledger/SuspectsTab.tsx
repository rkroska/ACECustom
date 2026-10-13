import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { accountPath } from './ledgerUtils'
import {
  EmptyState,
  ErrorState,
  FlagBadgeList,
  FlagLegend,
  LINK_CLASS,
  LoadingState,
  Money,
  SurplusMoney,
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

/** "Doesn't add up": integrity problems only. Heavy earners live on their own tab. */
export default function SuspectsTab({ days }: { days: number }) {
  const navigate = useNavigate()
  const [showReviewed, setShowReviewed] = useState(false)
  const { rows: all, loading, error, failure, mark, unmark } = useSuspectRows(days)

  const withReasons = all.filter(r => (r.reasons?.length ?? 0) > 0)
  const reviewedCount = withReasons.filter(r => r.reviewed).length
  const rows = withReasons.filter(r => showReviewed || !r.reviewed || r.newSinceReview)

  return (
    <div className="space-y-4">
      <FlagLegend />

      <ThresholdsControl keys={['minSurplus']} />

      <Panel
        title="Doesn't add up"
        help={
          <>
            Pyreals with no recorded reason: dupe flags, coins or notes disposed of beyond what was ever received, and bank gains
            from unlabeled code. Being paid a lot by an NPC or vendor is not listed here. A reason is a lead, not proof. Click a
            row to open the account.
            <span className="block mt-1">
              Currency surplus = coins, trade notes and peas the account got rid of beyond what it held when tracking began plus
              everything it was seen receiving. It is an all-time number and does not change with the period picker.
            </span>
          </>
        }
        actions={<ShowReviewedToggle count={reviewedCount} checked={showReviewed} onChange={setShowReviewed} />}
      >
        <p className="px-4 pt-3 text-xs text-neutral-400">
          Pyreals with no recorded reason: dupe flags, coins or notes disposed of beyond what was ever received, and bank gains from
          unlabeled code. Being paid a lot by an NPC or vendor is not listed here.
        </p>
        {failure && <ErrorState message={failure} />}
        {loading ? (
          <LoadingState />
        ) : error ? (
          <ErrorState message={error} />
        ) : rows.length === 0 ? (
          <EmptyState
            message={
              withReasons.length > reviewedCount || reviewedCount === 0
                ? 'Nothing to report: every pyreal in this window has a recorded reason.'
                : 'Nothing to report except reviewed accounts. Turn on "Show reviewed" to see them.'
            }
          />
        ) : (
          <TableScroll>
            <table className={TABLE_CLASS}>
              <thead className={THEAD_CLASS}>
                <tr className="border-b border-neutral-800">
                  <th className={TH_CLASS}>Account</th>
                  <th className={TH_CLASS}>Reasons</th>
                  <th className={TH_CLASS}>Flags</th>
                  <th className={TH_NUM_CLASS} title="Coins, notes and peas disposed of beyond what was received (all time)">Surplus</th>
                  <th className={TH_NUM_CLASS} title="Earned minus spent, moves between players excluded">Created</th>
                  <th className={TH_CLASS}>Review</th>
                </tr>
              </thead>
              <tbody className={TBODY_CLASS}>
                {rows.map(row => (
                  <tr
                    key={row.accountId}
                    className={`${ROW_CLICK_CLASS} align-top ${row.reviewed && !row.newSinceReview ? 'opacity-60' : ''}`}
                    onClick={() => navigate(accountPath(row.accountId, days))}
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
                      <div className="flex items-start gap-2">
                        <span
                          className="px-1.5 py-0.5 rounded-md border text-[10px] font-bold tabular-nums bg-amber-500/15 text-amber-300 border-amber-500/30"
                          title={`${row.score} reason${row.score === 1 ? '' : 's'}`}
                        >
                          {row.reasons.length}
                        </span>
                        <ul className="space-y-0.5 text-neutral-200">
                          {row.reasons.map((r, i) => (
                            <li key={i}>{r}</li>
                          ))}
                        </ul>
                      </div>
                    </td>
                    <td className={`${TD_CLASS} text-[11px] min-w-[160px]`}>
                      <FlagBadgeList flags={row.flags} />
                      <div className="mt-1 text-neutral-500">
                        flagged {row.flaggedAmount ? <Money value={row.flaggedAmount} className="text-red-300" /> : <span className="text-neutral-600">0</span>}
                        {row.unattributedIn ? (
                          <>
                            {' '}
                            - unattributed <Money value={row.unattributedIn} className="text-amber-300" />
                          </>
                        ) : null}
                      </div>
                    </td>
                    <td className={TD_NUM_CLASS}>
                      <SurplusMoney value={row.currencySurplus} />
                    </td>
                    <td className={TD_NUM_CLASS}>
                      <Money value={row.created} signed />
                    </td>
                    <td className={`${TD_CLASS} min-w-[200px]`}>
                      <ReviewCell row={row} onMark={mark} onUnmark={unmark} showNewBadge />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TableScroll>
        )}
      </Panel>
    </div>
  )
}
