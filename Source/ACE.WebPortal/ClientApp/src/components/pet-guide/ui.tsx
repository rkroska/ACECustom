import { ReactNode } from 'react'
import { MapPin, Power } from 'lucide-react'
import { cn } from '../../utils/cn'
import type { GuideItem, GuideNpc, PetGuideData } from './petGuideApi'

// ---- building blocks --------------------------------------------------------------------------

export function Section({ id, title, icon, lead, children }: { id: string; title: string; icon: ReactNode; lead?: ReactNode; children: ReactNode }) {
  return (
    <section id={id} className="space-y-4 scroll-mt-6">
      <div className="flex items-center gap-3">
        <div className="w-10 h-10 rounded-xl bg-gradient-to-tr from-violet-600/30 to-rose-500/30 border border-violet-500/30 flex items-center justify-center text-violet-300">
          {icon}
        </div>
        <h2 className="text-xl md:text-2xl font-black text-white tracking-tight">{title}</h2>
      </div>
      {lead && <p className="text-sm text-neutral-300 leading-relaxed max-w-3xl">{lead}</p>}
      {children}
    </section>
  )
}

export function Card({ title, icon, children, className, accent = 'neutral' }: { title?: ReactNode; icon?: ReactNode; children: ReactNode; className?: string; accent?: 'neutral' | 'violet' | 'rose' | 'amber' | 'emerald' | 'blue' }) {
  const border = {
    neutral: 'border-neutral-800',
    violet: 'border-violet-500/30',
    rose: 'border-rose-500/30',
    amber: 'border-amber-500/30',
    emerald: 'border-emerald-500/30',
    blue: 'border-blue-500/30',
  }[accent]
  return (
    <div className={cn('bg-neutral-900/70 backdrop-blur-md rounded-2xl border p-5 space-y-3 shadow-xl', border, className)}>
      {title && (
        <h3 className="text-sm font-black uppercase tracking-wider text-neutral-200 flex items-center gap-2">
          {icon}
          {title}
        </h3>
      )}
      <div className="text-[13px] text-neutral-300 leading-relaxed space-y-2">{children}</div>
    </div>
  )
}

export function Icon({ item, size = 32 }: { item?: { icon: string | null; name: string | null } | null; size?: number }) {
  // Fixed square in CSS: in a flex row the default stretch (and Tailwind's img height:auto) would
  // otherwise pull the icon to the row's height.
  const box = { width: size, height: size, minWidth: size, minHeight: size }
  if (!item?.icon) return <div className="rounded bg-neutral-800 shrink-0 self-start" style={box} />
  return <img src={item.icon} alt={item.name ?? ''} style={box} className="shrink-0 self-start object-contain [image-rendering:pixelated]" loading="lazy" />
}

/** An item as it appears in game: icon and name. */
export function Item({ item, fallback }: { item?: GuideItem | null; fallback: string }) {
  return (
    <span className="inline-flex items-center gap-1.5 align-middle font-semibold text-neutral-100">
      <Icon item={item} size={20} />
      {item?.name ?? fallback}
    </span>
  )
}

/** Where to find an NPC: every spawn spot the world database has for it. */
export function Where({ npcs, place }: { npcs?: GuideNpc[]; place?: string }) {
  const spots = (npcs ?? []).flatMap(n => n.spots)
  const coords = Array.from(new Set(spots.map(s => s.coords).filter((c): c is string => !!c)))
  const inDungeon = spots.some(s => !s.coords)
  const inBuilding = spots.some(s => s.coords && s.indoors)

  const parts: string[] = []
  if (coords.length) parts.push(coords.join(' / ') + (inBuilding ? ' (inside a building)' : ''))
  if (inDungeon && place) parts.push(place)
  if (!parts.length) parts.push(place ?? 'location not listed')

  return (
    <span className="inline-flex items-center gap-1 text-[11px] font-bold text-emerald-300 bg-emerald-500/10 border border-emerald-500/20 rounded px-1.5 py-0.5 align-middle">
      <MapPin className="w-3 h-3" />
      {parts.join(' / ')}
    </span>
  )
}

export function Npc({ data, npcKey, fallback, place }: { data: PetGuideData; npcKey: string; fallback: string; place?: string }) {
  const npcs = data.world.npcs[npcKey]
  return (
    <span className="inline-flex items-center gap-1.5 flex-wrap align-middle">
      <strong className="text-neutral-100">{npcs?.[0]?.name ?? fallback}</strong>
      <Where npcs={npcs} place={place} />
    </span>
  )
}

/** Shown when the server has a feature switched off, so players are not sent after something that is not live. */
export function Disabled({ when, what }: { when: boolean; what: string }) {
  if (!when) return null
  return (
    <div className="flex items-center gap-2 text-xs font-bold text-amber-300 bg-amber-500/10 border border-amber-500/30 rounded-xl px-3 py-2">
      <Power className="w-4 h-4 shrink-0" />
      {what} is currently turned off on this server.
    </div>
  )
}

export function Tip({ children }: { children: ReactNode }) {
  return (
    <div className="text-xs text-blue-200 bg-blue-500/10 border border-blue-500/30 rounded-xl px-3 py-2 leading-relaxed">
      <span className="font-black uppercase tracking-wider text-blue-300 mr-1.5">Tip</span>
      {children}
    </div>
  )
}

export function Steps({ steps }: { steps: ReactNode[] }) {
  return (
    <ol className="space-y-2">
      {steps.map((step, i) => (
        <li key={i} className="flex gap-3">
          <span className="w-6 h-6 rounded-full bg-violet-500/20 border border-violet-500/40 text-violet-200 text-[11px] font-black flex items-center justify-center shrink-0">
            {i + 1}
          </span>
          <div className="pt-0.5">{step}</div>
        </li>
      ))}
    </ol>
  )
}

export function Stat({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="bg-neutral-950/60 border border-neutral-800 rounded-xl px-3 py-2">
      <div className="text-[10px] font-bold uppercase tracking-widest text-neutral-500">{label}</div>
      <div className="text-sm font-black text-neutral-100">{value}</div>
    </div>
  )
}

export function Cmd({ children }: { children: ReactNode }) {
  return <code className="text-[12px] font-bold text-amber-200 bg-neutral-950 border border-neutral-800 rounded px-1.5 py-0.5">{children}</code>
}
