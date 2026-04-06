import { Book, Link as LinkIcon } from 'lucide-react'
import { EnumListItem } from '../../types'

interface EnumIndexItemProps {
  item: EnumListItem
  isSelected: boolean
  onClick: () => void
}

export default function EnumIndexItem({ item, isSelected, onClick }: EnumIndexItemProps) {
  return (
    <button
      onClick={onClick}
      className={`w-full flex items-center justify-between p-2.5 rounded-xl transition-all group ${
        isSelected 
          ? 'bg-blue-600 text-white shadow-lg shadow-blue-600/20' 
          : 'text-neutral-500 hover:bg-white/[0.03] hover:text-neutral-300'
      }`}
    >
      <div className="flex items-center gap-2.5 min-w-0 pr-2">
        <Book className={`w-3.5 h-3.5 shrink-0 ${isSelected ? 'text-white' : 'text-neutral-700 group-hover:text-neutral-500 transition-colors'}`} />
        <span className="text-sm font-bold truncate group-hover:text-blue-400/50 transition-colors">{item.name}</span>
      </div>
      {item.isLinked && (
        <LinkIcon className={`shrink-0 w-3 h-3 ${isSelected ? 'text-white' : 'text-purple-500'}`} />
      )}
    </button>
  )
}
