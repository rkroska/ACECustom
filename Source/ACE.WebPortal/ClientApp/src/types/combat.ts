export type CombatMode = 'melee' | 'missile' | 'magic'
export type CombatDirection = 'monsterAttacksPlayer' | 'playerAttacksMonster'

export interface PlayerStub {
  guid: number
  name: string
  isOnline: boolean
}

export interface WeenieSearchResult {
  wcid: number
  name: string
  weenieType: string
}

export interface WeenieSkill {
  skillId: number
  initLevel: number
  name: string
}

export interface WeenieCombat {
  wcid: number
  name: string
  weenieType: string
  skills: WeenieSkill[]
  meleeDefense: number
  missileDefense: number
  magicDefense: number
  meleeAttack: number
  missileAttack: number
  magicAttack: number
}

export interface CombatConfig {
  melee: { enabled: boolean; defaultAggression: number }
  missile: { enabled: boolean; defaultAggression: number }
  magic: { enabled: boolean; defaultAggression: number }
}

export interface TripletDto {
  primaryLabel: string
  secondaryLabel: string
  primaryBaseline: number
  primaryServerDefault: number
  primaryTest: number
  secondaryBaseline: number
  secondaryServerDefault: number
  secondaryTest: number
}

export interface RangeRowDto {
  sweep: number
  a: number
  b: number
  c: number
}

export interface CombatPreviewResponse {
  mode: string
  direction: string
  skillSource: string
  defSkillId: number
  scalingEnabled: boolean
  attackSkill: number
  defenseSkill: number
  playerAttackBase: number
  playerDefenseBase: number
  monsterAttackBase: number
  monsterDefenseBase: number
  effectivePlayerAttack: number
  effectivePlayerDefense: number
  effectiveMonsterAttack: number
  effectiveMonsterDefense: number
  playerAccuracyMod: number
  playerOffenseMod: number
  playerDefenseMod: number
  playerDefenseFlat: number
  monsterOffenseMod: number
  testAggression: number
  triplet: TripletDto
  rangeRows: RangeRowDto[] | null
}

export interface CombatPreviewRequest {
  playerGuid?: number
  monsterWcid?: number
  mode: CombatMode
  direction: CombatDirection
  overridePlayerAttack?: number
  overridePlayerDefense?: number
  overrideMonsterAttack?: number
  overrideMonsterDefense?: number
  playerAccuracyMod?: number
  playerOffenseMod?: number
  playerDefenseMod?: number
  playerDefenseFlat?: number
  monsterOffenseMod?: number
  testAggression?: number
  rangeMin?: number
  rangeMax?: number
  rangeStep?: number
}
