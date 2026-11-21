import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { ArrowLeftRight, BookOpen, Coins, Package, ChevronDown, Columns2, ChevronRight, History, Info, RotateCcw, Save, Search, SlidersHorizontal, X } from 'lucide-react'
import { api } from '../services/api'
import PageHeader from './common/PageHeader'
import AdminToast, { AdminToastVariant } from './common/AdminToast'
import BountiesPanel, { type BountyMode } from './ZoneControlBounties'
import { Example, V } from './ZoneControlExamples'

interface ZoneSummary {
  name: string
  variation: number
  enabled: boolean
  landblocks: number
  stats: number
  wcids: number[]
}

interface Overview {
  zones: ZoneSummary[]
  defaults: { variation: number; stats: number }[]
  canEdit: boolean
  groups: string[]
}

interface StatRow {
  stat: string
  label: string
  group: string
  known: boolean
  value: number | null
  // fingerprint of the whole stored curve (null = not set), sent back with an edit
  revision: string | null
  curved: boolean
  effective: number | null
  source: string | null
  toggledOff: boolean
  shadowed: boolean
}

type RungState = 'NotSet' | 'Wins' | 'Overridden' | 'Shadowed' | 'Off'

// One row of a stat's stack, from GET /api/zonecontrol/stats/ladder. Most specific first.
interface LadderRung {
  label: string
  layer: string
  variation: number | null
  rank: string | null
  isTarget: boolean
  value: number | null
  curved: boolean
  state: RungState
  anchor: number | null
  anchorState: RungState
  toggle: boolean | null
}

interface Ladder {
  stat: string
  inEffect: boolean
  turnedOff: boolean
  anchored: boolean
  rungs: LadderRung[]
}

type LadderSlot = Ladder | 'loading' | 'error'

interface EditResult {
  logged: boolean
  oldValue: number | null
  newValue: number | null
}

interface Change {
  id: number
  changedAtUtc: string
  actor: string
  source: string
  zone: string | null
  variation: number | null
  wcid: number | null
  rank: string
  stat: string
  oldValue: number | null
  newValue: number | null
  revertsId: number | null
  reason: string | null
  // rows saved together share it (one save that changed several parts, or one Revert all)
  batchId: string | null
  // 'stat' = one stat (From/To are its values); 'section' = a whole zone, tier Default or server-wide list (details say what moved)
  kind: string
  section: string | null
  details: string[] | null
  revertible: boolean
}

// History in groups, from GET /api/zonecontrol/changes/groups: one save, or one person's edits within a few minutes.
interface ChangeGroup {
  key: string
  actor: string
  source: string
  fromUtc: string
  toUtc: string
  reasons: string[]
  changes: Change[]
}

// What Revert all would do, from POST /api/zonecontrol/changes/revert-group/preview.
interface GroupPlan {
  revert: number[]
  skipped: { id: number; stat: string; why: string }[]
  // rows saved together with the group's rows but not listed in it (another zone's half of a rename): a save is undone whole
  added: Change[]
}

interface GroupRevertResult {
  reverted: number[]
  skipped: { id: number; why: string }[]
  stoppedAt: number | null
  stopReason: string | null
  logged: boolean
}

// The dry run of one edit, from POST /api/zonecontrol/stat/preview. Nothing is saved until the editor confirms.
interface ImpactMonster {
  label: string
  before: number | null
  after: number | null
}

interface ImpactZone {
  zone: string
  variation: number
  enabled: boolean
  reach: 'full' | 'partial' | 'none'
  monsterKinds: number
  changed: ImpactMonster[]
}

interface Preview {
  status: string
  message: string | null
  stat: string
  oldValue: number | null
  newValue: number | null
  losesCurve: boolean
  takesEffect: string | null
  zones: ImpactZone[]
}

interface PendingEdit {
  row: StatRow
  mode: 'set' | 'clear'
  value?: string
  preview: Preview
  // the exact target the preview was made for: Confirm sends this, never the page's current selection
  body: EditBody
  where: string
}

interface EditBody {
  zone?: string
  variation?: number
  wcid?: number
  rank?: string
  stat?: string
  expected?: number | null
  expectedRevision?: string
}

const REASON_MAX = 255

// Admin help for one stat, from the server's ZoneStatHelp (the in-game /zonecontrol help <stat> prints the same text).
interface StatHelp {
  stat: string
  summary: string
  details: string | null
  shape: string | null
  whenUnset: string | null
  raise: string | null
  lower: string | null
  range: string | null
  caution: string | null
  related: string[]
  timing: string
  takesEffect: string
  howToUndo: string
  anchorNote: string | null
}

interface GuideSection {
  title: string
  paragraphs: string[]
}

interface HelpData {
  stats: StatHelp[]
  guide: GuideSection[]
  total: number
}

type Scope = { kind: 'zone'; zone: string; variation: number } | { kind: 'default'; variation: number }

const RANKS = ['default', 'regular', 'leader', 'boss'] as const
type Rank = (typeof RANKS)[number]

// The progression tiers always show, so a tier with no Default yet can be started from here.
const PROGRESSION_TIERS = Array.from({ length: 15 }, (_, i) => i + 11)

const fmt = (v: number | null) =>
  v === null || v === undefined ? '-' : Number(v.toPrecision(6)).toLocaleString('en-US', { maximumFractionDigits: 10 })

const capitalize = (text: string) => (text ? text[0].toUpperCase() + text.slice(1) : text)

// Where a logged change landed, in words: the zone or tier, and for a stat the row on it.
const changeWhere = (c: Change) => {
  const place = c.zone ? `'${c.zone}'` : c.variation !== null ? `T${c.variation} Default` : 'server-wide'
  if (c.kind === 'section') return place
  const row = c.wcid !== null ? `monster ${c.wcid}` : c.rank === 'default' ? 'Default row' : `${capitalize(c.rank)} row`
  return `${place}, ${row}`
}

// A whole-part change that created or removed the part (a zone, a tier Default): its revert removes or brings it back.
const partCreated = (c: Change) => c.kind === 'section' && c.details?.[0] === 'Created.'
const partRemoved = (c: Change) => c.kind === 'section' && c.details?.[0] === 'Removed.'
const partName = (c: Change) => (c.section?.startsWith('zone:') ? `zone '${c.zone}'` : c.variation !== null ? `the T${c.variation} Default` : c.stat)

/**
 * Keyboard handling for a modal dialog (review 2026-11-19): focus moves inside when it opens (unless something inside already has
 * it), Tab and Shift+Tab stay inside, Escape closes it, and focus goes back to whatever opened it. Put the ref on the dialog's outer
 * element (with tabIndex -1).
 */
const useDialogFocus = (open: boolean, onClose: () => void) => {
  const ref = useRef<HTMLDivElement>(null)
  const closeRef = useRef(onClose)
  closeRef.current = onClose
  useEffect(() => {
    const el = ref.current
    if (!open || !el) return
    const opener = document.activeElement as HTMLElement | null
    const focusables = () => Array.from(el.querySelectorAll<HTMLElement>(
      'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'))
    if (!el.contains(document.activeElement)) (focusables()[0] ?? el).focus()
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { e.preventDefault(); closeRef.current(); return }
      if (e.key !== 'Tab') return
      const f = focusables()
      if (f.length === 0) { e.preventDefault(); el.focus(); return }
      const first = f[0], last = f[f.length - 1]
      if (!el.contains(document.activeElement)) { e.preventDefault(); first.focus() }
      else if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus() }
      else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus() }
    }
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('keydown', onKey)
      if (opener && document.contains(opener)) opener.focus()
    }
  }, [open])
  return ref
}

const scopeParams = (scope: Scope, wcid: number | null, rank: Rank) => {
  const q = new URLSearchParams()
  if (scope.kind === 'zone') q.set('zone', scope.zone)
  else q.set('variation', String(scope.variation))
  if (wcid !== null) q.set('wcid', String(wcid))
  if (rank !== 'default') q.set('rank', rank)
  return q
}

const ZoneControl = () => {
  const [overview, setOverview] = useState<Overview | null>(null)
  const [scope, setScope] = useState<Scope | null>(null)
  const [rank, setRank] = useState<Rank>('default')
  const [wcid, setWcid] = useState<number | null>(null)
  const [wcidText, setWcidText] = useState('')
  const [rows, setRows] = useState<StatRow[]>([])
  // the selection the rows were loaded for: editing only works when it is the one on screen (review 2026-11-19)
  const [rowsKey, setRowsKey] = useState<string | null>(null)
  const [drafts, setDrafts] = useState<Record<string, string>>({})
  const [search, setSearch] = useState('')
  const [zoneSearch, setZoneSearch] = useState('')
  const [onlySet, setOnlySet] = useState(true)
  const [closedGroups, setClosedGroups] = useState<Record<string, boolean>>({})
  const [view, setView] = useState<'stats' | 'history'>('stats')
  const [groups, setGroups] = useState<ChangeGroup[]>([])
  // the History filter the groups were loaded for: another filter's groups (and Revert buttons) are never shown
  const [groupsKey, setGroupsKey] = useState<string | null>(null)
  const [groupsError, setGroupsError] = useState<string | null>(null)
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({})
  // Single Revert asks in an in-page dialog: window.confirm is suppressed in some embedded browsers (it returns Cancel at once,
  // so the button silently did nothing - found 2026-11-11 in the desktop app's browser pane).
  const [confirmRevert, setConfirmRevert] = useState<{ change: Change; question: string; danger: boolean } | null>(null)
  const [groupRevert, setGroupRevert] = useState<{ group: ChangeGroup; plan: GroupPlan } | null>(null)
  const [busy, setBusy] = useState(false)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [toast, setToast] = useState<{ message: string; variant: AdminToastVariant } | null>(null)
  const [help, setHelp] = useState<HelpData | null>(null)
  const [openHelp, setOpenHelp] = useState<Record<string, boolean>>({})
  const [showGuide, setShowGuide] = useState(false)
  const [showCompare, setShowCompare] = useState(false)
  // Kill Rewards or Bounty Items: two different systems, two buttons (null = neither open)
  const [bountyMode, setBountyMode] = useState<BountyMode | null>(null)
  const [ladders, setLadders] = useState<Record<string, LadderSlot>>({})
  const [pending, setPending] = useState<PendingEdit | null>(null)
  // History: this zone or tier only, or every change (server-wide lists such as gear lock spots belong to no zone)
  const [historyAll, setHistoryAll] = useState(false)
  const [reason, setReason] = useState('')

  const dismissToast = useCallback(() => setToast(null), [])
  const revertDialog = useDialogFocus(confirmRevert !== null, () => setConfirmRevert(null))
  const canEdit = overview?.canEdit ?? false

  // STALE RESPONSES (review 2026-11-12): a slow answer for the zone you just left must never land under the one you opened.
  // Every fetch remembers the selection it was made for and drops its answer if the selection has moved since.
  const targetKey = scope ? `${scopeParams(scope, wcid, rank)}` : ''
  const targetRef = useRef(targetKey)
  targetRef.current = targetKey
  const historyKey = `${targetKey}|${historyAll}`
  const historyRef = useRef(historyKey)
  historyRef.current = historyKey
  const shownGroups = groupsKey === historyKey ? groups : []
  // bumped whenever the ladder cache is emptied, so a ladder still in flight from before cannot refill it with old data
  const ladderGen = useRef(0)
  // request counters: after A -> B -> A, an answer from the FIRST visit to A is older than the newest request and is dropped too
  const rowsSeq = useRef(0)
  const changesSeq = useRef(0)

  const loadOverview = useCallback(async () => {
    try {
      setOverview(await api.get<Overview>('/api/zonecontrol/overview'))
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : 'Failed to load zones.')
    }
  }, [])

  const loadRows = useCallback(async () => {
    if (!scope) return
    const key = `${scopeParams(scope, wcid, rank)}`
    // a refresh for a selection no longer on screen (a save on A finishing after a move to B) does nothing: it must not take a
    // sequence number and so invalidate B's own request still in flight (review 2026-11-19)
    if (targetRef.current !== key) return
    const seq = ++rowsSeq.current
    const fresh = () => rowsSeq.current === seq && targetRef.current === key
    try {
      const data = await api.get<{ rows: StatRow[] }>(`/api/zonecontrol/stats?${key}`)
      if (!fresh()) return
      setRows(data?.rows ?? [])
      setRowsKey(key)
      setDrafts({})
      // the stack may have moved (an edit, another scope): open rows fetch their ladder again
      ladderGen.current++
      setLadders({})
      setLoadError(null)
    } catch (err) {
      if (!fresh()) return
      setRows([])
      setRowsKey(key)
      setLoadError(err instanceof Error ? err.message : 'Failed to load stats.')
    }
  }, [scope, wcid, rank])

  const loadChanges = useCallback(async () => {
    if (!scope) return
    const q = new URLSearchParams()
    if (historyAll) q.set('limit', '300')
    else if (scope.kind === 'zone') q.set('zone', scope.zone)
    else q.set('variation', String(scope.variation))
    // the identity of THIS request, from what it asks for (not from whatever is selected when it is called): a callback kept from
    // zone A that runs after a move to B asks for A, and its answer is then dropped as not B's
    const key = `${scopeParams(scope, wcid, rank)}|${historyAll}`
    if (historyRef.current !== key) return   // as in loadRows: never invalidate the current filter's request
    const seq = ++changesSeq.current
    const fresh = () => changesSeq.current === seq && historyRef.current === key
    try {
      const data = await api.get<ChangeGroup[]>(`/api/zonecontrol/changes/groups?${q}`)
      if (!fresh()) return
      setGroups(data ?? [])
      setGroupsKey(key)
      setGroupsError(null)
    } catch (err) {
      if (!fresh()) return
      setGroups([])
      setGroupsKey(key)
      // kept on screen (with Retry) instead of a toast that fades into "No logged changes yet"
      setGroupsError(err instanceof Error ? err.message : 'Failed to load history.')
    }
  }, [scope, wcid, rank, historyAll])

  useEffect(() => { loadOverview() }, [loadOverview])
  useEffect(() => {
    // Help is optional: the page works without it, so a failure only leaves the help panels empty.
    api.get<HelpData>('/api/zonecontrol/help').then(setHelp).catch(() => setHelp(null))
  }, [])

  const helpByStat = useMemo(() => new Map((help?.stats ?? []).map(h => [h.stat, h])), [help])
  // a new selection starts empty: the last one's rows and drafts must never sit under the new name while it loads
  useEffect(() => {
    setRows([])
    setRowsKey(null)
    setDrafts({})
  }, [targetKey])
  useEffect(() => { loadRows() }, [loadRows])

  // Each open stat loads its ladder once; loadRows empties the cache, so an edit or a scope change refetches.
  useEffect(() => {
    if (!scope) return
    const wanted = Object.keys(openHelp).filter(stat => openHelp[stat] && ladders[stat] === undefined)
    if (wanted.length === 0) return
    setLadders(l => ({ ...l, ...Object.fromEntries(wanted.map(stat => [stat, 'loading' as const])) }))
    const key = targetRef.current
    const gen = ladderGen.current
    const fresh = () => targetRef.current === key && ladderGen.current === gen
    for (const stat of wanted) {
      const q = scopeParams(scope, wcid, rank)
      q.set('stat', stat)
      api.get<Ladder>(`/api/zonecontrol/stats/ladder?${q}`)
        .then(data => { if (fresh()) setLadders(l => ({ ...l, [stat]: data ?? 'error' })) })
        .catch(() => { if (fresh()) setLadders(l => ({ ...l, [stat]: 'error' })) })
    }
  }, [openHelp, ladders, scope, wcid, rank])
  useEffect(() => { if (view === 'history') loadChanges() }, [view, loadChanges])
  useEffect(() => { setPending(null) }, [targetKey])

  const selectScope = (next: Scope) => {
    setShowGuide(false)
    setShowCompare(false)
    setBountyMode(null)
    setScope(next)
    setRank('default')
    setWcid(null)
    setWcidText('')
  }

  const selectedZone = scope?.kind === 'zone' ? overview?.zones.find(z => z.name === scope.zone) : undefined

  const applyWcid = () => {
    const text = wcidText.trim()
    if (!text) { setWcid(null); return }
    const parsed = Number(text)
    if (!Number.isInteger(parsed) || parsed <= 0) {
      setToast({ message: 'A monster WCID is a whole number.', variant: 'error' })
      return
    }
    setRank('default')
    setWcid(parsed)
  }

  const finishEdit = async (result: EditResult | null, done: string) => {
    if (result && result.logged === false)
      setToast({ message: `${done}, but the change log could not record it. Check the server log.`, variant: 'warning' })
    else
      setToast({ message: done, variant: 'success' })
    await Promise.all([loadRows(), loadOverview(), view === 'history' ? loadChanges() : Promise.resolve()])
  }

  const failEdit = async (err: unknown) => {
    setToast({ message: err instanceof Error ? err.message : 'The edit failed.', variant: 'error' })
    await loadRows()
  }

  const editBody = (row: StatRow): EditBody => {
    if (!scope) return {}
    return {
      zone: scope.kind === 'zone' ? scope.zone : undefined,
      variation: scope.kind === 'default' ? scope.variation : undefined,
      wcid: wcid ?? undefined,
      rank: rank === 'default' ? undefined : rank,
      stat: row.stat,
      expected: row.value,
      // the whole curve as loaded: a growth or tier pin added since (same number) also refuses the edit
      expectedRevision: row.revision ?? undefined,
    }
  }

  // Save and Clear first ask the server what the edit would do; the dialog then confirms (with an optional reason) or cancels.
  const openPreview = async (row: StatRow, mode: 'set' | 'clear') => {
    const value = drafts[row.stat]?.trim()
    if (busy || (mode === 'set' && !value)) return
    // only rows loaded for the selection on screen can be edited (they carry its expected value and revision)
    if (rowsKey !== targetRef.current) return
    // captured before the request: the preview, the dialog and the save all name this one target
    const body = editBody(row)
    const where = rowName()
    const key = targetRef.current
    setBusy(true)
    try {
      const preview = await api.post<Preview>('/api/zonecontrol/stat/preview', { ...body, value, clear: mode === 'clear' })
      if (preview && targetRef.current === key) {
        setReason('')
        setPending({ row, mode, value, preview, body, where })
      }
    } catch (err) {
      await failEdit(err)
    } finally {
      setBusy(false)
    }
  }

  const saveRow = (row: StatRow) => openPreview(row, 'set')
  const clearRow = (row: StatRow) => openPreview(row, 'clear')

  const confirmPending = async () => {
    if (!pending || busy) return
    const { row, mode, value, body } = pending
    const why = reason.trim() || undefined
    setBusy(true)
    try {
      if (mode === 'set') {
        const result = await api.post<EditResult>('/api/zonecontrol/stat/set', { ...body, value, reason: why })
        setPending(null)
        await finishEdit(result, `${row.stat} set to ${fmt(result?.newValue ?? null)}`)
      } else {
        const result = await api.post<EditResult>('/api/zonecontrol/stat/clear', { ...body, reason: why })
        setPending(null)
        await finishEdit(result, `${row.stat} cleared`)
      }
    } catch (err) {
      setPending(null)
      await failEdit(err)
    } finally {
      setBusy(false)
    }
  }

  const rowName = () => {
    if (!scope) return ''
    const where = scope.kind === 'zone' ? `zone '${scope.zone}'` : `T${scope.variation} Default`
    if (wcid !== null) return `${where}, monster ${wcid}`
    return rank === 'default' ? `${where}, Default row` : `${where}, ${rank[0].toUpperCase()}${rank.slice(1)} row`
  }

  const revert = async (change: Change) => {
    if (busy) return
    if (change.batchId && (partCreated(change) || partRemoved(change))) {
      await previewGroupRevert({
        key: String(change.id), actor: change.actor, source: change.source, fromUtc: change.changedAtUtc, toUtc: change.changedAtUtc,
        reasons: change.reason ? [change.reason] : [], changes: [change],
      })
      return
    }
    const question = partCreated(change)
      ? `This change created ${partName(change)}. Reverting it DELETES ${partName(change)} again. Continue?`
      : partRemoved(change)
        ? `This change removed ${partName(change)}. Reverting it brings it back exactly as it was. Continue?`
        : change.kind === 'section'
          ? `Put ${change.stat} for ${changeWhere(change)} back exactly as it was before this change?`
          : `Put ${change.stat} on ${changeWhere(change)} back to ${change.oldValue === null ? 'not set' : fmt(change.oldValue)}?`
    setConfirmRevert({ change, question, danger: partCreated(change) })
  }

  const doRevert = async () => {
    if (!confirmRevert || busy) return
    const { change } = confirmRevert
    setConfirmRevert(null)
    setBusy(true)
    try {
      const result = await api.post<EditResult>(`/api/zonecontrol/changes/${change.id}/revert`)
      await finishEdit(result, change.kind === 'section'
        ? `${change.stat} put back`
        : `${change.stat} put back to ${result?.newValue === null ? 'not set' : fmt(result?.newValue ?? null)}`)
    } catch (err) {
      await failEdit(err)
    } finally {
      setBusy(false)
    }
  }

  // Revert all: ask the server what it would do first (which revert, which are skipped and why), then confirm in a dialog.
  const previewGroupRevert = async (group: ChangeGroup) => {
    if (busy) return
    setBusy(true)
    try {
      const plan = await api.post<GroupPlan>('/api/zonecontrol/changes/revert-group/preview', { ids: group.changes.map(c => c.id) })
      if (plan) {
        setReason('')
        setGroupRevert({ group, plan })
      }
    } catch (err) {
      setToast({ message: err instanceof Error ? err.message : 'Could not check the group.', variant: 'error' })
    } finally {
      setBusy(false)
    }
  }

  const confirmGroupRevert = async () => {
    if (!groupRevert || busy) return
    setBusy(true)
    try {
      const result = await api.post<GroupRevertResult>('/api/zonecontrol/changes/revert-group', {
        ids: groupRevert.group.changes.map(c => c.id),
        // exactly what the preview showed: the server reverts nothing outside it, even if the live plan has changed since
        approved: groupRevert.plan.revert,
        reason: reason.trim() || undefined,
      })
      setGroupRevert(null)
      const parts = [`${result?.reverted.length ?? 0} reverted`]
      if (result?.skipped.length) parts.push(`${result.skipped.length} skipped`)
      if (result?.stoppedAt) parts.push(`stopped at change ${result.stoppedAt}: ${result.stopReason ?? 'it failed'}`)
      const variant: AdminToastVariant = result?.stoppedAt ? 'error' : result?.logged === false || result?.skipped.length ? 'warning' : 'success'
      setToast({ message: parts.join(', ') + (result?.logged === false ? '. The change log missed some; check the server log.' : ''), variant })
      await Promise.all([loadRows(), loadOverview(), loadChanges()])
    } catch (err) {
      setGroupRevert(null)
      await failEdit(err)
    } finally {
      setBusy(false)
    }
  }

  const visibleRows = useMemo(() => {
    const q = search.trim().toLowerCase()
    return rows.filter(r => {
      if (q) return r.stat.toLowerCase().includes(q) || r.label.toLowerCase().includes(q) || (helpByStat.get(r.stat)?.summary.toLowerCase().includes(q) ?? false)
      if (r.stat in drafts) return true
      return !onlySet || r.value !== null
    })
  }, [rows, search, onlySet, drafts, helpByStat])

  const grouped = useMemo(() => {
    const groups = overview?.groups ?? []
    return groups
      .map(g => ({ group: g, rows: visibleRows.filter(r => r.group === g) }))
      .filter(g => g.rows.length > 0)
  }, [visibleRows, overview])

  const filteredZones = useMemo(() => {
    const q = zoneSearch.trim().toLowerCase()
    return (overview?.zones ?? []).filter(z => !q || z.name.toLowerCase().includes(q) || `v${z.variation}` === q)
  }, [overview, zoneSearch])

  const tiers = useMemo(() => {
    const counts = new Map((overview?.defaults ?? []).map(d => [d.variation, d.stats]))
    const all = Array.from(new Set([...PROGRESSION_TIERS, ...counts.keys()])).sort((a, b) => a - b)
    return all.map(v => ({ variation: v, stats: counts.get(v) ?? 0 }))
  }, [overview])

  const isSelected = (s: Scope) =>
    scope?.kind === s.kind && scope.variation === s.variation && (s.kind !== 'zone' || (scope as typeof s).zone === s.zone)

  const navClass = (active: boolean) =>
    `w-full flex items-center justify-between gap-2 px-3 py-2 rounded-lg text-left text-sm transition-colors ${
      active ? 'bg-blue-600/20 text-white' : 'text-neutral-300 hover:bg-neutral-800/60'
    }`

  const tabClass = (active: boolean) =>
    `px-3 py-1.5 rounded-lg text-xs font-bold border transition-colors ${
      active ? 'bg-neutral-800 text-white border-neutral-700' : 'bg-neutral-900/50 text-neutral-500 border-neutral-800/50 hover:text-neutral-300'
    }`

  return (
    <div className="p-4 md:p-8 h-full flex flex-col min-h-0">
      <PageHeader title="Zone Control" icon={SlidersHorizontal}>
        <div className="flex items-center gap-3">
          {overview && !canEdit && <span className="text-xs text-amber-400">Read only: editing needs Developer access</span>}
          <button type="button" className={tabClass(showCompare)} onClick={() => { setShowCompare(c => !c); setShowGuide(false); setBountyMode(null) }}>
            <Columns2 className="w-3.5 h-3.5 inline mr-1 -mt-0.5" aria-hidden />Compare
          </button>
          <button type="button" className={tabClass(bountyMode === 'kill')} title="Automatic: every N kills in an area pays an item"
            onClick={() => { setBountyMode(m => (m === 'kill' ? null : 'kill')); setShowGuide(false); setShowCompare(false) }}>
            <Coins className="w-3.5 h-3.5 inline mr-1 -mt-0.5" aria-hidden />Kill Rewards
          </button>
          <button type="button" className={tabClass(bountyMode === 'items')} title="Lootable bounty items: drop chance, drop mix, mystery rewards"
            onClick={() => { setBountyMode(m => (m === 'items' ? null : 'items')); setShowGuide(false); setShowCompare(false) }}>
            <Package className="w-3.5 h-3.5 inline mr-1 -mt-0.5" aria-hidden />Bounty Items
          </button>
          <button type="button" className={tabClass(showGuide)} onClick={() => { setShowGuide(g => !g); setShowCompare(false); setBountyMode(null) }}>
            <BookOpen className="w-3.5 h-3.5 inline mr-1 -mt-0.5" aria-hidden />How Zone Control works
          </button>
        </div>
      </PageHeader>

      <div className="flex-1 min-h-0 grid grid-cols-1 lg:grid-cols-[280px_1fr] gap-4">
        <aside className="min-h-0 overflow-y-auto custom-scrollbar bg-neutral-900/30 border border-neutral-800/50 rounded-2xl p-3 max-h-72 lg:max-h-none">
          <h2 className="px-3 pb-2 text-[10px] font-bold uppercase tracking-widest text-neutral-500">Tier Defaults</h2>
          <div className="grid grid-cols-3 gap-1 mb-4">
            {tiers.map(t => (
              <button
                key={t.variation}
                type="button"
                onClick={() => selectScope({ kind: 'default', variation: t.variation })}
                className={`px-2 py-1.5 rounded-lg text-xs text-center transition-colors ${
                  isSelected({ kind: 'default', variation: t.variation })
                    ? 'bg-blue-600/20 text-white'
                    : t.stats > 0 ? 'text-neutral-200 hover:bg-neutral-800/60' : 'text-neutral-600 hover:bg-neutral-800/60'
                }`}
                title={`${t.stats} stats set`}
              >
                v{t.variation}
              </button>
            ))}
          </div>

          <h2 className="px-3 pb-2 text-[10px] font-bold uppercase tracking-widest text-neutral-500">Zones</h2>
          <div className="relative mb-2">
            <Search className="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-neutral-600" aria-hidden />
            <input
              value={zoneSearch}
              onChange={e => setZoneSearch(e.target.value)}
              placeholder="Find a zone"
              aria-label="Find a zone"
              className="w-full bg-neutral-950/50 border border-neutral-800 rounded-lg pl-8 pr-3 py-1.5 text-sm text-white placeholder:text-neutral-600 focus:outline-none focus:border-blue-500/50"
            />
          </div>
          <div className="space-y-0.5">
            {filteredZones.map(z => (
              <button
                key={z.name}
                type="button"
                onClick={() => selectScope({ kind: 'zone', zone: z.name, variation: z.variation })}
                className={navClass(isSelected({ kind: 'zone', zone: z.name, variation: z.variation }))}
              >
                <span className={`truncate ${z.enabled ? '' : 'text-neutral-500'}`}>{z.name}</span>
                <span className="shrink-0 text-[10px] text-neutral-500">v{z.variation}{z.enabled ? '' : ' off'}</span>
              </button>
            ))}
            {overview && filteredZones.length === 0 && <p className="px-3 py-2 text-xs text-neutral-600">No zones found</p>}
          </div>
        </aside>

        <section className="min-h-0 flex flex-col bg-neutral-900/30 border border-neutral-800/50 rounded-2xl overflow-hidden">
          {bountyMode ? (
            <BountiesPanel
              key={bountyMode}
              mode={bountyMode}
              onClose={() => setBountyMode(null)}
              notify={(message, variant) => setToast({ message, variant })}
              onOpenStat={variation => {
                selectScope({ kind: 'default', variation })
                setView('stats')
                setOnlySet(false)
                setSearch('bounty_odds')
              }}
              onOpenHistory={() => {
                // bounty parts belong to no zone: History only lists them with "Show every change" on
                if (!scope) selectScope({ kind: 'default', variation: 11 })
                else setBountyMode(null)
                setView('history')
                setHistoryAll(true)
              }}
            />
          ) : showGuide ? (
            <GuidePanel help={help} onClose={() => setShowGuide(false)} />
          ) : showCompare ? (
            <ComparePanel
              overview={overview}
              tiers={tiers.map(t => t.variation)}
              start={scope}
              helpByStat={helpByStat}
              onClose={() => setShowCompare(false)}
              // opening a result shows that stat, so the Stats view (History may have been open) - review 2026-11-19
              onOpen={(target, stat, openRank) => { selectScope(target); setRank(openRank); setSearch(stat); setView('stats') }}
            />
          ) : !scope ? (
            <div className="flex-1 flex items-center justify-center p-8 text-sm text-neutral-500">
              {loadError ?? 'Pick a tier Default or a zone to see its stats.'}
            </div>
          ) : (
            <>
              <div className="p-4 border-b border-neutral-800/50 space-y-3">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <h2 className="text-lg font-bold text-white">
                      {scope.kind === 'zone' ? scope.zone : `Default v${scope.variation}`}
                    </h2>
                    <p className="text-xs text-neutral-500">
                      {scope.kind === 'zone'
                        ? `Variation ${scope.variation}${selectedZone?.enabled ? '' : ', disabled'}. Values set here override the tier Default.`
                        : `Every zone at v${scope.variation} that does not set a stat inherits it from here.`}
                    </p>
                  </div>
                  <div className="flex gap-1">
                    <button type="button" className={tabClass(view === 'stats')} onClick={() => setView('stats')}>Stats</button>
                    <button type="button" className={tabClass(view === 'history')} onClick={() => setView('history')}>
                      <History className="w-3.5 h-3.5 inline mr-1 -mt-0.5" aria-hidden />History
                    </button>
                  </div>
                </div>

                {view === 'stats' && (
                  <div className="flex flex-wrap items-center gap-3">
                    <div className="flex gap-1" role="group" aria-label="Rank row">
                      {RANKS.map(r => (
                        <button
                          key={r}
                          type="button"
                          disabled={wcid !== null}
                          title={wcid !== null ? 'A monster override has no rank rows' : undefined}
                          className={`${tabClass(rank === r)} capitalize disabled:opacity-40`}
                          onClick={() => setRank(r)}
                        >
                          {r}
                        </button>
                      ))}
                    </div>

                    {scope.kind === 'zone' && (
                      <div className="flex items-center gap-1">
                        <input
                          value={wcidText}
                          onChange={e => setWcidText(e.target.value)}
                          onBlur={applyWcid}
                          onKeyDown={e => { if (e.key === 'Enter') applyWcid() }}
                          list="zone-wcids"
                          inputMode="numeric"
                          placeholder="Monster WCID"
                          aria-label="Monster WCID"
                          className="w-36 bg-neutral-950/50 border border-neutral-800 rounded-lg px-3 py-1.5 text-sm text-white placeholder:text-neutral-600 focus:outline-none focus:border-blue-500/50"
                        />
                        <datalist id="zone-wcids">
                          {selectedZone?.wcids.map(w => <option key={w} value={w} />)}
                        </datalist>
                        {wcid !== null && (
                          <button
                            type="button"
                            aria-label="Back to the whole zone"
                            className="p-1.5 text-neutral-500 hover:text-white"
                            onClick={() => { setWcid(null); setWcidText('') }}
                          >
                            <X className="w-4 h-4" />
                          </button>
                        )}
                      </div>
                    )}

                    <div className="relative flex-1 min-w-[180px]">
                      <Search className="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-neutral-600" aria-hidden />
                      <input
                        value={search}
                        onChange={e => setSearch(e.target.value)}
                        placeholder="Search all stats"
                        aria-label="Search all stats"
                        className="w-full bg-neutral-950/50 border border-neutral-800 rounded-lg pl-8 pr-3 py-1.5 text-sm text-white placeholder:text-neutral-600 focus:outline-none focus:border-blue-500/50"
                      />
                    </div>

                    <label className="flex items-center gap-2 text-xs text-neutral-400 cursor-pointer">
                      <input type="checkbox" checked={onlySet} onChange={e => setOnlySet(e.target.checked)} />
                      Only set on this row
                    </label>
                  </div>
                )}
              </div>

              {view === 'stats' ? (
                <div className="flex-1 min-h-0 overflow-auto custom-scrollbar">
                  {loadError && <p className="p-4 text-sm text-red-400">{loadError}</p>}
                  {!loadError && rows.length > 0 && (
                    <div className="px-4 pt-3 pb-2"><StatsExample scope={scope} rank={rank} wcid={wcid} rows={rows} /></div>
                  )}
                  {!loadError && rowsKey !== targetKey && <p className="p-8 text-center text-sm text-neutral-500">Loading...</p>}
                  {!loadError && rowsKey === targetKey && grouped.length === 0 && (
                    <p className="p-8 text-center text-sm text-neutral-500">
                      {onlySet && !search ? 'Nothing is set on this row. Turn off "Only set on this row" or search to add a stat.' : 'No stats match.'}
                    </p>
                  )}
                  {grouped.map(({ group, rows: groupRows }) => (
                    <div key={group}>
                      <button
                        type="button"
                        onClick={() => setClosedGroups(c => ({ ...c, [group]: !c[group] }))}
                        className="sticky top-0 z-10 w-full flex items-center gap-2 px-4 py-2 bg-neutral-900 border-b border-neutral-800/50 text-xs font-bold text-neutral-300"
                      >
                        {closedGroups[group] ? <ChevronRight className="w-3.5 h-3.5" aria-hidden /> : <ChevronDown className="w-3.5 h-3.5" aria-hidden />}
                        {group} <span className="font-normal text-neutral-600">{groupRows.length}</span>
                      </button>
                      {!closedGroups[group] && groupRows.map(row => {
                        const draft = drafts[row.stat]
                        const dirty = draft !== undefined && draft.trim() !== '' && draft.trim() !== String(row.value ?? '')
                        return (
                          <div key={row.stat} className="border-b border-neutral-800/30">
                          <div className="grid grid-cols-1 md:grid-cols-[minmax(180px,1fr)_minmax(150px,1fr)_auto] items-center gap-x-4 gap-y-1 px-4 py-2">
                            <div className="min-w-0">
                              <div className="flex items-center gap-1.5 min-w-0">
                                <span className="text-sm text-neutral-200 truncate">{row.label}</span>
                                <button
                                  type="button"
                                  onClick={() => setOpenHelp(o => ({ ...o, [row.stat]: !o[row.stat] }))}
                                  aria-expanded={!!openHelp[row.stat]}
                                  aria-label={`What ${row.label} does`}
                                  title="What this does"
                                  className={`shrink-0 p-0.5 rounded ${openHelp[row.stat] ? 'text-blue-400' : helpByStat.has(row.stat) ? 'text-neutral-500 hover:text-blue-400' : 'text-neutral-700 hover:text-neutral-500'}`}
                                >
                                  <Info className="w-3.5 h-3.5" />
                                </button>
                              </div>
                              {helpByStat.has(row.stat) && <div className="text-[11px] text-neutral-500 truncate">{helpByStat.get(row.stat)!.summary}</div>}
                              <div className="text-[11px] font-mono text-neutral-600 truncate">
                                {row.stat}
                                {!row.known && <span className="ml-2 text-amber-500">retired</span>}
                                {row.curved && <span className="ml-2 text-amber-500">has growth or pins</span>}
                              </div>
                            </div>
                            <div className="text-xs text-neutral-500 min-w-0">
                              {row.toggledOff ? (
                                <span className="text-amber-500">Turned off here ({row.source})</span>
                              ) : row.shadowed ? (
                                <span className="text-amber-500" title="Set on a lower row, but a closer row sets the T11 value without a T25 anchor, so that value applies flat at every tier.">
                                  Dropped: a closer row sets the T11 value alone ({row.source})
                                </span>
                              ) : row.effective !== null ? (
                                <>In effect: <span className="text-neutral-200">{fmt(row.effective)}</span> <span className="text-neutral-600">from {row.source ?? 'nothing'}</span></>
                              ) : (
                                <span className="text-neutral-600">Not set anywhere</span>
                              )}
                            </div>
                            <div className="flex items-center gap-1">
                              <input
                                value={draft ?? (row.value === null ? '' : String(row.value))}
                                readOnly={!canEdit || !row.known}
                                onChange={e => setDrafts(d => ({ ...d, [row.stat]: e.target.value }))}
                                onKeyDown={e => { if (e.key === 'Enter' && dirty) saveRow(row) }}
                                placeholder="not set"
                                aria-label={`${row.label} value on this row`}
                                className="w-28 bg-neutral-950/50 border border-neutral-800 rounded-lg px-2 py-1 text-sm text-right text-white placeholder:text-neutral-700 read-only:text-neutral-500 focus:outline-none focus:border-blue-500/50"
                              />
                              {canEdit && (
                                <>
                                  <button
                                    type="button"
                                    disabled={!dirty || busy}
                                    onClick={() => saveRow(row)}
                                    aria-label={`Save ${row.label}`}
                                    title="Save"
                                    className="p-1.5 rounded-lg text-blue-400 hover:bg-blue-600/20 disabled:text-neutral-700 disabled:hover:bg-transparent"
                                  >
                                    <Save className="w-4 h-4" />
                                  </button>
                                  <button
                                    type="button"
                                    disabled={row.value === null || busy}
                                    onClick={() => clearRow(row)}
                                    aria-label={`Clear ${row.label}`}
                                    title="Clear from this row"
                                    className="p-1.5 rounded-lg text-neutral-400 hover:bg-neutral-800 disabled:text-neutral-700 disabled:hover:bg-transparent"
                                  >
                                    <X className="w-4 h-4" />
                                  </button>
                                </>
                              )}
                            </div>
                          </div>
                          {openHelp[row.stat] && (
                            <>
                              <LadderPanel slot={ladders[row.stat]} anchorKey={row.stat.endsWith('_t25')} />
                              <StatHelpPanel help={helpByStat.get(row.stat)} onRelated={s => setSearch(s)} />
                            </>
                          )}
                          </div>
                        )
                      })}
                    </div>
                  ))}
                </div>
              ) : (
                <div className="flex-1 min-h-0 overflow-auto custom-scrollbar">
                  <label className="flex items-center gap-2 px-4 py-2 text-xs text-neutral-400 cursor-pointer">
                    <input type="checkbox" checked={historyAll} onChange={e => setHistoryAll(e.target.checked)} />
                    Show every change (all zones, tiers and server-wide lists)
                  </label>
                  <div className="px-4 pb-2"><HistoryExample groups={shownGroups} /></div>
                  {groupsKey !== historyKey && <p className="p-8 text-center text-sm text-neutral-500">Loading history...</p>}
                  {groupsKey === historyKey && groupsError && (
                    <p className="p-8 text-center text-sm text-red-400">
                      History could not be read: {groupsError}{' '}
                      <button type="button" className="underline text-red-300 hover:text-red-200" onClick={() => loadChanges()}>Retry</button>
                    </p>
                  )}
                  {groupsKey === historyKey && !groupsError && shownGroups.length === 0 && <p className="p-8 text-center text-sm text-neutral-500">No logged changes yet.</p>}
                  {shownGroups.length > 0 && (
                    <table className="w-full text-sm">
                      <thead className="sticky top-0 bg-neutral-900 text-left text-[10px] uppercase tracking-widest text-neutral-500">
                        <tr>
                          <th className="px-4 py-2 font-bold">When</th>
                          <th className="px-4 py-2 font-bold">Who</th>
                          <th className="px-4 py-2 font-bold">Row</th>
                          <th className="px-4 py-2 font-bold">Stat</th>
                          <th className="px-4 py-2 font-bold text-right">From</th>
                          <th className="px-4 py-2 font-bold text-right">To</th>
                          <th className="px-4 py-2" />
                        </tr>
                      </thead>
                      <tbody>
                        {shownGroups.map((g, gi) => {
                          const open = openGroups[g.key] ?? gi === 0
                          const revertible = g.changes.filter(c => c.revertible).length
                          const from = new Date(g.fromUtc), to = new Date(g.toUtc)
                          return [
                          <tr key={`g${g.key}`} className="border-t border-neutral-700/60 bg-neutral-900/80">
                            <td colSpan={7} className="px-2 py-1.5">
                              <div className="sticky left-0 flex flex-wrap items-center gap-x-2 gap-y-1 max-w-[min(100%,90vw)]">
                                <button
                                  type="button"
                                  onClick={() => setOpenGroups(o => ({ ...o, [g.key]: !open }))}
                                  aria-expanded={open}
                                  className="flex items-center gap-2 text-left"
                                >
                                  {open ? <ChevronDown className="w-3.5 h-3.5 shrink-0 text-neutral-500" aria-hidden /> : <ChevronRight className="w-3.5 h-3.5 shrink-0 text-neutral-500" aria-hidden />}
                                  <span className="text-sm text-neutral-200">{g.actor}</span>
                                  <span className="text-[10px] text-neutral-600">{g.source}</span>
                                </button>
                                {canEdit && g.changes.length > 1 && revertible > 0 && (
                                  <button type="button" disabled={busy} onClick={() => previewGroupRevert(g)} className="shrink-0 inline-flex items-center gap-1 rounded border border-neutral-700 px-2 py-0.5 text-xs text-blue-400 hover:border-blue-500/50 disabled:text-neutral-700">
                                    <RotateCcw className="w-3.5 h-3.5" aria-hidden />Revert all {g.changes.length}
                                  </button>
                                )}
                                <span className="text-xs text-neutral-500">
                                  {from.toLocaleString()}{g.changes.length > 1 && to.getTime() !== from.getTime() ? ` - ${to.toLocaleTimeString()}` : ''}
                                  {' '}- {g.changes.length} change{g.changes.length === 1 ? '' : 's'}
                                  {g.reasons.length > 0 && <span className="ml-1 text-neutral-400">"{g.reasons.join('", "')}"</span>}
                                </span>
                              </div>
                            </td>
                          </tr>,
                          ...(!open ? [] : g.changes.map(c => (
                          <tr key={c.id} className="border-t border-neutral-800/30 text-neutral-300">
                            <td className="px-4 py-2 whitespace-nowrap text-xs text-neutral-500">{new Date(c.changedAtUtc).toLocaleString()}</td>
                            <td className="px-4 py-2 whitespace-nowrap">{c.actor} <span className="text-[10px] text-neutral-600">{c.source}</span></td>
                            <td className="px-4 py-2 whitespace-nowrap text-xs">{changeWhere(c)}</td>
                            <td className="px-4 py-2 font-mono text-xs">
                              {c.stat}{c.revertsId !== null && <span className="ml-2 font-sans text-neutral-600">revert</span>}
                              {partCreated(c) && <span className="ml-2 font-sans text-amber-500">created</span>}
                              {partRemoved(c) && <span className="ml-2 font-sans text-amber-500">removed</span>}
                              {c.reason && <div className="font-sans text-[11px] text-neutral-500 whitespace-normal">"{c.reason}"</div>}
                              {c.details && c.details.length > 0 && (
                                <ul className="mt-1 space-y-0.5 font-mono text-[11px] text-neutral-500 whitespace-normal break-all">
                                  {c.details.map((line, i) => <li key={i}>{line}</li>)}
                                </ul>
                              )}
                            </td>
                            {c.kind === 'section' ? (
                              <td colSpan={2} className="px-4 py-2 text-right text-xs text-neutral-600">whole part, see the lines</td>
                            ) : (
                              <>
                                <td className="px-4 py-2 text-right">{c.oldValue === null ? 'not set' : fmt(c.oldValue)}</td>
                                <td className="px-4 py-2 text-right">{c.newValue === null ? 'not set' : fmt(c.newValue)}</td>
                              </>
                            )}
                            <td className="px-4 py-2 text-right">
                              {canEdit && c.revertible && (
                                <button type="button" disabled={busy} onClick={() => revert(c)} className="inline-flex items-center gap-1 text-xs text-blue-400 hover:text-blue-300 disabled:text-neutral-700">
                                  <RotateCcw className="w-3.5 h-3.5" aria-hidden />Revert
                                </button>
                              )}
                            </td>
                          </tr>
                          ))),
                          ]
                        })}
                      </tbody>
                    </table>
                  )}
                </div>
              )}
            </>
          )}
        </section>
      </div>

      {pending && (
        <PreviewDialog
          pending={pending}
          rowName={pending.where}
          reason={reason}
          onReason={setReason}
          busy={busy}
          onCancel={() => setPending(null)}
          onConfirm={confirmPending}
        />
      )}

      {confirmRevert && (
        <div ref={revertDialog} tabIndex={-1} className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4" role="dialog" aria-modal="true" aria-label="Revert this change">
          <div className="w-full max-w-md rounded-xl border border-neutral-800 bg-neutral-900 p-4 space-y-3 text-sm text-neutral-300">
            <h2 className="text-base font-bold text-white">Revert this change?</h2>
            <p className={confirmRevert.danger ? 'rounded border border-amber-500/30 bg-amber-500/10 px-2 py-1.5 text-amber-200' : ''}>{confirmRevert.question}</p>
            <p className="text-xs text-neutral-500">The revert is logged, and can itself be reverted.</p>
            <div className="flex justify-end gap-2">
              <button type="button" autoFocus onClick={() => setConfirmRevert(null)} className="rounded-lg border border-neutral-700 px-3 py-1.5 text-xs text-neutral-300 hover:bg-neutral-800">Cancel</button>
              <button type="button" disabled={busy} onClick={doRevert} className="rounded-lg bg-blue-600 px-3 py-1.5 text-xs text-white hover:bg-blue-500 disabled:opacity-50">
                {confirmRevert.danger ? 'Revert and delete' : 'Revert'}
              </button>
            </div>
          </div>
        </div>
      )}

      {groupRevert && (
        <GroupRevertDialog
          group={groupRevert.group}
          plan={groupRevert.plan}
          reason={reason}
          onReason={setReason}
          busy={busy}
          onCancel={() => setGroupRevert(null)}
          onConfirm={confirmGroupRevert}
        />
      )}

      {toast && <AdminToast message={toast.message} variant={toast.variant} onDismiss={dismissToast} />}
    </div>
  )
}

const GroupRevertDialog = ({ group, plan, reason, onReason, busy, onCancel, onConfirm }: {
  group: ChangeGroup
  plan: GroupPlan
  reason: string
  onReason: (text: string) => void
  busy: boolean
  onCancel: () => void
  onConfirm: () => void
}) => {
  const dialog = useDialogFocus(true, onCancel)
  const added = plan.added ?? []
  const byId = new Map([...group.changes, ...added].map(c => [c.id, c]))
  const describe = (c: Change | undefined, willRevert = true) => {
    if (!c) return ''
    if (willRevert && partCreated(c)) return `${partName(c)}: DELETED again (this change created it)`
    if (willRevert && partRemoved(c)) return `${partName(c)}: brought back`
    if (c.kind === 'section') return `${c.stat} (${changeWhere(c)})`
    const name = `${c.stat} on ${changeWhere(c)}`
    return willRevert ? `${name}: back to ${c.oldValue === null ? 'not set' : fmt(c.oldValue)}` : name
  }

  return (
    <div ref={dialog} tabIndex={-1} className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4" role="dialog" aria-modal="true" aria-label="Revert all">
      <div className="w-full max-w-lg max-h-[90vh] overflow-auto custom-scrollbar rounded-xl border border-neutral-800 bg-neutral-900 p-4 space-y-3 text-sm text-neutral-300">
        <div>
          <h2 className="text-base font-bold text-white">Revert {group.actor}'s {group.changes.length + added.length} changes</h2>
          <p className="text-xs text-neutral-500">Undone newest first, each checked like a single Revert. Every revert is logged and can be reverted again.</p>
          {added.length > 0 && (
            <p className="mt-1 rounded border border-blue-500/30 bg-blue-500/10 px-2 py-1.5 text-xs text-blue-200">
              {added.length === 1 ? '1 more change was' : `${added.length} more changes were`} saved together with these (a rename saves the old and
              the new zone at once), so {added.length === 1 ? 'it goes' : 'they go'} back too: {added.map(c => describe(c, false) || c.stat).join('; ')}.
            </p>
          )}
        </div>

        {plan.revert.length > 0 ? (
          <div className="text-xs">
            <div className="text-neutral-400">{plan.revert.length} will be put back</div>
            <ul className="mt-1 space-y-0.5 text-neutral-200">
              {plan.revert.map(id => <li key={id}>{describe(byId.get(id))}</li>)}
            </ul>
          </div>
        ) : (
          <p className="rounded border border-amber-500/30 bg-amber-500/10 px-2 py-1.5 text-amber-200">Nothing in this group can be reverted.</p>
        )}

        {plan.skipped.length > 0 && (
          <div className="rounded border border-amber-500/30 bg-amber-500/10 px-2 py-1.5 text-xs text-amber-200">
            <div className="font-bold">{plan.skipped.length} will be left alone</div>
            <ul className="mt-1 space-y-0.5">
              {plan.skipped.map(s => <li key={s.id}>{describe(byId.get(s.id), false) || s.stat}: {s.why}</li>)}
            </ul>
          </div>
        )}

        {plan.revert.length > 0 && (
          <label className="block text-xs text-neutral-400">
            Reason (optional, shown in History)
            <input
              value={reason}
              maxLength={REASON_MAX}
              onChange={e => onReason(e.target.value)}
              placeholder="Rolling back the T14 test changes"
              autoFocus
              className="mt-1 w-full bg-neutral-950/50 border border-neutral-800 rounded-lg px-2 py-1.5 text-sm text-white placeholder:text-neutral-700 focus:outline-none focus:border-blue-500/50"
            />
          </label>
        )}

        <div className="flex justify-end gap-2 pt-1">
          <button type="button" onClick={onCancel} className="rounded-lg border border-neutral-700 px-3 py-1.5 text-xs text-neutral-300 hover:bg-neutral-800">
            {plan.revert.length > 0 ? 'Cancel' : 'Close'}
          </button>
          {plan.revert.length > 0 && (
            <button type="button" disabled={busy} onClick={onConfirm} className="rounded-lg bg-blue-600 px-3 py-1.5 text-xs text-white hover:bg-blue-500 disabled:opacity-50">
              Revert {plan.revert.length}
            </button>
          )}
        </div>
      </div>
    </div>
  )
}

const RUNG_TEXT: Record<RungState, string> = {
  NotSet: '',
  Wins: 'wins',
  Overridden: 'overridden',
  Shadowed: 'dropped',
  Off: 'turned off',
}

const RUNG_CLASS: Record<RungState, string> = {
  NotSet: 'text-neutral-600',
  Wins: 'text-neutral-100 font-bold',
  Overridden: 'text-neutral-500 line-through',
  Shadowed: 'text-amber-400/80 line-through',
  Off: 'text-amber-400 line-through',
}

// Where a stat's value comes from: every row of its stack, most specific first, the winner highlighted.
const LadderPanel = ({ slot, anchorKey }: { slot: LadderSlot | undefined; anchorKey: boolean }) => {
  const box = 'mx-4 mb-2 rounded-lg border border-neutral-800 bg-neutral-950/40 px-3 py-2 text-xs'
  if (slot === undefined || slot === 'loading') return <div className={`${box} text-neutral-600`}>Loading where the value comes from...</div>
  if (slot === 'error') return <div className={`${box} text-red-400`}>Could not load where the value comes from.</div>

  const showAnchor = anchorKey || slot.rungs.some(r => r.anchor !== null)
  const summary = slot.turnedOff
    ? 'Set, but turned off: the game treats it as not set.'
    : !slot.inEffect
      ? 'No row sets it, so the game uses its built-in behaviour (see When not set below).'
      : slot.anchored
        ? 'Tiers 12-24 sit on the line between the winning T11 value and the winning T25 anchor.'
        : 'The winning value applies at every tier.'

  return (
    <div className={box}>
      <div className="mb-1.5 flex flex-wrap items-baseline justify-between gap-2">
        <span className="text-neutral-400">Where the value comes from</span>
        <span className="text-neutral-500">{summary}</span>
      </div>
      <table className="w-full">
        <thead className="text-[10px] uppercase tracking-wider text-neutral-600">
          <tr>
            <th className="py-1 text-left font-normal">Row (most specific first)</th>
            <th className="py-1 text-right font-normal">T11 value</th>
            {showAnchor && <th className="py-1 text-right font-normal">T25 anchor</th>}
          </tr>
        </thead>
        <tbody>
          {slot.rungs.map(r => {
            const wins = r.state === 'Wins' || r.anchorState === 'Wins'
            return (
              <tr key={r.label} className={`border-t border-neutral-800/40 ${wins ? 'bg-blue-500/10' : ''}`}>
                <td className="py-1 pr-2">
                  <span className={r.isTarget ? 'text-blue-300' : 'text-neutral-300'}>{r.label}</span>
                  {r.isTarget && <span className="ml-1.5 text-[10px] text-blue-400/70">(this row)</span>}
                  {r.toggle === false && <span className="ml-1.5 text-[10px] text-amber-400">turned off here</span>}
                  {r.toggle === true && <span className="ml-1.5 text-[10px] text-neutral-500">turned back on here</span>}
                </td>
                <td className="py-1 text-right whitespace-nowrap">
                  {r.value === null ? <span className="text-neutral-700">-</span> : (
                    <>
                      <span className={RUNG_CLASS[r.state]}>{fmt(r.value)}</span>
                      {r.curved && <span className="ml-1 text-[10px] text-amber-500" title="Has growth or tier pins">curve</span>}
                      <span className="ml-1.5 text-[10px] text-neutral-500">{RUNG_TEXT[r.state]}</span>
                    </>
                  )}
                </td>
                {showAnchor && (
                  <td className="py-1 text-right whitespace-nowrap">
                    {r.anchor === null ? <span className="text-neutral-700">-</span> : (
                      <>
                        <span className={RUNG_CLASS[r.anchorState]}>{fmt(r.anchor)}</span>
                        <span className="ml-1.5 text-[10px] text-neutral-500">{RUNG_TEXT[r.anchorState]}</span>
                      </>
                    )}
                  </td>
                )}
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}

const PreviewDialog = ({ pending, rowName, reason, onReason, busy, onCancel, onConfirm }: {
  pending: PendingEdit
  rowName: string
  reason: string
  onReason: (text: string) => void
  busy: boolean
  onCancel: () => void
  onConfirm: () => void
}) => {
  const dialog = useDialogFocus(true, onCancel)
  const { preview, mode, row } = pending
  const refused = preview.status !== 'Applied'
  const live = preview.zones.filter(z => z.enabled)
  const off = preview.zones.filter(z => !z.enabled)
  const full = live.filter(z => z.reach === 'full')
  const partial = live.filter(z => z.reach === 'partial')
  const untouched = live.filter(z => z.reach === 'none')
  const offChanged = off.filter(z => z.reach !== 'none')
  const value = (v: number | null) => (v === null ? 'not set' : fmt(v))

  return (
    <div ref={dialog} tabIndex={-1} className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4" role="dialog" aria-modal="true" aria-label={`Change ${row.label}`}>
      <div className="w-full max-w-lg max-h-[90vh] overflow-auto custom-scrollbar rounded-xl border border-neutral-800 bg-neutral-900 p-4 space-y-3 text-sm text-neutral-300">
        <div>
          <h2 className="text-base font-bold text-white">{mode === 'clear' ? 'Clear' : 'Change'} {row.label}</h2>
          <p className="text-xs text-neutral-500"><span className="font-mono">{row.stat}</span> on {rowName}</p>
        </div>

        {refused ? (
          <p className="rounded border border-amber-500/30 bg-amber-500/10 px-2 py-1.5 text-amber-200">{preview.message ?? 'This edit would be refused.'}</p>
        ) : (
          <>
            <div className="grid grid-cols-3 gap-2">
              <div className="rounded-lg bg-neutral-950/50 p-2"><div className="text-[11px] text-neutral-500">On this row now</div><div className="text-lg text-white">{value(preview.oldValue)}</div></div>
              <div className="rounded-lg bg-neutral-950/50 p-2"><div className="text-[11px] text-neutral-500">After</div><div className="text-lg text-white">{value(preview.newValue)}</div></div>
              <div className="rounded-lg bg-neutral-950/50 p-2">
                <div className="text-[11px] text-neutral-500">Live zones changed</div>
                <div className="text-lg text-white">{full.length + partial.length} <span className="text-sm text-neutral-500">of {live.length}</span></div>
              </div>
            </div>

            <p className="text-xs text-neutral-400">
              <span className="text-neutral-500">In effect on this row now: </span>
              {row.toggledOff ? 'turned off here' : row.effective === null ? 'not set anywhere' : `${fmt(row.effective)} (from ${row.source ?? 'nothing'})`}
            </p>

            {preview.zones.length > 0 && full.length + partial.length === 0 && (
              <p className="rounded border border-amber-500/30 bg-amber-500/10 px-2 py-1.5 text-xs text-amber-200">
                No monster in a live zone changes with this edit: every zone this row reaches sets its own value, has the stat turned off,
                or already uses this value. It is still saved, and applies if those zones change later.
              </p>
            )}

            {preview.losesCurve && (
              <p className="rounded border border-amber-500/30 bg-amber-500/10 px-2 py-1.5 text-xs text-amber-200">
                This row's value has growth or tier pins. {mode === 'clear' ? 'Clearing removes them too.' : 'Saving replaces them with one flat value.'} History &gt; Revert brings them back.
              </p>
            )}

            {preview.zones.length === 0 && <p className="text-xs text-neutral-500">No zone uses this row yet, so nothing in game changes now.</p>}

            {full.length > 0 && (
              <div className="text-xs">
                <div className="text-neutral-400">Every monster changes in</div>
                <div className="text-neutral-200">{full.map(z => z.zone).join(', ')}</div>
              </div>
            )}
            {partial.length > 0 && (
              <div className="text-xs space-y-1">
                <div className="text-neutral-400">Only some monsters change in these (a rank row or a monster override keeps its own value):</div>
                {partial.map(z => (
                  <div key={z.zone} className="rounded border border-neutral-800 px-2 py-1">
                    <span className="text-neutral-200">{z.zone}</span>
                    <span className="text-neutral-500"> - {z.changed.length} of {z.monsterKinds} kinds: </span>
                    {z.changed.map(m => `${m.label} ${value(m.before)} -> ${value(m.after)}`).join('; ')}
                  </div>
                ))}
              </div>
            )}
            {full.length > 0 && full[0].changed[0] && (
              <p className="text-xs text-neutral-500">
                Example: {full[0].zone}, {full[0].changed[0].label}: {value(full[0].changed[0].before)} -&gt; {value(full[0].changed[0].after)} at its tier.
              </p>
            )}
            {untouched.length > 0 && (
              <p className="text-xs text-neutral-500">
                Unchanged ({untouched.length}) - they set their own value, have the stat turned off, or already use it: {untouched.map(z => z.zone).join(', ')}
              </p>
            )}
            {offChanged.length > 0 && (
              <p className="text-xs text-neutral-500">Also changes {offChanged.length} disabled zone{offChanged.length === 1 ? '' : 's'}: {offChanged.map(z => z.zone).join(', ')}</p>
            )}
            {preview.takesEffect && <p className="text-xs text-neutral-400"><span className="text-neutral-500">Takes effect: </span>{preview.takesEffect}</p>}

            <label className="block text-xs text-neutral-400">
              Reason (optional, shown in History)
              <input
                value={reason}
                maxLength={REASON_MAX}
                onChange={e => onReason(e.target.value)}
                placeholder="T14 bosses dying too fast after the charm buff"
                autoFocus
                className="mt-1 w-full bg-neutral-950/50 border border-neutral-800 rounded-lg px-2 py-1.5 text-sm text-white placeholder:text-neutral-700 focus:outline-none focus:border-blue-500/50"
              />
            </label>
          </>
        )}

        <div className="flex justify-end gap-2 pt-1">
          <button type="button" onClick={onCancel} className="rounded-lg border border-neutral-700 px-3 py-1.5 text-xs text-neutral-300 hover:bg-neutral-800">
            {refused ? 'Close' : 'Cancel'}
          </button>
          {!refused && (
            <button type="button" disabled={busy} onClick={onConfirm} className="rounded-lg bg-blue-600 px-3 py-1.5 text-xs text-white hover:bg-blue-500 disabled:opacity-50">
              {mode === 'clear' ? 'Clear it' : preview.zones.length > 0 && full.length + partial.length === 0 ? 'Save anyway' : 'Save change'}
            </button>
          )}
        </div>
      </div>
    </div>
  )
}

const TIMING_LABEL: Record<string, string> = {
  Spawn: 'At spawn',
  Live: 'Live',
  Loot: 'At loot roll',
  Kill: 'At kill',
}

const StatHelpPanel = ({ help, onRelated }: { help: StatHelp | undefined; onRelated: (stat: string) => void }) => {
  if (!help)
    return (
      <div className="mx-4 mb-3 rounded-lg border border-neutral-800 bg-neutral-950/40 px-3 py-2 text-xs text-neutral-500">
        No help has been written for this stat yet. Check with a developer before changing it.
      </div>
    )

  const fields: [string, string | null][] = [
    ['How it works', help.details],
    ['Value', help.shape],
    ['When not set', help.whenUnset],
    ['Raise it', help.raise],
    ['Lower it', help.lower],
    ['Sensible range', help.range],
    ['Takes effect', help.takesEffect],
    ['How to undo', help.howToUndo],
  ]

  return (
    <div className="mx-4 mb-3 rounded-lg border border-neutral-800 bg-neutral-950/40 px-3 py-3 text-xs text-neutral-300 space-y-2">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm text-neutral-100">{help.summary}</span>
        <span className="rounded bg-neutral-800 px-1.5 py-0.5 text-[10px] uppercase tracking-wider text-neutral-400">
          {TIMING_LABEL[help.timing] ?? help.timing}
        </span>
      </div>
      {help.anchorNote && <p className="text-blue-300/80">{help.anchorNote}</p>}
      {help.caution && (
        <p className="rounded border border-amber-500/30 bg-amber-500/10 px-2 py-1.5 text-amber-200">
          <span className="font-bold">Careful: </span>{help.caution}
        </p>
      )}
      <dl className="grid grid-cols-1 sm:grid-cols-[120px_1fr] gap-x-3 gap-y-1.5">
        {fields.filter(([, text]) => text).map(([label, text]) => (
          <div key={label} className="contents">
            <dt className="text-neutral-500">{label}</dt>
            <dd>{text}</dd>
          </div>
        ))}
      </dl>
      {help.related.length > 0 && (
        <div className="flex flex-wrap items-center gap-1.5 pt-1">
          <span className="text-neutral-500">See also</span>
          {help.related.map(r => (
            <button
              key={r}
              type="button"
              onClick={() => onRelated(r)}
              className="rounded border border-neutral-800 px-1.5 py-0.5 font-mono text-[11px] text-blue-400 hover:border-blue-500/50"
            >
              {r}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

// ---- Worked examples, built from the rows the page already loaded (see ZoneControlExamples.tsx).

const rowName = (scope: Scope, rank: Rank, wcid: number | null) => {
  const place = scope.kind === 'zone' ? `'${scope.zone}'` : `the T${scope.variation} Default`
  if (wcid !== null) return `monster ${wcid} in ${place}`
  return `${place}'s ${capitalize(rank)} row`
}

const StatsExample = ({ scope, rank, wcid, rows }: { scope: Scope; rank: Rank; wcid: number | null; rows: StatRow[] }) => {
  const here = rowName(scope, rank, wcid)
  const usable = rows.filter(r => r.known && !r.toggledOff && !r.shadowed && r.effective !== null)
  const own = usable.find(r => r.value !== null)
  const inherited = usable.find(r => r.value === null)
  return (
    <Example id="stats">
      <p>
        For each stat the most specific layer that sets it wins: the monster override, then the zone's rank row, then the zone's
        Default row, then the tier Default's rank row and Default row. T12 and up fall back last to the v11 Default.
      </p>
      {inherited && (
        <p>
          <V>{inherited.label}</V> is not set on {here}, but it is still <V>{fmt(inherited.effective)}</V> here, taken from{' '}
          <V>{inherited.source ?? 'a lower layer'}</V>. Type a number next to it and Save to give {here} its own value. Only
          monsters this row covers change; everything else keeps <V>{fmt(inherited.effective)}</V>.
        </p>
      )}
      {own && (
        <p>
          <V>{own.label}</V> is set to <V>{fmt(own.value)}</V> on {here}{own.curved ? ' (with growth or tier pins)' : ''}.
          Press X to clear it and {here} goes back to whatever the next layer down sets. Clearing is not the same as setting 0:
          0 is a real value.
        </p>
      )}
      {!own && !inherited && <p>Nothing is in effect on {here} yet. Turn off "Only set on this row" to see every stat you can set.</p>}
      <p>Saving or clearing first shows a preview: which zones and monster kinds it reaches, with the value before and after. Nothing changes until you confirm.</p>
    </Example>
  )
}

const HistoryExample = ({ groups }: { groups: ChangeGroup[] }) => {
  const all = groups.flatMap(g => g.changes)
  const stat = all.find(c => c.kind === 'stat' && c.revertsId === null)
  const part = all.find(c => c.kind === 'section' && c.revertsId === null && !partCreated(c) && !partRemoved(c))
  const multi = groups.find(g => g.changes.length > 1)
  return (
    <Example id="history">
      {stat ? (
        <p>
          <V>{stat.actor}</V> changed <V>{stat.stat}</V> on {changeWhere(stat)} from <V>{stat.oldValue === null ? 'not set' : fmt(stat.oldValue)}</V> to{' '}
          <V>{stat.newValue === null ? 'not set' : fmt(stat.newValue)}</V>. Revert puts{' '}
          {stat.oldValue === null ? 'it back to not set (the next layer down applies again)' : <><V>{fmt(stat.oldValue)}</V> back</>}{' '}
          and logs a row of its own, which can be reverted too. If <V>{stat.stat}</V> has changed again since, the revert is refused
          instead of overwriting the newer value.
        </p>
      ) : (
        <p>A stat change shows its old and new value. Revert puts the old value back and logs a row of its own, which can be reverted too.</p>
      )}
      {part ? (
        <p>
          <V>{part.actor}</V> saved <V>{part.stat}</V> on {changeWhere(part)} as a whole part
          {part.details?.[0] ? <>: <V>{part.details[0]}</V></> : null}. Revert puts that whole part back as it was before.
        </p>
      ) : (
        <p>Other edits (enable, landblocks, bounties, lists) are logged as a whole part. Revert puts the whole part back as it was.</p>
      )}
      {multi ? (
        <p>
          <V>{multi.actor}</V>'s group holds <V>{multi.changes.length}</V> changes made together. Revert all undoes them in one go and
          shows first which ones it can and cannot undo.
        </p>
      ) : (
        <p>Changes saved together, or one person's edits within a few minutes, are grouped. Revert all undoes a whole group in one go.</p>
      )}
      <p>History shows the zone or tier you are viewing. Tick "Show every change" for everything, including server-wide lists such as bounty reward tables.</p>
    </Example>
  )
}

const CompareExample = ({ rows, a, b }: { rows: CompareRow[]; a: Scope; b: Scope }) => {
  const both = rows.find(r => inEffect(r.a) !== null && inEffect(r.b) !== null && inEffect(r.a) !== inEffect(r.b))
  const one = rows.find(r => (inEffect(r.a) === null) !== (inEffect(r.b) === null))
  return (
    <Example id="compare">
      {both ? (() => {
        const va = inEffect(both.a)!, vb = inEffect(both.b)!
        return (
          <p>
            <V>{both.label}</V> is <V>{fmt(va)}</V> in {scopeLabel(a)} (from {both.a?.source ?? 'nothing'}) and <V>{fmt(vb)}</V> in{' '}
            {scopeLabel(b)} (from {both.b?.source ?? 'nothing'}). The last column shows B against A: <V>{vb > va ? '+' : ''}{fmt(vb - va)}</V>.
            "Open in A" jumps to that stat on A's own page to change it.
          </p>
        )
      })() : (
        <p>Each row shows the value in effect on both sides at their own tier, where it comes from, and how B differs from A.</p>
      )}
      {one && (
        <p>
          <V>{one.label}</V> is set only {inEffect(one.a) === null ? `in ${scopeLabel(b)}` : `in ${scopeLabel(a)}`}: the other side has no value in
          effect (not set on any layer, or turned off there), so the game's normal behaviour applies and the column says "only in {inEffect(one.a) === null ? 'B' : 'A'}".
        </p>
      )}
      <p>A zone against its own tier Default shows exactly what the zone overrides. Two tier Defaults side by side show how a stat climbs from tier to tier.</p>
    </Example>
  )
}

// ---- Compare: two tier Defaults or zones side by side, every stat's value in effect at each one's own tier.

const scopeKey = (sc: Scope) => (sc.kind === 'default' ? `d:${sc.variation}` : `z:${sc.zone}`)

const scopeLabel = (sc: Scope) => (sc.kind === 'default' ? `T${sc.variation} Default` : `${sc.zone} (T${sc.variation})`)

interface CompareRow {
  stat: string
  label: string
  group: string
  a: StatRow | undefined
  b: StatRow | undefined
}

// The value a side actually uses: null when not set anywhere or turned off there.
const inEffect = (r: StatRow | undefined) => (r && !r.toggledOff ? r.effective : null)

const ComparePanel = ({ overview, tiers, start, helpByStat, onClose, onOpen }: {
  overview: Overview | null
  tiers: number[]
  start: Scope | null
  helpByStat: Map<string, StatHelp>
  onClose: () => void
  onOpen: (scope: Scope, stat: string, rank: Rank) => void
}) => {
  const options = useMemo<Scope[]>(() => [
    ...tiers.map(v => ({ kind: 'default' as const, variation: v })),
    ...(overview?.zones ?? []).map(z => ({ kind: 'zone' as const, zone: z.name, variation: z.variation })),
  ], [tiers, overview])

  // Start from the open zone or tier: a tier against the next tier up, a zone against its own tier Default.
  const [a, setA] = useState<Scope>(() => start ?? { kind: 'default', variation: 11 })
  const [b, setB] = useState<Scope>(() =>
    !start ? { kind: 'default', variation: 12 }
      : start.kind === 'zone' ? { kind: 'default', variation: start.variation }
        // the next tier that exists; at the top tier, the one below it (never an unlisted T26)
        : { kind: 'default', variation: tiers.find(t => t > start.variation) ?? [...tiers].reverse().find(t => t < start.variation) ?? start.variation })
  const [rank, setRank] = useState<Rank>('default')
  const [onlyDiff, setOnlyDiff] = useState(true)
  const [search, setSearch] = useState('')
  const [data, setData] = useState<{ a: StatRow[]; b: StatRow[] } | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let live = true
    setData(null)
    setError(null)
    const load = (sc: Scope) => api.get<{ rows: StatRow[] }>(`/api/zonecontrol/stats?${scopeParams(sc, null, rank)}`).then(d => d?.rows ?? [])
    Promise.all([load(a), load(b)])
      .then(([ra, rb]) => { if (live) setData({ a: ra, b: rb }) })
      .catch(err => { if (live) setError(err instanceof Error ? err.message : 'Could not load the two sides.') })
    return () => { live = false }
  }, [a, b, rank])

  const rows = useMemo<CompareRow[]>(() => {
    if (!data) return []
    const byA = new Map(data.a.map(r => [r.stat, r]))
    const byB = new Map(data.b.map(r => [r.stat, r]))
    const stats = Array.from(new Set([...byA.keys(), ...byB.keys()]))
    const q = search.trim().toLowerCase()
    return stats
      .map(stat => {
        const ra = byA.get(stat), rb = byB.get(stat)
        return { stat, label: (ra ?? rb)!.label, group: (ra ?? rb)!.group, a: ra, b: rb }
      })
      .filter(r => {
        if (q) return r.stat.toLowerCase().includes(q) || r.label.toLowerCase().includes(q) || (helpByStat.get(r.stat)?.summary.toLowerCase().includes(q) ?? false)
        const va = inEffect(r.a), vb = inEffect(r.b)
        if (va === null && vb === null) return false
        return !onlyDiff || va !== vb
      })
  }, [data, search, onlyDiff, helpByStat])

  const grouped = useMemo(() => {
    const groups = new Map<string, CompareRow[]>()
    for (const r of rows) groups.set(r.group, [...(groups.get(r.group) ?? []), r])
    return Array.from(groups, ([group, list]) => ({ group, list }))
  }, [rows])

  const picker = (value: Scope, onChange: (sc: Scope) => void, label: string) => (
    <select
      value={scopeKey(value)}
      onChange={e => { const next = options.find(o => scopeKey(o) === e.target.value); if (next) onChange(next) }}
      aria-label={label}
      className="bg-neutral-950/50 border border-neutral-800 rounded-lg px-2 py-1.5 text-sm text-white focus:outline-none focus:border-blue-500/50"
    >
      <optgroup label="Tier Defaults">
        {options.filter(o => o.kind === 'default').map(o => <option key={scopeKey(o)} value={scopeKey(o)}>{scopeLabel(o)}</option>)}
      </optgroup>
      <optgroup label="Zones">
        {options.filter(o => o.kind === 'zone').map(o => <option key={scopeKey(o)} value={scopeKey(o)}>{scopeLabel(o)}</option>)}
      </optgroup>
    </select>
  )

  const side = (r: StatRow | undefined) => {
    if (!r) return <span className="text-neutral-700">-</span>
    if (r.toggledOff) return <span className="text-amber-500">turned off</span>
    if (r.effective === null) return <span className="text-neutral-600">not set</span>
    return (
      <>
        <span className="text-neutral-100">{fmt(r.effective)}</span>
        <div className="text-[10px] text-neutral-600 truncate">from {r.source ?? 'nothing'}</div>
      </>
    )
  }

  const change = (r: CompareRow) => {
    const va = inEffect(r.a), vb = inEffect(r.b)
    if (va === vb) return <span className="text-neutral-700">same</span>
    if (va === null || vb === null) return <span className="text-amber-400">{va === null ? 'only in B' : 'only in A'}</span>
    const diff = vb - va
    const percent = (diff / Math.abs(va)) * 100
    const pct = va !== 0 ? ` (${diff > 0 ? '+' : ''}${(Math.abs(percent) < 0.1 ? percent.toPrecision(1) : percent.toFixed(1))}%)` : ''
    return <span className={diff > 0 ? 'text-blue-300' : 'text-orange-300'}>{diff > 0 ? '+' : ''}{fmt(diff)}{pct}</span>
  }

  return (
    <div className="flex-1 min-h-0 flex flex-col">
      <div className="p-4 border-b border-neutral-800/50 space-y-3">
        <div className="flex items-start justify-between gap-3">
          <div>
            <h2 className="text-lg font-bold text-white">Compare</h2>
            <p className="text-xs text-neutral-500">Every stat's value in effect on each side, at that side's own tier, with where it comes from.</p>
          </div>
          <button type="button" aria-label="Close compare" className="p-1.5 text-neutral-500 hover:text-white" onClick={onClose}>
            <X className="w-4 h-4" />
          </button>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-xs font-bold text-neutral-500">A</span>
          {picker(a, setA, 'Side A')}
          <button type="button" aria-label="Swap A and B" title="Swap" onClick={() => { setA(b); setB(a) }} className="p-1.5 rounded-lg text-neutral-400 hover:bg-neutral-800">
            <ArrowLeftRight className="w-4 h-4" />
          </button>
          <span className="text-xs font-bold text-neutral-500">B</span>
          {picker(b, setB, 'Side B')}
          <div className="flex gap-1 ml-2">
            {RANKS.map(r => (
              <button key={r} type="button" onClick={() => setRank(r)}
                className={`px-2 py-1 rounded-lg text-xs border ${rank === r ? 'bg-neutral-800 text-white border-neutral-700' : 'text-neutral-500 border-neutral-800/50 hover:text-neutral-300'}`}>
                {capitalize(r)}
              </button>
            ))}
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-4">
          <div className="relative flex-1 min-w-48">
            <Search className="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-neutral-600" aria-hidden />
            <input value={search} onChange={e => setSearch(e.target.value)} placeholder="Search stats" aria-label="Search stats to compare"
              className="w-full bg-neutral-950/50 border border-neutral-800 rounded-lg pl-8 pr-3 py-1.5 text-sm text-white placeholder:text-neutral-600 focus:outline-none focus:border-blue-500/50" />
          </div>
          <label className="flex items-center gap-2 text-xs text-neutral-400 cursor-pointer">
            <input type="checkbox" checked={onlyDiff} onChange={e => setOnlyDiff(e.target.checked)} />
            Only stats that differ
          </label>
          {data && <span className="text-xs text-neutral-500">{rows.length} stat{rows.length === 1 ? '' : 's'}</span>}
        </div>
      </div>

      <div className="flex-1 min-h-0 overflow-auto custom-scrollbar">
        {error && <p className="p-4 text-sm text-red-400">{error}</p>}
        {!error && !data && <p className="p-8 text-center text-sm text-neutral-500">Loading both sides...</p>}
        {data && <div className="px-4 pt-3 pb-2"><CompareExample rows={rows} a={a} b={b} /></div>}
        {data && rows.length === 0 && (
          <p className="p-8 text-center text-sm text-neutral-500">
            {onlyDiff && !search ? 'Every stat in effect is the same on both sides.' : 'No stats match.'}
          </p>
        )}
        {rows.length > 0 && (
          <table className="w-full text-sm table-fixed">
            <thead className="sticky top-0 z-10 bg-neutral-900 text-left text-[10px] uppercase tracking-widest text-neutral-500">
              <tr>
                <th className="px-4 py-2 font-bold w-[30%]">Stat</th>
                <th className="px-4 py-2 font-bold">A: {scopeLabel(a)}</th>
                <th className="px-4 py-2 font-bold">B: {scopeLabel(b)}</th>
                <th className="px-4 py-2 font-bold w-[18%]">B against A</th>
              </tr>
            </thead>
            <tbody>
              {grouped.map(({ group, list }) => [
                <tr key={`g-${group}`} className="bg-neutral-900/80">
                  <td colSpan={4} className="px-4 py-1.5 text-xs font-bold text-neutral-300">{group} <span className="font-normal text-neutral-600">{list.length}</span></td>
                </tr>,
                ...list.map(r => (
                  <tr key={r.stat} className="border-t border-neutral-800/30 align-top">
                    <td className="px-4 py-2 min-w-0">
                      <div className="text-neutral-200 truncate" title={helpByStat.get(r.stat)?.summary}>{r.label}</div>
                      <div className="text-[11px] font-mono text-neutral-600 truncate">{r.stat}</div>
                      <div className="flex gap-2 text-[11px]">
                        <button type="button" className="text-blue-400 hover:text-blue-300" onClick={() => onOpen(a, r.stat, rank)}>Open in A</button>
                        <button type="button" className="text-blue-400 hover:text-blue-300" onClick={() => onOpen(b, r.stat, rank)}>Open in B</button>
                      </div>
                    </td>
                    <td className="px-4 py-2 min-w-0">{side(r.a)}</td>
                    <td className="px-4 py-2 min-w-0">{side(r.b)}</td>
                    <td className="px-4 py-2 whitespace-nowrap text-xs">{change(r)}</td>
                  </tr>
                )),
              ])}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}

const GuidePanel = ({ help, onClose }: { help: HelpData | null; onClose: () => void }) => (
  <div className="flex-1 min-h-0 overflow-auto custom-scrollbar p-4 md:p-6">
    <div className="max-w-3xl space-y-6">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-lg font-bold text-white">How Zone Control works</h2>
          {help && (
            <p className="text-xs text-neutral-500">
              Help is written for {help.stats.length} of {help.total} stats so far. Press the
              <Info className="w-3 h-3 inline mx-1 -mt-0.5" aria-hidden />
              next to a stat to read what it does.
            </p>
          )}
        </div>
        <button type="button" aria-label="Close the guide" className="p-1.5 text-neutral-500 hover:text-white" onClick={onClose}>
          <X className="w-4 h-4" />
        </button>
      </div>
      {!help && <p className="text-sm text-neutral-500">The guide could not be loaded.</p>}
      {help?.guide.map(section => (
        <section key={section.title} className="space-y-2">
          <h3 className="text-sm font-bold text-neutral-200">{section.title}</h3>
          {section.paragraphs.map((p, i) => (
            <p key={i} className="text-sm text-neutral-400 leading-relaxed">{p}</p>
          ))}
        </section>
      ))}
    </div>
  </div>
)

export default ZoneControl
