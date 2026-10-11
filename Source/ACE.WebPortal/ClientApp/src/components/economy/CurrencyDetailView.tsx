import { Link } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'
import type { CurrencyDetail, EconomyDetailRow } from '../../types/economy'
import {
  EmptyState,
  ErrorState,
  InfoNote,
  LoadingState,
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
  LINK_CLASS,
} from '../pyreal-ledger/LedgerShared'
import { Amount, ChangeText, NetAmount, StatusPill } from './EconomyShared'
import { FlowChart, SupplyChart } from './EconomyCharts'
import { ECONOMY_BASE_PATH, basisNote, formatCompact, formatDateTime, formatFull, formatPct, useEconomyFetch } from './economyUtils'

function DetailTable({ rows, mode, empty }: { rows: EconomyDetailRow[]; mode: 'in' | 'out'; empty: string }) {
  if (rows.length === 0) return <EmptyState message={empty} />
  return (
    <TableScroll>
      <table className={TABLE_CLASS}>
        <thead className={THEAD_CLASS}>
          <tr className="border-b border-neutral-800">
            <th className={TH_CLASS}>Source</th>
            <th className={TH_CLASS}>Detail</th>
            <th className={TH_NUM_CLASS}>{mode === 'in' ? 'Created' : 'Removed'}</th>
            <th className={TH_NUM_CLASS}>Events</th>
            <th className={TH_NUM_CLASS}>Characters</th>
          </tr>
        </thead>
        <tbody className={TBODY_CLASS}>
          {rows.map((r, i) => (
            <tr key={`${r.source}|${r.detailKey}|${i}`} className={ROW_CLASS}>
              <td className={TD_CLASS}>{r.source}</td>
              <td className={TD_CLASS} title={r.detailKey}>
                {r.detailName || r.detailKey || '-'}
              </td>
              <td className={TD_NUM_CLASS}>
                <Amount value={mode === 'in' ? r.amountIn : r.amountOut} />
              </td>
              <td className={TD_NUM_CLASS}>{r.events.toLocaleString()}</td>
              <td className={TD_NUM_CLASS}>{r.characters.toLocaleString()}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </TableScroll>
  )
}

export default function CurrencyDetailView({ currencyKey, days, reloadToken }: { currencyKey: string; days: number; reloadToken: number }) {
  const { data, loading, error } = useEconomyFetch<CurrencyDetail>(
    `/currencies/${encodeURIComponent(currencyKey)}?days=${days}&r=${reloadToken}`,
  )

  const back = (
    <Link to={`${ECONOMY_BASE_PATH}?days=${days}`} className={`inline-flex items-center gap-1.5 text-xs mb-4 ${LINK_CLASS}`}>
      <ArrowLeft className="w-3.5 h-3.5" /> All currencies
    </Link>
  )

  if (loading) {
    return (
      <div>
        {back}
        <LoadingState />
      </div>
    )
  }
  if (error || !data) {
    return (
      <div>
        {back}
        <ErrorState message={error ?? 'No data.'} />
      </div>
    )
  }

  const c = data.summary
  const s = c.supply
  const hasSnapshot = s.day !== null
  const top1Share = s.total > 0 ? (s.top1PctAmount / s.total) * 100 : null
  const sources = data.sources ?? []
  const faucets = data.faucets ?? []
  const sinks = data.sinks ?? []
  const earners = data.earners ?? []
  const holders = data.holders ?? []
  const settings = data.settings ?? []
  const notes = data.notes ?? []

  return (
    <div className="space-y-4">
      {back}

      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-xl font-black text-white">{c.name}</h2>
        <StatusPill status={c.status} />
        <span className="text-xs text-neutral-500">
          Weekly {c.weeklyPct === null ? '-' : formatPct(c.weeklyPct, 2)} ({basisNote(c.weeklyPctBasis)})
        </span>
      </div>

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <StatCard label="Total supply" value={hasSnapshot ? <Amount value={s.total} /> : '-'} hint={hasSnapshot ? `Snapshot ${s.day}` : 'no snapshots yet'} />
        <StatCard label="Banked / in items" value={hasSnapshot ? <><Amount value={s.banked} /> / <Amount value={s.items} /></> : '-'} />
        <StatCard
          label="Active players"
          value={hasSnapshot ? <Amount value={s.activeSupply} /> : '-'}
          hint={hasSnapshot ? `${formatCompact(s.activeHolders)} of ${formatCompact(s.holders)} holders` : undefined}
        />
        <StatCard
          label="Held by top 1%"
          value={top1Share === null ? '-' : `${top1Share.toFixed(1)}%`}
          hint={hasSnapshot ? `Largest holder ${formatCompact(s.largestHolder)}; admin ${formatCompact(s.adminSupply)}` : undefined}
        />
        <StatCard
          label="Unexplained"
          value={s.unexplained === null || s.unexplained === undefined ? '-' : <NetAmount value={s.unexplained} />}
          hint={s.unexplained === null || s.unexplained === undefined ? 'needs two snapshots with recording on in between' : 'supply change minus recorded flows, since the previous snapshot'}
        />
        <StatCard label="7d change" value={<ChangeText change={c.change7d} />} />
        <StatCard label="30d change" value={<ChangeText change={c.change30d} />} />
        <StatCard
          label={`Faucets / sinks (${days}d)`}
          value={<><Amount value={c.flow.faucets} className="text-emerald-300" /> / <Amount value={c.flow.sinks} className="text-red-300" /></>}
          hint={c.flow.firstDay ? `Tracked since ${c.flow.firstDay}` : 'no flow data yet'}
        />
        <StatCard label="Net" value={<NetAmount value={c.flow.net} />} hint={`Moved, not created: ${formatCompact(c.flow.internal)}`} />
      </div>

      <Panel title="Supply over time" help="Total supply and the part held by recently active players, one point per daily snapshot.">
        <SupplyChart series={c.series ?? []} />
      </Panel>

      <Panel title="Faucets vs sinks" help="Currency created (faucets, up) and removed (sinks, down) per day.">
        <FlowChart flows={c.flowSeries ?? []} />
      </Panel>

      <Panel title="By source" help="Every tracked source in the period. Dimmed rows are internal moves: the currency changed hands or form but was not created or destroyed.">
        {sources.length === 0 ? (
          <EmptyState message="No flow data yet." />
        ) : (
          <TableScroll>
            <table className={TABLE_CLASS}>
              <thead className={THEAD_CLASS}>
                <tr className="border-b border-neutral-800">
                  <th className={TH_CLASS}>Source</th>
                  <th className={TH_NUM_CLASS}>In</th>
                  <th className={TH_NUM_CLASS}>Out</th>
                  <th className={TH_NUM_CLASS}>Events</th>
                  <th className={TH_NUM_CLASS}>Characters</th>
                </tr>
              </thead>
              <tbody className={TBODY_CLASS}>
                {sources.map(r => (
                  <tr key={r.source} className={`${ROW_CLASS} ${r.kind === 'internal' ? 'opacity-50' : ''}`}>
                    <td className={TD_CLASS}>
                      {r.source}
                      {r.kind === 'internal' && <span className="ml-2 text-[10px] text-neutral-500 italic">moved, not created</span>}
                    </td>
                    <td className={TD_NUM_CLASS}>
                      <Amount value={r.amountIn} />
                    </td>
                    <td className={TD_NUM_CLASS}>
                      <Amount value={r.amountOut} />
                    </td>
                    <td className={TD_NUM_CLASS}>{r.events.toLocaleString()}</td>
                    <td className={TD_NUM_CLASS}>{r.characters.toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TableScroll>
        )}
      </Panel>

      <Panel title="Top faucets" help="The biggest specific places this currency was created.">
        <DetailTable rows={faucets} mode="in" empty="No faucets recorded in this period." />
      </Panel>

      <Panel title="Top sinks" help="The biggest specific places this currency was removed.">
        <DetailTable rows={sinks} mode="out" empty="No sinks recorded in this period." />
      </Panel>

      <Panel title="Top earners" help="Characters with the largest net gain in the period.">
        {earners.length === 0 ? (
          <EmptyState message="No earners recorded in this period." />
        ) : (
          <TableScroll>
            <table className={TABLE_CLASS}>
              <thead className={THEAD_CLASS}>
                <tr className="border-b border-neutral-800">
                  <th className={TH_CLASS}>#</th>
                  <th className={TH_CLASS}>Character</th>
                  <th className={TH_NUM_CLASS}>Account</th>
                  <th className={TH_NUM_CLASS}>In</th>
                  <th className={TH_NUM_CLASS}>Out</th>
                  <th className={TH_NUM_CLASS}>Net</th>
                </tr>
              </thead>
              <tbody className={TBODY_CLASS}>
                {earners.map((r, i) => (
                  <tr key={r.charId} className={ROW_CLASS}>
                    <td className={TD_CLASS}>{i + 1}</td>
                    <td className={TD_CLASS}>{r.charName}</td>
                    <td className={TD_NUM_CLASS}>{r.accountId}</td>
                    <td className={TD_NUM_CLASS}>
                      <Amount value={r.amountIn} />
                    </td>
                    <td className={TD_NUM_CLASS}>
                      <Amount value={r.amountOut} />
                    </td>
                    <td className={TD_NUM_CLASS}>
                      <NetAmount value={r.net} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TableScroll>
        )}
      </Panel>

      <Panel title="Largest holders" help="Characters holding the most of this currency in the bank at the last snapshot.">
        {holders.length === 0 ? (
          <EmptyState message="No snapshots yet." />
        ) : (
          <TableScroll>
            <table className={TABLE_CLASS}>
              <thead className={THEAD_CLASS}>
                <tr className="border-b border-neutral-800">
                  <th className={TH_CLASS}>#</th>
                  <th className={TH_CLASS}>Character</th>
                  <th className={TH_NUM_CLASS}>Account</th>
                  <th className={TH_NUM_CLASS}>Banked</th>
                  <th className={TH_CLASS}>Last login</th>
                </tr>
              </thead>
              <tbody className={TBODY_CLASS}>
                {holders.map((r, i) => (
                  <tr key={r.charId} className={ROW_CLASS}>
                    <td className={TD_CLASS}>{i + 1}</td>
                    <td className={TD_CLASS}>{r.charName}</td>
                    <td className={TD_NUM_CLASS}>{r.accountId}</td>
                    <td className={TD_NUM_CLASS}>
                      <Amount value={r.banked} />
                    </td>
                    <td className={TD_CLASS} title={formatFull(r.banked)}>
                      {formatDateTime(r.lastLoginUtc)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TableScroll>
        )}
      </Panel>

      <Panel title="Reward settings" help="Server settings that control how much of this currency enters the game.">
        {settings.length === 0 ? (
          <EmptyState message="No settings listed for this currency." />
        ) : (
          <TableScroll>
            <table className={TABLE_CLASS}>
              <thead className={THEAD_CLASS}>
                <tr className="border-b border-neutral-800">
                  <th className={TH_CLASS}>Setting</th>
                  <th className={TH_CLASS}>Value</th>
                  <th className={TH_CLASS}>Description</th>
                </tr>
              </thead>
              <tbody className={TBODY_CLASS}>
                {settings.map(r => (
                  <tr key={r.name} className={ROW_CLASS}>
                    <td className={`${TD_CLASS} font-mono`}>{r.name}</td>
                    <td className={`${TD_CLASS} font-mono text-white`}>{r.value}</td>
                    <td className={`${TD_CLASS} text-neutral-400`}>{r.description}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TableScroll>
        )}
      </Panel>

      {notes.length > 0 && (
        <Panel title="Notes">
          <ul className="p-4 space-y-2">
            {notes.map((n, i) => (
              <li key={i}>
                <InfoNote>{n}</InfoNote>
              </li>
            ))}
          </ul>
        </Panel>
      )}
    </div>
  )
}
