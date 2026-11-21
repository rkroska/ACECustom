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
  /** The snapshot per account, limited by the page's activity filter; null for days without per-account rows. */
  accounts: EconomyAccountView | null
}

/** One snapshot added up per account, keeping accounts that played within activeDays of it (0 = all). */
export interface EconomyAccountView {
  activeDays: number
  banked: number
  items: number
  total: number
  accounts: number
  top1PctAmount: number
  largestHolder: number
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
  /** Supply of the accounts the activity filter keeps; null for days without per-account rows. */
  filteredTotal: number | null
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
  /** Activity filter applied to the per-account figures (0 = all accounts). */
  activeDays: number
  activeDayOptions: number[]
  /** First snapshot day with per-account rows, or null if none yet. */
  accountsSince: string | null
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
  /** Newest game login of any character on the account (what the activity filter uses). */
  accountLastLoginUtc: string | null
}

export interface EconomySettingRow {
  name: string
  value: string
  description: string
}

export interface CurrencyDetail {
  activeDays: number
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
  activeDays: number
}

export interface EconomySnapshotResult {
  started: boolean
  message: string
}

/** GET /api/audit/economy/stalled */
export interface StalledCharacter {
  charId: number
  charName: string
  enlightenment: number
  augs: number
}

export interface StalledCurrency {
  key: string
  name: string
  earned: number
  spentOnSelf: number
  exported: number
}

export interface StalledRecipient {
  accountId: number
  accountName: string
  charName: string
  currency: string
  currencyName: string
  amount: number
}

export interface StalledRow {
  accountId: number
  accountName: string
  maxEnlightenment: number
  augs: number
  stalledDays: number
  stallIsMinimum: boolean
  lastProgressUtc: string | null
  lastLoginUtc: string | null
  characters: StalledCharacter[]
  currencies: StalledCurrency[]
  recipients: StalledRecipient[]
}

export interface StalledReport {
  days: number
  stallDays: number
  minEnlightenment: number
  maxEnlightenment: number
  currency: string
  minExport: number
  progressSinceUtc: string | null
  flowTrackingSinceUtc: string | null
  rows: StalledRow[]
  notes: string[]
}
