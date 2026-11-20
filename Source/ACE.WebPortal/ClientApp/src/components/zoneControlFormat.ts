// Number wording shared by the Zone Control worked examples (kept out of the component file for fast refresh).

export const pct = (part: number, total: number) => {
  if (total <= 0) return '-'
  const p = (100 * part) / total
  return `${p >= 10 ? p.toFixed(0) : p >= 1 ? p.toFixed(1) : p.toPrecision(2)}%`
}

export const fmtCooldown = (m: number) => (m >= 60 && m % 60 === 0 ? `${m / 60} hour${m === 60 ? '' : 's'}` : `${m} minute${m === 1 ? '' : 's'}`)
