using System;
using System.Collections.Generic;

using ACE.Common;
using ACE.Database.Models.World;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;
using ACE.Server.Factories.Tables.Wcids;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories
{
    /// <summary>
    /// T11+ zone-kill extras (owner 2026-10-24): T10-style mundane loot and summoning essences on top of the zone gear set.
    /// Per kill, independently: a chance of ONE T10 summoning essence at level 250 or 300, a chance of ONE mundane item
    /// (pyreal stack, mana stone, food or lockpick by weight), and aetheria at T10's own rate. No siphon lens here - the
    /// one per-death lens roll (Creature_Death.GenerateSiphonLens) already covers these kills. Everything is made at
    /// tier 10, through the same wcid tables and mutations a T10 corpse uses.
    /// </summary>
    public static partial class LootGenerationFactory
    {
        public enum ZoneKillMundane { Pyreal, ManaStone, Food, Lockpick }

        /// <summary>What one kill's extras roll decided, before any item exists.</summary>
        public sealed class ZoneKillExtrasPlan
        {
            /// <summary>250 or 300 when the essence roll hit, else null.</summary>
            public int? EssenceLevel;
            /// <summary>The mundane item kind when the mundane roll hit, else null.</summary>
            public ZoneKillMundane? Mundane;
        }

        /// <summary>The tier every extra is made at: the mundane wcid tables and coin ranges stop at 10.</summary>
        public const int ZoneKillExtrasTier = 10;

        /// <summary>
        /// The pure roll: rng returns a uniform value in [0, 1). Chances are clamped to [0, 1]; a negative weight counts
        /// as 0, and all-zero weights mean no mundane item even on a hit. Three draws when the mundane roll hits.
        /// </summary>
        public static ZoneKillExtrasPlan PlanZoneKillExtras(Func<double> rng, double essenceChance, double essence250Share,
            double mundaneChance, double pyrealWeight, double manaStoneWeight, double foodWeight, double lockpickWeight)
        {
            var plan = new ZoneKillExtrasPlan();

            if (rng() < Clamp01(essenceChance))
                plan.EssenceLevel = rng() < Clamp01(essence250Share) ? 250 : 300;

            if (rng() < Clamp01(mundaneChance))
                plan.Mundane = PickZoneKillMundane(rng(), pyrealWeight, manaStoneWeight, foodWeight, lockpickWeight);

            return plan;
        }

        /// <summary>Weighted pick from one uniform value in [0, 1); null when every weight is 0 or below.</summary>
        public static ZoneKillMundane? PickZoneKillMundane(double roll, double pyrealWeight, double manaStoneWeight, double foodWeight, double lockpickWeight)
        {
            var weights = new[] { Math.Max(0, pyrealWeight), Math.Max(0, manaStoneWeight), Math.Max(0, foodWeight), Math.Max(0, lockpickWeight) };
            var total = weights[0] + weights[1] + weights[2] + weights[3];
            if (!(total > 0))
                return null;

            var target = roll * total;
            var acc = 0.0;
            for (var i = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (target < acc && weights[i] > 0)
                    return (ZoneKillMundane)i;
            }
            // roll at the top edge: the last kind with weight
            for (var i = weights.Length - 1; i >= 0; i--)
                if (weights[i] > 0)
                    return (ZoneKillMundane)i;
            return null;
        }

        private static double Clamp01(double v) => double.IsNaN(v) ? 0 : Math.Clamp(v, 0.0, 1.0);

        /// <summary>
        /// Rolls and creates one T11+ zone kill's extras from the live zc_kill_extra_* settings. Never counts toward the
        /// zone gear budget - the caller adds these after the gear set is placed. killTreasure only lends its loot quality
        /// mod and type; the tier is always 10.
        /// </summary>
        public static List<WorldObject> CreateZoneKillExtras(TreasureDeath killTreasure)
        {
            var extras = new List<WorldObject>();

            var profile = new TreasureDeath
            {
                Id = killTreasure?.Id ?? 0,
                TreasureType = killTreasure?.TreasureType ?? 0,
                Tier = ZoneKillExtrasTier,
                LootQualityMod = killTreasure?.LootQualityMod ?? 0,
            };

            var plan = PlanZoneKillExtras(() => ThreadSafeRandom.Next(0.0f, 1.0f),
                ServerConfig.zc_kill_extra_essence_chance.Value,
                ServerConfig.zc_kill_extra_essence_250_share.Value,
                ServerConfig.zc_kill_extra_mundane_chance.Value,
                ServerConfig.zc_kill_extra_weight_pyreal.Value,
                ServerConfig.zc_kill_extra_weight_manastone.Value,
                ServerConfig.zc_kill_extra_weight_food.Value,
                ServerConfig.zc_kill_extra_weight_lockpick.Value);

            if (plan.EssenceLevel.HasValue)
            {
                var roll = new TreasureRoll(TreasureItemType_Orig.PetDevice) { Wcid = PetDeviceWcids.RollAtLevel(plan.EssenceLevel.Value) };
                AddIfMade(extras, CreateAndMutateWcid(profile, roll, false));
            }

            if (plan.Mundane.HasValue)
            {
                TreasureRoll roll;
                switch (plan.Mundane.Value)
                {
                    case ZoneKillMundane.Pyreal:
                        roll = new TreasureRoll(TreasureItemType_Orig.Pyreal) { Wcid = WeenieClassName.coinstack };
                        break;
                    case ZoneKillMundane.ManaStone:
                        roll = new TreasureRoll(TreasureItemType_Orig.ManaStone) { Wcid = ManaStoneWcids.Roll(profile) };
                        break;
                    case ZoneKillMundane.Food:
                        roll = new TreasureRoll(TreasureItemType_Orig.Consumable) { Wcid = ConsumeWcids.Roll(profile) };
                        break;
                    default:
                        roll = new TreasureRoll(TreasureItemType_Orig.Lockpick) { Wcid = LockpickWcids.Roll(profile) };
                        break;
                }
                AddIfMade(extras, CreateAndMutateWcid(profile, roll, false));
            }

            // aetheria at T10's rate: the existing aetheria roll alone - the mundane add-on's siphon lens part is left out
            if (ServerConfig.zc_kill_extra_aetheria_enabled.Value)
                AddIfMade(extras, TryRollAetheria(profile));

            return extras;
        }

        private static void AddIfMade(List<WorldObject> list, WorldObject wo)
        {
            if (wo != null)
                list.Add(wo);
        }

        /// <summary>How many of the extras still fit under the corpse cap given the items already on it (never below 0).</summary>
        public static int ZoneKillExtrasRoom(int itemsAlready, int cap) => Math.Max(0, cap - Math.Max(0, itemsAlready));
    }
}
