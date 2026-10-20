using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Server.Command.Handlers;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Optional player name or id on the player admin commands (2026-10-19): what an admin types after a command's own
    /// arguments is an object id when it is all decimal digits or 0x hex, and a character name otherwise.
    /// </summary>
    [TestClass]
    public class CommandTargetTests
    {
        /// <summary>Decimal ids, as the database and /setproperty use them.</summary>
        [TestMethod]
        public void TryParseObjectId_Decimal()
        {
            Assert.IsTrue(CommandHandlerHelper.TryParseObjectId("1342184762", out var guid));
            Assert.AreEqual(1342184762u, guid);
        }

        /// <summary>0x hex ids, as the audit lines of these commands print them.</summary>
        [TestMethod]
        public void TryParseObjectId_Hex()
        {
            Assert.IsTrue(CommandHandlerHelper.TryParseObjectId("0x50001D3A", out var guid));
            Assert.AreEqual(0x50001D3Au, guid);
            Assert.IsTrue(CommandHandlerHelper.TryParseObjectId("0X50001d3a", out guid));
            Assert.AreEqual(0x50001D3Au, guid);
        }

        /// <summary>Character names, with or without the + admin prefix or spaces, are never ids.</summary>
        [TestMethod]
        public void TryParseObjectId_NamesAreNotIds()
        {
            Assert.IsFalse(CommandHandlerHelper.TryParseObjectId("Dacuda", out _));
            Assert.IsFalse(CommandHandlerHelper.TryParseObjectId("+Grumpy Old Man", out _));
            Assert.IsFalse(CommandHandlerHelper.TryParseObjectId("Big Bots Burgers", out _));
            Assert.IsFalse(CommandHandlerHelper.TryParseObjectId("Face", out _));   // hex letters, but no 0x: a name
        }

        /// <summary>Junk, an empty 0x, negatives and ids past a uint are refused.</summary>
        [TestMethod]
        public void TryParseObjectId_RefusesJunk()
        {
            Assert.IsFalse(CommandHandlerHelper.TryParseObjectId("", out _));
            Assert.IsFalse(CommandHandlerHelper.TryParseObjectId("0x", out _));
            Assert.IsFalse(CommandHandlerHelper.TryParseObjectId("-5", out _));
            Assert.IsFalse(CommandHandlerHelper.TryParseObjectId("4294967296", out _));
            Assert.IsFalse(CommandHandlerHelper.TryParseObjectId("0x1FFFFFFFF", out _));
            Assert.IsFalse(CommandHandlerHelper.TryParseObjectId("12 34", out _));
            Assert.IsFalse(CommandHandlerHelper.TryParseObjectId(" 1342184762", out _));   // GetCommandTarget trims before asking
        }

        /// <summary>0x0 is a well-formed id (it is then simply not found).</summary>
        [TestMethod]
        public void TryParseObjectId_ZeroIsAnId()
        {
            Assert.IsTrue(CommandHandlerHelper.TryParseObjectId("0x0", out var guid));
            Assert.AreEqual(0u, guid);
        }
    }
}
