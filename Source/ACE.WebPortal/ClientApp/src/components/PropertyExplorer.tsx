import { useState, useEffect, useMemo } from 'react'
import { Search, Hash, Filter, SortAsc } from 'lucide-react'
import { api } from '../services/api'
import { PropertyMetadata } from '../types'
import { useDebounce } from '../hooks/useDebounce'
import PropertyListItem from './admin/PropertyListItem'
import PageHeader from './common/PageHeader'

interface PropertyExplorerProps {
  navigateToEnum: (name: string) => void
}

type SortField = 'name' | 'id'

const PropertyExplorer = ({ navigateToEnum }: PropertyExplorerProps) => {
  const [properties, setProperties] = useState<PropertyMetadata[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const [selectedType, setSelectedType] = useState<string | null>(null)
  const [sortBy, setSortBy] = useState<SortField>('id')
  const [copiedText, setCopiedText] = useState<string | null>(null)
  const [fetchError, setFetchError] = useState<string | null>(null)

  useEffect(() => {
    const fetchMetadata = async () => {
      try {
        setIsLoading(true)
        setFetchError(null)
        const data = await api.get<PropertyMetadata[]>('/api/property/metadata')
        setProperties(data)
      } catch (err) {
        console.error('Failed to fetch property metadata', err)
        setFetchError(err instanceof Error ? err.message : 'An unexpected error occurred while loading property metadata.')
      } finally {
        setIsLoading(false)
      }
    }
    fetchMetadata()
  }, [])

  const categories = useMemo(() => {
    return Array.from(new Set(properties.map(p => p.type))).sort()
  }, [properties])

  const sortedAndFilteredProperties = useMemo(() => {
    let result = properties.filter(p => {
      const matchesSearch = p.name.toLowerCase().includes(debouncedSearch.toLowerCase()) || 
                          p.id.toString().includes(debouncedSearch)
      const matchesType = !selectedType || p.type === selectedType
      return matchesSearch && matchesType
    })

    result.sort((a, b) => {
      if (sortBy === 'name') {
        return a.name.localeCompare(b.name)
      } else {
        const typeCompare = a.type.localeCompare(b.type)
        if (typeCompare !== 0) return typeCompare
        return a.id - b.id
      }
    })

    return result
  }, [properties, debouncedSearch, selectedType, sortBy])

  const copyToClipboard = (text: string, id: string) => {
    navigator.clipboard.writeText(text)
    setCopiedText(id)
    setTimeout(() => setCopiedText(null), 2000)
  }

  if (isLoading) {
    return (
      <div className="flex-1 flex items-center justify-center min-h-[400px]">
        <div className="flex flex-col items-center justify-center space-y-4">
          <div className="w-12 h-12 border-4 border-blue-600/20 border-t-blue-600 rounded-full animate-spin"></div>
          <div className="text-neutral-500 text-sm font-medium uppercase tracking-widest animate-pulse">Loading properties...</div>
        </div>
      </div>
    )
  }

  return (
    <div className="flex flex-col h-full bg-neutral-950 p-8 overflow-hidden text-glow-container">
      <PageHeader title="Property Explorer" icon={Search} className="shrink-0" />

      <div className="flex flex-col gap-4">
        <div className="flex flex-col sm:flex-row gap-4">
          <div className="relative flex-1">
            <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-neutral-600" />
            <input 
              type="text"
              placeholder="Search by property name or ID..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full bg-neutral-900 border border-neutral-800 rounded-2xl pl-12 pr-4 py-3 text-white placeholder-neutral-600 focus:outline-none focus:ring-2 focus:ring-blue-600/50 transition-all duration-200 shadow-inner font-medium"
            />
          </div>
          
          <div className="flex gap-2">
            <button 
              onClick={() => setSortBy('id')}
              className={`flex items-center gap-2 px-4 py-3 rounded-xl text-xs font-bold uppercase tracking-wider transition-all duration-200 border ${
                sortBy === 'id' 
                ? 'bg-blue-600 text-white border-blue-500 shadow-lg shadow-blue-600/20' 
                : 'bg-neutral-900 text-neutral-500 border-neutral-800 hover:text-neutral-300'
              }`}
            >
              <Hash className="w-4 h-4" />
              Type + ID
            </button>
            <button 
              onClick={() => setSortBy('name')}
              className={`flex items-center gap-2 px-4 py-3 rounded-xl text-xs font-bold uppercase tracking-wider transition-all duration-200 border ${
                sortBy === 'name' 
                ? 'bg-blue-600 text-white border-blue-500 shadow-lg shadow-blue-600/20' 
                : 'bg-neutral-900 text-neutral-500 border-neutral-800 hover:text-neutral-300'
              }`}
            >
              <SortAsc className="w-4 h-4" />
              Name
            </button>
          </div>
        </div>

        <div className="flex gap-2 overflow-x-auto pb-2 custom-scrollbar no-scrollbar text-[10px] font-bold uppercase tracking-widest">
           <button 
             onClick={() => setSelectedType(null)}
             className={`px-4 py-2.5 rounded-xl transition-all duration-200 whitespace-nowrap border ${
               selectedType === null 
               ? 'bg-neutral-800 text-white border-neutral-700' 
               : 'bg-neutral-900/50 text-neutral-500 border-neutral-800/50 hover:text-neutral-300'
             }`}
           >
             All Types
           </button>
           {categories.map(cat => (
             <button 
               key={cat}
               onClick={() => setSelectedType(cat)}
               className={`px-4 py-2.5 rounded-xl transition-all duration-200 whitespace-nowrap border ${
                 selectedType === cat 
                 ? 'bg-neutral-800 text-white border-neutral-700' 
                 : 'bg-neutral-900/50 text-neutral-500 border-neutral-800/50 hover:text-neutral-300'
               }`}
             >
               {cat}
             </button>
           ))}
        </div>
      </div>

      <div className="flex-1 overflow-y-auto custom-scrollbar bg-neutral-900/30 border border-neutral-800/50 rounded-3xl overflow-hidden shadow-2xl">
        <div className="min-w-full divide-y divide-neutral-800/50">
          <div className="grid grid-cols-[160px_80px_1fr_200px] gap-4 px-6 py-4 bg-neutral-900/50 text-[10px] font-bold text-neutral-500 uppercase tracking-[0.2em]">
            <span>Type</span>
            <span>ID</span>
            <span>Property Name</span>
            <span className="text-right">Actions</span>
          </div>
          
          <div className="divide-y divide-neutral-800/20">
            {sortedAndFilteredProperties.map(prop => (
              <PropertyListItem 
                key={`${prop.type}-${prop.id}`} 
                prop={prop} 
                copiedText={copiedText} 
                copyToClipboard={copyToClipboard}
                navigateToEnum={navigateToEnum}
              />
            ))}
          </div>

          {fetchError && (
            <div className="flex flex-col items-center justify-center py-20 text-red-500/80 bg-red-500/5">
              <div className="w-16 h-16 rounded-3xl bg-red-500/10 flex items-center justify-center mb-6 border border-red-500/20">
                <Filter className="w-8 h-8 opacity-50" />
              </div>
              <p className="text-[10px] font-black uppercase tracking-[0.2em] mb-2">Metadata Fetch Failed</p>
              <p className="text-xs font-medium text-neutral-500 max-w-sm text-center px-4 leading-relaxed">{fetchError}</p>
            </div>
          )}

          {!fetchError && sortedAndFilteredProperties.length === 0 && (
            <div className="flex flex-col items-center justify-center py-20 text-neutral-600 text-[10px] font-bold uppercase tracking-widest bg-black/10">
              <Filter className="w-12 h-12 mb-4 opacity-10" />
              <p>No matching properties found.</p>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

export default PropertyExplorer
