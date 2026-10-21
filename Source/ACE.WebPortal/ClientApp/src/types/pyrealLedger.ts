/** Shapes returned by PyrealLedgerController (/api/audit/pyreals). JSON is camelCase. */

export type LedgerDirection = 'in' | 'out'

export interface LedgerSourceTotal {
  source: string
  amountIn: number
  amountOut: number
  events: number
}

export interface LedgerItemTotal {
  direction: LedgerDirection
  source: string
  value: number
  units: number
}

export interface LedgerFlagCount {
  flag: string
  count: number
  amount: number
}

export interface LedgerOverview {
  ledgerStartUtc: string | null
  ledgerRunning: boolean
  days: number
  bankTotals: LedgerSourceTotal[]
  itemTotals: LedgerItemTotal[]
  flagCounts: LedgerFlagCount[]
}

export interface LedgerSuspectRow {
  accountId: number
  accountName: string
  /** Comma separated character names. */
  characters: string
  bankIn: number
  bankOut: number
  vendorSellIn: number
  topVendor: string | null
  topVendorPayout: number
  unattributedIn: number
  currencyIn: number
  currencyOut: number
  currencyDeposited: number
  /** ALL-TIME (ignores the period): how far the account's currency position is below zero. */
  currencySurplus: number
  flags: Record<string, number>
  flaggedAmount: number
  /** Number of reasons. */
  score: number
  /** Earned minus spent, moves excluded. */
  created: number
  peakHourAmount: number
  peakHourUtc: string | null
  peakHourSource: string | null
  activeHours: number
  windowHours: number
  topSource: string | null
  topSourceAmount: number
  topSourceTotal: number
  topSourceUsers: number
  topSourceSharePct: number
  /** Integrity problems only (dupe flags, disposed beyond received, unlabeled bank gains). score = reasons.length. */
  reasons: string[]
  /** Neutral heavy-earner statements (share of one source, best hour, hours active). */
  highlights: string[]
  reviewed: boolean
  reviewedNote: string | null
  reviewedBy: string | null
  reviewedUtc: string | null
  /** Reviewed, but a new flag appeared after the review. */
  newSinceReview: boolean
}

export interface LedgerReviewMark {
  accountId: number
  note: string
  by: string
  utc: string
}

export interface LedgerContext {
  days: number
  coveredDays: number
  created: number
  removed: number
  createdPerWeek: number
  removedPerWeek: number
  netPerWeek: number
  supply: number
  weeklyPct: number | null
  targetMinWeeklyPct: number
  targetMaxWeeklyPct: number
}

export interface LedgerNpcHandinRow {
  itemWcid: number
  itemName: string | null
  units: number
  gives: number
  characters: number
  topGiver: string | null
  topGiverUnits: number
}

export interface LedgerSuspectThresholds {
  minAmount: number
  minSurplus: number
  sharePct: number
  activePct: number
  peakTimes: number
}

export type LedgerEarnerScope = 'account' | 'character'

export interface LedgerEarnerRow {
  /** accountId when by=account, charId when by=character. */
  id: number
  name: string
  accountName: string | null
  bankIn: number
  bankOut: number
  net: number
  bySource: LedgerSourceTotal[]
}

export interface LedgerVendorRow {
  vendorWcid: number
  vendorName: string
  /** The vendor's current pay multiplier. Normal is <= 1. */
  buyPriceNow: number | null
  payout: number
  sales: number
  characters: number
  accounts: number
  perSale: number
  topSeller: string | null
  topSellerPayout: number
  topSellerSharePct: number
  topSellerHours: number
  perWeek: number
  shareOfCreationPct: number
}

export interface LedgerVendorSellerRow {
  charId: number
  charName: string
  accountId: number
  accountName: string
  payout: number
  sales: number
  firstHourUtc: string
  lastHourUtc: string
}

export interface LedgerFlagRow {
  id: number
  utc: string
  charId: number
  charName: string
  accountId: number
  accountName: string
  flag: string
  amount: number
  expected: number
  actual: number
  detail: string | null
}

export interface LedgerDetailBankRow {
  source: string
  detailKey: string | null
  detailName: string | null
  amountIn: number
  amountOut: number
  events: number
}

export interface LedgerDetailItemRow {
  direction: LedgerDirection
  source: string
  wcid: number
  itemName: string | null
  detailName: string | null
  units: number
  value: number
}

export interface LedgerHourRow {
  hourUtc: string
  bankIn: number
  bankOut: number
}

export interface LedgerCharacterSummary {
  charId: number
  name: string
  balance: number
  bankIn: number
  bankOut: number
}

export interface LedgerCharacterState {
  liveBalance: number
  savedBalance: number
  savedUtc: string | null
  pendingNotes: string | null
  /** All-time currency position of this character (see LedgerDetail.currencyPosition). */
  currencyPosition?: number | null
  updatedUtc: string
}

export type LedgerDetailKind = 'character' | 'account'

export interface LedgerDetail {
  kind: LedgerDetailKind
  id: number
  name: string
  accountId: number
  accountName: string | null
  days: number
  /** Current banked pyreals; for an account the sum of its characters. */
  balance: number | null
  state: LedgerCharacterState | null
  characters: LedgerCharacterSummary[]
  bank: LedgerDetailBankRow[]
  items: LedgerDetailItemRow[]
  hours: LedgerHourRow[]
  flags: LedgerFlagRow[]
  /**
   * All-time face value of currency this character / account should be holding:
   * held when tracking began + seen arriving - seen leaving. Negative = the dupe signal.
   */
  currencyPosition: number | null
  /** Items this character / account sold to vendors. */
  sold: LedgerDetailSoldRow[]
}

export interface LedgerDetailSoldRow {
  wcid: number
  itemName: string | null
  vendorWcid: number
  vendorName: string | null
  units: number
  payout: number
}

export interface LedgerItemSoldRow {
  wcid: number
  itemName: string | null
  /** Total quantity sold. */
  units: number
  /** Pyreals paid. */
  payout: number
  characters: number
  accounts: number
  topSeller: string | null
  topSellerCharId: number
  topSellerUnits: number
}

export interface LedgerItemSellerRow {
  charId: number
  charName: string
  accountId: number
  accountName: string
  units: number
  payout: number
  /** Comma separated vendor names. */
  vendors: string
  firstHourUtc: string
  lastHourUtc: string
}

export interface LedgerSearchRow {
  charId: number
  name: string
  accountId: number
  accountName: string | null
  balance: number
}

export interface LedgerNpcRow {
  npcWcid: number
  npcName: string | null
  /** Face value of coins / notes / peas the NPC handed out. */
  currencyValue: number
  /** Item count. */
  units: number
  /** Pyreals the NPC credited straight to the bank. */
  bankIn: number
  /** currencyValue + bankIn. */
  total: number
  gives: number
  characters: number
  accounts: number
  topReceiver: string | null
  topReceiverCharId: number
  topReceiverValue: number
  perGive: number
  topReceiverSharePct: number
  busiestHourValue: number
  busiestHourGives: number
  busiestHourChar: string | null
  busiestHourUtc: string | null
  perWeek: number
  shareOfCreationPct: number
  /** Items handed in to this NPC (units). */
  handedInUnits: number
}

export interface LedgerNpcReceiverRow {
  charId: number
  charName: string
  accountId: number
  accountName: string
  currencyValue: number
  units: number
  bankIn: number
  total: number
  gives: number
  firstHourUtc: string
  lastHourUtc: string
}

export interface LedgerTransferAccount {
  accountId: number
  accountName: string
  characters: string | null
  sent: number
  received: number
  net: number
  partners: number
  events: number
  isExchange: boolean
}

export interface LedgerTransferPair {
  fromAccountId: number
  fromAccountName: string
  toAccountId: number
  toAccountName: string
  amount: number
  events: number
  viaExchange: boolean
}

export interface LedgerTransfers {
  days: number
  crossAccount: number
  sameAccount: number
  viaExchanges: number
  exchangeAccounts: number[]
  accounts: LedgerTransferAccount[]
  pairs: LedgerTransferPair[]
}

export interface LedgerCopyRow {
  utc: string
  charId: number
  charName: string
  accountId: number
  accountName: string
  copiedBalance: number
  balanceNow: number
  exists: boolean
  detail: string
}

export interface LedgerCopies {
  copiedTotal: number
  stillHeld: number
  rows: LedgerCopyRow[]
}

export type LedgerTab = 'suspects' | 'heavy' | 'earners' | 'transfers' | 'vendors' | 'items' | 'npcs' | 'flags' | 'overview'
