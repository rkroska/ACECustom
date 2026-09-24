import { useEffect, useRef } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Activity, AlertTriangle, ArrowLeft, ArrowRight, PawPrint } from 'lucide-react'
import { cn } from '../../utils/cn'
import { usePetGuide } from './petGuideApi'
import StartHere, { GuideSectionId } from './StartHere'
import Capturing from './Capturing'
import Registry from './Registry'
import Summoning from './Summoning'
import BondPotency from './BondPotency'
import Breeding from './Breeding'

const SECTIONS: { id: GuideSectionId; label: string }[] = [
  { id: 'start', label: 'Start here' },
  { id: 'capture', label: 'Capturing' },
  { id: 'registry', label: 'Pet Log' },
  { id: 'summoning', label: 'Summoning' },
  { id: 'bond', label: 'Bond & Potency' },
  { id: 'breeding', label: 'Breeding' },
]

/** Public player guide to the pet system (#/pets/:section). All numbers come from /api/pet-guide. */
export default function PetGuide() {
  const { section } = useParams()
  const navigate = useNavigate()
  const { data, error } = usePetGuide()
  const scroller = useRef<HTMLDivElement>(null)

  const index = Math.max(0, SECTIONS.findIndex(s => s.id === section))
  const current = SECTIONS[index]
  const go = (id: GuideSectionId) => navigate(`/pets/${id}`)

  useEffect(() => { scroller.current?.scrollTo({ top: 0 }) }, [current.id])

  return (
    <div ref={scroller} className="absolute inset-0 bg-neutral-950 text-neutral-200 overflow-y-auto p-4 md:p-8 font-sans selection:bg-violet-500/30">
      <div className="absolute top-[-10%] left-[-10%] w-[50%] h-[50%] rounded-full bg-violet-500/10 blur-[120px] pointer-events-none" />
      <div className="absolute bottom-[-10%] right-[-10%] w-[50%] h-[50%] rounded-full bg-rose-500/10 blur-[120px] pointer-events-none" />

      <div className="max-w-6xl mx-auto space-y-6 relative z-10">
        <div className="flex items-center justify-between border-b border-neutral-800/80 pb-5 flex-wrap gap-4">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 rounded-xl bg-gradient-to-tr from-violet-600 to-rose-500 flex items-center justify-center shadow-lg shadow-violet-500/20">
              <PawPrint className="w-6 h-6 text-white" />
            </div>
            <div>
              <h1 className="text-2xl md:text-3xl font-extrabold tracking-tight text-white">Pet Guide</h1>
              <p className="text-xs md:text-sm text-neutral-400 font-medium">Capturing, summoning, bonding and breeding, from your first lens onward</p>
            </div>
          </div>

          <nav className="flex flex-wrap items-center bg-neutral-900 border border-neutral-800 rounded-xl p-1 gap-1">
            {SECTIONS.map(s => (
              <button
                key={s.id}
                onClick={() => go(s.id)}
                className={cn(
                  'px-3 py-1.5 rounded-lg text-xs font-black transition-all cursor-pointer',
                  s.id === current.id ? 'bg-violet-600 text-white shadow' : 'text-neutral-400 hover:text-neutral-200 hover:bg-neutral-800/50'
                )}
              >
                {s.label}
              </button>
            ))}
          </nav>
        </div>

        {error && (
          <div className="flex items-center gap-2 text-sm text-rose-300 bg-rose-500/10 border border-rose-500/30 rounded-xl px-4 py-3">
            <AlertTriangle className="w-4 h-4 shrink-0" />
            The guide could not load the server's pet settings ({error}). Try again in a moment.
          </div>
        )}

        {!data && !error && (
          <div className="flex flex-col items-center justify-center py-24 gap-4 text-neutral-500">
            <Activity className="w-8 h-8 animate-spin opacity-50" />
            <div className="text-[10px] font-black uppercase tracking-[0.2em]">Loading the guide</div>
          </div>
        )}

        {data && (
          <>
            {current.id === 'start' && <StartHere data={data} go={go} />}
            {current.id === 'capture' && <Capturing data={data} />}
            {current.id === 'registry' && <Registry data={data} />}
            {current.id === 'summoning' && <Summoning data={data} />}
            {current.id === 'bond' && <BondPotency data={data} />}
            {current.id === 'breeding' && <Breeding data={data} />}

            <div className="flex items-center justify-between pt-4 border-t border-neutral-800/80">
              {index > 0 ? (
                <button onClick={() => go(SECTIONS[index - 1].id)} className="flex items-center gap-1.5 text-xs font-black text-neutral-400 hover:text-white cursor-pointer">
                  <ArrowLeft className="w-4 h-4" /> {SECTIONS[index - 1].label}
                </button>
              ) : <span />}
              {index < SECTIONS.length - 1 && (
                <button onClick={() => go(SECTIONS[index + 1].id)} className="flex items-center gap-1.5 text-xs font-black text-violet-300 hover:text-white cursor-pointer">
                  Next: {SECTIONS[index + 1].label} <ArrowRight className="w-4 h-4" />
                </button>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  )
}
