import { useNavigate } from 'react-router-dom'
import type { CurrencySummary } from '../../types/economy'
import { Amount, ChangeText, NetAmount, StatusPill } from './EconomyShared'
import Sparkline from './Sparkline'
import { basisNote, currencyPath, formatCompact, formatFull, formatPct } from './economyUtils'

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-3 text-xs">
      <span className="text-neutral-500">{label}</span>
      <span className="text-neutral-200 text-right">{children}</span>
    </div>
  )
}

export default function CurrencyCard({ currency: c, days }: { currency: CurrencySummary; days: number }) {
  const navigate = useNavigate()
  const s = c.supply
  const hasFlow = c.flow.days > 0 && (c.flow.faucets > 0 || c.flow.sinks > 0 || c.flow.firstDay !== null)
  const top1Share = s.total > 0 ? (s.top1PctAmount / s.total) * 100 : null
  const hasSnapshot = s.day !== null

  return (
    <button
      type="button"
      onClick={() => navigate(currencyPath(c.key, days))}
      className="text-left rounded-2xl border border-neutral-800 bg-neutral-900/40 hover:bg-neutral-900/80 hover:border-neutral-700 transition-colors p-4 flex flex-col gap-3 min-w-0"
    >
      <div className="flex items-start justify-between gap-2">
        <h2 className="text-sm font-bold text-white truncate">{c.name}</h2>
        <StatusPill status={c.status} />
      </div>

      <div>
        <div className="text-[10px] font-bold text-neutral-500 uppercase tracking-widest">Total supply</div>
        <div className="text-2xl font-black text-white tabular-nums truncate" title={hasSnapshot ? formatFull(s.total) : undefined}>
          {hasSnapshot ? formatCompact(s.total) : '-'}
        </div>
      </div>

      <div>
        <div className="flex items-baseline gap-2">
          <span className="text-[10px] font-bold text-neutral-500 uppercase tracking-widest">Weekly</span>
          <span className="text-lg font-bold text-white tabular-nums">{c.weeklyPct === null ? '-' : formatPct(c.weeklyPct, 2)}</span>
        </div>
        <div className="text-[11px] text-neutral-500">{basisNote(c.weeklyPctBasis)}</div>
      </div>

      <div className="space-y-1">
        <Row label="7d change">
          <ChangeText change={c.change7d} />
        </Row>
        <Row label="30d change">
          <ChangeText change={c.change30d} />
        </Row>
      </div>

      <div className="space-y-1 border-t border-neutral-800 pt-3">
        {hasFlow ? (
          <>
            <Row label={`Faucets (${days}d)`}>
              <Amount value={c.flow.faucets} className="text-emerald-300" />
            </Row>
            <Row label={`Sinks (${days}d)`}>
              <Amount value={c.flow.sinks} className="text-red-300" />
            </Row>
            <Row label="Net">
              <NetAmount value={c.flow.net} />
            </Row>
          </>
        ) : (
          <div className="text-xs text-neutral-600">no flow data yet</div>
        )}
      </div>

      <div className="space-y-1 border-t border-neutral-800 pt-3">
        {hasSnapshot ? (
          <>
            <Row label="Banked">
              <Amount value={s.banked} />
            </Row>
            <Row label="In items">
              <Amount value={s.items} />
            </Row>
            <Row label="Active players">
              <Amount value={s.activeSupply} />
            </Row>
            <Row label="Holders">
              <span className="tabular-nums" title={`${formatFull(s.holders)} holders, ${formatFull(s.activeHolders)} active`}>
                {formatCompact(s.holders)} ({formatCompact(s.activeHolders)} active)
              </span>
            </Row>
            <Row label="Held by top 1%">
              {top1Share === null ? '-' : <span className="tabular-nums" title={formatFull(s.top1PctAmount)}>{top1Share.toFixed(1)}%</span>}
            </Row>
            {s.unexplained !== null && s.unexplained !== undefined && (
              <Row label="Unexplained">
                <NetAmount value={s.unexplained} />
              </Row>
            )}
          </>
        ) : (
          <div className="text-xs text-neutral-600">no snapshots yet</div>
        )}
      </div>

      <Sparkline values={(c.series ?? []).map(p => p.total)} />
    </button>
  )
}
