using System;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;

namespace ACE.Server.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class EconomyLedgerTests
    {
        [TestInitialize]
        public void Setup()
        {
            PyrealLedger.ForceScopesForTests = true;
            EconomyLedger.ResetForTests(true);
        }

        [TestCleanup]
        public void Cleanup()
        {
            EconomyLedger.ResetForTests(false);
            PyrealLedger.ForceScopesForTests = false;
        }

        // ---- what is tracked ---------------------------------------------------------------------------------------

        [TestMethod]
        public void Tracks_TheOtherBankedCurrencies_NotPyreals()
        {
            Assert.IsTrue(EconomyLedger.Tracks(PropertyInt64.BankedLuminance));
            Assert.IsTrue(EconomyLedger.Tracks(PropertyInt64.BankedEnlightenedCoins));
            Assert.IsTrue(EconomyLedger.Tracks(PropertyInt64.BankedWeaklyEnlightenedCoins));
            Assert.IsTrue(EconomyLedger.Tracks(PropertyInt64.BankedPrestigeCoins));
            Assert.IsTrue(EconomyLedger.Tracks(PropertyInt64.BankedLegendaryKeys));
            Assert.IsTrue(EconomyLedger.Tracks(PropertyInt64.BankedMythicalKeys));

            // pyreals belong to the pyreal ledger; XP must never be touched
            Assert.IsFalse(EconomyLedger.Tracks(PropertyInt64.BankedPyreals));
            Assert.IsFalse(EconomyLedger.Tracks(PropertyInt64.TotalExperience));
        }

        [TestMethod]
        public void Off_TracksNothingAndRecordsNothing()
        {
            EconomyLedger.ResetForTests(false);

            Assert.IsFalse(EconomyLedger.Tracks(PropertyInt64.BankedLuminance));
            EconomyLedger.OnBalanceChanged(1, "A", 1, 0, PropertyInt64.BankedLuminance, 0, 500);
            Assert.AreEqual(0, EconomyLedger.PendingForTests().Count);
        }

        [TestMethod]
        public void EveryCurrencyHasAUniqueKey_AndPyrealsReadThePyrealLedger()
        {
            Assert.AreEqual(EconomyLedger.Currencies.Length, EconomyLedger.Currencies.Select(c => c.Key).Distinct().Count());
            Assert.IsTrue(EconomyLedger.Find("pyreals").FromPyrealLedger);
            Assert.AreEqual(1, EconomyLedger.Currencies.Count(c => c.FromPyrealLedger));
            Assert.IsNull(EconomyLedger.Find("nope"));
        }

        // ---- recording ---------------------------------------------------------------------------------------------

        [TestMethod]
        public void BalanceChange_IsRecordedUnderTheOpenScope()
        {
            using (PyrealLedger.Begin(EconomyLedger.SrcKill, "1234", "Drudge"))
                EconomyLedger.OnBalanceChanged(7, "Alice", 3, 0, PropertyInt64.BankedLuminance, 1000, 1500);

            var row = EconomyLedger.PendingForTests().Single();
            Assert.AreEqual("luminance", row.currency);
            Assert.AreEqual(7u, row.charId);
            Assert.AreEqual(EconomyLedger.FormBank, row.form);
            Assert.AreEqual(EconomyLedger.SrcKill, row.source);
            Assert.AreEqual("1234", row.detailKey);
            Assert.AreEqual(500, row.amountIn);
            Assert.AreEqual(0, row.amountOut);
        }

        [TestMethod]
        public void SameSourceOnTheSameDay_AddsUp()
        {
            using (PyrealLedger.Begin(EconomyLedger.SrcKill, "1234", "Drudge"))
            {
                EconomyLedger.OnBalanceChanged(7, "Alice", 3, 0, PropertyInt64.BankedLuminance, 0, 100);
                EconomyLedger.OnBalanceChanged(7, "Alice", 3, 0, PropertyInt64.BankedLuminance, 100, 250);
            }

            Assert.AreEqual(250, EconomyLedger.PendingForTests().Single().amountIn);
        }

        [TestMethod]
        public void Spend_IsRecordedAsOut()
        {
            using (PyrealLedger.Begin(EconomyLedger.SrcEnlighten, "5", "Enlightenment 5"))
                EconomyLedger.OnBalanceChanged(7, "Alice", 3, 0, PropertyInt64.BankedLuminance, 900, 400);

            var row = EconomyLedger.PendingForTests().Single();
            Assert.AreEqual(0, row.amountIn);
            Assert.AreEqual(500, row.amountOut);
        }

        [TestMethod]
        public void NoScope_IsUnattributed()
        {
            EconomyLedger.OnBalanceChanged(7, "Alice", 3, 0, PropertyInt64.BankedPrestigeCoins, 0, 5);

            var row = EconomyLedger.PendingForTests().Single();
            Assert.AreEqual("prestige-coins", row.currency);
            Assert.AreEqual(PyrealLedger.SrcUnattributed, row.source);
        }

        [TestMethod]
        public void StaffCharacters_AreNotRecorded()
        {
            EconomyLedger.OnBalanceChanged(7, "Admin", 3, 5, PropertyInt64.BankedLuminance, 0, 1_000_000);
            Assert.AreEqual(0, EconomyLedger.PendingForTests().Count);
        }

        [TestMethod]
        public void NoChange_IsNotRecorded()
        {
            EconomyLedger.OnBalanceChanged(7, "Alice", 3, 0, PropertyInt64.BankedLuminance, 500, 500);
            Assert.AreEqual(0, EconomyLedger.PendingForTests().Count);
        }

        [TestMethod]
        public void BankCommand_IsAnInternalMove()
        {
            // the chat command scope opened by GameActionTalk for "/b" and "/bank"
            using (PyrealLedger.Begin(PyrealLedger.SrcCommand, "b", "@b"))
                EconomyLedger.OnBalanceChanged(7, "Alice", 3, 0, PropertyInt64.BankedEnlightenedCoins, 0, 10);

            var row = EconomyLedger.PendingForTests().Single();
            Assert.AreEqual(EconomyLedger.SrcBank, row.source);
            Assert.IsTrue(EconomyLedger.IsInternalSource(row.source));
        }

        [TestMethod]
        public void OtherCommands_StayCommands()
        {
            using (PyrealLedger.Begin(PyrealLedger.SrcCommand, "casino", "@casino"))
                EconomyLedger.OnBalanceChanged(7, "Alice", 3, 0, PropertyInt64.BankedLuminance, 100, 0);

            var row = EconomyLedger.PendingForTests().Single();
            Assert.AreEqual(PyrealLedger.SrcCommand, row.source);
            Assert.AreEqual("casino", row.detailKey);
            Assert.IsFalse(EconomyLedger.IsInternalSource(row.source));
        }

        [TestMethod]
        public void FellowshipShare_IsAlwaysFellowship_AndKeepsTheCreatureWhenKnown()
        {
            // paid inline, inside the killer's kill scope
            using (PyrealLedger.Begin(EconomyLedger.SrcKill, "1234", "Drudge"))
            using (EconomyLedger.BeginLuminance(ACE.Entity.Enum.XpType.Fellowship))
                EconomyLedger.OnBalanceChanged(7, "Alice", 3, 0, PropertyInt64.BankedLuminance, 0, 100);

            // paid later on the member's own thread: no scope open
            using (EconomyLedger.BeginLuminance(ACE.Entity.Enum.XpType.Fellowship))
                EconomyLedger.OnBalanceChanged(8, "Bob", 4, 0, PropertyInt64.BankedLuminance, 0, 100);

            var rows = EconomyLedger.PendingForTests();
            Assert.AreEqual(2, rows.Count);
            Assert.IsTrue(rows.All(r => r.source == EconomyLedger.SrcFellowship));
            Assert.AreEqual("1234", rows.Single(r => r.charId == 7).detailKey);
            Assert.AreEqual("", rows.Single(r => r.charId == 8).detailKey);
        }

        [TestMethod]
        public void KillAndQuestLuminance_KeepTheOpenScope_OrAreLabeledByType()
        {
            using (PyrealLedger.Begin(PyrealLedger.SrcEmote, "555", "Quest NPC"))
            using (EconomyLedger.BeginLuminance(ACE.Entity.Enum.XpType.Quest))
                EconomyLedger.OnBalanceChanged(7, "Alice", 3, 0, PropertyInt64.BankedLuminance, 0, 100);

            using (EconomyLedger.BeginLuminance(ACE.Entity.Enum.XpType.Kill))
                EconomyLedger.OnBalanceChanged(8, "Bob", 4, 0, PropertyInt64.BankedLuminance, 0, 100);

            var rows = EconomyLedger.PendingForTests();
            Assert.AreEqual(PyrealLedger.SrcEmote, rows.Single(r => r.charId == 7).source);
            Assert.AreEqual(EconomyLedger.SrcKill, rows.Single(r => r.charId == 8).source);
        }

        [TestMethod]
        public void BiotaWrite_ReturnsThePreviousValue()
        {
            var biota = new ACE.Entity.Models.Biota { Id = 1, WeenieClassId = 1, WeenieType = ACE.Entity.Enum.WeenieType.Creature };
            var rwLock = new System.Threading.ReaderWriterLockSlim();

            ACE.Entity.Models.BiotaExtensions.SetProperty(biota, PropertyInt64.BankedLuminance, 500, rwLock, out var changed, out var previous);
            Assert.IsTrue(changed);
            Assert.AreEqual(0, previous);

            ACE.Entity.Models.BiotaExtensions.SetProperty(biota, PropertyInt64.BankedLuminance, 800, rwLock, out changed, out previous);
            Assert.IsTrue(changed);
            Assert.AreEqual(500, previous);

            ACE.Entity.Models.BiotaExtensions.SetProperty(biota, PropertyInt64.BankedLuminance, 800, rwLock, out changed, out previous);
            Assert.IsFalse(changed);
            Assert.AreEqual(800, previous);

            Assert.IsTrue(ACE.Entity.Models.BiotaExtensions.TryRemoveProperty(biota, PropertyInt64.BankedLuminance, rwLock, out previous));
            Assert.AreEqual(800, previous);
            Assert.IsFalse(ACE.Entity.Models.BiotaExtensions.TryRemoveProperty(biota, PropertyInt64.BankedLuminance, rwLock, out previous));
            Assert.AreEqual(0, previous);
        }

        // ---- supply ------------------------------------------------------------------------------------------------

        [TestMethod]
        public void SnapshotDue_OncePerDay_AtTheQuietHour()
        {
            var today = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);
            var yesterday = today.AddDays(-1);

            Assert.IsFalse(EconomyLedger.SnapshotDue(today.AddHours(9), yesterday, true, 10));   // before the hour
            Assert.IsTrue(EconomyLedger.SnapshotDue(today.AddHours(10), yesterday, true, 10));
            Assert.IsTrue(EconomyLedger.SnapshotDue(today.AddHours(23), yesterday, true, 10));   // server was down at the hour
            Assert.IsFalse(EconomyLedger.SnapshotDue(today.AddHours(12), today, true, 10));      // already taken today
            Assert.IsTrue(EconomyLedger.SnapshotDue(today.AddHours(1), DateTime.MinValue, false, 10)); // first ever: at once
            Assert.IsTrue(EconomyLedger.SnapshotDue(today.AddHours(23), yesterday, true, 99));   // out of range hour is clamped
        }

        [TestMethod]
        public void Summarize_SplitsActiveStaffAndTopHolders()
        {
            var holdings = Enumerable.Range(1, 200).Select(i => new EconomyLedger.Holding(i, 0, active: i <= 50, admin: false)).ToList();
            holdings.Add(new EconomyLedger.Holding(1_000_000, 0, active: true, admin: true));    // staff: only in AdminSupply
            holdings.Add(new EconomyLedger.Holding(0, 0, active: true, admin: false));           // holds nothing: not a holder
            holdings.Add(new EconomyLedger.Holding(-500, 30, active: false, admin: false));      // negative bank counts as zero

            var row = EconomyLedger.Summarize("luminance", holdings);

            Assert.AreEqual(200 * 201 / 2, row.Banked);
            Assert.AreEqual(30, row.Items);
            Assert.AreEqual(201, row.Holders);
            Assert.AreEqual(50 * 51 / 2, row.ActiveSupply);
            Assert.AreEqual(50, row.ActiveHolders);
            Assert.AreEqual(1_000_000, row.AdminSupply);
            Assert.AreEqual(200, row.LargestHolder);
            // 201 holders -> the richest 3 (1%, rounded up)
            Assert.AreEqual(200 + 199 + 198, row.Top1PctAmount);
        }

        [TestMethod]
        public void Summarize_Empty_IsAllZero()
        {
            var row = EconomyLedger.Summarize("luminance", Array.Empty<EconomyLedger.Holding>());
            Assert.AreEqual(0, row.Banked + row.Items + row.Holders + row.Top1PctAmount + row.LargestHolder);
        }

        [TestMethod]
        public void NetSinceSnapshot_IsOnlyHandedOverForAFullyRecordedPeriod()
        {
            var luminance = EconomyLedger.Find("luminance").Index;

            // recording started after the last snapshot: this period is incomplete
            EconomyLedger.OnBalanceChanged(7, "Alice", 3, 0, PropertyInt64.BankedLuminance, 0, 100);
            Assert.IsNull(EconomyLedger.TakeNetSinceSnapshot());

            // from that snapshot on, everything is recorded; internal moves count too
            EconomyLedger.OnBalanceChanged(7, "Alice", 3, 0, PropertyInt64.BankedLuminance, 100, 600);
            EconomyLedger.OnBalanceChanged(7, "Alice", 3, 0, PropertyInt64.BankedLuminance, 600, 450);
            var net = EconomyLedger.TakeNetSinceSnapshot();
            Assert.AreEqual(350, net[luminance]);

            // and the next period starts from zero
            Assert.AreEqual(0, EconomyLedger.TakeNetSinceSnapshot()[luminance]);
        }

        [TestMethod]
        public void Unexplained_IsSupplyChangeMinusRecordedFlow()
        {
            Assert.AreEqual(0, EconomyReports.Unexplained(1000, 1350, 350));
            Assert.AreEqual(500, EconomyReports.Unexplained(1000, 1850, 350));     // appeared with no record
            Assert.AreEqual(-200, EconomyReports.Unexplained(1000, 1150, 350));    // vanished
            Assert.IsNull(EconomyReports.Unexplained(1000, 1350, null));           // period not fully recorded
        }

        // ---- stalled exporters -------------------------------------------------------------------------------------

        [TestMethod]
        public void Progress_NewCharactersAreStoredOnceTheyHaveProgress_AndChangesAreSeen()
        {
            Assert.AreEqual(EconomyProgress.RowAction.None, EconomyProgress.Decide(null, new EconomyProgress.Counts(0, 0)));
            Assert.AreEqual(EconomyProgress.RowAction.Insert, EconomyProgress.Decide(null, new EconomyProgress.Counts(1, 0)));
            Assert.AreEqual(EconomyProgress.RowAction.Insert, EconomyProgress.Decide(null, new EconomyProgress.Counts(0, 5)));

            var stored = new EconomyProgress.Counts(12, 300);
            Assert.AreEqual(EconomyProgress.RowAction.None, EconomyProgress.Decide(stored, new EconomyProgress.Counts(12, 300)));
            Assert.AreEqual(EconomyProgress.RowAction.Changed, EconomyProgress.Decide(stored, new EconomyProgress.Counts(13, 300)));
            Assert.AreEqual(EconomyProgress.RowAction.Changed, EconomyProgress.Decide(stored, new EconomyProgress.Counts(12, 301)));
        }

        private static EconomyStalledReport.ProgressChar Char(int firstSeenDaysAgo, int? lastChangeDaysAgo, DateTime now) =>
            new EconomyStalledReport.ProgressChar { FirstSeenUtc = now.AddDays(-firstSeenDaysAgo), LastChangeUtc = lastChangeDaysAgo.HasValue ? now.AddDays(-lastChangeDaysAgo.Value) : null };

        [TestMethod]
        public void Stall_IsMeasuredFromTheMostRecentProgressOnTheAccount()
        {
            var now = new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc);

            // one character progressed 5 days ago: the account is not stalled, whatever the others did
            var (days, isMinimum, _) = EconomyStalledReport.StallOf(new[] { Char(60, 40, now), Char(60, 5, now), Char(60, null, now) }, now);
            Assert.AreEqual(5, days);
            Assert.IsFalse(isMinimum);

            // nobody has progressed since tracking began 60 days ago: at least 60 days
            (days, isMinimum, _) = EconomyStalledReport.StallOf(new[] { Char(60, null, now), Char(60, null, now) }, now);
            Assert.AreEqual(60, days);
            Assert.IsTrue(isMinimum);

            // a character first recorded 3 days ago got its first enlightenment then: that is progress
            (days, _, _) = EconomyStalledReport.StallOf(new[] { Char(60, null, now), Char(3, null, now) }, now);
            Assert.AreEqual(3, days);
        }

        [TestMethod]
        public void Matches_NeedsEstablishedStalledAndExporting()
        {
            var sent = new System.Collections.Generic.Dictionary<string, long> { ["luminance"] = 5000, ["pyreals"] = 10 };
            var nothing = new System.Collections.Generic.Dictionary<string, long>();

            Assert.IsTrue(EconomyStalledReport.Matches(12, 45, sent, 10, 0, 30, null, 0));
            Assert.IsFalse(EconomyStalledReport.Matches(9, 45, sent, 10, 0, 30, null, 0));        // not established
            Assert.IsFalse(EconomyStalledReport.Matches(12, 29, sent, 10, 0, 30, null, 0));       // still progressing
            Assert.IsFalse(EconomyStalledReport.Matches(12, 45, nothing, 10, 0, 30, null, 0));    // sends nothing away
            Assert.IsFalse(EconomyStalledReport.Matches(40, 45, sent, 10, 30, 30, null, 0));      // above the upper limit
            Assert.IsTrue(EconomyStalledReport.Matches(30, 45, sent, 10, 30, 30, null, 0));

            // one currency selected: the threshold applies to that currency only
            Assert.IsTrue(EconomyStalledReport.Matches(12, 45, sent, 10, 0, 30, "luminance", 5000));
            Assert.IsFalse(EconomyStalledReport.Matches(12, 45, sent, 10, 0, 30, "luminance", 5001));
            Assert.IsFalse(EconomyStalledReport.Matches(12, 45, sent, 10, 0, 30, "prestige-coins", 0));

            // stall of 0 days shows every established exporter
            Assert.IsTrue(EconomyStalledReport.Matches(12, 0, sent, 10, 0, 0, null, 0));
        }

        // ---- reports -----------------------------------------------------------------------------------------------

        private static EconomyReports.FlowGroup Group(string source, string detail, long amountIn, long amountOut) =>
            new EconomyReports.FlowGroup { Day = DateTime.UtcNow.Date, Source = source, DetailKey = detail, AmountIn = amountIn, AmountOut = amountOut };

        [TestMethod]
        public void NetFlow_NetsEachSourceAndDetail_AndLeavesOutInternalMoves()
        {
            var (faucets, sinks, moved) = EconomyReports.NetFlow(new[]
            {
                Group(PyrealLedger.SrcEmote, "100", 5000, 0),               // quest reward: faucet 5000
                Group(PyrealLedger.SrcVendorBuy, "200", 250_000, 250_000),  // note bought for its face value: neither
                Group(PyrealLedger.SrcVendorBuy, "300", 0, 900),            // real purchase: sink 900
                Group(PyrealLedger.SrcGiveNpc, "100", 0, 100),              // turn-in: sink 100
                Group(EconomyLedger.SrcBank, "", 7000, 7000),               // deposit: moved, not created
                Group(PyrealLedger.SrcTransfer, "", 300, 300),
            });

            Assert.AreEqual(5000, faucets);
            Assert.AreEqual(1000, sinks);
            Assert.AreEqual(7300, moved);
        }

        [TestMethod]
        public void WeeklyPct_ScalesToAWeek()
        {
            Assert.AreEqual(10.0, EconomyReports.WeeklyPct(1000, 1100, 7).Value, 1e-9);
            // 1% in one day is 7% a week
            Assert.AreEqual(7.0, EconomyReports.WeeklyPct(1000, 1010, 1).Value, 1e-9);
            Assert.AreEqual(-5.0, EconomyReports.WeeklyPct(1000, 900, 14).Value, 1e-9);
            Assert.IsNull(EconomyReports.WeeklyPct(0, 500, 7));
            Assert.IsNull(EconomyReports.WeeklyPct(1000, 1100, 0));
        }

        [TestMethod]
        public void Status_UsesTheTargetBand()
        {
            Assert.AreEqual("unknown", EconomyReports.StatusOf(null, -1, 0));
            Assert.AreEqual("inflating", EconomyReports.StatusOf(0.01, -1, 0));
            Assert.AreEqual("healthy", EconomyReports.StatusOf(0, -1, 0));
            Assert.AreEqual("healthy", EconomyReports.StatusOf(-1, -1, 0));
            Assert.AreEqual("deflating", EconomyReports.StatusOf(-1.01, -1, 0));
        }
    }
}
