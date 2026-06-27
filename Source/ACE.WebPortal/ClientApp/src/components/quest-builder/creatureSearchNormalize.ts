import type { CreatureSearchResult } from '../../types/questBuilder'
import { sortBySearchRelevance } from './searchRelevance'

/** Normalize API rows (camelCase or PascalCase from server). */
export function normalizeCreatureRow(raw: Record<string, unknown>): CreatureSearchResult | null {
  const wcid = Number(raw.wcid ?? raw.Wcid)
  if (!Number.isFinite(wcid) || wcid <= 0) return null
  return {
    wcid,
    name: String(raw.name ?? raw.Name ?? `WCID ${wcid}`),
    className: String(raw.className ?? raw.ClassName ?? ''),
    weenieType: String(raw.weenieType ?? raw.WeenieType ?? ''),
  }
}

export function normalizeCreatureList(data: unknown, query?: string): CreatureSearchResult[] {
  if (!Array.isArray(data)) return []
  const rows = data
    .map((row) => normalizeCreatureRow(row as Record<string, unknown>))
    .filter((r): r is CreatureSearchResult => r != null)
  if (!query?.trim()) return rows
  return sortBySearchRelevance(rows, query, (r) => r.name, (r) => r.className, (r) => r.wcid)
}
