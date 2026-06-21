import { useEffect, useState } from 'react'
import { Search, Skull, Copy, MapPin, Loader, AlertTriangle, ChevronUp, ChevronDown, ArrowUpDown } from 'lucide-react'
import { api } from '../services/api'
import { formatTimeToRot, positionClipboard, AcePosition } from './character/CharacterStatus'

interface CorpseRow {
  corpseId: number
  characterId: number
  characterName: string
  name: string
  position: AcePosition | null
  timeToRotSeconds: number | null
  creationTimestamp: number | null
  killerId: number | null
  isLoaded: boolean
}

export default function CorpseFinder() {
  const [query, setQuery] = useState('')
  const [corpses, setCorpses] = useState<CorpseRow[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [copiedId, setCopiedId] = useState<number | null>(null)
  const [sortField, setSortField] = useState<string | null>(null)
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc')

  const handleSort = (field: string) => {
    if (sortField === field) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc')
    } else {
      setSortField(field)
      setSortDirection('asc')
    }
  }

  const sortedCorpses = [...corpses].sort((a, b) => {
    if (!sortField) return 0

    type SortableValue = string | number | boolean | AcePosition | null | undefined;
    let aVal: SortableValue = a[sortField as keyof CorpseRow]
    let bVal: SortableValue = b[sortField as keyof CorpseRow]

    if (sortField === 'location') {
      aVal = a.position?.description ?? ''
      bVal = b.position?.description ?? ''
    }

    if (aVal === bVal) return 0

    if (aVal == null) return sortDirection === 'asc' ? 1 : -1
    if (bVal == null) return sortDirection === 'asc' ? -1 : 1

    if (typeof aVal === 'string' && typeof bVal === 'string') {
      return sortDirection === 'asc'
        ? aVal.localeCompare(bVal)
        : bVal.localeCompare(aVal)
    }

    if (typeof aVal === 'number' && typeof bVal === 'number') {
      return sortDirection === 'asc'
        ? (aVal > bVal ? 1 : -1)
        : (aVal < bVal ? 1 : -1)
    }

    if (typeof aVal === 'boolean' && typeof bVal === 'boolean') {
      return sortDirection === 'asc'
        ? (aVal ? 1 : -1)
        : (bVal ? 1 : -1)
    }

    return 0;
  })

  const fetchCorpses = async (searchTerm: string) => {
    try {
      setLoading(true)
      setError(null)
      const res = await api.get<CorpseRow[]>(`/api/character/admin/corpses?query=${encodeURIComponent(searchTerm)}`)
      setCorpses(res ?? [])
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to retrieve corpses')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    const timer = setTimeout(() => {
      fetchCorpses(query)
    }, 300)
    return () => clearTimeout(timer)
  }, [query])

  const handleCopy = async (id: number, pos: AcePosition) => {
    try {
      const command = positionClipboard(pos)
      await navigator.clipboard.writeText(command)
      setCopiedId(id)
      setTimeout(() => setCopiedId(null), 2000)
    } catch {
      // ignore
    }
  }

  return (
    <div className="flex-1 flex flex-col min-h-0 overflow-hidden">
      <div className="flex-1 overflow-y-auto custom-scrollbar p-4 sm:p-6 lg:p-8 space-y-6 max-w-7xl mx-auto w-full">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-neutral-800 pb-5">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-3">
            <Skull className="w-7 h-7 text-red-500 animate-pulse" />
            Corpse Finder
          </h1>
          <p className="text-sm text-neutral-400 mt-1">
            Search and locate player corpses across loaded and inactive areas.
          </p>
        </div>
      </div>

      {/* Search Input */}
      <div className="relative max-w-md">
        <span className="absolute inset-y-0 left-0 flex items-center pl-3 pointer-events-none">
          <Search className="w-5 h-5 text-neutral-500" />
        </span>
        <input
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Filter by Character Name..."
          className="w-full pl-10 pr-4 py-2 text-sm bg-neutral-950 border border-neutral-800 rounded-xl text-neutral-200 placeholder-neutral-500 focus:outline-none focus:border-red-500/50 focus:ring-1 focus:ring-red-500/20 transition-all"
        />
      </div>

      {/* Main Panel */}
      {error && (
        <div className="rounded-xl border border-red-500/20 bg-red-500/5 text-red-400 text-sm p-4 flex items-center gap-3">
          <AlertTriangle className="w-5 h-5 flex-shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {loading ? (
        <div className="flex flex-col items-center justify-center py-20 text-neutral-500 gap-3">
          <Loader className="w-8 h-8 animate-spin text-red-500" />
          <span className="text-sm">Scanning world shard for corpses...</span>
        </div>
      ) : corpses.length === 0 ? (
        <div className="text-center py-20 border border-dashed border-neutral-800 rounded-2xl bg-neutral-950/20">
          <Skull className="w-12 h-12 text-neutral-600 mx-auto mb-3" />
          <h3 className="text-sm font-bold text-neutral-300 uppercase tracking-wider">No Corpses Found</h3>
          <p className="text-xs text-neutral-500 mt-1">No matching active or inactive player corpses match the search.</p>
        </div>
      ) : (
        <div className="overflow-hidden rounded-2xl border border-neutral-800 bg-neutral-950/40 backdrop-blur-md">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-sm">
              <thead>
                <tr className="border-b border-neutral-800 bg-neutral-900/40 text-neutral-400 font-semibold text-xs uppercase tracking-wider select-none">
                  <th 
                    className="py-3.5 px-4 cursor-pointer hover:bg-neutral-850 hover:text-neutral-200 transition-colors"
                    onClick={() => handleSort('characterName')}
                  >
                    <div className="flex items-center gap-1.5">
                      <span>Character</span>
                      {sortField === 'characterName' ? (
                        sortDirection === 'asc' ? <ChevronUp className="w-3.5 h-3.5 text-red-400" /> : <ChevronDown className="w-3.5 h-3.5 text-red-400" />
                      ) : (
                        <ArrowUpDown className="w-3 h-3 text-neutral-600" />
                      )}
                    </div>
                  </th>
                  <th 
                    className="py-3.5 px-4 cursor-pointer hover:bg-neutral-850 hover:text-neutral-200 transition-colors"
                    onClick={() => handleSort('name')}
                  >
                    <div className="flex items-center gap-1.5">
                      <span>Corpse Name</span>
                      {sortField === 'name' ? (
                        sortDirection === 'asc' ? <ChevronUp className="w-3.5 h-3.5 text-red-400" /> : <ChevronDown className="w-3.5 h-3.5 text-red-400" />
                      ) : (
                        <ArrowUpDown className="w-3 h-3 text-neutral-600" />
                      )}
                    </div>
                  </th>
                  <th 
                    className="py-3.5 px-4 cursor-pointer hover:bg-neutral-850 hover:text-neutral-200 transition-colors"
                    onClick={() => handleSort('location')}
                  >
                    <div className="flex items-center gap-1.5">
                      <span>Location</span>
                      {sortField === 'location' ? (
                        sortDirection === 'asc' ? <ChevronUp className="w-3.5 h-3.5 text-red-400" /> : <ChevronDown className="w-3.5 h-3.5 text-red-400" />
                      ) : (
                        <ArrowUpDown className="w-3 h-3 text-neutral-600" />
                      )}
                    </div>
                  </th>
                  <th 
                    className="py-3.5 px-4 cursor-pointer hover:bg-neutral-850 hover:text-neutral-200 transition-colors"
                    onClick={() => handleSort('timeToRotSeconds')}
                  >
                    <div className="flex items-center gap-1.5">
                      <span>Decay Timer</span>
                      {sortField === 'timeToRotSeconds' ? (
                        sortDirection === 'asc' ? <ChevronUp className="w-3.5 h-3.5 text-red-400" /> : <ChevronDown className="w-3.5 h-3.5 text-red-400" />
                      ) : (
                        <ArrowUpDown className="w-3 h-3 text-neutral-600" />
                      )}
                    </div>
                  </th>
                  <th 
                    className="py-3.5 px-4 cursor-pointer hover:bg-neutral-850 hover:text-neutral-200 transition-colors"
                    onClick={() => handleSort('isLoaded')}
                  >
                    <div className="flex items-center gap-1.5">
                      <span>Status</span>
                      {sortField === 'isLoaded' ? (
                        sortDirection === 'asc' ? <ChevronUp className="w-3.5 h-3.5 text-red-400" /> : <ChevronDown className="w-3.5 h-3.5 text-red-400" />
                      ) : (
                        <ArrowUpDown className="w-3 h-3 text-neutral-600" />
                      )}
                    </div>
                  </th>
                  <th className="py-3.5 px-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-neutral-850">
                {sortedCorpses.map((c) => (
                  <tr key={c.corpseId} className="hover:bg-neutral-900/20 transition-colors">
                    {/* Character */}
                    <td className="py-4 px-4">
                      <div className="font-semibold text-neutral-200">{c.characterName}</div>
                      <div className="text-[10px] text-neutral-500 font-mono mt-0.5">GUID: 0x{c.characterId.toString(16)}</div>
                    </td>

                    {/* Corpse Name */}
                    <td className="py-4 px-4">
                      <div className="text-neutral-300">{c.name}</div>
                      <div className="text-[10px] text-neutral-500 font-mono mt-0.5">ID: 0x{c.corpseId.toString(16)}</div>
                    </td>

                    {/* Location */}
                    <td className="py-4 px-4">
                      {c.position?.description ? (
                        <div className="flex items-start gap-1.5">
                          <MapPin className="w-3.5 h-3.5 text-red-400/80 mt-0.5 flex-shrink-0" />
                          <div>
                            <div className="text-neutral-200 font-medium">{c.position.description}</div>
                            <div className="text-[10px] text-neutral-500 font-mono mt-0.5">
                              0x{c.position.cell.toString(16)} [{c.position.positionX.toFixed(1)} {c.position.positionY.toFixed(1)} {c.position.positionZ.toFixed(1)}]
                            </div>
                          </div>
                        </div>
                      ) : (
                        <span className="text-neutral-500 italic">No position data</span>
                      )}
                    </td>

                    {/* Decay Timer */}
                    <td className="py-4 px-4 text-neutral-300 font-mono">
                      {formatTimeToRot(c.timeToRotSeconds)}
                    </td>

                    {/* Status */}
                    <td className="py-4 px-4">
                      <span className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-[10px] font-semibold border ${
                        c.isLoaded 
                          ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/10' 
                          : 'bg-amber-500/10 text-amber-400 border-amber-500/10'
                      }`}>
                        <span className={`w-1.5 h-1.5 rounded-full ${c.isLoaded ? 'bg-emerald-400' : 'bg-amber-400'}`} />
                        {c.isLoaded ? 'Active Area' : 'Inactive (Frozen)'}
                      </span>
                    </td>

                    {/* Actions */}
                    <td className="py-4 px-4 text-right">
                      {c.position && (
                        <button
                          type="button"
                          onClick={() => handleCopy(c.corpseId, c.position!)}
                          className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-neutral-800 hover:bg-neutral-700 text-xs font-medium text-neutral-200 border border-neutral-700 hover:border-neutral-600 transition-all"
                        >
                          <Copy className="w-3.5 h-3.5" />
                          <span>{copiedId === c.corpseId ? 'Copied!' : 'Copy /tele'}</span>
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
      </div>
    </div>
  )
}
