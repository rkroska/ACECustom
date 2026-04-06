import { useState, useEffect, useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Search, Filter, Layers, Link as LinkIcon, Database, ChevronLeft, ChevronRight } from 'lucide-react'
import { api } from '../services/api'
import { EnumListItem, EnumDetail, EnumValueMetadata } from '../types'
import { useDebounce } from '../hooks/useDebounce'
import EnumIndexItem from './admin/EnumIndexItem'
import EnumValueListItem from './admin/EnumValueListItem'
import PageHeader from './common/PageHeader'
import TypePill from './common/TypePill'

const ITEMS_PER_PAGE = 50

const LookupTables = () => {
  const [searchParams, setSearchParams] = useSearchParams()
  const selectedEnum = searchParams.get('enum')
  
  const [enumList, setEnumList] = useState<EnumListItem[]>([])
  const [enumDetail, setEnumDetail] = useState<EnumDetail | null>(null)
  const [isLoadingList, setIsLoadingList] = useState(true)
  const [isLoadingValues, setIsLoadingValues] = useState(false)
  const [listError, setListError] = useState<string | null>(null)
  const [detailError, setDetailError] = useState<string | null>(null)
  
  const [listSearch, setListSearch] = useState('')
  const debouncedListSearch = useDebounce(listSearch, 300)
  
  const [valueSearch, setValueSearch] = useState('')
  const debouncedValueSearch = useDebounce(valueSearch, 300)
  
  const [currentPage, setCurrentPage] = useState(1)
  const [retryKey, setRetryKey] = useState(0)
  const [copiedText, setCopiedText] = useState<string | null>(null)

  const setSelectedEnum = (name: string | null) => {
    setCurrentPage(1)
    if (name) {
      setSearchParams({ enum: name })
    } else {
      setSearchParams({})
    }
  }

  // Fetch the master list of enums
  useEffect(() => {
    const fetchEnumList = async () => {
      try {
        setListError(null)
        const data = await api.get<EnumListItem[]>('/api/enum/list')
        setEnumList(data ?? [])
      } catch (err) {
        console.error('Failed to fetch enum list', err)
        setListError(err instanceof Error ? err.message : 'Failed to load index.')
      } finally {
        setIsLoadingList(false)
      }
    }
    fetchEnumList()
  }, [])

  // Fetch the details for a specific enum when selected
  useEffect(() => {
    if (!selectedEnum) {
      setEnumDetail(null)
      return
    }

    let isAborted = false

    const fetchEnumDetail = async () => {
      try {
        setIsLoadingValues(true)
        setDetailError(null)
        const data = await api.get<EnumDetail>(`/api/enum/detail/${selectedEnum}`)
        if (!isAborted) {
          setEnumDetail(data)
        }
      } catch (err) {
        if (!isAborted) {
          console.error('Failed to fetch enum detail', err)
          setDetailError(err instanceof Error ? err.message : 'Failed to load table details.')
        }
      } finally {
        if (!isAborted) {
          setIsLoadingValues(false)
        }
      }
    }
    fetchEnumDetail()

    return () => { isAborted = true }
  }, [selectedEnum, retryKey])

  const copyToClipboard = (text: string, id: string) => {
    navigator.clipboard.writeText(text)
    setCopiedText(id)
    setTimeout(() => setCopiedText(null), 2000)
  }

  const filteredEnumList = useMemo(() => {
    return enumList.filter(e => e.name.toLowerCase().includes(debouncedListSearch.toLowerCase()))
  }, [enumList, debouncedListSearch])

  const filteredValues = useMemo(() => {
    if (!enumDetail) return []
    const search = debouncedValueSearch.toLowerCase()
    return enumDetail.values.filter(v => 
      v.name.toLowerCase().includes(search) || 
      v.id.toString().includes(search)
    )
  }, [enumDetail, debouncedValueSearch])

  const totalPages = Math.ceil(filteredValues.length / ITEMS_PER_PAGE)
  const paginatedValues = useMemo(() => {
    return filteredValues.slice(
      (currentPage - 1) * ITEMS_PER_PAGE,
      currentPage * ITEMS_PER_PAGE
    )
  }, [filteredValues, currentPage])

  // Reset page when filtering values
  useEffect(() => {
    setCurrentPage(1)
  }, [debouncedValueSearch])

  const getSetCommandTemplate = (value: EnumValueMetadata) => {
    if (!enumDetail) return ''
    const target = enumDetail.primaryProperty || (selectedEnum?.startsWith('Property') ? selectedEnum : `Property${selectedEnum}`)
    return `/setproperty ${target} ${value.hexValue || value.id.toString()}`
  }

  const getEnumValueCommand = (value: EnumValueMetadata) => {
    if (!enumDetail) return ''
    return `${selectedEnum}.${value.name}`
  }

  return (
    <div className="flex flex-col h-full bg-neutral-950 p-8 overflow-hidden text-glow-container">
      <PageHeader title="Lookup Tables" icon={Database} className="shrink-0" />

      <div className="flex-1 flex gap-6 overflow-hidden min-h-0">
        {/* Left Sidebar: Enum List */}
        <div className="w-80 flex flex-col bg-neutral-900/40 border border-neutral-800/60 rounded-3xl overflow-hidden shadow-xl">
          <div className="p-4 border-b border-neutral-800/40 space-y-3">
            <div className="flex items-center justify-between">
              <h3 className="text-[10px] font-black text-neutral-500 uppercase tracking-[0.2em] px-1">Master Enum Index</h3>
              <div className="px-2 py-0.5 bg-blue-500/10 rounded text-[9px] font-bold text-blue-500 border border-blue-500/20 uppercase tracking-widest">
                {enumList.length}
              </div>
            </div>
            <div className="relative group">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-neutral-600 group-focus-within:text-blue-500 transition-colors" />
              <input 
                type="text"
                placeholder="Filter index..."
                value={listSearch}
                onChange={(e) => setListSearch(e.target.value)}
                className="w-full bg-neutral-950/50 border border-neutral-800/80 rounded-xl pl-9 pr-3 py-2.5 text-xs text-white placeholder-neutral-700 focus:outline-none focus:ring-1 focus:ring-blue-600/50 transition-all font-medium"
              />
            </div>
          </div>

          <div className="flex-1 overflow-y-auto custom-scrollbar p-2">
            {isLoadingList ? (
              <div className="space-y-1 p-2">
                {[1, 2, 3, 4, 5, 6, 7, 8].map(i => (
                  <div key={i} className="h-9 bg-neutral-800/30 rounded-lg animate-pulse" />
                ))}
              </div>
            ) : listError ? (
              <div className="flex flex-col items-center justify-center py-10 px-4 text-center">
                <div className="w-10 h-10 rounded-xl bg-red-500/10 border border-red-500/20 flex items-center justify-center mb-3">
                  <Database className="w-5 h-5 text-red-500/50" />
                </div>
                <p className="text-[9px] font-black uppercase tracking-[0.2em] text-red-500/70 mb-1">Index Error</p>
                <p className="text-[10px] text-neutral-600 font-medium leading-relaxed">{listError}</p>
              </div>
            ) : filteredEnumList.length > 0 ? (
              <div className="space-y-0.5">
                {filteredEnumList.map(e => (
                  <EnumIndexItem 
                    key={e.name}
                    item={e}
                    isSelected={selectedEnum === e.name}
                    onClick={() => setSelectedEnum(e.name)}
                  />
                ))}
              </div>
            ) : (
              <div className="flex flex-col items-center justify-center h-40 text-neutral-700 text-[10px] font-bold uppercase tracking-widest font-mono">
                <Filter className="w-8 h-8 mb-3 opacity-10" />
                No matches
              </div>
            )}
          </div>
        </div>

        {/* Right Content: Enum Detail */}
        <div className="flex-1 flex flex-col bg-neutral-900/40 border border-neutral-800/60 rounded-3xl overflow-hidden shadow-xl min-w-0">
          {!selectedEnum ? (
            <div className="flex-1 flex flex-col items-center justify-center text-center p-8">
              <div className="w-24 h-24 rounded-full bg-neutral-950 flex items-center justify-center mb-6 border border-neutral-800 shadow-inner group transition-all">
                <Database className="w-10 h-10 text-neutral-800 group-hover:text-blue-600/30 transition-colors" />
              </div>
              <h2 className="text-xl font-black text-white tracking-tight uppercase leading-none mb-3">Select a lookup table</h2>
              <p className="text-neutral-500 text-xs max-w-xs font-medium leading-relaxed">
                Choose a table from the index to view its definitions, underlying types, and available bitmask flags.
              </p>
            </div>
          ) : isLoadingValues ? (
            <div className="flex-1 flex items-center justify-center">
              <div className="flex flex-col items-center gap-4">
                <Layers className="w-10 h-10 text-blue-500/40 animate-pulse" />
                <div className="text-[10px] font-black text-neutral-600 uppercase tracking-[0.2em] animate-pulse">Analyzing Table Schema...</div>
              </div>
            </div>
          ) : detailError ? (
            <div className="flex-1 flex flex-col items-center justify-center text-center p-8 bg-black/10">
              <div className="w-20 h-20 rounded-full bg-red-500/5 flex items-center justify-center mb-6 border border-red-500/10">
                <Database className="w-8 h-8 text-red-500/30" />
              </div>
              <h2 className="text-xl font-black text-white tracking-tight uppercase mb-3">Load Failed</h2>
              <p className="text-neutral-500 text-xs max-w-sm font-medium leading-relaxed mb-6">
                {detailError}
              </p>
              <button 
                onClick={() => setRetryKey(prev => prev + 1)}
                className="px-6 py-2.5 bg-neutral-800 hover:bg-neutral-700 text-white text-[10px] font-bold uppercase tracking-widest rounded-xl transition-all"
              >
                Retry Request
              </button>
            </div>
          ) : enumDetail ? (
            <>
              {/* Detail Header */}
              <div className="shrink-0 p-6 bg-neutral-900/40 border-b border-neutral-800/40">
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-6">
                  <div className="space-y-1.5 min-w-0">
                    <div className="flex items-center gap-3">
                      <h2 className="text-2xl font-bold text-white truncate leading-none">
                        {selectedEnum}
                      </h2>
                    </div>
                    <div className="flex items-center gap-2">
                       <TypePill type={enumDetail.underlyingType} />
                      {enumDetail.isFlags && (
                        <div className="px-3 py-1.5 rounded-lg text-[10px] font-bold uppercase tracking-widest bg-neutral-800/50 text-neutral-400 border border-neutral-700/50">
                          Bitmask Flags
                        </div>
                      )}
                    </div>
                  </div>

                  <div className="relative group min-w-[240px]">
                    <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-neutral-600 group-focus-within:text-blue-500 transition-colors" />
                    <input 
                      type="text"
                      placeholder="Filter table values..."
                      value={valueSearch}
                      onChange={(e) => setValueSearch(e.target.value)}
                      className="w-full bg-neutral-950/50 border border-neutral-800/80 rounded-2xl pl-10 pr-4 py-2.5 text-sm text-white placeholder-neutral-700 focus:outline-none focus:ring-1 focus:ring-blue-600/50 transition-all font-medium"
                    />
                  </div>
                </div>

                {enumDetail.primaryProperty && (
                  <div className="flex items-center gap-3 p-3 bg-purple-500/5 border border-purple-500/10 rounded-2xl group transition-all hover:bg-purple-500/10">
                    <div className="w-8 h-8 rounded-xl bg-purple-600/10 border border-purple-500/20 flex items-center justify-center text-purple-400 shrink-0">
                      <LinkIcon className="w-4 h-4" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="text-[10px] text-neutral-500 font-bold uppercase tracking-widest leading-none mb-1.5">Linked Property</p>
                      <p className="text-sm font-mono font-bold text-neutral-200 truncate leading-none">{enumDetail.primaryProperty}</p>
                    </div>
                  </div>
                )}
              </div>

              {/* Values List */}
              <div className="flex-1 overflow-y-auto custom-scrollbar p-1">
                <div className="divide-y divide-neutral-800/20">
                  {paginatedValues.map(v => (
                    <EnumValueListItem 
                      key={v.id}
                      v={v}
                      setCmd={getSetCommandTemplate(v)}
                      enumValueCmd={getEnumValueCommand(v)}
                      copiedText={copiedText}
                      copyToClipboard={copyToClipboard}
                    />
                  ))}

                  {paginatedValues.length === 0 && (
                    <div className="flex flex-col items-center justify-center py-20 text-neutral-700 text-[10px] font-bold uppercase tracking-widest font-mono">
                      <Filter className="w-12 h-12 mb-4 opacity-5" />
                      No matching definitions
                    </div>
                  )}
                </div>
              </div>

              {/* Pagination Footer */}
              {totalPages > 1 && (
                <div className="shrink-0 border-t border-neutral-800 bg-neutral-900/60 backdrop-blur-xl p-4">
                  <div className="flex items-center justify-between">
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
            </>
          ) : null}
        </div>
      </div>
    </div>
  )
}

export default LookupTables
