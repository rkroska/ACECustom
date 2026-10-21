import { useState } from 'react'
import { Link } from 'react-router-dom'
import type { LedgerTransfers } from '../../types/pyrealLedger'
import { accountPath, useLedgerFetch } from './ledgerUtils'
import {
  EmptyState,
  ErrorState,
  LINK_CLASS,
  LoadingState,
  Money,
  NetMoney,
  Panel,
  ROW_CLASS,
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

const LIMIT = 100

function ExchangeBadge({ text = 'Exchange', title }: { text?: string; title: string }) {
  return (
    <span className="px-1.5 py-0.5 rounded-md border text-[10px] font-bold bg-amber-500/15 text-amber-300 border-amber-500/30 whitespace-nowrap" title={title}>
      {text}
    </span>
  )
}

export default function TransfersTab({ days }: { days: number }) {
  const { data, loading, error } = useLedgerFetch<LedgerTransfers>(`/transfers?days=${days}&limit=${LIMIT}`)
  const [hideExchange, setHideExchange] = useState(false)
  const [filter, setFilter] = useState('')

  if (loading) return <LoadingState />
  if (error) return <ErrorState message={error} />
  if (!data) return <EmptyState message="No data." />

  const q = filter.trim().toLowerCase()
  const accounts = (data.accounts ?? []).filter(
    a => !q || a.accountName.toLowerCase().includes(q) || (a.characters ?? '').toLowerCase().includes(q),
  )
  const nameOf = new Map<number, string>()
  const charsOf = new Map<number, string>()
  for (const a of data.accounts ?? []) {
    nameOf.set(a.accountId, a.accountName)
    charsOf.set(a.accountId, (a.characters ?? '').toLowerCase())
  }
  const pairs = (data.pairs ?? [])
    .filter(p => !hideExchange || !p.viaExchange)
    .filter(
      p =>
        !q ||
        p.fromAccountName.toLowerCase().includes(q) ||
        p.toAccountName.toLowerCase().includes(q) ||
        (charsOf.get(p.fromAccountId) ?? '').includes(q) ||
        (charsOf.get(p.toAccountId) ?? '').includes(q),
    )

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
        <StatCard label="Between accounts" value={<Money value={data.crossAccount} />} hint="Pyreals moved from one account to another" />
        <StatCard
          label="Through exchanges"
          value={<Money value={data.viaExchanges} />}
          hint="Of the amount between accounts, flows that touch a marked exchange account"
          tone={data.viaExchanges > 0 ? 'warn' : 'default'}
        />
        <StatCard label="Within one account" value={<Money value={data.sameAccount} />} hint="Moves between characters of the same account" />
      </div>

      <div className="flex flex-col sm:flex-row sm:items-center gap-3">
        <input
          type="text"
          value={filter}
          onChange={e => setFilter(e.target.value)}
          placeholder="Filter by account or character"
          className="w-full sm:w-72 rounded-lg border border-neutral-800 bg-neutral-950 px-3 py-1.5 text-xs text-neutral-200 placeholder:text-neutral-600"
        />
        <label className="inline-flex items-center gap-2 text-xs text-neutral-300 cursor-pointer">
          <input type="checkbox" checked={hideExchange} onChange={e => setHideExchange(e.target.checked)} />
          Hide flows through exchanges
        </label>
      </div>

      <Panel title="Accounts" help="Accounts ranked by the pyreals they sent and received to other accounts in this period.">
        {accounts.length === 0 ? (
          <EmptyState message="No transfers between accounts in this period." />
        ) : (
          <TableScroll>
            <table className={TABLE_CLASS}>
              <thead className={THEAD_CLASS}>
                <tr className="border-b border-neutral-800">
                  <th className={TH_CLASS}>Account</th>
                  <th className={TH_CLASS}>Characters</th>
                  <th className={TH_NUM_CLASS}>Sent</th>
                  <th className={TH_NUM_CLASS}>Received</th>
                  <th className={TH_NUM_CLASS} title="Received minus sent">Net</th>
                  <th className={TH_NUM_CLASS} title="Different accounts dealt with">Partners</th>
                  <th className={TH_NUM_CLASS}>Events</th>
                  <th className={TH_CLASS} />
                </tr>
              </thead>
              <tbody className={TBODY_CLASS}>
                {accounts.map(a => (
                  <tr key={a.accountId} className={ROW_CLASS}>
                    <td className={`${TD_CLASS} whitespace-nowrap`}>
                      <Link to={accountPath(a.accountId, days)} className={LINK_CLASS}>
                        {a.accountName || `#${a.accountId}`}
                      </Link>
                    </td>
                    <td className={`${TD_CLASS} text-neutral-400 max-w-[240px] truncate`} title={a.characters ?? ''}>
                      {a.characters || '-'}
                    </td>
                    <td className={TD_NUM_CLASS}>
                      <Money value={a.sent} />
                    </td>
                    <td className={TD_NUM_CLASS}>
                      <Money value={a.received} />
                    </td>
                    <td className={TD_NUM_CLASS}>
                      <NetMoney value={a.net} />
                    </td>
                    <td className={TD_NUM_CLASS}>{a.partners.toLocaleString()}</td>
                    <td className={TD_NUM_CLASS}>{a.events.toLocaleString()}</td>
                    <td className={TD_CLASS}>{a.isExchange && <ExchangeBadge title="Marked as a player-run exchange" />}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TableScroll>
        )}
      </Panel>

      <Panel title="Largest flows" help="The biggest account-to-account flows in this period.">
        {pairs.length === 0 ? (
          <EmptyState message="No flows to show." />
        ) : (
          <TableScroll>
            <table className={TABLE_CLASS}>
              <thead className={THEAD_CLASS}>
                <tr className="border-b border-neutral-800">
                  <th className={TH_CLASS}>From</th>
                  <th className={TH_CLASS}>To</th>
                  <th className={TH_NUM_CLASS}>Amount</th>
                  <th className={TH_NUM_CLASS}>Events</th>
                  <th className={TH_CLASS} />
                </tr>
              </thead>
              <tbody className={TBODY_CLASS}>
                {pairs.map(p => (
                  <tr key={`${p.fromAccountId}-${p.toAccountId}`} className={ROW_CLASS}>
                    <td className={`${TD_CLASS} whitespace-nowrap`}>
                      <Link to={accountPath(p.fromAccountId, days)} className={LINK_CLASS}>
                        {p.fromAccountName || nameOf.get(p.fromAccountId) || `#${p.fromAccountId}`}
                      </Link>
                    </td>
                    <td className={`${TD_CLASS} whitespace-nowrap`}>
                      <span className="text-neutral-600 mr-1">-&gt;</span>
                      <Link to={accountPath(p.toAccountId, days)} className={LINK_CLASS}>
                        {p.toAccountName || nameOf.get(p.toAccountId) || `#${p.toAccountId}`}
                      </Link>
                    </td>
                    <td className={`${TD_NUM_CLASS} font-semibold`}>
                      <Money value={p.amount} />
                    </td>
                    <td className={TD_NUM_CLASS}>{p.events.toLocaleString()}</td>
                    <td className={TD_CLASS}>{p.viaExchange && <ExchangeBadge text="Via exchange" title="This flow touches a marked exchange account" />}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TableScroll>
        )}
      </Panel>

      <p className="text-[11px] text-neutral-500 leading-relaxed">
        Counted on the receiving side: bank transfers, trades and gives. Staff accounts are left out. Mark a player-run exchange with
        /modifystring pyreal_ledger_exchange_accounts &lt;character names, comma-separated&gt; (for example: Atm).
      </p>
    </div>
  )
}
