import { User, ChevronRight } from 'lucide-react'
import { Character } from '../../types'

interface CharacterListItemProps {
  character: Character
  onClick?: () => void
  locationInfo?: string | null
  secondaryInfo?: string | null
}

/**
 * Shared list item component used for both individual character lists and admin player views.
 * Follows the high-density aesthetic using tight padding and small typography.
 */
export default function CharacterListItem({ character, onClick, locationInfo, secondaryInfo }: CharacterListItemProps) {
  const displayName = character.isAdmin && !character.name.startsWith('+') 
    ? `+${character.name}` 
    : character.name

  return (
    <button
      onClick={onClick}
      className="group relative flex items-center justify-between p-2 bg-neutral-950 border border-neutral-800/80 rounded-lg hover:border-blue-500/40 hover:bg-neutral-900/50 transition-all duration-300 text-left overflow-hidden shadow-sm w-full"
    >
      <div className="flex items-center gap-3 relative z-10 transition-colors">
        <div className={`w-7 h-7 rounded flex items-center justify-center transition-all ${
          character.isOnline 
            ? 'bg-green-500/10 border border-green-500/20 text-green-500 group-hover:bg-green-500 group-hover:text-white' 
            : 'bg-neutral-900 border border-neutral-800 text-neutral-400 group-hover:text-blue-400 group-hover:border-blue-500/20'
        }`}>
          <User className="w-3.5 h-3.5" />
        </div>
        <h3 className={`text-[13px] font-bold transition-all ${
          character.isOnline 
            ? 'text-white' 
            : 'text-neutral-400 group-hover:text-white'
        }`}>
          {displayName}
        </h3>
      </div>

      <div className="flex items-center gap-4 relative z-10">
        <div className="flex flex-col items-end">
          {locationInfo && (
            <span className="text-[11px] font-bold text-neutral-500 group-hover:text-blue-400/80 transition-colors">
              {locationInfo}
            </span>
          )}
          {secondaryInfo && (
            <span className="text-[10px] font-bold text-neutral-600 group-hover:text-neutral-400 transition-colors">
              {secondaryInfo}
            </span>
          )}
        </div>
        <ChevronRight className="w-3.5 h-3.5 text-neutral-500 group-hover:text-blue-500 group-hover:translate-x-0.5 transition-all" />
      </div>
    </button>
  )
}
