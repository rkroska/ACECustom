import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { FileText, Plus, ExternalLink, Send, EyeOff, Trash2, Copy, Check, MessageCircle } from 'lucide-react'
import { api } from '../../services/api'
import {
  PatchNoteAdmin,
  PatchNotePublishResponse,
  PatchNoteWrite,
  PatchNotesDiscordResult,
  PatchNotesMeta,
  PatchNotesPaged,
} from '../../types/patchNotes'
import PageHeader from '../common/PageHeader'
import AdminToast, { AdminToastVariant } from '../common/AdminToast'
import PatchNotesBody from './PatchNotesBody'
import { buildPatchNotesAiPrompt } from '../../utils/patchNotesAiPrompt'
import { patchNotesPublicUrl } from '../../utils/patchNotesPublicUrl'

type EditorMode = 'list' | 'edit' | 'create'

const emptyDraft: PatchNoteWrite = {
  title: '',
  slug: '',
  summary: '',
  body: '',
  postToDiscord: true,
}

export default function PatchNotesManage() {
  const [mode, setMode] = useState<EditorMode>('list')
  const [notes, setNotes] = useState<PatchNoteAdmin[]>([])
  const [draft, setDraft] = useState<PatchNoteWrite>({ ...emptyDraft })
  const [editingId, setEditingId] = useState<number | null>(null)
  const [editingStatus, setEditingStatus] = useState<'draft' | 'published' | null>(null)
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [showPreview, setShowPreview] = useState(false)
  const [publicListUrl, setPublicListUrl] = useState<string | null>(null)
  const [toast, setToast] = useState<{ message: string; variant: AdminToastVariant } | null>(null)
  const [copiedPrompt, setCopiedPrompt] = useState(false)

  const aiPrompt = useMemo(() => {
    const noteUrl = publicListUrl
      ? patchNotesPublicUrl(publicListUrl, draft.slug || undefined)
      : undefined
    return buildPatchNotesAiPrompt(draft, noteUrl)
  }, [draft, publicListUrl])

  const loadList = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const data = await api.get<PatchNotesPaged<PatchNoteAdmin>>('/api/patch-notes/admin/all?pageSize=100')
      setNotes(data?.items ?? [])
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load notes')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (mode === 'list') loadList()
  }, [mode, loadList])

  useEffect(() => {
    if (mode !== 'create' && mode !== 'edit') return
    api.get<PatchNotesMeta>('/api/patch-notes/meta').then(m => setPublicListUrl(m?.publicUrl ?? null)).catch(() => {})
  }, [mode])

  const showDiscordToast = (discord: PatchNotesDiscordResult) => {
    if (discord.status === 'not_requested') return
    const variant: AdminToastVariant =
      discord.status === 'sent' ? 'success' : discord.status === 'skipped' ? 'warning' : 'error'
    setToast({ message: discord.message, variant })
  }

  const copyAiPrompt = async () => {
    try {
      await navigator.clipboard.writeText(aiPrompt)
      setCopiedPrompt(true)
      window.setTimeout(() => setCopiedPrompt(false), 2000)
    } catch {
      setError('Could not copy to clipboard')
    }
  }

  const openCreate = () => {
    setDraft({ ...emptyDraft })
    setEditingId(null)
    setEditingStatus(null)
    setMode('create')
    setShowPreview(false)
  }

  const openEdit = async (id: number) => {
    setLoading(true)
    try {
      const note = await api.get<PatchNoteAdmin>(`/api/patch-notes/admin/${id}`)
      if (!note) return
      setDraft({
        title: note.title,
        slug: note.slug,
        summary: note.summary ?? '',
        body: note.body,
        postToDiscord: note.postToDiscord,
      })
      setEditingId(id)
      setEditingStatus(note.status)
      setMode('edit')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load note')
    } finally {
      setLoading(false)
    }
  }

  const saveDraft = async () => {
    if (!draft.title.trim()) {
      setError('Title is required')
      return
    }
    setSaving(true)
    setError(null)
    try {
      if (editingId) {
        await api.put(`/api/patch-notes/admin/${editingId}`, draft)
      } else {
        const created = await api.post<PatchNoteAdmin>('/api/patch-notes/admin', draft)
        if (created) setEditingId(created.id)
      }
      setMode('list')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Save failed')
    } finally {
      setSaving(false)
    }
  }

  const publish = async (id: number, returnToList = true) => {
    setSaving(true)
    setError(null)
    try {
      const result = await api.post<PatchNotePublishResponse>(`/api/patch-notes/admin/${id}/publish`)
      if (result?.discord) showDiscordToast(result.discord)
      if (returnToList) {
        setMode('list')
        await loadList()
      } else if (result?.note) {
        setEditingStatus(result.note.status)
        setDraft({
          title: result.note.title,
          slug: result.note.slug,
          summary: result.note.summary ?? '',
          body: result.note.body,
          postToDiscord: result.note.postToDiscord,
        })
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Publish failed')
    } finally {
      setSaving(false)
    }
  }

  const postToDiscord = async (id: number) => {
    setSaving(true)
    setError(null)
    try {
      const result = await api.post<PatchNotePublishResponse>(`/api/patch-notes/admin/${id}/discord`)
      if (result?.discord) showDiscordToast(result.discord)
      if (result?.note && editingId === id) {
        setEditingStatus(result.note.status)
      }
      await loadList()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Discord post failed')
    } finally {
      setSaving(false)
    }
  }

  const unpublish = async (id: number) => {
    setSaving(true)
    try {
      await api.post(`/api/patch-notes/admin/${id}/unpublish`)
      await loadList()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unpublish failed')
    } finally {
      setSaving(false)
    }
  }

  const remove = async (id: number) => {
    if (!confirm('Delete this draft?')) return
    try {
      await api.delete(`/api/patch-notes/admin/${id}`)
      await loadList()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Delete failed')
    }
  }

  if (mode === 'create' || mode === 'edit') {
    return (
      <div className="flex flex-col h-full min-h-0 p-6">
        {toast && <AdminToast message={toast.message} variant={toast.variant} onDismiss={() => setToast(null)} />}
        <PageHeader title={mode === 'create' ? 'New patch note' : 'Edit patch note'} icon={FileText}>
          <button type="button" onClick={() => setMode('list')} className="text-sm text-neutral-400 hover:text-white">
            Back to list
          </button>
        </PageHeader>

        {error && <p className="text-red-400 text-sm mb-4">{error}</p>}

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 flex-1 min-h-0 overflow-auto">
          <div className="space-y-4">
            <Field label="Title" value={draft.title} onChange={v => setDraft(d => ({ ...d, title: v }))} />
            <Field label="Slug (optional)" value={draft.slug ?? ''} onChange={v => setDraft(d => ({ ...d, slug: v }))} placeholder="auto-generated from title" />
            <div>
              <label className="block text-[10px] font-bold text-neutral-500 uppercase tracking-wider mb-1.5">Summary</label>
              <textarea
                value={draft.summary ?? ''}
                onChange={e => setDraft(d => ({ ...d, summary: e.target.value }))}
                rows={3}
                className="w-full px-3 py-2 rounded-lg bg-neutral-950 border border-neutral-800 text-sm"
                placeholder="Short blurb for list view and Discord"
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-neutral-500 uppercase tracking-wider mb-1.5">Body (Markdown)</label>
              <textarea
                value={draft.body}
                onChange={e => setDraft(d => ({ ...d, body: e.target.value }))}
                rows={16}
                className="w-full px-3 py-2 rounded-lg bg-neutral-950 border border-neutral-800 text-sm font-mono"
              />
            </div>
            <label className="flex items-center gap-2 text-sm text-neutral-300">
              <input
                type="checkbox"
                checked={draft.postToDiscord}
                onChange={e => setDraft(d => ({ ...d, postToDiscord: e.target.checked }))}
                className="rounded border-neutral-700"
              />
              Post to Discord when published (embed with title, summary, link)
            </label>

            <div className="rounded-xl border border-neutral-800 bg-neutral-950/60 p-4 space-y-2">
              <div className="flex items-center justify-between gap-2">
                <span className="text-[10px] font-bold text-neutral-500 uppercase tracking-wider">AI writing prompt</span>
                <button
                  type="button"
                  onClick={copyAiPrompt}
                  className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-neutral-800 hover:bg-neutral-700 text-xs text-neutral-200"
                >
                  {copiedPrompt ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
                  {copiedPrompt ? 'Copied' : 'Copy prompt'}
                </button>
              </div>
              <p className="text-xs text-neutral-500">Paste into ChatGPT, Claude, etc. to polish summary and body.</p>
              <pre className="text-xs text-neutral-400 whitespace-pre-wrap font-mono max-h-40 overflow-auto">{aiPrompt}</pre>
            </div>

            <div className="flex flex-wrap gap-2 pt-2">
              <button type="button" onClick={saveDraft} disabled={saving} className="px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-sm font-semibold disabled:opacity-50">
                Save draft
              </button>
              {editingId && editingStatus !== 'published' && (
                <button type="button" onClick={() => publish(editingId, false)} disabled={saving} className="inline-flex items-center gap-2 px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-sm font-semibold disabled:opacity-50">
                  <Send className="w-4 h-4" /> Publish
                </button>
              )}
              {editingId && editingStatus === 'published' && (
                <button type="button" onClick={() => postToDiscord(editingId)} disabled={saving} className="inline-flex items-center gap-2 px-4 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-sm font-semibold disabled:opacity-50">
                  <MessageCircle className="w-4 h-4" /> Post to Discord
                </button>
              )}
              <button type="button" onClick={() => setShowPreview(v => !v)} className="px-4 py-2 rounded-lg bg-neutral-800 text-sm">
                {showPreview ? 'Hide preview' : 'Preview'}
              </button>
            </div>
          </div>
          {showPreview && (
            <div className="rounded-2xl border border-neutral-800 bg-neutral-900/40 p-6 overflow-auto">
              <h2 className="text-xl font-bold text-white mb-2">{draft.title || 'Untitled'}</h2>
              {draft.summary && <p className="text-neutral-400 mb-4">{draft.summary}</p>}
              <PatchNotesBody body={draft.body || '*No content yet*'} />
            </div>
          )}
        </div>
      </div>
    )
  }

  return (
    <div className="flex flex-col h-full min-h-0 p-6">
      {toast && <AdminToast message={toast.message} variant={toast.variant} onDismiss={() => setToast(null)} />}
      <PageHeader title="Patch notes" icon={FileText}>
        <div className="flex gap-2">
          <Link to="/patch-notes" target="_blank" className="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg bg-neutral-800 text-sm text-neutral-300 hover:text-white">
            <ExternalLink className="w-4 h-4" /> Public page
          </Link>
          <button type="button" onClick={openCreate} className="inline-flex items-center gap-2 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-sm font-semibold">
            <Plus className="w-4 h-4" /> New note
          </button>
        </div>
      </PageHeader>

      {error && <p className="text-red-400 text-sm mb-4">{error}</p>}

      {loading ? (
        <div className="flex justify-center py-12">
          <div className="w-10 h-10 border-4 border-blue-600/20 border-t-blue-600 rounded-full animate-spin" />
        </div>
      ) : (
        <div className="overflow-auto rounded-2xl border border-neutral-800">
          <table className="w-full text-left text-xs">
            <thead className="bg-neutral-950 text-neutral-500 uppercase tracking-wider">
              <tr>
                <th className="px-3 py-2">Title</th>
                <th className="px-3 py-2">Status</th>
                <th className="px-3 py-2">Published</th>
                <th className="px-3 py-2">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-neutral-800">
              {notes.map(note => (
                <tr key={note.id} className="hover:bg-neutral-800/30">
                  <td className="px-3 py-2 font-medium text-white">{note.title}</td>
                  <td className="px-3 py-2">
                    <span className={note.status === 'published' ? 'text-emerald-400' : 'text-amber-400'}>{note.status}</span>
                  </td>
                  <td className="px-3 py-2 text-neutral-500">
                    {note.publishedAt ? new Date(note.publishedAt).toLocaleString() : '—'}
                  </td>
                  <td className="px-3 py-2">
                    <div className="flex flex-wrap gap-2">
                      <button type="button" onClick={() => openEdit(note.id)} className="text-blue-400 hover:underline">Edit</button>
                      {note.status === 'draft' && (
                        <>
                          <button type="button" onClick={() => publish(note.id)} className="text-emerald-400 hover:underline">Publish</button>
                          <button type="button" onClick={() => remove(note.id)} className="text-red-400 hover:underline inline-flex items-center gap-1">
                            <Trash2 className="w-3 h-3" /> Delete
                          </button>
                        </>
                      )}
                      {note.status === 'published' && (
                        <>
                          <button type="button" onClick={() => postToDiscord(note.id)} disabled={saving} className="text-indigo-400 hover:underline inline-flex items-center gap-1">
                            <MessageCircle className="w-3 h-3" /> Discord
                          </button>
                          <button type="button" onClick={() => unpublish(note.id)} className="text-amber-400 hover:underline inline-flex items-center gap-1">
                            <EyeOff className="w-3 h-3" /> Unpublish
                          </button>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {notes.length === 0 && <p className="p-8 text-center text-neutral-500 text-sm">No patch notes yet.</p>}
        </div>
      )}
    </div>
  )
}

function Field({ label, value, onChange, placeholder }: { label: string; value: string; onChange: (v: string) => void; placeholder?: string }) {
  return (
    <div>
      <label className="block text-[10px] font-bold text-neutral-500 uppercase tracking-wider mb-1.5">{label}</label>
      <input
        type="text"
        value={value}
        onChange={e => onChange(e.target.value)}
        placeholder={placeholder}
        className="w-full px-3 py-2 rounded-lg bg-neutral-950 border border-neutral-800 text-sm"
      />
    </div>
  )
}
