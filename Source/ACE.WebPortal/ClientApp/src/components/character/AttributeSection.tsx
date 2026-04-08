import { Activity } from 'lucide-react'
import { StatsData } from '../../types'
import StatRow from './StatRow'

const ATTRIBUTE_ICONS = {
  strength: 0x060002C8,
  endurance: 0x060002C4,
  coordination: 0x060002C9,
  quickness: 0x060002C6,
  focus: 0x060002C5,
  self: 0x060002C7,
}


interface AttributeSectionProps {
  attributes: StatsData['attributes']
}

export default function AttributeSection({ attributes }: AttributeSectionProps) {
  return (
    <div className="bg-neutral-950/50 border border-neutral-800 rounded-2xl p-6 shadow-xl relative overflow-hidden">
      <div className="flex items-center justify-between mb-4 px-1">
        <div className="flex items-center gap-2">
          <Activity className="w-4 h-4 text-orange-400" />
          <h3 className="text-xs font-bold uppercase tracking-[0.2em] text-neutral-400">Attributes</h3>
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
        <StatRow label="Strength" detail={attributes.strength} iconId={ATTRIBUTE_ICONS.strength} />
        <StatRow label="Endurance" detail={attributes.endurance} iconId={ATTRIBUTE_ICONS.endurance} />
        <StatRow label="Coordination" detail={attributes.coordination} iconId={ATTRIBUTE_ICONS.coordination} />
        <StatRow label="Quickness" detail={attributes.quickness} iconId={ATTRIBUTE_ICONS.quickness} />
        <StatRow label="Focus" detail={attributes.focus} iconId={ATTRIBUTE_ICONS.focus} />
        <StatRow label="Self" detail={attributes.self} iconId={ATTRIBUTE_ICONS.self} />
      </div>
    </div>
  )
}
