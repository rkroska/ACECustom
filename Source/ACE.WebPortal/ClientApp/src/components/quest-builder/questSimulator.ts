import { effectiveIntroSteps, effectiveLandscapePickupSteps, effectiveTurnInGiveSteps, journeyToPackage } from './questJourney'
import type { QuestJourney, QuestStep } from '../../types/questBuilder'

export type SimLogLevel = 'info' | 'success' | 'warn' | 'error'

export interface SimLogEntry {
  level: SimLogLevel
  message: string
  at: number
}

export interface SimPlayerState {
  /** stamp name -> sim seconds when last stamped */
  stampTimes: Record<string, number>
  inventory: { wcid: number; count: number }[]
  simTime: number
}

export function createInitialSimState(): SimPlayerState {
  return { stampTimes: {}, inventory: [], simTime: 0 }
}

export function cloneSimState(state: SimPlayerState): SimPlayerState {
  return {
    simTime: state.simTime,
    stampTimes: { ...state.stampTimes },
    inventory: state.inventory.map((i) => ({ ...i })),
  }
}

function hasItem(state: SimPlayerState, wcid: number): boolean {
  return (state.inventory.find((i) => i.wcid === wcid)?.count ?? 0) > 0
}

function addItem(state: SimPlayerState, wcid: number, stack: number) {
  const row = state.inventory.find((i) => i.wcid === wcid)
  if (row) row.count += stack
  else state.inventory.push({ wcid, count: stack })
}

function removeItem(state: SimPlayerState, wcid: number, stack: number): boolean {
  const row = state.inventory.find((i) => i.wcid === wcid)
  if (!row || row.count < stack) return false
  row.count -= stack
  if (row.count <= 0) state.inventory = state.inventory.filter((i) => i.wcid !== wcid)
  return true
}

function stampCooldownRemaining(state: SimPlayerState, stampName: string, minDelta: number): number {
  const t = state.stampTimes[stampName]
  if (t == null) return 0
  const elapsed = state.simTime - t
  return Math.max(0, minDelta - elapsed)
}

function inqQuestBranch(
  state: SimPlayerState,
  stampName: string,
  minDelta: number
): 'onCooldown' | 'canComplete' {
  const t = state.stampTimes[stampName]
  if (t == null) return 'canComplete'
  if (minDelta <= 0) return 'onCooldown'
  return stampCooldownRemaining(state, stampName, minDelta) > 0 ? 'onCooldown' : 'canComplete'
}

function runSteps(
  steps: QuestStep[],
  state: SimPlayerState,
  stampDeltas: Record<string, number>,
  log: SimLogEntry[]
): void {
  for (const step of steps) {
    switch (step.type) {
      case 'Tell':
      case 'DirectBroadcast':
        log.push({ level: 'info', message: step.text || `(${step.type})`, at: state.simTime })
        if (step.delay) state.simTime += step.delay
        break
      case 'Motion':
        log.push({ level: 'info', message: 'NPC gestures.', at: state.simTime })
        break
      case 'Give':
        if (step.wcid) {
          addItem(state, step.wcid, step.stack ?? 1)
          log.push({ level: 'success', message: `Received item WCID ${step.wcid} ×${step.stack ?? 1}.`, at: state.simTime })
        }
        break
      case 'TakeItems':
        if (step.wcid) {
          const ok = removeItem(state, step.wcid, step.stack ?? 1)
          log.push({
            level: ok ? 'success' : 'error',
            message: ok
              ? `Gave item WCID ${step.wcid} ×${step.stack ?? 1}.`
              : `Missing item WCID ${step.wcid}.`,
            at: state.simTime,
          })
        }
        break
      case 'StampQuest':
        if (step.stamp) {
          state.stampTimes[step.stamp] = state.simTime
          log.push({ level: 'success', message: `Stamped quest «${step.stamp}».`, at: state.simTime })
        }
        break
      case 'InqQuest': {
        const stamp = step.stamp ?? ''
        const minDelta = stampDeltas[stamp] ?? 0
        const branch = inqQuestBranch(state, stamp, minDelta)
        log.push({
          level: 'info',
          message: `InqQuest «${stamp}» → ${branch === 'onCooldown' ? 'already stamped / on cooldown' : 'may proceed'}.`,
          at: state.simTime,
        })
        const branchSteps = branch === 'onCooldown' ? step.branches?.onCooldown : step.branches?.canComplete
        runSteps(branchSteps ?? [], state, stampDeltas, log)
        break
      }
      default:
        log.push({ level: 'warn', message: `Skipped unsupported step type: ${step.type}`, at: state.simTime })
    }
  }
}

function buildStampDeltas(journey: QuestJourney): Record<string, number> {
  const deltas: Record<string, number> = {}
  deltas[journey.meta.completionStamp.name] = journey.meta.completionStamp.cooldownSeconds
  if (journey.meta.startStamp && journey.start.grantStartStampOnUse) {
    deltas[journey.meta.startStamp.name] = journey.meta.startStamp.cooldownSeconds
  }
  if (journey.meta.pickupStamp) {
    deltas[journey.meta.pickupStamp.name] = journey.meta.pickupStamp.cooldownSeconds
  }
  return deltas
}

export function simTalkToNpc(journey: QuestJourney, state: SimPlayerState): SimLogEntry[] {
  const log: SimLogEntry[] = []
  log.push({ level: 'info', message: `Talk to ${journey.start.npcName} (Use).`, at: state.simTime })
  runSteps(effectiveIntroSteps(journey), state, buildStampDeltas(journey), log)
  return log
}

export function simObtainItem(journey: QuestJourney, state: SimPlayerState): SimLogEntry[] {
  const log: SimLogEntry[] = []
  const itemWcid = journey.obtainItem.itemWcid
  const deltas = buildStampDeltas(journey)

  if (journey.obtainItem.source.kind === 'corpse') {
    const mob = journey.obtainItem.source.creature.name
    if (hasItem(state, itemWcid)) {
      log.push({ level: 'warn', message: `You already have ${journey.obtainItem.itemName}.`, at: state.simTime })
      return log
    }
    addItem(state, itemWcid, journey.obtainItem.source.creature.dropStack ?? 1)
    log.push({
      level: 'success',
      message: `Looted ${journey.obtainItem.itemName} from ${mob} (no pickup stamp on corpse).`,
      at: state.simTime,
    })
    return log
  }

  log.push({
    level: 'info',
    message: `${journey.obtainItem.source.trigger} ${journey.obtainItem.source.objectName}.`,
    at: state.simTime,
  })
  const pickupSteps = effectiveLandscapePickupSteps(journey) ?? journey.obtainItem.source.pickupSteps
  runSteps(pickupSteps, state, deltas, log)
  return log
}

export function simGiveToNpc(journey: QuestJourney, state: SimPlayerState, itemWcid: number): SimLogEntry[] {
  const log: SimLogEntry[] = []
  const expected = journey.obtainItem.itemWcid
  const deltas = buildStampDeltas(journey)

  log.push({ level: 'info', message: `Give to ${journey.start.npcName}.`, at: state.simTime })

  if (itemWcid !== expected) {
    log.push({ level: 'warn', message: 'Wrong item — Refuse flow.', at: state.simTime })
    runSteps(journey.turnIn.refuseSteps, state, deltas, log)
    return log
  }

  if (!hasItem(state, expected)) {
    log.push({ level: 'error', message: `You do not have ${journey.obtainItem.itemName} to turn in.`, at: state.simTime })
    return log
  }

  runSteps(effectiveTurnInGiveSteps(journey), state, deltas, log)
  return log
}

export function simAdvanceTime(state: SimPlayerState, seconds: number): SimLogEntry[] {
  state.simTime += seconds
  return [{ level: 'info', message: `Time advanced ${seconds}s (sim t=${state.simTime}).`, at: state.simTime }]
}

export function simReset(): SimPlayerState {
  return createInitialSimState()
}

export function simValidateJourney(journey: QuestJourney): string[] {
  const warnings: string[] = []
  const pkg = journeyToPackage(journey)
  if (
    journey.obtainItem.source.kind === 'landscape' &&
    journey.obtainItem.source.useQuestGate &&
    journey.meta.pickupStamp &&
    journey.meta.pickupStamp.name === journey.meta.completionStamp.name
  ) {
    warnings.push('Pickup and completion use the same stamp — turn-in will fail after pickup until cooldown expires.')
  }
  if (journey.obtainItem.source.kind === 'landscape' && journey.obtainItem.source.useQuestGate && !journey.meta.pickupStamp) {
    warnings.push('Landscape pickup gate is on but no pickup stamp is configured.')
  }
  if (
    journey.obtainItem.source.kind === 'landscape' &&
    journey.obtainItem.source.requireStartStampForPickup &&
    (!journey.start.grantStartStampOnUse || !journey.meta.startStamp)
  ) {
    warnings.push('Pickup requires NPC talk but start stamp is not enabled on the Start phase.')
  }
  const stampNames = [
    journey.meta.completionStamp.name,
    journey.meta.pickupStamp?.name,
    journey.meta.startStamp?.name,
  ].filter(Boolean) as string[]
  if (new Set(stampNames).size !== stampNames.length) {
    warnings.push('Two or more stamp roles share the same name — flows will interfere.')
  }
  if (!journey.turnIn.giveSteps.some((s) => s.type === 'InqQuest') && journey.turnIn.giveSteps.length > 0) {
    warnings.push('Turn-in Give flow has no InqQuest — completion cooldown may not apply.')
  }
  if (pkg.stamps.length === 0) warnings.push('Package exports no quest stamp rows.')
  return warnings
}

export function formatCooldown(sec: number): string {
  if (sec <= 0) return 'ready'
  if (sec < 60) return `${sec}s`
  if (sec < 3600) return `${Math.floor(sec / 60)}m`
  return `${(sec / 3600).toFixed(1)}h`
}
