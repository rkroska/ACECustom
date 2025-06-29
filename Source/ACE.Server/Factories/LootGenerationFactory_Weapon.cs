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
            if (profile.Tier != 10)
                return false;

            int gearRating = GearRatingChance.RollT10(wo, profile, roll); // Make sure this supports weapon types

            if (gearRating == 0)
                return false;

            int rollType = ThreadSafeRandom.Next(0, 2); // 0 or 1

            if (roll.IsCaster || roll.IsMeleeWeapon || roll.IsMissileWeapon)
            {
                if (rollType == 0)
                    wo.GearDamage = gearRating;
                else
                    wo.GearCritDamage = gearRating;

                return true;
            }

            return false;
        }
    }
}
