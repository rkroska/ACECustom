import React from 'react'
import { Landmark, Coins, Sun, Key, Circle } from 'lucide-react'

interface BankSectionProps {
  bank: Record<string, number>
}

export default function BankSection({ bank }: BankSectionProps) {
  return (
    <div className="bg-neutral-950/50 border border-neutral-800 rounded-2xl p-5 shadow-xl relative overflow-hidden">
      <div className="absolute top-0 right-0 p-8 opacity-5">
        <Landmark className="w-24 h-24" />
      </div>
      <div className="flex items-center gap-2 mb-4 px-1">
        <Landmark className="w-4 h-4 text-emerald-400" />
        <h3 className="text-xs font-bold uppercase tracking-[0.2em] text-neutral-400">Bank</h3>
      </div>
      <div className="grid grid-cols-2 gap-x-6 gap-y-1.5">
        <CompactStat label="Pyreals" value={bank.Pyreals} icon={<Coins className="text-yellow-500" />} />
        <CompactStat label="Luminance" value={bank.Luminance} icon={<Sun className="text-orange-400" />} />
        <CompactStat label="Enl Coins" value={bank.EnlightenedCoins} icon={<Circle className="text-emerald-400" />} />
        <CompactStat label="W. Enl Coins" value={bank.WeaklyEnlightenedCoins} icon={<Circle className="text-blue-500/80" />} />
        <CompactStat label="Legendary Keys" value={bank.LegendaryKeys} icon={<Key className="text-purple-500" />} />
        <CompactStat label="Mythical Keys" value={bank.MythicalKeys} icon={<Key className="text-red-500" />} />
      </div>
    </div>
  )
}

function CompactStat({ label, value, icon }: { label: string, value: number, icon: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between py-1 group">
      <div className="flex items-center gap-2 text-xs font-medium text-neutral-500">
        <div className="w-5 h-5 flex items-center justify-center opacity-70 group-hover:opacity-100 transition-opacity">
          {React.isValidElement(icon) ? React.cloneElement(icon as any, { 
            className: `${(icon.props as any).className || ''} w-3.5 h-3.5` 
          }) : icon}
        </div>
        <span className="truncate group-hover:text-neutral-300 transition-colors uppercase tracking-tight">{label}</span>
      </div>
      <div className="text-neutral-200 font-mono font-bold text-xs tabular-nums group-hover:text-white">
        {(value || 0).toLocaleString()}
      </div>
    </div>
  )
}
