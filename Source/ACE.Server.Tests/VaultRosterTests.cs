using System.Collections.Generic;
using System.Linq;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Command.Handlers;
using ACE.Server.Managers;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// /vault (owner 2026-09-27): the reply's wording - the count line, one bulleted line per name, at most 50 names then
    /// "...and N more." - who is left out (staff: Sentinel and up, cloaked, "+" characters), what counts as the same
    /// dungeon, and the 30 second countdown.
    /// </summary>
    [TestClass]
    public class VaultRosterTests
    {
        private static Position At(uint cell, int? variation) => new Position(cell, 0, 0, 0, 0, 0, 0, 1, VariationId: variation);

        [TestMethod]
        public void NoOneElse_SaysSo()
        {
            var lines = RoomAssignManager.RosterLines("Chamber of Idols", new List<string>());
            CollectionAssert.AreEqual(new[] { "Chamber of Idols - no other players." }, lines);

            CollectionAssert.AreEqual(lines, RoomAssignManager.RosterLines("Chamber of Idols", null), "null is the same as nobody");
        }

        [TestMethod]
        public void OnePlayer_IsSingular()
        {
            var lines = RoomAssignManager.RosterLines("Wailing Cells", new List<string> { "Somebody" });
            CollectionAssert.AreEqual(new[] { "Wailing Cells - 1 other player:", "  - Somebody" }, lines);
        }

        [TestMethod]
        public void SeveralPlayers_CountThenOneLineEach()
        {
            var lines = RoomAssignManager.RosterLines("Tusgian Barracks", new List<string> { "Alpha", "Bravo", "Charlie" });
            CollectionAssert.AreEqual(new[] { "Tusgian Barracks - 3 other players:", "  - Alpha", "  - Bravo", "  - Charlie" }, lines);
        }

        [TestMethod]
        public void ExactlyTheCap_ListsEveryone_NoMoreLine()
        {
            var cap = RoomAssignManager.MaxRosterNames;
            var names = Enumerable.Range(1, cap).Select(i => "P" + i).ToList();
            var lines = RoomAssignManager.RosterLines("Quarry", names);

            Assert.AreEqual($"Quarry - {cap} other players:", lines[0]);
            Assert.AreEqual(1 + cap, lines.Count);
            Assert.IsFalse(lines.Any(l => l.Contains("more")));
        }

        [TestMethod]
        public void OverTheCap_ShowsTheCapThenTheRest_CountIsEveryone()
        {
            Assert.AreEqual(50, RoomAssignManager.MaxRosterNames, "the owner's number (2026-09-27)");

            var names = Enumerable.Range(1, 70).Select(i => "P" + i).ToList();
            var lines = RoomAssignManager.RosterLines("Quarry", names);

            Assert.AreEqual("Quarry - 70 other players:", lines[0]);
            Assert.AreEqual(1 + 50 + 1, lines.Count, "the count line, 50 names, the 'more' line");
            Assert.AreEqual("  - P50", lines[50]);
            Assert.AreEqual("  ...and 20 more.", lines[51]);
        }

        [TestMethod]
        public void Staff_AreLeftOut_PlayersAndAdvocatesAreNot()
        {
            Assert.IsFalse(RoomAssignManager.IsLeftOutOfRoster(AccessLevel.Player, CloakStatus.Undef, plussed: false));
            Assert.IsFalse(RoomAssignManager.IsLeftOutOfRoster(AccessLevel.Player, CloakStatus.Off, plussed: false));
            Assert.IsFalse(RoomAssignManager.IsLeftOutOfRoster(AccessLevel.Advocate, CloakStatus.Undef, plussed: false), "an Advocate is a player here, as in Zone Share");

            Assert.IsTrue(RoomAssignManager.IsLeftOutOfRoster(AccessLevel.Sentinel, CloakStatus.Off, plussed: false));
            Assert.IsTrue(RoomAssignManager.IsLeftOutOfRoster(AccessLevel.Envoy, CloakStatus.Off, plussed: false));
            Assert.IsTrue(RoomAssignManager.IsLeftOutOfRoster(AccessLevel.Developer, CloakStatus.Off, plussed: false));
            Assert.IsTrue(RoomAssignManager.IsLeftOutOfRoster(AccessLevel.Admin, CloakStatus.Off, plussed: false));
        }

        [TestMethod]
        public void PlusCharacter_IsLeftOut_EvenOnAPlayerAccount()
        {
            Assert.IsTrue(RoomAssignManager.IsLeftOutOfRoster(AccessLevel.Player, CloakStatus.Off, plussed: true));
        }

        [TestMethod]
        public void Cloaked_IsLeftOut_OtherCloakModesAreNot()
        {
            Assert.IsTrue(RoomAssignManager.IsLeftOutOfRoster(AccessLevel.Player, CloakStatus.On, plussed: false));
            Assert.IsTrue(RoomAssignManager.IsLeftOutOfRoster(AccessLevel.Player, CloakStatus.Ghost, plussed: false));

            Assert.IsFalse(RoomAssignManager.IsLeftOutOfRoster(AccessLevel.Player, CloakStatus.Player, plussed: false));
            Assert.IsFalse(RoomAssignManager.IsLeftOutOfRoster(AccessLevel.Player, CloakStatus.Creature, plussed: false));
            Assert.IsFalse(RoomAssignManager.IsLeftOutOfRoster(AccessLevel.Player, CloakStatus.Hybrid, plussed: false));
        }

        [TestMethod]
        public void SameDungeon_SameLandblockAndLayer_IndoorOnly()
        {
            var here = At(0x56490123, 3);

            Assert.IsTrue(RoomAssignManager.InSameDungeon(here, At(0x56490200, 3)), "another indoor cell of the same dungeon");
            Assert.IsFalse(RoomAssignManager.InSameDungeon(here, At(0x56480123, 3)), "another landblock");
            Assert.IsFalse(RoomAssignManager.InSameDungeon(here, At(0x56490123, 5)), "another layer");
            Assert.IsFalse(RoomAssignManager.InSameDungeon(here, At(0x56490123, null)), "the retail copy (base)");
            Assert.IsFalse(RoomAssignManager.InSameDungeon(here, At(0x56490001, 3)), "an outdoor cell of the landblock");
            Assert.IsFalse(RoomAssignManager.InSameDungeon(here, null), "no position");
        }

        [TestMethod]
        public void Countdown_ThirtySeconds_RoundsUp_ZeroOnceOver()
        {
            Assert.AreEqual(30, VaultCommands.WaitSeconds(1_000, 1_000), "just used: the whole 30 seconds");
            Assert.AreEqual(1, VaultCommands.WaitSeconds(0, VaultCommands.CooldownMs - 1), "a millisecond left still says 1");
            Assert.AreEqual(0, VaultCommands.WaitSeconds(0, VaultCommands.CooldownMs), "over at exactly 30 seconds");
            Assert.AreEqual(0, VaultCommands.WaitSeconds(0, VaultCommands.CooldownMs * 10));
        }
    }
}
