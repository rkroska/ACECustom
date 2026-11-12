using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.WorldObjects;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Item bounties (owner 2026-11-10; double-click rework 2026-11-12): the pure rules in ItemBounties - slot names (and the
    /// R1 / R2 / B1 / B2 names from before the rework), which slot a new bounty takes under the cap, the slot string on the
    /// character, which kills count, the tracker's wire format - the terms read off a bounty ITEM (a DB-free GenericObject),
    /// and the shipped defaults of its settings. No DB, no world.
    /// </summary>
    [TestClass]
    public class ItemBountiesTests
    {
        private static uint nextGuid = 0xF0005700;

        /// <summary>A Sweep (any kill) slot: 1,000 kills in T15 for a Bag of Rebirth.</summary>
        private static ItemBounties.Slot Sweep(string key, int tier = 15, int kills = 1000, double progress = 0)
            => new() { Key = key, Tier = tier, Kind = ItemBounties.KindSweep, Kills = kills, Progress = progress, RewardWcid = 78780315, RewardAmount = 1, AppliedUnix = 1794340000, Name = "Tier 15 Bounty: 1,000 Kills" };

        /// <summary>A Species slot: 300 kills of one creature type in T15.</summary>
        private static ItemBounties.Slot Species(string key, uint creatureType, int tier = 15, int kills = 300)
            => new() { Key = key, Tier = tier, Kind = ItemBounties.KindSpecies, Target = creatureType, Kills = kills, RewardWcid = 78780312, RewardAmount = 2, AppliedUnix = 1794340100, Name = "Tier 15 Bounty: 300 Banderlings", TargetName = "Banderlings" };

        /// <summary>A Valley of Death slot whose reward is a mystery (rolled from the table "secret_box" when done).</summary>
        private static ItemBounties.Slot VodMystery(string key, int kills = 1500, double progress = 12.5)
        {
            var s = Sweep(key, tier: 0, kills: kills, progress: progress);
            s.Region = "vod";
            s.RewardWcid = 0;
            s.RewardTableName = "secret_box";
            s.Mystery = ItemBounties.MysteryReward;
            return s;
        }

        /// <summary>A bounty weenie with the given int properties, the name "Test Bounty" and nothing else.</summary>
        private static Weenie BountyWeenie(uint wcid, params (PropertyInt Prop, int Value)[] ints)
        {
            var w = new Weenie
            {
                WeenieClassId = wcid, ClassName = "testbounty" + wcid, WeenieType = WeenieType.Generic,
                PropertiesInt = new Dictionary<PropertyInt, int>(),
                PropertiesString = new Dictionary<PropertyString, string> { [PropertyString.Name] = "Test Bounty" },
            };
            foreach (var (p, v) in ints) w.PropertiesInt[p] = v;
            return w;
        }

        /// <summary>The weenie made into an item the way EndgameVariationTests does it: a GenericObject, no DB.</summary>
        private static GenericObject MakeItem(Weenie w) => new(w, new ObjectGuid(nextGuid++));

        /// <summary>
        /// Runs <paramref name="body"/> in the culture <paramref name="name"/>: {:N0} groups digits by the current culture, so
        /// a test of a visible count pins it (as CharmCoinLedgerTests does) and passes on any machine. Shared with
        /// ItemBountyPlacesTests.
        /// </summary>
        internal static void InCulture(string name, Action body)
        {
            var culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            try
            {
                body();
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }

        /// <summary>Runs <paramref name="body"/> in en-US, so "1,000" matches whatever the machine's culture is.</summary>
        private static void InEnUs(Action body) => InCulture("en-US", body);

        // -- defaults --

        /// <summary>The settings ship on, with four bounties at once, a one-minute confirmation and the Bounty Journal 98760483.</summary>
        [TestMethod]
        public void Defaults()
        {
            Assert.IsTrue(ServerConfig.item_bounty_enabled.Default);
            Assert.AreEqual(4L, ServerConfig.item_bounty_max_active.Default);
            Assert.AreEqual(60L, ServerConfig.item_bounty_confirm_seconds.Default);
            Assert.AreEqual(98760483L, ServerConfig.item_bounty_journal_wcid.Default);
        }

        // -- slot names --

        /// <summary>Every way a player may type a slot reaches the same key - the number, #n, "slot n" - and anything else is refused.</summary>
        [TestMethod]
        public void ParseSlotKey_AcceptsNumbersAndWords()
        {
            Assert.AreEqual("1", ItemBounties.ParseSlotKey("1"));
            Assert.AreEqual("2", ItemBounties.ParseSlotKey("#2"));
            Assert.AreEqual("3", ItemBounties.ParseSlotKey("slot 3"));
            Assert.AreEqual("3", ItemBounties.ParseSlotKey("Slot #3"));
            Assert.AreEqual("4", ItemBounties.ParseSlotKey("bounty4"));
            Assert.AreEqual("8", ItemBounties.ParseSlotKey(" 8 "));

            Assert.IsNull(ItemBounties.ParseSlotKey("0"));
            Assert.IsNull(ItemBounties.ParseSlotKey("9"), "above MaxSlots");
            Assert.IsNull(ItemBounties.ParseSlotKey("12"));
            Assert.IsNull(ItemBounties.ParseSlotKey("#"));
            Assert.IsNull(ItemBounties.ParseSlotKey("r3"));
            Assert.IsNull(ItemBounties.ParseSlotKey("left ring"));
            Assert.IsNull(ItemBounties.ParseSlotKey("necklace"));
            Assert.IsNull(ItemBounties.ParseSlotKey(""));
            Assert.IsNull(ItemBounties.ParseSlotKey(null));
        }

        /// <summary>
        /// The slot names from before the double-click rework still work - typed or stored - as #1-#4: R1, R2 (the rings),
        /// then B1, B2 (the bracelets).
        /// </summary>
        [TestMethod]
        public void ParseSlotKey_LegacyNamesReadAsNumbers()
        {
            Assert.AreEqual("1", ItemBounties.ParseSlotKey("r1"));
            Assert.AreEqual("1", ItemBounties.ParseSlotKey("R1"));
            Assert.AreEqual("1", ItemBounties.ParseSlotKey("ring 1"));
            Assert.AreEqual("2", ItemBounties.ParseSlotKey("ring2"));
            Assert.AreEqual("2", ItemBounties.ParseSlotKey("Ring 2"));
            Assert.AreEqual("3", ItemBounties.ParseSlotKey("b1"));
            Assert.AreEqual("3", ItemBounties.ParseSlotKey("bracelet 1"));
            Assert.AreEqual("4", ItemBounties.ParseSlotKey("B2"));
            Assert.AreEqual("4", ItemBounties.ParseSlotKey("bracelet2"));
        }

        // -- which slot a bounty takes --

        /// <summary>The lowest free slot under the cap; #1 again once #1 is free, while #2 keeps its number through a save.</summary>
        [TestMethod]
        public void LowestFree_FillsLowestAndNeverShifts()
        {
            var slots = new Dictionary<string, ItemBounties.Slot>();
            Assert.AreEqual("1", ItemBounties.LowestFree(slots, 4));

            slots["1"] = Sweep("1");
            Assert.AreEqual("2", ItemBounties.LowestFree(slots, 4));

            slots["2"] = Sweep("2", kills: 2000);
            slots["3"] = Sweep("3");
            slots["4"] = Sweep("4");
            Assert.IsNull(ItemBounties.LowestFree(slots, 4), "all four going: refused, never overwritten");

            slots.Remove("1");   // #1 completed; #2 stays #2
            Assert.AreEqual("1", ItemBounties.LowestFree(slots, 4));

            var saved = ItemBounties.ParseSlots(ItemBounties.FormatSlots(slots));
            Assert.AreEqual(3, saved.Count);
            Assert.IsFalse(saved.ContainsKey("1"), "#2 does not move up into the free #1 when saved");
            Assert.AreEqual("2", saved["2"].Key);
            Assert.AreEqual(2000, saved["2"].Kills);
        }

        /// <summary>
        /// The cap counts every bounty going, even one in a slot above it (the cap was lowered after it started): a new one
        /// starts only while under the cap, and always in a slot within it.
        /// </summary>
        [TestMethod]
        public void LowestFree_RespectsTheCap()
        {
            var slots = new Dictionary<string, ItemBounties.Slot> { ["4"] = Sweep("4") };
            Assert.AreEqual("1", ItemBounties.LowestFree(slots, 2), "one going (above a cap of 2): #1 is free");

            slots["1"] = Sweep("1");
            Assert.IsNull(ItemBounties.LowestFree(slots, 2), "two going: at the cap of 2, though #2 is empty");
            Assert.AreEqual("2", ItemBounties.LowestFree(slots, 4));

            Assert.AreEqual("1", ItemBounties.LowestFree(new Dictionary<string, ItemBounties.Slot>(), 0), "a cap below 1 is held to 1");
            Assert.IsNull(ItemBounties.LowestFree(new Dictionary<string, ItemBounties.Slot> { ["1"] = Sweep("1") }, -5));

            var full = ItemBounties.SlotKeys.ToDictionary(k => k, k => Sweep(k));
            Assert.IsNull(ItemBounties.LowestFree(full, 99), "a cap above MaxSlots is held to it");
        }

        /// <summary>item_bounty_max_active is held to 1..MaxSlots, and ships at 4.</summary>
        [TestMethod]
        public void MaxActive_HeldToRange()
        {
            Assert.AreEqual(4, ItemBounties.MaxActive(4));
            Assert.AreEqual(1, ItemBounties.MaxActive(0));
            Assert.AreEqual(1, ItemBounties.MaxActive(-3));
            Assert.AreEqual(ItemBounties.MaxSlots, ItemBounties.MaxActive(100));
            Assert.AreEqual(4, ItemBounties.MaxActiveNow, "the shipped cap");
            Assert.AreEqual(ItemBounties.MaxSlots, ItemBounties.SlotKeys.Length);
        }

        /// <summary>The slots shown: #1 up to the cap, then any slot above it still holding a bounty; cap 0 = only the filled ones.</summary>
        [TestMethod]
        public void ShownKeys_CapThenStragglers()
        {
            var slots = new Dictionary<string, ItemBounties.Slot> { ["2"] = Sweep("2"), ["6"] = Sweep("6") };
            CollectionAssert.AreEqual(new[] { "1", "2", "3", "4", "6" }, ItemBounties.ShownKeys(slots, 4));
            CollectionAssert.AreEqual(new[] { "1", "2", "6" }, ItemBounties.ShownKeys(slots, 2));
            CollectionAssert.AreEqual(new[] { "2", "6" }, ItemBounties.ShownKeys(slots, 0));
            CollectionAssert.AreEqual(new[] { "1", "2", "3", "4" }, ItemBounties.ShownKeys(new Dictionary<string, ItemBounties.Slot>(), 4));
        }

        // -- the slot string on the character --

        /// <summary>Everything a slot holds survives a save and a load, decimals included.</summary>
        [TestMethod]
        public void Slots_RoundTrip()
        {
            var slots = new Dictionary<string, ItemBounties.Slot>
            {
                ["1"] = Sweep("1", progress: 640.25),
                ["4"] = Species("4", 2),
            };

            var back = ItemBounties.ParseSlots(ItemBounties.FormatSlots(slots));

            Assert.AreEqual(2, back.Count);
            Assert.AreEqual(640.25, back["1"].Progress, 1e-9);
            Assert.AreEqual(1000, back["1"].Kills);
            Assert.AreEqual(78780315u, back["1"].RewardWcid);
            Assert.AreEqual("Tier 15 Bounty: 1,000 Kills", back["1"].Name);
            Assert.AreEqual(ItemBounties.KindSpecies, back["4"].Kind);
            Assert.AreEqual(2u, back["4"].Target);
            Assert.AreEqual(2, back["4"].RewardAmount);
            Assert.AreEqual("Banderlings", back["4"].TargetName);
            Assert.AreEqual(slots["4"].Identity, back["4"].Identity);
            Assert.IsFalse(back["1"].IsRegion);
        }

        /// <summary>
        /// A character saved before the double-click rework: R1, R2, B1, B2 read as #1-#4 with everything they held, and the
        /// next save writes only the new keys - the same bounties, the same progress.
        /// </summary>
        [TestMethod]
        public void Slots_LegacyKeysMigrate()
        {
            const string stored = "R1|15|1|0|1000|640.25|78780315|1|1794340000|a|;R2|16|2|2|300|12|78780312|2|1794340100|b|Banderlings;"
                + "B1|0|1|0|500|5|78780315|1|1794340200|c||vod;B2|15|1|0|750|0|0|1|1794340300|d|||mystery|3";
            var slots = ItemBounties.ParseSlots(stored);
            CollectionAssert.AreEquivalent(new[] { "1", "2", "3", "4" }, slots.Keys.ToList());
            Assert.AreEqual("1", slots["1"].Key);
            Assert.AreEqual(640.25, slots["1"].Progress, 1e-9, "R1's progress, now #1's");
            Assert.AreEqual(16, slots["2"].Tier);
            Assert.AreEqual("Banderlings", slots["2"].TargetName);
            Assert.AreEqual("vod", slots["3"].Region, "B1 is #3");
            Assert.IsTrue(slots["4"].IsMysteryReward, "B2 is #4");

            var saved = ItemBounties.FormatSlots(slots);
            StringAssert.StartsWith(saved, "1|15|1|0|1000|640.25|");
            StringAssert.DoesNotMatch(saved, new Regex(@"(^|;)[RB][12]\|"), "no old key is written back");
            Assert.AreEqual(4, saved.Split(';').Length);

            var again = ItemBounties.ParseSlots(saved);
            foreach (var key in new[] { "1", "2", "3", "4" })
                Assert.AreEqual(slots[key].Identity, again[key].Identity, $"#{key} is the same bounty after the save");
        }

        /// <summary>A region bounty's region survives a save and a load; an entry saved before regions existed has none.</summary>
        [TestMethod]
        public void Slots_RoundTrip_Region()
        {
            var vod = Sweep("2", tier: 0, kills: 500, progress: 12);
            vod.Region = "vod";
            var back = ItemBounties.ParseSlots(ItemBounties.FormatSlots(new Dictionary<string, ItemBounties.Slot> { ["2"] = vod }));
            Assert.AreEqual("vod", back["2"].Region);
            Assert.IsTrue(back["2"].IsRegion);
            Assert.AreEqual(12, back["2"].Progress, 1e-9);

            var old = ItemBounties.ParseSlots("R1|15|1|0|100|5|78780315|1|0|x|");   // 11 fields: before 2026-11-10's regions (and R1: before the rework)
            Assert.AreEqual(1, old.Count);
            Assert.AreEqual("", old["1"].Region);
            Assert.AreEqual(15, old["1"].Tier);
        }

        /// <summary>The separators can't sneak in through a name, and bad entries are skipped rather than thrown.</summary>
        [TestMethod]
        public void Slots_SurviveBadInput()
        {
            var s = Sweep("1");
            s.Name = "Evil|Name;With separators";
            var back = ItemBounties.ParseSlots(ItemBounties.FormatSlots(new Dictionary<string, ItemBounties.Slot> { ["1"] = s }));
            Assert.AreEqual(1, back.Count);
            Assert.AreEqual("Evil Name With separators", back["1"].Name);

            Assert.AreEqual(0, ItemBounties.ParseSlots(null).Count);
            Assert.AreEqual(0, ItemBounties.ParseSlots("garbage").Count);
            Assert.AreEqual(0, ItemBounties.ParseSlots("R9|15|1|0|100|0|1|1|0|x|").Count, "unknown slot");
            Assert.AreEqual(0, ItemBounties.ParseSlots("9|15|1|0|100|0|1|1|0|x|").Count, "above MaxSlots");
            Assert.AreEqual(0, ItemBounties.ParseSlots("1|15|1|0|0|0|1|1|0|x|").Count, "zero kills");

            var clamped = ItemBounties.ParseSlots("1|15|1|0|100|250|78780315|1|0|x|");
            Assert.AreEqual(100, clamped["1"].Progress, 1e-9, "progress never beyond the kills");
        }

        /// <summary>
        /// A stored key in lower case, as a word or by its old name still reads as its slot; a slot named twice - also once
        /// by its old name and once by its new one - keeps its FIRST entry.
        /// </summary>
        [TestMethod]
        public void Slots_KeysAndDuplicates()
        {
            var dup = ItemBounties.ParseSlots("R1|15|1|0|100|0|78780315|1|0|first|;r1|15|1|0|200|0|78780315|1|0|second|");
            Assert.AreEqual(1, dup.Count);
            Assert.AreEqual("first", dup["1"].Name, "the first entry for a slot wins");
            Assert.AreEqual(100, dup["1"].Kills);

            var mixed = ItemBounties.ParseSlots("1|15|1|0|100|0|78780315|1|0|new|;R1|15|1|0|200|0|78780315|1|0|old|");
            Assert.AreEqual(1, mixed.Count);
            Assert.AreEqual("new", mixed["1"].Name, "R1 is #1: named twice, the first entry wins");

            var typed = ItemBounties.ParseSlots("r2|15|1|0|100|0|78780315|1|0|x|;ring 1|15|1|0|100|0|78780315|1|0|y|;#3|15|1|0|100|0|78780315|1|0|z|");
            CollectionAssert.AreEquivalent(new[] { "1", "2", "3" }, typed.Keys.ToList());
            Assert.AreEqual("2", typed["2"].Key);
            Assert.AreEqual("y", typed["1"].Name);
            Assert.AreEqual("z", typed["3"].Name);
        }

        /// <summary>An unreadable, infinite or negative progress reads 0, and a reward amount of 0 or less reads 1; the entry is kept.</summary>
        [TestMethod]
        public void Slots_BadNumbersFallBack()
        {
            foreach (var progress in new[] { "NaN", "-5", "Infinity", "abc", "" })
            {
                var s = ItemBounties.ParseSlots($"R1|15|1|0|100|{progress}|78780315|1|0|x|");
                Assert.AreEqual(1, s.Count, $"progress '{progress}' keeps the entry");
                Assert.AreEqual(0, s["1"].Progress, 1e-9, $"progress '{progress}'");
            }
            foreach (var amount in new[] { "0", "-3", "abc" })
            {
                var s = ItemBounties.ParseSlots($"1|15|1|0|100|0|78780315|{amount}|0|x|");
                Assert.AreEqual(1, s.Count, $"amount '{amount}' keeps the entry");
                Assert.AreEqual(1, s["1"].RewardAmount, $"amount '{amount}'");
            }
        }

        // -- which kills count --

        /// <summary>Exactly the bounty's tier; a Species bounty only its creature type; a Sweep any creature.</summary>
        [TestMethod]
        public void Matches_ExactTierAndSpecies()
        {
            var sweep = Sweep("1", tier: 15);
            Assert.IsTrue(ItemBounties.Matches(sweep, 15, null, 2));
            Assert.IsTrue(ItemBounties.Matches(sweep, 15, null, null));
            Assert.IsFalse(ItemBounties.Matches(sweep, 14, null, 2), "a lower tier never counts");
            Assert.IsFalse(ItemBounties.Matches(sweep, 16, null, 2), "nor a higher one: exactly the bounty's tier");
            Assert.IsFalse(ItemBounties.Matches(sweep, 0, "vod", 2), "a region kill never counts for a tier bounty");

            var bander = Species("2", 2, tier: 15);
            Assert.IsTrue(ItemBounties.Matches(bander, 15, null, 2));
            Assert.IsFalse(ItemBounties.Matches(bander, 15, null, 3), "another creature type");
            Assert.IsFalse(ItemBounties.Matches(bander, 15, null, null));
            Assert.IsFalse(ItemBounties.Matches(bander, 14, null, 2));
        }

        /// <summary>A region bounty: only kills in its region (any case), never a tier kill or another region's.</summary>
        [TestMethod]
        public void Matches_Region()
        {
            var vod = Sweep("3", tier: 0);
            vod.Region = "vod";
            Assert.IsTrue(ItemBounties.Matches(vod, 0, "vod", 7));
            Assert.IsTrue(ItemBounties.Matches(vod, 0, "VOD", null));
            Assert.IsFalse(ItemBounties.Matches(vod, 0, "t10", 7), "another region");
            Assert.IsFalse(ItemBounties.Matches(vod, 0, null, 7), "not in a region");
            Assert.IsFalse(ItemBounties.Matches(vod, 15, null, 7), "a T11+ kill");

            var t10Species = Species("1", 2, tier: 0);
            t10Species.Region = "t10";
            Assert.IsTrue(ItemBounties.Matches(t10Species, 0, "t10", 2));
            Assert.IsFalse(ItemBounties.Matches(t10Species, 0, "t10", 3), "another creature type");
        }

        /// <summary>
        /// A broken slot never counts: a tier-0 slot with no region (a corrupted entry) never matches a region kill, which
        /// arrives with killTier 0; an unknown kind never matches; nor does no slot at all.
        /// </summary>
        [TestMethod]
        public void Matches_BrokenSlotsNeverCount()
        {
            var tier0 = new ItemBounties.Slot { Key = "1", Tier = 0, Kind = ItemBounties.KindSweep, Kills = 100, RewardWcid = 5 };
            Assert.IsFalse(tier0.IsRegion);
            Assert.IsFalse(ItemBounties.Matches(tier0, 0, "vod", 2), "a Valley of Death kill must not count for a tier-0 slot");
            Assert.IsFalse(ItemBounties.Matches(tier0, 0, null, 2), "nor a kill outside every zone");

            var kind3 = Sweep("1", tier: 15);
            kind3.Kind = 3;
            Assert.IsFalse(ItemBounties.Matches(kind3, 15, null, 2), "kind 3 is neither Sweep nor Species");
            Assert.IsFalse(ItemBounties.Matches(kind3, 15, null, null));

            Assert.IsFalse(ItemBounties.Matches(null, 15, null, 2));
        }

        /// <summary>Where a bounty counts, as /bounty and the tracker say it.</summary>
        [TestMethod]
        public void Place_Labels()
        {
            var tier = Sweep("1", tier: 15);
            Assert.AreEqual("T15", ItemBounties.PlaceShort(tier));
            Assert.AreEqual("Tier 15", ItemBounties.PlaceName(tier));

            var vod = Sweep("1", tier: 0);
            vod.Region = "vod";
            Assert.AreEqual("VoD", ItemBounties.PlaceShort(vod));
            Assert.AreEqual("Valley of Death", ItemBounties.PlaceName(vod));

            var t10 = Sweep("1", tier: 0);
            t10.Region = "t10";
            Assert.AreEqual("T10", ItemBounties.PlaceShort(t10));
            Assert.AreEqual("Thaelaryn Island", ItemBounties.PlaceName(t10));
        }

        // -- the rolled kill count --

        /// <summary>The roll spans the whole range, lands on round numbers, never leaves the range, and is exact for a given roll.</summary>
        [TestMethod]
        public void RollKills_StaysInRange_OnRoundNumbers()
        {
            Assert.AreEqual(1500, ItemBounties.RollKills(1500, 3000, 0.0));
            Assert.AreEqual(2250, ItemBounties.RollKills(1500, 3000, 0.5));
            Assert.AreEqual(3000, ItemBounties.RollKills(1500, 3000, 0.9999999));
            for (var i = 0; i < 1000; i++)
            {
                var k = ItemBounties.RollKills(1500, 3000, i / 1000.0);
                Assert.IsTrue(k >= 1500 && k <= 3000, $"{k} outside 1,500-3,000");
                Assert.AreEqual(0, k % 50, $"{k} is not a round 50");
            }

            Assert.AreEqual(300, ItemBounties.RollKills(200, 400, 0.5), "the middle of 200-400, to the 10");

            Assert.AreEqual(5, ItemBounties.RollKills(5, 15, 0.0));
            Assert.AreEqual(15, ItemBounties.RollKills(5, 15, 0.9999999));
        }

        /// <summary>Every round number from the bottom of the range to the top can come up: nothing in the range is unreachable.</summary>
        [TestMethod]
        public void RollKills_EveryStepComesUp()
        {
            const int n = 10_000;
            var seen = new HashSet<int>();
            for (var i = 0; i < n; i++)
                seen.Add(ItemBounties.RollKills(1500, 3000, i / (double)n));
            for (var k = 1500; k <= 3000; k += 50)
                Assert.IsTrue(seen.Contains(k), $"{k} never comes up");
            Assert.AreEqual(31, seen.Count, "nothing but the 31 round numbers");

            seen.Clear();
            for (var i = 0; i < n; i++)
                seen.Add(ItemBounties.RollKills(5, 15, i / (double)n));
            CollectionAssert.AreEquivalent(Enumerable.Range(5, 11).ToList(), seen.ToList(), "below 100 every count can come up");
        }

        /// <summary>A backwards, a fixed or a silly range still gives a usable count; a backwards range rolls as the forward one.</summary>
        [TestMethod]
        public void RollKills_OddRanges()
        {
            Assert.AreEqual(1000, ItemBounties.RollKills(1000, 1000, 0.7), "min = max is a fixed count");
            Assert.AreEqual(ItemBounties.RollKills(1500, 3000, 0.5), ItemBounties.RollKills(3000, 1500, 0.5), "a backwards range is swapped");
            Assert.AreEqual(2250, ItemBounties.RollKills(3000, 1500, 0.5));
            Assert.AreEqual(1500, ItemBounties.RollKills(3000, 1500, 0.0), "swapped, a roll of 0 is the low end");
            Assert.AreEqual(1, ItemBounties.RollKills(0, 0, 0.5), "never zero kills");
            Assert.AreEqual(BountyManager.MaxKills, ItemBounties.RollKills(int.MaxValue, int.MaxValue, 0.5), "never past the cap");
        }

        /// <summary>A range whose ends are not on the rounding step (or narrower than one step) still never leaves the range.</summary>
        [TestMethod]
        public void RollKills_MisalignedRanges_StayInRange()
        {
            foreach (var (min, max) in new[] { (1234, 3000), (1010, 1040), (999, 1001), (105, 115), (95, 105) })
                for (var i = 0; i < 1000; i++)
                {
                    var k = ItemBounties.RollKills(min, max, i / 1000.0);
                    Assert.IsTrue(k >= min && k <= max, $"{k} outside {min}-{max} at u={i / 1000.0}");
                }
        }

        // -- monster drops --

        /// <summary>Each weight gets its own slice of the roll; zero and negative weights never come up; nothing to pick = -1.</summary>
        [TestMethod]
        public void WeightedPick_SlicesByWeight()
        {
            var w = new[] { 50, 10, 1 };   // the coin box, Rebirth, Vengeance
            Assert.AreEqual(0, ItemBounties.WeightedPick(w, 0.0));
            Assert.AreEqual(0, ItemBounties.WeightedPick(w, 49.9 / 61));
            Assert.AreEqual(1, ItemBounties.WeightedPick(w, 50.1 / 61));
            Assert.AreEqual(2, ItemBounties.WeightedPick(w, 60.5 / 61));
            Assert.AreEqual(2, ItemBounties.WeightedPick(w, 0.9999999999));

            Assert.AreEqual(1, ItemBounties.WeightedPick(new[] { 0, 5, 0 }, 0.99), "zero weights never come up");
            Assert.AreEqual(1, ItemBounties.WeightedPick(new[] { -5, 5 }, 0.0), "a negative weight counts as 0, not as a slice");
            Assert.AreEqual(1, ItemBounties.WeightedPick(new[] { -5, 5 }, 0.9999999999));
            Assert.AreEqual(-1, ItemBounties.WeightedPick(new[] { -5, -5 }, 0.5));
            Assert.AreEqual(-1, ItemBounties.WeightedPick(new[] { 0, 0 }, 0.5));
            Assert.AreEqual(-1, ItemBounties.WeightedPick(Array.Empty<int>(), 0.5));
        }

        /// <summary>Only bounties whose tier band holds the kill's tier, with a weight, can drop there.</summary>
        [TestMethod]
        public void Eligible_ByTierBand()
        {
            var table = new List<ItemBounties.DropEntry>
            {
                new(1, 50, 11, 25, false, "coins"),
                new(2, 10, 11, 15, false, "rebirth"),
                new(3, 1, 15, 15, true, "vengeance T15"),
                new(4, 0, 11, 25, false, "quest only"),
            };
            CollectionAssert.AreEqual(new uint[] { 1, 2 }, ItemBounties.Eligible(table, 11).ConvertAll(e => e.Wcid));
            CollectionAssert.AreEqual(new uint[] { 1, 2, 3 }, ItemBounties.Eligible(table, 15).ConvertAll(e => e.Wcid));
            CollectionAssert.AreEqual(new uint[] { 1 }, ItemBounties.Eligible(table, 20).ConvertAll(e => e.Wcid));
        }

        /// <summary>Region bounties drop only in their region, and never in a tier; tier bounties never in a region.</summary>
        [TestMethod]
        public void Eligible_RegionsApart()
        {
            var table = new List<ItemBounties.DropEntry>
            {
                new(1, 50, 11, 25, false, "tier coins"),
                new(5, 10, 0, 0, true, "vod coins", "vod"),
                new(6, 10, 0, 0, true, "t10 coins", "t10"),
                new(7, 0, 0, 0, true, "vod quest only", "vod"),
            };
            CollectionAssert.AreEqual(new uint[] { 5 }, ItemBounties.EligibleInRegion(table, "vod").ConvertAll(e => e.Wcid));
            CollectionAssert.AreEqual(new uint[] { 6 }, ItemBounties.EligibleInRegion(table, "T10").ConvertAll(e => e.Wcid));
            Assert.AreEqual(0, ItemBounties.EligibleInRegion(table, null).Count);
            CollectionAssert.AreEqual(new uint[] { 1 }, ItemBounties.Eligible(table, 11).ConvertAll(e => e.Wcid), "a region bounty never drops in a tier");
        }

        /// <summary>A weenie's drop entry: a fixed tier pins the band; no tier = the band (default 11-25); no weight = none.</summary>
        [TestMethod]
        public void EntryFor_ReadsTheWeenie()
        {
            var weight = (PropertyInt.ItemBountyDropWeight, 10);
            var reward = (PropertyInt.ItemBountyRewardWcid, 78780315);
            var range = (PropertyInt.ItemBountyKillsMin, 1000);

            var any = ItemBounties.EntryFor(BountyWeenie(99985001, weight, reward, range));
            Assert.IsTrue(any.HasValue);
            Assert.AreEqual((11, 25, false), (any.Value.MinTier, any.Value.MaxTier, any.Value.FixedTier));

            var band = ItemBounties.EntryFor(BountyWeenie(99985001, weight, reward, range,
                (PropertyInt.ItemBountyDropTierMin, 13), (PropertyInt.ItemBountyDropTierMax, 16)));
            Assert.AreEqual((13, 16), (band.Value.MinTier, band.Value.MaxTier));

            var fixedTier = ItemBounties.EntryFor(BountyWeenie(99985001, weight, reward, range,
                (PropertyInt.ItemBountyTier, 15), (PropertyInt.ItemBountyDropTierMin, 11)));
            Assert.AreEqual((15, 15, true), (fixedTier.Value.MinTier, fixedTier.Value.MaxTier, fixedTier.Value.FixedTier));

            Assert.IsNull(ItemBounties.EntryFor(BountyWeenie(99985001, reward, range)), "no weight: quest / NPC only");
            Assert.IsNull(ItemBounties.EntryFor(BountyWeenie(99985001, weight, range)), "no reward");
            Assert.IsNull(ItemBounties.EntryFor(BountyWeenie(99985001, weight, reward)), "no kill count");

            // the terms a made item needs (TermsOf), so a bounty nobody could apply never drops
            var species = (PropertyInt.ItemBountyKind, ItemBounties.KindSpecies);
            Assert.IsTrue(ItemBounties.EntryFor(BountyWeenie(99985001, weight, reward, range, species, (PropertyInt.ItemBountyTarget, 2))).HasValue);
            Assert.IsNull(ItemBounties.EntryFor(BountyWeenie(99985001, weight, reward, range, species)), "a Species bounty with no target");
            Assert.IsNull(ItemBounties.EntryFor(BountyWeenie(99985001, weight, reward, range, (PropertyInt.ItemBountyKind, 3))), "an unknown kind");
            Assert.IsNull(ItemBounties.EntryFor(BountyWeenie(99985001, weight, reward, (PropertyInt.ItemBountyKills, BountyManager.MaxKills + 1))),
                "a fixed count above MaxKills");

            // a region bounty: the region wins over any tier fields; an unknown region can't drop anywhere
            var vod = BountyWeenie(99985001, weight, reward, range, (PropertyInt.ItemBountyTier, 15));
            vod.PropertiesString[PropertyString.ItemBountyRegion] = " VoD ";
            var vodEntry = ItemBounties.EntryFor(vod);
            Assert.IsTrue(vodEntry.HasValue);
            Assert.AreEqual("vod", vodEntry.Value.Region);
            Assert.AreEqual(0, ItemBounties.Eligible(new[] { vodEntry.Value }, 15).Count, "never in its old tier");

            var nowhere = BountyWeenie(99985001, weight, reward, range);
            nowhere.PropertiesString[PropertyString.ItemBountyRegion] = "atlantis";
            Assert.IsNull(ItemBounties.EntryFor(nowhere), "unknown region");
        }

        /// <summary>The progress words: kills for a Sweep, the target's name for a Species bounty (en-US digit grouping).</summary>
        [TestMethod]
        public void ProgressText_Words()
        {
            InEnUs(() =>
            {
                Assert.AreEqual("640 of 1,000 kills", ItemBounties.ProgressText(Sweep("1", progress: 640)));
                Assert.AreEqual("0 of 300 Banderlings", ItemBounties.ProgressText(Species("3", 2)));
                Assert.AreEqual("12.5 of 1,500 kills", ItemBounties.ProgressText(Sweep("1", kills: 1500, progress: 12.5)));
            });
        }

        // -- MYSTERY bounties and Zone Control reward tables (owner 2026-11-10) --

        /// <summary>The pick follows the weights; an empty list picks nothing.</summary>
        [TestMethod]
        public void PickReward_ByWeight()
        {
            var t = new List<ItemBounties.RewardOption> { new(987014, 1, 70), new(43133, 50, 25), new(78780315, 1, 5) };
            Assert.AreEqual(987014u, ItemBounties.PickReward(t, 0.0).Value.Wcid);
            Assert.AreEqual(987014u, ItemBounties.PickReward(t, 0.69).Value.Wcid);
            Assert.AreEqual(43133u, ItemBounties.PickReward(t, 0.71).Value.Wcid);
            Assert.AreEqual(50, ItemBounties.PickReward(t, 0.71).Value.Amount);
            Assert.AreEqual(78780315u, ItemBounties.PickReward(t, 0.97).Value.Wcid);
            Assert.IsNull(ItemBounties.PickReward(new List<ItemBounties.RewardOption>(), 0.5));
        }

        /// <summary>
        /// A reward table from the store is made safe: bad rows can't be chosen, amounts and weights are held, IDs unique - in
        /// the COPY only: the table it was cloned from (the live store's) is left exactly as it was.
        /// </summary>
        [TestMethod]
        public void RewardTable_Sanitize()
        {
            var t = new BountyRewardTable
            {
                NextId = 1,
                Entries = new List<BountyRewardEntry>
                {
                    new() { Id = 1, Wcid = 987014, Amount = 0, Weight = 60 },
                    new() { Id = 1, Wcid = 43133, Amount = 999999999, Weight = 30 },   // a duplicate ID
                    new() { Id = 7, Wcid = 0, Amount = 1, Weight = 10 },              // no item: kept, never chosen
                    null,
                    new() { Id = 8, Wcid = 78780315, Amount = 1, Weight = int.MaxValue },
                    new() { Id = -4, Wcid = 5, Weight = -7 },                          // no valid ID, a negative weight
                },
            };
            var c = t.Clone();
            Assert.AreEqual(5, c.Entries.Count, "the null row is dropped");
            Assert.AreEqual(1, c.Entries[0].Amount, "amount 0 -> 1");
            Assert.AreEqual(BountyManager.MaxAmount, c.Entries[1].Amount, "a huge amount -> MaxAmount");
            Assert.AreEqual(BountyRewardTable.MaxWeight, c.Entries[3].Weight);
            Assert.AreEqual(0, c.Entries[4].Weight, "a negative weight -> 0");
            Assert.IsTrue(c.Entries[4].Id > 0, "a row with no valid ID gets a fresh one");
            Assert.AreEqual(5, c.Entries.Select(e => e.Id).Distinct().Count(), "every ID unique");
            Assert.IsTrue(c.NextId > c.Entries.Max(e => e.Id));
            Assert.AreEqual(3, c.Valid.Count(), "the row with no item and the row with weight 0 can't be chosen");
            Assert.IsFalse(c.Valid.Any(e => e.Wcid == 5));

            // the source is untouched: Clone is a deep copy, so sanitizing it never rewrites the store's live table
            Assert.AreEqual(6, t.Entries.Count, "the null row is still in the source");
            Assert.IsNull(t.Entries[3]);
            Assert.AreEqual(0, t.Entries[0].Amount);
            Assert.AreEqual(999999999, t.Entries[1].Amount);
            Assert.AreEqual(1, t.Entries[1].Id, "the duplicate ID is only moved in the copy");
            Assert.AreEqual(int.MaxValue, t.Entries[4].Weight);
            Assert.AreEqual((-4, -7), (t.Entries[5].Id, t.Entries[5].Weight));
            Assert.AreEqual(1, t.NextId);
            Assert.AreNotSame(t.Entries[0], c.Entries[0], "the copy's rows are its own");

            var big = new BountyRewardTable();
            for (var i = 1; i <= BountyRewardTable.MaxEntries + 5; i++)
                big.Entries.Add(new() { Id = i, Wcid = (uint)i, Amount = 1, Weight = 1 });
            Assert.AreEqual(BountyRewardTable.MaxEntries, big.Clone().Entries.Count);
            Assert.AreEqual(BountyRewardTable.MaxEntries + 5, big.Entries.Count, "the source keeps every row");
        }

        /// <summary>
        /// Table names: lower-case words, optionally ".place" for a place a bounty can count in (t11-t25 or a region); nothing
        /// that could break a slot string or a command, and no place no bounty ever reads.
        /// </summary>
        [TestMethod]
        public void RewardTable_Names()
        {
            foreach (var ok in new[] { "mystery", "mystery.t15", "mystery.t11", "mystery.t25", "mystery.vod", "mystery.t10", "big_box-2", "a",
                                       new string('a', 32) })
                Assert.IsTrue(ZoneControlManager.IsValidRewardTableName(ok), ok);
            foreach (var bad in new[] { "", null, "Mystery", "my table", "a|b", "a;b", "a.b.c", "x.", ".t15", new string('a', 33),
                                        "mystery\n", "mystery.t15\n", "mystery.abcdefghi",
                                        "mystery.t51", "mystery.t26", "mystery.t09", "mystery.atlantis", "mystery.T15" })
                Assert.IsFalse(ZoneControlManager.IsValidRewardTableName(bad), bad == null ? "null" : bad.Replace("\n", "\\n"));
        }

        /// <summary>
        /// The place's own table wins ("mystery.t15", "mystery.vod"), else the base one; a table with rows but nothing that
        /// can be chosen (weight 0, no item) doesn't count.
        /// </summary>
        [TestMethod]
        public void RewardTable_ResolvesByPlace()
        {
            static BountyRewardTable T(params uint[] wcids)
            {
                var t = new BountyRewardTable();
                foreach (var w in wcids) t.Entries.Add(new() { Id = t.NextId++, Wcid = w, Amount = 1, Weight = 1 });
                return t;
            }
            var tables = new Dictionary<string, BountyRewardTable>
            {
                ["mystery"] = T(1, 2),
                ["mystery.t15"] = T(3),
                ["mystery.vod"] = T(),   // empty: falls through to "mystery"
            };
            Assert.AreEqual("mystery.t15", ZoneControlManager.ResolveRewardTable(tables, "mystery", "t15")?.Name);
            Assert.AreEqual("mystery", ZoneControlManager.ResolveRewardTable(tables, "mystery", "t11")?.Name);
            Assert.AreEqual("mystery", ZoneControlManager.ResolveRewardTable(tables, "mystery", "vod")?.Name);
            Assert.IsNull(ZoneControlManager.ResolveRewardTable(tables, "nothing", "t15"));
            Assert.AreEqual("t15", ItemBounties.PlaceKey(Sweep("1", tier: 15)));

            // a REGION bounty looks for its region's own table: PlaceKey is the region key, never "t0"
            var vod = VodMystery("1");
            var t10 = Sweep("1", tier: 0);
            t10.Region = "t10";
            Assert.AreEqual("vod", ItemBounties.PlaceKey(vod));
            Assert.AreEqual("t10", ItemBounties.PlaceKey(t10));
            var withVod = new Dictionary<string, BountyRewardTable> { ["mystery"] = T(1, 2), ["mystery.vod"] = T(9) };
            var resolved = ZoneControlManager.ResolveRewardTable(withVod, "mystery", ItemBounties.PlaceKey(vod));
            Assert.AreEqual("mystery.vod", resolved?.Name);
            Assert.AreEqual(9u, resolved.Value.Table.Valid.Single().Wcid);
            Assert.AreEqual("mystery", ZoneControlManager.ResolveRewardTable(withVod, "mystery", ItemBounties.PlaceKey(t10))?.Name);

            // rows, but none that can be chosen: the place's table doesn't count, and a base table like it resolves to nothing
            BountyRewardTable Unchoosable() => new()
            {
                NextId = 3,
                Entries = { new() { Id = 1, Wcid = 3, Amount = 1, Weight = 0 }, new() { Id = 2, Wcid = 0, Amount = 1, Weight = 5 } },
            };
            var pulled = new Dictionary<string, BountyRewardTable> { ["mystery"] = T(1), ["mystery.t15"] = Unchoosable() };
            Assert.AreEqual("mystery", ZoneControlManager.ResolveRewardTable(pulled, "mystery", "t15")?.Name);
            Assert.IsNull(ZoneControlManager.ResolveRewardTable(new Dictionary<string, BountyRewardTable> { ["mystery"] = Unchoosable() }, "mystery", "t15"));
        }

        /// <summary>
        /// With a payable test (an item that can be made), a place table whose rows are all unpayable doesn't hide a good base
        /// table, and a base table like it resolves to nothing.
        /// </summary>
        [TestMethod]
        public void RewardTable_ResolvesPastUnpayableRows()
        {
            static BountyRewardTable T(params uint[] wcids)
            {
                var t = new BountyRewardTable();
                foreach (var w in wcids) t.Entries.Add(new() { Id = t.NextId++, Wcid = w, Amount = 1, Weight = 1 });
                return t;
            }
            static bool Payable(BountyRewardEntry e) => e.Wcid != 666;   // 666 stands for a reward that can't be made
            var tables = new Dictionary<string, BountyRewardTable> { ["mystery"] = T(1, 2), ["mystery.t15"] = T(666), ["mystery.vod"] = T(666, 9) };

            Assert.AreEqual("mystery.t15", ZoneControlManager.ResolveRewardTable(tables, "mystery", "t15")?.Name, "without the test every valid row counts");
            Assert.AreEqual("mystery", ZoneControlManager.ResolveRewardTable(tables, "mystery", "t15", Payable)?.Name, "nothing payable at t15: the base table");
            Assert.AreEqual("mystery.vod", ZoneControlManager.ResolveRewardTable(tables, "mystery", "vod", Payable)?.Name, "one payable row is enough");
            Assert.IsNull(ZoneControlManager.ResolveRewardTable(new Dictionary<string, BountyRewardTable> { ["mystery"] = T(666) }, "mystery", "t15", Payable));
        }

        /// <summary>A mystery reward's table name and the flags survive a save and a load; a slot that could never pay is dropped.</summary>
        [TestMethod]
        public void Slots_RoundTrip_Mystery()
        {
            var m = Sweep("3", tier: 15, kills: 750);
            m.RewardWcid = 0;
            m.RewardTableName = "mystery";
            m.Mystery = ItemBounties.MysteryKills | ItemBounties.MysteryReward;
            var back = ItemBounties.ParseSlots(ItemBounties.FormatSlots(new Dictionary<string, ItemBounties.Slot> { ["3"] = m }));
            Assert.IsTrue(back["3"].IsMysteryReward);
            Assert.AreEqual("mystery", back["3"].RewardTableName);
            Assert.AreEqual(3, back["3"].Mystery);
            Assert.AreEqual(750, back["3"].Kills);
            Assert.AreEqual(ItemBounties.MysteryRewardText, ItemBounties.RewardText(back["3"]));

            Assert.AreEqual(0, ItemBounties.ParseSlots("R1|15|1|0|100|0|0|1|0|x||||0").Count, "no reward wcid and not a mystery: nothing could pay");
            Assert.AreEqual(0, ItemBounties.ParseSlots("R1|15|1|0|100|0|0|1|0|x|||Bad Name|2").Count, "a mystery without a valid table name");
            Assert.AreEqual(1, ItemBounties.ParseSlots("R1|15|1|0|100|0|0|1|0|x|||mystery|2").Count);
        }

        /// <summary>
        /// A mystery bounty's description hides the count and the reward, never the place: neither end of the kill range nor
        /// the reward table's name may appear in it.
        /// </summary>
        [TestMethod]
        public void Describe_Mystery()
        {
            var t = Sweep("1", tier: 15, kills: 0);
            t.RewardWcid = 0;
            t.Mystery = ItemBounties.MysteryKills | ItemBounties.MysteryReward;
            t.RewardTableName = "secret_box";
            t.KillsMin = 300;
            t.KillsMax = 1500;
            Assert.IsTrue(ItemBounties.KillsHidden(t));

            var vod = VodMystery("1", kills: 0);
            vod.Mystery |= ItemBounties.MysteryKills;
            vod.KillsMin = 300;
            vod.KillsMax = 1500;

            InEnUs(() =>
            {
                var text = ItemBounties.Describe(t);
                StringAssert.Contains(text, "Kill a mystery number of creatures in Tier 15");
                StringAssert.Contains(text, "only in Tier 15");
                StringAssert.Contains(text, "Reward: a mystery");
                StringAssert.Contains(text, "To start it, double-click it and then click your Bounty Journal", "how to start it");
                StringAssert.Contains(text, "click yourself instead and one is added to your pack", "and how to get a journal");
                StringAssert.Contains(text, "You can have up to 4 bounties going at once", "the cap");
                StringAssert.DoesNotMatch(text, new Regex(@"\b(ring|bracelet)s?\b", RegexOptions.IgnoreCase), "no jewelry since the double-click rework");
                StringAssert.DoesNotMatch(text, new Regex("300|1,?500|secret_box"), "appraisal must not reveal the range or the table");

                var region = ItemBounties.Describe(vod);
                StringAssert.Contains(region, "Kill a mystery number of creatures in Valley of Death", "the place is never hidden");
                StringAssert.DoesNotMatch(region, new Regex("300|1,?500|secret_box"));
            });
        }

        /// <summary>A visible count is stated exactly (en-US grouping), even when only the reward is a mystery.</summary>
        [TestMethod]
        public void Describe_VisibleCount()
        {
            var sweep = Sweep("1", tier: 15, kills: 1000);
            sweep.RewardWcid = 0;
            sweep.Mystery = ItemBounties.MysteryReward;
            sweep.RewardTableName = "secret_box";

            var bander = Species("2", 2, kills: 1500);
            bander.RewardWcid = 0;
            bander.Mystery = ItemBounties.MysteryReward;
            bander.RewardTableName = "secret_box";

            Assert.IsFalse(ItemBounties.KillsHidden(sweep));
            InEnUs(() =>
            {
                StringAssert.StartsWith(ItemBounties.Describe(sweep), "Kill 1,000 creatures in Tier 15.");
                StringAssert.StartsWith(ItemBounties.Describe(bander), "Kill 1,500 Banderlings in Tier 15.");
                StringAssert.DoesNotMatch(ItemBounties.Describe(sweep), new Regex("secret_box"));
            });
        }

        /// <summary>
        /// The kill count is hidden while there is none yet, AND whenever the item carries the mystery-count flag - even if
        /// its weenie also carries a fixed ItemBountyKills, which appraisal would otherwise show.
        /// </summary>
        [TestMethod]
        public void KillsHidden_FollowsTheMysteryFlag()
        {
            Assert.IsTrue(ItemBounties.KillsHidden(new ItemBounties.Slot { Kills = 0 }), "no count yet");
            Assert.IsFalse(ItemBounties.KillsHidden(new ItemBounties.Slot { Kills = 500 }), "a plain count is shown");
            Assert.IsFalse(ItemBounties.KillsHidden(new ItemBounties.Slot { Kills = 500, Mystery = ItemBounties.MysteryReward }), "a mystery reward hides only the reward");
            Assert.IsTrue(ItemBounties.KillsHidden(new ItemBounties.Slot { Kills = 500, Mystery = ItemBounties.MysteryKills }),
                "the mystery-count flag hides a count the item carries too");
        }

        /// <summary>A bounty with no weight of its own can drop once Zone Control gives it one (bountyweight); its entry keeps weight 0 until then.</summary>
        [TestMethod]
        public void EntryFor_ZoneControlWeight()
        {
            var w = BountyWeenie(99985021, (PropertyInt.ItemBountyRewardWcid, 987014), (PropertyInt.ItemBountyKillsMin, 300));
            Assert.IsNull(ItemBounties.EntryFor(w), "no weight of its own and no override: quest / NPC only");
            var e = ItemBounties.EntryFor(w, overridden: true);
            Assert.IsTrue(e.HasValue);
            Assert.AreEqual(0, e.Value.Weight, "the entry keeps the weenie's own weight; the override is applied at each roll");
            Assert.AreEqual(0, ItemBounties.Eligible(new[] { e.Value }, 15).Count, "weight 0 never drops");
            Assert.AreEqual(1, ItemBounties.Eligible(new[] { e.Value with { Weight = 25 } }, 15).Count);
        }

        /// <summary>A mystery bounty naming a table can drop; one naming nothing usable can't.</summary>
        [TestMethod]
        public void EntryFor_MysteryTable()
        {
            var w = BountyWeenie(99985020, (PropertyInt.ItemBountyDropWeight, 10), (PropertyInt.ItemBountyKillsMin, 300),
                (PropertyInt.ItemBountyKillsMax, 1500), (PropertyInt.ItemBountyMystery, 3));
            w.PropertiesString[PropertyString.ItemBountyRewardTable] = "mystery";
            Assert.IsTrue(ItemBounties.EntryFor(w).HasValue);

            w.PropertiesString[PropertyString.ItemBountyRewardTable] = "Not A Name";
            Assert.IsNull(ItemBounties.EntryFor(w), "a mystery reward needs a valid table name");
        }

        // -- the Bounty Tracker's wire format (ItemBountyDisplay / BOUNTYDATA, parsed by position) --

        /// <summary>
        /// The fields in one slot entry for each wire version (DataVersion). The tracker parses by position, so a field added
        /// or moved needs a new version - and a new row here; a field added without a bump fails the 16-field check below.
        /// v3 (the double-click rework) kept the 16 fields and changed the slot keys: R1, R2, B1, B2 became 1-4.
        /// </summary>
        private static readonly Dictionary<string, int> TrackerFieldsByVersion = new() { ["v2"] = 16, ["v3"] = 16 };

        /// <summary>The format version is one the tracker knows, and an empty slot on the wire.</summary>
        [TestMethod]
        public void TrackerWire_VersionAndEmptySlot()
        {
            Assert.IsTrue(TrackerFieldsByVersion.ContainsKey(ItemBounties.DataVersion), $"unknown wire version {ItemBounties.DataVersion}: add its field count");
            Assert.AreEqual("v3", ItemBounties.DataVersion, "the slot keys changed with the rework: the tracker must see a new version");
            Assert.AreEqual("4|empty", ItemBounties.DisplayEntry("4", null));
            Assert.AreEqual("1|empty", ItemBounties.DisplayEntry("1", null));
        }

        /// <summary>
        /// A region mystery slot on the wire: exactly 16 fields in their fixed order, culture-invariant numbers, '|' ';' and a
        /// newline in the name made harmless, and no reward item looked up or reward table named.
        /// </summary>
        [TestMethod]
        public void TrackerWire_MysteryRegionSlot()
        {
            var s = VodMystery("2");
            s.Name = "A|B;C\nD";

            InCulture("de-DE", () =>   // a decimal comma here must not reach the wire
            {
                var entry = ItemBounties.DisplayEntry("2", s);
                Assert.IsFalse(entry.Contains(';'), "';' separates the slots");
                Assert.IsFalse(entry.Contains('\n'), "one line");
                StringAssert.DoesNotMatch(entry, new Regex("secret_box"), "the mystery table is never on the wire");

                var f = entry.Split('|');
                Assert.AreEqual(TrackerFieldsByVersion[ItemBounties.DataVersion], f.Length, entry);
                Assert.AreEqual(16, f.Length, "the v2 / v3 layout below");
                Assert.AreEqual("2", f[0], "key");
                Assert.AreEqual("0", f[1], "tier: 0 for a region bounty");
                Assert.AreEqual("1", f[2], "kind");
                Assert.AreEqual("12.5", f[3], "progress, invariant");
                Assert.AreEqual("1500", f[4], "kills, no grouping");
                Assert.AreEqual("", f[5], "target name: none for a Sweep");
                Assert.AreEqual("0", f[6], "reward wcid: none yet");
                Assert.AreEqual("1", f[7], "amount");
                Assert.AreEqual("0", f[8], "icon: the tracker draws its own mark");
                Assert.AreEqual("0", f[9], "overlay");
                Assert.AreEqual("0", f[10], "underlay");
                Assert.AreEqual("A/B,C D", f[11], "bounty name, cleaned");
                Assert.AreEqual(ItemBounties.MysteryRewardText, f[12], "reward name");
                Assert.IsFalse(string.IsNullOrEmpty(f[13]), "reward use text");
                Assert.AreEqual("VoD", f[14], "place short");
                Assert.AreEqual("Valley of Death", f[15], "place name");
            });
        }

        /// <summary>The whole display value: the version and the slots that exist, then one entry per slot, ';' only between them.</summary>
        [TestMethod]
        public void TrackerWire_BuildDisplay()
        {
            // item_bounty_max_active at its default (4): slots #1-#4 exist
            var v = ItemBounties.DataVersion;
            Assert.AreEqual($"{v}|1,2,3,4;1|empty;2|empty;3|empty;4|empty",
                ItemBounties.BuildDisplay(new Dictionary<string, ItemBounties.Slot>()));

            var s = VodMystery("3");
            s.Name = "x;y|z";
            var parts = ItemBounties.BuildDisplay(new Dictionary<string, ItemBounties.Slot> { ["3"] = s }).Split(';');
            Assert.AreEqual(5, parts.Length, "the header and four slots, whatever the name holds");
            Assert.AreEqual($"{v}|1,2,3,4", parts[0]);
            Assert.AreEqual("1|empty", parts[1]);
            Assert.AreEqual("2|empty", parts[2]);
            Assert.AreEqual(16, parts[3].Split('|').Length);
            StringAssert.StartsWith(parts[3], "3|0|");
            Assert.AreEqual("4|empty", parts[4]);

            // a bounty in a slot above the cap (the cap was lowered after it started) is still on the wire, after #1-#4
            var high = ItemBounties.BuildDisplay(new Dictionary<string, ItemBounties.Slot> { ["6"] = VodMystery("6") }).Split(';');
            Assert.AreEqual($"{v}|1,2,3,4,6", high[0]);
            Assert.AreEqual(6, high.Length);
            StringAssert.StartsWith(high[5], "6|0|");
        }

        /// <summary>The /bounty line for an empty slot and for a mystery slot: the count (en-US grouping), the place, never the table.</summary>
        [TestMethod]
        public void SlotLine_EmptyAndMystery()
        {
            Assert.AreEqual("#2: empty", ItemBounties.SlotLine("2", null));
            InEnUs(() => Assert.AreEqual("#1: 12.5 of 1,500 kills in VoD -> Mystery reward", ItemBounties.SlotLine("1", VodMystery("1"))));
        }

        /// <summary>The task a first double-click shows: the count and the place, or that the count stays a mystery until it starts.</summary>
        [TestMethod]
        public void TaskText_Words()
        {
            InEnUs(() =>
            {
                Assert.AreEqual("kill 1,000 creatures in Tier 15", ItemBounties.TaskText(Sweep("1")));
                Assert.AreEqual("kill 300 Banderlings in Tier 15", ItemBounties.TaskText(Species("2", 2)));

                var hidden = VodMystery("3");
                hidden.Mystery |= ItemBounties.MysteryKills;
                Assert.AreEqual("kill a mystery number of creatures in Valley of Death (revealed when it starts)", ItemBounties.TaskText(hidden));
            });
        }

        // -- the bounty ITEM: its terms, and what happens when one is made (a DB-free GenericObject) --

        /// <summary>A tier bounty's terms come off the item as authored: tier, kind (Sweep when unset), count, reward and amount.</summary>
        [TestMethod]
        public void TermsOf_ReadsATierBounty()
        {
            var item = MakeItem(BountyWeenie(99985101, (PropertyInt.ItemBountyTier, 15), (PropertyInt.ItemBountyKills, 1000),
                (PropertyInt.ItemBountyRewardWcid, 78780315), (PropertyInt.ItemBountyRewardAmount, 3)));
            var t = ItemBounties.TermsOf(item);
            Assert.IsNotNull(t);
            Assert.AreEqual(15, t.Tier);
            Assert.AreEqual(ItemBounties.KindSweep, t.Kind, "no kind = Sweep");
            Assert.AreEqual(1000, t.Kills);
            Assert.AreEqual(78780315u, t.RewardWcid);
            Assert.AreEqual(3, t.RewardAmount);
            Assert.AreEqual("Test Bounty", t.Name);
            Assert.AreEqual(99985101u, t.BountyWcid);
            Assert.IsFalse(t.IsRegion);
            Assert.IsFalse(t.IsMysteryReward);

            item.SetProperty(PropertyInt.ItemBountyRewardAmount, 0);
            Assert.AreEqual(1, ItemBounties.TermsOf(item).RewardAmount, "amount 0 -> 1");
            item.SetProperty(PropertyInt.ItemBountyRewardAmount, int.MaxValue);
            Assert.AreEqual(BountyManager.MaxAmount, ItemBounties.TermsOf(item).RewardAmount, "never past MaxAmount");

            Assert.IsNull(ItemBounties.TermsOf(null));
        }

        /// <summary>The place: a tier 11-25, or a known region - which overrides any tier - else the item isn't a valid bounty.</summary>
        [TestMethod]
        public void TermsOf_TierOrKnownRegion()
        {
            GenericObject At(params (PropertyInt Prop, int Value)[] place)
                => MakeItem(BountyWeenie(99985102, place.Concat(new[] { (PropertyInt.ItemBountyKills, 500), (PropertyInt.ItemBountyRewardWcid, 78780315) }).ToArray()));

            Assert.IsNotNull(ItemBounties.TermsOf(At((PropertyInt.ItemBountyTier, 11))));
            Assert.IsNotNull(ItemBounties.TermsOf(At((PropertyInt.ItemBountyTier, 25))));
            Assert.IsNull(ItemBounties.TermsOf(At((PropertyInt.ItemBountyTier, 10))), "below T11 a bounty needs a region");
            Assert.IsNull(ItemBounties.TermsOf(At((PropertyInt.ItemBountyTier, 26))));
            Assert.IsNull(ItemBounties.TermsOf(At()), "no tier and no region");

            var vod = At((PropertyInt.ItemBountyTier, 15));
            vod.SetProperty(PropertyString.ItemBountyRegion, " VoD ");
            var t = ItemBounties.TermsOf(vod);
            Assert.IsNotNull(t);
            Assert.AreEqual("vod", t.Region, "trimmed and lower case");
            Assert.AreEqual(0, t.Tier, "the region overrides the tier");
            Assert.IsTrue(t.IsRegion);

            var t10 = At();
            t10.SetProperty(PropertyString.ItemBountyRegion, "T10");
            Assert.AreEqual("t10", ItemBounties.TermsOf(t10)?.Region, "a region needs no tier");

            var nowhere = At((PropertyInt.ItemBountyTier, 15));
            nowhere.SetProperty(PropertyString.ItemBountyRegion, "atlantis");
            Assert.IsNull(ItemBounties.TermsOf(nowhere), "an unknown region is broken terms, never its tier instead");
        }

        /// <summary>The count: at most MaxKills; none at all is not a bounty; a mystery count may be just a range, still unrolled.</summary>
        [TestMethod]
        public void TermsOf_KillCount()
        {
            GenericObject With(params (PropertyInt Prop, int Value)[] kills)
                => MakeItem(BountyWeenie(99985103, kills.Concat(new[] { (PropertyInt.ItemBountyTier, 15), (PropertyInt.ItemBountyRewardWcid, 78780315) }).ToArray()));

            Assert.AreEqual(BountyManager.MaxKills, ItemBounties.TermsOf(With((PropertyInt.ItemBountyKills, BountyManager.MaxKills)))?.Kills);
            Assert.IsNull(ItemBounties.TermsOf(With((PropertyInt.ItemBountyKills, BountyManager.MaxKills + 1))), "above MaxKills is refused");
            Assert.IsNull(ItemBounties.TermsOf(With()), "no count");
            Assert.IsNull(ItemBounties.TermsOf(With((PropertyInt.ItemBountyKills, 0))), "a count of 0");

            var mystery = ItemBounties.TermsOf(With((PropertyInt.ItemBountyMystery, ItemBounties.MysteryKills),
                (PropertyInt.ItemBountyKillsMin, 300), (PropertyInt.ItemBountyKillsMax, 1500)));
            Assert.IsNotNull(mystery, "a mystery count is rolled at apply: the range is enough");
            Assert.AreEqual(0, mystery.Kills);
            Assert.AreEqual((300, 1500), (mystery.KillsMin, mystery.KillsMax));
            Assert.IsTrue(ItemBounties.KillsHidden(mystery));
        }

        /// <summary>A mystery reward: the fixed WCID is ignored (0), the named table is read trimmed and lower case, and it must be valid.</summary>
        [TestMethod]
        public void TermsOf_MysteryReward()
        {
            GenericObject Mystery(string table)
            {
                var item = MakeItem(BountyWeenie(99985104, (PropertyInt.ItemBountyTier, 15), (PropertyInt.ItemBountyKills, 500),
                    (PropertyInt.ItemBountyRewardWcid, 78780315), (PropertyInt.ItemBountyMystery, ItemBounties.MysteryReward)));
                if (table != null) item.SetProperty(PropertyString.ItemBountyRewardTable, table);
                return item;
            }

            var t = ItemBounties.TermsOf(Mystery(" Mystery "));
            Assert.IsNotNull(t);
            Assert.IsTrue(t.IsMysteryReward);
            Assert.AreEqual(0u, t.RewardWcid, "a mystery never pays the weenie's fixed reward");
            Assert.AreEqual("mystery", t.RewardTableName);
            Assert.AreEqual(ItemBounties.MysteryRewardText, ItemBounties.RewardText(t));

            Assert.IsNull(ItemBounties.TermsOf(Mystery(null)), "a mystery reward must name its table");
            Assert.IsNull(ItemBounties.TermsOf(Mystery("a|b")), "a name that would break the slot string");
            Assert.IsNull(ItemBounties.TermsOf(MakeItem(BountyWeenie(99985104, (PropertyInt.ItemBountyTier, 15), (PropertyInt.ItemBountyKills, 500)))),
                "no reward at all");
        }

        /// <summary>A Species bounty needs its creature type; its name is the authored one, else made from the type; other kinds are refused.</summary>
        [TestMethod]
        public void TermsOf_SpeciesRules()
        {
            GenericObject Kind(int kind, int target)
                => MakeItem(BountyWeenie(99985105, (PropertyInt.ItemBountyTier, 15), (PropertyInt.ItemBountyKills, 300),
                    (PropertyInt.ItemBountyRewardWcid, 78780315), (PropertyInt.ItemBountyKind, kind), (PropertyInt.ItemBountyTarget, target)));

            var named = Kind(ItemBounties.KindSpecies, (int)CreatureType.Banderling);
            named.SetProperty(PropertyString.ItemBountyTargetName, "Banderling Raiders");
            var t = ItemBounties.TermsOf(named);
            Assert.AreEqual(ItemBounties.KindSpecies, t.Kind);
            Assert.AreEqual((uint)CreatureType.Banderling, t.Target);
            Assert.AreEqual("Banderling Raiders", t.TargetName);

            var unnamed = ItemBounties.TermsOf(Kind(ItemBounties.KindSpecies, (int)CreatureType.Banderling));
            StringAssert.StartsWith(unnamed.TargetName, "Banderling", "no authored name: one made from the creature type");

            Assert.IsNull(ItemBounties.TermsOf(Kind(ItemBounties.KindSpecies, 0)), "a Species bounty with no target");
            Assert.IsNull(ItemBounties.TermsOf(Kind(3, 2)), "an unknown kind");
            Assert.IsNull(ItemBounties.TermsOf(Kind(0, 0)), "kind 0");
            Assert.AreEqual("", ItemBounties.TermsOf(Kind(ItemBounties.KindSweep, 0)).TargetName, "a Sweep names no target");
        }

        /// <summary>An item is routed to the bounty hook only when it carries a count or a range above 0.</summary>
        [TestMethod]
        public void IsBountyItem_NeedsACountOrRange()
        {
            Assert.IsFalse(ItemBounties.IsBountyItem(null));
            Assert.IsFalse(ItemBounties.IsBountyItem(MakeItem(BountyWeenie(99985106))), "an ordinary item");
            Assert.IsTrue(ItemBounties.IsBountyItem(MakeItem(BountyWeenie(99985106, (PropertyInt.ItemBountyKills, 500)))));
            Assert.IsTrue(ItemBounties.IsBountyItem(MakeItem(BountyWeenie(99985106, (PropertyInt.ItemBountyKillsMin, 300)))), "a mystery range still to roll");
            Assert.IsTrue(ItemBounties.IsBountyItem(MakeItem(BountyWeenie(99985106, (PropertyInt.ItemBountyKillsMax, 1500)))));
            Assert.IsFalse(ItemBounties.IsBountyItem(MakeItem(BountyWeenie(99985106, (PropertyInt.ItemBountyKills, 0), (PropertyInt.ItemBountyKillsMin, 0)))),
                "0 is unset");
        }

        /// <summary>
        /// MYSTERY rule: a mystery count is never stamped on the item when it is made (appraisal and trade would show it); the
        /// description says it is a mystery and names neither end of the range nor the reward table.
        /// </summary>
        [TestMethod]
        public void OnNewItem_MysteryCountIsNeverStamped()
        {
            var w = BountyWeenie(99985107, (PropertyInt.ItemBountyTier, 15), (PropertyInt.ItemBountyKillsMin, 300),
                (PropertyInt.ItemBountyKillsMax, 1500), (PropertyInt.ItemBountyMystery, ItemBounties.MysteryKills | ItemBounties.MysteryReward));
            w.PropertiesString[PropertyString.ItemBountyRewardTable] = "secret_box";

            InEnUs(() =>
            {
                for (var i = 0; i < 20; i++)
                {
                    var item = MakeItem(w);
                    ItemBounties.OnNewItem(item);
                    Assert.IsNull(item.GetProperty(PropertyInt.ItemBountyKills), "the count is rolled when the bounty starts, never at creation");
                    Assert.IsTrue(ItemBounties.IsBountyItem(item), "still routed to the bounty hook by its range");
                    var desc = item.GetProperty(PropertyString.LongDesc);
                    Assert.IsNotNull(desc, "a valid mystery bounty still gets its description");
                    StringAssert.Contains(desc, "mystery number");
                    StringAssert.Contains(desc, "Tier 15");
                    StringAssert.DoesNotMatch(desc, new Regex("300|1,?500|secret_box"));
                }
            });
        }

        /// <summary>A bounty made from a range gets its count rolled once at creation - in the range, a round number - and its description states it.</summary>
        [TestMethod]
        public void OnNewItem_RollsACountInTheRange()
        {
            // the reward is a mystery so the description needs no reward name (no DB)
            var w = BountyWeenie(99985108, (PropertyInt.ItemBountyTier, 15), (PropertyInt.ItemBountyKillsMin, 1000),
                (PropertyInt.ItemBountyKillsMax, 2000), (PropertyInt.ItemBountyMystery, ItemBounties.MysteryReward));
            w.PropertiesString[PropertyString.ItemBountyRewardTable] = "secret_box";

            InEnUs(() =>
            {
                for (var i = 0; i < 30; i++)
                {
                    var item = MakeItem(w);
                    ItemBounties.OnNewItem(item);
                    var kills = item.GetProperty(PropertyInt.ItemBountyKills);
                    Assert.IsTrue(kills.HasValue, "rolled at creation");
                    Assert.IsTrue(kills >= 1000 && kills <= 2000, $"{kills} outside 1,000-2,000");
                    Assert.AreEqual(0, kills.Value % 50, $"{kills} is not a round 50");
                    var desc = item.GetProperty(PropertyString.LongDesc);
                    StringAssert.StartsWith(desc, $"Kill {kills.Value.ToString("N0", CultureInfo.GetCultureInfo("en-US"))} creatures in Tier 15.");
                    StringAssert.DoesNotMatch(desc, new Regex("secret_box"));
                }
            });

            var plain = MakeItem(BountyWeenie(99985109));
            ItemBounties.OnNewItem(plain);
            Assert.IsNull(plain.GetProperty(PropertyString.LongDesc), "an item that isn't a bounty is left alone");
            Assert.IsNull(plain.GetProperty(PropertyInt.ItemBountyKills));
            Assert.IsNull(plain.ItemUseable, "nor given a bounty's use");
        }

        /// <summary>
        /// Bounty Journal rework: whatever the weenie carries - the old use-on-a-ring targeted jewelry only - a new bounty is
        /// used ON something (the source in the pack; the target the player or an item in the pack), and may target the
        /// journal (a book), the player (who gets a journal if they have none) and jewelry (the old habit). A mystery count
        /// still rolls only when it starts.
        /// </summary>
        [TestMethod]
        public void OnNewItem_MakesItUsableOnTheJournal()
        {
            var w = BountyWeenie(99985111, (PropertyInt.ItemBountyTier, 15), (PropertyInt.ItemBountyKills, 500),
                (PropertyInt.ItemUseable, (int)Usable.SourceContainedTargetContained), (PropertyInt.TargetType, (int)ItemType.Jewelry),
                (PropertyInt.ItemBountyMystery, ItemBounties.MysteryReward));
            w.PropertiesString[PropertyString.ItemBountyRewardTable] = "secret_box";
            var item = MakeItem(w);
            Assert.AreEqual(ItemType.Jewelry, item.TargetType, "as authored for the rings");
            ItemBounties.OnNewItem(item, w);
            Assert.AreEqual(Usable.SourceContainedTargetSelfOrContained, item.ItemUseable);
            Assert.AreEqual(ItemType.Writable | ItemType.Creature | ItemType.Jewelry, item.TargetType);
            Assert.AreEqual(500, item.GetProperty(PropertyInt.ItemBountyKills), "the terms are untouched");
            Assert.IsFalse(ItemBounties.MakeUsable(item), "already right: nothing to change (login reports only real changes)");

            var mystery = BountyWeenie(99985115, (PropertyInt.ItemBountyTier, 15), (PropertyInt.ItemBountyKillsMin, 300), (PropertyInt.ItemBountyKillsMax, 900),
                (PropertyInt.ItemBountyMystery, ItemBounties.MysteryKills | ItemBounties.MysteryReward));
            mystery.PropertiesString[PropertyString.ItemBountyRewardTable] = "secret_box";
            var hidden = MakeItem(mystery);
            ItemBounties.OnNewItem(hidden, mystery);
            Assert.AreEqual(Usable.SourceContainedTargetSelfOrContained, hidden.ItemUseable, "a mystery count bounty too");
            Assert.IsNull(hidden.GetProperty(PropertyInt.ItemBountyKills), "its count still rolls only when it starts");
        }

        /// <summary>
        /// Appraisal shows a bounty's description from its terms as they are now - so a bounty made before the rework, whose
        /// stored text says to use it on a ring or bracelet, reads how to start it now - without writing the item; anything
        /// else (and a bounty whose terms aren't complete yet) keeps its own text.
        /// </summary>
        [TestMethod]
        public void DescribeItem_CurrentWordsForOldItems()
        {
            var w = BountyWeenie(99985112, (PropertyInt.ItemBountyTier, 15), (PropertyInt.ItemBountyKills, 1000), (PropertyInt.ItemBountyMystery, ItemBounties.MysteryReward));
            w.PropertiesString[PropertyString.ItemBountyRewardTable] = "secret_box";
            var old = MakeItem(w);
            const string oldText = "Kill 1,000 creatures in Tier 15. Use it on a ring or bracelet you have to start it; /bounty shows your slots.";
            old.SetProperty(PropertyString.LongDesc, oldText);

            InEnUs(() =>
            {
                var text = ItemBounties.DescribeItem(old);
                StringAssert.StartsWith(text, "Kill 1,000 creatures in Tier 15.");
                StringAssert.Contains(text, "double-click it and then click your Bounty Journal");
                Assert.AreEqual(text, ItemBounties.AppraisalText(old, null), "appraisal shows a bounty's own terms, for anyone");
                StringAssert.DoesNotMatch(text, new Regex(@"\b(ring|bracelet)s?\b", RegexOptions.IgnoreCase));
            });
            Assert.AreEqual(oldText, old.GetProperty(PropertyString.LongDesc), "display only: the item is never written");

            Assert.IsNull(ItemBounties.DescribeItem(null));
            Assert.IsNull(ItemBounties.DescribeItem(MakeItem(BountyWeenie(99985113))), "not a bounty");
            var untiered = MakeItem(BountyWeenie(99985114, (PropertyInt.ItemBountyKills, 500), (PropertyInt.ItemBountyRewardWcid, 78780315)));
            Assert.IsNull(ItemBounties.DescribeItem(untiered), "takes its tier from the kill it drops from: no complete terms yet");
        }

        /// <summary>A KillsMin / Max row left at 0 counts as unset, as everywhere else: it never rolls a fixed count down to 1 or widens it.</summary>
        [TestMethod]
        public void OnNewItem_ZeroRangeEndIsUnset()
        {
            GenericObject Made(params (PropertyInt Prop, int Value)[] kills)
            {
                var w = BountyWeenie(99985110, kills.Concat(new[] { (PropertyInt.ItemBountyTier, 15), (PropertyInt.ItemBountyMystery, ItemBounties.MysteryReward) }).ToArray());
                w.PropertiesString[PropertyString.ItemBountyRewardTable] = "secret_box";
                var item = MakeItem(w);
                ItemBounties.OnNewItem(item);
                return item;
            }

            for (var i = 0; i < 20; i++)
            {
                Assert.AreEqual(500, Made((PropertyInt.ItemBountyKills, 500), (PropertyInt.ItemBountyKillsMin, 0), (PropertyInt.ItemBountyKillsMax, 0))
                    .GetProperty(PropertyInt.ItemBountyKills), "a fixed count with empty range rows stays as authored");
                Assert.AreEqual(500, Made((PropertyInt.ItemBountyKillsMin, 500), (PropertyInt.ItemBountyKillsMax, 0))
                    .GetProperty(PropertyInt.ItemBountyKills), "a missing top end takes the bottom one: never 1-500");
            }
        }

        // -- the Bounty Journal (2026-11-12) --

        /// <summary>A slot whose reward is a mystery, so its text needs no reward name (no DB).</summary>
        private static ItemBounties.Slot NoDb(ItemBounties.Slot s)
        {
            s.RewardWcid = 0;
            s.RewardTableName = "secret_box";
            s.Mystery |= ItemBounties.MysteryReward;
            return s;
        }

        /// <summary>Two bounties going: #1 a T16 Banderling bounty at 12 of 300, #2 a T15 sweep at 640 of 1,000.</summary>
        private static Dictionary<string, ItemBounties.Slot> TwoGoing()
        {
            var bander = NoDb(Species("1", 2, tier: 16, kills: 300));
            bander.Progress = 12;
            return new Dictionary<string, ItemBounties.Slot>
            {
                ["1"] = bander,
                ["2"] = NoDb(Sweep("2", tier: 15, kills: 1000, progress: 640)),
            };
        }

        /// <summary>A journal is any item made from item_bounty_journal_wcid (98760483 shipped); nothing else is.</summary>
        [TestMethod]
        public void Journal_IsJournalByWcid()
        {
            Assert.IsTrue(ItemBounties.JournalsOn);
            Assert.AreEqual(98760483u, ItemBounties.JournalWcid);
            Assert.IsTrue(ItemBounties.IsJournal(MakeItem(BountyWeenie(98760483))));
            Assert.IsFalse(ItemBounties.IsJournal(MakeItem(BountyWeenie(98760484))));
            Assert.IsFalse(ItemBounties.IsJournal(null));
            Assert.AreEqual(Usable.SourceContainedTargetSelfOrContained, ItemBounties.BountyUseable, "journals on: bounties are used ON something");
        }

        /// <summary>
        /// The first page: how many are going out of the cap, a line per slot with the place, the percent and the count, and
        /// how to add one; empty slots show as empty.
        /// </summary>
        [TestMethod]
        public void Journal_SummaryPage()
        {
            InEnUs(() =>
            {
                var text = ItemBounties.SummaryPage(TwoGoing(), 4, true);
                var lines = text.Split('\n');
                Assert.AreEqual("Bounties going: 2 of 4", lines[0]);
                CollectionAssert.Contains(lines, "#1  T16  4%  12/300");
                CollectionAssert.Contains(lines, "#2  T15  64%  640/1,000");
                CollectionAssert.Contains(lines, "#3  empty");
                CollectionAssert.Contains(lines, "#4  empty");
                StringAssert.Contains(text, "Turn the page for each bounty.");
                StringAssert.EndsWith(text, "To add a bounty: double-click it, then click this journal.");
                Assert.IsFalse(text.Contains("Paused"), "not paused");

                var none = ItemBounties.SummaryPage(new Dictionary<string, ItemBounties.Slot>(), 4, true);
                StringAssert.StartsWith(none, "Bounties going: 0 of 4\n");
                Assert.IsFalse(none.Contains("Turn the page"), "no bounty pages to turn to");

                StringAssert.Contains(ItemBounties.SummaryPage(TwoGoing(), 4, false), "Paused by the admins");
            });
        }

        /// <summary>
        /// A bounty's page: its name, what and where to kill, the progress with a 20-cell bar, the reward, the day it started
        /// (UTC), the counting rule for a tier or a region, and how to abandon it - with its own slot number.
        /// </summary>
        [TestMethod]
        public void Journal_BountyPage()
        {
            InEnUs(() =>
            {
                var tier = ItemBounties.BountyPage("2", TwoGoing()["2"]);
                var lines = tier.Split('\n');
                Assert.AreEqual("Tier 15 Bounty: 1,000 Kills", lines[0]);
                CollectionAssert.Contains(lines, "Kill: any creature");
                CollectionAssert.Contains(lines, "Where: Tier 15");
                CollectionAssert.Contains(lines, "Progress: 640 of 1,000 kills");
                CollectionAssert.Contains(lines, "[############--------] 64%");
                CollectionAssert.Contains(lines, "Reward: a mystery, revealed when it is done");
                CollectionAssert.Contains(lines, "Started: 2026-11-10");
                StringAssert.Contains(tier, "Only your own kills in Tier 15 count (the tier dip applies).");
                StringAssert.EndsWith(tier, "/bounty abandon 2");

                var bander = ItemBounties.BountyPage("1", TwoGoing()["1"]);
                StringAssert.Contains(bander, "Kill: Banderlings\n");
                StringAssert.Contains(bander, "Where: Tier 16\n");
                StringAssert.EndsWith(bander, "/bounty abandon 1");

                var vod = ItemBounties.BountyPage("3", VodMystery("3"));
                StringAssert.Contains(vod, "Where: Valley of Death\n");
                StringAssert.Contains(vod, "count, each one in full.");

                var done = VodMystery("4", kills: 500, progress: 500);   // done, its mystery reward still waiting for the table
                var page = ItemBounties.BountyPage("4", done);
                StringAssert.Contains(page, "[####################] 100%");
                StringAssert.Contains(page, "Done! Its mystery reward is paid as soon as the admins set it up.");

                var undated = TwoGoing()["2"];
                undated.AppliedUnix = 0;
                Assert.IsFalse(ItemBounties.BountyPage("2", undated).Contains("Started:"), "no date when none was stored");
            });
        }

        /// <summary>The whole journal: the summary first, then one page per bounty going, in slot order, titled by slot.</summary>
        [TestMethod]
        public void Journal_BuildJournal_PagesInSlotOrder()
        {
            InEnUs(() =>
            {
                var empty = ItemBounties.BuildJournal(new Dictionary<string, ItemBounties.Slot>(), 4, true);
                Assert.AreEqual(1, empty.Count, "the summary alone: never a blank book");
                Assert.AreEqual("Bounty Journal", empty[0].Title);

                var slots = TwoGoing();
                slots["4"] = NoDb(Sweep("4", tier: 17, kills: 200));
                var pages = ItemBounties.BuildJournal(slots, 4, true);
                CollectionAssert.AreEqual(new[] { "Bounty Journal", "Bounty #1", "Bounty #2", "Bounty #4" }, pages.Select(p => p.Title).ToArray());
                StringAssert.EndsWith(pages[3].Text, "/bounty abandon 4");
            });
        }

        /// <summary>Percent rounds down and reads 100 only when done; the bar has one cell per 5%.</summary>
        [TestMethod]
        public void Journal_PercentAndBar()
        {
            ItemBounties.Slot At(double progress, int kills = 100) => new() { Key = "1", Kind = ItemBounties.KindSweep, Kills = kills, Progress = progress };
            Assert.AreEqual(0, ItemBounties.Percent(At(0)));
            Assert.AreEqual(50, ItemBounties.Percent(At(50)));
            Assert.AreEqual(99, ItemBounties.Percent(At(99.99)), "never 100 before it is done");
            Assert.AreEqual(100, ItemBounties.Percent(At(100)));
            Assert.AreEqual(4, ItemBounties.Percent(At(12, 300)));
            Assert.AreEqual(0, ItemBounties.Percent(At(5, 0)), "a broken count reads 0, never a division by zero");
            Assert.AreEqual(0, ItemBounties.Percent(null));

            Assert.AreEqual("[--------------------]", ItemBounties.ProgressBar(At(0)));
            Assert.AreEqual("[############--------]", ItemBounties.ProgressBar(At(64)));
            Assert.AreEqual("[###################-]", ItemBounties.ProgressBar(At(99.99)));
            Assert.AreEqual("[####################]", ItemBounties.ProgressBar(At(100)));
        }

        /// <summary>The chat line opening the journal prints: every bounty's place and percent, or how to add one.</summary>
        [TestMethod]
        public void Journal_ChatLine()
        {
            Assert.AreEqual("Bounty Journal: 2 of 4 going - #1 T16 4%, #2 T15 64%.", ItemBounties.JournalChatLine(TwoGoing(), 4));
            StringAssert.StartsWith(ItemBounties.JournalChatLine(new Dictionary<string, ItemBounties.Slot>(), 4),
                "Bounty Journal: none going (you can have up to 4).");
        }

        /// <summary>The journal's description for its holder: a line per slot with the progress and the reward, then how to use it.</summary>
        [TestMethod]
        public void Journal_Description()
        {
            InEnUs(() =>
            {
                var text = ItemBounties.JournalDescription(TwoGoing(), 4, true);
                var lines = text.Split('\n');
                Assert.AreEqual("Your bounties: 2 of 4 going.", lines[0]);
                CollectionAssert.Contains(lines, "#1 T16: 12 of 300 Banderlings (4%) -> Mystery reward");
                CollectionAssert.Contains(lines, "#2 T15: 640 of 1,000 kills (64%) -> Mystery reward");
                CollectionAssert.Contains(lines, "#3 empty");
                StringAssert.Contains(text, "To add a bounty, double-click it and then click this journal.");
                StringAssert.Contains(ItemBounties.JournalDescription(TwoGoing(), 4, false), "Paused by the admins");
            });
        }

        /// <summary>Appraisal shows a journal's summary only to the player carrying it; with no examiner, the stored text stays.</summary>
        [TestMethod]
        public void Journal_AppraisalNeedsItsHolder()
        {
            Assert.IsNull(ItemBounties.AppraisalText(MakeItem(BountyWeenie(98760483)), null), "no examiner: the stored description");
            Assert.IsNull(ItemBounties.AppraisalText(MakeItem(BountyWeenie(99985116)), null), "neither a journal nor a bounty");
        }

        /// <summary>Everything the journal shows reaches the client: plain 7-bit ASCII, "\n" line breaks only (CLAUDE.md).</summary>
        [TestMethod]
        public void Journal_TextIsClientSafe()
        {
            InEnUs(() =>
            {
                var slots = TwoGoing();
                slots["3"] = VodMystery("3");
                var texts = ItemBounties.BuildJournal(slots, 4, false).SelectMany(p => new[] { p.Title, p.Text })
                    .Append(ItemBounties.JournalChatLine(slots, 4))
                    .Append(ItemBounties.JournalDescription(slots, 4, false))
                    .ToList();
                foreach (var t in texts)
                {
                    Assert.IsFalse(t.Contains('\r'), "no carriage return: the client draws it as a music note");
                    Assert.IsTrue(t.All(c => c < 128), $"non-ASCII in: {t}");
                }
            });
        }
    }
}
