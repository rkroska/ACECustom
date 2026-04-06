import { useState, useEffect, useMemo } from 'react'
import { Search, Filter, Settings } from 'lucide-react'
import { api } from '../services/api'
import { ServerParamMetadata } from '../types'
import ServerParamListItem from './admin/ServerParamListItem'
import PageHeader from './common/PageHeader'

const ServerParams = () => {
  const [params, setParams] = useState<ServerParamMetadata[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [search, setSearch] = useState('')
  const [selectedType, setSelectedType] = useState<string | null>(null)
  const [copiedText, setCopiedText] = useState<string | null>(null)

  useEffect(() => {
    const fetchParams = async () => {
      try {
        setIsLoading(true)
        const data = await api.get<ServerParamMetadata[]>('/api/serverparam/list')
        setParams(data)
      } catch (err) {
        console.error('Failed to fetch server params', err)
      } finally {
        setIsLoading(false)
      }
    }
    fetchParams()
  }, [])

  const categories = useMemo(() => {
    return Array.from(new Set(params.map(p => p.type))).sort()
  }, [params])

  const filteredParams = useMemo(() => {
    return params.filter(p => {
      const matchesSearch = p.name.toLowerCase().includes(search.toLowerCase()) || 
                          p.description.toLowerCase().includes(search.toLowerCase())
      const matchesType = !selectedType || p.type === selectedType
      return matchesSearch && matchesType
    })
  }, [params, search, selectedType])

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
          <div className="text-neutral-500 text-sm font-medium uppercase tracking-widest animate-pulse">Fetching server parameters...</div>
        </div>
      </div>
    )
  }

  return (
    <div className="flex flex-col h-full bg-neutral-950 p-8 overflow-hidden text-glow-container">
      <PageHeader title="Server Parameters" icon={Settings} className="shrink-0" />

      <div className="flex flex-col gap-4">
        <div className="flex flex-col sm:flex-row gap-4">
          <div className="relative flex-1">
            <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-neutral-600" />
            <input 
              type="text"
              placeholder="Search by parameter name or description..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full bg-neutral-900 border border-neutral-800 rounded-2xl pl-12 pr-4 py-3 text-sm text-white placeholder-neutral-600 focus:outline-none focus:ring-2 focus:ring-blue-600/50 transition-all shadow-inner font-medium"
            />
          </div>
          
          <div className="flex gap-2 overflow-x-auto pb-2 custom-scrollbar no-scrollbar text-[10px] font-bold uppercase tracking-widest">
            <button 
              onClick={() => setSelectedType(null)}
              className={`px-4 py-3 rounded-xl transition-all duration-200 whitespace-nowrap border ${
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
                className={`px-4 py-3 rounded-xl transition-all duration-200 whitespace-nowrap border ${
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
      </div>

      <div className="flex-1 overflow-y-auto custom-scrollbar bg-neutral-900/30 border border-neutral-800/50 rounded-3xl overflow-hidden shadow-2xl">
        <div className="min-w-full divide-y divide-neutral-800/50">
          <div className="grid grid-cols-[1fr_120px_2fr_140px_220px] gap-4 px-6 py-4 bg-neutral-900/50 text-[10px] font-bold text-neutral-500 uppercase tracking-[0.2em]">
            <span>Parameter Variable</span>
            <span>Metadata</span>
            <span>Description</span>
            <span>Current Status</span>
            <span className="text-right">Actions</span>
          </div>
          
          <div className="divide-y divide-neutral-800/20">
            {filteredParams.map(p => (
              <ServerParamListItem 
                key={p.name} 
                p={p} 
                copiedText={copiedText} 
                copyToClipboard={copyToClipboard} 
              />
            ))}
          </div>

          {filteredParams.length === 0 && (
            <div className="flex flex-col items-center justify-center py-20 text-neutral-600 text-[10px] font-bold uppercase tracking-widest bg-black/10">
              <Filter className="w-12 h-12 mb-4 opacity-10" />
              <p>No parameters found matching your search</p>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

export default ServerParams
