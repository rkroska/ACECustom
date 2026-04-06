import React from 'react'
import { TrendingUp, User, Box, Heart, Swords, Zap, Clock, Star, UserPlus, Target } from 'lucide-react'

interface AugmentationSectionProps {
  augmentations: Record<string, number>
}

export default function AugmentationSection({ augmentations }: AugmentationSectionProps) {
  return (
    <div className="bg-neutral-950/50 border border-neutral-800 rounded-2xl p-6 shadow-xl relative overflow-hidden">
      <div className="absolute top-0 right-0 p-8 opacity-5">
        <TrendingUp className="w-24 h-24 text-purple-500" />
      </div>
      <div className="flex items-center gap-2 mb-6 px-1">
        <TrendingUp className="w-4 h-4 text-purple-400" />
        <h3 className="text-xs font-bold uppercase tracking-[0.2em] text-neutral-400">Augmentations</h3>
      </div>
      <div className="grid grid-cols-2 gap-x-6 gap-y-2 relative z-10">
        {Object.entries(augmentations).map(([key, val]) => (
          <CompactStat key={key} label={key} value={val} icon={<AugIcon name={key} />} />
        ))}
        {Object.keys(augmentations).length === 0 && (
          <div className="col-span-2 py-4 text-center text-xs text-neutral-600 font-medium">
            No augmentations earned yet.
          </div>
        )}
      </div>
    </div>
  )
}

function CompactStat({ label, value, icon }: { label: string, value: number, icon: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between py-1 group">
      <div className="flex items-center gap-2 text-xs font-medium text-neutral-500">
        <div className="w-5 h-5 flex items-center justify-center opacity-70 group-hover:opacity-100 transition-opacity">
          {React.isValidElement(icon) ? React.cloneElement(icon as any, { className: 'w-3.5 h-3.5' }) : icon}
        </div>
        <span className="truncate group-hover:text-neutral-300 transition-colors uppercase tracking-tight">{label}</span>
      </div>
      <div className="text-neutral-200 font-mono font-bold text-xs tabular-nums group-hover:text-white">
        {value.toLocaleString()}
      </div>
    </div>
  )
}

function AugIcon({ name }: { name: string }) {
  const icons: Record<string, React.ReactElement> = {
    Creature: <User />,
    Item: <Box />,
    Life: <Heart />,
    War: <Swords />,
    Void: <Zap />,
    Duration: <Clock />,
    Specialize: <Star />,
    Summon: <UserPlus />,
    Melee: <Swords />,
    Missile: <Target />
  }
  return icons[name] || <Star />
}
