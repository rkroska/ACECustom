import { InventoryItem } from '../../types'
import { getIconUrl, getIconBgClass } from '../../utils/icon'

interface InventoryItemCardProps {
  item: InventoryItem
  isHeader?: boolean
}

export default function InventoryItemCard({ item, isHeader = false }: InventoryItemCardProps) {
  const GRID_COLS = "grid-cols-[1fr,90px,130px]"
  const paddingLeft = isHeader ? "pl-0" : "pl-[36px]"
  const textClass = isHeader 
    ? "text-xs font-bold text-neutral-200 group-hover:text-white" 
    : "text-xs font-medium text-neutral-400 group-hover:text-neutral-100"

  return (
    <div className={`grid ${GRID_COLS} gap-4 px-5 py-2.5 items-center group transition-colors hover:bg-white/[0.02]`}>
      <div className={`flex items-center gap-3 min-w-0 ${paddingLeft}`}>
        {item.iconId ? (
          <div className={`p-0.5 shrink-0 ${getIconBgClass(item)}`}>
            <img src={getIconUrl(item)} alt={item.name} className="w-7 h-7 object-contain" loading="lazy" />
          </div>
        ) : (
          <div className="w-7 h-7 shrink-0" />
        )}

        <span className={`${textClass} truncate leading-tight`} title={item.name}>
          {item.name}
          {item.stackSize > 1 && (
            <span className="ml-2 text-[10px] text-blue-500/80 font-black tracking-tight">×{item.stackSize}</span>
          )}
        </span>
      </div>

      <div className="flex justify-start items-center gap-1.5">
        <span className="text-[9px] font-black uppercase text-neutral-800 tracking-tighter w-8">WCID</span>
        <span className="text-[10px] font-mono font-bold text-neutral-600 group-hover:text-neutral-500 tabular-nums">{item.wcid}</span>
      </div>

      <div className="flex justify-start items-center gap-1.5 pl-2">
        <span className="text-[9px] font-black uppercase text-neutral-800 tracking-tighter w-4">ID</span>
        <span className="text-[10px] font-mono font-bold text-neutral-600 group-hover:text-neutral-500 tabular-nums">{item.guid}</span>
      </div>
    </div>
  )
}
