import { LucideIcon } from 'lucide-react'
import { ReactNode } from 'react'

interface PageHeaderProps {
  title: string
  icon?: LucideIcon
  children?: ReactNode
  className?: string
}

export default function PageHeader({ title, icon: Icon, children, className = '' }: PageHeaderProps) {
  return (
    <div className={`flex items-center justify-between mb-6 animate-in fade-in slide-in-from-top-4 duration-700 ${className}`}>
      <div className="flex items-center gap-4">
        {Icon && (
          <div className="w-10 h-10 rounded-xl bg-blue-600/10 border border-blue-500/20 flex items-center justify-center text-blue-500 shadow-lg shadow-blue-500/5 group-hover:scale-110 transition-transform">
            <Icon className="w-5 h-5" />
          </div>
        )}
        <h1 className="text-2xl font-black text-white tracking-tight leading-none lowercase first-letter:uppercase">
          {title}
        </h1>
      </div>
      {children && (
        <div className="flex items-center gap-3">
          {children}
        </div>
      )}
    </div>
  )
}
