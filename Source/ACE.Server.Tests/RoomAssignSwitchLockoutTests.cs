using System;
using System.Collections.Generic;

using ACE.Entity;
using ACE.Server.Managers;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The Vaulted Dungeon switch lockout (owner 2026-09-26): leaving a dungeon bars the account from a chamber in a DIFFERENT
    /// one for room_assign_switch_lockout_seconds; the same dungeon is never barred. RoomAssignManager.SwitchLockWait is the
    /// rule for one departure, LongestSwitchWait over all of an account's departures (one record per dungeon, review
    /// 2026-09-27 round 3), DungeonKey the identity of "one dungeon", SwitchSeconds the countdown players read.
    /// </summary>
    [TestClass]
    public class RoomAssignSwitchLockoutTests
    {
        private static readonly TimeSpan Lockout = TimeSpan.FromSeconds(60);
        private static readonly DateTime LeftAt = new DateTime(2026, 9, 26, 22, 0, 0, DateTimeKind.Utc);

        private static readonly string Idols = RoomAssignManager.DungeonKey(0x5649, 3);
        private static readonly string Cells = RoomAssignManager.DungeonKey(0x01C2, 3);
        private static readonly string Quarry = RoomAssignManager.DungeonKey(0x01F7, 3);

        private static Dictionary<string, RoomAssignManager.LeftDungeon> Left(params (string Dungeon, int SecondsAfter)[] departures)
        {
            var left = new Dictionary<string, RoomAssignManager.LeftDungeon>();
            foreach (var d in departures)
                left[d.Dungeon] = new RoomAssignManager.LeftDungeon { SourceWcid = 1, At = LeftAt.AddSeconds(d.SecondsAfter) };
            return left;
        }

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

        /// <summary>The hop this round closed: A left, then B left 30 s later - going back into A is barred by B.</summary>
        [TestMethod]
        public void HopBack_AThenBThenA_IsLockedByB()
        {
            var left = Left((Idols, 0), (Cells, 30));

            Assert.AreEqual(Cells, RoomAssignManager.LongestSwitchWait(left, Idols, Lockout, LeftAt.AddSeconds(40), out var wait));
            Assert.AreEqual(TimeSpan.FromSeconds(50), wait);
        }

        /// <summary>A later departure never ENDS an earlier lockout: both run, and the longer wait is the one reported.</summary>
        [TestMethod]
        public void EveryDeparture_Counts_TheLongestWaitWins()
        {
            var left = Left((Idols, 0), (Cells, 50));

            Assert.AreEqual(Cells, RoomAssignManager.LongestSwitchWait(left, Quarry, Lockout, LeftAt.AddSeconds(55), out var wait));
            Assert.AreEqual(TimeSpan.FromSeconds(55), wait);

            // Back into Cells: Cells itself never bars it, but Idols still has 5 s to run.
            Assert.AreEqual(Idols, RoomAssignManager.LongestSwitchWait(left, Cells, Lockout, LeftAt.AddSeconds(55), out wait));
            Assert.AreEqual(TimeSpan.FromSeconds(5), wait);

            // Once Idols has run out, Cells is open again.
            Assert.IsNull(RoomAssignManager.LongestSwitchWait(left, Cells, Lockout, LeftAt + Lockout, out wait));
            Assert.AreEqual(TimeSpan.Zero, wait);
        }

        /// <summary>A trip that stays inside the same dungeon (same landblock and layer, an indoor cell) records no departure.</summary>
        [TestMethod]
        public void InDungeon_SameLandblockAndLayer_IndoorOnly()
        {
            Assert.IsTrue(RoomAssignManager.InDungeon(new Position(0x56490123, 0, 0, 0, 0, 0, 0, 1, VariationId: 3), Idols), "another cell of the same dungeon");
            Assert.IsFalse(RoomAssignManager.InDungeon(new Position(0x56480123, 0, 0, 0, 0, 0, 0, 1, VariationId: 3), Idols), "another landblock");
            Assert.IsFalse(RoomAssignManager.InDungeon(new Position(0x56490123, 0, 0, 0, 0, 0, 0, 1, VariationId: 5), Idols), "another layer");
            Assert.IsFalse(RoomAssignManager.InDungeon(new Position(0x56490001, 0, 0, 0, 0, 0, 0, 1, VariationId: 3), Idols), "an outdoor cell");
            Assert.IsFalse(RoomAssignManager.InDungeon(null, Idols), "no destination");
        }

        [TestMethod]
        public void NoDepartures_OrLockoutOff_IsNotLocked()
        {
            Assert.IsNull(RoomAssignManager.LongestSwitchWait(null, Idols, Lockout, LeftAt, out _));
            Assert.IsNull(RoomAssignManager.LongestSwitchWait(Left(), Idols, Lockout, LeftAt, out _));
            Assert.IsNull(RoomAssignManager.LongestSwitchWait(Left((Cells, 0)), Idols, TimeSpan.Zero, LeftAt, out _));
        }

        /// <summary>
        /// A parsed room's dungeon is its list's landblock and layer - by its anchor, the cell its key is built from. The
        /// parser keeps every cell (and so the landing, which must be one of them) on one landblock.
        /// </summary>
        [TestMethod]
        public void ParsedRoom_BelongsToItsListsDungeon()
        {
            Assert.IsTrue(RoomAssignManager.TryParseRooms("1|0x56490123 [10 20 0] 1 0 0 0|0x56490123,0x56490124", out var rooms, out var error), error);
            Assert.AreEqual(Idols, RoomAssignManager.DungeonKey(rooms[0], 3));
            Assert.AreNotEqual(RoomAssignManager.DungeonKey(0x5649, 5), RoomAssignManager.DungeonKey(rooms[0], 3));

            Assert.IsFalse(RoomAssignManager.TryParseRooms(
                "1|0x56490123 [10 20 0] 1 0 0 0|0x56490123;2|0x56480123 [10 20 0] 1 0 0 0|0x56480123", out _, out error),
                "a second room on another landblock is refused");
        }

        [TestMethod]
        public void Refusal_SaysSecondOrSeconds_AndWhere()
        {
            StringAssert.Contains(RoomAssignManager.SwitchLockedText("Wailing Cells", TimeSpan.FromMilliseconds(300), stillInside: false), "in 1 second.");
            StringAssert.Contains(RoomAssignManager.SwitchLockedText("Wailing Cells", TimeSpan.FromSeconds(42), stillInside: false), "in 42 seconds.");
            StringAssert.StartsWith(RoomAssignManager.SwitchLockedText("Wailing Cells", TimeSpan.FromSeconds(60), stillInside: true), "You are still in Wailing Cells.");
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
