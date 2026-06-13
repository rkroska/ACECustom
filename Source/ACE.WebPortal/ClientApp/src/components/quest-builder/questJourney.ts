import type {
  QuestActor,
  QuestFlow,
  QuestJourney,
  QuestJourneyObtain,
  QuestJourneyTurnIn,
  QuestPackage,
  QuestStamp,
  QuestStampConfig,
  QuestStep,
  LandscapeObtainSource,
} from '../../types/questBuilder'
import { defaultCompletionStamp, defaultPickupStamp, defaultStartStamp } from './StampConfigPanel'

function cloneSteps(steps: QuestStep[] | undefined): QuestStep[] {
  return JSON.parse(JSON.stringify(steps ?? [])) as QuestStep[]
}

function findFlow(actor: QuestActor | undefined, trigger: string): QuestFlow | undefined {
  return actor?.flows.find((f) => f.trigger === trigger)
}

function extractRewardFromGiveSteps(steps: QuestStep[]): { wcid: number; stack: number } {
  const visit = (list: QuestStep[]): { wcid: number; stack: number } | null => {
    for (const step of list) {
      if (step.type === 'Give' && step.wcid) return { wcid: step.wcid, stack: step.stack ?? 1 }
      if (step.branches) {
        const nested = visit(step.branches.canComplete ?? []) ?? visit(step.branches.onCooldown ?? [])
        if (nested) return nested
      }
    }
    return null
  }

  return visit(steps) ?? { wcid: 300004, stack: 10 }
}

function isLandscapeActor(actor: QuestActor): boolean {
  if (actor.role === 'landscapePickup') return true
  const triggers = new Set(actor.flows.map((f) => f.trigger))
  return (triggers.has('PickUp') || triggers.has('Use')) && !triggers.has('Give')
}

function findStampInSteps(steps: QuestStep[] | undefined, types: Set<string>): string | undefined {
  for (const step of steps ?? []) {
    if (types.has(step.type) && step.stamp) return step.stamp
    if (step.branches) {
      const fromBranch =
        findStampInSteps(step.branches.onCooldown, types) ?? findStampInSteps(step.branches.canComplete, types)
      if (fromBranch) return fromBranch
    }
  }
  return undefined
}

function stampRowToConfig(row: QuestStamp | undefined, fallback: QuestStampConfig): QuestStampConfig {
  if (!row) return fallback
  return {
    name: row.name,
    message: row.message,
    cooldownSeconds: row.minDelta != null && row.minDelta >= 0 ? row.minDelta : fallback.cooldownSeconds,
    maxSolves: row.maxSolves,
  }
}

function resolveStampConfigs(
  pkg: QuestPackage,
  giveSteps: QuestStep[],
  pickupSteps: QuestStep[],
  useSteps: QuestStep[]
): {
  startStamp: QuestStampConfig | null
  completionStamp: QuestStampConfig
  pickupStamp: QuestStampConfig | null
} {
  const slug = pkg.package || 'custom_quest'
  const defaultCompletion = defaultCompletionStamp(slug)
  const defaultPickup = defaultPickupStamp(slug)
  const defaultStart = defaultStartStamp(slug)

  const parsedIntro = parseIntroUseSteps(useSteps)
  const startName = parsedIntro.startStampName

  const completionName =
    findStampInSteps(giveSteps, new Set(['InqQuest', 'StampQuest'])) ?? pkg.stamps?.[0]?.name ?? defaultCompletion.name
  const pickupName = findStampInSteps(pickupSteps, new Set(['InqQuest', 'StampQuest']))

  const completionRow = pkg.stamps?.find((s) => s.name === completionName) ?? pkg.stamps?.[0]
  const pickupRow = pickupName ? pkg.stamps?.find((s) => s.name === pickupName) : undefined
  const startRow = startName ? pkg.stamps?.find((s) => s.name === startName) : undefined

  const completionStamp = stampRowToConfig(completionRow, { ...defaultCompletion, name: completionName })

  let startStamp: QuestStampConfig | null = null
  if (startName && startName !== completionName && startName !== pickupName) {
    startStamp = stampRowToConfig(startRow, { ...defaultStart, name: startName })
  }

  let pickupStamp: QuestStampConfig | null = null
  if (pickupName && pickupName !== completionName) {
    pickupStamp = stampRowToConfig(pickupRow, { ...defaultPickup, name: pickupName })
  } else if (pickupName && pickupSteps.some((s) => s.type === 'InqQuest')) {
    pickupStamp = stampRowToConfig(pickupRow ?? completionRow, { ...defaultPickup, name: pickupName })
  }

  return { startStamp, completionStamp, pickupStamp }
}

/** Unwrap Use flow when it is InqQuest(start) → dialog + StampQuest. */
export function parseIntroUseSteps(useSteps: QuestStep[]): {
  introSteps: QuestStep[]
  grantStartStampOnUse: boolean
  startStampName?: string
} {
  const steps = cloneSteps(useSteps)
  const head = steps[0]
  if (
    steps.length === 1 &&
    head?.type === 'InqQuest' &&
    head.stamp &&
    head.branches?.canComplete?.some((s) => s.type === 'StampQuest' && s.stamp === head.stamp)
  ) {
    const stamp = head.stamp
    const canComplete = cloneSteps(head.branches.canComplete)
    const introSteps = canComplete.filter((s) => !(s.type === 'StampQuest' && s.stamp === stamp))
    return {
      introSteps: introSteps.length > 0 ? introSteps : canComplete,
      grantStartStampOnUse: true,
      startStampName: stamp,
    }
  }
  return { introSteps: steps, grantStartStampOnUse: false }
}

/** Unwrap pickup when outer InqQuest is start-stamp prerequisite. */
export function parsePickupSteps(
  pickupSteps: QuestStep[],
  startStampName: string | undefined
): { pickupSteps: QuestStep[]; requireStartStampForPickup: boolean } {
  const steps = cloneSteps(pickupSteps)
  const head = steps[0]
  if (
    startStampName &&
    steps.length === 1 &&
    head?.type === 'InqQuest' &&
    head.stamp === startStampName &&
    head.branches?.onCooldown
  ) {
    return { pickupSteps: cloneSteps(head.branches.onCooldown), requireStartStampForPickup: true }
  }
  return { pickupSteps: steps, requireStartStampForPickup: false }
}

export function effectiveIntroSteps(journey: QuestJourney): QuestStep[] {
  const { startStamp } = journey.meta
  if (journey.start.grantStartStampOnUse && startStamp) {
    return syncIntroWithStartStamp(journey.start.introSteps, startStamp, journey.start.npcName)
  }
  return cloneSteps(journey.start.introSteps)
}

export function effectiveLandscapePickupSteps(journey: QuestJourney): QuestStep[] | null {
  if (journey.obtainItem.source.kind !== 'landscape') return null
  const ls = journey.obtainItem.source
  const { pickupStamp, startStamp } = journey.meta
  const itemWcid = journey.obtainItem.itemWcid
  let pickupSteps = syncPickupSteps(
    ls.pickupSteps,
    pickupStamp ?? defaultPickupStamp(journey.meta.package),
    itemWcid,
    ls.useQuestGate
  )
  if (ls.requireStartStampForPickup && startStamp) {
    pickupSteps = wrapPickupRequiresStart(pickupSteps, startStamp.name, journey.start.npcName)
  }
  return pickupSteps
}

export function syncIntroWithStartStamp(
  introSteps: QuestStep[],
  startStamp: QuestStampConfig,
  npcName: string
): QuestStep[] {
  const stamp = startStamp.name
  const dialog = cloneSteps(introSteps)
  if (dialog.length === 0) {
    dialog.push({ type: 'Tell', text: `Greetings. I have a task for you, if you are willing.` })
  }
  return [
    {
      type: 'InqQuest',
      stamp,
      branches: {
        onCooldown: [
          {
            type: 'Tell',
            text: `You have already spoken with ${npcName}. Go complete what was asked.`,
          },
        ],
        canComplete: [...dialog, { type: 'StampQuest', stamp }],
      },
    },
  ]
}

export function wrapPickupRequiresStart(
  pickupSteps: QuestStep[],
  startStampName: string,
  npcName: string
): QuestStep[] {
  return [
    {
      type: 'InqQuest',
      stamp: startStampName,
      branches: {
        onCooldown: cloneSteps(pickupSteps),
        canComplete: [
          {
            type: 'DirectBroadcast',
            text: `You must speak with ${npcName} before you can take this.`,
          },
        ],
      },
    },
  ]
}

export function packageToJourney(pkg: QuestPackage): QuestJourney {
  const item = pkg.items[0]
  const giver =
    pkg.actors.find((a) => a.role === 'questGiver' || findFlow(a, 'Give')) ?? pkg.actors[0]
  const landscapeActor = pkg.actors.find((a) => isLandscapeActor(a))
  const creature = pkg.creatures[0]

  const useFlow = findFlow(giver, 'Use')
  const giveFlow = findFlow(giver, 'Give')
  const refuseFlow = findFlow(giver, 'Refuse')
  const reward = extractRewardFromGiveSteps(giveFlow?.steps ?? [])

  const landscapeFlow =
    landscapeActor &&
    (findFlow(landscapeActor, 'PickUp') ?? findFlow(landscapeActor, 'Use') ?? landscapeActor.flows[0])
  const rawPickupSteps = cloneSteps(landscapeFlow?.steps)
  const parsedIntro = parseIntroUseSteps(useFlow?.steps ?? [])
  const { startStamp, completionStamp, pickupStamp } = resolveStampConfigs(
    pkg,
    giveFlow?.steps ?? [],
    rawPickupSteps,
    useFlow?.steps ?? []
  )
  const parsedPickup = parsePickupSteps(rawPickupSteps, startStamp?.name ?? parsedIntro.startStampName)

  let obtainSource: QuestJourneyObtain['source']
  if (creature) {
    obtainSource = { kind: 'corpse', creature: { ...creature } }
  } else if (landscapeActor) {
    const hasInq = findStampInSteps(parsedPickup.pickupSteps, new Set(['InqQuest'])) != null
    obtainSource = {
      kind: 'landscape',
      objectWcid: landscapeActor.wcid,
      objectName: landscapeActor.name,
      objectCloneFromWcid: landscapeActor.cloneFromWcid,
      trigger: (landscapeFlow?.trigger === 'PickUp' ? 'PickUp' : 'Use') as 'PickUp' | 'Use',
      useQuestGate: hasInq,
      requireStartStampForPickup: parsedPickup.requireStartStampForPickup,
      pickupSteps: parsedPickup.pickupSteps,
    }
  } else {
    obtainSource = {
      kind: 'corpse',
      creature: {
        wcid: (giver?.wcid ?? 0) + 2,
        name: 'Quest Target',
        templateWcid: 78780092,
        patchExisting: false,
        dropItemWcid: item?.wcid ?? 0,
        dropStack: 1,
      },
    }
  }

  return {
    meta: {
      package: pkg.package,
      description: pkg.description,
      startStamp,
      completionStamp,
      pickupStamp,
    },
    start: {
      npcWcid: giver?.wcid ?? 0,
      npcName: giver?.name ?? 'Quest Giver',
      npcCloneFromWcid: giver?.cloneFromWcid,
      introSteps: parsedIntro.introSteps,
      grantStartStampOnUse: parsedIntro.grantStartStampOnUse,
    },
    obtainItem: {
      itemWcid: item?.wcid ?? 0,
      itemName: item?.name ?? 'Quest Item',
      itemLongDesc: item?.longDesc,
      itemCloneFromWcid: item?.cloneFromWcid,
      source: obtainSource,
    },
    turnIn: {
      giveSteps: cloneSteps(giveFlow?.steps),
      refuseSteps: cloneSteps(refuseFlow?.steps),
      rewardWcid: reward.wcid,
      rewardStack: reward.stack,
    },
  }
}

export function defaultLandscapePickup(
  pickupStamp: QuestStampConfig,
  itemWcid: number,
  useGate: boolean
): QuestStep[] {
  const stamp = pickupStamp.name
  if (!useGate) {
    return [
      { type: 'DirectBroadcast', text: 'You pick up the item.' },
      { type: 'Give', wcid: itemWcid, stack: 1 },
    ]
  }
  return [
    {
      type: 'InqQuest',
      stamp,
      branches: {
        onCooldown: [{ type: 'DirectBroadcast', text: 'You have already taken this recently.' }],
        canComplete: [
          { type: 'DirectBroadcast', text: 'You pick up the item.' },
          { type: 'Give', wcid: itemWcid, stack: 1 },
          { type: 'StampQuest', stamp },
        ],
      },
    },
  ]
}

function syncStampInSteps(steps: QuestStep[], stamp: string) {
  for (const s of steps) {
    if (s.type === 'InqQuest' || s.type === 'StampQuest') s.stamp = stamp
    if (s.branches) {
      syncStampInSteps(s.branches.onCooldown, stamp)
      syncStampInSteps(s.branches.canComplete, stamp)
    }
  }
}

export function syncPickupSteps(
  steps: QuestStep[],
  pickupStamp: QuestStampConfig,
  itemWcid: number,
  useGate: boolean
): QuestStep[] {
  if (steps.length === 0) return defaultLandscapePickup(pickupStamp, itemWcid, useGate)
  const next = cloneSteps(steps)
  syncStampInSteps(next, pickupStamp.name)
  return next
}

function syncCompletionGiveSteps(
  turnIn: QuestJourneyTurnIn,
  completionStamp: QuestStampConfig,
  itemWcid: number
): QuestStep[] {
  const steps = cloneSteps(turnIn.giveSteps)
  const patchBranch = (list: QuestStep[]) => {
    for (let i = 0; i < list.length; i++) {
      if (list[i].type === 'Give') list[i] = { ...list[i], wcid: turnIn.rewardWcid, stack: turnIn.rewardStack }
      if (list[i].type === 'TakeItems') list[i] = { ...list[i], wcid: itemWcid, stack: 1 }
      if (list[i].branches) {
        patchBranch(list[i].branches!.onCooldown)
        patchBranch(list[i].branches!.canComplete)
      }
    }
  }
  patchBranch(steps)
  syncStampInSteps(steps, completionStamp.name)
  return steps
}

export function effectiveTurnInGiveSteps(journey: QuestJourney): QuestStep[] {
  return syncCompletionGiveSteps(journey.turnIn, journey.meta.completionStamp, journey.obtainItem.itemWcid)
}

function stampConfigToRow(config: QuestStampConfig): QuestStamp {
  return {
    name: config.name,
    message: config.message,
    minDelta: config.cooldownSeconds,
    maxSolves: config.maxSolves ?? 1,
  }
}

export function journeyToPackage(journey: QuestJourney): QuestPackage {
  const { startStamp, completionStamp, pickupStamp } = journey.meta
  const itemWcid = journey.obtainItem.itemWcid

  const giver: QuestActor = {
    wcid: journey.start.npcWcid,
    name: journey.start.npcName,
    cloneFromWcid: journey.start.npcCloneFromWcid,
    role: 'questGiver',
    flows: [
      { trigger: 'Use', steps: effectiveIntroSteps(journey) },
      {
        trigger: 'Give',
        giveWcid: itemWcid,
        steps: syncCompletionGiveSteps(journey.turnIn, completionStamp, itemWcid),
      },
      { trigger: 'Refuse', steps: cloneSteps(journey.turnIn.refuseSteps) },
    ],
  }

  const actors: QuestActor[] = [giver]
  const creatures: QuestPackage['creatures'] = []
  const stamps: QuestStamp[] = []

  if (journey.start.grantStartStampOnUse && startStamp) {
    stamps.push(
      stampConfigToRow({
        ...startStamp,
        maxSolves: startStamp.maxSolves ?? 1,
      })
    )
  }

  if (journey.obtainItem.source.kind === 'landscape') {
    const ls = journey.obtainItem.source
    const activePickup = ls.useQuestGate && pickupStamp
    const pickupSteps = effectiveLandscapePickupSteps(journey) ?? []

    if (activePickup && pickupStamp) {
      stamps.push(stampConfigToRow({ ...pickupStamp, maxSolves: pickupStamp.maxSolves ?? -1 }))
    }

    const landscapeFlow: QuestFlow = { trigger: ls.trigger, steps: pickupSteps }

    if (ls.objectWcid === giver.wcid) {
      if (ls.trigger === 'Use') {
        const existing = giver.flows.find((f) => f.trigger === 'Use')
        if (existing) {
          giver.flows = giver.flows.map((f) =>
            f.trigger === 'Use' ? { ...f, steps: [...f.steps, ...landscapeFlow.steps] } : f
          )
        } else {
          giver.flows = [...giver.flows, landscapeFlow]
        }
      } else {
        giver.flows = [...giver.flows.filter((f) => f.trigger !== ls.trigger), landscapeFlow]
      }
      giver.role = 'questGiver'
    } else {
      actors.push({
        wcid: ls.objectWcid,
        name: ls.objectName,
        cloneFromWcid: ls.objectCloneFromWcid,
        role: 'landscapePickup',
        flows: [landscapeFlow],
      })
    }
  } else {
    const c = { ...journey.obtainItem.source.creature, dropItemWcid: itemWcid }
    creatures.push(c)
  }

  stamps.push(
    stampConfigToRow({
      ...completionStamp,
      maxSolves: completionStamp.maxSolves ?? 1,
    })
  )

  return {
    package: journey.meta.package,
    description: journey.meta.description,
    cooldownSeconds: completionStamp.cooldownSeconds,
    stamps,
    items: [
      {
        wcid: itemWcid,
        name: journey.obtainItem.itemName,
        longDesc: journey.obtainItem.itemLongDesc,
        cloneFromWcid: journey.obtainItem.itemCloneFromWcid,
      },
    ],
    actors,
    creatures,
  }
}

export function journeyPhaseSummary(
  journey: QuestJourney,
  phase: 'start' | 'obtainItem' | 'turnIn',
  labels: Record<number, string> = {}
): string {
  const rewardName = labels[journey.turnIn.rewardWcid] ?? `WCID ${journey.turnIn.rewardWcid}`
  switch (phase) {
    case 'start': {
      const grant = journey.start.grantStartStampOnUse && journey.meta.startStamp
      return `${journey.start.npcName} — ${journey.start.introSteps.length} intro step(s)${
        grant ? ` (start stamp: ${journey.meta.startStamp!.name})` : ''
      }`
    }
    case 'obtainItem': {
      const src = journey.obtainItem.source
      if (src.kind === 'corpse') {
        return `${journey.obtainItem.itemName} from ${src.creature.name}`
      }
      const gate = src.useQuestGate && journey.meta.pickupStamp
      return `${journey.obtainItem.itemName} from ${src.objectName}${gate ? ` (stamp: ${journey.meta.pickupStamp!.name})` : ''}`
    }
    case 'turnIn':
      return `Turn in ${journey.obtainItem.itemName} → ${journey.turnIn.rewardStack}× ${rewardName} (stamp: ${journey.meta.completionStamp.name})`
    default:
      return ''
  }
}

export function defaultLandscapeSource(
  objectWcid: number,
  itemWcid: number,
  pickupStamp: QuestStampConfig
): LandscapeObtainSource {
  return {
    kind: 'landscape',
    objectWcid,
    objectName: 'Quest Pickup Object',
    objectCloneFromWcid: 78780023,
    trigger: 'Use',
    useQuestGate: true,
    requireStartStampForPickup: false,
    pickupSteps: defaultLandscapePickup(pickupStamp, itemWcid, true),
  }
}

/** Enable landscape pickup gate with a distinct pickup stamp (won't block NPC turn-in). */
export function enableLandscapePickupGate(journey: QuestJourney): QuestJourney {
  const pickupStamp = journey.meta.pickupStamp ?? defaultPickupStamp(journey.meta.package)
  if (journey.obtainItem.source.kind !== 'landscape') return journey
  const src = journey.obtainItem.source
  return {
    ...journey,
    meta: { ...journey.meta, pickupStamp },
    obtainItem: {
      ...journey.obtainItem,
      source: {
        ...src,
        useQuestGate: true,
        pickupSteps: defaultLandscapePickup(pickupStamp, journey.obtainItem.itemWcid, true),
      },
    },
  }
}
