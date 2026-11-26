using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ACE.Entity.Enum;
using ACE.Server.Command.Handlers;
using ACE.Server.Managers.ZoneControl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Staff kill rates (owner 2026-11-26): KillRateStore's in-memory per-character, per-clock-hour T11+ kill counts and the
    /// rules that turn them into the /killrate and portal numbers - hour rollover, the 48-hour ring, per-tier counts, part
    /// hours, and the own-tier average over clean hours. No DB, no world.
    /// </summary>
    [TestClass]
    public class KillRateTrackerTests
    {
        private const uint Og = 0x50000001;
        private const uint Nerd = 0x50000002;

        /// <summary>2026-11-26 06:00:00 UTC.</summary>
        private static readonly long H6 = new DateTimeOffset(2026, 11, 26, 6, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        private static long Hour => KillRateStore.HourOf(H6);

        private static long At(int hoursAfter6, int minutes, int seconds = 0) => H6 + hoursAfter6 * 3600L + minutes * 60L + seconds;

        /// <summary><paramref name="count"/> kills at <paramref name="tier"/> spread evenly over a whole hour (first at :00, last at :59:59).</summary>
        private static void FullHour(KillRateStore store, uint guid, string name, int tier, int count, int hoursAfter6)
        {
            for (var i = 0; i < count; i++)
                store.Record(guid, name, tier, At(hoursAfter6, 0) + (count == 1 ? 0 : i * 3599L / (count - 1)));
        }

        // ---- counting and rollover ---------------------------------------------------------------------------------

        [TestMethod]
        public void Counts_Per_Tier_And_Ignores_Tiers_Outside_T11_T25()
        {
            var store = new KillRateStore();
            store.Record(Og, "Og", 15, At(0, 1));
            store.Record(Og, "Og", 15, At(0, 2));
            store.Record(Og, "Og", 14, At(0, 3));
            Assert.IsNull(store.Record(Og, "Og", 10, At(0, 4)), "T10 is not counted");
            Assert.IsNull(store.Record(Og, "Og", 26, At(0, 4)), "past T25 is not counted");

            var hour = store.Report(At(0, 30), 1).Single().Hours.Single();
            Assert.AreEqual(3, hour.Total);
            CollectionAssert.AreEqual(new[] { 14, 15 }, hour.ByTier.Keys.ToArray());
            Assert.AreEqual(2, hour.ByTier[15]);
            Assert.AreEqual(1, hour.ByTier[14]);
            Assert.AreEqual(At(0, 1), hour.First);
            Assert.AreEqual(At(0, 3), hour.Last);
        }

        [TestMethod]
        public void A_New_Clock_Hour_Opens_A_New_Bucket_Once()
        {
            var store = new KillRateStore();
            Assert.IsNotNull(store.Record(Og, "Og", 15, At(0, 59, 59)), "first kill of the hour opens a bucket");
            Assert.IsNull(store.Record(Og, "Og", 15, At(0, 59, 59)), "later kills that hour do not");

            var opened = store.Record(Og, "Og", 15, At(1, 0, 0));
            Assert.IsNotNull(opened, "06:59:59 and 07:00:00 are different hours");
            Assert.AreEqual(Hour + 1, opened.Hour);

            var hours = store.Report(At(1, 5), 2).Single().Hours;
            Assert.AreEqual(2, hours.Count);
            Assert.AreEqual(2, hours[0].Total);
            Assert.AreEqual(1, hours[1].Total);
        }

        [TestMethod]
        public void Advance_Reports_Each_Finished_Hour_Once()
        {
            var store = new KillRateStore();
            Assert.AreEqual(0, store.Advance(Hour).Count, "nothing to finish at start");
            Assert.AreEqual(0, store.Advance(Hour).Count, "same hour");
            CollectionAssert.AreEqual(new[] { Hour }, store.Advance(Hour + 1).ToArray());
            Assert.AreEqual(0, store.Advance(Hour + 1).Count, "already reported");
            Assert.AreEqual(0, store.Advance(Hour).Count, "an older hour never moves it back");
            CollectionAssert.AreEqual(new[] { Hour + 1, Hour + 2, Hour + 3 }, store.Advance(Hour + 4).ToArray(), "quiet hours in between");
            Assert.AreEqual(KillRateStore.HoursKept, store.Advance(Hour + 500).Count, "a long gap is capped at the hours kept");
        }

        [TestMethod]
        public void Concurrent_Kills_Are_All_Counted_And_The_Hour_Opens_Once()
        {
            var store = new KillRateStore();
            var opened = 0;
            Parallel.For(0, 20_000, i =>
            {
                if (store.Record(Og, "Og", 11 + i % 15, At(0, 10) + i % 600) != null)
                    System.Threading.Interlocked.Increment(ref opened);
            });

            var hour = store.Report(At(0, 30), 1).Single().Hours.Single();
            Assert.AreEqual(20_000, hour.Total);
            Assert.AreEqual(1, opened);
            Assert.AreEqual(At(0, 10), hour.First);
            Assert.AreEqual(At(0, 10) + 599, hour.Last);
        }

        [TestMethod]
        public void A_Kill_Stamped_Before_The_Hour_Its_Slot_Holds_Is_Dropped()
        {
            var store = new KillRateStore();
            store.Record(Og, "Og", 15, At(KillRateStore.HoursKept, 0));   // the slot now holds hour + 48
            store.Record(Og, "Og", 15, At(0, 0));                         // same slot, 48 hours older

            var hours = store.Report(At(KillRateStore.HoursKept, 1), KillRateStore.HoursKept).Single().Hours;
            Assert.AreEqual(1, hours.Count);
            Assert.AreEqual(1, hours[0].Total);
        }

        // ---- the 48-hour window --------------------------------------------------------------------------------------

        [TestMethod]
        public void Buckets_Older_Than_48_Hours_Are_Replaced_And_Not_Reported()
        {
            var store = new KillRateStore();
            store.Record(Og, "Og", 15, At(0, 10));
            store.Record(Og, "Og", 15, At(0, 20));
            store.Record(Og, "Og", 15, At(1, 10));

            // 48 hours on, 06:00's slot is reused: its 2 kills are gone, not added to
            store.Record(Og, "Og", 15, At(KillRateStore.HoursKept, 5));
            var hours = store.Report(At(KillRateStore.HoursKept, 30), KillRateStore.HoursKept).Single().Hours;
            CollectionAssert.AreEqual(new[] { Hour + 1, Hour + KillRateStore.HoursKept }, hours.Select(h => h.Hour).ToArray());
            Assert.AreEqual(1, hours[1].Total);

            // the window asked for limits it too
            Assert.AreEqual(1, store.Report(At(KillRateStore.HoursKept, 30), 1).Single().Hours.Count);
        }

        [TestMethod]
        public void Prune_Drops_Characters_With_No_Kill_In_The_Hours_Kept()
        {
            var store = new KillRateStore();
            store.Record(Og, "Og", 15, At(0, 10));
            store.Record(Nerd, "Nerd", 15, At(40, 10));

            store.Prune(Hour + 47);
            Assert.AreEqual(2, store.Report(At(47, 0), KillRateStore.HoursKept).Count, "06:00 is still inside 48 hours");

            store.Prune(Hour + KillRateStore.HoursKept);
            var left = store.Report(At(KillRateStore.HoursKept, 0), KillRateStore.HoursKept);
            CollectionAssert.AreEqual(new[] { "Nerd" }, left.Select(r => r.Name).ToArray());
        }

        // ---- part hours ----------------------------------------------------------------------------------------------

        [TestMethod]
        public void Part_Hour_Rules()
        {
            var h = Hour;
            Assert.IsFalse(KillRateStore.IsPartHour(h, At(0, 0), At(0, 59, 59), false), "whole hour");
            Assert.IsFalse(KillRateStore.IsPartHour(h, At(0, 5), At(0, 55), false), "exactly 5 minutes of slack each end");
            Assert.IsTrue(KillRateStore.IsPartHour(h, At(0, 5, 1), At(0, 59), false), "started late");
            Assert.IsTrue(KillRateStore.IsPartHour(h, At(0, 0), At(0, 54, 59), false), "stopped early");
            Assert.IsTrue(KillRateStore.IsPartHour(h, At(0, 40), At(0, 59), false), "played 20 minutes");
            Assert.IsTrue(KillRateStore.IsPartHour(h, At(0, 0), At(0, 59, 59), true), "the running hour is never full");
        }

        // ---- own tier and the average --------------------------------------------------------------------------------

        [TestMethod]
        public void Clean_Hour_Rules()
        {
            Assert.IsTrue(KillRateStore.IsCleanHour(700, 700, false));
            Assert.IsTrue(KillRateStore.IsCleanHour(95, 100, false), "95 pct own tier is clean");
            Assert.IsFalse(KillRateStore.IsCleanHour(94, 100, false), "under 95 pct is not");
            Assert.IsFalse(KillRateStore.IsCleanHour(700, 700, true), "a part hour is not");
            Assert.IsFalse(KillRateStore.IsCleanHour(0, 0, false), "no kills is not");
            Assert.IsFalse(KillRateStore.IsCleanHour(700, 700, false, ownTierKnown: false), "unknown own tier is not");

            // the hand-built table's rule (off-tier above 5 and above 5 pct), on clean or part hours
            Assert.IsFalse(KillRateStore.ShowsTotal(583, 588, part: false, clean: true), "5 off-tier kills are not worth showing");
            Assert.IsTrue(KillRateStore.ShowsTotal(188, 389, part: false, clean: false));
            Assert.IsFalse(KillRateStore.ShowsTotal(950, 1000, part: false, clean: true), "50 of 1000 is 5 pct: not shown");
            Assert.IsTrue(KillRateStore.ShowsTotal(940, 1000, part: true, clean: false));
            Assert.IsFalse(KillRateStore.ShowsTotal(3, 5, part: true, clean: false), "a part hour with 2 off-tier kills: not shown");

            // a full hour left out of the average always shows why
            Assert.IsTrue(KillRateStore.ShowsTotal(47, 50, part: false, clean: false), "94 pct own tier: \"47 (50)\"");
            Assert.IsTrue(KillRateStore.ShowsTotal(0, 3, part: false, clean: false), "no own-tier kill at all: \"0 (3)\"");
            Assert.IsFalse(KillRateStore.ShowsTotal(50, 50, part: false, clean: false), "nothing off-tier: nothing to add");
        }

        [TestMethod]
        public void Report_Row_Carries_The_Same_Shows_Total_Rule()
        {
            var store = new KillRateStore();
            store.SetOwnTier(Og, store.Record(Og, "Og", 15, At(0, 0)), 15);
            FullHour(store, Og, "Og", 15, 46, 0);                  // 47 own-tier over the full hour
            for (var i = 0; i < 3; i++) store.Record(Og, "Og", 14, At(0, 30));   // 3 off-tier: 94 pct, under the clean bar

            var h = store.Report(At(1, 0), 2).Single().Hours.Single();
            Assert.IsFalse(h.Part);
            Assert.IsFalse(h.Clean);
            Assert.IsTrue(h.ShowsTotal, "the row shows 47 (50), not a bare 47");
        }

        [TestMethod]
        public void Average_Uses_Only_Full_Own_Tier_Hours()
        {
            var store = new KillRateStore();
            void OwnTier(int hoursAfter6, int tier)
            {
                var b = store.Record(Og, "Og", 15, At(hoursAfter6, 0));   // opens the hour
                store.SetOwnTier(Og, b, tier);
            }

            // 06:00 clean 600 (1 opener + 599), 07:00 clean 700, 08:00 part hour (stopped at :30), 09:00 half off-tier, 10:00 running
            OwnTier(0, 15); FullHour(store, Og, "Og", 15, 599, 0);
            OwnTier(1, 15); FullHour(store, Og, "Og", 15, 699, 1);
            OwnTier(2, 15); for (var i = 0; i < 300; i++) store.Record(Og, "Og", 15, At(2, 0) + i * 6);
            OwnTier(3, 15); FullHour(store, Og, "Og", 15, 199, 3); FullHour(store, Og, "Og", 14, 200, 3);
            OwnTier(4, 15); store.Record(Og, "Og", 15, At(4, 1));

            var r = store.Report(At(4, 2), 6).Single();
            Assert.AreEqual(15, r.OwnTier);
            Assert.AreEqual(5, r.Hours.Count);
            Assert.AreEqual(2, r.CleanHours);
            Assert.AreEqual(650.0, r.AvgOwnPerHour.Value, 1e-9, "(600 + 700) / 2");

            var off = r.Hours[3];
            Assert.AreEqual(200, off.Own);
            Assert.AreEqual(400, off.Total);
            Assert.IsFalse(off.Clean);
            Assert.IsFalse(off.Part);
            Assert.IsTrue(r.Hours[2].Part);
            Assert.IsTrue(r.Hours[4].Current);
            Assert.IsTrue(r.Hours[4].Part);
            Assert.AreEqual(600 + 700 + 301 + 200 + 2, r.OwnKills);
            Assert.AreEqual(r.OwnKills + 200, r.TotalKills);
        }

        [TestMethod]
        public void Own_Tier_Is_Per_Hour_And_Falls_Back_To_The_Latest_Known()
        {
            var store = new KillRateStore();

            // 06:00 as T14, 07:00 as T15 (gained a tier), 08:00's lookup has not landed yet
            store.SetOwnTier(Og, store.Record(Og, "Og", 14, At(0, 0)), 14);
            FullHour(store, Og, "Og", 14, 300, 0);
            store.SetOwnTier(Og, store.Record(Og, "Og", 15, At(1, 0)), 15);
            FullHour(store, Og, "Og", 15, 500, 1);
            store.Record(Og, "Og", 15, At(2, 0));
            FullHour(store, Og, "Og", 15, 400, 2);

            var r = store.Report(At(3, 0), 4).Single();
            CollectionAssert.AreEqual(new[] { 14, 15, 15 }, r.Hours.Select(h => h.OwnTier).ToArray());
            CollectionAssert.AreEqual(new[] { 301, 501, 401 }, r.Hours.Select(h => h.Own).ToArray());
            Assert.AreEqual(15, r.OwnTier);
            Assert.AreEqual(3, r.CleanHours);

            // no lookup ever landed: own tier unknown, nothing is clean, total still shown
            var fresh = new KillRateStore();
            FullHour(fresh, Nerd, "Nerd", 15, 600, 0);
            var n = fresh.Report(At(1, 0), 2).Single();
            Assert.AreEqual(0, n.OwnTier);
            Assert.AreEqual(600, n.TotalKills);
            Assert.AreEqual(0, n.CleanHours);
            Assert.IsNull(n.AvgOwnPerHour);
        }

        [TestMethod]
        public void Report_Sorts_By_Own_Tier_Then_Average()
        {
            var store = new KillRateStore();
            void Hunter(uint guid, string name, int tier, int kills)
            {
                store.SetOwnTier(guid, store.Record(guid, name, tier, At(0, 0)), tier);
                FullHour(store, guid, name, tier, kills - 1, 0);
            }
            Hunter(1, "Meribia", 14, 371);
            Hunter(2, "Grumpy", 15, 583);
            Hunter(3, "Og", 15, 714);

            CollectionAssert.AreEqual(new[] { "Og", "Grumpy", "Meribia" }, store.Report(At(1, 0), 2).Select(r => r.Name).ToArray());
        }

        [TestMethod]
        public void A_Short_Average_Ranks_After_The_Steady_Ones_In_Its_Tier()
        {
            var store = new KillRateStore();
            // Grumpy: 3 clean hours at 500 (steady); Flash: 1 clean hour at 900 (short)
            for (var h = 0; h < 3; h++)
            {
                store.SetOwnTier(2, store.Record(2, "Grumpy", 15, At(h, 0)), 15);
                FullHour(store, 2, "Grumpy", 15, 499, h);
            }
            store.SetOwnTier(3, store.Record(3, "Flash", 15, At(2, 0)), 15);
            FullHour(store, 3, "Flash", 15, 899, 2);

            var rows = store.Report(At(3, 0), 4);
            CollectionAssert.AreEqual(new[] { "Grumpy", "Flash" }, rows.Select(r => r.Name).ToArray(), "3 hours at 500 before 1 hour at 900");
            Assert.IsTrue(rows[0].Steady);
            Assert.IsFalse(rows[1].Steady);
            Assert.AreEqual(KillRateStore.MinSteadyHours, rows[0].CleanHours);
        }

        [TestMethod]
        public void Prune_Never_Drops_A_Character_Whose_Hour_Just_Opened()
        {
            // SMOKE TEST: a brand-new character's first kill (empty ring) racing a prune at the top of the hour must survive. The
            // real window (between Prune's scan and its remove) is too narrow to force from here, so this passes without the
            // lock too; the guarantee itself is pruneLock (Prune scans and removes under it, Record opens an hour under it).
            for (var round = 0; round < 200; round++)
            {
                var store = new KillRateStore();
                var guid = (uint)(0x50010000 + round);
                Parallel.Invoke(
                    () => store.Record(guid, "New", 15, At(1, 0)),
                    () => store.Prune(Hour + 1));
                Assert.AreEqual(1, store.Report(At(1, 1), 1).Single().TotalKills, $"round {round}");
            }
        }

        // ---- log line and name lookup ---------------------------------------------------------------------------------

        [TestMethod]
        public void Log_Line_For_A_Finished_Hour()
        {
            var store = new KillRateStore();
            store.SetOwnTier(Og, store.Record(Og, "Masta Mage Og", 15, At(0, 0, 4)), 15);
            for (var i = 0; i < 729; i++) store.Record(Og, "Masta Mage Og", 15, At(0, 30));
            store.Record(Og, "Masta Mage Og", 14, At(0, 59, 58));
            store.Record(Og, "Masta Mage Og", 14, At(0, 59, 58));
            store.Record(Nerd, "Nerd Parade", 15, At(1, 0));   // another hour: not in 06:00's lines

            var lines = store.LogLines(Hour);
            Assert.AreEqual(1, lines.Count);
            // no style given: the kills count as Other
            Assert.AreEqual("[KILLRATE] 2026-11-26T06:00Z \"Masta Mage Og\" own T15 | T15:730 T14:2 | total 732 | Other:732 | first 06:00:04 last 06:59:58", lines[0]);
        }

        // ---- kill styles ---------------------------------------------------------------------------------------------

        [TestMethod]
        public void Style_Comes_From_Pet_Then_Stance_Then_Last_Cast_School()
        {
            Assert.AreEqual(KillStyle.Pet, KillRateTracker.StyleOf(true, CombatMode.Missile, MagicSchool.WarMagic), "a pet's kill is Pet whatever the owner does");
            Assert.AreEqual(KillStyle.Melee, KillRateTracker.StyleOf(false, CombatMode.Melee, MagicSchool.VoidMagic));
            Assert.AreEqual(KillStyle.Missile, KillRateTracker.StyleOf(false, CombatMode.Missile, MagicSchool.None));
            Assert.AreEqual(KillStyle.War, KillRateTracker.StyleOf(false, CombatMode.Magic, MagicSchool.WarMagic));
            Assert.AreEqual(KillStyle.Void, KillRateTracker.StyleOf(false, CombatMode.Magic, MagicSchool.VoidMagic));
            Assert.AreEqual(KillStyle.Other, KillRateTracker.StyleOf(false, CombatMode.Magic, MagicSchool.LifeMagic), "a life spell is not war or void");
            Assert.AreEqual(KillStyle.Other, KillRateTracker.StyleOf(false, CombatMode.Magic, MagicSchool.None));
            Assert.AreEqual(KillStyle.Other, KillRateTracker.StyleOf(false, CombatMode.NonCombat, MagicSchool.VoidMagic), "out of stance: a DoT or a smite");
            Assert.AreEqual(KillStyle.Other, KillRateTracker.StyleOf(false, CombatMode.Undef, MagicSchool.None));
        }

        [TestMethod]
        public void Styles_Are_Counted_Per_Hour_And_Summed_Over_The_Window()
        {
            var store = new KillRateStore();
            for (var i = 0; i < 90; i++) store.Record(Og, "Og", 15, At(0, 10), KillStyle.Missile);
            for (var i = 0; i < 10; i++) store.Record(Og, "Og", 15, At(0, 20), KillStyle.War);
            for (var i = 0; i < 30; i++) store.Record(Og, "Og", 14, At(1, 10), KillStyle.Void);   // styles count at every tier
            Assert.IsNull(store.Record(Og, "Og", 10, At(1, 11), KillStyle.Melee), "a T10 kill counts no style either");

            var og = store.Report(At(1, 30), 2).Single();
            Assert.AreEqual(90, og.Hours[0].ByStyle["Missile"]);
            Assert.AreEqual(10, og.Hours[0].ByStyle["War"]);
            Assert.IsFalse(og.Hours[0].ByStyle.ContainsKey("Void"), "only styles with kills are listed");
            Assert.AreEqual(30, og.Hours[1].ByStyle["Void"]);
            Assert.IsFalse(og.Hours[1].ByStyle.ContainsKey("Melee"));

            CollectionAssert.AreEqual(new[] { "Missile", "War", "Void" }, og.ByStyle.Keys.ToArray(), "window totals in KillStyle order");
            Assert.AreEqual(og.TotalKills, og.ByStyle.Values.Sum(), "every counted kill has exactly one style");
            Assert.AreEqual(130, og.TotalKills);
            Assert.AreEqual("Missile", og.MainStyle);
        }

        [TestMethod]
        public void Main_Style_Tie_Goes_To_The_Earlier_Style_And_None_Without_Kills()
        {
            var store = new KillRateStore();
            for (var i = 0; i < 5; i++) store.Record(Og, "Og", 15, At(0, 10), KillStyle.Void);
            for (var i = 0; i < 5; i++) store.Record(Og, "Og", 15, At(0, 11), KillStyle.Melee);
            Assert.AreEqual("Melee", store.Report(At(0, 30), 1).Single().MainStyle, "Melee comes before Void in KillStyle");

            var empty = KillRateStore.Summarize(Nerd, "Nerd", 15, new List<KillRateStore.HourRow>());
            Assert.IsNull(empty.MainStyle);
            Assert.AreEqual(0, empty.ByStyle.Count);
        }

        [TestMethod]
        public void Log_Line_Lists_Styles_In_Order()
        {
            var store = new KillRateStore();
            store.SetOwnTier(Og, store.Record(Og, "Og", 15, At(0, 0), KillStyle.War), 15);
            for (var i = 0; i < 3; i++) store.Record(Og, "Og", 15, At(0, 59), KillStyle.Missile);
            store.Record(Og, "Og", 15, At(0, 59), KillStyle.Pet);

            Assert.AreEqual("[KILLRATE] 2026-11-26T06:00Z \"Og\" own T15 | T15:5 | total 5 | Missile:3 War:1 Pet:1 | first 06:00:00 last 06:59:00", store.LogLines(Hour).Single());
        }

        [TestMethod]
        public void Character_Lookup_Exact_Then_Unique_Prefix()
        {
            var rows = new List<KillRateStore.CharacterReport>
            {
                new() { Name = "+Masta Claude Og", Hours = new() },
                new() { Name = "Masta Mage Og", Hours = new() },
                new() { Name = "Nerd Parade", Hours = new() },
            };

            Assert.AreEqual("Masta Mage Og", KillRateCommands.FindCharacter(rows, "masta mage og", out _).Name);
            Assert.AreEqual("+Masta Claude Og", KillRateCommands.FindCharacter(rows, "Masta Claude Og", out _).Name, "the + is optional");
            Assert.AreEqual("Nerd Parade", KillRateCommands.FindCharacter(rows, "nerd", out _).Name);
            Assert.IsNull(KillRateCommands.FindCharacter(rows, "Masta", out var both));
            Assert.AreEqual(2, both.Count);
            Assert.IsNull(KillRateCommands.FindCharacter(rows, "Avicii", out var none));
            Assert.AreEqual(0, none.Count);

            // a staff "+Og" and a player "Og" both with kills: each is found by its exact name
            var twins = new List<KillRateStore.CharacterReport> { new() { Name = "+Og", Hours = new() }, new() { Name = "Og", Hours = new() } };
            Assert.AreEqual("Og", KillRateCommands.FindCharacter(twins, "og", out _).Name);
            Assert.AreEqual("+Og", KillRateCommands.FindCharacter(twins, "+og", out _).Name);
        }

        [TestMethod]
        public void A_Kill_Before_Its_First_And_Last_Times_Are_Set_Is_Never_Visible()
        {
            // a kill counted in a bucket always has its time: Total > 0 implies First/Last are real times
            var store = new KillRateStore();
            Parallel.For(0, 2_000, i =>
            {
                store.Record(Og, "Og", 15, At(0, i % 60));
                foreach (var h in store.Report(At(0, 59), 1).SelectMany(r => r.Hours))
                    Assert.IsTrue(h.First != long.MaxValue && h.Last != long.MinValue);
            });
        }
    }
}
