using System;
using System.Collections.Generic;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Factories.Tables;
using ACE.Server.Managers.ZoneScaling;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// Zone Control loot: post-roll mutations applied per dropped item AFTER LootGenerationFactory has finished
    /// rolling it. Enhances the monster's own loot, never replaces it — only the per-instance dropped object is
    /// touched (weenies and treasure tables never are) — enhance-don't-replace, per-drop instance only.
    /// </summary>
    public static class ZoneLootMutator
    {
        /// <summary>
        /// Mutations for an item rolled from the death-treasure table: provenance, forced weapon
        /// properties, plus the low-chance special-property rolls.
        /// <paramref name="killed"/> is the dying monster (slayer type source); <paramref name="lootTier"/>
        /// is the effective treasure tier (levels the default proc spell). <paramref name="forceMax"/> = this
        /// piece won the per-kill slot special (Armor v2): every cantrip line rolls at band MAX.
        /// </summary>
        public static void MutateLootItem(WorldObject wo, EvaluatedProfile p, Creature killed = null, int lootTier = 1, bool forceMax = false)
        {
            if (wo == null || p == null)
                return;

            // currency: coin stacks are never mutated (coin_mult removed 2026-08-23)
            if (wo.WeenieType == WeenieType.Coin)
                return;

            // Zone Control origin: record where this item dropped as a readable sentence appended to the
            // item's description (p.ScopeKey = the winning zone's name; the variation the zone matched on =
            // the creature's effective variation; killed = the dropping monster). Every non-coin drop gets it.
            if (!string.IsNullOrEmpty(p.ScopeKey))
            {
                // Two-line provenance (owner 2026-08-01). FinalizeT11LongDesc and the AppraiseInfo
                // projection-insert anchor on the "Dropped by"/"Location:" prefixes - the three
                // move together.
                var variation = killed != null ? ZoneControlManager.GetEffectiveVariation(killed) : 0;
                var origin = killed != null
                    ? $"Dropped by: {killed.Name}\nLocation: {p.ScopeKey} v{variation}"
                    : $"Location: {p.ScopeKey} v{variation}";
                wo.LongDesc = string.IsNullOrEmpty(wo.LongDesc) ? origin : wo.LongDesc + "\n\n" + origin;
            }

            // (weapon_stat_mult, weapon_damage_min/max/roll, weapon_caster_elem_*, weapon_missile_elem_*
            //  removed 2026-08-23 — weapon damage is owned by the weapon aug-scaling system)

            var isWeapon = wo is MeleeWeapon || wo is MissileLauncher || wo is Caster;

            // forced properties on rolled WEAPONS only (never other items)
            if (isWeapon)
            {
                if (p.Get(ZoneStat.WeaponAttuned, 0) != 0)
                    wo.Attuned = AttunedStatus.Attuned;
                if (p.Get(ZoneStat.WeaponBonded, 0) != 0)
                    wo.Bonded = BondedStatus.Bonded;
                if (p.Get(ZoneStat.WeaponUnenchantable, 0) != 0)
                    wo.ResistMagic = 9999;
            }
            else
            {
                // Armor-side twin (2026-08-28): the same three rules for every NON-weapon zone-set
                // drop - armor, clothing, jewelry, cloaks - the population armor_modifier_chance
                // already governs. Authored from Loot > Armor > Rules.
                if (p.Get(ZoneStat.ArmorAttuned, 0) != 0)
                    wo.Attuned = AttunedStatus.Attuned;
                if (p.Get(ZoneStat.ArmorBonded, 0) != 0)
                    wo.Bonded = BondedStatus.Bonded;
                if (p.Get(ZoneStat.ArmorUnenchantable, 0) != 0)
                    wo.ResistMagic = 9999;
            }

            // (weapon_workmanship_min/max and value_mult/min/max removed 2026-08-23)

            TrySpecialRolls(wo, p, killed, lootTier, forceMax);
        }

        // ── special-property rolls ("fun stuff": independent 0..1 chance each; an item can win several) ──

        // ACE.Server-only custom float prop ids (no ACE.Entity enum change — same pattern as the
        // zone-cantrip prop block 50200-50399). Read hooks: WorldObject_Weapon.GetWeaponResistanceModifier
        // (rend power) and DamageEvent.DoCalculateDamage (armor rend amount).
        public const int RendingModOverridePropId = 9056;
        public const int ArmorRendOverridePropId = 9057;
        /// <summary>Cast on Strike damage B, ONE PER SLOT. Read hook: SpellProjectile.CalculateDamage,
        /// where the value for whichever slot's spell fired REPLACES the rolled spell base and the flat
        /// War/Void aug term. Their PRESENCE is also the display gate - stamped by this card and nothing
        /// else, which is what keeps the ~11,700 live Ring-Glyph items out of the new appraisal line and
        /// the new combat message.</summary>
        public const int ProcArcDamagePropId = 9058;
        public const int ProcRingDamagePropId = 9060;
        /// <summary>Cast on Strike slot 2 (the RING). Slot 1 (the ARC) reuses the engine's own
        /// ProcSpell/ProcSpellRate so every existing call site fires it unchanged; only the second slot
        /// needs new storage. PropertyDataId - 9041 is the first free id after VisualOverrideCombatTable.</summary>
        public const int ProcSpell2PropId = 9041;
        /// <summary>Slot 2's own per-hit rate. TWO INDEPENDENT RATES, owner 2026-08-27: the arc and the
        /// ring are separate entities and roll separately, so a weapon carrying both procs more often
        /// than one carrying either.</summary>
        public const int ProcRate2PropId = 9059;
        /// <summary>Ceiling on the FLAT Melee+Missile+War+Void aug sum added to B. Stamped on the
        /// weapon so the damage path can read it without a zone lookup. 0/absent = uncapped.</summary>
        public const int ProcAugCapPropId = 9061;
        /// <summary>Per-hit spread, per slot. Applied to the WHOLE base (B + augs) at hit time.</summary>
        public const int ProcArcVariancePropId = 9062;
        public const int ProcRingVariancePropId = 9063;
        /// <summary>The RING's own aug cap (owner 2026-08-29: the shared stamp is retired - each proc
        /// is its own thing). Prop 9061 is now the ARC's cap alone; the ring damage path reads this
        /// first and falls back to 9061 so the pre-split test daggers keep their cap.</summary>
        public const int ProcRingAugCapPropId = 9064;

        /// <summary>
        /// Card amount pairs: min only / max only = exact value, both = uniform roll in the range each
        /// drop, reversed bounds auto-swap, everything clamped to [lo, hi]. Neither set = def.
        /// </summary>
        private static double RollRange(EvaluatedProfile p, string minStat, string maxStat, double def, double lo, double hi, int tier)
            => RollRangeBand(p, minStat, maxStat, def, def, lo, hi, tier);

        /// <summary>The Cast on Strike aug-cap fallback (owner 2026-08-30: "cap the cast on
        /// strike... the same caps as the tier", then "we can have the floor at five hundred"):
        /// each proc's aug cap ROLLS uniform between 500 and the drop tier's LIVE weapon-scaling
        /// aug cap (2,500 at T11, +500/tier by default; read from the tunable config so a
        /// tier-cap retune carries to new procs automatically). With base B a token 1-2, this
        /// roll IS the drop's damage quality. forceMax (/wsforge) takes the ceiling. An authored
        /// weapon_proc_*_aug_cap stat overrides with its exact value. A proc can no longer stamp
        /// UNCAPPED by accident.</summary>
        private static double ProcAugCapRoll(int tier, bool forceMax)
        {
            var t = WeaponScaling.WeaponScalingManager.GetTier(tier);
            var cap = t != null && t.Cap > 0 ? t.Cap : 2500 + 500 * (Math.Clamp(tier, 11, 25) - 11);
            if (cap <= 500)
                return cap;
            return forceMax ? cap : ThreadSafeRandom.Next(500f, (float)cap);
        }

        /// <summary>The Cleave / Split Arrows fallback band (owner 2026-08-29: "T11 floor +1 -
        /// T25 max +10"): (1-2) at T11 lerping to (8-10) at T25.</summary>
        private static (double Lo, double Hi) CleaveSplitBandAt(int tier)
        {
            var t = Math.Clamp((tier - 11) / 14.0, 0.0, 1.0);
            return (1 + 7 * t, 2 + 8 * t);
        }

        private static double RollRangeBand(EvaluatedProfile p, string minStat, string maxStat, double defLo, double defHi, double lo, double hi, int tier)
        {
            // ANCHORED (2026-08-29): each end reads through GetT, so an authored _t25 twin walks the
            // pair up the tier line. Historical one-box rule preserved: ONE authored box = that
            // exact value; NEITHER authored = the (defLo, defHi) fallback band rolls.
            var a = p.Has(minStat) ? p.GetT(minStat, defLo, tier) : (p.Has(maxStat) ? p.GetT(maxStat, defLo, tier) : defLo);
            var b = p.Has(maxStat) ? p.GetT(maxStat, a, tier)
                  : p.Has(minStat) ? a : defHi;
            if (b < a)
                (a, b) = (b, a);
            a = Math.Clamp(a, lo, hi);
            b = Math.Clamp(b, lo, hi);
            return a >= b ? a : ThreadSafeRandom.Next((float)a, (float)b);
        }

        /// <summary>
        /// 🔴 THE WEAPON CARD WRITE SITE - all six of them, one method (2026-08-25, weapon/armour parity).
        ///
        /// WHAT CHANGED AND WHY. Until this method existed each card computed its FINAL number inline
        /// and wrote it straight onto the item. That number was then the only record of the roll: the
        /// weapon carried no grade, so ZoneStatResolver.ApplyIfStale returned at its HasRecord guard and
        /// a band retune could never reach a weapon already in someone's pack. Armour has not worked
        /// that way since 2026-08-22 - an armour line records a GRADE 0-1000 and the property is a cache
        /// resolved from that grade against the LIVE ladder. The owner's instruction was to make the two
        /// identical ("full parity with weapons and armor"), so this does for a weapon card exactly what
        /// ZoneModifiers.StampGraded does for an armour line:
        ///     roll a grade -> resolve it inside the effective band -> write the property -> RECORD THE GRADE.
        /// The grade is the truth; the property is its projection. RollBanded, which used to be the
        /// number producer, is now only consulted through ZoneStatResolver.WeaponDropBand.
        ///
        /// PRECEDENCE at drop is unchanged and lives in ZoneStatResolver.WeaponDropBand: an authored
        /// weapon_&lt;card&gt;_min/_max on the zone (which already has the tier Default merged into it)
        /// wins outright, including the "one box = EXACT value, not a range" rule; otherwise the card's
        /// T11-&gt;T25 ladder rung. The RESOLVE side reads the tier Default only - see WeaponResolveBand.
        ///
        /// TWO DELIBERATE BEHAVIOUR CHANGES, both of them the parity the owner asked for:
        ///   - the roll inside an AUTHORED band is now the tier-weighted grade (T11 uniform, climbing to
        ///     10/30/60 at T25) instead of RollRange's flat uniform draw. Armour lines have rolled that
        ///     way since 2026-08-22; "push a tier and your gear also ROLLS better" now holds for a
        ///     hand-tuned zone too, not only for a zone on the ladder fallback.
        ///   - forceMax (this drop won the per-kill slot special) now reaches an authored band as well.
        ///     It only ever reached the ladder fallback before, because RollRange had no notion of it.
        ///
        /// This does NOT change which cards roll. Every caller is behind its own Won(...) gate - all six
        /// of them since 2026-08-25, when Rend Power gained weapon_rend_power_chance and stopped gating
        /// on the presence of its own min/max pair - and Won() returns FALSE for an UNDEFINED stat:
        /// unset means NEVER, not "0 pct". A zone that authors nothing rolls nothing, as before.
        /// </summary>
        /// <summary>Stamps the elemental icon underlay that goes with a rend, so a rended drop LOOKS
        /// rended (owner 2026-08-27: a live-server rend shows a coloured background, our minted drops
        /// showed none). The real imbue recipes set this via RecipeManager.IconUnderlay; stamping the
        /// imbue bit without it produced a weapon that read as rended on appraisal but not in the pack.
        ///
        /// NetherRending is deliberately absent from RecipeManager's table - the fallback id is the one
        /// DeveloperCommands already hardcodes for it in two places, so this stays consistent with the
        /// rest of the codebase rather than inventing a third answer.
        /// </summary>
        public static void ApplyRendUnderlay(WorldObject wo, ImbuedEffectType rend)
        {
            if (ACE.Server.Managers.RecipeManager.IconUnderlay.TryGetValue(rend, out var underlayId))
                wo.IconUnderlayId = underlayId;
            else if (rend == ImbuedEffectType.NetherRending)
                wo.IconUnderlayId = 0x060067A1;
        }

        /// <returns>The DISPLAY value that landed (Crushing Blow: the advertised multiplier, not the
        /// stored one) - for a caller that reports what it stamped. TrySpecialRolls ignores it.</returns>
        private static double StampWeaponCard(WorldObject wo, EvaluatedProfile p,
            ZoneStatResolver.WeaponSpecial ws, int tier, bool forceMax)
        {
            var (lo, hi) = ZoneStatResolver.WeaponDropBand(p, ws, tier);
            var grade = ZoneStatResolver.RollGrade(tier, forceMax);
            var display = Math.Clamp(ZoneStatResolver.ValueForD(lo, hi, grade), ws.Band.Lo, ws.Band.Hi);
            // EngineValue is the ONE display -> engine conversion in the server (Crushing Blow's
            // "- 1.0"). Do not subtract anything here and do not pre-convert before calling: the
            // resolver calls the same method on every equip, and a second subtraction anywhere would
            // walk a 7.5x weapon down to 6.5x, then 5.5x, on every login, silently.
            wo.SetProperty(ws.Prop, ZoneStatResolver.EngineValue(ws, display));
            ZoneStatResolver.AddLine(wo, ws.Key, grade);
            return display;
        }

        /// <summary>
        /// THE FORGE'S DOOR into the one weapon-card write site above (owner 2026-09-01, `/wsforge
        /// cards:premade`). A premade test weapon has to carry exactly what a drop at the tier carries -
        /// same band precedence (WeaponDropBand), same grade roll (RollGrade: forceMax = band top for
        /// bis, the tier-weighted random grade for avg), same EngineValue conversion, same recorded
        /// grade line so a band retune re-resolves it. Routing the forge through this method instead of
        /// letting it compute its own number is what keeps the two paths from drifting; the forge
        /// must never re-implement the arithmetic. <paramref name="p"/> is the tier Default flattened
        /// (see WeaponScalingCommands.TierDefaultProfile); pass an EMPTY profile, not null, when no
        /// Default is authored and the ladder should rule.
        /// </summary>
        public static double StampWeaponCardForForge(WorldObject wo, EvaluatedProfile p,
            ZoneStatResolver.WeaponSpecial ws, int tier, bool forceMax)
            => StampWeaponCard(wo, p, ws, tier, forceMax);

        // ── pre-applied craft deltas that land on a CARD's own property ────────────────────────────
        // These two mirror the live Bandit Hilt recipe (527870063) and are stamped by the hilt block at
        // the bottom of TrySpecialRolls, AFTER the cards, so they ADD on top of Biting Strike and
        // Crushing Blow. They are consts rather than inline literals because ZoneStatResolver.Compute
        // has to add the same amounts back when it re-resolves a hilted weapon - if these two numbers
        // ever disagree, a hilted weapon's crit stats move every time it is equipped. One number, two
        // readers.
        public const double BanditHiltCritFrequencyBonus = 0.25;
        public const double BanditHiltCritMultiplierBonus = 0.175;

        /// <summary>
        /// True when this weapon carries a Bandit Hilt - ours from the drop path, or one a player
        /// applied later with the real recipe (both stamp the same marks, and both need the same
        /// treatment at re-resolve time).
        ///
        /// The test is deliberately FOUR conditions rather than one. ManaStoneDestroyChance alone is a
        /// real retail property and would false-positive on any weenie that happens to carry it, which
        /// would hand that weapon a free +0.25 crit chance on its next equip. All four together -
        /// melee, ivoryable, the hilt's completion marker, and the hilt's Two Handed Combat training
        /// gate on the SECOND wield slot - are what the hilt recipe stamps and effectively nothing else does.
        /// (The recipe REQUIRES ManaStoneDestroyChance &lt; 0.01 to apply, which is why stamping 0.01 is
        /// the marker that blocks a second hilt.)
        /// </summary>
        public static bool HasBanditHilt(WorldObject wo)
        {
            if (wo == null || !(wo is MeleeWeapon))
                return false;
            if ((wo.GetProperty(PropertyFloat.ManaStoneDestroyChance) ?? 0.0) < 0.01)
                return false;
            if (wo.GetProperty(PropertyBool.Ivoryable) != true)
                return false;
            return (wo.GetProperty(PropertyInt.WieldSkillType2) ?? 0) == 46
                && (wo.GetProperty(PropertyInt.WieldRequirements2) ?? 0) == 8;
        }

        // RollBanded (the old "authored min/max, else the tier ladder, else clamp" number producer)
        // was DELETED 2026-08-25. Its two jobs were split so that a weapon can carry a GRADE the way an
        // armour piece does: the PRECEDENCE half moved to ZoneStatResolver.WeaponDropBand (and gained a
        // resolve-time twin, WeaponResolveBand), and the ROLL + WRITE half moved to StampWeaponCard
        // above, which also records the grade. Nothing about which cards roll, or about what an
        // authored min/max means, changed in the move - see StampWeaponCard for the two deliberate
        // exceptions (grade-weighted rolls and forceMax now reach an authored band too).

        // Split-arrow props (already in ACE.Entity — the custom bowstring system)
        // public: the weapon forge (/wsforge cards) stamps the same split-arrow properties
        public const int SplitArrowsBoolId = 9030;      // PropertyBool.SplitArrows
        public const int SplitArrowCountIntId = 9031;   // PropertyInt.SplitArrowCount
        public const int SplitArrowRangeFloatId = 9032; // PropertyFloat.SplitArrowRange
        public const int SplitArrowDmgFloatId = 9033;   // PropertyFloat.SplitArrowDamageMultiplier

        // Non-elemental imbues ALL excluded from the Rending card pool (owner 2026-07-13):
        // CripplingBlow/CriticalStrike compete with the Crushing Blow / Biting Strike cards via
        // Math.Max (6.0 crit mult / 50% crit chance at endgame skill), and ArmorRending has its own
        // card — the pool is rends matching the weapon's damage type, nothing else.
        private const ImbuedEffectType AllRends =
            ImbuedEffectType.SlashRending | ImbuedEffectType.PierceRending | ImbuedEffectType.BludgeonRending |
            ImbuedEffectType.AcidRending | ImbuedEffectType.ColdRending | ImbuedEffectType.ElectricRending |
            ImbuedEffectType.FireRending | ImbuedEffectType.NetherRending;

        /// <summary>Rend imbues that MATCH the weapon's own damage type (owner rule: a fire sword can only
        /// get Fire Rend — a rend for an element the weapon can't deal is dead weight). Multi-type weapons
        /// (e.g. slash/pierce) return every matching rend.</summary>
        private static List<ImbuedEffectType> GetMatchingRends(DamageType dt)
        {
            var rends = new List<ImbuedEffectType>();
            if (dt.HasFlag(DamageType.Slash)) rends.Add(ImbuedEffectType.SlashRending);
            if (dt.HasFlag(DamageType.Pierce)) rends.Add(ImbuedEffectType.PierceRending);
            if (dt.HasFlag(DamageType.Bludgeon)) rends.Add(ImbuedEffectType.BludgeonRending);
            if (dt.HasFlag(DamageType.Acid)) rends.Add(ImbuedEffectType.AcidRending);
            if (dt.HasFlag(DamageType.Cold)) rends.Add(ImbuedEffectType.ColdRending);
            if (dt.HasFlag(DamageType.Electric)) rends.Add(ImbuedEffectType.ElectricRending);
            if (dt.HasFlag(DamageType.Fire)) rends.Add(ImbuedEffectType.FireRending);
            if (dt.HasFlag(DamageType.Nether)) rends.Add(ImbuedEffectType.NetherRending);
            return rends;
        }

        // ── Cast on Strike spell tables (REPLACED ProcSpellPool, 2026-08-27) ──────────────────────
        //
        // The old pool was an 8x8 table indexed `Math.Clamp(lootTier, 1, list.Count) - 1` with every
        // inner list Count == 8 and lootTier always >= 11 on this shard, so the index was ALWAYS 7 and
        // 56 of 64 entries were unreachable. It also picked uniformly at random with no reference to
        // the weapon, so a Fire sword could roll Frost Bolt - and then its Fire Rend would not apply,
        // because the rend resolves off the SPELL's damage type. Both problems die with the table:
        // the spell is now picked from the weapon's own W_DamageType, and the tier lives in the DAMAGE
        // band, which is where every other card puts it and where a retune reaches weapons already in
        // the world.
        //
        // IDS VERIFIED against ace_world.spell and confirmed by the owner 2026-08-27. Arcs are level 1
        // (non_Tracking = 1, single projectile, no spread); rings are level 6 (spread_Angle = 360, 9
        // projectiles). The LEVEL IS NOT A DAMAGE LEVER - B replaces the rolled base, so a level 1 arc
        // and a level 7 arc hit identically. Level 1 was chosen so the displayed name stays clean; the
        // numeral is stripped by ProcDisplayName below rather than by picking a higher spell, because
        // "Incantation of" reads as level 8 just as loudly as "I" reads as level 1 (owner).
        private static readonly (DamageType Dt, uint Arc, uint Ring)[] ProcSpellsByElement =
        {
            (DamageType.Slash,    2753, 1784),  // Blade Arc I        / Horizon's Blades
            (DamageType.Pierce,   2718, 1786),  // Force Arc I        / Nuhmudira's Spines
            (DamageType.Bludgeon, 2746, 1789),  // Shock Arc I        / Tectonic Rifts
            (DamageType.Acid,     2711, 1783),  // Acid Arc I         / Searing Disc
            (DamageType.Cold,     2725, 1787),  // Frost Arc I        / Halo of Frost
            (DamageType.Electric, 2732, 1788),  // Lightning Arc I    / Eye of the Storm
            (DamageType.Fire,     2739, 1785),  // Flame Arc I        / Cassius' Ring of Fire
            (DamageType.Nether,   5369, 5361),  // Nether Arc I       / Clouded Soul (the VOID one)
        };

        /// <summary>The arc/ring pair matching the weapon's own damage type, in the same fixed order
        /// GetMatchingRends uses so a multi-type weapon picks the same element for both cards. Returns
        /// false for a weapon with no resolvable element (a plain bow takes its element from the ammo,
        /// a generic caster has none) - those roll no proc, exactly as they roll no rend.</summary>
        public static bool TryGetProcSpells(DamageType dt, out uint arc, out uint ring)
        {
            foreach (var row in ProcSpellsByElement)
            {
                if (dt.HasFlag(row.Dt))
                {
                    arc = row.Arc; ring = row.Ring;
                    return true;
                }
            }
            arc = 0; ring = 0;
            return false;
        }

        /// <summary>Display name for a Cast on Strike spell, with the level marker stripped (owner
        /// 2026-08-27: "level 1 reads as a weak spell. That isn't the case since we are adding augs to
        /// the damage equation"). The client renders its OWN name from the DAT for anything it is handed
        /// a spell id for, which is why AppraiseInfo also has to withhold the id - see BuildSpells.
        /// Anything not on this table is not ours; callers fall back to the engine's Spell.Name.</summary>
        public static bool TryGetProcDisplayName(uint spellId, out string name)
        {
            switch (spellId)
            {
                case 2753: name = "Blade Arc"; return true;
                case 2718: name = "Force Arc"; return true;
                case 2746: name = "Shock Arc"; return true;
                case 2711: name = "Acid Arc"; return true;
                case 2725: name = "Frost Arc"; return true;
                case 2732: name = "Lightning Arc"; return true;
                case 2739: name = "Flame Arc"; return true;
                case 5369: name = "Nether Arc"; return true;
                // The level 6 rings already carry no numeral, but they still belong on the table: it is
                // what the combat-message and appraisal paths test to decide the line is OURS.
                case 1784: name = "Horizon's Blades"; return true;
                case 1786: name = "Nuhmudira's Spines"; return true;
                case 1789: name = "Tectonic Rifts"; return true;
                case 1783: name = "Searing Disc"; return true;
                case 1787: name = "Halo of Frost"; return true;
                case 1788: name = "Eye of the Storm"; return true;
                case 1785: name = "Cassius' Ring of Fire"; return true;
                case 5361: name = "Clouded Soul"; return true;
                default: name = null; return false;
            }
        }

        private static void TrySpecialRolls(WorldObject wo, EvaluatedProfile p, Creature killed, int lootTier, bool forceMax)
        {
            var isMelee = wo is MeleeWeapon;
            var isMissile = wo is MissileLauncher;
            var isWeapon = isMelee || isMissile || wo is Caster;

            // ── PHASE 1 - ELIGIBILITY, then CHANCE ROLLS, all decided up front (2026-08-30, the
            // modifier cap; eligibility split onto its own flags later that day for the FLOOR).
            // Stamping used to happen inline with each roll; the cap needs the full winner list
            // before anything writes, and the floor additionally needs to know which cards were
            // ELIGIBLE BUT LOST their roll (its top-up pool), so eligibility computes separately
            // from the roll. Eligibility rules are UNCHANGED and stay documented on each card's
            // stamp block. A card that wins its roll but fails eligibility is NOT a winner and
            // spends no cap slot (empty rend pool, invalid slayer creature type).
            uint procArcId = 0, procRingId = 0;
            var hasProcSlots = isWeapon && wo.ProcSpell == null && TryGetProcSpells(wo.W_DamageType, out procArcId, out procRingId);

            // The rend pool computes whenever the drop is a weapon (it used to hide behind the
            // chance roll) - the floor's top-up pool has to know rend eligibility even on a drop
            // whose rend roll lost, and the pool is two cheap list ops.
            var rendCandidates = isWeapon ? GetMatchingRends(wo.W_DamageType) : null;
            rendCandidates?.RemoveAll(rend => (wo.GetImbuedEffects() & rend) != 0);
            var eligRend = rendCandidates != null && rendCandidates.Count > 0;
            var eligSlayer = isWeapon && wo.SlayerCreatureType == null && killed?.CreatureType != null &&
                killed.CreatureType != ACE.Entity.Enum.CreatureType.Invalid;

            var gotArc = hasProcSlots && WonT(p, ZoneStat.WeaponProcArcChance, lootTier);
            var gotRing = hasProcSlots && WonT(p, ZoneStat.WeaponProcRingChance, lootTier);
            var gotRend = eligRend && WonT(p, ZoneStat.WeaponImbueChance, lootTier);
            var gotCleave = isMelee && WonT(p, ZoneStat.WeaponCleaveChance, lootTier);
            var gotSplit = isMissile && WonT(p, ZoneStat.WeaponSplitChance, lootTier);
            var gotBite = isWeapon && WonT(p, ZoneStat.WeaponBiteChance, lootTier);
            var gotCrush = isWeapon && WonT(p, ZoneStat.WeaponCrushChance, lootTier);
            var gotArmorRend = (isMelee || isMissile) && WonT(p, ZoneStat.WeaponArmorRendChance, lootTier);
            // Shields block MELEE and MISSILE, never magic - so this gates exactly like armor rend
            // (owner 2026-08-30: "why is shield cleaving on a wand"). It was `isWeapon`, which let
            // casters roll it, and the card is INERT on a caster: the stamped PropertyFloat.IgnoreShield
            // is only ever read by Creature_Combat.GetShieldMod(attacker, damageType, weapon), called
            // from DamageEvent.cs - the physical path. The spell path uses a DIFFERENT overload,
            // SpellProjectile.GetShieldMod(target, shield), which takes no weapon and consults the
            // shield's AbsorbMagicDamage instead. So a caster rolling this got a dead card that still
            // SPENT one of its weapon_modifier_cap slots - a 3-card T11 wand effectively dropped with 2.
            // Eligibility also feeds the Min-floor top-up pool (CouldWinT), so the old gate let the
            // floor FORCE a dead shield cleave onto a short caster drop as filler.
            var gotShieldCleave = (isMelee || isMissile) && WonT(p, ZoneStat.WeaponShieldCleaveChance, lootTier);
            var gotSlayer = eligSlayer && WonT(p, ZoneStat.WeaponSlayerChance, lootTier);

            // ── PHASE 2 - THE BOUNDS (cap: owner 2026-08-30, "combo impossible at T11"; floor:
            // same day, "need a hard min floor and max"). weapon_modifier_cap (anchored, unset =
            // uncapped) is the max cards this drop carries; winners at >= 100 pct effective chance
            // are ALWAYS INCLUDED (they skip the trim but spend their slots first), the rest trim
            // at RANDOM - so junk cards genuinely dilute god-rolls, which is intended.
            // weapon_modifier_min (anchored, unset = no floor) then tops a short drop up at random
            // from the eligible losers - COUNT guaranteed, identity random.
            var won = new[] { gotRend, gotSlayer, gotBite, gotCrush, gotArmorRend, gotShieldCleave, gotCleave, gotSplit, gotArc, gotRing };
            // Index order matches `won` above. Bite and Crush stay isWeapon on purpose - magic crits
            // exist, so those are live on a caster. Shield Cleave is (isMelee || isMissile) to match
            // its roll gate: this array drives the Min-floor TOP-UP, so leaving it as isWeapon here
            // would let the floor force a dead shield cleave onto a short caster drop even though the
            // roll itself can no longer produce one.
            var eligible = new[]
            {
                eligRend, eligSlayer, isWeapon, isWeapon, isMelee || isMissile, isMelee || isMissile,
                isMelee, isMissile, hasProcSlots, hasProcSlots,
            };
            ApplyModifierBounds(p, ZoneStat.WeaponModifierMin, ZoneStat.WeaponModifierCap, lootTier, won, eligible, new[]
            {
                ZoneStat.WeaponImbueChance, ZoneStat.WeaponSlayerChance, ZoneStat.WeaponBiteChance,
                ZoneStat.WeaponCrushChance, ZoneStat.WeaponArmorRendChance, ZoneStat.WeaponShieldCleaveChance,
                ZoneStat.WeaponCleaveChance, ZoneStat.WeaponSplitChance,
                ZoneStat.WeaponProcArcChance, ZoneStat.WeaponProcRingChance,
            });
            gotRend = won[0]; gotSlayer = won[1]; gotBite = won[2]; gotCrush = won[3]; gotArmorRend = won[4];
            gotShieldCleave = won[5]; gotCleave = won[6]; gotSplit = won[7]; gotArc = won[8]; gotRing = won[9];

            // Cast on Strike (melee/missile — procs fire from the swing path; never clobber an existing proc)
            // CASTERS INCLUDED since 2026-08-25 (owner). The old condition was (isMelee || isMissile)
            // and the comment on the card said procs "fire from the swing path, so casters never roll
            // it". That was simply wrong: ProcSpell fires through TryProcEquippedItems, and the PLAYER
            // MAGIC path calls it with the caster passed as the weapon - Player_Magic.cs:1426
            // (TryProcEquippedItems(this, targetCreature, false, caster)) and SpellProjectile.cs:455
            // (procs the ProjectileLauncher on impact). Monsters already did the same via
            // Monster_Magic.cs. We also stamp ProcSpellSelfTargeted = false, which is exactly what
            // those call sites match on - so a wand carrying a proc has always fired it. We just never
            // stamped one. Nothing in the engine needed changing; this is one condition.
            //
            // WORTH MORE ON A WAND than on a sword, and that is a tuning fact, not a bug: a caster's
            // "strike" is a spell cast at range, and casts come faster and safer than melee swings, so
            // the same weapon_proc_rate buys more procs. Watch it before raising the rate.
            // TWO INDEPENDENT SLOTS, 0/1/2 per item (owner 2026-08-27). Both are the weapon's OWN
            // element - a Fire weapon can roll Flame Arc, Cassius' Ring of Fire, both or neither, and
            // can never roll a Cold arc. Each slot has its own chance and its own per-hit rate.
            //
            // THE `wo.ProcSpell == null` GUARD STAYS and now gates BOTH slots: a weapon that already
            // carries a proc is a player's Ring Glyph craft, and this card leaves it alone entirely
            // rather than stacking a second spell onto someone else's work.
            if (gotArc || gotRing)
            {
                // PER-SLOT knobs throughout (owner 2026-08-29: the shared spellcraft / aug cap stamps
                // are retired - "each of those procs is its own thing"). Caps are stamped on the item
                // rather than read from the zone at hit time, like every other card value.
                if (gotArc)
                {
                    wo.ProcSpell = procArcId;
                    wo.ProcSpellRate = Math.Clamp(p.GetT(ZoneStat.WeaponProcArcRate, 0.05, lootTier), 0.0, 1.0);
                    wo.ProcSpellSelfTargeted = false;
                    StampWeaponCard(wo, p, ZoneStatResolver.SpecProcArcDamage, lootTier, forceMax);
                    var arcVar = Math.Clamp(p.GetT(ZoneStat.WeaponProcArcVariance, 0, lootTier), 0.0, 1.0);
                    if (arcVar > 0)
                        wo.SetProperty((PropertyFloat)ProcArcVariancePropId, arcVar);
                    var arcCap = p.Has(ZoneStat.WeaponProcArcAugCap)
                        ? p.GetT(ZoneStat.WeaponProcArcAugCap, 0, lootTier)
                        : ProcAugCapRoll(lootTier, forceMax);
                    if (arcCap > 0)
                        wo.SetProperty((PropertyFloat)ProcAugCapPropId, arcCap);
                }

                if (gotRing)
                {
                    wo.SetProperty((PropertyDataId)ProcSpell2PropId, procRingId);
                    wo.SetProperty((PropertyFloat)ProcRate2PropId,
                        Math.Clamp(p.GetT(ZoneStat.WeaponProcRingRate, 0.05, lootTier), 0.0, 1.0));
                    StampWeaponCard(wo, p, ZoneStatResolver.SpecProcRingDamage, lootTier, forceMax);
                    var ringVar = Math.Clamp(p.GetT(ZoneStat.WeaponProcRingVariance, 0, lootTier), 0.0, 1.0);
                    if (ringVar > 0)
                        wo.SetProperty((PropertyFloat)ProcRingVariancePropId, ringVar);
                    var ringCap = p.Has(ZoneStat.WeaponProcRingAugCap)
                        ? p.GetT(ZoneStat.WeaponProcRingAugCap, 0, lootTier)
                        : ProcAugCapRoll(lootTier, forceMax);
                    if (ringCap > 0)
                        wo.SetProperty((PropertyFloat)ProcRingAugCapPropId, ringCap);
                }

                if (gotArc || gotRing)
                {
                    // WITHOUT THIS THE CARD DOES NOTHING ON A MELEE CHARACTER. TryResistSpell:140-161
                    // uses the weapon's ItemSpellcraft if set, else the WIELDER's skill in the spell's
                    // school - and untrained War Magic (~600) against a T11 mob's authored
                    // magic_defense of 1100 is resisted essentially every time. Authored, not a
                    // literal: nothing on this shard is tuned yet, so it is a knob.
                    // ItemSpellcraft is ONE property, so a weapon that rolled BOTH slots stamps the
                    // HIGHER of the two per-slot spellcrafts; true per-slot resist is adjust-after.
                    var craft = 0.0;
                    if (gotArc)
                        craft = Math.Max(craft, p.GetT(ZoneStat.WeaponProcArcSpellcraft, 9999, lootTier));
                    if (gotRing)
                        craft = Math.Max(craft, p.GetT(ZoneStat.WeaponProcRingSpellcraft, 9999, lootTier));
                    if ((int)Math.Round(craft) > 0)
                        wo.ItemSpellcraft = (int)Math.Round(craft);
                }
            }

            // Rending card: a rend imbue matching the weapon's own damage type (fire sword or fire wand
            // -> Fire Rend). Casters ARE eligible (elemental rends reduce the target's resistance, boosting
            // magic damage). Weapons with no resolvable damage type (e.g. plain bows — element comes from
            // the ammo — or generic casters) roll nothing via the empty-pool guard below.
            //
            // THE GATE (changed 2026-08-26, owner ruling "a Zone Control drop ALWAYS ends up with a rend
            // matching its own damage type"). It used to be `wo.ImbuedEffect == ImbuedEffectType.Undef` -
            // a STRICT equality on imbue slot 1, so ANY imbue at all, rend or not, vetoed the whole card.
            // Two things were wrong with it:
            //   - it was a veto by PRESENCE, not by conflict. A weapon carrying, say, CriticalStrike would
            //     be refused its Fire Rend even though the two coexist happily (they are separate bits, and
            //     the Armor Rend card twenty lines down already ORs its own imbue in alongside whatever
            //     else is there). Nothing about a foreign imbue makes a matching rend wrong.
            //   - it read slot 1 only, while every engine read site (GetImbuedEffects, and the AllRends test
            //     on the Rend Power card below) ORs all five slots - so the gate and the readers disagreed.
            // VERIFIED before removing it (2026-08-26) that the gate was in practice dead anyway: none of
            // the 487 wcids across the ten weapon loot tables carries ImbuedEffect or ImbuedEffect2-5 in
            // ace_world, and no mutation script the loot pipeline runs (Casters./MeleeWeapons./
            // MissileWeapons./ArmorLevel.) writes ImbuedEffect - only the Recipes/ scripts do, and those
            // are RecipeManager, i.e. player crafting long after the drop. So a weapon reaching this line
            // from Creature_Death has always had a clean imbue field.
            //
            // WHAT REPLACED IT. No presence gate at all; instead the CANDIDATE POOL drops any rend the
            // weapon already carries in ANY of the five slots, and the winner is OR'd in rather than
            // assigned. That makes the card idempotent (re-running it can never duplicate or downgrade a
            // rend) and non-destructive (a foreign imbue - CriticalStrike, ArmorRending, a defense imbue -
            // survives untouched, which a plain `=` would have silently erased once the gate was gone).
            // "Matched" means the same thing PlayerFactoryEx.AddRend means by it - a rend for an element
            // the weapon can actually deal - except that GetMatchingRends is flag-based, so it handles
            // multi-type weapons properly and covers Nether, which AddRend has no case for.
            //
            // The Won(...) chance roll STAYS. The owner drives "always" by setting weapon_imbue_chance to
            // 1.0 in the store; that is a tuning value, not a code constant, and Won() still treats an
            // undefined stat as NEVER so a zone that authors nothing keeps rolling nothing.
            // RENDING + REND POWER ARE ONE MODIFIER (owner 2026-08-29: "we ended up with Rending and
            // Rend Power two separate things. It's 1"). One gate - weapon_imbue_chance - and every
            // rend the CARD stamps also rolls its power from the band (grade recorded, so a band
            // retune still reaches it at re-resolve). weapon_rend_power_chance is RETIRED.
            //
            // NATURAL RENDS STAY VANILLA (owner ruling, same day): the old separate Rend Power roll
            // could land on a rend the ordinary loot roll produced; that path is deliberately gone.
            // Only card-stamped rends carry prop 9056, so making big rends rarer is now the Rending
            // chance itself - the original "big rends rare without rends rare" knob folded into one.
            if (gotRend)
            {
                var rend = rendCandidates[ThreadSafeRandom.Next(0, rendCandidates.Count - 1)];
                wo.ImbuedEffect |= rend;
                ApplyRendUnderlay(wo, rend);
                StampWeaponCard(wo, p, ZoneStatResolver.SpecRendPower, lootTier, forceMax);
            }

            // Cleaving (melee): swing hits extra targets in a 180-degree arc.
            // DECIDED 2026-08-29 (owner "T11 floor +1 - T25 max +10"): the unauthored fallback is
            // a real tier band now, (1-2) at T11 -> (8-10) at T25, lerped like every other ladder.
            // Authored stats (incl. _t25 anchors) still override per end via RollRange's GetT reads.
            if (gotCleave)
            {
                var (defLo, defHi) = CleaveSplitBandAt(lootTier);
                var targets = (int)Math.Round(RollRangeBand(p, ZoneStat.WeaponCleaveMin, ZoneStat.WeaponCleaveMax, defLo, defHi, 1, 10, lootTier));
                wo.SetProperty(PropertyInt.Cleaving, targets + 1); // engine: CleaveTargets = Cleaving - 1
            }

            // Split Arrows (bows): shots fork to hit extra targets (the custom bowstring system).
            // Same owner-ruled band as Cleave - the two are the melee/bow twins of one design.
            if (gotSplit)
            {
                var (defLo, defHi) = CleaveSplitBandAt(lootTier);
                var count = (int)Math.Round(RollRangeBand(p, ZoneStat.WeaponSplitMin, ZoneStat.WeaponSplitMax, defLo, defHi, 1, 10, lootTier));
                wo.SetProperty((PropertyBool)SplitArrowsBoolId, true);
                wo.SetProperty((PropertyInt)SplitArrowCountIntId, count);
                wo.SetProperty((PropertyFloat)SplitArrowRangeFloatId,
                    Math.Clamp(p.GetT(ZoneStat.WeaponSplitRange, 8.0, lootTier), 0.0, 50.0));
                wo.SetProperty((PropertyFloat)SplitArrowDmgFloatId,
                    Math.Clamp(p.GetT(ZoneStat.WeaponSplitDmg, 1.0, lootTier), 0.0, 1.0));
            }

            // Biting Strike: crit chance override (base 0.1). Unauthored magnitude follows the
            // T11 -> T25 ladder (0.58-0.65 -> 0.78-0.88); the value is now the projection of a
            // recorded grade, so a band retune reaches weapons already in the world.
            if (gotBite)
                StampWeaponCard(wo, p, ZoneStatResolver.SpecBite, lootTier, forceMax);

            // Crushing Blow: the card value IS the final crit damage multiplier (2 = 2x, the floor),
            // and the engine computes CriticalDamageMod = 1 + CriticalMultiplier, so the STORED number
            // is (multiplier - 1). That subtraction used to sit inline right here.
            // 🔴 IT NO LONGER DOES, AND MUST NOT COME BACK. The number is produced from a recorded
            // grade now, and produced again on every equip re-stamp, so a subtraction at this site as
            // well would apply the "- 1" twice on the very first login and once more on every ladder
            // apply after that - a 7.5x weapon walking down to 6.5x, 5.5x, 4.5x with nothing logged.
            // The one and only conversion lives in ZoneStatResolver.EngineValue, which both this
            // write site (via StampWeaponCard) and the resolver call. The band table stays in display
            // space; do not bake the offset into it either.
            if (gotCrush)
                StampWeaponCard(wo, p, ZoneStatResolver.SpecCrush, lootTier, forceMax);

            // Armor Rend: stamps the REAL ArmorRending imbue (shows with the rend family on the item) plus
            // a tunable amount = fraction of armor ignored; the override prop replaces the skill formula
            // (which caps at 0.6) at hit time. OR'd in so it coexists with an elemental rend from above.
            // MELEE/MISSILE ONLY: armor rending is a physical-armor effect and does nothing for magic, so
            // casters (wands/orbs/staves) never roll it, regardless of the card's chance.
            if (gotArmorRend)
            {
                wo.ImbuedEffect |= ImbuedEffectType.ArmorRending;
                StampWeaponCard(wo, p, ZoneStatResolver.SpecArmorRend, lootTier, forceMax);
            }

            // Shield Cleaving: fraction of shield AL ignored (engine reads the value directly)
            if (gotShieldCleave)
                StampWeaponCard(wo, p, ZoneStatResolver.SpecShieldCleave, lootTier, forceMax);

            // REMOVED 2026-08-25 (owner): the Phantom ("hollow") loot card. It stamped PropertyBool
            // IgnoreMagicArmor + IgnoreMagicResist at drop time, behind weapon_phantom_chance.
            // Only the CARD is gone. Both properties are RETAIL and are untouched, along with every
            // engine read site (DamageEvent, Creature_Combat, Creature_BodyPart, Monster_Melee) and the
            // appraisal line - the ~830 existing hollow weapons still work and still read as hollow.

            // slayer attuned against the killed monster's own kind
            if (gotSlayer)
            {
                wo.SlayerCreatureType = killed.CreatureType;
                // damage multiplier vs that creature type, rolled per drop; floor 1.5x (a normal slayer),
                // cap 10x (=1000%). One box = exact, both = roll in range; neither = the tier ladder
                // (1.80-2.10 at T11 -> 2.40-3.00 at T25) instead of the old flat 1.5.
                StampWeaponCard(wo, p, ZoneStatResolver.SpecSlayer, lootTier, forceMax);
            }

            // REMOVED 2026-08-25 (owner): the Paragon, Bandit Hilt and Oiled Bowstring loot cards.
            // They stamped, at drop time, effects that already exist as player-obtainable content - the
            // Paragon Weapons recipe, hilt recipe 527870063 and bowstring recipe 527870116. Only the
            // drop-time CARDS are gone; the recipes and the Paragon gems are untouched and a player can
            // still apply any of them by hand.
            // NOTE for anyone deleting more: ZoneStatResolver's HasBanditHilt detection STAYS. It is not
            // part of this card - it exists so a hilt a player applied AFTER the drop is re-added when a
            // weapon re-resolves, and removing it would erase that player's bonus on their next equip.

            // the zone-cantrip LINES on top of whatever the roll produced — the zone's pool only
            // (prop-based ZoneModifiers catalog; retail cantrips deliberately excluded)
            TryExtraModifier(wo, p, isWeapon, forceMax, lootTier);

            // WEAPON RESOLVE IDENTITY, last, once the record is final (2026-08-25).
            //
            // Armour gets this from LootGenerationFactory.ApplyT11GearStats -> StampIdentity, but that
            // method returns at its `default:` case for weapons and casters, so nothing ever stamped a
            // weapon's ZcResolvedVersion. An unstamped weapon reads 0, which is a legitimate stamp
            // value (tier ladder v0, Zone Control on), so a weapon that dropped on a v0 tier would look
            // ALREADY RESOLVED for ever and skip the very first re-resolve it was owed.
            //
            // Only the version is stamped, never ZcTier: ZcTier is the armour-shaped tier property, and
            // ZoneStatResolver.TierOf / ZoneCraftGate.TierOf both expect a weapon's row to arrive via
            // WeaponAugScaleTier instead. That property is written a few lines later in the same
            // Creature_Death sweep (ApplyWeaponAugScaleStamp), which is also why lootTier has to be
            // passed in here rather than read off the item - at this instant the weapon has no tier
            // property at all.
            //
            // Guarded on HasRecord, not on isWeapon, so it also covers a weapon carrying only armour-
            // style cantrip lines (weapons have always been able to roll those through
            // weapon_cantrip_chance, and they were never stamped either).
            if (isWeapon && ZoneStatResolver.HasRecord(wo))
                ZoneStatResolver.StampWeaponResolve(wo, lootTier);
        }

        /// <summary>
        /// Armor line roll, one-row-per-Modifier model (owner 2026-08-29, ModifiersBandsMerge_Plan
        /// REV 2 - armor's version of the Rending merge). Every non-special catalog LINE rolls its
        /// OWN anchored chance (modifier_chance_&lt;key&gt; / _t25) independently on every eligible
        /// piece. RETIRED with this: the armor_/weapon_modifier_chance master gates, the line-count
        /// ladder (modifier_lines_*), the class weights (modifier_weight_*), the zone pool as the
        /// membership test, and lines on WEAPONS entirely.
        ///
        /// CONSEQUENCES, accepted (owner: "we will adjust after"): no lines-per-piece guarantee - a
        /// piece can roll zero lines or in principle every line whose chance hits; rarity lives in
        /// each line's own chance now, not in a shared weight draw. Distinct-per-piece still holds
        /// (each line rolls at most once - StampGraded replaces, never duplicates).
        ///
        /// Bands are unchanged: the zone's authored override (CustomModifierBands) wins over the
        /// tier-scaled catalog band, the roll is a recorded GRADE, and forceMax (the piece carries
        /// the per-kill slot special) stamps every line it rolls at band MAX.
        /// </summary>
        private static void TryExtraModifier(WorldObject wo, EvaluatedProfile p, bool isWeapon, bool forceMax, int lootTier = 11)
        {
            if (isWeapon)
                return;   // weapon drops no longer roll armor-style lines (weapon_modifier_chance retired 2026-08-29)

            // PHASE 1 (2026-08-30, the modifier cap): decide every line's chance roll first, so the
            // cap can trim the full winner list before anything stamps - same shape as the weapon
            // cards in TrySpecialRolls. The list holds every SLOT-ELIGIBLE line, winner or not
            // (2026-08-30 late, the modifier floor: the losers are the floor's top-up pool).
            var pieceMask = ZoneModifiers.PieceMask(wo);
            var lineDefs = new List<ZoneModifiers.Def>();
            foreach (var def in ZoneModifiers.AllDefs)
            {
                if (def.SlotSpecial)
                    continue;
                // per-line slot rule (owner 2026-08-22): zone / Default override per key, else the catalog's ArmorOnly / JewelryOnly
                if (!ZoneModifiers.SlotAllowed(ZoneModifiers.EffectiveSlotMask(def, p.ModifierSlots), pieceMask))
                    continue;
                lineDefs.Add(def);
            }

            // PHASE 2: armor_modifier_min / armor_modifier_cap (anchored, unset = no floor /
            // uncapped) - lines at >= 100 pct effective chance are always included and spend their
            // slots first, the rest trim at random; a short piece then tops up at random from the
            // eligible losers. Same rules as the weapon cards.
            var lineWon = new bool[lineDefs.Count];
            var lineElig = new bool[lineDefs.Count];
            var lineChances = new string[lineDefs.Count];
            for (int i = 0; i < lineDefs.Count; i++)
            {
                lineElig[i] = true;                       // slot-filtered above - every entry is eligible
                lineChances[i] = ZoneModifiers.LineChanceStat(lineDefs[i].Key);
                lineWon[i] = WonT(p, lineChances[i], lootTier);
            }
            ApplyModifierBounds(p, ZoneStat.ArmorModifierMin, ZoneStat.ArmorModifierCap, lootTier, lineWon, lineElig, lineChances);

            for (int i = 0; i < lineDefs.Count; i++)
            {
                if (!lineWon[i])
                    continue;
                var def = lineDefs[i];

                var (min, max) = p.ModifierBands.TryGetValue(def.Key, out var band)
                    ? (band.Min, band.Max) : ZoneModifiers.CatalogBandAt(def, lootTier);   // hardcoded fallback is tier-scaled (2026-08-23)

                // insurance against hand-edited store bands - an inverted band must not throw mid-loot
                if (min > max) (min, max) = (max, min);

                // Live stat resolution (owner 2026-08-22): roll a GRADE 0-1000 (tier-weighted thirds,
                // Option A: T11 uniform, climbing to 10/30/60 at T25) and stamp it through the record;
                // the prop value is ValueFor(grade) inside the effective band. Key 49 Reinforced routes
                // to the plain Stamp inside StampGraded (earned + frozen, never in the record).
                var grade = ZoneStatResolver.RollGrade(lootTier, forceMax);
                ZoneModifiers.StampGraded(wo, def, grade, (min, max));
            }
        }

        /// <summary>
        /// True when the card is switched ON at this scope, the profile defines the chance stat, AND the
        /// 0..1 roll comes up a winner.
        ///
        /// 🔴 THE EXPLICIT OFF SWITCH LIVES HERE, NOT AT THE CALL SITES (2026-08-25, weapon/armour parity).
        /// Every chance-gated weapon card - all fourteen, including the seven with no band of their own
        /// (Paragon, Cast on Strike, Cleave, Split Arrows, Bandit Hilt, Bowstring) - already
        /// passes through this one method, so one test covers the lot. Repeating it per call site would
        /// be seventeen chances to miss one, and the one missed would fail silently: the card would keep
        /// rolling while the plugin's checkbox said it was off, which is the exact class of bug this
        /// change exists to kill.
        ///
        /// WHY A SEPARATE FLAG AND NOT "clear the chance stat". Clearing works at the tier Default,
        /// because there is nothing under it. At ZONE scope it does NOT: the zone layer is merged ON TOP
        /// of the Default (ZoneVariantProfile.Merge), so a cleared zone key means INHERIT and the
        /// Default's chance sails straight through. An explicit false beats the inherited chance; it
        /// also leaves the zone's own tuned chance value sitting untouched in the store, so an off/on
        /// cycle is lossless.
        ///
        /// SCOPING - this method is NOT weapon-only. Its non-weapon caller is the extra-cantrip roll,
        /// which passes armor_cantrip_chance for a non-weapon drop. That name can never be a key in the
        /// toggle map (ZoneStat.IsWeaponCardChance rejects it at authoring time, and the map is the only
        /// writer), so WeaponCardEnabled misses the lookup and returns true: armour is untouched.
        /// </summary>
        private static bool WonT(EvaluatedProfile p, string chanceStat, int tier)
        {
            // Off beats everything, including an inherited chance - checked BEFORE Has() so it reads as
            // the master switch it is, and so it costs one dictionary miss on the overwhelmingly common
            // "nothing authored anywhere" path.
            if (!p.WeaponCardEnabled(chanceStat))
                return false;
            if (!p.Has(chanceStat))
                return false;
            // ANCHORED (2026-08-29): the base stat is the T11 anchor and "<stat>_t25" the T25 anchor;
            // tiers between roll the chance on the line. No _t25 = the old flat behaviour exactly.
            var chance = Math.Clamp(p.GetT(chanceStat, 0.0, tier), 0.0, 1.0);
            return chance > 0 && ThreadSafeRandom.Next(0.0f, 1.0f) < chance;
        }

        /// <summary>
        /// The MODIFIER BOUNDS pass - the CAP trim (owner 2026-08-30, "the full combo must be
        /// impossible at T11") followed by the FLOOR top-up (owner 2026-08-30 late, "need a hard
        /// min floor and max"). Callers roll every card/line first (phase 1), hand the winner and
        /// eligibility flags here, and stamp only what survives.
        ///
        /// CAP rules, all owner-ruled 2026-08-30:
        ///   - capStat unset = UNCAPPED, the pre-cap behaviour exactly (Has() test, like Won's).
        ///   - The cap is anchored (base = T11, _t25 twin = T25) and derived tiers ROUND the lerp.
        ///   - PINNED winners - effective chance >= 100 pct at this tier - are "always included":
        ///     they never trim, but they SPEND their cap slots FIRST (the cap is the player-legible
        ///     total on the item, and a guarantee spends budget rather than adding to it). Rend at
        ///     its authored 1.0 is the design case; there is no separate "always" flag on purpose.
        ///   - The rest trim at RANDOM until the remaining slots fit - so junk cards genuinely
        ///     dilute god-rolls (intended: more rarity, more variance).
        ///   - More pinned winners than cap = all of them still stamp (the cap floors at the pinned
        ///     count); only rolled cards are ever trimmed.
        ///
        /// FLOOR rules (why a floor when pins exist: a pin guarantees a NAMED modifier, the floor
        /// guarantees a NUMBER of them with identity left random - "5 to 8 of the 10" is not
        /// expressible with pins):
        ///   - minStat unset = NO FLOOR, the pre-floor behaviour exactly. Anchored + rounded like
        ///     the cap.
        ///   - When fewer winners survive than the floor demands, the drop TOPS UP from the lines
        ///     that were ELIGIBLE but lost their chance roll AND could have won it - enabled,
        ///     chance authored, chance above zero (CouldWinT). A disabled or unauthored line can
        ///     never be forced on, and an OFF toggle keeps meaning off.
        ///   - The top-up pick is WEIGHTED BY EACH LINE'S OWN AUTHORED CHANCE (2026-08-31), not
        ///     uniform. The COUNT is still guaranteed and the identity is still random, but a rare
        ///     line stays rare in the pool. See WeightedPick for why - a uniform draw inflated the
        ///     armour chase lines 2500x on the live shard.
        ///   - The cap stays HARD: a floor above the cap clamps down to it.
        ///   - Pool smaller than the deficit = stamp everything poolable and stop; no error.
        ///   - Trim and top-up are mutually exclusive on one drop (with floor &lt;= cap, too many
        ///     winners can't also be too few), so order is moot - trim runs first regardless.
        /// </summary>
        private static void ApplyModifierBounds(EvaluatedProfile p, string minStat, string capStat, int tier,
            bool[] won, bool[] eligible, string[] chanceStats)
        {
            var cap = int.MaxValue;
            if (p.Has(capStat))
            {
                // AwayFromZero so the half-steps climb early (3->6 anchors: 4 at T14, 5 at T18, 6 at
                // T23) instead of banker's rounding pushing them a tier later.
                cap = Math.Max(0, (int)Math.Round(p.GetT(capStat, 0.0, tier), MidpointRounding.AwayFromZero));
                var rolled = new List<int>();
                var pinned = 0;
                for (int i = 0; i < won.Length; i++)
                {
                    if (!won[i])
                        continue;
                    if (PinnedT(p, chanceStats[i], tier))
                        pinned++;
                    else
                        rolled.Add(i);
                }
                var slots = Math.Max(0, cap - pinned);
                while (rolled.Count > slots)
                {
                    var k = ThreadSafeRandom.Next(0, rolled.Count - 1);
                    won[rolled[k]] = false;
                    rolled.RemoveAt(k);
                }
            }

            if (!p.Has(minStat))
                return;                                                   // unset = no floor
            var min = Math.Max(0, (int)Math.Round(p.GetT(minStat, 0.0, tier), MidpointRounding.AwayFromZero));
            if (min > cap)
                min = cap;                                                // the cap is hard
            var winners = 0;
            for (int i = 0; i < won.Length; i++)
                if (won[i]) winners++;
            if (winners >= min)
                return;
            var pool = new List<int>();
            var poolWeights = new List<double>();
            for (int i = 0; i < won.Length; i++)
                if (eligible[i] && !won[i] && CouldWinT(p, chanceStats[i], tier))
                {
                    pool.Add(i);
                    poolWeights.Add(Math.Clamp(p.GetT(chanceStats[i], 0.0, tier), 0.0, 1.0));
                }
            while (winners < min && pool.Count > 0)
            {
                var k = WeightedPick(poolWeights);
                won[pool[k]] = true;
                pool.RemoveAt(k);
                poolWeights.RemoveAt(k);
                winners++;
            }
        }

        /// <summary>
        /// An index into <paramref name="weights"/>, drawn in proportion to the weights - the
        /// floor's top-up pick (2026-08-31).
        ///
        /// 🔴 WHY THIS IS NOT A UNIFORM DRAW. Do not "simplify" it back to one. The pool holds every
        /// eligible line that lost its roll, and those lines are NOT equally rare: the armour CHASE
        /// lines sit at 0.0000228 while the FILLER lines sit at 0.3 - four orders of magnitude apart.
        /// A uniform pick treats them as equals, so every short piece the floor fills becomes a
        /// roughly 1-in-5 chance to mint a chase line. MEASURED on the live shard 2026-08-31 with
        /// armor_modifier_min 2: chase lines went from their authored 1-in-21,930 to 15.8 pct of
        /// armour pieces and 33.3 pct of jewellery (6/38 and 9/27) - a 2500x / 4000x inflation, in a
        /// single loot run. Weighting by each line's own authored chance keeps the floor's COUNT
        /// guarantee exactly (the line-count distribution is bit-identical) while leaving relative
        /// rarity intact - measured 1.3x / 1.7x, i.e. noise.
        ///
        /// The plugin's Preview mirrors this draw; if you change one, change the other or the GUI
        /// starts lying about what a drop looks like.
        ///
        /// All-zero weights cannot happen today - CouldWinT already requires chance &gt; 0 - but a
        /// caller that ever loosens that gate gets a uniform fallback rather than a divide by zero.
        /// </summary>
        private static int WeightedPick(List<double> weights)
        {
            var total = 0.0;
            for (int i = 0; i < weights.Count; i++)
                total += weights[i];
            if (total <= 0.0)
                return ThreadSafeRandom.Next(0, weights.Count - 1);

            var roll = ThreadSafeRandom.Next(0.0f, 1.0f) * total;
            for (int i = 0; i < weights.Count; i++)
            {
                roll -= weights[i];
                if (roll < 0.0)
                    return i;
            }
            return weights.Count - 1;                                 // floating-point tail guard
        }

        /// <summary>The floor's top-up gate: every WonT condition EXCEPT the random roll - the
        /// card is enabled, its chance is authored, and the effective chance at this tier is above
        /// zero. Forcing on a line that could never have won on its own would turn the floor into
        /// a silent enable switch.</summary>
        private static bool CouldWinT(EvaluatedProfile p, string chanceStat, int tier)
            => p.WeaponCardEnabled(chanceStat) && p.Has(chanceStat)
                && Math.Clamp(p.GetT(chanceStat, 0.0, tier), 0.0, 1.0) > 0;

        /// <summary>True when the stat's effective chance at this tier is >= 100 pct - the "always
        /// included" test. Only ever called on a winner, so the enabled/off checks already passed
        /// in WonT and are not repeated here.</summary>
        private static bool PinnedT(EvaluatedProfile p, string chanceStat, int tier)
            => p.Has(chanceStat) && p.GetT(chanceStat, 0.0, tier) >= 1.0;

        /// <summary>Weighted 1-based index roll over a weight table summing to 100.</summary>
        private static int RollWeighted(int[] weights)
        {
            var roll = ThreadSafeRandom.Next(1, 100);
            var sum = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                sum += weights[i];
                if (roll <= sum)
                    return i + 1;
            }
            return weights.Length;
        }

        // MutateCreateListItem / MutateWeaponStats / ScaleBonusFraction / ScaleStack removed 2026-08-23
        // together with coin_mult and weapon_stat_mult (createlist items are no longer mutated at all).
    }
}
