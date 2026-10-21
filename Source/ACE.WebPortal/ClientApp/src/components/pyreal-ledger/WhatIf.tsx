import { useSearchParams } from 'react-router-dom'
import type { LedgerContext } from '../../types/pyrealLedger'
import { useLedgerFetch } from './ledgerUtils'
import { Money } from './LedgerShared'

const DEFAULT_CUT = 25

export function useContextData(days: number) {
  return useLedgerFetch<LedgerContext>(`/context?days=${days}`)
}

/** The shared "cut payouts by" percentage, kept in the URL. */
export function useCut(): [number, (v: number) => void] {
  const [params, setParams] = useSearchParams()
  const raw = params.get('cut')
  const n = raw === null ? DEFAULT_CUT : Number(raw)
  const cut = Number.isFinite(n) ? Math.min(100, Math.max(0, n)) : DEFAULT_CUT
  const set = (v: number) => {
    const next = new URLSearchParams(params)
    next.set('cut', String(Math.min(100, Math.max(0, Number.isFinite(v) ? v : 0))))
    setParams(next, { replace: true })
  }
  return [cut, set]
}

const pct = (n: number, d = 2) => n.toLocaleString(undefined, { maximumFractionDigits: d })

export function ContextLine({ ctx }: { ctx: LedgerContext | null }) {
  if (!ctx) return null
  return (
    <p className="text-xs text-neutral-400">
      Server-wide: <Money value={ctx.createdPerWeek} /> created and <Money value={ctx.removedPerWeek} /> removed per week, net{' '}
      <Money value={ctx.netPerWeek} signed /> ({ctx.weeklyPct === null ? '-' : pct(ctx.weeklyPct)}% of supply per week; target{' '}
      {ctx.targetMinWeeklyPct}% to {ctx.targetMaxWeeklyPct}%). Based on{' '}
      {ctx.coveredDays.toLocaleString(undefined, { minimumFractionDigits: 1, maximumFractionDigits: 1 })} days of data.
    </p>
  )
}

export function CutControl({ cut, onChange }: { cut: number; onChange: (v: number) => void }) {
  return (
    <div>
      <label className="inline-flex items-center gap-2 text-xs text-neutral-300">
        Cut payouts by
        <input
          type="number"
          min={0}
          max={100}
          value={cut}
          onChange={e => onChange(Number(e.target.value))}
          className="w-16 rounded-lg border border-neutral-800 bg-neutral-950 px-2 py-1 text-xs text-neutral-200 tabular-nums"
        />
        %
      </label>
      <p className="text-[11px] text-neutral-500 mt-1">Assumes players keep doing the same turn-ins; it is an estimate, not a forecast.</p>
    </div>
  )
}

export function WhatIfCell({ perWeek, cut, ctx }: { perWeek: number; cut: number; ctx: LedgerContext | null }) {
  const saved = (perWeek * cut) / 100
  const savedEl = (
    <span>
      saves <Money value={saved} /> /week
    </span>
  )
  if (!ctx || ctx.supply <= 0 || ctx.weeklyPct === null) return savedEl
  const now = ctx.weeklyPct
  const after = ((ctx.netPerWeek - saved) / ctx.supply) * 100
  const inBand = after >= ctx.targetMinWeeklyPct && after <= ctx.targetMaxWeeklyPct
  return (
    <div>
      {savedEl}
      <div className="text-[11px] text-neutral-500 tabular-nums">
        {pct(now)}% {'->'} <span className={inBand ? 'text-emerald-400 font-semibold' : 'text-neutral-300'}>{pct(after)}%</span> per week
      </div>
    </div>
  )
}
