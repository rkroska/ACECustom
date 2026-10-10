using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Models;
using ACE.Server.Entity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The clone zones' drop rule (CloneZones.ReduceDrops): a rolled corpse drop of a listed item is kept with a chance
    /// equal to the area's reward multiplier, guaranteed drops included; nothing else is touched and nothing is added.
    /// </summary>
    [TestClass]
    public class CloneZonesTests
    {
        private const uint Core = 30010112, Necro = 300101186, Pelt = 290500318;

        private static List<PropertiesCreateList> Drops(params uint[] wcids)
            => wcids.Select(w => new PropertiesCreateList { DestinationType = ACE.Entity.Enum.DestinationType.ContainTreasure, WeenieClassId = w, StackSize = 1, Palette = 0, Shade = 1f, TryToBond = false }).ToList();

        private static readonly HashSet<uint> Listed = new HashSet<uint> { Core, Necro };

        [TestMethod]
        public void ListedDrops_AreKeptOnlyWhenTheRollIsBelowTheChance()
        {
            var rolls = new Queue<double>(new[] { 0.10, 0.25, 0.90 });   // core kept, necro lost (0.25 is not below 0.25), core lost
            var kept = CloneZones.ReduceDrops(Drops(Core, Pelt, Necro, Core), 0.25, Listed, () => rolls.Dequeue());

            CollectionAssert.AreEqual(new[] { Core, Pelt }, kept.Select(k => k.WeenieClassId).ToArray());
            Assert.AreEqual(0, rolls.Count, "one roll per listed drop, none for the others");
        }

        [TestMethod]
        public void OtherDrops_NeverRollAndAlwaysStay()
        {
            var kept = CloneZones.ReduceDrops(Drops(Pelt, Pelt), 0.0, Listed, () => { Assert.Fail("an unlisted drop must not roll"); return 0; });
            Assert.AreEqual(2, kept.Count);
        }

        [TestMethod]
        public void ChanceOfZero_RemovesEveryListedDrop()
        {
            var kept = CloneZones.ReduceDrops(Drops(Core, Necro, Pelt), 0.0, Listed, () => 0.0);
            CollectionAssert.AreEqual(new[] { Pelt }, kept.Select(k => k.WeenieClassId).ToArray());
        }

        [TestMethod]
        public void ChanceOfOneOrMore_ChangesNothing()
        {
            var drops = Drops(Core, Necro);
            Assert.AreSame(drops, CloneZones.ReduceDrops(drops, 1.0, Listed, () => 0.99));
            Assert.AreSame(drops, CloneZones.ReduceDrops(drops, 2.5, Listed, () => 0.99));
        }

        [TestMethod]
        public void EmptyList_ChangesNothing()
        {
            var drops = Drops(Core);
            Assert.AreSame(drops, CloneZones.ReduceDrops(drops, 0.25, new HashSet<uint>(), () => 0.99));
            Assert.IsNull(CloneZones.ReduceDrops(null, 0.25, Listed, () => 0.0));
        }

        [TestMethod]
        public void OverManyKills_AQuarterOfTheGuaranteedDropsRemain()
        {
            var rng = new System.Random(12345);
            int kept = 0;
            for (int i = 0; i < 20000; i++)
                kept += CloneZones.ReduceDrops(Drops(Core, Core, Core), 0.25, Listed, rng.NextDouble).Count;

            // 60,000 guaranteed drops at a chance of 0.25: 15,000 expected
            Assert.IsTrue(kept > 14400 && kept < 15600, "kept " + kept);
        }

        [TestMethod]
        public void WcidList_TakesCommasSemicolonsAndSpaces_AndLeavesOutJunkAndZero()
        {
            var parsed = CloneZones.ParseWcids(" 30010112, 300101186;290500318  0 abc -5 ");
            Assert.IsTrue(parsed.SetEquals(new[] { Core, Necro, Pelt }));
            Assert.AreEqual(0, CloneZones.ParseWcids(null).Count);
            Assert.AreEqual(0, CloneZones.ParseWcids("").Count);
        }
    }
}
