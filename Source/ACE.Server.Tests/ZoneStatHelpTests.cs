using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>The admin help for Zone Control stats (ZoneStatHelp) and the portal guide (ZoneControlGuide).</summary>
    [TestClass]
    public class ZoneStatHelpTests
    {
        private static IEnumerable<string> HelpStrings(ZoneStatHelpEntry e) =>
            new[] { e.Key, e.Summary, e.Details, e.Shape, e.WhenUnset, e.Raise, e.Lower, e.Range, e.Caution }
                .Concat(e.Related)
                .Where(s => s != null);

        // The in-game help prints this text to the AC client, which draws non-ASCII and carriage returns as garbage (CLAUDE.md).
        private static void AssertClientSafe(string text, string where)
        {
            var bad = text.FirstOrDefault(c => c > 126 || (c < 32 && c != '\n'));
            Assert.AreEqual(default(char), bad, $"{where}: character U+{(int)bad:X4} cannot be shown by the game client: \"{text}\"");
        }

        [TestMethod]
        public void EveryStatInACompleteGroup_HasHelp()
        {
            var missing = ZoneStat.All
                .Where(s => ZoneStatHelp.CompleteGroups.Contains(ZoneStatCatalog.GroupOf(s)))
                .Where(s => ZoneStatHelp.For(s) == null)
                .ToList();
            Assert.AreEqual(0, missing.Count, "No help for: " + string.Join(", ", missing));
        }

        // Every group is written, so every stat must have help: a new stat without an entry fails here.
        [TestMethod]
        public void EveryStat_HasHelp()
        {
            var missing = ZoneStat.All.Where(s => ZoneStatHelp.For(s) == null).ToList();
            Assert.AreEqual(0, missing.Count, "No help for: " + string.Join(", ", missing));
        }

        [TestMethod]
        public void CompleteGroups_AreRealGroups()
        {
            foreach (var g in ZoneStatHelp.CompleteGroups)
                Assert.IsTrue(ZoneStatCatalog.Groups.Contains(g), $"'{g}' is not a ZoneStatCatalog group");
        }

        [TestMethod]
        public void Entries_CoverOnlyKnownStats_AndAreClientSafe()
        {
            foreach (var e in ZoneStatHelp.Entries)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(e.Summary), $"{e.Key}: no summary");
                Assert.IsTrue(ZoneStat.All.Any(e.Covers), $"{e.Key}: covers no stat in ZoneStat.All");
                foreach (var r in e.Related)
                    Assert.IsNotNull(ZoneStatEditor.NormalizeStat(r), $"{e.Key}: related stat '{r}' is not a stat");
                foreach (var s in HelpStrings(e))
                    AssertClientSafe(s, e.Key);
            }
        }

        [TestMethod]
        public void EachStat_HasAtMostOneExactEntry()
        {
            var dupes = ZoneStatHelp.Entries.Where(e => e.Pattern == null)
                .GroupBy(e => e.Key, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.AreEqual(0, dupes.Count, "Duplicate entries: " + string.Join(", ", dupes));
        }

        [TestMethod]
        public void AnchorKey_SharesItsBaseEntry()
        {
            var anchored = ZoneStat.All.FirstOrDefault(s => s.EndsWith(ZoneStatHelp.AnchorSuffix) && ZoneStatHelp.For(s.Substring(0, s.Length - 4)) != null);
            if (anchored == null)
                Assert.Inconclusive("No help entry has a _t25 twin yet.");
            var entry = ZoneStatHelp.For(anchored, out var isAnchor);
            Assert.IsTrue(isAnchor);
            Assert.AreSame(ZoneStatHelp.For(anchored.Substring(0, anchored.Length - 4)), entry);
        }

        [TestMethod]
        public void For_IsCaseInsensitive_AndNullForUnknown()
        {
            Assert.IsNotNull(ZoneStatHelp.For("MAX_HEALTH"));
            Assert.IsNull(ZoneStatHelp.For("no_such_stat"));
            Assert.IsNull(ZoneStatHelp.For(null));
        }

        [TestMethod]
        public void ChatLines_AreClientSafe_AndNameTheStat()
        {
            foreach (var stat in ZoneStat.All.Where(s => ZoneStatHelp.For(s) != null))
            {
                var lines = ZoneStatHelp.ChatLines(stat);
                Assert.IsTrue(lines.Count > 2, stat);
                StringAssert.StartsWith(lines[0], "== " + stat);
                foreach (var line in lines)
                    AssertClientSafe(line, stat);
            }
        }

        [TestMethod]
        public void TimingText_ExistsForEveryTiming()
        {
            foreach (ZoneStatTiming t in Enum.GetValues(typeof(ZoneStatTiming)))
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(ZoneStatHelp.TakesEffect(t)), t.ToString());
                AssertClientSafe(ZoneStatHelp.TakesEffect(t), t.ToString());
                AssertClientSafe(ZoneStatHelp.HowToUndo(t), t.ToString());
            }
        }

        [TestMethod]
        public void Guide_IsClientSafe()
        {
            Assert.IsTrue(ZoneControlGuide.Sections.Count > 0);
            foreach (var section in ZoneControlGuide.Sections)
            {
                AssertClientSafe(section.Title, section.Title);
                foreach (var p in section.Paragraphs)
                    AssertClientSafe(p, section.Title);
            }
        }
    }
}
