using System.Linq;
using System.Reflection;

using ACE.Server.Managers.ZoneControl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>The portal's Bounties tab (ZoneControlController.Bounties.cs, BountyEdits) and the change log that covers it.</summary>
    [TestClass]
    public class ZoneBountyEditTests
    {
        // The portal sends typed fields; BountyManager.Edit reads the command's argument list. The two must line up exactly.
        [TestMethod]
        public void EditArgs_PutsTheFieldsWhereTheCommandReadsThem()
        {
            CollectionAssert.AreEqual(new[] { "3", "100", "2", "50", "7.5", "qb=10" },
                BountyEdits.EditArgs("set", 3, 100, 2, 50, 7.5, 10));
            CollectionAssert.AreEqual(new[] { "100", "2", "50", "5" },
                BountyEdits.EditArgs("add", null, 100, 2, 50, 5, null));
            CollectionAssert.AreEqual(new[] { "4" }, BountyEdits.EditArgs("remove", 4, 1, 1, 1, 1, 1));
            Assert.AreEqual(0, BountyEdits.EditArgs("on", 4, 1, 1, 1, 1, 1).Count);
        }

        [TestMethod]
        public void CheckRewardRow_RefusesBadNumbersBeforeLookingTheItemUp()
        {
            StringAssert.Contains(BountyEdits.CheckRewardRow(0, 1, 1), "WCID");
            StringAssert.Contains(BountyEdits.CheckRewardRow(5, 0, 1), "amount");
            StringAssert.Contains(BountyEdits.CheckRewardRow(5, 1, 0), "weight");
            StringAssert.Contains(BountyEdits.CheckRewardRow(5, 1, BountyRewardTable.MaxWeight + 1), "weight");
        }

        // Every part of the store must be logged, or an edit to it (from the portal or a command) leaves no row to revert.
        // Zones and tier Defaults are logged one by one; every other store property must be a global part.
        [TestMethod]
        public void ChangeLog_CoversEveryPartOfTheStore()
        {
            var store = typeof(ZoneControlManager).GetNestedType("Store", BindingFlags.NonPublic);
            Assert.IsNotNull(store, "ZoneControlManager.Store moved: update this test");
            var logged = ZoneControlManager.GlobalSections.Select(g => g.Property).ToHashSet();
            var missing = store.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .Where(n => n != "Areas" && n != "VariationDefaults" && !logged.Contains(n))
                .ToList();
            Assert.AreEqual(0, missing.Count, "Store parts the change log does not cover: " + string.Join(", ", missing));
        }
    }
}
