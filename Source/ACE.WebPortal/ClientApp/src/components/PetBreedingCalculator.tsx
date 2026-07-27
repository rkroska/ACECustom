import { useState } from 'react'
import { Heart, Dna, Info, Sparkles, RefreshCw } from 'lucide-react'

interface SimulationResult {
  generation: number
  potency: number
  isMutated: boolean
  level: number
  mastery: string
}

export default function PetBreedingCalculator() {
  const [parent1Lvl, setParent1Lvl] = useState<number>(300)
  const [parent1Pot, setParent1Pot] = useState<number>(30)
  const [parent2Lvl, setParent2Lvl] = useState<number>(250)
  const [parent2Pot, setParent2Pot] = useState<number>(5)

  // Simulation state
  const [simResults, setSimResults] = useState<SimulationResult[]>([])
  const [currentGen, setCurrentGen] = useState<number>(1)

  const validLevels = [50, 80, 100, 125, 150, 180, 200, 250, 300]
  const masteries = ['Primalist', 'Necromancer', 'Naturalist']

  // Math functions
  const getFlooredPetLevel = (target: number) => {
    let result = validLevels[0]
    for (const lvl of validLevels) {
      if (lvl <= target) result = lvl
    }
    return result
  }

  // Snapped Level
  const avgLevel = (parent1Lvl + parent2Lvl) / 2
  const babyLevel = getFlooredPetLevel(avgLevel)

  // Potency Math
  const minPot = Math.floor((parent1Pot + parent2Pot) / 2)
  const maxPot = Math.max(parent1Pot, parent2Pot)
  const outcomesCount = maxPot - minPot + 1
  const rollMaxChance = 1 / outcomesCount
  const overallMutationChance = rollMaxChance * 0.2 // 20% mutation chance on rolling max

  // Simulate one breed
  const handleBreedSimulation = () => {
    // Potency Roll
    const rolledPot = Math.floor(Math.random() * outcomesCount) + minPot
    let finalPot = rolledPot
    let isMutated = false

    if (rolledPot === maxPot && Math.random() < 0.20) {
      finalPot = maxPot + 2
      isMutated = true
    }

    const rolledMastery = masteries[Math.floor(Math.random() * masteries.length)]

    const newResult: SimulationResult = {
      generation: currentGen,
      potency: finalPot,
      isMutated,
      level: babyLevel,
      mastery: rolledMastery,
    }

    setSimResults([newResult, ...simResults])
    setCurrentGen(currentGen + 1)
  }

  const resetSimulation = () => {
    setSimResults([])
    setCurrentGen(1)
  }

  return (
    <div className="absolute inset-0 bg-neutral-950 text-neutral-200 overflow-y-auto p-6 md:p-8 font-sans selection:bg-rose-500/30">
      
      {/* Decorative Gradients */}
      <div className="absolute top-[-10%] left-[-10%] w-[50%] h-[50%] rounded-full bg-blue-500/10 blur-[120px] pointer-events-none" />
      <div className="absolute bottom-[-10%] right-[-10%] w-[50%] h-[50%] rounded-full bg-rose-500/10 blur-[120px] pointer-events-none" />

      {/* Header */}
      <div className="max-w-6xl mx-auto space-y-8 relative z-10">
        <div className="flex items-center space-x-4">
          <div className="w-12 h-12 rounded-xl bg-gradient-to-tr from-rose-500 to-violet-600 flex items-center justify-center shadow-lg shadow-rose-500/20">
            <Heart className="w-6 h-6 text-white animate-pulse" />
          </div>
          <div>
            <h1 className="text-2xl md:text-3xl font-extrabold tracking-tight bg-clip-text text-transparent bg-gradient-to-r from-neutral-50 via-neutral-100 to-neutral-400">
              Gene Summons' Breeding Planner
            </h1>
            <p className="text-xs md:text-sm text-neutral-400 font-medium">
              Calculate genetic inheritance probabilities, level snaps, and pedigree mutation chances.
            </p>
          </div>
        </div>

        {/* Input Cards Grid */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          
          {/* Parent 1 Card */}
          <div className="bg-neutral-900/60 backdrop-blur-md rounded-2xl border border-neutral-800/80 p-6 space-y-6 shadow-xl relative overflow-hidden group hover:border-blue-500/30 transition-all duration-300">
            <div className="absolute top-0 right-0 w-32 h-32 bg-blue-500/5 rounded-full blur-2xl pointer-events-none" />
            <div className="flex items-center justify-between border-b border-neutral-800/60 pb-3">
              <span className="text-sm font-black uppercase tracking-wider text-blue-400 flex items-center gap-2">
                <Dna className="w-4 h-4" /> Parent Alpha
              </span>
              <span className="text-[10px] bg-blue-500/10 text-blue-400 px-2 py-0.5 rounded font-black uppercase">Attuned</span>
            </div>
            
            <div className="space-y-4">
              <div>
                <label className="text-xs text-neutral-400 font-semibold block mb-1">Base Level (Tier)</label>
                <select 
                  value={parent1Lvl}
                  onChange={(e) => setParent1Lvl(Number(e.target.value))}
                  className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-blue-500 transition-all"
                >
                  {validLevels.map(lvl => (
                    <option key={lvl} value={lvl}>Level {lvl}</option>
                  ))}
                </select>
              </div>

              <div>
                <label className="text-xs text-neutral-400 font-semibold block mb-1">Potency Stored (0 - 150)</label>
                <input 
                  type="number" 
                  min="0" 
                  max="150"
                  value={parent1Pot}
                  onChange={(e) => setParent1Pot(Math.max(0, Math.min(150, Number(e.target.value))))}
                  className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-blue-500 transition-all"
                />
              </div>
            </div>
          </div>

          {/* Parent 2 Card */}
          <div className="bg-neutral-900/60 backdrop-blur-md rounded-2xl border border-neutral-800/80 p-6 space-y-6 shadow-xl relative overflow-hidden group hover:border-violet-500/30 transition-all duration-300">
            <div className="absolute top-0 right-0 w-32 h-32 bg-violet-500/5 rounded-full blur-2xl pointer-events-none" />
            <div className="flex items-center justify-between border-b border-neutral-800/60 pb-3">
              <span className="text-sm font-black uppercase tracking-wider text-violet-400 flex items-center gap-2">
                <Dna className="w-4 h-4" /> Parent Beta
              </span>
              <span className="text-[10px] bg-violet-500/10 text-violet-400 px-2 py-0.5 rounded font-black uppercase">Attuned</span>
            </div>

            <div className="space-y-4">
              <div>
                <label className="text-xs text-neutral-400 font-semibold block mb-1">Base Level (Tier)</label>
                <select 
                  value={parent2Lvl}
                  onChange={(e) => setParent2Lvl(Number(e.target.value))}
                  className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-violet-500 transition-all"
                >
                  {validLevels.map(lvl => (
                    <option key={lvl} value={lvl}>Level {lvl}</option>
                  ))}
                </select>
              </div>

              <div>
                <label className="text-xs text-neutral-400 font-semibold block mb-1">Potency Stored (0 - 150)</label>
                <input 
                  type="number" 
                  min="0" 
                  max="150"
                  value={parent2Pot}
                  onChange={(e) => setParent2Pot(Math.max(0, Math.min(150, Number(e.target.value))))}
                  className="w-full bg-neutral-950 border border-neutral-800 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-violet-500 transition-all"
                />
              </div>
            </div>
          </div>

          {/* Expected Outcome Summary */}
          <div className="bg-gradient-to-tr from-neutral-900/80 to-neutral-900/40 backdrop-blur-md rounded-2xl border border-neutral-800 p-6 space-y-6 shadow-xl relative overflow-hidden">
            <div className="absolute top-0 right-0 w-32 h-32 bg-rose-500/5 rounded-full blur-2xl pointer-events-none" />
            <div className="flex items-center justify-between border-b border-neutral-800/60 pb-3">
              <span className="text-sm font-black uppercase tracking-wider text-rose-400 flex items-center gap-2">
                <Sparkles className="w-4 h-4" /> Probabilities
              </span>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="bg-neutral-950/60 border border-neutral-900 p-3 rounded-xl">
                <div className="text-[10px] font-bold text-neutral-500 uppercase tracking-wider">Baby Level</div>
                <div className="text-lg font-black text-rose-400 mt-1">Level {babyLevel}</div>
                <div className="text-[9px] text-neutral-500 mt-0.5">Average: {avgLevel} (Floored)</div>
              </div>

              <div className="bg-neutral-950/60 border border-neutral-900 p-3 rounded-xl">
                <div className="text-[10px] font-bold text-neutral-500 uppercase tracking-wider">Potency Range</div>
                <div className="text-lg font-black text-rose-400 mt-1">{minPot} - {maxPot}</div>
                <div className="text-[9px] text-neutral-500 mt-0.5">{outcomesCount} outcomes</div>
              </div>

              <div className="bg-neutral-950/60 border border-neutral-900 p-3 rounded-xl col-span-2">
                <div className="text-[10px] font-bold text-neutral-500 uppercase tracking-wider">Mutation Probability</div>
                <div className="text-lg font-black text-rose-400 mt-1">{(overallMutationChance * 100).toFixed(2)}%</div>
                <div className="text-[9px] text-neutral-500 mt-0.5">Requires rolling Max ({maxPot}) & hitting 20% Mutation.</div>
              </div>
            </div>
          </div>
        </div>

        {/* Simulator Sandbox */}
        <div className="grid grid-cols-1 lg:grid-cols-5 gap-6">
          
          {/* Controls & Metrics */}
          <div className="bg-neutral-900/60 border border-neutral-800 rounded-2xl p-6 lg:col-span-2 space-y-6 flex flex-col justify-between">
            <div className="space-y-4">
              <div className="flex items-center space-x-2 text-neutral-300">
                <Dna className="w-5 h-5 text-rose-500 animate-spin-slow" />
                <h2 className="font-extrabold text-lg">Simulation Sandbox</h2>
              </div>
              <p className="text-xs text-neutral-400 leading-relaxed">
                Click "Simulate Breed" below to roll a mock baby essence based on your inputs. If you roll the maximum value ({maxPot}), the system has a 20% chance to award a **+2 Mutation** and increase the lineage pedigree.
              </p>
            </div>

            <div className="space-y-3 pt-6 border-t border-neutral-800/60">
              <div className="flex items-center gap-2">
                <button
                  onClick={handleBreedSimulation}
                  className="flex-1 bg-gradient-to-r from-rose-500 to-violet-600 hover:from-rose-600 hover:to-violet-700 text-white font-extrabold text-sm py-3 px-4 rounded-xl transition-all shadow-lg shadow-rose-500/20 active:scale-95"
                >
                  Simulate Breed
                </button>
                <button 
                  onClick={resetSimulation}
                  className="bg-neutral-950 border border-neutral-800 hover:border-neutral-700 text-neutral-400 hover:text-neutral-200 p-3 rounded-xl transition-all active:scale-95"
                  title="Reset Simulation"
                >
                  <RefreshCw className="w-4 h-4" />
                </button>
              </div>
              <div className="text-center text-[10px] text-neutral-500">
                Current Lineage: Generation {currentGen}
              </div>
            </div>
          </div>

          {/* Results Queue */}
          <div className="bg-neutral-900/60 border border-neutral-800 rounded-2xl p-6 lg:col-span-3 flex flex-col">
            <div className="border-b border-neutral-800/60 pb-3 flex justify-between items-center mb-4">
              <span className="text-sm font-black uppercase tracking-wider text-neutral-400">Simulation History</span>
              <span className="text-[10px] bg-neutral-950 border border-neutral-800 px-2 py-0.5 rounded text-neutral-500 font-bold">
                {simResults.length} Bred
              </span>
            </div>

            <div className="flex-1 min-h-[220px] max-h-[300px] overflow-y-auto space-y-3 pr-2 scrollbar-thin">
              {simResults.length === 0 ? (
                <div className="h-full flex flex-col items-center justify-center text-center p-8 space-y-2 border border-dashed border-neutral-800 rounded-xl">
                  <Heart className="w-8 h-8 text-neutral-700" />
                  <div className="text-xs font-bold text-neutral-600 uppercase tracking-widest">No breeds simulated yet</div>
                  <p className="text-[10px] text-neutral-500 max-w-xs leading-normal">
                    Select your parent levels/potency and click "Simulate Breed" to see the outcomes!
                  </p>
                </div>
              ) : (
                simResults.map((res, index) => (
                  <div 
                    key={index} 
                    className={`p-3 rounded-xl border flex items-center justify-between transition-all duration-300 ${
                      res.isMutated 
                        ? 'bg-rose-500/10 border-rose-500/40 shadow-lg shadow-rose-500/5 animate-pulse-once' 
                        : 'bg-neutral-950/40 border-neutral-800/60'
                    }`}
                  >
                    <div className="flex items-center space-x-3">
                      <div className={`w-8 h-8 rounded-lg flex items-center justify-center text-xs font-black ${
                        res.isMutated ? 'bg-rose-500 text-white' : 'bg-neutral-900 text-neutral-400'
                      }`}>
                        G{res.generation}
                      </div>
                      <div>
                        <div className="text-xs font-black text-neutral-200">
                          {res.mastery} Essence
                        </div>
                        <div className="text-[10px] text-neutral-500">
                          Tier Index Level {res.level}
                        </div>
                      </div>
                    </div>

                    <div className="text-right">
                      <div className="flex items-center space-x-1 justify-end">
                        {res.isMutated && (
                          <Sparkles className="w-3.5 h-3.5 text-rose-400 fill-rose-400 animate-bounce" />
                        )}
                        <span className={`text-sm font-black ${res.isMutated ? 'text-rose-400' : 'text-neutral-300'}`}>
                          {res.potency} Potency
                        </span>
                      </div>
                      <div className="text-[9px] text-neutral-500">
                        {res.isMutated ? 'Mutated (+2)' : 'Normal Roll'}
                      </div>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>

        {/* Informational Footer */}
        <div className="bg-neutral-900/30 border border-neutral-800/50 rounded-2xl p-4 flex items-start space-x-3 text-neutral-400 text-xs leading-relaxed">
          <Info className="w-4 h-4 text-blue-400 shrink-0 mt-0.5" />
          <p>
            **Rules Reference:** Mating requires two players to stand side-by-side with active combat pets summoned in the Seedy Motel dungeon variation and perform the `/dance` emote. The baby starts at Bond Level 1 to prevent cloning loops. The baby's base tier is determined by flooring the average of both parents' levels down to the nearest valid tier: {validLevels.join(', ')}.
          </p>
        </div>
      </div>
    </div>
  )
}
