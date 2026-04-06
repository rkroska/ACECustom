import { Heart, Flame, Droplets } from 'lucide-react'
import { StatsData } from '../../types'
import StatRow from './StatRow'

interface VitalSectionProps {
  vitals: StatsData['vitals']
}

export default function VitalSection({ vitals }: VitalSectionProps) {
  const isOnline = vitals.health.total !== null

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
          {isOnline && (
            <>
              <div className="w-px h-3 opacity-0" />
              <div className="w-14 text-center text-red-400/80">Current</div>
            </>
          )}
        </div>
      </div>
      <div className="space-y-4">
        <StatRow label="Health" detail={vitals.health} icon={<Heart className="w-3.5 h-3.5" />} />
        <StatRow label="Stamina" detail={vitals.stamina} icon={<Flame className="w-3.5 h-3.5" />} />
        <StatRow label="Mana" detail={vitals.mana} icon={<Droplets className="w-3.5 h-3.5" />} />
      </div>
    </div>
  )
}
