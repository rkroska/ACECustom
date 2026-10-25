using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Server.Managers;
using ACE.Server.WorldObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace ACE.Server.Tests
{
    /// <summary>
    /// /settings export | import (owner 2026-10-25): the file round-trips exactly, bad files and names are refused, the
    /// import keeps the target's server-owned Zone Control parts, and the preview says what changes. Pure: no database.
    /// </summary>
    [TestClass]
    public class SettingsTransferTests
    {
        private const string ZoneJson = @"{""areas"":[{""name"":""Tou Tou 15"",""variation"":15,""enabled"":true}],
            ""variationDefaults"":{""15"":{""profile"":{""stats"":{""monster_damage_mult"":{""base"":1.2,""growth"":1.0},
            ""war_damage_taken_mult"":{""base"":1.81,""growth"":1.0}}}}},
            ""ladderApplies"":{""15"":{""version"":3}},""gearLockSpots"":[{""landblock"":61782,""variation"":15}],
            ""zoneWideBounty"":{""on"":true},""regionBounties"":{},""damageMults"":{},""allowedProcSpells"":[]}";
        private const string WeaponJson = @"{""Enabled"":true,""KcMin"":0.6,""KcMax"":0.8,""GradeWeights"":{""S"":0.1,""A"":1.0}}";

        private static Dictionary<string, IDictionary<string, string>> Charms(string share100 = "0.45") => new()
        {
            ["armorbreaker"] = new Dictionary<string, string> { ["share"] = "0.2", ["share_100"] = share100, ["enabled"] = "true" },
            ["chain"] = new Dictionary<string, string> { ["jumps"] = "2", ["enabled"] = "true" },
        };

        private static SettingsTransfer.Bundle Sample(string zone = ZoneJson, string share100 = "0.45") =>
            SettingsTransfer.Make("Test Server", zone, WeaponJson, Charms(share100), new DateTime(2026, 10, 25, 14, 30, 0, DateTimeKind.Utc));

        [TestMethod]
        public void Bundle_RoundTripsExactly()
        {
            var a = Sample();
            var b = SettingsTransfer.Parse(SettingsTransfer.Serialize(a));
            Assert.AreEqual("Test Server", b.Source);
            Assert.AreEqual(a.CreatedUtc, b.CreatedUtc);
            Assert.IsTrue(JToken.DeepEquals(a.ZoneControl, b.ZoneControl), "Zone Control store");
            Assert.IsTrue(JToken.DeepEquals(a.WeaponScaling, b.WeaponScaling), "weapon scaling store");
            CollectionAssert.AreEquivalent(a.Charms.Keys.ToList(), b.Charms.Keys.ToList());
            foreach (var name in a.Charms.Keys)
                CollectionAssert.AreEquivalent(a.Charms[name].ToList(), b.Charms[name].ToList(), name);
            // and a second trip changes nothing
            Assert.AreEqual(SettingsTransfer.Serialize(b), SettingsTransfer.Serialize(SettingsTransfer.Parse(SettingsTransfer.Serialize(b))));
        }

        [TestMethod]
        public void Parse_RefusesWhatIsNotASettingsFile()
        {
            Assert.ThrowsExactly<FormatException>(() => SettingsTransfer.Parse("not json"));
            Assert.ThrowsExactly<FormatException>(() => SettingsTransfer.Parse(@"{""areas"":[]}"), "a bare store is not a settings file");
            Assert.ThrowsExactly<FormatException>(() => SettingsTransfer.Parse(@"{""format"":""acecustom-settings"",""version"":99}"), "newer than this build");
            Assert.ThrowsExactly<FormatException>(() => SettingsTransfer.Parse(@"{""format"":""acecustom-settings"",""version"":0}"));
            // a store part that is there but not an object is broken, never "not in the file"
            Assert.ThrowsExactly<FormatException>(() => SettingsTransfer.Parse(@"{""format"":""acecustom-settings"",""version"":1,""zoneControl"":""x""}"));
            Assert.ThrowsExactly<FormatException>(() => SettingsTransfer.Parse(@"{""format"":""acecustom-settings"",""version"":1,""weaponScaling"":[]}"));
            var empty = SettingsTransfer.Parse(@"{""format"":""acecustom-settings"",""version"":1,""zoneControl"":null}");
            Assert.IsNull(empty.ZoneControl, "an explicit null is still accepted");
            Assert.IsNull(empty.WeaponScaling, "a missing part is still accepted");
            // charms: missing or null = none; a non-object, or a charm entry that is not an object, is refused
            Assert.ThrowsExactly<FormatException>(() => SettingsTransfer.Parse(@"{""format"":""acecustom-settings"",""version"":1,""charms"":[]}"));
            Assert.ThrowsExactly<FormatException>(() => SettingsTransfer.Parse(@"{""format"":""acecustom-settings"",""version"":1,""charms"":{""chain"":""x""}}"));
            Assert.AreEqual(0, SettingsTransfer.Parse(@"{""format"":""acecustom-settings"",""version"":1,""charms"":null}").Charms.Count);
        }

        [TestMethod]
        public void FileNames_OnlyPlainNamesInTheFolder()
        {
            Assert.AreEqual("balance-1025.json", SettingsTransfer.FileNameFor("balance-1025"));
            Assert.AreEqual("balance-1025.json", SettingsTransfer.FileNameFor("balance-1025.json"));
            Assert.AreEqual("backup-20261025-143000.json", SettingsTransfer.FileNameFor("backup-20261025-143000"));
            foreach (var bad in new[] { null, "", "  ", "../x", "..", "a/b", @"a\b", @"C:\ACE\x", "/ace/x", ".hidden", "a..b", "a b", new string('a', 65) })
                Assert.IsNull(SettingsTransfer.FileNameFor(bad), $"'{bad}' must be refused");
        }

        [TestMethod]
        public void Merge_KeepsTheTargetsServerOwnedParts()
        {
            var target = JObject.Parse(@"{""variationDefaults"":{},""ladderApplies"":{""15"":{""version"":7}},""gearLockSpots"":[],
                ""zoneWideBounty"":{""on"":false},""regionBounties"":{""prod"":{}}}");
            var incoming = JObject.Parse(ZoneJson);

            var merged = SettingsTransfer.MergeZoneControl(target, incoming, copyServerOwned: false);
            Assert.AreEqual(7, (int)merged["ladderApplies"]["15"]["version"], "the target's ladder state stays");
            Assert.AreEqual(0, ((JArray)merged["gearLockSpots"]).Count, "the target's gear-lock spots stay");
            Assert.IsFalse((bool)merged["zoneWideBounty"]["on"], "the target's bounty stays");
            Assert.IsNotNull(merged["regionBounties"]["prod"]);
            Assert.AreEqual(1.2, (double)merged["variationDefaults"]["15"]["profile"]["stats"]["monster_damage_mult"]["base"], "the balance comes over");

            var all = SettingsTransfer.MergeZoneControl(target, incoming, copyServerOwned: true);
            Assert.IsTrue(JToken.DeepEquals(incoming, all), "copyserver = the file exactly");
            Assert.IsTrue(JToken.DeepEquals(JObject.Parse(ZoneJson), incoming), "the incoming store is never changed in place");
        }

        [TestMethod]
        public void Preview_SameSettings_NoChanges()
        {
            var lines = SettingsTransfer.Preview(Sample(), Sample(), copyServerOwned: true);
            Assert.IsTrue(lines.Contains("Zone Control: no changes."), string.Join("\n", lines));
            Assert.IsTrue(lines.Contains("Weapon scaling: no changes."));
            Assert.IsTrue(lines.Contains("Charms: no changes."));
        }

        [TestMethod]
        public void Preview_SaysWhatChanges()
        {
            var changedZone = ZoneJson.Replace(@"""base"":1.2", @"""base"":1.26").Replace(@"""name"":""Tou Tou 15""", @"""name"":""Tou Tou 15b""");
            var lines = SettingsTransfer.Preview(Sample(), Sample(changedZone, share100: "0.7"), copyServerOwned: false);
            var text = string.Join("\n", lines);
            StringAssert.Contains(text, "T15 Default: 1 value(s) (monster_damage_mult 1.2 -> 1.26)");
            StringAssert.Contains(text, "zone Tou Tou 15: REMOVED");
            StringAssert.Contains(text, "zone Tou Tou 15b: new");
            StringAssert.Contains(text, "armorbreaker.share_100: 0.45 -> 0.7");
            StringAssert.Contains(text, "kept as this server has them");
            Assert.IsTrue(lines.All(l => l.All(c => c >= 32 && c <= 126)), "the preview is plain ASCII for the client");
        }

        [TestMethod]
        public void Preview_ReadsTheServersOwnPascalCaseExport()
        {
            // ExportStoreJson serializes the CLR names: Areas / Name / VariationDefaults / Profile / Stats / Base
            const string pascal = @"{""Areas"":[{""Name"":""Tou Tou 15"",""Variation"":15}],
                ""VariationDefaults"":{""15"":{""Profile"":{""Stats"":{""monster_damage_mult"":{""Base"":1.2,""Growth"":1.0}}}}}}";
            var changed = pascal.Replace(@"""Base"":1.2", @"""Base"":1.26").Replace(@"""Name"":""Tou Tou 15""", @"""Name"":""Tou Tou 15b""");
            var text = string.Join("\n", SettingsTransfer.ZonePreview(JObject.Parse(pascal), JObject.Parse(changed)));
            StringAssert.Contains(text, "T15 Default: 1 value(s) (monster_damage_mult 1.2 -> 1.26)");
            StringAssert.Contains(text, "zone Tou Tou 15: REMOVED");
            StringAssert.Contains(text, "zone Tou Tou 15b: new");
        }

        [TestMethod]
        public void Preview_ZoneNamesArePlainAscii()
        {
            var before = JObject.Parse(@"{""Areas"":[{""Name"":""Caf\u00e9 \u2014 15"",""Variation"":15}]}");
            var lines = SettingsTransfer.ZonePreview(before, new JObject());
            CollectionAssert.Contains(lines, "zone Caf? ? 15: REMOVED");
        }

        [TestMethod]
        public void Preview_CharmKeysTheFileLeavesOutGoBackToDefaults()
        {
            // the import resets the charm before applying the file, so a key the file omits returns to its default
            var current = Sample();
            var incoming = SettingsTransfer.Make("x", null, null, new Dictionary<string, IDictionary<string, string>>
            {
                ["armorbreaker"] = new Dictionary<string, string> { ["share"] = "0.2", ["enabled"] = "true" },
            }, DateTime.UtcNow);
            var defaults = new Dictionary<string, IDictionary<string, string>>
            {
                ["armorbreaker"] = new Dictionary<string, string> { ["share"] = "0.2", ["share_100"] = "0.7", ["enabled"] = "true" },
            };
            var text = string.Join("\n", SettingsTransfer.Preview(current, incoming, true, charmDefaults: defaults));
            StringAssert.Contains(text, "armorbreaker.share_100: 0.45 -> 0.7");
            StringAssert.Contains(text, "Charms: 1 setting(s) change:");
        }

        [TestMethod]
        public void CharmDefaults_AreWhatResetGives_AndLeaveTheLiveBlocksAlone()
        {
            CharmSettingsManager.ArmorBreaker.TrySet("share_100", "0.45");
            try
            {
                var defaults = CharmSettingsManager.ExportDefaults();
                Assert.AreEqual("0.7", defaults["armorbreaker"]["share_100"]);
                Assert.AreEqual("0.45", CharmSettingsManager.ExportAll()["armorbreaker"]["share_100"], "the live block is untouched");
                CollectionAssert.AreEquivalent(CharmSettingsManager.ExportAll().Keys.ToList(), defaults.Keys.ToList(), "every charm has defaults");
                foreach (var (name, keys) in CharmSettingsManager.ExportAll())
                    CollectionAssert.AreEquivalent(keys.Keys.ToList(), defaults[name].Keys.ToList(), name);
            }
            finally { CharmSettingsManager.ArmorBreaker.Reset(); }
        }

        [TestMethod]
        public void CharmExport_CoversEveryCharmWithItsLiveValues()
        {
            foreach (var block in CharmSettingsManager.LeveledBlocks.Values) block.Reset();
            var all = CharmSettingsManager.ExportAll();
            foreach (var name in new[] { "splitarrow", "chain", "pandemic", "improvedcleave", "armorbreaker", "shieldarc", "manabarrier", "fork" })
                Assert.IsTrue(all.ContainsKey(name), name);
            Assert.AreEqual("0.7", all["armorbreaker"]["share_100"], "the code default today");

            // a live change shows up in the export, and the export parses back into the same values
            CharmSettingsManager.ArmorBreaker.TrySet("share_100", "0.45");
            try
            {
                var bundle = SettingsTransfer.Make("x", null, null, CharmSettingsManager.ExportAll(), DateTime.UtcNow);
                var back = SettingsTransfer.Parse(SettingsTransfer.Serialize(bundle));
                Assert.AreEqual("0.45", back.Charms["armorbreaker"]["share_100"]);
                Assert.IsNull(back.ZoneControl, "an export without a store carries none (an import then leaves it alone)");
            }
            finally { CharmSettingsManager.ArmorBreaker.Reset(); }
        }
    }
}
