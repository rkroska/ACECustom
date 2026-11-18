using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using ACE.Database.Models.World;
using ACE.Server.Managers.TieredDungeons;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The pure parts of TieredDungeonRegistry: tier list parsing, quest / event name tags, the per-thread tier scope,
    /// room math, pay rules and the tier-copy row builder. Nothing here touches ServerConfig, a database or a world object.
    /// </summary>
    [TestClass]
    public class TieredDungeonRegistryTests
    {
        private static readonly TieredDungeon Colo = TieredDungeonRegistry.Colosseum;

        private static int[] Sorted(HashSet<int> s) => s.OrderBy(x => x).ToArray();

        // ------------------------------------------------------------------ ParseTiers

        [TestMethod]
        public void ParseTiers_EmptyAndNull_AreEmpty()
        {
            Assert.AreEqual(0, TieredDungeonRegistry.ParseTiers("").Count);
            Assert.AreEqual(0, TieredDungeonRegistry.ParseTiers(null).Count);
            Assert.AreEqual(0, TieredDungeonRegistry.ParseTiers("   ").Count);
        }

        [TestMethod]
        public void ParseTiers_Single()
        {
            CollectionAssert.AreEqual(new[] { 11 }, Sorted(TieredDungeonRegistry.ParseTiers("11")));
        }

        [TestMethod]
        public void ParseTiers_List()
        {
            CollectionAssert.AreEqual(new[] { 11, 12 }, Sorted(TieredDungeonRegistry.ParseTiers("11,12")));
        }

        [TestMethod]
        public void ParseTiers_Range()
        {
            CollectionAssert.AreEqual(new[] { 11, 12, 13, 14, 15 }, Sorted(TieredDungeonRegistry.ParseTiers("11-15")));
        }

        [TestMethod]
        public void ParseTiers_MixWithSpaces()
        {
            CollectionAssert.AreEqual(new[] { 11, 13, 14, 25 }, Sorted(TieredDungeonRegistry.ParseTiers("11, 13-14 ,25")));
        }

        [TestMethod]
        public void ParseTiers_OutOfRange_IsDroppedOrClamped()
        {
            Assert.AreEqual(0, TieredDungeonRegistry.ParseTiers("5").Count);
            Assert.AreEqual(0, TieredDungeonRegistry.ParseTiers("26").Count);
            CollectionAssert.AreEqual(new[] { 11, 12 }, Sorted(TieredDungeonRegistry.ParseTiers("9-12")));
            CollectionAssert.AreEqual(new[] { 24, 25 }, Sorted(TieredDungeonRegistry.ParseTiers("24-40")));
        }

        [TestMethod]
        public void ParseTiers_JunkTokens_AreIgnored()
        {
            Assert.AreEqual(0, TieredDungeonRegistry.ParseTiers("abc").Count);
            Assert.AreEqual(0, TieredDungeonRegistry.ParseTiers("12-").Count);
            Assert.AreEqual(0, TieredDungeonRegistry.ParseTiers("-").Count);
            CollectionAssert.AreEqual(new[] { 12 }, Sorted(TieredDungeonRegistry.ParseTiers("abc,12,-,12-")));
        }

        // ------------------------------------------------------------------ Tag

        [TestMethod]
        public void Tag_PlainName()
        {
            Assert.AreEqual("ColoArenaOneAccess~v11", TieredDungeonRegistry.Tag("ColoArenaOneAccess", 11));
        }

        [TestMethod]
        public void Tag_GoesBeforeTheAtComment()
        {
            Assert.AreEqual("ColoArenaOneAccess~v11@UseFellow", TieredDungeonRegistry.Tag("ColoArenaOneAccess@UseFellow", 11));
        }

        [TestMethod]
        public void Tag_IsIdempotent()
        {
            var once = TieredDungeonRegistry.Tag("Foo@bar", 11);
            Assert.AreEqual(once, TieredDungeonRegistry.Tag(once, 11));
            Assert.AreEqual(once, TieredDungeonRegistry.Tag(once, 12), "an already tagged name keeps its tier");
            var plain = TieredDungeonRegistry.Tag("Foo", 13);
            Assert.AreEqual("Foo~v13", TieredDungeonRegistry.Tag(plain, 14));
        }

        [TestMethod]
        public void Tag_BelowMinTier_IsUnchanged()
        {
            Assert.AreEqual("Foo", TieredDungeonRegistry.Tag("Foo", 10));
            Assert.AreEqual("Foo@bar", TieredDungeonRegistry.Tag("Foo@bar", 0));
            Assert.AreEqual("Foo", TieredDungeonRegistry.Tag("Foo", -5));
        }

        [TestMethod]
        public void Tag_NullAndEmpty_AreReturnedAsIs()
        {
            Assert.IsNull(TieredDungeonRegistry.Tag(null, 11));
            Assert.AreEqual("", TieredDungeonRegistry.Tag("", 11));
        }

        [TestMethod]
        public void Tag_ExemptName_StaysUntagged()
        {
            Assert.AreEqual(TieredDungeonRegistry.PayTimerQuest, TieredDungeonRegistry.Tag(TieredDungeonRegistry.PayTimerQuest, 11));
            var withComment = TieredDungeonRegistry.PayTimerQuest + "@Stamp";
            Assert.AreEqual(withComment, TieredDungeonRegistry.Tag(withComment, 11));
        }

        [TestMethod]
        public void Tag_NameContainingTildeVThenNonDigits_IsStillTagged()
        {
            Assert.AreEqual("Foo~vX~v11", TieredDungeonRegistry.Tag("Foo~vX", 11));
            Assert.AreEqual("Foo~vX~v11@c", TieredDungeonRegistry.Tag("Foo~vX@c", 11));
        }

        // ------------------------------------------------------------------ StripTag

        [TestMethod]
        public void StripTag_PlainAndComment()
        {
            Assert.AreEqual("Foo", TieredDungeonRegistry.StripTag("Foo~v11"));
            Assert.AreEqual("ColoArenaOneAccess@UseFellow", TieredDungeonRegistry.StripTag("ColoArenaOneAccess~v11@UseFellow"));
            Assert.AreEqual("Foo", TieredDungeonRegistry.StripTag("Foo~v25"));
        }

        [TestMethod]
        public void StripTag_UntaggedNames_AreUnchanged()
        {
            Assert.AreEqual("Foo", TieredDungeonRegistry.StripTag("Foo"));
            Assert.AreEqual("Foo@bar", TieredDungeonRegistry.StripTag("Foo@bar"));
            Assert.AreEqual("Foo~vX", TieredDungeonRegistry.StripTag("Foo~vX"));
            Assert.AreEqual("Foo@bar~v11", TieredDungeonRegistry.StripTag("Foo@bar~v11"), "a tag after the @ is part of the comment");
        }

        [TestMethod]
        public void StripTag_NullAndEmpty()
        {
            Assert.IsNull(TieredDungeonRegistry.StripTag(null));
            Assert.AreEqual("", TieredDungeonRegistry.StripTag(""));
        }

        [TestMethod]
        public void StripTag_RoundTrip()
        {
            foreach (var name in new[] { "Foo", "ColoArenaOneAccess@UseFellow", "A@b@c", "TieredColoRooms", "X@", "Foo~vX" })
                Assert.AreEqual(name, TieredDungeonRegistry.StripTag(TieredDungeonRegistry.Tag(name, 11)), name);
        }

        // ------------------------------------------------------------------ IsTagged

        [TestMethod]
        public void IsTagged_Cases()
        {
            Assert.IsTrue(TieredDungeonRegistry.IsTagged("Foo~v11"));
            Assert.IsTrue(TieredDungeonRegistry.IsTagged("Foo~v11@bar"));
            Assert.IsTrue(TieredDungeonRegistry.IsTagged("Foo~v25"));
            Assert.IsFalse(TieredDungeonRegistry.IsTagged("Foo"));
            Assert.IsFalse(TieredDungeonRegistry.IsTagged("Foo@bar"));
            Assert.IsFalse(TieredDungeonRegistry.IsTagged("Foo~vX"));
            Assert.IsFalse(TieredDungeonRegistry.IsTagged("Foo~v"));
            Assert.IsFalse(TieredDungeonRegistry.IsTagged(null));
            Assert.IsFalse(TieredDungeonRegistry.IsTagged(""));
        }

        [TestMethod]
        public void IsTagged_TagOnlyAfterAt_IsNotTagged()
        {
            Assert.IsFalse(TieredDungeonRegistry.IsTagged("Foo@bar~v11"));
        }

        [TestMethod]
        public void IsTagged_AgreesWithTag()
        {
            foreach (var name in new[] { "Foo", "Foo@bar", "ColoArenaOneAccess@UseFellow" })
            {
                Assert.IsFalse(TieredDungeonRegistry.IsTagged(name));
                Assert.IsTrue(TieredDungeonRegistry.IsTagged(TieredDungeonRegistry.Tag(name, 11)));
            }
            Assert.IsFalse(TieredDungeonRegistry.IsTagged(TieredDungeonRegistry.Tag(TieredDungeonRegistry.PayTimerQuest, 11)));
        }

        // ------------------------------------------------------------------ Scope

        [TestMethod]
        public void Scope_TagsInsideAndNotOutside()
        {
            Assert.AreEqual(0, TieredDungeonRegistry.ScopeTier);
            Assert.AreEqual("Foo@bar", TieredDungeonRegistry.ApplyScope("Foo@bar"));
            using (TieredDungeonRegistry.EnterScopeForTest(11))
            {
                Assert.AreEqual(11, TieredDungeonRegistry.ScopeTier);
                Assert.AreEqual("Foo~v11@bar", TieredDungeonRegistry.ApplyScope("Foo@bar"));
                Assert.AreEqual(TieredDungeonRegistry.PayTimerQuest, TieredDungeonRegistry.ApplyScope(TieredDungeonRegistry.PayTimerQuest));
            }
            Assert.AreEqual(0, TieredDungeonRegistry.ScopeTier);
            Assert.AreEqual("Foo@bar", TieredDungeonRegistry.ApplyScope("Foo@bar"));
        }

        [TestMethod]
        public void Scope_Nested_RestoresOuterTier()
        {
            using (TieredDungeonRegistry.EnterScopeForTest(11))
            {
                using (TieredDungeonRegistry.EnterScopeForTest(13))
                {
                    Assert.AreEqual(13, TieredDungeonRegistry.ScopeTier);
                    Assert.AreEqual("Foo~v13", TieredDungeonRegistry.ApplyScope("Foo"));
                }
                Assert.AreEqual(11, TieredDungeonRegistry.ScopeTier);
                Assert.AreEqual("Foo~v11", TieredDungeonRegistry.ApplyScope("Foo"));
            }
            Assert.AreEqual(0, TieredDungeonRegistry.ScopeTier);
        }

        [TestMethod]
        public void Scope_ZeroInsideTier_ClearsTaggingAndRestores()
        {
            using (TieredDungeonRegistry.EnterScopeForTest(11))
            {
                using (TieredDungeonRegistry.EnterScopeForTest(0))
                {
                    Assert.AreEqual(0, TieredDungeonRegistry.ScopeTier);
                    Assert.AreEqual("Foo", TieredDungeonRegistry.ApplyScope("Foo"));
                }
                Assert.AreEqual(11, TieredDungeonRegistry.ScopeTier);
                Assert.AreEqual("Foo~v11", TieredDungeonRegistry.ApplyScope("Foo"));
            }
        }

        [TestMethod]
        public void Scope_IsPerThread()
        {
            int otherTier = -1;
            string otherName = null;
            using (TieredDungeonRegistry.EnterScopeForTest(11))
            {
                var t = new Thread(() =>
                {
                    otherTier = TieredDungeonRegistry.ScopeTier;
                    otherName = TieredDungeonRegistry.ApplyScope("Foo");
                });
                t.Start();
                t.Join();
                Assert.AreEqual(11, TieredDungeonRegistry.ScopeTier, "the test thread keeps its own scope");
            }
            Assert.AreEqual(0, otherTier);
            Assert.AreEqual("Foo", otherName);
        }

        // ------------------------------------------------------------------ RoomAt

        [TestMethod]
        public void RoomAt_RoomCentres()
        {
            Assert.AreEqual(1, TieredDungeonRegistry.RoomAt(Colo, -1560f));
            Assert.AreEqual(6, TieredDungeonRegistry.RoomAt(Colo, -1560f + 90 * 5));
            Assert.AreEqual(18, TieredDungeonRegistry.RoomAt(Colo, -1560f + 90 * 17));
        }

        [TestMethod]
        public void RoomAt_RoundsToNearestRoom()
        {
            Assert.AreEqual(6, TieredDungeonRegistry.RoomAt(Colo, -1560f + 90 * 5 + 40f));
            Assert.AreEqual(6, TieredDungeonRegistry.RoomAt(Colo, -1560f + 90 * 5 - 40f));
            Assert.AreEqual(1, TieredDungeonRegistry.RoomAt(Colo, -1560f + 40f));
            Assert.AreEqual(18, TieredDungeonRegistry.RoomAt(Colo, -1560f + 90 * 17 - 40f));
        }

        [TestMethod]
        public void RoomAt_OutOfRange_IsZero()
        {
            Assert.AreEqual(0, TieredDungeonRegistry.RoomAt(Colo, -5000f));
            Assert.AreEqual(0, TieredDungeonRegistry.RoomAt(Colo, 5000f));
            Assert.AreEqual(0, TieredDungeonRegistry.RoomAt(Colo, -1560f - 90f));
            Assert.AreEqual(0, TieredDungeonRegistry.RoomAt(Colo, -1560f + 90 * 18));
        }

        // ------------------------------------------------------------------ FurthestRoom

        private static int Bits(params int[] rooms) => rooms.Aggregate(0, (acc, r) => acc | (1 << (r - 1)));

        [TestMethod]
        public void FurthestRoom_Cases()
        {
            Assert.AreEqual(0, TieredDungeonRegistry.FurthestRoom(0, 6, 18));
            Assert.AreEqual(18, TieredDungeonRegistry.FurthestRoom(Bits(Enumerable.Range(6, 13).ToArray()), 6, 18));
            Assert.AreEqual(12, TieredDungeonRegistry.FurthestRoom(Bits(Enumerable.Range(6, 7).ToArray()), 6, 18));
            Assert.AreEqual(7, TieredDungeonRegistry.FurthestRoom(Bits(6, 7, 9), 6, 18));
            Assert.AreEqual(0, TieredDungeonRegistry.FurthestRoom(Bits(17, 18), 6, 18));
            Assert.AreEqual(0, TieredDungeonRegistry.FurthestRoom(Bits(1, 2, 3, 4, 5), 6, 18));
        }

        // ------------------------------------------------------------------ CoinsForRoom

        private const string CoinTable = "13:1,14:1,15:2,16:3,17:4,18:5";

        [TestMethod]
        public void CoinsForRoom_Table()
        {
            var expected = new[] { 0, 1, 1, 2, 3, 4, 5 };
            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], TieredDungeonRegistry.CoinsForRoom(CoinTable, 12 + i), "room " + (12 + i));
        }

        [TestMethod]
        public void CoinsForRoom_ZeroNegativeAndEmpty()
        {
            Assert.AreEqual(0, TieredDungeonRegistry.CoinsForRoom(CoinTable, 0));
            Assert.AreEqual(0, TieredDungeonRegistry.CoinsForRoom(CoinTable, -3));
            Assert.AreEqual(0, TieredDungeonRegistry.CoinsForRoom("", 15));
            Assert.AreEqual(0, TieredDungeonRegistry.CoinsForRoom(null, 15));
        }

        [TestMethod]
        public void CoinsForRoom_JunkTokensIgnored()
        {
            Assert.AreEqual(2, TieredDungeonRegistry.CoinsForRoom("junk,15:2,x:y,:5,16:", 15));
            Assert.AreEqual(0, TieredDungeonRegistry.CoinsForRoom("junk,15:2,x:y,:5,16:", 16));
        }

        // ------------------------------------------------------------------ PayPercent / ScaleCoins

        [TestMethod]
        public void PayPercent_Rule()
        {
            const string p = "100,60,20";
            Assert.AreEqual(100, TieredDungeonRegistry.PayPercent(p, 11, 11, 11));
            Assert.AreEqual(100, TieredDungeonRegistry.PayPercent(p, 11, 11, 12));
            Assert.AreEqual(60, TieredDungeonRegistry.PayPercent(p, 11, 12, 12));
            Assert.AreEqual(60, TieredDungeonRegistry.PayPercent(p, 11, 11, 13));
            Assert.AreEqual(20, TieredDungeonRegistry.PayPercent(p, 11, 13, 13));
            Assert.AreEqual(20, TieredDungeonRegistry.PayPercent(p, 11, 11, 14));
            Assert.AreEqual(0, TieredDungeonRegistry.PayPercent(p, 11, 14, 14));
            Assert.AreEqual(0, TieredDungeonRegistry.PayPercent(p, 11, 11, 15));
            Assert.AreEqual(100, TieredDungeonRegistry.PayPercent(p, 13, 13, 14));
            Assert.AreEqual(60, TieredDungeonRegistry.PayPercent(p, 13, 14, 14));
            Assert.AreEqual(0, TieredDungeonRegistry.PayPercent(p, 11, 10, 11));
            Assert.AreEqual(100, TieredDungeonRegistry.PayPercent(p, 11, 11, 0));
        }

        [TestMethod]
        public void ScaleCoins_RoundsDown()
        {
            Assert.AreEqual(5, TieredDungeonRegistry.ScaleCoins(5, 100));
            Assert.AreEqual(3, TieredDungeonRegistry.ScaleCoins(5, 60));
            Assert.AreEqual(1, TieredDungeonRegistry.ScaleCoins(5, 20));
            Assert.AreEqual(2, TieredDungeonRegistry.ScaleCoins(4, 60));
            Assert.AreEqual(0, TieredDungeonRegistry.ScaleCoins(1, 60));
            Assert.AreEqual(0, TieredDungeonRegistry.ScaleCoins(5, 0));
        }

        // ------------------------------------------------------------------ BuildTierRows

        private static LandblockInstance Row(uint guid, uint wcid, int landblock, bool linkChild = false) => new LandblockInstance
        {
            Guid = guid,
            Landblock = landblock,
            WeenieClassId = wcid,
            ObjCellId = (uint)landblock << 16 | 0x0100u,
            OriginX = 1f, OriginY = 2f, OriginZ = 3f,
            AnglesW = 1f,
            IsLinkChild = linkChild,
        };

        private static void Link(LandblockInstance parent, LandblockInstance child)
            => parent.LandblockInstanceLink.Add(new LandblockInstanceLink { ParentGuid = parent.Guid, ChildGuid = child.Guid });

        private static Func<uint> Counter(uint start)
        {
            var next = start;
            return () => next++;
        }

        [TestMethod]
        public void BuildTierRows_Arena_NewUniqueGuids_RemappedLinks_NoMutation()
        {
            var parent = Row(1000, 71703, 0x00B0);
            var child1 = Row(1001, 5000, 0x00B0, true);
            var child2 = Row(1002, 5001, 0x00B0, true);
            var other = Row(1003, 7777, 0x00B0);
            Link(parent, child1);
            Link(parent, child2);
            var input = new List<LandblockInstance> { parent, child1, child2, other };
            var inputSnapshot = input.ToList();

            var output = TieredDungeonRegistry.BuildTierRows(Colo, 0x00B0, input, Counter(9000));

            Assert.AreEqual(4, output.Count);
            CollectionAssert.AreEqual(new uint[] { 71703, 5000, 5001, 7777 }, output.Select(r => r.WeenieClassId).ToArray());

            var oldGuids = new HashSet<uint> { 1000, 1001, 1002, 1003 };
            Assert.IsTrue(output.All(r => !oldGuids.Contains(r.Guid)), "every guid is replaced");
            Assert.AreEqual(output.Count, output.Select(r => r.Guid).Distinct().Count(), "no two rows share a guid");

            // links remapped to the new guids
            var newParent = output[0];
            var newChildGuids = new[] { output[1].Guid, output[2].Guid };
            Assert.AreEqual(2, newParent.LandblockInstanceLink.Count);
            Assert.IsTrue(newParent.LandblockInstanceLink.All(l => l.ParentGuid == newParent.Guid));
            CollectionAssert.AreEquivalent(newChildGuids, newParent.LandblockInstanceLink.Select(l => l.ChildGuid).ToArray());
            Assert.IsTrue(output[1].IsLinkChild);
            Assert.AreEqual(0, output[3].LandblockInstanceLink.Count);

            // the input is untouched
            CollectionAssert.AreEqual(inputSnapshot, input);
            Assert.AreEqual(1000u, parent.Guid);
            Assert.AreEqual(1001u, child1.Guid);
            Assert.AreEqual(2, parent.LandblockInstanceLink.Count);
            Assert.IsTrue(parent.LandblockInstanceLink.All(l => l.ParentGuid == 1000 && (l.ChildGuid == 1001 || l.ChildGuid == 1002)));
            Assert.AreEqual(71703u, parent.WeenieClassId);
        }

        [TestMethod]
        public void BuildTierRows_Lobby_DropsUnlisted_SwapsWcids_AddsExitPortal()
        {
            var gen = Row(2000, 28282, 0x00AF);
            var arbitrator = Row(2001, 34441, 0x00AF, true);
            var seller = Row(2002, 34442, 0x00AF, true);
            var dropped = Row(2003, 99999, 0x00AF, true);   // not on the keep list
            var teleporter = Row(2004, 34728, 0x00AF);
            var droppedLone = Row(2005, 34727, 0x00AF);     // room-1 teleporter, not kept
            Link(gen, arbitrator);
            Link(gen, seller);
            Link(gen, dropped);
            var input = new List<LandblockInstance> { gen, arbitrator, seller, dropped, teleporter, droppedLone };
            var inputSnapshot = input.ToList();

            var output = TieredDungeonRegistry.BuildTierRows(Colo, 0x00AF, input, Counter(8000));

            // 4 kept rows + exactly one exit portal
            Assert.AreEqual(5, output.Count);
            CollectionAssert.AreEqual(
                new uint[] { 28282, TieredDungeonRegistry.TierArbitratorWcid, TieredDungeonRegistry.TierTicketSellerWcid, 34728, TieredDungeonRegistry.TierExitPortalWcid },
                output.Select(r => r.WeenieClassId).ToArray());
            Assert.AreEqual(1, output.Count(r => r.WeenieClassId == TieredDungeonRegistry.TierExitPortalWcid));
            Assert.IsFalse(output.Any(r => r.WeenieClassId == 99999 || r.WeenieClassId == 34727));
            Assert.IsFalse(output.Any(r => r.WeenieClassId == 34441 || r.WeenieClassId == 34442), "retail wcids are swapped away");

            // the generator keeps links to kept children only, remapped
            var newGen = output[0];
            Assert.AreEqual(2, newGen.LandblockInstanceLink.Count);
            CollectionAssert.AreEquivalent(new[] { output[1].Guid, output[2].Guid }, newGen.LandblockInstanceLink.Select(l => l.ChildGuid).ToArray());
            Assert.IsTrue(newGen.LandblockInstanceLink.All(l => l.ParentGuid == newGen.Guid));

            // unique, fresh guids, including the extra portal row
            Assert.AreEqual(output.Count, output.Select(r => r.Guid).Distinct().Count());
            var oldGuids = new HashSet<uint> { 2000, 2001, 2002, 2003, 2004, 2005 };
            Assert.IsTrue(output.All(r => !oldGuids.Contains(r.Guid)));
            Assert.AreEqual(0x00AF, output[4].Landblock);

            // the input is untouched and the shared LobbyExtraRows template is not consumed
            CollectionAssert.AreEqual(inputSnapshot, input);
            Assert.AreEqual(3, gen.LandblockInstanceLink.Count);
            Assert.AreEqual(34441u, arbitrator.WeenieClassId);
            Assert.AreEqual(1, Colo.LobbyExtraRows.Count);
        }

        [TestMethod]
        public void BuildTierRows_Lobby_LinkToDroppedChildIsDropped()
        {
            var gen = Row(3000, 1154, 0x00AF);
            var droppedChild = Row(3001, 99999, 0x00AF, true);
            Link(gen, droppedChild);
            var output = TieredDungeonRegistry.BuildTierRows(Colo, 0x00AF, new List<LandblockInstance> { gen, droppedChild }, Counter(1));

            Assert.AreEqual(2, output.Count);   // generator + exit portal
            Assert.AreEqual(1154u, output[0].WeenieClassId);
            Assert.AreEqual(0, output[0].LandblockInstanceLink.Count);
        }

        [TestMethod]
        public void BuildTierRows_NonLobbyLandblock_GetsNoExtraRows()
        {
            var output = TieredDungeonRegistry.BuildTierRows(Colo, 0x00B1, new List<LandblockInstance> { Row(1, 1, 0x00B1) }, Counter(50));
            Assert.AreEqual(1, output.Count);
            Assert.AreNotEqual(TieredDungeonRegistry.TierExitPortalWcid, output[0].WeenieClassId);
        }

        // ------------------------------------------------------------------ ScaleForTier (pay tier step)

        private const string T11Table = "13:6,14:9,15:13,16:17,17:23,18:30";

        [TestMethod]
        public void ScaleForTier_T11IsTheTable()
        {
            Assert.AreEqual(30, TieredDungeonRegistry.ScaleForTier(30, 11, 1.2));
            Assert.AreEqual(6, TieredDungeonRegistry.ScaleForTier(6, 11, 1.2));
        }

        [TestMethod]
        public void ScaleForTier_FullClearPerTier_AtTheDefaultStep()
        {
            var full = TieredDungeonRegistry.CoinsForRoom(T11Table, 18);
            var expected = new[] { 30, 36, 43, 52, 62 };   // 30 x 1.2^(tier-11), halves up
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], TieredDungeonRegistry.ScaleForTier(full, 11 + i, 1.2), "tier " + (11 + i));
        }

        [TestMethod]
        public void ScaleForTier_RoundsHalvesUp()
        {
            Assert.AreEqual(9, TieredDungeonRegistry.ScaleForTier(6, 13, 1.2));   // 8.64
            Assert.AreEqual(3, TieredDungeonRegistry.ScaleForTier(2, 13, 1.25));  // 3.125
            Assert.AreEqual(2, TieredDungeonRegistry.ScaleForTier(1, 15, 1.2));   // 2.07
        }

        [TestMethod]
        public void ScaleForTier_BadStepReadsAsOne_AndZeroStaysZero()
        {
            Assert.AreEqual(30, TieredDungeonRegistry.ScaleForTier(30, 15, 0));
            Assert.AreEqual(30, TieredDungeonRegistry.ScaleForTier(30, 15, -2));
            Assert.AreEqual(30, TieredDungeonRegistry.ScaleForTier(30, 15, double.NaN));
            Assert.AreEqual(0, TieredDungeonRegistry.ScaleForTier(0, 15, 1.2));
            Assert.AreEqual(0, TieredDungeonRegistry.ScaleForTier(-5, 15, 1.2));
        }

        // ------------------------------------------------------------------ RoomRampMult

        [TestMethod]
        public void RoomRampMult_FirstRoomIsOne_LastRoomIsTheSetting()
        {
            Assert.AreEqual(1.0, TieredDungeonRegistry.RoomRampMult(Colo, 6, 1.8), 1e-9);
            Assert.AreEqual(1.8, TieredDungeonRegistry.RoomRampMult(Colo, 18, 1.8), 1e-9);
        }

        [TestMethod]
        public void RoomRampMult_StraightLine()
        {
            Assert.AreEqual(1.4, TieredDungeonRegistry.RoomRampMult(Colo, 12, 1.8), 1e-9);   // half way
            Assert.AreEqual(1.0 + 0.8 * 3 / 12.0, TieredDungeonRegistry.RoomRampMult(Colo, 9, 1.8), 1e-9);
            Assert.AreEqual(0.75, TieredDungeonRegistry.RoomRampMult(Colo, 12, 0.5), 1e-9);   // a ramp down is allowed
        }

        [TestMethod]
        public void RoomRampMult_RoomsBeforeTheRun_AndBadValues_AreOne()
        {
            Assert.AreEqual(1.0, TieredDungeonRegistry.RoomRampMult(Colo, 1, 1.8), 1e-9);
            Assert.AreEqual(1.0, TieredDungeonRegistry.RoomRampMult(Colo, 5, 1.8), 1e-9);
            Assert.AreEqual(1.0, TieredDungeonRegistry.RoomRampMult(Colo, 0, 1.8), 1e-9);
            Assert.AreEqual(1.0, TieredDungeonRegistry.RoomRampMult(Colo, 18, 0.05), 1e-9);
            Assert.AreEqual(1.0, TieredDungeonRegistry.RoomRampMult(Colo, 18, 11), 1e-9);
            Assert.AreEqual(1.0, TieredDungeonRegistry.RoomRampMult(Colo, 18, double.NaN), 1e-9);
            Assert.AreEqual(1.0, TieredDungeonRegistry.RoomRampMult(null, 18, 1.8), 1e-9);
        }

        [TestMethod]
        public void RoomRampMult_PastTheLastRoom_StaysAtTheLastRoom()
        {
            Assert.AreEqual(1.8, TieredDungeonRegistry.RoomRampMult(Colo, 25, 1.8), 1e-9);
        }

        // ------------------------------------------------------------------ RequiredTags (room participation)

        [TestMethod]
        public void RequiredTags_QuarterOfTheRoom_RoundedUp()
        {
            Assert.AreEqual(10, TieredDungeonRegistry.RequiredTags(40, 0.25));
            Assert.AreEqual(2, TieredDungeonRegistry.RequiredTags(6, 0.25));   // room 18: 1.5 -> 2
            Assert.AreEqual(1, TieredDungeonRegistry.RequiredTags(1, 0.25));
        }

        [TestMethod]
        public void RequiredTags_OffOrNoKills_IsZero()
        {
            Assert.AreEqual(0, TieredDungeonRegistry.RequiredTags(40, 0));
            Assert.AreEqual(0, TieredDungeonRegistry.RequiredTags(40, -1));
            Assert.AreEqual(0, TieredDungeonRegistry.RequiredTags(40, double.NaN));
            Assert.AreEqual(0, TieredDungeonRegistry.RequiredTags(0, 0.25));
        }

        [TestMethod]
        public void RequiredTags_ShareAboveOne_MeansEveryMonster()
        {
            Assert.AreEqual(40, TieredDungeonRegistry.RequiredTags(40, 3));
        }

        // ------------------------------------------------------------------ IsStaleTally (abandoned runs)

        [TestMethod]
        public void IsStaleTally_ActiveRoom_IsKept()
        {
            // kills seconds apart, room a few minutes old
            Assert.IsFalse(TieredDungeonRuns.IsStaleTally(1000, 1250, 1260));
            Assert.IsFalse(TieredDungeonRuns.IsStaleTally(1000, 1000, 1000 + TieredDungeonRuns.RoomIdleResetSeconds));
        }

        [TestMethod]
        public void IsStaleTally_AbandonedRun_StartsFresh()
        {
            // an abandoned room-6 run, the next group starting after the one-hour arena lock: still inside the
            // 65-minute run window, but the room sat idle far longer than any real gap between kills
            Assert.IsTrue(TieredDungeonRuns.IsStaleTally(1000, 1200, 1000 + 3640));
            Assert.IsTrue(TieredDungeonRuns.IsStaleTally(1000, 1000, 1000 + TieredDungeonRuns.RoomIdleResetSeconds + 1));
        }

        [TestMethod]
        public void IsStaleTally_PastTheRunWindow_StartsFresh()
        {
            Assert.IsTrue(TieredDungeonRuns.IsStaleTally(1000, 1000 + TieredDungeonRuns.RunWindowSeconds, 1001 + TieredDungeonRuns.RunWindowSeconds));
        }
    }
}
