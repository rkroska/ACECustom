import { useEffect, useState } from 'react'

/**
 * Shapes of /api/pet-guide (PetGuideController). Every number the guide shows comes from here:
 * the page itself carries no tuning values, so it always matches the live server.
 */

export interface GuideItem {
  wcid: number
  name: string | null
  icon: string | null
  value: number | null
}

export interface NpcSpot {
  /** Map coordinates, or null inside a dungeon (the page names the place instead). */
  coords: string | null
  indoors: boolean
}

export interface GuideNpc {
  wcid: number
  name: string | null
  spots: NpcSpot[]
}

export interface ShopItem {
  wcid: number
  name: string | null
  icon: string | null
  description: string | null
  price: number
}

export interface Shop {
  /** The item this vendor charges in (MMD notes, Savage Echo...), or null for pyreals. */
  currency: GuideItem | null
  stock: ShopItem[]
}

export interface PetGuideData {
  features: {
    siphonLensDrops: boolean
    bond: boolean
    potency: boolean
    savageEchoDrops: boolean
    essenceSalvage: boolean
    bredEssenceSalvage: boolean
    bondStrain: boolean
    breeding: boolean
    breedingSpirit: boolean
    maturity: boolean
    refillCharm: boolean
    universalCharm: boolean
    captureDamageType: boolean
  }
  capture: {
    rangeMeters: number
    maxHealthFraction: number
    maxHealthPoints: number
    minChance: number
    lenses: { key: string; baseChance: number; maxChance: number }[]
    assessSkillBonusMax: number
    assessSkillForMaxBonus: number
    assessSpecializedBonus: number
    lowHealthBonusMax: number
    levelPenaltyMax: number
    levelsForMaxPenalty: number
    resonance: { bonus: number; maxChance: number }
    enrage: { damageMultiplier: number; damageReduction: number }
    lensDrops: {
      flawedChance: number
      pristineChance: number
      perfectChance: number
      pristineMinCreatureLevel: number
      perfectMinCreatureLevel: number
      rampLevels: number
      levelBonusDivisor: number
    }
  }
  registry: { milestones: number[]; milestoneInterval: number }
  charms: { refillCostPerCharge: number; refillDiscountByTier: number[] }
  bond: { levelCap: number }
  potency: {
    damagePerLevel: number
    bondDivisor: number
    activeCap: number
    maxStored: number
    echoDropRequiresBond: boolean
    echoPerDrop: { standard: number; tier9: number; tier10: number; shinyMultiplier: number }
    salvage: { captured: number; shinyMultiplier: number; bred: number }
    strain: { threshold: number; perLevel: number; max: number }
  }
  breeding: {
    minTier: number
    minBond: number
    maleCharges: number
    maleChargeResetHours: number
    femaleRestHours: number
    danceWindowSeconds: number
    shinyCanBreed: boolean
    betterParentChance: number
    mutationChance: number
    potencyMutationChance: number
    mutationSteps: { damageRating: number; damageResistRating: number; critRating: number; vitality: number; potency: number }
    spiritSeconds: number
    maturity: {
      killsRequired: number
      minDamageShare: number
      imprintOnSummon: boolean
      stages: { name: string; strength: number; size: number }[]
    }
  }
  world: {
    items: Record<string, GuideItem>
    npcs: Record<string, GuideNpc[]>
    starterLensCount: number | null
    asheronsLensFlawedCost: number | null
    resonanceLensCooldownSeconds: number | null
    shimmeringEchoCooldownSeconds: number | null
    mastery: {
      firstChangeMinLevel: number | null
      paidChangeMinLevel: number | null
      paidChangeCooldownSeconds: number | null
      paidChanges: { mmd: number; luminance: number }[]
    } | null
    essences: {
      tiers: number[]
      tierRequirements: { tier: number; level: number | null; summoningSkill: number | null; summoningSpecialized: boolean; summonAugs: number | null }[]
      masteries: { mastery: string; families: GuideItem[] }[]
    }
    shops: { schneebs: Shop | null; banderling: Shop | null; ivo: Shop | null }
  }
}

export interface PotencyPlan {
  bond: number
  stored: number
  target: number
  active: number
  dormant: number
  activeLimitFromBond: number
  damageBonus: number
  nextLevelCost: number
  costToTarget: number
  strain: number
}

export function usePetGuide() {
  const [data, setData] = useState<PetGuideData | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    fetch('/api/pet-guide')
      .then(res => {
        if (!res.ok) throw new Error(`The server answered ${res.status}.`)
        return res.json()
      })
      .then(json => {
        if (!isPetGuideData(json)) throw new Error('The server sent incomplete pet guide data. It may need a restart after an update.')
        if (!cancelled) setData(json)
      })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : String(err)) })
    return () => { cancelled = true }
  }, [])

  return { data, error }
}

const isObject = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null

/** Checks the parts of /api/pet-guide the pages index into, so a partial payload fails loudly instead of crashing a page. */
function isPetGuideData(v: unknown): v is PetGuideData {
  if (!isObject(v)) return false
  const { features, capture, registry, charms, bond, potency, breeding, world } = v
  if (![features, capture, registry, charms, bond, potency, breeding, world].every(isObject)) return false
  const c = capture as Record<string, unknown>
  const b = breeding as Record<string, unknown>
  const w = world as Record<string, unknown>
  const essences = w.essences as Record<string, unknown> | undefined
  const shops = w.shops
  return Array.isArray(c.lenses)
    && isObject(c.resonance) && isObject(c.enrage) && isObject(c.lensDrops)
    && Array.isArray((registry as Record<string, unknown>).milestones)
    && Array.isArray((charms as Record<string, unknown>).refillDiscountByTier)
    && isObject(b.mutationSteps) && isObject(b.maturity) && Array.isArray((b.maturity as Record<string, unknown>).stages)
    && isObject(w.items) && isObject(w.npcs) && isObject(shops)
    && isObject(essences) && Array.isArray(essences.tiers) && Array.isArray(essences.tierRequirements) && Array.isArray(essences.masteries)
    && (essences.tierRequirements as unknown[]).every(t => isObject(t) && typeof t.summoningSpecialized === 'boolean')
    && Object.values(shops as Record<string, unknown>).every(s => s === null || (isObject(s) && Array.isArray(s.stock) && (s.currency === null || isObject(s.currency))))
}

export async function fetchPotencyPlan(bond: number, stored: number, target: number): Promise<PotencyPlan> {
  const res = await fetch(`/api/pet-guide/potency?bond=${bond}&stored=${stored}&target=${target}`)
  if (!res.ok) throw new Error(`The server answered ${res.status}.`)
  return res.json()
}
