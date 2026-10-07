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
  score: number
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

export type LedgerTab = 'suspects' | 'earners' | 'vendors' | 'items' | 'npcs' | 'flags' | 'overview'
