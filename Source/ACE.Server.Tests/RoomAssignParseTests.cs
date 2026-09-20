using ACE.Server.Managers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The Room Assign room list (PropertyString 50500) - RoomAssignManager.TryParseRooms. The list is authored by hand and
    /// shipped to live as SQL, so every mistake must be refused with the room it is in, never half-accepted.
    /// </summary>
    [TestClass]
    public class RoomAssignParseTests
    {
        private const string Room1 = "1|0x01F701D3 [40.006317 -110.304848 -11.971000] -0.998798 0.000000 0.000000 0.049006|0x01F701D3,0x01F701D4";
        private const string Room2 = "2|0x01F701C4 [20.0 -104.0 -12.0] 1 0 0 0|0x01F701C4";

        [TestMethod]
        public void TwoRooms_ParseInOrder_WithRotationAsWXYZ()
        {
            Assert.IsTrue(RoomAssignManager.TryParseRooms(Room2 + ";" + Room1, out var rooms, out var error), error);
            Assert.AreEqual(2, rooms.Count);
            Assert.AreEqual(1, rooms[0].Number, "rooms are handed out first free by NUMBER, so they must sort");
            Assert.AreEqual(0x01F701D3u, rooms[0].LandingCell);
            Assert.AreEqual(-0.998798f, rooms[0].QW, 0.00001f);
            Assert.AreEqual(0.049006f, rooms[0].QZ, 0.00001f);
            Assert.IsTrue(rooms[0].Cells.Contains(0x01F701D4u));
        }

        [TestMethod]
        public void TrailingSemicolonAndSpacesAroundEntries_AreFine()
        {
            Assert.IsTrue(RoomAssignManager.TryParseRooms(" " + Room1 + " ; " + Room2 + ";", out var rooms, out var error), error);
            Assert.AreEqual(2, rooms.Count);
        }

        [TestMethod]
        public void PastedLocationLine_IsRefused_NotSilentlyUnrotated()
        {
            // /location prints ", v:2" after the rotation; the comma lands on the last number.
            var pasted = "1|0x01F701D3 [40.006317 -110.304848 -11.971000] -0.998798 0.000000 0.000000 0.049006, v:2|0x01F701D3";
            Assert.IsFalse(RoomAssignManager.TryParseRooms(pasted, out _, out var error));
            StringAssert.Contains(error, "room 1");
        }

        [TestMethod]
        public void MissingRotation_IsRefused()
        {
            Assert.IsFalse(RoomAssignManager.TryParseRooms("1|0x01F701D3 [40 -110 -12]|0x01F701D3", out _, out _));
        }

        [TestMethod]
        public void LandingCellOutsideItsRoom_IsRefused()
        {
            Assert.IsFalse(RoomAssignManager.TryParseRooms("1|0x01F701D3 [40 -110 -12] 1 0 0 0|0x01F701D4", out _, out var error));
            StringAssert.Contains(error, "not in its own cell list");
        }

        [TestMethod]
        public void CellInTwoRooms_IsRefused()
        {
            var overlap = Room1 + ";2|0x01F701D4 [1 1 1] 1 0 0 0|0x01F701D4";
            Assert.IsFalse(RoomAssignManager.TryParseRooms(overlap, out _, out var error));
            StringAssert.Contains(error, "0x01F701D4");
        }

        [TestMethod]
        public void DuplicateRoomNumber_IsRefused()
        {
            Assert.IsFalse(RoomAssignManager.TryParseRooms(Room1 + ";1|0x01F701C4 [1 1 1] 1 0 0 0|0x01F701C4", out _, out var error));
            StringAssert.Contains(error, "twice");
        }

        [TestMethod]
        public void BadCellId_WrongFieldCount_AndEmpty_AreRefused()
        {
            Assert.IsFalse(RoomAssignManager.TryParseRooms("1|0x01F701D3 [1 1 1] 1 0 0 0|01F701D3", out _, out _), "cell without 0x");
            Assert.IsFalse(RoomAssignManager.TryParseRooms("1|0x01F701D3 [1 1 1] 1 0 0 0", out _, out _), "two fields");
            Assert.IsFalse(RoomAssignManager.TryParseRooms("", out _, out _), "empty");
            Assert.IsFalse(RoomAssignManager.TryParseRooms(" ; ", out _, out _), "no rooms");
        }

        [TestMethod]
        public void SameCellsInDifferentVariations_HaveDifferentKeys()
        {
            Assert.IsTrue(RoomAssignManager.TryParseRooms(Room1, out var rooms, out _));
            Assert.AreNotEqual(rooms[0].Key(2), rooms[0].Key(null), "base and v2 share every cell id - only the variation separates them");
            Assert.AreEqual(rooms[0].Key(0), rooms[0].Key(null), "variation 0 is base");
        }

        [TestMethod]
        public void RoomKey_IsTheLowestCell_SoMovingTheLandingKeepsIt()
        {
            Assert.IsTrue(RoomAssignManager.TryParseRooms("1|0x01F701D4 [1 1 1] 1 0 0 0|0x01F701D4,0x01F701D3", out var a, out _));
            Assert.IsTrue(RoomAssignManager.TryParseRooms("1|0x01F701D3 [2 2 2] 1 0 0 0|0x01F701D3,0x01F701D4", out var b, out _));
            Assert.AreEqual(a[0].Key(2), b[0].Key(2));
        }

        [TestMethod]
        public void NotANumber_OrZeroRotation_IsRefused()
        {
            Assert.IsFalse(RoomAssignManager.TryParseRooms("1|0x01F701D3 [NaN 1 1] 1 0 0 0|0x01F701D3", out _, out var e1), "NaN");
            StringAssert.Contains(e1, "room 1");
            Assert.IsFalse(RoomAssignManager.TryParseRooms("1|0x01F701D3 [1 Infinity 1] 1 0 0 0|0x01F701D3", out _, out _), "Infinity");
            Assert.IsFalse(RoomAssignManager.TryParseRooms("1|0x01F701D3 [1 1 1] 0 0 0 0|0x01F701D3", out _, out var e2), "zero rotation");
            StringAssert.Contains(e2, "rotation");
        }

        [TestMethod]
        public void CellsOnAnotherLandblock_AreRefused()
        {
            Assert.IsFalse(RoomAssignManager.TryParseRooms(Room1 + ";2|0x01F801C4 [1 1 1] 1 0 0 0|0x01F801C4", out _, out var error));
            StringAssert.Contains(error, "0x01F801C4");
        }

        [TestMethod]
        public void RotationNotUnitLength_IsNormalized()
        {
            Assert.IsTrue(RoomAssignManager.TryParseRooms("1|0x01F701D3 [1 1 1] 2 0 0 0|0x01F701D3", out var rooms, out var error), error);
            Assert.AreEqual(1f, rooms[0].QW, 0.00001f);
        }

        [TestMethod]
        public void ExtraSpacesInTheLanding_AreFine()
        {
            Assert.IsTrue(RoomAssignManager.TryParseRooms("1|0x01F701D3  [ 1  1 1 ]  1 0  0 0|0x01F701D3", out var rooms, out var error), error);
            Assert.AreEqual(1f, rooms[0].X);
        }

        [TestMethod]
        public void HugeRotation_IsNormalized_NotOverflowedToZero()
        {
            Assert.IsTrue(RoomAssignManager.TryParseRooms("1|0x01F701D3 [1 1 1] 1e20 0 0 0|0x01F701D3", out var rooms, out var error), error);
            Assert.AreEqual(1f, rooms[0].QW, 0.00001f);
        }

        [TestMethod]
        public void OutdoorCell_IsRefused()
        {
            Assert.IsFalse(RoomAssignManager.TryParseRooms("1|0x01F70001 [1 1 1] 1 0 0 0|0x01F70001", out _, out var error));
            StringAssert.Contains(error, "outdoor");
        }

        [TestMethod]
        public void RoomNumberZeroOrNegative_IsRefused()
        {
            Assert.IsFalse(RoomAssignManager.TryParseRooms("0|0x01F701D3 [1 1 1] 1 0 0 0|0x01F701D3", out _, out _), "zero");
            Assert.IsFalse(RoomAssignManager.TryParseRooms("-1|0x01F701D3 [1 1 1] 1 0 0 0|0x01F701D3", out _, out _), "negative");
        }

        [TestMethod]
        public void CellIdWithSpaceOrTooManyDigits_IsRefused()
        {
            Assert.IsFalse(RoomAssignManager.TryParseRooms("1|0x01F701D3 [1 1 1] 1 0 0 0|0x 01F701D3", out _, out _), "space inside the id");
            Assert.IsFalse(RoomAssignManager.TryParseRooms("1|0x01F701D3 [1 1 1] 1 0 0 0|0x001F701D3", out _, out _), "9 hex digits");
            Assert.IsFalse(RoomAssignManager.TryParseRooms("1|0x01F701D3 [1 1 1] 1 0 0 0|0x01F701D3,", out _, out _), "trailing comma");
        }
    }
}
