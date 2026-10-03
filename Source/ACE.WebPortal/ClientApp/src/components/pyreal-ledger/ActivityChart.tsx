import { useMemo } from 'react'
import type { LedgerHourRow } from '../../types/pyrealLedger'
import { formatFull, parseUtc } from './ledgerUtils'
import { Money } from './LedgerShared'

const HOUR_MS = 3600_000
const DAY_MS = 24 * HOUR_MS

interface Bucket {
  start: number
  bankIn: number
  bankOut: number
}

/** Bank in (up, green) and out (down, red) over time. Hourly for short ranges, daily otherwise. Plain SVG. */
export default function ActivityChart({ hours, days }: { hours: LedgerHourRow[]; days: number }) {
  const daily = days > 7
  const step = daily ? DAY_MS : HOUR_MS

  const { buckets, max, totalIn, totalOut } = useMemo(() => {
    const end = Math.floor(Date.now() / step) * step
    const start = end - (days * DAY_MS) + step
    const map = new Map<number, Bucket>()
    for (let t = start; t <= end; t += step) map.set(t, { start: t, bankIn: 0, bankOut: 0 })
    let tIn = 0
    let tOut = 0
    for (const h of hours) {
      const d = parseUtc(h.hourUtc)
      if (!d) continue
      const key = Math.floor(d.getTime() / step) * step
      let b = map.get(key)
      if (!b) {
        b = { start: key, bankIn: 0, bankOut: 0 }
        map.set(key, b)
      }
      b.bankIn += h.bankIn
      b.bankOut += h.bankOut
      tIn += h.bankIn
      tOut += h.bankOut
    }
    const list = [...map.values()].sort((a, b) => a.start - b.start)
    const m = Math.max(1, ...list.map(b => Math.max(b.bankIn, b.bankOut)))
    return { buckets: list, max: m, totalIn: tIn, totalOut: tOut }
  }, [hours, days, step])

  if (buckets.length === 0) return null

  const barW = 10
  const width = buckets.length * barW
  const height = 120
  const mid = height / 2
  const scale = (v: number) => (v / max) * (mid - 2)
  const label = (t: number) =>
    daily
      ? new Date(t).toLocaleDateString(undefined, { month: 'short', day: 'numeric', timeZone: 'UTC' })
      : new Date(t).toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', timeZone: 'UTC' })

  return (
    <div className="p-4 space-y-2">
      <div className="flex flex-wrap items-center gap-4 text-[11px] text-neutral-400">
        <span className="inline-flex items-center gap-1.5">
          <span className="w-2.5 h-2.5 rounded-sm bg-emerald-500/80" /> In <Money value={totalIn} className="text-emerald-300" />
        </span>
        <span className="inline-flex items-center gap-1.5">
          <span className="w-2.5 h-2.5 rounded-sm bg-red-500/70" /> Out <Money value={totalOut} className="text-red-300" />
        </span>
        <span className="text-neutral-600">
          Per {daily ? 'day' : 'hour'} (UTC). Tallest bar = <span title={`${formatFull(max)} pyreals`}>{max.toLocaleString()}</span>. Hover a bar
          for its value. A single huge spike is worth a look.
        </span>
      </div>
      <svg
        viewBox={`0 0 ${width} ${height}`}
        preserveAspectRatio="none"
        className="w-full h-40 rounded-lg bg-neutral-950/60 border border-neutral-800"
        role="img"
        aria-label="Bank pyreals in and out over time"
      >
        <line x1={0} x2={width} y1={mid} y2={mid} stroke="currentColor" className="text-neutral-700" strokeWidth={0.5} vectorEffect="non-scaling-stroke" />
        {buckets.map((b, i) => {
          const hIn = scale(b.bankIn)
          const hOut = scale(b.bankOut)
          const tip = `${label(b.start)} UTC\nIn: ${formatFull(b.bankIn)}\nOut: ${formatFull(b.bankOut)}`
          return (
            <g key={b.start}>
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
        <span>{label(buckets[0].start)}</span>
        <span>{label(buckets[buckets.length - 1].start)}</span>
      </div>
    </div>
  )
}
