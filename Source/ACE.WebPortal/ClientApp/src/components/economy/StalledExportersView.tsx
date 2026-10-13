import { Fragment, useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ArrowLeft, ChevronDown, ChevronRight, RotateCcw } from 'lucide-react'
import type { CurrencySummary, StalledCurrency, StalledReport, StalledRow } from '../../types/economy'
import {
  EmptyState,
  ErrorState,
  InfoNote,
  LINK_CLASS,
  LoadingState,
  Panel,
  ROW_CLASS,
  ROW_CLICK_CLASS,
  TABLE_CLASS,
  TBODY_CLASS,
  TD_CLASS,
  TD_NUM_CLASS,
  THEAD_CLASS,
  TH_CLASS,
  TH_NUM_CLASS,
  TableScroll,
} from '../pyreal-ledger/LedgerShared'
import { Amount } from './EconomyShared'
import { ECONOMY_BASE_PATH, formatDate, formatDateTime, formatPct, parseUtc, useEconomyFetch } from './economyUtils'

const DEFAULT_STALL_DAYS = 30
const DEFAULT_MIN_ENL = 10

function intParam(raw: string | null, fallback: number): number {
  if (raw === null || raw.trim() === '') return fallback
  const n = Math.floor(Number(raw))
  return Number.isFinite(n) && n >= 0 ? n : fallback
}

/** Number input that commits on blur or Enter, not on every keystroke. */
function NumberField({
  label,
  value,
  onCommit,
  disabled,
  hint,
}: {
  label: string
  value: number
  onCommit: (n: number) => void
  disabled?: boolean
  hint?: string
}) {
  const [draft, setDraft] = useState(String(value))
  useEffect(() => setDraft(String(value)), [value])

  const commit = () => {
    const n = Math.floor(Number(draft))
    if (draft.trim() === '' || !Number.isFinite(n) || n < 0) {
      setDraft(String(value))
      return
    }
    if (n !== value) onCommit(n)
    else setDraft(String(value))
  }

  return (
    <label className="flex flex-col gap-1 text-[10px] uppercase tracking-wider text-neutral-500 font-bold" title={hint}>
      {label}
      <input
        type="number"
        min={0}
        value={draft}
        disabled={disabled}
        onChange={e => setDraft(e.target.value)}
        onBlur={commit}
        onKeyDown={e => {
          if (e.key === 'Enter') commit()
        }}
        className="w-28 px-2 py-1.5 rounded-lg border border-neutral-700 bg-neutral-950 text-xs text-neutral-200 font-normal normal-case tracking-normal tabular-nums disabled:opacity-40"
      />
    </label>
  )
}

function StalledCell({ row }: { row: StalledRow }) {
  return (
    <span
      className="tabular-nums"
      title={row.stallIsMinimum ? 'No progress seen since tracking began, so the real figure may be longer.' : undefined}
    >
      {row.stallIsMinimum && <span className="text-neutral-500">at least </span>}
      {row.stalledDays.toLocaleString()} days
    </span>
  )
}

function lastLogin(iso: string | null): string {
  const d = parseUtc(iso)
  if (!d) return '-'
  const days = Math.floor((Date.now() - d.getTime()) / 86_400_000)
  if (days < 0) return formatDateTime(iso)
  if (days === 0) return 'today'
  if (days === 1) return '1 day ago'
  return `${days.toLocaleString()} days ago`
}

function CurrencyFigures({ c }: { c: StalledCurrency | undefined }) {
  if (!c) {
    return (
      <>
        <td className={TD_NUM_CLASS}><Amount value={null} /></td>
        <td className={TD_NUM_CLASS}><Amount value={null} /></td>
        <td className={TD_NUM_CLASS}><Amount value={null} /></td>
      </>
    )
  }
  return (
    <>
      <td className={TD_NUM_CLASS}><Amount value={c.earned} /></td>
      <td className={TD_NUM_CLASS}><Amount value={c.spentOnSelf} /></td>
      <td className={`${TD_NUM_CLASS} text-amber-300 font-semibold`}><Amount value={c.exported} /></td>
    </>
  )
}

function ExpandedRow({ row, colSpan }: { row: StalledRow; colSpan: number }) {
  const chars = row.characters ?? []
  const currencies = row.currencies ?? []
  const recipients = row.recipients ?? []
  return (
    <tr className="bg-neutral-950/60">
      <td colSpan={colSpan} className="px-4 py-3">
        <div className="grid gap-4 lg:grid-cols-3">
          <div>
            <h3 className="text-[10px] uppercase tracking-wider text-neutral-500 font-bold mb-1.5">Characters</h3>
            {chars.length === 0 ? (
              <p className="text-xs text-neutral-600">None.</p>
            ) : (
              <table className={TABLE_CLASS}>
                <thead className={THEAD_CLASS}>
                  <tr>
                    <th className={TH_CLASS}>Name</th>
                    <th className={TH_NUM_CLASS}>Enl</th>
                    <th className={TH_NUM_CLASS}>Augs</th>
                  </tr>
                </thead>
                <tbody className={TBODY_CLASS}>
                  {chars.map(c => (
                    <tr key={c.charId} className={ROW_CLASS}>
                      <td className={TD_CLASS}>{c.charName}</td>
                      <td className={TD_NUM_CLASS}>{c.enlightenment.toLocaleString()}</td>
                      <td className={TD_NUM_CLASS}>{c.augs.toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            <p className="text-[11px] text-neutral-500 mt-2">
              Last progress: {row.lastProgressUtc ? formatDateTime(row.lastProgressUtc) : 'none seen'}
            </p>
          </div>

          <div className="lg:col-span-2">
            <h3 className="text-[10px] uppercase tracking-wider text-neutral-500 font-bold mb-1.5">Currencies</h3>
            {currencies.length === 0 ? (
              <p className="text-xs text-neutral-600">No currency activity.</p>
            ) : (
              <table className={TABLE_CLASS}>
                <thead className={THEAD_CLASS}>
                  <tr>
                    <th className={TH_CLASS}>Currency</th>
                    <th className={TH_NUM_CLASS}>Earned</th>
                    <th className={TH_NUM_CLASS}>Spent on self</th>
                    <th className={TH_NUM_CLASS}>Sent to others</th>
                    <th className={TH_NUM_CLASS} title="Sent to other accounts as a share of earned">Sent / earned</th>
                  </tr>
                </thead>
                <tbody className={TBODY_CLASS}>
                  {currencies.map(c => (
                    <tr key={c.key} className={ROW_CLASS}>
                      <td className={TD_CLASS}>{c.name}</td>
                      <td className={TD_NUM_CLASS}><Amount value={c.earned} /></td>
                      <td className={TD_NUM_CLASS}><Amount value={c.spentOnSelf} /></td>
                      <td className={TD_NUM_CLASS}><Amount value={c.exported} /></td>
                      <td className={TD_NUM_CLASS}>
                        {c.earned > 0 ? formatPct((c.exported / c.earned) * 100) : <span className="text-neutral-600">-</span>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>

          <div className="lg:col-span-3">
            <h3 className="text-[10px] uppercase tracking-wider text-neutral-500 font-bold mb-1.5">Recipients</h3>
            {recipients.length === 0 ? (
              <p className="text-xs text-neutral-600">No transfers to other accounts recorded.</p>
            ) : (
              <TableScroll>
                <table className={TABLE_CLASS}>
                  <thead className={THEAD_CLASS}>
                    <tr>
                      <th className={TH_CLASS}>Account</th>
                      <th className={TH_CLASS}>Character</th>
                      <th className={TH_CLASS}>Currency</th>
                      <th className={TH_NUM_CLASS}>Amount</th>
                    </tr>
                  </thead>
                  <tbody className={TBODY_CLASS}>
                    {recipients.map((r, i) => (
                      <tr key={`${r.accountId}|${r.charName}|${r.currency}|${i}`} className={ROW_CLASS}>
                        <td className={TD_CLASS}>
                          {r.accountName} <span className="text-neutral-600">#{r.accountId}</span>
                        </td>
                        <td className={TD_CLASS}>{r.charName || '-'}</td>
                        <td className={TD_CLASS}>{r.currencyName || r.currency}</td>
                        <td className={TD_NUM_CLASS}><Amount value={r.amount} /></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </TableScroll>
            )}
          </div>
        </div>
      </td>
    </tr>
  )
}

export default function StalledExportersView({
  days,
  currencies,
  reloadToken,
}: {
  days: number
  currencies: CurrencySummary[]
  reloadToken: number
}) {
  const [params, setParams] = useSearchParams()
  const stallDays = intParam(params.get('stallDays'), DEFAULT_STALL_DAYS)
  const minEnl = intParam(params.get('minEnl'), DEFAULT_MIN_ENL)
  const maxEnl = intParam(params.get('maxEnl'), 0)
  const currency = params.get('currency') || 'all'
  const minExport = intParam(params.get('minExport'), 0)
  const query = params.get('q') ?? ''
  const oneCurrency = currency !== 'all'

  const [expanded, setExpanded] = useState<Set<number>>(new Set())

  const setParam = (changes: Record<string, string | null>) => {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(changes)) {
      if (v === null || v === '') next.delete(k)
      else next.set(k, v)
    }
    setParams(next, { replace: true })
  }

  const reset = () => {
    const next = new URLSearchParams()
    next.set('days', String(days))
    setParams(next, { replace: true })
  }

  const path =
    `/stalled?days=${days}&stallDays=${stallDays}&minEnl=${minEnl}&maxEnl=${maxEnl}` +
    `&currency=${encodeURIComponent(currency)}&minExport=${oneCurrency ? minExport : 0}&r=${reloadToken}`
  const { data, loading, error } = useEconomyFetch<StalledReport>(path)

  const rows = useMemo(() => {
    const all = data?.rows ?? []
    const q = query.trim().toLowerCase()
    if (!q) return all
    return all.filter(
      r =>
        r.accountName.toLowerCase().includes(q) ||
        (r.characters ?? []).some(c => c.charName.toLowerCase().includes(q)),
    )
  }, [data, query])

  const toggle = (id: number) =>
    setExpanded(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  const selectedName = currencies.find(c => c.key === currency)?.name ?? currency
  const colCount = oneCurrency ? 9 : 7

  const back = (
    <Link to={`${ECONOMY_BASE_PATH}?days=${days}`} className={`inline-flex items-center gap-1.5 text-xs mb-4 ${LINK_CLASS}`}>
      <ArrowLeft className="w-3.5 h-3.5" /> Currencies
    </Link>
  )

  const filters = (
    <div className="flex flex-wrap items-end gap-3 mb-4 rounded-2xl border border-neutral-800 bg-neutral-900/40 p-3">
      <NumberField
        label="Stalled for at least (days)"
        value={stallDays}
        onCommit={n => setParam({ stallDays: String(n) })}
        hint="No enlightenment or luminance aug gained for this many days."
      />
      <NumberField label="Enlightenment at least" value={minEnl} onCommit={n => setParam({ minEnl: String(n) })} />
      <NumberField label="Enlightenment at most" value={maxEnl} onCommit={n => setParam({ maxEnl: String(n) })} hint="0 = no limit" />
      <label className="flex flex-col gap-1 text-[10px] uppercase tracking-wider text-neutral-500 font-bold">
        Currency
        <select
          value={currency}
          onChange={e => setParam({ currency: e.target.value === 'all' ? null : e.target.value, minExport: null })}
          className="px-2 py-1.5 rounded-lg border border-neutral-700 bg-neutral-950 text-xs text-neutral-200 font-normal normal-case tracking-normal"
        >
          <option value="all">All currencies</option>
          {currencies.map(c => (
            <option key={c.key} value={c.key}>{c.name}</option>
          ))}
          {oneCurrency && !currencies.some(c => c.key === currency) && <option value={currency}>{currency}</option>}
        </select>
      </label>
      <NumberField
        label="Minimum sent out"
        value={oneCurrency ? minExport : 0}
        onCommit={n => setParam({ minExport: String(n) })}
        disabled={!oneCurrency}
        hint={oneCurrency ? `Minimum ${selectedName} sent to other accounts` : 'Select a single currency to use this filter'}
      />
      <label className="flex flex-col gap-1 text-[10px] uppercase tracking-wider text-neutral-500 font-bold">
        Find account or character
        <input
          type="text"
          value={query}
          onChange={e => setParam({ q: e.target.value })}
          placeholder="Name..."
          className="w-48 px-2 py-1.5 rounded-lg border border-neutral-700 bg-neutral-950 text-xs text-neutral-200 font-normal normal-case tracking-normal"
        />
      </label>
      <button
        type="button"
        onClick={reset}
        className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-neutral-700 hover:bg-neutral-800 text-neutral-200 text-xs font-semibold"
      >
        <RotateCcw className="w-3.5 h-3.5" /> Reset filters
      </button>
      {!oneCurrency && (
        <p className="text-[11px] text-neutral-500 basis-full">Minimum sent out applies to a single currency; pick one to use it.</p>
      )}
    </div>
  )

  let body
  if (loading) body = <LoadingState />
  else if (error || !data) body = <ErrorState message={error ?? 'No data.'} />
  else if (rows.length === 0) {
    body = (
      <div className="p-8 text-center text-sm text-neutral-500 space-y-2">
        <EmptyState message="No accounts match these filters." />
        <p className="text-xs">
          Progress tracking:{' '}
          <span className="text-neutral-300 font-semibold">
            {data.progressSinceUtc ? `since ${formatDate(data.progressSinceUtc)}` : 'has not started (it starts with the first supply snapshot)'}
          </span>
          {' | '}Flow tracking:{' '}
          <span className="text-neutral-300 font-semibold">
            {data.flowTrackingSinceUtc ? `since ${formatDate(data.flowTrackingSinceUtc)}` : 'not started'}
          </span>
        </p>
        <p className="text-xs">This report needs time to fill. Accounts cannot show as stalled until enough history exists.</p>
      </div>
    )
  } else {
    body = (
      <TableScroll>
        <table className={TABLE_CLASS}>
          <thead className={THEAD_CLASS}>
            <tr className="border-b border-neutral-800">
              <th className={TH_CLASS} />
              <th className={TH_CLASS}>Account</th>
              <th className={TH_NUM_CLASS}>Enlightenment</th>
              <th className={TH_NUM_CLASS}>Augs</th>
              <th className={TH_CLASS}>Stalled</th>
              <th className={TH_CLASS}>Last login</th>
              {oneCurrency ? (
                <>
                  <th className={TH_NUM_CLASS}>{selectedName} earned</th>
                  <th className={TH_NUM_CLASS}>Spent on self</th>
                  <th className={TH_NUM_CLASS}>Sent to other accounts</th>
                </>
              ) : (
                <th className={TH_CLASS}>Sent to other accounts</th>
              )}
            </tr>
          </thead>
          <tbody className={TBODY_CLASS}>
            {rows.map(r => {
              const open = expanded.has(r.accountId)
              const sent = (r.currencies ?? []).filter(c => c.exported > 0)
              return (
                <Fragment key={r.accountId}>
                  <tr className={ROW_CLICK_CLASS} onClick={() => toggle(r.accountId)}>
                    <td className={TD_CLASS}>
                      {open ? (
                        <ChevronDown className="w-3.5 h-3.5 text-neutral-500" />
                      ) : (
                        <ChevronRight className="w-3.5 h-3.5 text-neutral-500" />
                      )}
                    </td>
                    <td className={TD_CLASS}>
                      <span className="font-semibold text-neutral-100">{r.accountName}</span>{' '}
                      <span className="text-neutral-600">#{r.accountId}</span>
                    </td>
                    <td className={TD_NUM_CLASS}>{r.maxEnlightenment.toLocaleString()}</td>
                    <td className={TD_NUM_CLASS}>{r.augs.toLocaleString()}</td>
                    <td className={`${TD_CLASS} whitespace-nowrap`}><StalledCell row={r} /></td>
                    <td className={`${TD_CLASS} whitespace-nowrap`} title={formatDateTime(r.lastLoginUtc)}>{lastLogin(r.lastLoginUtc)}</td>
                    {oneCurrency ? (
                      <CurrencyFigures c={(r.currencies ?? []).find(c => c.key === currency)} />
                    ) : (
                      <td className={TD_CLASS}>
                        {sent.length === 0 ? (
                          <span className="text-neutral-600">-</span>
                        ) : (
                          <ul className="space-y-0.5">
                            {sent.map(c => (
                              <li key={c.key} className="whitespace-nowrap">
                                <span className="text-neutral-400">{c.name}:</span> <Amount value={c.exported} /> sent,{' '}
                                <Amount value={c.earned} /> earned
                              </li>
                            ))}
                          </ul>
                        )}
                      </td>
                    )}
                  </tr>
                  {open && <ExpandedRow row={r} colSpan={colCount} />}
                </Fragment>
              )
            })}
          </tbody>
        </table>
      </TableScroll>
    )
  }

  const notes = data?.notes ?? []

  return (
    <div>
      {back}
      {filters}
      <Panel
        title="Stalled exporters"
        help="Established accounts that have stopped gaining enlightenment or luminance augs but keep sending currency to other accounts - the shape of a farmer or feeder account. Click a row for details."
        actions={data && <span className="text-xs text-neutral-500">{rows.length.toLocaleString()} accounts</span>}
      >
        {body}
      </Panel>
      {notes.length > 0 && (
        <div className="mt-4 space-y-2">
          {notes.map((n, i) => (
            <InfoNote key={i}>{n}</InfoNote>
          ))}
        </div>
      )}
    </div>
  )
}
