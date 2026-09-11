using Microsoft.VisualStudio.TestTools.UnitTesting;
using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;

namespace ACE.DatLoader.Tests
{
    [TestClass]
    public class TryReadClothingTableTests
    {
        private class TestDatDatabase : DatDatabase
        {
            public TestDatDatabase() : base() { }
        }

        [TestMethod]
        public void TryReadClothingTable_InvalidClothingId_ReturnsFalse()
        {
            var db = new TestDatDatabase();
            // Not in 0x10xxxxxx range
            var result = db.TryReadClothingTable(0x01000001, out var clothingTable);

            Assert.IsFalse(result);
            Assert.IsNull(clothingTable);
        }

        [TestMethod]
        public void TryReadClothingTable_CachedValidTable_ReturnsTrue()
        {
            var db = new TestDatDatabase();
            uint customClothingId = 0x10000959;

            var table = new ClothingTable();
            table.ClothingBaseEffects.Add(0x02000001, new ClothingBaseEffect());

            db.FileCache[customClothingId] = table;

            var result = db.TryReadClothingTable(customClothingId, out var clothingTable);

            Assert.IsTrue(result);
            Assert.IsNotNull(clothingTable);
            Assert.AreSame(table, clothingTable);
        }

        [TestMethod]
        public void TryReadClothingTable_CachedWithSubPalEffects_ReturnsTrue()
        {
            var db = new TestDatDatabase();
            uint customClothingId = 0x100009D5;

            var table = new ClothingTable();
            table.ClothingSubPalEffects.Add(1, new CloSubPalEffect());

            db.FileCache[customClothingId] = table;

            var result = db.TryReadClothingTable(customClothingId, out var clothingTable);

            Assert.IsTrue(result);
            Assert.IsNotNull(clothingTable);
            Assert.AreSame(table, clothingTable);
        }

        [TestMethod]
        public void TryReadClothingTable_CachedEmptyTable_ReturnsFalse()
        {
            var db = new TestDatDatabase();
            uint customClothingId = 0x10000959;

            var table = new ClothingTable(); // 0 effects
            db.FileCache[customClothingId] = table;

            var result = db.TryReadClothingTable(customClothingId, out var clothingTable);

            Assert.IsFalse(result);
            Assert.IsNull(clothingTable);
        }

        [TestMethod]
        public void TryReadClothingTable_TrulyAbsent_ReturnsFalse()
        {
            var db = new TestDatDatabase();
            uint absentId = 0x10000999;

            var result = db.TryReadClothingTable(absentId, out var clothingTable);

            Assert.IsFalse(result);
            Assert.IsNull(clothingTable);
        }
    }
}
