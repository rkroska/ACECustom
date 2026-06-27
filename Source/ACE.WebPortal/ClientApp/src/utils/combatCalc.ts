/** Shared combat calculator logic (canvas + ACE Web Portal). Mirrors ACECustom SkillCheck. */

export type DefenseMode = "melee" | "missile" | "magic";
export type CombatDirection = "monsterAttacksPlayer" | "playerAttacksMonster";

export type ModeConfig = {
  label: string;
  configKey: string;
  baseFactor: number;
  serverDefaultAgg: number;
  defSkillId: number;
  buffedLabel: string;
  playerDefLabel: string;
  playerDefShort: string;
  monsterAtkLabel: string;
  monsterAtkShort: string;
  monsterDefLabel: string;
  monsterDefShort: string;
  playerAtkLabel: string;
  playerAtkShort: string;
  metricLabel: string;
  outcomeLabel: string;
  note?: string;
};

export type ModeInputs = {
  buffed: string;
  weaponBonusPct: string;
  monsterAttack: string;
  playerDefense: string;
  playerAttack: string;
  monsterDefense: string;
  testAggression: string;
  defMin: string;
  defMax: string;
  defStep: string;
};

export type RangeRow = { sweep: number; a: number; b: number; c: number };

export type TripletResult = ReturnType<typeof computeTriplet>;

export type WeenieSkill = { skillId: number; initLevel: number };

export const WEAPON_SKILL_IDS = [45, 47, 44, 46, 2, 5, 24];
export const MAGIC_CAST_SKILL_IDS = [34, 33, 43, 32, 31, 29];
export const DEFENSE_SKILL_IDS = [6, 7, 15];

export const MONSTER_ATTACK_SKILL_IDS_BY_MODE: Record<DefenseMode, number[]> = {
  melee: [45, 44, 46, 2],
  missile: [47, 5, 24],
  magic: MAGIC_CAST_SKILL_IDS,
};

export const PLAYER_ATTACK_SKILL_BY_MODE: Record<DefenseMode, number> = {
  melee: 45,
  missile: 47,
  magic: 34,
};

export const CONTEST_TYPE_OPTIONS: Array<{
  value: DefenseMode;
  labelWhenAttacking: string;
  labelWhenDefending: string;
  mobDefNote: string;
}> = [
  {
    value: "missile",
    labelWhenAttacking: "Missile attack",
    labelWhenDefending: "Missile defense",
    mobDefNote: "MissileWeapons → Mob Missile Defense (skill 7)",
  },
  {
    value: "melee",
    labelWhenAttacking: "Melee attack (heavy / finesse / light)",
    labelWhenDefending: "Melee defense",
    mobDefNote: "Weapon skills → Mob Melee Defense (skill 6)",
  },
  {
    value: "magic",
    labelWhenAttacking: "Magic attack (war / void)",
    labelWhenDefending: "Magic defense",
    mobDefNote: "Cast skills → Mob Magic Defense (skill 15)",
  },
];

export function tripletWithDeltas(t: {
  primaryBaseline: number;
  primaryServerDefault: number;
  primaryTest: number;
  primaryLabel: string;
  secondaryLabel: string;
  secondaryBaseline: number;
  secondaryServerDefault: number;
  secondaryTest: number;
}) {
  return {
    ...t,
    deltaPrimaryVsDefault: t.primaryTest - t.primaryServerDefault,
    deltaPrimaryVsBaseline: t.primaryTest - t.primaryBaseline,
  };
}

export const MODE_CONFIG: Record<DefenseMode, ModeConfig> = {
  melee: {
    label: "Melee",
    configKey: "melee",
    baseFactor: 0.03,
    serverDefaultAgg: 0.75,
    defSkillId: 6,
    buffedLabel: "Buffed Melee D",
    playerDefLabel: "Player EMD",
    playerDefShort: "EMD",
    monsterAtkLabel: "Monster melee atk",
    monsterAtkShort: "Mob melee atk",
    monsterDefLabel: "Monster melee def",
    monsterDefShort: "Mob MD",
    playerAtkLabel: "Player melee atk",
    playerAtkShort: "Melee atk",
    metricLabel: "You evade",
    outcomeLabel: "Monster hits you",
  },
  missile: {
    label: "Missile",
    configKey: "missile",
    baseFactor: 0.03,
    serverDefaultAgg: 0.75,
    defSkillId: 7,
    buffedLabel: "Buffed Missile D",
    playerDefLabel: "Player missile def",
    playerDefShort: "MissD",
    monsterAtkLabel: "Monster missile atk",
    monsterAtkShort: "Mob missile atk",
    monsterDefLabel: "Monster missile def",
    monsterDefShort: "Mob MissD",
    playerAtkLabel: "Player missile atk",
    playerAtkShort: "Missile atk",
    metricLabel: "You evade",
    outcomeLabel: "Monster hits you",
  },
  magic: {
    label: "Magic",
    configKey: "magic",
    baseFactor: 0.03,
    serverDefaultAgg: 1.75,
    defSkillId: 15,
    buffedLabel: "Buffed Magic D",
    playerDefLabel: "Player magic def",
    playerDefShort: "MagD",
    monsterAtkLabel: "Caster magic skill",
    monsterAtkShort: "Mob cast",
    monsterDefLabel: "Monster magic def",
    monsterDefShort: "Mob MagD",
    playerAtkLabel: "Player magic skill",
    playerAtkShort: "Cast skill",
    metricLabel: "You resist",
    outcomeLabel: "Spell lands",
    note: "Column A matches live spell resist (MagicDefenseCheck, factor 0.03, no scaling). B/C preview defense_scaling_magic if enabled on resist.",
  },
};

export function fmt(n: number | undefined): string {
  if (n === undefined || Number.isNaN(n)) return "—";
  return n.toLocaleString();
}

export function asNumberOrUndefined(s: string): number | undefined {
  const v = Number(s);
  return Number.isFinite(v) ? v : undefined;
}

/** Weapon appraisal +MD % (e.g. 405) → total combat multiplier (5.05). Includes standing 100%. */
export function appraisalBonusPctToDefenseMod(pct: number): number {
  return 1 + pct / 100;
}

export function defenseModToAppraisalBonusPct(mod: number): number {
  return Math.round((mod - 1) * 100);
}

function clamp01(x: number): number {
  if (Number.isNaN(x)) return 0;
  return Math.min(1, Math.max(0, x));
}

export function computeChanceHit({
  attackSkill,
  defenseSkill,
  baseFactor,
  scalingEnabled,
  aggression,
}: {
  attackSkill: number;
  defenseSkill: number;
  baseFactor: number;
  scalingEnabled: boolean;
  aggression: number;
}): number {
  let f = baseFactor;
  if (scalingEnabled) {
    const avg = (attackSkill + defenseSkill) / 2.0;
    const scale = Math.pow(500.0 / Math.max(500.0, avg), aggression);
    f *= scale;
  }
  const chance = 1.0 - 1.0 / (1.0 + Math.exp(f * (attackSkill - defenseSkill)));
  return clamp01(chance);
}

export function isDefenseSkillId(skillId: number): boolean {
  return DEFENSE_SKILL_IDS.includes(skillId);
}

export function isAttackSkillId(skillId: number): boolean {
  return WEAPON_SKILL_IDS.includes(skillId) || MAGIC_CAST_SKILL_IDS.includes(skillId);
}

export function skillRole(skillId: number): "attack" | "defense" | "other" {
  if (isDefenseSkillId(skillId)) return "defense";
  if (isAttackSkillId(skillId)) return "attack";
  return "other";
}

export function defaultModeInputs(mode: DefenseMode): ModeInputs {
  const agg = String(MODE_CONFIG[mode].serverDefaultAgg);
  return {
    buffed: "3000",
    weaponBonusPct: "250",
    monsterAttack: "36525",
    playerDefense: mode === "magic" ? "3500" : "35000",
    playerAttack: "36000",
    monsterDefense: mode === "missile" ? "1750" : mode === "melee" ? "3500" : "2500",
    testAggression: agg,
    defMin: "35000",
    defMax: "40000",
    defStep: "500",
  };
}

export function resolveContestSkills(
  direction: CombatDirection,
  inputs: ModeInputs,
  effectivePlayerAttack?: number
): { attackSkill: number; defenseSkill: number } {
  if (direction === "playerAttacksMonster") {
    return {
      attackSkill: effectivePlayerAttack ?? asNumberOrUndefined(inputs.playerAttack) ?? 0,
      defenseSkill: asNumberOrUndefined(inputs.monsterDefense) ?? 0,
    };
  }
  return {
    attackSkill: asNumberOrUndefined(inputs.monsterAttack) ?? 0,
    defenseSkill: asNumberOrUndefined(inputs.playerDefense) ?? 0,
  };
}

export function playerEvadePct(
  attackSkill: number,
  defenseSkill: number,
  baseFactor: number,
  scalingEnabled: boolean,
  aggression: number
): number {
  const hit = computeChanceHit({ attackSkill, defenseSkill, baseFactor, scalingEnabled, aggression });
  return Math.round(clamp01(1 - hit) * 1000) / 10;
}

export function fmtPct(n: number): string {
  return `${n.toFixed(1)}%`;
}

function padCol(s: string, width: number, align: "left" | "right" = "left"): string {
  return align === "right" ? s.padStart(width) : s.padEnd(width);
}

export function buildDiscordEvadeSummary(opts: {
  cfg: ModeConfig;
  direction: CombatDirection;
  attackSkill: number;
  defenseSkill: number;
  testAgg: number;
  triplet: TripletResult | ReturnType<typeof tripletWithDeltas>;
}): string {
  const { cfg, direction, attackSkill, defenseSkill, testAgg, triplet } = opts;
  const sign = (n: number) => (n >= 0 ? "+" : "") + n.toFixed(1);
  const key = cfg.configKey;
  const heading =
    direction === "playerAttacksMonster"
      ? `**${cfg.label} — you hit monster** (player attacks)`
      : `**${cfg.label} — ${cfg.metricLabel.toLowerCase()}** (monster attacks you)`;
  const atkLabel = direction === "playerAttacksMonster" ? cfg.playerAtkLabel : cfg.monsterAtkLabel;
  const defLabel = direction === "playerAttacksMonster" ? cfg.monsterDefLabel : cfg.playerDefLabel;

  return [
    `${heading} · ACECustom SkillCheck`,
    `${atkLabel} **${fmt(attackSkill)}** · ${defLabel} **${fmt(defenseSkill)}** · Test aggression **${testAgg}**`,
    ``,
    `**A) Scaling OFF** — today (\`defense_scaling_${key}_enabled=false\`)`,
    `→ ${triplet.primaryLabel} **${fmtPct(triplet.primaryBaseline)}** · ${triplet.secondaryLabel} **${fmtPct(triplet.secondaryBaseline)}**`,
    ``,
    `**B) Scaling ON · aggression ${cfg.serverDefaultAgg}** — server default (\`defense_scaling_${key}_agg\`)`,
    `→ ${triplet.primaryLabel} **${fmtPct(triplet.primaryServerDefault)}** · ${triplet.secondaryLabel} **${fmtPct(triplet.secondaryServerDefault)}**`,
    ``,
    `**C) Scaling ON · aggression ${testAgg}** — your test value`,
    `→ ${triplet.primaryLabel} **${fmtPct(triplet.primaryTest)}** · ${triplet.secondaryLabel} **${fmtPct(triplet.secondaryTest)}**`,
    ``,
    `**Δ vs default aggression:** ${sign(triplet.deltaPrimaryVsDefault)}% · **Δ vs scaling off:** ${sign(triplet.deltaPrimaryVsBaseline)}%`,
  ].join("\n");
}

export function buildDiscordRangeTable(opts: {
  cfg: ModeConfig;
  direction: CombatDirection;
  fixedAtk: number;
  fixedDef: number;
  serverDefaultAgg: number;
  testAgg: number;
  sweepMin: number;
  sweepMax: number;
  step: number;
  sweepColumnShort: string;
  rows: RangeRow[];
}): string {
  const {
    cfg,
    direction,
    fixedAtk,
    fixedDef,
    serverDefaultAgg,
    testAgg,
    sweepMin,
    sweepMax,
    step,
    sweepColumnShort,
    rows,
  } = opts;

  const labelB = `B ON ${serverDefaultAgg}`;
  const labelC = `C ON ${testAgg}`;
  const colW = Math.max(3, sweepColumnShort.length, ...rows.map((r) => fmt(r.sweep).length));
  const pctW = 7;
  const colBW = Math.max(pctW, labelB.length);
  const colCW = Math.max(pctW, labelC.length);

  const headerRow = [
    padCol(sweepColumnShort, colW, "right"),
    padCol("A OFF", pctW, "right"),
    padCol(labelB, colBW, "right"),
    padCol(labelC, colCW, "right"),
  ].join("  ");

  const ruleRow = ["-".repeat(colW), "-".repeat(pctW), "-".repeat(colBW), "-".repeat(colCW)].join("  ");

  const bodyRows = rows
    .map((r) =>
      [
        padCol(fmt(r.sweep), colW, "right"),
        padCol(fmtPct(r.a), pctW, "right"),
        padCol(fmtPct(r.b), colBW, "right"),
        padCol(fmtPct(r.c), colCW, "right"),
      ].join("  ")
    )
    .join("\n");

  const codeBlock = ["```", headerRow, ruleRow, bodyRows, "```"].join("\n");

  const tableTitle =
    direction === "playerAttacksMonster"
      ? `**${cfg.label} — you hit % (vary ${sweepColumnShort}, fixed ${cfg.monsterDefShort} ${fmt(fixedDef)})**`
      : `**${cfg.label} — ${cfg.metricLabel.toLowerCase()} % (vary ${sweepColumnShort})**`;

  const fixedLine =
    direction === "playerAttacksMonster"
      ? `Fixed ${cfg.monsterDefShort} **${fmt(fixedDef)}** · sweep ${sweepColumnShort} **${fmt(sweepMin)}–${fmt(sweepMax)}** step **${fmt(step)}**`
      : `Fixed monster atk **${fmt(fixedAtk)}** · sweep ${sweepColumnShort} **${fmt(sweepMin)}–${fmt(sweepMax)}** step **${fmt(step)}**`;

  return [
    tableTitle,
    fixedLine,
    `Columns = ${direction === "playerAttacksMonster" ? "you hit %" : "you evade %"}`,
    `**A** scaling off · **B** agg **${serverDefaultAgg}** · **C** agg **${testAgg}**`,
    ``,
    codeBlock,
  ].join("\n");
}

export function pickDefaultAttackSkillId(skills: WeenieSkill[], mode: DefenseMode): string {
  const forMode = MONSTER_ATTACK_SKILL_IDS_BY_MODE[mode];
  const modePick = skills
    .filter((s) => forMode.includes(s.skillId))
    .sort((a, b) => b.initLevel - a.initLevel)[0];
  if (modePick) return String(modePick.skillId);

  const anyAttack = skills
    .filter((s) => isAttackSkillId(s.skillId))
    .sort((a, b) => b.initLevel - a.initLevel)[0];
  if (anyAttack) return String(anyAttack.skillId);

  return mode === "magic" ? "34" : mode === "missile" ? "47" : "45";
}

export function pickMonsterDefenseSkill(skills: WeenieSkill[], defSkillId: number): number {
  return skills.find((s) => s.skillId === defSkillId)?.initLevel ?? 0;
}

export function skillLabel(skillId: number): string {
  const known: Record<number, string> = {
    2: "Unarmed",
    5: "Bow",
    6: "MeleeDefense",
    7: "MissileDefense",
    15: "MagicDefense",
    24: "Run",
    33: "LifeMagic",
    34: "WarMagic",
    43: "VoidMagic",
    44: "HeavyWeapons",
    45: "LightWeapons",
    46: "FinesseWeapons",
    47: "MissileWeapons",
  };
  return known[skillId] ?? `Skill ${skillId}`;
}

export function effectiveDefFromInputs(buffed: number, weaponBonusPct: number): number {
  return (1 + weaponBonusPct / 100) * buffed;
}

export function computeTriplet(
  cfg: ModeConfig,
  atk: number,
  def: number,
  testAgg: number,
  direction: CombatDirection
) {
  const evadeBaseline = playerEvadePct(atk, def, cfg.baseFactor, false, cfg.serverDefaultAgg);
  const evadeServerDefault = playerEvadePct(atk, def, cfg.baseFactor, true, cfg.serverDefaultAgg);
  const evadeTestAgg = playerEvadePct(atk, def, cfg.baseFactor, true, testAgg);
  const hitBaseline = Math.round((100 - evadeBaseline) * 10) / 10;
  const hitServerDefault = Math.round((100 - evadeServerDefault) * 10) / 10;
  const hitTestAgg = Math.round((100 - evadeTestAgg) * 10) / 10;

  const playerAttacks = direction === "playerAttacksMonster";
  const primaryLabel = playerAttacks ? "You hit" : cfg.metricLabel;
  const secondaryLabel = playerAttacks ? "Monster evades" : cfg.outcomeLabel;

  return {
    evadeBaseline,
    evadeServerDefault,
    evadeTestAgg,
    hitBaseline,
    hitServerDefault,
    hitTestAgg,
    primaryLabel,
    secondaryLabel,
    primaryBaseline: playerAttacks ? hitBaseline : evadeBaseline,
    primaryServerDefault: playerAttacks ? hitServerDefault : evadeServerDefault,
    primaryTest: playerAttacks ? hitTestAgg : evadeTestAgg,
    secondaryBaseline: playerAttacks ? evadeBaseline : hitBaseline,
    secondaryServerDefault: playerAttacks ? evadeServerDefault : hitServerDefault,
    secondaryTest: playerAttacks ? evadeTestAgg : hitTestAgg,
    deltaPrimaryVsDefault: playerAttacks
      ? Math.round((hitTestAgg - hitServerDefault) * 10) / 10
      : Math.round((evadeTestAgg - evadeServerDefault) * 10) / 10,
    deltaPrimaryVsBaseline: playerAttacks
      ? Math.round((hitTestAgg - hitBaseline) * 10) / 10
      : Math.round((evadeTestAgg - evadeBaseline) * 10) / 10,
  };
}

export function buildRangeRows(
  cfg: ModeConfig,
  testAgg: number,
  lo: number,
  hi: number,
  step: number,
  direction: CombatDirection,
  fixedAtk: number,
  fixedDef: number
): RangeRow[] {
  if (hi <= 0 || lo < 0) return [];
  const rows: RangeRow[] = [];
  const maxRows = 120;
  const useHit = direction === "playerAttacksMonster";
  const sweepAttack = direction === "playerAttacksMonster";

  for (let sweep = lo, i = 0; sweep <= hi && i < maxRows; sweep += step, i++) {
    const atk = sweepAttack ? sweep : fixedAtk;
    const def = sweepAttack ? fixedDef : sweep;
    const evA = playerEvadePct(atk, def, cfg.baseFactor, false, cfg.serverDefaultAgg);
    const evB = playerEvadePct(atk, def, cfg.baseFactor, true, cfg.serverDefaultAgg);
    const evC = playerEvadePct(atk, def, cfg.baseFactor, true, testAgg);
    const toPrimary = (ev: number) => Math.round((useHit ? 100 - ev : ev) * 10) / 10;
    rows.push({
      sweep,
      a: toPrimary(evA),
      b: toPrimary(evB),
      c: toPrimary(evC),
    });
  }
  return rows;
}

export function parseWeenieSkillsFromSql(sql: string): WeenieSkill[] {
  if (!sql.trim()) return [];

  const skillBlock =
    /INSERT\s+INTO\s+`?weenie_properties_skill`?[\s\S]*?(?=;\s*(?:INSERT|DELETE|UPDATE|CREATE|--\s*Done)|$)/i.exec(
      sql
    )?.[0] ?? sql;

  const out: WeenieSkill[] = [];
  const tupleRe = /\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,/g;
  let m: RegExpExecArray | null;
  while ((m = tupleRe.exec(skillBlock)) !== null) {
    const skillId = Number(m[2]);
    const initLevel = Number(m[6]);
    if (Number.isFinite(skillId) && Number.isFinite(initLevel) && initLevel > 0) {
      out.push({ skillId, initLevel });
    }
  }

  const map = new Map<number, number>();
  for (const s of out) map.set(s.skillId, Math.max(map.get(s.skillId) ?? 0, s.initLevel));
  return Array.from(map.entries())
    .map(([skillId, initLevel]) => ({ skillId, initLevel }))
    .sort((a, b) => b.initLevel - a.initLevel);
}

export type MockPlayerBundle = {
  guid: number;
  name: string;
  isOnline: boolean;
  weaponBonusPct: number;
  effectiveAttackByMode: Record<DefenseMode, number>;
  defenseByMode: Record<DefenseMode, number>;
  buffedDefByMode: Record<DefenseMode, number>;
};

/** Build combat inputs from weenie skills + mock player bundle. */
export function buildInputsFromSelection(opts: {
  mode: DefenseMode;
  player?: MockPlayerBundle | null;
  monsterSkills?: WeenieSkill[];
  monsterOffenseMod?: number;
}): Partial<ModeInputs> {
  const { mode, player, monsterSkills, monsterOffenseMod = 1 } = opts;
  const patch: Partial<ModeInputs> = {};

  if (player) {
    patch.playerAttack = String(player.effectiveAttackByMode[mode]);
    patch.playerDefense = String(player.defenseByMode[mode]);
    patch.buffed = String(player.buffedDefByMode[mode]);
    patch.weaponBonusPct = String(player.weaponBonusPct);
  }

  if (monsterSkills?.length) {
    const def = pickMonsterDefenseSkill(monsterSkills, MODE_CONFIG[mode].defSkillId);
    patch.monsterDefense = String(def);
    const atkId = pickDefaultAttackSkillId(monsterSkills, mode);
    const base = monsterSkills.find((s) => s.skillId === Number(atkId))?.initLevel ?? 0;
    patch.monsterAttack = String(Math.round(base * monsterOffenseMod));
  }

  return patch;
}
