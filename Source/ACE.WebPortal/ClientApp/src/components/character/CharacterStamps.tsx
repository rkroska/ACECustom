import { useState, useEffect, useMemo } from 'react'
import { Search, ScrollText, Activity, Clipboard, Check, ChevronLeft, ChevronRight } from 'lucide-react'
import { api } from '../../services/api'

interface CharacterStampsProps {
  guid: number
}

const ITEMS_PER_PAGE = 100

export default function CharacterStamps({ guid }: CharacterStampsProps) {
  const [stamps, setStamps] = useState<string[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [searchTerm, setSearchTerm] = useState('')
  const [currentPage, setCurrentPage] = useState(1)
  const [isCopied, setIsCopied] = useState(false)

  useEffect(() => {
    let isAborted = false
    fetchStamps(isAborted)
    return () => { isAborted = true }
  }, [guid])

  // Reset to first page when search or character changes
  useEffect(() => {
    setCurrentPage(1)
  }, [searchTerm, guid])

  const fetchStamps = async (isAborted: boolean) => {
    try {
      setIsLoading(true)
      const data = await api.get<string[]>(`/api/character/stamps/${guid}`)
      if (!isAborted) {
        setStamps(data)
      }
    } catch (err) {
      if (!isAborted) {
        console.error(err)
      }
    } finally {
      if (!isAborted) {
        setIsLoading(false)
      }
    }
  }

  const filteredStamps = useMemo(() => 
    stamps.filter(s => s.toLowerCase().includes(searchTerm.toLowerCase())),
    [stamps, searchTerm]
  )

  const totalPages = Math.ceil(filteredStamps.length / ITEMS_PER_PAGE)
  const paginatedStamps = filteredStamps.slice(
    (currentPage - 1) * ITEMS_PER_PAGE,
    currentPage * ITEMS_PER_PAGE
  )

  const handleCopyAll = async () => {
    if (filteredStamps.length === 0) return
    
    try {
      const textToCopy = filteredStamps.join('\n')
      await navigator.clipboard.writeText(textToCopy)
      setIsCopied(true)
      setTimeout(() => setIsCopied(false), 2000)
    } catch (err) {
      console.error('Failed to copy stamps:', err)
    }
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Activity className="w-8 h-8 text-blue-500 animate-spin opacity-50" />
      </div>
    )
  }

  return (
    <div className="space-y-6 max-w-2xl mx-auto">
      {/* Header Controls */}
      <div className="flex flex-col md:flex-row items-center gap-4 bg-neutral-950/50 p-4 border border-neutral-800 rounded-2xl shadow-xl">
        <div className="relative flex-1 w-full">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-neutral-500" />
          <input 
            type="text"
            placeholder="Search stamps..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full bg-neutral-900 border border-neutral-800 rounded-xl py-2 pl-10 pr-4 text-sm text-white placeholder-neutral-600 focus:outline-none focus:border-blue-500/50 transition-all"
          />
        </div>
        
        <div className="flex items-center gap-4 shrink-0 transition-all">
          <button
            onClick={handleCopyAll}
            disabled={filteredStamps.length === 0}
            className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-black uppercase tracking-wider transition-all border ${
              isCopied 
                ? 'bg-green-500/10 text-green-500 border-green-500/20' 
                : 'bg-neutral-900 text-neutral-400 hover:text-white border-neutral-800 hover:border-neutral-700'
            } disabled:opacity-30 disabled:cursor-not-allowed`}
          >
            {isCopied ? (
              <><Check className="w-3.5 h-3.5" /> Copied!</>
            ) : (
              <><Clipboard className="w-3.5 h-3.5" /> Copy ({filteredStamps.length})</>
            )}
          </button>

          <div className="flex flex-col items-end pr-2">
            <span className="text-[9px] font-black uppercase tracking-widest text-neutral-600 leading-none mb-1">Total</span>
            <span className="text-xl font-black text-white leading-none tabular-nums">{stamps.length}</span>
          </div>
        </div>
      </div>

      {/* Stamps List */}
      <div className="bg-neutral-950/30 border border-neutral-800/50 rounded-2xl overflow-hidden shadow-2xl backdrop-blur-sm">
        <div className="flex flex-col">
          {paginatedStamps.map((stamp, index) => (
            <div 
              key={index} 
              className="flex items-center gap-3 px-4 py-2 hover:bg-white/[0.02] border-b border-neutral-800/10 last:border-0 group transition-colors"
            >
              <ScrollText className="w-3.5 h-3.5 text-neutral-700 group-hover:text-blue-500/50 transition-colors shrink-0" />
              <span className="text-xs font-medium text-neutral-400 group-hover:text-neutral-100 truncate tracking-tight transition-colors">
                {stamp}
              </span>
            </div>
          ))}
        </div>

        {filteredStamps.length === 0 && (
          <div className="text-center py-24">
            <ScrollText className="w-12 h-12 mx-auto mb-4 text-neutral-800 opacity-20" />
            <p className="text-neutral-500 font-medium tracking-tight">No stamps matching your search</p>
          </div>
        )}
      </div>

      {/* Pagination Controls */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-6 pt-4">
          <button
            onClick={() => setCurrentPage(prev => Math.max(1, prev - 1))}
            disabled={currentPage === 1}
            className="p-2 rounded-xl bg-neutral-900 border border-neutral-800 text-neutral-400 hover:text-white hover:border-neutral-700 disabled:opacity-20 disabled:cursor-not-allowed transition-all"
          >
            <ChevronLeft className="w-5 h-5" />
          </button>
          
          <div className="flex flex-col items-center">
            <span className="text-[10px] font-black uppercase tracking-widest text-neutral-600 mb-0.5">Page</span>
            <span className="text-sm font-black text-neutral-300 tabular-nums">
              {currentPage} <span className="text-neutral-600 mx-1">/</span> {totalPages}
            </span>
          </div>

          <button
            onClick={() => setCurrentPage(prev => Math.min(totalPages, prev + 1))}
            disabled={currentPage === totalPages}
            className="p-2 rounded-xl bg-neutral-900 border border-neutral-800 text-neutral-400 hover:text-white hover:border-neutral-700 disabled:opacity-20 disabled:cursor-not-allowed transition-all"
          >
            <ChevronRight className="w-5 h-5" />
          </button>
        </div>
      )}
    </div>
  )
}
