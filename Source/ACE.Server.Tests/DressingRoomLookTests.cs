using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Entity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The Dressing Room rules that need no running server: what a stored look round-trips to, which saved pieces a new
    /// piece replaces, how the per-area lock counts grow, and what a piece costs.
    /// </summary>
    [TestClass]
    public class DressingRoomLookTests
    {
        // Wielded locations and coverage as real items carry them (values taken from the server log, 2026-10-06/07).
        private const uint HeadLoc = (uint)EquipMask.HeadWear;
        private const uint ChestLoc = (uint)EquipMask.ChestArmor;
        private const uint HauberkLoc = (uint)(EquipMask.ChestArmor | EquipMask.AbdomenArmor | EquipMask.UpperArmArmor | EquipMask.LowerArmArmor);
        private const uint BracersLoc = (uint)EquipMask.LowerArmArmor;
        private const uint ShirtLoc = 0xE;      // ChestWear | AbdomenWear | UpperArmWear
        private const uint TrousersLoc = 0x44;  // AbdomenWear | UpperLegWear  - shares AbdomenWear with the shirt
        private const uint BootsLoc = 0x180;    // LowerLegWear | FootWear

        private const uint HeadCov = (uint)CoverageMask.Head;
        private const uint ChestCov = (uint)CoverageMask.OuterwearChest;
        private const uint HauberkCov = (uint)(CoverageMask.OuterwearChest | CoverageMask.OuterwearAbdomen | CoverageMask.OuterwearUpperArms | CoverageMask.OuterwearLowerArms);
        private const uint UpperArmsCov = (uint)CoverageMask.OuterwearUpperArms;
        private const uint BracersCov = (uint)CoverageMask.OuterwearLowerArms;
        private const uint ShirtCov = 40;       // UnderwearChest | UnderwearUpperArms
        private const uint TrousersCov = 19;    // UnderwearUpperLegs | UnderwearAbdomen | Unknown
        private const uint BootsCov = (uint)CoverageMask.Feet;

        private static DressingRoomPiece Piece(string name, uint location, uint coverage) => new()
        {
            Wcid = 1234,
            Name = name,
            Guid = 0x80001234,
            Location = location,
            ItemType = (uint)ItemType.Armor,
            ClothingBase = 0x10000001,
            PaletteTemplate = 14,
            Shade = 0.25,
            ClothingPriority = coverage,
            VisualPriority = coverage,
            LockedAt = 1_760_000_000,
            Fee = 100,
        };

        private static DressingRoomPiece Helm(string name = "Helm") => Piece(name, HeadLoc, HeadCov);
        private static DressingRoomPiece Breastplate(string name = "Breastplate") => Piece(name, ChestLoc, ChestCov);
        private static DressingRoomPiece Hauberk(string name = "Hauberk") => Piece(name, HauberkLoc, HauberkCov);
        private static DressingRoomPiece Bracers(string name = "Bracers") => Piece(name, BracersLoc, BracersCov);
        private static DressingRoomPiece Shirt(string name = "Shirt") => Piece(name, ShirtLoc, ShirtCov);
        private static DressingRoomPiece Trousers(string name = "Trousers") => Piece(name, TrousersLoc, TrousersCov);
        private static DressingRoomPiece Boots(string name = "Boots") => Piece(name, BootsLoc, BootsCov);

        // ---- slot mask ------------------------------------------------------------------------

        [TestMethod]
        public void SlotMask_IsExactlyTheSlotsTheDrawCodeReads()
        {
            // Creature.CalculateObjDesc draws from Clothing | Armor | Cloak; 0x80000000 in Clothing is a flag, not a slot
            var drawn = (uint)(EquipMask.Clothing | EquipMask.Armor | EquipMask.Cloak) & 0x7FFFFFFF;
            Assert.AreEqual(DressingRoomLook.SlotMask, drawn);
        }

        [TestMethod]
        public void SlotMask_ExcludesWeaponsShieldsAndJewellery()
        {
            var never = EquipMask.MeleeWeapon | EquipMask.Shield | EquipMask.MissileWeapon | EquipMask.MissileAmmo | EquipMask.Held
                | EquipMask.TwoHanded | EquipMask.NeckWear | EquipMask.WristWearLeft | EquipMask.WristWearRight
                | EquipMask.FingerWearLeft | EquipMask.FingerWearRight | EquipMask.TrinketOne
                | EquipMask.SigilOne | EquipMask.SigilTwo | EquipMask.SigilThree;
            Assert.AreEqual(0u, (uint)never & DressingRoomLook.SlotMask);
        }

        [TestMethod]
        public void SlotBits_ListsEachSlotOnce_LowestFirst()
        {
            CollectionAssert.AreEqual(
                new[] { (uint)EquipMask.ChestArmor, (uint)EquipMask.AbdomenArmor, (uint)EquipMask.UpperArmArmor, (uint)EquipMask.LowerArmArmor },
                DressingRoomLook.SlotBits(HauberkLoc).ToArray());
            CollectionAssert.AreEqual(new[] { (uint)EquipMask.Cloak }, DressingRoomLook.SlotBits((uint)EquipMask.Cloak).ToArray());
            Assert.AreEqual(0, DressingRoomLook.SlotBits((uint)EquipMask.MeleeWeapon).Count());
        }

        // ---- which pieces conflict ------------------------------------------------------------

        [TestMethod]
        public void PiecesWornTogether_DoNotConflict_EvenWhenTheyShareAnEquipMaskBit()
        {
            // the bug of 2026-10-07: these pairs share an EquipMask bit and are worn together every day
            Assert.AreNotEqual(0u, ShirtLoc & TrousersLoc, "shirt and trousers both claim the waist");
            Assert.AreNotEqual(0u, 0xC4u & BootsLoc, "long trousers and boots both claim the lower leg");

            Assert.IsFalse(DressingRoomLook.Conflicts(Shirt(), Trousers()));
            Assert.IsFalse(DressingRoomLook.Conflicts(Piece("Long trousers", 0xC4, 22), Boots()));
            Assert.IsFalse(DressingRoomLook.Conflicts(Shirt(), Breastplate()), "underclothes go under armour");
        }

        [TestMethod]
        public void PiecesCoveringTheSameArea_Conflict()
        {
            Assert.IsTrue(DressingRoomLook.Conflicts(Hauberk(), Breastplate()));
            Assert.IsTrue(DressingRoomLook.Conflicts(Hauberk(), Bracers()));
            Assert.IsTrue(DressingRoomLook.Conflicts(Helm("a"), Helm("b")));
            Assert.IsFalse(DressingRoomLook.Conflicts(Helm(), Breastplate()));
        }

        [TestMethod]
        public void APieceWithNoCoverage_FallsBackToItsWieldedLocation()
        {
            var bare = Piece("No coverage", ChestLoc, 0);
            Assert.IsTrue(DressingRoomLook.Conflicts(bare, Breastplate()));
            Assert.IsTrue(DressingRoomLook.Conflicts(bare, Hauberk()));
            Assert.IsFalse(DressingRoomLook.Conflicts(bare, Helm()));
        }

        // ---- storage --------------------------------------------------------------------------

        [TestMethod]
        public void Look_RoundTrips()
        {
            var look = new DressingRoomLook().WithLocked(new[] { Hauberk("Diforsa Hauberk"), Helm("Olthoi Helm") });

            var back = DressingRoomLook.Parse(look.Serialize());

            Assert.IsNotNull(back);
            Assert.AreEqual(2, back.Pieces.Count);
            var hauberk = back.Pieces.Single(p => p.Name == "Diforsa Hauberk");
            Assert.AreEqual(HauberkLoc, hauberk.Location);
            Assert.AreEqual(HauberkCov, hauberk.ClothingPriority);
            Assert.AreEqual(0x10000001u, hauberk.ClothingBase);
            Assert.AreEqual(14, hauberk.PaletteTemplate);
            Assert.AreEqual(0.25, hauberk.Shade);
            Assert.IsNull(hauberk.TopLayer);
            Assert.AreEqual(1, back.PriorLocks(ChestCov));
            Assert.AreEqual(1, back.PriorLocks(HeadCov));
        }

        [TestMethod]
        public void APiecesForgeDye_RoundTrips_AndIsAbsentWhenUndyed()
        {
            var dyed = Breastplate("Dyed");
            dyed.ForgeDye = 0x04000478;
            var look = new DressingRoomLook().WithLocked(new[] { dyed, Helm("Plain") });

            var stored = look.Serialize();
            var back = DressingRoomLook.Parse(stored);

            Assert.AreEqual(0x04000478, back.Pieces.Single(p => p.Name == "Dyed").ForgeDye);
            Assert.IsNull(back.Pieces.Single(p => p.Name == "Plain").ForgeDye);
            Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(stored, "\"d\":").Count, "an undyed piece stores no dye field");
        }

        [TestMethod]
        public void AFullOutfit_WithShirtTrousersAndBoots_IsReadBackAfterSaving()
        {
            // exactly what an attendant writes for an ordinary outfit; it must always be readable again
            var look = new DressingRoomLook().WithLocked(new[] { Helm(), Shirt(), Trousers(), Boots(), Breastplate(), Bracers() });

            var back = DressingRoomLook.Parse(look.Serialize());

            Assert.IsNotNull(back, "a look the attendant just wrote could not be read");
            Assert.AreEqual(6, back.Pieces.Count);
        }

        [TestMethod]
        public void StoredLook_IsPlainAscii_EvenForANonAsciiName()
        {
            var look = new DressingRoomLook().WithLocked(new[] { Breastplate("Café — Robe") });
            var stored = look.Serialize();
            Assert.IsTrue(stored.All(c => c < 128), stored);
            Assert.AreEqual("Café — Robe", DressingRoomLook.Parse(stored).Pieces[0].Name);
        }

        [TestMethod]
        public void Parse_NeverThrows_AndRejectsAnythingThatIsNotALook()
        {
            Assert.IsNull(DressingRoomLook.Parse(null));
            Assert.IsNull(DressingRoomLook.Parse(""));
            Assert.IsNull(DressingRoomLook.Parse("   "));
            Assert.IsNull(DressingRoomLook.Parse("not json"));
            Assert.IsNull(DressingRoomLook.Parse("[1,2,3]"));
            Assert.IsNull(DressingRoomLook.Parse("null"));
            Assert.IsNull(DressingRoomLook.Parse("{\"v\":2,\"pieces\":[]}"), "a future version is not guessed at");
            Assert.IsNull(DressingRoomLook.Parse("{\"v\":1,\"pieces\":[null]}"));
            Assert.IsNull(DressingRoomLook.Parse("{\"v\":1,\"pieces\":[{\"l\":512,\"c\":0}]}"), "no clothing table");
            Assert.IsNull(DressingRoomLook.Parse("{\"v\":1,\"pieces\":[{\"l\":1048576,\"c\":268435457}]}"), "a weapon slot");
        }

        [TestMethod]
        public void Parse_DoesNotJudgeWhetherPiecesFitTogether()
        {
            // two pieces on the same slot: odd, but every piece is drawable, so the look is kept rather than thrown away
            var look = DressingRoomLook.Parse("{\"v\":1,\"pieces\":[{\"l\":512,\"c\":268435457,\"cp\":1024},{\"l\":1536,\"c\":268435457,\"cp\":3072}]}");
            Assert.IsNotNull(look);
            Assert.AreEqual(2, look.Pieces.Count);
        }

        [TestMethod]
        public void Parse_AcceptsAnEmptyLook_WithNullCollections()
        {
            var look = DressingRoomLook.Parse("{\"v\":1,\"pieces\":null,\"locks\":null}");
            Assert.IsNotNull(look);
            Assert.AreEqual(0, look.Pieces.Count);
            Assert.AreEqual(0, look.PriorLocks(ChestCov));
        }

        [TestMethod]
        public void Parse_RejectsMoreSavedPiecesThanThereAreSlots()
        {
            var pieces = string.Join(",", Enumerable.Range(0, DressingRoomLook.MaxPieces + 1).Select(_ => "{\"l\":512,\"c\":268435457}"));
            Assert.IsNull(DressingRoomLook.Parse("{\"v\":1,\"pieces\":[" + pieces + "]}"));
        }

        // ---- records written before 2026-10-07 --------------------------------------------------

        // The two records from the production log of 2026-10-07 18:49, verbatim. Both were refused as unreadable because a
        // shirt and trousers share an EquipMask bit; the players had already paid and lost the pieces.
        private const string GaryStored = """{"v":1,"pieces":[{"w":2595,"n":"Baggy Tunic","g":4026535031,"l":14,"t":4,"c":268435715,"p":10,"s":0.32456434827723013,"cp":40,"vp":40,"at":1791413359,"f":100000000},{"w":2603,"n":"Baggy Breeches","g":4026535032,"l":68,"t":4,"c":268435704,"p":18,"s":0.5116122928556169,"cp":19,"vp":19,"at":1791413359,"f":100000000},{"w":115,"n":"Leather Boots","g":4026535033,"l":384,"t":2,"c":268435463,"p":17,"s":0.9734183782464064,"cp":65536,"vp":66048,"at":1791413359,"f":100000000}],"locks":{"2":1,"4":2,"8":1,"40":1,"80":1,"100":1}}""";

        private const string LegolasStored = """{"v":1,"pieces":[{"w":5901,"n":"Empowered Helm of the Perfect Light","g":4026553840,"l":1,"t":4,"c":268437279,"cp":16384,"vp":16384,"at":1791413363,"f":100000000},{"w":2587,"n":"Lace Shirt","g":2152926613,"l":30,"t":4,"c":268436918,"p":90,"s":0.6596986286620138,"cp":104,"vp":104,"at":1791413363,"f":100000000},{"w":27222,"n":"Olthoi Gauntlets","g":4033237463,"l":32,"t":2,"c":268437270,"p":90,"s":0.8932576338053513,"cp":32768,"vp":32768,"at":1791413363,"f":100000000},{"w":28606,"n":"Viamontian Pants","g":4030506104,"l":196,"t":4,"c":268436914,"p":7,"s":0.3616772807956102,"cp":22,"vp":22,"at":1791413363,"f":100000000},{"w":28611,"n":"Sollerets of Grace","g":4035990823,"l":384,"t":2,"c":268436752,"p":20,"s":0.66,"cp":65536,"vp":65536,"at":1791413363,"f":100000000},{"w":27221,"n":"Empowered Breastplate of the Perfect Light","g":4029768687,"l":512,"t":2,"c":268437235,"cp":1024,"vp":1024,"at":1791413363,"f":100000000},{"w":28620,"n":"Olthoi Amuli Leggings","g":2148393663,"l":1024,"t":2,"c":268437291,"p":20,"s":0.36837588640319924,"cp":2048,"vp":2816,"at":1791413363,"f":100000000},{"w":88,"n":"Empowered Pauldrons of the Perfect Light","g":4027329344,"l":2048,"t":2,"c":268437236,"cp":4096,"vp":4096,"at":1791413363,"f":100000000},{"w":105,"n":"Empowered Bracers of the Perfect Light","g":4026797483,"l":4096,"t":2,"c":268437234,"cp":8192,"vp":8192,"at":1791413363,"f":100000000},{"w":108,"n":"Chainmail Tassets","g":4034195358,"l":8192,"t":2,"c":268436439,"p":20,"s":0.773973815503518,"cp":256,"vp":256,"at":1791413363,"f":100000000},{"w":80,"n":"Chainmail Leggings","g":2156292183,"l":16384,"t":2,"c":268435477,"p":2,"s":0.014741573955277714,"cp":512,"vp":768,"at":1791413363,"f":100000000},{"w":227190032,"n":"Rynthid Tentacles of Defense","g":2154378596,"l":134217728,"t":4,"c":268437766,"p":9,"s":1,"cp":131072,"vp":131072,"at":1791413363,"f":100000000}],"locks":{"1":1,"2":1,"4":2,"8":1,"10":1,"20":1,"40":1,"80":2,"100":1,"200":1,"400":1,"800":1,"1000":1,"2000":1,"4000":1,"8000000":1}}""";

        [TestMethod]
        public void TheLooksRefusedOnProduction_AreReadable()
        {
            var gary = DressingRoomLook.Parse(GaryStored);
            Assert.IsNotNull(gary);
            CollectionAssert.AreEqual(new[] { "Baggy Tunic", "Baggy Breeches", "Leather Boots" }, gary.Pieces.Select(p => p.Name).ToArray());

            var legolas = DressingRoomLook.Parse(LegolasStored);
            Assert.IsNotNull(legolas);
            Assert.AreEqual(12, legolas.Pieces.Count);
        }

        [TestMethod]
        public void TheLooksRefusedOnProduction_HoldNoConflictingPieces()
        {
            foreach (var stored in new[] { GaryStored, LegolasStored })
            {
                var pieces = DressingRoomLook.Parse(stored).Pieces;
                for (var i = 0; i < pieces.Count; i++)
                    for (var j = i + 1; j < pieces.Count; j++)
                        Assert.IsFalse(DressingRoomLook.Conflicts(pieces[i], pieces[j]), $"{pieces[i].Name} / {pieces[j].Name}");
            }
        }

        [TestMethod]
        public void AnOldRecord_CountsEachSavedPieceOnce_NotTheSharedSlotTwice()
        {
            // the old counts had "4":2 and "80":2 only because two pieces of ONE outfit shared those EquipMask bits
            foreach (var stored in new[] { GaryStored, LegolasStored })
            {
                var look = DressingRoomLook.Parse(stored);
                foreach (var piece in look.Pieces)
                    Assert.AreEqual(1, look.PriorLocks(piece.ClothingPriority ?? 0), piece.Name);
            }
        }

        [TestMethod]
        public void AnOldRecord_KeepsARealRepeatCount()
        {
            // a helm locked three times under the old keys: head slot (EquipMask 0x1) = 3
            var look = DressingRoomLook.Parse("{\"v\":1,\"pieces\":[{\"l\":1,\"c\":268435501,\"cp\":16384}],\"locks\":{\"1\":3}}");
            Assert.AreEqual(3, look.PriorLocks(HeadCov));
            Assert.AreEqual(0, look.PriorLocks(ChestCov));
        }

        [TestMethod]
        public void AnOldRecord_IsRewrittenWithCoverageCountsOnly()
        {
            var look = DressingRoomLook.Parse(GaryStored);

            var stored = look.Serialize();

            StringAssert.Contains(stored, "\"cover\":");
            Assert.IsFalse(stored.Contains("\"locks\":"), stored);
            var again = DressingRoomLook.Parse(stored);
            Assert.AreEqual(3, again.Pieces.Count);
            Assert.AreEqual(1, again.PriorLocks(ShirtCov));
        }

        // ---- replacing ------------------------------------------------------------------------

        [TestMethod]
        public void NewPiece_ReplacesEverySavedPieceItConflictsWith_Whole()
        {
            var look = new DressingRoomLook().WithLocked(new[] { Hauberk(), Helm() });

            var next = look.WithLocked(new[] { Breastplate() });

            CollectionAssert.AreEquivalent(new[] { "Helm", "Breastplate" }, next.Pieces.Select(p => p.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "Hauberk" }, look.ReplacedBy(Breastplate()).Select(p => p.Name).ToArray());
        }

        [TestMethod]
        public void NewPiece_InAFreeArea_ReplacesNothing()
        {
            var look = new DressingRoomLook().WithLocked(new[] { Breastplate() });

            var next = look.WithLocked(new[] { Helm() });

            Assert.AreEqual(2, next.Pieces.Count);
            Assert.AreEqual(0, look.ReplacedBy(Helm()).Count);
        }

        [TestMethod]
        public void NewShirt_DoesNotReplaceSavedTrousers()
        {
            var look = new DressingRoomLook().WithLocked(new[] { Shirt("Old shirt"), Trousers(), Boots() });

            var next = look.WithLocked(new[] { Shirt("New shirt") });

            CollectionAssert.AreEquivalent(new[] { "Trousers", "Boots", "New shirt" }, next.Pieces.Select(p => p.Name).ToArray());
        }

        [TestMethod]
        public void WithLocked_LeavesTheOriginalLookUntouched()
        {
            var look = new DressingRoomLook().WithLocked(new[] { Hauberk() });
            var stored = look.Serialize();

            look.WithLocked(new[] { Breastplate() });
            look.WithoutPieces();

            Assert.AreEqual(stored, look.Serialize());
        }

        [TestMethod]
        public void ALookNeverHoldsTwoConflictingPieces()
        {
            var look = new DressingRoomLook()
                .WithLocked(new[] { Hauberk() })
                .WithLocked(new[] { Breastplate(), Bracers() })
                .WithLocked(new[] { Hauberk("Hauberk again") });

            CollectionAssert.AreEqual(new[] { "Hauberk again" }, look.Pieces.Select(p => p.Name).ToArray());
            Assert.IsNotNull(DressingRoomLook.Parse(look.Serialize()));
        }

        // ---- lock counts ----------------------------------------------------------------------

        [TestMethod]
        public void LockCounts_GrowPerArea_AndAMultiAreaPieceTakesItsHighest()
        {
            var look = new DressingRoomLook()
                .WithLocked(new[] { Breastplate() })
                .WithLocked(new[] { Breastplate("Breastplate 2") });

            Assert.AreEqual(2, look.PriorLocks(ChestCov));
            Assert.AreEqual(0, look.PriorLocks(UpperArmsCov));
            Assert.AreEqual(0, look.PriorLocks(HeadCov));
            Assert.AreEqual(2, look.PriorLocks(HauberkCov), "a hauberk covers the chest, which has been locked twice");

            var next = look.WithLocked(new[] { Hauberk() });
            Assert.AreEqual(3, next.PriorLocks(ChestCov));
            Assert.AreEqual(1, next.PriorLocks(UpperArmsCov));
        }

        [TestMethod]
        public void LockCounts_AFirstOutfitCountsEveryAreaOnce()
        {
            var look = new DressingRoomLook().WithLocked(new[] { Shirt(), Trousers(), Boots(), Helm() });

            foreach (var piece in look.Pieces)
                Assert.AreEqual(1, look.PriorLocks(piece.ClothingPriority ?? 0), piece.Name);
        }

        [TestMethod]
        public void LockCounts_RelockingTheShirt_DoesNotRaiseTheTrousers()
        {
            var look = new DressingRoomLook()
                .WithLocked(new[] { Shirt(), Trousers() })
                .WithLocked(new[] { Shirt("Shirt 2") })
                .WithLocked(new[] { Shirt("Shirt 3") });

            Assert.AreEqual(3, look.PriorLocks(ShirtCov));
            Assert.AreEqual(1, look.PriorLocks(TrousersCov));
        }

        [TestMethod]
        public void ClearingALook_KeepsTheLockCounts()
        {
            var look = new DressingRoomLook().WithLocked(new[] { Breastplate() }).WithoutPieces();

            Assert.AreEqual(0, look.Pieces.Count);
            Assert.AreEqual(1, look.PriorLocks(ChestCov));
            Assert.AreEqual(1, DressingRoomLook.Parse(look.Serialize()).PriorLocks(ChestCov));
        }

        // ---- fee ------------------------------------------------------------------------------

        [TestMethod]
        public void Fee_DoublesPerEarlierLock_UpToTheCap()
        {
            const long baseFee = 100_000_000, cap = 10_000_000_000;
            Assert.AreEqual(100_000_000, DressingRoomLook.Fee(0, baseFee, 2.0, cap));
            Assert.AreEqual(200_000_000, DressingRoomLook.Fee(1, baseFee, 2.0, cap));
            Assert.AreEqual(400_000_000, DressingRoomLook.Fee(2, baseFee, 2.0, cap));
            Assert.AreEqual(6_400_000_000, DressingRoomLook.Fee(6, baseFee, 2.0, cap));
            Assert.AreEqual(cap, DressingRoomLook.Fee(7, baseFee, 2.0, cap));
            Assert.AreEqual(cap, DressingRoomLook.Fee(5000, baseFee, 2.0, cap));
        }

        [TestMethod]
        public void Fee_AtTheShippedSettings_ReachesOneThousandMmdOnTheEleventhLock()
        {
            const long baseFee = 100_000_000, cap = 250_000_000;
            Assert.AreEqual(100_000_000, DressingRoomLook.Fee(0, baseFee, 1.1, cap));
            Assert.AreEqual(110_000_000, DressingRoomLook.Fee(1, baseFee, 1.1, cap));
            Assert.AreEqual(235_794_769, DressingRoomLook.Fee(9, baseFee, 1.1, cap));
            Assert.AreEqual(cap, DressingRoomLook.Fee(10, baseFee, 1.1, cap));
        }

        [TestMethod]
        public void Fee_HandlesEveryOddSetting()
        {
            Assert.AreEqual(0, DressingRoomLook.Fee(3, 0, 2.0, 1000), "base 0 is free");
            Assert.AreEqual(0, DressingRoomLook.Fee(3, -5, 2.0, 1000), "a negative base is free");
            Assert.AreEqual(100, DressingRoomLook.Fee(9, 100, 0.5, 0), "growth below 1 never makes it cheaper");
            Assert.AreEqual(100, DressingRoomLook.Fee(9, 100, double.NaN, 0));
            Assert.AreEqual(100, DressingRoomLook.Fee(-4, 100, 2.0, 0), "a negative count is treated as 0");
            Assert.AreEqual(50, DressingRoomLook.Fee(0, 100, 2.0, 50), "a cap below the base wins");
            Assert.AreEqual(150, DressingRoomLook.Fee(1, 100, 1.5, 0));
            // no cap and a huge count: a fixed ceiling, never an overflow or a negative fee
            Assert.AreEqual(1_000_000_000_000_000_000, DressingRoomLook.Fee(5000, 100, 2.0, 0));
            Assert.AreEqual(1_000_000_000_000_000_000, DressingRoomLook.Fee(5000, 100, double.PositiveInfinity, 0));
            Assert.AreEqual(1_000_000_000_000_000_000, DressingRoomLook.Fee(1, long.MaxValue, 2.0, long.MaxValue));
        }

        // ---- the guide page's fee table --------------------------------------------------------

        [TestMethod]
        public void FeeSchedule_ListsEachFeeOnce_AndIsCompleteAtTheCap()
        {
            var (fees, complete) = ACE.Server.Controllers.DressingRoomGuideController.FeeSchedule(100_000_000, 2.0, 10_000_000_000);
            CollectionAssert.AreEqual(
                new long[] { 100_000_000, 200_000_000, 400_000_000, 800_000_000, 1_600_000_000, 3_200_000_000, 6_400_000_000, 10_000_000_000 },
                fees);
            Assert.IsTrue(complete, "the cap is reached, so the last row is every later fee");
        }

        [TestMethod]
        public void FeeSchedule_IsOneCompleteRow_WhenTheFeeNeverGrowsOrIsFree()
        {
            var flat = ACE.Server.Controllers.DressingRoomGuideController.FeeSchedule(500, 1.0, 0);
            CollectionAssert.AreEqual(new long[] { 500 }, flat.Fees);
            Assert.IsTrue(flat.Complete);

            var free = ACE.Server.Controllers.DressingRoomGuideController.FeeSchedule(0, 2.0, 0);
            CollectionAssert.AreEqual(new long[] { 0 }, free.Fees);
            Assert.IsTrue(free.Complete);
        }

        [TestMethod]
        public void FeeSchedule_IsBoundedButNotComplete_WhenThereIsNoCap()
        {
            var (fees, complete) = ACE.Server.Controllers.DressingRoomGuideController.FeeSchedule(1000, 2.0, 0);
            Assert.AreEqual(12, fees.Count);
            Assert.AreEqual(1000L << 11, fees[11]);
            Assert.IsFalse(complete, "a thirteenth lock costs more than anything listed");
        }

        [TestMethod]
        public void FeeSchedule_DoesNotStopOnEqualNeighbours_WhenTheFeeStillGrows()
        {
            // growth just above 1 rounds to the same fee at first (10, 10, 10...) and then rises
            var (fees, complete) = ACE.Server.Controllers.DressingRoomGuideController.FeeSchedule(10, 1.01, 0);
            Assert.AreEqual(12, fees.Count);
            Assert.AreEqual(fees[0], fees[1]);
            Assert.IsTrue(fees[11] > fees[0]);
            Assert.IsFalse(complete);
        }

        [TestMethod]
        public void FeeIsFinal_OnlyWhenNoLaterLockCanCostMore()
        {
            Assert.IsTrue(DressingRoomLook.FeeIsFinal(0, 0, 2.0, 0), "free");
            Assert.IsTrue(DressingRoomLook.FeeIsFinal(0, 100, 1.0, 0), "no growth");
            Assert.IsTrue(DressingRoomLook.FeeIsFinal(0, 100, 0.5, 0), "growth below 1 is treated as none");
            Assert.IsTrue(DressingRoomLook.FeeIsFinal(0, 100, double.NaN, 0));
            Assert.IsTrue(DressingRoomLook.FeeIsFinal(0, 100, 2.0, 50), "a cap below the base is already reached");
            Assert.IsFalse(DressingRoomLook.FeeIsFinal(0, 100, 2.0, 1000));
            Assert.IsFalse(DressingRoomLook.FeeIsFinal(3, 100, 2.0, 1000), "800 is still under the cap");
            Assert.IsTrue(DressingRoomLook.FeeIsFinal(4, 100, 2.0, 1000), "1,600 is capped to 1,000");
            Assert.IsFalse(DressingRoomLook.FeeIsFinal(0, 10, 1.01, 0), "equal neighbours, but it still grows");
            Assert.IsFalse(DressingRoomLook.FeeIsFinal(11, 1000, 2.0, 0));
        }

        // ---- paying ---------------------------------------------------------------------------

        [TestMethod]
        public void Split_TakesTheBankFirst_ThenThePack()
        {
            Assert.AreEqual((100L, 0L), PyrealFee.Split(500, 500, 100));
            Assert.AreEqual((60L, 40L), PyrealFee.Split(60, 500, 100));
            Assert.AreEqual((0L, 100L), PyrealFee.Split(0, 100, 100));
            Assert.AreEqual((0L, 0L), PyrealFee.Split(0, 0, 0));
        }

        [TestMethod]
        public void Split_IsNullWhenTheFeeCannotBeCovered()
        {
            Assert.IsNull(PyrealFee.Split(60, 39, 100));
            Assert.IsNull(PyrealFee.Split(-50, 99, 100), "a negative bank pays nothing");
            Assert.IsNull(PyrealFee.Split(0, long.MaxValue, 3_000_000_000), "more coins than one consume call can take");
        }
    }
}
