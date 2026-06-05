import { api } from '../../services/api'
import type { QuestJourney, QuestStep } from '../../types/questBuilder'
import type { CreatureSearchResult } from '../../types/questBuilder'
import { normalizeItemList } from './itemSearchNormalize'

const nameCache = new Map<number, string>()

export function seedWeenieLabel(wcid: number, name: string) {
  if (wcid > 0 && name.trim()) nameCache.set(wcid, name.trim())
}

export function getCachedWeenieLabel(wcid: number): string | undefined {
  return nameCache.get(wcid)
}

function collectStepWcids(steps: QuestStep[] | undefined, out: Set<number>) {
  for (const step of steps ?? []) {
    if (step.wcid && step.wcid > 0) out.add(step.wcid)
    if (step.branches) {
      collectStepWcids(step.branches.onCooldown, out)
      collectStepWcids(step.branches.canComplete, out)
    }
  }
}

/** WCIDs that benefit from a DB name lookup (clone templates, rewards, step Give/Take). */
export function collectResolvableWcids(journey: QuestJourney): number[] {
  const ids = new Set<number>()
  const add = (wcid?: number) => {
    if (wcid && wcid > 0) ids.add(wcid)
  }

  add(journey.start.npcCloneFromWcid)
  add(journey.obtainItem.itemCloneFromWcid)
  add(journey.turnIn.rewardWcid)

  if (journey.obtainItem.source.kind === 'corpse') {
    add(journey.obtainItem.source.creature.templateWcid)
  } else {
    add(journey.obtainItem.source.objectCloneFromWcid)
    collectStepWcids(journey.obtainItem.source.pickupSteps, ids)
  }

  collectStepWcids(journey.start.introSteps, ids)
  collectStepWcids(journey.turnIn.giveSteps, ids)
  collectStepWcids(journey.turnIn.refuseSteps, ids)

  return [...ids]
}

export function seedJourneyLabels(journey: QuestJourney) {
  seedWeenieLabel(journey.obtainItem.itemWcid, journey.obtainItem.itemName)
  if (journey.obtainItem.source.kind === 'corpse') {
    seedWeenieLabel(journey.obtainItem.source.creature.wcid, journey.obtainItem.source.creature.name)
  } else {
    seedWeenieLabel(journey.obtainItem.source.objectWcid, journey.obtainItem.source.objectName)
  }
}

export async function lookupWeenieName(wcid: number): Promise<string | null> {
  if (!wcid || wcid <= 0) return null
  const cached = nameCache.get(wcid)
  if (cached) return cached

  try {
    const items = await api.get<unknown>(`/api/item/search?q=${wcid}&limit=1`)
    const hit = normalizeItemList(items).find((r) => r.wcid === wcid)
    if (hit?.name) {
      nameCache.set(wcid, hit.name)
      return hit.name
    }
  } catch {
    /* item search may miss pure creatures */
  }

  try {
    const creature = await api.get<CreatureSearchResult>(`/api/quest-builder/creature/${wcid}`)
    if (creature?.name) {
      nameCache.set(wcid, creature.name)
      return creature.name
    }
  } catch {
    /* not a creature weenie */
  }

  return null
}

export async function resolveWeenieLabels(wcids: number[]): Promise<Record<number, string>> {
  const labels: Record<number, string> = {}
  const pending = wcids.filter((id) => id > 0 && !nameCache.has(id))
  for (const id of wcids) {
    const c = nameCache.get(id)
    if (c) labels[id] = c
  }
  await Promise.all(
    pending.map(async (id) => {
      const name = await lookupWeenieName(id)
      if (name) labels[id] = name
    })
  )
  for (const id of wcids) {
    const c = nameCache.get(id)
    if (c) labels[id] = c
  }
  return labels
}

export function formatWeenie(wcid: number, labels: Record<number, string>, fallback?: string): string {
  if (!wcid) return fallback ?? '—'
  const name = labels[wcid] ?? getCachedWeenieLabel(wcid) ?? fallback
  if (name) return `${name} (${wcid})`
  return `WCID ${wcid}`
}
