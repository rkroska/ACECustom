import { Heart } from 'lucide-react'
import { StatsData } from '../../types'
import StatRow from './StatRow'

const VITAL_ICONS = {
  health: 0x06001D79,
  stamina: 0x06001D7A,
  mana: 0x06001D7B,
}


interface VitalSectionProps {
  vitals: StatsData['vitals']
}

export default function VitalSection({ vitals }: VitalSectionProps) {
  return (
    <div className="bg-neutral-950/50 border border-neutral-800 rounded-2xl p-6 shadow-xl relative overflow-hidden">
      <div className="flex items-center justify-between mb-4 px-1">
        <div className="flex items-center gap-2">
          <Heart className="w-4 h-4 text-red-500" />
          <h3 className="text-xs font-bold uppercase tracking-[0.2em] text-neutral-400">Vitals</h3>
        </div>
        <div className="flex items-center gap-3 pr-4 font-black text-[9px] uppercase text-neutral-600">
          <div className="w-14 text-center">Innate</div>
          <div className="w-px h-3 opacity-0" />
          <div className="w-14 text-center">Base</div>
          <div className="w-px h-3 opacity-0" />
          <div className="w-14 text-center text-blue-400/80">Current</div>
        </div>
      </div>
      <div className="space-y-2">
        <StatRow label="Health" detail={vitals.health} iconId={VITAL_ICONS.health} />
        <StatRow label="Stamina" detail={vitals.stamina} iconId={VITAL_ICONS.stamina} />
        <StatRow label="Mana" detail={vitals.mana} iconId={VITAL_ICONS.mana} />
      </div>
    </div>
  )
}
