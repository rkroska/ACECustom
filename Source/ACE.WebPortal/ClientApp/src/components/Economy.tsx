import { useEffect, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { Camera, Landmark, Sparkles } from 'lucide-react'
import PageHeader from './common/PageHeader'
import type { EconomyOverview, EconomySnapshotResult } from '../types/economy'
import { api } from '../services/api'
import { EmptyState, ErrorState, InfoNote, LoadingState } from './pyreal-ledger/LedgerShared'
import CurrencyCard from './economy/CurrencyCard'
import CurrencyDetailView from './economy/CurrencyDetailView'
import PromptModal from './economy/PromptModal'
import StalledExportersView from './economy/StalledExportersView'
import {
  ECONOMY_API,
  ECONOMY_BASE_PATH,
  ECONOMY_DAY_OPTIONS,
  activeDaysQuery,
  activeLabel,
  clampEconomyDays,
  describeEconomyError,
  formatDate,
  formatDateTime,
  parseActiveDays,
  useEconomyFetch,
} from './economy/economyUtils'

/**
 * Economy dashboard: supply, faucets and sinks per currency.
 * URL state: /audit/economy/:currency?days=&active=  (currency 'stalled' is the stalled-exporters report).
 * active = the "active in the last N days" filter for the per-account figures; absent = the server default.
 */
export default function Economy() {
  const { currency: routeKey } = useParams<{ currency?: string }>()
  const stalledView = routeKey === 'stalled'
  const currency = stalledView ? undefined : routeKey
  const [params, setParams] = useSearchParams()
  const days = clampEconomyDays(params.get('days'))
  const active = parseActiveDays(params.get('active'))
  const activeSuffix = active === null ? '' : `&active=${active}`

  const [reloadToken, setReloadToken] = useState(0)
  const overview = useEconomyFetch<EconomyOverview>(`/overview?days=${days}${activeDaysQuery(active)}&r=${reloadToken}`)
  const [busy, setBusy] = useState(false)
  const [snapshotMsg, setSnapshotMsg] = useState<{ text: string; ok: boolean } | null>(null)
  const [promptOpen, setPromptOpen] = useState(false)

  const setDays = (d: number) => {
    const next = new URLSearchParams(params)
    next.set('days', String(d))
    setParams(next)
  }

  const setActive = (d: number) => {
    const next = new URLSearchParams(params)
    next.set('active', String(d))
    setParams(next)
  }

  // a saved or bookmarked window the server no longer offers (economy_active_days changed): drop it so the page
  // loads with the current default instead of staying on an error. Other errors keep the viewer's choice.
  useEffect(() => {
    if (active !== null && overview.errorCode === 'invalid_days') {
      const next = new URLSearchParams(params)
      next.delete('active')
      setParams(next, { replace: true })
    }
  }, [active, overview.errorCode, params, setParams])

  const takeSnapshot = async () => {
    setBusy(true)
    setSnapshotMsg(null)
    try {
      const res = await api.post<EconomySnapshotResult>(`${ECONOMY_API}/snapshot`)
      setSnapshotMsg({ text: res?.message ?? 'Snapshot requested.', ok: res?.started ?? true })
      setReloadToken(t => t + 1)
    } catch (err) {
      setSnapshotMsg({ text: describeEconomyError(err), ok: false })
    } finally {
      setBusy(false)
    }
  }

  const o = overview.data

  return (
    <div className="h-full min-h-0 p-4 sm:p-6 overflow-y-auto animate-in fade-in duration-500">
      <PageHeader title="Economy" icon={Landmark}>
        <div className="flex flex-wrap items-center justify-end gap-2">
          <div className="inline-flex rounded-lg border border-neutral-800 bg-neutral-950 p-0.5">
            {ECONOMY_DAY_OPTIONS.map(d => (
              <button
                key={d}
                type="button"
                onClick={() => setDays(d)}
                className={`px-2.5 py-1 rounded-md text-xs font-semibold transition-colors ${
                  days === d ? 'bg-blue-600 text-white' : 'text-neutral-400 hover:text-neutral-200'
                }`}
              >
                {d}d
              </button>
            ))}
          </div>
          {o && (o.activeDayOptions ?? []).length > 0 && (
            <label className="inline-flex items-center gap-1.5 text-xs text-neutral-400" title="Per-account figures only count accounts with a character that logged into the game within this many days of each snapshot.">
              Accounts
              <select
                value={o.activeDays}
                onChange={e => setActive(Number(e.target.value))}
                className="rounded-lg border border-neutral-800 bg-neutral-950 px-2 py-1 text-xs font-semibold text-neutral-200"
              >
                {o.activeDayOptions.map(d => (
                  <option key={d} value={d}>
                    {d === 0 ? 'All accounts' : `Active in last ${d} days`}
                  </option>
                ))}
              </select>
            </label>
          )}
          <button
            type="button"
            onClick={takeSnapshot}
            disabled={busy}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-neutral-700 hover:bg-neutral-800 disabled:opacity-40 text-neutral-200 text-xs font-semibold"
          >
            <Camera className="w-3.5 h-3.5" /> {busy ? 'Working...' : 'Take snapshot now'}
          </button>
          <button
            type="button"
            onClick={() => setPromptOpen(true)}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-xs font-semibold"
          >
            <Sparkles className="w-3.5 h-3.5" /> Claude prompt
          </button>
        </div>
      </PageHeader>

      <div className="mb-4 inline-flex rounded-lg border border-neutral-800 bg-neutral-950 p-0.5">
        {[
          { to: `${ECONOMY_BASE_PATH}?days=${days}${activeSuffix}`, label: 'Currencies', active: !stalledView },
          { to: `${ECONOMY_BASE_PATH}/stalled?days=${days}${activeSuffix}`, label: 'Stalled exporters', active: stalledView },
        ].map(t => (
          <Link
            key={t.label}
            to={t.to}
            className={`px-3 py-1 rounded-md text-xs font-semibold transition-colors ${
              t.active ? 'bg-blue-600 text-white' : 'text-neutral-400 hover:text-neutral-200'
            }`}
          >
            {t.label}
          </Link>
        ))}
      </div>

      {snapshotMsg && (
        <div className="mb-3">
          <InfoNote tone={snapshotMsg.ok ? 'info' : 'warn'}>{snapshotMsg.text}</InfoNote>
        </div>
      )}

      {o && (
        <div className="mb-4 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-neutral-400">
          <span className="inline-flex items-center gap-1.5">
            <span className={`w-1.5 h-1.5 rounded-full ${o.flowTracking ? 'bg-emerald-500' : 'bg-neutral-600'}`} />
            Flow tracking {o.flowTracking ? 'on' : 'off'}
            {o.flowTrackingSinceUtc && <> since <span className="text-neutral-200 font-semibold">{formatDate(o.flowTrackingSinceUtc)}</span></>}
          </span>
          <span>
            Last snapshot: <span className="text-neutral-200 font-semibold">{o.lastSnapshotUtc ? formatDateTime(o.lastSnapshotUtc) : 'none yet'}</span>
          </span>
          <span>
            Target: <span className="text-neutral-200 font-semibold">{o.targetMinWeeklyPct}% to {o.targetMaxWeeklyPct}%</span> per week
          </span>
          <span title="The per-account figures follow this filter. The weekly trend, changes and Unexplained always use the full supply.">
            Accounts: <span className="text-neutral-200 font-semibold">{activeLabel(o.activeDays)}</span>
            {o.accountsSince ? <> (per-account data since {o.accountsSince})</> : <> (no per-account data until the next snapshot)</>}
          </span>
        </div>
      )}

      {stalledView ? (
        <StalledExportersView days={days} currencies={o?.currencies ?? []} reloadToken={reloadToken} />
      ) : currency ? (
        <CurrencyDetailView currencyKey={currency} days={days} active={active} reloadToken={reloadToken} />
      ) : overview.loading ? (
        <LoadingState />
      ) : overview.error ? (
        <ErrorState message={overview.error} />
      ) : !o || (o.currencies ?? []).length === 0 ? (
        <EmptyState message="No currencies reported yet." />
      ) : (
        <div className="grid gap-4 grid-cols-1 md:grid-cols-2 xl:grid-cols-3">
          {o.currencies.map(c => (
            <CurrencyCard key={c.key} currency={c} days={days} active={active} />
          ))}
        </div>
      )}

      {promptOpen && <PromptModal currencyKey={currency ?? null} days={days} active={active} onClose={() => setPromptOpen(false)} />}
    </div>
  )
}
