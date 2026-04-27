import { Info } from 'lucide-react'

interface SearchHintProps {
  searchTerm: string
  className?: string
}

export default function SearchHint({ searchTerm, className = "" }: SearchHintProps) {
  const remaining = 3 - searchTerm.length
  
  if (remaining <= 0 || searchTerm.length === 0) return null

  return (
    <div className={`flex justify-center animate-in fade-in slide-in-from-top-2 duration-500 ${className}`}>
      <div className="flex items-center gap-2.5 text-blue-500/90 text-[10px] font-bold uppercase tracking-widest bg-blue-500/5 px-5 py-2.5 rounded-full border border-blue-500/20 shadow-[0_0_15px_-3px_rgba(59,130,246,0.1)] transition-all hover:bg-blue-500/10 hover:border-blue-500/30">
        <Info className="w-3.5 h-3.5" />
        Type {remaining} more character{remaining > 1 ? 's' : ''} to search global database
      </div>
    </div>
  )
}
