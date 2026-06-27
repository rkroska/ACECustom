export interface AuditPagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface TransferLogRow {
  id: number
  transferType: string
  fromPlayerName: string
  fromPlayerAccount: string | null
  toPlayerName: string
  toPlayerAccount: string | null
  itemName: string
  quantity: number
  timestamp: string
  fromAccountCreatedDate: string | null
  toAccountCreatedDate: string | null
  fromCharacterCreatedDate: string | null
  toCharacterCreatedDate: string | null
  additionalData: string | null
  fromPlayerIP: string | null
  toPlayerIP: string | null
}

export interface CharTrackerLoginRow {
  id: number
  characterId: number
  accountName: string | null
  characterName: string | null
  loginIP: string | null
  loginTimestamp: string
  connectionDuration: number
  landblock: string | null
}

export interface TransferSummaryRow {
  id: number
  fromPlayerName: string
  fromPlayerAccount: string | null
  toPlayerName: string
  toPlayerAccount: string | null
  transferType: string
  totalTransfers: number
  totalQuantity: number
  totalValue: number
  suspiciousTransfers: number
  isSuspicious: boolean
  firstTransfer: string
  lastTransfer: string
}

export type AuditTab = 'transfers' | 'logins' | 'summaries'

export interface AuditFilters {
  ip: string
  account: string
  character: string
  transferType: string
  itemContains: string
  days: number
}
