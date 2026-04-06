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
  const [error, setError] = useState<string | null>(null)

  const fetchParams = async (signal?: AbortSignal) => {
    try {
      setIsLoading(true)
      setError(null)
      const data = await api.get<ServerParamMetadata[]>('/api/serverparam/list', { signal })
      setParams(data ?? [])
    } catch (err) {
      if (err instanceof Error && err.name === 'AbortError') return
      console.error('Failed to fetch server params', err)
      setError(err instanceof Error ? err.message : 'Failed to load parameters from server.')
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    const controller = new AbortController()
    fetchParams(controller.signal)
    return () => controller.abort()
  }, [])

  const categories = useMemo(() => {
    return Array.from(new Set(params.map(p => p.type))).sort()
  }, [params])

  const filteredParams = useMemo(() => {
    const lowerSearch = search.toLowerCase()
    return params.filter(p => {
      const matchesSearch = p.name.toLowerCase().includes(lowerSearch) || 
                          (p.description || '').toLowerCase().includes(lowerSearch)
      const matchesType = !selectedType || p.type === selectedType
      return matchesSearch && matchesType
    })
  }, [params, search, selectedType])

  const copyToClipboard = (text: string, id: string) => {
    navigator.clipboard.writeText(text)
      .then(() => {
        setCopiedText(id)
        setTimeout(() => setCopiedText(null), 2000)
      })
      .catch(err => console.error('Clipboard copy failed:', err))
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

  if (error) {
    return (
      <div className="flex-1 flex items-center justify-center min-h-[400px] bg-neutral-950 p-8">
        <div className="max-w-md w-full bg-neutral-900 border border-red-500/20 rounded-3xl p-8 text-center shadow-2xl">
          <div className="w-16 h-16 bg-red-500/10 border border-red-500/20 rounded-2xl flex items-center justify-center mx-auto mb-6">
            <Settings className="w-8 h-8 text-red-500/50" />
          </div>
          <h2 className="text-xl font-black text-white uppercase tracking-tight mb-2">Sync Failed</h2>
          <p className="text-neutral-500 text-sm font-medium mb-8 leading-relaxed">
            {error}
          </p>
          <button 
            onClick={() => fetchParams()}
            className="w-full bg-neutral-800 hover:bg-neutral-700 text-white font-bold py-4 rounded-xl transition-all active:scale-[0.98] border border-neutral-700 shadow-lg"
          >
            Retry Request
          </button>
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
