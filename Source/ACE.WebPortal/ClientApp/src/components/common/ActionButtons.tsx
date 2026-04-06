import React from 'react'
import { Terminal, Ghost, Check, Clipboard } from 'lucide-react'

interface ActionButtonProps {
  onClick: () => void
  isCopied: boolean
  title: string
  label?: string
  className?: string
}

export const GetButton: React.FC<ActionButtonProps> = ({ onClick, isCopied, title, label = 'GET' }) => (
  <button 
    onClick={onClick}
    title={title}
    className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[10px] font-bold uppercase transition-all ${
      isCopied 
      ? 'bg-green-500/20 text-green-500 border border-green-500/30' 
      : 'bg-neutral-900 border border-neutral-800 text-neutral-500 hover:text-white hover:border-neutral-700'
    }`}
  >
    {isCopied ? <Check className="w-3.5 h-3.5" /> : <Ghost className="w-3.5 h-3.5" />}
    {label}
  </button>
)

export const SetButton: React.FC<ActionButtonProps> = ({ onClick, isCopied, title, label = 'SET' }) => (
  <button 
    onClick={onClick}
    title={title}
    className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-[10px] font-bold uppercase transition-all ${
      isCopied 
      ? 'bg-green-500/20 text-green-500 border border-green-500/30' 
      : 'bg-neutral-950 border border-neutral-800 text-neutral-500 hover:text-white hover:border-neutral-700'
    }`}
  >
    {isCopied ? <Check className="w-3.5 h-3.5" /> : <Terminal className="w-3.5 h-3.5" />}
    {label}
  </button>
)

interface CopyIconProps {
  onClick: () => void
  isCopied: boolean
  className?: string
}

export const CopyIcon: React.FC<CopyIconProps> = ({ onClick, isCopied, className = '' }) => (
  <button 
    onClick={onClick}
    className={`p-1 rounded-md transition-all ${
        isCopied 
        ? 'text-green-500' 
        : `text-neutral-700 hover:text-white ${className}`
    }`}
  >
    {isCopied ? <Check className="w-3.5 h-3.5" /> : <Clipboard className="w-3.5 h-3.5" />}
  </button>
)
