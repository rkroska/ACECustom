using System;
using System.Collections.Generic;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// What T11+ Zone Control gear is worth while the ZONE LOCK suppresses it (zc_armor_zone_lock / zc_weapon_zone_lock, a
    /// player outside every enabled area - or standing on a gear-lock spot, which the same gates cover). Owner 2026-10-07: locked T11 gear must act like "the same stats as T10 gear" -
    /// before this the lock dropped it to ZERO, and a full T11 kit outside a zone dealt ~4.3x less and took ~1.6x more than
    /// a real T10 set (bench, C:\AI\ZoneControl\Plan_ZoneLock_T10Fallback_2026-10-07.md).
    ///
    /// Every number is measured from REAL T10 gear on the shard (ilt1003, the 10-03 copy of live), never from a premade:
    ///   worn ratings  = the average of two NORMAL T10 characters' worn totals (Grumpy Old Man, Good Grief - the best-geared
    ///                   Drexel / Nerd Parade carry paragon items and enchants, owner: "Thats not normal"); aetheria excluded;
    ///   weapons       = the top loot cluster per weapon type (one-off admin items ignored) +10 pct (owner: "roughly 10%
    ///                   higher than the best in slot t10 for each weapon type");
    ///   weapon extras = the T10 ceiling of each property a T10 weapon can drop with or have applied.
    /// NOT the master-switch fallback (<see cref="ZoneFallback"/>): that one re-prices items while Zone Control is OFF and
    /// its rating anchors were calibrated before Max Health was a T10 rating (its line shrink gives ~100 HP; a normal T10 set
    /// carries ~715).
    /// </summary>
    public static class ZoneLockFallback
    {
        /// <summary>True = the zone lock's T10 fallback may apply (it is just zonecontrol_enabled). WeaponPowerSuppressed is also
        /// true while zonecontrol_enabled is OFF, and the armor lock (WornPowerSuppressed) ignores that switch; the switch keeps
        /// its own meaning (ruling 1, "fully inert") - so every read site asks this first and keeps its old behaviour when it
        /// is false.</summary>
        public static bool Active => ServerConfig.zonecontrol_enabled.Value;

        /// <summary>A full worn kit: 9 armor + shirt + pants + cloak + necklace + 2 rings + 2 bracelets + trinket (the /asforge
        /// roster). Only WORN ZC pieces count (Creature_Equipment.equippedZcWornCount) - never a weapon, caster, shield or ammo -
        /// and a multi-slot piece counts once, so a kit of multi-slot armor lands a little under the full totals.</summary>
        public const int FullKitPieces = 18;

        /// <summary>Real T10 worn totals of NORMAL players - the average of Grumpy Old Man / Good Grief (owner 2026-10-07: Drexel and
        /// Nerd Parade "have a ton of paragon items and enchants. Thats not normal"): GearDamage 128/104, DR 33/39, Crit 23/21,
        /// CritResist 4/0, CritDamage 121/110, CDR 25/17, HealBoost 195/50, Nether 102/90, MaxHealth 1,045/385. Aetheria
        /// EXCLUDED (owner 2026-10-07): they are not Zone Control gear, so a locked player's aetheria keep counting on their
        /// own - every real aetheria carries +4 Crit, which put GOM / GG at 23 / 21 and would have counted twice (11 / 9 without).</summary>
        private static readonly Dictionary<PropertyInt, int> WornTotals = new()
        {
            { PropertyInt.GearDamage, 116 },
            { PropertyInt.GearDamageResist, 36 },
            { PropertyInt.GearCrit, 10 },
            { PropertyInt.GearCritResist, 2 },
            { PropertyInt.GearCritDamage, 116 },
            { PropertyInt.GearCritDamageResist, 21 },
            { PropertyInt.GearHealingBoost, 123 },
            { PropertyInt.GearNetherResist, 96 },
            { PropertyInt.GearMaxHealth, 715 },
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

        /// <summary>Locked launcher damage mod (bow 3.44, crossbow 3.20, atlatl 3.16 - the top value at least 20 T10 launchers
        /// carry; Grumpy Old Man's bow is 3.39) +10 pct.</summary>
        public static float? LauncherDamageMod(WorldObject weapon) => weapon?.W_WeaponType switch
        {
            WeaponType.Bow => (float)(3.44 * OverBestT10),
            WeaponType.Crossbow => (float)(3.20 * OverBestT10),
            WeaponType.Thrown => (float)(3.16 * OverBestT10),
            _ => null,
        };

        /// <summary>Locked caster elemental damage mod: T10 best 1.50 (the top value at least 20 T10 casters carry; Good Grief's
        /// sceptre is 1.245) +10 pct.</summary>
        public const float CasterElementalMod = (float)(1.50 * OverBestT10);

        /// <summary>Biting Strike while locked: capped at the best T10 crit-chance craft (0.33 - Bag of Abyssal-Touched Gems /
        /// Salvaged Yellow Garnet), plus a Bandit Hilt's +0.25 when the weapon carries one (a T10 weapon can have both).</summary>
        public const double BitingStrikeCap = 0.33;

        /// <summary>Crushing Blow while locked, STORED (engine) space: capped at the common T10 craft both normal players carry
        /// (Bag of Abyssal-Touched Gems, 2.25 = 3.25x in combat), plus a Bandit Hilt's +0.175.</summary>
        public const double CrushingBlowCapStored = 2.25;

        /// <summary>Slayer while locked: the best T10 slayer on the shard (5.0x, 263 weapons).</summary>
        public const double SlayerCap = 5.0;

        /// <summary>Cleave while locked, in EXTRA targets (WorldObject.CleaveTargets = the raw Cleaving prop - 1): T10 weapons
        /// carry a raw Cleaving of up to 4 (1,320+ weapons; raw 2 is retail two-handers), i.e. 3 extra targets.</summary>
        public const int CleaveCap = 3;

        /// <summary>Cast on Strike while locked: each slot's proc RATE is capped at the top T10 weapon proc rate (0.60, 601
        /// weapons). Its damage is the spell's own (the Zone Control proc damage override stays off while locked). NOTE: outside
        /// T11 zones the wielded weapon is rolled twice per hit (WorldObject_Combat dedupeProcs) - a BUG re-added 09-10 that the
        /// owner wants fixed separately (10-07); a T10 weapon gets the same double roll today.</summary>
        public const double ProcRateCap = 0.60;

        /// <summary>Split Arrows while locked (owner 2026-10-07): a bow / crossbow / atlatl WITHOUT its own Split Arrows shoots
        /// the normal T10 split - 2 extra arrows at 0.33x damage, 8 m range (1,702 T10 launchers carry exactly that). One
        /// WITH its own keeps its own count; damage and range are held to the T10 ceiling (0.95x, 10 m). Runtime only - never
        /// written to the item, so it is gone the moment the player is back inside a zone.</summary>
        public const int SplitBonusCount = 2;
        public const float SplitBonusDamage = 0.33f, SplitBonusRange = 8f;
        public const float SplitDamageCap = 0.95f, SplitRangeCap = 10f;

        /// <summary>Blood Thirst / Spirit Thirst while locked (owner 2026-10-07: "add blood thirst and spirit thirst to all weapons",
        /// locked only - both real T10 sets measured carry Legendary Blood Thirst, T11 drops roll it 2 pct of the time). Every
        /// locked weapon fights as if it had the LEGENDARY cantrip: Blood Thirst +0.10 damage mod (melee / missile), Spirit
        /// Thirst +0.07 elemental mod (casters). Cantrips of one family do not stack, so a weapon with its own Thirst gets only
        /// the difference up to Legendary (its own spell already counts through its enchantments). Never written to the item.</summary>
        private static readonly (int Id, double Value)[] BloodThirstSpells = { (2598, 0.02), (2486, 0.03), (2586, 0.04), (4661, 0.07), (6089, 0.10) };
        private static readonly (int Id, double Value)[] SpiritThirstSpells = { (3251, 0.01), (3252, 0.02), (3250, 0.03), (4670, 0.05), (6098, 0.07) };
        public const double LegendaryBloodThirst = 0.10, LegendarySpiritThirst = 0.07;

        /// <summary>The Thirst top-up a locked weapon adds to its damage mod (Blood) or elemental mod (Spirit, casters).</summary>
        public static float ThirstTopUp(WorldObject weapon)
        {
            if (weapon == null)
                return 0f;
            var caster = weapon is Caster;
            var own = 0.0;
            foreach (var (id, value) in caster ? SpiritThirstSpells : BloodThirstSpells)
                if (value > own && weapon.Biota.SpellIsKnown(id, weapon.BiotaDatabaseLock))
                    own = value;
            return (float)Math.Max(0.0, (caster ? LegendarySpiritThirst : LegendaryBloodThirst) - own);
        }

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
            if (launcher == null)
                return (false, 0, 0f, 0f);
            // master switch OFF: the old lock behaviour - a suppressed launcher fires no split at all (its own damage value
            // kept for the appraisal line, as before)
            if (locked && !Active)
                return (false, 0, 0f, (float)(launcher.GetProperty(PropertyFloat.SplitArrowDamageMultiplier) ?? Creature.DEFAULT_SPLIT_ARROW_DAMAGE_MULTIPLIER));
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
