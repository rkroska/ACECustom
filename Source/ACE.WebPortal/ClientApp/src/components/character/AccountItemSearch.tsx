import { useState, useRef, FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search, Activity, Package } from 'lucide-react'
import { api } from '../../services/api'
import { AccountItemSearchResult } from '../../types'
import { getIconUrl, getIconBgClass } from '../../utils/icon'

/**
 * Searches the inventories of every character on the logged-in account.
 * Clicking a result opens that character's inventory filtered to the item.
 */
export default function AccountItemSearch() {
  const navigate = useNavigate()
  const [query, setQuery] = useState('')
  const [result, setResult] = useState<AccountItemSearchResult | null>(null)
  const [searchedFor, setSearchedFor] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  // Bumped on every search and on Clear, so a late response from an older request is ignored.
  const requestId = useRef(0)

  const runSearch = async (e: FormEvent) => {
    e.preventDefault()
    const q = query.trim()
    if (q.length < 2) {
      setError('Enter at least 2 characters.')
      return
    }
    const id = ++requestId.current
    try {
      setError(null)
      setIsLoading(true)
      const data = await api.get<AccountItemSearchResult>(`/api/character/inventory-search?q=${encodeURIComponent(q)}`)
      if (id !== requestId.current) return
      setResult(data)
      setSearchedFor(q)
    } catch (err) {
      if (id !== requestId.current) return
      setError(err instanceof Error ? err.message : 'Search failed')
    } finally {
      if (id === requestId.current) setIsLoading(false)
    }
  }

  const clear = () => {
    requestId.current++
    setIsLoading(false)
    setQuery('')
    setResult(null)
    setSearchedFor('')
    setError(null)
  }

  return (
    <div className="bg-neutral-950/40 border border-neutral-800/60 rounded-xl overflow-hidden">
      <form onSubmit={runSearch} className="flex items-center gap-2 p-3">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-neutral-500" />
          <input
            type="text"
            placeholder="Search items on all your characters (name or WCID)..."
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            className="w-full bg-neutral-950 border border-neutral-800 rounded-xl py-2.5 pl-10 pr-4 text-sm text-white placeholder-neutral-700 focus:outline-none focus:border-blue-500/50 transition-all font-medium"
          />
        </div>
        <button
          type="submit"
          disabled={isLoading}
          className="px-4 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-500 disabled:opacity-50 text-white text-xs font-bold uppercase tracking-wider"
        >
          {isLoading ? <Activity className="w-4 h-4 animate-spin" /> : 'Search'}
        </button>
        {result && (
          <button
            type="button"
            onClick={clear}
            className="px-3 py-2.5 rounded-xl border border-neutral-800 text-neutral-400 hover:text-white text-xs font-bold uppercase tracking-wider"
          >
            Clear
          </button>
        )}
      </form>

      {error && <p className="px-4 pb-3 text-xs text-red-400">{error}</p>}

      {result && (
        <div className="border-t border-neutral-800/60">
          <p className="px-4 py-2 text-[10px] font-bold uppercase tracking-wider text-neutral-500">
            {result.totalMatches} match{result.totalMatches === 1 ? '' : 'es'} for "{searchedFor}" across {result.charactersSearched} character{result.charactersSearched === 1 ? '' : 's'}
            {result.truncated && ` (showing first ${result.matches.length})`}
            {' '}- house storage not included
          </p>

          {result.matches.length === 0 ? (
            <div className="px-4 pb-6 pt-2 text-center text-neutral-500 text-sm">
              <Package className="w-8 h-8 mx-auto mb-2 text-neutral-800" />
              No items found.
            </div>
          ) : (
            <div className="max-h-[420px] overflow-y-auto custom-scrollbar">
              {result.matches.map(m => (
                <button
                  key={`${m.characterGuid}-${m.item.guid}`}
                  onClick={() => navigate(`/characters/${m.characterGuid}/inventory?q=${encodeURIComponent(m.item.name)}`)}
                  className="w-full grid grid-cols-[1fr,140px,140px] gap-3 px-4 py-2 items-center text-left hover:bg-white/[0.03] transition-colors"
                >
                  <div className="flex items-center gap-3 min-w-0">
                    {m.item.iconId ? (
                      <div className={`p-0.5 shrink-0 ${getIconBgClass(m.item)}`}>
                        <img src={getIconUrl(m.item)} alt={m.item.name} className="w-7 h-7 object-contain" loading="lazy" />
                      </div>
                    ) : (
                      <div className="w-7 h-7 shrink-0" />
                    )}
                    <span className="text-xs font-medium text-neutral-300 truncate" title={m.item.name}>
                      {m.item.name}
                      {m.item.stackSize > 1 && (
                        <span className="ml-2 text-[10px] text-blue-500/80 font-black">x{m.item.stackSize}</span>
                      )}
                    </span>
                  </div>
                  <span className="text-xs text-neutral-400 truncate flex items-center gap-1.5" title={m.characterName}>
                    <span className={`w-1.5 h-1.5 rounded-full shrink-0 ${m.characterOnline ? 'bg-green-500' : 'bg-neutral-700'}`} />
                    {m.characterName}
                  </span>
                  <span className="text-[11px] text-neutral-500 truncate" title={m.location}>{m.location}</span>
                </button>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  )
}
