import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Package, Search, ChevronRight, Copy, Check } from 'lucide-react'
import { api } from '../services/api'
import { useDebounce } from '../hooks/useDebounce'
import PageHeader from './common/PageHeader'
import Pagination from './common/Pagination'
import type { ItemReferenceResult, ItemSearchResult } from '../types'

const SHARD_PAGE_SIZE = 50

export default function ItemSearch() {
  const [query, setQuery] = useState('')
  const debouncedQuery = useDebounce(query, 350)
  const [suggestions, setSuggestions] = useState<ItemSearchResult[]>([])
  const [searching, setSearching] = useState(false)
  const [selected, setSelected] = useState<ItemSearchResult | null>(null)
  const [detail, setDetail] = useState<ItemReferenceResult | null>(null)
  const [loadingDetail, setLoadingDetail] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [shardPage, setShardPage] = useState(1)
  const [copiedId, setCopiedId] = useState<string | null>(null)

  useEffect(() => {
    if (!debouncedQuery.trim()) {
      setSuggestions([])
      return
    }

    const controller = new AbortController()
    setSearching(true)
    api
      .get<ItemSearchResult[]>(`/api/item/search?q=${encodeURIComponent(debouncedQuery)}&limit=30`, {
        signal: controller.signal,
      })
      .then((data) => setSuggestions(data ?? []))
      .catch((err) => {
        if (err instanceof Error && err.name === 'AbortError') return
        setSuggestions([])
      })
      .finally(() => setSearching(false))

    return () => controller.abort()
  }, [debouncedQuery])

  const loadReferences = useCallback(async (item: ItemSearchResult, page: number) => {
    setLoadingDetail(true)
    setError(null)
    const offset = (page - 1) * SHARD_PAGE_SIZE
    try {
      const data = await api.get<ItemReferenceResult>(
        `/api/item/${item.wcid}/references?shardLimit=${SHARD_PAGE_SIZE}&shardOffset=${offset}`
      )
      if (data) {
        setDetail(data)
        setSelected(item)
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load item references.')
      setDetail(null)
    } finally {
      setLoadingDetail(false)
    }
  }, [])

  const selectItem = (item: ItemSearchResult) => {
    setQuery(`${item.name} (${item.wcid})`)
    setSuggestions([])
    setShardPage(1)
    loadReferences(item, 1)
  }

  const shardTotalPages = useMemo(() => {
    if (!detail?.shard) return 1
    return Math.max(1, Math.ceil(detail.shard.totalCount / SHARD_PAGE_SIZE))
  }, [detail?.shard])

  const copyText = (text: string, id: string) => {
    navigator.clipboard.writeText(text).then(() => {
      setCopiedId(id)
      setTimeout(() => setCopiedId(null), 2000)
    })
  }

  const world = detail?.worldReferences
  const shard = detail?.shard

  return (
    <div className="flex flex-col h-full bg-neutral-950 p-8 overflow-hidden">
      <PageHeader title="Item Search" icon={Package} className="shrink-0" />

      <div className="relative shrink-0 mb-6">
        <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-neutral-600" />
        <input
          type="text"
          placeholder="Search by item name, classname, or WCID..."
          value={query}
          onChange={(e) => {
            setQuery(e.target.value)
            if (!e.target.value.trim()) {
              setSelected(null)
              setDetail(null)
            }
          }}
          className="w-full bg-neutral-900 border border-neutral-800 rounded-2xl pl-12 pr-4 py-3 text-sm text-white placeholder-neutral-600 focus:outline-none focus:ring-2 focus:ring-blue-600/50 font-medium"
        />
        {searching && (
          <span className="absolute right-4 top-1/2 -translate-y-1/2 text-[10px] text-neutral-500 uppercase tracking-widest">
            Searching...
          </span>
        )}
        {suggestions.length > 0 && (
          <div className="absolute z-20 left-0 right-0 top-full mt-2 bg-neutral-900 border border-neutral-800 rounded-2xl shadow-2xl overflow-hidden max-h-72 overflow-y-auto custom-scrollbar">
            {suggestions.map((s) => (
              <button
                key={s.wcid}
                type="button"
                onClick={() => selectItem(s)}
                className="w-full flex items-center justify-between gap-4 px-4 py-3 text-left hover:bg-neutral-800 transition-colors border-b border-neutral-800/50 last:border-0"
              >
                <div className="min-w-0">
                  <div className="text-sm font-bold text-white truncate">{s.name}</div>
                  <div className="text-[10px] text-neutral-500 font-mono truncate">{s.className}</div>
                </div>
                <div className="flex items-center gap-2 shrink-0">
                  <span className="text-[10px] text-neutral-500 uppercase">{s.weenieType}</span>
                  <span className="text-xs font-mono text-blue-400">{s.wcid}</span>
                  <ChevronRight className="w-4 h-4 text-neutral-600" />
                </div>
              </button>
            ))}
          </div>
        )}
      </div>

      {error && (
        <div className="mb-4 p-4 rounded-xl bg-red-500/10 border border-red-500/20 text-red-400 text-sm">{error}</div>
      )}

      {loadingDetail && (
        <div className="flex-1 flex items-center justify-center text-neutral-500 text-sm uppercase tracking-widest animate-pulse">
          Loading references...
        </div>
      )}

      {!loadingDetail && detail && (
        <div className="flex-1 overflow-y-auto custom-scrollbar space-y-6 pb-8">
          <div className="bg-neutral-900/50 border border-neutral-800 rounded-2xl p-5">
            <h2 className="text-lg font-black text-white">{detail.name}</h2>
            <p className="text-sm text-neutral-400 font-mono mt-1">
              WCID {detail.wcid} · {detail.className} · {detail.weenieType}
            </p>
          </div>

          <section className="bg-neutral-900/30 border border-neutral-800/50 rounded-2xl overflow-hidden">
            <header className="px-5 py-4 border-b border-neutral-800 bg-neutral-900/50">
              <h3 className="text-[10px] font-bold text-neutral-400 uppercase tracking-[0.2em]">World database</h3>
              <p className="text-xs text-neutral-500 mt-1">
                Static definitions — create lists, generators, landblock spawns
              </p>
            </header>
            <div className="p-5 grid grid-cols-3 gap-4 text-center">
              <Stat label="Create list" value={world?.createListCount ?? 0} shown={world?.createList.length ?? 0} />
              <Stat label="Generators" value={world?.generatorCount ?? 0} shown={world?.generators.length ?? 0} />
              <Stat
                label="Landblock spawns"
                value={world?.landblockInstanceCount ?? 0}
                shown={world?.landblockInstances.length ?? 0}
              />
            </div>

            {world && world.createList.length > 0 && (
              <RefTable title="Create list (sample)" headers={['Parent', 'WCID', 'Destination', 'Stack']}>
                {world.createList.map((r) => (
                  <tr key={`cl-${r.parentWcid}-${r.destinationType}`} className="border-t border-neutral-800/30">
                    <td className="px-4 py-3 text-sm text-white">{r.parentName}</td>
                    <td className="px-4 py-3 text-xs font-mono text-blue-400">{r.parentWcid}</td>
                    <td className="px-4 py-3 text-xs text-neutral-400">{r.destinationType}</td>
                    <td className="px-4 py-3 text-xs text-neutral-400">{r.stackSize}</td>
                  </tr>
                ))}
              </RefTable>
            )}

            {world && world.generators.length > 0 && (
              <RefTable title="Generators (sample)" headers={['Parent', 'WCID', 'Chance', 'Max']}>
                {world.generators.map((r) => (
                  <tr key={`gen-${r.parentWcid}-${r.probability}`} className="border-t border-neutral-800/30">
                    <td className="px-4 py-3 text-sm text-white">{r.parentName}</td>
                    <td className="px-4 py-3 text-xs font-mono text-blue-400">{r.parentWcid}</td>
                    <td className="px-4 py-3 text-xs text-neutral-400">{r.probability}</td>
                    <td className="px-4 py-3 text-xs text-neutral-400">{r.maxCreate}</td>
                  </tr>
                ))}
              </RefTable>
            )}

            {world && world.landblockInstances.length > 0 && (
              <RefTable title="Landblock instances (sample)" headers={['GUID', 'Landblock', 'Cell']}>
                {world.landblockInstances.map((r) => (
                  <tr key={`lb-${r.guid}`} className="border-t border-neutral-800/30">
                    <td className="px-4 py-3 text-xs font-mono text-neutral-300">0x{r.guid.toString(16).toUpperCase()}</td>
                    <td className="px-4 py-3 text-xs font-mono text-neutral-400">{r.landblockHex ?? '—'}</td>
                    <td className="px-4 py-3 text-xs font-mono text-neutral-400">{r.objCellId}</td>
                  </tr>
                ))}
              </RefTable>
            )}

            {world &&
              world.createList.length === 0 &&
              world.generators.length === 0 &&
              world.landblockInstances.length === 0 && (
                <p className="px-5 pb-5 text-sm text-neutral-600">No world DB references for this WCID.</p>
              )}
          </section>

          <section className="bg-neutral-900/30 border border-neutral-800/50 rounded-2xl overflow-hidden">
            <header className="px-5 py-4 border-b border-neutral-800 bg-neutral-900/50 flex items-center justify-between gap-4">
              <div>
                <h3 className="text-[10px] font-bold text-neutral-400 uppercase tracking-[0.2em]">Shard database</h3>
                <p className="text-xs text-neutral-500 mt-1">Live item instances (inventory, storage, etc.)</p>
              </div>
              {shard && (
                <span className="text-xs font-mono text-blue-400 shrink-0">
                  {shard.totalCount.toLocaleString()} total
                </span>
              )}
            </header>

            {shard && shard.instances.length > 0 ? (
              <>
                <RefTable
                  title=""
                  headers={['Biota', 'Stack', 'Location', 'Owner', '']}
                >
                  {shard.instances.map((inst) => {
                    const copyKey = `biota-${inst.biotaId}`
                    return (
                      <tr key={inst.biotaId} className="border-t border-neutral-800/30 group">
                        <td className="px-4 py-3">
                          <div className="text-xs font-mono text-white">{inst.biotaHex}</div>
                          {inst.itemName && (
                            <div className="text-[10px] text-neutral-500 truncate max-w-[140px]">{inst.itemName}</div>
                          )}
                        </td>
                        <td className="px-4 py-3 text-xs text-neutral-400">{inst.stackSize}</td>
                        <td className="px-4 py-3">
                          <div className="text-[10px] font-bold uppercase text-neutral-500">{inst.locationKind}</div>
                          <div className="text-xs text-neutral-400">{inst.locationDetail}</div>
                        </td>
                        <td className="px-4 py-3 text-sm text-white">
                          {inst.characterLinkGuid ? (
                            <Link
                              to={`/characters/${inst.characterLinkGuid}`}
                              className="text-blue-400 hover:underline"
                            >
                              {inst.ownerName}
                            </Link>
                          ) : (
                            inst.ownerName ?? '—'
                          )}
                        </td>
                        <td className="px-4 py-3 text-right">
                          <button
                            type="button"
                            onClick={() => copyText(inst.biotaHex, copyKey)}
                            className="p-1.5 rounded-lg text-neutral-600 hover:text-white opacity-0 group-hover:opacity-100 transition-all"
                            title="Copy biota GUID"
                          >
                            {copiedId === copyKey ? (
                              <Check className="w-4 h-4 text-green-500" />
                            ) : (
                              <Copy className="w-4 h-4" />
                            )}
                          </button>
                        </td>
                      </tr>
                    )
                  })}
                </RefTable>
                {shardTotalPages > 1 && (
                  <Pagination
                    currentPage={shardPage}
                    totalPages={shardTotalPages}
                    onPageChange={(page) => {
                      setShardPage(page)
                      if (selected) loadReferences(selected, page)
                    }}
                  />
                )}
              </>
            ) : (
              <p className="px-5 py-8 text-sm text-neutral-600 text-center">No shard instances for this WCID.</p>
            )}
          </section>
        </div>
      )}

      {!loadingDetail && !detail && debouncedQuery.trim() && suggestions.length === 0 && !searching && (
        <p className="text-sm text-neutral-600 text-center py-12">No items matched your search.</p>
      )}

      {!loadingDetail && !detail && !debouncedQuery.trim() && (
        <p className="text-sm text-neutral-600 text-center py-12">
          Enter a name, classname, or WCID to find where an item is defined and stored.
        </p>
      )}
    </div>
  )
}

function Stat({ label, value, shown }: { label: string; value: number; shown: number }) {
  return (
    <div className="bg-neutral-950/50 rounded-xl p-4 border border-neutral-800/50">
      <div className="text-2xl font-black text-white">{value.toLocaleString()}</div>
      <div className="text-[10px] text-neutral-500 uppercase tracking-widest mt-1">{label}</div>
      {shown < value && (
        <div className="text-[9px] text-amber-500/80 mt-1">Showing {shown} of {value}</div>
      )}
    </div>
  )
}

function RefTable({
  title,
  headers,
  children,
}: {
  title: string
  headers: string[]
  children: React.ReactNode
}) {
  return (
    <div className="mx-5 mb-5 border border-neutral-800/50 rounded-xl overflow-hidden">
      {title && (
        <div className="px-4 py-2 bg-neutral-950/50 text-[10px] font-bold text-neutral-500 uppercase tracking-widest">
          {title}
        </div>
      )}
      <table className="w-full text-left">
        <thead>
          <tr className="text-[10px] font-bold text-neutral-600 uppercase tracking-widest bg-neutral-950/30">
            {headers.map((h) => (
              <th key={h || 'action'} className="px-4 py-2">
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>{children}</tbody>
      </table>
    </div>
  )
}
