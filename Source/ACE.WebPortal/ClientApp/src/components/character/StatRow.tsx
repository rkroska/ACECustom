import React from 'react'
import { StatDetail } from '../../types'

interface StatRowProps {
  label: string
  detail: StatDetail
  icon: React.ReactNode
}

export default function StatRow({ label, detail, icon }: StatRowProps) {
  const { total, base, ranks } = detail
  const isOnline = total !== null
  const innate = base - ranks
  const mainColor = "text-white"

  return (
    <div className="flex items-center justify-between group/row py-1 text-sm border-b border-neutral-800/10 last:border-0 hover:bg-white/[0.02] px-1 rounded-lg transition-colors">
      <div className="flex items-center gap-3">
        <div className="w-8 h-8 rounded-lg bg-neutral-900 border border-neutral-800 flex items-center justify-center text-neutral-500 group-hover/row:border-neutral-700/50 group-hover/row:text-neutral-300 transition-all shrink-0">
          {icon}
        </div>
        <span className="text-sm font-medium text-neutral-300 group-hover/row:text-white transition-colors">{label}</span>
      </div>
      <div className="flex items-center gap-3 pr-4">
        {/* Innate Column */}
        <div className="flex flex-col items-center min-w-[3.5rem]">
          <div className="text-sm font-mono font-medium text-neutral-600 tabular-nums">
            {innate}
          </div>
        </div>

        {/* Divider */}
        <div className="w-px h-6 bg-neutral-800/20 self-center" />

        {/* Base Column */}
        <div className="flex flex-col items-center min-w-[3.5rem]">
          <div className={`text-sm font-mono font-medium ${isOnline ? 'text-neutral-500' : 'text-neutral-200'} tabular-nums`}>
            {base}
          </div>
        </div>

        {isOnline && (
          <>
            {/* Divider */}
            <div className="w-px h-6 bg-neutral-800/30 self-center" />

            {/* Current Column */}
            <div className="flex flex-col items-center min-w-[3.5rem]">
              <div className={`font-mono font-bold text-base tracking-tight tabular-nums ${mainColor} brightness-110`}>
                {total.toLocaleString()}
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  )
}
