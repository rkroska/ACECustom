using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using ACE.Server.Managers;
using ACE.Server.Managers.ZoneControl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Item bounty drop weights PER PLACE (owner 2026-11-10): a place is a tier "t11".."t25" or a bounty region ("vod",
    /// "t10"), each with its own pool. Pins the pure rules in ItemBounties - which places exist and in what order, where a
    /// bounty can drop and where its weenie's own weight applies, its weight in each pool, the place words an admin types,
    /// and the "bw" lines and undo commands - and that the Bounty Editor (ilt-dev tools\bounty-editor) reads and writes
    /// exactly what the server does: its weightAt, placesToken, poolCommands and bw-line parse are ported here verbatim and
    /// checked against the server's own. No DB, no world.
    /// </summary>
    [TestClass]
    public class ItemBountyPlacesTests
    {
        /// <summary>The Bounty Editor's bw-line pattern (BountyEditor.src.html, parseLiveLines), as written there.</summary>
        private static readonly Regex EditorBwLine = new(@"\bbw\s+(\d+)\s+own=(\d+)\s+off=(yes|no)((?:\s+[a-z0-9]+=\d+)*)", RegexOptions.IgnoreCase);

        /// <summary>The Bounty Editor's PLACES (t11..t25, then "vod", "t10"): the column order of its pool grid.</summary>
        private static readonly string[] EditorPlaces = Enumerable.Range(11, 15).Select(t => "t" + t).Concat(new[] { "vod", "t10" }).ToArray();

        /// <summary>A tier bounty that takes the kill's tier, with no band of its own (11-25).</summary>
        private static readonly ItemBounties.DropEntry AnyTier = new(99985001, 50, 11, 25, false, "Any Tier Bounty");

        /// <summary>A tier bounty with its own band, 13-16.</summary>
        private static readonly ItemBounties.DropEntry Band = new(99985002, 10, 13, 16, false, "Band Bounty");

        /// <summary>A bounty that always has Tier 15.</summary>
        private static readonly ItemBounties.DropEntry Fixed15 = new(99985003, 4, 15, 15, true, "Tier 15 Bounty");

        /// <summary>A Valley of Death bounty.</summary>
        private static readonly ItemBounties.DropEntry Vod = new(99985004, 3, 0, 0, true, "VoD Bounty", "vod");

        /// <summary>A Thaelaryn Island bounty: its region key "t10" looks like a tier but is not one.</summary>
        private static readonly ItemBounties.DropEntry Thaelaryn = new(99985005, 6, 0, 0, true, "Thaelaryn Bounty", "t10");

        /// <summary>A tier bounty with no weight of its own (it drops only where Zone Control gives it one).</summary>
        private static readonly ItemBounties.DropEntry NoOwn = new(99985006, 0, 11, 25, false, "Zone Control Only Bounty");

        /// <summary>Every kind of entry, for the tests that walk them all.</summary>
        private static readonly ItemBounties.DropEntry[] AllKinds = { AnyTier, Band, Fixed15, Vod, Thaelaryn, NoOwn };

        /// <summary>Zone Control weights for a bounty that is on: the given places and weights.</summary>
        private static BountyPlaceWeights On(params (string Place, int Weight)[] places)
        {
            var set = new BountyPlaceWeights();
            foreach (var (place, weight) in places) set.Places[place] = weight;
            return set;
        }

        /// <summary>Zone Control weights for a bounty that is OFF, keeping the given places and weights.</summary>
        private static BountyPlaceWeights Off(params (string Place, int Weight)[] places)
        {
            var set = On(places);
            set.Off = true;
            return set;
        }

        /// <summary>A set's state as one comparable string (null = nothing set): "off=True|t15=1,t16=1000".</summary>
        private static string StateOf(BountyPlaceWeights set)
            => $"off={set?.Off == true}|" + string.Join(",", (set?.Places ?? new Dictionary<string, int>())
                .OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value.ToString(CultureInfo.InvariantCulture)));

        // -- places --

        /// <summary>Only "t11".."t25" are tiers; "t10" is Thaelaryn Island (a region), and nothing else is guessed.</summary>
        [TestMethod]
        public void TryPlaceTier_OnlyT11ToT25()
        {
            Assert.IsTrue(ItemBounties.TryPlaceTier("t11", out var t11));
            Assert.AreEqual(11, t11);
            Assert.IsTrue(ItemBounties.TryPlaceTier("t15", out var t15));
            Assert.AreEqual(15, t15);
            Assert.IsTrue(ItemBounties.TryPlaceTier("T25", out var t25), "an admin's capital T");
            Assert.AreEqual(25, t25);

            foreach (var notATier in new[] { "t10", "t26", "t9", "t1", "t011", "t-1", "t+5", " t15", "t15 ", "x15", "15", "tt5", "vod", "t", "", null })
                Assert.IsFalse(ItemBounties.TryPlaceTier(notATier, out _), $"'{notATier}' is not a tier place");

            for (var tier = ItemBounties.MinTier; tier <= ItemBounties.MaxTier; tier++)
            {
                Assert.AreEqual("t" + tier, ItemBounties.TierPlace(tier));
                Assert.IsTrue(ItemBounties.TryPlaceTier(ItemBounties.TierPlace(tier), out var back));
                Assert.AreEqual(tier, back);
            }
        }

        /// <summary>
        /// AllPlaces is t11..t25, then the regions in BountyRegions order (vod, t10) - the order every list, bw line and
        /// command uses, and the Bounty Editor's PLACES - each a key the store keeps (IsBountyPlaceKey), none twice.
        /// </summary>
        [TestMethod]
        public void AllPlaces_TiersThenRegions()
        {
            CollectionAssert.AreEqual(EditorPlaces, ItemBounties.AllPlaces.ToArray(), "the server's order is the editor's");
            CollectionAssert.AreEqual(
                ZoneControlManager.BountyRegions.Select(r => r.Key).ToArray(),
                ItemBounties.AllPlaces.Skip(ItemBounties.MaxTier - ItemBounties.MinTier + 1).ToArray(),
                "the regions follow the tiers in BountyRegions order");
            CollectionAssert.AllItemsAreUnique(ItemBounties.AllPlaces.ToArray());
            foreach (var place in ItemBounties.AllPlaces)
            {
                Assert.IsTrue(ZoneControlManager.IsBountyPlaceKey(place), $"{place}: the store keeps a weight there");
                Assert.AreEqual(place, place.ToLowerInvariant(), "places are lower case");
            }
        }

        // -- where a bounty can drop --

        /// <summary>
        /// A tier bounty can be put in any tier but never a region; a fixed-tier bounty only in its tier; a region bounty only
        /// in its region - and its OWN places (where its weenie's weight counts) are its band, its tier or its region.
        /// </summary>
        [TestMethod]
        public void CanDropAt_And_NaturalAt()
        {
            // a band bounty: any tier can take it, its own places are its band
            foreach (var tier in new[] { "t11", "t12", "t13", "t16", "t17", "t25" })
                Assert.IsTrue(ItemBounties.CanDropAt(Band, tier), $"a tier bounty can be put in {tier}");
            foreach (var region in new[] { "vod", "t10" })
                Assert.IsFalse(ItemBounties.CanDropAt(Band, region), $"never a tier bounty in {region}");
            Assert.IsTrue(ItemBounties.NaturalAt(Band, "t13"));
            Assert.IsTrue(ItemBounties.NaturalAt(Band, "t16"));
            Assert.IsFalse(ItemBounties.NaturalAt(Band, "t12"), "below its band: can be added, not its own");
            Assert.IsFalse(ItemBounties.NaturalAt(Band, "t17"), "above its band");
            Assert.IsFalse(ItemBounties.NaturalAt(Band, "vod"));
            Assert.IsTrue(ItemBounties.NaturalAt(AnyTier, "t11") && ItemBounties.NaturalAt(AnyTier, "t25"), "no band of its own = every tier");

            // a fixed-tier bounty: its tier only
            Assert.IsTrue(ItemBounties.CanDropAt(Fixed15, "t15"));
            Assert.IsTrue(ItemBounties.NaturalAt(Fixed15, "t15"));
            foreach (var other in new[] { "t14", "t16", "t11", "t25", "vod", "t10" })
            {
                Assert.IsFalse(ItemBounties.CanDropAt(Fixed15, other), $"it always has Tier 15: never in {other}");
                Assert.IsFalse(ItemBounties.NaturalAt(Fixed15, other));
            }

            // region bounties: their region only (any case), never a tier or the other region
            Assert.IsTrue(ItemBounties.CanDropAt(Vod, "vod"));
            Assert.IsTrue(ItemBounties.CanDropAt(Vod, "VoD"), "an admin's capitals");
            Assert.IsTrue(ItemBounties.NaturalAt(Vod, "vod"));
            Assert.IsTrue(ItemBounties.CanDropAt(Thaelaryn, "t10"));
            Assert.IsTrue(ItemBounties.NaturalAt(Thaelaryn, "t10"));
            foreach (var other in new[] { "t10", "t11", "t15", "t25" })
                Assert.IsFalse(ItemBounties.CanDropAt(Vod, other), $"a VoD bounty never in {other}");
            foreach (var other in new[] { "vod", "t11", "t15" })
                Assert.IsFalse(ItemBounties.CanDropAt(Thaelaryn, other), $"a Thaelaryn bounty never in {other}");

            // not a place: nothing can drop there
            foreach (var e in AllKinds)
                foreach (var bad in new[] { "t26", "t9", "atlantis", "", null })
                {
                    Assert.IsFalse(ItemBounties.CanDropAt(e, bad), $"{e.Name} at '{bad}'");
                    Assert.IsFalse(ItemBounties.NaturalAt(e, bad), $"{e.Name} at '{bad}'");
                }

            // its own places are always places it can drop
            foreach (var e in AllKinds)
                foreach (var place in ItemBounties.AllPlaces)
                    if (ItemBounties.NaturalAt(e, place))
                        Assert.IsTrue(ItemBounties.CanDropAt(e, place), $"{e.Name} at {place}");
        }

        // -- a bounty's weight in one pool --

        /// <summary>
        /// WeightAt: off = 0 everywhere; a place's Zone Control weight wins there (0 = out of that pool); else the weenie's
        /// own weight in its own places and 0 elsewhere; a place added outside its band takes the set weight; and a weight
        /// set where the bounty can't drop (a hand-edited store) never puts it there.
        /// </summary>
        [TestMethod]
        public void WeightAt_Rules()
        {
            // off: nothing anywhere, whatever is set
            var off = Off(("t15", 500), ("t20", 5));
            foreach (var place in ItemBounties.AllPlaces)
                Assert.AreEqual(0, ItemBounties.WeightAt(Band, place, off), $"off at {place}");
            Assert.AreEqual(0, ItemBounties.WeightAt(Band, "t14", Off()), "off with nothing set: still off in its own places");

            // set: the place's weight replaces its own there, only there
            var set = On(("t14", 1000), ("t15", 0), ("t20", 7));
            Assert.AreEqual(1000, ItemBounties.WeightAt(Band, "t14", set), "set in its own band");
            Assert.AreEqual(0, ItemBounties.WeightAt(Band, "t15", set), "set 0: out of that pool");
            Assert.AreEqual(10, ItemBounties.WeightAt(Band, "t13", set), "not set: its own weight in its own places");
            Assert.AreEqual(10, ItemBounties.WeightAt(Band, "t16", set));
            Assert.AreEqual(7, ItemBounties.WeightAt(Band, "t20", set), "added outside its band");
            Assert.AreEqual(0, ItemBounties.WeightAt(Band, "t21", set), "outside its band and not set: 0");
            Assert.AreEqual(0, ItemBounties.WeightAt(Band, "t12", set));

            // no Zone Control at all: its own weight in its own band, 0 outside
            Assert.AreEqual(10, ItemBounties.WeightAt(Band, "t13", null));
            Assert.AreEqual(0, ItemBounties.WeightAt(Band, "t17", null), "own outside its band = 0");
            Assert.AreEqual(10, ItemBounties.WeightAt(Band, "t15", On()), "an empty set is its own weights");
            Assert.AreEqual(10, ItemBounties.WeightAt(Band, "t15", new BountyPlaceWeights { Places = null }), "a set with no Places");

            // regions
            Assert.AreEqual(3, ItemBounties.WeightAt(Vod, "vod", null), "a region bounty's own weight in its region");
            Assert.AreEqual(9, ItemBounties.WeightAt(Vod, "vod", On(("vod", 9))));
            Assert.AreEqual(0, ItemBounties.WeightAt(Vod, "vod", On(("vod", 0))));
            Assert.AreEqual(0, ItemBounties.WeightAt(Vod, "t10", null), "never the other region");
            Assert.AreEqual(6, ItemBounties.WeightAt(Thaelaryn, "t10", null), "\"t10\" is Thaelaryn's region, not a tier");

            // a weight where it can't drop (only a hand-edited store or a changed weenie could hold one): never there
            Assert.AreEqual(0, ItemBounties.WeightAt(Vod, "t15", On(("t15", 50))), "a region bounty in a tier");
            Assert.AreEqual(0, ItemBounties.WeightAt(Vod, "t10", On(("t10", 50))), "a VoD bounty in Thaelaryn");
            Assert.AreEqual(0, ItemBounties.WeightAt(AnyTier, "vod", On(("vod", 50))), "a tier bounty in a region");
            Assert.AreEqual(0, ItemBounties.WeightAt(Fixed15, "t16", On(("t16", 50))), "a fixed-tier bounty in another tier");
            Assert.AreEqual(4, ItemBounties.WeightAt(Fixed15, "t15", On(("t16", 50))), "...and that leaves its own tier alone");

            // a bounty with no weight of its own drops only where it is given one; nothing below 0 ever counts
            Assert.AreEqual(0, ItemBounties.WeightAt(NoOwn, "t15", null));
            Assert.AreEqual(2, ItemBounties.WeightAt(NoOwn, "t15", On(("t15", 2))));
            Assert.AreEqual(0, ItemBounties.WeightAt(new ItemBounties.DropEntry(1, -5, 11, 25, false, "negative"), "t15", null), "a negative own weight");
            Assert.AreEqual(0, ItemBounties.WeightAt(AnyTier, "t15", On(("t15", -5))), "a negative set weight");

            // the places are matched ignoring case, as the store keeps them
            var upper = new BountyPlaceWeights();
            upper.Places["T20"] = 8;
            Assert.AreEqual(8, ItemBounties.WeightAt(Band, "t20", upper));
        }

        /// <summary>
        /// One place's pool: every bounty with a weight above 0 there, in table order, with that weight - and the old
        /// Eligible / EligibleInRegion are the pools with the weenies' own weights only.
        /// </summary>
        [TestMethod]
        public void PoolAt_EachPlaceItsOwnPool()
        {
            var table = AllKinds.ToList();
            var sets = new Dictionary<uint, BountyPlaceWeights>
            {
                [Band.Wcid] = On(("t14", 0), ("t20", 7)),
                [Fixed15.Wcid] = Off(),
                [NoOwn.Wcid] = On(("t15", 2)),
                [Vod.Wcid] = On(("vod", 9)),
                [AnyTier.Wcid] = On(("vod", 5)),   // a tier bounty can't be in a region: ignored
            };

            string Pool(string place, IReadOnlyDictionary<uint, BountyPlaceWeights> s)
                => string.Join(" ", ItemBounties.PoolAt(table, place, s).Select(p => p.Entry.Wcid % 100 + ":" + p.Weight));

            Assert.AreEqual("1:50 2:10 3:4", Pool("t15", null), "the weenies' own weights");
            Assert.AreEqual("1:50 2:10 6:2", Pool("t15", sets), "Tier 15 Bounty is off; Zone Control Only joins");
            Assert.AreEqual("1:50", Pool("t14", sets), "Band Bounty set 0 at t14: out of that pool");
            Assert.AreEqual("1:50 2:7", Pool("t20", sets), "Band Bounty added at t20");
            Assert.AreEqual("1:50", Pool("t11", sets));
            Assert.AreEqual("4:9", Pool("vod", sets), "the tier bounty's vod weight never puts it there");
            Assert.AreEqual("5:6", Pool("t10", sets));

            Assert.AreEqual(0, ItemBounties.PoolAt(null, "t15", sets).Count);
            Assert.AreEqual(0, ItemBounties.PoolAt(table, null, sets).Count);
            Assert.AreEqual(0, ItemBounties.PoolAt(table, "", sets).Count);
            Assert.AreEqual(0, ItemBounties.PoolAt(table, "t26", sets).Count, "not a place: an empty pool");
            Assert.AreEqual(0, ItemBounties.PoolAt(table, "atlantis", null).Count);

            for (var tier = ItemBounties.MinTier; tier <= ItemBounties.MaxTier; tier++)
                CollectionAssert.AreEqual(
                    ItemBounties.PoolAt(table, ItemBounties.TierPlace(tier), null).Select(p => p.Entry.Wcid).ToArray(),
                    ItemBounties.Eligible(table, tier).Select(e => e.Wcid).ToArray(), $"T{tier}");
            foreach (var region in new[] { "vod", "t10" })
                CollectionAssert.AreEqual(
                    ItemBounties.PoolAt(table, region, null).Select(p => p.Entry.Wcid).ToArray(),
                    ItemBounties.EligibleInRegion(table, region).Select(e => e.Wcid).ToArray(), region);
        }

        /// <summary>
        /// The Bounty Editor's weightAt (ported verbatim below) gives the server's WeightAt for every kind of bounty, every
        /// place, and Zone Control states with set, 0, added, off and can't-drop places - so its pool grid and shares are the
        /// server's.
        /// </summary>
        [TestMethod]
        public void WeightAt_MatchesTheBountyEditor()
        {
            var rng = new Random(20261110);
            var sets = new List<BountyPlaceWeights> { null, On(), Off(), Off(("t15", 5)), On(("t15", 0), ("t16", 1000), ("vod", 4), ("t10", 0)) };
            for (var i = 0; i < 200; i++)
            {
                var set = new BountyPlaceWeights { Off = rng.Next(5) == 0 };
                foreach (var place in ItemBounties.AllPlaces)
                    if (rng.Next(3) == 0)
                        set.Places[place] = rng.Next(4) == 0 ? 0 : rng.Next(1, 2000);
                sets.Add(set);
            }

            var entries = AllKinds.Append(new ItemBounties.DropEntry(99985007, 7, 11, 11, true, "Tier 11 Bounty")).ToList();
            foreach (var e in entries)
                foreach (var set in sets)
                {
                    var state = new Dictionary<uint, BountyPlaceWeights>();
                    if (set != null) state[e.Wcid] = set;
                    foreach (var place in ItemBounties.AllPlaces)
                        Assert.AreEqual(ItemBounties.WeightAt(e, place, set), EditorWeightAt(state, EditorBounty.Of(e), place),
                            $"{e.Name} at {place} with {StateOf(set)}");
                }
        }

        // -- the place words an admin types --

        /// <summary>
        /// ParsePlaces reads one place, a run, a comma list, the groups, several tokens and any case, always into AllPlaces
        /// order without repeats; anything else - a bad place, a reversed run, a run through a region, nothing - is refused
        /// with a plain-ASCII reason, never guessed.
        /// </summary>
        [TestMethod]
        public void ParsePlaces_Words()
        {
            string Parse(params string[] tokens)
            {
                var places = ItemBounties.ParsePlaces(tokens, out var error);
                Assert.IsNotNull(places, $"'{string.Join(" ", tokens)}' refused: {error}");
                Assert.IsNull(error);
                return string.Join(" ", places);
            }

            Assert.AreEqual("t15", Parse("t15"));
            Assert.AreEqual("t11 t12 t13 t14 t15", Parse("t11-t15"));
            Assert.AreEqual("t15", Parse("t15-t15"), "a run of one");
            Assert.AreEqual("t11 t13 vod", Parse("t11,t13,vod"));
            Assert.AreEqual("t10", Parse("t10"), "Thaelaryn Island's key");
            Assert.AreEqual(string.Join(" ", ItemBounties.AllPlaces.Take(15)), Parse("tiers"));
            Assert.AreEqual("vod t10", Parse("regions"));
            Assert.AreEqual(string.Join(" ", ItemBounties.AllPlaces), Parse("all"));

            // several tokens, repeats, order and case
            Assert.AreEqual("t11 t13 vod", Parse("vod", "t13", "t11"), "always AllPlaces order");
            Assert.AreEqual("t11 t12 t20", Parse("t11-t12", "t12,t20", "T11"), "each place once");
            Assert.AreEqual("t11 t12 t13 vod", Parse("T11-T13", "VoD"));
            Assert.AreEqual(string.Join(" ", ItemBounties.AllPlaces), Parse("TIERS", "Regions"));
            Assert.AreEqual(string.Join(" ", ItemBounties.AllPlaces), Parse("all", "t15", "vod"), "a group and its own places");
            Assert.AreEqual("t11 t12", Parse("t11,,t12,"), "empty pieces of a comma list are skipped");
            Assert.AreEqual("t11 t12", Parse("t11", null, "", "t12"));

            void Refused(string why, params string[] tokens)
            {
                var places = ItemBounties.ParsePlaces(tokens, out var error);
                Assert.IsNull(places, $"'{string.Join(" ", tokens)}' must be refused ({why})");
                Assert.IsFalse(string.IsNullOrWhiteSpace(error), why);
                Assert.IsTrue(error.All(c => c < 128), $"7-bit ASCII: {error}");
            }

            Refused("a tier out of range", "t26");
            Refused("a tier below T11", "t9");
            Refused("not a region", "atlantis");
            Refused("one bad place spoils the line, nothing is half-done", "t15", "atlantis");
            Refused("one bad place in a list", "t11,t99,vod");
            Refused("a reversed run", "t15-t11");
            Refused("a run from the region t10", "t10-t12");
            Refused("a run between regions", "vod-t10");
            Refused("a run with no end", "t11-");
            Refused("a run with no start", "-t11");
            Refused("a run of runs", "t11-t13-t15");
            Refused("a run past T25", "t20-t26");
            Refused("a number alone", "15");
            Refused("no places");
            Refused("only empty pieces", ",", " ");
            Refused("only a null", (string)null);
            Assert.IsNull(ItemBounties.ParsePlaces(null, out var nullError));
            Assert.IsNotNull(nullError, "no tokens at all: refused with a reason");

            ItemBounties.ParsePlaces(new[] { "t15-t11" }, out var reversed);
            StringAssert.Contains(reversed, "low to high");
        }

        /// <summary>
        /// PlacesToken writes places the way the Bounty Editor does - in AllPlaces order, runs of tiers joined ("t11-t13"),
        /// never a run through a region - and ParsePlaces reads every such token back to the same places.
        /// </summary>
        [TestMethod]
        public void PlacesToken_JoinsRunsAndRoundTrips()
        {
            Assert.AreEqual("t11-t13,t15,vod", ItemBounties.PlacesToken(new[] { "t11", "t12", "t13", "t15", "vod" }));
            Assert.AreEqual("t11-t25,vod,t10", ItemBounties.PlacesToken(ItemBounties.AllPlaces));
            Assert.AreEqual("t25,vod,t10", ItemBounties.PlacesToken(new[] { "t25", "vod", "t10" }), "a run never reaches into the regions");
            Assert.AreEqual("vod,t10", ItemBounties.PlacesToken(new[] { "t10", "vod" }), "AllPlaces order, not the caller's");
            Assert.AreEqual("t11,t10", ItemBounties.PlacesToken(new[] { "t10", "t11" }), "t10 is a region: never t10-t11");
            Assert.AreEqual("t11,t13", ItemBounties.PlacesToken(new[] { "t13", "t11" }));
            Assert.AreEqual("t11-t12", ItemBounties.PlacesToken(new[] { "t12", "t11" }));
            Assert.AreEqual("t11-t13,vod", ItemBounties.PlacesToken(new[] { "T13", "t11", "t12", "t12", "VOD" }), "case and repeats");
            Assert.AreEqual("t15", ItemBounties.PlacesToken(new[] { "t99", "atlantis", "t15", null }), "not a place: left out");
            Assert.AreEqual("", ItemBounties.PlacesToken(Array.Empty<string>()));
            Assert.AreEqual("", ItemBounties.PlacesToken(null));

            // every subset (a fixed sample): the editor's token, and back through ParsePlaces to the same places
            var rng = new Random(1110);
            for (var i = 0; i < 2000; i++)
            {
                var subset = ItemBounties.AllPlaces.Where(_ => rng.Next(2) == 0).ToList();
                if (subset.Count == 0) continue;
                var token = ItemBounties.PlacesToken(subset.OrderBy(_ => rng.Next()));
                Assert.AreEqual(EditorPlacesToken(subset.OrderBy(_ => rng.Next()).ToList()), token, "the editor's token");
                var back = ItemBounties.ParsePlaces(new[] { token }, out var error);
                Assert.IsNotNull(back, $"'{token}' refused: {error}");
                CollectionAssert.AreEqual(subset, back, token);
            }
        }

        // -- the bw line the Bounty Editor loads --

        /// <summary>
        /// A bw line: "bw wcid own=N off=yes|no", the places Zone Control sets in AllPlaces order (0 kept), "# name" - with
        /// invariant digits on any machine, and a name that can't break the line.
        /// </summary>
        [TestMethod]
        public void BwLine_Format()
        {
            ItemBountiesTests.InCulture("de-DE", () =>   // a grouping dot here must never reach the line
            {
                Assert.AreEqual("bw 99985001 own=10 off=no t15=1 t16=1000000 # Bounty of Rebirth",
                    ItemBounties.BwLine(99985001, 10, On(("t16", 1_000_000), ("t15", 1)), "Bounty of Rebirth"));
                Assert.AreEqual("bw 99985001 own=1000 off=no # Bounty of Rebirth", ItemBounties.BwLine(99985001, 1000, null, "Bounty of Rebirth"));
            });
            Assert.AreEqual("bw 99985004 own=3 off=yes vod=0 # VoD Bounty", ItemBounties.BwLine(99985004, 3, Off(("vod", 0)), "VoD Bounty"));
            Assert.AreEqual("bw 99985006 own=0 off=no t11=2 t25=3 vod=4 t10=5 # x",
                ItemBounties.BwLine(99985006, 0, On(("t10", 5), ("vod", 4), ("t25", 3), ("t11", 2)), "x"), "tiers, then vod, then t10");
            Assert.AreEqual("bw 7 own=0 off=no # n", ItemBounties.BwLine(7, -5, null, "n"), "a negative own weight reads 0");
            Assert.AreEqual("bw 7 own=0 off=no t15=1 # n", ItemBounties.BwLine(7, 0, On(("t15", 1), ("t99", 9), ("atlantis", 2)), "n"),
                "a place that isn't one is never written");
            Assert.AreEqual("bw 7 own=0 off=no #", ItemBounties.BwLine(7, 0, null, null).TrimEnd(), "no name");
            StringAssert.DoesNotMatch(ItemBounties.BwLine(7, 0, null, "a|b;c"), new Regex("[|;]"), "the name can't split a chat line");
        }

        /// <summary>
        /// The Bounty Editor's bw pattern reads every bw line the server writes back to the same WCID, own weight, off switch
        /// and places - even with a name that looks like a place ("t15=9") or carries capitals - and its parse (ported below)
        /// gives the same live state.
        /// </summary>
        [TestMethod]
        public void BwLine_ReadByTheBountyEditor()
        {
            var cases = new List<(uint Wcid, int Own, BountyPlaceWeights Set, string Name)>
            {
                (99985001, 10, On(("t15", 1), ("t16", 1000)), "Bounty of Rebirth"),
                (99985004, 3, Off(("vod", 0)), "Bounty of Enlightenment (Valley of Death)"),
                (99985005, 0, Off(), "Thaelaryn #2 t15=9"),
                (99985002, 20, null, "Bounty of Second Thoughts"),
                (4294967295, 1_000_000, On(ItemBounties.AllPlaces.Select((p, i) => (p, i * 1000)).ToArray()), "Max = ALL"),
            };
            var rng = new Random(77);
            for (var i = 0; i < 300; i++)
            {
                var set = rng.Next(4) == 0 ? null : new BountyPlaceWeights { Off = rng.Next(3) == 0 };
                if (set != null)
                    foreach (var place in ItemBounties.AllPlaces)
                        if (rng.Next(4) == 0) set.Places[place] = rng.Next(3) == 0 ? 0 : rng.Next(0, BountyRewardTable.MaxWeight + 1);
                cases.Add(((uint)rng.Next(1, int.MaxValue), rng.Next(0, 5000), set, "Bounty " + i));
            }

            foreach (var (wcid, own, set, name) in cases)
            {
                var line = ItemBounties.BwLine(wcid, own, set, name);
                var m = EditorBwLine.Match("[ZoneControl] " + line);   // as pasted from chat
                Assert.IsTrue(m.Success, line);
                Assert.AreEqual(wcid.ToString(CultureInfo.InvariantCulture), m.Groups[1].Value, line);
                Assert.AreEqual(own.ToString(CultureInfo.InvariantCulture), m.Groups[2].Value, line);
                Assert.AreEqual(set?.Off == true ? "yes" : "no", m.Groups[3].Value, line);

                var places = m.Groups[4].Value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                    .Select(kv => kv.ToLowerInvariant().Split('='))
                    .ToDictionary(kv => kv[0], kv => int.Parse(kv[1], CultureInfo.InvariantCulture));
                Assert.AreEqual(StateOf(On(places.Select(kv => (kv.Key, kv.Value)).ToArray())),
                    StateOf(On((set?.Places ?? new Dictionary<string, int>()).Select(kv => (kv.Key, kv.Value)).ToArray())), line);

                // the editor's own parse: a bounty with nothing set is no entry; anything else is exactly the server's set
                var parsed = EditorParseLiveLines(line + "\r\n");
                Assert.AreEqual(1, parsed.Seen.Count, line);
                Assert.AreEqual((wcid, own), parsed.Seen[0], line);
                if (set == null || set.IsEmpty)
                    Assert.IsFalse(parsed.State.ContainsKey(wcid), line);
                else
                    Assert.AreEqual(StateOf(set), StateOf(parsed.State[wcid]), line);
            }
        }

        // -- the reply's words and the undo commands --

        /// <summary>WeightsText (the audit line): OFF first, then the places Zone Control sets in AllPlaces order, invariant digits.</summary>
        [TestMethod]
        public void WeightsText_Words()
        {
            Assert.AreEqual("its own weights", ItemBounties.WeightsText(null));
            Assert.AreEqual("its own weights", ItemBounties.WeightsText(On()));
            Assert.AreEqual("its own weights", ItemBounties.WeightsText(new BountyPlaceWeights { Places = null }));
            Assert.AreEqual("OFF", ItemBounties.WeightsText(Off()));
            Assert.AreEqual("t15=1, t16=1000, vod=0", ItemBounties.WeightsText(On(("vod", 0), ("t16", 1000), ("t15", 1))), "AllPlaces order, 0 kept");
            Assert.AreEqual("OFF, t15=1, t16=1000", ItemBounties.WeightsText(Off(("t16", 1000), ("t15", 1))));
            Assert.AreEqual("its own weights", ItemBounties.WeightsText(On(("t99", 5))), "not a place: not a setting");
            ItemBountiesTests.InCulture("de-DE", () =>
                Assert.AreEqual("t25=1000000, t10=0", ItemBounties.WeightsText(On(("t10", 0), ("t25", 1_000_000)))));
            Assert.AreNotEqual(ItemBounties.WeightsText(On(("t15", 0))), ItemBounties.WeightsText(On()), "a 0 is a change from nothing set");
        }

        /// <summary>
        /// UndoLines, for hand-picked edits: the off switch first, then one absolute line per value in the order its first
        /// place comes up, "clear" where nothing was set, places joined the editor's way - nothing when nothing changed.
        /// </summary>
        [TestMethod]
        public void UndoLines_Commands()
        {
            const uint w = 99985001;
            CollectionAssert.AreEqual(new[] { "/zonecontrol bountyweight 99985001 clear t15-t16" },
                ItemBounties.UndoLines(w, null, On(("t15", 5), ("t16", 5))), "a first setting is undone by clear");
            CollectionAssert.AreEqual(new[] { "/zonecontrol bountyweight 99985001 on" }, ItemBounties.UndoLines(w, null, Off()));
            CollectionAssert.AreEqual(new[] { "/zonecontrol bountyweight 99985001 off" }, ItemBounties.UndoLines(w, Off(("t15", 1)), On(("t15", 1))));
            CollectionAssert.AreEqual(new[]
                {
                    "/zonecontrol bountyweight 99985001 5 t11-t12",
                    "/zonecontrol bountyweight 99985001 7 t13",
                    "/zonecontrol bountyweight 99985001 clear t14",
                },
                ItemBounties.UndoLines(w, On(("t11", 5), ("t12", 5), ("t13", 7), ("t20", 0)), On(("t11", 9), ("t12", 9), ("t13", 9), ("t14", 9), ("t20", 0))));
            CollectionAssert.AreEqual(new[]
                {
                    "/zonecontrol bountyweight 99985001 off",
                    "/zonecontrol bountyweight 99985001 5 t15,vod",
                    "/zonecontrol bountyweight 99985001 0 t16",
                },
                ItemBounties.UndoLines(w, Off(("t15", 5), ("t16", 0), ("vod", 5)), null), "a reset is undone in full");
            CollectionAssert.AreEqual(new[] { "/zonecontrol bountyweight 99985001 0 t15" }, ItemBounties.UndoLines(w, On(("t15", 0)), On(("t15", 3))), "a 0 is put back as 0");
            Assert.AreEqual(0, ItemBounties.UndoLines(w, On(("t15", 1)), On(("t15", 1))).Count);
            Assert.AreEqual(0, ItemBounties.UndoLines(w, null, null).Count);
            Assert.AreEqual(0, ItemBounties.UndoLines(w, null, On()).Count, "nothing set either way");
            ItemBountiesTests.InCulture("de-DE", () =>
                CollectionAssert.AreEqual(new[] { "/zonecontrol bountyweight 99985001 1000000 t15" }, ItemBounties.UndoLines(w, On(("t15", 1_000_000)), null)));
        }

        /// <summary>
        /// Applying the undo lines to the state after an edit - the way EditWeights applies a command - always gives back the
        /// state before it, off switch included; every line is one the server accepts (no place the bounty can't drop at);
        /// and the lines are exactly the Bounty Editor's poolCommands for going from after back to before.
        /// </summary>
        [TestMethod]
        public void UndoLines_PutTheStateBack()
        {
            var rng = new Random(4242);
            foreach (var e in AllKinds)
            {
                var canDrop = ItemBounties.AllPlaces.Where(p => ItemBounties.CanDropAt(e, p)).ToList();
                BountyPlaceWeights RandomSet()
                {
                    if (rng.Next(6) == 0) return null;
                    var set = new BountyPlaceWeights { Off = rng.Next(3) == 0 };
                    foreach (var place in canDrop)
                        if (rng.Next(3) == 0) set.Places[place] = rng.Next(5) == 0 ? 0 : rng.Next(1, 4) * 500;   // few values: shared undo lines
                    return set;
                }

                for (var i = 0; i < 300; i++)
                {
                    var before = RandomSet();
                    var after = RandomSet();
                    var undo = ItemBounties.UndoLines(e.Wcid, before, after);
                    var state = after;
                    foreach (var line in undo)
                        state = ApplyCommand(state, e, line);
                    Assert.AreEqual(StateOf(before), StateOf(state), $"{e.Name}: {StateOf(after)} undone by [{string.Join(" ; ", undo)}]");
                    if (StateOf(before) == StateOf(after))
                        Assert.AreEqual(0, undo.Count, "no change, no undo");

                    var editor = EditorPoolCommands(
                        after == null ? new Dictionary<uint, BountyPlaceWeights>() : new Dictionary<uint, BountyPlaceWeights> { [e.Wcid] = after },
                        before == null ? new Dictionary<uint, BountyPlaceWeights>() : new Dictionary<uint, BountyPlaceWeights> { [e.Wcid] = before });
                    CollectionAssert.AreEqual(editor, undo, "the server's undo is the editor's command list for the same change");
                }
            }
        }

        /// <summary>
        /// One "/zonecontrol bountyweight wcid action [places]" command applied to <paramref name="state"/> by
        /// EditWeights' rules - off / on / reset take no places, a weight is a whole number 0..MaxWeight refused at an
        /// explicit place the bounty can't drop at, clear removes - then sanitized as the store does. Asserts the server
        /// would accept the command.
        /// </summary>
        private static BountyPlaceWeights ApplyCommand(BountyPlaceWeights state, ItemBounties.DropEntry e, string line)
        {
            var args = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.IsTrue(args.Length >= 4 && args[0] == "/zonecontrol" && args[1] == "bountyweight", line);
            Assert.AreEqual(e.Wcid.ToString(CultureInfo.InvariantCulture), args[2], line);
            var action = args[3];
            var tokens = args.Skip(4).ToList();
            var next = (state ?? new BountyPlaceWeights()).Clone();
            switch (action)
            {
                case "off":
                case "on":
                case "reset":
                    Assert.AreEqual(0, tokens.Count, $"'{action}' takes no places: {line}");
                    if (action == "reset") next.Places.Clear();
                    next.Off = action == "off";
                    break;
                default:
                    var places = ItemBounties.ParsePlaces(tokens, out var error);
                    Assert.IsNotNull(places, $"{line}: {error}");
                    if (action == "clear")
                    {
                        foreach (var place in places) next.Places.Remove(place);
                        break;
                    }
                    Assert.IsTrue(int.TryParse(action, NumberStyles.None, CultureInfo.InvariantCulture, out var weight) && weight <= BountyRewardTable.MaxWeight,
                        $"'{action}' isn't a weight the server takes: {line}");
                    var bad = places.Where(p => !ItemBounties.CanDropAt(e, p)).ToList();
                    Assert.AreEqual(0, bad.Count, $"the server refuses {string.Join(", ", bad)} for {e.Name}: {line}");
                    foreach (var place in places) next.Places[place] = weight;
                    break;
            }
            next.Sanitize(ZoneControlManager.IsBountyPlaceKey);
            return next.IsEmpty ? null : next;
        }

        // -- the Bounty Editor's rules, ported verbatim from tools\bounty-editor\BountyEditor.src.html --

        /// <summary>A bounty as the Bounty Editor holds it: tierMode "kill" (a band) / "fixed" / a region key.</summary>
        private sealed record EditorBounty(uint Wcid, string TierMode, int Tier, int BandMin, int BandMax, int DropWeight)
        {
            /// <summary>The editor's record for a server drop entry.</summary>
            public static EditorBounty Of(ItemBounties.DropEntry e)
                => new(e.Wcid, !string.IsNullOrEmpty(e.Region) ? e.Region : e.FixedTier ? "fixed" : "kill", e.MinTier, e.MinTier, e.MaxTier, e.Weight);

            /// <summary>isRegion(b): its tierMode is a region key.</summary>
            public bool IsRegion => TierMode == "vod" || TierMode == "t10";
        }

        /// <summary>isTierPlace(p): every place but the two regions.</summary>
        private static bool EditorIsTierPlace(string p) => p != "vod" && p != "t10";

        /// <summary>canDropAt(b, p).</summary>
        private static bool EditorCanDropAt(EditorBounty b, string p)
        {
            if (b.IsRegion) return b.TierMode == p;
            if (!EditorIsTierPlace(p)) return false;
            return b.TierMode != "fixed" || p == "t" + b.Tier;
        }

        /// <summary>naturalAt(b, p).</summary>
        private static bool EditorNaturalAt(EditorBounty b, string p)
        {
            if (!EditorCanDropAt(b, p)) return false;
            if (b.IsRegion) return true;
            var t = int.Parse(p[1..], CultureInfo.InvariantCulture);
            return b.TierMode == "fixed" ? t == b.Tier : t >= b.BandMin && t <= b.BandMax;
        }

        /// <summary>weightAt(state, b, p).</summary>
        private static int EditorWeightAt(Dictionary<uint, BountyPlaceWeights> state, EditorBounty b, string p)
        {
            var e = state.TryGetValue(b.Wcid, out var s) ? s : new BountyPlaceWeights();
            if (e.Off || !EditorCanDropAt(b, p)) return 0;
            if (e.Places.TryGetValue(p, out var w)) return w;
            return EditorNaturalAt(b, p) && b.DropWeight > 0 ? b.DropWeight : 0;
        }

        /// <summary>placesToken(places).</summary>
        private static string EditorPlacesToken(List<string> places)
        {
            var idx = places.Select(p => Array.IndexOf(EditorPlaces, p)).OrderBy(i => i).ToList();
            var output = new List<string>();
            for (var i = 0; i < idx.Count; i++)
            {
                var j = i;
                while (j + 1 < idx.Count && idx[j + 1] == idx[j] + 1 && EditorIsTierPlace(EditorPlaces[idx[j + 1]]) && EditorIsTierPlace(EditorPlaces[idx[i]])) j++;
                output.Add(j > i ? $"{EditorPlaces[idx[i]]}-{EditorPlaces[idx[j]]}" : EditorPlaces[idx[i]]);
                i = j;
            }
            return string.Join(",", output);
        }

        /// <summary>poolCommands(): the commands that take the live state (baseline) to the working one.</summary>
        private static List<string> EditorPoolCommands(Dictionary<uint, BountyPlaceWeights> baseline, Dictionary<uint, BountyPlaceWeights> working)
        {
            var lines = new List<string>();
            foreach (var w in baseline.Keys.Union(working.Keys).Distinct().OrderBy(x => x))
            {
                var before = baseline.TryGetValue(w, out var b) ? b : new BountyPlaceWeights();
                var after = working.TryGetValue(w, out var a) ? a : new BountyPlaceWeights();
                if (before.Off != after.Off) lines.Add($"/zonecontrol bountyweight {w} {(after.Off ? "off" : "on")}");
                var byValue = new List<(string Key, List<string> Places)>();   // a JS Map: insertion order
                foreach (var p in EditorPlaces)
                {
                    int? bv = before.Places.TryGetValue(p, out var x) ? x : null, av = after.Places.TryGetValue(p, out var y) ? y : null;
                    if (bv == av) continue;
                    var key = av == null ? "clear" : av.Value.ToString(CultureInfo.InvariantCulture);
                    var at = byValue.FindIndex(kv => kv.Key == key);
                    if (at < 0) byValue.Add((key, new List<string> { p }));
                    else byValue[at].Places.Add(p);
                }
                foreach (var (value, places) in byValue) lines.Add($"/zonecontrol bountyweight {w} {value} {EditorPlacesToken(places)}");
            }
            return lines;
        }

        /// <summary>parseLiveLines(text): the live state (only bounties with something set) and every (wcid, own) seen.</summary>
        private static (Dictionary<uint, BountyPlaceWeights> State, List<(uint Wcid, int Own)> Seen) EditorParseLiveLines(string text)
        {
            var state = new Dictionary<uint, BountyPlaceWeights>();
            var seen = new List<(uint, int)>();
            foreach (var line in Regex.Split(text, "\r?\n"))
            {
                var m = EditorBwLine.Match(line);
                if (!m.Success) continue;
                var w = uint.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                var places = new BountyPlaceWeights();
                foreach (var kv in m.Groups[4].Value.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                {
                    var pv = kv.ToLowerInvariant().Split('=');
                    if (EditorPlaces.Contains(pv[0])) places.Places[pv[0]] = int.Parse(pv[1], CultureInfo.InvariantCulture);
                }
                seen.Add((w, int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)));
                var off = m.Groups[3].Value.ToLowerInvariant() == "yes";
                if (off || places.Places.Count > 0)
                {
                    places.Off = off;
                    state[w] = places;
                }
            }
            return (state, seen);
        }
    }
}
