using log4net;

using ACE.Database.Models.World;
using ACE.Server.Factories.Entity;
using ACE.Server.WorldObjects;
using ACE.Server.Factories.Enum;

namespace ACE.Server.Factories.Tables
{
    public static class GearRatingChance
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        private static ChanceTable<bool> RatingChance = new ChanceTable<bool>()
        {
            ( false, 0.75f ),
            ( true,  0.25f ),
        };

        private static ChanceTable<int> ArmorRating = new ChanceTable<int>()
        {
            ( 1, 0.95f ),
            ( 2, 0.05f ),
        };

        private static ChanceTable<int> ClothingJewelryRating = new ChanceTable<int>()
        {
            ( 1, 0.70f ),
            ( 2, 0.25f ),
            ( 3, 0.05f ),
        };

        private static ChanceTable<bool> RatingChanceT9 = new ChanceTable<bool>()
        {
            ( false, 0.75f ),
            ( true,  0.25f ),
        };

        private static ChanceTable<int> ArmorRatingT9 = new ChanceTable<int>()
        {
            
            ( 2, 0.70f ),
            ( 3, 0.25f ),
            ( 4, 0.05f ),
        };

        private static ChanceTable<int> ClothingJewelryRatingT9 = new ChanceTable<int>()
        {
            
            ( 3, 0.70f ),
            ( 4, 0.25f ),
            ( 5, 0.05f ),
        };

        private static ChanceTable<bool> RatingChanceT10 = new ChanceTable<bool>()
        {
            ( false, 0.0f ),
            ( true,  1.0f ),
        };

        private static ChanceTable<int> ArmorRatingT10 = new ChanceTable<int>()
        {

            ( 6, 0.50f ),
            ( 7, 0.20f ),
            ( 8, 0.15f ),
            ( 9, 0.10f ),
            (10, 0.05f ),
        };

        private static ChanceTable<int> ClothingRatingT10 = new ChanceTable<int>()
        {

            ( 6, 0.50f ),
            ( 7, 0.25f ),
            ( 8, 0.15f ),
            ( 9, 0.07f ),
            ( 10, 0.03f ),
        };

        private static ChanceTable<int> JewelryRatingT10 = new ChanceTable<int>()
        {

            ( 20, 0.50f ),
            ( 25, 0.25f ),
            ( 50, 0.15f ),
            ( 75, 0.07f ),
            ( 100, 0.03f ),
        };

        private static ChanceTable<int> WeaponRatingT10 = new ChanceTable<int>()
        {

            ( 1, 0.70f ),
            ( 2, 0.20f ),
            ( 3, 0.10f ),
        };

        public static int Roll(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            // initial roll for rating chance
            if (!RatingChance.Roll(profile.LootQualityMod))
                return 0;

            // roll for the actual rating
            ChanceTable<int> rating = null;

            if (roll.HasArmorLevel(wo))
            {
                rating = ArmorRating;
            }
            else if (roll.IsClothing || roll.IsJewelry || roll.IsCloak)
            {
                rating = ClothingJewelryRating;
            }
            else
            {
                log.Error($"GearRatingChance.Roll({wo.Name}, {profile.TreasureType}, {roll.ItemType}): unknown item type");
                return 0;
            }

            return rating.Roll(profile.LootQualityMod);
        }

        public static int RollT9(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            // initial roll for rating chance
            if (!RatingChanceT9.Roll(profile.LootQualityMod))
                return 0;

            // roll for the actual rating
            ChanceTable<int> rating = null;

            if (roll.HasArmorLevel(wo))
            {
                rating = ArmorRatingT9;
            }
            else if (roll.IsClothing || roll.IsJewelry || roll.IsCloak)
            {
                rating = ClothingJewelryRatingT9;
            }
            else
            {
                log.Error($"GearRatingChance.Roll({wo.Name}, {profile.TreasureType}, {roll.ItemType}): unknown item type");
                return 0;
            }

            return rating.Roll(profile.LootQualityMod);
        }

        public static int RollT10(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            // initial roll for rating chance
            if (!RatingChanceT10.Roll(profile.LootQualityMod))
                return 0;

            // roll for the actual rating
            ChanceTable<int> rating = null;

            if (roll.HasArmorLevel(wo))
            {
                rating = ArmorRatingT10;
            }
            else if (roll.IsClothing || roll.IsCloak)
            {
                rating = ClothingRatingT10;
            }
            else if (roll.IsJewelry)
            {
                rating = JewelryRatingT10;
            }
            else if (roll.IsCaster || roll.IsMeleeWeapon || roll.IsMissileWeapon)
            {
                rating = WeaponRatingT10;
            }
            else
            {
                log.Error($"GearRatingChance.Roll({wo.Name}, {profile.TreasureType}, {roll.ItemType}): unknown item type");
                return 0;
            }

            return rating.Roll(profile.LootQualityMod);
        }
    }
}
