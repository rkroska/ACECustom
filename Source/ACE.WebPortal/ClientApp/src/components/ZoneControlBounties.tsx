import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { AlertTriangle, History, Plus, Power, RefreshCw, Save, Search, Trash2, X } from 'lucide-react'
import { api } from '../services/api'
import { AdminToastVariant } from './common/AdminToast'
import { Example, V } from './ZoneControlExamples'
import { fmtCooldown, pct } from './zoneControlFormat'

// Two systems that both used to be called "bounty", kept apart on purpose (design review 2026-11-13: admins mixed them up):
//   KILL REWARDS - automatic: every N kills in an area pays an item. Nothing to loot. (BountyManager)
//   BOUNTY ITEMS - a lootable item a player starts on their Bounty Ledger (slots #1-#N), then kills N monsters and is paid. (ItemBounties)
// Reading follows the Zone Control page; changing needs Admin (the server checks it on every change). Every change is logged
// in the change log and can be reverted from History with "Show every change" ticked (bounty parts belong to no zone).

interface BountyEntry {
  id: number
  wcid: number
  name: string
  amount: number
  kills: number
  minutes: number
  minQb: number
}

interface BountyRow {
  enabled: boolean
  active: boolean
  entries: BountyEntry[]
}

interface ZoneBounty {
  name: string
  variation: number
  enabled: boolean
  editable: boolean
  bounty: BountyRow
}

interface RegionBounty {
  key: string
  name: string
  bounty: BountyRow
  itemOdds: number
}

interface RewardRow {
  id: number
  wcid: number
  name: string
  amount: number
  weight: number
}

interface RewardTable {
  name: string
  totalWeight: number
  entries: RewardRow[]
}

interface Place {
  key: string
  label: string
  total: number
}

interface DropRow {
  wcid: number
  name: string
  own: number
  region: string | null
  minTier: number
  maxTier: number
  fixedTier: boolean
  canDrop: boolean
  off: boolean
  set: Record<string, number>
  // null = it can never drop at that place; 0 = it could, but has no share there
  weights: Record<string, number | null>
  // places where its own weenie gives it a share (worked out by the server); elsewhere a share exists only if Zone Control added it
  natural: string[]
}

interface Limits {
  maxAmount: number
  maxKills: number
  maxCooldownMinutes: number
  maxQb: number
  maxItemBountyOdds: number
  maxWeight: number
  maxEntries: number
}

interface BountyData {
  canEdit: boolean
  zones: ZoneBounty[]
  zoneWide: BountyRow
  regions: RegionBounty[]
  tables: RewardTable[]
  places: Place[]
  drops: DropRow[]
  limits: Limits
}

/** A tier Default's bounty_odds as the stats page reads it: undefined while loading, odds null when not set or off. */
interface TierOdds {
  odds: number | null
  source: string | null
  // the tier Default row's OWN value (null = not set on it; the odds may come from a row above), for the edit's conflict check
  stored: number | null
  // fingerprint of that row's whole curve, also sent with an edit
  revision: string | null
  // its value grows by tier or has tier pins: a plain save replaces that
  curved: boolean
  // turned off on the stats page
  toggledOff: boolean
}

/** One bounty item in the world database, as the server's health check reads it (GET bounties/health). */
interface WeenieHealth {
  wcid: number
  name: string
  drops: boolean
  ownWeight: number
  zoneControlWeight: boolean
  off: boolean
  // why it can't be used (broken terms on the item); null = complete
  problem: string | null
  // where it drops now, else where it would count
  places: string[]
  // where Zone Control may give it a share
  canDropAt: string[]
  mysteryReward: boolean
  rewardList: string | null
  rewardWcid: number
  rewardName: string | null
  rewardAmount: number
  // of places, where its reward can't be paid now
  unpaidPlaces: string[]
  // the catalog: the item's terms as stored, and the task in one sentence (server-built, the game's own words)
  kind: number
  targetName: string | null
  kills: number
  killsMin: number
  killsMax: number
  hiddenKills: boolean
  region: string | null
  fixedTier: number
  task: string | null
}

interface BountyHealth {
  enabled: boolean
  // bounties a character can have going at once: the stored item_bounty_max_active, and what the server uses (held to 1..maxSlots)
  maxActiveSetting: number
  maxActive: number
  maxSlots: number
  // the Bounty Ledger: the stored item_bounty_ledger_wcid, the wcid in use (0 = ledgers off), and whether that weenie exists
  ledgerSetting: number
  ledgerWcid: number
  ledgerExists: boolean
  ledgerName: string | null
  confirmSeconds: number
  dropTableCount: number
  dropTableAgeSeconds: number | null
  reading: boolean
  refreshSeconds: number
  weenies: WeenieHealth[]
  creatureTypes: { id: number; name: string }[]
}

export type BountyMode = 'kill' | 'items'
type Notify = (message: string, variant: AdminToastVariant) => void
/** okText, when given, is the success message shown instead of the server's (or instead of a bare "Saved."). */
type Send = (url: string, body: object, okText?: string) => Promise<boolean>
type ItemsTab = 'setup' | 'catalog' | 'chance' | 'mix' | 'mystery'

const KILLS_PER_HOUR = 300   // the balance target (kills-per-hour plan); only for the "about one every X hours" estimates

const inputClass =
  'bg-neutral-950/50 border border-neutral-800 rounded-lg px-2 py-1 text-sm text-white placeholder:text-neutral-600 focus:outline-none focus:border-blue-500/50 disabled:opacity-50'
const buttonClass =
  'inline-flex items-center gap-1 px-2 py-1 rounded-lg text-xs font-bold border border-neutral-700 bg-neutral-800 text-neutral-200 hover:bg-neutral-700 disabled:opacity-40 disabled:cursor-not-allowed'
const linkClass = 'text-xs text-blue-400 hover:text-blue-300 hover:underline'
const dangerClass =
  'inline-flex items-center gap-1 px-2 py-1 rounded-lg text-xs font-bold border border-red-900/60 bg-red-950/40 text-red-300 hover:bg-red-900/40 disabled:opacity-40'

const num = (text: string) => (text.trim() === '' ? undefined : Number(text))
const fmtMinutes = (m: number) => (m >= 60 && m % 60 === 0 ? `${m / 60} h` : `${m} min`)
const oddsText = (odds: number | null | undefined) => (odds === undefined ? '...' : odds && odds > 0 ? `1 in ${odds.toLocaleString()}` : 'not set')
const fmtAge = (sec: number) => (sec < 90 ? `${Math.round(sec)} s` : sec < 5400 ? `${Math.round(sec / 60)} min` : `${(sec / 3600).toFixed(1)} h`)
const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`
const hoursPerDrop = (odds: number) => {
  const h = odds / KILLS_PER_HOUR
  return h < 1 ? `${Math.round(h * 60)} min` : `${h < 10 ? h.toFixed(1) : Math.round(h)} h`
}

/** A button that asks once more before it acts: the first click arms it, the second does it. (window.confirm is blocked in some embedded browsers.) */
const ConfirmButton = ({ label, confirm, disabled, onConfirm, icon = true }: { label: string; confirm: string; disabled?: boolean; onConfirm: () => void; icon?: boolean }) => {
  const [armed, setArmed] = useState(false)
  useEffect(() => {
    if (!armed) return
    const t = setTimeout(() => setArmed(false), 4000)
    return () => clearTimeout(t)
  }, [armed])
  return (
    <button
      type="button"
      disabled={disabled}
      className={dangerClass}
      onClick={() => { if (armed) { setArmed(false); onConfirm() } else setArmed(true) }}
    >
      {icon && <Trash2 className="w-3 h-3" aria-hidden />}{armed ? confirm : label}
    </button>
  )
}

/** [OK] / [PROBLEM] / [WARNING] / [OFF] / [NOTE] in plain text, coloured. */
type Status = 'ok' | 'problem' | 'warning' | 'off' | 'note'
const STATUS_TEXT: Record<Status, string> = { ok: '[OK]', problem: '[PROBLEM]', warning: '[WARNING]', off: '[OFF]', note: '[NOTE]' }
const STATUS_CLASS: Record<Status, string> = {
  ok: 'text-emerald-400', problem: 'text-red-400', warning: 'text-amber-400', off: 'text-neutral-400', note: 'text-blue-300',
}
const Chip = ({ status }: { status: Status }) => <span className={`font-mono text-[11px] font-bold ${STATUS_CLASS[status]}`}>{STATUS_TEXT[status]}</span>

const BountiesPanel = ({ mode, onClose, notify, onOpenStat, onOpenHistory }: {
  mode: BountyMode
  onClose: () => void
  notify: Notify
  onOpenStat: (variation: number) => void
  onOpenHistory: () => void
}) => {
  const [data, setData] = useState<BountyData | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)

  // reloads can overlap (a save and "Re-read now"): only the newest one may set the data or the error (review 2026-11-19)
  const loadSeq = useRef(0)
  const load = useCallback(async () => {
    const seq = ++loadSeq.current
    try {
      const fresh = await api.get<BountyData>('/api/zonecontrol/bounties')
      if (seq !== loadSeq.current) return
      setData(fresh)
      setLoadError(null)
    } catch (err) {
      if (seq !== loadSeq.current) return
      setLoadError(err instanceof Error ? err.message : 'Could not load the bounties.')
    }
  }, [])
  useEffect(() => { load() }, [load])

  // set at once, before React re-renders: two quick Enters (or Enter and a click) must not both get past the busy check (review 2026-11-19)
  const inFlight = useRef(false)

  /** Sends one change, says how it went, and reloads: the server's answer is the truth (another admin may have edited too). */
  const send = useCallback<Send>(async (url, body, okText) => {
    if (inFlight.current) return false
    inFlight.current = true
    setBusy(true)
    try {
      const result = await api.post<{ message: string; logged?: boolean }>(url, { ...body, reason: reason.trim() || undefined })
      // saved, but History missed it: say so (the server's message explains), never a plain success
      if (result?.logged === false)
        notify(result.message ?? 'Saved, but the change log could not record it.', 'warning')
      else
        notify(okText ?? result?.message ?? 'Saved.', 'success')
      return true
    } catch (err) {
      notify(err instanceof Error ? err.message : 'The change failed.', 'error')
      return false
    } finally {
      await load()
      inFlight.current = false
      setBusy(false)
    }
  }, [reason, notify, load])

  const canEdit = data?.canEdit ?? false
  const title = mode === 'kill' ? 'Kill Rewards' : 'Bounty Items'
  const definition = mode === 'kill'
    ? 'Automatic. Every N kills in an area pays the player an item. Nothing to loot.'
    : 'A lootable item. The player uses it on their Bounty Ledger to start it, kills N monsters, and is paid automatically.'

  return (
    <div className="flex-1 min-h-0 flex flex-col">
      <div className="p-4 border-b border-neutral-800/50 space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 className="text-lg font-bold text-white">{title}</h2>
            <p className="text-xs text-neutral-500">{definition}</p>
          </div>
          <div className="flex items-center gap-2">
            {data && !canEdit && <span className="text-xs text-amber-400">Read only: changing these needs Admin access</span>}
            <button type="button" className={buttonClass} onClick={onOpenHistory} title="Every change here is logged and can be reverted">
              <History className="w-3 h-3" aria-hidden />Recent changes
            </button>
            <button type="button" aria-label={`Close ${title}`} className="p-1.5 text-neutral-500 hover:text-white" onClick={onClose}>
              <X className="w-4 h-4" />
            </button>
          </div>
        </div>
        {canEdit && (
          <input
            value={reason}
            onChange={e => setReason(e.target.value)}
            maxLength={255}
            placeholder="Reason for your changes (optional, logged)"
            aria-label="Reason for your changes"
            className={`${inputClass} w-full`}
          />
        )}
      </div>

      {loadError && <p className="p-4 text-sm text-red-400">{loadError}</p>}
      {!data && !loadError && <p className="p-4 text-sm text-neutral-500">Loading...</p>}
      {data && mode === 'kill' && (
        <div className="flex-1 min-h-0 overflow-auto custom-scrollbar p-4 space-y-4">
          <KillRewards data={data} canEdit={canEdit} busy={busy} send={send} />
        </div>
      )}
      {data && mode === 'items' && (
        <BountyItems data={data} canEdit={canEdit} busy={busy} send={send} onOpenStat={onOpenStat} notify={notify} onReload={load} />
      )}
    </div>
  )
}

interface SectionProps {
  data: BountyData
  canEdit: boolean
  busy: boolean
  send: Send
}

// ================================================================ KILL REWARDS

const KillRewards = ({ data, canEdit, busy, send }: SectionProps) => {
  const [zoneSearch, setZoneSearch] = useState('')
  const [showAll, setShowAll] = useState(false)
  const zones = useMemo(() => {
    const q = zoneSearch.trim().toLowerCase()
    return data.zones.filter(z =>
      (q ? z.name.toLowerCase().includes(q) : showAll || z.bounty.entries.length > 0 || z.bounty.enabled))
  }, [data.zones, zoneSearch, showAll])
  const zoneWideOn = data.zoneWide.enabled

  return (
    <>
      <KillExample data={data} />

      {zoneWideOn && (
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-red-900/60 bg-red-950/30 px-3 py-2 text-sm text-red-200">
          <span className="flex items-center gap-2">
            <AlertTriangle className="w-4 h-4 shrink-0" aria-hidden />
            The All T11+ zones reward is ON, so the zone rewards further down are NOT used.
          </span>
          {canEdit && (
            <button type="button" className={buttonClass} disabled={busy} onClick={() => send('/api/zonecontrol/bounties/edit', { kind: 'zonewide', op: 'off' })}>
              <Power className="w-3 h-3" aria-hidden />Turn it off
            </button>
          )}
        </div>
      )}

      <BountyCard
        title="All T11+ zones"
        note="While on, every enabled T11-T25 zone shares this one reward (one count and one cooldown per character, so hopping tiers cannot restart it) and the zones' own rewards are not used."
        row={data.zoneWide}
        target={{ kind: 'zonewide' }}
        limits={data.limits}
        canEdit={canEdit}
        busy={busy}
        send={send}
      />

      {data.regions.map(r => (
        <BountyCard
          key={r.key}
          title={r.name}
          note="Below T11 only. The all-zones reward never replaces it. (How often this region drops bounty ITEMS is under Bounty Items > Drop chance.)"
          row={r.bounty}
          target={{ kind: 'region', name: r.key }}
          limits={data.limits}
          canEdit={canEdit}
          busy={busy}
          send={send}
        />
      ))}

      <div className="pt-2">
        <div className="flex flex-wrap items-center gap-3 mb-2">
          <h3 className="text-sm font-bold text-neutral-200">Zones</h3>
          <div className="relative">
            <Search className="w-3.5 h-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-neutral-600" aria-hidden />
            <input
              value={zoneSearch}
              onChange={e => setZoneSearch(e.target.value)}
              placeholder="Find a zone"
              aria-label="Find a zone"
              className={`${inputClass} pl-8 w-56`}
            />
          </div>
          <label className="flex items-center gap-2 text-xs text-neutral-400 cursor-pointer">
            <input type="checkbox" checked={showAll} onChange={e => setShowAll(e.target.checked)} />
            Show zones without a reward
          </label>
        </div>
        {zones.length === 0 && (
          <p className="text-xs text-neutral-500">
            {zoneSearch ? 'No zones match.' : 'No zone has a kill reward. Tick "Show zones without a reward" or find a zone to add one.'}
          </p>
        )}
        <div className="space-y-3">
          {zones.map(z => (
            <BountyCard
              key={z.name}
              title={`${z.name} (T${z.variation})${z.enabled ? '' : ' - zone disabled'}`}
              note={!z.editable ? 'Below T11 (a retail layer): kill rewards need variation 11 or higher.' : zoneWideOn ? 'Not used while the All T11+ zones reward is on.' : undefined}
              row={z.bounty}
              target={{ kind: 'zone', name: z.name }}
              limits={data.limits}
              canEdit={canEdit && z.editable}
              busy={busy}
              send={send}
            />
          ))}
        </div>
      </div>

      <p className="text-[11px] text-neutral-600">
        Room dungeons can carry their own kill reward on their weenie. Those are not shown here and the all-zones reward never replaces them.
      </p>
    </>
  )
}

interface Target {
  kind: 'zone' | 'zonewide' | 'region'
  name?: string
}

const BountyCard = ({ title, note, row, target, limits, canEdit, busy, send }: {
  title: string
  note?: string
  row: BountyRow
  target: Target
  limits: Limits
  canEdit: boolean
  busy: boolean
  send: Send
}) => {
  const [editing, setEditing] = useState<number | 'new' | null>(null)
  const edit = (op: string, body: object = {}) => send('/api/zonecontrol/bounties/edit', { ...target, op, ...body })
  const hasItem = row.entries.length > 0

  return (
    <div className="border border-neutral-800/60 rounded-xl bg-neutral-950/30">
      <div className="flex flex-wrap items-center justify-between gap-2 px-3 py-2 border-b border-neutral-800/50">
        <div>
          <span className="text-sm font-bold text-white">{title}</span>
          <span className={`ml-2 text-[10px] font-bold uppercase ${row.active ? 'text-emerald-400' : row.enabled ? 'text-amber-400' : 'text-neutral-500'}`}>
            {row.active ? 'on' : row.enabled ? 'on, no complete reward' : 'off'}
          </span>
          {note && <p className="text-[11px] text-neutral-500">{note}</p>}
        </div>
        {canEdit && (
          <div className="flex gap-1">
            <button
              type="button"
              className={buttonClass}
              disabled={busy || (!row.enabled && !hasItem)}
              title={!row.enabled && !hasItem ? 'Add a reward first' : undefined}
              onClick={() => edit(row.enabled ? 'off' : 'on')}
            >
              <Power className="w-3 h-3" aria-hidden />{row.enabled ? 'Turn off' : 'Turn on'}
            </button>
            <button type="button" className={buttonClass} disabled={busy || editing !== null} onClick={() => setEditing('new')}>
              <Plus className="w-3 h-3" aria-hidden />Reward
            </button>
          </div>
        )}
      </div>
      {(hasItem || editing === 'new') && (
        <table className="w-full text-xs">
          <thead>
            <tr className="text-left text-neutral-500">
              <th className="px-3 py-1 font-normal">ID</th>
              <th className="px-3 py-1 font-normal">Item</th>
              <th className="px-3 py-1 font-normal">Amount</th>
              <th className="px-3 py-1 font-normal">Every N kills</th>
              <th className="px-3 py-1 font-normal">Then wait</th>
              <th className="px-3 py-1 font-normal">Min QB</th>
              <th className="px-3 py-1" />
            </tr>
          </thead>
          <tbody>
            {row.entries.map(e => editing === e.id ? (
              <BountyEntryForm
                key={e.id}
                start={e}
                limits={limits}
                busy={busy}
                saveLabel="Save"
                onCancel={() => setEditing(null)}
                onSave={async v => { if (await edit('set', { id: e.id, ...v })) setEditing(null) }}
              />
            ) : (
              <tr key={e.id} className="border-t border-neutral-800/40 text-neutral-200">
                <td className="px-3 py-1 text-neutral-500">{e.id}</td>
                <td className="px-3 py-1">{e.name} <span className="text-neutral-500">({e.wcid})</span></td>
                <td className="px-3 py-1">{e.amount.toLocaleString()}</td>
                <td className="px-3 py-1">{e.kills.toLocaleString()}</td>
                <td className="px-3 py-1">{fmtMinutes(e.minutes)}</td>
                <td className="px-3 py-1">{e.minQb > 0 ? e.minQb.toLocaleString() : '-'}</td>
                <td className="px-3 py-1 text-right whitespace-nowrap">
                  {canEdit && (
                    <span className="inline-flex gap-1">
                      <button type="button" className={buttonClass} disabled={busy || editing !== null} onClick={() => setEditing(e.id)}>Edit</button>
                      <ConfirmButton label="Remove" confirm="Remove?" disabled={busy} onConfirm={() => edit('remove', { id: e.id })} />
                    </span>
                  )}
                </td>
              </tr>
            ))}
            {editing === 'new' && (
              <BountyEntryForm
                limits={limits}
                busy={busy}
                saveLabel={row.enabled ? 'Save' : 'Save and turn on'}
                onCancel={() => setEditing(null)}
                onSave={async v => {
                  if (!(await edit('add', v))) return
                  setEditing(null)
                  if (!row.enabled) await edit('on')
                }}
              />
            )}
          </tbody>
        </table>
      )}
      {!hasItem && editing !== 'new' && <p className="px-3 py-2 text-xs text-neutral-600">No rewards.</p>}
    </div>
  )
}

interface EntryValues {
  wcid?: number
  amount?: number
  kills?: number
  minutes?: number
  minQb?: number
}

const BountyEntryForm = ({ start, limits, busy, saveLabel, onCancel, onSave }: {
  start?: BountyEntry
  limits: Limits
  busy: boolean
  saveLabel: string
  onCancel: () => void
  onSave: (v: EntryValues) => void
}) => {
  const [wcid, setWcid] = useState(start ? String(start.wcid) : '')
  const [amount, setAmount] = useState(start ? String(start.amount) : '1')
  const [kills, setKills] = useState(start ? String(start.kills) : '100')
  const [minutes, setMinutes] = useState(start ? String(start.minutes) : '5')
  const [minQb, setMinQb] = useState(start ? String(start.minQb) : '0')
  const save = () => onSave({ wcid: num(wcid), amount: num(amount), kills: num(kills), minutes: num(minutes), minQb: num(minQb) ?? 0 })
  const qb = num(minQb) ?? 0
  return (
    <>
      <tr className="border-t border-neutral-800/40 bg-blue-950/20">
        <td className="px-3 py-1 text-neutral-500">{start?.id ?? 'new'}</td>
        <td className="px-3 py-1">
          <input value={wcid} onChange={e => setWcid(e.target.value)} inputMode="numeric" placeholder="Item WCID" aria-label="Item WCID" className={`${inputClass} w-28`} />
        </td>
        <td className="px-3 py-1">
          <input value={amount} onChange={e => setAmount(e.target.value)} inputMode="numeric" aria-label="Amount" title={`1 - ${limits.maxAmount.toLocaleString()}`} className={`${inputClass} w-20`} />
        </td>
        <td className="px-3 py-1">
          <input value={kills} onChange={e => setKills(e.target.value)} inputMode="numeric" aria-label="Every N kills" title={`1 - ${limits.maxKills.toLocaleString()}`} className={`${inputClass} w-24`} />
        </td>
        <td className="px-3 py-1">
          <input value={minutes} onChange={e => setMinutes(e.target.value)} inputMode="decimal" aria-label="Wait in minutes" title="Minutes; kills during the wait do not count" className={`${inputClass} w-20`} />
          <span className="ml-1 text-neutral-500">min</span>
        </td>
        <td className="px-3 py-1">
          <input value={minQb} onChange={e => setMinQb(e.target.value)} inputMode="numeric" aria-label="Min QB" title="Only players with at least this much QB count. 0 = everyone." className={`${inputClass} w-20`} />
        </td>
        <td className="px-3 py-1 text-right whitespace-nowrap">
          <span className="inline-flex gap-1">
            <button type="button" className={buttonClass} disabled={busy || !wcid.trim()} onClick={save}><Save className="w-3 h-3" aria-hidden />{saveLabel}</button>
            <button type="button" className={buttonClass} disabled={busy} onClick={onCancel}>Cancel</button>
          </span>
        </td>
      </tr>
      <tr className="bg-blue-950/20">
        <td />
        <td colSpan={6} className="px-3 pb-2 text-[11px] text-neutral-400">
          Give <V>{amount || '?'} x item {wcid || '?'}</V> for every <V>{kills || '?'}</V> of a player's own kills here, then wait{' '}
          <V>{minutes || '?'} min</V> before kills count again.{qb > 0 ? <> Only players with at least <V>{qb.toLocaleString()}</V> QB count.</> : ' Everyone counts.'}
        </td>
      </tr>
    </>
  )
}

// ================================================================ BOUNTY ITEMS

/** Everything the Setup checklist and the place table need, worked out once from the loaded data. */
interface PlaceState {
  place: Place
  isRegion: boolean
  tier: number | null
  // undefined = not known: still reading, or the read failed (oddsFailed). Never null for "unknown": null means none drop.
  odds: number | null | undefined
  oddsFailed: boolean
  oddsSource: string | null
  items: number
  // tiers only: the Default row as read (undefined while reading)
  tierRow?: TierOdds
}

const placeStatus = (p: PlaceState): { status: Status; text: string } => {
  if (p.odds === undefined) return p.oddsFailed ? { status: 'warning', text: 'drop chance unknown (it could not be read)' } : { status: 'note', text: 'reading...' }
  const chance = !!p.odds && p.odds > 0
  if (chance && p.place.total > 0) return { status: 'ok', text: 'dropping' }
  if (chance) return { status: 'problem', text: 'drop chance set, but nothing to drop' }
  if (p.place.total > 0) return { status: 'off', text: 'no drop chance, none drop' }
  return { status: 'off', text: 'not set up' }
}

const BountyItems = ({ data, canEdit, busy, send, onOpenStat, notify, onReload }: SectionProps & {
  onOpenStat: (variation: number) => void
  notify: Notify
  onReload: () => Promise<void>
}) => {
  const [tab, setTab] = useState<ItemsTab>('setup')
  const [health, setHealth] = useState<BountyHealth | null>(null)
  const [healthError, setHealthError] = useState<string | null>(null)
  const [rereading, setRereading] = useState(false)

  // The server's own check (a world DB scan): the switches, every bounty item, broken ones, unpaid rewards. Read when the panel
  // opens and again when the bounties change while Setup or Bounty items (the tabs that show it) is open, or on moving to one of
  // them - not after every edit on the other tabs, where weights and odds can't change an item (review 2026-11-19).
  const showsHealth = tab === 'setup' || tab === 'catalog'
  const [healthFor, setHealthFor] = useState<BountyData | null>(null)
  useEffect(() => {
    if (health && (!showsHealth || healthFor === data)) return
    let live = true
    api.get<BountyHealth>('/api/zonecontrol/bounties/health')
      .then(h => { if (live) { setHealth(h); setHealthFor(data); setHealthError(null) } })
      .catch(err => { if (live) setHealthError(err instanceof Error ? err.message : 'Could not run the server check.') })
    return () => { live = false }
    // health / healthFor are read, not watched: a fresh answer must not trigger another read
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data, showsHealth])

  const reread = async () => {
    if (rereading) return
    setRereading(true)
    try {
      const r = await api.post<{ message: string; health: BountyHealth }>('/api/zonecontrol/bounties/refresh', {})
      if (r?.health) setHealth(r.health)
      notify(r?.message ?? 'Re-read.', 'success')
      await onReload()
    } catch (err) {
      notify(err instanceof Error ? err.message : 'The re-read failed.', 'error')
    } finally {
      setRereading(false)
    }
  }
  const [mixPlace, setMixPlace] = useState<string>(() => data.places.find(p => p.total > 0)?.key ?? data.places[0]?.key ?? 't11')
  const [tierOdds, setTierOdds] = useState<Record<number, TierOdds>>({})
  const [oddsError, setOddsError] = useState<string | null>(null)

  // Each tier Default's bounty_odds, read the same way the stats page reads it (its Default row, inherited values included), in one
  // request (GET bounties/odds). A failed read leaves the tiers UNKNOWN, never "no drop chance" (review 2026-11-19).
  const oddsSeq = useRef(0)
  const loadOdds = useCallback(async () => {
    // only the newest read may set the odds or the error: an older one failing late must not wipe a newer answer (review 2026-11-19)
    const seq = ++oddsSeq.current
    setOddsError(null)
    try {
      const d = await api.get<{ tiers: { variation: number; value: number | null; revision: string | null; curved: boolean; effective: number | null; source: string | null; toggledOff: boolean }[] }>('/api/zonecontrol/bounties/odds')
      const results = (d?.tiers ?? []).map(row => {
        const odds = !row.toggledOff && row.effective !== null && row.effective >= 1 ? Math.round(row.effective) : null
        return [row.variation, {
          odds,
          source: odds ? row.source : null,
          stored: row.value,
          revision: row.revision,
          curved: row.curved,
          toggledOff: row.toggledOff,
        }] as const
      })
      if (seq !== oddsSeq.current) return
      setTierOdds(Object.fromEntries(results))
    } catch (err) {
      if (seq !== oddsSeq.current) return
      setTierOdds({})
      setOddsError(err instanceof Error ? err.message : 'Could not read the tiers\' drop chance.')
    }
  }, [])
  useEffect(() => { loadOdds() }, [loadOdds])

  const regionKeys = useMemo(() => new Set(data.regions.map(r => r.key)), [data.regions])
  const states = useMemo<PlaceState[]>(() => data.places.map(place => {
    const region = data.regions.find(r => r.key === place.key)
    const tier = region ? null : Number(place.key.slice(1))
    const t = tier !== null ? tierOdds[tier] : undefined
    return {
      place,
      isRegion: !!region,
      tier,
      odds: region ? (region.itemOdds > 0 ? region.itemOdds : null) : t ? t.odds : undefined,
      oddsFailed: !region && !t && oddsError !== null,
      oddsSource: region ? (region.itemOdds > 0 ? region.name : null) : t?.source ?? null,
      items: data.drops.filter(d => !d.off && (d.weights[place.key] ?? 0) > 0).length,
      tierRow: t,
    }
  }), [data, tierOdds, oddsError])

  const openMix = (key: string) => { setMixPlace(key); setTab('mix') }
  const tabClass = (active: boolean) =>
    `px-3 py-1.5 rounded-lg text-xs font-bold border transition-colors ${
      active ? 'bg-neutral-800 text-white border-neutral-700' : 'bg-neutral-900/50 text-neutral-500 border-neutral-800/50 hover:text-neutral-300'
    }`

  return (
    <div className="flex-1 min-h-0 flex flex-col">
      <div className="px-4 pt-3 flex flex-wrap gap-1">
        <button type="button" className={tabClass(tab === 'setup')} onClick={() => setTab('setup')}>1. Setup</button>
        <button type="button" className={tabClass(tab === 'catalog')} onClick={() => setTab('catalog')}>2. Bounty items</button>
        <button type="button" className={tabClass(tab === 'chance')} onClick={() => setTab('chance')}>3. Drop chance</button>
        <button type="button" className={tabClass(tab === 'mix')} onClick={() => setTab('mix')}>4. Drop mix</button>
        <button type="button" className={tabClass(tab === 'mystery')} onClick={() => setTab('mystery')}>5. Mystery rewards</button>
      </div>
      <div className="flex-1 min-h-0 overflow-auto custom-scrollbar p-4 space-y-4">
        {tab === 'setup' && (
          <Setup data={data} states={states} oddsError={oddsError} health={health} healthError={healthError} canEdit={canEdit} rereading={rereading}
            onReread={reread} onTab={setTab} onMix={openMix} onRefresh={loadOdds} />
        )}
        {tab === 'catalog' && (
          <Catalog data={data} states={states} health={health} healthError={healthError} canEdit={canEdit} busy={busy} send={send} onTab={setTab} onMix={openMix} />
        )}
        {tab === 'chance' && (
          <DropChance data={data} states={states} canEdit={canEdit} busy={busy} send={send} oddsError={oddsError}
            onOpenStat={onOpenStat} onMix={openMix} onRefresh={loadOdds} />
        )}
        {tab === 'mix' && (
          <DropMix data={data} canEdit={canEdit} busy={busy} send={send} place={mixPlace} onPlace={setMixPlace} regionKeys={regionKeys} health={health}
            states={states} onChance={() => setTab('chance')} />
        )}
        {tab === 'mystery' && <MysteryRewards data={data} canEdit={canEdit} busy={busy} send={send} health={health} />}
      </div>
    </div>
  )
}

// ---- 1. Setup: is it working, and if not, what is missing ----

interface Check {
  status: Status
  text: ReactNode
  fix?: { label: string; go: () => void }
}

const Setup = ({ data, states, oddsError, health, healthError, canEdit, rereading, onReread, onTab, onMix, onRefresh }: {
  data: BountyData
  states: PlaceState[]
  oddsError: string | null
  health: BountyHealth | null
  healthError: string | null
  canEdit: boolean
  rereading: boolean
  onReread: () => void
  onTab: (t: ItemsTab) => void
  onMix: (key: string) => void
  onRefresh: () => void
}) => {
  const [showAllPlaces, setShowAllPlaces] = useState(false)
  const droppable = data.drops.filter(d => d.canDrop)
  const dropping = states.filter(s => placeStatus(s).status === 'ok')
  const withChance = states.filter(s => s.odds && s.odds > 0)
  const chanceNoItems = withChance.filter(s => s.place.total === 0)
  const itemsNoChance = states.filter(s => s.odds === null && s.place.total > 0)
  const orphans = data.drops.filter(d => !d.canDrop)
  const notDropping = droppable.filter(d => d.off)
  const emptyTables = data.tables.filter(t => t.totalWeight <= 0)
  // a failed read is not "reading": the headline and the checklist say the drop chances are unknown instead of waiting forever
  const reading = states.some(s => s.odds === undefined && !s.oddsFailed) || (!health && !healthError)
  const unknown = states.filter(s => s.oddsFailed)
  const weenies = health?.weenies ?? []
  const broken = weenies.filter(w => w.problem)
  const unpaid = weenies.filter(w => !w.problem && w.unpaidPlaces.length > 0)
  const noShare = weenies.filter(w => !w.problem && !droppable.some(d => d.wcid === w.wcid))
  const placeList = (keys: string[]) => placeRange(data, keys)
  const names = (list: WeenieHealth[]) => list.slice(0, 6).map(w => `${w.name} (${w.wcid})`).join(', ') + (list.length > 6 ? ` and ${list.length - 6} more` : '')

  const checks: Check[] = []
  if (unknown.length > 0)
    checks.push({
      status: 'warning',
      text: <>The drop chance of {placeRange(data, unknown.map(s => s.place.key))} could not be read{oddsError ? ` (${oddsError})` : ''}, so whether items drop there is unknown.</>,
      fix: { label: 'Re-read drop chances', go: onRefresh },
    })
  if (healthError)
    checks.push({ status: 'warning', text: <>The server check could not run ({healthError}), so the switches and broken items are not checked.</> })

  // the server switches (ServerConfig): changed in game, not here
  if (health && !health.enabled)
    checks.push({
      status: 'problem',
      text: <>The server switch <V>item_bounty_enabled</V> is off: nothing drops, no bounty can be started, kills do not count and nothing is paid (players' progress is kept). In game: <CopyText text="@modifybool item_bounty_enabled true" /></>,
    })
  else if (health)
    checks.push({ status: 'ok', text: <>The server switch <V>item_bounty_enabled</V> is on.</> })
  // how players start one: the Bounty Ledger (#605)
  if (health && health.ledgerWcid === 0)
    checks.push({
      status: 'note',
      text: <>Bounty Ledgers are off (<V>item_bounty_ledger_wcid</V> is {health.ledgerSetting}): players start a bounty by double-clicking it twice. To turn them on: <CopyText text="@modifylong item_bounty_ledger_wcid 98760483" /> (the ledger SQL must be imported).</>,
    })
  else if (health && !health.ledgerExists)
    checks.push({
      status: 'warning',
      text: <>
        <V>item_bounty_ledger_wcid</V> is {health.ledgerWcid}, but no weenie {health.ledgerWcid} exists, so no player can get a Bounty Ledger; bounties
        start with two double-clicks instead. A developer imports <V>Database/Updates/World/2026-11-12-00-Bounty-Ledger.sql</V> (then Re-read now
        below), or set it to 0: <CopyText text="@modifylong item_bounty_ledger_wcid 0" />
      </>,
    })
  else if (health)
    checks.push({ status: 'ok', text: <>Players start a bounty by using it on their {health.ledgerName ?? 'Bounty Ledger'} ({health.ledgerWcid}), handed out with their first bounty and at login.</> })
  if (health)
    checks.push({
      status: 'ok',
      text: <>
        A player can have up to {health.maxActive} {health.maxActive === 1 ? 'bounty' : 'bounties'} going at once (slots #1-#{health.maxActive}, <V>item_bounty_max_active</V>
        {health.maxActiveSetting !== health.maxActive ? ` is ${health.maxActiveSetting}, held to 1-${health.maxSlots}` : ''}).
      </>,
    })

  // do bounty items exist, and can they drop
  if (health && weenies.length === 0) {
    // no bounty items at all: the headline and the "Start here" box say it, once is enough
  } else if (droppable.length === 0)
    checks.push({
      status: 'problem',
      text: health
        ? <>{plural(weenies.length, 'bounty item')} exist{weenies.length === 1 ? 's' : ''}, but none can drop from monsters: {broken.length} broken, {noShare.length} with no drop share.</>
        : 'No bounty item can drop from monsters.',
      fix: noShare.length > 0 ? { label: 'Give one a share', go: () => onTab('mix') } : undefined,
    })
  else
    checks.push({ status: 'ok', text: <>{plural(droppable.length, 'bounty item')} can drop from monsters.</>, fix: { label: 'See them', go: () => onTab('mix') } })

  for (const w of broken)
    checks.push({ status: 'problem', text: <>{w.name} ({w.wcid}) can't be used: {w.problem}. A developer has to fix the item in the world database.</>, fix: { label: 'See it', go: () => onTab('catalog') } })
  for (const w of unpaid)
    checks.push(w.mysteryReward
      ? {
          status: 'problem',
          text: <>{w.name} ({w.wcid}) pays from the mystery list '{w.rewardList}', which has nothing payable in {placeList(w.unpaidPlaces)}. Players can't start it there, and finished ones wait unpaid.</>,
          fix: { label: 'Open lists', go: () => onTab('mystery') },
        }
      : { status: 'problem', text: <>{w.name} ({w.wcid}): its reward {w.rewardName ?? 'item'} ({w.rewardWcid}) can't be made as an item, so players can't start it. A developer has to fix the item.</> })
  if (droppable.length > 0 && noShare.length > 0)
    checks.push({
      status: 'note',
      text: <>{noShare.length === 1 ? '1 bounty item has' : `${noShare.length} bounty items have`} no drop share, so they only come from quests, NPCs or admins: {names(noShare)}.</>,
      fix: { label: 'Give a share', go: () => onTab('mix') },
    })

  // nothing exists yet: only the switches and the one thing to do; the rest would be noise until an item exists
  const empty = !!health && weenies.length === 0
  if (!reading && !empty && withChance.length === 0 && unknown.length === 0)
    checks.push({ status: 'off', text: 'No tier or region has a drop chance, so nothing drops anywhere.', fix: { label: 'Set a drop chance', go: () => onTab('chance') } })
  for (const s of chanceNoItems)
    checks.push({
      status: 'problem',
      text: <>{s.place.label} has a drop chance ({oddsText(s.odds)}) but nothing in its drop mix, so a winning roll drops nothing.</>,
      fix: { label: 'Open its mix', go: () => onMix(s.place.key) },
    })
  // only when SOME place has a chance: with none anywhere, the "no tier or region has a drop chance" line says it once
  if (itemsNoChance.length > 0 && withChance.length > 0)
    checks.push({
      status: 'warning',
      text: <>{placeRange(data, itemsNoChance.map(s => s.place.key))}: bounty items could drop there, but no drop chance is set.</>,
      fix: { label: 'Set a drop chance', go: () => onTab('chance') },
    })
  for (const d of notDropping)
    checks.push({ status: 'warning', text: <>{d.name} ({d.wcid}) is stopped: it does not drop anywhere.</>, fix: { label: 'Open', go: () => onTab('mix') } })
  for (const d of orphans)
    checks.push({ status: 'warning', text: <>The drop mix still has settings for {d.name} ({d.wcid}), which can no longer drop. Reset them to tidy up.</>, fix: { label: 'Open', go: () => onTab('mix') } })
  for (const t of emptyTables)
    checks.push({ status: 'problem', text: <>The mystery list '{t.name}' has nothing that can be paid.</>, fix: { label: 'Open', go: () => onTab('mystery') } })

  const order: Status[] = ['problem', 'warning', 'off', 'ok', 'note']
  checks.sort((a, b) => order.indexOf(a.status) - order.indexOf(b.status))
  const problems = checks.filter(c => c.status === 'problem' || c.status === 'warning').length

  const headline = reading
    ? { status: 'note' as Status, text: 'Reading each tier\'s drop chance...' }
    : health && !health.enabled
      ? { status: 'problem' as Status, text: 'Nothing drops or counts right now: the server switch item_bounty_enabled is off.' }
    : dropping.length > 0
      ? { status: 'ok' as Status, text: `Bounty items are dropping in ${dropping.map(s => s.place.label).join(', ')}.${problems ? ` ${problems} thing${problems === 1 ? '' : 's'} to look at below.` : ''}` }
    // never a definite "nothing drops" while some drop chances are unknown (review 2026-11-19)
    : unknown.length > 0
      ? { status: 'warning' as Status, text: `Whether bounty items drop is unknown: the drop chance of ${placeRange(data, unknown.map(s => s.place.key))} could not be read.` }
      : { status: 'problem' as Status, text: `Bounty items cannot drop anywhere right now: ${health && weenies.length === 0 ? 'the world database has no bounty items' : droppable.length === 0 ? 'no bounty item can drop from monsters' : withChance.length === 0 ? 'no tier or region has a drop chance' : 'no place with a drop chance has anything to drop'}.` }

  const used = states.filter(st => (st.odds && st.odds > 0) || st.place.total > 0)
  const unused = states.filter(st => !used.includes(st))

  return (
    <>
      <div className={`rounded-xl border px-3 py-2 text-sm ${headline.status === 'ok' ? 'border-emerald-900/60 bg-emerald-950/20 text-emerald-200' : headline.status === 'problem' ? 'border-red-900/60 bg-red-950/30 text-red-200' : 'border-neutral-800 text-neutral-300'}`}>
        <Chip status={headline.status} /> {headline.text}
      </div>

      {empty && (
        <div className="rounded-xl border border-blue-900/50 bg-blue-950/20 px-3 py-3 text-sm text-neutral-200 space-y-2">
          <p className="font-bold">Start here: no bounty items exist yet.</p>
          <p className="text-xs text-neutral-400">
            A developer makes each bounty item in the world database. Describe the one you want in a short form and hand the request over;
            once it exists, this page shows where it drops and what to set next.
          </p>
          <button type="button" className={buttonClass} onClick={() => onTab('catalog')}>Write a request</button>
        </div>
      )}

      {!empty && <div>
        <h3 className="text-xs font-bold uppercase tracking-widest text-neutral-500 mb-2">What do you want to do?</h3>
        <div className="flex flex-wrap gap-2">
          <button type="button" className={buttonClass} onClick={() => onTab('chance')}>Start drops in a tier</button>
          <button type="button" className={buttonClass} onClick={() => onTab('mix')}>Make one bounty item rarer or commoner</button>
          <button type="button" className={buttonClass} onClick={() => onTab('mystery')}>Change what mystery bounties pay</button>
          <button type="button" className={buttonClass} onClick={() => document.getElementById('bounty-emergency')?.scrollIntoView({ behavior: 'smooth' })}>Stop something</button>
        </div>
      </div>}

      <Example id="bounty-how" title="How a bounty item drops (all four must be true)">
        <p>1. <span className="text-neutral-100">The bounty item exists.</span> A developer makes it in the world database: what to kill, how many, the reward, which tiers it drops in. (Bounty items tab)</p>
        <p>2. <span className="text-neutral-100">The place has a drop chance:</span> 1 in N kills. (Drop chance tab)</p>
        <p>3. <span className="text-neutral-100">The item is in that place's drop mix:</span> when a kill wins, this decides which item. (Drop mix tab)</p>
        <p>4. <span className="text-neutral-100">Mystery items have a reward list</span> to pay from. Only for items whose reward is a mystery. (Mystery rewards tab)</p>
      </Example>

      <div>
        <div className="flex items-center gap-3 mb-2">
          <h3 className="text-xs font-bold uppercase tracking-widest text-neutral-500">Checklist</h3>
          <button type="button" className={linkClass} onClick={onRefresh}><RefreshCw className="w-3 h-3 inline -mt-0.5 mr-1" aria-hidden />Re-read drop chances</button>
        </div>
        {oddsError && <p className="text-xs text-red-400 mb-1">{oddsError}</p>}
        <ul className="space-y-1.5">
          {checks.map((c, i) => (
            <li key={i} className="flex items-start justify-between gap-3 text-xs text-neutral-300">
              <span><Chip status={c.status} /> {c.text}</span>
              {c.fix && <button type="button" className={`${linkClass} shrink-0`} onClick={c.fix.go}>{c.fix.label}</button>}
            </li>
          ))}
        </ul>
        <div className="mt-2 flex flex-wrap items-center gap-2 text-[11px] text-neutral-600">
          <span>
            The server re-reads the bounty items every {health ? Math.round(health.refreshSeconds / 60) : 5} minutes
            {health && (health.dropTableAgeSeconds === null ? '; it has not read them yet since it started.' : `; last read ${fmtAge(health.dropTableAgeSeconds)} ago (${plural(health.dropTableCount, 'droppable item')}).`)}
            {health?.reading && ' Reading now...'}
          </span>
          {canEdit && (
            <button type="button" className={linkClass} disabled={rereading} onClick={onReread} title="Fresh copies of the bounty items from the world database, then a new read">
              <RefreshCw className={`w-3 h-3 inline -mt-0.5 mr-1 ${rereading ? 'animate-spin' : ''}`} aria-hidden />{rereading ? 'Re-reading...' : 'Re-read now'}
            </button>
          )}
        </div>
      </div>

      {!empty && (
        <div>
          <h3 className="text-xs font-bold uppercase tracking-widest text-neutral-500 mb-2">Place by place</h3>
          {used.length > 0
            ? <PlaceTable states={showAllPlaces ? states : used} onMix={onMix} onChance={() => onTab('chance')} />
            : <p className="text-xs text-neutral-500">No tier or region has a drop chance or anything in its drop mix yet.</p>}
          {unused.length > 0 && used.length > 0 && (
            <p className="mt-1 text-[11px] text-neutral-600">
              {showAllPlaces ? '' : `${unused.length} more not set up: ${placeRange(data, unused.map(st => st.place.key))}. `}
              <button type="button" className={linkClass} onClick={() => setShowAllPlaces(v => !v)}>{showAllPlaces ? 'Show only the places in use' : 'Show them'}</button>
            </p>
          )}
        </div>
      )}

      {!empty && <div id="bounty-emergency" className="rounded-xl border border-red-900/50 bg-red-950/10 px-3 py-2 text-xs text-neutral-300 space-y-1.5">
        <p className="font-bold text-red-300">Stopping things</p>
        <p><span className="text-neutral-100">One bounty item, everywhere:</span> Drop mix tab, then "Stop dropping" on its row. Players keep the ones they have.</p>
        <p>
          <span className="text-neutral-100">All drops in one tier or region:</span> Drop chance tab, save 0 on its row. Not Clear: Clear only removes the
          tier's own number, so a number it inherits (from T11) still applies, and a stored 0 cleared starts drops again. Zones, Leader and Boss rows
          with their own bounty_odds (stats page) keep theirs.
        </p>
        <p>
          <span className="text-neutral-100">Everything at once:</span> in game, <CopyText text="@modifybool item_bounty_enabled false" />. No drops, no starting, no
          counting and no payouts until it is set back to true. Players' progress is kept.
        </p>
      </div>}
    </>
  )
}

const CopyText = ({ text }: { text: string }) => {
  const [copied, setCopied] = useState(false)
  return (
    <button
      type="button"
      title="Copy"
      className="font-mono text-neutral-100 bg-neutral-900 border border-neutral-700 rounded px-1 hover:border-blue-500/50"
      onClick={() => {
        navigator.clipboard?.writeText(text).then(() => { setCopied(true); setTimeout(() => setCopied(false), 1500) }, () => undefined)
      }}
    >
      {text}{copied ? ' (copied)' : ''}
    </button>
  )
}

const PlaceTable = ({ states, onMix, onChance }: { states: PlaceState[]; onMix: (key: string) => void; onChance: () => void }) => (
  <table className="w-full text-xs">
    <thead>
      <tr className="text-left text-neutral-500">
        <th className="px-2 py-1 font-normal">Where</th>
        <th className="px-2 py-1 font-normal">Drop chance</th>
        <th className="px-2 py-1 font-normal">Items in its mix</th>
        <th className="px-2 py-1 font-normal">Status</th>
      </tr>
    </thead>
    <tbody>
      {states.map(s => {
        const st = placeStatus(s)
        return (
          <tr key={s.place.key} className="border-t border-neutral-800/40 text-neutral-200">
            <td className="px-2 py-1">{s.place.label}</td>
            <td className="px-2 py-1">
              <button type="button" className="hover:underline" onClick={onChance}>{oddsText(s.odds)}</button>
            </td>
            <td className="px-2 py-1">
              <button type="button" className="hover:underline" onClick={() => onMix(s.place.key)}>{s.items}</button>
            </td>
            <td className="px-2 py-1"><Chip status={st.status} /> <span className="text-neutral-400">{st.text}</span></td>
          </tr>
        )
      })}
    </tbody>
  </table>
)

// ---- 2. Bounty items: what exists (read-only), what one needs, and a request for a developer ----

/** Place keys as a short list: consecutive tiers as a range ("T11-T25"), then the regions by name. */
const placeRange = (data: BountyData, keys: string[]) => {
  const isRegion = (k: string) => data.regions.some(r => r.key === k)
  const tiers = keys.filter(k => !isRegion(k) && /^t\d+$/.test(k)).map(k => Number(k.slice(1))).sort((a, b) => a - b)
  const parts: string[] = []
  for (let i = 0; i < tiers.length;) {
    let j = i
    while (j + 1 < tiers.length && tiers[j + 1] === tiers[j] + 1) j++
    parts.push(i === j ? `T${tiers[i]}` : `T${tiers[i]}-T${tiers[j]}`)
    i = j + 1
  }
  for (const k of keys) if (isRegion(k)) parts.push(placeSuffixLabel(data, k))
  return parts.join(', ')
}

const rewardText = (w: WeenieHealth) =>
  w.mysteryReward
    ? `Mystery, from the list '${w.rewardList}'`
    : w.rewardWcid
      ? `${w.rewardAmount > 1 ? `${w.rewardAmount.toLocaleString()} x ` : ''}${w.rewardName ?? 'WCID ' + w.rewardWcid}`
      : 'none set'

const Catalog = ({ data, states, health, healthError, canEdit, busy, send, onTab, onMix }: SectionProps & {
  states: PlaceState[]
  health: BountyHealth | null
  healthError: string | null
  onTab: (t: ItemsTab) => void
  onMix: (key: string) => void
}) => {
  const weenies = health?.weenies ?? []
  const [dev, setDev] = useState(() => { try { return localStorage.getItem('zc-bounty-dev') === '1' } catch { return false } })
  const setDevStored = (on: boolean) => {
    setDev(on)
    try { localStorage.setItem('zc-bounty-dev', on ? '1' : '0') } catch { /* private window: just not remembered */ }
  }
  const weightSend = (wcid: number, action: string) => send('/api/zonecontrol/bounties/weight', { wcid, action, places: [] })
  const statusOf = (w: WeenieHealth): Check => {
    if (w.problem) return { status: 'problem', text: `can't be used: ${w.problem}` }
    if (w.unpaidPlaces.length > 0)
      return w.mysteryReward
        ? { status: 'problem', text: `the list '${w.rewardList}' has nothing payable in ${placeRange(data, w.unpaidPlaces)}`, fix: { label: 'Open lists', go: () => onTab('mystery') } }
        : { status: 'problem', text: 'its reward item can\'t be made, so it can\'t be started' }
    if (w.off) return { status: 'off', text: 'stopped: drops nowhere' }
    if (w.drops) {
      // it has a share, but it only really drops where that place also has a drop chance
      const at = states.filter(s => w.places.includes(s.place.key))
      // never a green OK before the drop chances are known (review 2026-11-19)
      if (at.some(s => s.odds === undefined))
        return at.some(s => s.oddsFailed)
          ? { status: 'warning', text: 'in the drop mix; whether it drops is unknown (the drop chances could not be read)' }
          : { status: 'note', text: 'in the drop mix (reading the drop chances...)' }
      const live = at.filter(s => !!s.odds && s.odds > 0).map(s => s.place.key)
      if (live.length === 0)
        return { status: 'off', text: `in the mix, but ${placeRange(data, w.places)} ${w.places.length === 1 ? 'has' : 'have'} no drop chance, so none drop`, fix: { label: 'Set a drop chance', go: () => onTab('chance') } }
      // the server switch off stops every drop, whatever the odds and shares say (review 2026-11-19)
      if (health && !health.enabled)
        return { status: 'off', text: `set to drop in ${placeRange(data, live)}, but nothing drops while item_bounty_enabled is off` }
      return { status: 'ok', text: live.length === at.length ? 'dropping' : `dropping in ${placeRange(data, live)} (no drop chance in the rest)` }
    }
    return { status: 'note', text: 'no drop share: only from quests, NPCs or admins' }
  }

  return (
    <>
      <p className="text-xs text-neutral-400">
        Bounty items are made by a developer in the world database. This tab shows what exists and whether each one works; you can't
        change an item's terms here. Below it: what an item needs, and a form that writes the request for the developer.
      </p>

      {healthError && <p className="text-xs text-red-400">The server check could not run ({healthError}), so the list is unknown.</p>}
      {!health && !healthError && <p className="text-xs text-neutral-500">Reading the bounty items...</p>}
      {health && weenies.length === 0 && (
        <div className="rounded-xl border border-red-900/60 bg-red-950/20 px-3 py-2 text-sm text-red-200">
          <Chip status="problem" /> The world database has no bounty items yet. Fill in the request below and hand it to a developer.
        </div>
      )}

      {weenies.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full text-xs">
            <thead>
              <tr className="text-left text-neutral-500">
                <th className="px-2 py-1 font-normal">Item</th>
                <th className="px-2 py-1 font-normal">What the player must do</th>
                <th className="px-2 py-1 font-normal">Reward</th>
                <th className="px-2 py-1 font-normal">Drops in</th>
                <th className="px-2 py-1 font-normal">Status</th>
                <th className="px-2 py-1 font-normal" />
              </tr>
            </thead>
            <tbody>
              {weenies.map(w => {
                const st = statusOf(w)
                const drop = data.drops.find(d => d.wcid === w.wcid)
                const mixAt = w.places[0] ?? w.canDropAt[0]
                return (
                  <tr key={w.wcid} className="border-t border-neutral-800/40 text-neutral-200 align-top">
                    <td className="px-2 py-1.5">{w.name} <span className="text-neutral-500">({w.wcid})</span></td>
                    <td className="px-2 py-1.5 text-neutral-300">{w.task ?? '-'}</td>
                    <td className="px-2 py-1.5">{rewardText(w)}</td>
                    <td className="px-2 py-1.5">
                      {w.drops ? placeRange(data, w.places) : w.problem ? '-' : (
                        <>never{w.canDropAt.length > 0 && <span className="block text-[11px] text-neutral-500">can be given a share in {placeRange(data, w.canDropAt)}</span>}</>
                      )}
                    </td>
                    <td className="px-2 py-1.5">
                      <Chip status={st.status} /> <span className="text-neutral-400">{st.text}</span>
                      {st.fix && <button type="button" className={`${linkClass} ml-1`} onClick={st.fix.go}>{st.fix.label}</button>}
                    </td>
                    <td className="px-2 py-1.5 whitespace-nowrap space-x-1 text-right">
                      {canEdit && drop && !drop.off && <ConfirmButton label="Stop dropping" confirm="Stop everywhere?" icon={false} disabled={busy} onConfirm={() => weightSend(w.wcid, 'off')} />}
                      {canEdit && drop?.off && <button type="button" className={buttonClass} disabled={busy} onClick={() => weightSend(w.wcid, 'on')}><Power className="w-3 h-3" aria-hidden />Allow dropping</button>}
                      {mixAt && <button type="button" className={linkClass} onClick={() => onMix(mixAt)}>Open in Drop mix</button>}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      <label className="flex items-center gap-2 text-xs text-neutral-400">
        <input type="checkbox" checked={dev} onChange={e => setDevStored(e.target.checked)} />
        Show developer details (property numbers and SQL)
      </label>
      <ItemNeeds dev={dev} />
      <RequestBuilder data={data} health={health} dev={dev} />

      <Example id="bounty-dev" title="For developers">
        <p>After importing a bounty item (or the Bounty Ledger): Setup tab, "Re-read now" (or wait up to {health ? Math.round(health.refreshSeconds / 60) : 5} minutes) so the server reads it.</p>
        <p>To test one in game: <CopyText text="/bounty make <wcid>" /> puts one in your pack (add a tier 11-25 for one that takes the kill's tier).</p>
        <p><CopyText text="/bounty droptable" /> lists what can drop where, with each item's share.</p>
        <p>Player text (appraisal, chat) must stay plain ASCII: no emoji, curly quotes or long dashes in the name or target name.</p>
      </Example>
    </>
  )
}

const NEEDS: { choice: string; means: string; property: string }[] = [
  { choice: 'The item itself', means: 'A normal item: name, icon, item type, value, burden.', property: 'base weenie' },
  { choice: 'Where it counts', means: 'One tier (11-25), the tier it drops in, or a region (Valley of Death, Thaelaryn Island). Only kills there count.', property: 'ItemBountyTier 57700, or ItemBountyRegion (string 57703)' },
  { choice: 'Tiers it drops in', means: 'Only for "the tier it drops in": the lowest and highest tier. Unset = 11-25.', property: 'ItemBountyDropTierMin / Max 57709 / 57710' },
  { choice: 'What to kill', means: 'Anything, or one creature type (and the plural name players see, e.g. "Banderlings").', property: 'ItemBountyKind 57701, ItemBountyTarget 57702, ItemBountyTargetName (string 57701)' },
  { choice: 'How many', means: 'A fixed count, or a range: each new item rolls its own round number from it.', property: 'ItemBountyKills 57703, or ItemBountyKillsMin / Max 57706 / 57707' },
  { choice: 'Hidden count', means: 'The count is rolled, and shown, only when the player starts the bounty.', property: 'ItemBountyMystery flag 1 (57711)' },
  { choice: 'Reward', means: 'An item and how many, or a mystery: one pick from a reward list (lists are on the Mystery rewards tab).', property: 'ItemBountyRewardWcid 57704 + ItemBountyRewardAmount 57705, or flag 2 + ItemBountyRewardTable (string 57704)' },
  { choice: 'Built-in share points', means: 'How often it is picked from a place\'s drop mix. 0 = never drops from monsters (quests, NPCs, admins).', property: 'ItemBountyDropWeight 57708' },
]

const ItemNeeds = ({ dev }: { dev: boolean }) => {
  const text = 'What a bounty item needs\n' + NEEDS.map(n => `- ${n.choice}: ${n.means}${dev ? ` [${n.property}]` : ''}`).join('\n')
  return (
    <div className="rounded-xl border border-neutral-800/60 bg-neutral-950/30 px-3 py-2 text-xs space-y-2">
      <div className="flex items-center justify-between gap-2">
        <p className="font-bold text-neutral-200">What a bounty item needs</p>
        <CopyButton text={text} label="Copy as text" />
      </div>
      <table className="w-full">
        <tbody>
          {NEEDS.map(n => (
            <tr key={n.choice} className="border-t border-neutral-800/40 align-top">
              <td className="py-1 pr-3 text-neutral-100 whitespace-nowrap">{n.choice}</td>
              <td className="py-1 pr-3 text-neutral-300">{n.means}</td>
              {dev && <td className="py-1 font-mono text-[10px] text-neutral-600">{n.property}</td>}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

const CopyButton = ({ text, label, disabled }: { text: string; label: string; disabled?: boolean }) => {
  const [copied, setCopied] = useState(false)
  return (
    <button
      type="button"
      className={buttonClass}
      disabled={disabled}
      onClick={() => navigator.clipboard?.writeText(text).then(() => { setCopied(true); setTimeout(() => setCopied(false), 1500) }, () => undefined)}
    >
      {copied ? 'Copied' : label}
    </button>
  )
}

// ---- the request builder: a plain-text request and the SQL for the item's bounty terms; nothing is written here ----

interface WcidInfo {
  wcid: number
  exists: boolean
  name: string | null
  givable: boolean
  isBounty: boolean
}

/** The server's look at a WCID typed into the builder (debounced): undefined = nothing to look up, null = looking. */
const useWcidLookup = (text: string) => {
  const [info, setInfo] = useState<WcidInfo | null | undefined>(undefined)
  const wcid = /^\d{1,10}$/.test(text.trim()) ? Number(text.trim()) : 0
  useEffect(() => {
    if (!wcid) { setInfo(undefined); return }
    let live = true
    setInfo(null)
    const t = setTimeout(() => {
      api.get<WcidInfo>(`/api/zonecontrol/bounties/lookup?wcid=${wcid}`)
        .then(r => { if (live) setInfo(r) })
        .catch(() => { if (live) setInfo(undefined) })
    }, 400)
    return () => { live = false; clearTimeout(t) }
  }, [wcid])
  // an answer about another WCID (the field changed since) is not this field's answer: still looking (review 2026-11-19)
  return wcid && info && info.wcid !== wcid ? null : info
}

interface BountyRequest {
  name: string
  wcid: string
  where: 'drop' | 'tier' | 'region'
  tier: string
  region: string
  tierMin: string
  tierMax: string
  kind: 'any' | 'species'
  target: string
  targetName: string
  count: 'fixed' | 'range'
  kills: string
  killsMin: string
  killsMax: string
  hidden: boolean
  reward: 'item' | 'mystery'
  rewardWcid: string
  amount: string
  list: string
  weight: string
  notes: string
}

const LIST_NAME = /^[a-z0-9_-]{1,32}$/
const int = (text: string) => (/^\d+$/.test(text.trim()) ? Number(text.trim()) : NaN)
const n0 = (n: number) => n.toLocaleString('en-US')

/** The request as text and SQL, and what stops the SQL being usable. Pure: the form's state in, words out. */
const buildRequest = (r: BountyRequest, ctx: {
  limits: Limits
  regions: RegionBounty[]
  tables: string[]
  creature: string | null
  reward: WcidInfo | null | undefined
  item: WcidInfo | null | undefined
  // the Bounty Ledger's wcid (0 = ledgers off): never a bounty item itself
  ledgerWcid: number
}) => {
  const problems: string[] = []
  const notes: string[] = []
  const name = r.name.trim()
  if (!name) problems.push('Give the item a name.')
  if (/[^\x20-\x7e]/.test(name + r.targetName)) problems.push('Names must be plain ASCII (no emoji, curly quotes or long dashes): the game client can\'t draw them.')

  // where
  const region = ctx.regions.find(x => x.key === r.region)
  const tier = int(r.tier)
  const tierMin = r.tierMin.trim() ? int(r.tierMin) : 11
  const tierMax = r.tierMax.trim() ? int(r.tierMax) : 25
  if (r.where === 'tier' && !(tier >= 11 && tier <= 25)) problems.push('Pick a tier from 11 to 25.')
  if (r.where === 'region' && !region) problems.push('Pick a region.')
  if (r.where === 'drop' && !(tierMin >= 11 && tierMax <= 25 && tierMin <= tierMax)) problems.push('Drop tiers must be 11 to 25, lowest first.')
  const place = r.where === 'region' ? region?.name ?? 'a region' : r.where === 'tier' ? `Tier ${r.tier}` : 'the tier it dropped in'

  // what and how many
  const target = int(r.target)
  if (r.kind === 'species' && !(target > 0)) problems.push('Pick the creature type.')
  const what = r.kind === 'species' ? (r.targetName.trim() || (ctx.creature ? `${ctx.creature}s` : 'creatures of one type')) : 'creatures'
  const kills = int(r.kills)
  let lo = int(r.killsMin), hi = int(r.killsMax)
  if (lo > hi) [lo, hi] = [hi, lo]
  const max = ctx.limits.maxKills
  if (r.count === 'fixed' && !(kills >= 1 && kills <= max)) problems.push(`Kill count must be 1 to ${n0(max)}.`)
  if (r.count === 'range' && !(lo >= 1 && hi <= max)) problems.push(`Both ends of the kill range must be 1 to ${n0(max)}.`)
  const count = r.count === 'fixed' ? (kills > 0 ? n0(kills) : '?') : lo > 0 && hi > 0 ? (lo === hi ? n0(lo) : `${n0(lo)}-${n0(hi)}`) : '?'
  const task = r.hidden
    ? `Kill a hidden number of ${what} in ${place} (${count}, revealed when the bounty is started).`
    : r.count === 'range' && lo > 0 && hi > 0 && lo !== hi
      ? `Kill ${count} ${what} in ${place} (each new item rolls its own count).`
      : `Kill ${count} ${what} in ${place}.`

  // reward
  const rewardWcid = int(r.rewardWcid)
  const amount = r.amount.trim() ? int(r.amount) : 1
  const list = r.list.trim().toLowerCase()
  if (r.reward === 'item') {
    if (!(rewardWcid > 0)) problems.push('Give the reward item\'s WCID.')
    // the SQL is only offered once THIS WCID is checked and payable; the plain request can still be copied meanwhile
    else if (!ctx.reward) problems.push(`Checking reward WCID ${rewardWcid}...`)
    else if (!ctx.reward.exists) problems.push(`No item has WCID ${rewardWcid}.`)
    else if (!ctx.reward.givable) problems.push(`${ctx.reward.name ?? rewardWcid} can't be paid as a reward (it isn't a carryable item).`)
    if (!(amount >= 1 && amount <= ctx.limits.maxAmount)) problems.push(`Reward amount must be 1 to ${n0(ctx.limits.maxAmount)}.`)
  } else if (!LIST_NAME.test(list)) problems.push('A mystery list name is 1-32 of a-z, 0-9, _ or - (no dot: "name.t15" versions are found by themselves).')
  else if (!ctx.tables.includes(list)) notes.push(`There is no list '${list}' yet. Create it on the Mystery rewards tab before the item goes live, or players can't start it.`)
  const rewardLine = r.reward === 'item'
    ? `${amount > 1 ? `${n0(amount)} x ` : ''}${ctx.reward?.name ?? 'item'} (WCID ${r.rewardWcid.trim() || '?'})`
    : `a mystery: one pick from the list '${list || '?'}'`

  // share
  const weight = r.weight.trim() ? int(r.weight) : 0
  if (!(weight >= 0 && weight <= ctx.limits.maxWeight)) problems.push(`Share points must be 0 to ${n0(ctx.limits.maxWeight)}.`)

  // the item's WCID
  const wcid = int(r.wcid)
  if (r.wcid.trim() && !(wcid > 0)) problems.push('The item WCID must be a number.')
  if (wcid > 0 && wcid === ctx.ledgerWcid) problems.push(`WCID ${wcid} is the Bounty Ledger, which players start bounties on: pick another item.`)
  else if (ctx.item?.isBounty) notes.push(`WCID ${wcid} (${ctx.item.name}) is already a bounty item: the SQL replaces its terms.`)
  else if (ctx.item && ctx.item.exists) notes.push(`WCID ${wcid} is ${ctx.item.name}: the SQL turns it into a bounty item.`)
  else if (ctx.item && !ctx.item.exists) notes.push(`No weenie has WCID ${wcid} yet: the developer makes the item first (name, icon, type, value).`)
  else if (!r.wcid.trim()) notes.push('No item WCID given: the developer picks one (the SQL uses @wcid).')

  const text = [
    `Bounty item request: ${name || '(no name)'}`,
    '',
    `Task: ${task}`,
    `Reward: ${rewardLine}`,
    `Counts in: ${place}${r.where === 'drop' ? ` (drops in T${tierMin}-T${tierMax})` : ''}`,
    `Built-in share points: ${weight}${weight === 0 ? ' (never drops from monsters)' : ''}`,
    `Item WCID: ${r.wcid.trim() || 'developer picks'}`,
    ...(r.notes.trim() ? ['', `Notes: ${r.notes.trim()}`] : []),
    '',
    'Made with the Zone Control portal request builder (Bounty Items > Bounty items).',
  ].join('\n')

  // the SQL: only the bounty terms; the item weenie itself must exist
  const id = wcid > 0 ? String(wcid) : '@wcid'
  const ints: [number, number, string][] = []
  const strs: [number, string, string][] = []
  ints.push([57701, r.kind === 'species' ? 2 : 1, `ItemBountyKind = ${r.kind === 'species' ? 'one creature type' : 'any kill'}`])
  if (r.kind === 'species' && target > 0) ints.push([57702, target, `ItemBountyTarget = ${ctx.creature ?? target}`])
  if (r.where === 'tier' && tier > 0) ints.push([57700, tier, 'ItemBountyTier'])
  if (r.count === 'fixed' && kills > 0) ints.push([57703, kills, 'ItemBountyKills'])
  if (r.count === 'range' && lo > 0) ints.push([57706, lo, 'ItemBountyKillsMin'], [57707, hi, 'ItemBountyKillsMax'])
  if (r.reward === 'item' && rewardWcid > 0) ints.push([57704, rewardWcid, `ItemBountyRewardWcid = ${ctx.reward?.name ?? '?'}`], [57705, amount, 'ItemBountyRewardAmount'])
  if (weight > 0) ints.push([57708, weight, 'ItemBountyDropWeight'])
  if (r.where === 'drop' && tierMin !== 11) ints.push([57709, tierMin, 'ItemBountyDropTierMin'])
  if (r.where === 'drop' && tierMax !== 25) ints.push([57710, tierMax, 'ItemBountyDropTierMax'])
  const flags = (r.hidden ? 1 : 0) | (r.reward === 'mystery' ? 2 : 0)
  if (flags) ints.push([57711, flags, `ItemBountyMystery = ${[r.hidden && 'hidden count', r.reward === 'mystery' && 'mystery reward'].filter(Boolean).join(' + ')}`])
  if (r.where === 'region' && region) strs.push([57703, region.key, 'ItemBountyRegion'])
  if (r.reward === 'mystery' && list) strs.push([57704, list, 'ItemBountyRewardTable'])
  if (r.kind === 'species' && r.targetName.trim()) strs.push([57701, r.targetName.trim(), 'ItemBountyTargetName'])
  const q = (s: string) => `'${s.replace(/\\/g, '\\\\').replace(/'/g, "''")}'`
  // the repo's style, "(id, type, value), -- what it is": one line each, so a comment can't swallow the next row
  const rows = <T,>(list: T[], fmt: (x: T) => [string, string]) =>
    list.map((x, i) => { const [v, c] = fmt(x); return `${v}${i < list.length - 1 ? ',' : ';'} -- ${c.replace(/[\r\n]/g, ' ')}` }).join('\n')
  // header comments stay on one line: a name from the world DB with a line break would otherwise spill out of the comment
  const oneLine = (s: string) => s.replace(/[\r\n]+/g, ' ').replace(/[^\x20-\x7e]/g, '?')
  const sql = [
    `-- Bounty terms for ${oneLine(name) || '(no name)'}, from the Zone Control portal request builder. Review before committing.`,
    '-- Sets only the bounty properties: the item weenie itself (name, icon, item type, value, burden) must already exist.',
    `-- Task: ${oneLine(task)}`,
    `-- Reward: ${oneLine(rewardLine)}`,
    ...(wcid > 0 ? [] : ['SET @wcid = 0; -- the item\'s WCID']),
    `DELETE FROM \`weenie_properties_int\` WHERE \`object_Id\` = ${id} AND \`type\` BETWEEN 57700 AND 57711;`,
    `DELETE FROM \`weenie_properties_string\` WHERE \`object_Id\` = ${id} AND \`type\` IN (57701, 57703, 57704);`,
    '',
    'INSERT INTO `weenie_properties_int` (`object_Id`,`type`,`value`) VALUES',
    rows(ints, ([t, v, c]) => [`  (${id}, ${t}, ${v})`, c]),
    ...(strs.length ? ['', 'INSERT INTO `weenie_properties_string` (`object_Id`,`type`,`value`) VALUES', rows(strs, ([t, v, c]) => [`  (${id}, ${t}, ${q(v)})`, c])] : []),
  ].join('\n')

  return { text, sql, problems, notes }
}

const EMPTY_REQUEST: BountyRequest = {
  name: '', wcid: '', where: 'drop', tier: '15', region: '', tierMin: '11', tierMax: '25', kind: 'any', target: '', targetName: '',
  count: 'range', kills: '', killsMin: '', killsMax: '', hidden: false, reward: 'item', rewardWcid: '', amount: '1', list: '', weight: '10', notes: '',
}

const RequestBuilder = ({ data, health, dev }: { data: BountyData; health: BountyHealth | null; dev: boolean }) => {
  const [r, setR] = useState<BountyRequest>(() => ({ ...EMPTY_REQUEST, region: data.regions[0]?.key ?? '' }))
  const [show, setShow] = useState<'text' | 'sql'>('text')
  const shown = dev ? show : 'text'
  const set = <K extends keyof BountyRequest>(k: K, v: BountyRequest[K]) => setR(prev => ({ ...prev, [k]: v }))
  const reward = useWcidLookup(r.reward === 'item' ? r.rewardWcid : '')
  const item = useWcidLookup(r.wcid)
  const creatures = useMemo(() => [...(health?.creatureTypes ?? [])].sort((a, b) => a.name.localeCompare(b.name)), [health])
  const tables = useMemo(() => [...new Set(data.tables.map(t => t.name.split('.')[0]))].sort(), [data.tables])
  const out = buildRequest(r, {
    limits: data.limits,
    regions: data.regions,
    tables,
    creature: creatures.find(c => c.id === Number(r.target))?.name ?? null,
    reward,
    item,
    ledgerWcid: health?.ledgerWcid ?? 0,
  })
  const label = 'text-[11px] text-neutral-500 block mb-0.5'
  // each choice's radios share a group name, so the browser groups them (arrow keys, one tab stop)
  const radio = (k: keyof BountyRequest, value: string, text: string) => (
    <label className="inline-flex items-center gap-1 mr-3 text-neutral-300">
      <input type="radio" name={`bounty-request-${k}`} value={value} checked={r[k] === value} onChange={() => set(k, value as never)} />{text}
    </label>
  )
  const lookupText = (info: WcidInfo | null | undefined) =>
    info === null ? 'looking...' : !info ? '' : !info.exists ? 'no such item' : `${info.name}${info.isBounty ? ' (already a bounty item)' : ''}`

  return (
    <div className="rounded-xl border border-neutral-800/60 bg-neutral-950/30 px-3 py-3 text-xs space-y-3">
      <div>
        <p className="font-bold text-neutral-200">Request a bounty item</p>
        <p className="text-neutral-500">Fill this in and copy the request for a developer. Nothing is saved or changed from here.</p>
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <div>
          <span className={label}>Name players see</span>
          <input className={`${inputClass} w-full`} value={r.name} aria-label="Name players see" maxLength={64} placeholder="Banderling Bounty" onChange={e => set('name', e.target.value)} />
        </div>
        <div>
          <span className={label}>Item WCID (optional) <span className="text-neutral-400">{lookupText(item)}</span></span>
          <input className={`${inputClass} w-full`} value={r.wcid} aria-label="Item WCID" inputMode="numeric" placeholder="the developer picks one" onChange={e => set('wcid', e.target.value)} />
        </div>
      </div>

      <div>
        <span className={label}>Where it counts</span>
        {radio('where', 'drop', 'The tier it drops in')}
        {radio('where', 'tier', 'One tier')}
        {data.regions.length > 0 && radio('where', 'region', 'A region')}
        <div className="mt-1 flex flex-wrap items-center gap-2 text-neutral-400">
          {r.where === 'drop' && (<>Drops in T<input className={`${inputClass} w-14`} value={r.tierMin} aria-label="Lowest tier it drops in" onChange={e => set('tierMin', e.target.value)} /> to T<input className={`${inputClass} w-14`} value={r.tierMax} aria-label="Highest tier it drops in" onChange={e => set('tierMax', e.target.value)} /></>)}
          {r.where === 'tier' && (<>T<input className={`${inputClass} w-14`} value={r.tier} aria-label="The one tier it counts in" onChange={e => set('tier', e.target.value)} /> only (it drops only there too)</>)}
          {r.where === 'region' && (
            <select className={inputClass} aria-label="Region it counts in" value={r.region} onChange={e => set('region', e.target.value)}>
              {data.regions.map(x => <option key={x.key} value={x.key}>{x.name}</option>)}
            </select>
          )}
        </div>
      </div>

      <div>
        <span className={label}>What to kill</span>
        {radio('kind', 'any', 'Anything')}
        {radio('kind', 'species', 'One creature type')}
        {r.kind === 'species' && (
          <div className="mt-1 flex flex-wrap items-center gap-2">
            <select className={inputClass} aria-label="Creature type to kill" value={r.target} onChange={e => set('target', e.target.value)}>
              <option value="">Pick a creature type...</option>
              {creatures.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
            <input className={`${inputClass} w-48`} value={r.targetName} aria-label="Plural name players see for the creature type" maxLength={48} placeholder="plural name (optional)" onChange={e => set('targetName', e.target.value)} />
          </div>
        )}
      </div>

      <div>
        <span className={label}>How many</span>
        {radio('count', 'range', 'A range (each item rolls its own)')}
        {radio('count', 'fixed', 'A fixed count')}
        <label className="inline-flex items-center gap-1 text-neutral-300"><input type="checkbox" checked={r.hidden} onChange={e => set('hidden', e.target.checked)} />Hidden until started</label>
        <div className="mt-1 flex flex-wrap items-center gap-2 text-neutral-400">
          {r.count === 'fixed'
            ? <input className={`${inputClass} w-28`} value={r.kills} aria-label="Kill count" inputMode="numeric" placeholder="1000" onChange={e => set('kills', e.target.value)} />
            : (<><input className={`${inputClass} w-28`} value={r.killsMin} aria-label="Fewest kills" inputMode="numeric" placeholder="500" onChange={e => set('killsMin', e.target.value)} /> to <input className={`${inputClass} w-28`} value={r.killsMax} aria-label="Most kills" inputMode="numeric" placeholder="1500" onChange={e => set('killsMax', e.target.value)} /></>)}
          kills
        </div>
      </div>

      <div>
        <span className={label}>Reward</span>
        {radio('reward', 'item', 'An item')}
        {radio('reward', 'mystery', 'A mystery (from a reward list)')}
        <div className="mt-1 flex flex-wrap items-center gap-2 text-neutral-400">
          {r.reward === 'item'
            ? (<><input className={`${inputClass} w-20`} value={r.amount} aria-label="Reward amount" inputMode="numeric" onChange={e => set('amount', e.target.value)} /> x WCID <input className={`${inputClass} w-28`} value={r.rewardWcid} aria-label="Reward item WCID" inputMode="numeric" onChange={e => set('rewardWcid', e.target.value)} /><span className="text-neutral-300">{lookupText(reward)}{reward && reward.exists && !reward.givable ? ' - not payable' : ''}</span></>)
            : (<>
                <input className={`${inputClass} w-40`} value={r.list} aria-label="Mystery reward list" list="bounty-lists" placeholder="mystery" onChange={e => set('list', e.target.value)} />
                <datalist id="bounty-lists">{tables.map(t => <option key={t} value={t} />)}</datalist>
                <span>{tables.length ? `lists now: ${tables.join(', ')}` : 'no lists yet'}</span>
              </>)}
        </div>
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <div>
          <span className={label}>Built-in share points (0 = never drops from monsters)</span>
          <input className={`${inputClass} w-28`} value={r.weight} aria-label="Built-in share points" inputMode="numeric" onChange={e => set('weight', e.target.value)} />
        </div>
        <div>
          <span className={label}>Notes for the developer</span>
          <input className={`${inputClass} w-full`} value={r.notes} aria-label="Notes for the developer" maxLength={300} placeholder="icon like a scroll, sold by..." onChange={e => set('notes', e.target.value)} />
        </div>
      </div>

      {(out.problems.length > 0 || out.notes.length > 0) && (
        <ul className="space-y-1">
          {out.problems.map(p => <li key={p}><Chip status="problem" /> <span className="text-neutral-300">{p}</span></li>)}
          {out.notes.map(p => <li key={p}><Chip status="note" /> <span className="text-neutral-300">{p}</span></li>)}
        </ul>
      )}

      <div className="space-y-1.5">
        <div className="flex flex-wrap items-center gap-2">
          {dev && <>
            <button type="button" className={`${buttonClass} ${shown === 'text' ? 'border-blue-500/50' : ''}`} onClick={() => setShow('text')}>Request</button>
            <button type="button" className={`${buttonClass} ${shown === 'sql' ? 'border-blue-500/50' : ''}`} onClick={() => setShow('sql')}>SQL</button>
          </>}
          <span className="flex-1" />
          <CopyButton text={out.text} label="Copy request" />
          {dev && <CopyButton text={out.sql} label="Copy SQL" disabled={out.problems.length > 0} />}
          <button type="button" className={linkClass} onClick={() => setR({ ...EMPTY_REQUEST, region: data.regions[0]?.key ?? '' })}>Clear</button>
        </div>
        <pre className="whitespace-pre-wrap break-words rounded-lg border border-neutral-800 bg-neutral-950/60 p-2 font-mono text-[11px] text-neutral-300 max-h-72 overflow-auto custom-scrollbar">
          {shown === 'text' ? out.text : out.problems.length ? `-- Fix the problems above first.\n\n${out.sql}` : out.sql}
        </pre>
        {shown === 'sql' && <p className="text-[11px] text-neutral-600">For a file under Database/Updates/World. Re-runnable: it clears the item's bounty properties first. After the import, Setup tab: Re-read now.</p>}
      </div>
    </div>
  )
}

// ---- 3. Drop chance: how often a kill drops a bounty item ----

const DropChance = ({ data, states, canEdit, busy, send, oddsError, onOpenStat, onMix, onRefresh }: SectionProps & {
  states: PlaceState[]
  oddsError: string | null
  onOpenStat: (variation: number) => void
  onMix: (key: string) => void
  onRefresh: () => void
}) => (
  <>
    <p className="text-xs text-neutral-400">
      How often a kill drops a bounty item: <span className="text-neutral-100">1 in N kills</span>. A <span className="text-neutral-100">bigger</span> number
      means <span className="text-neutral-100">fewer</span> drops. Not set or 0 = none drop. One roll per kill; kills that give no XP or luminance never drop one.
    </p>
    <ChanceExample states={states} />
    {oddsError && <p className="text-xs text-red-400">{oddsError}</p>}
    <table className="w-full text-xs">
      <thead>
        <tr className="text-left text-neutral-500">
          <th className="px-2 py-1 font-normal">Where</th>
          <th className="px-2 py-1 font-normal">Drop chance</th>
          <th className="px-2 py-1 font-normal">Set on</th>
          <th className="px-2 py-1 font-normal">Items in its mix</th>
          <th className="px-2 py-1" />
        </tr>
      </thead>
      <tbody>
        {states.map(s => (
          <tr key={s.place.key} className="border-t border-neutral-800/40 text-neutral-200 align-middle">
            <td className="px-2 py-1.5">{s.place.label}</td>
            <td className="px-2 py-1.5">
              {s.isRegion ? (
                <RegionOdds region={data.regions.find(r => r.key === s.place.key)!} limits={data.limits} canEdit={canEdit} busy={busy} send={send} />
              ) : (
                <TierOddsEditor state={s} limits={data.limits} canEdit={canEdit} busy={busy} send={send} onSaved={onRefresh} onOpenStat={onOpenStat} />
              )}
            </td>
            <td className="px-2 py-1.5 text-neutral-500">{s.isRegion ? 'this page' : s.oddsSource ?? '-'}</td>
            <td className="px-2 py-1.5">
              <button type="button" className={`hover:underline ${s.odds && s.place.total === 0 ? 'text-red-400' : ''}`} onClick={() => onMix(s.place.key)}>
                {s.place.total === 0 ? 'none' : s.items}
              </button>
            </td>
            <td className="px-2 py-1.5 text-right">
              {s.tier !== null && (
                <button type="button" className={linkClass} onClick={() => onOpenStat(s.tier!)} title="Leaders, Bosses and single zones can have their own number: set those on the stats page">
                  Leaders, bosses, zones
                </button>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
    <p className="text-[11px] text-neutral-500">
      For T11-T25 this sets the zone stat <V>bounty_odds</V> on each tier's Default row: every monster in the tier uses it unless its Leader or Boss row, or
      its zone, sets its own (those are on the stats page: "Leaders, bosses, zones"). Saves are logged and can be reverted from History like any stat change.
      "Clear" removes the tier's own number, so it uses the one above it, if any.{' '}
      <button type="button" className={linkClass} onClick={onRefresh}>Re-read</button>
    </p>
  </>
)

/**
 * The "1 in N" box both drop-chance rows use (a tier's and a region's), so they behave the same: Enter or Save, the range hint, the
 * 0 warning and the "about one every" preview of the number typed. blankIsZero: an empty box means 0 (regions) instead of nothing
 * typed yet (tiers, where Clear is the way back to unset). confirmSave: Save asks first (a tier curve it would replace), and Enter
 * then does not save.
 */
const OddsInput = ({ place, initial, placeholder = 'not set', limits, busy, blankIsZero, isUnchanged, confirmSave, onSave, extra, hint }: {
  place: string
  initial: string
  placeholder?: string
  limits: Limits
  busy: boolean
  blankIsZero: boolean
  isUnchanged: (value: number) => boolean
  confirmSave?: string
  onSave: (value: number) => void
  extra?: ReactNode
  hint: ReactNode
}) => {
  const [text, setText] = useState(initial)
  useEffect(() => { setText(initial) }, [initial])
  const value = blankIsZero && text.trim() === '' ? 0 : num(text)
  const valid = value !== undefined && Number.isInteger(value) && value >= 0 && value <= limits.maxItemBountyOdds
  const unchanged = valid && isUnchanged(value!)
  const save = () => { if (valid && !unchanged) onSave(value!) }
  return (
    <span className="inline-flex flex-wrap items-center gap-1.5">
      <span>1 in</span>
      <input value={text} onChange={e => setText(e.target.value)} inputMode="numeric" placeholder={placeholder} aria-label={`${place} drop chance`} className={`${inputClass} w-32`}
        onKeyDown={e => { if (e.key === 'Enter' && !confirmSave) save() }} />
      {confirmSave
        ? <ConfirmButton label="Save" confirm={confirmSave} icon={false} disabled={busy || !valid || unchanged} onConfirm={save} />
        : <button type="button" className={buttonClass} disabled={busy || !valid || unchanged} onClick={save}><Save className="w-3 h-3" aria-hidden />Save</button>}
      {extra}
      {text.trim() !== '' && !valid && <span className="text-red-400">0 to {limits.maxItemBountyOdds.toLocaleString()}, whole number</span>}
      {valid && value === 0 && !unchanged && <span className="text-amber-400">{blankIsZero ? 'blank or 0' : '0'} = no bounty item drops in {place}</span>}
      {valid && !unchanged ? (value! > 0 && <span className="text-neutral-500">about one every {hoursPerDrop(value!)}</span>) : hint}
    </span>
  )
}

/**
 * One tier's drop chance, edited in place: the tier Default's bounty_odds through the same stat/set and stat/clear the stats page
 * uses (logged, revertable, refused if the row - its whole curve - changed since it was read). 0 saves "off" on this tier even when a
 * row above sets a number; Clear removes the tier's own number.
 */
const TierOddsEditor = ({ state, limits, canEdit, busy, send, onSaved, onOpenStat }: {
  state: PlaceState
  limits: Limits
  canEdit: boolean
  busy: boolean
  send: Send
  onSaved: () => void
  onOpenStat: (variation: number) => void
}) => {
  const row = state.tierRow
  const tier = state.tier!
  if (!row) return <span>{state.oddsFailed ? 'unknown (read failed)' : oddsText(state.odds)}</span>
  const hint = state.odds ? <span className="text-neutral-600">about one every {hoursPerDrop(state.odds)} at {KILLS_PER_HOUR} kills an hour</span> : null
  if (!canEdit) return <span className="inline-flex flex-wrap items-center gap-1.5">{oddsText(state.odds)}{hint}</span>
  if (row.toggledOff)
    return <span>turned off on the stats page <button type="button" className={linkClass} onClick={() => onOpenStat(tier)}>Open</button></span>

  const body = (extra: object) => ({ variation: tier, stat: 'bounty_odds', expected: row.stored, expectedRevision: row.revision ?? undefined, ...extra })
  const save = async (value: number) => {
    const ok = value > 0 ? `T${tier} drop chance: 1 in ${value.toLocaleString()}.` : `T${tier}: no bounty items drop (saved as 0).`
    // read the odds again whatever happened: after a refusal (someone changed it) the row must show the new value and revision
    // before a retry, or the next save is refused for the same reason (review 2026-11-19)
    await send('/api/zonecontrol/stat/set', body({ value: String(value) }), ok)
    onSaved()
  }
  const clear = async () => {
    await send('/api/zonecontrol/stat/clear', body({}), `T${tier} drop chance cleared.`)
    onSaved()
  }

  return (
    // the box holds this tier's OWN number; one it inherits shows greyed as the placeholder, so typing that same number pins it here
    // (a later T11 change then no longer moves this tier) - review 2026-11-19
    <OddsInput place={`T${tier}`} initial={row.stored !== null ? String(row.stored) : ''} limits={limits} busy={busy} blankIsZero={false}
      placeholder={row.stored === null && row.odds ? `${row.odds.toLocaleString()} (inherited)` : 'not set'}
      isUnchanged={v => row.stored !== null && v === row.stored}
      confirmSave={row.curved ? 'Replace its tier curve?' : undefined}
      onSave={save} hint={hint}
      extra={row.stored !== null && <ConfirmButton label="Clear" confirm={`Clear T${tier}?`} icon={false} disabled={busy} onConfirm={clear} />} />
  )
}

/** A region's drop chance (BountyConfig.ItemBountyOdds), edited in place like a tier's: blank or 0 = none drop. Refused when another
 * admin changed it since the page read it. */
const RegionOdds = ({ region, limits, canEdit, busy, send }: { region: RegionBounty; limits: Limits; canEdit: boolean; busy: boolean; send: Send }) => {
  const hint = region.itemOdds > 0 ? <span className="text-neutral-600">about one every {hoursPerDrop(region.itemOdds)} at {KILLS_PER_HOUR} kills an hour</span> : null
  if (!canEdit)
    return <span className="inline-flex flex-wrap items-center gap-1.5">{region.itemOdds > 0 ? `1 in ${region.itemOdds.toLocaleString()}` : 'not set'}{hint}</span>
  const save = (value: number) => {
    send('/api/zonecontrol/bounties/edit', { kind: 'region', name: region.key, op: 'itemodds', odds: value, expectedOdds: region.itemOdds },
      value > 0 ? `${region.name} drop chance: 1 in ${value.toLocaleString()}.` : `${region.name}: no bounty items drop.`)
  }
  return (
    <OddsInput place={region.name} initial={region.itemOdds > 0 ? String(region.itemOdds) : ''} limits={limits} busy={busy} blankIsZero
      isUnchanged={v => v === region.itemOdds} onSave={save} hint={hint} />
  )
}

// ---- 4. Drop mix: which bounty item a winning roll gives ----

type ApplyTo = 'here' | 'tiers' | 'all'

const DropMix = ({ data, canEdit, busy, send, place, onPlace, regionKeys, health, states, onChance }: SectionProps & {
  place: string
  onPlace: (key: string) => void
  regionKeys: Set<string>
  health: BountyHealth | null
  states: PlaceState[]
  onChance: () => void
}) => {
  const [view, setView] = useState<'one' | 'grid'>('one')
  // complete bounty items with no share anywhere (quest / NPC only so far): not in the drop list, but a share can still be given
  const unshared = (health?.weenies ?? []).filter(w => !w.problem && !data.drops.some(d => d.wcid === w.wcid))
  const weightSend = (wcid: number, action: string, places: string[] = []) => send('/api/zonecontrol/bounties/weight', { wcid, action, places })
  const current = data.places.find(p => p.key === place) ?? data.places[0]
  const orphans = data.drops.filter(d => !d.canDrop)

  return (
    <>
      <p className="text-xs text-neutral-400">
        When a kill wins its drop chance, <span className="text-neutral-100">one</span> bounty item is picked from that place's mix. Each item's chance is its{' '}
        <span className="text-neutral-100">share points</span> divided by all the points in that place. A bigger number means it drops more often compared with the others.
      </p>
      <MixExample data={data} place={current} />

      <div className="flex flex-wrap items-center gap-3">
        {view === 'one' && (
          <label className="flex items-center gap-2 text-xs text-neutral-400">
            Where
            <select value={current?.key} onChange={e => onPlace(e.target.value)} className={`${inputClass} py-1`} aria-label="Where">
              {data.places.map(p => <option key={p.key} value={p.key}>{p.label}{p.total === 0 ? ' (empty)' : ''}</option>)}
            </select>
          </label>
        )}
        <div className="flex gap-1">
          <button type="button" className={`${buttonClass} ${view === 'one' ? '' : 'opacity-60'}`} onClick={() => setView('one')}>One place</button>
          <button type="button" className={`${buttonClass} ${view === 'grid' ? '' : 'opacity-60'}`} onClick={() => setView('grid')}>All places</button>
        </div>
      </div>

      {data.drops.length === 0 && unshared.length === 0 && (
        <p className="text-xs text-neutral-500">No bounty item can drop from monsters yet, so there is nothing to mix. A developer has to add bounty items first.</p>
      )}
      {(data.drops.length > 0 || unshared.length > 0) && current && view === 'one' && (
        // keyed by the place: switching places drops an open share edit, so its Save can never land on the new place
        <OnePlace key={current.key} data={data} place={current} canEdit={canEdit} busy={busy} weightSend={weightSend} isRegion={regionKeys.has(current.key)}
          unshared={unshared.filter(w => w.canDropAt.includes(current.key))} odds={states.find(st => st.place.key === current.key)?.odds} onChance={onChance} />
      )}
      {data.drops.length > 0 && view === 'grid' && (
        <MixGrid data={data} canEdit={canEdit} busy={busy} weightSend={weightSend} onPick={key => { onPlace(key); setView('one') }} />
      )}

      {orphans.length > 0 && (
        <div className="text-xs text-neutral-400 space-y-1">
          <p className="font-bold text-neutral-300">Settings for items that can no longer drop</p>
          {orphans.map(d => (
            <div key={d.wcid} className="flex items-center gap-2">
              <span>{d.name} ({d.wcid})</span>
              {canEdit && <ConfirmButton label="Reset" confirm="Reset?" disabled={busy} onConfirm={() => weightSend(d.wcid, 'reset')} />}
            </div>
          ))}
        </div>
      )}
    </>
  )
}

const OnePlace = ({ data, place, canEdit, busy, weightSend, isRegion, unshared, odds, onChance }: {
  data: BountyData
  place: Place
  canEdit: boolean
  busy: boolean
  weightSend: (wcid: number, action: string, places?: string[]) => Promise<boolean>
  isRegion: boolean
  unshared: WeenieHealth[]
  // the place's drop chance (1 in N kills): undefined while reading, null when none is set
  odds: number | null | undefined
  onChance: () => void
}) => {
  // the number people think in: how many kills, on average, per drop of THIS item here (drop chance x its share)
  const perItem = (w: number) => (odds && odds > 0 && w > 0 && place.total > 0 ? Math.round(odds * place.total / w) : null)
  const [editing, setEditing] = useState<number | null>(null)
  const [addWcid, setAddWcid] = useState('')
  const [addPoints, setAddPoints] = useState('10')
  const key = place.key
  const here = data.drops.filter(d => d.weights[key] != null)
  const inMix = here.filter(d => !d.off && (d.weights[key] ?? 0) > 0).sort((a, b) => (b.weights[key] ?? 0) - (a.weights[key] ?? 0))
  const outside = here.filter(d => d.off || (d.weights[key] ?? 0) === 0)
  const addable: { wcid: number; name: string }[] = [...outside.filter(d => !d.off), ...unshared]
  const addPick = addable.find(d => String(d.wcid) === addWcid) ?? addable[0]
  const addValue = num(addPoints)

  return (
    <div className="space-y-3">
      <p className="text-xs text-neutral-400">
        {place.label} drop chance: <span className="text-neutral-100">{oddsText(odds)}</span>{odds === null ? ', so nothing here drops until one is set. ' : '. '}
        <button type="button" className={linkClass} onClick={onChance}>{odds ? 'Change it' : 'Set one'}</button>
      </p>
      <table className="w-full text-xs">
        <thead>
          <tr className="text-left text-neutral-500">
            <th className="px-2 py-1 font-normal">Item</th>
            <th className="px-2 py-1 font-normal" title="Drop chance x this item's share: on average, one of this item per this many kills here">Drops about</th>
            <th className="px-2 py-1 font-normal">Share</th>
            <th className="px-2 py-1 font-normal">Share points</th>
            <th className="px-2 py-1 font-normal">From</th>
            <th className="px-2 py-1" />
          </tr>
        </thead>
        <tbody>
          {inMix.length === 0 && (
            <tr><td colSpan={6} className="px-2 py-2 text-amber-400">Nothing is in the {place.label} mix: a winning roll here drops nothing.</td></tr>
          )}
          {inMix.map(d => {
            const w = d.weights[key] ?? 0
            const changed = key in d.set
            return editing === d.wcid ? (
              <EditPoints key={d.wcid} drop={d} place={place} isRegion={isRegion} busy={busy}
                onCancel={() => setEditing(null)}
                onSave={async (points, applyTo) => {
                  const places = applyTo === 'here' ? [key] : [applyTo]
                  if (await weightSend(d.wcid, String(points), places)) setEditing(null)
                }} />
            ) : (
              <tr key={d.wcid} className="border-t border-neutral-800/40 text-neutral-200">
                <td className="px-2 py-1">{d.name} <span className="text-neutral-500">({d.wcid})</span></td>
                <td className="px-2 py-1 font-bold">
                  {perItem(w) !== null
                    ? <>1 in {perItem(w)!.toLocaleString()} kills<span className="block text-[11px] font-normal text-neutral-500">about one every {hoursPerDrop(perItem(w)!)}</span></>
                    : <span className="font-normal text-neutral-500">{odds === undefined ? '...' : 'no drop chance'}</span>}
                </td>
                <td className="px-2 py-1">{pct(w, place.total)}</td>
                <td className="px-2 py-1">{w.toLocaleString()}</td>
                <td className={`px-2 py-1 ${changed ? 'text-blue-300' : 'text-neutral-500'}`}>{changed ? 'Changed here' : 'Built-in'}</td>
                <td className="px-2 py-1 text-right whitespace-nowrap">
                  {canEdit && (
                    <span className="inline-flex gap-1">
                      <button type="button" className={buttonClass} disabled={busy || editing !== null} onClick={() => setEditing(d.wcid)}>Edit</button>
                      {/* share 0 where it has a built-in share; where Zone Control only added it, take the addition back (no stray 0) */}
                      <button type="button" className={buttonClass} disabled={busy}
                        onClick={() => weightSend(d.wcid, changed && !(d.natural ?? []).includes(key) ? 'clear' : '0', [key])}
                        title={changed && !(d.natural ?? []).includes(key) ? `Undo adding it to ${place.label}` : `Share 0 at ${place.label}`}>Remove from {place.label}</button>
                      {changed && <button type="button" className={buttonClass} disabled={busy} onClick={() => weightSend(d.wcid, 'clear', [key])}>Back to built-in</button>}
                      <ConfirmButton label="Stop dropping" confirm="Stop everywhere?" icon={false} disabled={busy} onConfirm={() => weightSend(d.wcid, 'off')} />
                    </span>
                  )}
                </td>
              </tr>
            )
          })}
          <tr className="border-t border-neutral-700 text-neutral-500">
            <td className="px-2 py-1">Total</td>
            <td className="px-2 py-1">{odds && odds > 0 && place.total > 0 ? `any item: 1 in ${odds.toLocaleString()}` : '-'}</td>
            <td className="px-2 py-1">{place.total > 0 ? '100%' : '-'}</td>
            <td className="px-2 py-1">{place.total.toLocaleString()}</td>
            <td colSpan={2} />
          </tr>
        </tbody>
      </table>

      {(outside.length > 0 || unshared.length > 0) && (
        <div className="text-xs text-neutral-400 space-y-1">
          <p className="font-bold text-neutral-300">Could drop in {place.label}, but not in its mix now</p>
          {outside.map(d => (
            <div key={d.wcid} className="flex flex-wrap items-center gap-2">
              <span>{d.name} ({d.wcid})</span>
              <span className="text-neutral-600">{d.off ? 'stopped everywhere' : key in d.set ? 'share set to 0 here' : 'not one of its built-in places'}</span>
              {canEdit && d.off && <button type="button" className={buttonClass} disabled={busy} onClick={() => weightSend(d.wcid, 'on')}><Power className="w-3 h-3" aria-hidden />Allow dropping</button>}
              {canEdit && !d.off && key in d.set && <button type="button" className={buttonClass} disabled={busy} onClick={() => weightSend(d.wcid, 'clear', [key])}>Back to built-in</button>}
            </div>
          ))}
          {unshared.map(w => (
            <div key={w.wcid} className="flex flex-wrap items-center gap-2">
              <span>{w.name} ({w.wcid})</span>
              <span className="text-neutral-600">no drop share anywhere yet (quests, NPCs or admins only)</span>
            </div>
          ))}
          {canEdit && addable.length > 0 && (
            <div className="flex flex-wrap items-center gap-2 pt-1">
              <span>Add</span>
              <select value={addPick?.wcid ?? ''} onChange={e => setAddWcid(e.target.value)} className={`${inputClass} py-1`} aria-label="Item to add">
                {addable.map(d => <option key={d.wcid} value={d.wcid}>{d.name} ({d.wcid})</option>)}
              </select>
              <span>with</span>
              <input value={addPoints} onChange={e => setAddPoints(e.target.value)} inputMode="numeric" aria-label="Share points" className={`${inputClass} w-20`} />
              <span>share points</span>
              <button
                type="button"
                className={buttonClass}
                disabled={busy || !addPick || addValue === undefined || !Number.isInteger(addValue) || addValue < 1 || addValue > data.limits.maxWeight}
                onClick={() => addPick && weightSend(addPick.wcid, String(addValue), [key])}
              >
                <Plus className="w-3 h-3" aria-hidden />Add to {place.label}
              </button>
              {addPick && addValue && addValue > 0 && <span className="text-neutral-500">it would be {pct(addValue, place.total + addValue)} of {place.label} drops</span>}
            </div>
          )}
        </div>
      )}
    </div>
  )
}

const EditPoints = ({ drop, place, isRegion, busy, onCancel, onSave }: {
  drop: DropRow
  place: Place
  isRegion: boolean
  busy: boolean
  onCancel: () => void
  onSave: (points: number, applyTo: ApplyTo) => void
}) => {
  const old = drop.weights[place.key] ?? 0
  const [text, setText] = useState(String(old))
  const [applyTo, setApplyTo] = useState<ApplyTo>('here')
  const value = num(text)
  const valid = value !== undefined && Number.isInteger(value) && value >= 0
  const after = valid ? pct(value, place.total - old + value) : '-'
  // A region bounty or a fixed-tier one can only ever drop in this one place, so there is nothing else to apply it to. A tier
  // bounty can't drop in a region, so "every tier" already is everywhere it can drop.
  const onlyHere = isRegion || drop.fixedTier
  return (
    <tr className="border-t border-neutral-800/40 bg-blue-950/20 text-neutral-200">
      <td className="px-2 py-1">{drop.name} <span className="text-neutral-500">({drop.wcid})</span></td>
      <td className="px-2 py-1" colSpan={4}>
        <span className="inline-flex flex-wrap items-center gap-2">
          <input value={text} onChange={e => setText(e.target.value)} inputMode="numeric" aria-label="Share points" className={`${inputClass} w-20`} autoFocus />
          <span className="text-neutral-400">share points{onlyHere ? ` in ${place.label}` : ', for'}</span>
          {!onlyHere && (
            <select value={applyTo} onChange={e => setApplyTo(e.target.value as ApplyTo)} className={`${inputClass} py-1`} aria-label="Apply to">
              <option value="here">{place.label} only</option>
              <option value="tiers">every tier, T11-T25</option>
            </select>
          )}
          {applyTo === 'here' && valid && <span className="text-neutral-400">chance in {place.label}: {pct(old, place.total)} -&gt; <V>{after}</V>; the others change to match</span>}
          <button type="button" className={buttonClass} disabled={busy || !valid} onClick={() => valid && onSave(value, applyTo)}><Save className="w-3 h-3" aria-hidden />Save</button>
          <button type="button" className={buttonClass} disabled={busy} onClick={onCancel}>Cancel</button>
        </span>
      </td>
    </tr>
  )
}

const MixGrid = ({ data, canEdit, busy, weightSend, onPick }: {
  data: BountyData
  canEdit: boolean
  busy: boolean
  weightSend: (wcid: number, action: string, places?: string[]) => Promise<boolean>
  onPick: (key: string) => void
}) => (
  <div className="overflow-x-auto">
    <p className="text-[11px] text-neutral-500 mb-1">Share points per place. Blue = changed here. "." = it can never drop there. Click a place to edit it.</p>
    <table className="text-xs border-collapse">
      <thead>
        <tr className="text-neutral-500">
          <th className="sticky left-0 bg-neutral-900 px-2 py-1 text-left font-normal">Item</th>
          {data.places.map(p => (
            <th key={p.key} className="px-2 py-1 font-normal">
              <button type="button" className="hover:underline hover:text-neutral-200" onClick={() => onPick(p.key)}>{p.label}</button>
            </th>
          ))}
          <th className="px-2 py-1" />
        </tr>
      </thead>
      <tbody>
        {data.drops.filter(d => d.canDrop).map(d => (
          <tr key={d.wcid} className={`border-t border-neutral-800/40 ${d.off ? 'opacity-50' : ''}`}>
            <td className="sticky left-0 bg-neutral-900 px-2 py-1 text-neutral-200 whitespace-nowrap">
              {d.name} <span className="text-neutral-500">({d.wcid})</span>
              {d.off && <span className="ml-1 text-[10px] font-bold text-red-400">STOPPED</span>}
            </td>
            {data.places.map(p => {
              const w = d.weights[p.key]
              const set = p.key in d.set
              return (
                <td key={p.key} title={w && p.total > 0 && !d.off ? `${pct(w, p.total)} of ${p.label} drops` : ''}
                  className={`px-2 py-1 text-center ${w == null ? 'text-neutral-800' : set ? 'text-blue-300' : w > 0 ? 'text-neutral-200' : 'text-neutral-600'}`}>
                  {w == null ? '.' : w.toLocaleString()}
                </td>
              )
            })}
            <td className="px-2 py-1 whitespace-nowrap">
              {canEdit && (
                <span className="inline-flex gap-1">
                  <button type="button" className={buttonClass} disabled={busy} onClick={() => weightSend(d.wcid, d.off ? 'on' : 'off')}>
                    <Power className="w-3 h-3" aria-hidden />{d.off ? 'Allow dropping' : 'Stop dropping'}
                  </button>
                  {Object.keys(d.set).length > 0 && (
                    <ConfirmButton label="All built-in" confirm="Reset all?" disabled={busy} onConfirm={() => weightSend(d.wcid, 'reset')} />
                  )}
                </span>
              )}
            </td>
          </tr>
        ))}
        <tr className="border-t border-neutral-700 text-neutral-500">
          <td className="sticky left-0 bg-neutral-900 px-2 py-1">Total points</td>
          {data.places.map(p => <td key={p.key} className={`px-2 py-1 text-center ${p.total === 0 ? 'text-amber-500' : ''}`}>{p.total === 0 ? 'empty' : p.total.toLocaleString()}</td>)}
          <td />
        </tr>
      </tbody>
    </table>
  </div>
)

// ---- 5. Mystery rewards: what MYSTERY bounty items pay when done ----

const placeSuffixLabel = (data: BountyData, suffix: string) => data.places.find(p => p.key === suffix)?.label ?? suffix

const MysteryRewards = ({ data, canEdit, busy, send, health }: SectionProps & { health: BountyHealth | null }) => {
  const [newName, setNewName] = useState('')
  const [creating, setCreating] = useState<string | null>(null)
  const names = new Set(data.tables.map(t => t.name))
  // which bounty items name which list (a one-place version serves the same items)
  const usedBy = new Map<string, WeenieHealth[]>()
  for (const w of health?.weenies ?? [])
    if (w.mysteryReward && w.rewardList) usedBy.set(w.rewardList, [...(usedBy.get(w.rewardList) ?? []), w])
  // a missing list is only a problem where an item can't be paid: a T15-only item can pay from 'mystery.t15' with no 'mystery'
  const missing = [...usedBy.keys()].filter(n => !names.has(n) && (usedBy.get(n) ?? []).some(w => w.unpaidPlaces.length > 0)).sort()
  const tables = creating && !names.has(creating)
    ? [...data.tables, { name: creating, totalWeight: 0, entries: [] }]
    : data.tables
  // base lists first, each followed by its one-place versions
  const sorted = [...tables].sort((a, b) => a.name.split('.')[0].localeCompare(b.name.split('.')[0]) || (a.name.includes('.') ? 1 : 0) - (b.name.includes('.') ? 1 : 0) || a.name.localeCompare(b.name))

  const makePlaceVersion = async (base: RewardTable, place: string, copy: boolean) => {
    const name = `${base.name}.${place}`
    // a copy is created whole in one server step: payouts can never come from half a copy (review 2026-11-19)
    if (copy && base.entries.length > 0)
      await send('/api/zonecontrol/bounties/table', { name, op: 'create', rows: base.entries.map(e => ({ wcid: e.wcid, amount: e.amount, weight: e.weight })) })
    else
      setCreating(name)
  }

  return (
    <>
      <p className="text-xs text-neutral-400">
        A mystery bounty item pays <span className="text-neutral-100">one</span> reward, picked from its list when the bounty is finished. The bounty item names its
        list. Changing a list changes it for every mystery bounty already in players' hands. If the list is missing or has nothing payable, players cannot start
        that bounty, and finished ones wait unpaid until it has a reward.
      </p>
      <TableExample tables={data.tables} placeLabel={s => placeSuffixLabel(data, s)} />
      {missing.length > 0 && (
        <div className="rounded-lg border border-red-900/50 bg-red-950/20 px-3 py-2 text-xs text-neutral-300 space-y-1">
          <p><Chip status="problem" /> Bounty items ask for {missing.length === 1 ? 'a list that does not exist' : 'lists that do not exist'} yet:</p>
          {missing.map(n => (
            <div key={n} className="flex flex-wrap items-center gap-2">
              <span className="font-mono text-neutral-100">{n}</span>
              <span className="text-neutral-500">used by {(usedBy.get(n) ?? []).map(w => w.name).join(', ')}</span>
              {canEdit && <button type="button" className={buttonClass} disabled={busy || creating === n} onClick={() => setCreating(n)}><Plus className="w-3 h-3" aria-hidden />Create it</button>}
            </div>
          ))}
        </div>
      )}
      {canEdit && (
        <div className="flex flex-wrap items-center gap-2">
          <input
            value={newName}
            onChange={e => setNewName(e.target.value.toLowerCase())}
            placeholder="New list name, e.g. mystery"
            aria-label="New list name"
            maxLength={41}
            className={`${inputClass} w-56`}
          />
          <button
            type="button"
            className={buttonClass}
            disabled={busy || !newName.trim() || names.has(newName.trim())}
            onClick={() => { setCreating(newName.trim()); setNewName('') }}
          >
            <Plus className="w-3 h-3" aria-hidden />New list
          </button>
          <span className="text-[11px] text-neutral-600">Use the exact name the bounty item asks for (a-z, 0-9, _ and -). It is saved with its first reward.</span>
        </div>
      )}
      {sorted.length === 0 && <p className="text-xs text-neutral-500">No mystery reward lists yet.</p>}
      {sorted.map(t => (
        <RewardTableCard
          key={t.name}
          table={t}
          data={data}
          canEdit={canEdit}
          busy={busy}
          startAdding={t.name === creating && !names.has(t.name)}
          existing={names}
          usedBy={health ? usedBy.get(t.name.split('.')[0]) ?? [] : null}
          onAbandon={() => setCreating(null)}
          onMakePlaceVersion={(place, copy) => makePlaceVersion(t, place, copy)}
          send={async (body) => {
            const ok = await send('/api/zonecontrol/bounties/table', { name: t.name, ...body })
            if (ok && t.name === creating) setCreating(null)
            return ok
          }}
        />
      ))}
    </>
  )
}

const RewardTableCard = ({ table, data, canEdit, busy, startAdding, existing, usedBy, onAbandon, onMakePlaceVersion, send }: {
  table: RewardTable
  data: BountyData
  canEdit: boolean
  busy: boolean
  startAdding: boolean
  existing: Set<string>
  // the bounty items naming this list (null = the server check hasn't answered)
  usedBy: WeenieHealth[] | null
  onAbandon: () => void
  onMakePlaceVersion: (place: string, copy: boolean) => void
  send: (body: object) => Promise<boolean>
}) => {
  const [editing, setEditing] = useState<number | 'new' | null>(startAdding ? 'new' : null)
  const [versionPlace, setVersionPlace] = useState<string | null>(null)
  const [copy, setCopy] = useState(true)
  const limits = data.limits
  const [base, suffix] = table.name.split('.')
  const freePlaces = data.places.filter(p => !existing.has(`${table.name}.${p.key}`))
  return (
    <div className={`border border-neutral-800/60 rounded-xl bg-neutral-950/30 ${suffix ? 'ml-6' : ''}`}>
      <div className="flex flex-wrap items-center justify-between gap-2 px-3 py-2 border-b border-neutral-800/50">
        <div>
          <span className="text-sm font-bold text-white">{table.name}</span>
          <span className="ml-2 text-[11px] text-neutral-500">
            {suffix
              ? `${placeSuffixLabel(data, suffix)}-only version of '${base}': used instead of it for bounties that count in ${placeSuffixLabel(data, suffix)}`
              : `${table.entries.length} reward${table.entries.length === 1 ? '' : 's'}`}
          </span>
          {usedBy && !suffix && (
            <span className={`block text-[11px] ${usedBy.length ? 'text-neutral-400' : 'text-neutral-600'}`}>
              {usedBy.length ? `Used by ${usedBy.map(w => `${w.name} (${w.wcid})`).join(', ')}` : 'No bounty item names this list.'}
            </span>
          )}
        </div>
        {canEdit && (
          <div className="flex flex-wrap gap-1">
            <button type="button" className={buttonClass} disabled={busy || editing !== null || table.entries.length >= limits.maxEntries} onClick={() => setEditing('new')}>
              <Plus className="w-3 h-3" aria-hidden />Reward
            </button>
            {!suffix && table.entries.length > 0 && freePlaces.length > 0 && (
              <button type="button" className={buttonClass} disabled={busy} onClick={() => setVersionPlace(v => (v ? null : freePlaces[0].key))}>
                Make a one-place version
              </button>
            )}
            {/* every stored list, even one with no rewards left (only a list still being created, not saved yet, has none) */}
            {existing.has(table.name) && (
              <ConfirmButton label="Delete list" confirm="Delete it?" disabled={busy} onConfirm={() => send({ op: 'delete' })} />
            )}
          </div>
        )}
      </div>
      {versionPlace && (
        <div className="flex flex-wrap items-center gap-2 px-3 py-2 border-b border-neutral-800/50 bg-blue-950/20 text-xs text-neutral-300">
          <span>A version of '{table.name}' used only for bounties that count in</span>
          <select value={versionPlace} onChange={e => setVersionPlace(e.target.value)} className={`${inputClass} py-1`} aria-label="Place">
            {freePlaces.map(p => <option key={p.key} value={p.key}>{p.label}</option>)}
          </select>
          <label className="flex items-center gap-1 cursor-pointer">
            <input type="checkbox" checked={copy} onChange={e => setCopy(e.target.checked)} />
            start as a copy of these {table.entries.length} rewards
          </label>
          <button type="button" className={buttonClass} disabled={busy} onClick={() => { onMakePlaceVersion(versionPlace, copy); setVersionPlace(null) }}>
            <Plus className="w-3 h-3" aria-hidden />Create '{table.name}.{versionPlace}'
          </button>
          <button type="button" className={buttonClass} onClick={() => setVersionPlace(null)}>Cancel</button>
        </div>
      )}
      <table className="w-full text-xs">
        <thead>
          <tr className="text-left text-neutral-500">
            <th className="px-3 py-1 font-normal">ID</th>
            <th className="px-3 py-1 font-normal">Item</th>
            <th className="px-3 py-1 font-normal">Amount</th>
            <th className="px-3 py-1 font-normal">Chance</th>
            <th className="px-3 py-1 font-normal">Share points</th>
            <th className="px-3 py-1" />
          </tr>
        </thead>
        <tbody>
          {table.entries.map(e => editing === e.id ? (
            <RewardRowForm
              key={e.id}
              start={e}
              limits={limits}
              busy={busy}
              onCancel={() => setEditing(null)}
              onSave={async v => { if (await send({ op: 'set', id: e.id, ...v })) setEditing(null) }}
            />
          ) : (
            <tr key={e.id} className="border-t border-neutral-800/40 text-neutral-200">
              <td className="px-3 py-1 text-neutral-500">{e.id}</td>
              <td className="px-3 py-1">{e.name} <span className="text-neutral-500">({e.wcid})</span></td>
              <td className="px-3 py-1">{e.amount.toLocaleString()}</td>
              <td className="px-3 py-1 font-bold">{pct(e.weight, table.totalWeight)}</td>
              <td className="px-3 py-1">{e.weight.toLocaleString()}</td>
              <td className="px-3 py-1 text-right whitespace-nowrap">
                {canEdit && (
                  <span className="inline-flex gap-1">
                    <button type="button" className={buttonClass} disabled={busy || editing !== null} onClick={() => setEditing(e.id)}>Edit</button>
                    <ConfirmButton label="Remove" confirm="Remove?" disabled={busy} onConfirm={() => send({ op: 'remove', id: e.id })} />
                  </span>
                )}
              </td>
            </tr>
          ))}
          {editing === 'new' && (
            <RewardRowForm
              limits={limits}
              busy={busy}
              onCancel={() => { setEditing(null); if (table.entries.length === 0) onAbandon() }}
              onSave={async v => { if (await send({ op: 'add', ...v })) setEditing(null) }}
            />
          )}
        </tbody>
      </table>
    </div>
  )
}

const RewardRowForm = ({ start, limits, busy, onCancel, onSave }: {
  start?: RewardRow
  limits: Limits
  busy: boolean
  onCancel: () => void
  onSave: (v: { wcid?: number; amount?: number; weight?: number }) => void
}) => {
  const [wcid, setWcid] = useState(start ? String(start.wcid) : '')
  const [amount, setAmount] = useState(start ? String(start.amount) : '1')
  const [weight, setWeight] = useState(start ? String(start.weight) : '1')
  return (
    <tr className="border-t border-neutral-800/40 bg-blue-950/20">
      <td className="px-3 py-1 text-neutral-500">{start?.id ?? 'new'}</td>
      <td className="px-3 py-1">
        <input value={wcid} onChange={e => setWcid(e.target.value)} inputMode="numeric" placeholder="Item WCID" aria-label="Item WCID" className={`${inputClass} w-28`} />
      </td>
      <td className="px-3 py-1">
        <input value={amount} onChange={e => setAmount(e.target.value)} inputMode="numeric" aria-label="Amount" title={`1 - ${limits.maxAmount.toLocaleString()}`} className={`${inputClass} w-20`} />
      </td>
      <td className="px-3 py-1" />
      <td className="px-3 py-1">
        <input value={weight} onChange={e => setWeight(e.target.value)} inputMode="numeric" aria-label="Share points" title={`1 - ${limits.maxWeight.toLocaleString()}`} className={`${inputClass} w-24`} />
      </td>
      <td className="px-3 py-1 text-right whitespace-nowrap">
        <span className="inline-flex gap-1">
          <button
            type="button"
            className={buttonClass}
            disabled={busy || !wcid.trim()}
            onClick={() => onSave({ wcid: num(wcid), amount: num(amount), weight: num(weight) })}
          >
            <Save className="w-3 h-3" aria-hidden />Save
          </button>
          <button type="button" className={buttonClass} disabled={busy} onClick={onCancel}>Cancel</button>
        </span>
      </td>
    </tr>
  )
}

// ================================================================ worked examples (built from the loaded data; see ZoneControlExamples.tsx)

const KillExample = ({ data }: { data: BountyData }) => {
  const cards: { title: string; row: BountyRow }[] = [
    { title: 'the All T11+ zones reward', row: data.zoneWide },
    ...data.regions.map(r => ({ title: r.name, row: r.bounty })),
    ...data.zones.map(z => ({ title: `${z.name} (T${z.variation})`, row: z.bounty })),
  ]
  const card = cards.find(c => c.row.active && c.row.entries.length > 0) ?? cards.find(c => c.row.entries.length > 0)
  const e = card?.row.entries[0]
  const qbRow = cards.flatMap(c => c.row.entries.map(x => ({ c, x }))).find(r => r.x.minQb > 0)
  return (
    <Example id="bounty-kill">
      {card && e ? (
        <p>
          In {card.title}, reward {e.id}: each character counts only their own kills there (they and the monster both inside, and the
          kill must pay XP or luminance). After <V>{e.kills.toLocaleString()}</V> kills they get <V>{e.amount.toLocaleString()} x {e.name}</V>{' '}
          straight into their pack. Then kills do not count for <V>{fmtCooldown(e.minutes)}</V>, and after that the next{' '}
          <V>{e.kills.toLocaleString()}</V> kills start.{!card.row.active && ' This reward is off right now, so nothing counts until it is turned on.'}
        </p>
      ) : (
        <p>
          A reward of 5 x an item every 100 kills with a 30 minute wait pays a character 5 items for their 100th own kill in the area. Their
          kills then do not count for 30 minutes, and after that the next 100 start.
        </p>
      )}
      <p>Each reward row keeps its own count, so two rows on one card pay on their own schedules.</p>
      {qbRow && (
        <p>
          Min QB: reward {qbRow.x.id} in {qbRow.c.title} only counts kills by characters whose account has at least <V>{qbRow.x.minQb.toLocaleString()}</V> QB
          (as /qb shows it). Everyone else's kills there count only toward its other rows.
        </p>
      )}
      <p>To add one: find the zone, press <V>+ Reward</V>, fill in the sentence, and <V>Save and turn on</V>.</p>
    </Example>
  )
}

const ChanceExample = ({ states }: { states: PlaceState[] }) => {
  const s = states.find(x => x.odds && x.odds > 0)
  return (
    <Example id="bounty-chance">
      {s && s.odds ? (
        <p>
          {s.place.label} is <V>1 in {s.odds.toLocaleString()}</V>: on average one kill in {s.odds.toLocaleString()} drops a bounty item, about one every{' '}
          {hoursPerDrop(s.odds)} for a player making {KILLS_PER_HOUR} kills an hour. Change it to {(s.odds * 2).toLocaleString()} and drops halve; change it to{' '}
          {Math.max(1, Math.round(s.odds / 2)).toLocaleString()} and they double.
        </p>
      ) : (
        <p>1 in 2,000 means one kill in 2,000 drops a bounty item on average: about one every 6.7 hours at 300 kills an hour. 4,000 halves the drops; 1,000 doubles them.</p>
      )}
      <p>To start drops in T15: type a number in the T15 row's box (2000 = one in 2,000 kills) and press Save. Then check that T15 has items in its Drop mix.</p>
    </Example>
  )
}

const TableExample = ({ tables, placeLabel }: { tables: RewardTable[]; placeLabel: (suffix: string) => string }) => {
  const t = tables.find(x => x.entries.length > 1 && x.totalWeight > 0) ?? tables.find(x => x.totalWeight > 0)
  const override = tables.find(x => x.name.includes('.'))
  if (!t)
    return (
      <Example id="bounty-tables">
        <p>
          Rewards with 60, 30 and 10 share points add up to 100, so a mystery bounty pays the first 60% of the time, the second 30% and the third 10%.
        </p>
      </Example>
    )
  const first = t.entries[0]
  const base = t.name.split('.')[0]
  return (
    <Example id="bounty-tables">
      <p>A mystery bounty item that names <V>{base}</V> picks once from <V>{t.name}</V> when it is finished:</p>
      <ul className="list-disc pl-5 space-y-0.5">
        {t.entries.slice(0, 5).map(r => (
          <li key={r.id}>
            <V>{r.amount.toLocaleString()} x {r.name}</V>: {r.weight.toLocaleString()} of {t.totalWeight.toLocaleString()} points ={' '}
            <V>{pct(r.weight, t.totalWeight)}</V>
          </li>
        ))}
        {t.entries.length > 5 && <li>... and {t.entries.length - 5} more</li>}
      </ul>
      {t.entries.length > 1 && (
        <p>
          Raising {first.name} from {first.weight.toLocaleString()} to {(first.weight * 2).toLocaleString()} points would make it{' '}
          <V>{pct(first.weight * 2, t.totalWeight + first.weight)}</V>, and every other reward's chance would shrink to match.
        </p>
      )}
      {override ? (
        <p>
          <V>{override.name}</V> is used instead of <V>{override.name.split('.')[0]}</V> for bounties that count in {placeLabel(override.name.split('.')[1])}. If it
          ever has nothing payable, those bounties fall back to {override.name.split('.')[0]}.
        </p>
      ) : (
        <p>
          "Make a one-place version" on <V>{base}</V> could give T15 its own, better list without touching any other tier.
        </p>
      )}
    </Example>
  )
}

const MixExample = ({ data, place }: { data: BountyData; place: Place | undefined }) => {
  const key = place?.key ?? ''
  const live = data.drops.filter(d => !d.off && (d.weights[key] ?? 0) > 0)
  const top = [...live].sort((a, b) => (b.weights[key] ?? 0) - (a.weights[key] ?? 0))[0]
  return (
    <Example id="bounty-weights">
      {place && top ? (() => {
        const w = top.weights[key] ?? 0
        const half = Math.max(1, Math.round(w / 2))
        return (
          <p>
            In {place.label}, <V>{top.name}</V> has <V>{w.toLocaleString()}</V> of the <V>{place.total.toLocaleString()}</V> points, so it is{' '}
            <V>{pct(w, place.total)}</V> of the bounty items that drop there. Edit it to {half.toLocaleString()} and it becomes{' '}
            <V>{pct(half, place.total - w + half)}</V>; the others rise to match. How OFTEN anything drops is the Drop chance tab, not this.
          </p>
        )
      })() : (
        <p>
          An item with 30 points in a place totalling 120 points is a quarter of that place's bounty item drops. How OFTEN anything drops is the Drop chance tab.
        </p>
      )}
      <p>"Remove from {place?.label ?? 'here'}" sets its share there to 0 (an item that was only added there is simply taken back out). "Stop dropping" stops it everywhere but keeps its numbers, so "Allow dropping" puts it back exactly as it was.</p>
    </Example>
  )
}

export default BountiesPanel
