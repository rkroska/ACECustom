/** Shapes returned by the economy audit endpoints (/api/audit/economy). JSON is camelCase. */

export type EconomyStatus = 'inflating' | 'healthy' | 'deflating' | 'unknown'
export type WeeklyPctBasis = 'snapshots' | 'flows' | 'none'

export interface EconomySupply {
  day: string | null
  banked: number
  items: number
  total: number
  holders: number
  activeSupply: number
  activeHolders: number
  top1PctAmount: number
  largestHolder: number
  adminSupply: number
  /** Net recorded flow since the previous snapshot; null when that period was not fully recorded. */
  flowNet: number | null
  /** Supply change since the previous snapshot that no recorded flow accounts for. */
  unexplained: number | null
}

export interface EconomyChange {
  amount: number
  pct: number
}

export interface EconomyFlowTotals {
  days: number
  faucets: number
  sinks: number
  net: number
  internal: number
  firstDay: string | null
}

export interface EconomySeriesPoint {
  day: string
  banked: number
  items: number
  total: number
  activeSupply: number
}

export interface EconomyFlowPoint {
  day: string
  faucets: number
  sinks: number
}

export interface CurrencySummary {
  key: string
  name: string
  supply: EconomySupply
  change7d: EconomyChange | null
  change30d: EconomyChange | null
  flow: EconomyFlowTotals
  weeklyPct: number | null
  weeklyPctBasis: WeeklyPctBasis
  status: EconomyStatus
  series: EconomySeriesPoint[]
  flowSeries: EconomyFlowPoint[]
}

export interface EconomyOverview {
  generatedUtc: string
  days: number
  flowTracking: boolean
  flowTrackingSinceUtc: string | null
  pyrealLedger: boolean
  lastSnapshotUtc: string | null
  targetMinWeeklyPct: number
  targetMaxWeeklyPct: number
  currencies: CurrencySummary[]
}

export interface EconomySourceRow {
  source: string
  kind: 'flow' | 'internal'
  amountIn: number
  amountOut: number
  events: number
  characters: number
}

export interface EconomyDetailRow {
  source: string
  detailKey: string
  detailName: string
  amountIn: number
  amountOut: number
  events: number
  characters: number
}

export interface EconomyEarnerRow {
  charId: number
  charName: string
  accountId: number
  amountIn: number
  amountOut: number
  net: number
}

export interface EconomyHolderRow {
  charId: number
  charName: string
  accountId: number
  banked: number
  lastLoginUtc: string | null
}

export interface EconomySettingRow {
  name: string
  value: string
  description: string
}

export interface CurrencyDetail {
  summary: CurrencySummary
  sources: EconomySourceRow[]
  faucets: EconomyDetailRow[]
  sinks: EconomyDetailRow[]
  earners: EconomyEarnerRow[]
  holders: EconomyHolderRow[]
  settings: EconomySettingRow[]
  notes: string[]
}

export interface EconomyPrompt {
  text: string
  currency: string
  days: number
}

export interface EconomySnapshotResult {
  started: boolean
  message: string
}
