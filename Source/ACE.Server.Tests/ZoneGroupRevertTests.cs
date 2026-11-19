using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ACE.Server.Tests
{
    /// <summary>History groups and the Revert all plan (ZoneControlRevert), which is worked out on a copy of the store.</summary>
    [TestClass]
    public class ZoneGroupRevertTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 11, 14, 0, 0, DateTimeKind.Utc);

        private static string Curve(double? v) => v == null ? null : ZoneControlChangeLog.WriteCurve(new StatCurve { Base = v.Value });

        private static ZoneControlChange Stat(long id, double? from, double? to, string stat = "max_health", string actor = "Alice", int minutes = 0, string batch = null, string zone = "Tou Tou") => new ZoneControlChange
        {
            Id = id,
            ChangedAtUtc = T0.AddMinutes(minutes),
            Actor = actor,
            Source = ZoneStatActor.SourcePortal,
            Zone = zone,
            Rank = "default",
            Stat = stat,
            Kind = ZoneControlChangeLog.KindStat,
            OldCurveJson = Curve(from),
            NewCurveJson = Curve(to),
            BatchId = batch,
        };

        private static ZoneControlChange Part(long id, string from, string to, string section = "zone:tou tou", int minutes = 0) => new ZoneControlChange
        {
            Id = id,
            ChangedAtUtc = T0.AddMinutes(minutes),
            Actor = "Alice",
            Source = ZoneStatActor.SourceGame,
            Zone = section.StartsWith("zone:") ? "Tou Tou" : null,
            Rank = "-",
            Stat = "Zone settings",
            Kind = ZoneControlChangeLog.KindSection,
            Section = section,
            OldJson = from,
            NewJson = to,
        };

        /// <summary>A zone as the store writes it, with one stat on its Default row.</summary>
        private static string Zone(bool enabled, double? maxHealth, string name = "Tou Tou")
        {
            var stats = new JObject();
            if (maxHealth != null)
                stats["max_health"] = JObject.FromObject(new StatCurve { Base = maxHealth.Value });
            return new JObject
            {
                ["Name"] = name,
                ["Enabled"] = enabled,
                ["Profile"] = new JObject { ["Minion"] = new JObject { ["Stats"] = stats }, ["WcidOverrides"] = new JObject() },
            }.ToString(Formatting.None);
        }

        private static JObject Store(params string[] zones) => new JObject
        {
            ["Areas"] = new JArray(zones.Select(JObject.Parse)),
            ["VariationDefaults"] = new JObject(),
            ["GearLockSpots"] = new JArray(),
        };

        private static List<ZoneControlChange> NewestFirst(params ZoneControlChange[] rows) => rows.OrderByDescending(r => r.Id).ToList();

        private static long[] Ids(IEnumerable<ZoneControlChange> rows) => rows.Select(c => c.Id).ToArray();

        // ---- groups

        [TestMethod]
        public void Group_JoinsOnePersonsEditsWithinTheGap()
        {
            var groups = ZoneControlRevert.Group(NewestFirst(Stat(1, 1, 2, minutes: 0), Stat(2, 2, 3, minutes: 3), Stat(3, 3, 4, minutes: 20)));

            Assert.AreEqual(2, groups.Count);
            CollectionAssert.AreEqual(new long[] { 3 }, Ids(groups[0].Changes));
            CollectionAssert.AreEqual(new long[] { 2, 1 }, Ids(groups[1].Changes));
            Assert.AreEqual(T0, groups[1].FromUtc);
            Assert.AreEqual(T0.AddMinutes(3), groups[1].ToUtc);
        }

        [TestMethod]
        public void Group_SplitsOnAnotherPerson()
        {
            var groups = ZoneControlRevert.Group(NewestFirst(Stat(1, 1, 2), Stat(2, 2, 3, actor: "Bob", minutes: 1), Stat(3, 3, 4, minutes: 2)));
            Assert.AreEqual(3, groups.Count);
        }

        [TestMethod]
        public void Group_KeepsOneSaveTogether_HoweverLongApart()
        {
            var groups = ZoneControlRevert.Group(NewestFirst(Stat(1, 1, 2, minutes: 0, batch: "b"), Stat(2, 2, 3, minutes: 30, batch: "b")));
            Assert.AreEqual(1, groups.Count);
        }

        // a save's batch (an import) never shares a group with a separate edit made a minute later
        [TestMethod]
        public void Group_KeepsABatchApartFromNearbyUnbatchedEdits()
        {
            var groups = ZoneControlRevert.Group(NewestFirst(Stat(1, 1, 2, minutes: 0, batch: "import"), Stat(2, 2, 3, minutes: 0, batch: "import"), Stat(3, 3, 4, minutes: 1)));

            Assert.AreEqual(2, groups.Count);
            CollectionAssert.AreEqual(new long[] { 3 }, Ids(groups[0].Changes));
            CollectionAssert.AreEqual(new long[] { 2, 1 }, Ids(groups[1].Changes));
        }

        // ---- plan

        // a zone put back while it still exists keeps its place in the list (the order breaks landblock ties)
        [TestMethod]
        public void ApplySection_PutsAZoneBackInPlace()
        {
            var root = Store(Zone(false, 3, "A"), Zone(true, 1, "B"));
            var change = Part(1, Zone(true, 3, "A"), Zone(false, 3, "A"), section: "zone:a");

            Assert.IsNull(ZoneControlManager.ApplySectionToRoot(root, change));

            Assert.AreEqual("A", (string)root["Areas"][0]["Name"]);
            Assert.AreEqual(true, (bool)root["Areas"][0]["Enabled"]);
            Assert.AreEqual("B", (string)root["Areas"][1]["Name"]);
        }

        // undoing the stat that recreated a removed Default leaves the copy without that Default, as the live store would, so the
        // older "Default removed" change still matches and is reverted too
        [TestMethod]
        public void Plan_PrunesADefaultLeftEmpty_SoItsRemovalStillReverts()
        {
            var t14 = new JObject { ["Profile"] = new JObject { ["Stats"] = new JObject { ["max_health"] = JObject.FromObject(new StatCurve { Base = 5 }) } } };
            var root = Store();
            root["VariationDefaults"]["14"] = t14.DeepClone();
            var oldDefault = new JObject { ["Notes"] = "kept", ["Profile"] = new JObject() }.ToString(Formatting.None);

            var removed = Part(1, oldDefault, null, section: "default:14");
            removed.Zone = null;
            removed.Variation = 14;
            var recreated = Stat(2, null, 5, zone: null);
            recreated.Variation = 14;

            var plan = ZoneControlRevert.Plan(NewestFirst(removed, recreated), root);

            CollectionAssert.AreEqual(new long[] { 2, 1 }, Ids(plan.Revert), string.Join("; ", plan.Skipped.Select(s => s.Why)));
            Assert.AreEqual("kept", (string)root["VariationDefaults"]["14"]["Notes"]);
        }

        // a rename (zone A removed, zone B created, one save) goes back as one unit, or not at all
        [TestMethod]
        public void Plan_RevertsARenameTogether()
        {
            var root = Store(Zone(true, 3, "B"));
            var gone = Part(1, Zone(true, 3, "A"), null, section: "zone:a");
            var made = Part(2, null, Zone(true, 3, "B"), section: "zone:b");
            gone.BatchId = made.BatchId = "rename";

            var plan = ZoneControlRevert.Plan(NewestFirst(gone, made), root);

            CollectionAssert.AreEquivalent(new long[] { 1, 2 }, Ids(plan.Revert));
            Assert.AreEqual(1, plan.Units.Count);
            Assert.AreEqual("A", (string)((JArray)root["Areas"]).Single()["Name"]);
        }

        [TestMethod]
        public void Plan_LeavesARenameAlone_WhenTheNewZoneWasEditedSince()
        {
            var root = Store(Zone(false, 3, "B"));   // B was disabled after the rename
            var gone = Part(1, Zone(true, 3, "A"), null, section: "zone:a");
            var made = Part(2, null, Zone(true, 3, "B"), section: "zone:b");
            gone.BatchId = made.BatchId = "rename";

            var plan = ZoneControlRevert.Plan(NewestFirst(gone, made), root);

            Assert.AreEqual(0, plan.Revert.Count);
            CollectionAssert.AreEquivalent(new long[] { 1, 2 }, plan.Skipped.Select(s => s.Change.Id).ToArray());
            Assert.AreEqual("B", (string)((JArray)root["Areas"]).Single()["Name"], "nothing changed in the copy");
        }

        [TestMethod]
        public void Plan_RevertsEverything_NewestFirst_WhenNothingMovedOn()
        {
            var root = Store(Zone(true, 3));
            var plan = ZoneControlRevert.Plan(NewestFirst(Stat(1, 1, 2), Stat(2, 2, 3)), root);

            CollectionAssert.AreEqual(new long[] { 2, 1 }, Ids(plan.Revert));
            Assert.AreEqual(0, plan.Skipped.Count);
            Assert.AreEqual(1.0, (double)root["Areas"][0]["Profile"]["Minion"]["Stats"]["max_health"]["Base"], "the copy ends at the group's starting value");
        }

        [TestMethod]
        public void Plan_RevertOfASet_ClearsAStatThatWasNotSet()
        {
            var root = Store(Zone(true, 5));
            var plan = ZoneControlRevert.Plan(NewestFirst(Stat(1, null, 5)), root);

            Assert.AreEqual(1, plan.Revert.Count);
            Assert.IsNull(root["Areas"][0]["Profile"]["Minion"]["Stats"]["max_health"]);
        }

        [TestMethod]
        public void Plan_SkipsAStatChangedAfterTheGroup()
        {
            var plan = ZoneControlRevert.Plan(NewestFirst(Stat(1, 1, 2), Stat(2, 2, 3)), Store(Zone(true, 9)));

            Assert.AreEqual(0, plan.Revert.Count);
            StringAssert.Contains(plan.Skipped.Single(s => s.Change.Id == 2).Why, "after this group");
            StringAssert.Contains(plan.Skipped.Single(s => s.Change.Id == 1).Why, "in between");
        }

        // Alice 1->2, someone else 2->7 (not in the group), Alice 7->8: undo 8->7 is fine, but 2->1 would wipe the other edit.
        [TestMethod]
        public void Plan_StopsAKeyAtAGapFilledBySomeoneElse()
        {
            var plan = ZoneControlRevert.Plan(NewestFirst(Stat(1, 1, 2), Stat(3, 7, 8)), Store(Zone(true, 8)));

            CollectionAssert.AreEqual(new long[] { 3 }, Ids(plan.Revert));
            StringAssert.Contains(plan.Skipped.Single().Why, "in between");
        }

        // The case the first planner got wrong (review 2026-11-11): a whole-zone change, then a stat edit on the same zone.
        // Undoing the stat first puts the zone back to what the zone change left, so the zone change then undoes too.
        [TestMethod]
        public void Plan_UndoesAZoneChangeFollowedByAStatEdit()
        {
            var disable = Part(1, Zone(true, 2), Zone(false, 2));
            var statEdit = Stat(2, 2, 3);
            var root = Store(Zone(false, 3));

            var plan = ZoneControlRevert.Plan(NewestFirst(disable, statEdit), root);

            CollectionAssert.AreEqual(new long[] { 2, 1 }, Ids(plan.Revert));
            Assert.AreEqual(0, plan.Skipped.Count);
            Assert.AreEqual(ZoneControlManager.Canonical(Zone(true, 2)), ZoneControlManager.Canonical(root["Areas"][0].ToString(Formatting.None)));
        }

        [TestMethod]
        public void Plan_ChainsWholePartChanges()
        {
            var plan = ZoneControlRevert.Plan(NewestFirst(Part(1, Zone(true, 1), Zone(false, 1)), Part(2, Zone(false, 1), Zone(false, 4))), Store(Zone(false, 4)));
            CollectionAssert.AreEqual(new long[] { 2, 1 }, Ids(plan.Revert));
        }

        [TestMethod]
        public void Plan_SkipsAStatOnAZoneThatIsGone()
        {
            var plan = ZoneControlRevert.Plan(NewestFirst(Stat(1, 5, null)), Store());
            StringAssert.Contains(plan.Skipped.Single().Why, "no longer exists");
        }

        // A rename saved as one batch: old zone removed, new zone created. Undoing both brings the old name back.
        [TestMethod]
        public void Plan_UndoesARenameAsAPair()
        {
            var removed = Part(1, Zone(true, 2, "Old"), null, section: "zone:old");
            var created = Part(2, null, Zone(true, 2, "New"), section: "zone:new");
            var root = Store(Zone(true, 2, "New"));

            var plan = ZoneControlRevert.Plan(NewestFirst(removed, created), root);

            Assert.AreEqual(2, plan.Revert.Count);
            Assert.AreEqual("Old", (string)root["Areas"].Single()["Name"]);
        }

        // A mod (CustomClothingBase) can switch Newtonsoft to camelCase, so a live store reads "areas"/"name"/"profile" (found 2026-11-11).
        private static JObject CamelCase(JObject root)
        {
            static JToken Lower(JToken t) => t switch
            {
                JObject o => new JObject(o.Properties().Select(p => new JProperty(char.IsUpper(p.Name[0]) ? char.ToLowerInvariant(p.Name[0]) + p.Name.Substring(1) : p.Name, Lower(p.Value)))),
                JArray a => new JArray(a.Select(Lower)),
                _ => t.DeepClone(),
            };
            return (JObject)Lower(root);
        }

        [TestMethod]
        public void Plan_WorksOnACamelCaseStore()
        {
            var root = CamelCase(Store(Zone(false, 3)));
            var disable = Part(1, Zone(true, 2), Zone(false, 2));   // logged in PascalCase, store now camelCase: still the same part

            var plan = ZoneControlRevert.Plan(NewestFirst(disable, Stat(2, 2, 3)), root);

            CollectionAssert.AreEqual(new long[] { 2, 1 }, Ids(plan.Revert));
            Assert.AreEqual(1, root.Properties().Count(p => string.Equals(p.Name, "areas", StringComparison.OrdinalIgnoreCase)), "no second Areas key");
            Assert.AreEqual(1, ((JArray)root["areas"]).Count);
        }

        [TestMethod]
        public void ApplySection_KeepsOneKeyForAGlobalList_OnACamelCaseStore()
        {
            var root = CamelCase(Store(Zone(true, 1)));
            root["gearLockSpots"] = new JArray(5);
            var change = Part(1, "[]", "[5]", section: "gearlock");

            Assert.IsNull(ZoneControlManager.ApplySectionToRoot(root, change));

            Assert.AreEqual(1, root.Properties().Count(p => string.Equals(p.Name, "GearLockSpots", StringComparison.OrdinalIgnoreCase)));
            Assert.AreEqual(0, ((JArray)root["gearLockSpots"]).Count);
        }

        // Review 2026-11-12: a stat set on a NEW Boss row, then reverted, leaves an empty Boss row behind. That empty row must not
        // make the zone count as changed, or the older whole-zone change could never be reverted.
        [TestMethod]
        public void Plan_AnEmptyRowLeftByARevert_DoesNotBlockAnOlderZoneChange()
        {
            var disable = Part(1, Zone(true, 2), Zone(false, 2));
            var root = Store(Zone(false, 2));
            ((JObject)root["Areas"][0]["Profile"]["Minion"])["Ranks"] = new JObject
            {
                ["boss"] = new JObject { ["Stats"] = new JObject { ["max_health"] = JObject.FromObject(new StatCurve { Base = 9 }) } },
            };
            var bossSet = Stat(2, null, 9);
            bossSet.Rank = "boss";

            var plan = ZoneControlRevert.Plan(NewestFirst(disable, bossSet), root);

            CollectionAssert.AreEqual(new long[] { 2, 1 }, Ids(plan.Revert));
            Assert.AreEqual(0, plan.Skipped.Count);
        }

        // A refused zone revert must leave the shared plan copy exactly as it was (it used to remove the zone first).
        [TestMethod]
        public void ApplySection_ARefusal_LeavesTheCopyAlone()
        {
            var root = Store(Zone(true, 1, "A"), Zone(true, 1, "B"));
            var before = root.ToString(Formatting.None);
            var change = Part(1, Zone(true, 1, "B"), Zone(true, 1, "A"), section: "zone:a");

            StringAssert.Contains(ZoneControlManager.ApplySectionToRoot(root, change), "Another zone");
            Assert.AreEqual(before, root.ToString(Formatting.None));
        }

        [TestMethod]
        public void Plan_NeverRevertsLadderVersions()
        {
            var plan = ZoneControlRevert.Plan(NewestFirst(Part(1, "{}", "{\"14\":1}", section: ZoneControlManager.SectionLadder), Stat(2, 1, 2)), Store(Zone(true, 2)));

            CollectionAssert.AreEqual(new long[] { 2 }, Ids(plan.Revert));
            StringAssert.Contains(plan.Skipped.Single().Why, "Ladder");
        }

        [TestMethod]
        public void Plan_TreatsRankRowsAsDifferentStats()
        {
            var root = Store(Zone(true, 2));
            ((JObject)root["Areas"][0]["Profile"]["Minion"])["Ranks"] = new JObject
            {
                ["boss"] = new JObject { ["Stats"] = new JObject { ["max_health"] = JObject.FromObject(new StatCurve { Base = 6 }) } },
            };
            var boss = Stat(2, 5, 6);
            boss.Rank = "boss";

            var plan = ZoneControlRevert.Plan(NewestFirst(Stat(1, 1, 2), boss), root);

            Assert.AreEqual(2, plan.Revert.Count, "a Boss row edit does not break the Default row's chain");
            Assert.AreEqual(5.0, (double)root["Areas"][0]["Profile"]["Minion"]["Ranks"]["boss"]["Stats"]["max_health"]["Base"]);
        }
    }
}
