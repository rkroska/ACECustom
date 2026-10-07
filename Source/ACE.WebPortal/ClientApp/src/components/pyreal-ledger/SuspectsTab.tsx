import { Link, useNavigate } from 'react-router-dom'
import type { LedgerSuspectRow } from '../../types/pyrealLedger'
import { accountPath, useLedgerFetch } from './ledgerUtils'
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

export default function SuspectsTab({ days }: { days: number }) {
  const { data, loading, error } = useLedgerFetch<LedgerSuspectRow[]>(`/suspects?days=${days}&limit=100`)
  const navigate = useNavigate()
  const rows = data ?? []

  return (
    <div className="space-y-4">
      <FlagLegend />
      <Panel
        title="Suspect accounts"
        help={
          <>
            Accounts ranked by a suspicion score: pyreals tied to flags + currency surplus (if positive) + bank gains from
            unlabeled code paths. Higher score = look here first. A high score is a lead, not proof. Click a row to open the
            account.
            <span className="block mt-1">
              Currency surplus = coins, trade notes and peas the account got rid of beyond what it held when tracking began plus
              everything it was seen receiving. It is an all-time number and does not change with the period picker.
            </span>
          </>
        }
      >
        {loading ? (
          <LoadingState />
        ) : error ? (
          <ErrorState message={error} />
        ) : rows.length === 0 ? (
          <EmptyState message="No suspicious accounts in this period." />
        ) : (
          <TableScroll>
            <table className={TABLE_CLASS}>
              <thead className={THEAD_CLASS}>
                <tr className="border-b border-neutral-800">
                  <th className={TH_CLASS}>Account</th>
                  <th className={TH_CLASS}>Characters</th>
                  <th className={TH_NUM_CLASS} title="Flagged amount + positive currency surplus + unattributed gains">Score</th>
                  <th className={TH_NUM_CLASS} title="Pyreals tied to ledger flags">Flagged</th>
                  <th className={TH_NUM_CLASS} title="All time, not just this period: currency the account got rid of beyond what it held when tracking began plus everything it was seen receiving.">Currency surplus (all time)</th>
                  <th className={TH_NUM_CLASS} title="Bank gains from a code path with no label">Unattributed</th>
                  <th className={TH_NUM_CLASS} title="All bank credits in this period">Bank in</th>
                  <th className={TH_CLASS} title="The vendor that paid this account the most">Top vendor</th>
                  <th className={TH_CLASS}>Flags</th>
                </tr>
              </thead>
              <tbody className={TBODY_CLASS}>
                {rows.map(row => (
                  <tr key={row.accountId} className={ROW_CLICK_CLASS} onClick={() => navigate(accountPath(row.accountId, days))}>
                    <td className={`${TD_CLASS} whitespace-nowrap`}>
                      <Link to={accountPath(row.accountId, days)} className={LINK_CLASS} onClick={e => e.stopPropagation()}>
                        {row.accountName || `#${row.accountId}`}
                      </Link>
                    </td>
                    <td className={`${TD_CLASS} text-neutral-400 max-w-[220px] truncate`} title={row.characters}>
                      {row.characters || '-'}
                    </td>
                    <td className={`${TD_NUM_CLASS} font-bold text-white`}>
                      <Money value={row.score} />
                    </td>
                    <td className={TD_NUM_CLASS}>
                      {row.flaggedAmount ? <Money value={row.flaggedAmount} className="text-red-300" /> : <span className="text-neutral-600">0</span>}
                    </td>
                    <td className={TD_NUM_CLASS}>
                      <SurplusMoney value={row.currencySurplus} />
                    </td>
                    <td className={TD_NUM_CLASS}>
                      {row.unattributedIn ? <Money value={row.unattributedIn} className="text-amber-300" /> : <span className="text-neutral-600">0</span>}
                    </td>
                    <td className={TD_NUM_CLASS}>
                      <Money value={row.bankIn} />
                    </td>
                    <td className={`${TD_CLASS} whitespace-nowrap`}>
                      {row.topVendor ? (
                        <>
                          <span className="text-neutral-300">{row.topVendor}</span>{' '}
                          <Money value={row.topVendorPayout} className="text-neutral-500" />
                        </>
                      ) : (
                        <span className="text-neutral-600">-</span>
                      )}
                    </td>
                    <td className={TD_CLASS}>
                      <FlagBadgeList flags={row.flags} />
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
