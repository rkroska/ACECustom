using System;
using System.Collections.Generic;

using ACE.Entity;
using ACE.Entity.Models;
using ACE.Server.Managers;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Reduced-reward copies of Thaelaryn Island and the Valley of Death.
    ///
    /// The copy is the SAME landblocks on one extra variation (server property clone_zone_variation), filled through
    /// SQL alone: the base placements copied to that variation, and each encounter spawn point of the Valley of Death
    /// written as a placed generator there (the encounter table itself has no variation and stays on the base layer).
    /// Same generators, same monsters, same quests and kill tasks - no weenie is duplicated. What differs is decided here:
    ///
    ///   - kill luminance is multiplied by the area's reward multiplier;
    ///   - create-list drops of the listed items (clone_zone_reduced_drops) are kept with that same chance, whether the
    ///     item is rolled from the create list at death or carried by the creature and moved to its corpse (loot
    ///     generated from treasure tables is not touched);
    ///   - the UCM check does not run there.
    ///
    /// clone_zone_variation = 0 switches all of it off: no landblock is a clone zone and every method answers "no".
    /// </summary>
    public static class CloneZones
    {
        public enum Area { None, Thaelaryn, ValleyOfDeath }

        public static Area AreaOf(ushort landblock, int? variation)
        {
            var layer = ServerConfig.clone_zone_variation.Value;
            if (layer <= 0 || !variation.HasValue || variation.Value != layer)
                return Area.None;

            if (LandblockCollections.ThaelarynIslandLandblocks.Contains(landblock))
                return Area.Thaelaryn;
            if (LandblockCollections.ValleyOfDeathLandblocks.Contains(landblock))
                return Area.ValleyOfDeath;
            return Area.None;
        }

        public static Area AreaOf(Position position)
        {
            return position == null ? Area.None : AreaOf((ushort)position.Landblock, position.Variation);
        }

        public static bool Contains(Position position) => AreaOf(position) != Area.None;

        public static bool Contains(ushort landblock, int? variation) => AreaOf(landblock, variation) != Area.None;

        /// <summary>
        /// The reward multiplier of an area: 1 outside a clone zone. Never negative; a value that is not a number counts
        /// as 1, so a mistyped property cannot wipe out or explode the rewards.
        /// </summary>
        public static double RewardMultiplier(Area area)
        {
            double value;
            switch (area)
            {
                case Area.Thaelaryn: value = ServerConfig.clone_zone_thaelaryn_reward_mult.Value; break;
                case Area.ValleyOfDeath: value = ServerConfig.clone_zone_vod_reward_mult.Value; break;
                default: return 1.0;
            }
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 1.0;
            return Math.Max(0.0, value);
        }

        public static double RewardMultiplier(Position position) => RewardMultiplier(AreaOf(position));

        /// <summary>Kill luminance in a clone zone. Outside one, or at a multiplier of 1, the award comes back untouched.</summary>
        public static long ScaleLuminance(long award, Position position)
        {
            return ScaleLuminance(award, RewardMultiplier(position));
        }

        /// <summary>The award times the multiplier, rounded. A multiplier large enough to push the result past what a long
        /// holds gives the largest long instead of an undefined conversion.</summary>
        internal static long ScaleLuminance(long award, double mult)
        {
            if (mult == 1.0 || award <= 0)
                return award;
            var scaled = Math.Round(award * mult);
            return scaled >= long.MaxValue ? long.MaxValue : (long)scaled;
        }

        private static readonly object reducedLock = new object();
        private static string reducedRaw;
        private static HashSet<uint> reducedDrops = new HashSet<uint>();

        /// <summary>The items whose corpse drops are cut in a clone zone: the comma-separated wcids of clone_zone_reduced_drops.</summary>
        public static HashSet<uint> ReducedDrops
        {
            get
            {
                var raw = ServerConfig.clone_zone_reduced_drops.Value ?? "";
                lock (reducedLock)
                {
                    if (raw == reducedRaw)
                        return reducedDrops;

                    reducedRaw = raw;
                    reducedDrops = ParseWcids(raw);
                    return reducedDrops;
                }
            }
        }

        /// <summary>A list of wcids as an admin types it: separated by commas, semicolons or spaces. Anything that is not
        /// a number is left out; 0 is left out too, because 0 is the drop table's "nothing" entry.</summary>
        internal static HashSet<uint> ParseWcids(string raw)
        {
            var parsed = new HashSet<uint>();
            if (string.IsNullOrWhiteSpace(raw))
                return parsed;

            foreach (var part in raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (uint.TryParse(part, out var wcid) && wcid != 0)
                    parsed.Add(wcid);
            return parsed;
        }

        /// <summary>
        /// The corpse drops that were rolled for a creature that died at "position". In a clone zone every rolled drop of
        /// a listed item is kept with a chance equal to the area's reward multiplier - a guaranteed drop included - and
        /// everything else passes through.
        /// roll returns a number from 0 (inclusive) to 1 (exclusive).
        /// </summary>
        public static List<PropertiesCreateList> ReduceDrops(List<PropertiesCreateList> rolled, Position position, Func<double> roll)
        {
            var mult = RewardMultiplier(position);
            if (mult >= 1.0)
                return rolled;
            return ReduceDrops(rolled, mult, ReducedDrops, roll);
        }

        /// <summary>The rule itself: a rolled drop of a listed item stays when roll() is below keepChance. A keepChance of
        /// 1 or more changes nothing - this can only take drops away, never add one.</summary>
        internal static List<PropertiesCreateList> ReduceDrops(List<PropertiesCreateList> rolled, double keepChance, HashSet<uint> reduced, Func<double> roll)
        {
            if (rolled == null || rolled.Count == 0 || keepChance >= 1.0 || reduced == null || reduced.Count == 0)
                return rolled;

            var kept = new List<PropertiesCreateList>(rolled.Count);
            foreach (var item in rolled)
            {
                if (KeepsDrop(item.WeenieClassId, keepChance, reduced, roll))
                    kept.Add(item);
            }
            return kept;
        }

        /// <summary>
        /// One item a creature carries or wields and would hand to its corpse when it dies at "position": does it still
        /// drop? The same rule as for a rolled drop, for the drops that are not rolled at death - an item created with the
        /// creature and moved to the corpse. Outside a clone zone, and for an item that is not listed, always true.
        /// </summary>
        public static bool KeepsDrop(uint wcid, Position position, Func<double> roll)
        {
            var mult = RewardMultiplier(position);
            if (mult >= 1.0)
                return true;
            return KeepsDrop(wcid, mult, ReducedDrops, roll);
        }

        /// <summary>A listed item stays when roll() is below keepChance; an item that is not listed always stays, and no
        /// roll is spent on it.</summary>
        internal static bool KeepsDrop(uint wcid, double keepChance, HashSet<uint> reduced, Func<double> roll)
        {
            if (keepChance >= 1.0 || reduced == null || !reduced.Contains(wcid))
                return true;
            return roll() < keepChance;
        }
    }
}
