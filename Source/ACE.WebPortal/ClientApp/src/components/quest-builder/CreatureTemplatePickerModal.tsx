import { useEffect, useState } from 'react'
import { AlertCircle, Search } from 'lucide-react'
import Modal from '../common/Modal'
import { api } from '../../services/api'
import { useDebounce } from '../../hooks/useDebounce'
import type { CreatureSearchResult } from '../../types/questBuilder'
import { normalizeCreatureList } from './creatureSearchNormalize'

type Props = {
  isOpen: boolean
  onClose: () => void
  title?: string
  description?: string
  currentWcid?: number
  onSelect: (creature: CreatureSearchResult) => void
}

export default function CreatureTemplatePickerModal({
  isOpen,
  onClose,
  title = 'Pick creature template',
  description = 'Search creature weenies by display name, classname, or WCID (e.g. darkspire, moarsman, 71600031).',
  currentWcid,
  onSelect,
}: Props) {
  const [query, setQuery] = useState('')
  const debouncedQuery = useDebounce(query, 300)
  const [results, setResults] = useState<CreatureSearchResult[]>([])
  const [searching, setSearching] = useState(false)
  const [searchError, setSearchError] = useState<string | null>(null)

  useEffect(() => {
    if (!isOpen) {
      setQuery('')
      setResults([])
      setSearchError(null)
    }
  }, [isOpen])

  useEffect(() => {
    if (!isOpen || !debouncedQuery.trim()) {
      setResults([])
      setSearchError(null)
      return
    }

    const controller = new AbortController()
    setSearching(true)
    setSearchError(null)

    api
      .get<unknown>(
        `/api/quest-builder/creature-search?q=${encodeURIComponent(debouncedQuery.trim())}&limit=50`,
        { signal: controller.signal }
      )
      .then((data) => {
        setResults(normalizeCreatureList(data, debouncedQuery.trim()))
      })
      .catch((err) => {
        if (err instanceof Error && err.name === 'AbortError') return
        setResults([])
        setSearchError(err instanceof Error ? err.message : 'Creature search failed.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setSearching(false)
      })

    return () => controller.abort()
  }, [debouncedQuery, isOpen])

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={title}
      description={description}
      type="info"
      cancelLabel="Close"
      maxWidth="2xl"
    >
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-neutral-500" />
        <input
          type="text"
          autoFocus
          placeholder="Search: darkspire, drudge, WCID…"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          className="w-full bg-neutral-950 border border-neutral-700 rounded-xl pl-10 pr-4 py-2.5 text-sm text-white placeholder-neutral-600 focus:outline-none focus:ring-2 focus:ring-blue-600/40"
        />
        {searching && (
          <span className="absolute right-3 top-1/2 -translate-y-1/2 text-[10px] text-neutral-500 uppercase">
            Searching…
          </span>
        )}
      </div>

      {searchError && (
        <div className="mt-3 flex gap-2 items-start text-sm text-red-300 bg-red-950/40 border border-red-800/50 rounded-lg px-3 py-2">
          <AlertCircle className="w-4 h-4 shrink-0 mt-0.5" />
          <div>
            <p>{searchError}</p>
            <p className="text-[11px] text-red-400/80 mt-1">
              If this says 404, restart ACE.Server after updating — the creature-search API must be running.
            </p>
          </div>
        </div>
      )}

      <div className="mt-3 max-h-80 overflow-y-auto rounded-xl border border-neutral-800 divide-y divide-neutral-800">
        {results.length === 0 && debouncedQuery.trim() && !searching && !searchError && (
          <p className="text-sm text-neutral-500 text-center py-8">
            No creature weenies matched. Try classname (e.g. <span className="font-mono">darkspire</span>) or a WCID.
          </p>
        )}
        {!debouncedQuery.trim() && (
          <p className="text-sm text-neutral-500 text-center py-8">Type a name, classname, or WCID to search.</p>
        )}
        {results.map((r) => (
          <button
            key={r.wcid}
            type="button"
            onClick={() => {
              onSelect(r)
              onClose()
            }}
            className={`w-full flex items-start justify-between gap-3 px-4 py-3 text-left hover:bg-neutral-800 transition-colors ${
              currentWcid === r.wcid ? 'bg-blue-950/40' : ''
            }`}
          >
            <div className="min-w-0">
              <div className="text-sm font-medium text-white truncate">{r.name}</div>
              <div className="text-[11px] text-neutral-500 font-mono mt-0.5 truncate">{r.className}</div>
            </div>
            <div className="text-right shrink-0">
              <div className="text-xs font-mono text-blue-300">{r.wcid}</div>
              <div className="text-[10px] text-neutral-600">{r.weenieType}</div>
            </div>
          </button>
        ))}
      </div>
    </Modal>
  )
}
