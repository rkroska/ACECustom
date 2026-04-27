import React from 'react'

interface TabButtonProps {
  label: string
  active: boolean
  onClick: () => void
  icon: React.ReactNode
  disabled?: boolean
}

export default function TabButton({ label, active, onClick, icon, disabled }: TabButtonProps) {
  return (
    <button 
      onClick={onClick}
      disabled={disabled}
      className={`flex items-center gap-2 px-6 py-3 rounded-t-lg text-xs font-bold uppercase tracking-widest transition-all duration-200 border-b-2 ${
        active 
          ? 'text-white border-blue-500 -mb-[1px] bg-white/5' 
          : 'text-neutral-500 border-transparent hover:text-neutral-300 hover:bg-white/[0.02]'
      } ${disabled ? 'opacity-40 cursor-not-allowed grayscale' : ''}`}
    >
      {React.isValidElement(icon) ? React.cloneElement(icon as any, { 
        className: `w-3.5 h-3.5 transition-opacity ${active ? 'opacity-100' : 'opacity-40'}` 
      }) : icon}
      {label}
    </button>
  )
}
