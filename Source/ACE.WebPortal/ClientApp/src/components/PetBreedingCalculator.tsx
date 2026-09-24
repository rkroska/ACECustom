import { useState, useRef, useEffect, useMemo } from 'react'
import { Heart, Dna, Dices, Info, Sparkles, RefreshCw, ChevronRight, Award, Copy, BarChart3, HelpCircle, RotateCcw, Terminal, Check } from 'lucide-react'
import WorldViewer from './WorldViewer'
import { copyToClipboard } from '../utils/clipboard'
import {
  breed,
  runCampaign,
  runSelfCheck,
  seededRng,
  summonedStats,
  statMutationChance,
  potencyMutationChance,
  potencyCap,
  combinedIncenseBonus,
  normalizeBreedingConfig,
  totalStatMutations,
  totalMutations,
  normalizeBreedingCadence,
  femaleBreedsPerDay,
  maturityMultiplier,
  DEFAULT_BREEDING_CONFIG,
  DEFAULT_BREEDING_CADENCE,
  EMPTY_GENETICS,
  STAT_LINE_LABELS,
} from '../utils/breedingModel'
import type { BreedingCadence, BreedingConfig, BreedOptions, BreedResult, PetGenetics, StatLine, SummonedStats } from '../utils/breedingModel'

interface PetPalette {

  name: string
  hex: string
  templateId: number
  paletteId: number
  hueShift: number
  swatches: string[]
}

const COLOR_FAMILIES: { [key: string]: { name: string, hex: string, swatches: string[], templates: { [species: string]: { templateId: number, paletteId: number, hueShift?: number } } } } = {
  azure: {
    name: 'Azure Slate',
    hex: '#3B82F6',
    swatches: ['#3B82F6', '#2563EB', '#1D4ED8', '#1E40AF', '#1E3A8A', '#172554', '#0F172A', '#020617'],
    templates: {
      'Olthoi': { templateId: 39, paletteId: 0x04001165, hueShift: 0 },
      'Gromnie': { templateId: 11, paletteId: 0x04001DB4, hueShift: 0 },
      'Drudge': { templateId: 21, paletteId: 0x0400102F, hueShift: 0 },
      'Mattekar': { templateId: 1, paletteId: 0x040001A9, hueShift: 0 },
      'Lugian': { templateId: 2, paletteId: 0x040010CA, hueShift: 0 },
      'Banderling': { templateId: 14, paletteId: 0x0400142E, hueShift: 0 },
      'Burun': { templateId: 2, paletteId: 0x040017AF, hueShift: 0 },
      'Tusker': { templateId: 2, paletteId: 0x040001F6, hueShift: 0 },
      'Viridian Statue': { templateId: 27, paletteId: 0x04001356, hueShift: 0 },
    }
  },
  amber: {
    name: 'Royal Amber',
    hex: '#F59E0B',
    swatches: ['#F59E0B', '#D97706', '#B45309', '#78350F', '#451A03', '#27272A', '#18181B', '#09090B'],
    templates: {
      'Olthoi': { templateId: 10, paletteId: 0x04001342, hueShift: 0 },
      'Gromnie': { templateId: 15, paletteId: 0x0400135E, hueShift: 0 },
      'Drudge': { templateId: 40, paletteId: 0x0400197B, hueShift: 0 },
      'Mattekar': { templateId: 2, paletteId: 0x040001A8, hueShift: 0 },
      'Lugian': { templateId: 10, paletteId: 0x0400118C, hueShift: 0 },
      'Banderling': { templateId: 18, paletteId: 0x04001437, hueShift: 0 },
      'Burun': { templateId: 58, paletteId: 0x040017A7, hueShift: 0 },
      'Tusker': { templateId: 3, paletteId: 0x040001F4, hueShift: 0 },
      'Viridian Statue': { templateId: 25, paletteId: 0x0400135F, hueShift: 0 },
    }
  },
  crimson: {
    name: 'Ebon Crimson',
    hex: '#EF4444',
    swatches: ['#EF4444', '#DC2626', '#B91C1C', '#991B1B', '#7F1D1D', '#450A0A', '#260404', '#000000'],
    templates: {
      'Olthoi': { templateId: 13, paletteId: 0x04001162, hueShift: 0 },
      'Gromnie': { templateId: 12, paletteId: 0x04001DB7, hueShift: 0 },
      'Drudge': { templateId: 13, paletteId: 0x040019F9, hueShift: 0 },
      'Mattekar': { templateId: 3, paletteId: 0x040001AA, hueShift: 0 },
      'Lugian': { templateId: 4, paletteId: 0x040010D2, hueShift: 0 },
      'Banderling': { templateId: 81, paletteId: 0x04001432, hueShift: 0 },
      'Burun': { templateId: 62, paletteId: 0x040017A8, hueShift: 0 },
      'Tusker': { templateId: 4, paletteId: 0x040001F8, hueShift: 0 },
      'Viridian Statue': { templateId: 39, paletteId: 0x04001E9B, hueShift: 0 },
    }
  },
  emerald: {
    name: 'Emerald Olive',
    hex: '#10B981',
    swatches: ['#10B981', '#059669', '#047857', '#065F46', '#064E3B', '#022C22', '#061D15', '#000000'],
    templates: {
      'Olthoi': { templateId: 8, paletteId: 0x04001163, hueShift: 0 },
      'Gromnie': { templateId: 14, paletteId: 0x04001DB3, hueShift: 0 },
      'Drudge': { templateId: 89, paletteId: 0x04001E11, hueShift: 0 },
      'Mattekar': { templateId: 4, paletteId: 0x040001AB, hueShift: 0 },
      'Lugian': { templateId: 9, paletteId: 0x040010C7, hueShift: 0 },
      'Banderling': { templateId: 64, paletteId: 0x04001434, hueShift: 0 },
      'Burun': { templateId: 8, paletteId: 0x040017AE, hueShift: 0 },
      'Tusker': { templateId: 1, paletteId: 0x040001F5, hueShift: 0 },
      'Viridian Statue': { templateId: 5, paletteId: 0x04000FEA, hueShift: 0 },
    }
  },
  gold: {
    name: 'Sunburst Gold',
    hex: '#EAB308',
    swatches: ['#EAB308', '#CA8A04', '#A16207', '#854D0E', '#713F12', '#451A03', '#1C1917', '#000000'],
    templates: {
      'Olthoi': { templateId: 20, paletteId: 0x04001343, hueShift: 0 },
      'Gromnie': { templateId: 10, paletteId: 0x04001DAC, hueShift: 0 },
      'Drudge': { templateId: 17, paletteId: 0x040019FC, hueShift: 0 },
      'Mattekar': { templateId: 5, paletteId: 0x040001AC, hueShift: 0 },
      'Lugian': { templateId: 8, paletteId: 0x040010D0, hueShift: 0 },
      'Banderling': { templateId: 45, paletteId: 0x04001439, hueShift: 0 },
      'Burun': { templateId: 19, paletteId: 0x040017B0, hueShift: 0 },
      'Tusker': { templateId: 5, paletteId: 0x040001F9, hueShift: 0 },
      'Viridian Statue': { templateId: 8, paletteId: 0x04000FEB, hueShift: 0 },
    }
  },
  cobalt: {
    name: 'Cobalt Indigo',
    hex: '#6366F1',
    swatches: ['#6366F1', '#4F46E5', '#4338CA', '#3730A3', '#312E81', '#1E1B4B', '#0F172A', '#000000'],
    templates: {
      'Olthoi': { templateId: 82, paletteId: 0x04001164, hueShift: 0 },
      'Gromnie': { templateId: 6, paletteId: 0x040018BB, hueShift: 0 },
      'Drudge': { templateId: 76, paletteId: 0x040019FB, hueShift: 0 },
      'Mattekar': { templateId: 6, paletteId: 0x040001AD, hueShift: 0 },
      'Lugian': { templateId: 13, paletteId: 0x040010CD, hueShift: 0 },
      'Banderling': { templateId: 16, paletteId: 0x04001433, hueShift: 0 },
      'Burun': { templateId: 13, paletteId: 0x040017AD, hueShift: 0 },
      'Tusker': { templateId: 6, paletteId: 0x040001FA, hueShift: 0 },
      'Viridian Statue': { templateId: 76, paletteId: 0x0400130F, hueShift: 0 },
    }
  },
  steel: {
    name: 'Steel Slate',
    hex: '#64748B',
    swatches: ['#64748B', '#475569', '#334155', '#1E293B', '#0F172A', '#020617', '#000000', '#000000'],
    templates: {
      'Olthoi': { templateId: 8, paletteId: 0x04001163, hueShift: 0 },
      'Gromnie': { templateId: 4, paletteId: 0x04001DAE, hueShift: 0 },
      'Drudge': { templateId: 12, paletteId: 0x040019FA, hueShift: 0 },
      'Mattekar': { templateId: 2, paletteId: 0x040001A8, hueShift: 0 },
      'Lugian': { templateId: 20, paletteId: 0x040010CB, hueShift: 0 },
      'Banderling': { templateId: 25, paletteId: 0x04001438, hueShift: 0 },
      'Burun': { templateId: 52, paletteId: 0x040017AC, hueShift: 0 },
      'Tusker': { templateId: 2, paletteId: 0x040001F6, hueShift: 0 },
      'Viridian Statue': { templateId: 14, paletteId: 0x04001310, hueShift: 0 },
    }
  }
}

function resolvePaletteForSpecies(
  parentPal: PetPalette, 
  targetSpecies: string, 
  inheritedParentName: string,
  availableVariants: any[] = []
): PetPalette {
  let familyKey = 'azure'
  const palName = parentPal.name.toLowerCase()
  if (palName.includes('amber') || parentPal.hex === '#F59E0B') {
    familyKey = 'amber'
  } else if (palName.includes('crimson') || parentPal.hex === '#EF4444') {
    familyKey = 'crimson'
  } else if (palName.includes('emerald') || parentPal.hex === '#10B981') {
    familyKey = 'emerald'
  } else if (palName.includes('gold') || parentPal.hex === '#EAB308') {
    familyKey = 'gold'
  } else if (palName.includes('cobalt') || parentPal.hex === '#6366F1') {
    familyKey = 'cobalt'
  } else if (palName.includes('steel') || parentPal.hex === '#64748B') {
    familyKey = 'steel'
  }

  const fam = COLOR_FAMILIES[familyKey] || COLOR_FAMILIES['azure']
  let specTemplateId = 1
  let specPaletteId = 0
  let swatches = fam.swatches

  if (fam.templates[targetSpecies]) {
    specTemplateId = fam.templates[targetSpecies].templateId
    specPaletteId = fam.templates[targetSpecies].paletteId
  } else if (parentPal.templateId) {
    specTemplateId = parentPal.templateId
  }

  // If availableVariants are provided from DATs, resolve the exact 0x04... paletteId!
  if (availableVariants && availableVariants.length > 0) {
    const matched = availableVariants.find((v: any) => v.templateId === specTemplateId) 
      || availableVariants.find((v: any) => (v.paletteId & 0xFF000000) === 0x04000000)
      || availableVariants[0]

    if (matched) {
      specTemplateId = matched.templateId
      if (matched.paletteId && (matched.paletteId & 0xFF000000) === 0x04000000) {
        specPaletteId = matched.paletteId
      }
      if (matched.swatches && matched.swatches.length > 0) {
        swatches = matched.swatches
      }
    }
  }

  return {
    name: `${inheritedParentName}'s ${fam.name}`,
    hex: fam.hex,
    templateId: specTemplateId,
    paletteId: specPaletteId,
    hueShift: 0,
    swatches: parentPal.swatches && parentPal.swatches.length > 0 ? parentPal.swatches : swatches
  }
}

/** A lineage palette drawn from one of the colour families, resolved for a species when it has an entry. */
function randomPaletteForSpecies(species: string, label: string): PetPalette {
  const familyKey = randomPick(Object.keys(COLOR_FAMILIES))
  const fam = COLOR_FAMILIES[familyKey]
  const speciesTemplate = fam.templates[species]
  return {
    name: `${label} (${fam.name})`,
    hex: fam.hex,
    templateId: speciesTemplate ? speciesTemplate.templateId : 1,
    paletteId: speciesTemplate ? speciesTemplate.paletteId : 0,
    hueShift: 0,
    swatches: fam.swatches,
  }
}

interface GeneticsFieldProps {
  label: string
  value: number
  onChange: (v: number) => void
  color: string
  title?: string
}

function GeneticsField({ label, value, onChange, color, title }: GeneticsFieldProps) {
  return (
    <div title={title}>
      <label className="text-[10px] text-neutral-500 font-semibold block mb-0.5 truncate">{label}</label>
      <input
        type="number" min="0" value={value}
        onChange={(e) => onChange(Math.max(0, Math.trunc(Number(e.target.value) || 0)))}
        className={`w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2 py-1 text-xs font-bold ${color}`}
      />
    </div>
  )
}

interface GeneticsEditorProps {
  genetics: PetGenetics
  onChange: (g: PetGenetics) => void
  config: BreedingConfig
}

/** Gear base ratings + mutation counts + stored potency: the exact state the server keeps on a pet device. */
function GeneticsEditor({ genetics, onChange, config }: GeneticsEditorProps) {
  const set = (key: keyof PetGenetics) => (v: number) => onChange({ ...genetics, [key]: v })
  const adult = summonedStats(genetics, config)
  return (
    <div className="space-y-3">
      <div>
        <div className="text-[10px] font-black uppercase tracking-wider text-neutral-500 mb-1.5">Gear Base Ratings (loot rolled)</div>
        <div className="grid grid-cols-3 gap-2">
          <GeneticsField label="Damage" value={genetics.gearDamage} onChange={set('gearDamage')} color="text-rose-400" />
          <GeneticsField label="Dmg Resist" value={genetics.gearDamageResist} onChange={set('gearDamageResist')} color="text-emerald-400" />
          <GeneticsField label="Crit" value={genetics.gearCrit} onChange={set('gearCrit')} color="text-violet-400" />
          <GeneticsField label="Crit Damage" value={genetics.gearCritDamage} onChange={set('gearCritDamage')} color="text-rose-300" />
          <GeneticsField label="Crit Resist" value={genetics.gearCritResist} onChange={set('gearCritResist')} color="text-emerald-300" />
          <GeneticsField label="Crit Dmg Resist" value={genetics.gearCritDamageResist} onChange={set('gearCritDamageResist')} color="text-emerald-200" />
        </div>
      </div>

      <div>
        <div className="text-[10px] font-black uppercase tracking-wider text-neutral-500 mb-1.5">Mutation Counts</div>
        <div className="grid grid-cols-3 gap-2">
          <GeneticsField label={`Damage (x${config.damageMutationStep})`} value={genetics.dmg} onChange={set('dmg')} color="text-rose-400" />
          <GeneticsField label={`Dmg Resist (x${config.drMutationStep})`} value={genetics.dr} onChange={set('dr')} color="text-emerald-400" />
          <GeneticsField label={`Crit (x${config.critMutationStep})`} value={genetics.crit} onChange={set('crit')} color="text-violet-400" />
          <GeneticsField label={`Vitality (x${config.vitalityMutationStep} HP)`} value={genetics.vit} onChange={set('vit')} color="text-cyan-400" />
          <GeneticsField label="Potency (count)" value={genetics.pot} onChange={set('pot')} color="text-amber-400" title="Number of potency mutations already on this line. Display only: potency stored already includes them." />
          <GeneticsField label="Potency Stored" value={genetics.potencyStored} onChange={set('potencyStored')} color="text-amber-300" title="Complete effective stored potency (mutations already included). This is what inheritance compares." />
        </div>
      </div>

      <div className="bg-neutral-950/80 border border-neutral-800 rounded-xl px-2.5 py-2 text-[10px] text-neutral-400 leading-relaxed">
        <span className="font-bold text-neutral-300">Summoned (adult):</span>{' '}
        DR <span className="text-rose-400 font-bold">{adult.damageRating}</span> / DRR <span className="text-emerald-400 font-bold">{adult.damageResistRating}</span> / Crit <span className="text-violet-400 font-bold">{adult.critRating}</span> / CD <span className="text-rose-300 font-bold">{adult.critDamageRating}</span> / CR <span className="text-emerald-300 font-bold">{adult.critResistRating}</span> / CDR <span className="text-emerald-200 font-bold">{adult.critDamageResistRating}</span> / HP <span className="text-cyan-400 font-bold">+{adult.bonusHp}</span>
        {' '}| stat muts <span className="text-white font-bold">{totalStatMutations(genetics)}</span>, potency muts <span className="text-amber-400 font-bold">{genetics.pot}</span>
      </div>
    </div>
  )
}

interface SimulationResult {
  id: string
  generation: number
  parentAlphaSpecies: string
  parentBetaSpecies: string
  /** Parent whose species (and, in this simulator, palette) the baby took: a server 50/50 coin flip. */
  donorParent: 'Alpha' | 'Beta'
  species: string
  level: number
  genetics: PetGenetics
  adult: SummonedStats
  newborn: SummonedStats
  hasMutation: boolean
  mutationSummary: string[]
  guardianNote: string | null
  isColorMutated: boolean
  cooldownHours: number
  palette: PetPalette
  paletteName: string
  paletteHex: string
  paletteTemplateId: number
  paletteId: number
  paletteSwatches?: string[]
  hueShift: number
  timestamp: string
}

type ActivityProfile = 'casual' | 'dedicated' | 'hardcore'

/** Stud sessions per day: each male stud supplies 10 charges per 24h refill. */
const PROFILE_STUDS: Record<ActivityProfile, number> = { casual: 1, dedicated: 2, hardcore: 4 }

/** Verbose mode writes ~40 lines per breed, so keep enough history for a dozen of them. */
const DEBUG_LOG_LIMIT = 2000

const PROJECTION_TRIALS = 100
const PROJECTION_MAX_BREEDS = 4000
const PROJECTION_DAYS = 90

/** Randomizer presets: how far along a lineage the generated parents are. */
type RandomProfile = 'fresh' | 'bred' | 'veteran'

const RANDOM_PROFILE_OPTIONS: { key: RandomProfile, label: string, hint: string }[] = [
  { key: 'fresh', label: 'Fresh loot', hint: 'Straight off a drop: each gear rating has a 50% chance to roll (1-20), no mutations, no potency.' },
  { key: 'bred', label: 'Bred line', hint: 'A few generations in: 0-4 mutations per stat line, 0-2 potency mutations.' },
  { key: 'veteran', label: 'Veteran line', hint: 'Long lineage: 3-15 mutations per stat line, deep stored potency.' },
]

/** Inclusive on both ends, like the server's ThreadSafeRandom.Next(int, int). */
function randInt(min: number, max: number): number {
  return min + Math.floor(Math.random() * (max - min + 1))
}

function randomPick<T>(list: T[]): T {
  return list[Math.floor(Math.random() * list.length)]
}

/**
 * One gear base rating the way loot rolls it (LootGenerationFactory_PetDevice): 50% chance the line
 * rolls at all, then 1-10 plus a likely second 1-10 at high tier.
 */
function rollGearRating(): number {
  if (Math.random() < 0.5) return 0
  let rating = randInt(1, 10)
  if (Math.random() < 0.62) rating += randInt(1, 10)
  return rating
}

/** A plausible pet device for testing: gear ratings, mutation counts and stored potency that agree. */
function randomGenetics(profile: RandomProfile, config: BreedingConfig): PetGenetics {
  const gear = {
    gearDamage: rollGearRating(),
    gearDamageResist: rollGearRating(),
    gearCrit: rollGearRating(),
    gearCritDamage: rollGearRating(),
    gearCritResist: rollGearRating(),
    gearCritDamageResist: rollGearRating(),
  }

  if (profile === 'fresh')
    return { ...EMPTY_GENETICS, ...gear }

  const statRange: [number, number] = profile === 'veteran' ? [3, 15] : [0, 4]
  const potRange: [number, number] = profile === 'veteran' ? [2, 8] : [0, 2]
  const cap = config.maxStatMutations > 0 ? config.maxStatMutations : Number.MAX_SAFE_INTEGER
  const statMut = () => Math.min(cap, randInt(statRange[0], statRange[1]))

  const pot = randInt(potRange[0], potRange[1])
  // Stored potency already contains its mutations, the way the server keeps it.
  const potencyBase = profile === 'veteran' ? randInt(200, 1200) : randInt(0, 200)
  const potencyStored = pot * config.potencyMutationStep + potencyBase
  const hardCap = potencyCap(config)

  return {
    ...gear,
    dmg: statMut(),
    dr: statMut(),
    crit: statMut(),
    vit: statMut(),
    pot,
    potencyStored: hardCap > 0 ? Math.min(hardCap, potencyStored) : potencyStored,
  }
}

function percentile(sorted: number[], p: number): number {
  if (sorted.length === 0) return 0
  const idx = Math.min(sorted.length - 1, Math.max(0, Math.round((sorted.length - 1) * p)))
  return sorted[idx]
}

export default function PetBreedingCalculator() {
  // Navigation tab state: 'simulator' | 'calculator' | 'guide'
  const [activeTab, setActiveTab] = useState<'simulator' | 'calculator' | 'guide'>('simulator')

  // Database-Validated Creature Species List (Alphabetically Sorted)
  const speciesList = [
    { name: 'Acid Elemental', wcid: 14513, creatureTypeId: 60, setupId: '0x02000BEE' },
    { name: 'Anekshay', wcid: 44021, creatureTypeId: 101, setupId: '0x02001AA3' },
    { name: 'Armoredillo', wcid: 19, creatureTypeId: 17, setupId: '0x02000004' },
    { name: 'Auroch', wcid: 28637, creatureTypeId: 11, setupId: '0x02000CD0' },
    { name: 'Banderling', wcid: 183, creatureTypeId: 2, setupId: '0x02000E08' },
    { name: 'Burun', wcid: 26012, creatureTypeId: 75, setupId: '0x02001036' },
    { name: 'Carenzi', wcid: 11468, creatureTypeId: 55, setupId: '0x02000A95' },
    { name: 'Chittick', wcid: 4243, creatureTypeId: 33, setupId: '0x02000E66' },
    { name: 'Cow', wcid: 3110132, creatureTypeId: 12, setupId: '0x02000006' },
    { name: 'Deru', wcid: 4262, creatureTypeId: 37, setupId: '0x020002D9' },
    { name: 'Drudge', wcid: 28640, creatureTypeId: 3, setupId: '0x020007DD' },
    { name: 'Fae', wcid: 99999989, creatureTypeId: 18, setupId: '0x02001A10' },
    { name: 'Fire Elemental', wcid: 5705, creatureTypeId: 38, setupId: '0x020006A3' },
    { name: 'Frost Elemental', wcid: 14512, creatureTypeId: 61, setupId: '0x02000BEF' },
    { name: 'Gear Knight', wcid: 41244, creatureTypeId: 99, setupId: '0x0200190F' },
    { name: 'Ghost', wcid: 28048, creatureTypeId: 77, setupId: '0x02001120' },
    { name: 'Golem', wcid: 194, creatureTypeId: 13, setupId: '0x020007CA' },
    { name: 'Grievver', wcid: 7978, creatureTypeId: 44, setupId: '0x020008DA' },
    { name: 'Gromnie', wcid: 17, creatureTypeId: 15, setupId: '0x02000037' },
    { name: 'Gurog', wcid: 43391, creatureTypeId: 100, setupId: '0x02001A2B' },
    { name: 'Knathtead', wcid: 28659, creatureTypeId: 21, setupId: '0x020004AF' },
    { name: 'Lightning Elemental', wcid: 6379, creatureTypeId: 42, setupId: '0x020006AC' },
    { name: 'Lugian', wcid: 5, creatureTypeId: 5, setupId: '0x02000A0B' },
    { name: 'Mattekar', wcid: 2581, creatureTypeId: 23, setupId: '0x02000486' },
    { name: 'Merwart', wcid: 32051, creatureTypeId: 90, setupId: '0x0200003A' },
    { name: 'Mite', wcid: 10, creatureTypeId: 7, setupId: '0x02001080' },
    { name: 'Moarsman', wcid: 4246, creatureTypeId: 34, setupId: '0x02000992' },
    { name: 'Monouga', wcid: 2574, creatureTypeId: 28, setupId: '0x020002FF' },
    { name: 'Mosswart', wcid: 8, creatureTypeId: 4, setupId: '0x02000B4F' },
    { name: 'Mukkir', wcid: 31897, creatureTypeId: 89, setupId: '0x020014BD' },
    { name: 'Niffis', wcid: 7985, creatureTypeId: 45, setupId: '0x02000926' },
    { name: 'Olthoi', wcid: 3, creatureTypeId: 1, setupId: '0x02000AAC' },
    { name: 'Phyntos Wasp', wcid: 12, creatureTypeId: 9, setupId: '0x02001121' },
    { name: 'Rabbit', wcid: 2567, creatureTypeId: 25, setupId: '0x0200047B' },
    { name: 'Rat', wcid: 13, creatureTypeId: 10, setupId: '0x0200003D' },
    { name: 'Reedshark', wcid: 18, creatureTypeId: 16, setupId: '0x02000039' },
    { name: 'Ruschk', wcid: 28666, creatureTypeId: 81, setupId: '0x02001240' },
    { name: 'Sclavus', wcid: 2583, creatureTypeId: 26, setupId: '0x02000498' },
    { name: 'Shadow', wcid: 1756, creatureTypeId: 22, setupId: '0x02000001' },
    { name: 'Shallows Shark', wcid: 2577, creatureTypeId: 27, setupId: '0x02001480' },
    { name: 'Shreth', wcid: 4108, creatureTypeId: 32, setupId: '0x020005C4' },
    { name: 'Siraluun', wcid: 11486, creatureTypeId: 56, setupId: '0x02000A43' },
    { name: 'Skeleton', wcid: 1760, creatureTypeId: 30, setupId: '0x02000059' },
    { name: 'Snowman', wcid: 5760, creatureTypeId: 39, setupId: '0x020006FD' },
    { name: 'Thrungus', wcid: 28672, creatureTypeId: 82, setupId: '0x02001253' },
    { name: 'Tumerok', wcid: 226, creatureTypeId: 6, setupId: '0x02001408' },
    { name: 'Tusker', wcid: 11, creatureTypeId: 8, setupId: '0x02000964' },
    { name: 'Undead', wcid: 950, creatureTypeId: 14, setupId: '0x02000197' },
    { name: 'Ursuin', wcid: 7991, creatureTypeId: 46, setupId: '0x02000925' },
    { name: 'Viridian Statue', wcid: 19267, creatureTypeId: 68, setupId: '0x020008DA' },
    { name: 'Virindi', wcid: 238, creatureTypeId: 19, setupId: '0x02000041' },
    { name: 'Wisp', wcid: 1535, creatureTypeId: 20, setupId: '0x0200059A' },
    { name: 'Zefir', wcid: 2608, creatureTypeId: 29, setupId: '0x0200049A' },
  ]

  // Parent Alpha (Stud) State
  const [alphaSpecies, setAlphaSpecies] = useState<string>('Acid Elemental')
  const [alphaLvl, setAlphaLvl] = useState<number>(300)
  const [alphaGenetics, setAlphaGenetics] = useState<PetGenetics>({ ...EMPTY_GENETICS })
  const [alphaIncense, setAlphaIncense] = useState<number>(0)
  const [alphaCatalyst, setAlphaCatalyst] = useState<boolean>(false)
  const [alphaCharges, setAlphaCharges] = useState<number>(DEFAULT_BREEDING_CADENCE.maleChargesPerRefill)
  const [alphaPalette, setAlphaPalette] = useState<PetPalette>({
    name: 'Alpha Lineage (Royal Amber)',
    hex: '#F59E0B',
    templateId: 4,
    paletteId: 0x0F000004,
    hueShift: 0,
    swatches: ['#F59E0B', '#D97706', '#B45309', '#78350F', '#451A03', '#27272A', '#18181B', '#09090B']
  })

  // Parent Beta (Donor) State
  const [betaSpecies, setBetaSpecies] = useState<string>('Acid Elemental')
  const [betaLvl, setBetaLvl] = useState<number>(300)
  const [betaGenetics, setBetaGenetics] = useState<PetGenetics>({ ...EMPTY_GENETICS })
  const [betaIncense, setBetaIncense] = useState<number>(0)
  const [betaCatalyst, setBetaCatalyst] = useState<boolean>(false)
  const [betaPalette, setBetaPalette] = useState<PetPalette>({
    name: 'Beta Lineage (Azure Slate)',
    hex: '#3B82F6',
    templateId: 1,
    paletteId: 0x040001BB,
    hueShift: 0,
    swatches: ['#3B82F6', '#2563EB', '#1D4ED8', '#1E40AF', '#1E3A8A', '#172554', '#0F172A', '#020617']
  })

  // Randomizer (testing aid: fills parents with plausible devices instead of typing every field)
  const [randomProfile, setRandomProfile] = useState<RandomProfile>('bred')

  // Guardian assumptions (simulator toggles)
  const [guardianKilled, setGuardianKilled] = useState<boolean>(true)
  const [guardianOverride, setGuardianOverride] = useState<boolean | null>(null)

  // Simulation state
  const [simResults, setSimResults] = useState<SimulationResult[]>([])
  const [selectedBabyId, setSelectedBabyId] = useState<string | null>(null)
  const [currentGen, setCurrentGen] = useState<number>(1)
  const [copiedCmd, setCopiedCmd] = useState<boolean>(false)
  const [copiedSql, setCopiedSql] = useState<boolean>(false)
  const [showNewborn, setShowNewborn] = useState<boolean>(false)

  // Live Debug Terminal State
  const [debugLogs, setDebugLogs] = useState<string[]>([
    `[${new Date().toLocaleTimeString()}] Pet Breeding Visualizer Debug Console Ready`
  ])
  const [copiedDebug, setCopiedDebug] = useState<boolean>(false)
  const [isConsoleOpen, setIsConsoleOpen] = useState<boolean>(true)
  /** Verbose: log every input, intermediate value and rng draw, so a pasted log can be checked. */
  const [verboseLog, setVerboseLog] = useState<boolean>(true)

  const addDebugLog = (msg: string) => {
    const time = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })
    const formatted = `[${time}] ${msg}`
    setDebugLogs(prev => [formatted, ...prev].slice(0, DEBUG_LOG_LIMIT))
  }
  const addDebugLogs = (msgs: string[]) => {
    const time = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })
    const formatted = msgs.map(m => `[${time}] ${m}`).reverse()
    setDebugLogs(prev => [...formatted, ...prev].slice(0, DEBUG_LOG_LIMIT))
  }

  // Time-to-Target Estimator Inputs
  const [calcTargetMuts, setCalcTargetMuts] = useState<number>(10)
  const [calcDonors, setCalcDonors] = useState<number>(20)
  const [calcProfile, setCalcProfile] = useState<ActivityProfile>('dedicated')
  const [calcSeed, setCalcSeed] = useState<number>(1)

  const [serverBreedingConfig, setServerBreedingConfig] = useState<BreedingConfig>(DEFAULT_BREEDING_CONFIG)
  // Breeding limits, essence tiers and incense options: the server's live values, same payload as the config.
  const [cadence, setCadence] = useState<BreedingCadence>(DEFAULT_BREEDING_CADENCE)
  const [configSource, setConfigSource] = useState<'fallback' | 'server'>('fallback')
  const validLevels = cadence.tiers
  const incenseOptions = [{ label: 'None', bonus: 0 }, ...cadence.incense]
  const pctText = (fraction: number) => `${+(fraction * 100).toFixed(2)}%`

  useEffect(() => {
    fetch('/api/visualizer/breeding-config')
      .then(res => res.json())
      .then(data => {
        if (data && typeof data.baseMutationChance === 'number') {
          setServerBreedingConfig(normalizeBreedingConfig(data))
          const serverCadence = normalizeBreedingCadence(data)
          setCadence(serverCadence)
          setAlphaCharges(serverCadence.maleChargesPerRefill)
          setConfigSource('server')
        }
      })
      .catch(e => console.error('Failed to fetch server breeding config', e))
  }, [])

  const config: BreedingConfig = guardianOverride === null
    ? serverBreedingConfig
    : { ...serverBreedingConfig, guardianEnabled: guardianOverride }

  const breedOptions: BreedOptions = {
    incenseA: alphaIncense, incenseB: betaIncense,
    catalystA: alphaCatalyst, catalystB: betaCatalyst,
    guardianKilled,
  }

  /**
   * Fill one parent with a random plausible device. Consumables are left alone: incense and the
   * catalyst are deliberate test switches, not part of the pet.
   */
  const randomizeParent = (slot: 'alpha' | 'beta', profile: RandomProfile = randomProfile) => {
    const species = randomPick(speciesList).name
    // Tier gate: only devices at or above the server's minimum tier can breed, so do not generate parents that cannot.
    const level = randomPick(validLevels.filter(l => l >= cadence.minParentTier))
    const genetics = randomGenetics(profile, config)
    const label = slot === 'alpha' ? 'Alpha Lineage' : 'Beta Lineage'
    const palette = randomPaletteForSpecies(species, label)

    if (slot === 'alpha') {
      setAlphaSpecies(species); setAlphaLvl(level); setAlphaGenetics(genetics); setAlphaPalette(palette)
    } else {
      setBetaSpecies(species); setBetaLvl(level); setBetaGenetics(genetics); setBetaPalette(palette)
    }

    const adult = summonedStats(genetics, config)
    addDebugLog(
      `[RANDOMIZE] Parent ${slot === 'alpha' ? 'Alpha' : 'Beta'} (${RANDOM_PROFILE_OPTIONS.find(p => p.key === profile)?.label}): ` +
      `${species} lvl ${level}, palette "${palette.name}". Gear dmg/dr/crit ${genetics.gearDamage}/${genetics.gearDamageResist}/${genetics.gearCrit}, ` +
      `muts ${genetics.dmg}/${genetics.dr}/${genetics.crit}/${genetics.vit} (+${genetics.pot} pot, stored ${genetics.potencyStored}). ` +
      `Summoned adult: DR ${adult.damageRating}, DRR ${adult.damageResistRating}, Crit ${adult.critRating}, HP +${adult.bonusHp}.`
    )
  }

  const randomizeBothParents = () => {
    randomizeParent('alpha')
    randomizeParent('beta')
  }

  // Stat mutation odds depend on the BABY's inherited counts, so show the reachable range.
  const incenseBonus = combinedIncenseBonus(alphaIncense, betaIncense, config)
  const statLines: StatLine[] = ['dmg', 'dr', 'crit', 'vit']
  const minInheritedMuts = statLines.reduce((s, l) => s + Math.min(alphaGenetics[l], betaGenetics[l]), 0)
  const maxInheritedMuts = statLines.reduce((s, l) => s + Math.max(alphaGenetics[l], betaGenetics[l]), 0)
  const statChanceBest = statMutationChance(minInheritedMuts, config, incenseBonus)
  const statChanceWorst = statMutationChance(maxInheritedMuts, config, incenseBonus)
  const potChance = potencyMutationChance(config)
  const anyMutationBest = 1 - (1 - statChanceBest) * (1 - potChance)
  const anyMutationWorst = 1 - (1 - statChanceWorst) * (1 - potChance)

  const speciesPalettesCache = useRef<{ [wcid: number]: any[] }>({})
  const masterMutationPoolRef = useRef<any[]>([])
  const vibrantMutationPoolRef = useRef<any[] | null>(null)

  const fetchMasterMutationPool = async (): Promise<any[]> => {
    if (masterMutationPoolRef.current.length > 0) {
      return masterMutationPoolRef.current
    }
    try {
      const res = await fetch('/api/visualizer/master-mutation-pool')
      const data = await res.json()
      if (Array.isArray(data) && data.length > 0) {
        masterMutationPoolRef.current = data
        return data
      }
    } catch (e) {
      console.error('Failed to fetch master mutation pool', e)
    }
    return []
  }

  /** Vibrant (Chromatic Catalyst) pool; falls back to the master pool when the server exposes no vibrant endpoint. */
  const fetchVibrantMutationPool = async (): Promise<{ pool: any[], vibrant: boolean }> => {
    if (vibrantMutationPoolRef.current === null) {
      try {
        const res = await fetch('/api/visualizer/vibrant-mutation-pool')
        const data = res.ok ? await res.json() : null
        vibrantMutationPoolRef.current = Array.isArray(data) && data.length > 0 ? data : []
      } catch {
        vibrantMutationPoolRef.current = []
      }
    }
    if (vibrantMutationPoolRef.current.length > 0) return { pool: vibrantMutationPoolRef.current, vibrant: true }
    return { pool: await fetchMasterMutationPool(), vibrant: false }
  }

  const fetchSpeciesPalettes = async (wcid: number): Promise<any[]> => {
    if (speciesPalettesCache.current[wcid]) {
      return speciesPalettesCache.current[wcid]
    }
    try {
      const res = await fetch(`/api/visualizer/species-palettes/${wcid}`)
      const data = await res.json()
      if (Array.isArray(data)) {
        speciesPalettesCache.current[wcid] = data
        return data
      }
    } catch (e) {
      console.error('Failed to fetch species palettes for wcid', wcid, e)
    }
    return []
  }

  const describeMutations = (result: BreedResult): string[] => {
    const summary: string[] = []
    if (result.potencyRoll.applied) summary.push(`+${result.potencyRoll.step} Potency`)
    if (result.statRoll.line) summary.push(`+${result.statRoll.step} ${STAT_LINE_LABELS[result.statRoll.line]}`)
    if (result.guardian.blessingLine) summary.push(`Awakened Blessing: +${result.guardian.blessingStep} ${STAT_LINE_LABELS[result.guardian.blessingLine]}`)
    return summary
  }

  // Simulate single breed
  const handleBreedSimulation = async () => {
    addDebugLog(`--- [BREEDING RITUAL INITIATED] ---`)
    addDebugLog(`[PARENTS] Alpha (Stud): ${alphaSpecies} (Lvl ${alphaLvl}, ${totalStatMutations(alphaGenetics)} stat muts / ${alphaGenetics.pot} pot muts, potency ${alphaGenetics.potencyStored}, Palette: "${alphaPalette.name}") x Beta (Donor): ${betaSpecies} (Lvl ${betaLvl}, ${totalStatMutations(betaGenetics)} stat muts / ${betaGenetics.pot} pot muts, potency ${betaGenetics.potencyStored}, Palette: "${betaPalette.name}")`)
    addDebugLog(`[CONFIG:${configSource}] base ${config.baseMutationChance} decay ${config.mutationDecayRate} floor ${config.mutationMinFloor} potChance ${config.potencyMutationChance} steps dmg/dr/crit/vit/pot ${config.damageMutationStep}/${config.drMutationStep}/${config.critMutationStep}/${config.vitalityMutationStep}/${config.potencyMutationStep} softCap ${config.potencySoftCap} hardCap ${config.potencyHardCap} maxStored ${config.potencyMaxStored} maxStatMuts ${config.maxStatMutations} force ${config.forceMutation} guardian ${config.guardianEnabled}`)

    if (alphaCharges <= 0) {
      addDebugLog(`[ERROR] Alpha Stud is out of breeding charges (0/${cadence.maleChargesPerRefill})!`)
      alert(`Alpha Stud is out of breeding charges (0/${cadence.maleChargesPerRefill})! Charges refill ${cadence.maleRefillHours}h after the last refill, or click 'Reset Alpha Charges'.`)
      return
    }

    // Server: species donor is a 50/50 coin flip. Palette carry-over from the donor is a simulator assumption.
    const donorIsAlpha = Math.random() < 0.50
    const donorParent: 'Alpha' | 'Beta' = donorIsAlpha ? 'Alpha' : 'Beta'
    const babyLevel = donorIsAlpha ? alphaLvl : betaLvl
    const babySpecies = donorIsAlpha ? alphaSpecies : betaSpecies
    const babyWcid = speciesList.find(s => s.name === babySpecies)?.wcid || 25749

    addDebugLog(`[SPECIES ROLL] 50/50 -> species donor Parent ${donorParent}: ${babySpecies}`)

    const result = breed(alphaGenetics, betaGenetics, config, { ...breedOptions, verbose: verboseLog }, Math.random)
    addDebugLogs(result.log)

    const mutationSummary = describeMutations(result)
    if (result.statRoll.line && result.potencyRoll.applied) {
      addDebugLog(`[DOUBLE MUTATION] Both the stat roll and the potency roll hit on this breed!`)
    }
    if (!result.hasMutation) {
      addDebugLog(`[RESULT] Normal breed: stats inherited, no mutation, palette inherited`)
    } else {
      addDebugLog(`[RESULT] ${mutationSummary.join(', ')}`)
    }

    let guardianNote: string | null = null
    if (result.guardian.spawned) {
      guardianNote = result.guardian.killed
        ? (result.guardian.blessingLine ? `Guardian slain: Awakened Blessing +1 ${STAT_LINE_LABELS[result.guardian.blessingLine]}` : 'Guardian slain: every line capped, no blessing')
        : 'Guardian not killed: no blessing'
    }

    // Palette: inherited unless the breed mutated, in which case the server rolls from a pool.
    let babyPalette: PetPalette
    const availableVariants = await fetchSpeciesPalettes(babyWcid)

    if (!result.paletteRolled) {
      const parentPal = donorIsAlpha ? alphaPalette : betaPalette
      babyPalette = resolvePaletteForSpecies(parentPal, babySpecies, donorParent, availableVariants)
      addDebugLog(`[PALETTE RESOLUTION] Inherited Parent ${donorParent}'s Color Family "${parentPal.name}"`)
      addDebugLog(`[DAT MAPPING] Mapped to ${babySpecies} DAT Palette Entry #${babyPalette.templateId} (PaletteID: 0x${babyPalette.paletteId ? babyPalette.paletteId.toString(16).toUpperCase() : '0'}, HueShift: ${babyPalette.hueShift}deg)`)
    } else {
      let pool: any[]
      let poolName = 'master'
      if (result.paletteUsesVibrantPool) {
        const vib = await fetchVibrantMutationPool()
        pool = vib.pool
        poolName = vib.vibrant ? 'vibrant' : 'master (vibrant pool not exposed by server; using master)'
      } else {
        pool = await fetchMasterMutationPool()
      }
      if (pool.length > 0) {
        const chosenPal = pool[Math.floor(Math.random() * pool.length)]
        babyPalette = {
          name: `${result.paletteUsesVibrantPool ? 'Vibrant' : 'Universal'} Mutation (${chosenPal.paletteHex})`,
          hex: chosenPal.swatches && chosenPal.swatches.length > 0 ? chosenPal.swatches[0] : '#F59E0B',
          templateId: 0,
          paletteId: chosenPal.paletteId,
          hueShift: 0,
          swatches: chosenPal.swatches || []
        }
        addDebugLog(`[COLOR MUTATION] Rolled DAT palette ${chosenPal.paletteHex} from the ${poolName} pool`)
      } else {
        const nonDefaultVariants = availableVariants.filter((v: any) => !v.isDefault)
        if (nonDefaultVariants.length > 0) {
          const chosenVar = nonDefaultVariants[Math.floor(Math.random() * nonDefaultVariants.length)]
          babyPalette = {
            name: `Rare Mutation (${chosenVar.name})`,
            hex: chosenVar.swatches && chosenVar.swatches.length > 0 ? chosenVar.swatches[0] : '#F59E0B',
            templateId: chosenVar.templateId,
            paletteId: chosenVar.paletteId,
            hueShift: 0,
            swatches: chosenVar.swatches || []
          }
          addDebugLog(`[COLOR MUTATION] Pool unavailable; picked species variant "${chosenVar.name}" (DAT PaletteID: 0x${chosenVar.paletteId ? chosenVar.paletteId.toString(16).toUpperCase() : '0'}, Template #${chosenVar.templateId})`)
        } else {
          const mutKeys = ['emerald', 'gold', 'cobalt', 'steel', 'crimson', 'amber', 'azure']
          const chosenKey = mutKeys[Math.floor(Math.random() * mutKeys.length)]
          const mutFam = COLOR_FAMILIES[chosenKey] || COLOR_FAMILIES['emerald']
          const specMap = mutFam.templates[babySpecies] || { templateId: 1, paletteId: 0, hueShift: 0 }
          babyPalette = {
            name: `Rare Mutation (${mutFam.name})`,
            hex: mutFam.hex,
            templateId: specMap.templateId,
            paletteId: specMap.paletteId,
            hueShift: Math.floor(Math.random() * 360),
            swatches: mutFam.swatches
          }
          addDebugLog(`[COLOR MUTATION] Pool unavailable; picked colour family "${mutFam.name}"`)
        }
      }
    }

    // Consumables: the server clears incense on every breed; a catalyst is spent only when a palette was rolled.
    if (alphaIncense > 0 || betaIncense > 0) {
      addDebugLog(`[CONSUMED] Courtship Incense on both parents cleared`)
      setAlphaIncense(0)
      setBetaIncense(0)
    }
    if (result.catalystConsumed) {
      addDebugLog(`[CONSUMED] Chromatic Catalyst spent (palette rolled)`)
      setAlphaCatalyst(false)
      setBetaCatalyst(false)
    }

    const babyId = `baby_${Date.now()}_${Math.floor(Math.random() * 1000)}`

    const palHex = babyPalette.paletteId ? `0x${babyPalette.paletteId.toString(16).toUpperCase()}` : '0'
    addDebugLog(`[IN-GAME COMMAND] @create ${babyWcid} 1 ${palHex} 0.5`)

    const newResult: SimulationResult = {
      id: babyId,
      generation: currentGen,
      parentAlphaSpecies: alphaSpecies,
      parentBetaSpecies: betaSpecies,
      donorParent,
      species: babySpecies,
      level: babyLevel,
      genetics: result.baby,
      adult: summonedStats(result.baby, config),
      newborn: summonedStats(result.baby, config, 1),
      hasMutation: result.hasMutation,
      mutationSummary,
      guardianNote,
      isColorMutated: result.paletteRolled,
      cooldownHours: cadence.femaleRecoveryHours,
      palette: babyPalette,
      paletteName: babyPalette.name,
      paletteHex: babyPalette.hex,
      paletteTemplateId: babyPalette.templateId,
      paletteId: babyPalette.paletteId,
      paletteSwatches: babyPalette.swatches,
      hueShift: babyPalette.hueShift,
      timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' }),
    }

    setSimResults([newResult, ...simResults])
    setSelectedBabyId(babyId)
    setCurrentGen(currentGen + 1)
    setAlphaCharges(alphaCharges - 1)
  }

  const resetSimulation = () => {
    addDebugLog(`[RESET] Simulation reset to Generation 1 (${cadence.maleChargesPerRefill} Alpha Stud Charges restored)`)
    setSimResults([])
    setSelectedBabyId(null)
    setCurrentGen(1)
    setAlphaCharges(cadence.maleChargesPerRefill)
  }

  const promoteToAlpha = (baby: SimulationResult) => {
    setAlphaSpecies(baby.species)
    setAlphaLvl(baby.level)
    setAlphaGenetics({ ...baby.genetics })
    if (baby.palette) {
      setAlphaPalette(baby.palette)
    }
    addDebugLog(`[PROMOTION] Promoted Gen ${baby.generation} ${baby.species} to Parent Alpha (Stud). Lineage Palette "${baby.paletteName}" set as Alpha Stud Palette.`)
    alert(`Gen ${baby.generation} ${baby.species} promoted to Parent Alpha (Stud)! Palette "${baby.paletteName}" carried into Alpha Lineage.`)
  }

  const promoteToBeta = (baby: SimulationResult) => {
    setBetaSpecies(baby.species)
    setBetaLvl(baby.level)
    setBetaGenetics({ ...baby.genetics })
    if (baby.palette) {
      setBetaPalette(baby.palette)
    }
    addDebugLog(`[PROMOTION] Promoted Gen ${baby.generation} ${baby.species} to Parent Beta (Donor). Lineage Palette "${baby.paletteName}" set as Beta Donor Palette.`)
    alert(`Gen ${baby.generation} ${baby.species} promoted to Parent Beta (Donor)! Palette "${baby.paletteName}" carried into Beta Lineage.`)
  }

  const runModelSelfCheck = () => {
    const res = runSelfCheck()
    addDebugLogs(res.lines)
  }

  const activeSelectedBaby = simResults.find(b => b.id === selectedBabyId) || simResults[0] || null
  const isSkinCleansed = false

  const handleCopyCmd = async () => {
    if (!activeSelectedBaby) return
    const babyWcid = speciesList.find(s => s.name === activeSelectedBaby.species)?.wcid || 25749
    let cmd = ''
    if (isSkinCleansed || (!activeSelectedBaby.paletteId && !activeSelectedBaby.paletteTemplateId)) {
      cmd = `@create ${babyWcid} 1 0`
    } else {
      let palParam = '0';
      const pId = activeSelectedBaby.paletteId || 0;
      const pTemp = activeSelectedBaby.paletteTemplateId || 0;

      if ((pId & 0xFF000000) === 0x04000000) {
        palParam = `0x${pId.toString(16).toUpperCase()}`;
      } else {
        const variants = speciesPalettesCache.current[babyWcid] || [];
        const matched = variants.find((v: any) => v.templateId === pTemp || v.templateId === pId || v.paletteId === pId);
        if (matched && matched.paletteId && (matched.paletteId & 0xFF000000) === 0x04000000) {
          palParam = `0x${matched.paletteId.toString(16).toUpperCase()}`;
        } else if (matched && matched.paletteHex && matched.paletteHex.startsWith('0x04')) {
          palParam = matched.paletteHex;
        } else if (pTemp > 0) {
          palParam = `${pTemp}`;
        } else if (pId > 0 && (pId & 0xFF000000) !== 0x0F000000) {
          palParam = `${pId}`;
        }
      }
      cmd = `@create ${babyWcid} 1 ${palParam} 0.5`
    }
    const success = await copyToClipboard(cmd)
    if (success) {
      setCopiedCmd(true)
      setTimeout(() => setCopiedCmd(false), 2000)
    }
  }

  const handleCopySql = async () => {
    if (!activeSelectedBaby) return
    const pId = activeSelectedBaby.paletteId || 0;
    const pTemp = activeSelectedBaby.paletteTemplateId || 0;
    let sql = '';
    if ((pId & 0xFF000000) === 0x04000000) {
      const palHex = `0x${pId.toString(16).toUpperCase()}`;
      sql = `INSERT INTO \`weenie_properties_did\` (\`object_wcid\`, \`type\`, \`value\`) VALUES (YOUR_WCID, 8, ${palHex});`;
    } else {
      const palVal = pTemp > 0 ? pTemp : (pId > 0 && (pId & 0xFF000000) !== 0x0F000000 ? pId : 0);
      sql = `INSERT INTO \`weenie_properties_int\` (\`object_wcid\`, \`type\`, \`value\`) VALUES (YOUR_WCID, 23, ${palVal});`;
    }
    const success = await copyToClipboard(sql)
    if (success) {
      setCopiedSql(true)
      setTimeout(() => setCopiedSql(false), 2000)
    }
  }

  // Throughput: each stud gives its charges once per refill period; each female breeds once per recovery.
  // A refill time of 0 means stud charges never come back: the studs' first charges are all there will ever be.
  const studsOwned = PROFILE_STUDS[calcProfile]
  const studsRefill = cadence.maleRefillHours > 0
  const studBreedsPerDay = studsRefill ? studsOwned * cadence.maleChargesPerRefill * (24 / cadence.maleRefillHours) : Infinity
  const studLifetimeBreeds = studsRefill ? Infinity : studsOwned * cadence.maleChargesPerRefill
  const femaleRate = femaleBreedsPerDay(cadence)
  const donorBreedsPerDay = calcDonors * femaleRate
  const breedsPerDay = Math.max(1, Math.min(studBreedsPerDay, donorBreedsPerDay, studLifetimeBreeds))
  /** Breeds the studs can ever supply within the projection window. */
  const breedsWithin = (dayCount: number) => Math.floor(Math.min(breedsPerDay * dayCount, studLifetimeBreeds))

  // Monte Carlo projections through the shared model: keep-the-best-baby campaign against a fixed donor.
  const est = useMemo(() => {
    const rng = seededRng(calcSeed * 7919 + 17)
    const toTarget: number[] = []
    let reached = 0
    for (let t = 0; t < PROJECTION_TRIALS; t++) {
      const r = runCampaign(alphaGenetics, betaGenetics, config, { ...breedOptions, maxBreeds: PROJECTION_MAX_BREEDS, targetStatMutations: calcTargetMuts }, rng)
      toTarget.push(r.breeds)
      if (r.reachedTarget) reached++
    }
    toTarget.sort((a, b) => a - b)

    const ninetyDayBreeds = breedsWithin(PROJECTION_DAYS)
    const statMuts: number[] = []
    const potMuts: number[] = []
    const paletteRolls: number[] = []
    const blessings: number[] = []
    for (let t = 0; t < PROJECTION_TRIALS; t++) {
      const r = runCampaign(alphaGenetics, betaGenetics, config, { ...breedOptions, maxBreeds: Math.min(ninetyDayBreeds, PROJECTION_MAX_BREEDS) }, rng)
      statMuts.push(totalStatMutations(r.best))
      potMuts.push(r.best.pot)
      paletteRolls.push(r.mutatedBreeds)
      blessings.push(r.blessings)
    }
    statMuts.sort((a, b) => a - b)
    potMuts.sort((a, b) => a - b)
    paletteRolls.sort((a, b) => a - b)
    blessings.sort((a, b) => a - b)

    return {
      breedsMedian: percentile(toTarget, 0.5),
      breedsFast: percentile(toTarget, 0.2),
      breedsSlow: percentile(toTarget, 0.8),
      reachedPct: (reached / PROJECTION_TRIALS) * 100,
      ninetyDayBreeds,
      statMutsMedian: percentile(statMuts, 0.5),
      statMutsFast: percentile(statMuts, 0.8),
      statMutsSlow: percentile(statMuts, 0.2),
      potMutsMedian: percentile(potMuts, 0.5),
      paletteRollsMedian: percentile(paletteRolls, 0.5),
      blessingsMedian: percentile(blessings, 0.5),
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [alphaGenetics, betaGenetics, config, alphaIncense, betaIncense, alphaCatalyst, betaCatalyst, guardianKilled, calcTargetMuts, breedsPerDay, studLifetimeBreeds, calcSeed])

  /** Whether the studs can supply this many breeds at all (they cannot past their charges when refills are off). */
  const reachable = (breeds: number) => breeds <= studLifetimeBreeds
  const days = (breeds: number) => (reachable(breeds) ? `${(breeds / breedsPerDay).toFixed(1)} d` : 'never')
  const stageMultipliers = config.maturityMultipliers
  const potencyCapValue = potencyCap(config)
  const shownStats = activeSelectedBaby ? (showNewborn ? activeSelectedBaby.newborn : activeSelectedBaby.adult) : null

  return (
    <div className="absolute inset-0 bg-neutral-950 text-neutral-200 overflow-y-auto p-4 md:p-8 font-sans selection:bg-rose-500/30">

      {/* Background Glow */}
      <div className="absolute top-[-10%] left-[-10%] w-[50%] h-[50%] rounded-full bg-blue-500/10 blur-[120px] pointer-events-none" />
      <div className="absolute bottom-[-10%] right-[-10%] w-[50%] h-[50%] rounded-full bg-rose-500/10 blur-[120px] pointer-events-none" />

      <div className="max-w-7xl mx-auto space-y-6 relative z-10">

        {/* Header */}
        <div className="flex items-center justify-between border-b border-neutral-800/80 pb-5 flex-wrap gap-4">
          <div className="flex items-center space-x-4">
            <div className="w-12 h-12 rounded-xl bg-gradient-to-tr from-rose-500 to-violet-600 flex items-center justify-center shadow-lg shadow-rose-500/20">
              <Heart className="w-6 h-6 text-white animate-pulse" />
            </div>
            <div>
              <h1 className="text-2xl md:text-3xl font-extrabold tracking-tight bg-clip-text text-transparent bg-gradient-to-r from-neutral-50 via-neutral-100 to-neutral-400">
                Gene Summons' Next-Gen Pet Breeding Simulator
              </h1>
              <p className="text-xs md:text-sm text-neutral-400 font-medium">
                Alpha/Non-Alpha Pairing • Server-Mirrored Breeding Math • Live 3D Showroom
              </p>
            </div>
          </div>

          {/* Navigation Tabs */}
          <div className="flex items-center bg-neutral-900 border border-neutral-800 rounded-xl p-1 space-x-1">
            <button
              onClick={() => setActiveTab('simulator')}
              className={`px-4 py-2 rounded-lg text-xs font-black transition-all cursor-pointer flex items-center gap-1.5 ${
                activeTab === 'simulator'
                  ? 'bg-rose-500 text-white shadow'
                  : 'text-neutral-400 hover:text-neutral-200 hover:bg-neutral-800/50'
              }`}
            >
              <Sparkles className="w-3.5 h-3.5" /> Breeding Simulator
            </button>
            <button
              onClick={() => setActiveTab('calculator')}
              className={`px-4 py-2 rounded-lg text-xs font-black transition-all cursor-pointer flex items-center gap-1.5 ${
                activeTab === 'calculator'
                  ? 'bg-rose-500 text-white shadow'
                  : 'text-neutral-400 hover:text-neutral-200 hover:bg-neutral-800/50'
              }`}
            >
              <BarChart3 className="w-3.5 h-3.5" /> Time-to-Target Estimator
            </button>
            <button
              onClick={() => setActiveTab('guide')}
              className={`px-4 py-2 rounded-lg text-xs font-black transition-all cursor-pointer flex items-center gap-1.5 ${
                activeTab === 'guide'
                  ? 'bg-rose-500 text-white shadow'
                  : 'text-neutral-400 hover:text-neutral-200 hover:bg-neutral-800/50'
              }`}
            >
              <HelpCircle className="w-3.5 h-3.5" /> How It Works
            </button>
          </div>
        </div>

        {/* STEP-BY-STEP QUICK GUIDE BANNER */}
        <div className="bg-gradient-to-r from-blue-950/40 via-purple-950/30 to-rose-950/40 border border-blue-500/30 rounded-2xl p-4 flex items-center justify-between gap-4 flex-wrap">
          <div className="flex items-center space-x-3">
            <Info className="w-5 h-5 text-blue-400 shrink-0" />
            <div className="text-xs">
              <span className="font-extrabold text-white uppercase tracking-wider mr-2">Breeding Quick Guide:</span>
              <span className="text-neutral-300">
                1. Bring a <strong>Male Stud</strong> ({cadence.maleChargesPerRefill} charges; refill {cadence.maleRefillHours}h after the last refill) • 2. Bring a <strong>Female Dam</strong> ({cadence.femaleRecoveryHours}h recovery after each breed) • 3. In the <strong>Seedy Motel</strong>, with both pets in the same landcell, both players perform <code>*dance*</code> (or <code>@dance</code>) within {cadence.danceWindowSeconds} seconds of each other. Boost results with <strong>Courtship Incense</strong> ({cadence.incense.map(i => `+${pctText(i.bonus)}`).join(', ')} stat mutation chance, decaying with the line like the base chance; potency is unaffected) and a <strong>Chromatic Catalyst</strong> (vibrant palette pool when a mutation rolls a new colour); use <strong>Pet Tailoring Kits</strong> for cosmetic appearances and <strong>Pet Neutering Kits</strong> to prevent breeding.
              </span>
            </div>
          </div>
          <button
            onClick={() => setActiveTab('guide')}
            className="text-[11px] bg-blue-500/20 hover:bg-blue-500/30 text-blue-300 border border-blue-500/40 px-3 py-1 rounded-lg font-bold transition-all cursor-pointer whitespace-nowrap"
          >
            Read Full Guide →
          </button>
        </div>

        {/* TAB 1: BREEDING SIMULATOR */}
        {activeTab === 'simulator' && (
          <div className="space-y-6">

            {/* TOP ROW: PARENT ALPHA + PARENT BETA + BREED ACTION */}
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">

              {/* PARENT ALPHA (COLUMN 1) */}
              <div className="bg-neutral-900/70 backdrop-blur-md rounded-2xl border border-blue-500/30 p-5 space-y-4 shadow-xl relative overflow-hidden">
                <div className="flex items-center justify-between border-b border-neutral-800/80 pb-3">
                  <span className="text-sm font-black uppercase tracking-wider text-blue-400 flex items-center gap-2">
                    <Dna className="w-4 h-4" /> Parent Alpha (Stud)
                  </span>
                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => randomizeParent('alpha')}
                      title="Randomize this parent"
                      className="text-neutral-400 hover:text-blue-300 border border-neutral-800 hover:border-blue-500/40 rounded-lg p-1 transition-all active:scale-95 cursor-pointer"
                    >
                      <Dices className="w-3.5 h-3.5" />
                    </button>
                    <span className="text-[10px] bg-blue-500/20 text-blue-300 border border-blue-500/40 px-2 py-0.5 rounded font-black">
                      {alphaCharges}/{cadence.maleChargesPerRefill} Charges
                    </span>
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-neutral-400 font-semibold block mb-1">Species</label>
                    <select
                      value={alphaSpecies}
                      onChange={(e) => setAlphaSpecies(e.target.value)}
                      className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs font-bold focus:outline-none focus:border-blue-500 text-blue-300"
                    >
                      {speciesList.map(s => <option key={s.name} value={s.name}>{s.name}</option>)}
                    </select>
                  </div>

                  <div>
                    <label className="text-xs text-neutral-400 font-semibold block mb-1">Level (Tier)</label>
                    <select
                      value={alphaLvl}
                      onChange={(e) => setAlphaLvl(Number(e.target.value))}
                      className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs font-bold text-neutral-200"
                    >
                      {validLevels.map(lvl => <option key={lvl} value={lvl}>Level {lvl}</option>)}
                    </select>
                  </div>
                </div>

                <GeneticsEditor genetics={alphaGenetics} onChange={setAlphaGenetics} config={config} />

                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-neutral-400 font-semibold block mb-1">Courtship Incense</label>
                    <select
                      value={alphaIncense}
                      onChange={(e) => setAlphaIncense(Number(e.target.value))}
                      className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs font-bold text-pink-300"
                    >
                      {incenseOptions.map(t => <option key={t.bonus} value={t.bonus}>{t.bonus > 0 ? `${t.label} (+${pctText(t.bonus)})` : t.label}</option>)}
                    </select>
                  </div>
                  <label className="flex items-end gap-2 pb-1.5 text-xs font-semibold text-neutral-300 cursor-pointer">
                    <input type="checkbox" checked={alphaCatalyst} onChange={(e) => setAlphaCatalyst(e.target.checked)} className="accent-rose-500" />
                    Chromatic Catalyst
                  </label>
                </div>

                {/* PARENT ALPHA PALETTE DISPLAY */}
                <div className="bg-neutral-950/80 border border-neutral-800 p-2.5 rounded-xl flex flex-col gap-1.5 text-xs mt-1">
                  <div className="flex items-center justify-between">
                    <span className="font-bold text-neutral-400">Alpha Lineage Palette:</span>
                    <span className="font-black text-amber-300 flex items-center gap-1.5">
                      <div className="w-3 h-3 rounded-full border border-white/20" style={{ backgroundColor: alphaPalette.hex }} />
                      {alphaPalette.name}
                    </span>
                  </div>
                  {alphaPalette.swatches && alphaPalette.swatches.length > 0 && (
                    <div className="flex h-3.5 w-full rounded-lg overflow-hidden border border-neutral-800 mt-0.5">
                      {alphaPalette.swatches.map((hex, i) => (
                        <div key={i} className="flex-1 h-full" style={{ backgroundColor: hex }} title={hex} />
                      ))}
                    </div>
                  )}
                </div>
              </div>

              {/* PARENT BETA (COLUMN 2) */}
              <div className="bg-neutral-900/70 backdrop-blur-md rounded-2xl border border-violet-500/30 p-5 space-y-4 shadow-xl relative overflow-hidden">
                <div className="flex items-center justify-between border-b border-neutral-800/80 pb-3">
                  <span className="text-sm font-black uppercase tracking-wider text-violet-400 flex items-center gap-2">
                    <Dna className="w-4 h-4" /> Parent Beta (Donor)
                  </span>
                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => randomizeParent('beta')}
                      title="Randomize this parent"
                      className="text-neutral-400 hover:text-violet-300 border border-neutral-800 hover:border-violet-500/40 rounded-lg p-1 transition-all active:scale-95 cursor-pointer"
                    >
                      <Dices className="w-3.5 h-3.5" />
                    </button>
                    <span className="text-[10px] bg-violet-500/20 text-violet-300 border border-violet-500/40 px-2 py-0.5 rounded font-black">
                      Female ({cadence.femaleRecoveryHours}h Recovery)
                    </span>
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-neutral-400 font-semibold block mb-1">Species</label>
                    <select
                      value={betaSpecies}
                      onChange={(e) => setBetaSpecies(e.target.value)}
                      className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs font-bold focus:outline-none focus:border-violet-500 text-violet-300"
                    >
                      {speciesList.map(s => <option key={s.name} value={s.name}>{s.name}</option>)}
                    </select>
                  </div>

                  <div>
                    <label className="text-xs text-neutral-400 font-semibold block mb-1">Level (Tier)</label>
                    <select
                      value={betaLvl}
                      onChange={(e) => setBetaLvl(Number(e.target.value))}
                      className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs font-bold text-neutral-200"
                    >
                      {validLevels.map(lvl => <option key={lvl} value={lvl}>Level {lvl}</option>)}
                    </select>
                  </div>
                </div>

                <GeneticsEditor genetics={betaGenetics} onChange={setBetaGenetics} config={config} />

                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-neutral-400 font-semibold block mb-1">Courtship Incense</label>
                    <select
                      value={betaIncense}
                      onChange={(e) => setBetaIncense(Number(e.target.value))}
                      className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs font-bold text-pink-300"
                    >
                      {incenseOptions.map(t => <option key={t.bonus} value={t.bonus}>{t.bonus > 0 ? `${t.label} (+${pctText(t.bonus)})` : t.label}</option>)}
                    </select>
                  </div>
                  <label className="flex items-end gap-2 pb-1.5 text-xs font-semibold text-neutral-300 cursor-pointer">
                    <input type="checkbox" checked={betaCatalyst} onChange={(e) => setBetaCatalyst(e.target.checked)} className="accent-rose-500" />
                    Chromatic Catalyst
                  </label>
                </div>

                {/* PARENT BETA PALETTE DISPLAY */}
                <div className="bg-neutral-950/80 border border-neutral-800 p-2.5 rounded-xl flex flex-col gap-1.5 text-xs mt-1">
                  <div className="flex items-center justify-between">
                    <span className="font-bold text-neutral-400">Beta Lineage Palette:</span>
                    <span className="font-black text-violet-300 flex items-center gap-1.5">
                      <div className="w-3 h-3 rounded-full border border-white/20" style={{ backgroundColor: betaPalette.hex }} />
                      {betaPalette.name}
                    </span>
                  </div>
                  {betaPalette.swatches && betaPalette.swatches.length > 0 && (
                    <div className="flex h-3.5 w-full rounded-lg overflow-hidden border border-neutral-800 mt-0.5">
                      {betaPalette.swatches.map((hex, i) => (
                        <div key={i} className="flex-1 h-full" style={{ backgroundColor: hex }} title={hex} />
                      ))}
                    </div>
                  )}
                </div>
              </div>

              {/* ACTION DASHBOARD (COLUMN 3) */}
              <div className="bg-gradient-to-tr from-neutral-900/90 to-neutral-900/50 backdrop-blur-md rounded-2xl border border-neutral-800 p-5 space-y-4 shadow-xl flex flex-col justify-between">
                <div className="space-y-3">
                  <div className="flex items-center justify-between border-b border-neutral-800/80 pb-3">
                    <span className="text-sm font-black uppercase tracking-wider text-rose-400 flex items-center gap-2">
                      <Sparkles className="w-4 h-4" /> Genetic Odds & Action
                    </span>
                    <span className="text-[10px] bg-rose-500/10 text-rose-400 border border-rose-500/20 px-2 py-0.5 rounded font-black">
                      Gen {currentGen}
                    </span>
                  </div>

                  <div className="space-y-2">
                    <div className="bg-neutral-950/70 border border-neutral-800 p-2.5 rounded-xl flex items-center justify-between gap-2">
                      <div className="text-[11px] font-bold text-neutral-400">Stat Mutation Chance:</div>
                      <div className="text-xs font-black text-emerald-400 text-right">
                        {statChanceBest === statChanceWorst
                          ? `${(statChanceBest * 100).toFixed(2)}%`
                          : `${(statChanceWorst * 100).toFixed(2)}% - ${(statChanceBest * 100).toFixed(2)}%`}
                        {config.forceMutation && <span className="text-amber-400"> (FORCED)</span>}
                      </div>
                    </div>
                    <div className="text-[10px] text-neutral-500 px-1 -mt-1">
                      Decays with the baby's inherited stat-mutation count ({minInheritedMuts}-{maxInheritedMuts} possible here)
                      {incenseBonus > 0 && <>; includes incense +{(incenseBonus * 100).toFixed(1)}%</>}.
                    </div>

                    <div className="bg-neutral-950/70 border border-neutral-800 p-2.5 rounded-xl flex items-center justify-between">
                      <div className="text-[11px] font-bold text-neutral-400">Potency Mutation Chance:</div>
                      <div className="text-xs font-black text-amber-400">
                        {(potChance * 100).toFixed(2)}% (separate roll, no incense)
                      </div>
                    </div>

                    <div className="bg-neutral-950/70 border border-neutral-800 p-2.5 rounded-xl flex items-center justify-between">
                      <div className="text-[11px] font-bold text-neutral-400">Any Mutation (new palette):</div>
                      <div className="text-xs font-black text-rose-400">
                        {anyMutationBest === anyMutationWorst
                          ? `${(anyMutationBest * 100).toFixed(2)}%`
                          : `${(anyMutationWorst * 100).toFixed(2)}% - ${(anyMutationBest * 100).toFixed(2)}%`}
                      </div>
                    </div>

                    <div className="bg-neutral-950/70 border border-neutral-800 p-2.5 rounded-xl flex items-center justify-between">
                      <div className="text-[11px] font-bold text-neutral-400">Alpha Stud Energy:</div>
                      <div className="text-xs font-black text-blue-400">
                        {alphaCharges} / {cadence.maleChargesPerRefill} Charges Remaining
                      </div>
                    </div>

                    <div className="bg-neutral-950/70 border border-neutral-800 p-2.5 rounded-xl flex items-center justify-between">
                      <div className="text-[11px] font-bold text-neutral-400">Species Roll:</div>
                      <div className="text-xs font-black text-violet-400">
                        50% {alphaSpecies} / 50% {betaSpecies}
                      </div>
                    </div>

                    <div className="bg-neutral-950/70 border border-neutral-800 p-2.5 rounded-xl space-y-1.5">
                      <div className="flex items-center justify-between">
                        <div className="text-[11px] font-bold text-neutral-400">Mating Guardian:</div>
                        <label className="flex items-center gap-1.5 text-[11px] font-bold text-neutral-300 cursor-pointer">
                          <input
                            type="checkbox"
                            checked={config.guardianEnabled}
                            onChange={(e) => setGuardianOverride(e.target.checked === serverBreedingConfig.guardianEnabled ? null : e.target.checked)}
                            className="accent-rose-500"
                          />
                          Enabled{guardianOverride !== null && <span className="text-amber-400"> (override)</span>}
                        </label>
                      </div>
                      <label className={`flex items-center gap-1.5 text-[11px] font-bold cursor-pointer ${config.guardianEnabled ? 'text-neutral-300' : 'text-neutral-600'}`}>
                        <input type="checkbox" checked={guardianKilled} disabled={!config.guardianEnabled} onChange={(e) => setGuardianKilled(e.target.checked)} className="accent-rose-500" />
                        Parents kill it (Awakened Blessing: +1 extra mutation, any line incl. potency)
                      </label>
                    </div>

                    <div className="bg-neutral-950/70 border border-neutral-800 p-2.5 rounded-xl text-[10px] text-neutral-400 leading-relaxed">
                      <div className="flex items-center justify-between mb-1">
                        <span className="font-bold text-neutral-300">Breeding Config</span>
                        <span className={`px-1.5 py-0.5 rounded border font-black ${configSource === 'server' ? 'bg-emerald-500/10 text-emerald-300 border-emerald-500/30' : 'bg-amber-500/10 text-amber-300 border-amber-500/30'}`}>
                          {configSource === 'server' ? 'LIVE SERVER' : 'FALLBACK DEFAULTS'}
                        </span>
                      </div>
                      base {config.baseMutationChance} / decay {config.mutationDecayRate} / floor {config.mutationMinFloor} / potency {config.potencyMutationChance}<br />
                      steps dmg {config.damageMutationStep}, dr {config.drMutationStep}, crit {config.critMutationStep}, vit {config.vitalityMutationStep} HP, pot {config.potencyMutationStep}<br />
                      potency soft cap {config.potencySoftCap || 'none'} (quarter step above), hard cap {potencyCapValue || 'none'}; per-line cap {config.maxStatMutations || 'uncapped'}
                    </div>
                  </div>
                </div>

                <div className="space-y-2 pt-3 border-t border-neutral-800/80">
                  <div className="flex gap-2">
                    <select
                      value={randomProfile}
                      onChange={(e) => setRandomProfile(e.target.value as RandomProfile)}
                      title={RANDOM_PROFILE_OPTIONS.find(p => p.key === randomProfile)?.hint}
                      className="bg-neutral-950 border border-neutral-800 rounded-xl px-2.5 py-2 text-xs font-bold text-neutral-300 focus:outline-none focus:border-amber-500 cursor-pointer"
                    >
                      {RANDOM_PROFILE_OPTIONS.map(p => <option key={p.key} value={p.key} title={p.hint}>{p.label}</option>)}
                    </select>
                    <button
                      onClick={randomizeBothParents}
                      title="Fill both parents with random plausible devices. Incense and catalyst are left as set."
                      className="flex-1 bg-neutral-950 border border-neutral-800 hover:border-amber-500/40 text-neutral-300 hover:text-amber-300 text-xs font-bold py-2 rounded-xl transition-all active:scale-95 cursor-pointer flex items-center justify-center gap-1.5"
                    >
                      <Dices className="w-3.5 h-3.5" /> Randomize Both Parents
                    </button>
                  </div>

                  <button
                    onClick={handleBreedSimulation}
                    disabled={alphaCharges <= 0}
                    className={`w-full font-extrabold text-sm py-3 px-4 rounded-xl transition-all shadow-lg flex items-center justify-center gap-2 cursor-pointer active:scale-95 ${
                      alphaCharges > 0
                        ? 'bg-gradient-to-r from-rose-500 via-pink-600 to-violet-600 hover:from-rose-600 hover:to-violet-700 text-white shadow-rose-500/20'
                        : 'bg-neutral-800 text-neutral-500 cursor-not-allowed border border-neutral-700'
                    }`}
                  >
                    <Heart className="w-4 h-4 fill-white" />
                    {alphaCharges > 0 ? 'Perform *dance* Breeding Ritual' : `Alpha Charges Exhausted (0/${cadence.maleChargesPerRefill})`}
                  </button>

                  <div className="flex gap-2">
                    <button
                      onClick={() => setAlphaCharges(cadence.maleChargesPerRefill)}
                      className="flex-1 bg-neutral-950 border border-neutral-800 hover:border-neutral-700 text-neutral-400 hover:text-neutral-200 text-xs font-bold py-2 rounded-xl transition-all active:scale-95 cursor-pointer flex items-center justify-center gap-1"
                    >
                      <RotateCcw className="w-3.5 h-3.5 text-blue-400" /> Reset Alpha Charges
                    </button>
                    <button
                      onClick={resetSimulation}
                      className="bg-neutral-950 border border-neutral-800 hover:border-neutral-700 text-neutral-400 hover:text-neutral-200 text-xs font-bold p-2 rounded-xl transition-all active:scale-95 cursor-pointer"
                      title="Reset Simulation History"
                    >
                      <RefreshCw className="w-3.5 h-3.5" />
                    </button>
                  </div>
                </div>
              </div>
            </div>

            {/* MIDDLE SECTION: OFFSPRING LINEAGE LOG */}
            <div className="bg-neutral-900/70 border border-neutral-800/90 rounded-2xl p-5 space-y-3 shadow-xl">
              <div className="flex justify-between items-center border-b border-neutral-800/80 pb-2">
                <span className="text-xs font-black uppercase tracking-wider text-neutral-300 flex items-center gap-2">
                  <Dna className="w-4 h-4 text-rose-400" /> Offspring Lineage Log ({simResults.length} Bred)
                </span>
                <span className="text-[10px] text-neutral-500 font-semibold">Click any row below to view full 3D Showroom model & stats</span>
              </div>

              {simResults.length === 0 ? (
                <div className="p-8 text-center border border-dashed border-neutral-800 rounded-xl space-y-1">
                  <Heart className="w-6 h-6 text-neutral-700 mx-auto" />
                  <div className="text-xs font-bold text-neutral-500 uppercase tracking-wider">No Offspring Bred Yet</div>
                  <p className="text-[11px] text-neutral-600">Configure Parent Alpha and Parent Beta above and click "Perform *dance* Breeding Ritual".</p>
                </div>
              ) : (
                <div className="max-h-56 overflow-y-auto space-y-2 pr-1 scrollbar-thin">
                  {simResults.map((res) => {
                    const isSelected = activeSelectedBaby?.id === res.id
                    return (
                      <div
                        key={res.id}
                        onClick={() => setSelectedBabyId(res.id)}
                        className={`p-3 rounded-xl border flex items-center justify-between cursor-pointer transition-all ${
                          isSelected
                            ? 'bg-rose-500/15 border-rose-500/60 ring-1 ring-rose-500/50 shadow-md'
                            : 'bg-neutral-950/60 border-neutral-800/80 hover:bg-neutral-900 hover:border-neutral-700'
                        }`}
                      >
                        <div className="flex items-center space-x-3">
                          <div className={`w-8 h-8 rounded-lg flex items-center justify-center text-xs font-black ${
                            res.hasMutation ? 'bg-rose-500 text-white shadow' : 'bg-neutral-900 text-neutral-400 border border-neutral-800'
                          }`}>
                            G{res.generation}
                          </div>

                          <div className="flex items-center space-x-2 flex-wrap gap-y-1">
                            <span className="text-xs font-black text-white">{res.species}</span>
                            <span className="text-[10px] bg-neutral-900 text-neutral-400 border border-neutral-800 px-2 py-0.5 rounded font-bold">
                              Lvl {res.level}
                            </span>
                            <span className="text-[10px] bg-neutral-900 text-neutral-400 border border-neutral-800 px-2 py-0.5 rounded font-bold">
                              {totalStatMutations(res.genetics)} stat / {res.genetics.pot} pot muts
                            </span>

                            {res.hasMutation && (
                              <span className="text-[10px] bg-rose-500/20 text-rose-300 border border-rose-500/40 px-2 py-0.5 rounded font-black flex items-center gap-1">
                                <Sparkles className="w-3 h-3 text-rose-400" /> Mutated ({res.mutationSummary.join(', ')})
                              </span>
                            )}
                          </div>
                        </div>

                        <div className="flex items-center space-x-4">
                          <div className="text-right">
                            <span className="text-xs font-black text-amber-400">{res.genetics.potencyStored} Potency</span>
                            <div className="text-[10px] text-neutral-500 font-mono">{res.timestamp}</div>
                          </div>
                          <ChevronRight className={`w-4 h-4 transition-transform ${isSelected ? 'text-rose-400 translate-x-1' : 'text-neutral-600'}`} />
                        </div>
                      </div>
                    )
                  })}
                </div>
              )}
            </div>

            {/* BOTTOM SECTION: 3D SHOWROOM & OFFSPRING INSPECTOR CARD */}
            {activeSelectedBaby && shownStats && (
              <div className="bg-gradient-to-tr from-neutral-900/95 via-neutral-900/80 to-neutral-950 border border-rose-500/30 rounded-2xl p-6 space-y-6 shadow-2xl relative overflow-hidden">

                {/* Header */}
                <div className="flex items-center justify-between border-b border-neutral-800 pb-4 flex-wrap gap-3">
                  <div className="flex items-center space-x-3">
                    <div className="p-2.5 bg-rose-500/20 text-rose-400 rounded-xl border border-rose-500/30">
                      <Award className="w-6 h-6" />
                    </div>
                    <div>
                      <h3 className="text-lg font-extrabold text-white flex items-center gap-2">
                        Generation {activeSelectedBaby.generation} Offspring ({activeSelectedBaby.species})
                        {activeSelectedBaby.hasMutation && (
                          <span className="text-xs bg-rose-500 text-white font-black px-2 py-0.5 rounded shadow">
                            GENETIC MUTATION
                          </span>
                        )}
                      </h3>
                      <p className="text-xs text-neutral-400 font-medium">
                        Bred at {activeSelectedBaby.timestamp} • Species donor: <strong className={activeSelectedBaby.donorParent === 'Alpha' ? 'text-blue-400' : 'text-violet-400'}>Parent {activeSelectedBaby.donorParent}</strong>
                        {activeSelectedBaby.mutationSummary.length > 0 && <> • {activeSelectedBaby.mutationSummary.join(', ')}</>}
                        {activeSelectedBaby.guardianNote && <> • {activeSelectedBaby.guardianNote}</>}
                      </p>
                    </div>
                  </div>

                  <div className="flex items-center gap-2 flex-wrap">
                    <button
                      onClick={() => promoteToAlpha(activeSelectedBaby)}
                      className="bg-blue-600/30 hover:bg-blue-600/50 text-blue-300 font-bold text-xs px-3 py-2 rounded-lg border border-blue-500/40 transition-all flex items-center gap-1.5 cursor-pointer"
                      title="Promote this offspring to become the new Parent Alpha (Stud) for next generation breeding"
                    >
                      <Award className="w-3.5 h-3.5 text-blue-400" /> Make Parent Alpha
                    </button>

                    <button
                      onClick={() => promoteToBeta(activeSelectedBaby)}
                      className="bg-violet-600/30 hover:bg-violet-600/50 text-violet-300 font-bold text-xs px-3 py-2 rounded-lg border border-violet-500/40 transition-all flex items-center gap-1.5 cursor-pointer"
                      title="Promote this offspring to become the new Parent Beta (Donor) for next generation breeding"
                    >
                      <Heart className="w-3.5 h-3.5 text-violet-400" /> Make Parent Beta
                    </button>

                    <button
                      onClick={handleCopyCmd}
                      className="bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs px-3 py-2 rounded-lg shadow border border-emerald-400/30 transition-all flex items-center gap-1.5 cursor-pointer"
                    >
                      <Copy className="w-3.5 h-3.5" />
                      {copiedCmd ? 'Copied @create!' : 'Copy @create Command'}
                    </button>

                    <button
                      onClick={handleCopySql}
                      className="bg-cyan-700 hover:bg-cyan-600 text-white font-bold text-xs px-3 py-2 rounded-lg shadow border border-cyan-400/30 transition-all flex items-center gap-1.5 cursor-pointer"
                      title="Copy SQL INSERT statement for weenie_properties_did Type 8 (PaletteBase)"
                    >
                      <Copy className="w-3.5 h-3.5" />
                      {copiedSql ? 'Copied SQL!' : 'Copy SQL (Type 8 Palette)'}
                    </button>

                  </div>
                </div>

                {/* 2-COLUMN LAYOUT: 3D SHOWROOM CANVAS (LEFT) + RATING CARDS (RIGHT) */}
                <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">

                  {/* 3D SHOWROOM CANVAS */}
                  <div className="lg:col-span-1 bg-neutral-950 border border-neutral-800 rounded-2xl p-4 space-y-3 relative overflow-hidden flex flex-col justify-between">
                    <div className="flex items-center justify-between border-b border-neutral-800 pb-2">
                      <span className="text-xs font-black uppercase tracking-wider text-rose-400 flex items-center gap-1.5">
                        <Sparkles className="w-3.5 h-3.5" /> 3D Creature Showroom
                      </span>
                      <span className="text-[10px] text-neutral-500 font-mono">Interactive Orbit</span>
                    </div>

                    {/* Three.js WorldViewer Viewport */}
                    <div className="w-full h-64 rounded-xl overflow-hidden border border-neutral-800 relative bg-neutral-900/50">
                      <WorldViewer
                        wcid={speciesList.find(s => s.name === activeSelectedBaby.species)?.wcid || 25749}
                        paletteOverride={isSkinCleansed ? 0 : (activeSelectedBaby.paletteId || activeSelectedBaby.paletteTemplateId || 0)}
                        hueShiftOverride={isSkinCleansed ? 0 : activeSelectedBaby.hueShift}
                        compactMode={true}
                      />
                    </div>

                    <div className="bg-neutral-900/80 border border-neutral-800 p-2.5 rounded-xl flex flex-col gap-1.5 text-xs">
                      <div className="flex items-center justify-between">
                        <span className="font-bold text-neutral-400">DAT Palette Variant:</span>
                        <span className="font-black text-rose-300 flex items-center gap-1.5">
                          <div className="w-3 h-3 rounded-full border border-white/20" style={{ backgroundColor: isSkinCleansed ? '#3B82F6' : activeSelectedBaby.paletteHex }} />
                          {isSkinCleansed ? 'Natural Base Essence' : activeSelectedBaby.paletteName}
                        </span>
                      </div>
                      {!isSkinCleansed && activeSelectedBaby.paletteSwatches && activeSelectedBaby.paletteSwatches.length > 0 && (
                        <div className="flex h-3.5 w-full rounded-lg overflow-hidden border border-neutral-800 mt-0.5">
                          {activeSelectedBaby.paletteSwatches.map((hex, i) => (
                            <div key={i} className="flex-1 h-full" style={{ backgroundColor: hex }} title={hex} />
                          ))}
                        </div>
                      )}
                    </div>
                  </div>

                  {/* STAT RATINGS BREAKDOWN */}
                  <div className="lg:col-span-2 space-y-3">
                    <div className="flex items-center justify-between">
                      <span className="text-[10px] font-black uppercase tracking-wider text-neutral-500">
                        Summoned Ratings ({showNewborn ? (stageMultipliers.length > 0 ? `Newborn, stage 1: x${maturityMultiplier(1, config)}` : 'Born adult: x1.0') : 'Adult: x1.0'})
                      </span>
                      <label className="flex items-center gap-1.5 text-[11px] font-bold text-neutral-300 cursor-pointer">
                        <input type="checkbox" checked={showNewborn} onChange={(e) => setShowNewborn(e.target.checked)} className="accent-rose-500" />
                        Show newborn values
                      </label>
                    </div>

                    <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                      <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                        <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Potency Stored</div>
                        <div className="text-lg font-black text-amber-400">{activeSelectedBaby.genetics.potencyStored}</div>
                        <div className="text-[10px] text-neutral-400">{activeSelectedBaby.genetics.pot} potency mutations</div>
                      </div>

                      <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                        <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Damage Rating</div>
                        <div className="text-lg font-black text-rose-400">+{shownStats.damageRating}</div>
                        <div className="text-[10px] text-neutral-400">gear {activeSelectedBaby.genetics.gearDamage} + {activeSelectedBaby.genetics.dmg} x {config.damageMutationStep}</div>
                      </div>

                      <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                        <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Damage Resist</div>
                        <div className="text-lg font-black text-emerald-400">+{shownStats.damageResistRating}</div>
                        <div className="text-[10px] text-neutral-400">gear {activeSelectedBaby.genetics.gearDamageResist} + {activeSelectedBaby.genetics.dr} x {config.drMutationStep}</div>
                      </div>

                      <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                        <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Crit Rating</div>
                        <div className="text-lg font-black text-violet-400">+{shownStats.critRating}</div>
                        <div className="text-[10px] text-neutral-400">gear {activeSelectedBaby.genetics.gearCrit} + {activeSelectedBaby.genetics.crit} x {config.critMutationStep}</div>
                      </div>

                      <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                        <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Crit Damage</div>
                        <div className="text-lg font-black text-rose-300">+{shownStats.critDamageRating}</div>
                        <div className="text-[10px] text-neutral-400">gear {activeSelectedBaby.genetics.gearCritDamage} + 0.8 x dmg bonus</div>
                      </div>

                      <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                        <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Crit Resist</div>
                        <div className="text-lg font-black text-emerald-300">+{shownStats.critResistRating}</div>
                        <div className="text-[10px] text-neutral-400">gear {activeSelectedBaby.genetics.gearCritResist} + 0.8 x DR bonus</div>
                      </div>

                      <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                        <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Crit Dmg Resist</div>
                        <div className="text-lg font-black text-emerald-200">+{shownStats.critDamageResistRating}</div>
                        <div className="text-[10px] text-neutral-400">gear {activeSelectedBaby.genetics.gearCritDamageResist} + 0.6 x DR bonus</div>
                      </div>

                      <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                        <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Bonus HP</div>
                        <div className="text-lg font-black text-cyan-400">+{shownStats.bonusHp} HP</div>
                        <div className="text-[10px] text-neutral-400">{activeSelectedBaby.genetics.vit} x {config.vitalityMutationStep} on top of species base</div>
                      </div>

                      <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                        <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Mutation Counts</div>
                        <div className="text-lg font-black text-rose-400">{totalMutations(activeSelectedBaby.genetics)} Total</div>
                        <div className="text-[10px] text-neutral-400">dmg {activeSelectedBaby.genetics.dmg} / dr {activeSelectedBaby.genetics.dr} / crit {activeSelectedBaby.genetics.crit} / vit {activeSelectedBaby.genetics.vit} / pot {activeSelectedBaby.genetics.pot}</div>
                      </div>

                      <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                        <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Next Stat Mutation</div>
                        <div className="text-lg font-black text-emerald-400">{(statMutationChance(totalStatMutations(activeSelectedBaby.genetics), config) * 100).toFixed(2)}%</div>
                        <div className="text-[10px] text-neutral-400">if bred as-is, no incense</div>
                      </div>

                      <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                        <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Female Recovery</div>
                        <div className="text-lg font-black text-emerald-400">{activeSelectedBaby.cooldownHours.toFixed(1)} Hours</div>
                        <div className="text-[10px] text-neutral-400">After each breed (dam)</div>
                      </div>

                      <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                        <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Growth</div>
                        <div className="text-lg font-black text-amber-300">{stageMultipliers.length} Stages</div>
                        <div className="text-[10px] text-neutral-400">{stageMultipliers.length > 0 ? `x${stageMultipliers.join(' / ')}, adult x1.0` : 'Born adult, x1.0'}</div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            )}
          </div>
        )}

        {/* TAB 2: TIME-TO-TARGET ESTIMATOR CALCULATOR */}
        {activeTab === 'calculator' && (
          <div className="bg-neutral-900/70 border border-neutral-800/90 rounded-2xl p-6 space-y-6 shadow-xl">
            <div className="border-b border-neutral-800 pb-4">
              <h2 className="text-xl font-extrabold text-white flex items-center gap-2">
                <BarChart3 className="w-5 h-5 text-rose-400" /> Lineage Odds & Time-to-Target Estimator
              </h2>
              <p className="text-xs text-neutral-400">
                {PROJECTION_TRIALS} Monte Carlo campaigns through the same breeding model as the simulator, starting from the current Parent Alpha and Parent Beta
                (incense / catalyst / guardian settings included). Each campaign keeps the best baby as the new Parent Alpha and breeds it against the fixed Parent Beta donor.
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
              <div>
                <label className="text-xs text-neutral-400 font-bold block mb-1">Target Stat Mutations (dmg+dr+crit+vit)</label>
                <input
                  type="number" min="1" max="200" value={calcTargetMuts}
                  onChange={(e) => setCalcTargetMuts(Math.max(1, Number(e.target.value)))}
                  className="w-full bg-neutral-950 border border-neutral-800 rounded-xl px-4 py-2.5 text-sm font-black text-rose-400"
                />
              </div>

              <div>
                <label className="text-xs text-neutral-400 font-bold block mb-1">Available Female Dams</label>
                <input
                  type="number" min="1" max="100" value={calcDonors}
                  onChange={(e) => setCalcDonors(Math.max(1, Number(e.target.value)))}
                  className="w-full bg-neutral-950 border border-neutral-800 rounded-xl px-4 py-2.5 text-sm font-black text-blue-400"
                />
              </div>

              <div>
                <label className="text-xs text-neutral-400 font-bold block mb-1">Breeder Activity Profile</label>
                <select
                  value={calcProfile}
                  onChange={(e) => setCalcProfile(e.target.value as ActivityProfile)}
                  className="w-full bg-neutral-950 border border-neutral-800 rounded-xl px-4 py-2.5 text-sm font-black text-violet-400"
                >
                  <option value="casual">Casual Breeder (1 Stud / 10 Breeds per Day)</option>
                  <option value="dedicated">Dedicated Breeder (2 Studs / 20 Breeds per Day)</option>
                  <option value="hardcore">Hardcore Breeder (4 Studs / 40 Breeds per Day)</option>
                </select>
              </div>

              <div>
                <label className="text-xs text-neutral-400 font-bold block mb-1">Seed (reproducible)</label>
                <div className="flex gap-2">
                  <input
                    type="number" min="1" value={calcSeed}
                    onChange={(e) => setCalcSeed(Math.max(1, Math.trunc(Number(e.target.value) || 1)))}
                    className="w-full bg-neutral-950 border border-neutral-800 rounded-xl px-4 py-2.5 text-sm font-black text-neutral-200"
                  />
                  <button
                    onClick={() => setCalcSeed(s => s + 1)}
                    className="bg-neutral-950 border border-neutral-800 hover:border-neutral-700 text-neutral-300 px-3 rounded-xl cursor-pointer"
                    title="Re-roll with the next seed"
                  >
                    <RefreshCw className="w-4 h-4" />
                  </button>
                </div>
              </div>
            </div>

            <div className="text-[11px] text-neutral-500">
              Throughput: {+breedsPerDay.toFixed(2)} breeds/day = min(studs {studsOwned} x {cadence.maleChargesPerRefill} charges {studsRefill ? `per ${cadence.maleRefillHours}h` : '(they never refill, so these are all there are)'}, dams {calcDonors} x {Number.isFinite(femaleRate) ? `${+femaleRate.toFixed(2)} per day at a ${cadence.femaleRecoveryHours}h recovery` : 'unlimited (no recovery)'}).
              Campaigns are capped at {PROJECTION_MAX_BREEDS} breeds; {est.reachedPct.toFixed(0)}% of runs reached the target within that cap.
            </div>

            {/* ESTIMATION METRICS DISPLAY */}
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4 pt-4 border-t border-neutral-800">
              <div className="bg-neutral-950 border border-neutral-800 p-4 rounded-xl space-y-1">
                <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Median Time to Target</div>
                <div className="text-2xl font-black text-emerald-400">{reachable(est.breedsMedian) ? `${(est.breedsMedian / breedsPerDay).toFixed(1)} Days` : 'Never'}</div>
                <div className="text-[10px] text-neutral-400">{reachable(est.breedsMedian) ? `~${(est.breedsMedian / breedsPerDay / 7.0).toFixed(1)} weeks` : 'the studs run out of charges first'}; lucky (p20) {days(est.breedsFast)}, unlucky (p80) {days(est.breedsSlow)}</div>
              </div>

              <div className="bg-neutral-950 border border-neutral-800 p-4 rounded-xl space-y-1">
                <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Median Breeds to Target</div>
                <div className="text-2xl font-black text-rose-400">{est.breedsMedian} Breeds</div>
                <div className="text-[10px] text-neutral-400">p20 {est.breedsFast} / p80 {est.breedsSlow}</div>
              </div>

              <div className="bg-neutral-950 border border-neutral-800 p-4 rounded-xl space-y-1">
                <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">{PROJECTION_DAYS}-Day Stat Mutations</div>
                <div className="text-2xl font-black text-amber-400">{est.statMutsMedian} Muts</div>
                <div className="text-[10px] text-neutral-400">best pet after {est.ninetyDayBreeds} breeds; p20 {est.statMutsSlow} / p80 {est.statMutsFast}</div>
              </div>

              <div className="bg-neutral-950 border border-neutral-800 p-4 rounded-xl space-y-1">
                <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">{PROJECTION_DAYS}-Day Extras</div>
                <div className="text-2xl font-black text-violet-400">{est.paletteRollsMedian} Palettes</div>
                <div className="text-[10px] text-neutral-400">mutated breeds (new colour each); {est.potMutsMedian} potency muts on best pet{config.guardianEnabled ? `; ${est.blessingsMedian} blessings` : ''}</div>
              </div>
            </div>
          </div>
        )}

        {/* TAB 3: HOW IT WORKS STEP-BY-STEP GUIDE */}
        {activeTab === 'guide' && (
          <div className="bg-neutral-900/70 border border-neutral-800/90 rounded-2xl p-6 space-y-6 shadow-xl leading-relaxed text-sm text-neutral-300">
            <div className="border-b border-neutral-800 pb-4">
              <h2 className="text-xl font-extrabold text-white flex items-center gap-2">
                <HelpCircle className="w-5 h-5 text-rose-400" /> Complete Guide to Pet Breeding in Asheron's Call
              </h2>
              <p className="text-xs text-neutral-400">Everything you need to know about Male Studs, Female Dams, mutations, and breeding supplies.</p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="bg-neutral-950 border border-neutral-800 p-5 rounded-xl space-y-2">
                <h3 className="font-extrabold text-white text-base flex items-center gap-2">
                  <span className="w-6 h-6 rounded-full bg-rose-500 text-white text-xs flex items-center justify-center font-black">1</span>
                  Male Studs & Female Dams
                </h3>
                <p className="text-xs text-neutral-400">
                  Breeding requires one <strong>Male Stud</strong>, with {cadence.maleChargesPerRefill} breeding charges that refill {cadence.maleRefillHours} hours after the last refill, and one <strong>Female Dam</strong>, who enters a {cadence.femaleRecoveryHours}-hour recovery after each breed.
                </p>
              </div>

              <div className="bg-neutral-950 border border-neutral-800 p-5 rounded-xl space-y-2">
                <h3 className="font-extrabold text-white text-base flex items-center gap-2">
                  <span className="w-6 h-6 rounded-full bg-rose-500 text-white text-xs flex items-center justify-center font-black">2</span>
                  The Seedy Motel Dance Ritual (*dance*)
                </h3>
                <p className="text-xs text-neutral-400">
                  Two players stand in the <strong>Seedy Motel</strong> with their combat pets summoned in the same landcell, and both execute the <code>*dance*</code> emote (or type <code>@dance</code>) within {cadence.danceWindowSeconds} seconds of each other to initiate breeding. The species is a 50/50 coin flip between the parents.
                </p>
              </div>

              <div className="bg-neutral-950 border border-neutral-800 p-5 rounded-xl space-y-2">
                <h3 className="font-extrabold text-white text-base flex items-center gap-2">
                  <span className="w-6 h-6 rounded-full bg-rose-500 text-white text-xs flex items-center justify-center font-black">3</span>
                  Inheritance & Mutation Rolls
                </h3>
                <p className="text-xs text-neutral-400">
                  Each stat line is inherited independently: {pctText(config.higherParentChance)} chance to take the higher parent's line (ties favour Alpha), {pctText(1 - config.higherParentChance)} the lower. Damage, Damage Resist and Crit carry both the gear base and the mutation count; Vitality carries its count; Potency carries the stored value and its count.
                  Then two independent rolls: a <strong>stat mutation</strong> (base {config.baseMutationChance * 100}%, decaying with the baby's inherited stat-mutation count, floor {config.mutationMinFloor * 100}%) adds +1 to a random eligible line (+{config.damageMutationStep} damage, +{config.drMutationStep} DR, +{config.critMutationStep} crit or +{config.vitalityMutationStep} HP), and a <strong>potency mutation</strong> ({config.potencyMutationChance * 100}%) adds +{config.potencyMutationStep} stored potency (quarter step above the {config.potencySoftCap || 'n/a'} soft cap{potencyCapValue ? `, never past ${potencyCapValue}` : ''}). Any mutation also rolls a brand-new colour palette.
                </p>
              </div>

              <div className="bg-neutral-950 border border-neutral-800 p-5 rounded-xl space-y-2">
                <h3 className="font-extrabold text-white text-base flex items-center gap-2">
                  <span className="w-6 h-6 rounded-full bg-rose-500 text-white text-xs flex items-center justify-center font-black">4</span>
                  Mutation Aids & Rare Color Palettes
                </h3>
                <p className="text-xs text-neutral-400">
                  <strong>Courtship Incense</strong> adds {cadence.incense.map(i => `+${pctText(i.bonus)}`).join(', ')} to the base before the decay, so it shrinks with the line like the base does (both parents' incense stacks, up to +{pctText(config.incenseBonusMax)}); it does not affect the potency roll. A <strong>Chromatic Catalyst</strong> on either parent makes a rolled mutation palette come from the vibrant pool, and is only consumed when a palette is actually rolled.
                </p>
              </div>

              <div className="bg-neutral-950 border border-neutral-800 p-5 rounded-xl space-y-2">
                <h3 className="font-extrabold text-white text-base flex items-center gap-2">
                  <span className="w-6 h-6 rounded-full bg-rose-500 text-white text-xs flex items-center justify-center font-black">5</span>
                  Mating Guardian & Awakened Blessing
                </h3>
                <p className="text-xs text-neutral-400">
                  When the guardian is enabled on the server ({config.guardianEnabled ? 'currently ON' : 'currently OFF'}; off by default) and the breed mutates, a mating guardian spawns. If the parent pets kill it, the newborn gains an <strong>Awakened Blessing</strong>: one extra mutation on a random eligible line, potency included.
                </p>
              </div>

              <div className="bg-neutral-950 border border-neutral-800 p-5 rounded-xl space-y-2">
                <h3 className="font-extrabold text-white text-base flex items-center gap-2">
                  <span className="w-6 h-6 rounded-full bg-rose-500 text-white text-xs flex items-center justify-center font-black">6</span>
                  Growth, Cosmetic Tailoring & Neutering
                </h3>
                <p className="text-xs text-neutral-400">
                  {stageMultipliers.length > 0
                    ? `Newborns summon at x${maturityMultiplier(1, config)} of their ratings and HP and grow through ${stageMultipliers.length} stages (${stageMultipliers.join(', ')}) to adult x1.0.`
                    : 'Babies are born adult (x1.0): growing up is turned off on this server.'} <strong>Pet Tailoring Kits</strong> extract and apply a pet's cosmetic appearance. <strong>Pet Neutering Kits</strong> permanently prevent a pet from breeding.
                </p>
              </div>
            </div>
          </div>
        )}

        {/* LIVE GENETIC & VISUALIZER DEBUG TERMINAL CONSOLE */}
        <div className="bg-neutral-950 border border-neutral-800 rounded-2xl overflow-hidden shadow-2xl mt-6">
          <div className="bg-neutral-900/90 border-b border-neutral-800 px-4 py-3 flex items-center justify-between flex-wrap gap-2">
            <div className="flex items-center gap-2">
              <Terminal className="w-4 h-4 text-emerald-400" />
              <span className="text-xs font-black text-white uppercase tracking-wider">Live Genetic & Visualizer Debug Console</span>
              <span className="text-[10px] bg-emerald-500/20 text-emerald-300 border border-emerald-500/40 px-2 py-0.5 rounded font-bold">
                {debugLogs.length} Log Entries
              </span>
            </div>

            <div className="flex items-center gap-2">
              <label
                className="flex items-center gap-1.5 text-xs font-bold text-neutral-300 bg-neutral-800/60 border border-neutral-700 px-3 py-1.5 rounded-lg cursor-pointer"
                title="Log every input, intermediate value and random draw, plus a [REPLAY] line that reproduces the breed exactly"
              >
                <input type="checkbox" checked={verboseLog} onChange={(e) => setVerboseLog(e.target.checked)} className="accent-emerald-500" />
                Verbose
              </label>

              <button
                onClick={runModelSelfCheck}
                className="bg-violet-600/30 hover:bg-violet-600/50 text-violet-300 font-bold text-xs px-3 py-1.5 rounded-lg border border-violet-500/40 transition-all flex items-center gap-1.5 cursor-pointer"
                title="Replay fixed rolls through the breeding model and compare against hand-computed results"
              >
                <Check className="w-3.5 h-3.5" /> Run Model Self-Check
              </button>

              <button
                onClick={async () => {
                  const text = debugLogs.join('\n')
                  const success = await copyToClipboard(text)
                  if (success) {
                    setCopiedDebug(true)
                    setTimeout(() => setCopiedDebug(false), 2000)
                  }
                }}
                className="bg-emerald-600/30 hover:bg-emerald-600/50 text-emerald-300 font-bold text-xs px-3 py-1.5 rounded-lg border border-emerald-500/40 transition-all flex items-center gap-1.5 cursor-pointer"
              >

                {copiedDebug ? <Check className="w-3.5 h-3.5" /> : <Copy className="w-3.5 h-3.5" />}
                {copiedDebug ? 'Copied Debug Log!' : 'Copy Full Debug Log'}
              </button>

              <button
                onClick={() => setIsConsoleOpen(!isConsoleOpen)}
                className="bg-neutral-800 hover:bg-neutral-700 text-neutral-300 text-xs px-3 py-1.5 rounded-lg border border-neutral-700 transition-all cursor-pointer"
              >
                {isConsoleOpen ? 'Hide Console' : 'Show Console'}
              </button>
            </div>
          </div>

          {isConsoleOpen && (
            <div className="p-4 bg-black/90 font-mono text-xs text-neutral-300 max-h-60 overflow-y-auto space-y-1 select-all">
              {debugLogs.length === 0 ? (
                <div className="text-neutral-500 italic">No logs yet. Perform a breeding ritual to generate real-time genetic & visualizer logs!</div>
              ) : (
                debugLogs.map((log, idx) => (
                  <div key={idx} className="leading-relaxed hover:bg-neutral-900/60 px-1 rounded">
                    {log}
                  </div>
                ))
              )}
            </div>
          )}
        </div>

      </div>
    </div>
  )
}
