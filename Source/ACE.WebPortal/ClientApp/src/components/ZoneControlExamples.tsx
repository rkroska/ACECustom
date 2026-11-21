import { useState, type ReactNode } from 'react'
import { ChevronDown, ChevronRight, Lightbulb } from 'lucide-react'

// Worked examples for the Zone Control page. Every example is built from the data the section already loaded (a real stat, a
// real bounty row, a real reward table), so it describes the server as it is now - never a number typed in here. When the data
// has nothing to show, the example says how the section works in words instead.

const STORE_KEY = 'zc-example-open'

const readOpen = (): Record<string, boolean> => {
  try {
    return JSON.parse(localStorage.getItem(STORE_KEY) ?? '{}') ?? {}
  } catch {
    return {}
  }
}

/** A collapsed "Example" line; open, it shows the worked example. Open or closed is remembered per section in this browser. */
export const Example = ({ id, title = 'Example', children }: { id: string; title?: string; children: ReactNode }) => {
  const [open, setOpen] = useState(() => readOpen()[id] ?? false)
  const toggle = () => {
    const next = !open
    setOpen(next)
    try {
      localStorage.setItem(STORE_KEY, JSON.stringify({ ...readOpen(), [id]: next }))
    } catch {
      // private window or blocked storage: it just won't be remembered
    }
  }
  return (
    <div className="rounded-lg border border-blue-900/40 bg-blue-950/10 text-xs">
      <button type="button" onClick={toggle} aria-expanded={open} className="w-full flex items-center gap-1.5 px-3 py-1.5 text-left text-blue-300 hover:text-blue-200">
        {open ? <ChevronDown className="w-3.5 h-3.5" aria-hidden /> : <ChevronRight className="w-3.5 h-3.5" aria-hidden />}
        <Lightbulb className="w-3.5 h-3.5" aria-hidden />
        <span className="font-bold">{title}</span>
      </button>
      {open && <div className="px-3 pb-3 pt-1 space-y-1.5 text-neutral-300 leading-relaxed">{children}</div>}
    </div>
  )
}

/** A value from the data, set off from the words around it. */
export const V = ({ children }: { children: ReactNode }) => <span className="font-mono text-neutral-100">{children}</span>
