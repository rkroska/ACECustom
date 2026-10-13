import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { ChevronDown, ChevronRight } from 'lucide-react'
import { api } from '../../services/api'
import type { LedgerReviewMark, LedgerSuspectRow } from '../../types/pyrealLedger'
import { LEDGER_API, formatDate, useLedgerFetch } from './ledgerUtils'

// ---- thresholds ---------------------------------------------------------------------------------------------

export const THRESHOLDS = [
  { key: 'minAmount', label: 'Min amount', def: 1000000000, hint: 'Smallest amount (pyreals) in one hour, or from one source, that counts' },
  { key: 'minSurplus', label: 'Min surplus', def: 1000000, hint: 'Smallest currency surplus that counts' },
  { key: 'sharePct', label: 'Share %', def: 25, hint: 'Share of a source that one account must hold' },
  { key: 'activePct', label: 'Active %', def: 80, hint: 'Share of the hours in the period the account must be active' },
  { key: 'peakTimes', label: 'Peak x', def: 10, hint: 'Best hour must be at least this many times the typical account best hour' },
] as const

export type ThresholdKey = (typeof THRESHOLDS)[number]['key']

/** Collapsible threshold editor for the given keys; values live in the URL. */
export function ThresholdsControl({ keys }: { keys: ThresholdKey[] }) {
  const [params, setParams] = useSearchParams()
  const [open, setOpen] = useState(false)
  const shown = THRESHOLDS.filter(t => keys.includes(t.key))
  const [draft, setDraft] = useState<Record<string, string>>(() => {
    const d: Record<string, string> = {}
    for (const t of shown) d[t.key] = params.get(t.key) ?? ''
    return d
  })
  const customised = shown.some(t => params.get(t.key))

  const apply = () => {
    const next = new URLSearchParams(params)
    for (const t of shown) {
      const v = (draft[t.key] ?? '').trim()
      if (v !== '' && Number.isFinite(Number(v)) && Number(v) >= 0) next.set(t.key, String(Number(v)))
      else next.delete(t.key)
    }
    setParams(next)
  }

  const reset = () => {
    const next = new URLSearchParams(params)
    const empty: Record<string, string> = {}
    for (const t of shown) {
      next.delete(t.key)
      empty[t.key] = ''
    }
    setDraft(empty)
    setParams(next)
  }

  return (
    <div className="rounded-xl border border-neutral-800 bg-neutral-900/60">
      <button
        type="button"
        onClick={() => setOpen(o => !o)}
        className="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold text-neutral-300 hover:text-white"
      >
        {open ? <ChevronDown className="w-4 h-4" /> : <ChevronRight className="w-4 h-4" />}
        Thresholds
        {customised && <span className="px-1.5 py-0.5 rounded-md border border-blue-500/30 bg-blue-500/10 text-blue-300 text-[10px]">custom</span>}
      </button>
      {open && (
        <div className="px-3 pb-3">
          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
            {shown.map(t => (
              <label key={t.key} className="block text-[10px] font-bold text-neutral-500 uppercase tracking-wider" title={t.hint}>
                {t.label}
                <input
                  type="number"
                  min={0}
                  value={draft[t.key] ?? ''}
                  placeholder={String(t.def)}
                  onChange={e => setDraft(d => ({ ...d, [t.key]: e.target.value }))}
                  onKeyDown={e => e.key === 'Enter' && apply()}
                  className="mt-1 w-full rounded-lg border border-neutral-800 bg-neutral-950 px-2 py-1 text-xs font-normal normal-case tracking-normal text-neutral-200 tabular-nums"
                />
              </label>
            ))}
          </div>
          <div className="mt-3 flex gap-2">
            <button type="button" onClick={apply} className="px-3 py-1 rounded-md bg-blue-600 text-white text-xs font-semibold hover:bg-blue-500">
              Apply
            </button>
            <button type="button" onClick={reset} className="px-3 py-1 rounded-md border border-neutral-700 text-neutral-300 text-xs font-semibold hover:text-white">
              Reset
            </button>
          </div>
        </div>
      )}
    </div>
  )
}

// ---- suspects data + review marks ---------------------------------------------------------------------------

export function useSuspectRows(days: number) {
  const [params] = useSearchParams()
  const query = new URLSearchParams({ days: String(days), limit: '100' })
  for (const t of THRESHOLDS) {
    const v = params.get(t.key)
    if (v) query.set(t.key, v)
  }
  const { data, loading, error } = useLedgerFetch<LedgerSuspectRow[]>(`/suspects?${query.toString()}`)
  const [overrides, setOverrides] = useState<Record<number, Partial<LedgerSuspectRow>>>({})
  const [failure, setFailure] = useState<string | null>(null)

  useEffect(() => {
    setOverrides({})
    setFailure(null)
  }, [data])

  const rows = (data ?? []).map(r => (overrides[r.accountId] ? { ...r, ...overrides[r.accountId] } : r))

  const mark = async (accountId: number, note: string) => {
    try {
      setFailure(null)
      const res = await api.post<LedgerReviewMark>(`${LEDGER_API}/reviewed`, { accountId, note })
      setOverrides(o => ({
        ...o,
        [accountId]: {
          reviewed: true,
          reviewedNote: res?.note ?? note,
          reviewedBy: res?.by ?? null,
          reviewedUtc: res?.utc ?? new Date().toISOString(),
          newSinceReview: false,
        },
      }))
    } catch (e) {
      setFailure(e instanceof Error ? e.message : 'Could not save the review.')
    }
  }

  const unmark = async (accountId: number) => {
    try {
      setFailure(null)
      await api.delete(`${LEDGER_API}/reviewed/${accountId}`)
      setOverrides(o => ({
        ...o,
        [accountId]: { reviewed: false, reviewedNote: null, reviewedBy: null, reviewedUtc: null, newSinceReview: false },
      }))
    } catch (e) {
      setFailure(e instanceof Error ? e.message : 'Could not clear the review.')
    }
  }

  return { rows, loading, error, failure, mark, unmark }
}

/** "Mark reviewed" with an inline optional note, or the reviewed line with Unmark. */
export function ReviewCell({
  row,
  onMark,
  onUnmark,
  showNewBadge,
}: {
  row: LedgerSuspectRow
  onMark: (accountId: number, note: string) => Promise<void>
  onUnmark: (accountId: number) => Promise<void>
  showNewBadge?: boolean
}) {
  const [editing, setEditing] = useState(false)
  const [note, setNote] = useState('')
  const [busy, setBusy] = useState(false)

  const save = async () => {
    setBusy(true)
    await onMark(row.accountId, note.trim())
    setBusy(false)
    setEditing(false)
    setNote('')
  }
  const clear = async () => {
    setBusy(true)
    await onUnmark(row.accountId)
    setBusy(false)
  }

  const btn = 'text-[11px] font-semibold text-blue-400 hover:text-blue-300 disabled:opacity-50'

  return (
    <div className="text-[11px] space-y-1" onClick={e => e.stopPropagation()}>
      {showNewBadge && row.reviewed && row.newSinceReview && (
        <span className="inline-block px-1.5 py-0.5 rounded-md border text-[10px] font-bold bg-amber-500/15 text-amber-300 border-amber-500/30">
          New flag since review
        </span>
      )}
      {row.reviewed && (
        <div className="text-neutral-500">
          Reviewed by {row.reviewedBy ?? 'unknown'} {formatDate(row.reviewedUtc)}
          {row.reviewedNote ? `: ${row.reviewedNote}` : ''}{' '}
          <button type="button" className={btn} disabled={busy} onClick={() => void clear()}>
            Unmark
          </button>
        </div>
      )}
      {editing ? (
        <div className="flex items-center gap-1.5">
          <input
            autoFocus
            type="text"
            value={note}
            maxLength={200}
            placeholder="Note (optional)"
            onChange={e => setNote(e.target.value)}
            onKeyDown={e => {
              if (e.key === 'Enter') void save()
              if (e.key === 'Escape') setEditing(false)
            }}
            className="w-44 rounded-md border border-neutral-800 bg-neutral-950 px-2 py-1 text-xs text-neutral-200"
          />
          <button type="button" className={btn} disabled={busy} onClick={() => void save()}>
            Save
          </button>
          <button type="button" className="text-[11px] text-neutral-500 hover:text-neutral-300" onClick={() => setEditing(false)}>
            Cancel
          </button>
        </div>
      ) : (
        (!row.reviewed || row.newSinceReview) && (
          <button type="button" className={btn} onClick={() => setEditing(true)}>
            Mark reviewed
          </button>
        )
      )}
    </div>
  )
}

export function ShowReviewedToggle({ count, checked, onChange }: { count: number; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="inline-flex items-center gap-2 text-xs text-neutral-300 cursor-pointer">
      <input type="checkbox" checked={checked} onChange={e => onChange(e.target.checked)} />
      Show reviewed ({count.toLocaleString()})
    </label>
  )
}
