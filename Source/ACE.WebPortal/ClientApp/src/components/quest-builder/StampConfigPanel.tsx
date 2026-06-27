import type { QuestStampConfig } from '../../types/questBuilder'

const COOLDOWN_PRESETS: { label: string; seconds: number }[] = [
  { label: 'One-time (0)', seconds: 0 },
  { label: 'Daily (24h)', seconds: 86400 },
  { label: '20 hours', seconds: 72000 },
]

type Props = {
  title: string
  description: string
  config: QuestStampConfig
  onChange: (patch: Partial<QuestStampConfig>) => void
  namePlaceholder?: string
}

export default function StampConfigPanel({ title, description, config, onChange, namePlaceholder }: Props) {
  return (
    <div className="rounded-lg border border-neutral-800 bg-neutral-950/50 p-3 space-y-2">
      <div>
        <h4 className="text-sm font-semibold text-white">{title}</h4>
        <p className="text-[10px] text-neutral-500 mt-0.5 leading-relaxed">{description}</p>
      </div>
      <label className="text-[10px] text-neutral-500 block">
        Stamp name
        <input
          value={config.name}
          onChange={(e) => onChange({ name: e.target.value })}
          className="w-full mt-0.5 bg-neutral-900 border border-neutral-700 rounded px-2 py-1 text-xs text-white font-mono"
          placeholder={namePlaceholder}
        />
      </label>
      <label className="text-[10px] text-neutral-500 block">
        Quest journal message
        <input
          value={config.message ?? ''}
          onChange={(e) => onChange({ message: e.target.value })}
          className="w-full mt-0.5 bg-neutral-900 border border-neutral-700 rounded px-2 py-1 text-xs text-white"
          placeholder="Shown when the player receives this stamp."
        />
      </label>
      <label className="text-[10px] text-neutral-500 block">
        Cooldown (min_delta seconds)
        <input
          type="number"
          min={0}
          value={config.cooldownSeconds}
          onChange={(e) => onChange({ cooldownSeconds: Math.max(0, Number(e.target.value) || 0) })}
          className="w-full mt-0.5 bg-neutral-900 border border-neutral-700 rounded px-2 py-1 text-xs text-white font-mono"
        />
      </label>
      <div className="flex flex-wrap gap-1">
        {COOLDOWN_PRESETS.map((p) => (
          <button
            key={p.seconds}
            type="button"
            onClick={() => onChange({ cooldownSeconds: p.seconds })}
            className={`text-[9px] px-2 py-0.5 rounded border ${
              config.cooldownSeconds === p.seconds
                ? 'border-blue-500 text-blue-300 bg-blue-950/40'
                : 'border-neutral-700 text-neutral-500 hover:border-neutral-500'
            }`}
          >
            {p.label}
          </button>
        ))}
      </div>
    </div>
  )
}

export function defaultPickupStamp(packageSlug: string): QuestStampConfig {
  const base = packageSlug.replace(/[^a-z0-9_]/gi, '_').toLowerCase() || 'custom_quest'
  return {
    name: `${base}_pickup`,
    message: 'You obtained the quest item.',
    cooldownSeconds: 86400,
    maxSolves: -1,
  }
}

export function defaultCompletionStamp(packageSlug: string): QuestStampConfig {
  const base = packageSlug.replace(/[^a-z0-9_]/gi, '_').toLowerCase() || 'custom_quest'
  return {
    name: `${base}_complete`,
    message: 'You completed this quest.',
    cooldownSeconds: 0,
    maxSolves: 1,
  }
}

export function defaultStartStamp(packageSlug: string): QuestStampConfig {
  const base = packageSlug.replace(/[^a-z0-9_]/gi, '_').toLowerCase() || 'custom_quest'
  return {
    name: `${base}_started`,
    message: 'You spoke with the quest giver.',
    cooldownSeconds: 0,
    maxSolves: 1,
  }
}
