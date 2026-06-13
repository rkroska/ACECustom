import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { ClipboardList, Search, Filter, ArrowRightLeft, LogIn, BarChart3 } from 'lucide-react'
import { api } from '../services/api'
import PageHeader from './common/PageHeader'
import Pagination from './common/Pagination'
import {
  AuditFilters,
  AuditPagedResult,
  AuditTab,
  CharTrackerLoginRow,
  TransferLogRow,
  TransferSummaryRow,
} from '../types/audit'

const PAGE_SIZE = 50
const DEFAULT_DAYS = 30

/** Must match AuditLogController limits. */
const MAX_DAYS_BY_TAB: Record<AuditTab, number> = {
  transfers: 365,
  logins: 90,
  summaries: 365,
}

const DAY_OPTIONS = [7, 14, 30, 60, 90, 180, 365] as const

const EMPTY_FILTERS: AuditFilters = {
  ip: '',
  account: '',
  character: '',
  transferType: '',
  itemContains: '',
  days: DEFAULT_DAYS,
}

function formatUtc(iso: string | null | undefined): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  return d.toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' })
}

function formatDuration(seconds: number): string {
  if (seconds <= 0) return '—'
  if (seconds < 60) return `${seconds}s`
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m ${seconds % 60}s`
  const h = Math.floor(seconds / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  return `${h}h ${m}m`
}

function buildQuery(filters: AuditFilters, page: number): string {
  const params = new URLSearchParams()
  params.set('page', String(page))
  params.set('pageSize', String(PAGE_SIZE))
  params.set('days', String(filters.days))
  if (filters.ip.trim()) params.set('ip', filters.ip.trim())
  if (filters.account.trim()) params.set('account', filters.account.trim())
  if (filters.character.trim()) params.set('character', filters.character.trim())
  if (filters.transferType.trim()) params.set('transferType', filters.transferType.trim())
  if (filters.itemContains.trim()) params.set('itemContains', filters.itemContains.trim())
  return params.toString()
}

type FilterChipField = 'ip' | 'account' | 'character'

export default function AuditLog() {
  const [activeTab, setActiveTab] = useState<AuditTab>('transfers')
  const [draftFilters, setDraftFilters] = useState<AuditFilters>({ ...EMPTY_FILTERS })
  const [appliedFilters, setAppliedFilters] = useState<AuditFilters>({ ...EMPTY_FILTERS })
  const [page, setPage] = useState(1)

  const [transfers, setTransfers] = useState<AuditPagedResult<TransferLogRow> | null>(null)
  const [logins, setLogins] = useState<AuditPagedResult<CharTrackerLoginRow> | null>(null)
  const [summaries, setSummaries] = useState<AuditPagedResult<TransferSummaryRow> | null>(null)

  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [hasSearched, setHasSearched] = useState(false)

  const maxDays = MAX_DAYS_BY_TAB[activeTab]
  const dayOptions = useMemo(
    () => DAY_OPTIONS.filter(d => d <= maxDays),
    [maxDays]
  )

  useEffect(() => {
    const clamp = (f: AuditFilters) => (f.days > maxDays ? { ...f, days: maxDays } : f)
    setDraftFilters(clamp)
    setAppliedFilters(clamp)
  }, [activeTab, maxDays])

  const activeResult = useMemo(() => {
    switch (activeTab) {
      case 'transfers': return transfers
      case 'logins': return logins
      case 'summaries': return summaries
    }
  }, [activeTab, transfers, logins, summaries])

  const fetchTab = useCallback(async (tab: AuditTab, filters: AuditFilters, pageNum: number, signal?: AbortSignal) => {
    const qs = buildQuery(filters, pageNum)
    const path =
      tab === 'transfers' ? `/api/audit/transfers?${qs}` :
      tab === 'logins' ? `/api/audit/logins?${qs}` :
      `/api/audit/summaries?${qs}`

    return api.get<AuditPagedResult<TransferLogRow | CharTrackerLoginRow | TransferSummaryRow>>(path, { signal })
  }, [])

  const runSearch = useCallback(async (tab: AuditTab, filters: AuditFilters, pageNum: number, signal?: AbortSignal) => {
    setIsLoading(true)
    setError(null)

    try {
      const data = await fetchTab(tab, filters, pageNum, signal)
      if (!data) return

      const normalized: AuditPagedResult<unknown> = {
        items: data.items ?? [],
        totalCount: data.totalCount ?? 0,
        page: data.page ?? pageNum,
        pageSize: data.pageSize ?? PAGE_SIZE,
        totalPages: data.totalPages ?? 0,
      }

      if (tab === 'transfers') setTransfers(normalized as AuditPagedResult<TransferLogRow>)
      else if (tab === 'logins') setLogins(normalized as AuditPagedResult<CharTrackerLoginRow>)
      else setSummaries(normalized as AuditPagedResult<TransferSummaryRow>)
    } catch (err) {
      if (err instanceof Error && err.name === 'AbortError') return
      setError(err instanceof Error ? err.message : 'Failed to load audit data')
    } finally {
      setIsLoading(false)
    }
  }, [fetchTab])

  const handleSearch = () => {
    setAppliedFilters({ ...draftFilters })
    setPage(1)
    setHasSearched(true)
  }

  const applyChip = (field: FilterChipField, value: string) => {
    const next = { ...appliedFilters, [field]: value }
    setDraftFilters(next)
    setAppliedFilters(next)
    setPage(1)
    setHasSearched(true)
  }

  useEffect(() => {
    if (!hasSearched) return
    const controller = new AbortController()
    runSearch(activeTab, appliedFilters, page, controller.signal)
    return () => controller.abort()
  }, [hasSearched, activeTab, appliedFilters, page, runSearch])

  const onTabChange = (tab: AuditTab) => {
    setActiveTab(tab)
    setPage(1)
  }

  const tabCounts = {
    transfers: transfers?.totalCount,
    logins: logins?.totalCount,
    summaries: summaries?.totalCount,
  }

  const totalPages = activeResult?.totalPages ?? 0

  return (
    <div className="flex flex-col h-full min-h-0 p-6 animate-in fade-in duration-500">
      <PageHeader title="Audit Log" icon={ClipboardList} />

      <div className="shrink-0 mb-4 p-4 rounded-2xl bg-neutral-900/80 border border-neutral-800 space-y-4">
        <div className="flex items-center gap-2 text-[10px] font-black text-neutral-500 uppercase tracking-widest">
          <Filter className="w-3.5 h-3.5" />
          Filters
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
          <FilterInput label="IP address" value={draftFilters.ip} onChange={v => setDraftFilters(f => ({ ...f, ip: v }))} placeholder="Exact IP" />
          <FilterInput label="Account" value={draftFilters.account} onChange={v => setDraftFilters(f => ({ ...f, account: v }))} placeholder="Partial match" />
          <FilterInput label="Character" value={draftFilters.character} onChange={v => setDraftFilters(f => ({ ...f, character: v }))} placeholder="Partial match" />
          <FilterInput
            label="Transfer type"
            value={draftFilters.transferType}
            onChange={v => setDraftFilters(f => ({ ...f, transferType: v }))}
            placeholder="e.g. Bank Transfer"
            disabled={activeTab === 'logins'}
          />
          <FilterInput
            label="Item contains"
            value={draftFilters.itemContains}
            onChange={v => setDraftFilters(f => ({ ...f, itemContains: v }))}
            placeholder="e.g. Pyreal"
            disabled={activeTab !== 'transfers'}
          />
          <div>
            <label className="block text-[10px] font-bold text-neutral-500 uppercase tracking-wider mb-1.5">Days</label>
            <select
              value={Math.min(draftFilters.days, maxDays)}
              onChange={e => setDraftFilters(f => ({ ...f, days: Number(e.target.value) }))}
              className="w-full px-3 py-2 rounded-lg bg-neutral-950 border border-neutral-800 text-sm text-white focus:outline-none focus:border-blue-500/50"
            >
              {dayOptions.map(d => (
                <option key={d} value={d}>{d} days</option>
              ))}
            </select>
            <p className="text-[10px] text-neutral-500 mt-1">Max {maxDays} days for {activeTab}</p>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <button
            type="button"
            onClick={handleSearch}
            disabled={isLoading}
            className="inline-flex items-center gap-2 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-sm font-semibold disabled:opacity-50 transition-colors"
          >
            <Search className="w-4 h-4" />
            Search
          </button>
          <button
            type="button"
            onClick={() => {
              setDraftFilters({ ...EMPTY_FILTERS })
              setAppliedFilters({ ...EMPTY_FILTERS })
              setTransfers(null)
              setLogins(null)
              setSummaries(null)
              setHasSearched(false)
              setPage(1)
              setError(null)
            }}
            className="px-4 py-2 rounded-lg bg-neutral-800 hover:bg-neutral-700 text-neutral-300 text-sm font-medium transition-colors"
          >
            Clear
          </button>
          {activeTab === 'summaries' && (
            <span className="text-xs text-neutral-500">IP filter applies to Transfers and Logins only.</span>
          )}
        </div>
      </div>

      <div className="shrink-0 flex gap-1 mb-4 border-b border-neutral-800">
        <TabButton
          active={activeTab === 'transfers'}
          onClick={() => onTabChange('transfers')}
          icon={<ArrowRightLeft className="w-4 h-4" />}
          label="Transfers"
          count={tabCounts.transfers}
        />
        <TabButton
          active={activeTab === 'logins'}
          onClick={() => onTabChange('logins')}
          icon={<LogIn className="w-4 h-4" />}
          label="Logins"
          count={tabCounts.logins}
        />
        <TabButton
          active={activeTab === 'summaries'}
          onClick={() => onTabChange('summaries')}
          icon={<BarChart3 className="w-4 h-4" />}
          label="Summaries"
          count={tabCounts.summaries}
        />
      </div>

      <div className="flex-1 min-h-0 flex flex-col rounded-2xl border border-neutral-800 bg-neutral-900/40 overflow-hidden">
        {!hasSearched ? (
          <div className="flex-1 flex items-center justify-center p-12 text-center">
            <p className="text-neutral-500 text-sm max-w-md">
              Set filters and click Search to query transfer logs, login history, or aggregated summaries.
            </p>
          </div>
        ) : isLoading ? (
          <div className="flex-1 flex items-center justify-center">
            <div className="w-10 h-10 border-4 border-blue-600/20 border-t-blue-600 rounded-full animate-spin" />
          </div>
        ) : error ? (
          <div className="flex-1 flex items-center justify-center p-8 text-red-400 text-sm">{error}</div>
        ) : (
          <>
            <div className="shrink-0 px-4 py-2 border-b border-neutral-800 text-[10px] font-bold text-neutral-500 uppercase tracking-widest">
              {activeResult?.totalCount ?? 0} result{(activeResult?.totalCount ?? 0) === 1 ? '' : 's'}
            </div>
            <div className="flex-1 min-h-0 overflow-auto">
              {activeTab === 'transfers' && (
                <TransfersTable rows={transfers?.items ?? []} onChip={applyChip} />
              )}
              {activeTab === 'logins' && (
                <LoginsTable rows={logins?.items ?? []} onChip={applyChip} />
              )}
              {activeTab === 'summaries' && (
                <SummariesTable rows={summaries?.items ?? []} onChip={applyChip} />
              )}
            </div>
            <Pagination currentPage={page} totalPages={totalPages} onPageChange={setPage} />
          </>
        )}
      </div>
    </div>
  )
}

function FilterInput({
  label,
  value,
  onChange,
  placeholder,
  disabled,
}: {
  label: string
  value: string
  onChange: (v: string) => void
  placeholder?: string
  disabled?: boolean
}) {
  return (
    <div>
      <label className="block text-[10px] font-bold text-neutral-500 uppercase tracking-wider mb-1.5">{label}</label>
      <input
        type="text"
        value={value}
        onChange={e => onChange(e.target.value)}
        placeholder={placeholder}
        disabled={disabled}
        onKeyDown={e => { if (e.key === 'Enter') e.currentTarget.form?.requestSubmit() }}
        className="w-full px-3 py-2 rounded-lg bg-neutral-950 border border-neutral-800 text-sm text-white placeholder:text-neutral-600 focus:outline-none focus:border-blue-500/50 disabled:opacity-40"
      />
    </div>
  )
}

function TabButton({
  active,
  onClick,
  icon,
  label,
  count,
}: {
  active: boolean
  onClick: () => void
  icon: ReactNode
  label: string
  count?: number
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`inline-flex items-center gap-2 px-4 py-2.5 text-sm font-semibold border-b-2 -mb-px transition-colors ${
        active
          ? 'border-blue-500 text-white'
          : 'border-transparent text-neutral-500 hover:text-neutral-300'
      }`}
    >
      {icon}
      {label}
      {count !== undefined && (
        <span className={`text-[10px] px-1.5 py-0.5 rounded-md ${active ? 'bg-blue-600/20 text-blue-400' : 'bg-neutral-800 text-neutral-500'}`}>
          {count.toLocaleString()}
        </span>
      )}
    </button>
  )
}

function Chip({ label, onClick }: { label: string; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="text-blue-400 hover:text-blue-300 hover:underline font-medium truncate max-w-[120px]"
      title={`Filter by ${label}`}
    >
      {label}
    </button>
  )
}

function TransfersTable({
  rows,
  onChip,
}: {
  rows: TransferLogRow[]
  onChip: (field: FilterChipField, value: string) => void
}) {
  if (rows.length === 0) {
    return <EmptyTable message="No transfers match these filters." />
  }

  return (
    <table className="w-full text-left text-xs">
      <thead className="sticky top-0 bg-neutral-950 z-10 text-[10px] uppercase tracking-wider text-neutral-500">
        <tr className="border-b border-neutral-800">
          <th className="px-3 py-2 font-bold">Time (UTC)</th>
          <th className="px-3 py-2 font-bold">Type</th>
          <th className="px-3 py-2 font-bold">From</th>
          <th className="px-3 py-2 font-bold">To</th>
          <th className="px-3 py-2 font-bold">Item</th>
          <th className="px-3 py-2 font-bold">IPs</th>
        </tr>
      </thead>
      <tbody className="divide-y divide-neutral-800/80">
        {rows.map(row => (
          <tr key={row.id} className="hover:bg-neutral-800/30 text-neutral-300">
            <td className="px-3 py-2 whitespace-nowrap text-neutral-400">{formatUtc(row.timestamp)}</td>
            <td className="px-3 py-2 text-neutral-400">{row.transferType}</td>
            <td className="px-3 py-2">
              <Chip label={row.fromPlayerName} onClick={() => onChip('character', row.fromPlayerName)} />
              {row.fromPlayerAccount && (
                <div className="text-neutral-500 truncate max-w-[140px]">
                  <Chip label={row.fromPlayerAccount} onClick={() => onChip('account', row.fromPlayerAccount!)} />
                </div>
              )}
            </td>
            <td className="px-3 py-2">
              <Chip label={row.toPlayerName} onClick={() => onChip('character', row.toPlayerName)} />
              {row.toPlayerAccount && (
                <div className="text-neutral-500 truncate max-w-[140px]">
                  <Chip label={row.toPlayerAccount} onClick={() => onChip('account', row.toPlayerAccount!)} />
                </div>
              )}
            </td>
            <td className="px-3 py-2">
              {row.itemName} <span className="text-neutral-500">×{row.quantity.toLocaleString()}</span>
            </td>
            <td className="px-3 py-2 text-neutral-500 font-mono text-[10px]">
              {row.fromPlayerIP && <div><Chip label={row.fromPlayerIP} onClick={() => onChip('ip', row.fromPlayerIP!)} /> (from)</div>}
              {row.toPlayerIP && <div><Chip label={row.toPlayerIP} onClick={() => onChip('ip', row.toPlayerIP!)} /> (to)</div>}
              {!row.fromPlayerIP && !row.toPlayerIP && '—'}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function LoginsTable({
  rows,
  onChip,
}: {
  rows: CharTrackerLoginRow[]
  onChip: (field: FilterChipField, value: string) => void
}) {
  if (rows.length === 0) {
    return <EmptyTable message="No logins match these filters (login history is retained ~90 days)." />
  }

  return (
    <table className="w-full text-left text-xs">
      <thead className="sticky top-0 bg-neutral-950 z-10 text-[10px] uppercase tracking-wider text-neutral-500">
        <tr className="border-b border-neutral-800">
          <th className="px-3 py-2 font-bold">Login (UTC)</th>
          <th className="px-3 py-2 font-bold">Character</th>
          <th className="px-3 py-2 font-bold">Account</th>
          <th className="px-3 py-2 font-bold">IP</th>
          <th className="px-3 py-2 font-bold">Duration</th>
          <th className="px-3 py-2 font-bold">Landblock</th>
        </tr>
      </thead>
      <tbody className="divide-y divide-neutral-800/80">
        {rows.map(row => (
          <tr key={row.id} className="hover:bg-neutral-800/30 text-neutral-300">
            <td className="px-3 py-2 whitespace-nowrap text-neutral-400">{formatUtc(row.loginTimestamp)}</td>
            <td className="px-3 py-2">
              {row.characterName ? (
                <Chip label={row.characterName} onClick={() => onChip('character', row.characterName!)} />
              ) : '—'}
            </td>
            <td className="px-3 py-2">
              {row.accountName ? (
                <Chip label={row.accountName} onClick={() => onChip('account', row.accountName!)} />
              ) : '—'}
            </td>
            <td className="px-3 py-2 font-mono text-[10px]">
              {row.loginIP ? (
                <Chip label={row.loginIP} onClick={() => onChip('ip', row.loginIP!)} />
              ) : '—'}
            </td>
            <td className="px-3 py-2 text-neutral-400">{formatDuration(row.connectionDuration)}</td>
            <td className="px-3 py-2 text-neutral-500 font-mono">{row.landblock ?? '—'}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function SummariesTable({
  rows,
  onChip,
}: {
  rows: TransferSummaryRow[]
  onChip: (field: FilterChipField, value: string) => void
}) {
  if (rows.length === 0) {
    return <EmptyTable message="No transfer summaries match these filters." />
  }

  return (
    <table className="w-full text-left text-xs">
      <thead className="sticky top-0 bg-neutral-950 z-10 text-[10px] uppercase tracking-wider text-neutral-500">
        <tr className="border-b border-neutral-800">
          <th className="px-3 py-2 font-bold">Last</th>
          <th className="px-3 py-2 font-bold">Type</th>
          <th className="px-3 py-2 font-bold">From → To</th>
          <th className="px-3 py-2 font-bold">Count</th>
          <th className="px-3 py-2 font-bold">Qty</th>
          <th className="px-3 py-2 font-bold">Value</th>
        </tr>
      </thead>
      <tbody className="divide-y divide-neutral-800/80">
        {rows.map(row => (
          <tr key={row.id} className="hover:bg-neutral-800/30 text-neutral-300">
            <td className="px-3 py-2 whitespace-nowrap text-neutral-400">{formatUtc(row.lastTransfer)}</td>
            <td className="px-3 py-2 text-neutral-400">{row.transferType}</td>
            <td className="px-3 py-2">
              <Chip label={row.fromPlayerName} onClick={() => onChip('character', row.fromPlayerName)} />
              <span className="text-neutral-600 mx-1">→</span>
              <Chip label={row.toPlayerName} onClick={() => onChip('character', row.toPlayerName)} />
            </td>
            <td className="px-3 py-2">{row.totalTransfers.toLocaleString()}</td>
            <td className="px-3 py-2">{row.totalQuantity.toLocaleString()}</td>
            <td className="px-3 py-2">{row.totalValue.toLocaleString()}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function EmptyTable({ message }: { message: string }) {
  return (
    <div className="p-12 text-center text-neutral-500 text-sm">{message}</div>
  )
}
