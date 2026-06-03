import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'
import { api } from '../../services/api'
import { PatchNotePublic } from '../../types/patchNotes'
import PatchNotesBody from './PatchNotesBody'

function formatDate(iso: string | null | undefined) {
  if (!iso) return ''
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(undefined, { dateStyle: 'long', timeStyle: 'short' })
}

export default function PatchNoteDetail() {
  const { slug } = useParams<{ slug: string }>()
  const [note, setNote] = useState<PatchNotePublic | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!slug) return
    const load = async () => {
      setLoading(true)
      try {
        const data = await api.get<PatchNotePublic>(`/api/patch-notes/${encodeURIComponent(slug)}`)
        setNote(data)
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Patch note not found')
      } finally {
        setLoading(false)
      }
    }
    load()
  }, [slug])

  if (loading) {
    return (
      <div className="flex justify-center py-16">
        <div className="w-10 h-10 border-4 border-blue-600/20 border-t-blue-600 rounded-full animate-spin" />
      </div>
    )
  }

  if (error || !note) {
    return (
      <div className="space-y-4">
        <Link to="/patch-notes" className="inline-flex items-center gap-2 text-sm text-blue-400 hover:text-blue-300">
          <ArrowLeft className="w-4 h-4" /> All patch notes
        </Link>
        <p className="text-red-400">{error ?? 'Not found'}</p>
      </div>
    )
  }

  return (
    <article className="space-y-6 animate-in fade-in duration-500">
      <Link to="/patch-notes" className="inline-flex items-center gap-2 text-sm text-blue-400 hover:text-blue-300">
        <ArrowLeft className="w-4 h-4" /> All patch notes
      </Link>
      <header>
        <h1 className="text-3xl font-black text-white tracking-tight mb-2">{note.title}</h1>
        <p className="text-sm text-neutral-500">Published {formatDate(note.publishedAt)}</p>
        {note.summary && <p className="mt-4 text-neutral-400 text-lg">{note.summary}</p>}
      </header>
      <PatchNotesBody body={note.body} />
    </article>
  )
}
