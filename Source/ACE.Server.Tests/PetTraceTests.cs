using System.Globalization;
using System.Linq;

using ACE.Server.WorldObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// The [PetTrace] record contract: one line, "[PetTrace] event | session=.. | t=.. | k=v ...",
    /// 7-bit ASCII with no tabs, line breaks or pipes inside a value, and every value formatted so
    /// it parses back with the invariant culture. A pasted log must survive a round trip.
    /// </summary>
    [TestClass]
    public class PetTraceTests
    {
        [TestMethod]
        public void Record_RoundTrips_KeyValueShape()
        {
            var line = PetTrace.Begin("breed.roll", "abc12345")
                .Add("kind", "stat").Add("chance", 0.2).Add("roll", 0.123456f).Add("mutated", true).Add("count", 3).Add("big", 1234567890123L)
                .Add("name", "Fire Skeleton Samurai Essence (250)").AddGuid("guid", 0x80001234u).Add("nothing", (object)null)
                .ToString();

            Assert.IsTrue(line.StartsWith("[PetTrace] breed.roll | session=abc12345 | t="), line);
            Assert.IsTrue(PetTrace.TryParse(line, out var evt, out var kv), line);
            Assert.AreEqual("breed.roll", evt);
            Assert.AreEqual("abc12345", kv["session"]);
            Assert.IsTrue(kv["t"].EndsWith("Z") && kv["t"].Contains("T"), kv["t"]);
            Assert.AreEqual("stat", kv["kind"]);
            Assert.AreEqual(0.2, double.Parse(kv["chance"], CultureInfo.InvariantCulture));
            Assert.AreEqual(0.123456f, float.Parse(kv["roll"], CultureInfo.InvariantCulture));
            Assert.AreEqual("true", kv["mutated"]);
            Assert.AreEqual("3", kv["count"]);
            Assert.AreEqual("1234567890123", kv["big"]);
            Assert.AreEqual("Fire Skeleton Samurai Essence (250)", kv["name"]);
            Assert.AreEqual("0x80001234", kv["guid"]);
            Assert.AreEqual("null", kv["nothing"]);
        }

        [TestMethod]
        public void Record_StaysAscii_SingleLine_NoPipesInValues()
        {
            // e-acute, em dash, tab, pipe, CR LF: built from char codes so this source file stays 7-bit ASCII.
            var dirty = "Ol" + (char)0x00E9 + " " + (char)0x2014 + " pet" + '\t' + "|" + "\r\n" + "end";
            var line = PetTrace.Begin("device.dump", "s1")
                .Add("name", dirty)
                .Add("json", "{\"a\":1,\"b\":[0.5]}")
                .ToString();

            Assert.IsTrue(line.All(c => c >= 0x20 && c <= 0x7E), "non-ASCII or control character in record: " + line);
            Assert.IsTrue(PetTrace.TryParse(line, out _, out var kv));
            Assert.AreEqual("Ol? ? pet /  end", kv["name"]);
            Assert.AreEqual("{\"a\":1,\"b\":[0.5]}", kv["json"]);
        }

        [TestMethod]
        public void Record_NullSession_IsWrittenAsNone()
        {
            var line = PetTrace.Begin("guardian.lost", null).ToString();
            Assert.IsTrue(PetTrace.TryParse(line, out _, out var kv));
            Assert.AreEqual("none", kv["session"]);
        }

        [TestMethod]
        public void TryParse_RejectsNonTraceLines()
        {
            Assert.IsFalse(PetTrace.TryParse("2026-09-19 INFO [PetBreeding] something else", out _, out _));
            Assert.IsFalse(PetTrace.TryParse("", out _, out _));
            Assert.IsFalse(PetTrace.TryParse(null, out _, out _));
        }

        [TestMethod]
        public void TryParse_TakesTheRecordFromBehindALogPrefix()
        {
            var line = "2026-09-19 12:00:00,000 [thread] INFO  (PetTrace) " + PetTrace.Begin("combat.death", "x").Add("killer", "Drudge Skulker").ToString();
            Assert.IsTrue(PetTrace.TryParse(line, out var evt, out var kv));
            Assert.AreEqual("combat.death", evt);
            Assert.AreEqual("Drudge Skulker", kv["killer"]);
        }

        [TestMethod]
        public void SessionId_IsShortHex()
        {
            var id = PetTrace.NewSessionId();
            Assert.AreEqual(8, id.Length);
            Assert.IsTrue(id.All(c => "0123456789abcdef".Contains(c)), id);
        }
    }
}
