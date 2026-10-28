using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Tier rewards (owner 2026-10-24): the pure tier-dip rules (TierRewardScale) and the shipped defaults of its live
    /// settings. No DB, no world.
    /// </summary>
    [TestClass]
    public class TierRewardScaleTests
    {
        // -- shipped defaults --

        [TestMethod]
        public void Defaults_TierDip()
        {
            Assert.IsTrue(ServerConfig.zc_tier_dip_enabled.Default);
            Assert.AreEqual(0.15, ServerConfig.zc_tier_dip_step.Default, 1e-9);
            Assert.AreEqual(0.0, ServerConfig.zc_tier_dip_floor.Default, 1e-9);
        }

        // -- credit --

        [TestMethod]
        public void Credit_StepsDownPerTierBelow()
        {
            Assert.AreEqual(1.00, TierRewardScale.Credit(13, 13, 0.15, 0), 1e-9);
            Assert.AreEqual(0.85, TierRewardScale.Credit(13, 12, 0.15, 0), 1e-9);
            Assert.AreEqual(0.70, TierRewardScale.Credit(13, 11, 0.15, 0), 1e-9);
            Assert.AreEqual(0.55, TierRewardScale.Credit(14, 11, 0.15, 0), 1e-9);
            Assert.AreEqual(0.40, TierRewardScale.Credit(15, 11, 0.15, 0), 1e-9);
        }

        [TestMethod]
        public void Credit_FullAtOrAboveOwnTier()
        {
            Assert.AreEqual(1.0, TierRewardScale.Credit(13, 14, 0.15, 0), "above own tier (Zone Share)");
            Assert.AreEqual(1.0, TierRewardScale.Credit(13, 25, 0.15, 0));
        }

        [TestMethod]
        public void Credit_NeverBelowT11()
        {
            Assert.AreEqual(1.0, TierRewardScale.Credit(15, 10, 0.15, 0), "a T10 / VoD kill is never cut");
            Assert.AreEqual(1.0, TierRewardScale.Credit(15, 0, 0.15, 0), "retail");
            Assert.AreEqual(1.0, TierRewardScale.Credit(0, 11, 0.15, 0), "a player with no tier");
            Assert.AreEqual(1.0, TierRewardScale.Credit(10, 11, 0.15, 0));
        }

        [TestMethod]
        public void Credit_FloorAndClamp()
        {
            Assert.AreEqual(0.0, TierRewardScale.Credit(25, 11, 0.15, 0), "far below hits the 0 floor, never negative");
            Assert.AreEqual(0.25, TierRewardScale.Credit(25, 11, 0.15, 0.25), 1e-9);
            Assert.AreEqual(0.85, TierRewardScale.Credit(13, 12, 0.15, 0.25), 1e-9, "the floor only applies under it");
            Assert.AreEqual(1.0, TierRewardScale.Credit(25, 11, 0.15, 7), "a floor above 1 is clamped");
            Assert.AreEqual(0.0, TierRewardScale.Credit(25, 11, 0.15, -1), "a negative floor is clamped to 0");
        }

        [TestMethod]
        public void Credit_BadStepNeverCuts()
        {
            Assert.AreEqual(1.0, TierRewardScale.Credit(15, 11, 0, 0));
            Assert.AreEqual(1.0, TierRewardScale.Credit(15, 11, -0.15, 0), "a negative step never raises credit above 1");
            Assert.AreEqual(1.0, TierRewardScale.Credit(15, 11, double.NaN, 0));
            Assert.AreEqual(0.4, TierRewardScale.Credit(15, 11, 0.15, double.NaN), 1e-9, "a NaN floor counts as 0");
        }
        // -- Prestige Coins per kill --

        [TestMethod]
        public void Defaults_PrestigeCoinsPerKill()
        {
            Assert.AreEqual(0.05, ServerConfig.zc_pcoin_kill_base.Default, 1e-9);
            Assert.AreEqual(1.20, ServerConfig.zc_pcoin_kill_tier_step.Default, 1e-9);
        }

        [TestMethod]
        public void ZoneValue_PlusTwentyPctPerTier()
        {
            Assert.AreEqual(0.050, TierRewardScale.ZoneValue(11, 0.05, 1.2), 1e-9);
            Assert.AreEqual(0.060, TierRewardScale.ZoneValue(12, 0.05, 1.2), 1e-9);
            Assert.AreEqual(0.072, TierRewardScale.ZoneValue(13, 0.05, 1.2), 1e-9);
            Assert.AreEqual(0.0864, TierRewardScale.ZoneValue(14, 0.05, 1.2), 1e-9);
            Assert.AreEqual(0.10368, TierRewardScale.ZoneValue(15, 0.05, 1.2), 1e-9);
        }

        [TestMethod]
        public void ZoneValue_OnlyT11ToT25()
        {
            Assert.AreEqual(0.0, TierRewardScale.ZoneValue(10, 0.05, 1.2), "T10 / Valley of Death pays nothing");
            Assert.AreEqual(0.0, TierRewardScale.ZoneValue(0, 0.05, 1.2));
            Assert.AreEqual(0.0, TierRewardScale.ZoneValue(26, 0.05, 1.2));
            Assert.AreEqual(0.0, TierRewardScale.ZoneValue(13, 0, 1.2), "a base of 0 is off");
            Assert.AreEqual(0.0, TierRewardScale.ZoneValue(13, double.NaN, 1.2));
            Assert.AreEqual(0.05, TierRewardScale.ZoneValue(13, 0.05, 0), 1e-9, "a bad step counts as 1");
        }

        [TestMethod]
        public void AddCoins_CarriesTheFraction()
        {
            var (whole, frac) = TierRewardScale.AddCoins(0, 0.05);
            Assert.AreEqual(0, whole);
            Assert.AreEqual(0.05, frac, 1e-12);

            (whole, frac) = TierRewardScale.AddCoins(0.97, 0.05);
            Assert.AreEqual(1, whole);
            Assert.AreEqual(0.02, frac, 1e-9);

            (whole, frac) = TierRewardScale.AddCoins(0.5, 2.75);
            Assert.AreEqual(3, whole);
            Assert.AreEqual(0.25, frac, 1e-9);
        }

        [TestMethod]
        public void AddCoins_TwentyKillsMakeOneCoin()
        {
            double carried = 0;
            long banked = 0;
            for (var i = 0; i < 20; i++)
            {
                var (whole, frac) = TierRewardScale.AddCoins(carried, 0.05);
                banked += whole;
                carried = frac;
            }
            Assert.AreEqual(1, banked);
        }

        [TestMethod]
        public void AddCoins_BadInputsMintNothing()
        {
            Assert.AreEqual((0L, 0.0), TierRewardScale.AddCoins(5.0, 0), "a hand-edited carry of 5 is not 5 coins");
            Assert.AreEqual((0L, 0.0), TierRewardScale.AddCoins(double.NaN, 0));
            Assert.AreEqual((0L, 0.0), TierRewardScale.AddCoins(-3, 0));
            Assert.AreEqual(0L, TierRewardScale.AddCoins(0.5, -10).Whole);
            Assert.AreEqual(0L, TierRewardScale.AddCoins(0.5, double.PositiveInfinity).Whole);
        }

        // -- kill-task credit --

        [TestMethod]
        public void Defaults_KillTaskDecimalCreditOn()
        {
            Assert.IsTrue(ServerConfig.zc_kt_decimal_credit_enabled.Default);
        }

        private static double Share(int n) => ACE.Server.Entity.Fellowship.GetMemberSharePercent(n);

        [TestMethod]
        public void KillTaskCredit_TaggerFullSharerShare()
        {
            Assert.AreEqual(1.0, TierRewardScale.KillTaskCredit(true, 3, false, 1.0, Share), 1e-9, "a tagger gets a full kill");
            Assert.AreEqual(0.75, TierRewardScale.KillTaskCredit(false, 2, false, 1.0, Share), 1e-9);
            Assert.AreEqual(0.6, TierRewardScale.KillTaskCredit(false, 3, false, 1.0, Share), 1e-9);
            Assert.AreEqual(1.0, TierRewardScale.KillTaskCredit(false, 1, false, 1.0, Share), 1e-9, "a group of one is the full share");
            Assert.AreEqual(1.0, TierRewardScale.KillTaskCredit(false, 0, false, 1.0, Share), 1e-9, "a bad size counts as one");
        }

        [TestMethod]
        public void KillTaskCredit_SingleSolveIsFullShare()
        {
            Assert.AreEqual(1.0, TierRewardScale.KillTaskCredit(false, 6, true, 1.0, Share), 1e-9);
            Assert.AreEqual(0.85, TierRewardScale.KillTaskCredit(false, 6, true, 0.85, Share), 1e-9, "the tier dip still applies");
        }

        [TestMethod]
        public void KillTaskCredit_TimesTierCredit()
        {
            Assert.AreEqual(0.85, TierRewardScale.KillTaskCredit(true, 1, false, 0.85, Share), 1e-9);
            Assert.AreEqual(0.6 * 0.7, TierRewardScale.KillTaskCredit(false, 3, false, 0.7, Share), 1e-9);
            Assert.AreEqual(0.0, TierRewardScale.KillTaskCredit(true, 1, false, 0, Share));
            Assert.AreEqual(0.0, TierRewardScale.KillTaskCredit(true, 1, false, double.NaN, Share));
            Assert.AreEqual(1.0, TierRewardScale.KillTaskCredit(true, 1, false, 5, Share), "a tier credit above 1 is clamped");
        }

        [TestMethod]
        public void AddKillTaskProgress_Decimals()
        {
            Assert.AreEqual((4, 0.5), Round(TierRewardScale.AddKillTaskProgress(4, 0, 0.5, 20)));
            Assert.AreEqual((5, 0.0), Round(TierRewardScale.AddKillTaskProgress(4, 0.5, 0.5, 20)));
            Assert.AreEqual((5, 0.25), Round(TierRewardScale.AddKillTaskProgress(4, 0.5, 0.75, 20)));
            Assert.AreEqual((6, 0.0), Round(TierRewardScale.AddKillTaskProgress(4, 0, 2, 20)), "pet multicredit: two whole kills");
        }

        [TestMethod]
        public void AddKillTaskProgress_FloatingPointLandsOnTheWholeKill()
        {
            // 0.85 + 0.15 is a hair under 1 in floating point - it must still be the whole kill
            Assert.AreEqual((1, 0.0), Round(TierRewardScale.AddKillTaskProgress(0, 0.85, 0.15, 20)));

            // a sharer at 0.6 x 0.85: 100 kills = 51 whole kills, nothing lost to drift
            int count = 0;
            double frac = 0;
            for (var i = 0; i < 100; i++)
                (count, frac) = TierRewardScale.AddKillTaskProgress(count, frac, 0.6 * 0.85, 1000);
            Assert.AreEqual(51, count);
            Assert.AreEqual(0.0, frac, 1e-6);
        }

        [TestMethod]
        public void AddKillTaskProgress_CapDropsOverflow()
        {
            Assert.AreEqual((20, 0.0), Round(TierRewardScale.AddKillTaskProgress(19, 0.75, 0.5, 20)));
            Assert.AreEqual((20, 0.0), Round(TierRewardScale.AddKillTaskProgress(20, 0, 1, 20)), "already complete");
            Assert.AreEqual((0, 0.85), Round(TierRewardScale.AddKillTaskProgress(0, 0, 0.85, 1)), "a one-solve task keeps the tier dip as a decimal");
        }

        [TestMethod]
        public void AddKillTaskProgress_NoCapAndBadInputs()
        {
            Assert.AreEqual((30, 0.5), Round(TierRewardScale.AddKillTaskProgress(30, 0, 0.5, -1)), "MaxSolves -1 = no cap");
            Assert.AreEqual((3, 0.0), Round(TierRewardScale.AddKillTaskProgress(3, 7.0, 0, 20)), "a bad saved decimal is ignored");
            Assert.AreEqual((3, 0.0), Round(TierRewardScale.AddKillTaskProgress(3, 0, double.NaN, 20)));
            Assert.AreEqual((3, 0.0), Round(TierRewardScale.AddKillTaskProgress(3, 0, -1, 20)));
        }

        // -- bounty credit --

        [TestMethod]
        public void AddBountyProgress_FullKillsStillCountOneEach()
        {
            Assert.AreEqual((3.0, false), TierRewardScale.AddBountyProgress(2, 1.0, 100));
            Assert.AreEqual((0.0, true), TierRewardScale.AddBountyProgress(99, 1.0, 100));
            Assert.AreEqual((0.0, true), TierRewardScale.AddBountyProgress(0, 1.0, 1), "a one-kill bounty");
        }

        [TestMethod]
        public void AddBountyProgress_DipCountsDecimals()
        {
            var (kills, done) = TierRewardScale.AddBountyProgress(0.85, 0.85, 100);
            Assert.AreEqual(1.7, kills, 1e-9);
            Assert.IsFalse(done);

            // 100 kills one tier down at 0.85: 85 kills of progress, no drift
            double k = 0;
            var completions = 0;
            for (var i = 0; i < 100; i++)
            {
                (k, done) = TierRewardScale.AddBountyProgress(k, 0.85, 1000);
                if (done) completions++;
            }
            Assert.AreEqual(85.0, k, 1e-6);
            Assert.AreEqual(0, completions);
        }

        [TestMethod]
        public void AddBountyProgress_CompletesAtTargetAndDropsOverflow()
        {
            Assert.AreEqual((0.0, true), TierRewardScale.AddBountyProgress(99.5, 0.7, 100), "overflow 0.2 dropped");
            Assert.AreEqual((0.0, true), TierRewardScale.AddBountyProgress(99.85, 0.15, 100), "a hair under the target in floating point still completes");
            Assert.AreEqual((5.0, false), TierRewardScale.AddBountyProgress(5, double.NaN, 100));
            Assert.AreEqual((0.0, false), TierRewardScale.AddBountyProgress(double.NaN, 0, 100), "a bad saved count starts at 0");
        }

        [TestMethod]
        public void FormatProgress_ShowsDecimals()
        {
            Assert.AreEqual("4.5", TierRewardScale.FormatProgress(4.5));
            Assert.AreEqual("4", TierRewardScale.FormatProgress(4));
            Assert.AreEqual("4.25", TierRewardScale.FormatProgress(4.25));
            Assert.AreEqual("0.51", TierRewardScale.FormatProgress(0.51));
            Assert.AreEqual("0", TierRewardScale.FormatProgress(double.NaN));
        }

        // -- XP / luminance hook --

        [TestMethod]
        public void Defaults_XpDipShipsOff()
        {
            Assert.IsFalse(ServerConfig.zc_tier_dip_xp_enabled.Default, "the XP / luminance dip is built but ships OFF");
        }

        [TestMethod]
        public void ScaleXp_NoPlayerOrNoTierLeavesXpAlone()
        {
            Assert.AreEqual(5_000_000_001L, TierRewardScale.ScaleXp(5_000_000_001L, null, 13));
            Assert.AreEqual(1234L, TierRewardScale.ScaleXp(1234L, null, 0));
        }

        private static (int, double) Round((int Count, double Fraction) r) => (r.Count, System.Math.Round(r.Fraction, 6));
    }
}
