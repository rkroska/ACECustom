import React, { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Activity, RefreshCw, Trophy } from 'lucide-react'
import { api } from '../services/api'
import PageHeader from './common/PageHeader'
import Modal from './common/Modal'
import { cn } from '../utils/cn'

type BoardMeta = { id: string; title: string }

type BoardRow = {
  rank: number
  score: number
  character: string
  account: number | null
  characterGuid: string | null
  you?: boolean
}

type SelfPlacementRow = {
  rank: number
  score: number
  character: string
  account: number | null
  characterGuid: string | null
  inTopList: boolean
}

type BoardPayload = {
  id: string
  title: string
  nextRefreshApproxUtc: string | null
  cached: boolean
  rows: BoardRow[]
  selfRow?: SelfPlacementRow | null
}

type PetRegistryEntry = {
  wcid: number
  creatureName: string
  creatureType: string | null
  isShiny: boolean
  registeredAt: string
}

const DEFAULT_BOARD = 'lum'

type BoardGroup = { title: string; ids: string[] }

const BOARD_GROUPS: BoardGroup[] = [
  { title: 'Progression', ids: ['level', 'enl', 'attr', 'augs', 'title', 'deaths'] },
  { title: 'Wealth', ids: ['bank', 'lum', 'enlcoins', 'wenlcoins', 'mkeys', 'lkeys'] },
  { title: 'Account', ids: ['qb'] },
  { title: 'Pets', ids: ['pets', 'shinies', 'bond', 'sumbond', 'potency'] },
  { title: 'Discipline', ids: ['jails', 'notguilty'] },
]

function orderBoards(boards: BoardMeta[]): BoardMeta[] {
  const byId = new Map(boards.map((b) => [b.id, b] as const))
  const ordered: BoardMeta[] = []
  for (const g of BOARD_GROUPS) {
    for (const id of g.ids) {
      const b = byId.get(id)
      if (b) ordered.push(b)
    }
  }
  const known = new Set(ordered.map((b) => b.id))
  const leftovers = boards
    .filter((b) => !known.has(b.id))
    .slice()
    .sort((a, b) => a.title.localeCompare(b.title))
  return [...ordered, ...leftovers]
}

function formatScore(boardId: string, score: number): string {
  if (
    boardId === 'bank' ||
    boardId === 'lum' ||
    boardId === 'augs' ||
    boardId === 'attr' ||
    boardId === 'enlcoins' ||
    boardId === 'wenlcoins' ||
    boardId === 'mkeys' ||
    boardId === 'lkeys'
  ) {
    return score.toLocaleString()
  }
  return String(score)
}

function refreshLabel(iso: string | null): string | null {
  if (!iso) return null
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return null
  return d.toLocaleString(undefined, { timeZone: 'UTC', timeZoneName: 'short' }) + ' (approx.)'
}

function leaderboardCharacterName(
  row: BoardRow,
  boardId: string,
  openPetModal: (accountId: number, displayName: string, shiniesOnly: boolean) => void
): React.ReactNode {
  const name = row.character || '—'
  if (row.characterGuid) {
    return (
      <Link to={`/characters/${row.characterGuid}`} className="hover:text-blue-400 transition-colors">
        {name}
      </Link>
    )
  }
  const petBoard = boardId === 'pets' || boardId === 'shinies'
  if (petBoard && row.account != null) {
    return (
      <button
        type="button"
        onClick={() => openPetModal(row.account!, row.character || 'Player', boardId === 'shinies')}
        className="text-left hover:text-blue-400 transition-colors underline decoration-neutral-600 underline-offset-2 hover:decoration-blue-400"
      >
        {name}
      </button>
    )
  }
  return <span>{name}</span>
}

export default function Leaderboards() {
  const [catalog, setCatalog] = useState<BoardMeta[]>([])
  const [boardId, setBoardId] = useState(DEFAULT_BOARD)
  const [payload, setPayload] = useState<BoardPayload | null>(null)
  const [loadingCatalog, setLoadingCatalog] = useState(true)
  const [loadingBoard, setLoadingBoard] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const [petModalOpen, setPetModalOpen] = useState(false)
  const [petModalTitle, setPetModalTitle] = useState('')
  const [petModalShiniesOnly, setPetModalShiniesOnly] = useState(false)
  const [petModalLoading, setPetModalLoading] = useState(false)
  const [petModalError, setPetModalError] = useState<string | null>(null)
  const [petModalEntries, setPetModalEntries] = useState<PetRegistryEntry[]>([])

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      try {
        setLoadingCatalog(true)
        const res = await api.get<{ boards: BoardMeta[] }>('/api/leaderboard/boards')
        if (cancelled) return
        const boards = res?.boards ?? []
        setCatalog(orderBoards(boards))

        if (boards.length && !boards.some((b) => b.id === boardId)) {
          setBoardId(boards[0].id)
        }
      } catch (e) {
        if (!cancelled) {
          setError(e instanceof Error ? e.message : 'Failed to load leaderboards.')
        }
      } finally {
        if (!cancelled) setLoadingCatalog(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [])

  const groupedCatalog = useMemo(() => {
    const byId = new Map(catalog.map((b) => [b.id, b] as const))
    const groups = BOARD_GROUPS.map((g) => ({
      title: g.title,
      boards: g.ids.map((id) => byId.get(id)).filter(Boolean) as BoardMeta[],
    })).filter((g) => g.boards.length > 0)

    const known = new Set(BOARD_GROUPS.flatMap((g) => g.ids))
    const other = catalog.filter((b) => !known.has(b.id))
    if (other.length) groups.push({ title: 'Other', boards: other })
    return groups
  }, [catalog])

  const loadBoard = useCallback(async (id: string) => {
    try {
      setLoadingBoard(true)
      setError(null)
      const res = await api.get<BoardPayload>(`/api/leaderboard/${encodeURIComponent(id)}`)
      if (res) setPayload(res)
    } catch (e) {
      setPayload(null)
      setError(e instanceof Error ? e.message : 'Failed to load this board.')
    } finally {
      setLoadingBoard(false)
    }
  }, [])

  useEffect(() => {
    if (!boardId || loadingCatalog) return
    loadBoard(boardId)
  }, [boardId, loadingCatalog, loadBoard])

  const scoreHeader = useMemo(() => {
    if (boardId === 'pets' || boardId === 'shinies') return 'Count'
    if (boardId === 'bank') return 'Bank'
    if (boardId === 'lum') return 'Luminance'
    return 'Score'
  }, [boardId])

  const openPetModal = useCallback(
    async (accountId: number, displayName: string, shiniesOnly: boolean) => {
      setPetModalOpen(true)
      setPetModalTitle(displayName)
      setPetModalShiniesOnly(shiniesOnly)
      setPetModalLoading(true)
      setPetModalError(null)
      setPetModalEntries([])
      try {
        const q = shiniesOnly ? '?shiniesOnly=true' : ''
        const res = await api.get<{ entries: PetRegistryEntry[] }>(
          `/api/leaderboard/pet-registry/${accountId}${q}`
        )
        setPetModalEntries(res?.entries ?? [])
      } catch (e) {
        setPetModalError(e instanceof Error ? e.message : 'Failed to load captures.')
      } finally {
        setPetModalLoading(false)
      }
    },
    []
  )

  if (loadingCatalog && !catalog.length) {
    return (
      <div className="flex-1 flex items-center justify-center min-h-[400px] bg-neutral-900">
        <Activity className="w-8 h-8 text-blue-500 animate-spin" />
      </div>
    )
  }

  return (
    <div className="flex-1 flex flex-col min-h-0 bg-neutral-900 overflow-hidden text-neutral-100">
      <Modal
        isOpen={petModalOpen}
        onClose={() => setPetModalOpen(false)}
        title={petModalShiniesOnly ? `Shiny captures — ${petModalTitle}` : `Pet log — ${petModalTitle}`}
        description={
          petModalShiniesOnly
            ? 'Shiny essences registered on this account.'
            : 'All registered essences on this account (normal and shiny).'
        }
        type="info"
        cancelLabel="Close"
        maxWidth="lg"
      >
        {petModalLoading && (
          <div className="flex justify-center py-10">
            <Activity className="w-7 h-7 text-blue-500 animate-spin opacity-70" />
          </div>
        )}
        {!petModalLoading && petModalError && (
          <p className="text-sm text-red-300">{petModalError}</p>
        )}
        {!petModalLoading && !petModalError && petModalEntries.length === 0 && (
          <p className="text-sm text-neutral-500">No entries.</p>
        )}
        {!petModalLoading && !petModalError && petModalEntries.length > 0 && (
          <ul className="max-h-[min(60vh,28rem)] overflow-y-auto custom-scrollbar space-y-2 pr-1 text-sm">
            {petModalEntries.map((e) => (
              <li
                key={`${e.wcid}-${e.creatureName}-${e.isShiny}-${e.registeredAt}`}
                className="flex flex-wrap items-baseline gap-x-3 gap-y-1 rounded-lg border border-neutral-800 bg-neutral-950/60 px-3 py-2"
              >
                <span className="font-medium text-white">
                  {e.isShiny ? <span className="text-amber-400/90">Shiny </span> : null}
                  {e.creatureName}
                </span>
                {e.creatureType ? (
                  <span className="text-xs text-neutral-500">{e.creatureType}</span>
                ) : null}
                <span className="text-xs text-neutral-600 tabular-nums ml-auto">WCID {e.wcid}</span>
              </li>
            ))}
          </ul>
        )}
      </Modal>

      <div className="p-8 pb-0 shrink-0">
        <div className="max-w-4xl mx-auto w-full">
          <PageHeader title="Leaderboards" icon={Trophy}>
            <button
              type="button"
              onClick={() => loadBoard(boardId)}
              disabled={loadingBoard}
              className="flex items-center gap-2 px-3 py-1.5 rounded-xl text-[10px] font-bold uppercase tracking-wider bg-neutral-800 border border-neutral-700 text-neutral-300 hover:bg-neutral-700 hover:text-white disabled:opacity-50"
            >
              <RefreshCw className={cn('w-3.5 h-3.5', loadingBoard && 'animate-spin')} />
              Refresh
            </button>
          </PageHeader>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto custom-scrollbar p-8 pt-4">
        <div className="max-w-4xl mx-auto w-full space-y-6">
          {error && (
            <div className="rounded-xl border border-red-500/30 bg-red-500/10 text-red-300 text-sm px-4 py-3">
              {error}
            </div>
          )}

          <div className="space-y-3">
            {groupedCatalog.map((g) => (
              <div key={g.title} className="space-y-2">
                <div className="text-[10px] uppercase tracking-widest text-neutral-500">{g.title}</div>
                <div className="flex flex-wrap gap-2">
                  {g.boards.map((b) => (
                    <button
                      key={b.id}
                      type="button"
                      onClick={() => setBoardId(b.id)}
                      className={cn(
                        'px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors border',
                        b.id === boardId
                          ? 'bg-blue-600 border-blue-500 text-white'
                          : 'bg-neutral-800/80 border-neutral-700 text-neutral-400 hover:text-neutral-200 hover:border-neutral-600'
                      )}
                    >
                      {b.title}
                    </button>
                  ))}
                </div>
              </div>
            ))}
          </div>

          {payload && (
            <div className="text-[11px] text-neutral-500 uppercase tracking-wider space-y-1">
              <p>{payload.title}</p>
              {payload.cached && refreshLabel(payload.nextRefreshApproxUtc) && (
                <p className="text-neutral-600 normal-case tracking-normal">
                  Cache rolls after {refreshLabel(payload.nextRefreshApproxUtc)}
                </p>
              )}
              {!payload.cached && (
                <p className="text-neutral-600 normal-case tracking-normal">Live query (not cached)</p>
              )}
            </div>
          )}

          <div className="rounded-2xl border border-neutral-800 overflow-hidden bg-neutral-950/50">
            {loadingBoard && (
              <div className="flex justify-center py-16">
                <Activity className="w-7 h-7 text-blue-500 animate-spin opacity-60" />
              </div>
            )}
            {!loadingBoard && payload && (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="text-left text-[10px] uppercase tracking-widest text-neutral-500 border-b border-neutral-800">
                      <th className="px-4 py-3 w-14">#</th>
                      <th className="px-4 py-3">Character</th>
                      <th className="px-4 py-3 text-right">{scoreHeader}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {payload.rows.length === 0 && (
                      <tr>
                        <td colSpan={3} className="px-4 py-10 text-center text-neutral-500">
                          No entries yet.
                        </td>
                      </tr>
                    )}
                    {payload.rows.map((row) => (
                      <tr
                        key={`${payload.id}-${row.characterGuid ?? row.account ?? row.character}-${row.rank}`}
                        className={cn(
                          'border-b border-neutral-800/80 last:border-0',
                          row.you && 'bg-blue-600/10'
                        )}
                      >
                        <td className="px-4 py-2.5 text-neutral-500 tabular-nums">{row.rank}</td>
                        <td className="px-4 py-2.5 font-medium text-white">
                          {leaderboardCharacterName(row, payload.id, openPetModal)}
                          {row.you && (
                            <span className="ml-2 text-[10px] font-bold uppercase text-blue-400">You</span>
                          )}
                        </td>
                        <td className="px-4 py-2.5 text-right tabular-nums text-neutral-200">
                          {formatScore(payload.id, row.score)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                  {payload.selfRow && !payload.selfRow.inTopList && (
                    <tbody>
                      <tr>
                        <td
                          colSpan={3}
                          className="px-4 pt-4 pb-1 text-[10px] uppercase tracking-widest text-neutral-500 border-t border-neutral-800"
                        >
                          Your placement
                        </td>
                      </tr>
                      <tr className="bg-blue-600/10 border-b border-neutral-800/80">
                        <td className="px-4 py-2.5 text-blue-300 tabular-nums font-semibold">
                          {payload.selfRow.rank}
                        </td>
                        <td className="px-4 py-2.5 font-medium text-white">
                          {leaderboardCharacterName(
                            {
                              rank: payload.selfRow.rank,
                              score: payload.selfRow.score,
                              character: payload.selfRow.character,
                              account: payload.selfRow.account,
                              characterGuid: payload.selfRow.characterGuid,
                              you: true,
                            },
                            payload.id,
                            openPetModal
                          )}
                          <span className="ml-2 text-[10px] font-bold uppercase text-blue-400">You</span>
                        </td>
                        <td className="px-4 py-2.5 text-right tabular-nums text-neutral-200">
                          {formatScore(payload.id, payload.selfRow.score)}
                        </td>
                      </tr>
                    </tbody>
                  )}
                </table>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
