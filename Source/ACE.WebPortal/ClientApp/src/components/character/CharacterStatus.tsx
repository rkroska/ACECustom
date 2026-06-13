import { useEffect, useState } from 'react'
import { Activity, MapPin, Skull, Copy, ExternalLink } from 'lucide-react'
import { api } from '../../services/api'

export interface AcePosition {
  cell: number
  landblockX: number
  landblockY: number
  cellX: number
  cellY: number
  positionX: number
  positionY: number
  positionZ: number
  variation: number | null
  indoors: boolean
}

export interface PortalCorpseRow {
  objectGuid: number
  name: string | null
  longDesc: string | null
  killerId: number | null
  position: AcePosition | null
  timeToRotSeconds: number | null
  creationTimestamp: number | null
}

export interface CharacterPortalStatus {
  death: {
    numDeaths: number
    deathLevel: number | null
    deathTimestampUnix: number | null
    deathTimeUtc: string | null
  }
  vitae: {
    hasVitae: boolean
    multiplier: number | null
    penaltyPercent: number | null
    vitaeCpPool: number | null
  }
  live: {
    isOnline: boolean
    position: AcePosition | null
    note: string
  }
  corpses: PortalCorpseRow[]
  narrativeNote: string
}

function formatUtc(iso: string | null) {
  if (!iso) return '—'
  try {
    const d = new Date(iso)
    return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(undefined, { timeZoneName: 'short' })
  } catch {
    return iso
  }
}

function positionClipboard(p: AcePosition) {
  return `0x${p.cell.toString(16)} ${p.positionX.toFixed(3)} ${p.positionY.toFixed(3)} ${p.positionZ.toFixed(3)}`
}

export default function CharacterStatus({ guid, isAdmin }: { guid: number; isAdmin?: boolean }) {
  const [data, setData] = useState<CharacterPortalStatus | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      try {
        setLoading(true)
        setError(null)
        const res = await api.get<CharacterPortalStatus>(`/api/character/status/${guid}`)
        if (!cancelled) setData(res ?? null)
      } catch (e) {
        if (!cancelled) setError(e instanceof Error ? e.message : 'Failed to load status')
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [guid])

  const copyCoords = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text)
    } catch {
      // ignore
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center py-24 text-neutral-500 gap-2">
        <Activity className="w-5 h-5 animate-spin" />
        <span className="text-sm">Loading status…</span>
      </div>
    )
  }

  if (error || !data) {
    return (
      <div className="rounded-xl border border-red-500/20 bg-red-500/5 text-red-400 text-sm p-4">
        {error || 'No data'}
      </div>
    )
  }

  return (
    <div className="space-y-8">
      <p className="text-xs text-neutral-500 leading-relaxed max-w-3xl">{data.narrativeNote}</p>

      <section className="rounded-2xl border border-neutral-800 bg-neutral-950/40 p-6 space-y-4">
        <div className="flex items-center gap-2 text-white font-bold text-sm uppercase tracking-wider">
          <Skull className="w-4 h-4 text-red-400/80" />
          Death (biota)
        </div>
        <dl className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-sm">
          <div>
            <dt className="text-neutral-500 text-[10px] uppercase tracking-wider font-bold">Total deaths</dt>
            <dd className="text-neutral-200 font-mono mt-1">{data.death.numDeaths}</dd>
          </div>
          <div>
            <dt className="text-neutral-500 text-[10px] uppercase tracking-wider font-bold">Death level (last)</dt>
            <dd className="text-neutral-200 font-mono mt-1">{data.death.deathLevel ?? '—'}</dd>
          </div>
          <div className="sm:col-span-2">
            <dt className="text-neutral-500 text-[10px] uppercase tracking-wider font-bold">Last death (UTC)</dt>
            <dd className="text-neutral-200 font-mono mt-1">{formatUtc(data.death.deathTimeUtc)}</dd>
          </div>
        </dl>
      </section>

      <section className="rounded-2xl border border-neutral-800 bg-neutral-950/40 p-6 space-y-4">
        <div className="text-white font-bold text-sm uppercase tracking-wider">Vitae</div>
        <dl className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-sm">
          <div>
            <dt className="text-neutral-500 text-[10px] uppercase tracking-wider font-bold">Active vitae</dt>
            <dd className="text-neutral-200 mt-1">{data.vitae.hasVitae ? 'Yes' : 'No'}</dd>
          </div>
          <div>
            <dt className="text-neutral-500 text-[10px] uppercase tracking-wider font-bold">Multiplier</dt>
            <dd className="text-neutral-200 font-mono mt-1">
              {data.vitae.multiplier != null ? data.vitae.multiplier.toFixed(4) : '—'}
            </dd>
          </div>
          <div>
            <dt className="text-neutral-500 text-[10px] uppercase tracking-wider font-bold">Penalty %</dt>
            <dd className="text-neutral-200 font-mono mt-1">
              {data.vitae.penaltyPercent != null ? `${data.vitae.penaltyPercent}%` : '—'}
            </dd>
          </div>
          <div>
            <dt className="text-neutral-500 text-[10px] uppercase tracking-wider font-bold">Vitae CP pool</dt>
            <dd className="text-neutral-200 font-mono mt-1">{data.vitae.vitaeCpPool ?? '—'}</dd>
          </div>
        </dl>
      </section>

      <section className="rounded-2xl border border-neutral-800 bg-neutral-950/40 p-6 space-y-4">
        <div className="flex items-center gap-2 text-white font-bold text-sm uppercase tracking-wider">
          <MapPin className="w-4 h-4 text-emerald-400/80" />
          Live position
        </div>
        <p className="text-xs text-neutral-500">{data.live.note}</p>
        {data.live.position ? (
          <div className="space-y-3">
            <pre className="text-xs font-mono text-neutral-300 bg-neutral-900/80 rounded-lg p-3 overflow-x-auto border border-neutral-800">
              {JSON.stringify(data.live.position, null, 2)}
            </pre>
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                onClick={() => copyCoords(positionClipboard(data.live.position!))}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-neutral-800 hover:bg-neutral-700 text-xs text-neutral-200 border border-neutral-700"
              >
                <Copy className="w-3.5 h-3.5" />
                Copy cell + XYZ
              </button>
              {isAdmin && (
                <a
                  href="#/map"
                  className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-blue-500/10 hover:bg-blue-500/20 text-xs text-blue-300 border border-blue-500/20"
                >
                  <ExternalLink className="w-3.5 h-3.5" />
                  Open world map
                </a>
              )}
            </div>
          </div>
        ) : (
          <p className="text-sm text-neutral-500">No live position (character offline).</p>
        )}
      </section>

      <section className="rounded-2xl border border-neutral-800 bg-neutral-950/40 p-6 space-y-4">
        <div className="text-white font-bold text-sm uppercase tracking-wider">Active corpses (loaded landblocks)</div>
        {data.corpses.length === 0 ? (
          <p className="text-sm text-neutral-500">No player corpse found in memory for this character.</p>
        ) : (
          <ul className="space-y-4">
            {data.corpses.map((c) => (
              <li key={c.objectGuid} className="border border-neutral-800 rounded-xl p-4 bg-neutral-900/30 space-y-2">
                <div className="text-sm text-neutral-200 font-medium">{c.name ?? 'Corpse'}</div>
                {c.longDesc && <div className="text-xs text-neutral-400">{c.longDesc}</div>}
                <div className="text-[10px] text-neutral-600 font-mono">
                  Object 0x{c.objectGuid.toString(16)} · Killer IID {c.killerId != null ? `0x${c.killerId.toString(16)}` : '—'}
                </div>
                {c.timeToRotSeconds != null && (
                  <div className="text-xs text-neutral-500">Time to rot (sec): {c.timeToRotSeconds}</div>
                )}
                {c.position && (
                  <div className="flex flex-wrap gap-2 pt-1">
                    <button
                      type="button"
                      onClick={() => copyCoords(positionClipboard(c.position!))}
                      className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-neutral-800 hover:bg-neutral-700 text-[10px] text-neutral-300 border border-neutral-700"
                    >
                      <Copy className="w-3 h-3" />
                      Copy corpse loc
                    </button>
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  )
}
