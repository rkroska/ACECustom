import type { ItemSearchResult } from '../../types'
import { sortBySearchRelevance } from './searchRelevance'

export function normalizeItemRow(raw: Record<string, unknown>) {
  const wcid = Number(raw.wcid ?? raw.Wcid)
  if (!Number.isFinite(wcid) || wcid <= 0) return null
  return {
    wcid,
    name: String(raw.name ?? raw.Name ?? `WCID ${wcid}`),
    className: String(raw.className ?? raw.ClassName ?? ''),
    weenieType: String(raw.weenieType ?? raw.WeenieType ?? ''),
  } satisfies ItemSearchResult
}

export function normalizeItemList(data: unknown, query?: string): ItemSearchResult[] {
  if (!Array.isArray(data)) return []
  const rows = data
    .map((row) => normalizeItemRow(row as Record<string, unknown>))
    .filter((r): r is ItemSearchResult => r != null)
  if (!query?.trim()) return rows
  return sortBySearchRelevance(rows, query, (r) => r.name, (r) => r.className, (r) => r.wcid)
}
