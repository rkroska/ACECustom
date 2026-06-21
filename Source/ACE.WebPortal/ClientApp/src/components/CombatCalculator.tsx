import { useCallback, useEffect, useMemo, useState } from 'react'
import { Swords, ChevronDown, ChevronUp } from 'lucide-react'
import { api } from '../services/api'
import { useDebounce } from '../hooks/useDebounce'
import { useAuthStore } from '../store/useAuthStore'
import PageHeader from './common/PageHeader'
import {
  type CombatDirection,
  type CombatMode,
  type CombatPreviewRequest,
  type CombatPreviewResponse,
  type CombatConfig,
  type PlayerStub,
  type WeenieCombat,
  type WeenieSearchResult,
} from '../types/combat'
import {
  CONTEST_TYPE_OPTIONS,
  MODE_CONFIG,
  buildDiscordEvadeSummary,
  buildDiscordRangeTable,
  fmt,
  fmtPct,
  asNumberOrUndefined,
  appraisalBonusPctToDefenseMod,
  defenseModToAppraisalBonusPct,
  tripletWithDeltas,
  skillRole,
  skillLabel,
} from '../utils/combatCalc'

function applyConfigToMode(cfg: CombatConfig | null, mode: CombatMode) {
  const base = MODE_CONFIG[mode]
  if (!cfg) return base
  const live = cfg[mode]
  return { ...base, serverDefaultAgg: live.defaultAggression }
}

function displayedSkill(overrideVal: string, previewBase?: number) {
  if (overrideVal !== '') return overrideVal
  if (previewBase != null) return String(previewBase)
  return ''
}

function syncModFromResponse(value: number | undefined, setter: (v: string) => void) {
  if (value != null && Number.isFinite(value)) setter(String(value))
}

function syncIntFromResponse(value: number | undefined, setter: (v: string) => void) {
  if (value != null && Number.isFinite(value)) setter(String(Math.round(value)))
}

export default function CombatCalculator() {
  const { canAccessPage } = useAuthStore()
  const isPlayerAdmin = canAccessPage('players')
  const [myCharacters, setMyCharacters] = useState<PlayerStub[]>([])

  const [config, setConfig] = useState<CombatConfig | null>(null)
  const [mode, setMode] = useState<CombatMode>('missile')
  const [direction, setDirection] = useState<CombatDirection>('playerAttacksMonster')

  const [playerQuery, setPlayerQuery] = useState('')
  const [monsterQuery, setMonsterQuery] = useState('')
  const debouncedPlayerQuery = useDebounce(playerQuery, 400)
  const debouncedMonsterQuery = useDebounce(monsterQuery, 400)

  const [playerResults, setPlayerResults] = useState<PlayerStub[]>([])
  const [monsterResults, setMonsterResults] = useState<WeenieSearchResult[]>([])
  const [selectedPlayer, setSelectedPlayer] = useState<PlayerStub | null>(null)
  const [selectedMonster, setSelectedMonster] = useState<WeenieSearchResult | null>(null)
  const [weenieDetail, setWeenieDetail] = useState<WeenieCombat | null>(null)

  const [playerAccuracyMod, setPlayerAccuracyMod] = useState('1')
  const [playerOffenseMod, setPlayerOffenseMod] = useState('1')
  const [playerDefenseMod, setPlayerDefenseMod] = useState('1')
  const [playerDefenseFlat, setPlayerDefenseFlat] = useState('0')
  const [monsterOffenseMod, setMonsterOffenseMod] = useState('1')
  const [playerAccuracyModDirty, setPlayerAccuracyModDirty] = useState(false)
  const [playerOffenseModDirty, setPlayerOffenseModDirty] = useState(false)
  const [playerDefenseModDirty, setPlayerDefenseModDirty] = useState(false)
  const [playerDefenseFlatDirty, setPlayerDefenseFlatDirty] = useState(false)
  const [monsterOffenseModDirty, setMonsterOffenseModDirty] = useState(false)
  const [overridePlayerAttack, setOverridePlayerAttack] = useState('')
  const [overridePlayerDefense, setOverridePlayerDefense] = useState('')
  const [overrideMonsterAttack, setOverrideMonsterAttack] = useState('')
  const [overrideMonsterDefense, setOverrideMonsterDefense] = useState('')
  const [testAggression, setTestAggression] = useState('')
  const [rangeMin, setRangeMin] = useState('1000')
  const [rangeMax, setRangeMax] = useState('2500')
  const [rangeStep, setRangeStep] = useState('100')

  const [preview, setPreview] = useState<CombatPreviewResponse | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [copyStatus, setCopyStatus] = useState('')
  const [showDiscordPreview, setShowDiscordPreview] = useState(false)
  const [showMonsterSkills, setShowMonsterSkills] = useState(false)

  const clearPlayerModDirtyFlags = () => {
    setPlayerAccuracyModDirty(false)
    setPlayerOffenseModDirty(false)
    setPlayerDefenseModDirty(false)
    setPlayerDefenseFlatDirty(false)
  }

  const activeCfg = useMemo(() => applyConfigToMode(config, mode), [config, mode])
  const scalingEnabled = config?.[mode]?.enabled ?? false
  const contestMeta = CONTEST_TYPE_OPTIONS.find((o) => o.value === mode)
  const contestTypeLabel =
    direction === 'playerAttacksMonster'
      ? contestMeta?.labelWhenAttacking
      : contestMeta?.labelWhenDefending

  useEffect(() => {
    api.get<CombatConfig>('/api/combat/config').then((c) => {
      if (c) {
        setConfig(c)
        setTestAggression(String(c.missile.defaultAggression))
      }
    })
  }, [])

  useEffect(() => {
    if (!isPlayerAdmin) {
      api.get<PlayerStub[]>('/api/character/list').then((list) => {
        setMyCharacters(list ?? [])
      })
    }
  }, [isPlayerAdmin])

  useEffect(() => {
    if (!config) return
    setTestAggression(String(config[mode].defaultAggression))
  }, [mode, config])

  useEffect(() => {
    const run = async () => {
      const q = debouncedPlayerQuery.trim().toLowerCase()
      if (q.length < 2) {
        setPlayerResults([])
        return
      }

      if (!isPlayerAdmin) {
        const filtered = myCharacters.filter((c) =>
          c.name.toLowerCase().includes(q) ||
          c.guid.toString().includes(q)
        )
        setPlayerResults(filtered)
        return
      }

      if (/^\d+$/.test(q)) {
        const stub = await api.get<PlayerStub>(`/api/character/lookup/${q}`)
        setPlayerResults(stub ? [stub] : [])
        return
      }
      if (debouncedPlayerQuery.length >= 3) {
        const data = await api.get<PlayerStub[]>(`/api/character/search-all/${encodeURIComponent(debouncedPlayerQuery)}`)
        setPlayerResults(data ?? [])
      }
    }
    run()
  }, [debouncedPlayerQuery, isPlayerAdmin, myCharacters])

  useEffect(() => {
    const run = async () => {
      if (debouncedMonsterQuery.length < 2) {
        setMonsterResults([])
        return
      }
      const data = await api.get<WeenieSearchResult[]>(`/api/combat/weenie/search?q=${encodeURIComponent(debouncedMonsterQuery)}`)
      setMonsterResults(data ?? [])
    }
    run()
  }, [debouncedMonsterQuery])

  useEffect(() => {
    if (!selectedMonster) {
      setWeenieDetail(null)
      return
    }
    api.get<WeenieCombat>(`/api/combat/weenie/${selectedMonster.wcid}`).then((w) => setWeenieDetail(w ?? null))
  }, [selectedMonster])

  const runPreview = useCallback(async (useFormValues = false) => {
    setLoading(true)
    setError(null)
    try {
      const sendMod = (dirty: boolean, value: string) =>
        useFormValues || !selectedPlayer || dirty ? asNumberOrUndefined(value) : undefined
      const sendMonsterMod = (dirty: boolean, value: string) =>
        useFormValues || !selectedMonster || dirty ? asNumberOrUndefined(value) : undefined
      const sendOverride = (value: string) =>
        value.trim() === '' ? undefined : asNumberOrUndefined(value)

      const body: CombatPreviewRequest = {
        playerGuid: selectedPlayer?.guid,
        monsterWcid: selectedMonster?.wcid,
        mode,
        direction,
        playerAccuracyMod: sendMod(playerAccuracyModDirty, playerAccuracyMod),
        playerOffenseMod: sendMod(playerOffenseModDirty, playerOffenseMod),
        playerDefenseMod: sendMod(playerDefenseModDirty, playerDefenseMod),
        playerDefenseFlat: sendMod(playerDefenseFlatDirty, playerDefenseFlat),
        monsterOffenseMod: sendMonsterMod(monsterOffenseModDirty, monsterOffenseMod),
        overridePlayerAttack: sendOverride(overridePlayerAttack),
        overridePlayerDefense: sendOverride(overridePlayerDefense),
        overrideMonsterAttack: sendOverride(overrideMonsterAttack),
        overrideMonsterDefense: sendOverride(overrideMonsterDefense),
        testAggression: asNumberOrUndefined(testAggression),
        rangeMin: asNumberOrUndefined(rangeMin),
        rangeMax: asNumberOrUndefined(rangeMax),
        rangeStep: asNumberOrUndefined(rangeStep),
      }
      const res = await api.post<CombatPreviewResponse>('/api/combat/preview', body)
      if (!res) {
        setError('Preview failed — no response from server.')
        setPreview(null)
        return
      }
      setPreview(res)
      syncModFromResponse(res.playerAccuracyMod, setPlayerAccuracyMod)
      syncModFromResponse(res.playerOffenseMod, setPlayerOffenseMod)
      syncModFromResponse(res.playerDefenseMod, setPlayerDefenseMod)
      syncIntFromResponse(res.playerDefenseFlat, setPlayerDefenseFlat)
      syncModFromResponse(res.monsterOffenseMod, setMonsterOffenseMod)
      clearPlayerModDirtyFlags()
      setMonsterOffenseModDirty(false)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Preview request failed.')
      setPreview(null)
    } finally {
      setLoading(false)
    }
  }, [
    selectedPlayer,
    selectedMonster,
    mode,
    direction,
    playerAccuracyMod,
    playerOffenseMod,
    playerDefenseMod,
    playerDefenseFlat,
    monsterOffenseMod,
    playerAccuracyModDirty,
    playerOffenseModDirty,
    playerDefenseModDirty,
    playerDefenseFlatDirty,
    monsterOffenseModDirty,
    overridePlayerAttack,
    overridePlayerDefense,
    overrideMonsterAttack,
    overrideMonsterDefense,
    testAggression,
    rangeMin,
    rangeMax,
    rangeStep,
    preview,
  ])

  useEffect(() => {
    if (selectedPlayer || selectedMonster) runPreview(false)
  }, [selectedPlayer, selectedMonster, mode, direction])

  const playerDefenseBaseDisplay = asNumberOrUndefined(
    displayedSkill(overridePlayerDefense, preview?.playerDefenseBase)
  )
  const localEffectivePlayerDefense = useMemo(() => {
    if (playerDefenseBaseDisplay == null) return undefined
    const mod = asNumberOrUndefined(playerDefenseMod) ?? 1
    const flat = asNumberOrUndefined(playerDefenseFlat) ?? 0
    return Math.round(playerDefenseBaseDisplay * mod + flat)
  }, [playerDefenseBaseDisplay, playerDefenseMod, playerDefenseFlat])

  const defenseInputsDirty =
    playerDefenseModDirty || playerDefenseFlatDirty || overridePlayerDefense !== ''
  const displayedEffectivePlayerDefense = defenseInputsDirty
    ? localEffectivePlayerDefense
    : (preview?.effectivePlayerDefense ?? localEffectivePlayerDefense)

  const playerDefenseBonusPct = useMemo(() => {
    const mod = asNumberOrUndefined(playerDefenseMod)
    if (mod == null) return ''
    return String(defenseModToAppraisalBonusPct(mod))
  }, [playerDefenseMod])

  const playerDefenseModTotal = asNumberOrUndefined(playerDefenseMod)

  const atk = preview?.attackSkill ?? 0
  const def =
    direction === 'monsterAttacksPlayer'
      ? (displayedEffectivePlayerDefense ?? preview?.effectivePlayerDefense ?? preview?.defenseSkill ?? 0)
      : (preview?.defenseSkill ?? 0)
  const testAgg = preview?.testAggression ?? asNumberOrUndefined(testAggression) ?? activeCfg.serverDefaultAgg

  const triplet = useMemo(
    () => (preview?.triplet ? tripletWithDeltas(preview.triplet) : null),
    [preview]
  )

  const rangeRows = preview?.rangeRows ?? []

  const sweepColumnShort = direction === 'playerAttacksMonster' ? activeCfg.playerAtkShort : activeCfg.playerDefShort

  const discordFull = useMemo(() => {
    if (!triplet || !preview) return ''
    const summary = buildDiscordEvadeSummary({
      cfg: activeCfg,
      direction,
      attackSkill: atk,
      defenseSkill: def,
      testAgg,
      triplet,
    })
    if (!rangeRows.length) return summary
    const lo = Math.min(asNumberOrUndefined(rangeMin) ?? 0, asNumberOrUndefined(rangeMax) ?? 0)
    const hi = Math.max(asNumberOrUndefined(rangeMin) ?? 0, asNumberOrUndefined(rangeMax) ?? 0)
    const table = buildDiscordRangeTable({
      cfg: activeCfg,
      direction,
      fixedAtk: atk,
      fixedDef: def,
      serverDefaultAgg: activeCfg.serverDefaultAgg,
      testAgg,
      sweepMin: lo,
      sweepMax: hi,
      step: Math.max(1, asNumberOrUndefined(rangeStep) ?? 100),
      sweepColumnShort,
      rows: rangeRows,
    })
    return `${summary}\n\n${table}`
  }, [triplet, preview, activeCfg, direction, atk, def, testAgg, rangeRows, rangeMin, rangeMax, rangeStep, sweepColumnShort])

  const copyDiscord = async () => {
    if (!discordFull) return
    try {
      await navigator.clipboard.writeText(discordFull)
      setCopyStatus('Copied Discord block')
    } catch {
      setCopyStatus('Copy failed')
    }
  }

  const setRangeAround = (center: number, pct: number, stepSmall: number, stepLarge: number) => {
    const c = Math.max(0, center)
    setRangeMin(String(Math.max(0, Math.round(c * (1 - pct)))))
    setRangeMax(String(Math.round(c * (1 + pct))))
    setRangeStep(String(c >= 10_000 ? stepLarge : stepSmall))
  }

  return (
    <div className="flex-1 overflow-y-auto custom-scrollbar p-8 space-y-6">
      <PageHeader title="Combat Calculator" icon={Swords} />

      <p className="text-neutral-400 text-sm max-w-3xl">
        Admin tool: compare attack vs defense using live server SkillCheck math and defense_scaling config.
        Enter skills manually below, or search for a player / creature to auto-fill.
      </p>

      {config && (
        <div className="flex flex-wrap gap-2 text-xs">
          <span className={`px-2 py-1 rounded border ${config.melee.enabled ? 'border-green-700 text-green-400' : 'border-neutral-700 text-neutral-500'}`}>
            Melee scaling {config.melee.enabled ? 'ON' : 'OFF'}
          </span>
          <span className={`px-2 py-1 rounded border ${config.missile.enabled ? 'border-green-700 text-green-400' : 'border-neutral-700 text-neutral-500'}`}>
            Missile scaling {config.missile.enabled ? 'ON' : 'OFF'}
          </span>
          <span className={`px-2 py-1 rounded border ${config.magic.enabled ? 'border-green-700 text-green-400' : 'border-neutral-700 text-neutral-500'}`}>
            Magic scaling {config.magic.enabled ? 'ON' : 'OFF'}
          </span>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <div className="bg-neutral-900 border border-neutral-800 rounded-xl p-4 space-y-3">
          <h3 className="text-sm font-bold text-white uppercase tracking-wider">1 · Player <span className="text-neutral-500 font-normal normal-case">(optional)</span></h3>
          <input
            className="w-full bg-neutral-950 border border-neutral-700 rounded-lg px-3 py-2 text-sm"
            placeholder="Name or character id"
            value={playerQuery}
            onChange={(e) => setPlayerQuery(e.target.value)}
          />
          <div className="space-y-2 max-h-48 overflow-y-auto">
            {playerResults.map((p) => (
              <div key={p.guid} className="flex items-center justify-between gap-2 text-sm">
                <div>
                  <div className="font-medium text-white">{p.name}</div>
                  <div className="text-neutral-500 text-xs">GUID {p.guid}</div>
                </div>
                <div className="flex items-center gap-2">
                  <span
                    className={`text-xs px-2 py-0.5 rounded ${p.isOnline ? 'bg-green-900/40 text-green-400' : 'bg-neutral-800 text-neutral-400'}`}
                  >
                    {p.isOnline ? 'Online' : 'Offline'}
                  </span>
                  <button
                    type="button"
                    className="px-3 py-1 bg-blue-600 hover:bg-blue-500 rounded text-xs font-medium"
                    onClick={() => {
                      setSelectedPlayer(p)
                      setPlayerQuery(p.name)
                      setOverridePlayerAttack('')
                      setOverridePlayerDefense('')
                      clearPlayerModDirtyFlags()
                    }}
                  >
                    Select
                  </button>
                </div>
              </div>
            ))}
          </div>
          {selectedPlayer && preview && (
            <p className="text-xs text-blue-400/80">
              Skills: {preview.skillSource}
              {preview.skillSource === 'offline-approximate' && (
                <span className="text-amber-400"> — offline values are approximate; use an online character for balance checks.</span>
              )}
            </p>
          )}
        </div>

        <div className="bg-neutral-900 border border-neutral-800 rounded-xl p-4 space-y-3">
          <h3 className="text-sm font-bold text-white uppercase tracking-wider">1 · Monster <span className="text-neutral-500 font-normal normal-case">(optional)</span></h3>
          <input
            className="w-full bg-neutral-950 border border-neutral-700 rounded-lg px-3 py-2 text-sm"
            placeholder="WCID or name"
            value={monsterQuery}
            onChange={(e) => setMonsterQuery(e.target.value)}
          />
          <div className="space-y-2 max-h-48 overflow-y-auto">
            {monsterResults.map((m) => (
              <div key={m.wcid} className="flex items-center justify-between gap-2 text-sm">
                <div>
                  <div className="font-medium text-white">{m.name}</div>
                  <div className="text-neutral-500 text-xs">
                    WCID {m.wcid} · {m.weenieType}
                  </div>
                </div>
                <button
                  type="button"
                  className="px-3 py-1 bg-blue-600 hover:bg-blue-500 rounded text-xs font-medium"
                  onClick={() => {
                    setSelectedMonster(m)
                    setMonsterQuery(m.name)
                    setOverrideMonsterAttack('')
                    setOverrideMonsterDefense('')
                    setMonsterOffenseModDirty(false)
                  }}
                >
                  Select
                </button>
              </div>
            ))}
          </div>
          {weenieDetail && (
            <button
              type="button"
              className="text-xs text-neutral-400 hover:text-white flex items-center gap-1"
              onClick={() => setShowMonsterSkills((v) => !v)}
            >
              {showMonsterSkills ? <ChevronUp className="w-3 h-3" /> : <ChevronDown className="w-3 h-3" />}
              Template skills ({weenieDetail.skills.length})
            </button>
          )}
          {showMonsterSkills && weenieDetail && (
            <div className="max-h-40 overflow-y-auto border border-neutral-800 rounded text-xs">
              <table className="w-full">
                <thead className="bg-neutral-950 text-neutral-500">
                  <tr>
                    <th className="text-left p-2">Role</th>
                    <th className="text-left p-2">Skill</th>
                    <th className="text-right p-2">Init</th>
                  </tr>
                </thead>
                <tbody>
                  {weenieDetail.skills.map((s) => (
                    <tr key={s.skillId} className="border-t border-neutral-800">
                      <td className="p-2 capitalize">{skillRole(s.skillId)}</td>
                      <td className="p-2">{s.name || skillLabel(s.skillId)}</td>
                      <td className="p-2 text-right">{fmt(s.initLevel)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      <div className="bg-neutral-900 border border-neutral-800 rounded-xl p-4 space-y-4">
        <h3 className="text-sm font-bold text-white uppercase tracking-wider">2 · Contest setup</h3>

        <div className="flex flex-wrap gap-4 items-end">
          <label className="text-sm">
            <span className="text-neutral-400 block mb-1">Direction</span>
            <select
              className="bg-neutral-950 border border-neutral-700 rounded px-2 py-1"
              value={direction}
              onChange={(e) => setDirection(e.target.value as CombatDirection)}
            >
              <option value="playerAttacksMonster">Player attacks monster</option>
              <option value="monsterAttacksPlayer">Player defends from monster</option>
            </select>
          </label>
          <label className="text-sm">
            <span className="text-neutral-400 block mb-1">Contest type</span>
            <select
              className="bg-neutral-950 border border-neutral-700 rounded px-2 py-1"
              value={mode}
              onChange={(e) => setMode(e.target.value as CombatMode)}
            >
              {CONTEST_TYPE_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {direction === 'playerAttacksMonster' ? o.labelWhenAttacking : o.labelWhenDefending}
                </option>
              ))}
            </select>
          </label>
          {isPlayerAdmin && (
            <label className="text-sm">
              <span className="text-neutral-400 block mb-1">Test aggression</span>
              <input
                className="bg-neutral-950 border border-neutral-700 rounded px-2 py-1 w-24"
                value={testAggression}
                onChange={(e) => setTestAggression(e.target.value)}
              />
            </label>
          )}
          <button
            type="button"
            className="px-4 py-2 bg-blue-600 hover:bg-blue-500 rounded text-sm disabled:opacity-50"
            disabled={loading}
            onClick={() => runPreview(true)}
          >
            {loading ? 'Calculating…' : 'Recalculate'}
          </button>
        </div>

        {contestMeta && (
          <p className="text-xs text-neutral-500">
            {direction === 'playerAttacksMonster'
              ? `Attacking with ${contestTypeLabel} → monster uses ${activeCfg.monsterDefShort} (skill ${activeCfg.defSkillId}). ${contestMeta.mobDefNote}`
              : `Defending with ${contestTypeLabel} → monster attacks; you use ${activeCfg.playerDefShort}.`}
          </p>
        )}

        {mode === 'magic' && MODE_CONFIG.magic.note && (
          <p className="text-xs text-amber-400/90 border border-amber-900/40 rounded p-2">{MODE_CONFIG.magic.note}</p>
        )}

        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div className="space-y-3">
            <h4 className="text-sm font-medium text-white">Player</h4>
            {direction === 'playerAttacksMonster' ? (
              <>
                <label className="text-xs text-neutral-500 block">Attack skill (base)</label>
                <input
                  className="w-full bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-sm"
                  value={displayedSkill(overridePlayerAttack, preview?.playerAttackBase)}
                  onChange={(e) => setOverridePlayerAttack(e.target.value)}
                  placeholder="Base attack skill"
                />
                <div className="flex flex-wrap gap-3 text-sm">
                  <label>
                    <span className="text-neutral-500 text-xs">accuracy</span>
                    <input
                      className="block w-20 mt-1 bg-neutral-950 border border-neutral-700 rounded px-2 py-1"
                      value={playerAccuracyMod}
                      onChange={(e) => {
                        setPlayerAccuracyModDirty(true)
                        setPlayerAccuracyMod(e.target.value)
                      }}
                    />
                  </label>
                  <label>
                    <span className="text-neutral-500 text-xs">offense</span>
                    <input
                      className="block w-20 mt-1 bg-neutral-950 border border-neutral-700 rounded px-2 py-1"
                      value={playerOffenseMod}
                      onChange={(e) => {
                        setPlayerOffenseModDirty(true)
                        setPlayerOffenseMod(e.target.value)
                      }}
                    />
                  </label>
                </div>
                <p className="text-xs text-neutral-400">
                  Effective {activeCfg.playerAtkShort}:{' '}
                  <span className="text-white font-medium">{fmt(preview?.effectivePlayerAttack)}</span>
                </p>
              </>
            ) : (
              <>
                <label className="text-xs text-neutral-500 block">Defense skill (base)</label>
                <input
                  className="w-full bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-sm"
                  value={displayedSkill(overridePlayerDefense, preview?.playerDefenseBase)}
                  onChange={(e) => setOverridePlayerDefense(e.target.value)}
                  placeholder="Base defense skill"
                />
                <div className="flex flex-wrap gap-3 text-sm">
                  <label>
                    <span
                      className="text-neutral-500 text-xs"
                      title="Bonus % shown on weapon appraisal (+405). Standing is 100%; this adds on top."
                    >
                      weapon +MD %
                    </span>
                    <input
                      className="block w-24 mt-1 bg-neutral-950 border border-neutral-700 rounded px-2 py-1"
                      value={playerDefenseBonusPct}
                      onChange={(e) => {
                        setPlayerDefenseModDirty(true)
                        const pct = asNumberOrUndefined(e.target.value)
                        if (pct == null) {
                          setPlayerDefenseMod('')
                          return
                        }
                        setPlayerDefenseMod(String(appraisalBonusPctToDefenseMod(pct)))
                      }}
                      placeholder="0"
                    />
                  </label>
                  <label>
                    <span className="text-neutral-500 text-xs" title="Total combat multiplier sent to SkillCheck (1 + weapon % / 100)">
                      total ×
                    </span>
                    <input
                      className="block w-20 mt-1 bg-neutral-950 border border-neutral-700 rounded px-2 py-1"
                      value={playerDefenseMod}
                      onChange={(e) => {
                        setPlayerDefenseModDirty(true)
                        setPlayerDefenseMod(e.target.value)
                      }}
                      placeholder="1.0"
                    />
                  </label>
                  <label>
                    <span className="text-neutral-500 text-xs" title="Flat bonus from +MeleeD imbues and luminance aug">
                      flat +def
                    </span>
                    <input
                      className="block w-20 mt-1 bg-neutral-950 border border-neutral-700 rounded px-2 py-1"
                      value={playerDefenseFlat}
                      onChange={(e) => {
                        setPlayerDefenseFlatDirty(true)
                        setPlayerDefenseFlat(e.target.value)
                      }}
                      placeholder="0"
                    />
                  </label>
                </div>
                <p className="text-xs text-neutral-500">
                  Standing = 100% (×1.0). Appraisal +405% → total ×5.05 (505%) · EMD = base × total × burden × stance + flat
                  {playerDefenseModTotal != null && playerDefenseModTotal > 1 && (
                    <span className="text-neutral-400">
                      {' '}
                      · {playerDefenseModTotal.toFixed(2)}× = {Math.round(playerDefenseModTotal * 100)}% of base skill
                    </span>
                  )}
                </p>
                <p className="text-xs text-neutral-400">
                  Effective {activeCfg.playerDefShort}:{' '}
                  <span className="text-white font-medium">{fmt(displayedEffectivePlayerDefense)}</span>
                </p>
              </>
            )}
          </div>

          <div className="space-y-3">
            <h4 className="text-sm font-medium text-white">Monster</h4>
            {direction === 'playerAttacksMonster' ? (
              <>
                <label className="text-xs text-neutral-500 block">
                  Defense skill (skill {activeCfg.defSkillId})
                </label>
                <input
                  className="w-full bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-sm"
                  value={displayedSkill(overrideMonsterDefense, preview?.monsterDefenseBase)}
                  onChange={(e) => setOverrideMonsterDefense(e.target.value)}
                  placeholder="Base defense skill"
                />
                <p className="text-xs text-neutral-400">
                  Effective {activeCfg.monsterDefShort}:{' '}
                  <span className="text-white font-medium">{fmt(preview?.effectiveMonsterDefense)}</span>
                </p>
              </>
            ) : (
              <>
                <label className="text-xs text-neutral-500 block">Attack skill (base)</label>
                <input
                  className="w-full bg-neutral-950 border border-neutral-700 rounded px-2 py-1 text-sm"
                  value={displayedSkill(overrideMonsterAttack, preview?.monsterAttackBase)}
                  onChange={(e) => setOverrideMonsterAttack(e.target.value)}
                  placeholder="Base attack skill"
                />
                <label className="text-sm">
                  <span className="text-neutral-500 text-xs">offense</span>
                  <input
                    className="block w-20 mt-1 bg-neutral-950 border border-neutral-700 rounded px-2 py-1"
                    value={monsterOffenseMod}
                    onChange={(e) => {
                      setMonsterOffenseModDirty(true)
                      setMonsterOffenseMod(e.target.value)
                    }}
                  />
                </label>
                <p className="text-xs text-neutral-400">
                  Effective {activeCfg.monsterAtkShort}:{' '}
                  <span className="text-white font-medium">{fmt(preview?.effectiveMonsterAttack)}</span>
                </p>
              </>
            )}
          </div>
        </div>
      </div>

      <div className="bg-neutral-900 border border-neutral-800 rounded-xl p-4 space-y-4">
        <h3 className="text-sm font-bold text-white uppercase tracking-wider">
          3 · Results
          {preview && (
            <span className="font-normal text-neutral-500 normal-case ml-2">
              {activeCfg.label}, {preview.triplet.primaryLabel.toLowerCase()} (atk {fmt(atk)} vs def {fmt(def)})
            </span>
          )}
        </h3>

        {error && <p className="text-sm text-red-400">{error}</p>}

        {!preview && !error && (
          <p className="text-sm text-neutral-500">Enter skills in section 2 and click Recalculate.</p>
        )}

        {triplet && (
          <>
            {isPlayerAdmin ? (
              <>
                <p className="text-xs text-neutral-500">
                  A = scaling off · B = on (agg {activeCfg.serverDefaultAgg}
                  {scalingEnabled ? '' : ', scaling disabled on server'}) · C = on (test {testAgg})
                </p>
                <div className="flex flex-wrap gap-2 text-sm">
                  <span className={`px-3 py-1 rounded ${!preview?.scalingEnabled ? 'bg-blue-900/40 text-blue-300 border border-blue-500/50 font-semibold' : 'bg-neutral-800 text-neutral-400'}`}>
                    A OFF {fmtPct(triplet.primaryBaseline)} {!preview?.scalingEnabled && ' (Active)'}
                  </span>
                  <span className={`px-3 py-1 rounded ${preview?.scalingEnabled ? 'bg-blue-900/40 text-blue-300 border border-blue-500/50 font-semibold' : 'bg-neutral-800 text-neutral-400'}`}>
                    B {fmtPct(triplet.primaryServerDefault)} {preview?.scalingEnabled && ' (Active)'}
                  </span>
                  <span className="px-3 py-1 rounded bg-neutral-800/60 text-neutral-500 border border-neutral-800">
                    C {fmtPct(triplet.primaryTest)} (Test)
                  </span>
                  <span
                    className={`px-3 py-1 rounded ${triplet.deltaPrimaryVsDefault >= 0 ? 'bg-green-900/30 text-green-400' : 'bg-amber-900/30 text-amber-400'}`}
                  >
                    Δ(C−B) {triplet.deltaPrimaryVsDefault >= 0 ? '+' : ''}
                    {triplet.deltaPrimaryVsDefault.toFixed(1)}%
                  </span>
                </div>
              </>
            ) : (
              <div className="flex flex-wrap gap-2 text-sm">
                <span className="px-3 py-1 rounded bg-blue-900/40 text-blue-300">
                  Active Setting: {fmtPct(preview?.scalingEnabled ? triplet.primaryServerDefault : triplet.primaryBaseline)}
                </span>
              </div>
            )}

            <div className="flex flex-wrap gap-2 items-center">
              <button type="button" className="px-3 py-1.5 bg-blue-600 hover:bg-blue-500 rounded text-sm" onClick={copyDiscord}>
                Copy Discord
              </button>
              <button
                type="button"
                className="px-3 py-1.5 bg-neutral-800 hover:bg-neutral-700 rounded text-sm"
                onClick={() => setShowDiscordPreview((v) => !v)}
              >
                {showDiscordPreview ? 'Hide preview' : 'Show preview'}
              </button>
              {copyStatus && <span className="text-xs text-neutral-500">{copyStatus}</span>}
            </div>
            {showDiscordPreview && (
              <pre className="text-xs bg-neutral-950 border border-neutral-800 rounded p-3 overflow-x-auto whitespace-pre-wrap">
                {discordFull}
              </pre>
            )}

            <div className="space-y-2 pt-2 border-t border-neutral-800">
              <p className="text-sm font-medium text-white">
                {direction === 'playerAttacksMonster'
                  ? `Attack sweep — fixed mob ${activeCfg.monsterDefShort} ${fmt(def)}, vary ${activeCfg.playerAtkShort} (effective)`
                  : `Defense sweep — fixed mob atk ${fmt(atk)}, vary effective ${activeCfg.playerDefShort}`}
              </p>
              <div className="flex flex-wrap gap-2 items-center text-sm">
                <input
                  className="w-24 bg-neutral-950 border border-neutral-700 rounded px-2 py-1"
                  value={rangeMin}
                  onChange={(e) => setRangeMin(e.target.value)}
                />
                <span className="text-neutral-500">to</span>
                <input
                  className="w-24 bg-neutral-950 border border-neutral-700 rounded px-2 py-1"
                  value={rangeMax}
                  onChange={(e) => setRangeMax(e.target.value)}
                />
                <span className="text-neutral-500">step</span>
                <input
                  className="w-20 bg-neutral-950 border border-neutral-700 rounded px-2 py-1"
                  value={rangeStep}
                  onChange={(e) => setRangeStep(e.target.value)}
                />
                {direction === 'playerAttacksMonster' && def > 0 && (
                  <button
                    type="button"
                    className="px-2 py-1 bg-neutral-800 hover:bg-neutral-700 rounded text-xs"
                    onClick={() => setRangeAround(def, 0.2, 100, 500)}
                  >
                    ±20% mob {activeCfg.monsterDefShort}
                  </button>
                )}
                <button
                  type="button"
                  className="px-2 py-1 bg-neutral-800 hover:bg-neutral-700 rounded text-xs"
                  onClick={() => {
                    const center =
                      direction === 'playerAttacksMonster'
                        ? atk
                        : (displayedEffectivePlayerDefense ?? preview?.effectivePlayerDefense ?? 0)
                    const span = direction === 'playerAttacksMonster' ? 750 : 2500
                    const step = direction === 'playerAttacksMonster' ? 100 : 500
                    setRangeMin(String(Math.max(0, center - span)))
                    setRangeMax(String(center + span))
                    setRangeStep(String(step))
                  }}
                >
                  {direction === 'playerAttacksMonster' ? `±750 atk` : `±2500 ${activeCfg.playerDefShort}`}
                </button>
                <button type="button" className="px-2 py-1 bg-blue-600 hover:bg-blue-500 rounded text-xs" onClick={() => runPreview(true)}>
                  Update table
                </button>
              </div>

              {rangeRows.length > 0 ? (
                <div className="overflow-x-auto border border-neutral-800 rounded">
                  <table className="w-full text-sm">
                    <thead className="bg-neutral-950 text-neutral-400">
                      <tr>
                        <th className="text-right p-2">{sweepColumnShort}</th>
                        {isPlayerAdmin ? (
                          <>
                            <th className={`text-right p-2 ${!preview?.scalingEnabled ? 'text-blue-300 font-semibold border-b border-blue-500/30' : 'text-neutral-500 font-normal'}`}>
                              A OFF {!preview?.scalingEnabled && ' (Active)'}
                            </th>
                            <th className={`text-right p-2 ${preview?.scalingEnabled ? 'text-blue-300 font-semibold border-b border-blue-500/30' : 'text-neutral-500 font-normal'}`}>
                              B {activeCfg.serverDefaultAgg} {preview?.scalingEnabled && ' (Active)'}
                            </th>
                            <th className="text-right p-2 text-neutral-500 font-normal">
                              C {testAgg} (Test)
                            </th>
                          </>
                        ) : (
                          <th className="text-right p-2">Active Setting</th>
                        )}
                      </tr>
                    </thead>
                    <tbody>
                      {rangeRows.map((r) => (
                        <tr key={r.sweep} className="border-t border-neutral-800">
                          <td className="p-2 text-right">{fmt(r.sweep)}</td>
                          {isPlayerAdmin ? (
                            <>
                              <td className={`p-2 text-right ${!preview?.scalingEnabled ? 'bg-blue-900/10 text-blue-300 font-semibold border-x border-blue-500/10' : 'text-neutral-500'}`}>{fmtPct(r.a)}</td>
                              <td className={`p-2 text-right ${preview?.scalingEnabled ? 'bg-blue-900/10 text-blue-300 font-semibold border-x border-blue-500/10' : 'text-neutral-500'}`}>{fmtPct(r.b)}</td>
                              <td className="p-2 text-right text-neutral-600">{fmtPct(r.c)}</td>
                            </>
                          ) : (
                            <td className="p-2 text-right">
                              {fmtPct(preview?.scalingEnabled ? r.b : r.a)}
                            </td>
                          )}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <p className="text-xs text-amber-400">Enter a valid min / max / step, then Update table.</p>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  )
}
