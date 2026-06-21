import type { MutableRefObject } from 'react'
import { DEFAULT_MOTION, MOTION_PRESETS } from './motionPresets'
import { branchStepKey, type StepKey } from './stepSelection'
import type { QuestStep } from '../../types/questBuilder'
import { formatWeenie } from './weenieLookup'

export const STEP_TYPES = ['Tell', 'DirectBroadcast', 'Motion', 'InqQuest', 'StampQuest', 'Give', 'TakeItems']

type StepEditorProps = {
  step: QuestStep
  weenieLabels?: Record<number, string>
  selected?: boolean
  onSelect?: () => void
  onChange: (p: Partial<QuestStep>) => void
  onRemove: () => void
  onAddBranchStep?: (branch: 'onCooldown' | 'canComplete') => void
  onUpdateBranchStep?: (branch: 'onCooldown' | 'canComplete', index: number, p: Partial<QuestStep>) => void
  onRemoveBranchStep?: (branch: 'onCooldown' | 'canComplete', index: number) => void
  branchRefs?: MutableRefObject<Record<string, HTMLDivElement | null>>
  stepIndex?: number
  selectedKey?: StepKey | null
  onSelectBranchStep?: (branch: 'onCooldown' | 'canComplete', index: number) => void
  compact?: boolean
}

export default function StepEditor({
  step,
  weenieLabels = {},
  selected = false,
  onSelect,
  onChange,
  onRemove,
  onAddBranchStep,
  onUpdateBranchStep,
  onRemoveBranchStep,
  branchRefs,
  stepIndex = 0,
  selectedKey,
  onSelectBranchStep,
  compact = false,
}: StepEditorProps) {
  const wrapperClass = onSelect
    ? `rounded-lg border p-3 space-y-2 cursor-pointer transition-colors ${
        selected
          ? 'border-blue-500 bg-blue-950/30 ring-1 ring-blue-500/40'
          : 'border-neutral-700 bg-neutral-900/60 hover:border-neutral-600'
      }`
    : 'rounded-lg border border-neutral-700 bg-neutral-900/60 p-3 space-y-2'

  return (
    <div
      role={onSelect ? 'button' : undefined}
      tabIndex={onSelect ? 0 : undefined}
      onClick={onSelect}
      onKeyDown={onSelect ? (e) => e.key === 'Enter' && onSelect() : undefined}
      className={wrapperClass}
    >
      <div className="flex gap-2" onClick={(e) => e.stopPropagation()}>
        <select
          value={step.type}
          onChange={(e) => onChange({ type: e.target.value })}
          className="flex-1 bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-xs text-white"
        >
          {STEP_TYPES.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </select>
        <button type="button" onClick={onRemove} className="text-xs text-red-400 hover:text-red-300">
          Remove
        </button>
      </div>

      {step.type === 'Motion' && (
        <div className="space-y-1" onClick={(e) => e.stopPropagation()}>
          <select
            value={step.motion ?? DEFAULT_MOTION}
            onChange={(e) => onChange({ motion: e.target.value })}
            className="w-full bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-xs text-white"
          >
            {MOTION_PRESETS.map((m) => (
              <option key={m.id} value={m.id}>
                {m.label}
              </option>
            ))}
          </select>
        </div>
      )}

      {(step.type === 'Tell' || step.type === 'DirectBroadcast') && (
        <div className="space-y-1" onClick={(e) => e.stopPropagation()}>
          <textarea
            value={step.text ?? ''}
            onChange={(e) => onChange({ text: e.target.value })}
            rows={compact ? 2 : 3}
            className="w-full bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-xs text-white"
            placeholder="Message text"
          />
          <label className="text-[10px] text-neutral-500 flex items-center gap-2">
            Delay (sec)
            <input
              type="number"
              min={0}
              step={0.5}
              value={step.delay ?? 0}
              onChange={(e) => onChange({ delay: Number(e.target.value) })}
              className="w-16 bg-neutral-950 border border-neutral-700 rounded px-1 py-0.5 text-xs text-white"
            />
          </label>
        </div>
      )}

      {(step.type === 'InqQuest' || step.type === 'StampQuest') && (
        <input
          value={step.stamp ?? ''}
          onChange={(e) => onChange({ stamp: e.target.value })}
          onClick={(e) => e.stopPropagation()}
          className="w-full bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-xs text-white font-mono"
          placeholder="stamp_name"
        />
      )}

      {(step.type === 'Give' || step.type === 'TakeItems') && (
        <div className="space-y-1" onClick={(e) => e.stopPropagation()}>
          {step.wcid ? (
            <p className="text-[10px] text-neutral-400 truncate">{formatWeenie(step.wcid, weenieLabels)}</p>
          ) : null}
          <div className="flex gap-2">
            <input
              type="number"
              value={step.wcid ?? ''}
              onChange={(e) => onChange({ wcid: Number(e.target.value) })}
              className="w-28 bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-xs text-white font-mono"
              placeholder="WCID"
              title="Item weenie class ID"
            />
            <input
              type="number"
              value={step.stack ?? 1}
              onChange={(e) => onChange({ stack: Number(e.target.value) })}
              className="w-16 bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-xs text-white"
              placeholder="Stack"
            />
          </div>
        </div>
      )}

      {step.type === 'InqQuest' && onAddBranchStep && onUpdateBranchStep && onRemoveBranchStep && (
        <div className="space-y-2 pt-2 border-t border-neutral-800" onClick={(e) => e.stopPropagation()}>
          <p className="text-[10px] text-neutral-500 leading-snug">
            <strong className="text-neutral-400">InqQuest</strong> — left: already has stamp. Right: can complete turn-in.
          </p>
          <div className="grid grid-cols-1 gap-2">
            <BranchColumn
              title="Already completed (has stamp)"
              steps={step.branches?.onCooldown ?? []}
              weenieLabels={weenieLabels}
              onAdd={() => onAddBranchStep('onCooldown')}
              onChange={(i, p) => onUpdateBranchStep('onCooldown', i, p)}
              onRemove={(i) => onRemoveBranchStep('onCooldown', i)}
              branchRefs={branchRefs}
              inqIndex={stepIndex}
              branch="onCooldown"
              selectedKey={selectedKey}
              onSelectStep={onSelectBranchStep}
            />
            <BranchColumn
              title="Can turn in (no stamp yet)"
              steps={step.branches?.canComplete ?? []}
              weenieLabels={weenieLabels}
              onAdd={() => onAddBranchStep('canComplete')}
              onChange={(i, p) => onUpdateBranchStep('canComplete', i, p)}
              onRemove={(i) => onRemoveBranchStep('canComplete', i)}
              branchRefs={branchRefs}
              inqIndex={stepIndex}
              branch="canComplete"
              selectedKey={selectedKey}
              onSelectStep={onSelectBranchStep}
            />
          </div>
        </div>
      )}
    </div>
  )
}

function BranchColumn({
  title,
  steps,
  weenieLabels,
  onAdd,
  onChange,
  onRemove,
  branchRefs,
  inqIndex,
  branch,
  selectedKey,
  onSelectStep,
}: {
  title: string
  steps: QuestStep[]
  weenieLabels: Record<number, string>
  onAdd: () => void
  onChange: (index: number, p: Partial<QuestStep>) => void
  onRemove: (index: number) => void
  branchRefs?: MutableRefObject<Record<string, HTMLDivElement | null>>
  inqIndex: number
  branch: 'onCooldown' | 'canComplete'
  selectedKey?: StepKey | null
  onSelectStep?: (branch: 'onCooldown' | 'canComplete', index: number) => void
}) {
  return (
    <div className="rounded border border-neutral-800 bg-neutral-950/50 p-2 space-y-1">
      <div className="text-[10px] font-bold text-neutral-400">{title}</div>
      {steps.map((s, i) => {
        const key = branchStepKey(inqIndex, branch, i)
        const sel = selectedKey === key
        return (
          <div
            key={s._key || `fallback_${i}`}
            ref={branchRefs ? (el) => { branchRefs.current[key] = el } : undefined}
            role={onSelectStep ? 'button' : undefined}
            tabIndex={onSelectStep ? 0 : undefined}
            onClick={onSelectStep ? () => onSelectStep(branch, i) : undefined}
            className={`rounded p-1.5 space-y-1 ${onSelectStep ? 'cursor-pointer' : ''} ${
              sel ? 'bg-blue-900/40 ring-1 ring-blue-500/50' : onSelectStep ? 'hover:bg-neutral-900' : ''
            }`}
          >
            <div className="flex gap-1">
              <select
                value={s.type}
                onChange={(e) => onChange(i, { type: e.target.value })}
                onClick={(e) => e.stopPropagation()}
                className="flex-1 bg-neutral-950 border border-neutral-700 rounded text-[10px] text-white"
              >
                {STEP_TYPES.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
              <button type="button" onClick={(e) => { e.stopPropagation(); onRemove(i) }} className="text-[10px] text-red-400">
                ×
              </button>
            </div>
            {(s.type === 'Tell' || s.type === 'DirectBroadcast') && (
              <input
                value={s.text ?? ''}
                onChange={(e) => onChange(i, { text: e.target.value })}
                onClick={(e) => e.stopPropagation()}
                className="w-full bg-neutral-950 border border-neutral-700 rounded text-[10px] text-white"
              />
            )}
            {(s.type === 'Give' || s.type === 'TakeItems') && (
              <div onClick={(e) => e.stopPropagation()}>
                {s.wcid ? (
                  <p className="text-[9px] text-neutral-500 truncate mb-0.5">{formatWeenie(s.wcid, weenieLabels)}</p>
                ) : null}
                <input
                  type="number"
                  value={s.wcid ?? ''}
                  onChange={(e) => onChange(i, { wcid: Number(e.target.value) })}
                  className="w-full bg-neutral-950 border border-neutral-700 rounded text-[10px] text-white font-mono"
                />
              </div>
            )}
          </div>
        )
      })}
      <button type="button" onClick={onAdd} className="text-[10px] text-blue-400">
        + step
      </button>
    </div>
  )
}
