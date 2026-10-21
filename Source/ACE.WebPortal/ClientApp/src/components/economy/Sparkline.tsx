/** Tiny line chart of a number series. Plain SVG. */
export default function Sparkline({ values, className = '' }: { values: number[]; className?: string }) {
  if (values.length < 2) {
    return <div className={`text-[11px] text-neutral-600 h-8 flex items-center ${className}`}>no snapshots yet</div>
  }
  const w = 100
  const h = 28
  const min = Math.min(...values)
  const max = Math.max(...values)
  const span = max - min || 1
  const pts = values.map((v, i) => `${((i / (values.length - 1)) * w).toFixed(2)},${(h - 2 - ((v - min) / span) * (h - 4)).toFixed(2)}`)
  return (
    <svg viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none" className={`w-full h-8 ${className}`} role="img" aria-label="Total supply trend">
      <polyline points={pts.join(' ')} fill="none" stroke="currentColor" className="text-blue-400" strokeWidth={1.5} vectorEffect="non-scaling-stroke" />
    </svg>
  )
}
