import { Link } from 'react-router-dom'
import { ArrowLeft, User, Users } from 'lucide-react'
import type {
  LedgerDetail as LedgerDetailData,
  LedgerDetailBankRow,
  LedgerDetailItemRow,
  LedgerDetailKind,
  LedgerDetailSoldRow,
} from '../../types/pyrealLedger'
import {
  LEDGER_BASE_PATH,
  accountPath,
  formatCompact,
  characterPath,
  formatDateTime,
  itemPath,
  npcPath,
  parseVendorMultiplier,
  sortFlagsBySeverity,
  useLedgerFetch,
} from './ledgerUtils'
import {
  EmptyState,
  ErrorState,
  FlagLegend,
  InfoNote,
  LINK_CLASS,
  LoadingState,
  Money,
  MultiplierBadge,
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
import FlagsTable from './FlagsTable'
import ActivityChart from './ActivityChart'

export default function LedgerDetail({
  kind,
  id,
  days,
}: {
  kind: LedgerDetailKind
  id: number
  days: number
}) {
  const path = Number.isFinite(id) && id > 0 ? `/${kind === 'character' ? 'characters' : 'accounts'}/${id}?days=${days}` : null
  const { data, loading, error } = useLedgerFetch<LedgerDetailData>(path)

  return (
    <div className="space-y-4">
      <Link to={`${LEDGER_BASE_PATH}?days=${days}`} className="inline-flex items-center gap-1.5 text-xs text-neutral-400 hover:text-white">
        <ArrowLeft className="w-3.5 h-3.5" /> Back to the ledger
      </Link>
      {path === null ? (
        <ErrorState message={`Invalid ${kind} id.`} />
      ) : loading ? (
        <LoadingState />
      ) : error ? (
        <ErrorState message={error} />
      ) : !data ? (
        <EmptyState message="Nothing found." />
      ) : (
        <DetailBody data={data} days={days} />
      )}
    </div>
  )
}

function DetailBody({ data, days }: { data: LedgerDetailData; days: number }) {
  const isChar = data.kind === 'character'
  const bank = data.bank ?? []
  const items = data.items ?? []
  const flags = data.flags ?? []
  const bankIn = bank.reduce((s, r) => s + r.amountIn, 0)
  const bankOut = bank.reduce((s, r) => s + r.amountOut, 0)
  const curIn = items.filter(i => i.direction === 'in').reduce((s, r) => s + r.value, 0)
  const curOut = items.filter(i => i.direction === 'out').reduce((s, r) => s + r.value, 0)
  const position = data.currencyPosition ?? null
  const flaggedAmount = flags.reduce((s, f) => s + Math.max(0, f.amount), 0)
  const unattributedIn = bank.filter(b => b.source === 'Unattributed').reduce((s, r) => s + r.amountIn, 0)
  const Icon = isChar ? User : Users

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <div className="w-10 h-10 rounded-xl bg-neutral-800 border border-neutral-700 flex items-center justify-center text-neutral-300">
          <Icon className="w-5 h-5" />
        </div>
        <div className="min-w-0">
          <div className="text-[10px] font-bold text-neutral-500 uppercase tracking-widest">
            {isChar ? 'Character' : 'Account'} - last {data.days} day{data.days === 1 ? '' : 's'}
          </div>
          <h2 className="text-xl font-black text-white truncate">{data.name || data.accountName || `#${data.id}`}</h2>
        </div>
        {isChar && data.accountId > 0 && (
          <Link
            to={accountPath(data.accountId, days)}
            className="ml-auto inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-neutral-800 hover:bg-neutral-700 text-xs text-neutral-200 border border-neutral-700"
          >
            <Users className="w-3.5 h-3.5" /> Account: {data.accountName || `#${data.accountId}`}
          </Link>
        )}
      </div>

      <div className="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-6 gap-3">
        <StatCard
          label="Bank balance now"
          value={<Money value={data.balance} />}
          hint={isChar ? 'Banked pyreals today' : 'Sum of all characters'}
        />
        <StatCard label="Bank in" value={<Money value={bankIn} />} tone="good" hint="Credits this period" />
        <StatCard label="Bank out" value={<Money value={bankOut} />} hint="Debits this period" />
        <StatCard label="Net" value={<NetMoney value={bankIn - bankOut} />} hint="In minus out" />
        <StatCard
          label="Flagged"
          value={<Money value={flaggedAmount} />}
          tone={flaggedAmount > 0 ? 'bad' : 'default'}
          hint={`${flags.length.toLocaleString()} flag event${flags.length === 1 ? '' : 's'}`}
        />
        <StatCard
          label="Currency position (all time)"
          value={<Money value={position} />}
          tone={position !== null && position < 0 ? 'bad' : 'default'}
          hint={
            position === null ? (
              'Not available'
            ) : position < 0 ? (
              <span className="text-red-300" title={`${Math.abs(position).toLocaleString()} pyreals of face value`}>
                Surplus: got rid of {formatCompact(Math.abs(position))} more currency than ever seen arriving
              </span>
            ) : (
              'Should be holding about this much in coins, notes and peas'
            )
          }
        />
      </div>

      {unattributedIn > 0 && (
        <InfoNote tone="warn">
          <Money value={unattributedIn} /> pyreals arrived from a code path nobody labeled (Unattributed). That is suspicious by
          itself - see the flags below for the stack trace developers need.
        </InfoNote>
      )}

      {isChar && data.state && <StateBlock state={data.state} />}

      {!isChar && <CharactersPanel data={data} days={days} />}

      <Panel
        title={`Flags (${flags.length.toLocaleString()})`}
        help="Moments where pyreals appeared or vanished in a way that does not add up. The explanation is next to each one."
      >
        {flags.length === 0 ? (
          <EmptyState message="No flags in this period. Good sign." />
        ) : (
          <>
            <div className="p-3 border-b border-neutral-800">
              <FlagLegend />
            </div>
            <FlagsTable rows={sortFlagsBySeverity(flags)} days={days} showCharacter={!isChar} showAccount={false} />
          </>
        )}
      </Panel>

      <Panel title="Bank activity over time" help="Banked pyreals in and out. Gaps mean no activity.">
        {(data.hours ?? []).length === 0 ? (
          <EmptyState message="No bank activity in this period." />
        ) : (
          <ActivityChart hours={data.hours} days={data.days || days} />
        )}
      </Panel>

      <BankPanel rows={bank} days={days} />

      <SoldPanel rows={data.sold ?? []} days={days} />

      <ItemsPanel rows={items} curIn={curIn} curOut={curOut} />
    </div>
  )
}

function StateBlock({ state }: { state: NonNullable<LedgerDetailData['state']> }) {
  const diff = state.liveBalance - state.savedBalance
  const pending = (state.pendingNotes ?? '')
    .split('|')
    .map(s => s.trim())
    .filter(Boolean)
  return (
    <Panel
      title="Live vs saved balance"
      help="The game keeps the bank balance in memory and saves it to the database now and then. If the server crashes between saves, unsaved changes are lost - that is how rollbacks and dupes happen."
    >
      <div className="p-4 grid grid-cols-1 md:grid-cols-3 gap-3">
        <StatCard label="Live balance" value={<Money value={state.liveBalance} />} hint={`As of ${formatDateTime(state.updatedUtc)}`} />
        <StatCard label="Last saved balance" value={<Money value={state.savedBalance} />} hint={state.savedUtc ? `Saved ${formatDateTime(state.savedUtc)}` : 'Never saved'} />
        <StatCard
          label="Not yet saved"
          value={diff === 0 ? 'Nothing' : <NetMoney value={diff} />}
          tone={diff === 0 ? 'good' : 'warn'}
          hint={diff === 0 ? 'Live and saved match' : 'Would be lost if the server crashed now'}
        />
      </div>
      {pending.length > 0 && (
        <div className="px-4 pb-4">
          <div className="text-[10px] font-bold text-neutral-500 uppercase tracking-widest mb-1">Unsaved changes</div>
          <ul className="space-y-1 text-xs">
            {pending.map((p, i) => {
              const risky = p.startsWith('!')
              return (
                <li
                  key={i}
                  className={`px-2 py-1 rounded-md border font-mono break-all ${
                    risky ? 'border-red-500/30 bg-red-500/10 text-red-300' : 'border-neutral-800 bg-neutral-950 text-neutral-400'
                  }`}
                  title={risky ? 'The other side of this change is already saved. A crash now would duplicate these pyreals.' : undefined}
                >
                  {p}
                </li>
              )
            })}
          </ul>
          {pending.some(p => p.startsWith('!')) && (
            <p className="text-[11px] text-red-300/80 mt-1">
              Lines starting with ! have their other side already saved. A crash now would duplicate them.
            </p>
          )}
        </div>
      )}
    </Panel>
  )
}

function CharactersPanel({ data, days }: { data: LedgerDetailData; days: number }) {
  const chars = [...(data.characters ?? [])].sort((a, b) => b.bankIn - a.bankIn)
  return (
    <Panel title={`Characters (${chars.length})`} help="Every character on this account. Click one to see where its pyreals came from.">
      {chars.length === 0 ? (
        <EmptyState message="No characters." />
      ) : (
        <TableScroll>
          <table className={TABLE_CLASS}>
            <thead className={THEAD_CLASS}>
              <tr className="border-b border-neutral-800">
                <th className={TH_CLASS}>Character</th>
                <th className={TH_NUM_CLASS}>Balance now</th>
                <th className={TH_NUM_CLASS}>Bank in</th>
                <th className={TH_NUM_CLASS}>Bank out</th>
                <th className={TH_NUM_CLASS}>Net</th>
              </tr>
            </thead>
            <tbody className={TBODY_CLASS}>
              {chars.map(c => (
                <tr key={c.charId} className={ROW_CLASS}>
                  <td className={`${TD_CLASS} whitespace-nowrap`}>
                    <Link to={characterPath(c.charId, days)} className={LINK_CLASS}>
                      {c.name || `#${c.charId}`}
                    </Link>
                  </td>
                  <td className={TD_NUM_CLASS}>
                    <Money value={c.balance} />
                  </td>
                  <td className={TD_NUM_CLASS}>
                    <Money value={c.bankIn} className="text-emerald-300" />
                  </td>
                  <td className={TD_NUM_CLASS}>
                    <Money value={c.bankOut} className="text-neutral-400" />
                  </td>
                  <td className={TD_NUM_CLASS}>
                    <NetMoney value={c.bankIn - c.bankOut} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </TableScroll>
      )}
    </Panel>
  )
}

function BankDetailCell({ row, days }: { row: LedgerDetailBankRow; days: number }) {
  if (!row.detailName && !row.detailKey) return <span className="text-neutral-600">-</span>
  if (row.source === 'VendorSell') {
    const mult = parseVendorMultiplier(row.detailName)
    return (
      <span className="inline-flex items-center gap-2">
        <span className={mult !== null && mult > 1 ? 'text-red-300' : 'text-neutral-300'}>{row.detailName}</span>
        {mult !== null && mult > 1 && <MultiplierBadge multiplier={mult} />}
      </span>
    )
  }
  if (row.source === 'Emote' && row.detailKey && /^\d+$/.test(row.detailKey) && Number(row.detailKey) > 0) {
    return (
      <Link to={npcPath(Number(row.detailKey), days)} className={LINK_CLASS} title="See everyone this NPC paid">
        {row.detailName || `WCID ${row.detailKey}`}
      </Link>
    )
  }
  if (row.source === 'Transfer' && row.detailKey && /^\d+$/.test(row.detailKey)) {
    return (
      <Link to={characterPath(Number(row.detailKey), days)} className={LINK_CLASS} title="Open the other character">
        {row.detailName || `#${row.detailKey}`}
      </Link>
    )
  }
  return <span className="text-neutral-300 break-words">{row.detailName || row.detailKey}</span>
}

function BankPanel({ rows, days }: { rows: LedgerDetailBankRow[]; days: number }) {
  return (
    <Panel
      title="Bank by source"
      help="Every way banked pyreals came in or went out. 'Detail' is the vendor, NPC or other character involved. A vendor in red pays more than items are worth."
    >
      {rows.length === 0 ? (
        <EmptyState message="No bank activity in this period." />
      ) : (
        <TableScroll>
          <table className={TABLE_CLASS}>
            <thead className={THEAD_CLASS}>
              <tr className="border-b border-neutral-800">
                <th className={TH_CLASS}>Source</th>
                <th className={TH_CLASS}>Detail</th>
                <th className={TH_NUM_CLASS}>In</th>
                <th className={TH_NUM_CLASS}>Out</th>
                <th className={TH_NUM_CLASS}>Events</th>
              </tr>
            </thead>
            <tbody className={TBODY_CLASS}>
              {rows.map((r, i) => (
                <tr key={`${r.source}-${r.detailKey ?? ''}-${i}`} className={ROW_CLASS}>
                  <td className={TD_CLASS}>
                    <SourceLabel source={r.source} />
                  </td>
                  <td className={`${TD_CLASS} max-w-[360px]`}>
                    <BankDetailCell row={r} days={days} />
                  </td>
                  <td className={TD_NUM_CLASS}>
                    {r.amountIn ? <Money value={r.amountIn} className="text-emerald-300" /> : <span className="text-neutral-600">0</span>}
                  </td>
                  <td className={TD_NUM_CLASS}>
                    {r.amountOut ? <Money value={r.amountOut} className="text-neutral-400" /> : <span className="text-neutral-600">0</span>}
                  </td>
                  <td className={TD_NUM_CLASS}>{r.events.toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </TableScroll>
      )}
    </Panel>
  )
}

function ItemsTable({ rows }: { rows: LedgerDetailItemRow[] }) {
  if (rows.length === 0) return <EmptyState message="None." />
  return (
    <TableScroll>
      <table className={TABLE_CLASS}>
        <thead className={THEAD_CLASS}>
          <tr className="border-b border-neutral-800">
            <th className={TH_CLASS}>Source</th>
            <th className={TH_CLASS}>Item</th>
            <th className={TH_CLASS}>Detail</th>
            <th className={TH_NUM_CLASS}>Units</th>
            <th className={TH_NUM_CLASS}>Value</th>
          </tr>
        </thead>
        <tbody className={TBODY_CLASS}>
          {rows.map((r, i) => (
            <tr key={`${r.source}-${r.wcid}-${r.detailName ?? ''}-${i}`} className={ROW_CLASS}>
              <td className={TD_CLASS}>
                <SourceLabel source={r.source} />
              </td>
              <td className={`${TD_CLASS} whitespace-nowrap`} title={`WCID ${r.wcid}`}>
                {r.itemName || `WCID ${r.wcid}`}
              </td>
              <td className={`${TD_CLASS} text-neutral-400 max-w-[220px] truncate`} title={r.detailName ?? undefined}>
                {r.detailName || '-'}
              </td>
              <td className={`${TD_NUM_CLASS} text-neutral-400`}>{r.units.toLocaleString()}</td>
              <td className={TD_NUM_CLASS}>
                <Money value={r.value} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </TableScroll>
  )
}

function ItemsPanel({
  rows,
  curIn,
  curOut,
}: {
  rows: LedgerDetailItemRow[]
  curIn: number
  curOut: number
}) {
  const inRows = rows.filter(r => r.direction === 'in')
  const outRows = rows.filter(r => r.direction === 'out')
  return (
    <Panel
      title="Currency items (coins, trade notes, peas)"
      help="Physical currency at face value, for this period. Received = picked up, looted, traded or withdrawn. Given up = deposited, sold or given away. Opening rows are what the character already held when tracking began; they are not suspicious. The all-time verdict is the Currency position number at the top."
    >
      <div className="p-4 grid grid-cols-1 sm:grid-cols-3 gap-3">
        <StatCard label="Received" value={<Money value={curIn} />} tone="good" />
        <StatCard label="Given up" value={<Money value={curOut} />} />
        <StatCard label="Net this period" value={<NetMoney value={curIn - curOut} />} hint="Received minus given up" />
      </div>
      <div className="grid grid-cols-1 xl:grid-cols-2 gap-px bg-neutral-800 border-t border-neutral-800">
        <div className="bg-neutral-900/90">
          <div className="px-4 py-2 text-[10px] font-bold text-emerald-400/80 uppercase tracking-widest">Received ({inRows.length})</div>
          <ItemsTable rows={inRows} />
        </div>
        <div className="bg-neutral-900/90">
          <div className="px-4 py-2 text-[10px] font-bold text-neutral-400 uppercase tracking-widest">Given up ({outRows.length})</div>
          <ItemsTable rows={outRows} />
        </div>
      </div>
    </Panel>
  )
}

function SoldPanel({ rows, days }: { rows: LedgerDetailSoldRow[]; days: number }) {
  return (
    <Panel
      title="Sold to vendors"
      help="Items sold to vendors for pyreals, per item and vendor. A huge quantity of one stackable item can mean a duplicated stack - click the item to compare with everyone else who sold it."
    >
      {rows.length === 0 ? (
        <EmptyState message="Nothing sold to vendors in this period." />
      ) : (
        <TableScroll>
          <table className={TABLE_CLASS}>
            <thead className={THEAD_CLASS}>
              <tr className="border-b border-neutral-800">
                <th className={TH_CLASS}>Item</th>
                <th className={TH_CLASS}>Vendor</th>
                <th className={TH_NUM_CLASS}>Quantity</th>
                <th className={TH_NUM_CLASS}>Paid</th>
              </tr>
            </thead>
            <tbody className={TBODY_CLASS}>
              {rows.map((r, i) => (
                <tr key={`${r.wcid}-${r.vendorWcid}-${i}`} className={ROW_CLASS}>
                  <td className={`${TD_CLASS} whitespace-nowrap`}>
                    <Link to={itemPath(r.wcid, days)} className={LINK_CLASS} title={`WCID ${r.wcid} - see everyone who sold this item`}>
                      {r.itemName || `WCID ${r.wcid}`}
                    </Link>
                  </td>
                  <td className={`${TD_CLASS} text-neutral-300 whitespace-nowrap`} title={`Vendor WCID ${r.vendorWcid}`}>
                    {r.vendorName || `WCID ${r.vendorWcid}`}
                  </td>
                  <td className={`${TD_NUM_CLASS} font-semibold`}>{r.units.toLocaleString()}</td>
                  <td className={TD_NUM_CLASS}>
                    <Money value={r.payout} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </TableScroll>
      )}
    </Panel>
  )
}
