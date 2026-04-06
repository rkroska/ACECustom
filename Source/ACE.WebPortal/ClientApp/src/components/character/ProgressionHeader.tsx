import { TrendingUp } from 'lucide-react'

interface ProgressionHeaderProps {
  level: number
  enlightenment: number
}

export default function ProgressionHeader({ level, enlightenment }: ProgressionHeaderProps) {
  return (
    <div className="bg-neutral-950/50 border border-neutral-800 rounded-2xl p-6 shadow-xl relative overflow-hidden">
      <div className="flex items-center justify-between px-1">
        <div className="flex items-center gap-2">
          <TrendingUp className="w-4 h-4 text-blue-400" />
          <h3 className="text-xs font-bold uppercase tracking-[0.2em] text-neutral-400">Progression</h3>
        </div>
        <div className="flex items-center gap-4 text-[10px] font-bold uppercase tracking-[0.1em]">
          <div className="flex items-center gap-1.5 bg-blue-600/10 px-3 py-1.5 rounded-full border border-blue-500/20">
            <span className="text-neutral-500">Enlightenment</span>
            <span className="text-blue-400 font-mono text-sm">{enlightenment}</span>
          </div>
          <div className="flex items-center gap-1.5 bg-neutral-800/40 px-3 py-1.5 rounded-full border border-neutral-700/30">
            <span className="text-neutral-500">Level</span>
            <span className="text-white font-mono text-sm">{level.toLocaleString()}</span>
          </div>
        </div>
      </div>
    </div>
  )
}
