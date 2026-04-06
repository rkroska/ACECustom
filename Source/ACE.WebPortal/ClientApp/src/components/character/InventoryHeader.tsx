import { Search } from 'lucide-react'

interface InventoryHeaderProps {
  searchTerm: string
  setSearchTerm: (term: string) => void
  totalCount: number
}

export default function InventoryHeader({ searchTerm, setSearchTerm, totalCount }: InventoryHeaderProps) {
  return (
    <div className="sticky top-0 z-50 bg-neutral-900 border-b border-neutral-800 -mx-12 px-12 shadow-2xl">
      <div className="flex items-center justify-between gap-4 max-w-4xl pb-4 mx-auto pt-4">
        <div className="relative flex-1 max-w-md">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-neutral-500" />
          <input 
            type="text"
            placeholder="Search name, WCID, or Biota ID..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full bg-neutral-950 border border-neutral-800 rounded-xl py-2.5 pl-10 pr-4 text-sm text-white placeholder-neutral-700 focus:outline-none focus:border-blue-500/50 transition-all font-medium"
          />
        </div>
        <div className="flex items-center gap-6 pr-2">
          <div className="flex flex-col items-end">
              <span className="text-[9px] font-black uppercase tracking-[0.2em] text-neutral-600">Total Items</span>
              <span className="text-lg font-black text-white leading-none tracking-tighter">{totalCount}</span>
          </div>
        </div>
      </div>
    </div>
  )
}
