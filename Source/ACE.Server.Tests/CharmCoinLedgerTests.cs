using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The levelled charm coin ledger (owner 2026-10-24): a charm's level comes from the Prestige Coins put into it and its
    /// price curve, cost of level L -> L+1 = coinbase x coingrowth^(L-1). These read the code defaults (coinbase 230,
    /// coingrowth 1.03) - nothing here changes a setting.
    /// </summary>
    [TestClass]
    public class CharmCoinLedgerTests
    {
        private static CharmSettingsManager.LeveledCharmBlock Block => CharmSettingsManager.ArmorBreaker;

        [TestMethod]
        public void Defaults_AreTheAgreedCurve()
        {
            foreach (var block in CharmSettingsManager.LeveledBlocks.Values)
            {
                Assert.AreEqual(230.0, block.CoinBase, 1e-9, block.Name);
                Assert.AreEqual(1.03, block.CoinGrowth, 1e-9, block.Name);
            }
        }

        [TestMethod]
        public void FirstLevels_CostWhatTheFormulaSays()
        {
            Assert.AreEqual(0, Block.CoinsToReach(1));
            Assert.AreEqual(230, Block.CoinsToReach(2));
            Assert.AreEqual(467, Block.CoinsToReach(3));   // 230 + 236.9, rounded up
            Assert.AreEqual(230, Block.CoinsForLevel(1));
            Assert.AreEqual(237, Block.CoinsForLevel(2));
            Assert.AreEqual(0, Block.CoinsForLevel(100));
        }

        [TestMethod]
        public void MaxLevel_TakesAbout135kCoins()
        {
            // 230 x (1.03^99 - 1) / 0.03 = ~135,700: ~6 months at 20 hours a day on the agreed income
            Assert.IsTrue(Block.MaxCoins > 130_000 && Block.MaxCoins < 140_000, Block.MaxCoins.ToString());
            Assert.AreEqual(Block.CoinsToReach(100), Block.MaxCoins);
        }

        [TestMethod]
        public void Thresholds_RiseEveryLevel_AndEachLevelCostsMore()
        {
            for (var level = 2; level <= 100; level++)
                Assert.IsTrue(Block.CoinsToReach(level) > Block.CoinsToReach(level - 1), $"level {level}");
            for (var level = 2; level < 100; level++)
                Assert.IsTrue(Block.CoinsForLevel(level) >= Block.CoinsForLevel(level - 1), $"level {level}");
        }

        [TestMethod]
        public void LevelFromCoins_IsExactAtEveryThreshold()
        {
            for (var level = 2; level <= 100; level++)
            {
                var at = Block.CoinsToReach(level);
                Assert.AreEqual(level, Block.LevelFromCoins(at), $"at {at}");
                Assert.AreEqual(level - 1, Block.LevelFromCoins(at - 1), $"at {at - 1}");
            }
            Assert.AreEqual(1, Block.LevelFromCoins(0));
            Assert.AreEqual(1, Block.LevelFromCoins(-5));
            Assert.AreEqual(100, Block.LevelFromCoins(long.MaxValue));
        }

        [TestMethod]
        public void ExactLevel_StaysInsideItsLevel()
        {
            for (long coins = 0; coins <= Block.MaxCoins; coins += 997)
            {
                var whole = Block.LevelFromCoins(coins);
                var exact = Block.ExactLevelFromCoins(coins);
                Assert.IsTrue(exact >= whole && exact < whole + 1, $"{coins} coins: {exact} vs {whole}");
            }
            Assert.AreEqual(100.0, Block.ExactLevelFromCoins(Block.MaxCoins), 1e-9);
        }

        [TestMethod]
        public void LevelText_ReadsForPlayers()
        {
            Assert.AreEqual("Level 1 (0% to 2)", Player.CharmLevelText(Block, 0));
            Assert.AreEqual("Level 1 (50% to 2)", Player.CharmLevelText(Block, 115));
            Assert.AreEqual("Level 2 (0% to 3)", Player.CharmLevelText(Block, 230));
            Assert.AreEqual("Level 100 (max)", Player.CharmLevelText(Block, Block.MaxCoins));
        }

        [TestMethod]
        public void InfusionPrompt_ShowsCoins_SoOneCoinIsVisible()
        {
            // level 5 starts at 963 coins, level 6 at 1,222, level 7 at 1,488
            Assert.AreEqual("Put 1 Prestige Coin into your Armor Breaker Charm?\n\nCoins: 963 -> 964\nLevel 5 (0% to 6), 258 more coins to level 6",
                Player.CharmInfusionPromptText(Block, 963, 1));
            Assert.AreEqual("Put 300 Prestige Coins into your Armor Breaker Charm?\n\nCoins: 963 -> 1,263\nLevel 5 -> Level 6 (15% to 7)",
                Player.CharmInfusionPromptText(Block, 963, 300));
            Assert.IsTrue(Player.CharmInfusionPromptText(Block, 1220, 1).EndsWith("Level 5 (99% to 6), 1 more coin to level 6"));
            Assert.IsTrue(Player.CharmInfusionPromptText(Block, Block.MaxCoins - 1, 1).EndsWith("Level 99 -> Level 100 (max)"));
        }

        [TestMethod]
        public void CurveKeys_AreTheTwoPriceSettings()
        {
            Assert.IsTrue(CharmSettingsManager.LeveledCharmBlock.IsCurveKey("coinbase"));
            Assert.IsTrue(CharmSettingsManager.LeveledCharmBlock.IsCurveKey("coingrowth"));
            Assert.IsFalse(CharmSettingsManager.LeveledCharmBlock.IsCurveKey("share"));
        }

        [TestMethod]
        public void EveryLevelledCharm_HasACoinProperty()
        {
            foreach (var block in CharmSettingsManager.LeveledBlocks.Values)
                Assert.IsNotNull(Player.CharmCoinsProperty(CharmSettingsManager.LeveledAbilityId(block.Name)), block.Name);
            Assert.AreEqual(PropertyInt64.SplitArrowCharmCoins, Player.CharmCoinsProperty(CharmAbilityRegistry.SplitArrowAbilityId));
            Assert.IsNull(Player.CharmCoinsProperty(CharmAbilityRegistry.ForkAbilityId));
        }
    }
}
