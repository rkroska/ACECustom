import { Link } from 'react-router-dom'
import type { LedgerFlagRow } from '../../types/pyrealLedger'
import { accountPath, characterPath, flagInfo, formatDateTime } from './ledgerUtils'
import {
  FlagBadge,
  LINK_CLASS,
  Money,
  NetMoney,
  ROW_CLASS,
  TABLE_CLASS,
  TBODY_CLASS,
  TD_CLASS,
  TD_NUM_CLASS,
  THEAD_CLASS,
  TH_CLASS,
  TH_NUM_CLASS,
  TableScroll,
} from './LedgerShared'

/** Flag events. Hide the character / account columns when the table already sits on that detail page. */
export default function FlagsTable({
  rows,
  days,
  showCharacter = true,
  showAccount = true,
}: {
  rows: LedgerFlagRow[]
  days: number
  showCharacter?: boolean
  showAccount?: boolean
}) {
  return (
    <TableScroll>
      <table className={TABLE_CLASS}>
        <thead className={THEAD_CLASS}>
          <tr className="border-b border-neutral-800">
            <th className={TH_CLASS}>When</th>
            {showCharacter && <th className={TH_CLASS}>Character</th>}
            {showAccount && <th className={TH_CLASS}>Account</th>}
            <th className={TH_CLASS}>Flag</th>
            <th className={TH_NUM_CLASS} title="Pyreals gained (+) or lost (-)">Amount</th>
            <th className={TH_NUM_CLASS} title="The balance the ledger expected">Expected</th>
            <th className={TH_NUM_CLASS} title="The balance actually found">Actual</th>
            <th className={TH_CLASS}>What happened</th>
          </tr>
        </thead>
        <tbody className={TBODY_CLASS}>
          {rows.map(row => (
            <tr key={row.id} className={`${ROW_CLASS} align-top`}>
              <td className={`${TD_CLASS} whitespace-nowrap text-neutral-400`}>{formatDateTime(row.utc)}</td>
              {showCharacter && (
                <td className={`${TD_CLASS} whitespace-nowrap`}>
                  <Link to={characterPath(row.charId, days)} className={LINK_CLASS}>
                    {row.charName || `#${row.charId}`}
                  </Link>
                </td>
              )}
              {showAccount && (
                <td className={`${TD_CLASS} whitespace-nowrap`}>
                  {row.accountId ? (
                    <Link to={accountPath(row.accountId, days)} className={LINK_CLASS}>
                      {row.accountName || `#${row.accountId}`}
                    </Link>
                  ) : (
                    <span className="text-neutral-600">-</span>
                  )}
                </td>
              )}
              <td className={TD_CLASS}>
                <FlagBadge flag={row.flag} />
              </td>
              <td className={`${TD_NUM_CLASS} font-semibold`}>
                <NetMoney value={row.amount} />
              </td>
              <td className={TD_NUM_CLASS}>
                <Money value={row.expected} className="text-neutral-400" />
              </td>
              <td className={TD_NUM_CLASS}>
                <Money value={row.actual} className="text-neutral-400" />
              </td>
              <td className={`${TD_CLASS} min-w-[260px] max-w-[520px]`}>
                <p className="text-neutral-400 leading-snug">{flagInfo(row.flag).explanation}</p>
                {row.detail && <FlagDetail text={row.detail} />}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </TableScroll>
  )
}

function FlagDetail({ text }: { text: string }) {
  const firstLine = text.split('\n')[0]
  const multiLine = text.includes('\n') || text.length > 160
  if (!multiLine) return <p className="mt-1 text-neutral-300 break-words">{text}</p>
  return (
    <details className="mt-1 group">
      <summary className="cursor-pointer text-neutral-300 hover:text-white break-words">
        {firstLine.length > 160 ? `${firstLine.slice(0, 160)}...` : firstLine}
        <span className="ml-1 text-[10px] text-blue-400 group-open:hidden">(show details)</span>
      </summary>
      <pre className="mt-1 p-2 rounded-lg bg-neutral-950 border border-neutral-800 text-[10px] text-neutral-400 whitespace-pre-wrap break-all max-h-64 overflow-auto">
        {text}
      </pre>
    </details>
  )
}
