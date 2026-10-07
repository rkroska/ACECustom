import { useEffect, useState } from 'react'
import { api } from '../../services/api'

export const LEDGER_API = '/api/audit/pyreals'
export const LEDGER_BASE_PATH = '/audit/pyreals'

export const DAY_OPTIONS = [1, 7, 30, 90, 365] as const
export const DEFAULT_DAYS = 30

export function clampDays(raw: string | null | undefined): number {
  const n = Number(raw)
  if (!Number.isFinite(n) || n <= 0) return DEFAULT_DAYS
  return Math.min(365, Math.max(1, Math.round(n)))
}

// ---- formatting ---------------------------------------------------------------------------------------------

const compactFormatter = new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 })

export function formatFull(n: number | null | undefined): string {
  if (n === null || n === undefined || Number.isNaN(n)) return '-'
  return n.toLocaleString()
}

export function formatCompact(n: number | null | undefined): string {
  if (n === null || n === undefined || Number.isNaN(n)) return '-'
  if (Math.abs(n) < 10000) return n.toLocaleString()
  return compactFormatter.format(n)
}

export function formatSigned(n: number): string {
  return `${n > 0 ? '+' : ''}${formatCompact(n)}`
}

/** Server sends UTC ISO strings; tolerate a missing zone suffix by treating it as UTC. */
export function parseUtc(iso: string | null | undefined): Date | null {
  if (!iso) return null
  const hasZone = /([zZ]|[+-]\d\d:?\d\d)$/.test(iso)
  const d = new Date(hasZone ? iso : `${iso}Z`)
  return Number.isNaN(d.getTime()) ? null : d
}

export function formatDateTime(iso: string | null | undefined): string {
  const d = parseUtc(iso)
  if (!d) return '-'
  return d.toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' })
}

export function formatDate(iso: string | null | undefined): string {
  const d = parseUtc(iso)
  if (!d) return '-'
  return d.toLocaleDateString(undefined, { dateStyle: 'medium' })
}

// ---- flags --------------------------------------------------------------------------------------------------

export type FlagSeverity = 'critical' | 'high' | 'medium' | 'low' | 'info'

export interface FlagInfo {
  flag: string
  label: string
  severity: FlagSeverity
  explanation: string
}

export const FLAG_INFO: FlagInfo[] = [
  {
    flag: 'LikelyDupe',
    label: 'Likely dupe',
    severity: 'critical',
    explanation:
      'A server crash or restart rolled back a payment whose other side had already been saved (for example pyreals sent to an offline alt, or trade notes withdrawn). Those pyreals now exist twice.',
  },
  {
    flag: 'Unexplained',
    label: 'Unexplained',
    severity: 'high',
    explanation:
      'The balance loaded at startup matches neither what the game last had nor what it last saved. Something changed it outside the game (a database edit) or through an unknown path.',
  },
  {
    flag: 'Tampered',
    label: 'Tampered',
    severity: 'high',
    explanation: 'Deposited a currency item (coin, trade note, pea) worth more than its face value.',
  },
  {
    flag: 'Bypass',
    label: 'Bypass',
    severity: 'high',
    explanation: 'The bank balance changed without going through the ledger at all.',
  },
  {
    flag: 'RollbackGain',
    label: 'Rollback gain',
    severity: 'medium',
    explanation:
      'A crash rolled back purchases or spending, so the character kept pyreals they had already spent. Often innocent.',
  },
  {
    flag: 'Unattributed',
    label: 'Unattributed',
    severity: 'low',
    explanation:
      'A bank change came from a code path nobody labeled. The detail holds a stack trace for developers to find and label it.',
  },
  {
    flag: 'RollbackLoss',
    label: 'Rollback loss',
    severity: 'info',
    explanation: 'A crash rolled back earnings, so the character lost pyreals. Not a cheat; listed for completeness.',
  },
  {
    flag: 'CopyChar',
    label: 'Copied character',
    severity: 'info',
    explanation: 'An admin copied a character that holds pyreals, which creates new pyreals on the copy.',
  },
]

const FLAG_MAP = new Map(FLAG_INFO.map(f => [f.flag, f]))
const SEVERITY_ORDER: Record<FlagSeverity, number> = { critical: 0, high: 1, medium: 2, low: 3, info: 4 }

export function flagInfo(flag: string): FlagInfo {
  return FLAG_MAP.get(flag) ?? { flag, label: flag, severity: 'info', explanation: 'Unknown flag type.' }
}

export function sortFlagsBySeverity<T extends { flag: string }>(rows: T[]): T[] {
  return [...rows].sort((a, b) => SEVERITY_ORDER[flagInfo(a.flag).severity] - SEVERITY_ORDER[flagInfo(b.flag).severity])
}

export const SEVERITY_CLASSES: Record<FlagSeverity, string> = {
  critical: 'bg-red-600/20 text-red-300 border-red-500/40',
  high: 'bg-orange-500/15 text-orange-300 border-orange-500/30',
  medium: 'bg-amber-500/15 text-amber-300 border-amber-500/30',
  low: 'bg-yellow-500/10 text-yellow-200/80 border-yellow-500/20',
  info: 'bg-neutral-800 text-neutral-400 border-neutral-700',
}

export const SEVERITY_LABEL: Record<FlagSeverity, string> = {
  critical: 'Highest',
  high: 'High',
  medium: 'Medium',
  low: 'Low',
  info: 'Info',
}

// ---- sources ------------------------------------------------------------------------------------------------

export const SOURCE_INFO: Record<string, string> = {
  VendorSell: 'Sold items to a vendor',
  VendorBuy: 'Bought items from a vendor',
  Deposit: 'Deposited coins / notes into the bank',
  Withdraw: 'Withdrew coins / notes from the bank',
  Transfer: 'Bank-to-bank transfer between characters',
  Emote: 'NPC quest reward or script',
  Command: 'An admin or player @command',
  Give: 'Given by another player',
  GiveNPC: 'Given to / by an NPC',
  Trade: 'Player trade window',
  Loot: 'Looted from a creature corpse',
  PlayerCorpse: 'Looted from a player corpse',
  Ground: 'Picked up from the ground',
  Chest: 'Taken from a chest',
  Recipe: 'Crafting recipe',
  KillReward: 'Kill reward (bounty)',
  PetDevice: 'Pet device',
  Clap: 'Clap',
  BankClamp: 'Balance clamped to the bank limit',
  CopyChar: 'Admin copied a character',
  Death: 'Lost on death',
  Opening: 'Held when tracking began',
  Unattributed: 'Unlabeled code path - suspicious by itself',
}

export function sourceHelp(source: string): string {
  return SOURCE_INFO[source] ?? source
}

/** VendorSell detail names look like "Vendor Name (buys at 1.5x)". Returns the multiplier when present. */
export function parseVendorMultiplier(detailName: string | null | undefined): number | null {
  if (!detailName) return null
  const m = /buys at\s+([\d.]+)\s*x/i.exec(detailName)
  if (!m) return null
  const n = Number(m[1])
  return Number.isFinite(n) ? n : null
}

// ---- links --------------------------------------------------------------------------------------------------

export function characterPath(charId: number, days: number): string {
  return `${LEDGER_BASE_PATH}/character/${charId}?days=${days}`
}

export function itemPath(wcid: number, days: number): string {
  return `${LEDGER_BASE_PATH}?tab=items&item=${wcid}&days=${days}`
}

export function npcPath(wcid: number, days: number): string {
  return `${LEDGER_BASE_PATH}?tab=npcs&npc=${wcid}&days=${days}`
}

export function accountPath(accountId: number, days: number): string {
  return `${LEDGER_BASE_PATH}/account/${accountId}?days=${days}`
}

// ---- data loading -------------------------------------------------------------------------------------------

export interface LedgerFetchState<T> {
  data: T | null
  loading: boolean
  error: string | null
}

function describeError(err: unknown): string {
  if (!(err instanceof Error)) return 'Failed to load pyreal ledger data.'
  const e = err as Error & { code?: string; correlationId?: string }
  if (e.code === '403') return 'You do not have access to the Pyreal Ledger.'
  const id = e.correlationId ? ` (correlation id ${e.correlationId})` : ''
  return `${e.message || 'Failed to load pyreal ledger data.'}${id}`
}

/** GETs a ledger endpoint; pass null to skip. Re-fetches when the path changes and aborts stale requests. */
export function useLedgerFetch<T>(path: string | null): LedgerFetchState<T> {
  const [state, setState] = useState<LedgerFetchState<T>>({ data: null, loading: path !== null, error: null })

  useEffect(() => {
    if (path === null) {
      setState({ data: null, loading: false, error: null })
      return
    }
    const controller = new AbortController()
    setState({ data: null, loading: true, error: null })
    api
      .get<T>(`${LEDGER_API}${path}`, { signal: controller.signal })
      .then(data => {
        if (!controller.signal.aborted) setState({ data, loading: false, error: null })
      })
      .catch(err => {
        if (controller.signal.aborted || (err instanceof Error && err.name === 'AbortError')) return
        setState({ data: null, loading: false, error: describeError(err) })
      })
    return () => controller.abort()
  }, [path])

  return state
}
