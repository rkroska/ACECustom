using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ACE.Server.Managers.ZoneControl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>The whole-part change log (ZoneControlManager.Sections.cs): what counts as a change, how it is described, who made it.</summary>
    [TestClass]
    public class ZoneSectionLogTests
    {
        private static ZoneControlManager.SectionState Part(string key, string json, string zone = null) => new ZoneControlManager.SectionState
        {
            Key = key,
            Label = zone != null ? "Zone settings" : key,
            Zone = zone,
            Json = json,
            Canonical = ZoneControlManager.Canonical(json),
        };

        private static Dictionary<string, ZoneControlManager.SectionState> Parts(params ZoneControlManager.SectionState[] parts)
            => parts.ToDictionary(p => p.Key, StringComparer.Ordinal);

        [TestMethod]
        public void Canonical_IgnoresKeyOrder_AndTheOrderOfNumberLists()
        {
            Assert.AreEqual(
                ZoneControlManager.Canonical("{\"B\":1,\"A\":{\"Landblocks\":[3,1,2]}}"),
                ZoneControlManager.Canonical("{\"A\":{\"Landblocks\":[1,2,3]},\"B\":1}"));
        }

        [TestMethod]
        public void Canonical_IgnoresTheCaseOfPropertyNames()
        {
            Assert.AreEqual(
                ZoneControlManager.Canonical("{\"Name\":\"A\",\"Profile\":{\"Minion\":{}}}"),
                ZoneControlManager.Canonical("{\n  \"name\": \"A\",\n  \"profile\": {\"minion\": {}}\n}"));
        }

        [TestMethod]
        public void Canonical_TreatsEmptyAndNullPropertiesAsMissing()
        {
            Assert.AreEqual(
                ZoneControlManager.Canonical("{\"Name\":\"A\"}"),
                ZoneControlManager.Canonical("{\"Name\":\"A\",\"Ranks\":{\"boss\":{\"Stats\":{},\"Props\":[]}},\"Notes\":null}"));
        }

        [TestMethod]
        public void Canonical_StillSeesAListEmptied()
        {
            Assert.AreNotEqual(
                ZoneControlManager.Canonical("{\"Landblocks\":[5]}"),
                ZoneControlManager.Canonical("{\"Landblocks\":[]}"));
            Assert.AreNotEqual(ZoneControlManager.Canonical("[5]"), ZoneControlManager.Canonical("[]"));
            Assert.AreNotEqual(ZoneControlManager.Canonical("{\"Enabled\":false}"), ZoneControlManager.Canonical("{}"));
        }

        [TestMethod]
        public void Canonical_KeepsTheOrderOfObjectLists()
        {
            Assert.AreNotEqual(
                ZoneControlManager.Canonical("[{\"Wcid\":1},{\"Wcid\":2}]"),
                ZoneControlManager.Canonical("[{\"Wcid\":2},{\"Wcid\":1}]"));
        }

        [TestMethod]
        public void Diff_ListsOnlyChangedParts_WithTheirWholeJson()
        {
            var before = Parts(Part("zone:a", "{\"Name\":\"A\",\"Enabled\":true}", "A"), Part("zone:b", "{\"Name\":\"B\"}", "B"), Part("gearlock", "[]"));
            var after = Parts(Part("zone:a", "{\"Enabled\":false,\"Name\":\"A\"}", "A"), Part("zone:b", "{\"Name\":\"B\"}", "B"), Part("gearlock", "[]"));

            var changes = ZoneControlManager.DiffSections(before, after);

            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual("zone:a", changes[0].Section);
            Assert.AreEqual("A", changes[0].Zone);
            StringAssert.Contains(changes[0].OldJson, "true");
            StringAssert.Contains(changes[0].NewJson, "false");
        }

        [TestMethod]
        public void Diff_SeesCreateDeleteAndRename()
        {
            var before = Parts(Part("zone:old", "{\"Name\":\"Old\"}", "Old"));
            var after = Parts(Part("zone:new", "{\"Name\":\"New\"}", "New"));

            var changes = ZoneControlManager.DiffSections(before, after);

            Assert.AreEqual(2, changes.Count);
            Assert.IsNull(changes.Single(c => c.Section == "zone:new").OldJson, "created");
            Assert.IsNull(changes.Single(c => c.Section == "zone:old").NewJson, "removed");
        }

        // A stat edit logs its own row, so the save it triggers skips the part it touched - and only that part.
        [TestMethod]
        public void Diff_SkipsThePartAStatEditLogsItself()
        {
            var before = Parts(Part("zone:a", "{\"x\":1}", "A"), Part("gearlock", "[]"));
            var after = Parts(Part("zone:a", "{\"x\":2}", "A"), Part("gearlock", "[5]"));

            var changes = ZoneControlManager.DiffSections(before, after, skip: "zone:a");

            Assert.AreEqual("gearlock", changes.Single().Section);
        }

        [TestMethod]
        public void Describe_NamesEachChangedField()
        {
            var lines = ZoneControlManager.DescribeSectionChange(
                "{\"Enabled\":true,\"Profile\":{\"Minion\":{\"StatToggles\":{}}}}",
                "{\"Enabled\":false,\"Profile\":{\"Minion\":{\"StatToggles\":{\"max_health\":false}}}}");

            CollectionAssert.Contains(lines, "Enabled: true -> false");
            CollectionAssert.Contains(lines, "Profile.Minion.StatToggles.max_health: (none) -> false");
        }

        [TestMethod]
        public void Describe_AWholeList_NamesItValue()
        {
            Assert.AreEqual("value: [] -> [131072]", ZoneControlManager.DescribeSectionChange("[]", "[131072]").Single());
        }

        [TestMethod]
        public void Describe_CreatedRemovedAndCapped()
        {
            Assert.AreEqual("Created.", ZoneControlManager.DescribeSectionChange(null, "{}").Single());
            Assert.AreEqual("Removed.", ZoneControlManager.DescribeSectionChange("{}", null).Single());

            var many = ZoneControlManager.DescribeSectionChange("{}", "{" + string.Join(",", Enumerable.Range(0, 20).Select(i => $"\"k{i:00}\":{i}")) + "}", max: 5);
            Assert.AreEqual(6, many.Count);
            Assert.AreEqual("... and 15 more", many.Last());
        }

        [TestMethod]
        public void ActingAs_SetsTheActor_AndPutsTheOldOneBack()
        {
            Assert.AreEqual("server", ZoneControlChangeLog.CurrentActor.Name);
            using (ZoneControlChangeLog.ActingAs(new ZoneStatActor { Name = "Alice", Source = ZoneStatActor.SourceGame }))
            {
                Assert.AreEqual("Alice", ZoneControlChangeLog.CurrentActor.Name);
                using (ZoneControlChangeLog.ActingAs(new ZoneStatActor { Name = "Bob", Source = ZoneStatActor.SourcePortal }))
                    Assert.AreEqual("Bob", ZoneControlChangeLog.CurrentActor.Name);
                Assert.AreEqual("Alice", ZoneControlChangeLog.CurrentActor.Name);
            }
            Assert.AreEqual("server", ZoneControlChangeLog.CurrentActor.Name);
        }

        // A portal request and a game command run on different threads: one's actor must never leak into the other's saves.
        [TestMethod]
        public async Task ActingAs_DoesNotLeakIntoOtherWork()
        {
            string seen = null;
            using (ZoneControlChangeLog.ActingAs(new ZoneStatActor { Name = "Alice", Source = ZoneStatActor.SourceGame }))
            {
                await Task.Factory.StartNew(() => { }, TaskCreationOptions.LongRunning);
            }
            await Task.Run(() => seen = ZoneControlChangeLog.CurrentActor.Name);
            Assert.AreEqual("server", seen);
        }

        [TestMethod]
        public void StatEditOn_ClearsTheSkipAfterward()
        {
            using (ZoneControlChangeLog.StatEditOn(null))
            {
                ZoneControlChangeLog.SetSkipSection("zone:a");
                Assert.AreEqual("zone:a", ZoneControlChangeLog.SkipSection);
            }
            Assert.IsNull(ZoneControlChangeLog.SkipSection);
        }

        [TestMethod]
        public void SectionKeys_AreCaseInsensitiveForZones()
        {
            Assert.AreEqual(ZoneControlManager.ZoneSectionKey("Tou Tou"), ZoneControlManager.ZoneSectionKey("TOU TOU"));
            Assert.AreEqual("default:14", ZoneControlManager.DefaultSectionKey(14));
        }
    }
}
