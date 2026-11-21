using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.DatLoader;
using ACE.Server.Physics.Common;

namespace ACE.Server.Tests
{
    [TestClass]
    public class CellDatIndexTests
    {
        // a cell dat index holding two landblocks: outdoor A9B4 and dungeon 00E5 (Temple of the Three)
        private static readonly Dictionary<uint, DatFile> Index = new()
        {
            [0xA9B4FFFF] = new DatFile(),
            [0xA9B4FFFE] = new DatFile(),
            [0x00E5FFFF] = new DatFile(),
            [0x00E50130] = new DatFile(),
        };

        [TestMethod]
        [DataRow(0xA9B4001Fu)]   // outdoor land cell
        [DataRow(0xA9B40105u)]   // building cell in an outdoor landblock
        [DataRow(0x00E50130u)]   // dungeon cell
        [DataRow(0x00E5FFFFu)]   // the landblock id itself
        public void CellInKnownLandblock_Exists(uint cellId)
        {
            Assert.IsTrue(CellDatIndex.LandblockExists(Index, cellId));
        }

        [TestMethod]
        [DataRow(0xFFFF0100u)]   // the 2026-11-19 crash: landblock FFFF is off the map
        [DataRow(0xFFFFFFFFu)]
        [DataRow(0xA9B5001Fu)]   // a neighbour that isn't in this index
        [DataRow(0x00000000u)]
        public void CellInMissingLandblock_DoesNotExist(uint cellId)
        {
            Assert.IsFalse(CellDatIndex.LandblockExists(Index, cellId));
        }

        [TestMethod]
        public void OnlyTheLandblockRecordCounts_NotOtherFilesInIt()
        {
            // 00E50130 is present but 00E5's landblock record is what the loader reads; a block with only
            // LandblockInfo (FFFE) or cells but no FFFF record still can't be built
            var index = new Dictionary<uint, DatFile> { [0x1234FFFE] = new DatFile(), [0x12340100] = new DatFile() };
            Assert.IsFalse(CellDatIndex.LandblockExists(index, 0x12340100));
        }
    }
}
