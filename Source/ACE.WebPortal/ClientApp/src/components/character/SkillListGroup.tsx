import { SkillData } from '../../types'
import SkillListItem from './SkillListItem'

interface SkillListGroupProps {
  title: string
  skills: SkillData[]
  category: string
}

export default function SkillListGroup({ title, skills, category }: SkillListGroupProps) {
  const headerColors: Record<string, string> = {
    specialized: 'bg-purple-900/10 text-purple-400/80 border-purple-500/10',
    trained: 'bg-blue-900/10 text-blue-400/80 border-blue-500/10',
    untrained: 'bg-neutral-900/40 text-neutral-400/80 border-neutral-800/50',
    unusable: 'bg-red-900/10 text-red-400/60 border-red-500/10'
  }

  const isOnline = skills.some(s => s.total !== null)

  return (
    <div className="flex flex-col">
      <div className={`px-4 py-1.5 text-[9px] font-black uppercase tracking-[0.2em] border-y border-neutral-800/50 flex justify-between items-center ${headerColors[category] || headerColors.untrained}`}>
        <span>{title}</span>
        <div className="flex items-center gap-3 pr-2 font-bold opacity-40">
          <div className="w-14 text-center">Base</div>
          {isOnline && (
            <>
              <div className="w-px h-3 opacity-0" />
              <div className="w-14 text-center">Current</div>
            </>
          )}
        </div>
      </div>
      <div className="flex flex-col">
        {skills.map(skill => (
          <SkillListItem key={skill.name} skill={skill} category={category} />
        ))}
      </div>
    </div>
  )
}
