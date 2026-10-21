import { useMemo } from 'react'
import type { EconomyFlowPoint, EconomySeriesPoint } from '../../types/economy'
import { dayLabel, formatCompact, formatFull } from './economyUtils'

/** Total and active-player supply over time. Plain SVG line chart. */
export function SupplyChart({ series }: { series: EconomySeriesPoint[] }) {
  const geo = useMemo(() => {
    if (series.length < 2) return null
    const all = series.flatMap(p => [p.total, p.activeSupply])
    const min = Math.min(...all)
    const max = Math.max(...all)
    const span = max - min || 1
    const w = 600
    const h = 160
    const x = (i: number) => (i / (series.length - 1)) * w
    const y = (v: number) => h - 6 - ((v - min) / span) * (h - 12)
    return {
      w,
      h,
      min,
      max,
      total: series.map((p, i) => `${x(i).toFixed(1)},${y(p.total).toFixed(1)}`).join(' '),
      active: series.map((p, i) => `${x(i).toFixed(1)},${y(p.activeSupply).toFixed(1)}`).join(' '),
      x,
    }
  }, [series])

  if (!geo) {
    return <div className="p-10 text-center text-neutral-500 text-sm">No snapshots yet. Take a snapshot, or wait for the daily one; a line needs at least two days.</div>
  }

  return (
    <div className="p-4 space-y-2">
      <div className="flex flex-wrap items-center gap-4 text-[11px] text-neutral-400">
        <span className="inline-flex items-center gap-1.5">
          <span className="w-2.5 h-0.5 bg-blue-400" /> Total supply
        </span>
        <span className="inline-flex items-center gap-1.5">
          <span className="w-2.5 h-0.5 bg-emerald-400" /> Active players
        </span>
        <span className="text-neutral-600">
          Scale <span title={formatFull(geo.min)}>{formatCompact(geo.min)}</span> to <span title={formatFull(geo.max)}>{formatCompact(geo.max)}</span>.
          Hover a point for its value.
        </span>
      </div>
      <svg
        viewBox={`0 0 ${geo.w} ${geo.h}`}
        preserveAspectRatio="none"
        className="w-full h-44 rounded-lg bg-neutral-950/60 border border-neutral-800"
        role="img"
        aria-label="Supply over time"
      >
        <polyline points={geo.total} fill="none" stroke="currentColor" className="text-blue-400" strokeWidth={1.5} vectorEffect="non-scaling-stroke" />
        <polyline points={geo.active} fill="none" stroke="currentColor" className="text-emerald-400" strokeWidth={1.5} vectorEffect="non-scaling-stroke" />
        {series.map((p, i) => (
          <rect key={p.day} x={geo.x(i) - geo.w / series.length / 2} y={0} width={geo.w / series.length} height={geo.h} fill="transparent">
            <title>{`${dayLabel(p.day)} UTC\nTotal: ${formatFull(p.total)}\nBanked: ${formatFull(p.banked)}\nIn items: ${formatFull(p.items)}\nActive: ${formatFull(p.activeSupply)}`}</title>
          </rect>
        ))}
      </svg>
      <div className="flex justify-between text-[10px] text-neutral-600">
        <span>{dayLabel(series[0].day)}</span>
        <span>{dayLabel(series[series.length - 1].day)}</span>
      </div>
    </div>
  )
}

/** Faucets (up, green) and sinks (down, red) per day. Plain SVG. */
export function FlowChart({ flows }: { flows: EconomyFlowPoint[] }) {
  if (flows.length === 0) return <div className="p-10 text-center text-neutral-500 text-sm">No flow data yet.</div>

  const max = Math.max(1, ...flows.map(f => Math.max(f.faucets, f.sinks)))
  const barW = 10
  const width = flows.length * barW
  const height = 120
  const mid = height / 2
  const scale = (v: number) => (v / max) * (mid - 2)
  const totalIn = flows.reduce((s, f) => s + f.faucets, 0)
  const totalOut = flows.reduce((s, f) => s + f.sinks, 0)

  return (
    <div className="p-4 space-y-2">
      <div className="flex flex-wrap items-center gap-4 text-[11px] text-neutral-400">
        <span className="inline-flex items-center gap-1.5">
          <span className="w-2.5 h-2.5 rounded-sm bg-emerald-500/80" /> Faucets <span title={formatFull(totalIn)}>{formatCompact(totalIn)}</span>
        </span>
        <span className="inline-flex items-center gap-1.5">
          <span className="w-2.5 h-2.5 rounded-sm bg-red-500/70" /> Sinks <span title={formatFull(totalOut)}>{formatCompact(totalOut)}</span>
        </span>
        <span className="text-neutral-600">
          Per day (UTC). Tallest bar = <span title={formatFull(max)}>{formatCompact(max)}</span>.
        </span>
      </div>
      <svg
        viewBox={`0 0 ${width} ${height}`}
        preserveAspectRatio="none"
        className="w-full h-40 rounded-lg bg-neutral-950/60 border border-neutral-800"
        role="img"
        aria-label="Faucets and sinks per day"
      >
        <line x1={0} x2={width} y1={mid} y2={mid} stroke="currentColor" className="text-neutral-700" strokeWidth={0.5} vectorEffect="non-scaling-stroke" />
        {flows.map((f, i) => {
          const hIn = scale(f.faucets)
          const hOut = scale(f.sinks)
          const tip = `${dayLabel(f.day)} UTC\nFaucets: ${formatFull(f.faucets)}\nSinks: ${formatFull(f.sinks)}`
          return (
            <g key={f.day}>
              <rect x={i * barW} y={0} width={barW} height={height} fill="transparent">
                <title>{tip}</title>
              </rect>
              {hIn > 0 && (
                <rect x={i * barW + 1} y={mid - Math.max(hIn, 0.8)} width={barW - 2} height={Math.max(hIn, 0.8)} className="fill-emerald-500/80">
                  <title>{tip}</title>
                </rect>
              )}
              {hOut > 0 && (
                <rect x={i * barW + 1} y={mid} width={barW - 2} height={Math.max(hOut, 0.8)} className="fill-red-500/70">
                  <title>{tip}</title>
                </rect>
              )}
            </g>
          )
        })}
      </svg>
      <div className="flex justify-between text-[10px] text-neutral-600">
        <span>{dayLabel(flows[0].day)}</span>
        <span>{dayLabel(flows[flows.length - 1].day)}</span>
      </div>
    </div>
  )
}
