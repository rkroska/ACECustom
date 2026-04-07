import { TrendingUp } from 'lucide-react'

interface ProgressionHeaderProps {
  level: number
  enlightenment: number
}

export default function ProgressionHeader({ level, enlightenment }: ProgressionHeaderProps) {
  return (
    <div className="bg-neutral-950/50 border border-neutral-800 rounded-2xl p-6 shadow-xl relative overflow-hidden">
      <div className="flex flex-col gap-5">
        <div className="flex items-center gap-2 px-1">
          <TrendingUp className="w-4 h-4 text-blue-400" />
          <h3 className="text-xs font-bold uppercase tracking-[0.2em] text-neutral-400">Progression</h3>
        </div>
        
        <div className="grid grid-cols-3 gap-4 text-[10px] font-bold uppercase tracking-[0.1em]">
          <div className="flex items-center justify-center gap-2 bg-red-600/10 py-2.5 rounded-xl border border-red-500/20 shadow-inner transition-all hover:bg-red-600/15">
            <span className="text-neutral-500">Prestige</span>
            <span className="text-red-400 font-mono text-sm tracking-tighter">-</span>
          </div>

          <div className="flex items-center justify-center gap-2 bg-blue-600/10 py-2.5 rounded-xl border border-blue-500/20 shadow-inner transition-all hover:bg-blue-600/15">
            <span className="text-neutral-500">Enlightenment</span>
            <span className="text-blue-400 font-mono text-sm tracking-tighter">{enlightenment}</span>
          </div>

          <div className="flex items-center justify-center gap-2 bg-neutral-800/40 py-2.5 rounded-xl border border-neutral-700/30 shadow-inner transition-all hover:bg-neutral-800/60">
            <span className="text-neutral-500">Level</span>
            <span className="text-white font-mono text-sm tracking-tighter">{level.toLocaleString()}</span>
          </div>
        </div>
      </div>
    </div>
  )
}
