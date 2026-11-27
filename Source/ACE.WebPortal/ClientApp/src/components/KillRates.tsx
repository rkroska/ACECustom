import { Fragment, useCallback, useEffect, useMemo, useRef, useState, type KeyboardEvent as ReactKeyboardEvent, type ReactNode } from 'react'
import { useSearchParams } from 'react-router-dom'
import {
  ArrowDown,
  ArrowUp,
  ArrowUpDown,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Gauge,
  Info,
  RefreshCw,
  X,
} from 'lucide-react'
import PageHeader from './common/PageHeader'
import { api } from '../services/api'
import type { KillRateCharacter, KillRateHour, KillRateReport, KillStyle } from '../types/killRates'
import { ErrorState, LoadingState, Segmented } from './pyreal-ledger/LedgerShared'

const KILL_RATES_API = '/api/audit/kill-rates'
/** Hourly windows (from memory) and daily ones (7d / 30d, from the saved history). */
const HOUR_OPTIONS = ['6', '12', '24', '48', '7d', '30d'] as const
type HourOption = (typeof HOUR_OPTIONS)[number]
const DEFAULT_HOURS: HourOption = '24'

const PAGE_SIZES = [25, 50, 100, 200] as const
const DEFAULT_PAGE_SIZE = 100
/** Fallback for the server's minSteadyHours (an average over fewer clean hours is marked "~" and ranked after the rest). */
const MIN_AVG_HOURS = 3
/** A full hour below this share of the character's own average is tinted as a dip (a death, a stuck, a disconnect). */
const DIP_SHARE = 0.6
/** An average at or above this multiple of its tier's median is flagged as unusually high. */
const HIGH_FACTOR = 1.5
/** A tier needs this many steady averages before its median is trusted for the high flag. */
const MIN_TIER_SAMPLE = 3

/**
 * Kill styles in the server's order, each with its own colour. Kept clear of the colours that already mean something on
 * the page: red (dip), violet (high average), amber (off-tier), blue (now).
 */
const STYLES: { key: KillStyle; badge: string; text: string }[] = [
  { key: 'Melee', badge: 'bg-orange-500/15 text-orange-200 ring-orange-500/30', text: 'text-orange-300' },
  { key: 'Missile', badge: 'bg-emerald-500/15 text-emerald-200 ring-emerald-500/30', text: 'text-emerald-300' },
  { key: 'War', badge: 'bg-sky-500/15 text-sky-200 ring-sky-500/30', text: 'text-sky-300' },
  { key: 'Void', badge: 'bg-pink-500/15 text-pink-200 ring-pink-500/30', text: 'text-pink-300' },
  { key: 'Pet', badge: 'bg-lime-500/15 text-lime-200 ring-lime-500/30', text: 'text-lime-300' },
  { key: 'Other', badge: 'bg-neutral-700/40 text-neutral-300 ring-neutral-600/40', text: 'text-neutral-400' },
]
/** A style's place in STYLES (the server's order); -1 for none. */
const styleIndex = (s: KillStyle | null) => (s === null ? -1 : STYLES.findIndex(x => x.key === s))
/** The style filter from the URL (?style=); null = all styles, or an unknown value. */
const parseStyle = (raw: string | null): KillStyle | null => STYLES.find(s => s.key === raw)?.key ?? null

/** "Missile 92%, War 8%": each style's share of a character's kills, largest first. */
function styleBreakdown(c: KillRateCharacter): string {
  const total = Object.values(c.byStyle).reduce((a, b) => a + (b ?? 0), 0)
  return Object.entries(c.byStyle)
    .sort((a, b) => (b[1] ?? 0) - (a[1] ?? 0))
    .map(([k, v]) => `${k} ${Math.round(((v ?? 0) / Math.max(1, total)) * 100)}%`)
    .join(', ')
}

/** The window from the URL (?hours=); anything not offered falls back to 24. */
function parseHours(raw: string | null): HourOption {
  return (HOUR_OPTIONS as readonly string[]).includes(raw ?? '') ? (raw as HourOption) : DEFAULT_HOURS
}

/** The own-tier filter from the URL (?tier=11-25); null = all tiers. */
function parseTier(raw: string | null): number | null {
  const n = Number(raw)
  return Number.isInteger(n) && n >= 11 && n <= 25 ? n : null
}

/** A sortable column: a summary column, or one clock hour ("h" + its unix start, sorted by that hour's own-tier kills). */
type SortKey = 'name' | 'avg' | 'style' | 'hrs' | 'share' | 'kills' | `h${number}`
type SortDir = 'asc' | 'desc'
const SUMMARY_SORTS = ['name', 'avg', 'style', 'hrs', 'share', 'kills'] as const

/** The sort column from the URL (?sort=); null = the default order (own tier, then average, then name). */
function parseSortKey(raw: string | null): SortKey | null {
  if ((SUMMARY_SORTS as readonly string[]).includes(raw ?? '')) return raw as SortKey
  return raw !== null && /^h\d{9,11}$/.test(raw) ? (raw as SortKey) : null
}

/** Names and styles sort in their own order first (A-Z, Melee..Other), numbers high first. */
const defaultDir = (key: SortKey | null): SortDir => (key === 'name' || key === 'style' ? 'asc' : 'desc')

/** Whether a character's average rests on enough clean hours to rank (the server's rule, shared with /killrate). */
const steadyAvg = (c: KillRateCharacter) => c.avgOwnPerHour !== null && c.steady

/** Rank group for average sorts: steady averages, then short ones, then none. */
const avgGroup = (c: KillRateCharacter) => (c.avgOwnPerHour === null ? 2 : c.steady ? 0 : 1)

/** A day row (7d / 30d) carries its clean-hour count; an hour row has none (null, or missing from the JSON). */
const isDayRow = (h: KillRateHour) => typeof h.cleanHours === 'number'

/** The value a character sorts by in one column; null = nothing to show there (sorted last either way). */
function sortValue(c: KillRateCharacter, key: SortKey, byHour: Map<string, KillRateHour>): number | string | null {
  switch (key) {
    case 'name':
      return c.name.replace(/^\+/, '').toLowerCase()   // admins' "+" doesn't move them to the top
    case 'avg':
      return c.avgOwnPerHour
    case 'style':
      return c.mainStyle === null ? null : styleIndex(c.mainStyle)
    case 'hrs':
      return c.cleanHours
    case 'share':
      return c.ownShare
    case 'kills':
      return c.totalKills
    default: {
      // an hour column sorts by that hour's own-tier kills; a day column by that day's average per clean hour
      const h = byHour.get(`${c.guid}:${key.slice(1)}`)
      return h === undefined ? null : isDayRow(h) ? h.avgOwnPerHour : h.own
    }
  }
}

/** A load error for the page: no access, or the server's message with its correlation id. */
function describeError(err: unknown): string {
  if (!(err instanceof Error)) return 'Failed to load kill rates.'
  const e = err as Error & { code?: string; correlationId?: string }
  if (e.code === '403') return 'You do not have access to Kill Rates.'
  const id = e.correlationId ? ` (correlation id ${e.correlationId})` : ''
  return `${e.message || 'Failed to load kill rates.'}${id}`
}

const n0 = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 0 })
const pct = (share: number) => `${Math.round(share * 100)}%`
/** "T15", or "?" for an unknown tier (0). */
const tierText = (tier: number) => (tier > 0 ? `T${tier}` : '?')
/** The middle value (the mean of the two middle ones for an even count); callers pass a non-empty list. */
const median = (xs: number[]) => {
  const s = [...xs].sort((a, b) => a - b)
  const m = Math.floor(s.length / 2)
  return s.length % 2 ? s[m] : (s[m - 1] + s[m]) / 2
}

/** "Thu 3 AM-4 AM" in the viewer's local time. */
function hourLabel(hourStart: number): string {
  const start = new Date(hourStart * 1000)
  const end = new Date((hourStart + 3600) * 1000)
  const day = start.toLocaleDateString(undefined, { weekday: 'short' })
  // a half-hour time zone (+5:30, -3:30) starts UTC hours at :30 locally: show the minutes then
  const h = (d: Date) => d.toLocaleTimeString(undefined, d.getMinutes() === 0 ? { hour: 'numeric' } : { hour: 'numeric', minute: '2-digit' })
  return `${day} ${h(start)}-${h(end)}`
}

/** An hour column's start ("3 AM", "3:30 AM" in a half-hour zone) and its day ("Thu, Nov 26"), both local. */
function hourHead(hourStart: number): { day: string; time: string } {
  const d = new Date(hourStart * 1000)
  return {
    day: d.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' }),
    time: d.toLocaleTimeString(undefined, d.getMinutes() === 0 ? { hour: 'numeric' } : { hour: 'numeric', minute: '2-digit' }),
  }
}

/** A day column (7d / 30d): its UTC date ("Thu 26") and month ("Nov 2026"). Days are UTC days, as the server stores them. */
function dayHead(dayStart: number): { day: string; time: string } {
  const d = new Date(dayStart * 1000)
  return {
    day: d.toLocaleDateString(undefined, { timeZone: 'UTC', month: 'short', year: 'numeric' }),
    time: d.toLocaleDateString(undefined, { timeZone: 'UTC', weekday: 'short', day: 'numeric' }),
  }
}

/** "Thu, Nov 26 (UTC)" for a day column's tooltip. */
const dayLabel = (dayStart: number) =>
  `${new Date(dayStart * 1000).toLocaleDateString(undefined, { timeZone: 'UTC', weekday: 'short', month: 'short', day: 'numeric' })} (UTC)`

const clock = (unix: number) => new Date(unix * 1000).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' })

/** Hover text for one cell: own tier, kills per tier, first and last kill, and whether the hour is in the average (for a day
 * cell: that day's clean hours and their average). */
function cellTitle(c: KillRateCharacter, h: KillRateHour, dip: boolean): string {
  const tiers = Object.entries(h.byTier)
    .sort((a, b) => Number(b[0]) - Number(a[0]))
    .map(([t, k]) => `T${t}: ${n0(k)}`)
    .join(', ')
  if (isDayRow(h)) {
    const avg = h.avgOwnPerHour !== null ? `avg ${n0(h.avgOwnPerHour)} own-tier kills per clean hour over ${h.cleanHours} clean hour${h.cleanHours === 1 ? '' : 's'}` : 'no clean hour that day'
    const dipDay = dip ? `\nunder ${Math.round(DIP_SHARE * 100)}% of this character's average (${n0(c.avgOwnPerHour ?? 0)})` : ''
    return `${c.name} - ${dayLabel(h.hourStart)}${h.current ? ', today so far' : ''}\n${avg}\n${n0(h.own)} own-tier of ${n0(h.total)} kills; ${tiers}${dipDay}`
  }
  const state = h.current
    ? 'hour still running'
    : h.part
      ? 'part hour, not in the average'
      : h.clean
        ? 'full hour, in the average'
        : h.ownTier <= 0
          ? 'own tier unknown, not in the average'
          : 'too many off-tier kills, not in the average'
  const dipText = dip ? `\nunder ${Math.round(DIP_SHARE * 100)}% of this character's average (${n0(c.avgOwnPerHour ?? 0)})` : ''
  return `${c.name} - ${hourLabel(h.hourStart)}\nown tier ${tierText(h.ownTier)}; ${tiers}\nfirst kill ${clock(h.first)}, last ${clock(h.last)}\n${state}${dipText}`
}

/** Page numbers around the current one, with gaps: 1 ... 4 5 [6] 7 8 ... 20. */
function pageList(page: number, total: number): (number | 'gap')[] {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1)
  const keep = new Set([1, total, page - 1, page, page + 1].filter(p => p >= 1 && p <= total))
  const out: (number | 'gap')[] = []
  let prev = 0
  for (const p of [...keep].sort((a, b) => a - b)) {
    if (p - prev > 1) out.push('gap')
    out.push(p)
    prev = p
  }
  return out
}

// ---- table styling (the portal's dark surfaces, used as a data grid) --------------------------------------------------

/** Opaque row colours: pinned cells must hide what scrolls under them, so rows can't use translucent zebra tints. */
const ROW_BG = ['bg-neutral-900', 'bg-[#1b1b1b]'] as const
const CELL = 'px-3 py-1.5 border-b border-neutral-800/70'
const NUM = `${CELL} text-right tabular-nums whitespace-nowrap`
const HOUR = 'px-2 py-1.5 border-b border-neutral-800/70 text-right tabular-nums whitespace-nowrap min-w-[4.25rem]'
const TH = 'px-3 py-2 bg-neutral-950 text-[11px] font-bold uppercase tracking-wider text-neutral-400 whitespace-nowrap border-b border-neutral-800'
/** The pinned group: name (with tier badge) then average, with a shadow edge where the hours scroll under. */
const PIN_NAME = 'sticky left-0 w-[11rem] min-w-[11rem] max-w-[11rem]'
const PIN_AVG = 'sticky left-[11rem] w-[5.5rem] min-w-[5.5rem] border-r-2 border-r-neutral-700 shadow-[8px_0_8px_-4px_rgba(0,0,0,.9)]'
/** Clean hrs / On-tier % / All kills: below xl they give way to the hours and move into the Avg tooltip. */
const WIDE_ONLY = 'hidden xl:table-cell'

const CHIP = 'inline-flex items-center gap-1.5 rounded-md border border-neutral-800 bg-neutral-950 px-2 py-0.5 text-[11px] text-neutral-400 whitespace-nowrap'
const BTN = 'inline-flex items-center gap-1.5 rounded-lg bg-neutral-800 hover:bg-neutral-700 px-2.5 py-1 text-xs font-semibold text-neutral-200 focus-visible:outline focus-visible:outline-2 focus-visible:outline-blue-500'

/**
 * Kill Rates: T11+ kills per character per clock hour, from the server's in-memory counters (the same numbers as the
 * in-game /killrate). One row per character: a pinned name and average, the rest of the summary, then one column per
 * hour, newest first. URL state: ?hours=&tier=&q=&now=&sort=&dir=&page=&size=
 */
export default function KillRates() {
  const [params, setParams] = useSearchParams()
  const hours = parseHours(params.get('hours'))
  const tier = parseTier(params.get('tier'))
  const urlQ = params.get('q') ?? ''
  const query = urlQ.trim().toLowerCase()
  const playingNow = params.get('now') === '1'
  const styleFilter = parseStyle(params.get('style'))
  const requestedSort = parseSortKey(params.get('sort'))

  // the report remembers its window: after a switch (48h -> 6h) the old window's table is not shown under the new
  // selector, even when the new request fails; a Refresh of the same window keeps it on screen while it reloads
  const [loaded, setLoaded] = useState<{ window: string; report: KillRateReport } | null>(null)
  const data = loaded !== null && loaded.window === hours ? loaded.report : null
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [tick, setTick] = useState(0)
  const reload = useCallback(() => setTick(t => t + 1), [])

  // a sort on an hour that has left the window (a shorter window, or it rolled out on Refresh) is no sort at all
  const sortKey: SortKey | null =
    requestedSort !== null && /^h\d+$/.test(requestedSort) && data !== null && !data.hourStarts.includes(Number(requestedSort.slice(1)))
      ? null
      : requestedSort
  const sortDir: SortDir = params.get('dir') === 'asc' ? 'asc' : params.get('dir') === 'desc' ? 'desc' : defaultDir(sortKey)
  const minSteady = data?.minSteadyHours ?? MIN_AVG_HOURS

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setError(null)
    api
      .get<KillRateReport>(`${KILL_RATES_API}?${hours.endsWith('d') ? `days=${parseInt(hours, 10)}` : `hours=${hours}`}`, {
        signal: controller.signal,
      })
      .then(res => {
        if (!controller.signal.aborted) {
          setLoaded(res === null ? null : { window: hours, report: res })
          setLoading(false)
        }
      })
      .catch(err => {
        if (controller.signal.aborted || (err instanceof Error && err.name === 'AbortError')) return
        setError(describeError(err))
        setLoading(false)
      })
    return () => controller.abort()
  }, [hours, tick])

  /** Sets URL settings in place (no history entry per keystroke or click); anything but the page itself goes back to page 1. */
  const setParam = useCallback(
    (changes: Record<string, string | null>) => {
      setParams(
        prev => {
          const next = new URLSearchParams(prev)
          for (const [key, value] of Object.entries(changes)) {
            if (value === null) next.delete(key)
            else next.set(key, value)
          }
          if (!('page' in changes)) next.delete('page')
          return next
        },
        { replace: true },
      )
    },
    [setParams],
  )

  // the search box types locally and reaches the URL 200 ms after the last key; a URL change (a chip's x, Clear) refills it
  const [qInput, setQInput] = useState(urlQ)
  const [seenQ, setSeenQ] = useState(urlQ)
  if (urlQ !== seenQ) {
    setSeenQ(urlQ)
    setQInput(urlQ)
  }
  useEffect(() => {
    if (qInput === urlQ) return
    const t = setTimeout(() => setParam({ q: qInput === '' ? null : qInput }), 200)
    return () => clearTimeout(t)
  }, [qInput, urlQ, setParam])

  // the "How kills are counted" popover closes on Escape or a click outside it
  const [infoOpen, setInfoOpen] = useState(false)
  const infoRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (!infoOpen) return
    const onDown = (e: MouseEvent) => {
      if (infoRef.current && !infoRef.current.contains(e.target as Node)) setInfoOpen(false)
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setInfoOpen(false)
    }
    document.addEventListener('mousedown', onDown)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onDown)
      document.removeEventListener('keydown', onKey)
    }
  }, [infoOpen])

  // "Updated" turns amber once the data is over 5 minutes old (checked every 30 s)
  const [wallClock, setWallClock] = useState(() => Date.now())
  useEffect(() => {
    const t = setInterval(() => setWallClock(Date.now()), 30_000)
    return () => clearInterval(t)
  }, [])
  const stale = data !== null && wallClock / 1000 - data.generatedAt > 300

  // header keyboard: one Tab stop for the sort buttons; Left/Right (Home/End) move between them
  const [focusSort, setFocusSort] = useState<string>('name')
  /** Left/Right/Home/End on the header row move focus between the visible sort buttons. */
  const onHeaderKey = (e: ReactKeyboardEvent<HTMLTableRowElement>) => {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(e.key)) return
    const buttons = [...e.currentTarget.querySelectorAll<HTMLButtonElement>('button[data-sort]')].filter(b => b.offsetParent !== null)
    const at = buttons.findIndex(b => b === document.activeElement)
    if (at < 0) return
    e.preventDefault()
    const to = e.key === 'Home' ? 0 : e.key === 'End' ? buttons.length - 1 : Math.min(buttons.length - 1, Math.max(0, at + (e.key === 'ArrowRight' ? 1 : -1)))
    buttons[to].focus()
    buttons[to].scrollIntoView({ block: 'nearest', inline: 'nearest' })
  }

  const all = useMemo(() => data?.characters ?? [], [data])
  // the columns are clock hours, or UTC days for 7d / 30d (from the saved history); nowHour = the running column's start
  const bucketSeconds = data?.bucketSeconds ?? 3600
  const dayMode = bucketSeconds === 86400
  const nowHour = data ? data.generatedAt - (data.generatedAt % bucketSeconds) : 0
  // newest hour first
  const hourRows = useMemo(() => [...(data?.hourStarts ?? [])].reverse(), [data])
  const heads = useMemo(() => hourRows.map(s => (dayMode ? dayHead(s) : hourHead(s))), [hourRows, dayMode])
  const byHour = useMemo(() => {
    const map = new Map<string, KillRateHour>()
    for (const c of all) for (const h of c.hours) map.set(`${c.guid}:${h.hourStart}`, h)
    return map
  }, [all])

  // per tier, over everyone (not the filtered view): character count, and the median of steady averages for the high flag
  const tierStats = useMemo(() => {
    const m = new Map<number, { count: number; median: number | null; best: number | null; styles: Map<KillStyle, number> }>()
    const groups = new Map<number, KillRateCharacter[]>()
    for (const c of all) groups.set(c.ownTier, [...(groups.get(c.ownTier) ?? []), c])
    for (const [t, cs] of groups) {
      const avgs = cs.filter(steadyAvg).map(c => c.avgOwnPerHour as number)
      // characters per main style, for the balance view in the tier heading
      const styles = new Map<KillStyle, number>()
      for (const c of cs) if (c.mainStyle) styles.set(c.mainStyle, (styles.get(c.mainStyle) ?? 0) + 1)
      m.set(t, {
        count: cs.length,
        median: avgs.length >= MIN_TIER_SAMPLE ? median(avgs) : null,
        best: avgs.length ? Math.max(...avgs) : null,
        styles,
      })
    }
    return m
  }, [all])
  // characters per main style over everyone, for the style filter's counts
  const styleCounts = useMemo(() => {
    const m = new Map<KillStyle, number>()
    for (const c of all) if (c.mainStyle) m.set(c.mainStyle, (m.get(c.mainStyle) ?? 0) + 1)
    return m
  }, [all])
  const tierChips = useMemo(() => [...tierStats.keys()].filter(t => t > 0).sort((a, b) => b - a), [tierStats])

  const characters = useMemo(() => {
    const shown = all.filter(
      c =>
        (tier === null || c.ownTier === tier) &&
        (styleFilter === null || c.mainStyle === styleFilter) &&
        (query === '' || c.name.toLowerCase().includes(query)) &&
        (!playingNow || c.online),
    )
    // default: own tier high to low, steady averages before short ones, then the server's order (average, then name)
    if (sortKey === null) return [...shown].sort((a, b) => b.ownTier - a.ownTier || avgGroup(a) - avgGroup(b))
    const sign = sortDir === 'asc' ? 1 : -1
    // stable: ties keep the server's order; a character with no value (no average, no kills that hour) always goes last
    return [...shown].sort((a, b) => {
      if (sortKey === 'avg' && avgGroup(a) !== avgGroup(b)) return avgGroup(a) - avgGroup(b)
      const va = sortValue(a, sortKey, byHour)
      const vb = sortValue(b, sortKey, byHour)
      if (va === null || vb === null) return va === vb ? 0 : va === null ? 1 : -1
      return (typeof va === 'string' ? va.localeCompare(vb as string) : va - (vb as number)) * sign
    })
  }, [all, tier, styleFilter, query, playingNow, sortKey, sortDir, byHour])

  // paging (?page=, ?size=): a page past the end (the list shrank) shows the last page
  const pageSize = (PAGE_SIZES as readonly number[]).includes(Number(params.get('size'))) ? Number(params.get('size')) : DEFAULT_PAGE_SIZE
  const totalPages = Math.max(1, Math.ceil(characters.length / pageSize))
  const page = Math.min(totalPages, Math.max(1, Math.floor(Number(params.get('page')) || 1)))
  const firstRow = (page - 1) * pageSize
  const pageRows = characters.slice(firstRow, firstRow + pageSize)
  /** Shows page <p>; page 1 is the default and leaves no ?page= in the URL. */
  const goPage = (p: number) => setParam({ page: p <= 1 ? null : String(p) })

  const filtersOn = query !== '' || playingNow || tier !== null || styleFilter !== null
  /** Drops the search, Playing now, tier and style filters (not the window, sort or page size). */
  const clearFilters = () => {
    setQInput('')
    setParam({ q: null, now: null, tier: null, style: null })
  }

  /** Header click (or Enter/Space): a new column sorts its natural way; the same column again reverses. */
  const sortBy = (key: SortKey) =>
    setParam({ sort: key, dir: key === sortKey ? (sortDir === 'asc' ? 'desc' : 'asc') : defaultDir(key) })

  /** A header's sort button: the label, an arrow for the active direction (a faint two-way arrow otherwise), one Tab stop. */
  const sortHead = (key: SortKey, label: ReactNode, title: string, align: 'left' | 'right' = 'right') => {
    const active = key === sortKey
    const Icon = !active ? ArrowUpDown : sortDir === 'asc' ? ArrowUp : ArrowDown
    return (
      <button
        type="button"
        data-sort={key}
        tabIndex={key === focusSort ? 0 : -1}
        onFocus={() => setFocusSort(key)}
        onClick={() => sortBy(key)}
        title={`${title} - sort (Left/Right moves between columns)`}
        className={`inline-flex w-full items-center gap-1 rounded uppercase tracking-wider hover:text-neutral-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-blue-500 ${
          align === 'right' ? 'justify-end' : 'justify-start'
        } ${active ? 'text-blue-300' : ''}`}
      >
        {label}
        <Icon className={`w-3 h-3 shrink-0 ${active ? '' : 'opacity-40'}`} aria-hidden />
      </button>
    )
  }
  /** The th's aria-sort: the active column's direction, "none" elsewhere. */
  const ariaSort = (key: SortKey) => (key === sortKey ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none') as 'ascending' | 'descending' | 'none'

  // day groups for the top header row: consecutive hour columns on the same local day
  const dayGroups = useMemo(() => {
    const out: { day: string; span: number }[] = []
    for (const h of heads) {
      const last = out[out.length - 1]
      if (last && last.day === h.day) last.span++
      else out.push({ day: h.day, span: 1 })
    }
    return out
  }, [heads])
  /** True for an hour column that starts a new local day (gets the midnight divider). */
  const dayStart = (i: number) => i > 0 && heads[i].day !== heads[i - 1].day
  const colCount = 6 + hourRows.length

  return (
    <div className="flex flex-col h-full min-h-0 overflow-hidden p-4 sm:p-6 gap-4">
      <PageHeader title="Kill Rates" icon={Gauge} className="!mb-0 shrink-0 flex-wrap gap-3">
        {data && (
          <span
            className={`text-[11px] whitespace-nowrap ${stale ? 'text-amber-300' : 'text-neutral-400'}`}
            title={stale ? 'More than 5 minutes old: Refresh for the latest counts' : undefined}
          >
            Updated {clock(data.generatedAt)}
          </span>
        )}
        <div className="relative" ref={infoRef}>
          <button
            type="button"
            className={BTN}
            aria-expanded={infoOpen}
            aria-controls="kill-rates-info"
            onClick={() => setInfoOpen(o => !o)}
          >
            <Info className="w-3.5 h-3.5" aria-hidden /> How kills are counted
          </button>
          {infoOpen && (
            <div
              id="kill-rates-info"
              role="dialog"
              aria-label="How kills are counted"
              className="absolute right-0 top-full mt-2 z-40 w-[min(34rem,90vw)] rounded-xl border border-neutral-700 bg-neutral-900 p-3 shadow-2xl text-xs leading-relaxed text-neutral-300"
            >
              <p>
                Kills of T11-T25 Zone Control monsters that pay kill XP or luminance, credited to the top damager (a pet&apos;s
                kill to its owner). Fellowship and Zone Share members are not counted, and neither are mules. Each hour cell is
                that hour&apos;s kills <b>at the character&apos;s own tier</b>; <i>188 (389)</i> means 188 own-tier kills of 389 in
                all. The average uses only clean hours: full hours (first kill in the first {data?.partHourSlackMinutes ?? 5}{' '}
                minutes, last in the last {data?.partHourSlackMinutes ?? 5}) with at least{' '}
                {Math.round((data?.cleanOwnShare ?? 0.95) * 100)}% own-tier kills. The counters live in server memory: a restart
                clears them, and the server log keeps one [KILLRATE] line per character per hour. Times are your local time.
              </p>
              <p className="mt-2">
                <b>7d / 30d</b> read the saved history (each finished hour is stored,{' '}
                {!data || data.historyDays <= 0 ? 'kept indefinitely' : `kept ${n0(data.historyDays)} days`}): one column per UTC day,
                showing that day&apos;s average own-tier kills per clean hour. The summary columns cover the whole window.
              </p>
            </div>
          )}
        </div>
        <button type="button" onClick={reload} className={BTN} disabled={loading} aria-busy={loading}>
          <RefreshCw className={`w-3.5 h-3.5 ${loading ? 'animate-spin' : ''}`} aria-hidden /> Refresh
        </button>
      </PageHeader>

      {/* filters: window, tier, name, playing now, and what is in force */}
      <div className="shrink-0 flex flex-wrap items-center gap-2">
        <Segmented<HourOption>
          value={hours}
          options={HOUR_OPTIONS.map(h => ({ value: h, label: h.endsWith('d') ? h : `${h}h` }))}
          onChange={v => {
            // hour columns and day columns are different things: a column sort doesn't carry across (a day start can equal
            // an hour start and would quietly re-sort by something else)
            const crossing = v.endsWith('d') !== hours.endsWith('d') && requestedSort !== null && /^h\d+$/.test(requestedSort)
            setParam({ hours: v === DEFAULT_HOURS ? null : v, ...(crossing ? { sort: null, dir: null } : {}) })
          }}
        />
        <div className="inline-flex flex-wrap rounded-lg border border-neutral-800 bg-neutral-950 p-0.5" role="group" aria-label="Own tier">
          {[null, ...tierChips].map(t => {
            const on = t === tier
            const count = t === null ? all.length : tierStats.get(t)?.count ?? 0
            return (
              <button
                key={t ?? 'all'}
                type="button"
                aria-pressed={on}
                onClick={() => setParam({ tier: t === null ? null : String(t) })}
                className={`px-2.5 py-1 rounded-md text-xs font-semibold whitespace-nowrap transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-blue-500 ${
                  on ? 'bg-blue-600 text-white' : 'text-neutral-400 hover:text-neutral-200'
                }`}
              >
                {t === null ? 'All' : `T${t}`} <span className={on ? 'text-blue-100' : 'text-neutral-500'}>{count}</span>
              </button>
            )
          })}
        </div>
        <select
          value={styleFilter ?? ''}
          onChange={e => setParam({ style: e.target.value === '' ? null : e.target.value })}
          aria-label="Main kill style"
          className="bg-neutral-950 border border-neutral-800 rounded-lg px-2 py-1 text-xs text-neutral-200 focus-visible:outline focus-visible:outline-2 focus-visible:outline-blue-500"
        >
          <option value="">All styles</option>
          {STYLES.filter(s => styleCounts.has(s.key) || s.key === styleFilter).map(s => (
            <option key={s.key} value={s.key}>
              {s.key} ({styleCounts.get(s.key) ?? 0})
            </option>
          ))}
        </select>
        <input
          type="search"
          value={qInput}
          onChange={e => setQInput(e.target.value)}
          placeholder="Find character"
          aria-label="Find character"
          className="w-56 bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1 text-xs text-neutral-200 placeholder:text-neutral-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-blue-500"
        />
        <label className="inline-flex items-center gap-1.5 text-xs text-neutral-300 cursor-pointer select-none whitespace-nowrap">
          <input type="checkbox" checked={playingNow} onChange={e => setParam({ now: e.target.checked ? '1' : null })} className="accent-blue-500" />
          Playing now
        </label>
        {filtersOn && (
          <button
            type="button"
            className="inline-flex items-center gap-1 text-xs text-blue-400 hover:text-blue-300 hover:underline whitespace-nowrap focus-visible:outline focus-visible:outline-2 focus-visible:outline-blue-500 rounded"
            onClick={clearFilters}
          >
            <X className="w-3 h-3" aria-hidden /> Clear filters
          </button>
        )}
      </div>

      {error && data && <div className="shrink-0 text-xs text-red-400">Refresh failed: {error}</div>}

      {loading && !data ? (
        <LoadingState />
      ) : error && !data ? (
        <ErrorState message={error} />
      ) : (
        <section className="flex-1 min-h-0 flex flex-col rounded-2xl border border-neutral-800 bg-neutral-900/40 overflow-hidden">
          <header className="shrink-0 flex flex-wrap items-center justify-between gap-x-4 gap-y-2 px-4 py-2.5 border-b border-neutral-800">
            <h2 className="text-sm font-bold text-white whitespace-nowrap">
              Characters{' '}
              <span className="font-normal text-neutral-400">
                {characters.length === all.length ? n0(all.length) : `${n0(characters.length)} of ${n0(all.length)}`}
              </span>
            </h2>
            <div className="flex flex-wrap items-center gap-1.5" aria-label="Legend">
              {dayMode ? (
                <>
                  <span className={CHIP}>
                    <span className="w-2.5 h-2.5 rounded-sm bg-blue-500/40" aria-hidden /> Today (UTC) so far
                  </span>
                  <span className={CHIP}>day cell = avg kills per clean hour that day</span>
                  <span className={CHIP}>
                    <i className="text-neutral-500">-</i> no clean hour that day
                  </span>
                </>
              ) : (
                <>
                  <span className={CHIP}>
                    <span className="w-2.5 h-2.5 rounded-sm bg-blue-500/40" aria-hidden /> Now: running hour
                  </span>
                  <span className={CHIP}>
                    <i className="text-neutral-500">123</i> part hour
                  </span>
                  <span className={CHIP}>
                    <span className="text-amber-300">188</span>
                    <span className="text-neutral-400">(389)</span> off-tier kills
                  </span>
                </>
              )}
              <span className={CHIP}>
                <span className="w-2.5 h-2.5 rounded-sm bg-red-500/40" aria-hidden /> dip: under {Math.round(DIP_SHARE * 100)}% of avg
              </span>
              <span className={CHIP}>
                <span className="w-2.5 h-2.5 rounded-sm bg-violet-500/40" aria-hidden /> avg {HIGH_FACTOR}x+ tier median
              </span>
              <span className={CHIP}>
                <span className="text-neutral-400">~469</span> under {minSteady} clean hrs
              </span>
            </div>
          </header>

          {characters.length === 0 ? (
            <div className="flex-1 flex flex-col items-center justify-center gap-3 p-10 text-sm text-neutral-400">
              {all.length === 0 ? (
                hours.endsWith('d') ? (
                  <>No T11+ kills in the last {parseInt(hours, 10)} days of saved history.</>
                ) : (
                  <>No T11+ kills in the last {hours} hours. The hourly counters start empty after a server restart.</>
                )
              ) : (
                <>
                  No character matches the filters.
                  <button type="button" className={BTN} onClick={clearFilters}>
                    Clear filters
                  </button>
                </>
              )}
            </div>
          ) : (
            <div className={`flex-1 min-h-0 overflow-auto transition-opacity ${loading ? 'opacity-60' : ''}`}>
              <table className="w-full text-left text-xs border-separate border-spacing-0">
                <thead className="sticky top-0 z-20">
                  {/* day row: which day each block of hours belongs to */}
                  <tr>
                    <th colSpan={2} className={`${TH} ${PIN_NAME} z-30 !w-auto !max-w-none`} />
                    <th className={TH} />
                    <th colSpan={3} className={`${TH} ${WIDE_ONLY}`} />
                    {dayGroups.map((g, i) => (
                      <th
                        key={`${g.day}-${i}`}
                        colSpan={g.span}
                        className={`${TH} normal-case tracking-normal text-left text-neutral-300 ${i === 0 ? 'border-l border-l-neutral-800' : 'border-l border-l-neutral-600'}`}
                      >
                        {/* the label rides along just right of the pinned columns while its day is in view */}
                        <span className="sticky left-[17.25rem] inline-block">{g.day}</span>
                      </th>
                    ))}
                  </tr>
                  <tr
                    onKeyDown={onHeaderKey}
                    onBlur={e => {
                      // focus left the header row: the Tab stop goes back to Character, which always exists (an hour can
                      // leave the window and the narrow-screen columns hide, taking a remembered stop with them)
                      if (!e.currentTarget.contains(e.relatedTarget as Node | null)) setFocusSort('name')
                    }}
                  >
                    <th className={`${TH} ${PIN_NAME} z-30`} aria-sort={ariaSort('name')}>
                      {sortHead('name', 'Character', 'Character name', 'left')}
                    </th>
                    <th className={`${TH} ${PIN_AVG} z-30 text-right`} aria-sort={ariaSort('avg')}>
                      {sortHead('avg', 'Avg / hr', 'Average own-tier kills per clean hour')}
                    </th>
                    <th className={TH} aria-sort={ariaSort('style')}>
                      {sortHead('style', 'Style', 'Main kill style over the window: stance at the kill, magic by last cast school', 'left')}
                    </th>
                    <th className={`${TH} ${WIDE_ONLY} text-right`} aria-sort={ariaSort('hrs')}>
                      {sortHead('hrs', 'Clean hrs', 'Clean hours in the average')}
                    </th>
                    <th className={`${TH} ${WIDE_ONLY} text-right`} aria-sort={ariaSort('share')}>
                      {sortHead('share', 'On-tier %', 'Own-tier kills as a share of all T11+ kills in the window')}
                    </th>
                    <th className={`${TH} ${WIDE_ONLY} text-right`} aria-sort={ariaSort('kills')}>
                      {sortHead('kills', 'All kills', 'All T11+ kills in the window, any tier')}
                    </th>
                    {hourRows.map((start, i) => {
                      const key: SortKey = `h${start}`
                      const isNow = start === nowHour
                      return (
                        <th
                          key={start}
                          aria-sort={ariaSort(key)}
                          className={`${TH} !px-2 text-right ${i === 0 ? 'border-l border-l-neutral-800' : dayStart(i) ? 'border-l border-l-neutral-600' : ''} ${
                            isNow ? '!bg-[#0f1a2e]' : ''
                          }`}
                        >
                          {sortHead(
                            key,
                            isNow ? (
                              <span className="text-blue-300 normal-case">{dayMode ? 'Today' : 'Now'}</span>
                            ) : (
                              <span className="normal-case">{heads[i].time}</span>
                            ),
                            dayMode
                              ? `${dayLabel(start)}${isNow ? ', so far' : ''}: average own-tier kills per clean hour that day`
                              : `${hourLabel(start)}${isNow ? ' (running)' : ''}: kills that hour`,
                          )}
                        </th>
                      )
                    })}
                  </tr>
                </thead>
                <tbody>
                  {pageRows.map((c, row) => {
                    const bg = ROW_BG[row % 2]
                    const steady = steadyAvg(c)
                    const stats = tierStats.get(c.ownTier)
                    const high = steady && stats?.median != null && (c.avgOwnPerHour ?? 0) >= HIGH_FACTOR * stats.median
                    const lowShare = c.ownShare < (data?.cleanOwnShare ?? 0.95)
                    // heading rows: in the default order a tier heading wherever a tier starts (or carries over from the
                    // last page); sorted by average, a divider before the short averages and before the ones with none
                    const prev = row > 0 ? pageRows[row - 1] : firstRow > 0 ? characters[firstRow - 1] : undefined
                    let heading: ReactNode = null
                    if (sortKey === null && (row === 0 || prev?.ownTier !== c.ownTier)) {
                      heading = (
                        <>
                          <span className="font-bold text-neutral-200">{tierText(c.ownTier)}</span>
                          {prev?.ownTier === c.ownTier && <span className="text-neutral-500"> (continued)</span>}
                          {' · '}
                          {stats?.count ?? 0} character{stats?.count === 1 ? '' : 's'}
                          {stats?.median != null && <> · median {n0(stats.median)}/hr</>}
                          {stats?.best != null && <> · best {n0(stats.best)}</>}
                          {/* balance at a glance: how many characters in this tier mainly use each style */}
                          {stats &&
                            STYLES.filter(s => stats.styles.has(s.key)).map(s => (
                              <span key={s.key}>
                                {' · '}
                                <span className={s.text}>
                                  {s.key} {stats.styles.get(s.key)}
                                </span>
                              </span>
                            ))}
                        </>
                      )
                    } else if (sortKey === 'avg' && avgGroup(c) > 0 && (row === 0 || (prev && avgGroup(prev) !== avgGroup(c)))) {
                      heading =
                        avgGroup(c) === 1 ? (
                          <span className="text-neutral-300">Fewer than {minSteady} clean hours: ranked after the steady averages</span>
                        ) : (
                          <span className="text-neutral-300">No average yet: no clean hour in the window</span>
                        )
                    }
                    return (
                      <Fragment key={c.guid}>
                        {heading && (
                          <tr>
                            <td colSpan={colCount} className="bg-neutral-950 border-b border-neutral-800 p-0">
                              <div className="sticky left-0 w-fit px-3 py-1.5 text-[11px] text-neutral-400 whitespace-nowrap">{heading}</div>
                            </td>
                          </tr>
                        )}
                        <tr className={`group ${bg} hover:bg-neutral-800`}>
                          <td className={`${CELL} ${PIN_NAME} z-10 ${bg} group-hover:bg-neutral-800`}>
                            <div className="flex items-center justify-between gap-2">
                              <span className="truncate font-medium text-white" title={c.name}>
                                {c.name}
                              </span>
                              {/* the default order already heads each tier; the badge matters once rows are re-sorted */}
                              {sortKey !== null && (
                                <span className="shrink-0 rounded px-1.5 py-px text-[10px] font-bold bg-neutral-800 text-neutral-300 group-hover:bg-neutral-700">
                                  {tierText(c.ownTier)}
                                </span>
                              )}
                            </div>
                          </td>
                          <td
                            className={`${NUM} ${PIN_AVG} z-10 ${bg} group-hover:bg-neutral-800`}
                            title={`${c.cleanHours} clean hour${c.cleanHours === 1 ? '' : 's'} · ${pct(c.ownShare)} on-tier · ${n0(c.totalKills)} kills in all`}
                          >
                            {c.avgOwnPerHour === null ? (
                              <span className="text-neutral-500">-</span>
                            ) : !steady ? (
                              <span
                                className="text-neutral-400"
                                title={`Only ${c.cleanHours} clean hour${c.cleanHours === 1 ? '' : 's'}: ranked after averages over ${minSteady}+ hours`}
                              >
                                ~{n0(c.avgOwnPerHour)}
                              </span>
                            ) : high ? (
                              <span
                                className="rounded px-1.5 py-px bg-violet-500/20 text-violet-200 font-semibold"
                                title={`${((c.avgOwnPerHour ?? 0) / (stats?.median ?? 1)).toFixed(1)}x the ${tierText(c.ownTier)} median (${n0(stats?.median ?? 0)}/hr)`}
                              >
                                {n0(c.avgOwnPerHour)}
                              </span>
                            ) : (
                              <span className="font-semibold text-white">{n0(c.avgOwnPerHour)}</span>
                            )}
                          </td>
                          <td className={`${CELL} whitespace-nowrap`} title={c.mainStyle ? styleBreakdown(c) : undefined}>
                            {c.mainStyle && (() => {
                              const s = STYLES[styleIndex(c.mainStyle)] ?? STYLES[STYLES.length - 1]
                              const share = Math.round(((c.byStyle[c.mainStyle] ?? 0) / Math.max(1, c.totalKills)) * 100)
                              return (
                                <span className={`inline-flex items-center gap-1 rounded px-1.5 py-px text-[11px] font-semibold ring-1 ring-inset ${s.badge}`}>
                                  {c.mainStyle}
                                  {share < 90 && <span className="font-normal opacity-70">{share}%</span>}
                                </span>
                              )
                            })()}
                          </td>
                          <td className={`${NUM} ${WIDE_ONLY} text-neutral-400`}>{c.cleanHours}</td>
                          <td className={`${NUM} ${WIDE_ONLY} ${lowShare ? 'text-amber-300' : 'text-neutral-400'}`}>{pct(c.ownShare)}</td>
                          <td className={`${NUM} ${WIDE_ONLY} text-neutral-400`}>{n0(c.totalKills)}</td>
                          {hourRows.map((start, i) => {
                            const edge = i === 0 ? 'border-l border-l-neutral-800' : dayStart(i) ? 'border-l border-l-neutral-600' : ''
                            const nowCol = start === nowHour ? 'bg-blue-500/[0.06]' : ''
                            const h = byHour.get(`${c.guid}:${start}`)
                            if (!h) return <td key={start} className={`${HOUR} ${edge} ${nowCol}`} />
                            if (isDayRow(h)) {
                              // a day cell (7d / 30d): the average own-tier kills per clean hour that day; a dip is a past day
                              // well under the character's own steady average over the window
                              const avg = h.avgOwnPerHour
                              const dipDay = avg !== null && steady && !h.current && avg < DIP_SHARE * (c.avgOwnPerHour ?? 0)
                              const toneDay =
                                avg === null ? `${nowCol} text-neutral-500 italic` : dipDay ? 'bg-red-500/20 text-red-200' : h.current ? `${nowCol} text-neutral-300` : 'text-neutral-300'
                              const srDay = avg === null ? 'no clean hour' : dipDay ? 'dip' : h.current ? 'today so far' : ''
                              return (
                                <td key={start} className={`${HOUR} ${edge} ${toneDay}`} title={cellTitle(c, h, dipDay)}>
                                  {avg !== null ? n0(avg) : '-'}
                                  {srDay && <span className="sr-only">, {srDay}</span>}
                                </td>
                              )
                            }
                            // a dip: a clean (full, on-tier) hour well under this character's own steady average
                            const dip = h.clean && steady && h.own < DIP_SHARE * (c.avgOwnPerHour ?? 0)
                            const tone = h.current
                              ? `${nowCol} text-neutral-400`
                              : h.part
                                ? 'text-neutral-500 italic'
                                : dip
                                  ? 'bg-red-500/20 text-red-200'
                                  : h.clean
                                    ? 'text-neutral-300'
                                    : 'text-amber-300'
                            // the state in words for screen readers (sighted users get the colour and the tooltip)
                            const srState = h.current
                              ? 'running hour'
                              : h.part
                                ? 'part hour'
                                : dip
                                  ? 'dip'
                                  : h.clean
                                    ? ''
                                    : h.ownTier <= 0
                                      ? 'own tier unknown'
                                      : 'off-tier kills'
                            return (
                              <td key={start} className={`${HOUR} ${edge} ${tone}`} title={cellTitle(c, h, dip)}>
                                {h.ownTier > 0 ? n0(h.own) : '?'}
                                {(h.showsTotal || h.ownTier === 0) && <span className="text-[11px] text-neutral-400"> ({n0(h.total)})</span>}
                                {srState && <span className="sr-only">, {srState}</span>}
                              </td>
                            )
                          })}
                        </tr>
                      </Fragment>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}

          {characters.length > 0 && (
            <footer className="shrink-0 flex flex-wrap items-center justify-between gap-2 px-4 py-2 border-t border-neutral-800 text-xs text-neutral-400">
              <span className="whitespace-nowrap">
                {n0(firstRow + 1)}-{n0(firstRow + pageRows.length)} of {n0(characters.length)} characters
              </span>
              <div className="flex items-center gap-3">
                <label className="inline-flex items-center gap-1.5 whitespace-nowrap">
                  Rows
                  <select
                    value={pageSize}
                    onChange={e => setParam({ size: Number(e.target.value) === DEFAULT_PAGE_SIZE ? null : e.target.value })}
                    className="bg-neutral-950 border border-neutral-800 rounded-lg px-1.5 py-0.5 text-xs text-neutral-200"
                  >
                    {PAGE_SIZES.map(s => (
                      <option key={s} value={s}>
                        {s}
                      </option>
                    ))}
                  </select>
                </label>
                {totalPages > 1 && (
                  <nav className="flex items-center gap-1" aria-label="Pages">
                    <PagerButton label="First page" disabled={page === 1} onClick={() => goPage(1)}>
                      <ChevronsLeft className="w-3.5 h-3.5" aria-hidden />
                    </PagerButton>
                    <PagerButton label="Previous page" disabled={page === 1} onClick={() => goPage(page - 1)}>
                      <ChevronLeft className="w-3.5 h-3.5" aria-hidden />
                    </PagerButton>
                    {pageList(page, totalPages).map((p, i) =>
                      p === 'gap' ? (
                        <span key={`gap-${i}`} className="px-1 text-neutral-600">
                          ...
                        </span>
                      ) : (
                        <PagerButton key={p} label={`Page ${p}`} current={p === page} onClick={() => goPage(p)}>
                          {p}
                        </PagerButton>
                      ),
                    )}
                    <PagerButton label="Next page" disabled={page === totalPages} onClick={() => goPage(page + 1)}>
                      <ChevronRight className="w-3.5 h-3.5" aria-hidden />
                    </PagerButton>
                    <PagerButton label="Last page" disabled={page === totalPages} onClick={() => goPage(totalPages)}>
                      <ChevronsRight className="w-3.5 h-3.5" aria-hidden />
                    </PagerButton>
                  </nav>
                )}
              </div>
            </footer>
          )}
        </section>
      )}
    </div>
  )
}

/** One pager button: an icon or a page number, with a label for screen readers and the current page marked. */
function PagerButton({
  label,
  disabled = false,
  current = false,
  onClick,
  children,
}: {
  label: string
  disabled?: boolean
  current?: boolean
  onClick: () => void
  children: ReactNode
}) {
  return (
    <button
      type="button"
      aria-label={label}
      aria-current={current ? 'page' : undefined}
      disabled={disabled}
      onClick={onClick}
      className={`min-w-[1.75rem] h-7 px-1.5 inline-flex items-center justify-center rounded-md text-xs font-semibold tabular-nums focus-visible:outline focus-visible:outline-2 focus-visible:outline-blue-500 disabled:opacity-30 disabled:cursor-not-allowed ${
        current ? 'bg-blue-600 text-white' : 'text-neutral-300 hover:bg-neutral-800'
      }`}
    >
      {children}
    </button>
  )
}
