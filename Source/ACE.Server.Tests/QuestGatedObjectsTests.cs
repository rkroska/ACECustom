using System;

using ACE.Server.WorldObjects;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using WorldQuest = ACE.Database.Models.World.Quest;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Quest-gated objects (PropertyString.VisibleWithQuest, Player_QuestVisibility.cs): pins Player.QuestGateOpen, the rule
    /// for whether a player is sent such an object. Open while the stamp's timer runs; a quest with min_Delta 0 is open while
    /// the stamp is held; closed without the stamp, without a quest row, after the timer ran out, and at max solves.
    /// </summary>
    [TestClass]
    public class QuestGatedObjectsTests
    {
        private static WorldQuest Quest(uint minDelta, int maxSolves = -1) =>
            new WorldQuest { Name = "ShoushiCourierBells", MinDelta = minDelta, MaxSolves = maxSolves };

        [TestMethod]
        public void TimerRunning_IsOpen()
        {
            Assert.IsTrue(Player.QuestGateOpen(true, Quest(900), TimeSpan.FromSeconds(600)));
        }

        [TestMethod]
        public void TimerRanOut_IsClosed()
        {
            Assert.IsFalse(Player.QuestGateOpen(true, Quest(900), TimeSpan.MinValue));
        }

        [TestMethod]
        public void NoStamp_IsClosed()
        {
            Assert.IsFalse(Player.QuestGateOpen(false, Quest(900), TimeSpan.FromSeconds(600)));
            Assert.IsFalse(Player.QuestGateOpen(false, Quest(0), TimeSpan.MinValue));
        }

        [TestMethod]
        public void NoQuestRow_IsClosed()
        {
            Assert.IsFalse(Player.QuestGateOpen(true, null, TimeSpan.FromSeconds(600)));
        }

        [TestMethod]
        public void MaxSolvesReached_IsClosed()
        {
            Assert.IsFalse(Player.QuestGateOpen(true, Quest(900, 1), TimeSpan.MaxValue));
        }

        [TestMethod]
        public void NoTimer_OpenWhileHeld()
        {
            Assert.IsTrue(Player.QuestGateOpen(true, Quest(0), TimeSpan.MinValue));
        }
    }
}
