/**
 * K / M / B / T / Q amounts (owner 2026-09-27: "Too hard to read 00000000000").
 * Same rule as the server's ShortNumber / Creature.FormatDamage mode 2: exact with commas below 10,000,
 * one optional decimal above (5B, 1.2M, 250K). Pinned to en-US so every browser prints what the server
 * prints (InvariantCulture): "1.2M", never "1,2M"; no grouping inside a unit ("1000K", as the server).
 * Amounts only - never run ids through this.
 */
const UNITS: [number, string][] = [
  [1e15, 'Q'],
  [1e12, 'T'],
  [1e9, 'B'],
  [1e6, 'M'],
  [1e3, 'K'],
]

export function formatShortNumber(value: number): string {
  const abs = Math.abs(value)
  if (abs < 10_000) return value.toLocaleString('en-US')
  for (const [size, suffix] of UNITS) {
    if (abs >= size) {
      const n = Math.round((abs / size) * 10) / 10   // one decimal, rounded - the server's "0.#"
      return `${value < 0 ? '-' : ''}${n.toLocaleString('en-US', { maximumFractionDigits: 1, useGrouping: false })}${suffix}`
    }
  }
  return value.toLocaleString('en-US')
}

/** Short or full, by the page toggle. */
export function formatAmount(value: number, short: boolean): string {
  return short ? formatShortNumber(value) : value.toLocaleString('en-US')
}
