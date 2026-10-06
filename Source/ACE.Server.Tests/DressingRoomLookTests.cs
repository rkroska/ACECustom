using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Entity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The Dressing Room rules that need no running server: what a stored look round-trips to, which saved pieces a new
    /// piece replaces, how the per-slot lock counts grow, and what a piece costs.
    /// </summary>
    [TestClass]
    public class DressingRoomLookTests
    {
        private const uint Chest = (uint)EquipMask.ChestArmor;
        private const uint Abdomen = (uint)EquipMask.AbdomenArmor;
        private const uint UpperArms = (uint)EquipMask.UpperArmArmor;
        private const uint LowerArms = (uint)EquipMask.LowerArmArmor;
        private const uint Head = (uint)EquipMask.HeadWear;
        private const uint Hauberk = Chest | Abdomen | UpperArms | LowerArms;

        private static DressingRoomPiece Piece(string name, uint location, uint clothingBase = 0x10000001) => new()
        {
            Wcid = 1234,
            Name = name,
            Guid = 0x80001234,
            Location = location,
            ItemType = (uint)ItemType.Armor,
            ClothingBase = clothingBase,
            PaletteTemplate = 14,
            Shade = 0.25,
            ClothingPriority = (uint)CoverageMask.OuterwearChest,
            VisualPriority = (uint)CoverageMask.OuterwearChest,
            LockedAt = 1_760_000_000,
            Fee = 100,
        };

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
            CollectionAssert.AreEqual(new[] { Chest, Abdomen, UpperArms, LowerArms }, DressingRoomLook.SlotBits(Hauberk).ToArray());
            CollectionAssert.AreEqual(new[] { (uint)EquipMask.Cloak }, DressingRoomLook.SlotBits((uint)EquipMask.Cloak).ToArray());
            Assert.AreEqual(0, DressingRoomLook.SlotBits((uint)EquipMask.MeleeWeapon).Count());
        }

        [TestMethod]
        public void ArmourAndUnderclothes_OnTheSameBodyPart_DoNotOverlap()
        {
            Assert.IsFalse(DressingRoomLook.Overlaps((uint)EquipMask.ChestWear, Chest));
            Assert.IsTrue(DressingRoomLook.Overlaps(Hauberk, Chest));
        }

        // ---- storage --------------------------------------------------------------------------

        [TestMethod]
        public void Look_RoundTrips()
        {
            var look = new DressingRoomLook().WithLocked(new[] { Piece("Diforsa Hauberk", Hauberk), Piece("Olthoi Helm", Head) });

            var back = DressingRoomLook.Parse(look.Serialize());

            Assert.IsNotNull(back);
            Assert.AreEqual(2, back.Pieces.Count);
            var hauberk = back.Pieces.Single(p => p.Name == "Diforsa Hauberk");
            Assert.AreEqual(Hauberk, hauberk.Location);
            Assert.AreEqual(0x10000001u, hauberk.ClothingBase);
            Assert.AreEqual(14, hauberk.PaletteTemplate);
            Assert.AreEqual(0.25, hauberk.Shade);
            Assert.IsNull(hauberk.TopLayer);
            Assert.AreEqual(1, back.PriorLocks(Chest));
            Assert.AreEqual(1, back.PriorLocks(Head));
        }

        [TestMethod]
        public void StoredLook_IsPlainAscii_EvenForANonAsciiName()
        {
            var look = new DressingRoomLook().WithLocked(new[] { Piece("Café — Robe", Chest) });
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
            Assert.IsNull(DressingRoomLook.Parse("{\"v\":1,\"pieces\":[{\"l\":512,\"c\":268435457},{\"l\":1536,\"c\":268435457}]}"), "two pieces in one slot");
        }

        [TestMethod]
        public void Parse_AcceptsAnEmptyLook_WithNullCollections()
        {
            var look = DressingRoomLook.Parse("{\"v\":1,\"pieces\":null,\"locks\":null}");
            Assert.IsNotNull(look);
            Assert.AreEqual(0, look.Pieces.Count);
            Assert.AreEqual(0, look.PriorLocks(Chest));
        }

        [TestMethod]
        public void Parse_RejectsMoreSavedPiecesThanThereAreSlots()
        {
            var pieces = string.Join(",", Enumerable.Range(0, DressingRoomLook.MaxPieces + 1).Select(_ => "{\"l\":512,\"c\":268435457}"));
            Assert.IsNull(DressingRoomLook.Parse("{\"v\":1,\"pieces\":[" + pieces + "]}"));
        }

        // ---- replacing ------------------------------------------------------------------------

        [TestMethod]
        public void NewPiece_ReplacesEverySavedPieceItOverlaps_Whole()
        {
            var look = new DressingRoomLook().WithLocked(new[] { Piece("Hauberk", Hauberk), Piece("Helm", Head) });

            var next = look.WithLocked(new[] { Piece("Breastplate", Chest) });

            CollectionAssert.AreEquivalent(new[] { "Helm", "Breastplate" }, next.Pieces.Select(p => p.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "Hauberk" }, look.ReplacedBy(Chest).Select(p => p.Name).ToArray());
        }

        [TestMethod]
        public void NewPiece_InAFreeSlot_ReplacesNothing()
        {
            var look = new DressingRoomLook().WithLocked(new[] { Piece("Breastplate", Chest) });

            var next = look.WithLocked(new[] { Piece("Helm", Head) });

            Assert.AreEqual(2, next.Pieces.Count);
            Assert.AreEqual(0, look.ReplacedBy(Head).Count);
        }

        [TestMethod]
        public void WithLocked_LeavesTheOriginalLookUntouched()
        {
            var look = new DressingRoomLook().WithLocked(new[] { Piece("Hauberk", Hauberk) });
            var stored = look.Serialize();

            look.WithLocked(new[] { Piece("Breastplate", Chest) });
            look.WithoutPieces();

            Assert.AreEqual(stored, look.Serialize());
        }

        [TestMethod]
        public void ALookNeverHoldsTwoPiecesInOneSlot()
        {
            var look = new DressingRoomLook()
                .WithLocked(new[] { Piece("Hauberk", Hauberk) })
                .WithLocked(new[] { Piece("Breastplate", Chest), Piece("Bracers", LowerArms) })
                .WithLocked(new[] { Piece("Hauberk again", Hauberk) });

            CollectionAssert.AreEqual(new[] { "Hauberk again" }, look.Pieces.Select(p => p.Name).ToArray());
            Assert.IsNotNull(DressingRoomLook.Parse(look.Serialize()));
        }

        // ---- lock counts ----------------------------------------------------------------------

        [TestMethod]
        public void LockCounts_GrowPerSlot_AndAMultiSlotPieceTakesItsHighest()
        {
            var look = new DressingRoomLook()
                .WithLocked(new[] { Piece("Breastplate", Chest) })
                .WithLocked(new[] { Piece("Breastplate 2", Chest) });

            Assert.AreEqual(2, look.PriorLocks(Chest));
            Assert.AreEqual(0, look.PriorLocks(UpperArms));
            Assert.AreEqual(0, look.PriorLocks(Head));
            Assert.AreEqual(2, look.PriorLocks(Hauberk), "a hauberk covers the chest, which has been locked twice");

            var next = look.WithLocked(new[] { Piece("Hauberk", Hauberk) });
            Assert.AreEqual(3, next.PriorLocks(Chest));
            Assert.AreEqual(1, next.PriorLocks(UpperArms));
        }

        [TestMethod]
        public void ClearingALook_KeepsTheLockCounts()
        {
            var look = new DressingRoomLook().WithLocked(new[] { Piece("Breastplate", Chest) }).WithoutPieces();

            Assert.AreEqual(0, look.Pieces.Count);
            Assert.AreEqual(1, look.PriorLocks(Chest));
            Assert.AreEqual(1, DressingRoomLook.Parse(look.Serialize()).PriorLocks(Chest));
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
        public void FeeSchedule_ListsEachFeeOnce_AndStopsAtTheCap()
        {
            CollectionAssert.AreEqual(
                new long[] { 100_000_000, 200_000_000, 400_000_000, 800_000_000, 1_600_000_000, 3_200_000_000, 6_400_000_000, 10_000_000_000 },
                ACE.Server.Controllers.DressingRoomGuideController.FeeSchedule(100_000_000, 2.0, 10_000_000_000));
        }

        [TestMethod]
        public void FeeSchedule_IsOneRow_WhenTheFeeNeverGrowsOrIsFree()
        {
            CollectionAssert.AreEqual(new long[] { 500 }, ACE.Server.Controllers.DressingRoomGuideController.FeeSchedule(500, 1.0, 0));
            CollectionAssert.AreEqual(new long[] { 0 }, ACE.Server.Controllers.DressingRoomGuideController.FeeSchedule(0, 2.0, 0));
        }

        [TestMethod]
        public void FeeSchedule_IsBounded_WhenThereIsNoCap()
        {
            var schedule = ACE.Server.Controllers.DressingRoomGuideController.FeeSchedule(1000, 2.0, 0);
            Assert.AreEqual(12, schedule.Count);
            Assert.AreEqual(1000L << 11, schedule[11]);
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
