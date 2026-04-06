import React from 'react'
import { ChevronDown, ChevronRight, Package } from 'lucide-react'
import { InventoryItem } from '../../types'
import InventoryItemCard from './InventoryItemCard'

interface InventorySectionProps {
  title: string
  icon: React.ReactNode
  items: InventoryItem[]
  isExpanded: boolean
  onToggle: () => void
  placeholder?: string
  headerItem?: InventoryItem
}

export default function InventorySection({ 
  title, 
  icon, 
  items, 
  isExpanded, 
  onToggle, 
  placeholder = "No items found",
  headerItem
}: InventorySectionProps) {
  const GRID_COLS = "grid-cols-[1fr,90px,130px]"

  return (
    <div className="bg-neutral-950/40 border border-neutral-800/60 rounded-xl overflow-hidden shadow-sm">
      <div 
        className="flex items-center cursor-pointer hover:bg-neutral-800/20 group transition-colors"
        onClick={onToggle}
      >
        {headerItem ? (
           <InventoryItemCard item={headerItem} isHeader={true} />
        ) : (
          <div className={`grid ${GRID_COLS} gap-4 w-full px-5 py-2.5 items-center`}>
            <div className="flex items-center gap-3 min-w-0">
              <div className="flex items-center gap-2">
                <div className="p-1 rounded-md bg-neutral-900 border border-neutral-800 text-neutral-400 group-hover:border-blue-500/20 transition-colors">
                  {icon}
                </div>
                {isExpanded ? <ChevronDown className="w-3.5 h-3.5 text-neutral-600" /> : <ChevronRight className="w-3.5 h-3.5 text-neutral-600" />}
              </div>
              <h3 className="text-xs font-black uppercase tracking-[0.15em] text-neutral-300 group-hover:text-white transition-colors">
                {title}
                <span className="ml-3 text-[9px] text-neutral-600 font-bold uppercase tracking-widest">{items.length} items</span>
              </h3>
            </div>
          </div>
        )}
      </div>
      
      {isExpanded && (
        <div className="border-t border-neutral-800/40 bg-neutral-950/20 divide-y divide-neutral-800/10">
          {items.map(item => (
            <InventoryItemCard key={item.guid} item={item} />
          ))}
          {items.length === 0 && (
            <div className="px-5 py-3 text-[10px] font-bold uppercase tracking-widest text-neutral-500 bg-neutral-950/10 flex items-center gap-3">
              <div className="pl-7 flex items-center gap-3">
                <Package className="w-3.5 h-3.5 opacity-40" />
                <span>{placeholder}</span>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
