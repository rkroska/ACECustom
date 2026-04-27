import { StatDetail } from '../../types'
import { getIconUrl } from '../../utils/icon'

interface StatRowProps {
  label: string
  detail: StatDetail
  iconId: number
}

export default function StatRow({ label, detail, iconId }: StatRowProps) {
  const { innate, ranks, total } = detail
  const base = innate + ranks;
  const mainColor = "text-white"

  return (
    <div className="flex items-center justify-between group/row pr-1 py-1 text-sm border-b border-neutral-800/10 last:border-0 hover:bg-white/[0.02] rounded-lg transition-colors">
      <div className="flex items-center gap-5">
        <div className="flex items-center justify-center text-neutral-500 group-hover/row:text-neutral-300 transition-all shrink-0">
          <img 
            src={getIconUrl({ iconId })} 
            className="w-6 h-5 object-contain rounded-md shadow-sm grayscale-[0.3] group-hover/row:grayscale-0 transition-all duration-300" 
            alt="" 
          />
        </div>
        <span className="text-sm font-medium text-neutral-300 group-hover/row:text-white transition-colors">{label}</span>
      </div>
      <div className="flex items-center gap-3 pr-4">
        {/* Innate Column */}
        <div className="flex flex-col items-center min-w-14">
          <div className="text-sm font-mono font-medium text-neutral-600 tabular-nums">
            {innate?.toLocaleString()}
          </div>
        </div>

        {/* Divider */}
        <div className="w-px h-6 bg-neutral-800/20 self-center" />

        {/* Base Column */}
        <div className="flex flex-col items-center min-w-14">
          <div className="text-sm font-mono font-medium text-neutral-600 tabular-nums">
            {base?.toLocaleString()}
          </div>
        </div>

        {/* Divider */}
        <div className="w-px h-6 bg-neutral-800/30 self-center" />

        {/* Current Column */}
        <div className="flex flex-col items-center min-w-14">
          <div className={`font-mono font-bold text-base tracking-tight tabular-nums ${mainColor} brightness-110`}>
            {total?.toLocaleString()}
          </div>
        </div>
      </div>
    </div>
  )
}
