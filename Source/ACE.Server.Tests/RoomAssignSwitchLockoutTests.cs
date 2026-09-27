using System;

using ACE.Server.Managers;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The Vaulted Dungeon switch lockout (owner 2026-09-26): leaving a dungeon bars the account from a chamber in a DIFFERENT
    /// one for room_assign_switch_lockout_seconds; the same dungeon is never barred. RoomAssignManager.SwitchLockWait is the
    /// rule itself, DungeonKey the identity of "one dungeon", SwitchSeconds the countdown players read.
    /// </summary>
    [TestClass]
    public class RoomAssignSwitchLockoutTests
    {
        private static readonly TimeSpan Lockout = TimeSpan.FromSeconds(60);
        private static readonly DateTime LeftAt = new DateTime(2026, 9, 26, 22, 0, 0, DateTimeKind.Utc);

        private static readonly string Idols = RoomAssignManager.DungeonKey(0x5649, 3);
        private static readonly string Cells = RoomAssignManager.DungeonKey(0x01C2, 3);

        [TestMethod]
        public void SameDungeon_IsNeverLocked()
        {
            Assert.IsFalse(RoomAssignManager.SwitchLockWait(Idols, LeftAt, Idols, Lockout, LeftAt, out var wait));
            Assert.AreEqual(TimeSpan.Zero, wait);
        }

        [TestMethod]
        public void DifferentDungeon_IsLockedUntilExactlyTheLockout()
        {
            Assert.IsTrue(RoomAssignManager.SwitchLockWait(Cells, LeftAt, Idols, Lockout, LeftAt.AddSeconds(15), out var wait));
            Assert.AreEqual(TimeSpan.FromSeconds(45), wait);

            Assert.IsTrue(RoomAssignManager.SwitchLockWait(Cells, LeftAt, Idols, Lockout, LeftAt + Lockout - TimeSpan.FromMilliseconds(1), out _),
                "a millisecond before the end it still holds");
            Assert.IsFalse(RoomAssignManager.SwitchLockWait(Cells, LeftAt, Idols, Lockout, LeftAt + Lockout, out wait),
                "over at exactly the lockout - the same boundary PurgeExpired prunes at");
            Assert.AreEqual(TimeSpan.Zero, wait);
        }

        [TestMethod]
        public void LockoutZero_IsOff()
        {
            Assert.IsFalse(RoomAssignManager.SwitchLockWait(Cells, LeftAt, Idols, TimeSpan.Zero, LeftAt, out _));
        }

        [TestMethod]
        public void NothingLeft_IsNotLocked()
        {
            Assert.IsFalse(RoomAssignManager.SwitchLockWait(null, LeftAt, Idols, Lockout, LeftAt, out _));
        }

        [TestMethod]
        public void DungeonKey_IsLandblockAndLayer()
        {
            Assert.AreNotEqual(RoomAssignManager.DungeonKey(0x5649, 3), RoomAssignManager.DungeonKey(0x5649, 5), "v3 and v5 of one landblock are different dungeons");
            Assert.AreNotEqual(RoomAssignManager.DungeonKey(0x5649, 3), RoomAssignManager.DungeonKey(0x01C2, 3));
            Assert.AreEqual(RoomAssignManager.DungeonKey(0x5649, null), RoomAssignManager.DungeonKey(0x5649, 0), "null and 0 are both the base layer");
        }

        [TestMethod]
        public void LockoutOver_IsTheSharedBoundary()
        {
            Assert.IsFalse(RoomAssignManager.LockoutOver(LeftAt, Lockout, LeftAt + Lockout - TimeSpan.FromMilliseconds(1)));
            Assert.IsTrue(RoomAssignManager.LockoutOver(LeftAt, Lockout, LeftAt + Lockout), "PurgeExpired prunes at exactly the lockout");
        }

        [TestMethod]
        public void RoomKey_BelongsToItsDungeon()
        {
            var key = RoomAssignManager.Room.KeyFor(3, 0x56490123);
            Assert.AreEqual(RoomAssignManager.DungeonKey(0x5649, 3), RoomAssignManager.DungeonOfRoomKey(key));
            Assert.AreNotEqual(RoomAssignManager.DungeonKey(0x5649, 5), RoomAssignManager.DungeonOfRoomKey(key));
            Assert.IsNull(RoomAssignManager.DungeonOfRoomKey("garbage"));
        }

        [TestMethod]
        public void Refusal_SaysSecondOrSeconds_AndWhere()
        {
            StringAssert.Contains(RoomAssignManager.SwitchLockedText("Wailing Cells", TimeSpan.FromMilliseconds(300), false), "in 1 second.");
            StringAssert.Contains(RoomAssignManager.SwitchLockedText("Wailing Cells", TimeSpan.FromSeconds(42), false), "in 42 seconds.");
            StringAssert.StartsWith(RoomAssignManager.SwitchLockedText("Wailing Cells", TimeSpan.FromSeconds(60), true), "You are still in Wailing Cells.");
        }

        [TestMethod]
        public void Countdown_RoundsUp_AndNeverShowsZero()
        {
            Assert.AreEqual(1, RoomAssignManager.SwitchSeconds(TimeSpan.FromMilliseconds(1)));
            Assert.AreEqual(1, RoomAssignManager.SwitchSeconds(TimeSpan.Zero));
            Assert.AreEqual(45, RoomAssignManager.SwitchSeconds(TimeSpan.FromSeconds(44.2)));
            Assert.AreEqual(60, RoomAssignManager.SwitchSeconds(TimeSpan.FromSeconds(60)));
        }
    }
}
