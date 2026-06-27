import { useMemo, useState } from 'react'
import { Play, RotateCcw, FastForward, AlertTriangle } from 'lucide-react'
import type { QuestJourney } from '../../types/questBuilder'
import {
  cloneSimState,
  createInitialSimState,
  formatCooldown,
  simAdvanceTime,
  simGiveToNpc,
  simObtainItem,
  simReset,
  simTalkToNpc,
  simValidateJourney,
  type SimLogEntry,
  type SimPlayerState,
} from './questSimulator'

type Props = {
  journey: QuestJourney
  weenieLabels: Record<number, string>
}

function itemLabel(wcid: number, labels: Record<number, string>) {
  return labels[wcid] ? `${labels[wcid]} (${wcid})` : `WCID ${wcid}`
}

export default function QuestFlowSimulator({ journey, weenieLabels }: Props) {
  const [state, setState] = useState<SimPlayerState>(createInitialSimState)
  const [log, setLog] = useState<SimLogEntry[]>([])

  const warnings = useMemo(() => simValidateJourney(journey), [journey])
  const stampDeltas = useMemo(() => {
    const d: Record<string, number> = {
      [journey.meta.completionStamp.name]: journey.meta.completionStamp.cooldownSeconds,
    }
    if (journey.meta.startStamp && journey.start.grantStartStampOnUse) {
      d[journey.meta.startStamp.name] = journey.meta.startStamp.cooldownSeconds
    }
    if (journey.meta.pickupStamp) {
      d[journey.meta.pickupStamp.name] = journey.meta.pickupStamp.cooldownSeconds
    }
    return d
  }, [journey])

  const run = (fn: (next: SimPlayerState) => SimLogEntry[]) => {
    const next = cloneSimState(state)
    const entries = fn(next)
    setState(next)
    setLog((prev) => [...prev, ...entries])
  }

  const reset = () => {
    setState(simReset())
    setLog([])
  }

  const stampRows = Object.keys(stampDeltas)

  return (
    <div className="flex flex-col h-full min-h-0 border-l border-neutral-800 pl-4">
      <div className="shrink-0 mb-3">
        <h3 className="text-sm font-semibold text-white flex items-center gap-2">
          <Play className="w-4 h-4 text-blue-400" /> Quest simulator
        </h3>
        <p className="text-[10px] text-neutral-500 mt-1 leading-relaxed">
          Walk through the player journey in order. Stamps and cooldowns use your configured min_delta values.
        </p>
      </div>

      {warnings.length > 0 && (
        <div className="shrink-0 mb-3 space-y-1">
          {warnings.map((w, i) => (
            <div key={i} className="text-[10px] text-amber-200 bg-amber-950/30 border border-amber-900/50 rounded px-2 py-1 flex gap-1">
              <AlertTriangle className="w-3 h-3 shrink-0 mt-0.5" />
              {w}
            </div>
          ))}
        </div>
      )}

      <div className="shrink-0 rounded-lg border border-neutral-800 bg-neutral-950/60 p-2 mb-3 space-y-2 text-[10px]">
        <div className="text-neutral-500 font-bold uppercase">Your stamps</div>
        {stampRows.length === 0 ? (
          <p className="text-neutral-600">None</p>
        ) : (
          stampRows.map((name) => {
            const minDelta = stampDeltas[name]
            const remaining =
              state.stampTimes[name] != null
                ? Math.max(0, minDelta - (state.simTime - state.stampTimes[name]))
                : 0
            const has = state.stampTimes[name] != null
            return (
              <div key={name} className="flex justify-between gap-2 font-mono text-neutral-400">
                <span className="truncate" title={name}>
                  {name}
                </span>
                <span className={has ? 'text-blue-300' : 'text-neutral-600'}>
                  {has ? (remaining > 0 ? `cooldown ${formatCooldown(remaining)}` : 'stamped') : '—'}
                </span>
              </div>
            )
          })
        )}
        <div className="text-neutral-500 font-bold uppercase pt-1">Inventory</div>
        {state.inventory.length === 0 ? (
          <p className="text-neutral-600">Empty</p>
        ) : (
          state.inventory.map((row) => (
            <div key={row.wcid} className="text-neutral-300">
              {itemLabel(row.wcid, weenieLabels)} ×{row.count}
            </div>
          ))
        )}
      </div>

      <div className="shrink-0 flex flex-col gap-1.5 mb-3">
        <button
          type="button"
          onClick={() => run((next) => simTalkToNpc(journey, next))}
          className="w-full text-left px-3 py-2 rounded-lg bg-neutral-900 border border-neutral-700 text-xs text-white hover:border-neutral-500"
        >
          <span className="text-neutral-500">1.</span> Talk to NPC
        </button>
        <button
          type="button"
          onClick={() => run((next) => simObtainItem(journey, next))}
          className="w-full text-left px-3 py-2 rounded-lg bg-neutral-900 border border-neutral-700 text-xs text-white hover:border-neutral-500"
        >
          <span className="text-neutral-500">2.</span>{' '}
          {journey.obtainItem.source.kind === 'corpse' ? 'Loot mob corpse' : 'Use pickup object'}
        </button>
        <button
          type="button"
          onClick={() => run((next) => simGiveToNpc(journey, next, journey.obtainItem.itemWcid))}
          className="w-full text-left px-3 py-2 rounded-lg bg-neutral-900 border border-neutral-700 text-xs text-white hover:border-neutral-500"
        >
          <span className="text-neutral-500">3.</span> Turn in {journey.obtainItem.itemName}
        </button>
        <button
          type="button"
          onClick={() => run((next) => simGiveToNpc(journey, next, 0))}
          className="w-full text-left px-3 py-1.5 rounded-lg bg-neutral-950 border border-neutral-800 text-[10px] text-neutral-500 hover:border-neutral-600"
        >
          Give wrong item (Refuse)
        </button>
        <div className="flex gap-1">
          <button
            type="button"
            onClick={() => run((next) => simAdvanceTime(next, 3600))}
            className="flex-1 px-2 py-1.5 rounded bg-neutral-900 border border-neutral-700 text-[10px] text-neutral-400 hover:text-white flex items-center justify-center gap-1"
          >
            <FastForward className="w-3 h-3" /> +1h
          </button>
          <button
            type="button"
            onClick={() => run((next) => simAdvanceTime(next, 86400))}
            className="flex-1 px-2 py-1.5 rounded bg-neutral-900 border border-neutral-700 text-[10px] text-neutral-400 hover:text-white flex items-center justify-center gap-1"
          >
            <FastForward className="w-3 h-3" /> +24h
          </button>
          <button
            type="button"
            onClick={reset}
            className="px-2 py-1.5 rounded bg-neutral-900 border border-neutral-700 text-[10px] text-neutral-400 hover:text-white"
            title="Reset"
          >
            <RotateCcw className="w-3 h-3" />
          </button>
        </div>
      </div>

      <div className="flex-1 min-h-0 overflow-y-auto rounded-lg border border-neutral-800 bg-black/40 p-2">
        {log.length === 0 ? (
          <p className="text-[10px] text-neutral-600 text-center py-6">Run steps to simulate the quest flow.</p>
        ) : (
          <div className="space-y-1">
            {log.map((entry, i) => (
              <div
                key={i}
                className={`text-[10px] leading-snug ${
                  entry.level === 'success'
                    ? 'text-green-400'
                    : entry.level === 'error'
                      ? 'text-red-400'
                      : entry.level === 'warn'
                        ? 'text-amber-300'
                        : 'text-neutral-400'
                }`}
              >
                <span className="text-neutral-600 font-mono mr-1">[{entry.at}s]</span>
                {entry.message}
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
