import { useState, useEffect, useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search, Activity, Users, ShieldAlert, ChevronLeft, ChevronRight, Info } from 'lucide-react'
import { api } from '../services/api'
import { Character } from '../types'
import { useDebounce } from '../hooks/useDebounce'
import CharacterListItem from './character/CharacterListItem'
import PageHeader from './common/PageHeader'

const ITEMS_PER_PAGE = 25

export default function PlayerList() {
  const navigate = useNavigate()
  const [onlinePlayersBase, setOnlinePlayersBase] = useState<Character[]>([])
  const [searchResults, setSearchResults] = useState<Character[]>([])
  const [searchTerm, setSearchTerm] = useState('')
  const debouncedSearchTerm = useDebounce(searchTerm, 500)
  const [currentPage, setCurrentPage] = useState(1)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  // Initial fetch of online players
  useEffect(() => {
    fetchOnlinePlayers()
  }, [])

  const fetchOnlinePlayers = async () => {
    try {
      setIsLoading(true)
      const data = await api.get<Character[]>('/api/character/all-online')
      // Mark initial online list as online
      setOnlinePlayersBase((data ?? []).map(p => ({ ...p, isOnline: true })))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error')
    } finally {
      setIsLoading(false)
    }
  }

  // Unified Search Logic using Debounce Hook
  useEffect(() => {
    setCurrentPage(1) // Reset pagination on search change

    if (debouncedSearchTerm.length >= 3) {
      searchAllPlayers(debouncedSearchTerm)
    } else {
      setSearchResults([])
      // If we're not searching the global DB, we aren't loading anymore
      setIsLoading(false)
    }
  }, [debouncedSearchTerm])

  const searchAllPlayers = async (name: string) => {
    try {
      setIsLoading(true)
      const data = await api.get<Character[]>(`/api/character/search-all/${encodeURIComponent(name)}`)
      setSearchResults(data ?? [])
    } catch (err) {
      console.error('Unified search error:', err)
    } finally {
      setIsLoading(false)
    }
  }

  // Derived list based on search term
  const effectivePlayersList = useMemo(() => {
    if (debouncedSearchTerm.length >= 3) {
      return searchResults
    }
    
    // Otherwise, use local filtering on online players
    if (!searchTerm.trim()) return onlinePlayersBase
    
    const lowerSearch = searchTerm.toLowerCase()
    return onlinePlayersBase.filter(p => p.name.toLowerCase().includes(lowerSearch))
  }, [onlinePlayersBase, searchResults, debouncedSearchTerm, searchTerm])

  const totalPages = Math.ceil(effectivePlayersList.length / ITEMS_PER_PAGE)
  const paginatedPlayers = effectivePlayersList.slice(
    (currentPage - 1) * ITEMS_PER_PAGE,
    currentPage * ITEMS_PER_PAGE
  )

  const handleSelect = (guid: number) => {
    navigate(`/players/${guid}/general`)
  }

  if (error) {
    return (
      <div className="flex-1 flex flex-col items-center justify-center p-8 text-center bg-neutral-900 text-neutral-100">
        <ShieldAlert className="w-12 h-12 text-red-500 mb-4 opacity-50" />
        <h2 className="text-xl font-bold text-white mb-2 uppercase tracking-widest text-[10px]">Access Restricted</h2>
        <p className="text-neutral-500 text-sm max-w-xs font-medium">{error}</p>
        <button 
          onClick={fetchOnlinePlayers}
          className="mt-6 px-4 py-2 bg-neutral-800 hover:bg-neutral-700 rounded-xl text-xs font-bold transition-all uppercase tracking-widest border border-neutral-700"
        >
          Retry Connection
        </button>
      </div>
    )
  }

  return (
    <div className="flex-1 flex flex-col min-h-0 bg-neutral-900 overflow-hidden text-neutral-100">
      {/* Header Container */}
      <div className="p-8 pb-0 shrink-0">
        <div className="max-w-2xl mx-auto w-full">
          <PageHeader title="Player list" icon={Users}>
            <div className="flex items-center gap-2 px-3 py-1.5 bg-green-500/10 border border-green-500/20 rounded-full text-green-500 text-[10px] font-bold uppercase tracking-wider">
              <div className="w-1.5 h-1.5 rounded-full bg-green-500 animate-pulse" />
              {onlinePlayersBase.filter(p => p.isOnline).length} Online
            </div>
          </PageHeader>

          <div className="relative group mb-4">
            <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-neutral-500 group-focus-within:text-blue-500 transition-colors" />
            <input 
              type="text" 
              placeholder="Search all players..."
              value={searchTerm}
              onChange={(e) => {
                setSearchTerm(e.target.value)
                if (e.target.value.length >= 3) setIsLoading(true)
              }}
              className="w-full bg-neutral-950 border border-neutral-800 rounded-xl pl-12 pr-4 py-3.5 text-white focus:outline-none focus:ring-2 focus:ring-blue-600/20 focus:border-blue-600 transition-all placeholder:text-neutral-700 font-medium text-sm"
            />
            {isLoading && (
              <div className="absolute right-4 top-1/2 -translate-y-1/2">
                <Activity className="w-4 h-4 text-blue-500 animate-spin" />
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Main List Container (Scrollable) */}
      <div className="flex-1 overflow-y-auto custom-scrollbar p-8 pt-4">
        <div className="max-w-2xl mx-auto w-full flex flex-col gap-1.5">
          {isLoading && paginatedPlayers.length === 0 ? (
            <div className="space-y-1.5">
              {[1, 2, 3, 4, 5, 6].map(i => (
                <div key={i} className="h-12 bg-neutral-950/50 border border-neutral-800/50 rounded-lg animate-pulse" />
              ))}
            </div>
          ) : paginatedPlayers.length > 0 ? (
            <>
              <div className="flex flex-col gap-1.5">
                {paginatedPlayers.map(player => (
                  <CharacterListItem 
                    key={player.guid} 
                    character={player} 
                    onClick={() => handleSelect(player.guid)} 
                  />
                ))}
              </div>

              {/* Global Search Hint */}
              {searchTerm.length > 0 && searchTerm.length < 3 && (
                <div className="mt-4 px-4 py-3 bg-neutral-900/50 border border-neutral-800 border-dashed rounded-xl flex items-center justify-center gap-3">
                  <p className="text-blue-400 text-[10px] font-bold uppercase tracking-widest flex items-center gap-2">
                    <Info className="w-4 h-4" />
                    Type {3 - searchTerm.length} more character{3 - searchTerm.length > 1 ? 's' : ''} to search the global player database
                  </p>
                </div>
              )}
            </>
          ) : (
            <div className="py-12 flex flex-col items-center justify-center bg-neutral-950/30 border border-neutral-800 border-dashed rounded-2xl text-center">
              <Search className="w-8 h-8 text-neutral-800 mb-3" />
              <p className="text-neutral-600 text-sm font-medium">
                {searchTerm.length === 0 ? (
                  <span className="text-neutral-500 flex items-center gap-2">
                    <Users className="w-4 h-4 opacity-50" />
                    No players online. Use search to find offline players.
                  </span>
                ) : (
                  `No players found matching "${searchTerm}"`
                )}
              </p>
            </div>
          )}

          {/* Result Count Status */}
          {debouncedSearchTerm.length >= 3 && searchResults.length >= 100 && (
            <div className="mt-6 px-4 py-3 bg-neutral-950 border border-neutral-800 border-dashed rounded-xl flex items-center justify-center gap-3">
              <div className="w-1.5 h-1.5 rounded-full bg-blue-500 animate-pulse" />
              <p className="text-neutral-500 text-[10px] font-bold uppercase tracking-widest">
                Showing top 100 results - Narrow search for more...
              </p>
            </div>
          )}
        </div>
      </div>

      {/* Pagination Footer */}
      {totalPages > 1 && (
        <div className="shrink-0 border-t border-neutral-800 bg-neutral-950/50 backdrop-blur-xl p-4 mt-auto">
          <div className="max-w-2xl mx-auto flex items-center justify-between">
            <div className="text-[10px] text-neutral-500 font-bold uppercase tracking-widest">
              Page {currentPage} of {totalPages}
            </div>
            <div className="flex items-center gap-2">
              <button 
                onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
                disabled={currentPage === 1}
                className="p-1.5 rounded-lg bg-neutral-900 border border-neutral-800 text-neutral-400 hover:text-white disabled:opacity-30 disabled:pointer-events-none transition-all"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              <button 
                onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
                disabled={currentPage === totalPages}
                className="p-1.5 rounded-lg bg-neutral-900 border border-neutral-800 text-neutral-400 hover:text-white disabled:opacity-30 disabled:pointer-events-none transition-all"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
