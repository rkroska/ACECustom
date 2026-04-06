import React from 'react'
import { Swords, Shield, TrendingUp, Crosshair } from 'lucide-react'
import { StatsData } from '../../types'

interface RatingSectionProps {
  ratings: StatsData['ratings']
}

export default function RatingSection({ ratings }: RatingSectionProps) {
  return (
    <div className="bg-neutral-950/50 border border-neutral-800 rounded-2xl p-5 shadow-xl relative overflow-hidden">
      <div className="flex items-center gap-2 mb-4 px-1">
        <Swords className="w-4 h-4 text-blue-400" />
        <h3 className="text-xs font-bold uppercase tracking-[0.2em] text-neutral-400">Combat Ratings</h3>
      </div>
      
      <div className="mb-4">
        <div className="flex items-center justify-between group">
          <div className="flex items-center gap-2">
            <Shield className="w-4 h-4 text-blue-400/50 group-hover:text-blue-400 transition-colors" />
            <span className="text-[10px] font-bold uppercase tracking-wider text-neutral-500 group-hover:text-neutral-300 transition-colors">Effective Melee Defense</span>
          </div>
          <div className="flex flex-col items-end">
            {ratings.emd !== null ? (
              <div className="text-sm font-mono font-bold text-neutral-300 group-hover:text-white transition-colors">
                {ratings.emd.toLocaleString()}
              </div>
            ) : (
              <div className="text-[9px] font-bold uppercase tracking-tighter text-blue-400/60 bg-blue-500/5 px-2 py-0.5 rounded border border-blue-500/10">
                Log in to see
              </div>
            )}
          </div>
        </div>
        <div className="h-px bg-neutral-800/50 mt-3" />
      </div>
      <div className="grid grid-cols-2 gap-x-6 gap-y-1.5">
        <RatingStat label="Damage (D)" value={ratings.damage} icon={<Swords />} />
        <RatingStat label="Crit Damage (CD)" value={ratings.critDamage} icon={<TrendingUp />} />
        <RatingStat label="Damage Resist (DR)" value={ratings.dr} icon={<Shield />} />
        <RatingStat label="Crit Resist (CDR)" value={ratings.cdr} icon={<Crosshair />} />
      </div>
    </div>
  )
}

function RatingStat({ label, value, icon }: { label: string, value: number, icon: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between py-1 group">
      <div className="flex items-center gap-2">
        <div className="text-neutral-500 group-hover:text-neutral-300 transition-colors">
          {React.isValidElement(icon) ? React.cloneElement(icon as any, { className: 'w-3.5 h-3.5' }) : icon}
        </div>
        <span className="text-[10px] font-bold uppercase tracking-widest text-neutral-500 group-hover:text-neutral-400 transition-colors">{label}</span>
      </div>
      <div className="text-sm font-mono font-bold text-neutral-300 group-hover:text-white transition-colors">
        {value}
      </div>
    </div>
  )
}
