import { useCallback, useEffect, useRef, useState } from 'react'
import { Download, Database, RefreshCw, AlertCircle, CheckCircle2, ChevronDown, ChevronRight, GitBranch } from 'lucide-react'
import { api } from '../../services/api'
import PageHeader from '../common/PageHeader'
import CreatureTemplatePickerModal from './CreatureTemplatePickerModal'
import ItemTemplatePickerModal from './ItemTemplatePickerModal'
import { journeyPhaseSummary, journeyToPackage, packageToJourney } from './questJourney'
import { ObtainPhaseEditor, StartPhaseEditor, TurnInPhaseEditor } from './JourneyPhaseEditors'
import QuestFlowSimulator from './QuestFlowSimulator'
import QuestTimingHelp from './QuestTimingHelp'
import { useJourneyWeenieLabels } from './useJourneyWeenieLabels'
import { seedWeenieLabel } from './weenieLookup'
import type {
  JourneyPhaseId,
  QuestJourney,
  QuestPackage,
  QuestImportResult,
  QuestTemplateInfo,
  QuestValidationResult,
} from '../../types/questBuilder'
import type { CreatureSearchResult } from '../../types/questBuilder'
import type { ItemSearchResult } from '../../types'
import type { StepKey } from './stepSelection'

const PHASES: { id: JourneyPhaseId; label: string; num: string }[] = [
  { id: 'start', label: 'Start — talk to NPC', num: '1' },
  { id: 'obtainItem', label: 'Obtain item', num: '2' },
  { id: 'turnIn', label: 'Turn in', num: '3' },
]

type PickerTarget = 'creature' | 'item' | 'landscapeObject' | 'reward' | 'npcClone' | null

export default function QuestBuilder() {
  const [journey, setJourney] = useState<QuestJourney | null>(null)
  const [templates, setTemplates] = useState<QuestTemplateInfo[]>([])
  const [selectedTemplate, setSelectedTemplate] = useState('kill_turnin')
  const [activePhase, setActivePhase] = useState<JourneyPhaseId>('start')
  const [selectedKey, setSelectedKey] = useState<StepKey | null>(null)
  const [validation, setValidation] = useState<QuestValidationResult | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [nextWcid, setNextWcid] = useState<number | null>(null)
  const [picker, setPicker] = useState<PickerTarget>(null)
  const [showAdvanced, setShowAdvanced] = useState(false)
  const [importNpcWcid, setImportNpcWcid] = useState('78780090')
  const [importStamp, setImportStamp] = useState('')
  const [updateOnlyExport, setUpdateOnlyExport] = useState(false)
  const [importNotice, setImportNotice] = useState<string | null>(null)
  const stepRefs = useRef<Record<string, HTMLDivElement | null>>({})
  const weenieLabels = useJourneyWeenieLabels(journey)

  const [serverCapabilities, setServerCapabilities] = useState<{
    importNpc?: boolean
  } | null>(null)

  useEffect(() => {
    api.get<QuestTemplateInfo[]>('/api/quest-builder/templates').then((t) => setTemplates(t ?? []))
    api.get<{ wcid: number }>('/api/quest-builder/next-wcid').then((r) => setNextWcid(r?.wcid ?? null))
    api
      .get<{ importNpc?: boolean }>('/api/quest-builder/ping')
      .then((c) => setServerCapabilities(c ?? {}))
      .catch(() => setServerCapabilities(null))
  }, [])

  const loadTemplate = useCallback(async (id: string) => {
    setBusy(true)
    setError(null)
    try {
      const data = await api.get<QuestPackage>(`/api/quest-builder/template/${id}`)
      if (data) {
        setJourney(packageToJourney(data))
        setActivePhase('start')
        setSelectedKey(null)
        setValidation(null)
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load template')
    } finally {
      setBusy(false)
    }
  }, [])

  useEffect(() => {
    if (!journey) loadTemplate(selectedTemplate)
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  const updateJourney = (patch: Partial<QuestJourney> | ((j: QuestJourney) => QuestJourney)) => {
    setJourney((prev) => {
      if (!prev) return prev
      const next = typeof patch === 'function' ? patch(prev) : { ...prev, ...patch }
      return next
    })
    setValidation(null)
  }

  const validate = async () => {
    if (!journey) return
    setBusy(true)
    setError(null)
    try {
      const pkg = journeyToPackage(journey)
      const result = await api.post<QuestValidationResult>('/api/quest-builder/validate', pkg)
      setValidation(result)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Validation failed')
    } finally {
      setBusy(false)
    }
  }

  const exportZip = async () => {
    if (!journey) return
    setBusy(true)
    setError(null)
    try {
      const pkg = journeyToPackage(journey)
      const q = updateOnlyExport ? '?updateOnly=true' : ''
      await api.postBlob(`/api/quest-builder/export${q}`, pkg, `${journey.meta.package || 'quest'}.zip`)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Export failed')
    } finally {
      setBusy(false)
    }
  }

  const loadFromDatabase = async (by: 'npc' | 'stamp') => {
    setBusy(true)
    setError(null)
    setImportNotice(null)
    try {
      let result: QuestImportResult | null = null
      if (by === 'npc') {
        const wcid = parseInt(importNpcWcid, 10)
        if (!wcid) throw new Error('Enter a valid quest giver WCID.')
        result = await api.get<QuestImportResult>(`/api/quest-builder/import/npc/${wcid}?includeRelated=true`)
      } else {
        const name = importStamp.trim()
        if (!name) throw new Error('Enter a quest stamp name.')
        result = await api.get<QuestImportResult>(
          `/api/quest-builder/import/stamp?name=${encodeURIComponent(name)}`
        )
      }
      if (!result?.package) throw new Error(result?.message ?? 'Import returned no package.')
      setJourney(packageToJourney(result.package))
      setActivePhase('start')
      setSelectedKey(null)
      setValidation(null)
      setUpdateOnlyExport(true)
      const warn = result.warnings?.length ? ` Warnings: ${result.warnings.join(' ')}` : ''
      setImportNotice(`${result.message}${warn}`)
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Import failed'
      setError(
        msg.includes('404')
          ? `${msg} — If Quest Builder otherwise works, rebuild and restart ACE.Server. If you already did, WCID may be missing from the DB this portal uses.`
          : msg
      )
    } finally {
      setBusy(false)
    }
  }

  const loadPackageJson = async () => {
    if (!journey) return
    setBusy(true)
    setError(null)
    try {
      const pkg = journeyToPackage(journey)
      const result = await api.post<QuestImportResult>('/api/quest-builder/import/package', pkg)
      if (result?.package) {
        setJourney(packageToJourney(result.package))
        setImportNotice(result.message ?? 'Reloaded from package JSON.')
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load package JSON')
    } finally {
      setBusy(false)
    }
  }

  if (!journey) {
    return (
      <div className="p-8">
        {error ? (
          <div className="rounded-lg bg-red-950/50 border border-red-800 text-red-200 text-sm p-3 space-y-3">
            <div className="flex items-center gap-2">
              <AlertCircle className="w-4 h-4 shrink-0" />
              <span>{error}</span>
            </div>
            <button
              type="button"
              onClick={() => loadTemplate(selectedTemplate)}
              className="px-3 py-1.5 rounded-lg bg-neutral-800 text-white hover:bg-neutral-700"
            >
              Retry
            </button>
          </div>
        ) : (
          <div className="text-neutral-400 flex items-center gap-2">
            <RefreshCw className="w-4 h-4 animate-spin" /> Loading quest builder…
          </div>
        )}
      </div>
    )
  }

  const previewPkg = journeyToPackage(journey)

  return (
    <div className="flex flex-col h-full min-h-0">
      <div className="px-6 pt-4 shrink-0">
        <PageHeader title="Quest Builder" icon={GitBranch} />
        <p className="text-xs text-neutral-500 -mt-4 mb-2">
          Build in player order, simulate, then export SQL. Import an existing quest by NPC WCID to edit without starting over.
        </p>
      </div>

      <div className="mx-6 mb-3 p-3 rounded-lg border border-violet-900/50 bg-violet-950/20 shrink-0">
        <div className="text-[10px] uppercase font-bold text-violet-300 mb-2">Import existing quest</div>
        {serverCapabilities === null && (
          <p className="text-[10px] text-amber-300/90 mb-2">
            Server missing import API — rebuild Release <span className="font-mono">ACE.Server</span>,{' '}
            <span className="font-mono">npm run build</span> in ClientApp, restart. Open{' '}
            <span className="font-mono">/api/quest-builder/ping</span> in the browser (no login) to verify.
          </p>
        )}
        <div className="flex flex-wrap gap-2 items-end">
          <label className="text-[10px] text-neutral-500">
            Quest giver WCID
            <input
              value={importNpcWcid}
              onChange={(e) => setImportNpcWcid(e.target.value)}
              className="block mt-0.5 w-28 bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-sm text-white font-mono"
            />
          </label>
          <button
            type="button"
            disabled={busy}
            onClick={() => loadFromDatabase('npc')}
            className="px-3 py-1.5 rounded-lg bg-violet-700 text-sm text-white hover:bg-violet-600 flex items-center gap-1"
          >
            <Database className="w-3.5 h-3.5" /> Load from DB
          </button>
          <label className="text-[10px] text-neutral-500">
            Or stamp name
            <input
              value={importStamp}
              onChange={(e) => setImportStamp(e.target.value)}
              placeholder="schneebly_…_started"
              className="block mt-0.5 w-48 bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-xs text-white font-mono"
            />
          </label>
          <button
            type="button"
            disabled={busy}
            onClick={() => loadFromDatabase('stamp')}
            className="px-3 py-1.5 rounded-lg bg-neutral-800 text-sm text-white hover:bg-neutral-700"
          >
            Find by stamp
          </button>
        </div>
        {importNotice && <p className="text-[10px] text-violet-200/80 mt-2">{importNotice}</p>}
      </div>

      <div className="flex flex-wrap gap-2 px-6 pb-4 border-b border-neutral-800 shrink-0">
        <select
          value={selectedTemplate}
          onChange={(e) => setSelectedTemplate(e.target.value)}
          className="bg-neutral-900 border border-neutral-700 rounded-lg px-3 py-2 text-sm text-white"
        >
          {templates.map((t) => (
            <option key={t.id} value={t.id}>
              {t.label}
            </option>
          ))}
        </select>
        <button
          type="button"
          disabled={busy}
          onClick={() => loadTemplate(selectedTemplate)}
          className="px-3 py-2 rounded-lg bg-neutral-800 text-sm text-white hover:bg-neutral-700"
        >
          Load template
        </button>
        {nextWcid != null && (
          <span className="text-xs text-neutral-500 self-center px-2">Next WCID: {nextWcid}</span>
        )}
        <div className="flex-1" />
        <button type="button" disabled={busy} onClick={validate} className="px-3 py-2 rounded-lg bg-neutral-800 text-sm text-white hover:bg-neutral-700">
          Validate
        </button>
        <label className="flex items-center gap-2 text-xs text-neutral-400 self-center px-1">
          <input
            type="checkbox"
            checked={updateOnlyExport}
            onChange={(e) => setUpdateOnlyExport(e.target.checked)}
          />
          Update export (quests + emotes only)
        </label>
        <button
          type="button"
          disabled={busy}
          onClick={exportZip}
          className="px-3 py-2 rounded-lg bg-blue-600 text-sm text-white hover:bg-blue-500 flex items-center gap-2"
        >
          <Download className="w-4 h-4" /> Export ZIP
        </button>
      </div>

      {error && (
        <div className="mx-6 mt-4 p-3 rounded-lg bg-red-950/50 border border-red-800 text-red-200 text-sm flex gap-2 shrink-0">
          <AlertCircle className="w-4 h-4 shrink-0" /> {error}
        </div>
      )}

      {validation && (
        <div className="mx-6 mt-4 space-y-1 max-h-28 overflow-y-auto shrink-0">
          {validation.issues.map((issue, i) => (
            <div
              key={i}
              className={`text-xs px-3 py-1.5 rounded flex gap-2 ${
                issue.severity === 'error' ? 'bg-red-950/40 text-red-300' : 'bg-amber-950/40 text-amber-200'
              }`}
            >
              {issue.severity === 'error' ? <AlertCircle className="w-3 h-3" /> : <CheckCircle2 className="w-3 h-3" />}
              {issue.message}
            </div>
          ))}
          {validation.ok && validation.issues.length === 0 && (
            <div className="text-xs text-green-400 px-3">No issues — ready to export.</div>
          )}
        </div>
      )}

      <div className="flex flex-1 min-h-0 overflow-hidden px-4 pb-4 gap-4">
        <aside className="w-72 shrink-0 flex flex-col gap-2 overflow-y-auto py-2 border-r border-neutral-800 pr-4">
          <label className="text-[10px] uppercase text-neutral-500 font-bold px-1">Package</label>
          <input
            value={journey.meta.package}
            onChange={(e) => updateJourney({ meta: { ...journey.meta, package: e.target.value } })}
            className="w-full bg-neutral-900 border border-neutral-700 rounded px-2 py-1.5 text-sm text-white"
          />
          <div className="rounded border border-neutral-800 bg-neutral-950/40 p-2 space-y-1 text-[10px]">
            <div className="text-neutral-500 font-bold uppercase">Stamps</div>
            <div className="text-neutral-400">
              Turn-in: <span className="font-mono text-blue-300">{journey.meta.completionStamp.name}</span>
            </div>
            {journey.meta.startStamp && journey.start.grantStartStampOnUse ? (
              <div className="text-neutral-400">
                Start: <span className="font-mono text-violet-300">{journey.meta.startStamp.name}</span>
              </div>
            ) : (
              <div className="text-neutral-600">Start: none (talk is flavor only)</div>
            )}
            {journey.meta.pickupStamp ? (
              <div className="text-neutral-400">
                Pickup: <span className="font-mono text-emerald-300">{journey.meta.pickupStamp.name}</span>
              </div>
            ) : (
              <div className="text-neutral-600">Pickup: none (corpse or ungated object)</div>
            )}
          </div>

          <QuestTimingHelp
            hasPickupStamp={journey.meta.pickupStamp != null}
            hasStartStamp={journey.start.grantStartStampOnUse && journey.meta.startStamp != null}
          />

          <label className="text-[10px] uppercase text-neutral-500 font-bold px-1 mt-4">Journey</label>
          {PHASES.map((p) => (
            <button
              key={p.id}
              type="button"
              onClick={() => {
                setActivePhase(p.id)
                setSelectedKey(null)
              }}
              className={`w-full text-left rounded-lg border p-3 transition-colors ${
                activePhase === p.id
                  ? 'border-blue-500 bg-blue-950/40'
                  : 'border-neutral-800 bg-neutral-900/60 hover:border-neutral-600'
              }`}
            >
              <div className="text-[10px] font-bold text-neutral-500">{p.num}</div>
              <div className="text-sm font-medium text-white mt-0.5">{p.label}</div>
              <div className="text-[10px] text-neutral-500 mt-1 line-clamp-2">
                {journeyPhaseSummary(journey, p.id, weenieLabels)}
              </div>
            </button>
          ))}

          <button
            type="button"
            onClick={() => setShowAdvanced((v) => !v)}
            className="mt-4 flex items-center gap-1 text-[10px] text-neutral-500 hover:text-neutral-300 px-1"
          >
            {showAdvanced ? <ChevronDown className="w-3 h-3" /> : <ChevronRight className="w-3 h-3" />}
            Advanced (raw package JSON)
          </button>
          {showAdvanced && (
            <div className="space-y-2">
              <button
                type="button"
                onClick={loadPackageJson}
                className="text-[10px] text-blue-400 hover:text-blue-300"
              >
                Apply package JSON below → journey
              </button>
              <pre className="text-[9px] text-neutral-500 bg-neutral-950 border border-neutral-800 rounded p-2 overflow-auto max-h-48">
                {JSON.stringify(previewPkg, null, 2)}
              </pre>
            </div>
          )}
        </aside>

        <main className="flex-1 min-w-0 overflow-y-auto py-2 px-2">
          <h2 className="text-lg font-semibold text-white mb-1">
            {PHASES.find((p) => p.id === activePhase)?.label}
          </h2>
          <p className="text-xs text-neutral-500 mb-4">{journeyPhaseSummary(journey, activePhase, weenieLabels)}</p>
          {activePhase === 'start' && (
            <StartPhaseEditor
              start={journey.start}
              packageSlug={journey.meta.package}
              startStamp={journey.meta.startStamp}
              grantStartStampOnUse={journey.start.grantStartStampOnUse}
              onStartStampChange={(startStamp) => updateJourney({ meta: { ...journey.meta, startStamp } })}
              onGrantStartStampChange={(grantStartStampOnUse) =>
                updateJourney({ start: { ...journey.start, grantStartStampOnUse } })
              }
              completionStampName={journey.meta.completionStamp.name}
              weenieLabels={weenieLabels}
              onChange={(patch) => updateJourney({ start: { ...journey.start, ...patch } })}
              onPickNpcCloneTemplate={() => setPicker('npcClone')}
              selectedKey={selectedKey}
              onSelectKey={setSelectedKey}
              stepRefs={stepRefs}
            />
          )}
          {activePhase === 'obtainItem' && (
            <ObtainPhaseEditor
              obtain={journey.obtainItem}
              packageSlug={journey.meta.package}
              pickupStamp={journey.meta.pickupStamp}
              startStamp={journey.meta.startStamp}
              grantStartStampOnUse={journey.start.grantStartStampOnUse}
              npcName={journey.start.npcName}
              weenieLabels={weenieLabels}
              onChange={(patch) => updateJourney({ obtainItem: { ...journey.obtainItem, ...patch } })}
              onPickupStampChange={(pickupStamp) => updateJourney({ meta: { ...journey.meta, pickupStamp } })}
              onPickItemTemplate={() => setPicker('item')}
              onPickCreatureTemplate={() => setPicker('creature')}
              onPickObjectTemplate={() => setPicker('landscapeObject')}
            />
          )}
          {activePhase === 'turnIn' && (
            <TurnInPhaseEditor
              turnIn={journey.turnIn}
              itemWcid={journey.obtainItem.itemWcid}
              itemName={journey.obtainItem.itemName}
              completionStamp={journey.meta.completionStamp}
              weenieLabels={weenieLabels}
              onChange={(patch) => updateJourney({ turnIn: { ...journey.turnIn, ...patch } })}
              onCompletionStampChange={(patch) =>
                updateJourney({ meta: { ...journey.meta, completionStamp: { ...journey.meta.completionStamp, ...patch } } })
              }
              onPickRewardTemplate={() => setPicker('reward')}
              selectedKey={selectedKey}
              onSelectKey={setSelectedKey}
              stepRefs={stepRefs}
            />
          )}
        </main>

        <div className="w-80 shrink-0 min-h-0 hidden xl:flex flex-col">
          <QuestFlowSimulator journey={journey} weenieLabels={weenieLabels} />
        </div>
      </div>

      <div className="xl:hidden shrink-0 border-t border-neutral-800 mx-4 mb-4 h-72 min-h-48">
        <QuestFlowSimulator journey={journey} weenieLabels={weenieLabels} />
      </div>

      <CreatureTemplatePickerModal
        isOpen={picker === 'creature' || picker === 'npcClone'}
        onClose={() => setPicker(null)}
        title={picker === 'npcClone' ? 'Pick NPC template to clone' : undefined}
        description={
          picker === 'npcClone'
            ? 'Search by display name, classname, or WCID (e.g. town crier, 78780020).'
            : undefined
        }
        currentWcid={
          picker === 'npcClone'
            ? journey.start.npcCloneFromWcid
            : journey.obtainItem.source.kind === 'corpse'
              ? journey.obtainItem.source.creature.templateWcid
              : undefined
        }
        onSelect={(picked: CreatureSearchResult) => {
          seedWeenieLabel(picked.wcid, picked.name)
          if (picker === 'npcClone') {
            updateJourney({ start: { ...journey.start, npcCloneFromWcid: picked.wcid } })
            return
          }
          if (journey.obtainItem.source.kind !== 'corpse') return
          updateJourney({
            obtainItem: {
              ...journey.obtainItem,
              source: {
                kind: 'corpse',
                creature: { ...journey.obtainItem.source.creature, templateWcid: picked.wcid },
              },
            },
          })
        }}
      />

      <ItemTemplatePickerModal
        isOpen={picker === 'item'}
        onClose={() => setPicker(null)}
        title="Pick quest item template"
        currentWcid={journey.obtainItem.itemCloneFromWcid}
        onSelect={(picked: ItemSearchResult) => {
          seedWeenieLabel(picked.wcid, picked.name)
          updateJourney({
            obtainItem: {
              ...journey.obtainItem,
              itemCloneFromWcid: picked.wcid,
              itemName: journey.obtainItem.itemName || picked.name,
            },
          })
        }}
      />

      <ItemTemplatePickerModal
        isOpen={picker === 'reward'}
        onClose={() => setPicker(null)}
        title="Pick reward item"
        description="Search for the item the NPC gives on successful turn-in (e.g. enlightened coins)."
        currentWcid={journey.turnIn.rewardWcid}
        onSelect={(picked: ItemSearchResult) => {
          seedWeenieLabel(picked.wcid, picked.name)
          updateJourney({
            turnIn: {
              ...journey.turnIn,
              rewardWcid: picked.wcid,
              rewardStack: journey.turnIn.rewardStack || 1,
            },
          })
        }}
      />

      <ItemTemplatePickerModal
        isOpen={picker === 'landscapeObject'}
        onClose={() => setPicker(null)}
        title="Pick world object template"
        description="Choose a generic object or usable to clone for the landscape pickup (not creatures)."
        currentWcid={
          journey.obtainItem.source.kind === 'landscape' ? journey.obtainItem.source.objectCloneFromWcid : undefined
        }
        onSelect={(picked: ItemSearchResult) => {
          if (journey.obtainItem.source.kind !== 'landscape') return
          seedWeenieLabel(picked.wcid, picked.name)
          updateJourney({
            obtainItem: {
              ...journey.obtainItem,
              source: {
                ...journey.obtainItem.source,
                objectCloneFromWcid: picked.wcid,
                objectName: journey.obtainItem.source.objectName || picked.name,
              },
            },
          })
        }}
      />
    </div>
  )
}
