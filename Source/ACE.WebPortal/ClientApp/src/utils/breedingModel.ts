/**
 * Pure, dependency-free model of the server's pet breeding math
 * (ACE.Server/WorldObjects/PetDevice_Breeding.cs and the Awakened Blessing handler).
 *
 * Everything random goes through the injected `Rng` so a breed can be replayed
 * with a fixed seed. Draw order inside `breed()` is fixed and documented so
 * `runSelfCheck()` can compare against a known outcome:
 *
 *   1. inheritance rolls, one per line, in the order of INHERIT_LINES
 *   2. stat mutation roll
 *   3. stat line pick (only if the stat roll mutated and a line is eligible)
 *   4. potency mutation roll
 *   5. Awakened Blessing pick (only if the guardian is enabled, the breed mutated,
 *      and the parents killed the guardian)
 *
 * Species / palette selection is NOT part of this module: the server picks the
 * species donor with its own coin flip and rolls a palette from a server-side
 * pool. `BreedResult.paletteRolled` / `paletteUsesVibrantPool` tell the caller
 * whether (and from which pool) a palette must be rolled.
 */

export type Rng = () => number

export interface BreedingConfig {
  baseMutationChance: number
  potencyMutationChance: number
  mutationDecayRate: number
  mutationMinFloor: number
  damageMutationStep: number
  drMutationStep: number
  critMutationStep: number
  vitalityMutationStep: number
  potencyMutationStep: number
  potencySoftCap: number
  /** 0 = no hard cap */
  potencyHardCap: number
  /** 0 = no stored-potency cap */
  potencyMaxStored: number
  /** 0 = uncapped per-line mutation count */
  maxStatMutations: number
  forceMutation: boolean
  guardianEnabled: boolean
}

/** Server fallback defaults (PropertyManager). Stored shard values override these live. */
export const DEFAULT_BREEDING_CONFIG: BreedingConfig = {
  baseMutationChance: 0.05,
  potencyMutationChance: 0.03,
  mutationDecayRate: 0,
  mutationMinFloor: 0.02,
  damageMutationStep: 10,
  drMutationStep: 10,
  critMutationStep: 5,
  vitalityMutationStep: 50,
  potencyMutationStep: 25,
  potencySoftCap: 1000,
  potencyHardCap: 0,
  potencyMaxStored: 0,
  maxStatMutations: 0,
  forceMutation: false,
  guardianEnabled: false,
}

/** Merge a (possibly partial / untyped) /api/visualizer/breeding-config payload over the defaults. */
export function normalizeBreedingConfig(raw: unknown): BreedingConfig {
  const src = (raw && typeof raw === 'object' ? raw : {}) as Record<string, unknown>
  const num = (key: keyof BreedingConfig): number => {
    const v = src[key]
    return typeof v === 'number' && Number.isFinite(v) ? v : (DEFAULT_BREEDING_CONFIG[key] as number)
  }
  const bool = (key: keyof BreedingConfig): boolean => {
    const v = src[key]
    return typeof v === 'boolean' ? v : (DEFAULT_BREEDING_CONFIG[key] as boolean)
  }
  return {
    baseMutationChance: num('baseMutationChance'),
    potencyMutationChance: num('potencyMutationChance'),
    mutationDecayRate: num('mutationDecayRate'),
    mutationMinFloor: num('mutationMinFloor'),
    damageMutationStep: Math.trunc(num('damageMutationStep')),
    drMutationStep: Math.trunc(num('drMutationStep')),
    critMutationStep: Math.trunc(num('critMutationStep')),
    vitalityMutationStep: Math.trunc(num('vitalityMutationStep')),
    potencyMutationStep: Math.trunc(num('potencyMutationStep')),
    potencySoftCap: Math.trunc(num('potencySoftCap')),
    potencyHardCap: Math.trunc(num('potencyHardCap')),
    potencyMaxStored: Math.trunc(num('potencyMaxStored')),
    maxStatMutations: Math.trunc(num('maxStatMutations')),
    forceMutation: bool('forceMutation'),
    guardianEnabled: bool('guardianEnabled'),
  }
}

/** The heritable state of one pet, exactly as the server stores it on the pet device. */
export interface PetGenetics {
  /** Clean base ratings rolled by loot; mutations are NOT folded into these. */
  gearDamage: number
  gearDamageResist: number
  gearCrit: number
  gearCritDamage: number
  gearCritResist: number
  gearCritDamageResist: number
  /** Mutation counts per line. */
  dmg: number
  dr: number
  crit: number
  vit: number
  pot: number
  /** Complete effective stored potency (mutations already included). */
  potencyStored: number
}

export const EMPTY_GENETICS: PetGenetics = {
  gearDamage: 0, gearDamageResist: 0, gearCrit: 0,
  gearCritDamage: 0, gearCritResist: 0, gearCritDamageResist: 0,
  dmg: 0, dr: 0, crit: 0, vit: 0, pot: 0,
  potencyStored: 0,
}

export type StatLine = 'dmg' | 'dr' | 'crit' | 'vit'
export type MutationLine = StatLine | 'pot'

export const STAT_LINE_LABELS: Record<MutationLine, string> = {
  dmg: 'Damage Rating',
  dr: 'Damage Resist Rating',
  crit: 'Crit Rating',
  vit: 'Vitality',
  pot: 'Potency',
}

export function lineStep(line: MutationLine, config: BreedingConfig): number {
  switch (line) {
    case 'dmg': return config.damageMutationStep
    case 'dr': return config.drMutationStep
    case 'crit': return config.critMutationStep
    case 'vit': return config.vitalityMutationStep
    case 'pot': return config.potencyMutationStep
  }
}

/** Sum of the four stat-line mutation counts (the number that drives decay). */
export function totalStatMutations(pet: PetGenetics): number {
  return pet.dmg + pet.dr + pet.crit + pet.vit
}

export function totalMutations(pet: PetGenetics): number {
  return totalStatMutations(pet) + pet.pot
}

/** Courtship Incense tiers as stored on a device. Both parents' bonuses are summed and clamped to +50%. */
export const INCENSE_TIERS: { label: string; bonus: number }[] = [
  { label: 'None', bonus: 0 },
  { label: 'Minor (+2.5%)', bonus: 0.025 },
  { label: 'Major (+5%)', bonus: 0.05 },
  { label: 'Grand (+10%)', bonus: 0.10 },
]

export function combinedIncenseBonus(incenseA: number, incenseB: number): number {
  return clamp01((incenseA || 0) + (incenseB || 0), 0.5)
}

function clamp01(v: number, max = 1): number {
  return Math.min(max, Math.max(0, v))
}

/**
 * Stat mutation chance for a baby whose inherited stat-mutation count is `babyStatMutations`.
 * statChance = clamp(max(floor, base / (1 + decay * count)) + incense, 0, 1)
 */
export function statMutationChance(babyStatMutations: number, config: BreedingConfig, incenseBonus = 0): number {
  const decayed = config.baseMutationChance / (1.0 + config.mutationDecayRate * babyStatMutations)
  return clamp01(Math.max(config.mutationMinFloor, decayed) + incenseBonus)
}

/** Potency mutation chance. Incense does not apply. */
export function potencyMutationChance(config: BreedingConfig): number {
  return clamp01(config.potencyMutationChance)
}

/**
 * The potency step a mutation would add on top of `potencyStored`, after the soft cap
 * (quarter step at/above it) and the smallest positive of hardCap / maxStored. 0 = capped out.
 */
export function cappedPotencyStep(potencyStored: number, config: BreedingConfig): number {
  let step = config.potencyMutationStep
  if (config.potencySoftCap > 0 && potencyStored >= config.potencySoftCap) {
    step = Math.max(1, Math.floor(config.potencyMutationStep / 4))
  }
  const cap = potencyCap(config)
  if (cap > 0) step = Math.min(step, Math.max(0, cap - potencyStored))
  return step
}

/** Smallest positive of potencyHardCap and potencyMaxStored; 0 when neither is set. */
export function potencyCap(config: BreedingConfig): number {
  const caps = [config.potencyHardCap, config.potencyMaxStored].filter(c => c > 0)
  return caps.length > 0 ? Math.min(...caps) : 0
}

export function isStatLineEligible(pet: PetGenetics, line: StatLine, config: BreedingConfig): boolean {
  return config.maxStatMutations <= 0 || pet[line] < config.maxStatMutations
}

export function eligibleStatLines(pet: PetGenetics, config: BreedingConfig): StatLine[] {
  return (['dmg', 'dr', 'crit', 'vit'] as StatLine[]).filter(l => isStatLineEligible(pet, l, config))
}

// ---------------------------------------------------------------------------
// Inheritance
// ---------------------------------------------------------------------------

export type ParentSlot = 'A' | 'B'

export type InheritLine =
  | 'damage' | 'damageResist' | 'crit'
  | 'critDamage' | 'critResist' | 'critDamageResist'
  | 'vitality' | 'potency'

export const INHERIT_LINES: InheritLine[] = [
  'damage', 'damageResist', 'crit',
  'critDamage', 'critResist', 'critDamageResist',
  'vitality', 'potency',
]

export const INHERIT_LINE_LABELS: Record<InheritLine, string> = {
  damage: 'Damage Rating',
  damageResist: 'Damage Resist',
  crit: 'Crit Rating',
  critDamage: 'Crit Damage',
  critResist: 'Crit Resist',
  critDamageResist: 'Crit Damage Resist',
  vitality: 'Vitality',
  potency: 'Potency',
}

export interface InheritanceRoll {
  line: InheritLine
  roll: number
  /** Which parent had the higher effective value (ties -> A). */
  higher: ParentSlot
  /** Which parent the baby actually took the line from. */
  chosen: ParentSlot
  effectiveA: number
  effectiveB: number
}

/** Effective value of a line as the server compares it during inheritance. */
export function effectiveLineValue(pet: PetGenetics, line: InheritLine, config: BreedingConfig): number {
  switch (line) {
    case 'damage': return pet.gearDamage + pet.dmg * config.damageMutationStep
    case 'damageResist': return pet.gearDamageResist + pet.dr * config.drMutationStep
    case 'crit': return pet.gearCrit + pet.crit * config.critMutationStep
    case 'critDamage': return pet.gearCritDamage
    case 'critResist': return pet.gearCritResist
    case 'critDamageResist': return pet.gearCritDamageResist
    case 'vitality': return pet.vit * config.vitalityMutationStep
    case 'potency': return pet.potencyStored
  }
}

function copyLine(target: PetGenetics, source: PetGenetics, line: InheritLine): void {
  switch (line) {
    case 'damage': target.gearDamage = source.gearDamage; target.dmg = source.dmg; break
    case 'damageResist': target.gearDamageResist = source.gearDamageResist; target.dr = source.dr; break
    case 'crit': target.gearCrit = source.gearCrit; target.crit = source.crit; break
    case 'critDamage': target.gearCritDamage = source.gearCritDamage; break
    case 'critResist': target.gearCritResist = source.gearCritResist; break
    case 'critDamageResist': target.gearCritDamageResist = source.gearCritDamageResist; break
    case 'vitality': target.vit = source.vit; break
    case 'potency': target.potencyStored = source.potencyStored; target.pot = source.pot; break
  }
}

/** Pick higher parent if r < 0.55 else lower; ties count parent A as higher. */
export const HIGHER_PARENT_CHANCE = 0.55

// ---------------------------------------------------------------------------
// Breeding
// ---------------------------------------------------------------------------

export interface BreedOptions {
  /** Courtship Incense bonus active on parent A's device (0, 0.025, 0.05, 0.10). */
  incenseA?: number
  incenseB?: number
  /** Chromatic Catalyst active on either device: mutation palette comes from the vibrant pool. */
  catalystA?: boolean
  catalystB?: boolean
  /** Simulator assumption: the parents kill the mating guardian (Awakened Blessing). Default true. */
  guardianKilled?: boolean
}

export interface StatRoll {
  chance: number
  roll: number
  forced: boolean
  mutated: boolean
  /** Line that received the +1, or null when no line was eligible / no mutation. */
  line: StatLine | null
  /** Rating/HP granted by the mutation (for display). */
  step: number
  allLinesCapped: boolean
}

export interface PotencyRoll {
  chance: number
  roll: number
  mutated: boolean
  /** Capped step actually applied (0 when capped out). */
  step: number
  applied: boolean
  softCapped: boolean
}

export interface GuardianOutcome {
  /** Guardian gating applied (config enabled AND the breed mutated). */
  spawned: boolean
  killed: boolean
  /** Awakened Blessing line, or null (not killed / not spawned / everything capped). */
  blessingLine: MutationLine | null
  blessingStep: number
  allLinesCapped: boolean
}

export interface BreedResult {
  baby: PetGenetics
  inheritance: InheritanceRoll[]
  /** Baby stat-mutation count right after inheritance (what the decay curve used). */
  inheritedStatMutations: number
  incenseBonus: number
  statRoll: StatRoll
  potencyRoll: PotencyRoll
  guardian: GuardianOutcome
  /** True when at least one mutation line changed (stat, potency, or blessing). */
  hasMutation: boolean
  /** The server rolls a new colour palette only for a mutated breed. */
  paletteRolled: boolean
  paletteUsesVibrantPool: boolean
  /** A catalyst is consumed only when a palette was rolled. */
  catalystConsumed: boolean
  /** Human-readable roll log in server order. */
  log: string[]
}

const fmt = (n: number) => n.toFixed(4)
const pct = (n: number) => (n * 100).toFixed(2) + '%'

export function breed(
  parentA: PetGenetics,
  parentB: PetGenetics,
  config: BreedingConfig,
  options: BreedOptions = {},
  rng: Rng = Math.random,
): BreedResult {
  const log: string[] = []
  const baby: PetGenetics = { ...EMPTY_GENETICS }

  // 1. Inheritance: independent 55/45 roll per line, higher effective value favoured.
  const inheritance: InheritanceRoll[] = []
  for (const line of INHERIT_LINES) {
    const effectiveA = effectiveLineValue(parentA, line, config)
    const effectiveB = effectiveLineValue(parentB, line, config)
    const higher: ParentSlot = effectiveA >= effectiveB ? 'A' : 'B'
    const lower: ParentSlot = higher === 'A' ? 'B' : 'A'
    const roll = rng()
    const chosen: ParentSlot = roll < HIGHER_PARENT_CHANCE ? higher : lower
    copyLine(baby, chosen === 'A' ? parentA : parentB, line)
    inheritance.push({ line, roll, higher, chosen, effectiveA, effectiveB })
    log.push(`[INHERIT ${INHERIT_LINE_LABELS[line]}] A=${effectiveA} B=${effectiveB} higher=${higher} roll ${fmt(roll)} ${roll < HIGHER_PARENT_CHANCE ? '<' : '>='} 0.55 -> took parent ${chosen}`)
  }

  const inheritedStatMutations = totalStatMutations(baby)
  const incenseBonus = combinedIncenseBonus(options.incenseA ?? 0, options.incenseB ?? 0)

  // 2. Stat mutation roll (decay is driven by the BABY's inherited counts).
  const statChance = statMutationChance(inheritedStatMutations, config, incenseBonus)
  const statRollValue = rng()
  const statMutated = config.forceMutation || statRollValue < statChance
  log.push(`[STAT ROLL] baby stat mutations after inheritance = ${inheritedStatMutations}; chance = ${pct(statChance)}` +
    (incenseBonus > 0 ? ` (incl. incense +${pct(incenseBonus)})` : '') +
    `; roll ${fmt(statRollValue)} -> ${config.forceMutation ? 'FORCED' : statMutated ? 'MUTATED' : 'no mutation'}`)

  const statRoll: StatRoll = {
    chance: statChance, roll: statRollValue, forced: config.forceMutation && !(statRollValue < statChance),
    mutated: statMutated, line: null, step: 0, allLinesCapped: false,
  }

  // 3. Stat line pick.
  if (statMutated) {
    const eligible = eligibleStatLines(baby, config)
    if (eligible.length > 0) {
      const pickRoll = rng()
      const line = eligible[Math.min(eligible.length - 1, Math.floor(pickRoll * eligible.length))]
      baby[line] += 1
      statRoll.line = line
      statRoll.step = lineStep(line, config)
      log.push(`[STAT LINE] eligible [${eligible.join(', ')}] pick roll ${fmt(pickRoll)} -> ${STAT_LINE_LABELS[line]} +1 mutation (+${statRoll.step})`)
    } else {
      statRoll.allLinesCapped = true
      log.push(`[STAT LINE] every stat line is at the per-line cap (${config.maxStatMutations}); nothing applied`)
    }
  }

  // 4. Potency roll: independent, incense does not apply.
  const potChance = potencyMutationChance(config)
  const potRollValue = rng()
  const potMutated = potRollValue < potChance
  const softCapped = config.potencySoftCap > 0 && baby.potencyStored >= config.potencySoftCap
  const potencyRoll: PotencyRoll = {
    chance: potChance, roll: potRollValue, mutated: potMutated, step: 0, applied: false, softCapped,
  }
  log.push(`[POTENCY ROLL] chance = ${pct(potChance)} (incense ignored); roll ${fmt(potRollValue)} -> ${potMutated ? 'MUTATED' : 'no mutation'}`)
  if (potMutated) {
    const step = cappedPotencyStep(baby.potencyStored, config)
    potencyRoll.step = step
    if (step > 0) {
      baby.potencyStored += step
      baby.pot += 1
      potencyRoll.applied = true
      log.push(`[POTENCY] +${step} stored potency${softCapped ? ' (soft cap: quarter step)' : ''} -> ${baby.potencyStored}, pot mutations ${baby.pot}`)
    } else {
      log.push(`[POTENCY] stored potency ${baby.potencyStored} is at the cap (${potencyCap(config)}); nothing applied`)
    }
  }

  let hasMutation = statRoll.line !== null || potencyRoll.applied

  // 5. Guardian + Awakened Blessing.
  const guardian: GuardianOutcome = {
    spawned: false, killed: false, blessingLine: null, blessingStep: 0, allLinesCapped: false,
  }
  if (config.guardianEnabled && hasMutation) {
    guardian.spawned = true
    guardian.killed = options.guardianKilled ?? true
    if (guardian.killed) {
      const eligible: MutationLine[] = eligibleStatLines(baby, config)
      const potStep = cappedPotencyStep(baby.potencyStored, config)
      if (potStep > 0) eligible.push('pot')
      if (eligible.length > 0) {
        const pickRoll = rng()
        const line = eligible[Math.min(eligible.length - 1, Math.floor(pickRoll * eligible.length))]
        if (line === 'pot') {
          baby.potencyStored += potStep
          baby.pot += 1
          guardian.blessingStep = potStep
        } else {
          baby[line] += 1
          guardian.blessingStep = lineStep(line, config)
        }
        guardian.blessingLine = line
        log.push(`[GUARDIAN] mating guardian slain; Awakened Blessing pick roll ${fmt(pickRoll)} over [${eligible.join(', ')}] -> ${STAT_LINE_LABELS[line]} +1 (+${guardian.blessingStep})`)
      } else {
        guardian.allLinesCapped = true
        log.push(`[GUARDIAN] mating guardian slain; Awakened Blessing flares but every line is capped`)
      }
    } else {
      log.push(`[GUARDIAN] mating guardian spawned and was NOT killed; no Awakened Blessing`)
    }
  } else if (config.guardianEnabled) {
    log.push(`[GUARDIAN] enabled but the breed did not mutate; no guardian`)
  }
  hasMutation = hasMutation || guardian.blessingLine !== null

  const catalyst = !!(options.catalystA || options.catalystB)
  const paletteRolled = hasMutation
  log.push(paletteRolled
    ? `[PALETTE] mutation present -> new palette rolled from the ${catalyst ? 'VIBRANT (Chromatic Catalyst)' : 'master'} pool`
    : `[PALETTE] no mutation -> no palette roll${catalyst ? ' (catalyst kept)' : ''}`)

  return {
    baby,
    inheritance,
    inheritedStatMutations,
    incenseBonus,
    statRoll,
    potencyRoll,
    guardian,
    hasMutation,
    paletteRolled,
    paletteUsesVibrantPool: paletteRolled && catalyst,
    catalystConsumed: paletteRolled && catalyst,
    log,
  }
}

// ---------------------------------------------------------------------------
// Summoned stats
// ---------------------------------------------------------------------------

export interface SummonedStats {
  damageRating: number
  damageResistRating: number
  critRating: number
  critDamageRating: number
  critResistRating: number
  critDamageResistRating: number
  /** Bonus HP from vitality mutations (added on top of the species base). */
  bonusHp: number
  potencyStored: number
}

/** Juvenile growth multipliers by stage 1..5; adults (stage >= 6 or 0) are 1.0. */
export const MATURITY_MULTIPLIERS = [0.5, 0.6, 0.7, 0.8, 0.9]

export function maturityMultiplier(stage: number): number {
  if (stage >= 1 && stage <= MATURITY_MULTIPLIERS.length) return MATURITY_MULTIPLIERS[stage - 1]
  return 1.0
}

/**
 * Ratings the pet shows when summoned. `stage` 1..5 applies the juvenile multiplier;
 * anything else is adult (x1.0).
 */
export function summonedStats(pet: PetGenetics, config: BreedingConfig, stage = 0): SummonedStats {
  const m = maturityMultiplier(stage)
  const dmgBonus = pet.dmg * config.damageMutationStep
  const drBonus = pet.dr * config.drMutationStep
  const scale = (v: number) => Math.round(v * m)
  return {
    damageRating: scale(pet.gearDamage + dmgBonus),
    damageResistRating: scale(pet.gearDamageResist + drBonus),
    critRating: scale(pet.gearCrit + pet.crit * config.critMutationStep),
    critDamageRating: scale(pet.gearCritDamage + Math.round(0.8 * dmgBonus)),
    critResistRating: scale(pet.gearCritResist + Math.round(0.8 * drBonus)),
    critDamageResistRating: scale(pet.gearCritDamageResist + Math.round(0.6 * drBonus)),
    bonusHp: scale(pet.vit * config.vitalityMutationStep),
    potencyStored: pet.potencyStored,
  }
}

// ---------------------------------------------------------------------------
// Multi-breed projections
// ---------------------------------------------------------------------------

/** Breeding cadence facts used for throughput projections. */
export const BREEDING_CADENCE = {
  maleChargesPerRefill: 10,
  maleRefillHours: 24,
  femaleRecoveryHours: 4,
}

/** Breeds per day a single female can physically supply (24h / 4h recovery). */
export const FEMALE_BREEDS_PER_DAY = Math.floor(24 / BREEDING_CADENCE.femaleRecoveryHours)

export interface CampaignOptions extends BreedOptions {
  /** Stop after this many breeds (safety bound). */
  maxBreeds: number
  /** Stop once the kept pet's stat-line mutation count reaches this (0 = never). */
  targetStatMutations?: number
}

export interface CampaignResult {
  breeds: number
  /** Best pet kept at the end. */
  best: PetGenetics
  reachedTarget: boolean
  mutatedBreeds: number
  potencyMutations: number
  blessings: number
}

/**
 * Greedy "keep the best baby as parent A" campaign: parent B stays fixed (a donor of the
 * given genetics), every baby whose total mutation count beats the current parent A replaces it.
 */
export function runCampaign(
  parentA: PetGenetics,
  parentB: PetGenetics,
  config: BreedingConfig,
  options: CampaignOptions,
  rng: Rng = Math.random,
): CampaignResult {
  let best = { ...parentA }
  const target = options.targetStatMutations ?? 0
  let breeds = 0
  let mutatedBreeds = 0
  let potencyMutations = 0
  let blessings = 0

  while (breeds < options.maxBreeds && !(target > 0 && totalStatMutations(best) >= target)) {
    const result = breed(best, parentB, config, options, rng)
    breeds++
    if (result.hasMutation) mutatedBreeds++
    if (result.potencyRoll.applied) potencyMutations++
    if (result.guardian.blessingLine) blessings++
    if (totalMutations(result.baby) > totalMutations(best)) best = result.baby
  }

  return {
    breeds, best, mutatedBreeds, potencyMutations, blessings,
    reachedTarget: target > 0 && totalStatMutations(best) >= target,
  }
}

// ---------------------------------------------------------------------------
// Deterministic RNG + self-check
// ---------------------------------------------------------------------------

/** Small seeded PRNG (mulberry32) so a breed can be replayed exactly. */
export function seededRng(seed: number): Rng {
  let a = seed >>> 0
  return () => {
    a = (a + 0x6D2B79F5) >>> 0
    let t = a
    t = Math.imul(t ^ (t >>> 15), t | 1)
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61)
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

/** Rng that replays a fixed list of draws (throws if it runs dry). */
export function scriptedRng(values: number[]): Rng {
  let i = 0
  return () => {
    if (i >= values.length) throw new Error(`scriptedRng ran out of values after ${values.length} draws`)
    return values[i++]
  }
}

export interface SelfCheckResult {
  ok: boolean
  lines: string[]
}

/**
 * Replays fixed rolls through `breed()` and compares to hand-computed expectations.
 * Run from the debug console button in the simulator, or from a REPL.
 */
export function runSelfCheck(): SelfCheckResult {
  const lines: string[] = []
  let ok = true
  const check = (name: string, actual: unknown, expected: unknown) => {
    const pass = JSON.stringify(actual) === JSON.stringify(expected)
    if (!pass) ok = false
    lines.push(`${pass ? 'PASS' : 'FAIL'} ${name}: expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`)
  }

  const config: BreedingConfig = { ...DEFAULT_BREEDING_CONFIG, mutationDecayRate: 0.5, potencySoftCap: 100, potencyHardCap: 120 }
  const a: PetGenetics = { ...EMPTY_GENETICS, gearDamage: 12, gearCrit: 3, gearCritDamage: 4, dmg: 2, vit: 1, potencyStored: 95 }
  const b: PetGenetics = { ...EMPTY_GENETICS, gearDamage: 20, gearDamageResist: 6, gearCrit: 3, gearCritResist: 7, dr: 1, potencyStored: 110, pot: 3 }

  // Effective comparisons: damage A=32 vs B=20 (A higher), DR A=0 vs B=16 (B), crit 3 vs 3 (tie -> A),
  // critDamage 4 vs 0 (A), critResist 0 vs 7 (B), critDmgResist 0 vs 0 (tie -> A), vitality 50 vs 0 (A), potency 95 vs 110 (B).
  const draws = [
    0.10, // damage: <0.55 -> higher (A): gearDamage 12, dmg 2
    0.90, // damageResist: >=0.55 -> lower (A): 0 / 0
    0.20, // crit: tie -> A: gearCrit 3, crit 0
    0.60, // critDamage: lower (B): 0
    0.30, // critResist: higher (B): 7
    0.54, // critDamageResist: tie -> A: 0
    0.70, // vitality: lower (B): vit 0
    0.01, // potency: higher (B): stored 110, pot 3
    // baby stat mutations after inheritance = dmg 2 + dr 0 + crit 0 + vit 0 = 2
    // chance = max(0.02, 0.05 / (1 + 0.5*2)) + incense 0.05 = 0.025 + 0.05 = 0.075
    0.070, // stat roll < 0.075 -> mutated
    0.99,  // line pick over [dmg, dr, crit, vit] -> index 3 -> vit
    0.02,  // potency roll < 0.03 -> mutated; stored 110 >= soft cap 100 -> step 6; cap 120 -> min(6, 10) = 6
    // guardian disabled in this config -> no blessing draw
  ]
  const r1 = breed(a, b, config, { incenseA: 0.025, incenseB: 0.025, catalystB: true }, scriptedRng(draws))
  check('inheritedStatMutations', r1.inheritedStatMutations, 2)
  check('statRoll.chance', Number(r1.statRoll.chance.toFixed(6)), 0.075)
  check('statRoll.line', r1.statRoll.line, 'vit')
  check('potencyRoll.step', r1.potencyRoll.step, 6)
  check('baby', r1.baby, {
    gearDamage: 12, gearDamageResist: 0, gearCrit: 3, gearCritDamage: 0, gearCritResist: 7, gearCritDamageResist: 0,
    dmg: 2, dr: 0, crit: 0, vit: 1, pot: 4, potencyStored: 116,
  })
  check('hasMutation / palette', [r1.hasMutation, r1.paletteRolled, r1.paletteUsesVibrantPool, r1.catalystConsumed], [true, true, true, true])
  check('guardian not spawned when disabled', r1.guardian.spawned, false)
  check('summoned adult', summonedStats(r1.baby, config), {
    damageRating: 32, damageResistRating: 0, critRating: 3, critDamageRating: 16, critResistRating: 7, critDamageResistRating: 0,
    bonusHp: 50, potencyStored: 116,
  })
  check('summoned stage 1', summonedStats(r1.baby, config, 1).damageRating, 16)

  // Guardian enabled + killed: blessing over [dmg, dr, crit, vit, pot]; pot step = min(6, 120-116) = 4.
  const guardianConfig: BreedingConfig = { ...config, guardianEnabled: true, forceMutation: true }
  const draws2 = [
    0.10, 0.90, 0.20, 0.60, 0.30, 0.54, 0.70, 0.01,
    0.50,  // stat roll (forced anyway)
    0.0,   // line pick -> dmg
    0.02,  // potency roll -> mutated, +6 -> 116
    0.999, // blessing pick over 5 entries -> index 4 -> pot, +4 -> 120
  ]
  const r2 = breed(a, b, guardianConfig, { guardianKilled: true }, scriptedRng(draws2))
  check('guardian blessing line', [r2.guardian.spawned, r2.guardian.killed, r2.guardian.blessingLine, r2.guardian.blessingStep], [true, true, 'pot', 4])
  check('guardian baby counts', [r2.baby.dmg, r2.baby.pot, r2.baby.potencyStored], [3, 5, 120])

  // Hard-capped potency: no step -> no potency mutation, no palette.
  const cappedA: PetGenetics = { ...EMPTY_GENETICS, potencyStored: 120 }
  const r3 = breed(cappedA, cappedA, { ...config, maxStatMutations: 0 }, {}, scriptedRng([0, 0, 0, 0, 0, 0, 0, 0, 0.5, 0, 0.0]))
  check('capped potency mutation not applied', [r3.potencyRoll.mutated, r3.potencyRoll.applied, r3.hasMutation, r3.paletteRolled], [true, false, false, false])

  // Incense clamp and chance floor.
  check('incense clamp', combinedIncenseBonus(0.4, 0.3), 0.5)
  check('chance floor', statMutationChance(100, { ...config, mutationDecayRate: 1 }), 0.02)
  check('chance clamp to 1', statMutationChance(0, { ...config, baseMutationChance: 0.8 }, 0.5), 1)
  check('potency cap picks smallest positive', potencyCap({ ...config, potencyHardCap: 500, potencyMaxStored: 300 }), 300)
  check('potency cap none', potencyCap({ ...config, potencyHardCap: 0, potencyMaxStored: 0 }), 0)

  lines.unshift(ok ? 'Breeding model self-check: ALL PASS' : 'Breeding model self-check: FAILURES')
  return { ok, lines }
}
