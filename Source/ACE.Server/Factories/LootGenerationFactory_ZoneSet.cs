using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;
using ACE.Server.Factories.Tables;
using ACE.Server.Factories.Tables.Wcids;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories
{
    public static partial class LootGenerationFactory
    {
        // =====================================================================================
        // Structured tier-11+ loot set ("T11" endgame loot)
        //
        // Every kill from a tier-11+ treasure profile drops one weapon per FAMILY (subtype/skill
        // randomized inside the pick) plus a fixed gear allotment (armor / jewelry / cloak). All
        // pieces roll BLANK (isMagical = false -> no legacy spell sets); useful cantrips/spells/
        // procs come from the Zone Control post-roll mutation layer (ZoneLootMutator).
        // Quality/tier come from the (QB-scaled) treasure profile passed in.
        //
        // This set is the DEFAULT behaviour of a tier-11+ profile and does NOT require Zone
        // Control: the T11 profiles carry zero item/magic/mundane chances of their own, so this
        // generator is what makes them drop anything at all. Drops are PER-SLOT (owner decision
        // 2026-07-20, "no set enabler"): every equip slot has its own count, defaulting to 1 at
        // tier 11+ and 0 below. A resolved zone profile overrides individual slot counts via the
        // loot_slot_* stats; setting a slot to 0 turns just that slot off, and there is no
        // separate enable flag. With Zone Control absent, tier-11+ mobs drop one of everything.
        //
        // Design doc: C:\AI\ZoneControl\ACE_Loot_Systems_DeepDive_2026-07-17.md §12-13
        // =====================================================================================

        /// <summary>
        /// Lowest treasure tier that drops the structured set by default (no Zone Control needed).
        /// </summary>
        public const int ZoneLootSetMinTier = 11;

        /// <summary>Death-treasure profile handed to a creature that dies inside a governed v11+ zone with NO
        /// table of its own (owner 2026-08-23, zone loot floor): 73001 = the Tou Tou T11 profile.</summary>
        public const uint ZoneLootFallbackProfile = 73001;

        /// <summary>
        /// Per-slot drop counts for one kill. Weapons = drops PER FAMILY (9 families). Each armor
        /// slot counts pieces whose coverage includes that slot; a multi-slot piece (coat) credits
        /// every slot it covers, so covered slots don't roll again.
        /// </summary>
        public class ZoneLootSetCounts
        {
            public int Weapons;
            public int Helm, Chest, Shoulder, Bracer, Glove, Girth, UpperLeg, LowerLeg, Boot;
            public int Shield;
            public int Amulet, Ring, Bracelet, Trinket;
            public int Cloak;

            /// <summary>BUDGET MODE only: the exact weapon families to create, one weapon each. Null in
            /// legacy mode, where Weapons is instead a MULTIPLIER over every family (1 = all nine).</summary>
            public List<int> WeaponFamilyPicks;

            public bool Any =>
                (WeaponFamilyPicks != null && WeaponFamilyPicks.Count > 0) ||
                Weapons > 0 || Helm > 0 || Chest > 0 || Shoulder > 0 || Bracer > 0 || Glove > 0 ||
                Girth > 0 || UpperLeg > 0 || LowerLeg > 0 || Boot > 0 || Shield > 0 ||
                Amulet > 0 || Ring > 0 || Bracelet > 0 || Trinket > 0 || Cloak > 0;

            /// <summary>Tier default: one of everything at tier 11+, nothing below.</summary>
            public static ZoneLootSetCounts TierDefault(int tier)
            {
                var n = tier >= ZoneLootSetMinTier ? 1 : 0;
                return new ZoneLootSetCounts
                {
                    Weapons = n,
                    Helm = n, Chest = n, Shoulder = n, Bracer = n, Glove = n,
                    Girth = n, UpperLeg = n, LowerLeg = n, Boot = n,
                    Shield = n, Amulet = n, Ring = n, Bracelet = n, Trinket = n, Cloak = n,
                };
            }
        }

        // Jewelry pools by equip slot. Crowns/coronets are deliberately excluded: they roll down
        // the armor path (they have an armor level) and would duplicate the Head slot.
        private static readonly WeenieClassName[] zoneSetNeckWcids =
            { WeenieClassName.amulet, WeenieClassName.gorget, WeenieClassName.necklace, WeenieClassName.necklaceheavy };

        private static readonly WeenieClassName[] zoneSetWristWcids =
            { WeenieClassName.bracelet, WeenieClassName.braceletheavy };

        private static readonly WeenieClassName[] zoneSetFingerWcids =
            { WeenieClassName.ring, WeenieClassName.ringjeweled };

        private static readonly WeenieClassName[] zoneSetTrinketWcids =
        {
            WeenieClassName.ace41483_compass, WeenieClassName.ace41484_goggles,
            WeenieClassName.ace41487_mechanicalscarab, WeenieClassName.ace41486_puzzlebox,
            WeenieClassName.ace41485_pocketwatch, WeenieClassName.ace41488_top,
        };

        // wcid -> ClothingPriority, read from the cached weenie so candidate pieces can be
        // accepted/rejected by slot WITHOUT instantiating a WorldObject for each attempt.
        private static readonly ConcurrentDictionary<WeenieClassName, ACE.Entity.Enum.CoverageMask> zoneSetCoverage = new();

        private static ACE.Entity.Enum.CoverageMask GetZoneSetCoverage(WeenieClassName wcid)
        {
            return zoneSetCoverage.GetOrAdd(wcid, w =>
            {
                var weenie = DatabaseManager.World.GetCachedWeenie((uint)w);
                return (ACE.Entity.Enum.CoverageMask)(weenie?.GetProperty(PropertyInt.ClothingPriority) ?? 0);
            });
        }

        /// <summary>
        /// Removes ALL wield requirements from a tier-11+ drop (owner 2026-07-20: level reqs first,
        /// then ALL reqs -- an item-augmentation wield requirement will replace them later).
        ///
        /// This is a final sweep rather than a fix at each producer, because a requirement can
        /// arrive from three independent places: a mutation script, factory code (e.g.
        /// MutateCloak's ItemMaxLevel -> WieldDifficulty, SetWieldT10's MeleeDefense gate), or the
        /// BASE WEENIE itself -- cloaks in particular ship with WieldRequirements = Level already
        /// set, which no mutation ever clears. Patching producers one at a time misses that.
        /// </summary>
        /// <summary>Item augmentations required to wield any tier-11+ drop (owner 2026-07-20).</summary>
        public const int ZoneLootSetWieldItemAugs = 2000;

        /// <summary>
        /// The tier-11+ wield gate: item augmentations (LumAugItemCount), replacing every
        /// requirement StripWieldRequirements removed. Validated server-side by the
        /// WieldRequirement.Int64Stat case. The client cannot render this requirement type, so
        /// the LongDesc block appends the line instead.
        ///
        /// PER-TIER (T11 weapon relevance plan §7.10): the floor comes from the weaponscaling_data
        /// tier table (minWieldAugs — the market-segmentation gate: minWield(Tn) = cap(Tn-1)),
        /// editable live in the plugin's Weapons panel; a missing tier row or a 0 value falls back
        /// to the legacy 2000 constant, preserving pre-plan behavior. Applies to ALL T11+ drops
        /// (weapons, armor, jewelry) — the gate is the tier's, not the weapon system's.
        /// </summary>
        public static void ApplyT11WieldRequirement(WorldObject wo, int tier = 0)
        {
            if (wo == null)
                return;

            var minWield = ZoneLootSetWieldItemAugs;
            var tierRow = ACE.Server.Managers.WeaponScaling.WeaponScalingManager.GetTier(tier);
            if (tierRow != null && tierRow.MinWieldAugs > 0)
                minWield = tierRow.MinWieldAugs;

            wo.WieldRequirements = ACE.Entity.Enum.WieldRequirement.Int64Stat;
            wo.WieldSkillType = (int)PropertyInt64.LumAugItemCount;
            wo.WieldDifficulty = minWield;

            // T16+ charm gates (owner 2026-08-15): the item-aug ladder purchase-caps at 4,000
            // (reached at T15), so higher tiers freeze the item req and gate on growth charm
            // counters instead — Triune Weave plus the weapon-family charm, both +500/tier.
            // WEAPONS ONLY, and slots 3/4: slot 2 belongs to the forge's training requirement.
            var isWeapon = wo is MeleeWeapon || wo is MissileLauncher || wo is Caster;
            if (isWeapon && tierRow != null && tierRow.MinWieldTriune > 0)
            {
                wo.WieldRequirements3 = ACE.Entity.Enum.WieldRequirement.Int64Stat;
                wo.WieldSkillType3 = (int)PropertyInt64.TriuneWeaveCount;
                wo.WieldDifficulty3 = tierRow.MinWieldTriune;
            }
            if (isWeapon && tierRow != null && tierRow.MinWieldSkillCharm > 0)
            {
                wo.WieldRequirements4 = ACE.Entity.Enum.WieldRequirement.Int64Stat;
                wo.WieldSkillType4 = (int)GetWieldCharmProperty(wo);
                wo.WieldDifficulty4 = tierRow.MinWieldSkillCharm;
            }

            // Armor / jewelry / cloaks at T16+ (owner 2026-08-23: "4k item augs isn't enough - Item Augs +
            // Triune Count"): slot 3 = Triune Weave count on the same 500 x (tier-15) ladder the weapons use.
            // No family charm for non-weapons (none exists).
            if (!isWeapon && tierRow != null && tierRow.MinWieldTriune > 0)
            {
                wo.WieldRequirements3 = ACE.Entity.Enum.WieldRequirement.Int64Stat;
                wo.WieldSkillType3 = (int)PropertyInt64.TriuneWeaveCount;
                wo.WieldDifficulty3 = tierRow.MinWieldTriune;
            }

            // The client cannot render Int64Stat requirements. WEAPONS show the gate in the
            // Property Details section instead (AppraiseInfo, pinned bottom - owner 2026-08-01);
            // armor/jewelry have no such section, so they keep this LongDesc line.
            if (!isWeapon)
                AppendLongDescLine(wo, WieldLineFor(wo));
        }

        /// <summary>LIVE re-stamp of a recorded NON-weapon's wield gates from the current tier row (owner 2026-08-23:
        /// "change live and for existing pieces"). Called from ZoneStatResolver.Apply so every equip / login /
        /// ladder apply refreshes slot 1 (item augs) and slot 3 (T16+ Triune Weave) and rewrites the LongDesc
        /// line. Weapons are left to the weapon-scaling system. Returns the number of properties changed.</summary>
        public static int RefreshWieldGate(WorldObject wo, int tier)
        {
            if (wo == null || wo is MeleeWeapon || wo is MissileLauncher || wo is Caster)
                return 0;

            // WIELD GATES IGNORE zonecontrol_enabled ON PURPOSE (owner 2026-08-23). Clearing them off the
            // switch was built, then removed: a wield requirement is checked ONLY at the moment of equipping
            // (Player_Inventory.DoHandleActionGetAndWieldItem -> CheckWieldRequirements) and never again -
            // there is no login revalidation and no periodic sweep. An ungated window is therefore permanent
            // for anyone who equips during it: switch off -> gate gone -> a 0-aug character equips a T25
            // piece -> switch on -> the piece re-resolves to full ladder stats while the restored gate is
            // never rechecked, because they are already wearing it. They keep it until they choose to
            // unequip. Keeping the gate costs only cosmetics (a T25 piece asks 5,000 Triune while its stats
            // read T10 in fallback); clearing it costs a hole. DO NOT re-add the clearing branch.
            var tierRow = ACE.Server.Managers.WeaponScaling.WeaponScalingManager.GetTier(tier);
            var minWield = tierRow != null && tierRow.MinWieldAugs > 0 ? tierRow.MinWieldAugs : ZoneLootSetWieldItemAugs;
            var triune = tierRow != null ? tierRow.MinWieldTriune : 0;
            var changed = 0;

            if (wo.WieldRequirements != ACE.Entity.Enum.WieldRequirement.Int64Stat || wo.WieldSkillType != (int)PropertyInt64.LumAugItemCount || wo.WieldDifficulty != minWield)
            {
                wo.WieldRequirements = ACE.Entity.Enum.WieldRequirement.Int64Stat;
                wo.WieldSkillType = (int)PropertyInt64.LumAugItemCount;
                wo.WieldDifficulty = minWield;
                changed++;
            }
            if (triune > 0)
            {
                if (wo.WieldRequirements3 != ACE.Entity.Enum.WieldRequirement.Int64Stat || wo.WieldSkillType3 != (int)PropertyInt64.TriuneWeaveCount || wo.WieldDifficulty3 != triune)
                {
                    wo.WieldRequirements3 = ACE.Entity.Enum.WieldRequirement.Int64Stat;
                    wo.WieldSkillType3 = (int)PropertyInt64.TriuneWeaveCount;
                    wo.WieldDifficulty3 = triune;
                    changed++;
                }
            }
            else if (wo.WieldRequirements3 == ACE.Entity.Enum.WieldRequirement.Int64Stat && wo.WieldSkillType3 == (int)PropertyInt64.TriuneWeaveCount)
            {
                wo.WieldRequirements3 = ACE.Entity.Enum.WieldRequirement.Invalid;
                wo.WieldSkillType3 = null;
                wo.WieldDifficulty3 = null;
                changed++;
            }

            // rewrite the LongDesc gate block so the appraisal matches the live gate. The block sits in
            // its own paragraph (blank line before it) and each requirement has its own line (owner 2026-08-23).
            var want = WieldLineFor(wo);
            var cur = wo.LongDesc ?? string.Empty;
            if (!cur.Contains(want))
            {
                var kept = cur.Split('\n').Where(l => !l.StartsWith("Wield requires:")).ToList();
                // drop the blank lines that separated the old gate block so we don't stack paragraphs
                while (kept.Count > 0 && kept[^1].Trim().Length == 0) kept.RemoveAt(kept.Count - 1);
                var head = string.Join("\n", kept).Trim('\n');
                wo.LongDesc = head.Length > 0 ? head + "\n\n" + want : want;
                changed++;
            }
            return changed;
        }

        /// <summary>The "Wield requires:" LongDesc block for a non-weapon: one line per requirement -
        /// item augs, then the T16+ Triune gate when stamped (owner 2026-08-23).</summary>
        private static string WieldLineFor(WorldObject wo)
        {
            var line = $"Wield requires: {wo.WieldDifficulty ?? 0:N0} Item Augmentations";
            if (wo.WieldRequirements3 == ACE.Entity.Enum.WieldRequirement.Int64Stat &&
                wo.WieldSkillType3 == (int)PropertyInt64.TriuneWeaveCount && (wo.WieldDifficulty3 ?? 0) > 0)
                line += $"\nWield requires: {wo.WieldDifficulty3 ?? 0:N0} Triune Weave";
            return line;
        }

        /// <summary>Standard weapon mods (owner 2026-08-15): every T10+ weapon and wand leaves
        /// the loot/forge pipeline with EXACTLY +20 pct attack mod and +20 pct melee defense
        /// (wands additionally 20 pct mana conversion), replacing the rolled values — so tier
        /// difficulty can be tuned against KNOWN player mods ("predictable hit or not hit
        /// levers"). New drops/forges only; existing items keep what they rolled.</summary>
        public static void ApplyStandardWeaponMods(WorldObject wo, int tier)
        {
            if (wo == null || tier < 10)
                return;
            if (!(wo is MeleeWeapon || wo is MissileLauncher || wo is Caster))
                return;

            wo.WeaponOffense = 1.20;
            wo.WeaponDefense = 1.20;
            if (wo is Caster)
                wo.ManaConversionMod = 0.20;
        }

        /// <summary>The weapon-family growth charm whose counter gates T16+ wield (owner
        /// 2026-08-15): melee = Crashing Steel, launchers = True Shot, nether casters =
        /// Nether Veil, every other caster = Battlemage's Wrath.</summary>
        public static PropertyInt64 GetWieldCharmProperty(WorldObject wo)
        {
            if (wo is MissileLauncher)
                return PropertyInt64.TrueShotCharmCount;
            if (wo is Caster)
                return wo.W_DamageType == ACE.Entity.Enum.DamageType.Nether
                    ? PropertyInt64.NetherVeilCharmCount
                    : PropertyInt64.BattlemagesWrathCharmCount;
            return PropertyInt64.CrashingSteelCharmCount;
        }

        /// <summary>Append one line to an item's LongDesc (the LIVE description path - the
        /// provenance line from ZoneLootMutator lands the same way). Idempotent per line text.</summary>
        private static void AppendLongDescLine(WorldObject wo, string line)
        {
            if (wo == null || string.IsNullOrWhiteSpace(line))
                return;
            var cur = wo.LongDesc;
            if (!string.IsNullOrEmpty(cur) && cur.Contains(line))
                return;
            wo.LongDesc = string.IsNullOrEmpty(cur) ? line : cur + "\n" + line;
        }

        /// <summary>
        /// Stamp the aug-scaling identity on a tier-11+ WEAPON drop: a QUALITY percentile roll
        /// (0-1000) and the loot TIER. That is ALL the item ever carries — k ranges, tier caps and
        /// kc live in the weaponscaling_data config and resolve at swing time, so plugin edits
        /// re-price every stamped weapon retroactively (plan §9.1). Stamped even while the master
        /// switch is OFF (inert data; flipping Enabled later activates existing drops). Casters
        /// are stamped too — inert until the caster wire-in.
        /// </summary>
        public static void ApplyWeaponAugScaleStamp(WorldObject wo, int tier)
        {
            if (wo == null || tier < ZoneLootSetMinTier)
                return;
            if (!(wo is MeleeWeapon || wo is MissileLauncher || wo is Caster))
                return;

            // Weighted grade roll (owner 2026-08-02): grade from the config weights table
            // (S ~1-in-1000, A 5 / B 10 / C 15 / D 25 / F 44.9 seeds), uniform within the band.
            var quality = ACE.Server.Managers.WeaponScaling.WeaponScalingManager.RollQuality();
            wo.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.WeaponAugScaleQuality, quality);
            wo.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.WeaponAugScaleTier, tier);

            // Visible identity line (the sectioned composer with the original line is dead code -
            // this is the live LongDesc path, same as the wield gate + provenance lines). Grade
            // first for the at-a-glance read; the percent says how close to a perfect roll this
            // copy came (owner 2026-08-01: the raw 0-1000 fraction confused even the owner).
            // The grade line lives in the Property Details section (AppraiseInfo, pinned top -
            // owner 2026-08-01), computed live from the quality prop; nothing baked here.
        }

        /// <summary>Final pass over a T11+ drop's description (owner 2026-08-01): drop the inherited
        /// weenie flavor text (e.g. a bare "Claw" line), keep our known lines in stamp order, and
        /// move the ZoneLootMutator provenance ("Dropped by ...") to the very bottom. Runs LAST in
        /// the Creature_Death presentation sweep. Whitelist-based: a future line-adder must either
        /// run after this or register its prefix here.</summary>
        public static void FinalizeT11LongDesc(WorldObject wo)
        {
            var ld = wo?.LongDesc;
            if (string.IsNullOrWhiteSpace(ld))
                return;

            var keep = new System.Text.StringBuilder();
            var provenance = new System.Text.StringBuilder();
            foreach (var raw in ld.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0)
                    continue;
                if (line.StartsWith("Dropped by") || line.StartsWith("Dropped in ") || line.StartsWith("Location:"))
                    provenance.Append(provenance.Length > 0 ? "\n" : "").Append(line);
                // A "Creature Augmentation:" line used to be regenerated here from prop 50213.
                // The fixed base that stamped it was deleted 2026-08-22 - Creature Augs is now a
                // plain banded cantrip (key 35) and stamps "Zone Cantrip: Creature Augs" like the
                // other six augs. Legacy drops still carrying the old line fall through to the
                // discard below, which is what we want: the line is stale by definition.
                else if (line.StartsWith("Wield requires:") || line.StartsWith("Weapon Grade:") || line.StartsWith("Aug Scaling")
                      || line.StartsWith("Zone Cantrip:"))
                    keep.Append(keep.Length > 0 ? "\n" : "").Append(line);
                // anything else = inherited weenie flavor text - discarded by design
            }

            if (provenance.Length > 0)
                keep.Append(keep.Length > 0 ? "\n\n" : "").Append(provenance);
            wo.LongDesc = keep.Length > 0 ? keep.ToString() : null;
        }

        public static void StripWieldRequirements(WorldObject wo)
        {
            if (wo == null)
                return;

            if (wo.WieldRequirements != ACE.Entity.Enum.WieldRequirement.Invalid)
            {
                wo.WieldRequirements = ACE.Entity.Enum.WieldRequirement.Invalid;
                wo.WieldSkillType = null;
                wo.WieldDifficulty = null;
            }

            if (wo.WieldRequirements2 != ACE.Entity.Enum.WieldRequirement.Invalid)
            {
                wo.WieldRequirements2 = ACE.Entity.Enum.WieldRequirement.Invalid;
                wo.WieldSkillType2 = null;
                wo.WieldDifficulty2 = null;
            }
        }

        private static readonly ACE.Entity.Enum.Properties.PropertyFloat[] armorModVsProps =
        {
            ACE.Entity.Enum.Properties.PropertyFloat.ArmorModVsSlash,
            ACE.Entity.Enum.Properties.PropertyFloat.ArmorModVsPierce,
            ACE.Entity.Enum.Properties.PropertyFloat.ArmorModVsBludgeon,
            ACE.Entity.Enum.Properties.PropertyFloat.ArmorModVsFire,
            ACE.Entity.Enum.Properties.PropertyFloat.ArmorModVsCold,
            ACE.Entity.Enum.Properties.PropertyFloat.ArmorModVsAcid,
            ACE.Entity.Enum.Properties.PropertyFloat.ArmorModVsElectric,
            ACE.Entity.Enum.Properties.PropertyFloat.ArmorModVsNether,
        };

        /// <summary>
        /// Equalizes a tier-11+ armor piece's eight resistance multipliers to their mean (owner
        /// 2026-07-20: one uniform protection value instead of eight per-element spreads). Budget-
        /// neutral: the mean of the rolled mods, applied to every mod the piece actually has.
        /// The stock client panel now renders its eight per-element lines with one identical
        /// value each (panel takeover reverted 2026-07-21).
        /// </summary>
        /// <summary>
        /// Tier-11+ deterministic gear budget (owner plan 2026-08-20/21): every non-weapon
        /// piece of zone-set loot and /asforge gear carries the same fixed per-slot stats,
        /// doubling per tier - "fixed base, variable extras", all drop variance lives in
        /// cantrips so damage-taken tuning works against a known floor. C# is the DEFAULT
        /// layer only; the cantrip stamps run AFTER this and layer on top (the armor_al_* zone knobs were removed 2026-08-23).
        /// Shared by Creature_Death (loot sweep) and TestCharacterCommands (/asforge) so
        /// premades match drops exactly. Classifier is ItemType: Armor=2 covers body armor
        /// AND shields; Clothing=4 is shirt/pants/cloak (never authored AL); Jewelry=8
        /// contributes no AL by engine rule (only WeenieType.Clothing is an armor layer).
        /// </summary>
        public static void ApplyT11GearStats(WorldObject wo, int tier, bool forceMax = false,
            ACE.Server.Managers.ZoneScaling.EvaluatedProfile p = null, double? coreFrac = null)
        {
            if (wo == null || tier < ZoneLootSetMinTier)
                return;

            // THE ANCHORED LINEAR LADDER (owner 2026-08-21, supersedes the rejected doubling):
            // T25 best-in-slot anchors - 2,500 armor PER PIECE; SET totals 2,500 Damage Resist /
            // 1,500 CritDmgResist / CritResist / NetherResist. T11 BiS = half the anchor; per-tier
            // value = base x (1 + (t-11)/14), so every stat exactly doubles across the T11->T25
            // journey and armor steps a flat +100/tier.
            //
            // Armor v2 (owner 2026-08-21 afternoon, Cantrip_Band_Ladder v2 section 1): the
            // GUARANTEED defensive core is the four resist ratings, ROLLED uniformly inside a
            // per-tier window instead of written as a constant, FLAT across all 18 slots (the
            // inherited 50/30/55 armor/clothing/jewelry weighting is dead). Per piece:
            //   cap(t)  = anchor/18 x (1 + (t-11)/14)
            //   step    = anchor/18/14            (FIXED T11 step, not per-tier cap/14)
            //   floor   = cap - 1.5 step          (T11: cap - 0.5 step - no previous tier)
            // Rounded at the END only: rounding the cap first breaks the 750-class T11 window
            // (spec wants 40-42). Damage Rating and Max Health are NOT here any more - they are
            // random chase lines (keys 28 / 19). Mobs are tuned against the FLOOR of the core.

            // zone override surface for the anchors (core_anchor_dr / core_anchor_cdr); C# is the default
            var anchorDr = p?.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.CoreAnchorDr, 1250.0) ?? 1250.0;
            var anchorCdr = p?.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.CoreAnchorCdr, 750.0) ?? 750.0;

            // Live stat resolution (owner 2026-08-22): the core four are GRADED. Window = the same
            // formula (ZoneStatResolver.CoreWindow, fed the ZONE's evaluated anchor so the drop is
            // identical to before), grade = uniform 0-1000 inside it (the core is uniform today and
            // stays so - RollGrade's tier thirds are for the cantrip lines only), value = ValueFor.
            // The grade is recorded in ZcModifiers; the Gear* prop is its cache. A piece is authored from
            // scratch here, so any pre-existing record is cleared first.
            ACE.Server.Managers.ZoneControl.ZoneStatResolver.Write(wo, null);
            int RollCore(int coreKey, double anchor)
            {
                var (min, max) = ACE.Server.Managers.ZoneControl.ZoneStatResolver.CoreWindow(coreKey, tier, anchor);
                int grade;
                if (forceMax) grade = ACE.Server.Managers.ZoneControl.ZoneStatResolver.GradeMax;
                // coreFrac: deterministic point inside the window (premade "Average" suits pass
                // 0.5 = the midpoint). forceMax still wins above; null = the live random roll.
                else if (coreFrac.HasValue) grade = (int)System.Math.Round(coreFrac.Value * ACE.Server.Managers.ZoneControl.ZoneStatResolver.GradeMax);
                else grade = ThreadSafeRandom.Next(0, ACE.Server.Managers.ZoneControl.ZoneStatResolver.GradeMax);   // inclusive both ends
                ACE.Server.Managers.ZoneControl.ZoneStatResolver.AddLine(wo, coreKey, grade);
                return ACE.Server.Managers.ZoneControl.ZoneStatResolver.ValueFor(min, max, grade);
            }

            switch (wo.ItemType)
            {
                case ACE.Entity.Enum.ItemType.Armor:
                    // RESOLVE-stage stat: armor_base_level on the tier Default, else 1100 + 100 x (tier-11).
                    // This same call runs again inside ZoneStatResolver.Compute on every equip / login, so
                    // re-authoring it re-prices EXISTING pieces - unlike the two protection knobs below.
                    wo.ArmorLevel = ACE.Server.Managers.ZoneControl.ZoneStatResolver.BaseArmorLevel(tier);

                    // Every element authored explicitly: ArmorModVs* defaults to 0.0 and
                    // MULTIPLIES the piece AL - an absent prop is literally zero protection
                    // for that element. Fill absent with armor_prot_base (default 1.0 = the
                    // Average band), then equalize unless armor_prot_equalize is switched off.
                    //
                    // DROP-stage stats, BOTH of them: these are stamped onto the piece here and never read
                    // again. Changing either moves NEW DROPS ONLY; nothing already in a backpack shifts.
                    // (Armor_Base_Values_Plan_2026-08-24.md sections 2.2 / 2.3.)
                    var protBase = ACE.Server.Managers.ZoneControl.ZoneStatResolver.ArmorProtBase(tier, p);
                    foreach (var prop in armorModVsProps)
                        if (!wo.GetProperty(prop).HasValue)
                            wo.SetProperty(prop, protBase);
                    EqualizeT11ArmorResists(wo, tier, p);
                    break;

                case ACE.Entity.Enum.ItemType.Clothing:
                case ACE.Entity.Enum.ItemType.Jewelry:
                    break;

                default:
                    // weapons / casters / ammo: the weapon-scaling system owns their STATS - but the
                    // TIER is stamped here anyway as of 2026-08-25 (owner: "stamp ZcTier on weapons too").
                    //
                    // WHY. ZoneCraftGate.TierOf is max(ZcTier, WeaponAugScaleTier), and the entire T11+
                    // crafting gate returns immediately when that reads 0. Weapons carried ONLY
                    // WeaponAugScaleTier, so if that one stamp fails to land the gate is silently OFF for
                    // every weapon on the shard - which is how an unguarded SetValue recipe (the Bag of
                    // Abyssal-Touched Gems, recipe 527870098) can overwrite a rolled Biting Strike down to
                    // 0.33 with nothing refusing it. On 2026-08-25 exactly 2 items on the shard carried
                    // WeaponAugScaleTier and both were hand-made, so that was the live state, not a theory.
                    //
                    // The old rule "a weapon must not carry ZcTier" was a CONVENTION that documented
                    // itself as a constraint. It is not one: both TierOf implementations take a plain max
                    // and never branch on WHICH property is present, and the only code that decides "is
                    // this armour" is ZoneStatResolver.Compute, which keys on ItemType + ArmorLevel and
                    // not on ZcTier. So a weapon carrying ZcTier resolves its tier identically and cannot
                    // accidentally acquire an ArmorLevel.
                    //
                    // Tested on ItemType, deliberately NOT on the runtime `wo is MeleeWeapon ||
                    // MissileLauncher || Caster` that ApplyWeaponAugScaleStamp uses. Two INDEPENDENT
                    // signals for the same fact: if a weapon somehow fails one test it still gets a tier
                    // from the other. Belt and braces on purpose - the gate being live matters more than
                    // tidiness about which property carries the row.
                    //
                    // Only the tier. No StampIdentity, no ZcResolvedVersion: the resolve stamp has to be
                    // written AFTER the weapon's grades are recorded, and that happens later, in
                    // ZoneLootMutator (StampWeaponResolve). Stamping a version here would mark the weapon
                    // resolved before its record existed.
                    if ((wo.ItemType & ACE.Entity.Enum.ItemType.WeaponOrCaster) != 0)
                        wo.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.ZcTier, tier);
                    return;
            }

            // identity FIRST: tier + current ladder version, so StampGraded's key-25 path (TierOf) and
            // Compute see the tier before any line lands
            ACE.Server.Managers.ZoneControl.ZoneStatResolver.StampIdentity(wo, tier);

            // Damage / Max Health left the fixed base (random pool now). REMOVE rather than skip:
            // TryMutateGearRatingT10's coin-flip rolls would otherwise leak onto T11 drops.
            // 2026-08-23: the same mutator also coin-flips Healing Boost (jewelry) and Crit
            // Damage (armor) - a T11 bracelet shipped with Healing Boost 100 (the T10 rating) and
            // no key-31 line. Strip every line-produced Gear* it can stamp; the ZcModifiers record is
            // the only source of truth on T11+.
            wo.RemoveProperty(ACE.Entity.Enum.Properties.PropertyInt.GearDamage);
            wo.RemoveProperty(ACE.Entity.Enum.Properties.PropertyInt.GearMaxHealth);
            wo.RemoveProperty(ACE.Entity.Enum.Properties.PropertyInt.GearHealingBoost);
            wo.RemoveProperty(ACE.Entity.Enum.Properties.PropertyInt.GearCritDamage);

            wo.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.GearDamageResist, RollCore(ACE.Server.Managers.ZoneControl.ZoneStatResolver.CoreDamageResist, anchorDr));
            wo.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.GearCritDamageResist, RollCore(ACE.Server.Managers.ZoneControl.ZoneStatResolver.CoreCritDamageResist, anchorCdr));
            wo.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.GearCritResist, RollCore(ACE.Server.Managers.ZoneControl.ZoneStatResolver.CoreCritResist, anchorCdr));
            wo.SetProperty(ACE.Entity.Enum.Properties.PropertyInt.GearNetherResist, RollCore(ACE.Server.Managers.ZoneControl.ZoneStatResolver.CoreNetherResist, anchorCdr));

            // Gear Creature Augs used to get a fixed 30/20/12 x scale base here, written straight
            // into prop 50213 alongside its own "Creature Augmentation:" LongDesc line. DELETED
            // 2026-08-22 (owner): it pre-dated the 08-21 band pass and was never removed, so
            // Creature Augs was the ONLY aug carrying a guaranteed floor on top of its banded
            // cantrip. All seven aug keys (34 Item, 35 Creature, 36 Life, 37 War, 38 Void,
            // 39 Melee, 40 Missile) now come from the cantrip roll alone, on the same [14-69]
            // T11 band, so the set totals land on the authored 2500-at-T25 ladder instead of
            // overshooting it into the gear_cap_line clamp.
        }

        /// <summary>
        /// Gated on armor_prot_equalize since 2026-08-24 (default ON = the historical behaviour). OFF means
        /// the elements keep whatever they rolled, so Poor and Unparalleled both survive instead of being
        /// averaged toward the middle - that averaging, not any explicit exclusion, is why every T11+ piece
        /// read as "Average" (plan section 1a).
        ///
        /// DROP-stage: this runs while a piece is being created and never afterwards, so flipping the stat
        /// changes NEW DROPS ONLY. Existing gear keeps its stamped protections through every equip, login
        /// and Apply Ladder. (Only armor_base_level is re-read at resolve time.)
        ///
        /// tier / p are OPTIONAL so the presentation-sweep call sites keep compiling unchanged: tier &lt;= 0
        /// falls back to the piece's own stamped ZcTier, which by then is set, so the tier Default is still
        /// honoured. A call with no profile cannot see a ZONE-level override - the tier Default layer is the
        /// authoritative surface for this key.
        /// </summary>
        public static void EqualizeT11ArmorResists(WorldObject wo, int tier = 0,
            ACE.Server.Managers.ZoneScaling.EvaluatedProfile p = null)
        {
            if (wo == null || (wo.ArmorLevel ?? 0) == 0)
                return;

            if (tier <= 0)
                tier = ACE.Server.Managers.ZoneControl.ZoneStatResolver.TierOf(wo);
            if (!ACE.Server.Managers.ZoneControl.ZoneStatResolver.ArmorProtEqualize(tier, p))
                return;

            var present = new List<ACE.Entity.Enum.Properties.PropertyFloat>();
            var sum = 0.0;

            foreach (var prop in armorModVsProps)
            {
                var val = wo.GetProperty(prop);
                if (val.HasValue)
                {
                    present.Add(prop);
                    sum += val.Value;
                }
            }

            if (present.Count == 0)
                return;

            var mean = sum / present.Count;

            foreach (var prop in present)
                wo.SetProperty(prop, mean);
        }

        private enum ZoneSetFamily
        {
            Axe,        // skill: Heavy/Light/Finesse/TwoHanded
            Dagger,     // skill: Heavy/Light/Finesse; MS or non-MS
            Mace,       // skill: Heavy/Light/Finesse/TwoHanded; Mace or MaceJitte on 1H
            Spear,      // skill: Heavy/Light/Finesse/TwoHanded
            Staff,      // skill: Heavy/Light/Finesse
            Sword,      // skill: Heavy/Light/Finesse/TwoHanded; MS or non-MS on 1H
            Unarmed,    // skill: Heavy/Light/Finesse
            Missile,    // bow / crossbow / atlatl
            Caster,     // wand / orb / staff
        }

        private static readonly ZoneSetFamily[] zoneSetFamilies = (ZoneSetFamily[])System.Enum.GetValues(typeof(ZoneSetFamily));

        /// <summary>Index of the two RANGED lanes inside <see cref="zoneSetFamilies"/>. Everything
        /// else in that array is a MELEE family. Derived, not hardcoded, so reordering or extending
        /// the enum cannot silently mis-lane the draw.</summary>
        private static readonly int zoneSetMissileFamily = System.Array.IndexOf(zoneSetFamilies, ZoneSetFamily.Missile);
        private static readonly int zoneSetCasterFamily = System.Array.IndexOf(zoneSetFamilies, ZoneSetFamily.Caster);

        /// <summary>
        /// BUDGET MODE sampler (owner 2026-08-24). Turns a total item budget plus category weights into
        /// an ordinary ZoneLootSetCounts, so every existing creation path downstream is reused unchanged.
        ///
        /// For each of <paramref name="budget"/> items: weighted-pick a CATEGORY (weapon / armor /
        /// jewelry / cloak - shield rides armor), then weighted-pick a SLOT inside it using the
        /// per-slot weights carried on <paramref name="w"/>. Per-item sampling is what makes "on
        /// average 3 armor" true: the split varies kill to kill and converges on the weights.
        ///
        /// 🔴 WEAPONS ARE DRAWN LANE-FIRST (owner 2026-08-31): "1/3 should be melee, 1/3 missile,
        /// 1/3 caster - that's the goal". Every weapon pick chooses a LANE at equal weight, THEN a
        /// family inside it. Within melee the seven families are still drawn WITHOUT REPLACEMENT
        /// (three melee picks yield three different families, never three swords); missile and
        /// caster are single families and DO repeat, which is what makes the thirds hold - see
        /// the block comment at the weapon case. Armor and jewelry allow repeats too - two rings
        /// is a legitimate roll.
        ///
        /// The budget is a CEILING, not a quota: armor coverage credit (one coat covering chest +
        /// abdomen + upper arms) can satisfy several sampled slots with a single item, so the corpse
        /// can land under budget. That is intended - the owner asked for "max drops".
        /// </summary>
        public static ZoneLootSetCounts RollBudgetedCounts(
            ZoneLootSetCounts w, int budget,
            double wWeapon, double wArmor, double wJewelry, double wCloak)
        {
            var result = new ZoneLootSetCounts { WeaponFamilyPicks = new List<int>() };
            if (budget <= 0)
                return result;

            // slots per category, paired with their authored weight (0 = never)
            var armorSlots = new (int Slot, double Weight)[]
            {
                (0, w.Helm), (1, w.Chest), (2, w.Shoulder), (3, w.Bracer), (4, w.Glove),
                (5, w.Girth), (6, w.UpperLeg), (7, w.LowerLeg), (8, w.Boot), (9, w.Shield),
            };
            var jewelSlots = new (int Slot, double Weight)[]
            {
                (0, w.Amulet), (1, w.Ring), (2, w.Bracelet), (3, w.Trinket),
            };

            // melee families only - the two ranged lanes are single families that repeat, so they
            // are never consumed and never need a free-list
            var freeMelee = new List<int>();
            for (var i = 0; i < zoneSetFamilies.Length; i++)
                if (i != zoneSetMissileFamily && i != zoneSetCasterFamily)
                    freeMelee.Add(i);

            static double Sum((int Slot, double Weight)[] a)
            {
                var t = 0.0;
                foreach (var e in a) if (e.Weight > 0) t += e.Weight;
                return t;
            }

            static int PickSlot((int Slot, double Weight)[] a, double total)
            {
                var roll = ACE.Common.ThreadSafeRandom.Next(0.0f, (float)total);
                var run = 0.0;
                foreach (var e in a)
                {
                    if (e.Weight <= 0) continue;
                    run += e.Weight;
                    if (roll < run) return e.Slot;
                }
                for (var i = a.Length - 1; i >= 0; i--) if (a[i].Weight > 0) return a[i].Slot;
                return -1;
            }

            for (var n = 0; n < budget; n++)
            {
                // rebuild category availability each pick: weapons fall out once all nine are drawn,
                // and a category whose every slot is weighted 0 must never be chosen
                var armorTotal = Sum(armorSlots);
                var jewelTotal = Sum(jewelSlots);
                var cats = new List<(int Cat, double Weight)>();
                // w.Weapons gates the category on/off exactly like every other slot ("a slot at 0 is
                // off"). Without this the weapon pool is always non-empty, so a sub-tier-11 profile -
                // where TierDefault zeroes every slot - would drop WEAPONS ONLY instead of nothing.
                // No free-list test any more: missile and caster repeat, so the weapon category can
                // never run dry the way it did when all nine families were consumable.
                if (wWeapon > 0 && w.Weapons > 0) cats.Add((0, wWeapon));
                if (wArmor > 0 && armorTotal > 0) cats.Add((1, wArmor));
                if (wJewelry > 0 && jewelTotal > 0) cats.Add((2, wJewelry));
                if (wCloak > 0 && w.Cloak > 0) cats.Add((3, wCloak));
                if (cats.Count == 0)
                    break;

                var catTotal = 0.0;
                foreach (var c in cats) catTotal += c.Weight;
                var catRoll = ACE.Common.ThreadSafeRandom.Next(0.0f, (float)catTotal);
                var picked = cats[cats.Count - 1].Cat;
                var acc = 0.0;
                foreach (var c in cats)
                {
                    acc += c.Weight;
                    if (catRoll < acc) { picked = c.Cat; break; }
                }

                switch (picked)
                {
                    case 0:   // weapon - LANE first (equal thirds), then the family inside the lane
                        //
                        // 🔴 WHY LANE-FIRST AND NOT A WEIGHT ON THE FAMILY DRAW. The obvious fix for
                        // "melee is 7 of 9 families" is to weight the flat family pick - melee 1 each,
                        // missile and caster 16. Measured, that DOES land 33.5/33.2/33.3 today, and it
                        // is a fitted number that silently rots: because missile and caster were single
                        // families drawn WITHOUT replacement they were hard-capped at one per corpse,
                        // so the split moves with how many weapons a corpse drops. Same weight of 16 at
                        // budget 10-14 gives melee 44.1 pct; at weapon category weight 4 it gives 45.1
                        // pct. The thirds would quietly break every time someone touched an unrelated
                        // Drop Table knob, with nothing in the GUI to say so.
                        // Choosing the LANE first makes the ratio STRUCTURAL: measured 33.3/33.4/33.3
                        // and flat across budget 4-6 / 7-9 / 10-14 and weapon weights 1, 2 and 4.
                        //
                        // 🔴 MISSILE AND CASTER MUST BE ALLOWED TO REPEAT. Capping either at one per
                        // corpse re-introduces the exact bias this replaces (measured: missile capped
                        // = 37.2 melee / 25.5 missile / 37.4 caster). Repeats are harmless - each entry
                        // re-rolls its own sub-type downstream in CreateZoneSetWeapon, so two missile
                        // picks are usually a bow and a crossbow, and CasterWcids is one pool anyway.
                        // About 12.6 pct of corpses now carry two or more of one ranged lane; that is
                        // the intended cost of a true third (owner ruled "caster repeats allowed").
                    {
                        var lanes = new List<int>(3);
                        if (freeMelee.Count > 0) lanes.Add(0);   // melee - only while a family is left
                        lanes.Add(1);                            // missile - repeats
                        lanes.Add(2);                            // caster  - repeats
                        var lane = lanes[ACE.Common.ThreadSafeRandom.Next(0, lanes.Count - 1)];
                        if (lane == 0)
                        {
                            var fi = ACE.Common.ThreadSafeRandom.Next(0, freeMelee.Count - 1);
                            result.WeaponFamilyPicks.Add(freeMelee[fi]);
                            freeMelee.RemoveAt(fi);
                        }
                        else
                            result.WeaponFamilyPicks.Add(lane == 1 ? zoneSetMissileFamily : zoneSetCasterFamily);
                    }
                        break;

                    case 1:   // armor (shield included)
                        switch (PickSlot(armorSlots, armorTotal))
                        {
                            case 0: result.Helm++; break;
                            case 1: result.Chest++; break;
                            case 2: result.Shoulder++; break;
                            case 3: result.Bracer++; break;
                            case 4: result.Glove++; break;
                            case 5: result.Girth++; break;
                            case 6: result.UpperLeg++; break;
                            case 7: result.LowerLeg++; break;
                            case 8: result.Boot++; break;
                            case 9: result.Shield++; break;
                        }
                        break;

                    case 2:   // jewelry
                        switch (PickSlot(jewelSlots, jewelTotal))
                        {
                            case 0: result.Amulet++; break;
                            case 1: result.Ring++; break;
                            case 2: result.Bracelet++; break;
                            case 3: result.Trinket++; break;
                        }
                        break;

                    default:  // cloak
                        result.Cloak++;
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// Generates the full structured loot set for one kill, one category per configured slot,
        /// all mutated against <paramref name="profile"/>.
        /// </summary>
        public static List<WorldObject> CreateZoneLootSet(TreasureDeath profile, ZoneLootSetCounts counts)
        {
            var items = new List<WorldObject>();

            if (counts.WeaponFamilyPicks != null)
            {
                // budget mode: exactly the sampled families, ONE weapon each
                foreach (var famIdx in counts.WeaponFamilyPicks)
                {
                    var family = (ZoneSetFamily)famIdx;
                    var weapon = CreateZoneSetWeapon(profile, family);
                    if (weapon != null)
                        items.Add(weapon);
                    else
                        log.Warn($"[ZONELOOT] CreateZoneLootSet({profile.TreasureType}): failed to create {family} weapon");
                }
            }
            else
            {
                // legacy mode: the count is a MULTIPLIER over every family - 1 yields all nine
                for (var i = 0; i < counts.Weapons; i++)
                {
                    foreach (var family in zoneSetFamilies)
                    {
                        var weapon = CreateZoneSetWeapon(profile, family);
                        if (weapon != null)
                            items.Add(weapon);
                        else
                            log.Warn($"[ZONELOOT] CreateZoneLootSet({profile.TreasureType}): failed to create {family} weapon");
                    }
                }
            }

            AddZoneSetArmorSlots(items, profile, counts);

            for (var i = 0; i < counts.Shield; i++)
                AddZoneSetShield(items, profile);

            for (var i = 0; i < counts.Amulet; i++)
                AddZoneSetJewelryPiece(items, profile, zoneSetNeckWcids, "neck");
            for (var i = 0; i < counts.Bracelet; i++)
                AddZoneSetJewelryPiece(items, profile, zoneSetWristWcids, "wrist");
            for (var i = 0; i < counts.Ring; i++)
                AddZoneSetJewelryPiece(items, profile, zoneSetFingerWcids, "finger");
            for (var i = 0; i < counts.Trinket; i++)
                AddZoneSetJewelryPiece(items, profile, zoneSetTrinketWcids, "trinket");

            for (var i = 0; i < counts.Cloak; i++)
                AddZoneSetGearPiece(items, profile, TreasureItemType_Orig.Cloak);

            return items;
        }

        /// <summary>
        /// Walks the nine armor equip slots top-down, dropping pieces until every slot has met its
        /// configured count. A multi-slot piece (an Amuli coat covers chest + abdomen + upper arms)
        /// CREDITS every slot it covers, so covered slots don't roll again (owner decision
        /// 2026-07-20) -- with all counts at 1 this yields one clean wearable set, 4-9 pieces.
        /// </summary>
        private static void AddZoneSetArmorSlots(List<WorldObject> items, TreasureDeath profile, ZoneLootSetCounts counts)
        {
            // top-down slot order; each entry pairs the CoverageMask bit with its configured count
            var slots = new (ACE.Entity.Enum.CoverageMask mask, int target)[]
            {
                (ACE.Entity.Enum.CoverageMask.Head,               counts.Helm),
                (ACE.Entity.Enum.CoverageMask.OuterwearChest,     counts.Chest),
                (ACE.Entity.Enum.CoverageMask.OuterwearUpperArms, counts.Shoulder),
                (ACE.Entity.Enum.CoverageMask.OuterwearLowerArms, counts.Bracer),
                (ACE.Entity.Enum.CoverageMask.Hands,              counts.Glove),
                (ACE.Entity.Enum.CoverageMask.OuterwearAbdomen,   counts.Girth),
                (ACE.Entity.Enum.CoverageMask.OuterwearUpperLegs, counts.UpperLeg),
                (ACE.Entity.Enum.CoverageMask.OuterwearLowerLegs, counts.LowerLeg),
                (ACE.Entity.Enum.CoverageMask.Feet,               counts.Boot),
            };

            var credit = new int[slots.Length];

            for (var s = 0; s < slots.Length; s++)
            {
                while (credit[s] < slots[s].target)
                {
                    // reject-sample the normal armor roll until it yields a piece covering this
                    // slot. Coverage comes from the cached weenie, so rejects cost no object
                    // creation.
                    var armorType = TreasureArmorType.Undef;
                    var wcid = WeenieClassName.undef;

                    for (var attempt = 0; attempt < 50; attempt++)
                    {
                        // same two-step as the stock armor path (LootGenerationFactory.cs:1130-1131):
                        // roll the armor TYPE for the tier, then a wcid within it
                        var candidateType = ArmorTypeChance.Roll(profile.Tier);
                        var candidate = ArmorWcids.Roll(profile, ref candidateType);

                        if (candidate == WeenieClassName.undef)
                            continue;

                        var candidateCoverage = GetZoneSetCoverage(candidate);

                        if ((candidateCoverage & slots[s].mask) == 0)
                            continue;   // wrong slot (or a shield / non-clothing entry)

                        armorType = candidateType;
                        wcid = candidate;
                        break;
                    }

                    if (wcid == WeenieClassName.undef)
                    {
                        log.Warn($"[ZONELOOT] CreateZoneLootSet({profile.TreasureType}): no armor wcid found covering {slots[s].mask}");
                        break;
                    }

                    var roll = new TreasureRoll(TreasureItemType_Orig.Armor) { ArmorType = armorType, Wcid = wcid };
                    var wo = CreateAndMutateWcid(profile, roll, false);

                    if (wo == null)
                    {
                        log.Warn($"[ZONELOOT] CreateZoneLootSet({profile.TreasureType}): failed to create {slots[s].mask} armor ({wcid})");
                        break;
                    }

                    items.Add(wo);

                    // ONE PICK = ONE PIECE (owner 2026-08-30). This used to credit EVERY slot the
                    // piece covered, so a coat satisfied chest + abdomen + upper arms with a single
                    // item and the corpse landed well under its armor budget - measured against T10
                    // that is the ONLY bucket that shrinks, because weapons / jewelry / cloak have no
                    // coverage overlap. Retail T10 rolls each armor item independently
                    // (ArmorWcids.Roll, no state between items - ten girths is a legal T10 corpse),
                    // and the owner ruled T11 must match: "if t10 rolls independant, we need to do
                    // the same for t11".
                    // This REVERSES the 07-20 spec decision ("a multi-slot piece fills every slot it
                    // covers -> one clean wearable set, no overlap"). Overlap is now expected and
                    // intended: two pieces covering the same slot is a normal corpse, as at T10.
                    // The slot WEIGHTS still steer which slot each pick asks for, so the Drop Table >
                    // Slots tab keeps its meaning - what changed is only that satisfying one slot no
                    // longer silently satisfies its neighbours.
                    credit[s]++;
                }
            }
        }

        /// <summary>
        /// Shields carry no ClothingPriority, so they can never satisfy an armor slot and are
        /// rolled separately here.
        /// </summary>
        private static void AddZoneSetShield(List<WorldObject> items, TreasureDeath profile)
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var armorType = ArmorTypeChance.Roll(profile.Tier);
                var wcid = ArmorWcids.Roll(profile, ref armorType);

                if (wcid == WeenieClassName.undef)
                    continue;

                var weenie = DatabaseManager.World.GetCachedWeenie((uint)wcid);

                if ((weenie?.GetProperty(PropertyInt.CombatUse) ?? 0) != (int)ACE.Entity.Enum.CombatUse.Shield)
                    continue;

                var roll = new TreasureRoll(TreasureItemType_Orig.Armor) { ArmorType = armorType, Wcid = wcid };
                var wo = CreateAndMutateWcid(profile, roll, false);

                if (wo != null)
                {
                    items.Add(wo);
                    return;
                }
            }

            log.Warn($"[ZONELOOT] CreateZoneLootSet({profile.TreasureType}): failed to roll a shield");
        }

        /// <summary>
        /// Renames a tier-11+ drop to "T11 - [base weenie name]" (owner decision 2026-07-20).
        /// MaterialType is cleared because the CLIENT prepends the material adjective to the
        /// displayed name ("Iron T11 - Frost Ken" otherwise) -- the trade-off is these drops
        /// cannot be salvaged for material (acceptable: T11 gear is worn, not salvage fodder;
        /// ItemWorkmanship stays, so tinkering ONTO the item still works).
        /// </summary>
        public static void ApplyT11NamePrefix(WorldObject wo)
        {
            if (wo == null)
                return;

            var weenie = DatabaseManager.World.GetCachedWeenie(wo.WeenieClassId);
            var baseName = weenie?.GetProperty(ACE.Entity.Enum.Properties.PropertyString.Name) ?? wo.Name;

            if (string.IsNullOrWhiteSpace(baseName) || baseName.StartsWith("T11 - "))
                return;

            wo.Name = $"T11 - {baseName}";
            wo.MaterialType = null;
        }

        /// <summary>
        /// Corpse display order for tier-11+ loot (owner decision 2026-07-20): quest/create-list
        /// items are added to the corpse before treasure (see GenerateTreasure), and treasure
        /// itself sorts casters, then missile launchers, then unarmed, then sword, then the other
        /// melee families, then armor, shields, jewelry, cloaks. Lower = earlier. Stable-sorted,
        /// so generation order is preserved within a group.
        /// </summary>
        public static int GetZoneLootDisplayOrder(WorldObject wo)
        {
            if (wo is Caster)
                return 10;

            if (wo is MissileLauncher || wo is Missile)
                return 20;

            if (wo is MeleeWeapon)
            {
                return wo.W_WeaponType switch
                {
                    ACE.Entity.Enum.WeaponType.Unarmed => 30,
                    ACE.Entity.Enum.WeaponType.Sword => 40,
                    ACE.Entity.Enum.WeaponType.Axe => 50,
                    ACE.Entity.Enum.WeaponType.Dagger => 60,
                    ACE.Entity.Enum.WeaponType.Mace => 70,
                    ACE.Entity.Enum.WeaponType.Spear => 80,
                    ACE.Entity.Enum.WeaponType.Staff => 90,
                    _ => 95,
                };
            }

            if (wo.IsShield)
                return 110;

            if ((wo.ArmorLevel ?? 0) > 0)
                return 100;

            if (wo.ItemType == ACE.Entity.Enum.ItemType.Jewelry)
                return 120;

            if (ACE.Server.Entity.Cloak.IsCloak(wo))
                return 130;

            if (wo is Clothing)
                return 105;

            return 200;     // coins, gems, anything uncategorized -> last
        }

        /// <summary>
        /// Tints a tier-11+ weapon's name by its damage element (owner 2026-07-20, trial: "may
        /// revert"). UiEffects drives the client's name-text tint; DamageType bits map 1:1 onto
        /// UiEffects bits (Cold -> Frost, Electric -> Lightning, etc.). Weapons only; a weapon
        /// with no damage type (e.g. a plain caster) keeps whatever it had.
        /// </summary>
        public static void ApplyT11ElementTint(WorldObject wo)
        {
            if (wo == null || !(wo is MeleeWeapon || wo is MissileLauncher || wo is Caster))
                return;

            var dt = wo.W_DamageType;
            var fx = default(ACE.Entity.Enum.UiEffects);

            if (dt.HasFlag(ACE.Entity.Enum.DamageType.Fire))     fx |= ACE.Entity.Enum.UiEffects.Fire;
            if (dt.HasFlag(ACE.Entity.Enum.DamageType.Cold))     fx |= ACE.Entity.Enum.UiEffects.Frost;
            if (dt.HasFlag(ACE.Entity.Enum.DamageType.Acid))     fx |= ACE.Entity.Enum.UiEffects.Acid;
            if (dt.HasFlag(ACE.Entity.Enum.DamageType.Electric)) fx |= ACE.Entity.Enum.UiEffects.Lightning;
            if (dt.HasFlag(ACE.Entity.Enum.DamageType.Nether))   fx |= ACE.Entity.Enum.UiEffects.Nether;
            if (dt.HasFlag(ACE.Entity.Enum.DamageType.Slash))    fx |= ACE.Entity.Enum.UiEffects.Slashing;
            if (dt.HasFlag(ACE.Entity.Enum.DamageType.Pierce))   fx |= ACE.Entity.Enum.UiEffects.Piercing;
            if (dt.HasFlag(ACE.Entity.Enum.DamageType.Bludgeon)) fx |= ACE.Entity.Enum.UiEffects.Bludgeoning;

            if (fx != 0)
                wo.UiEffects = fx;
        }

        /// <summary>
        /// The client's qualitative armor-protection labels, calibrated against observed client
        /// output (0.40 -> Below Average; 0.84..1.18 -> Average; 1.30 -> Above Average).
        /// </summary>
        public static string ProtectionLabel(double mod)
        {
            if (mod < 0.2) return "Poor";
            if (mod < 0.8) return "Below Average";
            if (mod < 1.2) return "Average";
            if (mod < 1.4) return "Above Average";
            if (mod < 1.6) return "Superior";
            if (mod < 1.8) return "Excellent";
            return "Unparalleled";
        }

        /// <summary>
        /// Splits a PascalCase enum name into words ("HeavyWeapons" -> "Heavy Weapons").
        /// </summary>
        private static string SpaceOutEnum(string name)
        {
            var sb = new System.Text.StringBuilder(name.Length + 4);
            foreach (var c in name)
            {
                if (char.IsUpper(c) && sb.Length > 0)
                    sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// CURRENTLY UNCALLED -- the full panel takeover was reverted (owner 2026-07-21): the
        /// client renders its stock examine panel and AppraiseInfo no longer suppresses anything.
        /// Kept as the skeleton for the planned replacement, which APPENDS a small block to the
        /// bottom of the default panel (LongDesc renders last) instead of replacing it -- the
        /// ratings-range, wield-requirement and provenance pieces here are the reuse candidates;
        /// the armor/weapon stat lines duplicate what the stock panel already shows and go away.
        /// ASCII only (old client cannot render anything else).
        /// </summary>
        public static void AppendRatingsAppraisalBlock(WorldObject wo, string droppedBy = null, int? variation = null, string zoneName = null)
        {
            if (wo == null)
                return;

            var sb = new System.Text.StringBuilder();

            // ---- 1. ratings, at the very top (finally possible now that we own the panel)
            var ratings = new List<(string label, int value)>();

            void Collect(string label, int? value)
            {
                if (value.HasValue && value.Value > 0)
                    ratings.Add((label, value.Value));
            }

            Collect("Damage", wo.GearDamage);
            Collect("Damage Resist", wo.GearDamageResist);
            Collect("Crit", wo.GearCrit);
            Collect("Crit Resist", wo.GearCritResist);
            Collect("Crit Damage", wo.GearCritDamage);
            Collect("Crit Damage Resist", wo.GearCritDamageResist);
            Collect("Nether Resist", wo.GearNetherResistRating);
            Collect("Healing Boost", wo.GearHealingBoost);
            Collect("Max Health", wo.GearMaxHealth);

            if (ratings.Count > 0)
            {
                // category determines which tier-11 table produced the rolls (mirrors the
                // category logic in GearRatingChance.RollT11 / TryMutateGearRatingT10)
                var category =
                    wo is MeleeWeapon || wo is MissileLauncher || wo is Caster ? Tables.GearRatingChance.GearRatingCategory.Weapon :
                    (wo.ArmorLevel ?? 0) > 0 || wo.IsShield ? Tables.GearRatingChance.GearRatingCategory.Armor :
                    wo.ItemType == ACE.Entity.Enum.ItemType.Jewelry ? Tables.GearRatingChance.GearRatingCategory.Jewelry :
                    Tables.GearRatingChance.GearRatingCategory.Clothing;

                var (min, max) = Tables.GearRatingChance.GetRangeT11(category);

                sb.Append("Ratings:");

                foreach (var (label, value) in ratings)
                {
                    // shield Max Health is a flat 100-200 roll, not a table roll
                    var (lo, hi) = label == "Max Health" && wo.IsShield ? (100, 200) : (min, max);

                    sb.Append($"\n* {label} {value} [{lo}-{hi}]");
                }

                sb.Append("\n\n");
            }

            // ---- 2. armor stats
            if ((wo.ArmorLevel ?? 0) > 0)
            {
                sb.Append($"Armor Level: {wo.ArmorLevel}\n");

                var mod = wo.GetProperty(ACE.Entity.Enum.Properties.PropertyFloat.ArmorModVsSlash);
                if (mod.HasValue)
                    sb.Append($"Protection: {ProtectionLabel(mod.Value)} ({(int)System.Math.Round(wo.ArmorLevel.Value * mod.Value)})\n");
            }

            // ---- 3. weapon stats (replaces the suppressed WeaponProfile section)
            if (wo is MeleeWeapon || wo is MissileLauncher || wo is Caster)
            {
                if (wo.WeaponSkill != ACE.Entity.Enum.Skill.None)
                    sb.Append($"Skill: {SpaceOutEnum(wo.WeaponSkill.ToString())}\n");

                if ((wo.Damage ?? 0) > 0)
                {
                    var variance = wo.DamageVariance ?? 0.0;
                    var maxDmg = wo.Damage.Value;
                    var minDmg = (int)System.Math.Round(maxDmg * (1.0 - variance));
                    sb.Append($"Damage: {minDmg} - {maxDmg}\n");
                }

                if ((wo.ElementalDamageBonus ?? 0) > 0)
                    sb.Append($"Elemental Damage Bonus: +{wo.ElementalDamageBonus}\n");

                if ((wo.DamageMod ?? 0) > 1.0)
                    sb.Append($"Damage Modifier: x{wo.DamageMod:0.00}\n");

                if ((wo.ElementalDamageMod ?? 0) > 1.0)
                    sb.Append($"Elemental Damage: +{(int)System.Math.Round((wo.ElementalDamageMod.Value - 1.0) * 100)}%\n");

                if ((wo.ManaConversionMod ?? 0) > 0)
                    sb.Append($"Mana Conversion: +{(int)System.Math.Round(wo.ManaConversionMod.Value * 100)}%\n");

                if ((wo.WeaponTime ?? 0) > 0)
                    sb.Append($"Speed: {wo.WeaponTime}\n");

                if ((wo.WeaponOffense ?? 0) > 1.0)
                    sb.Append($"Attack Bonus: +{(int)System.Math.Round((wo.WeaponOffense.Value - 1.0) * 100)}%\n");

                if ((wo.WeaponDefense ?? 0) > 1.0)
                    sb.Append($"Melee Defense Bonus: +{(int)System.Math.Round((wo.WeaponDefense.Value - 1.0) * 100)}%\n");
            }

            // ---- 4. general info
            // Value / Burden / "Covers" are NOT composed here: the client renders those three
            // lines unconditionally (Value/EncumbranceVal ints are kept in the appraisal profile;
            // Covers comes from the object description) -- adding them again would duplicate.
            // Workmanship is intentionally NOT shown on T11 drops (owner 2026-07-20); the
            // ItemWorkmanship property itself stays, so tinkering onto the gear still works.
            if ((wo.ItemMaxLevel ?? 0) > 0)
                sb.Append($"Item Levels: {wo.ItemMaxLevel}\n");

            // the item-aug wield gate (client cannot render Int64Stat requirements)
            if (wo.WieldRequirements == ACE.Entity.Enum.WieldRequirement.Int64Stat &&
                wo.WieldSkillType == (int)PropertyInt64.LumAugItemCount)
                sb.Append(WieldLineFor(wo)).Append('\n');

            // aug-scaling identity (the per-wielder damage term itself shows live in the weapon
            // panel via WeaponProfile; this line records the item's fixed roll)
            if (wo.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.WeaponAugScaleQuality) is int wsq)
                sb.Append($"Aug Scaling Quality: {wsq:N0} / 1,000\n");

            // ---- 5. provenance, at the very bottom: what dropped it, at which variant, and the
            // Zone Control zone when one governed the kill (omitted otherwise)
            if (!string.IsNullOrWhiteSpace(droppedBy))
            {
                sb.Append($"\nDropped by: {droppedBy}\n");
                sb.Append($"Variant: {(variation.HasValue && variation.Value != 0 ? variation.Value.ToString() : "base")}");

                if (!string.IsNullOrWhiteSpace(zoneName))
                    sb.Append($"\nZone: {zoneName}");
            }

            wo.LongDesc = sb.ToString().TrimEnd('\n');
        }

        private static void AddZoneSetJewelryPiece(List<WorldObject> items, TreasureDeath profile, WeenieClassName[] pool, string slotName)
        {
            var wcid = pool[ThreadSafeRandom.Next(0, pool.Length - 1)];

            var roll = new TreasureRoll(TreasureItemType_Orig.Jewelry) { Wcid = wcid };
            var wo = CreateAndMutateWcid(profile, roll, false);

            if (wo != null)
                items.Add(wo);
            else
                log.Warn($"[ZONELOOT] CreateZoneLootSet({profile.TreasureType}): failed to create {slotName} jewelry ({wcid})");
        }

        private static void AddZoneSetGearPiece(List<WorldObject> items, TreasureDeath profile, TreasureItemType_Orig itemType)
        {
            // TreasureItemCategory.Item => isMagical = false throughout the mutation chain (blank gear)
            var wo = CreateRandomLootObjects_New(profile, TreasureItemCategory.Item, itemType);

            if (wo != null)
                items.Add(wo);
            else
                log.Warn($"[ZONELOOT] CreateZoneLootSet({profile.TreasureType}): failed to create {itemType} piece");
        }

        private static WorldObject CreateZoneSetWeapon(TreasureDeath profile, ZoneSetFamily family)
        {
            var treasureRoll = new TreasureRoll(TreasureItemType_Orig.Weapon);

            switch (family)
            {
                case ZoneSetFamily.Missile:
                    switch (ThreadSafeRandom.Next(0, 2))
                    {
                        case 0:
                            treasureRoll.WeaponType = TreasureWeaponType.Bow;
                            switch (ThreadSafeRandom.Next(0, 2))
                            {
                                case 0: treasureRoll.Wcid = BowWcids_Aluvian.Roll(profile.Tier); break;
                                case 1: treasureRoll.Wcid = BowWcids_Gharundim.Roll(profile.Tier); break;
                                default: treasureRoll.Wcid = BowWcids_Sho.Roll(profile.Tier); break;
                            }
                            break;
                        case 1:
                            treasureRoll.WeaponType = TreasureWeaponType.Crossbow;
                            treasureRoll.Wcid = CrossbowWcids.Roll(profile.Tier);
                            break;
                        default:
                            treasureRoll.WeaponType = TreasureWeaponType.Atlatl;
                            treasureRoll.Wcid = AtlatlWcids.Roll(profile.Tier);
                            break;
                    }
                    break;

                case ZoneSetFamily.Caster:
                    treasureRoll.WeaponType = TreasureWeaponType.Caster;
                    treasureRoll.Wcid = CasterWcids.Roll(profile.Tier);
                    break;

                default:
                    RollZoneSetMeleeWeapon(family, treasureRoll);
                    break;
            }

            if (treasureRoll.Wcid == WeenieClassName.undef)
                return null;

            // isMagical = false -> blank weapon (no spell sets); zone mutation layer adds the fun
            return CreateAndMutateWcid(profile, treasureRoll, false);
        }

        private static void RollZoneSetMeleeWeapon(ZoneSetFamily family, TreasureRoll treasureRoll)
        {
            var canTwoHand = family == ZoneSetFamily.Axe || family == ZoneSetFamily.Mace ||
                             family == ZoneSetFamily.Spear || family == ZoneSetFamily.Sword;

            // 1 = Heavy, 2 = Light, 3 = Finesse, 4 = TwoHanded (folded into the family skill roll)
            var skill = ThreadSafeRandom.Next(1, canTwoHand ? 4 : 3);

            if (skill == 4)
            {
                var twoHandType = family switch
                {
                    ZoneSetFamily.Axe => TreasureWeaponType.TwoHandedAxe,
                    ZoneSetFamily.Mace => TreasureWeaponType.TwoHandedMace,
                    ZoneSetFamily.Spear => TreasureWeaponType.TwoHandedSpear,
                    _ => TreasureWeaponType.TwoHandedSword,
                };

                treasureRoll.WeaponType = twoHandType;
                treasureRoll.Wcid = TwoHandedWeaponWcids.RollForWeaponType(twoHandType);
                return;
            }

            // subtype inner rolls: MS-vs-non for sword/dagger, jitte for mace
            var subType = family switch
            {
                ZoneSetFamily.Axe => TreasureWeaponType.Axe,
                ZoneSetFamily.Dagger => ThreadSafeRandom.Next(0, 1) == 0 ? TreasureWeaponType.Dagger : TreasureWeaponType.DaggerMS,
                ZoneSetFamily.Mace => ThreadSafeRandom.Next(0, 1) == 0 ? TreasureWeaponType.Mace : TreasureWeaponType.MaceJitte,
                ZoneSetFamily.Spear => TreasureWeaponType.Spear,
                ZoneSetFamily.Staff => TreasureWeaponType.Staff,
                ZoneSetFamily.Sword => ThreadSafeRandom.Next(0, 1) == 0 ? TreasureWeaponType.Sword : TreasureWeaponType.SwordMS,
                _ => TreasureWeaponType.Unarmed,
            };

            var wcid = RollSkillTableForWeaponType(skill, subType);

            // not every skill table carries every variant (e.g. a skill without jitte/MS tables):
            // fall back to the family's base subtype, then to any skill that has it
            if (wcid == WeenieClassName.undef && (subType == TreasureWeaponType.DaggerMS || subType == TreasureWeaponType.SwordMS || subType == TreasureWeaponType.MaceJitte))
            {
                subType = family switch
                {
                    ZoneSetFamily.Dagger => TreasureWeaponType.Dagger,
                    ZoneSetFamily.Mace => TreasureWeaponType.Mace,
                    _ => TreasureWeaponType.Sword,
                };
                wcid = RollSkillTableForWeaponType(skill, subType);
            }

            if (wcid == WeenieClassName.undef)
            {
                for (var altSkill = 1; altSkill <= 3 && wcid == WeenieClassName.undef; altSkill++)
                {
                    if (altSkill == skill) continue;
                    wcid = RollSkillTableForWeaponType(altSkill, subType);
                }
            }

            treasureRoll.WeaponType = subType;
            treasureRoll.Wcid = wcid;
        }

        private static WeenieClassName RollSkillTableForWeaponType(int skill, TreasureWeaponType weaponType)
        {
            return skill switch
            {
                1 => HeavyWeaponWcids.RollForWeaponType(weaponType),
                2 => LightWeaponWcids.RollForWeaponType(weaponType),
                _ => FinesseWeaponWcids.RollForWeaponType(weaponType),
            };
        }
    }
}
