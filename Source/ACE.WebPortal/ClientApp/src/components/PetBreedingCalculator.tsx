import { useState } from 'react'
import { Heart, Dna, Info, Sparkles, RefreshCw, ChevronRight, Award, Copy, BarChart3, HelpCircle, RotateCcw, Terminal, Check } from 'lucide-react'
import WorldViewer from './WorldViewer'
import { copyToClipboard } from '../utils/clipboard'

interface PetPalette {

  name: string
  hex: string
  templateId: number
  paletteId: number
  hueShift: number
  swatches: string[]
}

interface SimulationResult {
  id: string
  generation: number
  parentAlphaSpecies: string
  parentBetaSpecies: string
  inheritedParent: 'Alpha' | 'Beta'
  species: string
  level: number
  potency: number
  damageRating: number
  damageResistRating: number
  critRating: number
  critDamageRating: number
  critResistRating: number
  critDamageResistRating: number
  vitality: number
  isMutated: boolean
  mutatedStatName: string | null
  mutatedStatBoost: number
  isColorMutated: boolean
  mastery: string
  alphaMutations: number
  betaMutations: number
  totalMutations: number
  bondLevel: number
  cooldownHours: number
  palette: PetPalette
  paletteName: string
  paletteHex: string
  paletteTemplateId: number
  paletteId: number
  paletteSwatches?: string[]
  hueShift: number
  hasPaletteOverride: boolean
  timestamp: string
}

const COLOR_FAMILIES: { [key: string]: { name: string, hex: string, swatches: string[], templates: { [species: string]: { templateId: number, paletteId: number, hueShift?: number } } } } = {
  azure: {
    name: 'Azure Slate',
    hex: '#3B82F6',
    swatches: ['#3B82F6', '#2563EB', '#1D4ED8', '#1E40AF', '#1E3A8A', '#172554', '#0F172A', '#020617'],
    templates: {
      'Olthoi': { templateId: 2, paletteId: 0x040005F0, hueShift: 0 },
      'Gromnie': { templateId: 11, paletteId: 0x040001BB, hueShift: 0 },
      'Drudge': { templateId: 3, paletteId: 0x0400025F, hueShift: 0 },
      'Mattekar': { templateId: 1, paletteId: 0x040001A9, hueShift: 0 },
      'Lugian': { templateId: 2, paletteId: 0x040001D4, hueShift: 0 },
      'Banderling': { templateId: 2, paletteId: 0x040001EA, hueShift: 0 },
      'Tusker': { templateId: 2, paletteId: 0x040001F6, hueShift: 0 },
      'Viridian Statue': { templateId: 1, paletteId: 0x04000210, hueShift: 0 },
    }
  },
  amber: {
    name: 'Royal Amber',
    hex: '#F59E0B',
    swatches: ['#F59E0B', '#D97706', '#B45309', '#78350F', '#451A03', '#27272A', '#18181B', '#09090B'],
    templates: {
      'Olthoi': { templateId: 17, paletteId: 0x040005AD, hueShift: 0 },
      'Gromnie': { templateId: 15, paletteId: 0x040001BF, hueShift: 0 },
      'Drudge': { templateId: 2, paletteId: 0x0400025D, hueShift: 0 },
      'Mattekar': { templateId: 2, paletteId: 0x040001A8, hueShift: 0 },
      'Lugian': { templateId: 3, paletteId: 0x040001D2, hueShift: 0 },
      'Banderling': { templateId: 3, paletteId: 0x040001E8, hueShift: 0 },
      'Tusker': { templateId: 3, paletteId: 0x040001F4, hueShift: 0 },
      'Viridian Statue': { templateId: 1, paletteId: 0x04000210, hueShift: 0 },
    }
  },
  crimson: {
    name: 'Ebon Crimson',
    hex: '#EF4444',
    swatches: ['#EF4444', '#DC2626', '#B91C1C', '#991B1B', '#7F1D1D', '#450A0A', '#260404', '#000000'],
    templates: {
      'Olthoi': { templateId: 14, paletteId: 0x040005C3, hueShift: 0 },
      'Gromnie': { templateId: 12, paletteId: 0x040001BC, hueShift: 0 },
      'Drudge': { templateId: 4, paletteId: 0x04000261, hueShift: 0 },
      'Mattekar': { templateId: 3, paletteId: 0x040001AA, hueShift: 0 },
      'Lugian': { templateId: 4, paletteId: 0x040001D6, hueShift: 0 },
      'Banderling': { templateId: 4, paletteId: 0x040001EC, hueShift: 0 },
      'Tusker': { templateId: 4, paletteId: 0x040001F8, hueShift: 0 },
      'Viridian Statue': { templateId: 1, paletteId: 0x04000210, hueShift: 0 },
    }
  },
  emerald: {
    name: 'Emerald Olive',
    hex: '#10B981',
    swatches: ['#10B981', '#059669', '#047857', '#065F46', '#064E3B', '#022C22', '#061D15', '#000000'],
    templates: {
      'Olthoi': { templateId: 1, paletteId: 0x040005F3, hueShift: 0 },
      'Gromnie': { templateId: 14, paletteId: 0x040001BE, hueShift: 0 },
      'Drudge': { templateId: 1, paletteId: 0x0400025C, hueShift: 0 },
      'Mattekar': { templateId: 4, paletteId: 0x040001AB, hueShift: 0 },
      'Lugian': { templateId: 1, paletteId: 0x040001D3, hueShift: 0 },
      'Banderling': { templateId: 1, paletteId: 0x040001E9, hueShift: 0 },
      'Tusker': { templateId: 1, paletteId: 0x040001F5, hueShift: 0 },
      'Viridian Statue': { templateId: 1, paletteId: 0x04000210, hueShift: 0 },
    }
  },
  gold: {
    name: 'Sunburst Gold',
    hex: '#EAB308',
    swatches: ['#EAB308', '#CA8A04', '#A16207', '#854D0E', '#713F12', '#451A03', '#1C1917', '#000000'],
    templates: {
      'Olthoi': { templateId: 17, paletteId: 0x040005AD, hueShift: 0 },
      'Gromnie': { templateId: 10, paletteId: 0x040001BA, hueShift: 0 },
      'Drudge': { templateId: 5, paletteId: 0x04000262, hueShift: 0 },
      'Mattekar': { templateId: 5, paletteId: 0x040001AC, hueShift: 0 },
      'Lugian': { templateId: 5, paletteId: 0x040001D7, hueShift: 0 },
      'Banderling': { templateId: 5, paletteId: 0x040001ED, hueShift: 0 },
      'Tusker': { templateId: 5, paletteId: 0x040001F9, hueShift: 0 },
      'Viridian Statue': { templateId: 1, paletteId: 0x04000210, hueShift: 0 },
    }
  },
  cobalt: {
    name: 'Cobalt Indigo',
    hex: '#6366F1',
    swatches: ['#6366F1', '#4F46E5', '#4338CA', '#3730A3', '#312E81', '#1E1B4B', '#0F172A', '#000000'],
    templates: {
      'Olthoi': { templateId: 12, paletteId: 0x040005CB, hueShift: 0 },
      'Gromnie': { templateId: 6, paletteId: 0x040001B6, hueShift: 0 },
      'Drudge': { templateId: 3, paletteId: 0x0400025F, hueShift: 0 },
      'Mattekar': { templateId: 6, paletteId: 0x040001AD, hueShift: 0 },
      'Lugian': { templateId: 6, paletteId: 0x040001D8, hueShift: 0 },
      'Banderling': { templateId: 6, paletteId: 0x040001EE, hueShift: 0 },
      'Tusker': { templateId: 6, paletteId: 0x040001FA, hueShift: 0 },
      'Viridian Statue': { templateId: 1, paletteId: 0x04000210, hueShift: 0 },
    }
  },

  steel: {
    name: 'Steel Slate',
    hex: '#64748B',
    swatches: ['#64748B', '#475569', '#334155', '#1E293B', '#0F172A', '#020617', '#000000', '#000000'],
    templates: {
      'Olthoi': { templateId: 20, paletteId: 0x0F000014, hueShift: 0 },
      'Gromnie': { templateId: 4, paletteId: 0x040001B4, hueShift: 0 },
      'Drudge': { templateId: 2, paletteId: 0x0400025D, hueShift: 0 },
      'Mattekar': { templateId: 2, paletteId: 0x040001A8, hueShift: 0 },
      'Lugian': { templateId: 2, paletteId: 0x040001D4, hueShift: 0 },
      'Banderling': { templateId: 2, paletteId: 0x040001EA, hueShift: 0 },
      'Tusker': { templateId: 2, paletteId: 0x040001F6, hueShift: 0 },
      'Viridian Statue': { templateId: 1, paletteId: 0x04000210, hueShift: 0 },
    }
  }
}

function resolvePaletteForSpecies(parentPal: PetPalette, targetSpecies: string, inheritedParentName: string): PetPalette {
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
  const specMap = fam.templates[targetSpecies] || { templateId: parentPal.templateId || 1, paletteId: parentPal.paletteId || 1, hueShift: parentPal.hueShift || 0 }

  return {
    name: `${inheritedParentName}'s ${fam.name}`,
    hex: fam.hex,
    templateId: specMap.templateId,
    paletteId: specMap.paletteId,
    hueShift: 0,
    swatches: parentPal.swatches && parentPal.swatches.length > 0 ? parentPal.swatches : fam.swatches
  }
}

export default function PetBreedingCalculator() {
  // Navigation tab state: 'simulator' | 'calculator' | 'guide'
  const [activeTab, setActiveTab] = useState<'simulator' | 'calculator' | 'guide'>('simulator')

  // Species Options
  const speciesList = [
    { name: 'Olthoi', wcid: 25749 },
    { name: 'Gromnie', wcid: 17 },
    { name: 'Drudge', wcid: 35427 },
    { name: 'Tusker', wcid: 36967 },
    { name: 'Mattekar', wcid: 18 },
    { name: 'Lugian', wcid: 35134 },
    { name: 'Banderling', wcid: 35146 },
    { name: 'Viridian Statue', wcid: 420600 },
  ]

  // Parent Alpha (Stud) State
  const [alphaSpecies, setAlphaSpecies] = useState<string>('Olthoi')
  const [alphaLvl, setAlphaLvl] = useState<number>(300)
  const [alphaPot, setAlphaPot] = useState<number>(150)
  const [alphaDamageRating, setAlphaDamageRating] = useState<number>(12)
  const [alphaDamageResist, setAlphaDamageResist] = useState<number>(8)
  const [alphaCritRating, setAlphaCritRating] = useState<number>(5)
  const [alphaVitality, setAlphaVitality] = useState<number>(1200)
  const [alphaMutations, setAlphaMutations] = useState<number>(2)
  const [alphaCharges, setAlphaCharges] = useState<number>(10)
  const [alphaPalette, setAlphaPalette] = useState<PetPalette>({
    name: 'Alpha Lineage (Royal Amber)',
    hex: '#F59E0B',
    templateId: 4,
    paletteId: 0x0F000004,
    hueShift: 0,
    swatches: ['#F59E0B', '#D97706', '#B45309', '#78350F', '#451A03', '#27272A', '#18181B', '#09090B']
  })

  // Parent Beta (Donor) State
  const [betaSpecies, setBetaSpecies] = useState<string>('Gromnie')
  const [betaLvl, setBetaLvl] = useState<number>(300)
  const [betaPot, setBetaPot] = useState<number>(100)
  const [betaDamageRating, setBetaDamageRating] = useState<number>(6)
  const [betaDamageResist, setBetaDamageResist] = useState<number>(4)
  const [betaCritRating, setBetaCritRating] = useState<number>(2)
  const [betaVitality, setBetaVitality] = useState<number>(1000)
  const [betaMutations, setBetaMutations] = useState<number>(0)
  const [betaPalette, setBetaPalette] = useState<PetPalette>({
    name: 'Beta Lineage (Azure Slate)',
    hex: '#3B82F6',
    templateId: 1,
    paletteId: 0x040001BB,
    hueShift: 0,
    swatches: ['#3B82F6', '#2563EB', '#1D4ED8', '#1E40AF', '#1E3A8A', '#172554', '#0F172A', '#020617']
  })

  // Simulation state
  const [simResults, setSimResults] = useState<SimulationResult[]>([])
  const [selectedBabyId, setSelectedBabyId] = useState<string | null>(null)
  const [currentGen, setCurrentGen] = useState<number>(1)
  const [copiedCmd, setCopiedCmd] = useState<boolean>(false)

  // Live Debug Terminal State
  const [debugLogs, setDebugLogs] = useState<string[]>([
    `[${new Date().toLocaleTimeString()}] 🚀 Pet Breeding Visualizer Debug Console Ready`
  ])
  const [copiedDebug, setCopiedDebug] = useState<boolean>(false)
  const [isConsoleOpen, setIsConsoleOpen] = useState<boolean>(true)

  const addDebugLog = (msg: string) => {
    const time = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })
    const formatted = `[${time}] ${msg}`
    setDebugLogs(prev => [formatted, ...prev].slice(0, 100))
  }

  // Time-to-Target Estimator Inputs
  const [calcTargetMuts, setCalcTargetMuts] = useState<number>(10)
  const [calcDonors, setCalcDonors] = useState<number>(20)
  const [calcProfile, setCalcProfile] = useState<'casual' | 'dedicated' | 'hardcore'>('dedicated')

  const validLevels = [50, 80, 100, 125, 150, 180, 200, 250, 300]
  const masteries = ['Primalist', 'Necromancer', 'Naturalist']

  // Calculate diminishing mutation probability
  const getMutationChance = (mutations: number) => {
    return Math.max(0.015, 0.15 / (1.0 + 0.75 * mutations))
  }

  const alphaMutChance = getMutationChance(alphaMutations + betaMutations)

  // Simulate single breed
  const handleBreedSimulation = () => {
    addDebugLog(`─────────── [BREEDING RITUAL INITIATED] ───────────`)
    addDebugLog(`🧬 [PARENTS] Alpha (Stud): ${alphaSpecies} (Lvl ${alphaLvl}, ${alphaMutations} Muts, Palette: "${alphaPalette.name}") x Beta (Donor): ${betaSpecies} (Lvl ${betaLvl}, ${betaMutations} Muts, Palette: "${betaPalette.name}")`)

    if (alphaCharges <= 0) {
      addDebugLog(`⚠️ [ERROR] Alpha Stud is out of daily breeding charges (0/10)!`)
      alert("Alpha Stud is out of daily breeding charges (0/10)! Rest for 24h or click 'Reset Alpha Charges'.")
      return
    }

    const inheritFromAlpha = Math.random() < 0.50
    const inheritedParent: 'Alpha' | 'Beta' = inheritFromAlpha ? 'Alpha' : 'Beta'
    const babyLevel = inheritFromAlpha ? alphaLvl : betaLvl
    const babySpecies = Math.random() < 0.50 ? alphaSpecies : betaSpecies

    addDebugLog(`🎲 [SPECIES ROLL] 50/50 Roll Result -> Birthed Species: ${babySpecies} (Stat Lineage Winner: Parent ${inheritedParent})`)

    // Stat Inheritance (55/45 Rule)
    const inheritStat = (valAlpha: number, mutCountAlpha: number, valBeta: number, mutCountBeta: number) => {
      const isAlphaHigh = valAlpha >= valBeta
      const chosenIsAlpha = Math.random() < 0.55 ? isAlphaHigh : !isAlphaHigh
      const chosenVal = chosenIsAlpha ? valAlpha : valBeta
      const chosenMutCount = chosenIsAlpha ? mutCountAlpha : mutCountBeta
      return { val: chosenVal, mutCount: chosenMutCount }
    }

    const potRes = inheritStat(alphaPot, 0, betaPot, 0)
    const dmgRes = inheritStat(alphaDamageRating, Math.floor((alphaDamageRating - 10) / 3), betaDamageRating, Math.floor((betaDamageRating - 10) / 3))
    const drRes = inheritStat(alphaDamageResist, Math.floor((alphaDamageResist - 8) / 3), betaDamageResist, Math.floor((betaDamageResist - 8) / 3))
    const critRes = inheritStat(alphaCritRating, Math.floor((alphaCritRating - 5) / 2), betaCritRating, Math.floor((betaCritRating - 5) / 2))
    const vitRes = inheritStat(alphaVitality, Math.floor((alphaVitality - 1000) / 200), betaVitality, Math.floor((betaVitality - 1000) / 200))

    let babyPot = potRes.val
    let babyDmg = dmgRes.val
    let babyDR = drRes.val
    let babyCrit = critRes.val
    let babyVit = vitRes.val

    let babyDmgMuts = dmgRes.mutCount
    let babyDrMuts = drRes.mutCount
    let babyCritMuts = critRes.mutCount
    let babyVitMuts = vitRes.mutCount

    let babyCritDmg = Math.round(babyDmg * 0.8)
    let babyCritResist = Math.round(babyDR * 0.8)
    let babyCritDmgResist = Math.round(babyDR * 0.6)

    // Roll 1: Normal Stat Mutation (15% base decaying odds, capped at 20 muts per stat)
    const statRoll = Math.random()
    const isMutated = statRoll < alphaMutChance
    const isColorMutated = isMutated

    // Roll 2: Independent Potency Mutation Roll (2.0% Fixed Rare Chance, Uncapped)
    const potRoll = Math.random()
    const isPotencyMutated = potRoll < 0.02

    let mutatedStatName: string | null = null
    let mutatedStatBoost = 0

    if (isMutated && isPotencyMutated) {
      addDebugLog(`🌟 [DOUBLE MUTATION JACKPOT!] Birthed BOTH a Stat Mutation AND a Potency Mutation!`)
    }

    if (isPotencyMutated) {
      const potBoost = 20 // Fixed +20 Potency Step
      babyPot += potBoost
      addDebugLog(`🔮 [POTENCY MUTATION] Roll ${potRoll.toFixed(4)} < 0.0200 -> Rare Potency Mutation! Boosted Potency by +${potBoost} (Total Potency: ${babyPot})`)
    }

    if (isMutated) {
      const statsToMutate: string[] = []
      if (babyDmgMuts < 20) statsToMutate.push('DamageRating')
      if (babyDrMuts < 20) statsToMutate.push('DamageResistRating')
      if (babyCritMuts < 20) statsToMutate.push('CritRating')
      if (babyVitMuts < 20) statsToMutate.push('Vitality')

      if (statsToMutate.length > 0) {
        mutatedStatName = statsToMutate[Math.floor(Math.random() * statsToMutate.length)]

        if (mutatedStatName === 'DamageRating') {
          mutatedStatBoost = 3 // Fixed +3 Step
          babyDmg += mutatedStatBoost
          babyDmgMuts += 1
        } else if (mutatedStatName === 'DamageResistRating') {
          mutatedStatBoost = 3 // Fixed +3 Step
          babyDR += mutatedStatBoost
          babyDrMuts += 1
        } else if (mutatedStatName === 'CritRating') {
          mutatedStatBoost = 2 // Fixed +2 Step
          babyCrit += mutatedStatBoost
          babyCritMuts += 1
        } else if (mutatedStatName === 'Vitality') {
          mutatedStatBoost = 200 // Fixed +200 HP Step
          babyVit += mutatedStatBoost
          babyVitMuts += 1
        }
        addDebugLog(`🌟 [STAT MUTATION] Roll ${statRoll.toFixed(4)} < Chance ${alphaMutChance.toFixed(4)} -> MUTATED! Boosted ${mutatedStatName} by +${mutatedStatBoost} (Fixed Step)`)
      } else {
        addDebugLog(`🛑 [MUTATION CAP REACHED] All stat lines at 20/20 max mutations!`)
      }
    } else if (!isPotencyMutated) {
      addDebugLog(`📊 [STAT INHERITANCE] Stat Roll ${statRoll.toFixed(4)} >= Chance ${alphaMutChance.toFixed(4)} -> Normal Breed (No Stat Mutation)`)
    }

    // Mendelian Palette Inheritance vs Color Mutation
    let babyPalette: PetPalette

    if (!isColorMutated) {
      const parentPal = inheritFromAlpha ? alphaPalette : betaPalette
      babyPalette = resolvePaletteForSpecies(parentPal, babySpecies, inheritedParent)
      addDebugLog(`🎨 [PALETTE RESOLUTION] Inherited Parent ${inheritedParent}'s Color Family "${parentPal.name}"`)
      addDebugLog(`🖼️ [DAT MAPPING] Mapped to ${babySpecies} DAT Palette Entry #${babyPalette.templateId} (PaletteID: 0x${babyPalette.paletteId.toString(16).toUpperCase()}, HueShift: ${babyPalette.hueShift}°)`)
    } else {
      // COLOR MUTATION: Birthed a NEW RARE DAT MUTATED PALETTE FAMILY!
      const mutKeys = ['emerald', 'gold', 'cobalt', 'steel', 'crimson']
      const chosenKey = mutKeys[Math.floor(Math.random() * mutKeys.length)]
      const mutFam = COLOR_FAMILIES[chosenKey] || COLOR_FAMILIES['emerald']
      const specMap = mutFam.templates[babySpecies] || { templateId: 1, paletteId: 0x040001BE, hueShift: 0 }

      babyPalette = {
        name: `⭐ Rare Mutation (${mutFam.name})`,
        hex: mutFam.hex,
        templateId: specMap.templateId,
        paletteId: specMap.paletteId,
        hueShift: 0,
        swatches: mutFam.swatches
      }
      addDebugLog(`🌈 [COLOR MUTATION ACTIVATED] Stat Mutation Triggered -> Birthed NEW RARE DAT MUTATION PALETTE "${mutFam.name}"! (DAT PaletteID: 0x${specMap.paletteId.toString(16).toUpperCase()}, Template #${specMap.templateId})`)
    }

    const babyTotalMuts = babyDmgMuts + babyDrMuts + babyCritMuts + babyVitMuts
    const babyId = `baby_${Date.now()}_${Math.floor(Math.random() * 1000)}`

    const babyWcid = speciesList.find(s => s.name === babySpecies)?.wcid || 25749
    const palHex = babyPalette.paletteId ? `0x${babyPalette.paletteId.toString(16).toUpperCase()}` : '0'
    addDebugLog(`📋 [IN-GAME COMMAND] @create ${babyWcid} 1 ${palHex} 0.5`)

    const newResult: SimulationResult = {
      id: babyId,
      generation: currentGen,
      parentAlphaSpecies: alphaSpecies,
      parentBetaSpecies: betaSpecies,
      inheritedParent,
      species: babySpecies,
      level: babyLevel,
      potency: babyPot,
      damageRating: babyDmg,
      damageResistRating: babyDR,
      critRating: babyCrit,
      critDamageRating: babyCritDmg,
      critResistRating: babyCritResist,
      critDamageResistRating: babyCritDmgResist,
      vitality: babyVit,
      isMutated: isMutated || isPotencyMutated,
      mutatedStatName: mutatedStatName || (isPotencyMutated ? 'Potency' : null),
      mutatedStatBoost: mutatedStatBoost || (isPotencyMutated ? 20 : 0),
      isColorMutated,
      mastery: masteries[Math.floor(Math.random() * masteries.length)],
      alphaMutations: Math.floor(babyTotalMuts / 2),
      betaMutations: Math.ceil(babyTotalMuts / 2),
      totalMutations: babyTotalMuts,
      bondLevel: 1,
      cooldownHours: 4.0, // Non-Alpha gets 4-hour cooldown
      palette: babyPalette,
      paletteName: babyPalette.name,
      paletteHex: babyPalette.hex,
      paletteTemplateId: babyPalette.templateId,
      paletteId: babyPalette.paletteId,
      paletteSwatches: babyPalette.swatches,
      hueShift: babyPalette.hueShift,
      hasPaletteOverride: true,
      timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' }),
    }


    setSimResults([newResult, ...simResults])
    setSelectedBabyId(babyId)
    setCurrentGen(currentGen + 1)
    setAlphaCharges(alphaCharges - 1)
  }

  const resetSimulation = () => {
    addDebugLog(`🔄 [RESET] Simulation reset to Generation 1 (10 Alpha Stud Charges restored)`)
    setSimResults([])
    setSelectedBabyId(null)
    setCurrentGen(1)
    setAlphaCharges(10)
  }

  const promoteToAlpha = (baby: SimulationResult) => {
    setAlphaSpecies(baby.species)
    setAlphaLvl(baby.level)
    setAlphaPot(baby.potency)
    setAlphaDamageRating(baby.damageRating)
    setAlphaDamageResist(baby.damageResistRating)
    setAlphaCritRating(baby.critRating)
    setAlphaVitality(baby.vitality)
    setAlphaMutations(baby.totalMutations)
    if (baby.palette) {
      setAlphaPalette(baby.palette)
    }
    addDebugLog(`👑 [PROMOTION] Promoted Gen ${baby.generation} ${baby.species} to Parent Alpha (Stud)! Lineage Palette "${baby.paletteName}" set as Alpha Stud Palette.`)
    alert(`Gen ${baby.generation} ${baby.species} promoted to Parent Alpha (Stud)! Palette "${baby.paletteName}" carried into Alpha Lineage.`)
  }

  const promoteToBeta = (baby: SimulationResult) => {
    setBetaSpecies(baby.species)
    setBetaLvl(baby.level)
    setBetaPot(baby.potency)
    setBetaDamageRating(baby.damageRating)
    setBetaDamageResist(baby.damageResistRating)
    setBetaCritRating(baby.critRating)
    setBetaVitality(baby.vitality)
    setBetaMutations(baby.totalMutations)
    if (baby.palette) {
      setBetaPalette(baby.palette)
    }
    addDebugLog(`💖 [PROMOTION] Promoted Gen ${baby.generation} ${baby.species} to Parent Beta (Donor)! Lineage Palette "${baby.paletteName}" set as Beta Donor Palette.`)
    alert(`Gen ${baby.generation} ${baby.species} promoted to Parent Beta (Donor)! Palette "${baby.paletteName}" carried into Beta Lineage.`)
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
      const palVal = activeSelectedBaby.paletteId || activeSelectedBaby.paletteTemplateId || 0
      const palHex = `0x${palVal.toString(16).toUpperCase()}`
      cmd = `@create ${babyWcid} 1 ${palHex} 0.5`
    }
    const success = await copyToClipboard(cmd)
    if (success) {
      setCopiedCmd(true)
      setTimeout(() => setCopiedCmd(false), 2000)
    }
  }


  // Time-to-Target Calculator Estimations
  const getBreedsPerDay = () => {
    if (calcProfile === 'casual') return 10
    if (calcProfile === 'dedicated') return 20
    return 40
  }

  const breedsPerDay = getBreedsPerDay()
  
  // Calculate Monte Carlo estimation for target mutations
  const calculateEstimations = () => {
    let totalBreeds = 0
    let muts = 0
    while (muts < calcTargetMuts && totalBreeds < 10000) {
      totalBreeds++
      const p = getMutationChance(muts)
      if (Math.random() < p) {
        muts++
      }
    }
    const daysMedian = (totalBreeds / breedsPerDay).toFixed(1)
    const daysFast = ((totalBreeds * 0.6) / breedsPerDay).toFixed(1)
    const daysSlow = ((totalBreeds * 1.5) / breedsPerDay).toFixed(1)
    const echoYield = totalBreeds * 1.5
    const palettesUnlocked = Math.round(calcTargetMuts * 1.2)

    return { totalBreeds, daysMedian, daysFast, daysSlow, echoYield, palettesUnlocked }
  }

  const est = calculateEstimations()

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
                Alpha/Non-Alpha Pairing • Uncapped Stat Mutations • Live 3D Showroom
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
                1. Designate <strong>Alpha Stud</strong> (10 daily charges) • 2. Bring <strong>Non-Alpha Donor</strong> (4h cooldown) • 3. Stand within 5m in <strong>Seedy Motel</strong> & perform <code>/dance</code>!
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
                  <span className="text-[10px] bg-blue-500/20 text-blue-300 border border-blue-500/40 px-2 py-0.5 rounded font-black">
                    {alphaCharges}/10 Daily Charges
                  </span>
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

                  <div>
                    <label className="text-xs text-neutral-400 font-semibold block mb-1">Potency (Uncapped)</label>
                    <input 
                      type="number" min="0" value={alphaPot}
                      onChange={(e) => setAlphaPot(Math.max(0, Number(e.target.value)))}
                      className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs font-bold text-amber-400"
                    />
                  </div>

                  <div>
                    <label className="text-xs text-neutral-400 font-semibold block mb-1">Damage Rating</label>
                    <input 
                      type="number" min="0" value={alphaDamageRating}
                      onChange={(e) => setAlphaDamageRating(Math.max(0, Number(e.target.value)))}
                      className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs font-bold text-rose-400"
                    />
                  </div>

                  <div>
                    <label className="text-xs text-neutral-400 font-semibold block mb-1">Damage Resist</label>
                    <input 
                      type="number" min="0" value={alphaDamageResist}
                      onChange={(e) => setAlphaDamageResist(Math.max(0, Number(e.target.value)))}
                      className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs font-bold text-emerald-400"
                    />
                  </div>

                  <div>
                    <label className="text-xs text-neutral-400 font-semibold block mb-1">Crit Rating</label>
                    <input 
                      type="number" min="0" value={alphaCritRating}
                      onChange={(e) => setAlphaCritRating(Math.max(0, Number(e.target.value)))}
                      className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs font-bold text-violet-400"
                    />
                  </div>

                  <div>
                    <label className="text-xs text-neutral-400 font-semibold block mb-1">Vitality (HP)</label>
                    <input 
                      type="number" min="0" value={alphaVitality}
                      onChange={(e) => setAlphaVitality(Math.max(0, Number(e.target.value)))}
                      className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs font-bold text-cyan-400"
                    />
                  </div>

                  <div>
                    <label className="text-xs text-neutral-400 font-semibold block mb-1">Pedigree Mutations</label>
                    <input 
                      type="number" min="0" value={alphaMutations}
                      onChange={(e) => setAlphaMutations(Math.max(0, Number(e.target.value)))}
                      className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs font-bold text-rose-400"
                    />
                  </div>

                  {/* PARENT ALPHA PALETTE DISPLAY */}
                  <div className="bg-neutral-950/80 border border-neutral-800 p-2.5 rounded-xl flex flex-col gap-1.5 text-xs col-span-2 mt-1">
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
              </div>

              {/* PARENT BETA (COLUMN 2) */}
              <div className="bg-neutral-900/70 backdrop-blur-md rounded-2xl border border-violet-500/30 p-5 space-y-4 shadow-xl relative overflow-hidden">
                <div className="flex items-center justify-between border-b border-neutral-800/80 pb-3">
                  <span className="text-sm font-black uppercase tracking-wider text-violet-400 flex items-center gap-2">
                    <Dna className="w-4 h-4" /> Parent Beta (Donor)
                  </span>
                  <span className="text-[10px] bg-violet-500/20 text-violet-300 border border-violet-500/40 px-2 py-0.5 rounded font-black">
                    Non-Alpha (4h Cooldown)
                  </span>
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

                  <div>
                    <label className="text-xs text-neutral-400 font-semibold block mb-1">Potency (Uncapped)</label>
                    <input 
                      type="number" min="0" value={betaPot}
                      onChange={(e) => setBetaPot(Math.max(0, Number(e.target.value)))}
                      className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs font-bold text-amber-400"
                    />
                  </div>

                  <div>
                    <label className="text-xs text-neutral-400 font-semibold block mb-1">Damage Rating</label>
                    <input 
                      type="number" min="0" value={betaDamageRating}
                      onChange={(e) => setBetaDamageRating(Math.max(0, Number(e.target.value)))}
                      className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs font-bold text-rose-400"
                    />
                  </div>

                  <div>
                    <label className="text-xs text-neutral-400 font-semibold block mb-1">Damage Resist</label>
                    <input 
                      type="number" min="0" value={betaDamageResist}
                      onChange={(e) => setBetaDamageResist(Math.max(0, Number(e.target.value)))}
                      className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs font-bold text-emerald-400"
                    />
                  </div>

                  <div>
                    <label className="text-xs text-neutral-400 font-semibold block mb-1">Crit Rating</label>
                    <input 
                      type="number" min="0" value={betaCritRating}
                      onChange={(e) => setBetaCritRating(Math.max(0, Number(e.target.value)))}
                      className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs font-bold text-violet-400"
                    />
                  </div>

                  <div>
                    <label className="text-xs text-neutral-400 font-semibold block mb-1">Vitality (HP)</label>
                    <input 
                      type="number" min="0" value={betaVitality}
                      onChange={(e) => setBetaVitality(Math.max(0, Number(e.target.value)))}
                      className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs font-bold text-cyan-400"
                    />
                  </div>

                  <div>
                    <label className="text-xs text-neutral-400 font-semibold block mb-1">Pedigree Mutations</label>
                    <input 
                      type="number" min="0" value={betaMutations}
                      onChange={(e) => setBetaMutations(Math.max(0, Number(e.target.value)))}
                      className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-2.5 py-1.5 text-xs font-bold text-rose-400"
                    />
                  </div>

                  {/* PARENT BETA PALETTE DISPLAY */}
                  <div className="bg-neutral-950/80 border border-neutral-800 p-2.5 rounded-xl flex flex-col gap-1.5 text-xs col-span-2 mt-1">
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
                    <div className="bg-neutral-950/70 border border-neutral-800 p-2.5 rounded-xl flex items-center justify-between">
                      <div className="text-[11px] font-bold text-neutral-400">Mutation Probability:</div>
                      <div className="text-xs font-black text-emerald-400">
                        {(alphaMutChance * 100).toFixed(2)}% (+Stat & Rare Color)
                      </div>
                    </div>

                    <div className="bg-neutral-950/70 border border-neutral-800 p-2.5 rounded-xl flex items-center justify-between">
                      <div className="text-[11px] font-bold text-neutral-400">Alpha Stud Energy:</div>
                      <div className="text-xs font-black text-blue-400">
                        {alphaCharges} / 10 Charges Remaining
                      </div>
                    </div>

                    <div className="bg-neutral-950/70 border border-neutral-800 p-2.5 rounded-xl flex items-center justify-between">
                      <div className="text-[11px] font-bold text-neutral-400">Hybrid Species Roll:</div>
                      <div className="text-xs font-black text-violet-400">
                        50% {alphaSpecies} / 50% {betaSpecies}
                      </div>
                    </div>
                  </div>
                </div>

                <div className="space-y-2 pt-3 border-t border-neutral-800/80">
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
                    {alphaCharges > 0 ? 'Perform /dance Breeding Ritual' : 'Alpha Charges Exhausted (0/10)'}
                  </button>

                  <div className="flex gap-2">
                    <button 
                      onClick={() => setAlphaCharges(10)}
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
                  <p className="text-[11px] text-neutral-600">Configure Parent Alpha and Parent Beta above and click "Perform /dance Breeding Ritual".</p>
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
                            res.isMutated ? 'bg-rose-500 text-white shadow' : 'bg-neutral-900 text-neutral-400 border border-neutral-800'
                          }`}>
                            G{res.generation}
                          </div>

                          <div className="flex items-center space-x-2">
                            <span className="text-xs font-black text-white">{res.species}</span>
                            <span className="text-[10px] bg-neutral-900 text-neutral-400 border border-neutral-800 px-2 py-0.5 rounded font-bold">
                              Lvl {res.level}
                            </span>

                            {res.isMutated && (
                              <span className="text-[10px] bg-rose-500/20 text-rose-300 border border-rose-500/40 px-2 py-0.5 rounded font-black flex items-center gap-1">
                                <Sparkles className="w-3 h-3 text-rose-400" /> Mutated ({res.mutatedStatName} +{res.mutatedStatBoost})
                              </span>
                            )}
                          </div>
                        </div>

                        <div className="flex items-center space-x-4">
                          <div className="text-right">
                            <span className="text-xs font-black text-amber-400">{res.potency} Potency</span>
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
            {activeSelectedBaby && (
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
                        {activeSelectedBaby.isMutated && (
                          <span className="text-xs bg-rose-500 text-white font-black px-2 py-0.5 rounded shadow">
                            🌟 GENETIC MUTATION
                          </span>
                        )}
                      </h3>
                      <p className="text-xs text-neutral-400 font-medium">
                        Bred at {activeSelectedBaby.timestamp} • 50/50 Roll Winner: <strong className={activeSelectedBaby.inheritedParent === 'Alpha' ? 'text-blue-400' : 'text-violet-400'}>Parent {activeSelectedBaby.inheritedParent}</strong>
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
                      {copiedCmd ? '✓ Copied @create!' : 'Copy @create Command'}
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
                  <div className="lg:col-span-2 grid grid-cols-2 sm:grid-cols-4 gap-3">
                    <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                      <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Potency Stored</div>
                      <div className="text-lg font-black text-amber-400">{activeSelectedBaby.potency}</div>
                      <div className="text-[10px] text-neutral-400">+{(activeSelectedBaby.potency * 2)}% Damage</div>
                    </div>

                    <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                      <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Damage Rating</div>
                      <div className="text-lg font-black text-rose-400">+{activeSelectedBaby.damageRating}</div>
                      <div className="text-[10px] text-neutral-400">+{activeSelectedBaby.damageRating}% Outgoing</div>
                    </div>

                    <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                      <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Damage Resist</div>
                      <div className="text-lg font-black text-emerald-400">+{activeSelectedBaby.damageResistRating}</div>
                      <div className="text-[10px] text-neutral-400">-{activeSelectedBaby.damageResistRating}% Incoming</div>
                    </div>

                    <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                      <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Crit Rating</div>
                      <div className="text-lg font-black text-violet-400">+{activeSelectedBaby.critRating}</div>
                      <div className="text-[10px] text-neutral-400">+{(activeSelectedBaby.critRating)}% Crit Rate</div>
                    </div>

                    <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                      <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Vitality (HP)</div>
                      <div className="text-lg font-black text-cyan-400">+{activeSelectedBaby.vitality} HP</div>
                      <div className="text-[10px] text-neutral-400">Base Health Boost</div>
                    </div>

                    <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                      <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Lineage Mutations</div>
                      <div className="text-lg font-black text-rose-400">{activeSelectedBaby.totalMutations} Muts</div>
                      <div className="text-[10px] text-neutral-400">Alpha: {activeSelectedBaby.alphaMutations} | Beta: {activeSelectedBaby.betaMutations}</div>
                    </div>

                    <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                      <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Breeding Cooldown</div>
                      <div className="text-lg font-black text-emerald-400">4.0 Hours</div>
                      <div className="text-[10px] text-neutral-400">Non-Alpha Donor Timer</div>
                    </div>

                    <div className="bg-neutral-950/80 border border-neutral-800 p-3 rounded-xl space-y-1">
                      <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Salvage Value</div>
                      <div className="text-lg font-black text-amber-300">1-2 Echoes</div>
                      <div className="text-[10px] text-neutral-400">Essence Resonator</div>
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
                Statistical Monte Carlo projections based on donor pool size, breeder activity profile, and diminishing mutation curves.
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              <div>
                <label className="text-xs text-neutral-400 font-bold block mb-1">Target Mutations (1 - 20+)</label>
                <input 
                  type="number" min="1" max="50" value={calcTargetMuts}
                  onChange={(e) => setCalcTargetMuts(Math.max(1, Number(e.target.value)))}
                  className="w-full bg-neutral-950 border border-neutral-800 rounded-xl px-4 py-2.5 text-sm font-black text-rose-400"
                />
              </div>

              <div>
                <label className="text-xs text-neutral-400 font-bold block mb-1">Available Non-Alpha Donors</label>
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
                  onChange={(e) => setCalcProfile(e.target.value as any)}
                  className="w-full bg-neutral-950 border border-neutral-800 rounded-xl px-4 py-2.5 text-sm font-black text-violet-400"
                >
                  <option value="casual">Casual Breeder (1 Session / 10 Breeds per Day)</option>
                  <option value="dedicated">Dedicated Breeder (2 Sessions / 20 Breeds per Day)</option>
                  <option value="hardcore">Hardcore Breeder (4 Sessions / 40 Breeds per Day)</option>
                </select>
              </div>
            </div>

            {/* ESTIMATION METRICS DISPLAY */}
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4 pt-4 border-t border-neutral-800">
              <div className="bg-neutral-950 border border-neutral-800 p-4 rounded-xl space-y-1">
                <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Median Time to Target</div>
                <div className="text-2xl font-black text-emerald-400">{est.daysMedian} Days</div>
                <div className="text-[10px] text-neutral-400">~{(Number(est.daysMedian)/7.0).toFixed(1)} Weeks</div>
              </div>

              <div className="bg-neutral-950 border border-neutral-800 p-4 rounded-xl space-y-1">
                <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Estimated Total Breeds</div>
                <div className="text-2xl font-black text-rose-400">{est.totalBreeds} Breeds</div>
                <div className="text-[10px] text-neutral-400">Monte Carlo Sim Avg</div>
              </div>

              <div className="bg-neutral-950 border border-neutral-800 p-4 rounded-xl space-y-1">
                <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Savage Echoes Yielded</div>
                <div className="text-2xl font-black text-amber-400">~{est.echoYield} Echoes</div>
                <div className="text-[10px] text-neutral-400">Via Essence Resonator</div>
              </div>

              <div className="bg-neutral-950 border border-neutral-800 p-4 rounded-xl space-y-1">
                <div className="text-[10px] font-black text-neutral-500 uppercase tracking-wider">Palettes Unlocked</div>
                <div className="text-2xl font-black text-violet-400">~{est.palettesUnlocked} Colors</div>
                <div className="text-[10px] text-neutral-400">Cosmetic Wardrobe</div>
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
              <p className="text-xs text-neutral-400">Everything you need to know about Alpha Studs, Non-Alpha Donors, Mutations, and Savage Echoes.</p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="bg-neutral-950 border border-neutral-800 p-5 rounded-xl space-y-2">
                <h3 className="font-extrabold text-white text-base flex items-center gap-2">
                  <span className="w-6 h-6 rounded-full bg-rose-500 text-white text-xs flex items-center justify-center font-black">1</span>
                  Alpha Studs & Non-Alpha Donors
                </h3>
                <p className="text-xs text-neutral-400">
                  Breeding requires one <strong>Alpha Stud</strong> (promoted using an <em>Alpha Serum Catalyst</em>; has 10 daily charges) and one <strong>Non-Alpha Donor</strong> (incurs a 4-hour cooldown). An Alpha cannot breed with another Alpha!
                </p>
              </div>

              <div className="bg-neutral-950 border border-neutral-800 p-5 rounded-xl space-y-2">
                <h3 className="font-extrabold text-white text-base flex items-center gap-2">
                  <span className="w-6 h-6 rounded-full bg-rose-500 text-white text-xs flex items-center justify-center font-black">2</span>
                  The Seedy Motel `/dance` Ritual
                </h3>
                <p className="text-xs text-neutral-400">
                  Two players stand side-by-side in the <strong>Seedy Motel</strong> with active combat pets summoned (within 5m distance) and execute the <code>/dance</code> emote to initiate breeding.
                </p>
              </div>

              <div className="bg-neutral-950 border border-neutral-800 p-5 rounded-xl space-y-2">
                <h3 className="font-extrabold text-white text-base flex items-center gap-2">
                  <span className="w-6 h-6 rounded-full bg-rose-500 text-white text-xs flex items-center justify-center font-black">3</span>
                  Positive Mutations & Rare Color Palettes
                </h3>
                <p className="text-xs text-neutral-400">
                  Mutations are <strong>100% POSITIVE</strong>! Every mutation roll grants a stat boost (+2 to +5 Potency, +3 to +5 Damage Rating, +200 HP, etc.) and unlocks a rare visual essence palette (Gold, Silver, Ebon, Rose Red).
                </p>
              </div>

              <div className="bg-neutral-950 border border-neutral-800 p-5 rounded-xl space-y-2">
                <h3 className="font-extrabold text-white text-base flex items-center gap-2">
                  <span className="w-6 h-6 rounded-full bg-rose-500 text-white text-xs flex items-center justify-center font-black">4</span>
                  Salvaging & Palette Cleansing
                </h3>
                <p className="text-xs text-neutral-400">
                  Unwanted baby pets can be salvaged using an <strong>Essence Resonator</strong> into <strong>Savage Echoes</strong>. Use a <strong>Palette Cleansing Wash</strong> to strip visual overrides back to base appearance.
                </p>
              </div>
            </div>
          </div>
        )}

        {/* LIVE GENETIC & VISUALIZER DEBUG TERMINAL CONSOLE */}
        <div className="bg-neutral-950 border border-neutral-800 rounded-2xl overflow-hidden shadow-2xl mt-6">
          <div className="bg-neutral-900/90 border-b border-neutral-800 px-4 py-3 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Terminal className="w-4 h-4 text-emerald-400" />
              <span className="text-xs font-black text-white uppercase tracking-wider">Live Genetic & Visualizer Debug Console</span>
              <span className="text-[10px] bg-emerald-500/20 text-emerald-300 border border-emerald-500/40 px-2 py-0.5 rounded font-bold">
                {debugLogs.length} Log Entries
              </span>
            </div>

            <div className="flex items-center gap-2">
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
