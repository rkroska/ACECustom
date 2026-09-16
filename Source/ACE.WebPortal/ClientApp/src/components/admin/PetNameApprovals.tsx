import { useCallback, useEffect, useState } from 'react'
import { Heart, Check, X, RefreshCw } from 'lucide-react'
import { api } from '../../services/api'
import PageHeader from '../common/PageHeader'

interface PetNameRequest {
  id: number
  characterId: number
  characterName: string
  petGuid: number
  oldName: string
  requestedName: string
  status: number
  reviewNote: string | null
  createdAt: string
  reviewedAt: string | null
  reviewedBy: string | null
}

type StatusFilter = 'pending' | 'approved' | 'denied' | 'all'

const STATUS_LABEL: Record<number, string> = {
  0: 'Pending',
  1: 'Approved',
  2: 'Denied',
}

const STATUS_BADGE_CLASS: Record<number, string> = {
  0: 'bg-amber-500/10 text-amber-400 border-amber-500/20',
  1: 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20',
  2: 'bg-red-500/10 text-red-400 border-red-500/20',
}

function formatDate(iso: string | null): string {
  if (!iso) return '-'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  return d.toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' })
}

export default function PetNameApprovals() {
  const [requests, setRequests] = useState<PetNameRequest[]>([])
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('pending')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busyId, setBusyId] = useState<number | null>(null)

  const load = useCallback(async () => {
    setIsLoading(true)
    setError(null)
    try {
      const data = await api.get<PetNameRequest[]>('/api/PetNaming/requests?limit=200')
      setRequests(data ?? [])
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load pet name requests.')
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    load()
  }, [load])

  const approve = async (id: number) => {
    setBusyId(id)
    setError(null)
    try {
      await api.post(`/api/PetNaming/approve/${id}`)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to approve request.')
    } finally {
      setBusyId(null)
    }
  }

  const deny = async (id: number) => {
    const reason = window.prompt('Reason for denial (optional):') ?? ''
    setBusyId(id)
    setError(null)
    try {
      await api.post(`/api/PetNaming/deny/${id}`, { reason })
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to deny request.')
    } finally {
      setBusyId(null)
    }
  }

  const filtered = requests.filter(r => {
    if (statusFilter === 'all') return true
    if (statusFilter === 'pending') return r.status === 0
    if (statusFilter === 'approved') return r.status === 1
    return r.status === 2
  })

  return (
    <div className="p-6">
      <PageHeader title="Pet Name Approvals" icon={Heart}>
        <button
          onClick={load}
          disabled={isLoading}
          className="flex items-center gap-2 px-3 py-1.5 text-sm rounded-lg bg-neutral-800 hover:bg-neutral-700 text-neutral-200 border border-neutral-700 disabled:opacity-50"
        >
          <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} />
          Refresh
        </button>
      </PageHeader>

      <div className="flex gap-2 mb-4">
        {(['pending', 'approved', 'denied', 'all'] as StatusFilter[]).map(f => (
          <button
            key={f}
            onClick={() => setStatusFilter(f)}
            className={`px-3 py-1.5 text-sm rounded-lg border capitalize ${
              statusFilter === f
                ? 'bg-blue-600/20 border-blue-500/40 text-blue-300'
                : 'bg-neutral-900 border-neutral-800 text-neutral-400 hover:text-neutral-200'
            }`}
          >
            {f}
          </button>
        ))}
      </div>

      {error && (
        <div className="mb-4 px-4 py-3 rounded-lg bg-red-500/10 border border-red-500/20 text-red-300 text-sm">
          {error}
        </div>
      )}

      <div className="rounded-xl border border-neutral-800 overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-neutral-900 text-neutral-400 text-left">
            <tr>
              <th className="px-4 py-3 font-medium">Player</th>
              <th className="px-4 py-3 font-medium">Old Name</th>
              <th className="px-4 py-3 font-medium">Requested Name</th>
              <th className="px-4 py-3 font-medium">Status</th>
              <th className="px-4 py-3 font-medium">Requested</th>
              <th className="px-4 py-3 font-medium">Reviewed</th>
              <th className="px-4 py-3 font-medium text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-neutral-800">
            {filtered.length === 0 && !isLoading && (
              <tr>
                <td colSpan={7} className="px-4 py-8 text-center text-neutral-500">
                  No pet name requests found.
                </td>
              </tr>
            )}
            {filtered.map(r => (
              <tr key={r.id} className="text-neutral-200 hover:bg-neutral-900/50">
                <td className="px-4 py-3">{r.characterName}</td>
                <td className="px-4 py-3 text-neutral-400">{r.oldName}</td>
                <td className="px-4 py-3 font-medium">{r.requestedName}</td>
                <td className="px-4 py-3">
                  <span className={`px-2 py-0.5 rounded-full text-xs border ${STATUS_BADGE_CLASS[r.status] ?? ''}`}>
                    {STATUS_LABEL[r.status] ?? 'Unknown'}
                  </span>
                  {r.reviewNote && (
                    <div className="text-xs text-neutral-500 mt-1">{r.reviewNote}</div>
                  )}
                </td>
                <td className="px-4 py-3 text-neutral-400">{formatDate(r.createdAt)}</td>
                <td className="px-4 py-3 text-neutral-400">
                  {r.reviewedAt ? `${formatDate(r.reviewedAt)} (${r.reviewedBy ?? '?'})` : '-'}
                </td>
                <td className="px-4 py-3">
                  {r.status === 0 && (
                    <div className="flex items-center justify-end gap-2">
                      <button
                        onClick={() => approve(r.id)}
                        disabled={busyId === r.id}
                        className="flex items-center gap-1 px-2 py-1 text-xs rounded-lg bg-emerald-600/20 hover:bg-emerald-600/30 text-emerald-300 border border-emerald-500/30 disabled:opacity-50"
                      >
                        <Check className="w-3.5 h-3.5" /> Approve
                      </button>
                      <button
                        onClick={() => deny(r.id)}
                        disabled={busyId === r.id}
                        className="flex items-center gap-1 px-2 py-1 text-xs rounded-lg bg-red-600/20 hover:bg-red-600/30 text-red-300 border border-red-500/30 disabled:opacity-50"
                      >
                        <X className="w-3.5 h-3.5" /> Deny
                      </button>
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
