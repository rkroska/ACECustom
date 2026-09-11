using System.Linq;

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

        private static ChanceTable<bool> RatingChanceT11 = new ChanceTable<bool>()
        {
            ( false, 0.0f ),
            ( true,  1.0f ),
        };

        // Tier 11 = T10 plus twice the T9->T10 step, matching the mutation scripts:
        //   armor    T9 2..4 -> T10 6..10  (step +4/+6)  -> T11 16..20
        //   clothing T9 3..5 -> T10 6..10  (step +3/+5)  -> T11 14..18
        // Weapons have no T9 basis (WeaponRatingT10 was the first weapon table), so they
        // take a flat +2. Jewelry is NOT delta-doubled: T9 3..5 -> T10 20..100 is a change
        // of scale rather than a step, so doubling that delta is meaningless -- it takes a
        // straight 2x instead.
        private static ChanceTable<int> ArmorRatingT11 = new ChanceTable<int>()
        {
            (16, 0.50f ),
            (17, 0.20f ),
            (18, 0.15f ),
            (19, 0.10f ),
            (20, 0.05f ),
        };

        private static ChanceTable<int> ClothingRatingT11 = new ChanceTable<int>()
        {
            (14, 0.50f ),
            (15, 0.25f ),
            (16, 0.15f ),
            (17, 0.07f ),
            (18, 0.03f ),
        };

        private static ChanceTable<int> JewelryRatingT11 = new ChanceTable<int>()
        {
            ( 40, 0.50f ),
            ( 50, 0.25f ),
            (100, 0.15f ),
            (150, 0.07f ),
            (200, 0.03f ),
        };

        private static ChanceTable<int> WeaponRatingT11 = new ChanceTable<int>()
        {
            ( 3, 0.70f ),
            ( 4, 0.20f ),
            ( 5, 0.10f ),
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

        public static int RollT11(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            // initial roll for rating chance
            if (!RatingChanceT11.Roll(profile.LootQualityMod))
                return 0;

            // roll for the actual rating
            ChanceTable<int> rating = null;

            if (roll.HasArmorLevel(wo))
            {
                rating = ArmorRatingT11;
            }
            else if (roll.IsClothing || roll.IsCloak)
            {
                rating = ClothingRatingT11;
            }
            else if (roll.IsJewelry)
            {
                rating = JewelryRatingT11;
            }
            else if (roll.IsCaster || roll.IsMeleeWeapon || roll.IsMissileWeapon)
            {
                rating = WeaponRatingT11;
            }
            else
            {
                log.Error($"GearRatingChance.RollT11({wo.Name}, {profile.TreasureType}, {roll.ItemType}): unknown item type");
                return 0;
            }

            return rating.Roll(profile.LootQualityMod);
        }

        /// <summary>
        /// Dispatches to the highest authored gear-rating table for the profile's tier.
        /// Tiers above the last authored table clamp to it.
        /// </summary>
        public static int RollForTier(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            return profile.Tier switch
            {
                >= 11 => RollT11(wo, profile, roll),
                10 => RollT10(wo, profile, roll),
                9 => RollT9(wo, profile, roll),
                _ => Roll(wo, profile, roll),
            };
        }

        public enum GearRatingCategory
        {
            Armor,      // pieces with an armor level, incl. shields and crowns
            Clothing,   // clothing / cloaks
            Jewelry,
            Weapon,
        }

        /// <summary>
        /// The possible roll range of the tier-11 table for a category — used by the item's
        /// appraisal Ratings block ("min X, max Y"). Computed from the tables so it can never
        /// drift from what actually rolls.
        /// </summary>
        public static (int Min, int Max) GetRangeT11(GearRatingCategory category)
        {
            var table = category switch
            {
                GearRatingCategory.Armor => ArmorRatingT11,
                GearRatingCategory.Clothing => ClothingRatingT11,
                GearRatingCategory.Jewelry => JewelryRatingT11,
                _ => WeaponRatingT11,
            };

            return (table.Min(e => e.result), table.Max(e => e.result));
        }
    }
}
