import { ChevronLeft, ChevronRight } from 'lucide-react'

interface PaginationProps {
  currentPage: number
  totalPages: number
  onPageChange: (page: number) => void
}

export default function Pagination({ currentPage, totalPages, onPageChange }: PaginationProps) {
  if (totalPages <= 1) return null

  return (
    <div className="shrink-0 border-t border-neutral-800 bg-neutral-950/50 backdrop-blur-xl p-4 mt-auto">
      <div className="max-w-2xl mx-auto flex items-center justify-between">
        <div className="text-[10px] text-neutral-500 font-bold uppercase tracking-widest">
          Page {currentPage} of {totalPages}
        </div>
        <div className="flex items-center gap-2">
          <button 
            onClick={() => onPageChange(currentPage - 1)}
            disabled={currentPage === 1}
            className="p-1.5 rounded-lg bg-neutral-900 border border-neutral-800 text-neutral-400 hover:text-white disabled:opacity-30 disabled:pointer-events-none transition-all hover:bg-neutral-800"
          >
            <ChevronLeft className="w-4 h-4" />
          </button>
          <button 
            onClick={() => onPageChange(currentPage + 1)}
            disabled={currentPage === totalPages}
            className="p-1.5 rounded-lg bg-neutral-900 border border-neutral-800 text-neutral-400 hover:text-white disabled:opacity-30 disabled:pointer-events-none transition-all hover:bg-neutral-800"
          >
            <ChevronRight className="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  )
}
