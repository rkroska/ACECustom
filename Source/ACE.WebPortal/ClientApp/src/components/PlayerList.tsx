import { useState, useEffect, useMemo, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search, Activity, Users, ShieldAlert, MapPin } from 'lucide-react'
import { api } from '../services/api'
import { Character } from '../types'
import { useDebounce } from '../hooks/useDebounce'
import CharacterListItem from './common/CharacterListItem'
import PageHeader from './common/PageHeader'
import SearchHint from './common/SearchHint'
import Pagination from './common/Pagination'
import PlayerLocationGroup from './admin/PlayerLocationGroup'

const ITEMS_PER_PAGE = 25

interface LocationGroup {
  key: string;
  title: string;
  players: Character[];
  categoryOrdinal: number;
  adminCount: number;
  variation: number | null;
  landblock: number;
}

export default function PlayerList() {
  const navigate = useNavigate()
  const [onlinePlayersBase, setOnlinePlayersBase] = useState<Character[]>([])
  const [searchResults, setSearchResults] = useState<Character[]>([])
  const [searchTerm, setSearchTerm] = useState('')
  const debouncedSearchTerm = useDebounce(searchTerm, 500)
  const [currentPage, setCurrentPage] = useState(1)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  
  // Grouping state - Persisted to localStorage
  const [isGroupedByLocation, setIsGroupedByLocation] = useState(() => {
    return localStorage.getItem('ace_admin_grouped_players') === 'true'
  })
  const latestSearchIdRef = useRef(0)

  // Persist grouping choice
  useEffect(() => {
    localStorage.setItem('ace_admin_grouped_players', isGroupedByLocation.toString())
  }, [isGroupedByLocation])

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

  // Reset pagination immediately when search changes
  useEffect(() => {
    setCurrentPage(1)
  }, [searchTerm])

  // Unified Search Logic using Debounce Hook
  useEffect(() => {
    if (debouncedSearchTerm.length >= 3) {
      searchAllPlayers(debouncedSearchTerm)
    } else {
      setSearchResults([])
      // If we're not searching the global DB, we aren't loading anymore
      setIsLoading(false)
    }
  }, [debouncedSearchTerm])

  const searchAllPlayers = async (name: string) => {
    const requestId = ++latestSearchIdRef.current
    try {
      setIsLoading(true)
      setSearchResults([]) // Clear existing results while searching to show loading state
      const data = await api.get<Character[]>(`/api/character/search-all/${encodeURIComponent(name)}`)
      
      // Ignore response if a newer request has been started
      if (requestId !== latestSearchIdRef.current) return

      const onlineGuids = new Set(onlinePlayersBase.map(p => p.guid))
      setSearchResults((data ?? []).map(p => ({ 
        ...p, 
        isOnline: onlineGuids.has(p.guid) 
      })))
    } catch (err) {
      if (requestId === latestSearchIdRef.current) {
        console.error('Unified search error:', err)
      }
    } finally {
      if (requestId === latestSearchIdRef.current) {
        setIsLoading(false)
      }
    }
  }

  // Derived list based on search term
  const effectivePlayersList = useMemo(() => {
    // Mode switch should be instantaneous (use raw searchTerm)
    if (searchTerm.length >= 3) {
      return searchResults
    }
    
    // Otherwise, use local filtering on online players
    if (!searchTerm.trim()) return onlinePlayersBase
    
    const lowerSearch = searchTerm.toLowerCase()
    return onlinePlayersBase.filter(p => p.name.toLowerCase().includes(lowerSearch))
  }, [onlinePlayersBase, searchResults, searchTerm])

  // Explicitly override grouping when search is active
  const isActuallyGrouping = isGroupedByLocation && !searchTerm.trim();

  const totalPages = Math.ceil(effectivePlayersList.length / ITEMS_PER_PAGE)
  // Pagination: only applies if not grouping
  const paginatedPlayers = useMemo(() => {
    if (isActuallyGrouping) return effectivePlayersList
    
    return effectivePlayersList.slice(
      (currentPage - 1) * ITEMS_PER_PAGE,
      currentPage * ITEMS_PER_PAGE
    )
  }, [effectivePlayersList, currentPage, isActuallyGrouping])

  const handleSelect = (guid: number) => {
    navigate(`/players/${guid}/general`)
  }

  // Header action toggle
  const toggleGrouping = () => {
    setIsGroupedByLocation(!isGroupedByLocation)
    setCurrentPage(1) // Reset to first page when toggling grouping for simplicity
  }

  // Unused locationKeys and resolvedNames removed as location is now embedded.

  // Grouped logic - Hierarchical Category -> Location
    /**
     * Hierarchical Location Grouping Logic
     * ------------------------------------
     * Players are organized in a two-tier hierarchy:
     * 1. Category (sorted by ordinal: Special > Outdoors > Dungeons).
     * 2. Location (sorted by player count, then title).
     * 
     * Technical Consideration: Dungeons (Category 3) are grouped by their 16-bit
     * normalized landblock ID. This ensures players in the same dungeon area stay
     * together even if they are in different specific cells.
     */
  const groupedCategories = useMemo(() => {
    if (!isActuallyGrouping) return null

    const categoryMap: Record<string, { name: string, ordinal: number, locationGroups: Record<string, LocationGroup> }> = {}

    paginatedPlayers.forEach((p: Character) => {
        const loc = p.location
        const catName = loc?.categoryName || "Unknown"
        const catOrdinal = loc?.categoryOrdinal ?? 99
        let groupTitle = loc?.name || loc?.hex || "Unknown Location"
        const variation = loc?.variation ?? null
        const landblock = loc?.landblock ?? 0
        
        /**
         * BITWISE SAFETY: Unsigned Right Shift (>>>)
         * We use >>> instead of >> for all landblock ID manipulations.
         * JavaScript bitwise operators default to signed 32-bit integers; using >>>
         * treats the ID as an unsigned value, preventing negative hex display bugs.
         */
        const normalizedLandblock = (landblock >>> 16) || (landblock & 0xFFFF)
        
        /**
         * VARIATION LABELING: (v: N)
         * If a location has a variation (e.g., specific dungeon wings or apartment floors),
         * we append a "v: N" suffix to the title for administrative clarity.
         */
        if (variation !== null) {
            groupTitle = `${groupTitle} v: ${variation}`
        }
        
        if (!categoryMap[catName]) {
            categoryMap[catName] = { 
                name: catName, 
                ordinal: catOrdinal,
                locationGroups: {}
            }
        }

        // For Dungeons (Category 3), include landblock and variation in the key to keep unknown dungeons split
        let groupKey = `${catOrdinal}_${groupTitle}`
        if (catOrdinal === 3) {
            groupKey = `${catOrdinal}_${normalizedLandblock}_${variation ?? 'null'}_${groupTitle}`
        }
        
        if (!categoryMap[catName].locationGroups[groupKey]) {
            categoryMap[catName].locationGroups[groupKey] = {
                key: groupKey,
                title: groupTitle,
                players: [],
                categoryOrdinal: catOrdinal,
                adminCount: 0,
                variation: variation,
                landblock: normalizedLandblock
            }
        }

        const group = categoryMap[catName].locationGroups[groupKey]
        group.players.push(p)
        if (p.isAdmin) group.adminCount++
    })

    // Convert to sorted array
    return Object.values(categoryMap)
        .sort((a, b) => a.ordinal - b.ordinal)
        .map(cat => ({
            categoryName: cat.name,
            categoryOrdinal: cat.ordinal,
            // WITHIN CATEGORIES: Sort locations by total player count DESCENDING
            locations: Object.values(cat.locationGroups).sort((a, b) => b.players.length - a.players.length)
        }))
  }, [isActuallyGrouping, paginatedPlayers])

  if (error) {
    return (
      <div className="flex-1 flex flex-col items-center justify-center p-8 text-center bg-neutral-900 text-neutral-100">
        <ShieldAlert className="w-12 h-12 text-red-500 mb-4 opacity-50" />
        <h2 className="font-bold text-white mb-2 uppercase tracking-widest text-[10px]">Access Restricted</h2>
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
            <div className="flex items-center gap-2">
              <button
                onClick={toggleGrouping}
                className={`px-3 py-1.5 rounded-full text-[10px] font-bold uppercase tracking-wider transition-all border flex items-center gap-1.5 ${
                    isGroupedByLocation 
                      ? 'bg-blue-500/10 border-blue-500/20 text-blue-500' 
                      : 'bg-neutral-800 border-neutral-700 text-neutral-400 hover:bg-neutral-700 hover:text-neutral-200'
                }`}
              >
                <MapPin className="w-3 h-3" />
                {isGroupedByLocation ? 'Grouped by Area' : 'No Grouping'}
              </button>
              <div className="flex items-center gap-2 px-3 py-1.5 bg-green-500/10 border border-green-500/20 rounded-full text-green-500 text-[10px] font-bold uppercase tracking-wider">
                <div className="w-1.5 h-1.5 rounded-full bg-green-500 animate-pulse" />
                {onlinePlayersBase.length} Online
              </div>
            </div>
          </PageHeader>

          <div className="relative group mb-4">
            <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-neutral-500 group-focus-within:text-blue-500 transition-colors" />
            <input 
              type="text" 
              placeholder="Search all players..."
              value={searchTerm}
              onChange={(e) => {
                const val = e.target.value
                setSearchTerm(val)
                if (val.length >= 3) {
                  setIsLoading(true)
                } else {
                  // Instant mode switch back to online list
                  setIsLoading(false)
                }
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
          {isLoading ? (
            <div className="py-20 flex flex-col items-center justify-center animate-in fade-in duration-500">
              <Activity className="w-8 h-8 text-blue-500 animate-spin mb-4" />
              <p className="text-neutral-500 text-xs font-bold uppercase tracking-widest">Searching Global Database...</p>
            </div>
          ) : paginatedPlayers.length === 0 ? (
            searchTerm.length > 0 && searchTerm.length < 3 ? (
              <SearchHint searchTerm={searchTerm} />
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
            )
          ) : (
            <>
              <div className="flex flex-col gap-6">
                {isActuallyGrouping && groupedCategories ? (
                    groupedCategories.map((cat) => (
                      <div key={cat.categoryName} className="flex flex-col gap-1.5">
                        {/* Category Header/Divider */}
                        <div className="flex items-center gap-4 px-2 mb-1">
                          <span className="text-[10px] font-black uppercase tracking-[0.2em] text-neutral-600 whitespace-nowrap">
                            {cat.categoryName}
                          </span>
                          <div className="h-px w-full bg-neutral-800/50" />
                        </div>

                        {/* Location Groups */}
                        <div className="flex flex-col gap-1.5">
                          {cat.locations.map((group) => (
                             <PlayerLocationGroup 
                                key={group.key}
                                title={group.title}
                                players={group.players}
                                adminCount={group.adminCount}
                                categoryOrdinal={group.categoryOrdinal}
                                variation={group.variation}
                                landblock={group.landblock}
                                onSelect={handleSelect}
                             />
                          ))}
                        </div>
                      </div>
                    ))
                ) : (
                    paginatedPlayers.map((player: Character) => {
                      const loc = player.location
                      return (
                        <CharacterListItem 
                          key={player.guid} 
                          character={player} 
                          onClick={() => handleSelect(player.guid)} 
                          locationInfo={!isActuallyGrouping && loc ? (loc.name || loc.hex) : undefined}
                          secondaryInfo={!isActuallyGrouping && loc ? loc.coordinates : undefined}
                        />
                      )
                    })
                )}
              </div>

              <SearchHint searchTerm={searchTerm} />
            </>
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

      {/* Pagination Footer - only if not grouping */}
      {!isActuallyGrouping && (
          <Pagination 
            currentPage={currentPage} 
            totalPages={totalPages} 
            onPageChange={setCurrentPage} 
          />
      )}
    </div>
  )
}
