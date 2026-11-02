using System.Collections.Generic;
using System.Linq;
using ACE.Server.WorldObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    [TestClass]
    public class PandemicSlotTests
    {
        // One DoT's tick against the rule, nearest first: returns who it reaches and grows the list as copies land.
        private static List<string> Spread(HashSet<string> list, string[] nearestFirst, int cap, int maxTargets)
        {
            var room = cap - list.Count;
            var reached = new List<string>();
            var newMonsters = 0;
            foreach (var monster in nearestFirst)
            {
                if (reached.Count >= maxTargets)
                    break;
                var holdsSlot = list.Contains(monster);
                if (!Creature.ZcPandemicMaySend(holdsSlot, newMonsters, room))
                    continue;
                reached.Add(monster);
                if (!holdsSlot)
                    newMonsters++;
            }
            foreach (var monster in reached)
                list.Add(monster);
            return reached;
        }

        [TestMethod]
        public void TwoSlotsThreeMonstersAllThreeDotsReachBothOthers()
        {
            var list = new HashSet<string>();
            var others = new[] { "B", "C" };
            foreach (var dot in new[] { "Corrosion", "Corruption", "Curse" })
                CollectionAssert.AreEqual(others, Spread(list, others, 2, 2), dot);
            Assert.AreEqual(2, list.Count);
        }

        [TestMethod]
        public void FullSlotsStillBlockANewMonster()
        {
            var list = new HashSet<string>();
            var nearestFirst = new[] { "B", "C", "D" };
            CollectionAssert.AreEqual(new[] { "B", "C" }, Spread(list, nearestFirst, 2, 2));
            // D is nearest for the next DoT, and it still stays out while B and C hold both slots
            CollectionAssert.AreEqual(new[] { "B", "C" }, Spread(list, new[] { "D", "B", "C" }, 2, 2));
            Assert.IsFalse(list.Contains("D"));
        }

        [TestMethod]
        public void AnOpenSlotGoesToOneNewMonsterOnly()
        {
            var list = new HashSet<string> { "C" };
            var reached = Spread(list, new[] { "D", "E", "C" }, 2, 3);
            CollectionAssert.AreEqual(new[] { "D", "C" }, reached);
            Assert.AreEqual(2, list.Count);
        }

        [TestMethod]
        public void NoSlotAndNotOnTheListIsRefused()
        {
            Assert.IsFalse(Creature.ZcPandemicMaySend(false, 0, 0));
            Assert.IsFalse(Creature.ZcPandemicMaySend(false, 1, 1));
            Assert.IsTrue(Creature.ZcPandemicMaySend(false, 0, 1));
            Assert.IsTrue(Creature.ZcPandemicMaySend(true, 5, 0));
            Assert.IsTrue(Creature.ZcPandemicMaySend(false, 500, int.MaxValue));
        }
    }
}
