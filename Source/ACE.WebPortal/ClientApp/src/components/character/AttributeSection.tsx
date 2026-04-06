import { Activity, Flame, Shield, Zap, Target, User } from 'lucide-react'
import { StatsData } from '../../types'
import StatRow from './StatRow'

interface AttributeSectionProps {
  attributes: StatsData['attributes']
}

export default function AttributeSection({ attributes }: AttributeSectionProps) {
  const isOnline = attributes.strength.total !== null

  return (
    <div className="bg-neutral-950/50 border border-neutral-800 rounded-2xl p-6 shadow-xl relative overflow-hidden">
      <div className="flex items-center justify-between mb-6 px-1">
        <div className="flex items-center gap-2">
          <Activity className="w-4 h-4 text-orange-400" />
          <h3 className="text-xs font-bold uppercase tracking-[0.2em] text-neutral-400">Attributes</h3>
        </div>
        <div className="flex items-center gap-3 pr-4 font-black text-[9px] uppercase text-neutral-600">
          <div className="w-14 text-center">Innate</div>
          <div className="w-px h-3 opacity-0" />
          <div className="w-14 text-center">Base</div>
          {isOnline && (
            <>
              <div className="w-px h-3 opacity-0" />
              <div className="w-14 text-center text-blue-400/80">Current</div>
            </>
          )}
        </div>
      </div>
      <div className="space-y-4">
        <StatRow label="Strength" detail={attributes.strength} icon={<Flame className="w-3.5 h-3.5" />} />
        <StatRow label="Endurance" detail={attributes.endurance} icon={<Shield className="w-3.5 h-3.5" />} />
        <StatRow label="Coordination" detail={attributes.coordination} icon={<Activity className="w-3.5 h-3.5" />} />
        <StatRow label="Quickness" detail={attributes.quickness} icon={<Zap className="w-3.5 h-3.5" />} />
        <StatRow label="Focus" detail={attributes.focus} icon={<Target className="w-3.5 h-3.5" />} />
        <StatRow label="Self" detail={attributes.self} icon={<User className="w-3.5 h-3.5" />} />
      </div>
    </div>
  )
}
