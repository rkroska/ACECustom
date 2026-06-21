export interface QuestPackage {
  package: string
  description?: string
  cooldownSeconds: number
  stamps: QuestStamp[]
  items: QuestItem[]
  actors: QuestActor[]
  creatures: QuestCreature[]
}

export interface QuestStamp {
  name: string
  message?: string
  minDelta?: number
  maxSolves?: number
}

export interface QuestItem {
  wcid: number
  name: string
  longDesc?: string
  cloneFromWcid?: number
}

export type QuestActorRole = 'questGiver' | 'landscapePickup'

export interface QuestActor {
  wcid: number
  name: string
  cloneFromWcid?: number
  /** Used by journey sync for export labels and validation */
  role?: QuestActorRole
  flows: QuestFlow[]
}

export interface QuestFlow {
  trigger: 'Use' | 'Give' | 'Refuse' | 'PickUp' | 'Portal'
  giveWcid?: number
  steps: QuestStep[]
}

export interface QuestStep {
  /** Client-only stable key for React list reconciliation (not sent to server). */
  _key?: string
  type: string
  text?: string
  stamp?: string
  wcid?: number
  stack?: number
  delay?: number
  motion?: string
  branches?: QuestStepBranches
}

let _stepKeyCounter = 0
/** Stamp a stable _key onto a step if it doesn't already have one. */
export function ensureStepKey<T extends QuestStep>(step: T): T {
  if (!step._key) step._key = `sk_${++_stepKeyCounter}_${Date.now()}`
  return step
}

export interface QuestStepBranches {
  onCooldown: QuestStep[]
  canComplete: QuestStep[]
}

export interface QuestCreature {
  wcid: number
  name: string
  templateWcid?: number
  patchExisting: boolean
  dropItemWcid: number
  dropStack: number
}

export interface QuestValidationIssue {
  severity: 'error' | 'warning'
  code: string
  message: string
}

export interface QuestValidationResult {
  ok: boolean
  issues: QuestValidationIssue[]
}

export interface QuestTemplateInfo {
  id: string
  label: string
  description: string
}

export interface QuestImportResult {
  ok: boolean
  message: string
  package?: QuestPackage
  warnings?: string[]
}

export interface NextWcidResult {
  wcid: number
  rangeStart: number
  rangeEnd: number
}

export interface CreatureSearchResult {
  wcid: number
  name: string
  className: string
  weenieType: string
}

export type JourneyPhaseId = 'start' | 'obtainItem' | 'turnIn'

export type ObtainItemSourceKind = 'corpse' | 'landscape'

/** One row in the `quest` table — name + min_delta cooldown. */
export interface QuestStampConfig {
  name: string
  message?: string
  cooldownSeconds: number
  /** -1 = unlimited solves (typical for daily pickup stamps). Default 1 for completion. */
  maxSolves?: number
}

export interface QuestJourneyMeta {
  package: string
  description?: string
  /** Granted on first NPC Use when grantStartStampOnUse is enabled; can gate landscape pickup. */
  startStamp: QuestStampConfig | null
  /** NPC Give / turn-in completion (InqQuest + StampQuest on hand-in). */
  completionStamp: QuestStampConfig
  /** Landscape pickup gate only; separate timer from completion (e.g. echo pickup vs lens turn-in). */
  pickupStamp: QuestStampConfig | null
}

export interface QuestJourneyStart {
  npcWcid: number
  npcName: string
  npcCloneFromWcid?: number
  /** Dialog steps (wrapped with InqQuest + StampQuest on export when grantStartStampOnUse). */
  introSteps: QuestStep[]
  /** Wrap Use flow: first talk stamps startStamp; repeat talk uses onCooldown branch. */
  grantStartStampOnUse: boolean
}

export interface CorpseObtainSource {
  kind: 'corpse'
  creature: QuestCreature
}

export interface LandscapeObtainSource {
  kind: 'landscape'
  objectWcid: number
  objectName: string
  objectCloneFromWcid?: number
  trigger: 'PickUp' | 'Use'
  /** When true, pickup flow uses InqQuest + stamp gate before giving item */
  useQuestGate: boolean
  /** When true, pickup requires meta.startStamp (player must talk to NPC first). */
  requireStartStampForPickup: boolean
  pickupSteps: QuestStep[]
}

export interface QuestJourneyObtain {
  itemWcid: number
  itemName: string
  itemLongDesc?: string
  itemCloneFromWcid?: number
  source: CorpseObtainSource | LandscapeObtainSource
}

export interface QuestJourneyTurnIn {
  giveSteps: QuestStep[]
  refuseSteps: QuestStep[]
  rewardWcid: number
  rewardStack: number
}

export interface QuestJourney {
  meta: QuestJourneyMeta
  start: QuestJourneyStart
  obtainItem: QuestJourneyObtain
  turnIn: QuestJourneyTurnIn
}
