import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Search, PenLine } from 'lucide-react'
import { api } from '../../services/api'
import { useAuthStore } from '../../store/useAuthStore'
import { PatchNotePublic, PatchNotesMeta, PatchNotesPaged } from '../../types/patchNotes'
import Pagination from '../common/Pagination'

const PAGE_SIZE = 20

function formatDate(iso: string | null | undefined) {
  if (!iso) return ''
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

export default function PatchNotesList() {
  const { canAccessPage } = useAuthStore()
  const canManage = canAccessPage('patch-notes-admin')
  const [meta, setMeta] = useState<PatchNotesMeta | null>(null)
  const [data, setData] = useState<PatchNotesPaged<PatchNotePublic> | null>(null)
  const [search, setSearch] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api.get<PatchNotesMeta>('/api/patch-notes/meta').then(m => setMeta(m ?? null)).catch(() => {})
  }, [])

  useEffect(() => {
    const load = async () => {
      setLoading(true)
      setError(null)
      try {
        const params = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) })
        if (appliedSearch.trim()) params.set('search', appliedSearch.trim())
        const result = await api.get<PatchNotesPaged<PatchNotePublic>>(`/api/patch-notes?${params}`)
        setData(result)
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load patch notes')
      } finally {
        setLoading(false)
      }
    }
    load()
  }, [page, appliedSearch])

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault()
    setPage(1)
    setAppliedSearch(search)
  }

  return (
    <div className="p-6 space-y-8 animate-in fade-in duration-500 max-w-4xl">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-black text-white tracking-tight mb-2">Server patch notes</h1>
          {meta?.lastUpdatedAt && (
            <p className="text-sm text-neutral-500">
              Last updated: {formatDate(meta.lastUpdatedAt)}
            </p>
          )}
        </div>
        {canManage && (
          <Link
            to="/patch-notes/manage"
            className="inline-flex items-center gap-2 px-4 py-2 rounded-xl bg-blue-600 hover:bg-blue-500 text-sm font-semibold text-white shrink-0"
          >
            <PenLine className="w-4 h-4" />
            Manage / New note
          </Link>
        )}
      </div>

      <form onSubmit={handleSearch} className="flex gap-2">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-neutral-500" />
          <input
            type="search"
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder="Search patch notes…"
            className="w-full pl-10 pr-4 py-2.5 rounded-xl bg-neutral-900 border border-neutral-800 text-sm focus:outline-none focus:border-blue-500/50"
          />
        </div>
        <button type="submit" className="px-4 py-2 rounded-xl bg-blue-600 hover:bg-blue-500 text-sm font-semibold">
          Search
        </button>
      </form>

      {loading ? (
        <div className="flex justify-center py-16">
          <div className="w-10 h-10 border-4 border-blue-600/20 border-t-blue-600 rounded-full animate-spin" />
        </div>
      ) : error ? (
        <p className="text-red-400 text-sm">{error}</p>
      ) : !data?.items?.length ? (
        <div className="space-y-3">
          <p className="text-neutral-500 text-sm">No patch notes published yet.</p>
          {canManage && (
            <Link to="/patch-notes/manage" className="text-sm text-blue-400 hover:text-blue-300 hover:underline">
              Create the first patch note →
            </Link>
          )}
        </div>
      ) : (
        <ul className="space-y-4">
          {data.items.map(note => (
            <li key={note.id}>
              <Link
                to={`/patch-notes/${note.slug}`}
                className="block p-5 rounded-2xl bg-neutral-900/60 border border-neutral-800 hover:border-blue-500/30 transition-colors"
              >
                <div className="flex flex-wrap items-baseline justify-between gap-2 mb-1">
                  <h2 className="text-lg font-bold text-white">{note.title}</h2>
                  <time className="text-xs text-neutral-500">{formatDate(note.publishedAt)}</time>
                </div>
                {note.summary && <p className="text-sm text-neutral-400 line-clamp-2">{note.summary}</p>}
              </Link>
            </li>
          ))}
        </ul>
      )}

      {data && (
        <Pagination
          currentPage={page}
          totalPages={data.totalPages}
          onPageChange={setPage}
        />
      )}
    </div>
  )
}
