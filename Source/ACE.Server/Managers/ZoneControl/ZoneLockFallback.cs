using System;
using System.Collections.Generic;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// What T11+ Zone Control gear is worth while the ZONE LOCK suppresses it (zc_armor_zone_lock / zc_weapon_zone_lock, a
    /// player outside every enabled area). Owner 2026-10-07: locked T11 gear must act like "the same stats as T10 gear" -
    /// before this the lock dropped it to ZERO, and a full T11 kit outside a zone dealt ~4.3x less and took ~1.6x more than
    /// a real T10 set (bench, C:\AI\ZoneControl\Plan_ZoneLock_T10Fallback_2026-10-07.md).
    ///
    /// Every number is measured from REAL T10 gear on the shard (ilt1003, the 10-03 copy of live), never from a premade:
    ///   worn ratings  = the average of the two best-geared T10 characters' worn totals (Drexel, Nerd Parade);
    ///   weapons       = the top loot cluster per weapon type (one-off admin items ignored) +10 pct (owner: "roughly 10%
    ///                   higher than the best in slot t10 for each weapon type");
    ///   weapon extras = the T10 ceiling of each property a T10 weapon can drop with or have applied.
    /// NOT the master-switch fallback (<see cref="ZoneFallback"/>): that one re-prices items while Zone Control is OFF and
    /// its rating anchors were calibrated before Max Health was a T10 rating (its line shrink gives ~100 HP; real T10 sets
    /// carry ~1,750).
    /// </summary>
    public static class ZoneLockFallback
    {
        /// <summary>The fallback is the ZONE LOCK's answer only. WeaponPowerSuppressed is also true while zonecontrol_enabled is
        /// OFF, and that switch keeps its own meaning (ruling 1, "fully inert": base stats only) - so every read site asks
        /// this first and keeps its old behaviour when it is false.</summary>
        public static bool Active => ServerConfig.zonecontrol_enabled.Value;

        /// <summary>A full worn kit - the worn ratings below are the total a full locked T11 kit lands on.</summary>
        public const int FullKitPieces = 18;

        /// <summary>Real T10 worn totals (Drexel / Nerd Parade): GearDamage 211/198, DR 92/83, Crit 40/40, CritResist 19/19,
        /// CritDamage 196/202, CDR 73/73, HealBoost 355/355, Nether 142/144, MaxHealth 1,810/1,675.</summary>
        private static readonly Dictionary<PropertyInt, int> WornTotals = new()
        {
            { PropertyInt.GearDamage, 205 },
            { PropertyInt.GearDamageResist, 88 },
            { PropertyInt.GearCrit, 40 },
            { PropertyInt.GearCritResist, 19 },
            { PropertyInt.GearCritDamage, 199 },
            { PropertyInt.GearCritDamageResist, 73 },
            { PropertyInt.GearHealingBoost, 355 },
            { PropertyInt.GearNetherResist, 143 },
            { PropertyInt.GearMaxHealth, 1742 },
        };

        /// <summary>What the worn ZC pieces are worth for one rating while locked: the real T10 total, scaled by how much of a
        /// full kit is Zone Control gear (a half-T10 / half-T11 player keeps the T10 half's own ratings and gets half of this).</summary>
        public static int WornRating(PropertyInt rating, int zcPieces)
        {
            if (zcPieces <= 0 || !WornTotals.TryGetValue(rating, out var total))
                return 0;
            return (int)Math.Round(total * Math.Min(1.0, zcPieces / (double)FullKitPieces));
        }

        // ── weapons ─────────────────────────────────────────────────────────────────────────────

        /// <summary>T10 best base damage per weapon type (single strike, multi-strike), shard loot clusters 2026-10-07.</summary>
        private static readonly Dictionary<WeaponType, (int Single, int Multi)> MeleeBest = new()
        {
            { WeaponType.Unarmed, (132, 132) },
            { WeaponType.Sword, (152, 91) },
            { WeaponType.Axe, (157, 90) },
            { WeaponType.Mace, (148, 90) },
            { WeaponType.Spear, (154, 90) },
            { WeaponType.Dagger, (153, 86) },
            { WeaponType.Staff, (136, 136) },
            { WeaponType.TwoHanded, (140, 82) },
        };

        /// <summary>The owner's margin over best-in-slot T10.</summary>
        public const double OverBestT10 = 1.10;

        /// <summary>Locked melee max damage: best T10 of this weapon type (multi-strike rolls lower, so it has its own) +10 pct.
        /// Null for a type with no T10 measurement - the weapon keeps its own base.</summary>
        public static int? MeleeDamage(WorldObject weapon)
        {
            if (weapon == null || !MeleeBest.TryGetValue(weapon.W_WeaponType, out var best))
                return null;
            var multi = ((int)(weapon.W_AttackType) & MultiStrikeMask) != 0;
            return (int)Math.Round((multi ? best.Multi : best.Single) * OverBestT10);
        }

        // DoubleSlash .. TripleThrust and the offhand doubles / triples - the same mask the bench query used
        private const int MultiStrikeMask = 0x79E0;

        /// <summary>Locked launcher damage mod (bow 4.11, crossbow 3.20, atlatl 3.16) +10 pct.</summary>
        public static float? LauncherDamageMod(WorldObject weapon) => weapon?.W_WeaponType switch
        {
            WeaponType.Bow => (float)(4.11 * OverBestT10),
            WeaponType.Crossbow => (float)(3.20 * OverBestT10),
            WeaponType.Thrown => (float)(3.16 * OverBestT10),
            _ => null,
        };

        /// <summary>Locked caster elemental damage mod: T10 best 1.58 +10 pct.</summary>
        public const float CasterElementalMod = (float)(1.58 * OverBestT10);

        /// <summary>Biting Strike while locked: capped at the best T10 crit-chance craft (0.33 - Bag of Abyssal-Touched Gems /
        /// Salvaged Yellow Garnet), plus a Bandit Hilt's +0.25 when the weapon carries one (a T10 weapon can have both).</summary>
        public const double BitingStrikeCap = 0.33;

        /// <summary>Crushing Blow while locked, STORED (engine) space: capped at the best general T10 craft, Salvaged Turquoise
        /// 2.45 (3.45x in combat), plus a Bandit Hilt's +0.175.</summary>
        public const double CrushingBlowCapStored = 2.45;

        /// <summary>Slayer while locked: the best T10 slayer on the shard (5.0x, 263 weapons).</summary>
        public const double SlayerCap = 5.0;

        /// <summary>Cleave while locked: T10 weapons cleave up to 4 targets (1,320 weapons; 2 is retail two-handers).</summary>
        public const int CleaveCap = 4;

        /// <summary>Shield Cleaving while locked: T10 tops out at 1.0 (322 weapons).</summary>
        public const double ShieldCleaveCap = 1.0;

        /// <summary>Cast on Strike while locked: fires at most this often per hit - the top T10 weapon proc rate (0.60, 601
        /// weapons). Its damage is the spell's own (the Zone Control proc damage override stays off while locked).</summary>
        public const double ProcRateCap = 0.60;

        /// <summary>Split Arrows while locked (owner 2026-10-07): a bow / crossbow / atlatl WITHOUT its own Split Arrows shoots
        /// the normal T10 split - 2 extra arrows at 0.33x damage, 8 m range (1,702 T10 launchers carry exactly that). One
        /// WITH its own keeps its own count; damage and range are held to the T10 ceiling (0.95x, 10 m). Runtime only - never
        /// written to the item, so it is gone the moment the player is back inside a zone.</summary>
        public const int SplitBonusCount = 2;
        public const float SplitBonusDamage = 0.33f, SplitBonusRange = 8f;
        public const float SplitDamageCap = 0.95f, SplitRangeCap = 10f;

        /// <summary>A weapon's % stats while locked (owner 2026-10-07: "% attack and % melee d and % magic d etc, need to be t10
        /// tuned"): the best real T10 value per weapon class - the top value at least 20 T10 weapons carry (shard, wield 275+).
        /// T11 drops carry a flat 1.20 attack / 1.20 melee d / 1.00 missile d / 1.00 magic d / 0.20 mana conversion.</summary>
        private static readonly Dictionary<PropertyFloat, (float Melee, float Launcher, float Caster)> WeaponPercents = new()
        {
            { PropertyFloat.WeaponOffense, (1.45f, 1.20f, 1.20f) },
            { PropertyFloat.WeaponDefense, (1.60f, 1.50f, 1.37f) },
            { PropertyFloat.WeaponMissileDefense, (1.10f, 1.04f, 1.04f) },
            { PropertyFloat.WeaponMagicDefense, (1.04f, 1.04f, 1.04f) },
            { PropertyFloat.ManaConversionMod, (0f, 0f, 0.40f) },
        };

        /// <summary>The base value of one weapon % stat: the T10 value while the zone lock holds this ZC weapon, else
        /// <paramref name="own"/> unchanged.</summary>
        public static float WeaponPercent(WorldObject weapon, Creature wielder, PropertyFloat prop, float own)
        {
            if (weapon == null || !Active || !WeaponPercents.TryGetValue(prop, out var t10)
                || !ZoneControlManager.WeaponPowerSuppressed(weapon, wielder))
                return own;
            return weapon is Caster ? t10.Caster : weapon is MissileLauncher ? t10.Launcher : t10.Melee;
        }

        public static bool IsLauncher(WorldObject weapon)
            => weapon is MissileLauncher && weapon.W_WeaponType is WeaponType.Bow or WeaponType.Crossbow or WeaponType.Thrown;

        /// <summary>The Split Arrows a launcher fires with - the ONE reader for both the shot (Creature_Missile) and the split
        /// arrow's hit (DamageEvent), so the two can never disagree. Unlocked: the launcher's own props, exactly as before.
        /// Locked: see <see cref="SplitBonusCount"/>.</summary>
        public static (bool On, int Count, float Range, float Damage) SplitFor(WorldObject launcher, bool locked)
        {
            // master switch OFF: the old lock behaviour - a suppressed launcher fires no split at all
            if (locked && !Active)
                return (false, 0, 0f, 0f);
            if (launcher == null)
                return (false, 0, 0f, 0f);
            var own = launcher.GetProperty(PropertyBool.SplitArrows) == true;
            var count = launcher.GetProperty(PropertyInt.SplitArrowCount) ?? Creature.DEFAULT_SPLIT_ARROW_COUNT;
            var range = (float)(launcher.GetProperty(PropertyFloat.SplitArrowRange) ?? Creature.DEFAULT_SPLIT_ARROW_RANGE);
            var damage = (float)(launcher.GetProperty(PropertyFloat.SplitArrowDamageMultiplier) ?? Creature.DEFAULT_SPLIT_ARROW_DAMAGE_MULTIPLIER);
            if (!locked)
                return (own, count, range, damage);
            if (!IsLauncher(launcher))
                return (false, 0, 0f, 0f);
            // owner 2026-10-07: "If a t11 bow already has split arrow, no split arrow bonus, just use the t11 bow split arrow count"
            if (own)
                return (true, count, Math.Min(range, SplitRangeCap), Math.Min(damage, SplitDamageCap));
            return (true, SplitBonusCount, SplitBonusRange, SplitBonusDamage);
        }
    }
}
