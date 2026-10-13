using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Server.Managers;

namespace ACE.Server.Tests
{
    [TestClass]
    public class PyrealLedgerInsightsTests
    {
        private static readonly DateTime H0 = new DateTime(2026, 10, 9, 19, 0, 0, DateTimeKind.Utc);

        private static PyrealLedgerReports.EarnRow Row(uint account, int hour, string source, string detail, long amountIn, long amountOut = 0, string name = null) =>
            new PyrealLedgerReports.EarnRow { AccountId = account, Hour = H0.AddHours(hour), Source = source, DetailKey = detail, DetailName = name ?? detail, AmountIn = amountIn, AmountOut = amountOut };

        // ---- signals ---------------------------------------------------------------------------------------------

        [TestMethod]
        public void Signals_PeakHourTopSourceAndShare()
        {
            var signals = PyrealLedgerReports.ComputeSignals(new[]
            {
                // account 1: 24B from one NPC in one hour, a little loot in another
                Row(1, 0, "Emote", "115850034", 24_000_000_000, 0, "Forgotten Madman"),
                Row(1, 1, "Loot", "", 5_000_000),
                // accounts 2 and 3 use the same NPC lightly
                Row(2, 0, "Emote", "115850034", 4_000_000_000, 0, "Forgotten Madman"),
                Row(3, 5, "Emote", "115850034", 2_000_000_000, 0, "Forgotten Madman"),
            });

            var one = signals[1];
            Assert.AreEqual(24_005_000_000, one.Created);
            Assert.AreEqual(24_000_000_000, one.PeakHourAmount);
            Assert.AreEqual(H0, one.PeakHourUtc);
            Assert.AreEqual("Emote: Forgotten Madman", one.PeakHourSource);
            Assert.AreEqual(2, one.ActiveHours);
            Assert.AreEqual("Emote: Forgotten Madman", one.TopSource);
            Assert.AreEqual(30_000_000_000, one.TopSourceTotal);
            Assert.AreEqual(3, one.TopSourceUsers);
            Assert.AreEqual(80.0, one.TopSourceSharePct, 1e-9);
        }

        [TestMethod]
        public void Signals_BuyingANoteForItsFaceValueEarnsNothing()
        {
            // 25.875B paid from the bank, 22.5B of trade notes received, same vendor, same hour: a net spend
            var signals = PyrealLedgerReports.ComputeSignals(new[]
            {
                Row(1, 0, "VendorBuy", "24594", 0, 25_875_000_000),
                Row(1, 0, "VendorBuy", "24594", 22_500_000_000, 0),
            });

            var s = signals[1];
            Assert.AreEqual(-3_375_000_000, s.Created);
            Assert.AreEqual(0, s.PeakHourAmount);
            Assert.AreEqual(0, s.ActiveHours);
            Assert.IsNull(s.TopSource);
        }

        [TestMethod]
        public void Signals_SpendingInOneHourDoesNotHideEarningsInAnother()
        {
            var signals = PyrealLedgerReports.ComputeSignals(new[]
            {
                Row(1, 0, "VendorSell", "24596", 3_000_000_000),
                Row(1, 1, "Fee", "Forge", 0, 2_000_000_000),
            });

            Assert.AreEqual(1_000_000_000, signals[1].Created);
            Assert.AreEqual(3_000_000_000, signals[1].PeakHourAmount);
            Assert.AreEqual(1, signals[1].ActiveHours);
        }

        [TestMethod]
        public void Median_IgnoresZeros()
        {
            Assert.AreEqual(0, PyrealLedgerReports.Median(Array.Empty<long>()));
            Assert.AreEqual(0, PyrealLedgerReports.Median(new long[] { 0, 0 }));
            Assert.AreEqual(30, PyrealLedgerReports.Median(new long[] { 0, 10, 30, 1000 }));
        }

        // ---- reasons ---------------------------------------------------------------------------------------------

        private static readonly PyrealLedgerReports.SuspectOptions Defaults = new PyrealLedgerReports.SuspectOptions();

        [TestMethod]
        public void Reasons_NothingStandsOut()
        {
            var row = new PyrealLedgerReports.SuspectRow { CurrencySurplus = 14 };   // a new character's starter coins
            var s = new PyrealLedgerReports.Signals { Created = 40_000_000, PeakHourAmount = 20_000_000, ActiveHours = 6, TopSource = "Loot", TopSourceAmount = 40_000_000, TopSourceTotal = 900_000_000, TopSourceUsers = 70, TopSourceSharePct = 4.4 };

            Assert.AreEqual(0, PyrealLedgerReports.ReasonsFor(row, Defaults).Count);
            Assert.AreEqual(0, PyrealLedgerReports.HighlightsFor(s, 15_000_000, 72, Defaults).Count);
        }

        [TestMethod]
        public void BeingPaidALot_IsAHighlight_NeverAReason()
        {
            var row = new PyrealLedgerReports.SuspectRow();
            var s = new PyrealLedgerReports.Signals
            {
                Created = 12_500_000_000, PeakHourAmount = 1_200_000_000, PeakHourUtc = H0, PeakHourSource = "VendorSell: Tunlok Weapons Master",
                ActiveHours = 69, TopSource = "VendorSell: Tunlok Weapons Master", TopSourceAmount = 12_500_000_000, TopSourceTotal = 12_560_000_000, TopSourceUsers = 12, TopSourceSharePct = 99.5,
            };

            // paid the designed amount, however much and however fast: not a suspect
            Assert.AreEqual(0, PyrealLedgerReports.ReasonsFor(row, Defaults).Count);

            var highlights = PyrealLedgerReports.HighlightsFor(s, 15_000_000, 72, Defaults);
            Assert.AreEqual(3, highlights.Count);
            Assert.IsTrue(highlights.Any(r => r.Contains("of everything VendorSell: Tunlok Weapons Master paid out")));
            Assert.IsTrue(highlights.Any(r => r.StartsWith("Best hour 1.2B")));
            Assert.IsTrue(highlights.Any(r => r.StartsWith("Earned in 69 of 72 hours")));
        }

        [TestMethod]
        public void Highlights_ThresholdsApply()
        {
            var row = new PyrealLedgerReports.SuspectRow();
            var s = new PyrealLedgerReports.Signals { Created = 900_000_000, PeakHourAmount = 900_000_000, ActiveHours = 70, TopSource = "Emote: X", TopSourceAmount = 900_000_000, TopSourceTotal = 900_000_000, TopSourceUsers = 1, TopSourceSharePct = 100 };

            // just under the 1B floor: nothing
            Assert.AreEqual(0, PyrealLedgerReports.HighlightsFor(s, 1_000_000, 72, Defaults).Count);

            // floor lowered: all three
            var loose = new PyrealLedgerReports.SuspectOptions { MinAmount = 500_000_000 };
            Assert.AreEqual(3, PyrealLedgerReports.HighlightsFor(s, 1_000_000, 72, loose).Count);

            // a peak hour that is ordinary for the server is not a reason
            s.TopSourceSharePct = 5;
            s.ActiveHours = 3;
            Assert.AreEqual(0, PyrealLedgerReports.HighlightsFor(s, 400_000_000, 72, loose).Count);

            // round the clock needs at least a day of data
            s.ActiveHours = 10;
            Assert.AreEqual(0, PyrealLedgerReports.HighlightsFor(s, 400_000_000, 10, loose).Count);
        }

        [TestMethod]
        public void Reasons_FlagsSurplusAndUnlabeledGains()
        {
            var row = new PyrealLedgerReports.SuspectRow { FlaggedAmount = 5_000_000, CurrencySurplus = 2_000_000, UnattributedIn = 1 };
            Assert.AreEqual(3, PyrealLedgerReports.ReasonsFor(row, Defaults).Count);
            Assert.AreEqual(0, PyrealLedgerReports.HighlightsFor(null, 0, 72, Defaults).Count);

            row = new PyrealLedgerReports.SuspectRow { CurrencySurplus = 999_999 };
            Assert.AreEqual(0, PyrealLedgerReports.ReasonsFor(row, Defaults).Count);
        }

        [TestMethod]
        public void ExchangeAccountIds_AreParsedLeniently()
        {
            CollectionAssert.AreEquivalent(new uint[] { 1895, 22 }, PyrealLedgerReports.ParseIds(" 1895, 22 ;x,, ").ToList());
            Assert.AreEqual(0, PyrealLedgerReports.ParseIds(null).Count);
        }
    }
}
