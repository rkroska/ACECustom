using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using log4net;

using Newtonsoft.Json;

using ACE.Database;
using ACE.Database.Models.Shard;

namespace ACE.Server.Managers.WeaponScaling
{
    /// <summary>One tier band: the aug count the scaling term stops growing at, and the item-aug
    /// floor required to wield the tier's weapons (the market-segmentation gate — NOT power gating;
    /// power is self-gated by the per-wielder scaling itself).</summary>
    public class WeaponScalingTier
    {
        public int Tier { get; set; }
        public int Cap { get; set; }
        public int MinWieldAugs { get; set; }

        // Charm wield gates (owner 2026-08-15): item augs purchase-cap at 4,000, so T16+ weapons
        // can't keep climbing the item-aug ladder. From T16 the item req FREEZES at 4,000 and
        // these two climb instead, +500/tier each: Triune Weave, plus the weapon-family charm
        // (Crashing Steel melee / True Shot launchers / Battlemage's Wrath elemental casters /
        // Nether Veil nether casters). 0 = no charm gate (all tiers through T15).
        /// <summary>
        /// CREATURE aug requirement for the T11+ hit gate (owner 2026-08-31). Unlike MinWieldAugs this
        /// is NOT a wield requirement - nothing stops you equipping the gear. It gates whether your
        /// swings and spells can LAND on a monster at this variation (TierHitGate).
        /// 4,000 at T11, +500/tier, frozen at the 6,000 purchase cap from T15 - above which TRIUNE
        /// carries the ladder, exactly as it does for the item-aug wield gate.
        /// </summary>
        public int MinWieldCreature { get; set; }
        public int MinWieldTriune { get; set; }
        public int MinWieldSkillCharm { get; set; }
    }

    /// <summary>Per-loot-script k roll range. A weapon's stored QUALITY (0-1000) lerps between
    /// KMin and KMax at swing time, so retuning the range re-prices every existing drop.</summary>
    public class WeaponScalingScript
    {
        public double KMin { get; set; }
        public double KMax { get; set; }

        // Scheme C (2026-08-03): the family's authored per-hit variance. Non-crit melee hits
        // roll the WHOLE envelope (weenie base + aug term) down from max by the quality-
        // tightened fraction of this. 0 = inert (launchers/casters, or a store from before
        // this field existed — the old flat-hit behavior is the fallback either way).
        public double Variance { get; set; }

        // Grade ladder (owner 2026-08-03, WeaponGradeLadder plan): k resolved from the weapon's
        // SUB-GRADE, not lerped from quality. Sixteen AUTHORED values keyed by sub-grade name
        // (S, A+, A, A-, ... F-) — the ladder is drawn, not derived, because the quality bands
        // are wildly uneven in width (S->A is 50 quality points, D->F is 325) and a linear lerp
        // inherited that geometry, bunching S/A/B within +7.5 pct of each other.
        // NULL/EMPTY = this family falls back to the KMin/KMax lerp — that is the migration path
        // for old stores AND the deliberate current state of launchers (their rows are a damage
        // MOD band, retuned in the missile pass) and casters (inert).
        public Dictionary<string, double> Grades { get; set; }

        /// <summary>True when this family resolves off an authored ladder rather than the lerp.
        /// Gates BOTH k resolution and the variance sub-grade snap, so the two never disagree.</summary>
        [JsonIgnore]
        public bool HasLadder => Grades != null && Grades.Count > 0;
    }

    public class WeaponScalingConfig
    {
        public bool Enabled { get; set; }

        public List<WeaponScalingTier> Tiers { get; set; } = new();

        public Dictionary<string, WeaponScalingScript> Scripts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public double KcMin { get; set; }
        public double KcMax { get; set; }

        // Scheme C: how much of the family variance a perfect-quality weapon sheds.
        // v_eff = Variance x (1 - TightenStrength x quality/1000); 0.7 = S keeps 30 pct of
        // the family's wildness, F keeps ~83 pct. 0 (old stores) = no tightening.
        public double TightenStrength { get; set; }

        // Launcher tier scaling (owner 2026-08-06). A launcher's damage modifier gains this
        // fraction per TIER STEP the wielder's item augs have actually unlocked, gated on
        // min(augs, the weapon tier's Cap). Bows therefore inherit melee's dead zone: once a
        // tier's cap climbs past what the player owns, a higher-tier bow gives nothing —
        // exactly as a higher-tier melee weapon already gives nothing (owner: "Bows should
        // track min(augs, cap) like melee does").
        //
        // T11 is the baseline and higher tiers only ADD. Nothing is ever subtracted, so item
        // augs always pay and Blood Drinker is never touched.
        //
        // REPLACES the missile aug cap shipped and reverted the same day, which produced tier
        // growth by clawing BACK over-cap Blood Drinker. That froze a T11 bow user's BD
        // contribution at 1,250 no matter how many augs they bought (owner: "Everything
        // dealing with Blood Drinker is supposed to stay uncapped. Capping BD destroys the
        // purpose of increasing Item Augs completely"). Do not reintroduce a subtractive form.
        //
        // STRAIGHT step, deliberately not compounding — G^steps snowballs over 14 tiers
        // (G=1.09 matches melee's upgrade feel but lands bows at 2.41x melee by T25).
        //
        // SIZED AT 0.06 for the tiers players are actually in. Owner 2026-08-06: melee should be
        // "about 20-25 pct ahead starting out... Melee has to get close to mobs, missile shoots
        // from distance so the 20 pct difference is fine", and on the far end, "T25 is years away
        // — don't need to plan around that". So this is tuned on T11-T15, not on the asymptote.
        //
        // Melee sits 26 pct ahead at T11, which is the owner's target and is fixed by kMin/kMax:
        // steps is 0 at T11 by construction, so this value CANNOT move the starting gap. Raising
        // the bow's floor means editing the family's k range instead.
        //
        // Buys +4.0 pct per unlocked step at fixed augs against melee's +6.1 pct. The remaining
        // gap matters less than it reads: the WIELD FLOOR means augs cannot be held fixed across
        // many tiers anyway (at 3,500 augs only T11-T14 are wieldable), so the upgrade players
        // actually experience is "bought augs, can now wield a better bow" — where bows already
        // gain +11.6 pct per tier before this term exists at all.
        //
        // Far out this does cross over (bow ~38 pct ahead by T25 at full augs). Deliberately not
        // designed around — revisit if the live ladder ever reaches those tiers.
        //
        // Bows also SHOULD have a smaller fixed-aug step than melee. Melee's +6.1 pct is
        // recovering waste — a T11 melee user at 3,500 augs draws value from only 2,500 of
        // them and the upgrade unlocks the rest. A bow user has no waste; BD is uncapped and
        // fully multiplied, so all 3,500 already count. There is less there to hand back.
        //
        // INITIALIZED rather than defaulted to 0 so stores written before this field still get
        // the scaling; an explicit 0 disables it.
        public double LauncherTierStep { get; set; } = 0.06;

        // The caster twin of LauncherTierStep (owner 2026-08-06: "Lets keep bow and caster
        // weapons scaling identical. We will tune vs magic on mobs. This keeps weapons simple").
        // Casters run the SAME mechanism as launchers — quality resolves the mod, the tier term
        // multiplies it, min(augs, cap) gates it — driving ElementalDamageMod instead of
        // DamageMod. It gets its OWN knob rather than sharing the launcher's (owner, same day:
        // "Do the CasterTierStep - incase we want to tune caster different later"), defaulted
        // EQUAL so the two lanes are identical until someone deliberately parts them.
        //
        // The physical-vs-magic balance is NOT tuned here — owner ruled it belongs on the mob
        // side (magic defense/resists), because monster defenses differ between spell and
        // physical and no weapon band can express that. Do not re-derive this from a
        // caster-vs-bow damage ratio.
        public double CasterTierStep { get; set; } = 0.06;

        // Multiplicative composition rescale (owner GO 2026-08-06, CasterDamageShare_Plan):
        // the caster modifier is wandMod x (1 + THIS x enchantmentSum) instead of the stock
        // wandMod + enchantmentSum. Without it Spirit Drinker (+0.005/itemAug, +17.50 at the
        // 3,500-aug reference) is an additive PEER ~11x the wand's own 1.24-1.58, making the
        // wand ~8 pct of its own multiplier — T11 vs T14 measured ~1 pct apart in game, S vs
        // F- +1.8 pct, when the owner's target is the weapon carrying 25-35 pct of total
        // damage like melee's flat term and the bow's mod both do.
        //
        // 0.6329 = 1/kMax (1/1.58): solving kMax x (1 + r x A) = kMax + A gives r = 1/kMax
        // INDEPENDENT of A — so an S-grade wand's total damage is bit-identical to the old
        // additive math at EVERY aug count, and the change strictly re-grades everyone else
        // around that anchor (lower grades lose, higher tiers finally gain their +6 pct/step).
        // Retune this only in step with the caster band's kMax or the anchor property breaks.
        public double CasterAuraRescale { get; set; } = 0.6329;

        // Grade drop weights (owner 2026-08-02): the quality roll picks a GRADE from these
        // weights, then rolls uniform INSIDE that grade's quality band — frequency decoupled
        // from what a grade MEANS (the band cutoffs stay fixed; S is always the single perfect
        // roll q=1000). Relative weights, normalized at roll time. Null/empty (old stores) =
        // the legacy uniform 0-1000 roll.
        public Dictionary<string, double> GradeWeights { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Weapon aug-scaling config: the plugin-tunable knobs for the T11+ weapon relevance system
    /// (plan: C:\AI\ZoneControl\T11_WeaponRelevance_Plan_2026-07-31.md).
    ///
    /// Weapons store a QUALITY percentile + tier; the MEANING (k ranges, tier caps, wield floors)
    /// lives here, resolved at swing time — so every knob edit retroactively re-prices existing
    /// drops. Persisted as a single JSON blob in shard config (same pattern as zonecontrol_data).
    ///
    /// This manager is deploy-cold: nothing reads it in combat until the wire-in ships, and the
    /// master Enabled flag (default OFF) gates that wire-in when it does.
    /// </summary>
    public static class WeaponScalingManager
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const string StoreKey = "weaponscaling_data";

        /// <summary>Quality rolls span 0..1000 (permille, integer-friendly for the old client wire).</summary>
        public const int QualityMax = 1000;

        private static readonly object _lock = new object();
        private static volatile bool _initialized;

        // ── Grade tables ──
        // DECLARED BEFORE _current ON PURPOSE: static field initializers run in declaration order,
        // and _current calls BuildDefaults(), which seeds ladders off these. Move them below and
        // the type initializer throws a NullReferenceException on first touch.

        /// <summary>Grade quality bands — MUST match the ForgeGrade cutoffs (plugin + appraisal
        /// labels). The weights table picks the grade; quality rolls uniform inside the band.
        /// S is the single perfect roll (owner 2026-08-02: "S remains 100 pct of max").</summary>
        public static readonly (string Grade, int QMin, int QMax)[] GradeBands =
        {
            ("S", 1000, 1000), ("A", 900, 999), ("B", 800, 899),
            ("C", 650, 799), ("D", 500, 649), ("F", 0, 499),
        };

        /// <summary>SUB-grade bands (owner 2026-08-03): each full grade split into thirds, S left
        /// alone as the single perfect roll. Costs NOTHING in the drop system — these are
        /// sub-ranges of the existing <see cref="GradeBands"/>, so GradeWeights and per-grade drop
        /// rates are untouched; the sub-grade is simply where inside the band the uniform roll
        /// landed. Order is the LADDER order (best to worst) and the payload wire order — the
        /// plugin indexes by position, so never reorder without bumping both sides.</summary>
        public static readonly (string Grade, int QMin, int QMax, int QMid)[] SubGradeBands =
        {
            ("S",  1000, 1000, 1000),
            ("A+",  967,  999,  983),
            ("A",   933,  966,  950),
            ("A-",  900,  932,  916),
            ("B+",  867,  899,  883),
            ("B",   833,  866,  850),
            ("B-",  800,  832,  816),
            ("C+",  750,  799,  775),
            ("C",   700,  749,  725),
            ("C-",  650,  699,  675),
            ("D+",  600,  649,  625),
            ("D",   550,  599,  575),
            ("D-",  500,  549,  525),
            ("F+",  333,  499,  416),
            ("F",   167,  332,  250),
            ("F-",    0,  166,   83),
        };

        /// <summary>Ladder step: +18 pct per FULL grade = three sub-grade steps, so one sub-grade
        /// step is the cube root. Owner 2026-08-03; S sits on the same even step (the premium
        /// variant was offered and declined).</summary>
        public static readonly double LadderStep = Math.Pow(1.18, 1.0 / 3.0);

        // Immutable-by-convention snapshot: mutations clone + swap; readers never see a torn config.
        private static volatile WeaponScalingConfig _current = BuildDefaults();

        /// <summary>Current config snapshot. Treat as read-only — all edits go through <see cref="Mutate"/>.</summary>
        public static WeaponScalingConfig Current
        {
            get { Initialize(); return _current; }
        }

        public static void Initialize()
        {
            if (_initialized)
                return;

            lock (_lock)
            {
                if (_initialized)
                    return;

                try { Load(); }
                catch (Exception ex) { log.Error($"WeaponScalingManager: failed to load store, using defaults. {ex}"); }

                _initialized = true;
            }
        }

        /// <summary>Locked launch defaults (plan §4, owner 2026-07-31): k 0.90-1.15 baseline with
        /// per-script ratios, kc 0.60-0.80, caps T11=2500 +500/tier, minWieldAugs = previous tier's
        /// cap (T11 exempt). Enabled = FALSE — the system is inert until deliberately switched on.
        /// Script keys follow the T11 loot-script names; step 3 (loot stamping) must use the same keys.</summary>
        public static WeaponScalingConfig BuildDefaults()
        {
            var cfg = new WeaponScalingConfig
            {
                Enabled = false,
                KcMin = 0.60,
                KcMax = 0.80,
            };

            // Tiers T11..T25: cap = 2500 + 500 * (tier - 11); minWieldAugs = previous tier's cap.
            // T11's floor is the PRE-EXISTING live gate (2,000 item augs, owner 2026-07-20,
            // LootGenerationFactory.ZoneLootSetWieldItemAugs) — not 0: every T11+ drop already
            // requires it, and ApplyT11WieldRequirement now reads this table per tier.
            for (var tier = 11; tier <= 25; tier++)
            {
                // Item augs purchase-cap at 4,000 (EmoteManager.AugmentationCaps), which the
                // ladder reaches at T15. T16+ freezes the item req there and gates on charm
                // counters instead: Triune Weave + the weapon-family charm, 500 each at T16,
                // +500/tier (owner 2026-08-15).
                cfg.Tiers.Add(new WeaponScalingTier
                {
                    Tier = tier,
                    Cap = 2500 + 500 * (tier - 11),
                    MinWieldAugs = tier == 11 ? 2000 : Math.Min(4000, 2500 + 500 * (tier - 12)),
                    MinWieldCreature = Math.Min(6000, 4000 + 500 * (tier - 11)),
                    MinWieldTriune = tier >= 16 ? 500 * (tier - 15) : 0,
                    MinWieldSkillCharm = tier >= 16 ? 500 * (tier - 15) : 0,
                });
            }

            // One key per weapon FAMILY, weight subtypes merged (owner 2026-08-01). k ranges are
            // EQUAL WITHIN MECHANICS GROUPS (owner, same day): all weapons speed-cap at endgame
            // (item-aug Alacrity aura -1 time/aug, WeaponTime floors at 0 in
            // WorldObject_Weapon.GetWeaponSpeed), so authored per-family damage differences would just
            // crown a best-in-slot — k encodes MECHANICS, not weapon identity. The discount rule
            // follows what the weapon GIVES UP (owner, final):
            //  - multi-strike gives up nothing (longer animation only) -> k discounted by strike count
            //    for per-swing weapon-term parity with singles;
            //  - TWO-HANDED gives up a SHIELD (AL + block + a gem/cantrip slot) -> NO discount: k
            //    equals singles per hit, so both strikes carry it and every damage term doubles per
            //    swing — the +100% weapon-term premium IS the shield compensation.
            // Step-3 mapping: damage mutation file -> family key (heavy_X / light_finesse_X -> X;
            // *_ms own rows; two_handed_cleaver -> cleaver; jitte -> mace; bow/xbow/atlatl elem +
            // non-elem -> launcher key). CASTER rows are seeded for authoring but INERT until the
            // caster wire-in ships (parity audit pending — caster damage rides ElementalDamageMod).
            // Scheme C (2026-08-03, WeaponVariance_SchemeC_Plan): per-family authored variance
            // (config-owned — loot's per-drop DamageVariance roll is display-legacy). k stays
            // UNIFORM within mechanics groups: the EV normalization for variance is LIVE in
            // WeaponScalingCombat.EvNormalization (owner ask: editing a Variance knob must
            // auto-rebalance) — never bake it into these numbers, that would double-dip.
            // GRADE LADDER (owner 2026-08-03): melee families resolve k from a 16-value authored
            // ladder, seeded here at +18 pct per full grade (+5.67 pct per sub-grade) off an S
            // anchor. KMin/KMax are KEPT on every row as the fallback (and as what launchers and
            // casters still actually use) — a family drops back to the lerp the moment its ladder
            // is cleared. Singles anchor S = 0.90; multi-strike anchors 0.40 = the same 0.444x
            // per-swing discount the old 0.40/0.51 band encoded (they strike 2-3x per swing).
            cfg.TightenStrength = 0.7;
            void Melee(string key, double variance, double anchorS = 0.90, double kMin = 0.90, double kMax = 1.15)
                => cfg.Scripts[key] = new WeaponScalingScript
                {
                    KMin = kMin,
                    KMax = kMax,
                    Variance = variance,
                    Grades = BuildLadder(anchorS, variance, cfg.TightenStrength),
                };

            Melee("mace", 0.35);
            Melee("sword", 0.40);
            Melee("staff", 0.45);
            Melee("cleaver", 0.50);
            Melee("two_handed_spear", 0.50);
            Melee("dagger", 0.55);
            Melee("unarmed", 0.55);
            Melee("spear", 0.60);
            Melee("axe", 0.70);
            Melee("sword_ms", 0.40, anchorS: 0.40, kMin: 0.40, kMax: 0.51);
            Melee("dagger_ms", 0.55, anchorS: 0.40, kMin: 0.40, kMax: 0.51);
            // CASTERS (owner 2026-08-06, wire-in): the elemental family's rows are REINTERPRETED
            // as the ElementalDamageMod band, exactly as launchers reinterpret theirs as the
            // DamageMod band. Band ported from the launcher construction so the two lanes scale
            // IDENTICALLY:
            //   real T10 casters roll 1.20-1.22   (every wield >= 500 falls to RollElementalDamageMod's
            //                                      "// 550" default — T9/T10/T11 all roll the same,
            //                                      i.e. casters had NO generational scaling at all)
            //   S  = 1.22 x 1.299 = 1.58          (+29.9 pct over the T10 ceiling, bow's exact bump)
            //   F- = 1.58 x 0.90/1.15 = 1.24      (same F->S ratio as melee, +27.8 pct)
            // The 1.24 floor clears the 1.22 T10 ceiling by +1.6 pct — the same margin by which
            // the launcher floor 3.13 clears its T10 ceiling 3.08. Both properties multiply their
            // lane's WHOLE damage expression, so +29.9 pct on the mod is +29.9 pct on damage in
            // both lanes; the parity is real, not just numerically parallel.
            cfg.Scripts["caster_elemental"] = new WeaponScalingScript { KMin = 1.24, KMax = 1.58 };

            // NON-elemental casters (a plain Orb/Sceptre/Staff/Wand from the wield==0 loot branch)
            // carry NO element and NO ElementalDamageMod, and GetCasterElementalDamageModifier
            // returns a flat 1.0 whenever the caster's damage type does not match the spell's — so
            // there is nothing on this path to scale. Left on the old band deliberately: it is
            // INERT, not tuned. These items do not scale by construction.
            cfg.Scripts["caster_non_elemental"] = new WeaponScalingScript { KMin = 0.90, KMax = 1.15 };

            // Grade drop weights (owner 2026-08-02): S stays ~1-in-1000; A 5 / B 10 / C 15
            // owner-picked, D/F fill per the "gentle ladder" option.
            cfg.GradeWeights["S"] = 0.1;
            cfg.GradeWeights["A"] = 5;
            cfg.GradeWeights["B"] = 10;
            cfg.GradeWeights["C"] = 15;
            cfg.GradeWeights["D"] = 25;
            cfg.GradeWeights["F"] = 44.9;
            // LAUNCHERS (owner 2026-08-01): kMin/kMax are REINTERPRETED as the EFFECTIVE DAMAGE
            // MODIFIER band (replace semantics), not a flat-term coefficient — bows always scaled
            // through their mod (it multiplies ammo + Blood Drinker + elemental, and BD is
            // 0.5 x item augs, so the mod is already aug-coupled). No flat term (double-dip).
            // Band RETUNED 2026-08-02 (owner, two passes): S = 4.00 (just past the pre-system
            // authored 3.90), KMin = 4.00 x (0.90/1.15) = 3.13 — the SAME F->S ratio as the
            // melee k band (+27.8 pct), so grades ladder identically across all weapon types.
            // Floor still clears the real legacy T10 roll ceiling (T10 bows roll 2.84-3.08 in
            // the mutation scripts). Per-hit parity caveat accepted 08-01/08-02; the deferred
            // DPS session can retune — pure config.
            foreach (var launcher in new[] { "bow", "crossbow", "atlatl" })
                cfg.Scripts[launcher] = new WeaponScalingScript { KMin = 3.13, KMax = 4.00 };

            return cfg;
        }

        private static void Load()
        {
            string json = null;
            if (DatabaseManager.ShardConfig.StringExists(StoreKey))
                json = DatabaseManager.ShardConfig.GetString(StoreKey)?.Value;

            if (string.IsNullOrWhiteSpace(json))
            {
                _current = BuildDefaults();
                return;
            }

            var cfg = JsonConvert.DeserializeObject<WeaponScalingConfig>(json);
            _current = cfg != null ? Normalize(cfg) : BuildDefaults();
        }

        /// <summary>Apply an edit to a CLONE of the current config, persist it, and publish the clone.
        /// Combat effect (once wired in) is instant — same synchronous Save->swap shape as ZoneControlManager.</summary>
        public static void Mutate(Action<WeaponScalingConfig> edit)
        {
            Initialize();
            lock (_lock)
            {
                var clone = Clone(_current);
                edit(clone);
                Normalize(clone);
                Save(clone);
                _current = clone;
            }
        }

        /// <summary>Discard in-memory state and re-read the store (or defaults when absent).</summary>
        public static void Reload()
        {
            lock (_lock)
            {
                Load();
                _initialized = true;
            }
        }

        private static WeaponScalingConfig Clone(WeaponScalingConfig cfg)
        {
            return JsonConvert.DeserializeObject<WeaponScalingConfig>(JsonConvert.SerializeObject(cfg));
        }

        /// <summary>The live EV-normalization multiplier for an effective variance. THE one
        /// definition — <see cref="WeaponScalingCombat"/> calls this at swing time and
        /// <see cref="BuildLadder"/> calls it when generating rungs, so the ladder a knob writes
        /// and the damage combat deals can never drift apart. (p = 0.5 crit chance, M = 3.0 crit
        /// cap; see the plan's EV-normalization section before changing the constants.)</summary>
        public static double EvNormalization(double vEff) => 2.0 / (2.0 - 0.25 * vEff);

        /// <summary>v_eff for a family variance + tighten at a given quality.</summary>
        public static double EffectiveVariance(double familyVariance, double tighten, int quality)
        {
            var t = Math.Max(0.0, Math.Min(1.0, quality / (double)QualityMax));
            return familyVariance * (1.0 - tighten * t);
        }

        /// <summary>Generate the 16-value ladder from an S anchor, each rung one
        /// <see cref="LadderStep"/> below the last IN DEALT DAMAGE — not in raw k.
        ///
        /// The distinction is load-bearing: dealt damage is k x augs x EvNormalization(v_eff), and
        /// v_eff RISES as grade falls (lower grades shed less family variance), so EV normalization
        /// quietly inflates the low rungs. A purely geometric k ladder therefore lands +5.6 pct at
        /// the top and only +4.8 pct at the bottom — visibly uneven, which is the exact defect this
        /// whole system was built to remove. Dividing the normalization back out makes every
        /// observed step exactly +5.67 pct. Consequence: the ladder is FAMILY-SPECIFIC (an axe at
        /// variance 0.70 gets different k than a mace at 0.35 for the same damage), which is why
        /// this takes variance/tighten rather than being a shared constant table.</summary>
        public static Dictionary<string, double> BuildLadder(double anchorS, double variance, double tighten)
        {
            var d = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var evAnchor = EvNormalization(EffectiveVariance(variance, tighten, QualityMax));
            for (var i = 0; i < SubGradeBands.Length; i++)
            {
                var ev = EvNormalization(EffectiveVariance(variance, tighten, SubGradeBands[i].QMid));
                d[SubGradeBands[i].Grade] = Math.Round(anchorS * evAnchor / (Math.Pow(LadderStep, i) * ev), 4);
            }
            return d;
        }

        /// <summary>Re-price an authored ladder for a new variance/tighten so DEALT DAMAGE is
        /// unchanged. Because a rung is stored with EV normalization divided out, editing a
        /// family's Variance would otherwise leave every rung compensating for the OLD variance —
        /// the ladder would go uneven and shift level, breaking the owner's standing invariant
        /// that "editing a Variance knob auto-rebalances" (the reason normalization was moved to
        /// swing-time in the first place). Multiplying each rung by evOld/evNew preserves any
        /// HAND-AUTHORED shape, which regenerating from the anchor would silently discard.</summary>
        public static void RebaseLadder(WeaponScalingScript s, double oldVariance, double oldTighten,
                                        double newVariance, double newTighten)
        {
            if (s == null || !s.HasLadder)
                return;

            foreach (var b in SubGradeBands)
            {
                if (!s.Grades.TryGetValue(b.Grade, out var k))
                    continue;
                var evOld = EvNormalization(EffectiveVariance(oldVariance, oldTighten, b.QMid));
                var evNew = EvNormalization(EffectiveVariance(newVariance, newTighten, b.QMid));
                if (evNew > 0)
                    s.Grades[b.Grade] = Math.Round(k * evOld / evNew, 4);
            }
        }

        /// <summary>The sub-grade band a quality roll lands in. Never null — F- catches 0.</summary>
        public static (string Grade, int QMin, int QMax, int QMid) GetSubGradeBand(int quality)
        {
            var q = Math.Clamp(quality, 0, QualityMax);
            foreach (var b in SubGradeBands)
                if (q >= b.QMin)
                    return b;
            return SubGradeBands[SubGradeBands.Length - 1];
        }

        /// <summary>Sub-grade label ("B+") for a quality roll — the appraisal/plugin-facing name.
        /// <see cref="GetQualityGrade"/> stays the FULL-grade label and still drives GradeWeights.</summary>
        public static string GetQualitySubGrade(int quality) => GetSubGradeBand(quality).Grade;

        /// <summary>Roll a drop's quality: weighted grade pick, then uniform inside the band.
        /// Falls back to the legacy uniform 0-1000 roll when no weights are authored.</summary>
        public static int RollQuality()
        {
            Initialize();
            var weights = _current.GradeWeights;

            var total = 0.0;
            if (weights != null)
                foreach (var b in GradeBands)
                    if (weights.TryGetValue(b.Grade, out var w) && w > 0)
                        total += w;
            if (total <= 0)
                return ACE.Common.ThreadSafeRandom.Next(0, QualityMax);   // legacy uniform

            var pick = ACE.Common.ThreadSafeRandom.Next(0f, (float)total);
            var acc = 0.0;
            foreach (var b in GradeBands)
            {
                if (!weights.TryGetValue(b.Grade, out var w) || w <= 0)
                    continue;
                acc += w;
                if (pick <= acc)
                    return b.QMin >= b.QMax ? b.QMin : ACE.Common.ThreadSafeRandom.Next(b.QMin, b.QMax);
            }
            return QualityMax;   // float edge: pick landed exactly on total
        }

        /// <summary>Deserialized dictionaries lose the case-insensitive comparer, and hand-edited
        /// values can arrive inverted or negative — repair rather than reject.</summary>
        public static WeaponScalingConfig Normalize(WeaponScalingConfig cfg)
        {
            cfg.LauncherTierStep = Math.Max(0, cfg.LauncherTierStep);
            cfg.CasterTierStep = Math.Max(0, cfg.CasterTierStep);
            cfg.CasterAuraRescale = Math.Max(0, cfg.CasterAuraRescale);

            cfg.Tiers ??= new List<WeaponScalingTier>();
            cfg.Tiers.RemoveAll(t => t == null);
            cfg.Tiers = cfg.Tiers.GroupBy(t => t.Tier).Select(g => g.First()).OrderBy(t => t.Tier).ToList();
            foreach (var t in cfg.Tiers)
            {
                t.Cap = Math.Max(0, t.Cap);
                t.MinWieldAugs = Math.Max(0, t.MinWieldAugs);
                t.MinWieldCreature = Math.Max(0, t.MinWieldCreature);
                t.MinWieldTriune = Math.Max(0, t.MinWieldTriune);
                t.MinWieldSkillCharm = Math.Max(0, t.MinWieldSkillCharm);

                // Migration for stores saved before the charm gates existed (owner 2026-08-15):
                // a T16+ row with BOTH charm fields 0 predates the feature — seed the +500/tier
                // ladders and pull the item req back to the 4,000 purchase cap it can't exceed.
                if (t.Tier >= 16 && t.MinWieldTriune == 0 && t.MinWieldSkillCharm == 0)
                {
                    t.MinWieldTriune = 500 * (t.Tier - 15);
                    t.MinWieldSkillCharm = 500 * (t.Tier - 15);
                    t.MinWieldAugs = Math.Min(4000, t.MinWieldAugs);
                }

                // Migration for stores saved before MinWieldCreature existed (the column was added
                // 2026-08-31 with the T11+ hit gate). The FRESH-config seed above authors this ladder,
                // but an existing store was written without the field, so every live row loaded as 0 -
                // and TierHitGate reads `MinWieldCreature > 0 && ...`, so a 0 SKIPS the check outright.
                // The gate shipped ON on 2026-09-01 enforcing only Item augs and Triune, with the
                // 4,000-6,000 Creature requirement - the largest component - silently inert. Found
                // 2026-09-01 when `tier curves` printed a creature column of zeros and a suspiciously
                // straight interpolation.
                // 0 is read as UNSET rather than as an authored "no requirement", same assumption the
                // charm migration above makes: the field was never reachable from any command, so no
                // stored 0 can be deliberate.
                if (t.Tier >= 11 && t.MinWieldCreature == 0)
                    t.MinWieldCreature = Math.Min(6000, 4000 + 500 * (t.Tier - 11));
            }

            var scripts = new Dictionary<string, WeaponScalingScript>(StringComparer.OrdinalIgnoreCase);
            if (cfg.Scripts != null)
            {
                foreach (var kv in cfg.Scripts)
                {
                    if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value == null) continue;
                    var s = kv.Value;
                    s.KMin = Math.Max(0, s.KMin);
                    s.KMax = Math.Max(0, s.KMax);
                    if (s.KMax < s.KMin)
                        (s.KMin, s.KMax) = (s.KMax, s.KMin);
                    s.Variance = Math.Max(0, Math.Min(0.95, s.Variance));

                    // Grade ladder: rebuild with the case-insensitive comparer (JSON loses it),
                    // drop unknown sub-grade keys, clamp negatives. An EMPTY dictionary is
                    // normalized to null so HasLadder and the payload agree on "no ladder".
                    // A PARTIAL ladder is completed from the lerp at each missing sub-grade's
                    // midpoint — never left half-authored, because ResolveScriptK would then
                    // silently mix ladder rungs with lerp rungs and the ladder would be uneven
                    // in a way nobody authored.
                    if (s.Grades != null)
                    {
                        var ladder = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                        foreach (var g in SubGradeBands)
                            if (s.Grades.TryGetValue(g.Grade, out var k))
                                ladder[g.Grade] = Math.Max(0, k);

                        if (ladder.Count > 0)
                        {
                            foreach (var g in SubGradeBands)
                                if (!ladder.ContainsKey(g.Grade))
                                    ladder[g.Grade] = ResolveFromQuality(s.KMin, s.KMax, g.QMid);
                            s.Grades = ladder;
                        }
                        else
                            s.Grades = null;
                    }

                    scripts[kv.Key.Trim()] = s;
                }
            }
            cfg.Scripts = scripts;

            cfg.TightenStrength = Math.Max(0, Math.Min(1.0, cfg.TightenStrength));

            var gradeWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (cfg.GradeWeights != null)
                foreach (var kv in cfg.GradeWeights)
                    if (!string.IsNullOrWhiteSpace(kv.Key))
                        gradeWeights[kv.Key.Trim()] = Math.Max(0, kv.Value);
            cfg.GradeWeights = gradeWeights;

            // 2026-08-01 semantics migration: launcher rows became the EFFECTIVE DAMAGE MODIFIER
            // band (replace semantics — quality grades the mod, no flat term). A store written
            // before the change still carries flat-term-era coefficients (~0.9-1.15); resolving
            // those AS the modifier would ~quarter launcher damage. Any launcher row entirely
            // below 2.0 is unmistakably pre-migration (real launcher mods are T10 2.92+) and is
            // bumped to the current defaults; deliberate values >= 2.0 are always respected.
            foreach (var launcherKey in new[] { "bow", "crossbow", "atlatl" })
            {
                if (cfg.Scripts.TryGetValue(launcherKey, out var row) && row.KMax < 2.0)
                {
                    row.KMin = 3.13;
                    row.KMax = 4.00;
                }
            }

            cfg.KcMin = Math.Max(0, cfg.KcMin);
            cfg.KcMax = Math.Max(0, cfg.KcMax);
            if (cfg.KcMax < cfg.KcMin)
                (cfg.KcMin, cfg.KcMax) = (cfg.KcMax, cfg.KcMin);

            return cfg;
        }

        private static void Save(WeaponScalingConfig cfg)
        {
            var jsonOut = JsonConvert.SerializeObject(cfg);
            if (DatabaseManager.ShardConfig.StringExists(StoreKey))
                DatabaseManager.ShardConfig.SaveString(new ConfigPropertiesString { Key = StoreKey, Value = jsonOut, Description = "Weapon aug-scaling config (JSON)" });
            else
                DatabaseManager.ShardConfig.AddString(StoreKey, jsonOut, "Weapon aug-scaling config (JSON)");
        }

        // ── Resolve helpers (consumed by the step-3 combat wire-in; pure math is static for tests) ──

        public static WeaponScalingTier GetTier(int tier)
        {
            return Current.Tiers.FirstOrDefault(t => t.Tier == tier);
        }

        /// <summary>Lerp a 0..1000 quality roll across [min, max]. Out-of-range quality clamps.</summary>
        public static double ResolveFromQuality(double min, double max, int quality)
        {
            var q = Math.Clamp(quality, 0, QualityMax);
            return min + (max - min) * (q / (double)QualityMax);
        }

        /// <summary>Letter grade for a quality roll — the at-a-glance "how good was this roll vs the
        /// best possible" read. Grades the ROLL, not the damage, so it means the same thing on every
        /// family. SCHOOL-STYLE bands (owner 2026-08-01, second revision — the letters now match the
        /// percent intuition): S = a literally PERFECT 100 pct roll (1 in 1,001 — the chase item),
        /// A 90+, B 80+, C 65+, D 50+, F below 50.</summary>
        public static string GetQualityGrade(int quality)
        {
            var q = Math.Clamp(quality, 0, QualityMax);
            if (q >= 1000) return "S";
            if (q >= 900) return "A";
            if (q >= 800) return "B";
            if (q >= 650) return "C";
            if (q >= 500) return "D";
            return "F";
        }

        /// <summary>k for a script row + quality roll. On a family with an authored LADDER, k
        /// INTERPOLATES BETWEEN the two nearest rungs — rungs are anchored at their sub-grade
        /// midpoints, and a roll between two midpoints prices between their k values.
        ///
        /// REVISED 2026-08-06 (owner: "those 2 weapons should not be identical damage"), replacing
        /// the 08-03 flat-per-sub-grade resolution. The defect the ladder was built to fix was never
        /// interpolation itself — it was interpolating across RAW QUALITY, whose grade bands are
        /// wildly uneven (S->A spans 50 quality points, D->F spans 325), which bunched S/A/B within
        /// +7.5 pct of each other. Interpolating between EVENLY-SPACED RUNGS keeps that fix intact:
        /// the rungs still sit +5.67 pct apart in dealt damage and S->F- is still 2.288x, but every
        /// distinct roll now yields distinct damage, so the appraisal percent means something.
        ///
        /// Lerp is in k-space, which keeps every rung's authored value EXACT at its midpoint and
        /// leaves only a second-order ripple in dealt damage between midpoints (EvNormalization
        /// varies slightly across the gap). Rung fidelity matters more than perfect linearity
        /// between them — the rungs are what is authored and what the Damage Chart displays.
        ///
        /// Ladder-less families (launchers' mod band, casters, pre-ladder stores) keep the legacy
        /// KMin/KMax lerp across the full quality range.</summary>
        public static double ResolveScriptK(WeaponScalingScript s, int quality)
        {
            if (!s.HasLadder)
                return ResolveFromQuality(s.KMin, s.KMax, quality);

            var q = Math.Clamp(quality, 0, QualityMax);

            // SubGradeBands is ordered best -> worst, so midpoints DESCEND. Walk to the first rung
            // whose midpoint the roll is at or above, then lerp against the rung one better.
            for (var i = 0; i < SubGradeBands.Length; i++)
            {
                if (q < SubGradeBands[i].QMid)
                    continue;

                if (!s.Grades.TryGetValue(SubGradeBands[i].Grade, out var kLow))
                    break;

                if (i == 0)                       // at or above the S midpoint (q1000) — no rung above
                    return kLow;

                if (!s.Grades.TryGetValue(SubGradeBands[i - 1].Grade, out var kHigh))
                    return kLow;

                var span = SubGradeBands[i - 1].QMid - SubGradeBands[i].QMid;
                if (span <= 0)
                    return kLow;

                var t = (q - SubGradeBands[i].QMid) / (double)span;
                return kLow + (kHigh - kLow) * t;
            }

            // Below the worst rung's midpoint (F- sits at q83): F- is the floor, nothing beneath it.
            return s.Grades.TryGetValue(SubGradeBands[SubGradeBands.Length - 1].Grade, out var kFloor)
                ? kFloor
                : ResolveFromQuality(s.KMin, s.KMax, q);
        }

        /// <summary>The weapon-term damage a roll actually produces, in the same units the combat
        /// path deals it: k(quality) x EvNormalization(v_eff(quality)). Augs and tier cap are
        /// deliberately excluded — they are wielder-side and identical for any two weapons being
        /// compared, so this is the pure weapon contribution.</summary>
        public static double DealtWeaponTerm(WeaponScalingScript s, double tighten, int quality)
        {
            if (s == null)
                return 0;
            var k = ResolveScriptK(s, quality);
            if (s.Variance <= 0)
                return k;                          // launchers/casters: no variance, no normalization
            return k * EvNormalization(EffectiveVariance(s.Variance, tighten, quality));
        }

        /// <summary>Percent of a PERFECT roll's damage this quality delivers — the appraisal's
        /// "pct of max" (owner 2026-08-06: "everyone really likes the percent and it needs to be
        /// accurate"). Measures DAMAGE, not the quality percentile: the old display printed
        /// quality/10, so an F- weapon read "0 pct of max" while dealing 41.7 pct of an S weapon's
        /// damage, and — before the inter-rung lerp landed — two identical B+ weapons read 86 pct
        /// and 88 pct. Range is therefore ~42-100 pct on a laddered family, not 0-100.</summary>
        public static int RelativeDamagePercent(WeaponScalingScript s, double tighten, int quality)
        {
            var max = DealtWeaponTerm(s, tighten, QualityMax);
            if (max <= 0)
                return 0;
            return (int)Math.Round(DealtWeaponTerm(s, tighten, quality) / max * 100.0);
        }

        /// <summary>The wielder-facing k for a script + quality roll, or null when the script is unknown
        /// (unknown script = weapon contributes no scaling term; loud in logs at the wire-in, silent here).</summary>
        public static double? ResolveK(string script, int quality)
        {
            if (string.IsNullOrWhiteSpace(script))
                return null;
            if (!Current.Scripts.TryGetValue(script, out var s))
                return null;
            return ResolveScriptK(s, quality);
        }

        public static double ResolveKc(int quality)
        {
            var cfg = Current;
            return ResolveFromQuality(cfg.KcMin, cfg.KcMax, quality);
        }
    }
}
