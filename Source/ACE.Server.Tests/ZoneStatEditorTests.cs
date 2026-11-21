using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;
using ACE.Server.Web.Controllers;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>The rules every stat editor shares (chat command and portal page), minus the store and the database.</summary>
    [TestClass]
    public class ZoneStatEditorTests
    {
        private static ZoneStatEditResult Apply(ZoneVariantProfile row, string stat, StatCurve next, ZoneStatExpectation expected = null)
            => ZoneStatEditor.ApplyToRow(_ => row, stat, next, expected);

        [DataTestMethod]
        [DataRow("151,200", 151200d)]
        [DataRow("0.5", 0.5d)]
        [DataRow("1e3", 1000d)]
        [DataRow(" 12 ", 12d)]
        public void ParseValue_Accepts(string text, double expected)
        {
            Assert.IsTrue(ZoneStatEditor.TryParseValue(text, out var value));
            Assert.AreEqual(expected, value);
        }

        [DataTestMethod]
        [DataRow("0,5")]
        [DataRow("0,500")]
        [DataRow("NaN")]
        [DataRow("Infinity")]
        [DataRow("$5")]
        [DataRow("")]
        [DataRow(null)]
        public void ParseValue_Refuses(string text) => Assert.IsFalse(ZoneStatEditor.TryParseValue(text, out _));

        [TestMethod]
        public void Set_WritesAndReportsOld()
        {
            var row = new ZoneVariantProfile();
            row.Stats["max_health"] = new StatCurve { Base = 100 };

            var result = Apply(row, "max_health", new StatCurve { Base = 200 });

            Assert.AreEqual(ZoneStatEditStatus.Applied, result.Status);
            Assert.AreEqual(100, result.Old.Base);
            Assert.AreEqual(200, row.Stats["max_health"].Base);
        }

        [TestMethod]
        public void StaleExpectation_ChangesNothing()
        {
            var row = new ZoneVariantProfile();
            row.Stats["max_health"] = new StatCurve { Base = 150 };

            var result = Apply(row, "max_health", new StatCurve { Base = 200 }, new ZoneStatExpectation { Base = 100 });

            Assert.AreEqual(ZoneStatEditStatus.Conflict, result.Status);
            Assert.AreEqual(150, row.Stats["max_health"].Base);
        }

        [TestMethod]
        public void ExpectingUnset_ConflictsWithAValueAddedSince()
        {
            var row = new ZoneVariantProfile();
            row.Stats["max_health"] = new StatCurve { Base = 150 };

            Assert.AreEqual(ZoneStatEditStatus.Conflict, Apply(row, "max_health", new StatCurve { Base = 200 }, new ZoneStatExpectation()).Status);
        }

        // A revert expects the change's whole new curve: a growth or tier-pin edit made since, with the same Base, must block it.
        [TestMethod]
        public void RevertExpectation_ConflictsWhenOnlyGrowthChanged()
        {
            var row = new ZoneVariantProfile();
            row.Stats["max_health"] = new StatCurve { Base = 150, Growth = 1.2 };

            var result = Apply(row, "max_health", new StatCurve { Base = 100 }, ZoneStatExpectation.Exactly(new StatCurve { Base = 150, Growth = 1.0 }));

            Assert.AreEqual(ZoneStatEditStatus.Conflict, result.Status);
            StringAssert.Contains(result.Message, "growth");
            Assert.AreEqual(1.2, row.Stats["max_health"].Growth);
        }

        [TestMethod]
        public void RevertExpectation_ConflictsWhenATierPinWasAdded()
        {
            var row = new ZoneVariantProfile();
            row.Stats["max_health"] = new StatCurve { Base = 150, Overrides = new Dictionary<int, double> { [14] = 900 } };

            var result = Apply(row, "max_health", new StatCurve { Base = 100 }, ZoneStatExpectation.Exactly(new StatCurve { Base = 150 }));

            Assert.AreEqual(ZoneStatEditStatus.Conflict, result.Status);
            Assert.AreEqual(150, row.Stats["max_health"].Base);
        }

        [TestMethod]
        public void RevertExpectation_AppliesWhenTheCurveIsUnchanged()
        {
            var row = new ZoneVariantProfile();
            row.Stats["max_health"] = new StatCurve { Base = 150, Growth = 1.2, Overrides = new Dictionary<int, double> { [14] = 900 } };
            var expected = ZoneStatExpectation.Exactly(new StatCurve { Base = 150, Growth = 1.2, Overrides = new Dictionary<int, double> { [14] = 900 } });

            Assert.AreEqual(ZoneStatEditStatus.Applied, Apply(row, "max_health", new StatCurve { Base = 100 }, expected).Status);
        }

        [TestMethod]
        public void RevertExpectation_OfAClear_NeedsTheStatStillUnset()
        {
            var row = new ZoneVariantProfile();
            row.Stats["max_health"] = new StatCurve { Base = 150 };

            Assert.AreEqual(ZoneStatEditStatus.Conflict, Apply(row, "max_health", new StatCurve { Base = 100 }, ZoneStatExpectation.Exactly(null)).Status);
            Assert.AreEqual(ZoneStatEditStatus.Applied, Apply(new ZoneVariantProfile(), "max_health", new StatCurve { Base = 100 }, ZoneStatExpectation.Exactly(null)).Status);
        }

        // The reason is stored and echoed to the game client, which cannot show non-ASCII or carriage returns (CLAUDE.md).
        [DataTestMethod]
        [DataRow("  T14 bosses — too fast\r\n ", "T14 bosses too fast")]
        [DataRow("plain", "plain")]
        [DataRow("   ", null)]
        [DataRow(null, null)]
        public void CleanReason_IsAsciiAndTrimmed(string input, string expected)
        {
            Assert.AreEqual(expected, ZoneControlChangeLog.CleanReason(input));
        }

        [TestMethod]
        public void CleanReason_IsCutToTheColumn()
        {
            var cleaned = ZoneControlChangeLog.CleanReason(new string('a', 400));
            Assert.AreEqual(ZoneControlChangeLog.ReasonMaxLength, cleaned.Length);
        }

        [TestMethod]
        public void ExpectingAValue_ConflictsWhenClearedSince()
        {
            var row = new ZoneVariantProfile();

            Assert.AreEqual(ZoneStatEditStatus.Conflict, Apply(row, "max_health", null, new ZoneStatExpectation { Base = 100 }).Status);
        }

        [TestMethod]
        public void Clear_UnsetStat_IsNothingToClear_AndCreatesNoRow()
        {
            var creates = 0;
            var result = ZoneStatEditor.ApplyToRow(create => { if (create) creates++; return null; }, "max_health", null, null);

            Assert.AreEqual(ZoneStatEditStatus.NothingToClear, result.Status);
            Assert.AreEqual(0, creates);
        }

        [TestMethod]
        public void Conflict_CreatesNoRow()
        {
            var creates = 0;
            var result = ZoneStatEditor.ApplyToRow(create => { if (create) creates++; return null; }, "max_health", new StatCurve { Base = 1 }, new ZoneStatExpectation { Base = 5 });

            Assert.AreEqual(ZoneStatEditStatus.Conflict, result.Status);
            Assert.AreEqual(0, creates);
        }

        [TestMethod]
        public void RowFor_RefusesWcidWithRank()
        {
            var row = ZoneStatEditor.RowFor(new ZoneScalingProfile(), 123, ZcRank.Boss, create: true, out var refusal);

            Assert.IsNull(row);
            Assert.AreEqual(ZoneStatEditor.WcidRankRefusal, refusal);
        }

        [TestMethod]
        public void RowFor_Inspecting_CreatesNoBucket()
        {
            var profile = new ZoneScalingProfile();

            Assert.IsNull(ZoneStatEditor.RowFor(profile, 123, ZcRank.None, create: false, out _));
            Assert.AreEqual(0, profile.WcidOverrides.Count);
        }

        [TestMethod]
        public void SetUnknownStat_IsRefusedBeforeTouchingTheStore()
        {
            var result = ZoneStatEditor.Set(ZoneStatTarget.ForDefault(11, ZcRank.None), "not_a_stat", 1, null);

            Assert.AreEqual(ZoneStatEditStatus.Refused, result.Status);
        }

        [TestMethod]
        public void NegativeVariation_IsRefusedBeforeTouchingTheStore()
        {
            var result = ZoneStatEditor.Set(ZoneStatTarget.ForDefault(-1, ZcRank.None), ZoneStat.MaxHealth, 1, null);

            Assert.AreEqual(ZoneStatEditStatus.Refused, result.Status);
        }

        [TestMethod]
        public void CurveJson_RoundTripsGrowthAndPins()
        {
            var curve = new StatCurve { Base = 7, Growth = 1.5, Additive = true, Overrides = new Dictionary<int, double> { [15] = 42 } };

            var back = ZoneControlChangeLog.ReadCurve(ZoneControlChangeLog.WriteCurve(curve));

            Assert.IsTrue(ZoneStatEditor.SameCurve(curve, back));
            Assert.IsNull(ZoneControlChangeLog.WriteCurve(null));
            Assert.IsNull(ZoneControlChangeLog.ReadCurve(null));
        }

        [TestMethod]
        public void SameCurve_SeesGrowthAndPinDifferences()
        {
            var plain = new StatCurve { Base = 7 };

            Assert.IsTrue(ZoneStatEditor.SameCurve(plain, new StatCurve { Base = 7 }));
            Assert.IsFalse(ZoneStatEditor.SameCurve(plain, new StatCurve { Base = 7, Growth = 2 }));
            Assert.IsFalse(ZoneStatEditor.SameCurve(plain, new StatCurve { Base = 7, Overrides = new Dictionary<int, double> { [12] = 1 } }));
            Assert.IsFalse(ZoneStatEditor.SameCurve(plain, null));
        }

        [TestMethod]
        public void Catalog_EveryKnownStatHasAGroupAndLabel()
        {
            foreach (var stat in ZoneStat.All)
            {
                Assert.IsTrue(ZoneStatCatalog.Groups.Contains(ZoneStatCatalog.GroupOf(stat)), stat);
                Assert.IsFalse(string.IsNullOrWhiteSpace(ZoneStatCatalog.LabelOf(stat)), stat);
            }
        }

        [TestMethod]
        public void Catalog_EveryStatHasARealGroup()
        {
            var other = ZoneStat.All.Where(s => ZoneStatCatalog.GroupOf(s) == ZoneStatCatalog.OtherGroup).ToList();

            Assert.AreEqual(0, other.Count, "Stats with no group rule: " + string.Join(", ", other));
        }

        [TestMethod]
        public void Catalog_LabelsNameTheModifierLineAndTheT25Anchor()
        {
            Assert.AreEqual("Max health", ZoneStatCatalog.LabelOf(ZoneStat.MaxHealth));
            StringAssert.EndsWith(ZoneStatCatalog.LabelOf("max_health_t25"), "(T25)");
            Assert.IsTrue(ZoneModifiers.TryGet(19, out var def));
            Assert.AreEqual(def.Name + " chance", ZoneStatCatalog.LabelOf("modifier_chance_19"));
        }

        [TestMethod]
        public void EditRule_MatchesTheChatCommand() => Assert.AreEqual(AccessLevel.Developer, ZoneControlController.MinEditLevel);
    }
}
