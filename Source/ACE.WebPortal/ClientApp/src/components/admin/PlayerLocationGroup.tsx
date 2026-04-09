import { useState } from 'react'
import { ChevronDown } from 'lucide-react'
import { Character } from '../../types'
import CharacterListItem from '../common/CharacterListItem'
import LocationIcon from '../common/LocationIcon'

interface PlayerLocationGroupProps {
  title: string;
  players: Character[];
  adminCount: number;
  categoryOrdinal: number;
  variation: number | null;
  landblock: number;
  onSelect: (guid: number) => void;
}

/**
 * PlayerLocationGroup Component
 * ----------------------------
 * Handles the display and interaction for a specific grouped location in the Player List.
 * 
 * Responsibilities:
 * - Manages its own collapse state.
 * - Displays area metadata (Title, Variation, Hex, Player/Admin counts).
 * - Maps individual character rows within the group.
 */
export default function PlayerLocationGroup({
  title,
  players,
  adminCount,
  categoryOrdinal,
  variation,
  landblock,
  onSelect
}: PlayerLocationGroupProps) {
  const [isCollapsed, setIsCollapsed] = useState(true)

  // Formats the membership label: "1 Admin & 5 Players here"
  const label = (() => {
    const total = players.length
    const admins = adminCount
    const playerCount = total - admins
    const parts = []
    if (admins > 0) parts.push(`${admins} Admin${admins !== 1 ? 's' : ''}`)
    if (playerCount > 0) parts.push(`${playerCount} Player${playerCount !== 1 ? 's' : ''}`)
    return parts.join(' & ') + ' here'
  })()

  return (
    <div className="flex flex-col gap-1">
      <button 
        onClick={() => setIsCollapsed(!isCollapsed)}
        className={`w-full flex items-center justify-between px-4 py-3 rounded-xl border transition-all duration-300 text-left group/header hover:bg-neutral-900/50 hover:border-blue-500/40 shadow-sm ${
          !isCollapsed
            ? 'bg-neutral-800/40 border-neutral-700 mb-1' 
            : 'bg-neutral-950 border-neutral-800'
        }`}
      >
        <div className="flex items-center gap-3">
          <LocationIcon 
            categoryOrdinal={categoryOrdinal} 
            variation={variation} 
            className={
                categoryOrdinal === 1 ? "group-hover/header:bg-emerald-500 group-hover/header:text-white group-hover/header:border-emerald-400/20" :
                categoryOrdinal === 2 ? "group-hover/header:bg-amber-500 group-hover/header:text-white group-hover/header:border-amber-400/20" :
                variation !== null ? "group-hover/header:bg-violet-500 group-hover/header:text-white group-hover/header:border-violet-400/20" :
                "group-hover/header:bg-blue-500 group-hover/header:text-white group-hover/header:border-blue-400/20"
            }
          />
          <div className="flex flex-col">
            <span className="text-xs font-bold text-white group-hover/header:text-blue-400 transition-colors">
              {title}
            </span>
            <span className="text-[11px] font-bold text-neutral-500 tracking-tight lowercase">
              {label}
            </span>
          </div>
        </div>
        <div className="flex items-center gap-4">
          {categoryOrdinal === 3 && (
            <span className="text-[10px] font-bold text-neutral-600 transition-colors">
              0x{landblock.toString(16).toUpperCase().padStart(4, '0')}
            </span>
          )}
          <ChevronDown className={`w-4 h-4 text-neutral-600 transition-all duration-300 ${!isCollapsed ? 'rotate-180 text-blue-500' : 'group-hover/header:text-blue-500 group-hover/header:translate-y-0.5'}`} />
        </div>
      </button>
      
      {!isCollapsed && (
        <div className="flex flex-col gap-1.5 pl-4 border-l border-neutral-800/50 mb-4 animate-in fade-in slide-in-from-top-2 duration-300">
          {players.map((player) => (
            <CharacterListItem 
              key={player.guid} 
              character={player} 
              onClick={() => onSelect(player.guid)}
              locationInfo={categoryOrdinal === 3 ? undefined : player.location?.hex}
              secondaryInfo={
                categoryOrdinal === 3 
                  ? `0x${((player.location?.landblock ?? 0) >>> 16).toString(16).toUpperCase().padStart(4, '0')}` 
                  : (categoryOrdinal === 2 ? player.location?.coordinates : undefined)
              }
            />
          ))}
        </div>
      )}
    </div>
  )
}
