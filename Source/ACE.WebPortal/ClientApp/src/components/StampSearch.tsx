import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { ScrollText, Search, ChevronRight } from 'lucide-react'
import { api } from '../services/api'
import { useDebounce } from '../hooks/useDebounce'
import PageHeader from './common/PageHeader'
import Pagination from './common/Pagination'
import type { StampLookupResult } from '../types'

const CHARACTER_PAGE_SIZE = 50

export default function StampSearch() {
  const [query, setQuery] = useState('')
  const debouncedQuery = useDebounce(query, 350)
  const [suggestions, setSuggestions] = useState<string[]>([])
  const [searching, setSearching] = useState(false)
  const [selectedStamp, setSelectedStamp] = useState<string | null>(null)
  const [detail, setDetail] = useState<StampLookupResult | null>(null)
  const [loadingDetail, setLoadingDetail] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [characterPage, setCharacterPage] = useState(1)

  useEffect(() => {
    if (!debouncedQuery.trim() || debouncedQuery.trim().length < 2) {
      setSuggestions([])
      return
    }

    const controller = new AbortController()
    setSearching(true)
    api
      .get<string[]>(`/api/stamp/suggest?q=${encodeURIComponent(debouncedQuery)}&limit=30`, {
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

  const loadLookup = useCallback(async (stamp: string, page: number) => {
    setLoadingDetail(true)
    setError(null)
    const offset = (page - 1) * CHARACTER_PAGE_SIZE
    try {
      const data = await api.get<StampLookupResult>(
        `/api/stamp/lookup?stamp=${encodeURIComponent(stamp)}&limit=${CHARACTER_PAGE_SIZE}&offset=${offset}`
      )
      if (data) {
        setDetail(data)
        setSelectedStamp(stamp)
      }
    } catch (err) {
      setDetail(null)
      setError(err instanceof Error ? err.message : 'Failed to look up stamp.')
    } finally {
      setLoadingDetail(false)
    }
  }, [])

  const selectStamp = (stamp: string) => {
    setQuery(stamp)
    setSuggestions([])
    setCharacterPage(1)
    loadLookup(stamp, 1)
  }

  const characterTotalPages = useMemo(() => {
    if (!detail) return 1
    return Math.max(1, Math.ceil(detail.characterHolderCount / CHARACTER_PAGE_SIZE))
  }, [detail])

  return (
    <div className="flex flex-col h-full bg-neutral-950 p-8 overflow-hidden">
      <PageHeader title="Quest Stamp Search" icon={ScrollText} className="shrink-0" />

      <div className="relative shrink-0 mb-6">
        <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-neutral-600" />
        <input
          type="text"
          placeholder="Search quest stamp name..."
          value={query}
          onChange={(e) => {
            setQuery(e.target.value)
            if (!e.target.value.trim()) {
              setSelectedStamp(null)
              setDetail(null)
            }
          }}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && query.trim()) {
              setCharacterPage(1)
              loadLookup(query.trim(), 1)
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
            {suggestions.map((stamp) => (
              <button
                key={stamp}
                type="button"
                onClick={() => selectStamp(stamp)}
                className="w-full flex items-center justify-between gap-4 px-4 py-3 text-left hover:bg-neutral-800 transition-colors border-b border-neutral-800/50 last:border-0"
              >
                <span className="text-sm font-mono text-white truncate">{stamp}</span>
                <ChevronRight className="w-4 h-4 text-neutral-600 shrink-0" />
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
          Looking up holders...
        </div>
      )}

      {!loadingDetail && detail && (
        <div className="flex-1 overflow-y-auto custom-scrollbar space-y-6 pb-8">
          <div className="bg-neutral-900/50 border border-neutral-800 rounded-2xl p-5">
            <h2 className="text-lg font-black text-white font-mono">{detail.stampName}</h2>
            <div className="flex flex-wrap gap-6 mt-3 text-sm">
              <Stat label="Account holders" value={detail.accountHolderCount} />
              <Stat label="Characters" value={detail.characterHolderCount} />
              <Stat label="Server completions" value={detail.serverTotalCompletions} />
            </div>
          </div>

          <section className="bg-neutral-900/30 border border-neutral-800/50 rounded-2xl overflow-hidden">
            <header className="px-5 py-4 border-b border-neutral-800 bg-neutral-900/50">
              <h3 className="text-[10px] font-bold text-neutral-400 uppercase tracking-[0.2em]">Account stamps</h3>
              <p className="text-xs text-neutral-500 mt-1">Auth DB — account-wide quest bonuses</p>
            </header>
            {detail.accountHolders.length > 0 ? (
              <table className="w-full text-left">
                <thead>
                  <tr className="text-[10px] font-bold text-neutral-600 uppercase tracking-widest bg-neutral-950/30">
                    <th className="px-5 py-2">Account</th>
                    <th className="px-5 py-2">Account ID</th>
                    <th className="px-5 py-2">Times</th>
                  </tr>
                </thead>
                <tbody>
                  {detail.accountHolders.map((a) => (
                    <tr key={a.accountId} className="border-t border-neutral-800/30">
                      <td className="px-5 py-3 text-sm text-white">{a.accountName}</td>
                      <td className="px-5 py-3 text-xs font-mono text-neutral-400">{a.accountId}</td>
                      <td className="px-5 py-3 text-xs text-neutral-400">{a.numTimesCompleted}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <p className="px-5 py-8 text-sm text-neutral-600">No accounts have this stamp in account_quest.</p>
            )}
          </section>

          <section className="bg-neutral-900/30 border border-neutral-800/50 rounded-2xl overflow-hidden">
            <header className="px-5 py-4 border-b border-neutral-800 bg-neutral-900/50">
              <h3 className="text-[10px] font-bold text-neutral-400 uppercase tracking-[0.2em]">Character stamps</h3>
              <p className="text-xs text-neutral-500 mt-1">Shard DB — per-character quest registry</p>
            </header>
            {detail.characterHolders.length > 0 ? (
              <>
                <table className="w-full text-left">
                  <thead>
                    <tr className="text-[10px] font-bold text-neutral-600 uppercase tracking-widest bg-neutral-950/30">
                      <th className="px-5 py-2">Character</th>
                      <th className="px-5 py-2">Account ID</th>
                      <th className="px-5 py-2">Times</th>
                      <th className="px-5 py-2">Last completed</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.characterHolders.map((c) => (
                      <tr key={c.characterId} className="border-t border-neutral-800/30">
                        <td className="px-5 py-3">
                          <Link
                            to={`/characters/${c.characterId}`}
                            className="text-sm text-blue-400 hover:underline"
                          >
                            {c.characterName}
                          </Link>
                        </td>
                        <td className="px-5 py-3 text-xs font-mono text-neutral-400">{c.accountId}</td>
                        <td className="px-5 py-3 text-xs text-neutral-400">{c.numTimesCompleted}</td>
                        <td className="px-5 py-3 text-xs text-neutral-500 font-mono">
                          {c.lastTimeCompletedUtc ?? (c.lastTimeCompletedUnix ? `unix ${c.lastTimeCompletedUnix}` : '—')}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                {characterTotalPages > 1 && (
                  <Pagination
                    currentPage={characterPage}
                    totalPages={characterTotalPages}
                    onPageChange={(page) => {
                      setCharacterPage(page)
                      if (selectedStamp) loadLookup(selectedStamp, page)
                    }}
                  />
                )}
              </>
            ) : (
              <p className="px-5 py-8 text-sm text-neutral-600">No characters have this stamp in quest registry.</p>
            )}
          </section>
        </div>
      )}

      {!loadingDetail && !detail && debouncedQuery.trim().length >= 2 && suggestions.length === 0 && !searching && (
        <p className="text-sm text-neutral-600 text-center py-12">No stamps matched. Press Enter to try an exact lookup.</p>
      )}

      {!loadingDetail && !detail && !debouncedQuery.trim() && (
        <p className="text-sm text-neutral-600 text-center py-12">
          Search for a quest stamp to see which accounts and characters have it.
        </p>
      )}
    </div>
  )
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div>
      <div className="text-xl font-black text-white">{value.toLocaleString()}</div>
      <div className="text-[10px] text-neutral-500 uppercase tracking-widest">{label}</div>
    </div>
  )
}
