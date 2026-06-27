import { useState, useEffect, useMemo, useRef } from 'react'
import { Search, Filter, Calendar } from 'lucide-react'
import { api } from '../services/api'
import { ServerEventMetadata } from '../types'
import EventListItem from './admin/EventListItem'
import PageHeader from './common/PageHeader'

type StatusFilter = 'all' | 'active' | 'inactive' | 'disabled'

const ServerEvents = () => {
  const [events, setEvents] = useState<ServerEventMetadata[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all')
  const [copiedText, setCopiedText] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const copyTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const fetchEvents = async (signal?: AbortSignal) => {
    try {
      setIsLoading(true)
      setError(null)
      const data = await api.get<ServerEventMetadata[]>('/api/event/list', { signal })
      setEvents(data ?? [])
    } catch (err) {
      if (err instanceof Error && err.name === 'AbortError') return
      console.error('Failed to fetch events', err)
      setError(err instanceof Error ? err.message : 'Failed to load events from server.')
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    const controller = new AbortController()
    fetchEvents(controller.signal)
    return () => {
      controller.abort()
      if (copyTimeoutRef.current) clearTimeout(copyTimeoutRef.current)
    }
  }, [])

  const counts = useMemo(() => ({
    active: events.filter(e => e.isActive).length,
    inactive: events.filter(e => !e.isActive && !e.isDisabled).length,
    disabled: events.filter(e => e.isDisabled).length,
  }), [events])

  const filteredEvents = useMemo(() => {
    const lowerSearch = search.toLowerCase()
    return events.filter(e => {
      const matchesSearch = e.name.toLowerCase().includes(lowerSearch) || e.state.toLowerCase().includes(lowerSearch)
      const matchesStatus =
        statusFilter === 'all' ||
        (statusFilter === 'active' && e.isActive) ||
        (statusFilter === 'inactive' && !e.isActive && !e.isDisabled) ||
        (statusFilter === 'disabled' && e.isDisabled)
      return matchesSearch && matchesStatus
    })
  }, [events, search, statusFilter])

  const copyToClipboard = (text: string, id: string) => {
    if (copyTimeoutRef.current) clearTimeout(copyTimeoutRef.current)

    navigator.clipboard.writeText(text)
      .then(() => {
        setCopiedText(id)
        copyTimeoutRef.current = setTimeout(() => {
          setCopiedText(null)
          copyTimeoutRef.current = null
        }, 2000)
      })
      .catch(err => console.error('Clipboard copy failed:', err))
  }

  if (isLoading) {
    return (
      <div className="flex-1 flex items-center justify-center min-h-[400px]">
        <div className="flex flex-col items-center justify-center space-y-4">
          <div className="w-12 h-12 border-4 border-blue-600/20 border-t-blue-600 rounded-full animate-spin"></div>
          <div className="text-neutral-500 text-sm font-medium uppercase tracking-widest animate-pulse">Fetching server events...</div>
        </div>
      </div>
    )
  }

  if (error) {
    return (
      <div className="flex-1 flex items-center justify-center min-h-[400px] bg-neutral-950 p-8">
        <div className="max-w-md w-full bg-neutral-900 border border-red-500/20 rounded-3xl p-8 text-center shadow-2xl">
          <div className="w-16 h-16 bg-red-500/10 border border-red-500/20 rounded-2xl flex items-center justify-center mx-auto mb-6">
            <Calendar className="w-8 h-8 text-red-500/50" />
          </div>
          <h2 className="text-xl font-black text-white uppercase tracking-tight mb-2">Sync Failed</h2>
          <p className="text-neutral-500 text-sm font-medium mb-8 leading-relaxed">{error}</p>
          <button
            onClick={() => fetchEvents()}
            className="w-full bg-neutral-800 hover:bg-neutral-700 text-white font-bold py-4 rounded-xl transition-all active:scale-[0.98] border border-neutral-700 shadow-lg"
          >
            Retry Request
          </button>
        </div>
      </div>
    )
  }

  const filterButtons: { key: StatusFilter; label: string; count?: number }[] = [
    { key: 'all', label: 'All' },
    { key: 'active', label: 'Active', count: counts.active },
    { key: 'inactive', label: 'Inactive', count: counts.inactive },
    { key: 'disabled', label: 'Disabled', count: counts.disabled },
  ]

  return (
    <div className="flex flex-col h-full bg-neutral-950 p-8 overflow-hidden text-glow-container">
      <PageHeader title="Server Events" icon={Calendar} className="shrink-0" />

      <div className="flex flex-col gap-4">
        <div className="flex flex-col sm:flex-row gap-4">
          <div className="relative flex-1">
            <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-neutral-600" />
            <input
              type="text"
              placeholder="Search by event name or state..."
              value={search}
              onChange={(ev) => setSearch(ev.target.value)}
              className="w-full bg-neutral-900 border border-neutral-800 rounded-2xl pl-12 pr-4 py-3 text-sm text-white placeholder-neutral-600 focus:outline-none focus:ring-2 focus:ring-blue-600/50 transition-all shadow-inner font-medium"
            />
          </div>

          <div className="flex gap-2 overflow-x-auto pb-2 custom-scrollbar no-scrollbar text-[10px] font-bold uppercase tracking-widest">
            {filterButtons.map(({ key, label, count }) => (
              <button
                key={key}
                onClick={() => setStatusFilter(key)}
                className={`px-4 py-3 rounded-xl transition-all duration-200 whitespace-nowrap border ${
                  statusFilter === key
                    ? 'bg-neutral-800 text-white border-neutral-700'
                    : 'bg-neutral-900/50 text-neutral-500 border-neutral-800/50 hover:text-neutral-300'
                }`}
              >
                {label}
                {count !== undefined ? ` (${count})` : ''}
              </button>
            ))}
          </div>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto custom-scrollbar bg-neutral-900/30 border border-neutral-800/50 rounded-3xl overflow-hidden shadow-2xl">
        <div className="min-w-full divide-y divide-neutral-800/50">
          <div className="grid grid-cols-[1fr_140px_120px_1fr_220px] gap-4 px-6 py-4 bg-neutral-900/50 text-[10px] font-bold text-neutral-500 uppercase tracking-[0.2em]">
            <span>Event Name</span>
            <span>Status</span>
            <span>Runtime</span>
            <span>Notes</span>
            <span className="text-right">Commands</span>
          </div>

          <div className="divide-y divide-neutral-800/20">
            {filteredEvents.map((e) => (
              <EventListItem
                key={e.name}
                e={e}
                copiedText={copiedText}
                copyToClipboard={copyToClipboard}
              />
            ))}
          </div>

          {filteredEvents.length === 0 && (
            <div className="flex flex-col items-center justify-center py-20 text-neutral-600 text-[10px] font-bold uppercase tracking-widest bg-black/10">
              <Filter className="w-12 h-12 mb-4 opacity-10" />
              <p>No events found matching your filters</p>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

export default ServerEvents
