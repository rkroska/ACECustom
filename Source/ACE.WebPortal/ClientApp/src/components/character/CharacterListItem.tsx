import { User, ChevronRight } from 'lucide-react'
import { Character } from '../../types'

interface CharacterListItemProps {
  character: Character
  onClick: () => void
}

export default function CharacterListItem({ character, onClick }: CharacterListItemProps) {
  const displayName = character.isAdmin && !character.name.startsWith('+') 
    ? `+${character.name}` 
    : character.name

  return (
    <button
      onClick={onClick}
      className="group relative flex items-center justify-between p-2.5 bg-neutral-950 border border-neutral-800/80 rounded-lg hover:border-blue-500/40 hover:bg-neutral-900/50 transition-all duration-300 text-left overflow-hidden shadow-sm w-full"
    >
      <div className="flex items-center gap-3 relative z-10 transition-colors">
        <div className={`w-8 h-8 rounded flex items-center justify-center transition-all ${
          character.isOnline 
            ? 'bg-green-500/10 border border-green-500/20 text-green-500 group-hover:bg-green-500 group-hover:text-white' 
            : 'bg-neutral-900 border border-neutral-800 text-neutral-400 group-hover:text-blue-400 group-hover:border-blue-500/20'
        }`}>
          <User className="w-4 h-4" />
        </div>
        <h3 className={`text-sm font-bold transition-all ${
          character.isOnline 
            ? 'text-white' 
            : 'text-neutral-400 group-hover:text-white'
        }`}>
          {displayName}
        </h3>
      </div>

      <ChevronRight className="w-3.5 h-3.5 text-neutral-800 group-hover:text-blue-500 group-hover:translate-x-0.5 transition-all relative z-10" />
    </button>
  )
}
