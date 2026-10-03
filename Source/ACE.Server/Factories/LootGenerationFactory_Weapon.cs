using ACE.Common;
using ACE.Database.Models.World;
using ACE.Server.Factories.Tables;
using ACE.Server.Factories.Entity;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories
{
    public static partial class LootGenerationFactory
    {
        private static WorldObject CreateWeapon(TreasureDeath profile, bool isMagical)
        {
            int chance = ThreadSafeRandom.Next(1, 100);

            // Aligning drop ratio to better align with retail - HarliQ 11/11/19
            // Melee - 42%
            // Missile - 36%
            // Casters - 22%

            return chance switch
            {
                var rate when (rate < 34) => CreateMeleeWeapon(profile, isMagical),
                var rate when (rate > 33 && rate < 67) => CreateMissileWeapon(profile, isMagical),
                _ => CreateCaster(profile, isMagical),
            };
        }

        private static float RollWeaponSpeedMod(TreasureDeath treasureDeath)
        {
            var qualityLevel = QualityChance.Roll(treasureDeath);

            if (qualityLevel == 0)
                return 1.0f;    // no bonus

            var rng = (float)ThreadSafeRandom.Next(-0.025f, 0.025f);

            // min/max range: 67.5% - 100%
            var weaponSpeedMod = 1.0f - (qualityLevel * 0.025f + rng);

            //Console.WriteLine($"WeaponSpeedMod: {weaponSpeedMod}");

            return weaponSpeedMod;
        }

        private static bool TryMutateGearRatingForWeapons(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            // was == 10, which silently dropped weapon gear ratings at tier 11+
            if (profile.Tier < 10)
                return false;

            if (roll == null || !(roll.IsCaster || roll.IsMeleeWeapon || roll.IsMissileWeapon))   // null = legacy_loot_system
                return false;

            // T11+ (owner 2026-09-27): the old table gave 3-5, far below every other T11 roll. A weapon's
            // Damage / Crit Damage rating is now worth one Zone Control line of the same name - it feeds the
            // same worn total - so it rolls that line's band and grade: 14-69 at T11, tier-scaled to T25 like
            // the lines, following a band authored on the tier Default (not a zone's own band override).
            // The value is FROZEN at drop: it is a plain GearDamage / GearCritDamage with no grade record, so
            // Live Stat Resolution, a later band edit and the zonecontrol_enabled switch never move it.
            if (profile.Tier >= 11)
            {
                var critDamage = ThreadSafeRandom.Next(0, 1) == 1;
                var key = critDamage ? 29 : 28;   // 28 Damage Rating, 29 Crit Damage Rating
                var (min, max) = Managers.ZoneControl.ZoneStatResolver.EffectiveBand(key, profile.Tier);
                var value = Managers.ZoneControl.ZoneStatResolver.ValueFor(min, max, Managers.ZoneControl.ZoneStatResolver.RollGrade(profile.Tier, false, Managers.ZoneControl.ZoneStatResolver.DropFloor));
                // Frozen at drop, so a mistyped authored band would mint permanent outliers: never above the
                // line's own T25 catalog ceiling (138 for 28/29).
                if (Managers.ZoneControl.ZoneModifiers.TryGet(key, out var def))
                    value = System.Math.Min(value, Managers.ZoneControl.ZoneModifiers.CatalogBandAt(def, 25).Max);

                if (value <= 0)
                    return false;

                if (critDamage)
                    wo.GearCritDamage = value;
                else
                    wo.GearDamage = value;

                return true;
            }

            int gearRating = GearRatingChance.RollForTier(wo, profile, roll); // Make sure this supports weapon types

            if (gearRating == 0)
                return false;

            int rollType = ThreadSafeRandom.Next(0, 2); // 0 or 1

            if (rollType == 0)
                wo.GearDamage = gearRating;
            else
                wo.GearCritDamage = gearRating;

            return true;
        }
    }
}
