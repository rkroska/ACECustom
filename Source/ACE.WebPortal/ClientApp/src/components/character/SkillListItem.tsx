import { SkillData } from '../../types'

interface SkillListItemProps {
  skill: SkillData
  category: string
}

export default function SkillListItem({ skill, category }: SkillListItemProps) {
  const { total, base, isUsable } = skill
  const isOnline = total !== null
  
  let valueColor = "text-white" 
  if (isOnline) {
      if (!isUsable) valueColor = "text-neutral-500/80"
      else valueColor = "text-white/90"
  }

  const dotColors: Record<string, string> = {
    specialized: 'bg-purple-500',
    trained: 'bg-blue-500',
    untrained: 'bg-neutral-600',
    unusable: 'bg-red-600/40'
  }

  return (
    <div className="group flex items-center justify-between px-4 py-1.5 hover:bg-white/[0.02] transition-colors border-b border-neutral-800/10 last:border-0 relative">
      <div className="flex items-center gap-3">
        <div className={`w-1 h-1 rounded-full ${dotColors[category] || dotColors.untrained} shrink-0 shadow-sm transition-transform group-hover:scale-125`} />
        <span className={`text-xs font-medium tracking-tight ${category === 'unusable' ? 'text-neutral-500' : 'text-neutral-300 group-hover:text-white'} transition-colors`}>
          {skill.name}
        </span>
      </div>
      
      <div className="flex items-center gap-3 pr-2">
        <div className="flex flex-col items-center min-w-[3.5rem]">
          <div className={`text-[11px] font-mono font-medium ${isOnline ? 'text-neutral-500' : 'text-neutral-200'} tabular-nums`}>
            {base}
          </div>
        </div>

        {isOnline && (
          <>
            <div className="w-px h-6 bg-neutral-800/30 self-center" />
            <div className="flex flex-col items-center min-w-[3.5rem]">
              <div className={`font-mono font-bold text-xs tracking-tight tabular-nums ${valueColor} brightness-110`}>
                {total.toLocaleString()}
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  )
}
