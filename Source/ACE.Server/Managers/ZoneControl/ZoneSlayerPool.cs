using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers.ZoneScaling;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.ZoneControl
{
    /// <summary>
    /// Two T11+ loot rules that share the same monster data (owner 2026-10-30).
    ///
    /// THE POOL. A normal Slayer drop can name any species in the zone, not only the one killed, so every corpse is worth
    /// checking. It only changes which species a Slayer names: the Slayer chance is untouched and the weapon's element plays
    /// no part (owner ruling: normal drops are NOT matched to their element - that rule is for rares only). Monsters are
    /// counted as they spawn. Each spawn source counts only up to what it can hold at once, so the weights settle on the
    /// zone's capacity: a one-off boss's Slayer stays rare, and respawns and landblock reloads cannot drift the mix over a
    /// long uptime. The pool is in memory: it starts empty on restart, fills as areas load, and a drop falls back to the
    /// killed species until then. Zone Control reloads leave it alone (the plugin syncs every couple of seconds);
    /// `/zcslayerpool clear` empties it. Rares and Bags of Vengeance keep the killed species.
    ///
    /// THE RARE ELEMENT. A Pristine or Ascendant weapon deals the element the monster that dropped it is weakest to. Tou
    /// Tou's monsters take full damage from one element and 25% from the rest, a rare's Slayer is that monster's species,
    /// and a rare takes no bags, so a random element would usually leave a Slayer cancelled by a 4x resist for good.
    /// </summary>
    public static class ZoneSlayerPool
    {
        /// <summary>The seven elements a weakness is chosen from. Nether is out: every custom-resist monster takes the same
        /// from it, so it is never what sets one apart.</summary>
        private static readonly DamageType[] Elements =
        {
            DamageType.Slash, DamageType.Pierce, DamageType.Bludgeon, DamageType.Cold, DamageType.Fire, DamageType.Acid, DamageType.Electric,
        };

        internal readonly record struct Entry(CreatureType Species, long Count);

        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<CreatureType, long>> pools = new();

        // how many of each WCID each spawn source has counted, so a source stops at its cap
        private static readonly ConcurrentDictionary<(string Zone, uint Source, uint Wcid), int> counted = new();

        // a nested generator chain multiplies; this only stops an absurd authored value overflowing
        private const int MaxSourceCap = 100000;

        // -- weakest element -----------------------------------------------------------------------------

        /// <summary>
        /// The element(s) with the top score, as a mask; Undef when all seven are equal (no weakness to speak of). Pure, so
        /// the tests can drive it - a live Creature cannot be built without the DATs. Inputs are in <see cref="ElementOrder"/>.
        /// Score = resist x weakness factor x the armor curve, the same parts combat multiplies, without the attacker's rend
        /// or any vuln on the monster, which would change the answer from one kill to the next.
        /// </summary>
        internal static DamageType WeakestElements(IReadOnlyList<double> resist, DamageType weakMask, double weakFactor, IReadOnlyList<double> armorVs, double baseArmor)
        {
            var scores = new double[Elements.Length];
            for (var i = 0; i < Elements.Length; i++)
            {
                var score = resist[i] * SkillFormula.CalcArmorMod((float)(baseArmor * armorVs[i]));
                if ((weakMask & Elements[i]) != 0 && weakFactor > 0)
                    score *= weakFactor;
                scores[i] = score;
            }

            var top = scores.Max();
            var mask = DamageType.Undef;
            var count = 0;
            for (var i = 0; i < Elements.Length; i++)
            {
                if (Math.Abs(scores[i] - top) < 1e-6)
                {
                    mask |= Elements[i];
                    count++;
                }
            }
            return count == Elements.Length ? DamageType.Undef : mask;
        }

        /// <summary>The order <see cref="WeakestElements"/> reads its inputs in.</summary>
        internal static IReadOnlyList<DamageType> ElementOrder => Elements;

        /// <summary>
        /// The element a rare should be stamped with, or null to leave it as rolled. Any Nether weapon stays Nether, not only
        /// a Void wand (owner 2026-10-30: the melee and missile ones too), plain bows and casters
        /// with no damage type are left alone (their element is not the weapon's to decide), and so is a weapon already on a
        /// weakest element. A Slash/Pierce weapon always ends on ONE type: left on two, a Pierce-weak monster could produce
        /// Slash Rending, which ZoneSlashRendDamageType then makes Slashing only. A tie takes the lowest bit, so it is
        /// deterministic.
        /// </summary>
        internal static DamageType? ChooseRareElement(DamageType current, DamageType weakMask)
        {
            if (weakMask == DamageType.Undef || current == DamageType.Undef || current == DamageType.Base || (current & DamageType.Nether) != 0)
                return null;

            var overlap = current & weakMask;
            if (overlap == current && IsSingle(current))
                return null;

            return LowestBit(overlap != 0 ? overlap : weakMask);
        }

        private static bool IsSingle(DamageType type) => ((uint)type & ((uint)type - 1)) == 0;

        private static DamageType LowestBit(DamageType mask) => (DamageType)((uint)mask & (~(uint)mask + 1));

        // -- the pool ------------------------------------------------------------------------------------

        /// <summary>A monster from a generator. The source is the top of the generator chain, because a nested generator's guid
        /// can change on a reload. The cap is the profile's own MaxCreate (or the generator's limit when that is infinite),
        /// times the limit of each generator above it.</summary>
        public static void RecordGenerated(Creature creature, WorldObject generator, int profileMaxCreate)
        {
            if (generator == null)
                return;

            var cap = (long)(profileMaxCreate > 0 ? profileMaxCreate : Math.Max(generator.MaxGeneratedObjects, 1));
            var root = generator;
            for (var depth = 0; root.Generator != null && depth < 8; depth++)
            {
                root = root.Generator;
                cap = Math.Min(cap * Math.Max(root.MaxGeneratedObjects, 1), MaxSourceCap);
            }
            Record(creature, root.Guid.Full, (int)cap);
        }

        /// <summary>A monster placed directly in the world: it counts once, however often its landblock reloads.</summary>
        public static void RecordPlaced(Creature creature) => Record(creature, creature?.Guid.Full ?? 0, 1);

        private static void Record(Creature creature, uint source, int cap)
        {
            if (creature == null || !Counts(creature.IsNPC, creature.Attackable, creature.CreatureType, creature.GetProperty(PropertyBool.OnlyCombatPetsCanDamage) == true))
                return;

            // the zone must govern it, the same answer loot and combat get: exempt monsters and generators and all pets are out
            var zone = ZoneControlManager.ZoneKeyFor(creature);
            if (zone == null || !Admit(zone, source, creature.WeenieClassId, cap))
                return;

            Add(zone, creature.CreatureType.Value);
        }

        /// <summary>Only real monsters count. Town NPCs carry Attackable false, which is what keeps them out.</summary>
        internal static bool Counts(bool isNpc, bool attackable, CreatureType? species, bool onlyCombatPetsCanDamage)
            => !isNpc && attackable && species != null && species != CreatureType.Invalid && !onlyCombatPetsCanDamage;

        /// <summary>True while this source has counted fewer of this WCID than its cap.</summary>
        internal static bool Admit(string zone, uint source, uint wcid, int cap)
            => counted.AddOrUpdate((zone, source, wcid), 1, (_, n) => n < cap ? n + 1 : cap + 1) <= cap;

        internal static void Add(string zone, CreatureType species)
        {
            var pool = pools.GetOrAdd(zone, _ => new ConcurrentDictionary<CreatureType, long>());
            pool.AddOrUpdate(species, 1, (_, n) => n + 1);
        }

        /// <summary>
        /// The species for a normal Slayer drop, or null to keep the killed species: the share roll missed, no zone, or the
        /// pool is still empty.
        /// </summary>
        public static CreatureType? PickFor(Creature killed, EvaluatedProfile profile, int tier)
        {
            if (profile == null || !UsesPool(profile.GetT(ZoneStat.SlayerPoolShare, 0.0, tier), () => ThreadSafeRandom.Next(0.0f, 1.0f)))
                return null;

            var zone = ZoneControlManager.ZoneKeyFor(killed);
            return zone == null ? null : Pick(zone, ThreadSafeRandom.Next(0.0f, 1.0f));
        }

        /// <summary>The share roll. Draws nothing when the share is 0 or full.</summary>
        internal static bool UsesPool(double share, Func<double> roll)
        {
            if (!double.IsFinite(share) || share <= 0.0)
                return false;
            return share >= 1.0 || roll() < share;
        }

        /// <summary>A species weighted by spawn count. <paramref name="roll"/> is 0..1.</summary>
        internal static CreatureType? Pick(string zone, double roll)
        {
            if (!pools.TryGetValue(zone, out var pool))
                return null;

            var picks = pool.ToArray();
            var total = picks.Sum(e => e.Value);
            if (total <= 0)
                return null;

            var target = Math.Clamp(roll, 0.0, 1.0) * total;
            foreach (var e in picks)
            {
                target -= e.Value;
                if (target < 0)
                    return e.Key;
            }
            return picks[^1].Key;
        }

        // -- dev -----------------------------------------------------------------------------------------

        /// <summary>Every zone's counts, largest first, for `/zcslayerpool`.</summary>
        internal static IReadOnlyList<(string Zone, Entry[] Entries)> Snapshot()
            => pools.OrderBy(z => z.Key, StringComparer.Ordinal)
                .Select(z => (z.Key, z.Value.ToArray().Select(e => new Entry(e.Key, e.Value)).OrderByDescending(e => e.Count).ToArray()))
                .ToList();

        internal static void Clear()
        {
            pools.Clear();
            counted.Clear();
        }
    }
}
