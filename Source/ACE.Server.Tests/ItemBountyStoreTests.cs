using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.Managers.ZoneScaling;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Item bounties (owner 2026-11-10) in the Zone Control store: a region's ItemBountyOdds on BountyConfig, the named
    /// reward tables (BountyRewardTable) and the drop weights per place (BountyPlaceWeights), plus the bounty_odds zone stat. Pins what
    /// ZoneStoreCompatTests pins for MinQb: the JSON round trip, the clones the kill hook reads, older stores reading the new
    /// values as unset, and zones that never set them writing nothing new.
    /// </summary>
    [TestClass]
    public class ItemBountyStoreTests
    {
        /// <summary>A region's item bounty odds survive the store (JSON) and the clone RegionItemBountyOdds reads.</summary>
        [TestMethod]
        public void ItemBountyOdds_RoundTripsAndClones()
        {
            var cfg = new BountyConfig { Enabled = true, ItemBountyOdds = 500 };
            Assert.AreEqual(500, JsonConvert.DeserializeObject<BountyConfig>(JsonConvert.SerializeObject(cfg)).ItemBountyOdds);
            Assert.AreEqual(500, cfg.Clone().ItemBountyOdds, "the region snapshot is built from clones");
            Assert.AreEqual(500, JsonConvert.DeserializeObject<BountyConfig>(JsonConvert.SerializeObject(cfg.Clone())).Clone().ItemBountyOdds);
        }

        /// <summary>A hand-edited store can't hold odds below 0 or above MaxItemBountyOdds: the clone holds them in range.</summary>
        [TestMethod]
        public void ItemBountyOdds_Clamped()
        {
            Assert.AreEqual(0, new BountyConfig { ItemBountyOdds = -1 }.Clone().ItemBountyOdds);
            Assert.AreEqual(0, new BountyConfig { ItemBountyOdds = int.MinValue }.Clone().ItemBountyOdds);
            Assert.AreEqual(BountyConfig.MaxItemBountyOdds, new BountyConfig { ItemBountyOdds = int.MaxValue }.Clone().ItemBountyOdds);
            Assert.AreEqual(BountyConfig.MaxItemBountyOdds, new BountyConfig { ItemBountyOdds = BountyConfig.MaxItemBountyOdds }.Clone().ItemBountyOdds);

            var hand = JsonConvert.DeserializeObject<BountyConfig>(@"{ ""Enabled"": true, ""NextId"": 1, ""Entries"": [], ""ItemBountyOdds"": -40 }");
            Assert.AreEqual(0, hand.Clone().ItemBountyOdds, "a negative value from the store reads as none");
        }

        /// <summary>
        /// Odds of 0 - every zone's own Bounty - write no key, so zone rows are saved exactly as before; a row saved before
        /// item bounties existed reads 0 = none drop.
        /// </summary>
        [TestMethod]
        public void ItemBountyOdds_ZeroIsNotWritten_OldDataReadsZero()
        {
            StringAssert.DoesNotMatch(JsonConvert.SerializeObject(new BountyConfig()), new Regex("ItemBountyOdds"));
            StringAssert.DoesNotMatch(JsonConvert.SerializeObject(new BountyConfig().Clone()), new Regex("ItemBountyOdds"));
            StringAssert.Contains(JsonConvert.SerializeObject(new BountyConfig { ItemBountyOdds = 7 }), "\"ItemBountyOdds\":7");

            const string old = @"{ ""Name"": ""Quarry"", ""Bounty"": { ""Enabled"": true, ""NextId"": 2,
                ""Entries"": [ { ""Id"": 1, ""Wcid"": 273, ""Amount"": 50, ""Kills"": 96, ""CooldownMinutes"": 40 } ] } }";
            var area = JsonConvert.DeserializeObject<ControlledArea>(old);
            Assert.AreEqual(0, area.Bounty.ItemBountyOdds);
            Assert.AreEqual(0, area.Bounty.Clone().ItemBountyOdds);
            StringAssert.DoesNotMatch(JsonConvert.SerializeObject(area), new Regex("ItemBountyOdds"), "a zone's row is written as before");
        }

        /// <summary>A reward table survives the store: its rows (a pulled weight-0 row included) and NextId; Valid is computed, never written.</summary>
        [TestMethod]
        public void RewardTable_RoundTrips_WithoutValid()
        {
            var t = new BountyRewardTable { NextId = 4 };
            t.Entries.Add(new BountyRewardEntry { Id = 1, Wcid = 43133, Amount = 50, Weight = 25 });
            t.Entries.Add(new BountyRewardEntry { Id = 3, Wcid = 987014, Amount = 1, Weight = 0 });

            var json = JsonConvert.SerializeObject(t);
            StringAssert.DoesNotMatch(json, new Regex("\"Valid\""), "Valid is [JsonIgnore]");

            var back = JsonConvert.DeserializeObject<BountyRewardTable>(json);
            Assert.AreEqual(4, back.NextId);
            Assert.AreEqual(2, back.Entries.Count, "the list is not doubled by the property's own initializer");
            Assert.AreEqual((1, 43133u, 50, 25), (back.Entries[0].Id, back.Entries[0].Wcid, back.Entries[0].Amount, back.Entries[0].Weight));
            Assert.AreEqual((3, 987014u, 1, 0), (back.Entries[1].Id, back.Entries[1].Wcid, back.Entries[1].Amount, back.Entries[1].Weight));

            var old = JsonConvert.DeserializeObject<BountyRewardTable>(@"{ ""Entries"": [ { ""Id"": 1, ""Wcid"": 43133 } ] }");
            Assert.AreEqual((1, 1), (old.Entries[0].Amount, old.Entries[0].Weight), "a row without Amount / Weight reads 1 / 1");
            Assert.IsTrue(old.Clone().NextId > 1, "a table without NextId still never reuses row 1's ID");
        }

        /// <summary>The private ZoneControlManager.Store type (the zone store JSON shape), reached by reflection.</summary>
        private static Type StoreType()
        {
            var storeType = typeof(ZoneControlManager).GetNestedType("Store", BindingFlags.NonPublic);
            Assert.IsNotNull(storeType, "ZoneControlManager.Store (the zone store JSON shape)");
            return storeType;
        }

        /// <summary>A store read from JSON exactly as ZoneControlManager.Load reads it (plain JsonConvert).</summary>
        private static object ReadStore(string json) => JsonConvert.DeserializeObject(json, StoreType());

        /// <summary>Store.BountyPlaceWeights of a store read by <see cref="ReadStore"/>.</summary>
        private static Dictionary<uint, BountyPlaceWeights> PlaceWeights(object store)
        {
            var prop = StoreType().GetProperty("BountyPlaceWeights");
            Assert.IsNotNull(prop, "Store.BountyPlaceWeights");
            return (Dictionary<uint, BountyPlaceWeights>)prop.GetValue(store);
        }

        /// <summary>
        /// The store itself: one saved before item bounties loads with no reward tables and no place weights, and a new one
        /// keeps both through a save - the off switch, and a place weight of 0 ("out of that pool"), which must not vanish.
        /// The store class is private to ZoneControlManager, so it is reached by reflection and read with JsonConvert exactly
        /// as Load does.
        /// </summary>
        [TestMethod]
        public void Store_OldLoads_NewRoundTrips()
        {
            var storeType = StoreType();
            var tablesProp = storeType.GetProperty("BountyRewardTables");
            var weightsProp = storeType.GetProperty("BountyPlaceWeights");
            Assert.IsNotNull(tablesProp, "Store.BountyRewardTables");
            Assert.IsNotNull(weightsProp, "Store.BountyPlaceWeights");
            Assert.AreEqual(typeof(Dictionary<uint, BountyPlaceWeights>), weightsProp.PropertyType, "per bounty WCID, its weights per place");
            Assert.IsNull(storeType.GetProperty("BountyDropWeights"), "the draft's global weights are gone (never live)");

            IDictionary Tables(object store) => (IDictionary)tablesProp.GetValue(store);

            var old = ReadStore(@"{ ""Areas"": [], ""RegionBounties"": { ""vod"": { ""Enabled"": true, ""NextId"": 1, ""Entries"": [] } } }");
            Assert.IsNotNull(Tables(old), "a store saved before item bounties: no tables, never null");
            Assert.AreEqual(0, Tables(old).Count);
            Assert.IsNotNull(PlaceWeights(old), "no place weights, never null");
            Assert.AreEqual(0, PlaceWeights(old).Count);

            const string json = @"{
                ""BountyRewardTables"": { ""mystery"": { ""Entries"": [ { ""Id"": 1, ""Wcid"": 43133, ""Amount"": 50, ""Weight"": 25 } ], ""NextId"": 2 } },
                ""BountyPlaceWeights"": { ""78780315"": { ""Off"": true, ""Places"": { ""t15"": 0, ""t16"": 1000 } },
                                          ""78780312"": { ""Places"": { ""vod"": 40 } } } }";
            var saved = JsonConvert.SerializeObject(ReadStore(json));
            StringAssert.DoesNotMatch(saved, new Regex("\"Valid\""));
            StringAssert.DoesNotMatch(saved, new Regex("\"IsEmpty\""), "IsEmpty is computed, never written");
            var back = ReadStore(saved);

            var table = (BountyRewardTable)Tables(back)["mystery"];
            Assert.IsNotNull(table);
            Assert.AreEqual(1, table.Entries.Count);
            Assert.AreEqual(43133u, table.Entries[0].Wcid);
            Assert.AreEqual(2, table.NextId);

            var weights = PlaceWeights(back);
            Assert.AreEqual(2, weights.Count);
            var rebirth = weights[78780315u];
            Assert.IsTrue(rebirth.Off, "the off switch survives a save");
            Assert.AreEqual(2, rebirth.Places.Count);
            Assert.AreEqual(0, rebirth.Places["t15"], "a weight of 0 means out of that pool: it must survive a save, not vanish");
            Assert.AreEqual(1000, rebirth.Places["t16"]);
            var second = weights[78780312u];
            Assert.IsFalse(second.Off, "a bounty saved without Off is on");
            Assert.AreEqual(1, second.Places.Count);
            Assert.AreEqual(40, second.Places["vod"]);
        }

        /// <summary>
        /// A store from the earlier draft, with the global "BountyDropWeights" key (WCID -> weight, never live), still loads
        /// without error and gives no place weights - a draft weight of 0 must not turn into "off" anywhere - and the next
        /// save writes the old key no more.
        /// </summary>
        [TestMethod]
        public void Store_DraftDropWeightsKey_IsIgnored()
        {
            const string draft = @"{ ""Areas"": [], ""BountyDropWeights"": { ""78780315"": 0, ""78780312"": 40 } }";
            var store = ReadStore(draft);
            Assert.IsNotNull(store, "the draft store still loads");
            Assert.IsNotNull(PlaceWeights(store));
            Assert.AreEqual(0, PlaceWeights(store).Count, "the draft's weights are not carried into the place weights");

            var saved = JsonConvert.SerializeObject(store);
            StringAssert.DoesNotMatch(saved, new Regex("BountyDropWeights"), "the next save drops the draft key");

            const string both = @"{ ""Areas"": [], ""BountyDropWeights"": { ""78780315"": 0 },
                ""BountyPlaceWeights"": { ""78780315"": { ""Places"": { ""t16"": 7 } } } }";
            var mixed = PlaceWeights(ReadStore(both));
            Assert.AreEqual(1, mixed.Count);
            Assert.IsFalse(mixed[78780315u].Off, "the draft's 0 is not an off switch");
            Assert.AreEqual(7, mixed[78780315u].Places["t16"], "only the new key counts");
        }

        /// <summary>
        /// Sanitize (Load and every edit run it with IsBountyPlaceKey): an unknown place - a typo, a tier outside 11-25, a
        /// region that isn't one - is dropped, a key is trimmed and lower-cased, and every weight is held to 0..MaxWeight.
        /// </summary>
        [TestMethod]
        public void PlaceWeights_SanitizeDropsUnknownPlacesAndClamps()
        {
            var set = new BountyPlaceWeights
            {
                Off = true,
                Places = new Dictionary<string, int>
                {
                    ["t15"] = 5, ["T16"] = 2_000_000, [" VoD "] = -3, ["t10"] = 0, ["t25"] = BountyRewardTable.MaxWeight,
                    ["t99"] = 7, ["atlantis"] = 1, ["t9"] = 2, ["t26"] = 3, [""] = 4, ["t1"] = 5,
                },
            };
            set.Sanitize(ZoneControlManager.IsBountyPlaceKey);

            Assert.IsTrue(set.Off, "Sanitize leaves the off switch alone");
            CollectionAssert.AreEquivalent(new[] { "t15", "t16", "vod", "t10", "t25" }, set.Places.Keys.ToArray());
            Assert.AreEqual(5, set.Places["t15"]);
            Assert.AreEqual(BountyRewardTable.MaxWeight, set.Places["t16"], "held to MaxWeight");
            Assert.AreEqual(0, set.Places["vod"], "a negative weight reads 0 (out of that pool), not a slice");
            Assert.AreEqual(0, set.Places["t10"], "a 0 is kept: it is a setting");
            Assert.AreEqual(BountyRewardTable.MaxWeight, set.Places["t25"]);
            Assert.IsFalse(set.Places.ContainsKey("t99"));
            Assert.IsFalse(set.Places.ContainsKey("atlantis"));
            Assert.IsTrue(set.Places.ContainsKey("T15"), "the cleaned dictionary still looks places up ignoring case");

            var none = new BountyPlaceWeights { Places = null };
            none.Sanitize(ZoneControlManager.IsBountyPlaceKey);
            Assert.IsNotNull(none.Places, "a store row with no Places reads as none, never null");
            Assert.IsTrue(none.IsEmpty);

            var onlyBad = new BountyPlaceWeights { Places = new Dictionary<string, int> { ["t99"] = 5, ["atlantis"] = 1 } };
            onlyBad.Sanitize(ZoneControlManager.IsBountyPlaceKey);
            Assert.IsTrue(onlyBad.IsEmpty, "an entry left with nothing set is empty (the store drops it)");
            Assert.IsFalse(new BountyPlaceWeights { Off = true }.IsEmpty, "off alone is a setting");
        }

        /// <summary>Clone is deep (snapshots and edits work on copies) and keeps the case-insensitive place lookup.</summary>
        [TestMethod]
        public void PlaceWeights_CloneIsDeep()
        {
            var live = new BountyPlaceWeights { Off = true, Places = new Dictionary<string, int> { ["t15"] = 1, ["t16"] = 1000 } };
            var copy = live.Clone();
            copy.Off = false;
            copy.Places["t15"] = 9;
            copy.Places.Remove("t16");

            Assert.IsTrue(live.Off, "the original's switch is untouched");
            Assert.AreEqual(1, live.Places["t15"], "the original's weights are untouched");
            Assert.AreEqual(1000, live.Places["t16"]);
            Assert.IsTrue(live.Clone().Places.ContainsKey("T16"), "a copy looks places up ignoring case");
            Assert.IsNotNull(new BountyPlaceWeights { Places = null }.Clone().Places, "a copy of a row with no Places has none, never null");
        }

        /// <summary>
        /// /settings import keeps the target server's own place weights (bounties already out there depend on them), matched
        /// whatever the case of the key (the server's export writes the CLR name), unless asked to copy them too.
        /// </summary>
        [TestMethod]
        public void SettingsImport_KeepsTheTargetsPlaceWeights()
        {
            CollectionAssert.Contains(SettingsTransfer.ServerOwnedZoneKeys, "bountyPlaceWeights");

            var target = JObject.Parse(@"{ ""Areas"": [], ""BountyPlaceWeights"": { ""78780315"": { ""Off"": false, ""Places"": { ""t16"": 1000 } } } }");
            var incoming = JObject.Parse(@"{ ""Areas"": [], ""BountyPlaceWeights"": { ""78780315"": { ""Off"": true, ""Places"": { ""t15"": 5 } } } }");

            var kept = SettingsTransfer.MergeZoneControl(target, incoming, copyServerOwned: false);
            Assert.AreEqual(1000, (int)kept["BountyPlaceWeights"]["78780315"]["Places"]["t16"], "the target's own weights are kept");
            Assert.IsFalse((bool)kept["BountyPlaceWeights"]["78780315"]["Off"]);
            Assert.IsNull(kept["BountyPlaceWeights"]["78780315"]["Places"]["t15"], "nothing of the incoming weights leaks in");

            var copied = SettingsTransfer.MergeZoneControl(target, incoming, copyServerOwned: true);
            Assert.IsTrue((bool)copied["BountyPlaceWeights"]["78780315"]["Off"], "copied when asked");
            Assert.AreEqual(5, (int)copied["BountyPlaceWeights"]["78780315"]["Places"]["t15"]);
        }

        /// <summary>bounty_odds is a zone stat appended after the previous tail of ZoneStat.All (append-only, name-matched wire).</summary>
        [TestMethod]
        public void BountyOdds_IsAppended()
        {
            var at = Array.IndexOf(ZoneStat.All, "bounty_odds");   // the store and the plugin match stats by this name
            Assert.IsTrue(at > 0, "bounty_odds is in ZoneStat.All under its wire name");
            Assert.AreEqual(at, Array.IndexOf(ZoneStat.All, ZoneStat.BountyOdds));
            Assert.AreEqual(ZoneStat.PcoinFalloffMult, ZoneStat.All[at - 1], "appended after the previous tail (Prestige Coin falloff)");
            Assert.AreEqual(at, Array.LastIndexOf(ZoneStat.All, ZoneStat.BountyOdds), "listed once");
        }
    }
}
