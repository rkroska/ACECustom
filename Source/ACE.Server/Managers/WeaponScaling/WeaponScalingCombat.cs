using System;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.WeaponScaling
{
    /// <summary>
    /// Swing-time resolution for the weapon aug-scaling system (T11 weapon relevance plan §6).
    ///
    /// A stamped weapon carries only QUALITY (0-1000) + TIER; everything else — k ranges, tier
    /// caps, kc — resolves LIVE from <see cref="WeaponScalingManager.Current"/> (lock-free
    /// snapshot read), and the wielder's aug counts are read fresh on every damage event, never
    /// baked or cached (the Blood-Drinker-bakes-at-cast trap, deliberately avoided).
    ///
    /// Everything is gated on the master Enabled flag: disabled = every method returns 0 after
    /// one volatile read + null check, and combat is byte-identical to pre-system behavior.
    /// </summary>
    public static class WeaponScalingCombat
    {
        /// <summary>The family key a weapon resolves against in the config's Scripts table —
        /// derived from weapon properties at call time (nothing stamped): weight subtypes merge
        /// by design, multi-strike splits via the MultiStrike attack flag, two-handed splits
        /// cleaver/spear via thrust flags. Null = not a weapon we scale.</summary>
        public static string GetFamilyKey(WorldObject weapon)
        {
            if (weapon == null)
                return null;

            if (weapon is Caster)
                return (weapon.ElementalDamageMod ?? 1.0) > 1.0 ? "caster_elemental" : "caster_non_elemental";

            if (weapon is MissileLauncher)
            {
                switch (weapon.AmmoType)
                {
                    case AmmoType.Arrow: return "bow";
                    case AmmoType.Bolt: return "crossbow";
                    case AmmoType.Atlatl: return "atlatl";
                }
                switch (weapon.W_WeaponType)
                {
                    case WeaponType.Bow: return "bow";
                    case WeaponType.Crossbow: return "crossbow";
                    case WeaponType.Thrown: return "atlatl";
                }
                return null;
            }

            if (!(weapon is MeleeWeapon))
                return null;

            var attackType = weapon.W_AttackType;
            var multiStrike = (attackType & AttackType.MultiStrike) != 0;

            switch (weapon.W_WeaponType)
            {
                case WeaponType.Sword: return multiStrike ? "sword_ms" : "sword";
                case WeaponType.Dagger: return multiStrike ? "dagger_ms" : "dagger";
                case WeaponType.Axe: return "axe";
                case WeaponType.Mace: return "mace";       // jitte folds in
                case WeaponType.Spear: return "spear";
                case WeaponType.Staff: return "staff";
                case WeaponType.Unarmed: return "unarmed";
                case WeaponType.TwoHanded:
                    // Both 2H families strike twice via stance; thrust-flagged = the spear line.
                    var thrust = AttackType.Thrust | AttackType.DoubleThrust | AttackType.TripleThrust;
                    return (attackType & thrust) != 0 ? "two_handed_spear" : "cleaver";
            }
            return null;
        }

        /// <summary>Resolve a stamped weapon's k coefficient + tier row under all the system gates
        /// (enabled, stamped, known family, known tier). False = the weapon has no scaling
        /// identity and every term is 0.
        ///
        /// TYPE-NEUTRAL as of the caster wire-in (2026-08-06). It used to reject casters outright,
        /// which was fine while they were inert but would now block the very path they need. Each
        /// TERM enforces its own exclusions instead — the pattern launchers already followed —
        /// so the rule stays readable at the point it matters: flat/floor/variance/crit all
        /// exclude BOTH launchers and casters, because those two lanes scale through a MOD
        /// instead.</summary>
        private static bool TryResolve(WorldObject weapon, out double k, out WeaponScalingTier tierRow)
        {
            k = 0;
            tierRow = null;

            if (weapon == null)
                return false;

            var cfg = WeaponScalingManager.Current;
            if (!cfg.Enabled)
                return false;

            var quality = weapon.GetProperty(PropertyInt.WeaponAugScaleQuality);
            if (quality == null)
                return false;
            var tier = weapon.GetProperty(PropertyInt.WeaponAugScaleTier);
            if (tier == null)
                return false;

            var family = GetFamilyKey(weapon);
            if (family == null)
                return false;

            if (!cfg.Scripts.TryGetValue(family, out var script))
                return false;

            foreach (var t in cfg.Tiers)
                if (t.Tier == tier.Value) { tierRow = t; break; }
            if (tierRow == null)
                return false;

            k = WeaponScalingManager.ResolveScriptK(script, quality.Value);
            return true;
        }

        /// <summary>Scheme C (2026-08-03): the quality-tightened per-hit variance for a stamped
        /// melee weapon. Non-crit hits roll the WHOLE envelope (base + term) down from max by
        /// this fraction: v_eff = family Variance x (1 - TightenStrength x quality/1000).
        /// False = old flat-hit behavior (launchers, casters, unstamped, disabled, or a store
        /// without the Scheme C fields — Variance/TightenStrength default 0).
        ///
        /// SUB-GRADE SNAP REMOVED 2026-08-06 (owner: "those 2 weapons should not be identical
        /// damage"). The 08-03 snap rounded quality to its sub-grade midpoint so every B+ rolled
        /// the same floor as well as the same max. It existed to keep variance in step with k,
        /// which back then resolved flat per sub-grade — now that k interpolates BETWEEN rungs
        /// (see WeaponScalingManager.ResolveScriptK), a snapped variance would be the thing out of
        /// step instead, freezing the min while the max slid. Continuous on every family again.</summary>
        public static bool TryGetEffectiveVariance(WorldObject weapon, out double vEff) => TryGetEffectiveVariance(weapon, out vEff, true);

        /// <summary>As above; <paramref name="honed"/> false gives the variance before any blacksmithing hone, which is
        /// what the EV normalization must use - normalizing on the honed value would hand the gain straight back.</summary>
        private static bool TryGetEffectiveVariance(WorldObject weapon, out double vEff, bool honed)
        {
            vEff = 0;

            if (weapon is MissileLauncher || weapon is Caster || !TryResolve(weapon, out _, out _))
                return false;

            var cfg = WeaponScalingManager.Current;
            var family = GetFamilyKey(weapon);
            if (family == null || !cfg.Scripts.TryGetValue(family, out var script) || script.Variance <= 0)
                return false;

            var quality = weapon.GetProperty(PropertyInt.WeaponAugScaleQuality) ?? 0;

            vEff = WeaponScalingManager.EffectiveVariance(script.Variance, cfg.TightenStrength, quality);
            if (honed)
                vEff *= HoneFactor(weapon, ACE.Server.Entity.ForgeMath.ForgeLine.Variance);
            return vEff > 0;
        }

        /// <summary>
        /// Blacksmithing hones on a quality-scaled weapon (2026-10-04). Combat takes this weapon's damage, variance and
        /// damage modifier from its QUALITY, not from the stored stats a hone writes, so without this a Damage, Variance
        /// or Damage Modifier hone on a tier 11+ weapon would do nothing. Each level therefore scales the quality-derived
        /// value by the same step it applies to a normal weapon's stat: x (1 + step x levels), or for variance
        /// x (1 - step x levels), never below 0. The step is read live from ServerConfig (forge_hone_step_*), so unlike
        /// a normal weapon's stored hone a retune applies to existing tier 11+ hones too. 1.0 for an unhoned weapon.
        /// </summary>
        public static double HoneFactor(WorldObject weapon, ACE.Server.Entity.ForgeMath.ForgeLine line)
        {
            var text = weapon?.GetProperty(PropertyString.ForgeHoneLevels);
            if (string.IsNullOrEmpty(text))
                return 1.0;
            if (!ACE.Server.Entity.ForgeHones.Parse(text).Levels.TryGetValue(line, out var levels) || levels <= 0)
                return 1.0;
            // one setting read: this runs on every hit of a honed weapon, so no config object is built here
            var step = ACE.Server.Entity.ForgeMath.ForgeConfig.ServerStepFor(line) * levels;
            return ACE.Server.Entity.ForgeMath.LowerIsBetter(line) ? Math.Max(0.0, 1.0 - step) : 1.0 + step;
        }

        /// <summary>
        /// True when a hone on <paramref name="line"/> reaches this weapon through <see cref="HoneFactor"/> rather than
        /// through its stored stat: Damage and Variance on a scaled melee weapon, Damage Modifier on a scaled launcher
        /// or caster. The hone stone uses it to allow those lines even when the stored stat is 0.
        /// </summary>
        public static bool HoneScalesQuality(WorldObject weapon, ACE.Server.Entity.ForgeMath.ForgeLine line)
        {
            if (!TryResolve(weapon, out _, out _))
                return false;
            var modLane = weapon is MissileLauncher || weapon is Caster;
            return line switch
            {
                ACE.Server.Entity.ForgeMath.ForgeLine.MaxDamage => !modLane,
                ACE.Server.Entity.ForgeMath.ForgeLine.Variance => !modLane && TryGetEffectiveVariance(weapon, out _, false),
                ACE.Server.Entity.ForgeMath.ForgeLine.DamageMod => modLane,
                _ => false,
            };
        }

        /// <summary>The per-strike flat damage term: k(quality) x min(wielder's item augs, tier cap).
        /// 0 when disabled, unstamped, casters (inert until the caster wire-in), LAUNCHERS (their
        /// quality grades the damage modifier instead — owner 2026-08-01), unknown family, or a
        /// non-player wielder. Added post-roll in DamageEvent alongside the melee/missile aug
        /// flat — NOT via BaseDamageMod.DamageBonus, which launcher DamageMod would multiply
        /// ~3.6-3.9x on atlatls (plan §6.1 trap).</summary>
        public static float GetFlatBonus(WorldObject weapon, Player wielder)
        {
            if (wielder == null || weapon is MissileLauncher || weapon is Caster
                || !TryResolve(weapon, out var k, out var tierRow))
                return 0f;

            var augs = wielder.EffectiveItemAugCount;   // gems + Triune Weave, matching the aug-caps rule everywhere else
            return (float)(k * Math.Min(augs, tierRow.Cap) * HoneFactor(weapon, ACE.Server.Entity.ForgeMath.ForgeLine.MaxDamage)) * EvNormalization(weapon);
        }

        /// <summary>LIVE EV normalization (owner 2026-08-03): editing a family's Variance
        /// auto-rebalances its flat term, so wilder families never fall behind steady ones —
        /// families all author the SAME k and stay equal in total expected damage at the
        /// CB+CS reference build (p = 0.5 crit chance from the 400-skill imbue cap, M = 3.0
        /// = 3.0, historically the player_crit_damage_cap, which was deleted 2026-08-29 - M stays
        /// as this normalization's own constant): m = [(1-p)+pM] / [(1-p)(1-v_eff/2)+pM] = 2/(2-0.25v).
        /// 1.0 for zero-variance weapons (launchers/casters/legacy) = exact old behavior.</summary>
        private static float EvNormalization(WorldObject weapon)
        {
            if (!TryGetEffectiveVariance(weapon, out var vEff, false))
                return 1f;
            return (float)WeaponScalingManager.EvNormalization(vEff);
        }

        /// <summary>Launcher grading (owner 2026-08-01): bows have ALWAYS scaled through their
        /// damage modifier — the multiplier applies to (ammo + Blood Drinker + elemental), and BD
        /// is 0.5 x item augs, so the mod already couples the weapon to the wielder's augs. A flat
        /// term on top double-dips, so launchers get NO flat term; instead the quality roll
        /// RESOLVES the effective damage modifier directly: lerp(kMin, kMax, quality/1000) with
        /// the launcher family's rows REINTERPRETED as the modifier band (T11 seed 3.00-3.40 —
        /// grade F just above the legacy T10 authored 2.92, S ~+10% over it). REPLACE semantics:
        /// the authored DamageMod property is the fallback whenever this returns false (system
        /// disabled, unstamped legacy launcher, unknown family) — the kill switch restores
        /// pre-system behavior exactly.</summary>
        public static bool TryGetLauncherDamageMod(WorldObject weapon, Player holder, out float damageMod)
        {
            damageMod = 0f;

            if (!(weapon is MissileLauncher))
                return false;

            return TryGetScaledMod(weapon, holder, WeaponScalingManager.Current.LauncherTierStep, out damageMod);
        }

        /// <summary>Caster grading (owner 2026-08-06: "Lets keep bow and caster weapons scaling
        /// identical"). The exact launcher mechanism pointed at a different property: the quality
        /// roll RESOLVES the effective ElementalDamageMod, which multiplies the whole spell damage
        /// expression (SpellProjectile life :652 and war/void :767) just as the launcher's
        /// DamageMod multiplies its own. Same REPLACE semantics — the authored ElementalDamageMod
        /// is the fallback whenever this returns false, so the kill switch restores pre-system
        /// behavior exactly.
        ///
        /// TWO THINGS THIS DOES NOT DO, both deliberate:
        /// - It does NOT touch the damage-type match gate in GetCasterElementalDamageModifier
        ///   (WorldObject_Weapon.cs:491). A caster still only boosts spells of its OWN element;
        ///   scaling a mod that the gate zeroes out would change nothing.
        /// - It therefore does NOTHING for caster_non_elemental — a plain Orb/Sceptre/Staff/Wand
        ///   has no element to match, so its modifier is pinned at 1.0 forever. Those items do not
        ///   scale BY CONSTRUCTION; that is a loot question, not a scaling one.</summary>
        public static bool TryGetCasterElementalMod(WorldObject weapon, Player holder, out float elementalMod)
        {
            elementalMod = 0f;

            if (!(weapon is Caster))
                return false;

            return TryGetScaledMod(weapon, holder, WeaponScalingManager.Current.CasterTierStep, out elementalMod);
        }

        /// <summary>Multiplicative caster composition (owner GO 2026-08-06,
        /// CasterDamageShare_Plan): the wand's resolved mod MULTIPLIES the enchantment sum the
        /// way the bow's DamageMod multiplies Blood Drinker, instead of sitting beside it as an
        /// additive peer ~11x its size. modifier = wandMod x (1 + rescale x enchantments).
        ///
        /// The rescale (default 1/kMax = 0.6329) anchors S grade: an S wand's total is identical
        /// to the old additive math at every aug count, and every other grade/tier re-grades
        /// around that anchor — which is what finally makes the wand carry the owner's 25-35 pct
        /// of total damage (it was ~3 pct under additive composition; T11 vs T14 measured ~1 pct
        /// apart in game 2026-08-06).
        ///
        /// Pure math, no world state — the caller (WorldObject_Weapon) resolves the mod, gathers
        /// the enchantment sum, and passes cfg.CasterAuraRescale; this exists as its own method
        /// so the anchor property is unit-testable against plain configs.</summary>
        public static float ComposeCasterModifier(double wandMod, double enchantments, double auraRescale)
        {
            return (float)(wandMod * (1.0 + auraRescale * enchantments));
        }

        /// <summary>The shared mod resolver behind both mod-scaled lanes: lerp(quality) x the tier
        /// term. ONE path so the two lanes cannot drift apart (owner 2026-08-06: "This keeps
        /// weapons simple"); they differ only in which step knob they pass and which property the
        /// caller writes the result to.</summary>
        private static bool TryGetScaledMod(WorldObject weapon, Player holder, double tierStep, out float mod)
        {
            mod = 0f;

            if (!TryResolve(weapon, out var k, out var tierRow))
                return false;

            if (tierStep > 0)
            {
                // Floored at the tier's wield floor for the same reason GetExamineBonus is: the
                // wield gate guarantees no real wielder is below it, so an unwielded examine
                // (holder null, or a viewer short of the requirement) reads the honest minimum for
                // any hands rather than a modifier this weapon can never actually produce. A no-op
                // for a genuine wielder, who by definition already clears the floor.
                //
                // The floor is the WIELD GATE's, which is the weapon's own tier for every weapon but one: an Ascendant
                // rare is a higher-tier weapon wielded with the requirements of the tier it dropped in (WieldFloorAugs).
                // Flooring that at its own tier's minimum would hand a T11 character holding a T14 bow the T14 minimum
                // they never earned - tier steps for free. The steps themselves are still counted against the weapon's
                // own row (its cap), so what a higher tier really gives - a higher ceiling to grow into - is kept.
                var augs = Math.Max(WieldFloorAugs(weapon, tierRow), holder?.EffectiveItemAugCount ?? 0);   // gems + Triune Weave, like every other aug read
                k *= 1.0 + tierStep * LauncherTierSteps(WeaponScalingManager.Current, tierRow, augs);
            }

            mod = (float)(k * HoneFactor(weapon, ACE.Server.Entity.ForgeMath.ForgeLine.DamageMod));
            return true;
        }

        /// <summary>
        /// The item-aug count the WIELD GATE guarantees for this weapon - the floor under the launcher / caster tier steps
        /// and under the melee "natural damage" display.
        ///
        /// Every weapon but an Ascendant rare: its own tier row's MinWieldAugs, exactly as before - one nullable int read
        /// finds no gate tier and the row already in hand is used. An Ascendant rare (T11+ rares, 2026-10-09) carries the
        /// wield requirements of the tier it DROPPED in, so the guarantee is THAT tier's minimum, and that is the floor.
        /// Runs on every shot and cast: no allocation, and the tier table is walked only for a weapon that has a gate tier.
        /// </summary>
        private static long WieldFloorAugs(WorldObject weapon, WeaponScalingTier tierRow)
        {
            var gateTier = weapon.GetProperty(PropertyInt.ZcRareGateTier);
            if (gateTier == null)
                return tierRow.MinWieldAugs;

            // the same rule the gate stamp uses, so the floor and the requirement on the item cannot disagree; a stored
            // tier that rule ignores (not below the weapon's own, or off the ladder) leaves the weapon gated by its own tier
            var gate = ACE.Server.Managers.ZoneControl.ZoneRare.GateTier(gateTier, tierRow.Tier);
            if (gate == tierRow.Tier)
                return tierRow.MinWieldAugs;

            WeaponScalingTier gateRow = null;
            foreach (var t in WeaponScalingManager.Current.Tiers)
                if (t.Tier == gate) { gateRow = t; break; }
            return WieldFloorAugs(tierRow, gateRow, gateRowMissing: gateRow == null);
        }

        /// <summary>
        /// The floor rule itself, on plain rows so it can be tested without a world. <paramref name="gateRow"/> null =
        /// the weapon is gated by its own tier. <paramref name="gateRowMissing"/> = the weapon IS gated by a lower tier but
        /// that tier has no row (deleted from the table): nothing is guaranteed then, so the floor is 0 and the holder's
        /// real count rules - never the weapon's own tier minimum, which is the free bonus this rule exists to remove.
        /// The floor is never above the weapon's own tier minimum.
        /// </summary>
        internal static long WieldFloorAugs(WeaponScalingTier tierRow, WeaponScalingTier gateRow, bool gateRowMissing = false)
        {
            if (gateRowMissing)
                return 0;
            if (gateRow == null)
                return tierRow.MinWieldAugs;
            return Math.Min(gateRow.MinWieldAugs, tierRow.MinWieldAugs);
        }

        /// <summary>The tier steps a launcher / caster of <paramref name="tierRow"/> shows for a holder with this many item
        /// augs, floor applied - the composition TryGetScaledMod uses, on plain values for the unit tests.</summary>
        internal static int ScaledModTierSteps(WeaponScalingConfig cfg, WeaponScalingTier tierRow, WeaponScalingTier gateRow, long holderAugs)
            => LauncherTierSteps(cfg, tierRow, Math.Max(WieldFloorAugs(tierRow, gateRow), holderAugs));

        /// <summary>How many TIER STEPS this launcher's holder has actually unlocked (owner
        /// 2026-08-06: "Bows should track min(augs, cap) like melee does").
        ///
        /// Gated by the WEAPON's own cap, so augs past it belong to a tier this weapon is not —
        /// which reproduces melee's dead zone exactly: at 3,500 augs a T13 bow and a T25 bow
        /// return the same step count, just as a T13 and T25 melee weapon already share the same
        /// min(augs, cap). A higher tier you cannot fill is worth nothing in either lane.
        ///
        /// Counted off the AUTHORED tier rows rather than assuming the code seed's 500 spacing.
        /// Caps are hand-authored and the store is authoritative — the live grade weights already
        /// differ from the seed (A 3 / B 5 / F 50 vs A 5 / B 10 / F 44.9), so nothing here may
        /// assume seed values.
        ///
        /// Takes a raw aug count rather than a Player so the whole rule is unit-testable without a
        /// live world object; callers apply the wield floor.</summary>
        public static int LauncherTierSteps(WeaponScalingConfig cfg, WeaponScalingTier tierRow, long augs)
        {
            var effective = Math.Min(augs, tierRow.Cap);

            var unlocked = 0;
            foreach (var t in cfg.Tiers)
                if (t.Cap <= effective)
                    unlocked++;

            // The lowest tier is the baseline and adds nothing — T11 is 1.0x by construction.
            return Math.Max(0, unlocked - 1);
        }

        /// <summary>The GUARANTEED-at-equip term: k(quality) x the tier's wield floor (capped).
        /// Because the item-aug wield req means no possible wielder has fewer augs than the floor,
        /// this is an honest minimum for ANY hands — shown as the weapon's "natural" damage to
        /// every examiner, so drops read like real weapons without baking anything on the item
        /// (owner 2026-08-01: baking would freeze the config at drop time and break both the
        /// retroactive re-pricing and the kill switch's full revert).</summary>
        public static float GetFloorBonus(WorldObject weapon)
        {
            if (weapon is MissileLauncher || weapon is Caster || !TryResolve(weapon, out var k, out var tierRow))
                return 0f;

            // WieldFloorAugs, not tierRow.MinWieldAugs directly: the same number for every weapon gated by its own tier, and
            // the DROP tier's minimum for an Ascendant rare - so the panel does not promise an under-tier wielder damage
            // only the item tier's minimum would give (GetExamineBonus and the AppraiseInfo floor both come through here)
            return (float)(k * Math.Min(WieldFloorAugs(weapon, tierRow), tierRow.Cap) * HoneFactor(weapon, ACE.Server.Entity.ForgeMath.ForgeLine.MaxDamage)) * EvNormalization(weapon);
        }

        /// <summary>The UNWIELDED examine value (owner 2026-08-03): read the term off the
        /// EXAMINER's own item augs, so a drop sitting in a corpse or pack reads as what it would
        /// do in THAT player's hands rather than a stranger's. Never below
        /// <see cref="GetFloorBonus"/> — the wield gate guarantees no real wielder has fewer augs
        /// than the tier floor, so a sub-floor examiner would otherwise be shown a number this
        /// weapon can never actually produce for anyone. Supersedes the 2026-08-01
        /// tier-floor-for-every-examiner display.</summary>
        public static float GetExamineBonus(WorldObject weapon, Player examiner)
        {
            var floor = GetFloorBonus(weapon);

            if (examiner == null)
                return floor;

            return Math.Max(floor, GetFlatBonus(weapon, examiner));
        }

        /// <summary>The crit-damage term: kc(quality) x melee_missile_aug_crit_modifier x
        /// min(matching combat augs, tier cap) — the aug-pegged crit floor. Composed via
        /// Math.Max against the CriticalMultiplier/Crippling Blow path, so zone crit cards
        /// stay the jackpot above it. 0 under the same gates as the flat term.</summary>
        public static float GetCritDamageBonus(WorldObject weapon, Creature wielder)
        {
            if (!(wielder is Player player) || weapon == null)
                return 0f;
            if (weapon is Caster)
                return 0f;   // a spell takes the floor through GetSpellCritDamageBonus (Zone Control hand casts only)

            var count = weapon.IsMissileWeapon
                ? player.EffectiveMissileAugCount
                : player.EffectiveMeleeAugCount;
            return CritFloor(weapon, count);
        }

        /// <summary>The same aug-pegged crit floor for a Zone Control HAND-CAST war / void / life spell (owner 2026-10-04, "spell crits
        /// same as melee"): kc(wand quality) x melee_missile_aug_crit_modifier x min(the spell school's augs, tier cap). The
        /// caller (SpellProjectile / Player_Magic rings, gated by Creature.ZcSpellCritMirrorsMelee) composes it with Math.Max
        /// against the wand's Crushing exactly as GetWeaponCritDamageMod does for melee. Without it a claw's crit mod read
        /// 3.1 (T11) .. 13.9 (T25) while the wand stayed on Crushing 2.9 .. 5.6, so spell crits landed at 2.8x .. 2.1x.</summary>
        public static float GetSpellCritDamageBonus(WorldObject caster, Creature wielder, long schoolAugs)
        {
            if (!(wielder is Player) || !(caster is Caster))
                return 0f;
            return CritFloor(caster, schoolAugs);
        }

        private static float CritFloor(WorldObject weapon, long count)
        {
            var cfg = WeaponScalingManager.Current;
            if (!cfg.Enabled)
                return 0f;

            var quality = weapon.GetProperty(PropertyInt.WeaponAugScaleQuality);
            if (quality == null)
                return 0f;
            var tier = weapon.GetProperty(PropertyInt.WeaponAugScaleTier);
            if (tier == null)
                return 0f;

            WeaponScalingTier tierRow = null;
            foreach (var t in cfg.Tiers)
                if (t.Tier == tier.Value) { tierRow = t; break; }
            if (tierRow == null)
                return 0f;

            var kc = WeaponScalingManager.ResolveFromQuality(cfg.KcMin, cfg.KcMax, quality.Value);
            // Same per-aug crit modifier the aug crit bonus itself uses, so the peg stays honest
            // if the server ever retunes it.
            var modifier = ServerConfig.melee_missile_aug_crit_modifier.Value;
            return (float)(kc * modifier * Math.Min(count, tierRow.Cap));
        }
    }
}
