using System;
using System.Collections.Generic;
using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects.Entity;

namespace ACE.Server.WorldObjects
{
    partial class WorldObject
    {
        public Skill WeaponSkill
        {
            get => (Skill)(GetProperty(PropertyInt.WeaponSkill) ?? 0);
            set { if (value == 0) RemoveProperty(PropertyInt.WeaponSkill); else SetProperty(PropertyInt.WeaponSkill, (int)value); }
        }

        public DamageType W_DamageType
        {
            get => (DamageType)(GetProperty(PropertyInt.DamageType) ?? 0);
            set { if (value == 0) RemoveProperty(PropertyInt.DamageType); else SetProperty(PropertyInt.DamageType, (int)value); }
        }

        public AttackType W_AttackType
        {
            get => (AttackType)(GetProperty(PropertyInt.AttackType) ?? 0);
            set { if (value == 0) RemoveProperty(PropertyInt.AttackType); else SetProperty(PropertyInt.AttackType, (int)value); }
        }

        public WeaponType W_WeaponType
        {
            get => (WeaponType)(GetProperty(PropertyInt.WeaponType) ?? 0);
            set { if (value == 0) RemoveProperty(PropertyInt.WeaponType); else SetProperty(PropertyInt.WeaponType, (int)value); }
        }

        public bool AutoWieldLeft
        {
            get => GetProperty(PropertyBool.AutowieldLeft) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.AutowieldLeft); else SetProperty(PropertyBool.AutowieldLeft, value); }
        }

        /// <summary>
        /// Returns TRUE if this weapon cleaves
        /// </summary>
        public bool IsCleaving { get => GetProperty(PropertyInt.Cleaving) != null;  }

        /// <summary>
        /// Returns the number of cleave targets for this weapon
        /// If cleaving weapon, this is PropertyInt.Cleaving - 1
        /// </summary>
        public int CleaveTargets
        {
            get
            {
                if (!IsCleaving)
                    return 0;

                return GetProperty(PropertyInt.Cleaving).Value - 1;
            }
        }

        /// <summary>
        /// Returns the primary weapon equipped by a creature
        /// (melee, missile, or wand)
        /// </summary>
        private static WorldObject GetWeapon(Creature wielder, bool forceMainHand = false)
        {
            if (wielder == null)
                return null;

            WorldObject weapon = wielder.GetEquippedWeapon(forceMainHand);

            if (weapon == null)
                weapon = wielder.GetEquippedWand();

            return weapon;
        }

        private const float defaultModifier = 1.0f;

        /// <summary>
        /// Like GetWeaponMeleeDefenseModifier but reads equipped weapon stats even in NonCombat (calculator / admin preview).
        /// </summary>
        public static float GetWeaponMeleeDefenseModifierForPreview(Creature wielder)
        {
            if (wielder == null)
                return defaultModifier;

            var mainhand = GetWeapon(wielder, true);
            var offhand = wielder.GetDualWieldWeapon();

            if (offhand == null)
                return GetWeaponMeleeDefenseModifier(wielder, mainhand);

            return Math.Max(
                GetWeaponMeleeDefenseModifier(wielder, mainhand),
                GetWeaponMeleeDefenseModifier(wielder, offhand));
        }

        /// <summary>
        /// Returns the Melee Defense skill modifier for the current weapon
        /// </summary>
        public static float GetWeaponMeleeDefenseModifier(Creature wielder)
        {
            // creatures only receive defense bonus in combat mode
            if (wielder == null || wielder.CombatMode == CombatMode.NonCombat)
                return defaultModifier;

            var mainhand = GetWeapon(wielder, true);
            var offhand = wielder.GetDualWieldWeapon();

            if (offhand == null)
            {
                return GetWeaponMeleeDefenseModifier(wielder, mainhand);
            }
            else
            {
                var mainhand_defenseMod = GetWeaponMeleeDefenseModifier(wielder, mainhand);
                var offhand_defenseMod = GetWeaponMeleeDefenseModifier(wielder, offhand);

                return Math.Max(mainhand_defenseMod, offhand_defenseMod);
            }
        }

        private static float GetWeaponMeleeDefenseModifier(Creature wielder, WorldObject weapon)
        {
            if (weapon == null)
                return defaultModifier;

            //var defenseMod = (float)(weapon.WeaponDefense ?? defaultModifier) + weapon.EnchantmentManager.GetDefenseMod();

            // TODO: Resolve this issue a better way?
            // Because of the way ACE handles default base values in recipe system (or rather the lack thereof)
            // we need to check the following weapon properties to see if they're below expected minimum and adjust accordingly
            // The issue is that the recipe system likely added 0.01 to 0 instead of 1, which is what *should* have happened.
            var baseWepDef = (float)(weapon.WeaponDefense ?? defaultModifier);
            if (weapon.WeaponDefense > 0 && weapon.WeaponDefense < 1 && ((weapon.GetProperty(PropertyInt.ImbueStackingBits) ?? 0) & 4) != 0)
                baseWepDef += 1;

            var defenseMod = baseWepDef + weapon.EnchantmentManager.GetDefenseMod();

            if (weapon.IsEnchantable)
                defenseMod += wielder.EnchantmentManager.GetDefenseMod();

            return defenseMod;
        }

        /// <summary>
        /// Like GetWeaponMissileDefenseModifier but reads equipped weapon stats even in NonCombat (calculator / admin preview).
        /// </summary>
        public static float GetWeaponMissileDefenseModifierForPreview(Creature wielder)
        {
            if (wielder == null)
                return defaultModifier;

            WorldObject weapon = GetWeapon(wielder as Player);
            if (weapon == null)
                return defaultModifier;

            var baseWepDef = (float)(weapon.WeaponMissileDefense ?? 1.0f);
            if (weapon.WeaponMissileDefense > 0 && weapon.WeaponMissileDefense < 1 && ((weapon.GetProperty(PropertyInt.ImbueStackingBits) ?? 0) & 1) == 1)
                baseWepDef += 1;

            return baseWepDef;
        }

        /// <summary>
        /// Returns the Missile Defense skill modifier for the current weapon
        /// </summary>
        public static float GetWeaponMissileDefenseModifier(Creature wielder)
        {
            WorldObject weapon = GetWeapon(wielder as Player);

            if (weapon == null || wielder.CombatMode == CombatMode.NonCombat)
                return defaultModifier;

            //// no enchantments?
            //return (float)(weapon.WeaponMissileDefense ?? 1.0f);

            var baseWepDef = (float)(weapon.WeaponMissileDefense ?? 1.0f);
            // TODO: Resolve this issue a better way?
            // Because of the way ACE handles default base values in recipe system (or rather the lack thereof)
            // we need to check the following weapon properties to see if they're below expected minimum and adjust accordingly
            // The issue is that the recipe system likely added 0.005 to 0 instead of 1, which is what *should* have happened.
            if (weapon.WeaponMissileDefense > 0 && weapon.WeaponMissileDefense < 1 && ((weapon.GetProperty(PropertyInt.ImbueStackingBits) ?? 0) & 1) == 1)
                baseWepDef += 1;

            // no enchantments?
            return baseWepDef;
        }

        /// <summary>
        /// Like GetWeaponMagicDefenseModifier but reads equipped weapon stats even in NonCombat (calculator / admin preview).
        /// </summary>
        public static float GetWeaponMagicDefenseModifierForPreview(Creature wielder)
        {
            if (wielder == null)
                return defaultModifier;

            WorldObject weapon = GetWeapon(wielder as Player);
            if (weapon == null)
                return defaultModifier;

            var baseWepDef = (float)(weapon.WeaponMagicDefense ?? 1.0f);
            if (weapon.WeaponMagicDefense > 0 && weapon.WeaponMagicDefense < 1 && ((weapon.GetProperty(PropertyInt.ImbueStackingBits) ?? 0) & 1) == 1)
                baseWepDef += 1;

            return baseWepDef;
        }

        /// <summary>
        /// Returns the Magic Defense skill modifier for the current weapon
        /// </summary>
        public static float GetWeaponMagicDefenseModifier(Creature wielder)
        {
            WorldObject weapon = GetWeapon(wielder as Player);

            if (weapon == null || wielder.CombatMode == CombatMode.NonCombat)
                return defaultModifier;

            //// no enchantments?
            //return (float)(weapon.WeaponMagicDefense ?? 1.0f);

            var baseWepDef = (float)(weapon.WeaponMagicDefense ?? 1.0f);
            // TODO: Resolve this issue a better way?
            // Because of the way ACE handles default base values in recipe system (or rather the lack thereof)
            // we need to check the following weapon properties to see if they're below expected minimum and adjust accordingly
            // The issue is that the recipe system likely added 0.005 to 0 instead of 1, which is what *should* have happened.
            if (weapon.WeaponMagicDefense > 0 && weapon.WeaponMagicDefense < 1 && ((weapon.GetProperty(PropertyInt.ImbueStackingBits) ?? 0) & 1) == 1)
                baseWepDef += 1;

            // no enchantments?
            return baseWepDef;
        }

        /// <summary>
        /// Returns the attack skill modifier for the current weapon
        /// </summary>
        public static float GetWeaponOffenseModifier(Creature wielder)
        {
            // creatures only receive offense bonus in combat mode
            if (wielder == null || wielder.CombatMode == CombatMode.NonCombat)
                return defaultModifier;

            var mainhand = GetWeapon(wielder, true);
            var offhand = wielder.GetDualWieldWeapon();

            if (offhand == null)
            {
                return GetWeaponOffenseModifier(wielder, mainhand);
            }
            else
            {
                var mainhand_attackMod = GetWeaponOffenseModifier(wielder, mainhand);
                var offhand_attackMod = GetWeaponOffenseModifier(wielder, offhand);

                return Math.Max(mainhand_attackMod, offhand_attackMod);
            }
        }

        private static float GetWeaponOffenseModifier(Creature wielder, WorldObject weapon)
        {
            /* Excerpt from http://acpedia.org/wiki/Announcements_-_2002/07_-_Repercussions#Letter_to_the_Players
             The second issue will, in some ways, be both more troubling and more inconsequential for players. HeartSeeker does not affect missile launchers.
             It never has. Bows, crossbows, and atlatls get no benefit from the HeartSeeker spell or from innate attack bonuses (such as those found on the Singularity Bow).
             The only variables that determine whether a missile character hits their target is their bow/xbow/tw skill, the missile defense of the target, and where they set their accuracy meter while they are attacking.
             However, the Defender spell, as well as innate defensive bonuses, do work on missile launchers.
             The AC Live team has been aware of this for the last several months. Once we knew the situation, the question became what to do about it. Should we “fix” an issue that probably isn't broken?
             Almost no archer/atlatler complains about not being able to hit their target.
             They have a built in “HeartSeeker” all the time.
             If anything, most monsters' missile defense scores have historically been so low that many players regard archery as the fastest way to level a character up through the first 30-40 levels.
             We did not feel that “fixing” such a system would improve the game balance for anyone in Asheron's Call, archer or no.
             Ultimately, we decided to resolve the situation through our changes to the treasure system this month. From now on, missile launchers will have a chance of having an innate defensive bonus, but not an offensive one.
             While many old quest weapons still retain their (useless) attack bonus, we will not be putting any new ones into the system.
             */
            if (weapon == null || weapon.IsRanged /* see note above */)
                return defaultModifier;

            var offenseMod = (float)(weapon.WeaponOffense ?? defaultModifier) + weapon.EnchantmentManager.GetAttackMod();

            if (weapon.IsEnchantable)
                offenseMod += wielder.EnchantmentManager.GetAttackMod();

            return offenseMod;
        }

        /// <summary>
        /// Returns the Mana Conversion skill modifier for the current weapon
        /// </summary>
        public static float GetWeaponManaConversionModifier(Creature wielder)
        {
            WorldObject weapon = GetWeapon(wielder as Player);

            if (weapon == null)
                return defaultModifier;

            if (wielder.CombatMode != CombatMode.NonCombat)
            {
                // hermetic link / void

                // base mod starts at 0
                var baseMod = (float)(weapon.ManaConversionMod ?? 0.0f);

                // enchantments are multiplicative, so they are only effective if there is a base mod
                var manaConvMod = weapon.EnchantmentManager.GetManaConvMod();

                var auraManaConvMod = 1.0f;

                if (weapon.IsEnchantable)
                    auraManaConvMod = wielder?.EnchantmentManager.GetManaConvMod() ?? 1.0f;

                var enchantmentMod = manaConvMod * auraManaConvMod;

                return 1.0f + baseMod * enchantmentMod;
            }

            return defaultModifier;
        }

        private const uint defaultSpeed = 40;   // TODO: find default speed

        /// <summary>
        /// Returns the weapon speed, with enchantments factored in
        /// </summary>
        public static uint GetWeaponSpeed(Creature wielder)
        {
            WorldObject weapon = GetWeapon(wielder as Player);

            var baseSpeed = weapon?.WeaponTime ?? (int)defaultSpeed;

            var speedMod = weapon != null ? weapon.EnchantmentManager.GetWeaponSpeedMod() : 0;
            var auraSpeedMod = wielder != null ? wielder.EnchantmentManager.GetWeaponSpeedMod() : 0;

            return (uint)Math.Max(0, baseSpeed + speedMod + auraSpeedMod);
        }

        /// <summary>
        /// Biting Strike
        /// </summary>
        public double? CriticalFrequency
        {
            get => GetProperty(PropertyFloat.CriticalFrequency);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.CriticalFrequency); else SetProperty(PropertyFloat.CriticalFrequency, value.Value); }
        }

        /// <summary>
        /// OWNER RULING 2026-08-25: Biting Strike and Crushing Blow - the Zone Control weapon cards -
        /// are the ONLY sources of crit chance and crit damage on a PLAYER's weapon. The two vanilla
        /// imbues that do the same job are suppressed for players:
        ///   Critical Strike  - a FLAT 0.50 crit chance at capped skill (MaxCriticalStrikeMod)
        ///   Crippling Blow   - 6.0 stored, which the engine turns into 7.0x in combat
        /// Both meet our card through Math.Max, so an unsuppressed imbue is a free floor the card has
        /// to clear before it means anything. Suppressing them makes the band the whole story.
        ///
        /// PLAYERS ONLY, deliberately (owner's word). A monster's own imbues are part of ITS balance
        /// and keep working exactly as before - suppressing shard-wide would quietly re-tune every mob
        /// carrying one, which nobody asked for.
        ///
        /// WHY A READ-SITE GUARD rather than blocking the crafts. Two reasons the craft gate cannot
        /// cover, both measured 2026-08-25:
        ///   - 125 weenies carry these imbues BAKED IN (55 of them casters). No crafting gate ever
        ///     sees those; they arrive already imbued.
        ///   - The gate's layer-2 rule is "a weaker imbue cannot go on, but NOT-imbued could", so a
        ///     weapon with no card would accept Critical Strike for ever, by design.
        /// Craft-gate denies are still worth having as a second line - they give the player a refusal
        /// reason, which a silent read-site guard cannot - but only this expresses NEVER.
        ///
        /// NOTE this does not touch the OTHER crit contributors: the weapon aug-scaling crit term, the
        /// luminance-aug crit term or gear crit rating. (A historical note here about the
        /// player_crit_damage_cap clamp binding on melee is obsolete - that clamp was deleted
        /// 2026-08-29 with the unified crit model; the Crushing Blow band bounds the ratio now.)
        ///
        /// Flip this const to restore retail behaviour; it is the single switch.
        /// </summary>
        public const bool CritImbuesSuppressedForPlayers = true;

        /// <summary>True when Critical Strike / Crippling Blow must be ignored for this wielder.</summary>
        public static bool CritImbuesSuppressed(Creature wielder)
            => CritImbuesSuppressedForPlayers && wielder is Player;

        /// <summary>The weapon zone lock (owner 2026-08-30): true when this ZC weapon's custom
        /// power is suppressed for this swing - toggle on, player wielder, ZcTier 11+ item,
        /// outside every enabled authored area. One name so the card read sites below stay
        /// one-line gates; the rule itself lives in ZoneControlManager.WeaponPowerSuppressed.</summary>
        public static bool ZcPowerSuppressed(WorldObject weapon, Creature wielder)
            => ACE.Server.Managers.ZoneControl.ZoneControlManager.WeaponPowerSuppressed(weapon, wielder);

        private const float defaultPhysicalCritFrequency = 0.1f;    // 10% base chance

        /// <summary>
        /// Returns the critical chance for the attack weapon
        /// </summary>
        public static float GetWeaponCriticalChance(WorldObject weapon, Creature wielder, CreatureSkill skill, Creature target)
        {
            // zone lock: outside authored areas a ZC weapon's Biting Strike stamp reads as absent
            var critRate = ZcPowerSuppressed(weapon, wielder)
                ? defaultPhysicalCritFrequency
                : (float)(weapon?.CriticalFrequency ?? defaultPhysicalCritFrequency);

            if (weapon != null && weapon.HasImbuedEffect(ImbuedEffectType.CriticalStrike)
                && !CritImbuesSuppressed(wielder))   // owner 2026-08-25: Biting Strike is the only crit-chance source on a player weapon
            {
                var criticalStrikeBonus = GetCriticalStrikeMod(skill);

                critRate = Math.Max(critRate, criticalStrikeBonus);
            }

            if (wielder != null)
                critRate += wielder.GetCritRating() * 0.01f;

            // Zone Control: an authored crit_rating REPLACES the governed monster's crit chance outright
            // (value in percent; the defender's crit-resist below still applies)
            var zoneCrit = GetZoneCritChanceOverride(wielder);
            if (zoneCrit.HasValue)
                critRate = zoneCrit.Value;

            // mitigation
            var critResistRatingMod = Creature.GetNegativeRatingMod(target.GetCritResistRating());
            critRate *= critResistRatingMod;

            return critRate;
        }

        /// <summary>Zone Control: zone-authored crit_rating REPLACES a governed monster's crit chance
        /// (stat value in percent, e.g. 25 = crits 25% of the time). Null for players/ungoverned mobs.</summary>
        private static float? GetZoneCritChanceOverride(Creature wielder)
        {
            if (wielder == null || wielder is Player)
                return null;
            var zp = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveForCreature(wielder);
            if (zp == null || !zp.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.CritRating))
                return null;
            return (float)(zp.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.CritRating) / 100.0);
        }

        /// <summary>Zone Control: zone-authored crit_damage_rating IS a governed monster's final crit
        /// multiplier (e.g. 4 = crits deal 4x). Null for players/ungoverned mobs.</summary>
        private static float? GetZoneCritDamageOverride(Creature wielder)
        {
            if (wielder == null || wielder is Player)
                return null;
            var zp = ACE.Server.Managers.ZoneControl.ZoneControlManager.ResolveForCreature(wielder);
            if (zp == null || !zp.Has(ACE.Server.Managers.ZoneScaling.ZoneStat.CritDamageRating))
                return null;
            return (float)zp.Get(ACE.Server.Managers.ZoneScaling.ZoneStat.CritDamageRating);
        }

        // http://acpedia.org/wiki/Announcements_-_2002/08_-_Atonement#Letter_to_the_Players - 2% originally

        // http://acpedia.org/wiki/Announcements_-_2002/11_-_The_Iron_Coast#Release_Notes
        // The chance for causing a critical hit with magic, both with and without a Critical Strike wand, has been increased.
        // what this was actually increased to for base, was never stated directly in the dev notes
        // speculation is that it was 5%, to align with the minimum that CS magic scales from

        // UNIFIED 2026-08-29 (owner, "all 3 should get the same crit treatment"): magic's base crit
        // chance comes up from retail's speculative 0.05 to match melee/missile's 0.10, so Biting
        // Strike means the same thing on every school. The retail note this shadowed was itself
        // speculation ("what this was actually increased to ... was never stated").
        private const float defaultMagicCritFrequency = 0.10f;

        /// <summary>
        /// Returns the critical chance for the caster weapon
        /// </summary>
        public static float GetWeaponMagicCritFrequency(WorldObject weapon, Creature wielder, CreatureSkill skill, Creature target)
        {
            // TODO : merge with above function

            if (weapon == null)
            {
                // Wandless casters = MONSTERS only (players cannot cast without a wielded caster).
                // FIXED 2026-08-21 (owner ruling): the target's Crit Resist now applies on this
                // branch too - it was silently skipped, making mob-caster crits un-resistable
                // while the same mob's MELEE crits were reduced. Zone-authored crit chance still
                // replaces the default first, same as the with-wand path below.
                var wandlessRate = GetZoneCritChanceOverride(wielder) ?? defaultMagicCritFrequency;
                return wandlessRate * Creature.GetNegativeRatingMod(target.GetCritResistRating());
            }

            // zone lock: outside authored areas a ZC weapon's Biting Strike stamp reads as absent
            var critRate = ZcPowerSuppressed(weapon, wielder)
                ? defaultMagicCritFrequency
                : (float)(weapon.GetProperty(PropertyFloat.CriticalFrequency) ?? defaultMagicCritFrequency);

            if (weapon.HasImbuedEffect(ImbuedEffectType.CriticalStrike)
                && !CritImbuesSuppressed(wielder))   // same ruling on the magic path
            {
                var isPvP = wielder is Player && target is Player;

                var criticalStrikeMod = GetCriticalStrikeMod(skill, isPvP);

                critRate = Math.Max(critRate, criticalStrikeMod);
            }

            critRate += wielder.GetCritRating() * 0.01f;

            // Zone Control: authored crit_rating REPLACES the governed monster's crit chance (see melee path)
            var zoneCrit = GetZoneCritChanceOverride(wielder);
            if (zoneCrit.HasValue)
                critRate = zoneCrit.Value;

            // mitigation
            var critResistRatingMod = Creature.GetNegativeRatingMod(target.GetCritResistRating());
            critRate *= critResistRatingMod;

            return critRate;
        }

        private const float defaultCritDamageMultiplier = 1.0f;

        /// <summary>
        /// Returns the critical damage multiplier for the attack weapon
        /// </summary>
        public static float GetWeaponCritDamageMod(WorldObject weapon, Creature wielder, CreatureSkill skill, Creature target)
        {
            // zone lock: outside authored areas a ZC weapon's Crushing Blow stamp AND its
            // aug-scaling crit term both read as absent
            var zcSuppressed = ZcPowerSuppressed(weapon, wielder);

            var critDamageMod = zcSuppressed
                ? defaultCritDamageMultiplier
                : (float)(weapon?.GetProperty(PropertyFloat.CriticalMultiplier) ?? defaultCritDamageMultiplier);

            if (weapon != null && weapon.HasImbuedEffect(ImbuedEffectType.CripplingBlow)
                && !CritImbuesSuppressed(wielder))   // owner 2026-08-25: Crushing Blow is the only crit-damage source on a player weapon
            {
                var cripplingBlowMod = GetCripplingBlowMod(skill);

                critDamageMod = Math.Max(critDamageMod, cripplingBlowMod);
            }

            // Weapon aug-scaling crit term (T11 weapon relevance): kc(quality) x per-aug crit
            // modifier x min(matching combat augs, tier cap) — the aug-pegged floor. Math.Max so
            // a Crushing Blow card / big CriticalMultiplier stays the jackpot above it (§7.10).
            if (!zcSuppressed)
                critDamageMod = Math.Max(critDamageMod,
                    ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.GetCritDamageBonus(weapon, wielder));

            // Zone Control: authored crit_damage_rating IS the final multiplier; engine computes 1 + mod,
            // so store value - 1 (e.g. authored 4 -> crits deal exactly 4x)
            var zoneCritDmg = GetZoneCritDamageOverride(wielder);
            if (zoneCritDmg.HasValue)
                critDamageMod = Math.Max(0f, zoneCritDmg.Value - 1.0f);

            return critDamageMod;
        }

        /// <summary>
        /// PvP damaged is halved, automatically displayed in the client
        /// </summary>
        public static readonly float ElementalDamageBonusPvPReduction = 0.5f;

        /// <summary>
        /// Returns a multiplicative elemental damage modifier for the magic caster weapon type
        /// </summary>
        public static float GetCasterElementalDamageModifier(WorldObject weapon, Creature wielder, Creature target, DamageType damageType)
        {
            if (wielder == null || !(weapon is Caster) || weapon.W_DamageType != damageType)
                return 1.0f;

            var wielderEnchantments = wielder.EnchantmentManager.GetElementalDamageMod();
            var weaponEnchantments = weapon.EnchantmentManager.GetElementalDamageMod();

            var enchantments = wielderEnchantments + weaponEnchantments;

            // Weapon aug-scaling: a stamped T11+ caster's quality roll GRADES its elemental
            // modifier and the weapon's TIER scales it — the launcher mechanism on a different
            // property (owner 2026-08-06: "keep bow and caster weapons scaling identical").
            //
            // COMPOSITION (owner GO 2026-08-06, CasterDamageShare_Plan): the resolved mod
            // MULTIPLIES the enchantment sum, the way the bow's DamageMod multiplies Blood
            // Drinker. Stock math added them, which made Spirit Drinker (+17.50 at 3,500 augs)
            // an additive peer ~11x the wand — the wand was ~8 pct of its own multiplier and a
            // T11 vs T14 wand measured ~1 pct apart. The rescale anchors S grade to the old
            // totals exactly (r = 1/kMax); see CasterAuraRescale in WeaponScalingManager.
            //
            // Replace semantics: the STOCK ADDITIVE path is the fallback whenever the system is
            // off or the caster is unstamped legacy — bit-for-bit pre-system behavior, so the
            // kill switch has a clean prediction.
            // zone lock: outside authored areas the graded caster mod is suppressed - the stock
            // additive fallback below IS the base-stats behaviour the lock lands on
            float modifier;
            if (!ZcPowerSuppressed(weapon, wielder)
                && ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.TryGetCasterElementalMod(weapon, wielder as Player, out var gradedMod))
                modifier = ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.ComposeCasterModifier(
                    gradedMod, enchantments, ACE.Server.Managers.WeaponScaling.WeaponScalingManager.Current.CasterAuraRescale);
            else
                modifier = (float)((weapon.ElementalDamageMod ?? 1.0f) + enchantments);

            if (modifier > 1.0f && target is Player)
                modifier = 1.0f + (modifier - 1.0f) * ElementalDamageBonusPvPReduction;

            return modifier;
        }

        /// <summary>
        /// Returns an additive elemental damage bonus for the missile launcher weapon type
        /// </summary>
        public static int GetMissileElementalDamageBonus(WorldObject weapon, Creature wielder, DamageType damageType)
        {
            if (weapon is MissileLauncher && weapon.ElementalDamageBonus != null)
            {
                var elementalDamageType = weapon.W_DamageType;

                if (elementalDamageType != DamageType.Undef && elementalDamageType == damageType)
                    return weapon.ElementalDamageBonus.Value;
            }
            return 0;
        }

        public CreatureType? SlayerCreatureType
        {
            get => (CreatureType?)GetProperty(PropertyInt.SlayerCreatureType);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.SlayerCreatureType); else SetProperty(PropertyInt.SlayerCreatureType, (int)value.Value); }
        }

        public double? SlayerDamageBonus
        {
            get => GetProperty(PropertyFloat.SlayerDamageBonus);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.SlayerDamageBonus); else SetProperty(PropertyFloat.SlayerDamageBonus, value.Value); }
        }

        /// <summary>
        /// Returns the slayer damage multiplier for the attack weapon
        /// against a particular creature type
        /// </summary>
        public static float GetWeaponCreatureSlayerModifier(WorldObject weapon, Creature wielder, Creature target)
        {
            // zone lock: outside authored areas a ZC weapon's Slayer stamp reads as absent
            if (ZcPowerSuppressed(weapon, wielder))
                return defaultModifier;

            if (weapon != null && weapon.SlayerDamageBonus != null && target != null
                && ((weapon.SlayerCreatureType != null && weapon.SlayerCreatureType == target.CreatureType)
                    // forge-only "slayer of all creatures" (PropertyBool 50052; /wsforge cards:premade or
                    // slayertype=all): the bonus applies to every target. Drops never carry it.
                    || weapon.GetProperty(PropertyBool.SlayerAllCreatures) == true))
            {
                // TODO: scale with base weapon skill?
                return (float)weapon.SlayerDamageBonus;
            }
            else
                return defaultModifier;
        }

        public DamageType? ResistanceModifierType
        {
            get => (DamageType?)GetProperty(PropertyInt.ResistanceModifierType);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.ResistanceModifierType); else SetProperty(PropertyInt.ResistanceModifierType, (int)value.Value); }
        }

        public double? ResistanceModifier
        {
            get => GetProperty(PropertyFloat.ResistanceModifier);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.ResistanceModifier); else SetProperty(PropertyFloat.ResistanceModifier, value.Value); }
        }

        /// <summary>
        /// Returns the resistance modifier or rending modifier
        /// </summary>
        public static float GetWeaponResistanceModifier(WorldObject weapon, Creature wielder, CreatureSkill skill, DamageType damageType)
        {
            float resistMod = defaultModifier;

            if (wielder == null)
                return defaultModifier;

            // handle quest weapon fixed resistance cleaving
            if (weapon != null && weapon.ResistanceModifierType != null && weapon.ResistanceModifierType == damageType)
                resistMod = 1.0f + (float)(weapon.ResistanceModifier ?? defaultModifier);       // 1.0 in the data, equivalent to a level 5 vuln

            // handle elemental resistance rending
            var rendDamageType = GetRendDamageType(damageType);

            if (rendDamageType == ImbuedEffectType.Undef)
            {
                if (weapon != null)
                    log.Debug($"{wielder.Name}.GetRendDamageType({damageType}) unexpected damage type for {weapon.Name} ({weapon.Guid})");
                return resistMod;
            }

            // Check weapon first, then check creature itself for CombatPets without weapons
            bool hasRending = false;
            if (weapon != null && weapon.HasImbuedEffect(rendDamageType))
            {
                hasRending = true;
            }
            else if (weapon != null && wielder is Player player && player.HasOmnistrike && CharmSettingsManager.Omnistrike.Enabled && player.CombatMode == CombatMode.Melee)
            {
                // Omni Strike design intent: while the charm is active, any rend imbue on the weapon
                // applies its rend bonus to the target's weakest element — regardless of which element the
                // rend was originally imbued for. The charm makes the weapon "adapt" its rend to match
                // whatever vulnerability was chosen by GetWeakestElement() in DamageEvent.
                // e.g. a SlashRending weapon vs a Fire-weak target will apply the rend mod to Fire resistance.
                var weaponImbues = weapon.GetImbuedEffects();
                bool weaponHasAnyRend = (weaponImbues & (
                    ImbuedEffectType.SlashRending |
                    ImbuedEffectType.PierceRending |
                    ImbuedEffectType.BludgeonRending |
                    ImbuedEffectType.AcidRending |
                    ImbuedEffectType.ColdRending |
                    ImbuedEffectType.ElectricRending |
                    ImbuedEffectType.FireRending |
                    ImbuedEffectType.NetherRending
                )) != 0;

                if (weaponHasAnyRend)
                {
                    hasRending = true;
                }
            }
            else if (wielder is CombatPet && wielder.HasImbuedEffect(rendDamageType))
            {
                // For CombatPets without weapons, check if rending was applied to the creature itself
                hasRending = true;
            }

            // zone lock: outside authored areas a ZC weapon's Rend (and its rolled power) reads
            // as absent - suppressed here, after eligibility, so retail/pet rends stay untouched
            if (hasRending && ZcPowerSuppressed(weapon, wielder))
                hasRending = false;

            if (hasRending && skill != null)
            {
                var rendingMod = GetRendingMod(skill);

                // Zone Control loot: per-weapon rend power override (ZoneLootMutator stamp) is a DIRECT
                // vuln bonus (wire 1.5..10.0 = +150%..+1000%). rendingMod = 1 + override, REPLACING the
                // skill-scaled formula and its 2.5 cap, so the drop's rolled strength is exactly 1000% max.
                var rendOverride = weapon?.GetProperty((PropertyFloat)ACE.Server.Managers.ZoneControl.ZoneLootMutator.RendingModOverridePropId);
                if (rendOverride.HasValue && rendOverride.Value > 0)
                    rendingMod = 1.0f + (float)rendOverride.Value;

                resistMod = Math.Max(resistMod, rendingMod);
            }

            return resistMod;
        }

        public ImbuedEffectType GetImbuedEffects()
        {
            return (ImbuedEffectType)(
                (GetProperty(PropertyInt.ImbuedEffect) ?? 0) |
                (GetProperty(PropertyInt.ImbuedEffect2) ?? 0) |
                (GetProperty(PropertyInt.ImbuedEffect3) ?? 0) |
                (GetProperty(PropertyInt.ImbuedEffect4) ?? 0) |
                (GetProperty(PropertyInt.ImbuedEffect5) ?? 0));
        }

        public bool HasImbuedEffect(ImbuedEffectType type)
        {
            return ImbuedEffect.HasFlag(type);
        }

        public static ImbuedEffectType GetRendDamageType(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Slash:
                    return ImbuedEffectType.SlashRending;
                case DamageType.Pierce:
                    return ImbuedEffectType.PierceRending;
                case DamageType.Bludgeon:
                    return ImbuedEffectType.BludgeonRending;
                case DamageType.Fire:
                    return ImbuedEffectType.FireRending;
                case DamageType.Cold:
                    return ImbuedEffectType.ColdRending;
                case DamageType.Acid:
                    return ImbuedEffectType.AcidRending;
                case DamageType.Electric:
                    return ImbuedEffectType.ElectricRending;
                case DamageType.Nether:
                    return ImbuedEffectType.NetherRending;
                default:
                    //log.Debug($"GetRendDamageType({damageType}) unexpected damage type");
                    return ImbuedEffectType.Undef;
            }
        }

        /// <summary>
        /// Returns TRUE if this item is enchantable, as per the client formula.
        /// </summary>
        public bool IsEnchantable => (ResistMagic ?? 0) < 9999;

        public int? ResistMagic
        {
            get => GetProperty(PropertyInt.ResistMagic);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.ResistMagic); else SetProperty(PropertyInt.ResistMagic, value.Value); }
        }

        public bool IgnoreMagicArmor
        {
            get => GetProperty(PropertyBool.IgnoreMagicArmor) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.IgnoreMagicArmor); else SetProperty(PropertyBool.IgnoreMagicArmor, value); }
        }

        public bool IgnoreMagicResist
        {
            get => GetProperty(PropertyBool.IgnoreMagicResist) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.IgnoreMagicResist); else SetProperty(PropertyBool.IgnoreMagicResist, value); }
        }

        //
        // Rending
        //
        // Rending gives the weapon the ability to make its opponent vulnerable to attacks of a certain specific element.
        // The amount of vulnerability depends on the attack skill of the wielder.
        // This effect does not stack with Life Magic Vulnerability spells.
        // Rending properties can be added to loot weapons by imbuing them with certain types of Salvage using the Weapon Tinkering skill.
        //
        // Rending can be found on some quest reward weapons, although it is much more common for them to have Resistance Cleaving,
        // which is a fixed effect instead of being skill-based like Rending.
        //

        // From Anon's formula docs:

        // Imbues for melee:
        // BS = base skill. These cap at 400 base. Additional base ranks do nothing more.
        // AR (Armor Rending): (BS - 160) / 400 = % of armor ignored, min 0%, max 60%
        // CS (Critical Strike): (BS - 100) / 600 = crit rate bonus, min 10%, max 50%
        // CB (Crippling Blow): (BS - 40) / 60, crit damage bonus, minimum of 2x, caps at 6x (7x damage cap upstream?)
        // Resistance Rending: BS / 160, elemental vuln damage modifier, capped at 2.5

        // Imbues for missile / war / void:
        // BS = base skill. These cap at 360 base. Additional base ranks do nothing more.
        // AR (Armor Rending) (missile only): (BS - 144) / 360
        // CS (Critical Strike): (BS - 60) / 600
        // CB (Crippling Blow): BS / 60
        // Resistance Rending: BS / 144

        // Later modified:
        // Accurate imbue formulas for CS/CB:

        // CS/Melee: (BS - 100) / 600
        // CB/Melee: (BS - 40) / 60

        // CS/Missile_Magic: (BS - 60) / 600
        // CB/Missile_Magic: BS / 60

        // Note that CS/CB scaling / modifiers / caps are different for PVP

        // Imbue effect hard caps, PVE (repeating some of the info in the doc):
        // - AR minimum of 0%, max of 60% armor ignored
        // - CS minimum of 5% (war/void), or 10% (melee/missile) to max 50% crit chance
        // - CB minimum of 1x (magic) or 2x (melee/missile) to max 6x crit dmg mod (see note below)
        // - Resist Rends min of 1.0x to max 2.5x (equivalent of vuln 6)

        // CB crit damage calc:
        // - Melee/missile: this is a direct multiplier of your maximum non-crit damage.
        // - Magic: my best guess here is that the CB modifier modifies the additional crit damage (see note-2), but i'm not 100% on this

        // http://acpedia.org/wiki/Announcements_-_2004/07_-_Treaties_in_Stone#Letter_to_the_Players

        // For PvE only:

        // Critical Strike for War Magic currently scales from 5% critical hit chance to 25% critical hit chance at maximum effectiveness.
        // In July, the maximum effectiveness will be increased to 50% chance.

        //public static float MinCriticalStrikeMagicMod = 0.05f;

        public static float MaxCriticalStrikeMod = 0.5f;

        public static float GetCriticalStrikeMod(CreatureSkill skill, bool isPvP = false)
        {
            var baseMod = 0.0f;

            var skillType = GetImbuedSkillType(skill);

            var baseSkill = GetBaseSkillImbued(skill);

            switch (skillType)
            {
                case ImbuedSkillType.Melee:

                    baseMod = Math.Max(0, baseSkill - 100) / 600.0f;
                    break;

                case ImbuedSkillType.Missile:
                case ImbuedSkillType.Magic:

                    baseMod = Math.Max(0, baseSkill - 60) / 600.0f;
                    break;

                default:
                    return 0.0f;
            }

            // http://acpedia.org/wiki/Announcements_-_2004/07_-_Treaties_in_Stone#Letter_to_the_Players

            // For PvE only:

            // Critical Strike for War Magic currently scales from 5% critical hit chance to 25% critical hit chance at maximum effectiveness.
            // In July, the maximum effectiveness will be increased to 50% chance.

            if (skillType == ImbuedSkillType.Magic && isPvP)
                baseMod *= 0.5f;

            // In the original formula for CS Magic pre-July 2004, (BS - 60) / 1200.0f, the minimum 5% crit rate would have been achieved at BS 120,
            // which is exactly equal to the minimum base skill for CS Missile becoming effective.

            // CS Magic is slightly different from all the other skill/imbue combinations, in that the MinCriticalStrikeMagicMod
            // is different from the defaultMagicCritFrequency (5% vs. 2%)

            // If we simply clamp to min. 5% here, then a player will be getting a +3% bonus from from base skill 0-90 in PvE,
            // and base skill 0-120 in PvP

            // This code is checking if the player has reached the skill threshold for receiving the 5% bonus
            // (base skill 90 in PvE, base skill 120 in PvP)

            /*var criticalStrikeMod = skillType == ImbuedSkillType.Magic ? defaultMagicCritFrequency : defaultPhysicalCritFrequency;

            var minEffective = skillType == ImbuedSkillType.Magic ? MinCriticalStrikeMagicMod : defaultPhysicalCritFrequency;

            if (baseMod >= minEffective)
                criticalStrikeMod = baseMod;*/

            var defaultCritFrequency = skillType == ImbuedSkillType.Magic ? defaultMagicCritFrequency : defaultPhysicalCritFrequency;

            var criticalStrikeMod = Math.Max(defaultCritFrequency, baseMod);

            //Console.WriteLine($"CriticalStrikeMod: {criticalStrikeMod}");

            return criticalStrikeMod;
        }

        public static float MaxCripplingBlowMod = 6.0f;

        public static float GetCripplingBlowMod(CreatureSkill skill)
        {
            // increases the critical damage multiplier, additive

            // http://acpedia.org/wiki/Announcements_-_2004/07_-_Treaties_in_Stone#Letter_to_the_Players

            // PvP only:

            // Crippling Blow for War Magic currently scales from adding 50% of the spells damage on critical hits to adding 100% at maximum effectiveness.
            // In July, the maximum effectiveness will be increased to adding up to 500% of the spell's damage.

            // ( +500% sounds like it would be 6.0 multiplier)

            var baseSkill = GetBaseSkillImbued(skill);

            var baseMod = 1.0f;

            switch(GetImbuedSkillType(skill))
            {
                case ImbuedSkillType.Melee:
                    baseMod = Math.Max(0, baseSkill - 40) / 60.0f;
                    break;

                case ImbuedSkillType.Missile:
                case ImbuedSkillType.Magic:

                    baseMod = baseSkill / 60.0f;
                    break;
            }

            var cripplingBlowMod = Math.Max(1.0f, baseMod);

            //Console.WriteLine($"CripplingBlowMod: {cripplingBlowMod}");

            return cripplingBlowMod;
        }

        // elemental rending cap, equivalent to level 6 vuln
        public static float MaxRendingMod = 2.5f;

        public static float GetRendingMod(CreatureSkill skill)
        {
            var baseSkill = GetBaseSkillImbued(skill);

            var rendingMod = 1.0f;

            switch (GetImbuedSkillType(skill))
            {
                case ImbuedSkillType.Melee:
                    rendingMod = baseSkill / 160.0f;
                    break;

                case ImbuedSkillType.Missile:
                case ImbuedSkillType.Magic:
                    rendingMod = baseSkill / 144.0f;
                    break;
            }

            rendingMod = Math.Clamp(rendingMod, 1.0f, MaxRendingMod);

            //Console.WriteLine($"RendingMod: {rendingMod}");

            return rendingMod;
        }

        public static float MaxArmorRendingMod = 0.6f;

        public static float GetArmorRendingMod(CreatureSkill skill)
        {
            // % of armor ignored, min 0%, max 60%

            var baseSkill = GetBaseSkillImbued(skill);

            var armorRendingMod = 1.0f;

            switch (GetImbuedSkillType(skill))
            {
                case ImbuedSkillType.Melee:
                    armorRendingMod -= Math.Max(0, baseSkill - 160) / 400.0f;
                    break;

                case ImbuedSkillType.Missile:
                    armorRendingMod -= Math.Max(0, baseSkill - 144) / 360.0f;
                    break;
            }

            //Console.WriteLine($"ArmorRendingMod: {armorRendingMod}");

            return armorRendingMod;
        }

        /// <summary>
        /// Armor Cleaving
        /// </summary>
        public double? IgnoreArmor
        {
            get => GetProperty(PropertyFloat.IgnoreArmor);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.IgnoreArmor); else SetProperty(PropertyFloat.IgnoreArmor, value.Value); }
        }

        public float GetArmorCleavingMod(WorldObject weapon)
        {
            // investigate: should this value be on creatures directly?
            var creatureMod = GetArmorCleavingMod();
            var weaponMod = weapon != null ? weapon.GetArmorCleavingMod() : 1.0f;

            return Math.Min(creatureMod, weaponMod);
        }

        public float GetArmorCleavingMod()
        {
            if (IgnoreArmor == null)
                return 1.0f;

            // FIXME: data
            var maxSpellLevel = GetMaxSpellLevel();

            // thanks to moro for this formula
            return 1.0f - (0.1f + maxSpellLevel * 0.05f);
        }

        public double? IgnoreShield
        {
            get => GetProperty(PropertyFloat.IgnoreShield);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.IgnoreShield); else SetProperty(PropertyFloat.IgnoreShield, value.Value); }
        }

        public float GetIgnoreShieldMod(WorldObject weapon)
        {
            var creatureMod = IgnoreShield ?? 0.0f;
            // zone lock: outside authored areas a ZC weapon's Shield Cleaving stamp reads as
            // absent (the attacker's own creature-side IgnoreShield is never gated)
            var weaponMod = ZcPowerSuppressed(weapon, this as Creature)
                ? 0.0
                : weapon?.IgnoreShield ?? 0.0f;

            return 1.0f - (float)Math.Max(creatureMod, weaponMod);
        }

        public static int GetBaseSkillImbued(CreatureSkill skill)
        {
            switch (GetImbuedSkillType(skill))
            {
                case ImbuedSkillType.Melee:
                    return (int)Math.Min(skill.Base, 400);

                case ImbuedSkillType.Missile:
                case ImbuedSkillType.Magic:
                default:
                    return (int)Math.Min(skill.Base, 360);
            }
        }

        public enum ImbuedSkillType
        {
            Undef,
            Melee,
            Missile,
            Magic
        }

        public static ImbuedSkillType GetImbuedSkillType(CreatureSkill skill)
        {
            switch (skill?.Skill)
            {
                case Skill.LightWeapons:
                case Skill.HeavyWeapons:
                case Skill.FinesseWeapons:
                case Skill.DualWield:
                case Skill.TwoHandedCombat:

                // legacy
                case Skill.Axe:
                case Skill.Dagger:
                case Skill.Mace:
                case Skill.Spear:
                case Skill.Staff:
                case Skill.Sword:
                case Skill.UnarmedCombat:

                    return ImbuedSkillType.Melee;

                case Skill.MissileWeapons:

                // legacy
                case Skill.Bow:
                case Skill.Crossbow:
                case Skill.Sling:
                case Skill.ThrownWeapon:

                    return ImbuedSkillType.Missile;


                case Skill.WarMagic:
                case Skill.VoidMagic:
                case Skill.LifeMagic:   // Martyr's Hecatomb

                    return ImbuedSkillType.Magic;

                default:
                    log.Debug($"WorldObject_Weapon.GetImbuedSkillType({skill?.Skill}): unexpected skill");
                    return ImbuedSkillType.Undef;
            }
        }

        /// <summary>
        /// Returns the base skill multiplier to the maximum bonus
        /// </summary>
        public static float GetImbuedInterval(CreatureSkill skill, bool useMin = true)
        {
            var skillType = GetImbuedSkillType(skill);

            var min = 0;
            if (useMin)
                min = skillType == ImbuedSkillType.Melee ? 150 : 125;
            var max = skillType == ImbuedSkillType.Melee ? 400 : 360;

            return GetInterval((int)skill.Base, min, max);
        }

        /// <summary>
        /// Returns an interval between 0-1
        /// </summary>
        public static float GetInterval(int num, int min, int max)
        {
            if (num <= min) return 0.0f;
            if (num >= max) return 1.0f;

            var range = max - min;

            return (float)(num - min) / range;
        }

        /// <summary>
        /// Projects a 0-1 interval between min and max
        /// </summary>
        public static float SetInterval(float interval, float min, float max)
        {
            var range = max - min;

            return interval * range + min;
        }

        /// Spell ID for 'Cast on Strike'
        /// </summary>
        public uint? ProcSpell
        {
            get => GetProperty(PropertyDataId.ProcSpell);
            set { if (!value.HasValue) RemoveProperty(PropertyDataId.ProcSpell); else SetProperty(PropertyDataId.ProcSpell, value.Value); }
        }

        /// <summary>
        /// The chance for activating 'Cast on strike' spell
        /// </summary>
        public double? ProcSpellRate
        {
            get => GetProperty(PropertyFloat.ProcSpellRate);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.ProcSpellRate); else SetProperty(PropertyFloat.ProcSpellRate, value.Value); }
        }

        /// <summary>
        /// If TRUE, 'Cast on strike' spell targets self
        /// instead of the target
        /// </summary>
        public bool ProcSpellSelfTargeted
        {
            get => GetProperty(PropertyBool.ProcSpellSelfTargeted) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.ProcSpellSelfTargeted); else SetProperty(PropertyBool.ProcSpellSelfTargeted, value); }
        }

        /// <summary>
        /// If TRUE on a weapon, its cast-on-strike can proc on cleaved targets
        /// Default is FALSE
        /// </summary>
        public bool ProcOnCleaveTargets
        {
            get => GetProperty(PropertyBool.WeaponProcOnCleaveTargets) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.WeaponProcOnCleaveTargets); else SetProperty(PropertyBool.WeaponProcOnCleaveTargets, value); }
        }

        // Deprecated: Non-projectile cleave proc flag removed in favor of single toggle

        /// <summary>
        /// If TRUE, multi-strike hits each roll a proc using geometric decay
        /// </summary>
        public bool AllowMultiStrikeProcs
        {
            get => GetProperty(PropertyBool.WeaponAllowMultiStrikeProcs) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.WeaponAllowMultiStrikeProcs); else SetProperty(PropertyBool.WeaponAllowMultiStrikeProcs, value); }
        }

        /// <summary>
        /// Geometric decay factor r for multi-strike (default 0.5)
        /// Stored value is complement c = (1 - r). Getter returns r = (1 - c).
        /// </summary>
        public float MultiStrikeDecay
        {
            get
            {
                var stored = (float)(GetProperty(PropertyFloat.WeaponMultiStrikeDecay) ?? 0.5f); // default 0.5 => r = 0.5
                if (stored < 0f) stored = 0f;
                if (stored > 1f) stored = 1f;
                var r = 1.0f - stored;
                if (r < 0f) r = 0f;
                if (r > 1f) r = 1f;
                return r;
            }
            set
            {
                var clamped = Math.Clamp(value, 0f, 1f);
                var stored = 1.0f - clamped;
                SetProperty(PropertyFloat.WeaponMultiStrikeDecay, stored);
            }
        }

        /// <summary>
        /// Geometric decay factor r for cleave procs per strike (default 0.5)
        /// Stored value is complement c = (1 - r). Getter returns r = (1 - c).
        /// </summary>
        public float CleaveStrikeDecay
        {
            get
            {
                var stored = (float)(GetProperty(PropertyFloat.WeaponCleaveStrikeDecay) ?? 0.5f);
                if (stored < 0f) stored = 0f;
                if (stored > 1f) stored = 1f;
                var r = 1.0f - stored;
                if (r < 0f) r = 0f;
                if (r > 1f) r = 1f;
                return r;
            }
            set
            {
                var clamped = Math.Clamp(value, 0f, 1f);
                var stored = 1.0f - clamped;
                SetProperty(PropertyFloat.WeaponCleaveStrikeDecay, stored);
            }
        }

        /// <summary>
        /// Returns TRUE if this item has a proc / 'cast on strike' spell
        /// </summary>
        /// <summary>True when EITHER Cast on Strike slot is filled. This has to include slot 2: every
        /// caller in WorldObject_Combat gates on HasProc before calling TryProcItem, so a weapon that
        /// rolled only a ring would otherwise never fire it.</summary>
        public bool HasProc => ProcSpell != null || ProcSpell2 != null;

        /// <summary>
        /// Returns TRUE if this item has a proc spell
        /// that matches the input spell
        /// </summary>
        public bool HasProcSpell(uint spellID)
        {
            return HasProc && ProcSpell == spellID;
        }

        /// <summary>Cast on Strike slot 2 - the RING. Slot 1 (the arc) is the engine's own ProcSpell,
        /// so nothing outside this file needs to know a second slot exists: TryProcItem rolls both.</summary>
        public uint? ProcSpell2
        {
            get => GetProperty((PropertyDataId)ACE.Server.Managers.ZoneControl.ZoneLootMutator.ProcSpell2PropId);
            set { if (!value.HasValue) RemoveProperty((PropertyDataId)ACE.Server.Managers.ZoneControl.ZoneLootMutator.ProcSpell2PropId); else SetProperty((PropertyDataId)ACE.Server.Managers.ZoneControl.ZoneLootMutator.ProcSpell2PropId, value.Value); }
        }

        /// <summary>Slot 2's own per-hit rate. Independent of slot 1's (owner 2026-08-27).</summary>
        public double? ProcSpellRate2
        {
            get => GetProperty((PropertyFloat)ACE.Server.Managers.ZoneControl.ZoneLootMutator.ProcRate2PropId);
            set { if (!value.HasValue) RemoveProperty((PropertyFloat)ACE.Server.Managers.ZoneControl.ZoneLootMutator.ProcRate2PropId); else SetProperty((PropertyFloat)ACE.Server.Managers.ZoneControl.ZoneLootMutator.ProcRate2PropId, value.Value); }
        }

        public bool HasProc2 => ProcSpell2 != null;

        /// <summary>
        /// Rolls BOTH Cast on Strike slots. Two independent rolls, not one roll that picks a spell
        /// (owner 2026-08-27: the arc and the ring are separate entities) - so a weapon carrying both
        /// procs more often than one carrying either, which is the only version where the second slot
        /// is actually worth rolling.
        ///
        /// Kept as a single public entry point on purpose: every caller in WorldObject_Combat and the
        /// cleave paths already calls TryProcItem, so folding slot 2 in here means the second slot
        /// needs no call-site changes anywhere and cannot be forgotten at one of them.
        /// </summary>
        public void TryProcItem(WorldObject attacker, Creature target, bool selfTarget)
        {
            TryProcItemWithChanceMod(attacker, target, selfTarget, 1.0f);
        }

        /// <summary>
        /// Try to proc with an external chance multiplier applied to the computed proc rate.
        /// </summary>
        public void TryProcItemWithChanceMod(WorldObject attacker, Creature target, bool selfTarget, float chanceMultiplier)
        {
            // zone lock: outside authored areas a ZC weapon's Cast on Strike slots (arc AND ring)
            // never fire. `this` is the proccing item, so cloaks/aetheria/player Ring Glyph
            // crafts are untouched - they never carry ZcTier.
            if (ZcPowerSuppressed(this, attacker as Creature))
                return;

            // Slot 1 - the arc, and every pre-existing proc on the shard (cloaks, aetheria, the ~11,700
            // player Ring Glyph crafts). Aetheria's own rate curve applies here and only here.
            if (ProcSpell != null)
            {
                var baseChance = ProcSpellRate ?? 0.0f;

                if (Aetheria.IsAetheria(WeenieClassId) && attacker is Creature wielder)
                    baseChance = Aetheria.CalcProcRate(this, wielder);

                TryProcOneSpell(attacker, target, selfTarget, ProcSpell.Value, baseChance, chanceMultiplier);
            }

            // Slot 2 - the ring. Rolled separately against its own rate.
            if (ProcSpell2 != null)
                TryProcOneSpell(attacker, target, selfTarget, ProcSpell2.Value, ProcSpellRate2 ?? 0.0, chanceMultiplier);
        }

        /// <summary>The body that was TryProcItem before the card gained a second slot - one roll, one
        /// spell. Unchanged in behaviour; it just takes the spell and rate as arguments now.</summary>
        /// <summary>Fires this item's primary proc spell with a 100 pct chance - the owner's /buff leg
        /// (2026-09-01) uses it to pre-cast the self-targeted surge of each worn aetheria, through the
        /// exact cast path a real proc takes (item caster, no creature-aug bake), so a test character
        /// starts a fight with the surges a real fight would have produced.</summary>
        public void ForceProcSpell(WorldObject attacker, Creature target, bool selfTarget)
        {
            if (ProcSpell == null)
                return;
            TryProcOneSpell(attacker, target, selfTarget, ProcSpell.Value, 1.0, 1.0f);
        }

        private void TryProcOneSpell(WorldObject attacker, Creature target, bool selfTarget, uint procSpellId, double baseChance, float chanceMultiplier)
        {
            var chance = Math.Clamp(baseChance * Math.Max(0f, chanceMultiplier), 0.0, 1.0);

            var rng = ThreadSafeRandom.Next(0.0f, 1.0f);
            if (rng >= chance)
                return;

            var spell = new Spell(procSpellId);

            if (spell.NotFound)
            {
                if (attacker is Player player)
                {
                    if (spell._spellBase == null)
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"SpellId {procSpellId} Invalid.", ChatMessageType.System));
                    else
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{spell.Name} spell not implemented, yet!", ChatMessageType.System));
                }
                return;
            }

            // not sure if this should go before or after the resist check
            // after would match Player_Magic, but would require changing the signature of TryCastSpell yet again
            // starting with the simpler check here
            if (!selfTarget && target != null && target.NonProjectileMagicImmune && !spell.IsProjectile)
            {
                if (attacker is Player player)
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You fail to affect {target.Name} with {spell.Name}", ChatMessageType.Magic));

                return;
            }

            var itemCaster = this is Creature ? null : this;

            // For self-targeted spells, use the attacker as the target
            var spellTarget = selfTarget ? attacker : target;

            if (spell.NonComponentTargetType == ItemType.None)
                attacker.TryCastSpell(spell, null, itemCaster, itemCaster, isWeaponSpell: true, fromProc: true);
            else if (spell.NonComponentTargetType == ItemType.Vestements)
            {
                // TODO: spell.NonComponentTargetType should probably always go through TryCastSpell_WithItemRedirects,
                // however i don't feel like testing every possible known type of item procspell in the current db to ensure there are no regressions
                // current test case: 33990 Composite Bow casting Tattercoat
                attacker.TryCastSpell_WithRedirects(spell, spellTarget, itemCaster, itemCaster, isWeaponSpell: true, fromProc: true);
            }
            else
                attacker.TryCastSpell(spell, spellTarget, itemCaster, itemCaster, isWeaponSpell: true, fromProc: true);
        }

        private bool? isMasterable;

        public bool IsMasterable
        {
            get
            {
                // should be based on this, but a bunch of the weapon data probably needs to be updated...
                //return W_WeaponType != WeaponType.Undef;

                // cache this?
                if (isMasterable == null)
                    isMasterable = LongDesc == null || !LongDesc.Contains("This weapon seems tough to master.", StringComparison.OrdinalIgnoreCase);

                return isMasterable.Value;
            }
        }

        // from the Dark Majesty strategy guide, page 150:

        // -   0 - 1/3 sec. Power-up Time = High Stab
        // - 1/3 - 2/3 sec. Power-up Time = High Backhand
        // -       2/3 sec+ Power-up Time = High Slash

        public static readonly float ThrustThreshold = 0.33f;

        /// <summary>
        /// Returns TRUE if this is a thrust/slash weapon,
        /// or if this weapon uses 2 different attack types based on the ThrustThreshold
        /// </summary>
        public bool IsThrustSlash
        {
            get
            {
                return W_AttackType.HasFlag(AttackType.Slash | AttackType.Thrust) ||
                       W_AttackType.HasFlag(AttackType.DoubleSlash | AttackType.DoubleThrust) ||
                       W_AttackType.HasFlag(AttackType.TripleSlash | AttackType.TripleThrust) ||
                       W_AttackType.HasFlag(AttackType.DoubleSlash);  // stiletto
            }
        }

        public AttackType GetAttackType(MotionStance stance, float powerLevel, bool offhand)
        {
            if (offhand)
                return GetOffhandAttackType(stance, powerLevel);

            var attackType = W_AttackType;

            if ((attackType & AttackType.Offhand) != 0)
            {
                //log.Warn($"{Name} ({Guid}, {WeenieClassId}).GetAttackType(): {attackType}");
                attackType &= ~AttackType.Offhand;
            }

            if (stance == MotionStance.DualWieldCombat)
            {
                if (attackType.HasFlag(AttackType.TripleThrust | AttackType.TripleSlash))
                {
                    if (powerLevel >= ThrustThreshold)
                        attackType = AttackType.TripleSlash;
                    else
                        attackType = AttackType.TripleThrust;
                }
                else if (attackType.HasFlag(AttackType.DoubleThrust | AttackType.DoubleSlash))
                {
                    if (powerLevel >= ThrustThreshold)
                        attackType = AttackType.DoubleSlash;
                    else
                        attackType = AttackType.DoubleThrust;
                }

                // handle old bugged stilettos that only have DoubleThrust
                // handle old bugged rapiers w/ Thrust, DoubleThrust
                else if (attackType.HasFlag(AttackType.DoubleThrust))
                {
                    if (powerLevel >= ThrustThreshold || !attackType.HasFlag(AttackType.Thrust))
                        attackType = AttackType.DoubleThrust;
                    else
                        attackType = AttackType.Thrust;
                }

                // handle old bugged poniards and newer tachis
                else if (attackType.HasFlag(AttackType.Thrust | AttackType.DoubleSlash))
                {
                    if (powerLevel >= ThrustThreshold)
                        attackType = AttackType.DoubleSlash;
                    else
                        attackType = AttackType.Thrust;
                }

                // gaerlan sword / py16 (iasparailaun)
                else if (attackType.HasFlag(AttackType.Thrust | AttackType.TripleSlash))
                {
                    if (powerLevel >= ThrustThreshold)
                        attackType = AttackType.TripleSlash;
                    else
                        attackType = AttackType.Thrust;
                }
            }
            else if (stance == MotionStance.SwordShieldCombat)
            {
                // force thrust animation when using a shield with a multi-strike weapon
                if (attackType.HasFlag(AttackType.TripleThrust))
                {
                    if (powerLevel >= ThrustThreshold || !attackType.HasFlag(AttackType.Thrust))
                        attackType = AttackType.TripleThrust;
                    else
                        attackType = AttackType.Thrust;
                }
                else if (attackType.HasFlag(AttackType.DoubleThrust))
                {
                    if (powerLevel >= ThrustThreshold || !attackType.HasFlag(AttackType.Thrust))
                        attackType = AttackType.DoubleThrust;
                    else
                        attackType = AttackType.Thrust;
                }

                // handle old bugged poniards and newer tachis w/ Thrust, DoubleSlash
                // and gaerlan sword / py16 (iasparailaun) w/ Thrust, TripleSlash
                else if (attackType.HasFlag(AttackType.Thrust) && (attackType & (AttackType.DoubleSlash | AttackType.TripleSlash)) != 0)
                    attackType = AttackType.Thrust;
            }
            else if (stance == MotionStance.SwordCombat)
            {
                // force slash animation when using no shield with a multi-strike weapon
                if (attackType.HasFlag(AttackType.TripleSlash))
                {
                    if (powerLevel >= ThrustThreshold || !attackType.HasFlag(AttackType.Thrust))
                        attackType = AttackType.TripleSlash;
                    else
                        attackType = AttackType.Thrust;
                }
                else if (attackType.HasFlag(AttackType.DoubleSlash))
                {
                    if (powerLevel >= ThrustThreshold || !attackType.HasFlag(AttackType.Thrust))
                        attackType = AttackType.DoubleSlash;
                    else
                        attackType = AttackType.Thrust;
                }

                // handle old bugged stilettos that only have DoubleThrust
                else if (attackType.HasFlag(AttackType.DoubleThrust))
                    attackType = AttackType.Thrust;
            }

            if (attackType.HasFlag(AttackType.Thrust | AttackType.Slash))
            {
                if (powerLevel >= ThrustThreshold)
                    attackType = AttackType.Slash;
                else
                    attackType = AttackType.Thrust;
            }

            return attackType;
        }

        public AttackType GetOffhandAttackType(MotionStance stance, float powerLevel)
        {
            var attackType = W_AttackType;

            if ((attackType & AttackType.Offhand) != 0)
            {
                //log.Warn($"{Name} ({Guid}, {WeenieClassId}).GetOffhandAttackType(): {attackType}");
                attackType &= ~AttackType.Offhand;
            }

            if (attackType.HasFlag(AttackType.TripleThrust | AttackType.TripleSlash))
            {
                if (powerLevel >= ThrustThreshold)
                    attackType = AttackType.OffhandTripleSlash;
                else
                    attackType = AttackType.OffhandTripleThrust;
            }
            else if (attackType.HasFlag(AttackType.DoubleThrust | AttackType.DoubleSlash))
            {
                if (powerLevel >= ThrustThreshold)
                    attackType = AttackType.OffhandDoubleSlash;
                else
                    attackType = AttackType.OffhandDoubleThrust;
            }

            // handle old bugged stilettos that only have DoubleThrust
            // handle old bugged rapiers w/ Thrust, DoubleThrust
            else if (attackType.HasFlag(AttackType.DoubleThrust))
            {
                if (powerLevel >= ThrustThreshold || !attackType.HasFlag(AttackType.Thrust))
                    attackType = AttackType.OffhandDoubleThrust;
                else
                    attackType = AttackType.OffhandThrust;
            }

            // handle old bugged poniards and newer tachis w/ Thrust, DoubleSlash
            else if (attackType.HasFlag(AttackType.Thrust | AttackType.DoubleSlash))
            {
                if (powerLevel >= ThrustThreshold)
                    attackType = AttackType.OffhandDoubleSlash;
                else
                    attackType = AttackType.OffhandThrust;
            }

            // gaerlan sword / py16 (iasparailaun) w/ Thrust, TripleSlash
            else if (attackType.HasFlag(AttackType.Thrust | AttackType.TripleSlash))
            {
                if (powerLevel >= ThrustThreshold)
                    attackType = AttackType.OffhandTripleSlash;
                else
                    attackType = AttackType.OffhandThrust;
            }

            else if (attackType.HasFlag(AttackType.Thrust | AttackType.Slash))
            {
                if (powerLevel >= ThrustThreshold)
                    attackType = AttackType.OffhandSlash;
                else
                    attackType = AttackType.OffhandThrust;
            }
            else
            {
                switch (attackType)
                {
                    case AttackType.Thrust:
                        attackType = AttackType.OffhandThrust;
                        break;

                    case AttackType.Slash:
                        attackType = AttackType.OffhandSlash;
                        break;

                    case AttackType.Punch:
                        attackType = AttackType.OffhandPunch;
                        break;
                }
            }
            return attackType;
        }
    }
}
