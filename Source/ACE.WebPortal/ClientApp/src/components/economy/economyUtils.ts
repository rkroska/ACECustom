import { useCallback, useEffect, useState } from 'react'
import { api } from '../../services/api'
import type { EconomyStatus, WeeklyPctBasis } from '../../types/economy'

export { formatCompact, formatFull, formatSigned, formatDate, formatDateTime, parseUtc } from '../pyreal-ledger/ledgerUtils'

export const ECONOMY_API = '/api/audit/economy'
export const ECONOMY_BASE_PATH = '/audit/economy'
export const ECONOMY_DAY_OPTIONS = [7, 30, 90] as const
export const ECONOMY_DEFAULT_DAYS = 30

export function clampEconomyDays(raw: string | null | undefined): number {
  const n = Number(raw)
  return (ECONOMY_DAY_OPTIONS as readonly number[]).includes(n) ? n : ECONOMY_DEFAULT_DAYS
}

/**
 * The page's "active in the last N days" filter from the URL (?active=). null = not chosen, so the server applies
 * its default (economy_active_days). The server refuses windows it does not offer.
 */
export function parseActiveDays(raw: string | null | undefined): number | null {
  if (raw === null || raw === undefined || raw === '') return null
  const n = Number(raw)
  return Number.isInteger(n) && n >= 0 ? n : null
}

/** Query-string suffix for API calls: '' when no filter was chosen, else '&activeDays=N'. */
export function activeDaysQuery(active: number | null): string {
  return active === null ? '' : `&activeDays=${active}`
}

/** Short label for a window: 0 = all accounts. */
export function activeLabel(days: number): string {
  return days <= 0 ? 'All accounts' : `Active ${days}d`
}

export function currencyPath(key: string, days: number, active: number | null = null): string {
  return `${ECONOMY_BASE_PATH}/${encodeURIComponent(key)}?days=${days}${active === null ? '' : `&active=${active}`}`
}

export function formatPct(n: number | null | undefined, digits = 1): string {
  if (n === null || n === undefined || Number.isNaN(n)) return '-'
  return `${n > 0 ? '+' : ''}${n.toLocaleString(undefined, { maximumFractionDigits: digits })}%`
}

export const STATUS_LABEL: Record<EconomyStatus, string> = {
  inflating: 'Inflating',
  healthy: 'Healthy',
  deflating: 'Deflating',
  unknown: 'Unknown',
}

export const STATUS_CLASSES: Record<EconomyStatus, string> = {
  inflating: 'bg-red-600/20 text-red-300 border-red-500/40',
  healthy: 'bg-emerald-500/15 text-emerald-300 border-emerald-500/30',
  deflating: 'bg-blue-500/15 text-blue-300 border-blue-500/30',
  unknown: 'bg-neutral-800 text-neutral-400 border-neutral-700',
}

export function basisNote(basis: WeeklyPctBasis): string {
  switch (basis) {
    case 'snapshots':
      return 'from daily supply snapshots'
    case 'flows':
      return 'estimated from tracked faucets and sinks'
    default:
      return 'not enough data yet'
  }
}

export function statusOf(s: string | undefined): EconomyStatus {
  return s === 'inflating' || s === 'healthy' || s === 'deflating' ? s : 'unknown'
}

export function describeEconomyError(err: unknown): string {
  if (!(err instanceof Error)) return 'Failed to load economy data.'
  const e = err as Error & { code?: string; correlationId?: string }
  if (e.code === '403') return 'You do not have access to the Economy dashboard.'
  const id = e.correlationId ? ` (correlation id ${e.correlationId})` : ''
  return `${e.message || 'Failed to load economy data.'}${id}`
}

export interface EconomyFetchState<T> {
  data: T | null
  loading: boolean
  error: string | null
  /** The server's error code (e.g. 'invalid_days', '403'), when it sent one. */
  errorCode: string | null
  reload: () => void
}

/** GETs an economy endpoint; pass null to skip. Re-fetches when the path changes or reload() is called. */
export function useEconomyFetch<T>(path: string | null): EconomyFetchState<T> {
  const [tick, setTick] = useState(0)
  const [state, setState] = useState<{ data: T | null; loading: boolean; error: string | null; errorCode: string | null }>({
    data: null,
    loading: path !== null,
    error: null,
    errorCode: null,
  })
  const reload = useCallback(() => setTick(t => t + 1), [])

  useEffect(() => {
    if (path === null) {
      setState({ data: null, loading: false, error: null, errorCode: null })
      return
    }
    const controller = new AbortController()
    setState({ data: null, loading: true, error: null, errorCode: null })
    api
      .get<T>(`${ECONOMY_API}${path}`, { signal: controller.signal })
      .then(data => {
        if (!controller.signal.aborted) setState({ data, loading: false, error: null, errorCode: null })
      })
      .catch(err => {
        if (controller.signal.aborted || (err instanceof Error && err.name === 'AbortError')) return
        const code = (err as { code?: string } | null)?.code
        setState({ data: null, loading: false, error: describeEconomyError(err), errorCode: code ?? null })
      })
    return () => controller.abort()
  }, [path, tick])

  return { ...state, reload }
}

export function dayLabel(day: string): string {
  const d = new Date(/T/.test(day) ? day : `${day}T00:00:00Z`)
  if (Number.isNaN(d.getTime())) return day
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', timeZone: 'UTC' })
}
