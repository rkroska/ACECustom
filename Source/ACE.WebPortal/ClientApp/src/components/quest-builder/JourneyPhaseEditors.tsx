import type { MutableRefObject } from 'react'
import { Plus } from 'lucide-react'
import StepEditor from './StepEditor'
import WcidField from './WcidField'
import type {
  CorpseObtainSource,
  LandscapeObtainSource,
  QuestJourneyObtain,
  QuestJourneyStart,
  QuestJourneyTurnIn,
  QuestStep,
} from '../../types/questBuilder'
import { ensureStepKey } from '../../types/questBuilder'
import StampConfigPanel, { defaultPickupStamp, defaultStartStamp } from './StampConfigPanel'
import { defaultLandscapePickup, defaultLandscapeSource, syncPickupSteps } from './questJourney'
import type { QuestStampConfig } from '../../types/questBuilder'
import type { StepKey } from './stepSelection'
import { mainStepKey } from './stepSelection'
import { formatWeenie } from './weenieLookup'

export function StartPhaseEditor({
  start,
  packageSlug,
  startStamp,
  grantStartStampOnUse,
  onStartStampChange,
  onGrantStartStampChange,
  completionStampName,
  weenieLabels,
  onChange,
  onPickNpcCloneTemplate,
  selectedKey,
  onSelectKey,
  stepRefs,
}: {
  start: QuestJourneyStart
  packageSlug: string
  startStamp: QuestStampConfig | null
  grantStartStampOnUse: boolean
  onStartStampChange: (stamp: QuestStampConfig | null) => void
  onGrantStartStampChange: (grant: boolean) => void
  completionStampName: string
  weenieLabels: Record<number, string>
  onChange: (patch: Partial<QuestJourneyStart>) => void
  onPickNpcCloneTemplate: () => void
  selectedKey: StepKey | null
  onSelectKey: (k: StepKey | null) => void
  stepRefs: MutableRefObject<Record<string, HTMLDivElement | null>>
}) {
  const updateIntro = (steps: QuestStep[]) => onChange({ introSteps: steps })

  const updateStep = (si: number, patch: Partial<QuestStep>) => {
    const steps = [...start.introSteps]
    steps[si] = { ...steps[si], ...patch }
    updateIntro(steps)
  }

  return (
    <div className="space-y-4">
      <p className="text-xs text-neutral-500">
        Player talks to the quest giver (Use). Optional <strong className="text-neutral-400">start stamp</strong> flags
        that they have been briefed. Turn-in completion stamp{' '}
        <span className="font-mono text-neutral-400">{completionStampName}</span> is applied on hand-in, not here.
      </p>
      <label className="flex items-center gap-2 text-[10px] text-neutral-400">
        <input
          type="checkbox"
          checked={grantStartStampOnUse}
          onChange={(e) => {
            const grant = e.target.checked
            onGrantStartStampChange(grant)
            if (grant && !startStamp) onStartStampChange(defaultStartStamp(packageSlug))
            if (!grant) onStartStampChange(null)
          }}
        />
        Grant start stamp on first talk (InqQuest + StampQuest on Use)
      </label>
      {grantStartStampOnUse && startStamp && (
        <StampConfigPanel
          title="Start stamp"
          description="Stamped when the player hears the intro. Use a different name than pickup and completion stamps. Landscape pickup can require this stamp."
          config={startStamp}
          onChange={(patch) => onStartStampChange({ ...startStamp, ...patch })}
          namePlaceholder={`${packageSlug}_started`}
        />
      )}
      <div className="grid grid-cols-2 gap-3">
        <label className="text-[10px] text-neutral-500 block">
          Quest giver name
          <input
            value={start.npcName}
            onChange={(e) => onChange({ npcName: e.target.value })}
            className="w-full mt-0.5 bg-neutral-950 border border-neutral-700 rounded px-2 py-1.5 text-sm text-white"
          />
        </label>
        <WcidField
          label="Quest giver WCID (new)"
          wcid={start.npcWcid}
          displayName={start.npcName}
          onWcidChange={(npcWcid) => onChange({ npcWcid })}
          idOnly={false}
          hint="Allocated from Next WCID when you load a template."
        />
        <div className="col-span-2">
          <WcidField
            label="Clone appearance from (optional)"
            wcid={start.npcCloneFromWcid ?? 0}
            displayName={start.npcCloneFromWcid ? weenieLabels[start.npcCloneFromWcid] : null}
            onWcidChange={(v) => onChange({ npcCloneFromWcid: v > 0 ? v : undefined })}
            onSearch={onPickNpcCloneTemplate}
            hint="Search an existing NPC or creature weenie to copy model/stats from."
          />
        </div>
      </div>

      <div className="flex justify-between items-center">
        <h4 className="text-sm font-semibold text-white">Intro steps (Use)</h4>
        <button
          type="button"
          onClick={() => updateIntro([...start.introSteps, ensureStepKey({ type: 'Tell', text: '' })])}
          className="text-xs text-blue-400 flex items-center gap-1"
        >
          <Plus className="w-3 h-3" /> Add step
        </button>
      </div>
      {start.introSteps.map((step, si) => (
        <div
          key={si}
          ref={(el) => {
            stepRefs.current[mainStepKey(si)] = el
          }}
        >
          <StepEditor
            step={step}
            weenieLabels={weenieLabels}
            selected={selectedKey === mainStepKey(si)}
            onSelect={() => onSelectKey(mainStepKey(si))}
            onChange={(p) => updateStep(si, p)}
            onRemove={() => updateIntro(start.introSteps.filter((_, i) => i !== si))}
          />
        </div>
      ))}
    </div>
  )
}

export function ObtainPhaseEditor({
  obtain,
  packageSlug,
  pickupStamp,
  startStamp,
  grantStartStampOnUse,
  npcName,
  weenieLabels,
  onChange,
  onPickupStampChange,
  onPickItemTemplate,
  onPickCreatureTemplate,
  onPickObjectTemplate,
}: {
  obtain: QuestJourneyObtain
  packageSlug: string
  pickupStamp: QuestStampConfig | null
  startStamp: QuestStampConfig | null
  grantStartStampOnUse: boolean
  npcName: string
  weenieLabels: Record<number, string>
  onChange: (patch: Partial<QuestJourneyObtain>) => void
  onPickupStampChange: (stamp: QuestStampConfig | null) => void
  onPickItemTemplate: () => void
  onPickCreatureTemplate: () => void
  onPickObjectTemplate: () => void
}) {
  const setSource = (source: CorpseObtainSource | LandscapeObtainSource) => onChange({ source })

  return (
    <div className="space-y-4">
      <p className="text-xs text-neutral-500">Define the quest item and how the player obtains it.</p>

      <div className="rounded-lg border border-neutral-800 bg-neutral-900/50 p-3 space-y-2">
        <h4 className="text-sm font-semibold text-white">Quest item</h4>
        <div className="grid grid-cols-2 gap-2">
          <label className="text-[10px] text-neutral-500 block col-span-2">
            Item name
            <input
              value={obtain.itemName}
              onChange={(e) => onChange({ itemName: e.target.value })}
              className="w-full mt-0.5 bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-sm text-white"
            />
          </label>
          <WcidField
            label="Quest item WCID (new)"
            wcid={obtain.itemWcid}
            displayName={obtain.itemName}
            onWcidChange={(itemWcid) => {
              if (obtain.source.kind === 'corpse') {
                onChange({
                  itemWcid,
                  source: {
                    kind: 'corpse',
                    creature: { ...obtain.source.creature, dropItemWcid: itemWcid },
                  },
                })
                return
              }

              const stamp = pickupStamp ?? defaultPickupStamp(packageSlug)
              onChange({
                itemWcid,
                source: {
                  ...obtain.source,
                  pickupSteps: syncPickupSteps(
                    obtain.source.pickupSteps,
                    stamp,
                    itemWcid,
                    obtain.source.useQuestGate,
                  ),
                },
              })
            }}
          />
          <WcidField
            label="Clone item from"
            wcid={obtain.itemCloneFromWcid ?? 0}
            displayName={obtain.itemCloneFromWcid ? weenieLabels[obtain.itemCloneFromWcid] : null}
            onWcidChange={(v) => onChange({ itemCloneFromWcid: v > 0 ? v : undefined })}
            onSearch={onPickItemTemplate}
          />
        </div>
      </div>

      <div>
        <h4 className="text-sm font-semibold text-white mb-2">How player gets the item</h4>
        <div className="flex gap-2">
          <button
            type="button"
            onClick={() => {
              if (obtain.source.kind === 'corpse') return
              setSource({
                kind: 'corpse',
                creature: {
                  wcid: obtain.itemWcid + 2,
                  name: 'Quest Target',
                  templateWcid: 78780092,
                  patchExisting: false,
                  dropItemWcid: obtain.itemWcid,
                  dropStack: 1,
                },
              })
            }}
            className={`flex-1 py-2 rounded-lg text-xs font-medium border ${
              obtain.source.kind === 'corpse'
                ? 'bg-amber-950/50 border-amber-600 text-amber-100'
                : 'bg-neutral-900 border-neutral-700 text-neutral-400'
            }`}
          >
            Monster corpse
          </button>
          <button
            type="button"
            onClick={() => {
              if (obtain.source.kind === 'landscape') return
              const ps = pickupStamp ?? defaultPickupStamp(packageSlug)
              onPickupStampChange(ps)
              setSource(defaultLandscapeSource(obtain.itemWcid + 3, obtain.itemWcid, ps))
            }}
            className={`flex-1 py-2 rounded-lg text-xs font-medium border ${
              obtain.source.kind === 'landscape'
                ? 'bg-emerald-950/50 border-emerald-600 text-emerald-100'
                : 'bg-neutral-900 border-neutral-700 text-neutral-400'
            }`}
          >
            Landscape object
          </button>
        </div>
      </div>

      {obtain.source.kind === 'corpse' ? (
        <CorpseEditor
          source={obtain.source}
          itemName={obtain.itemName}
          weenieLabels={weenieLabels}
          onChange={(creature) => setSource({ kind: 'corpse', creature })}
          onPickCreatureTemplate={onPickCreatureTemplate}
        />
      ) : (
        <LandscapeEditor
          source={obtain.source}
          itemWcid={obtain.itemWcid}
          itemName={obtain.itemName}
          packageSlug={packageSlug}
          pickupStamp={pickupStamp}
          startStamp={startStamp}
          grantStartStampOnUse={grantStartStampOnUse}
          npcName={npcName}
          weenieLabels={weenieLabels}
          onPickupStampChange={onPickupStampChange}
          onChange={(patch) => {
            if (obtain.source.kind !== 'landscape') return
            setSource({ ...obtain.source, ...patch })
          }}
          onPickObjectTemplate={onPickObjectTemplate}
        />
      )}
    </div>
  )
}

function CorpseEditor({
  source,
  itemName,
  weenieLabels,
  onChange,
  onPickCreatureTemplate,
}: {
  source: CorpseObtainSource
  itemName: string
  weenieLabels: Record<number, string>
  onChange: (c: CorpseObtainSource['creature']) => void
  onPickCreatureTemplate: () => void
}) {
  const c = source.creature
  return (
    <div className="rounded-lg border border-amber-900/40 bg-amber-950/20 p-3 space-y-2">
      <h4 className="text-sm font-semibold text-amber-100">Mob (corpse drop)</h4>
      <p className="text-[10px] text-neutral-500">
        Export writes <span className="font-mono">weenie_properties_create_list</span> (Treasure, destination 8,
        shade 0).
      </p>
      <input
        value={c.name}
        onChange={(e) => onChange({ ...c, name: e.target.value })}
        className="w-full bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-xs text-white"
        placeholder="Mob name"
      />
      <div className="grid grid-cols-2 gap-2">
        <WcidField
          label="Mob WCID (new)"
          wcid={c.wcid}
          displayName={c.name}
          onWcidChange={(wcid) => onChange({ ...c, wcid })}
        />
        <WcidField
          label="Clone mob from"
          wcid={c.templateWcid ?? 0}
          displayName={c.templateWcid ? weenieLabels[c.templateWcid] : null}
          onWcidChange={(v) => onChange({ ...c, templateWcid: v > 0 ? v : undefined })}
          onSearch={onPickCreatureTemplate}
        />
      </div>
      <p className="text-[10px] text-neutral-500">
        Drops <span className="text-white">{itemName}</span> on corpse (Treasure table).
      </p>
      <label className="text-[9px] text-neutral-500 block">
        Drop stack on corpse
        <input
          type="number"
          min={1}
          value={c.dropStack}
          onChange={(e) => onChange({ ...c, dropStack: Number(e.target.value) || 1 })}
          className="w-20 bg-neutral-950 border border-neutral-700 rounded px-1 py-0.5 text-xs text-white"
        />
      </label>
      <label className="flex items-center gap-2 text-[10px] text-neutral-400">
        <input
          type="checkbox"
          checked={c.patchExisting}
          onChange={(e) => onChange({ ...c, patchExisting: e.target.checked })}
        />
        Patch existing mob only (no full clone SQL)
      </label>
    </div>
  )
}

function LandscapeEditor({
  source,
  itemWcid,
  itemName,
  packageSlug,
  pickupStamp,
  startStamp,
  grantStartStampOnUse,
  npcName,
  weenieLabels,
  onChange,
  onPickupStampChange,
  onPickObjectTemplate,
}: {
  source: LandscapeObtainSource
  itemWcid: number
  itemName: string
  packageSlug: string
  pickupStamp: QuestStampConfig | null
  startStamp: QuestStampConfig | null
  grantStartStampOnUse: boolean
  npcName: string
  weenieLabels: Record<number, string>
  onChange: (patch: Partial<LandscapeObtainSource>) => void
  onPickupStampChange: (stamp: QuestStampConfig | null) => void
  onPickObjectTemplate: () => void
}) {
  const updatePickupStep = (si: number, patch: Partial<QuestStep>) => {
    const steps = [...source.pickupSteps]
    steps[si] = { ...steps[si], ...patch }
    onChange({ pickupSteps: steps })
  }

  return (
    <div className="rounded-lg border border-emerald-900/40 bg-emerald-950/20 p-3 space-y-2">
      <h4 className="text-sm font-semibold text-emerald-100">World object (gives item)</h4>
      <p className="text-[10px] text-neutral-500">
        Place the object in the world manually after import. Player {source.trigger}s it to receive the item.
      </p>
      <input
        value={source.objectName}
        onChange={(e) => onChange({ objectName: e.target.value })}
        className="w-full bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-xs text-white"
      />
      <div className="grid grid-cols-2 gap-2">
        <WcidField
          label="Object WCID (new)"
          wcid={source.objectWcid}
          displayName={source.objectName}
          onWcidChange={(objectWcid) => onChange({ objectWcid })}
        />
        <WcidField
          label="Clone object from"
          wcid={source.objectCloneFromWcid ?? 0}
          displayName={source.objectCloneFromWcid ? weenieLabels[source.objectCloneFromWcid] : null}
          onWcidChange={(v) => onChange({ objectCloneFromWcid: v > 0 ? v : undefined })}
          onSearch={onPickObjectTemplate}
        />
      </div>
      <p className="text-[10px] text-neutral-500">
        Gives <span className="text-white">{itemName}</span> ({formatWeenie(itemWcid, weenieLabels, itemName)}).
      </p>
      <label className="text-[9px] text-neutral-500 block">
        Trigger
        <select
          value={source.trigger}
          onChange={(e) => onChange({ trigger: e.target.value as 'PickUp' | 'Use' })}
          className="w-full mt-0.5 bg-neutral-950 border border-neutral-700 rounded px-1 py-1 text-xs text-white"
        >
          <option value="Use">Use</option>
          <option value="PickUp">PickUp</option>
        </select>
      </label>
      <label className="flex items-center gap-2 text-[10px] text-neutral-400">
        <input
          type="checkbox"
          checked={source.useQuestGate}
          onChange={(e) => {
            const useQuestGate = e.target.checked
            const stamp = pickupStamp ?? defaultPickupStamp(packageSlug)
            if (useQuestGate) onPickupStampChange(stamp)
            else onPickupStampChange(null)
            onChange({
              useQuestGate,
              requireStartStampForPickup: useQuestGate ? source.requireStartStampForPickup : false,
              pickupSteps: defaultLandscapePickup(stamp, itemWcid, useQuestGate),
            })
          }}
        />
        Gated pickup (separate stamp + cooldown from turn-in)
      </label>
      <label className="flex items-center gap-2 text-[10px] text-neutral-400">
        <input
          type="checkbox"
          checked={source.requireStartStampForPickup}
          disabled={!grantStartStampOnUse || !startStamp}
          onChange={(e) => onChange({ requireStartStampForPickup: e.target.checked })}
        />
        Require NPC talk first (checks start stamp before pickup)
      </label>
      {source.requireStartStampForPickup && (!grantStartStampOnUse || !startStamp) && (
        <p className="text-[10px] text-amber-400">Enable “Grant start stamp on first talk” on the Start phase.</p>
      )}
      {source.requireStartStampForPickup && startStamp && (
        <p className="text-[10px] text-neutral-500">
          Pickup blocked until player has stamp{' '}
          <span className="font-mono text-violet-300">{startStamp.name}</span> from talking to {npcName}.
        </p>
      )}
      {source.useQuestGate && pickupStamp && (
        <StampConfigPanel
          title="Pickup stamp & timer"
          description="Controls how often the player can take the item from this object. Use a different name than the turn-in completion stamp."
          config={pickupStamp}
          onChange={(patch) => {
            const next = { ...pickupStamp, ...patch }
            onPickupStampChange(next)
            onChange({
              pickupSteps: syncPickupSteps(source.pickupSteps, next, itemWcid, true),
            })
          }}
          namePlaceholder={`${packageSlug}_pickup`}
        />
      )}
      <div className="space-y-2 pt-2 border-t border-neutral-800">
        <div className="flex justify-between">
          <span className="text-xs text-white font-medium">Pickup emote steps</span>
          <button
            type="button"
            onClick={() => onChange({ pickupSteps: [...source.pickupSteps, ensureStepKey({ type: 'Tell', text: '' })] })}
            className="text-[10px] text-blue-400"
          >
            + step
          </button>
        </div>
        {source.pickupSteps.map((step, si) => (
          <StepEditor
            key={si}
            step={step}
            weenieLabels={weenieLabels}
            onChange={(p) => updatePickupStep(si, p)}
            onRemove={() => onChange({ pickupSteps: source.pickupSteps.filter((_, i) => i !== si) })}
            onAddBranchStep={
              step.type === 'InqQuest'
                ? (branch) => {
                    const inq = { ...step, branches: { onCooldown: [...(step.branches?.onCooldown ?? [])], canComplete: [...(step.branches?.canComplete ?? [])] } }
                    inq.branches![branch].push(ensureStepKey({ type: 'Tell', text: '' }))
                    updatePickupStep(si, inq)
                  }
                : undefined
            }
            onUpdateBranchStep={
              step.type === 'InqQuest'
                ? (branch, bi, p) => {
                    const inq = { ...step, branches: { ...step.branches!, [branch]: [...step.branches![branch]] } }
                    inq.branches![branch][bi] = { ...inq.branches![branch][bi], ...p }
                    updatePickupStep(si, inq)
                  }
                : undefined
            }
            onRemoveBranchStep={
              step.type === 'InqQuest'
                ? (branch, bi) => {
                    const inq = { ...step, branches: { ...step.branches!, [branch]: step.branches![branch].filter((_, i) => i !== bi) } }
                    updatePickupStep(si, inq)
                  }
                : undefined
            }
            stepIndex={si}
          />
        ))}
      </div>
    </div>
  )
}

export function TurnInPhaseEditor({
  turnIn,
  itemWcid,
  itemName,
  completionStamp,
  weenieLabels,
  onChange,
  onCompletionStampChange,
  onPickRewardTemplate,
  selectedKey,
  onSelectKey,
  stepRefs,
}: {
  turnIn: QuestJourneyTurnIn
  itemWcid: number
  itemName: string
  completionStamp: QuestStampConfig
  weenieLabels: Record<number, string>
  onChange: (patch: Partial<QuestJourneyTurnIn>) => void
  onCompletionStampChange: (patch: Partial<QuestStampConfig>) => void
  onPickRewardTemplate: () => void
  selectedKey: StepKey | null
  onSelectKey: (k: StepKey | null) => void
  stepRefs: MutableRefObject<Record<string, HTMLDivElement | null>>
}) {
  const updateGive = (steps: QuestStep[]) => onChange({ giveSteps: steps })
  const updateRefuse = (steps: QuestStep[]) => onChange({ refuseSteps: steps })

  const patchGiveStep = (si: number, patch: Partial<QuestStep>, branch?: 'onCooldown' | 'canComplete') => {
    const steps = [...turnIn.giveSteps]
    if (branch !== undefined) {
      const inq = { ...steps[si] }
      const branches = { ...inq.branches! }
      const list = [...branches[branch]]
      list[0] = { ...list[0], ...patch }
      branches[branch] = list
      inq.branches = branches
      steps[si] = inq
    } else {
      steps[si] = { ...steps[si], ...patch }
    }
    updateGive(steps)
  }

  return (
    <div className="space-y-4">
      <p className="text-xs text-neutral-500">
        Player returns and gives <span className="text-white">{itemName}</span> ({formatWeenie(itemWcid, weenieLabels, itemName)}).
      </p>
      <StampConfigPanel
        title="Turn-in completion stamp & timer"
        description="Applied when the NPC accepts the quest item (StampQuest in Give flow). This is separate from landscape pickup stamps."
        config={completionStamp}
        onChange={onCompletionStampChange}
        namePlaceholder="myquest_complete"
      />
      <div className="rounded-lg border border-neutral-800 bg-neutral-900/40 p-3 space-y-2">
        <h4 className="text-sm font-semibold text-white">Reward on success</h4>
        <div className="grid grid-cols-2 gap-2">
          <div className="col-span-2">
            <WcidField
              label="Reward item (existing weenie)"
              wcid={turnIn.rewardWcid}
              displayName={weenieLabels[turnIn.rewardWcid]}
              onWcidChange={(rewardWcid) => onChange({ rewardWcid })}
              onSearch={onPickRewardTemplate}
            />
          </div>
          <label className="text-[10px] text-neutral-500 block">
            Stack
            <input
              type="number"
              min={1}
              value={turnIn.rewardStack}
              onChange={(e) => onChange({ rewardStack: Number(e.target.value) || 1 })}
              className="w-full mt-0.5 bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-xs text-white"
            />
          </label>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 items-start">
        <div className="rounded-lg border border-green-900/40 bg-green-950/10 p-3 min-h-[200px]">
          <div className="flex justify-between items-center mb-2">
            <h4 className="text-sm font-semibold text-green-100">Give flow</h4>
            <button
              type="button"
              onClick={() => updateGive([...turnIn.giveSteps, ensureStepKey({ type: 'Tell', text: '' })])}
              className="text-xs text-blue-400 flex items-center gap-1"
            >
              <Plus className="w-3 h-3" /> Add
            </button>
          </div>
          <p className="text-[10px] text-neutral-500 mb-3">Player hands in {itemName}</p>
          {turnIn.giveSteps.map((step, si) => (
          <div key={si} ref={(el) => { stepRefs.current[`give:${si}`] = el }} className="mb-2">
            <StepEditor
              step={step}
              weenieLabels={weenieLabels}
              selected={selectedKey === `give:${si}`}
              onSelect={() => onSelectKey(`give:${si}` as StepKey)}
              onChange={(p) => patchGiveStep(si, p)}
              onRemove={() => updateGive(turnIn.giveSteps.filter((_, i) => i !== si))}
              onAddBranchStep={
                step.type === 'InqQuest'
                  ? (branch) => {
                      const inq = { ...step, branches: { onCooldown: [...(step.branches?.onCooldown ?? [])], canComplete: [...(step.branches?.canComplete ?? [])] } }
                      inq.branches![branch].push(ensureStepKey({ type: 'Tell', text: '' }))
                      patchGiveStep(si, inq)
                    }
                  : undefined
              }
              onUpdateBranchStep={
                step.type === 'InqQuest'
                  ? (branch, bi, p) => {
                      const inq = { ...step, branches: { ...step.branches!, [branch]: [...step.branches![branch]] } }
                      inq.branches![branch][bi] = { ...inq.branches![branch][bi], ...p }
                      patchGiveStep(si, inq)
                    }
                  : undefined
              }
              onRemoveBranchStep={
                step.type === 'InqQuest'
                  ? (branch, bi) => {
                      const inq = { ...step, branches: { ...step.branches!, [branch]: step.branches![branch].filter((_, i) => i !== bi) } }
                      patchGiveStep(si, inq)
                    }
                  : undefined
              }
              branchRefs={stepRefs}
              stepIndex={si}
              selectedKey={selectedKey}
              onSelectBranchStep={(branch, bi) => onSelectKey(`inq:${si}:${branch === 'onCooldown' ? 'cooldown' : 'complete'}:${bi}` as StepKey)}
            />
          </div>
        ))}
        </div>

        <div className="rounded-lg border border-red-900/40 bg-red-950/10 p-3 min-h-[200px]">
          <div className="flex justify-between items-center mb-2">
            <h4 className="text-sm font-semibold text-red-100">Refuse flow</h4>
            <button
              type="button"
              onClick={() => updateRefuse([...turnIn.refuseSteps, ensureStepKey({ type: 'Tell', text: '' })])}
              className="text-xs text-blue-400 flex items-center gap-1"
            >
              <Plus className="w-3 h-3" /> Add
            </button>
          </div>
          <p className="text-[10px] text-neutral-500 mb-3">Wrong item dragged onto NPC</p>
          {turnIn.refuseSteps.map((step, si) => (
            <div key={si} className="mb-2">
              <StepEditor
                step={step}
                weenieLabels={weenieLabels}
                onChange={(p) => {
                  const steps = [...turnIn.refuseSteps]
                  steps[si] = { ...steps[si], ...p }
                  updateRefuse(steps)
                }}
                onRemove={() => updateRefuse(turnIn.refuseSteps.filter((_, i) => i !== si))}
                compact
              />
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
