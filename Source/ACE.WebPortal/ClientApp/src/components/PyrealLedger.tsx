import { Navigate, useParams, useSearchParams } from 'react-router-dom'
import { AlertTriangle, BarChart3, Coins, Flag, Package, Store, TrendingUp, UserSearch } from 'lucide-react'
import PageHeader from './common/PageHeader'
import TabButton from './common/TabButton'
import type { LedgerEarnerScope, LedgerOverview, LedgerTab } from '../types/pyrealLedger'
import { DAY_OPTIONS, LEDGER_BASE_PATH, clampDays, formatDate, parseUtc, useLedgerFetch } from './pyreal-ledger/ledgerUtils'
import SuspectsTab from './pyreal-ledger/SuspectsTab'
import EarnersTab from './pyreal-ledger/EarnersTab'
import VendorsTab from './pyreal-ledger/VendorsTab'
import FlagsTab from './pyreal-ledger/FlagsTab'
import ItemsSoldTab from './pyreal-ledger/ItemsSoldTab'
import OverviewTab from './pyreal-ledger/OverviewTab'
import LedgerDetail from './pyreal-ledger/LedgerDetail'
import LedgerSearch from './pyreal-ledger/LedgerSearch'

const TABS: LedgerTab[] = ['suspects', 'earners', 'vendors', 'items', 'flags', 'overview']
/** Currency surplus is unreliable until the ledger has been running this long. */
const SURPLUS_SETTLE_DAYS = 28

/**
 * Pyreal Ledger: find pyreals that appear without a reason.
 * URL state: /audit/pyreals?tab=&days=&by=&flag=&vendor=&item=  and  /audit/pyreals/(character|account)/:id?days=
 */
export default function PyrealLedger() {
  const { kind, id } = useParams<{ kind?: string; id?: string }>()
  const [params, setParams] = useSearchParams()

  const days = clampDays(params.get('days'))
  const tabParam = params.get('tab') as LedgerTab | null
  const tab: LedgerTab = tabParam && TABS.includes(tabParam) ? tabParam : 'suspects'
  const by: LedgerEarnerScope = params.get('by') === 'character' ? 'character' : 'account'
  const flag = params.get('flag') ?? ''
  const vendorRaw = Number(params.get('vendor'))
  const openVendor = Number.isFinite(vendorRaw) && vendorRaw > 0 ? vendorRaw : null
  const itemRaw = Number(params.get('item'))
  const openItem = Number.isFinite(itemRaw) && itemRaw > 0 ? itemRaw : null

  const overview = useLedgerFetch<LedgerOverview>(`/overview?days=${days}`)
  const start = parseUtc(overview.data?.ledgerStartUtc)
  const surplusNoisy = !start || Date.now() - start.getTime() < SURPLUS_SETTLE_DAYS * 24 * 3600_000

  const isDetail = kind !== undefined
  if (isDetail && kind !== 'character' && kind !== 'account') {
    return <Navigate to={LEDGER_BASE_PATH} replace />
  }

  /** Update query params; empty values are removed. */
  const update = (changes: Record<string, string | number | null>) => {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(changes)) {
      if (v === null || v === '') next.delete(k)
      else next.set(k, String(v))
    }
    setParams(next)
  }

  return (
    <div className="h-full min-h-0 p-4 sm:p-6 overflow-y-auto animate-in fade-in duration-500">
      <PageHeader title="Pyreal Ledger" icon={Coins} />

      <LedgerStatus overview={overview.data} loading={overview.loading} />

      <div className="shrink-0 mb-4 flex flex-col sm:flex-row sm:items-center gap-3">
        <LedgerSearch days={days} />
        <div className="flex items-center gap-2 sm:ml-auto">
          <span className="text-[10px] font-bold text-neutral-500 uppercase tracking-wider">Period</span>
          <div className="inline-flex rounded-lg border border-neutral-800 bg-neutral-950 p-0.5">
            {DAY_OPTIONS.map(d => (
              <button
                key={d}
                type="button"
                onClick={() => update({ days: d })}
                className={`px-2.5 py-1 rounded-md text-xs font-semibold transition-colors ${
                  days === d ? 'bg-blue-600 text-white' : 'text-neutral-400 hover:text-neutral-200'
                }`}
              >
                {d === 1 ? '24h' : `${d}d`}
              </button>
            ))}
          </div>
        </div>
      </div>

      {isDetail ? (
        <LedgerDetail kind={kind} id={Number(id)} days={days} surplusNoisy={surplusNoisy} />
      ) : (
        <>
          <div className="shrink-0 flex gap-1 mb-4 border-b border-neutral-800 overflow-x-auto">
            <TabButton active={tab === 'suspects'} onClick={() => update({ tab: null })} icon={<UserSearch />} label="Suspects" />
            <TabButton active={tab === 'earners'} onClick={() => update({ tab: 'earners' })} icon={<TrendingUp />} label="Top earners" />
            <TabButton active={tab === 'vendors'} onClick={() => update({ tab: 'vendors' })} icon={<Store />} label="Vendors" />
            <TabButton active={tab === 'items'} onClick={() => update({ tab: 'items' })} icon={<Package />} label="Items sold" />
            <TabButton active={tab === 'flags'} onClick={() => update({ tab: 'flags' })} icon={<Flag />} label="Flags" />
            <TabButton active={tab === 'overview'} onClick={() => update({ tab: 'overview' })} icon={<BarChart3 />} label="Overview" />
          </div>

          {tab === 'suspects' && <SuspectsTab days={days} surplusNoisy={surplusNoisy} />}
          {tab === 'earners' && <EarnersTab days={days} by={by} onByChange={b => update({ by: b === 'account' ? null : b })} />}
          {tab === 'vendors' && <VendorsTab days={days} openWcid={openVendor} onToggle={w => update({ vendor: w })} />}
          {tab === 'items' && <ItemsSoldTab days={days} openWcid={openItem} onToggle={w => update({ item: w })} />}
          {tab === 'flags' && <FlagsTab days={days} flag={flag} onFlagChange={f => update({ flag: f })} />}
          {tab === 'overview' && <OverviewTab overview={overview} onFlagClick={f => update({ tab: 'flags', flag: f })} />}
        </>
      )}
    </div>
  )
}

function LedgerStatus({ overview, loading }: { overview: LedgerOverview | null; loading: boolean }) {
  if (loading || !overview) return null
  const start = overview.ledgerStartUtc

  if (!start) {
    return (
      <div className="shrink-0 mb-4 flex items-start gap-2 rounded-xl border border-amber-500/30 bg-amber-500/10 px-4 py-3 text-sm text-amber-200">
        <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" />
        <div>
          <div className="font-semibold">The ledger has not recorded anything yet.</div>
          <div className="text-xs text-amber-200/80 mt-0.5">
            {overview.ledgerRunning
              ? 'It is running; data appears here as players earn and spend pyreals.'
              : 'It is not running on this server, so nothing is being recorded. Check the pyreal ledger server setting.'}
          </div>
        </div>
      </div>
    )
  }

  if (!overview.ledgerRunning) {
    return (
      <div className="shrink-0 mb-4 flex items-start gap-2 rounded-xl border border-amber-500/30 bg-amber-500/10 px-4 py-3 text-sm text-amber-200">
        <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" />
        <div>
          <div className="font-semibold">The ledger is not recording right now.</div>
          <div className="text-xs text-amber-200/80 mt-0.5">
            It has data since {formatDate(start)}, but new pyreal movement is not being tracked. Gaps will show as missing activity.
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="shrink-0 -mt-3 mb-4 flex items-center gap-2 text-xs text-neutral-400">
      <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 shadow-[0_0_8px_rgba(34,197,94,0.5)]" />
      Recording since <span className="text-neutral-200 font-semibold">{formatDate(start)}</span>
      <span className="text-neutral-600 hidden sm:inline">- nothing before this date is known to the ledger.</span>
    </div>
  )
}
