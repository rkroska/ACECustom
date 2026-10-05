using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.Common;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.DatLoader;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Managers.ZoneControl;
using ACE.Server.WorldObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Blacksmithing against REAL data: the world and shard databases named in Config.js and the client DAT files.
    /// Everything here only reads. Forged items are built in memory with throwaway guids and never saved.
    ///
    /// These take a few minutes and need a database, so they run only when the environment variable
    /// ACE_FORGE_DB_TESTS is set to 1 (otherwise they report Inconclusive). One command:
    ///   scripts/run-forge-tests.ps1
    /// </summary>
    [TestClass]
    [TestCategory("ForgeDatabase")]
    public class ForgeDatabaseTests
    {
        private static bool ready;
        private static string notReady = "set ACE_FORGE_DB_TESTS=1 to run the database-backed forge tests (scripts/run-forge-tests.ps1)";
        private static uint nextGuid = 0xFFFF0000;
        private static int forgedWithSpells;

        [ClassInitialize]
        public static void Setup(TestContext context)
        {
            if (Environment.GetEnvironmentVariable("ACE_FORGE_DB_TESTS") != "1")
                return;
            try
            {
                var here = Environment.CurrentDirectory;
                var config = new[]
                {
                    Environment.GetEnvironmentVariable("ACE_CONFIG"),
                    Path.Combine(here, @"..\..\..\..\..\ACE.Server\bin\x64\Release\net10.0\Config.js"),
                    Path.Combine(here, @"..\..\..\..\..\ACE.Server\Config.js"),
                }.FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));
                if (config == null)
                {
                    notReady = "no Config.js found (set ACE_CONFIG to its path)";
                    return;
                }

                ConfigManager.Initialize(config);
                if (DatabaseManager.Shard == null)
                    DatabaseManager.Initialize();
                // as ACE.Server Program.cs does: without it the spell table fails to read and every spell lookup is null
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                if (DatManager.PortalDat == null)
                    DatManager.Initialize(ConfigManager.Config.Server.DatFilesDirectory, false, false, false);

                ready = DatManager.PortalDat?.SpellTable != null;
                if (!ready)
                    notReady = "the DAT spell table could not be read";
            }
            catch (Exception ex)
            {
                notReady = "setup failed: " + ex.Message;
            }
        }

        private static void Need()
        {
            if (!ready)
                Assert.Inconclusive(notReady);
        }

        // ---------------------------------------------------------------- helpers

        private static WorldObject Load(uint biotaId)
        {
            var wo = WorldObjectFactory.CreateWorldObject(DatabaseManager.Shard.BaseDatabase.GetBiota(biotaId, true));
            return wo is MeleeWeapon || wo is MissileLauncher || wo is Caster ? wo : null;
        }

        /// <summary>Item ids carrying an int property, straight off the property table (fast, indexed).</summary>
        private static List<uint> IdsWithInt(int type, int minValue, int take)
        {
            using var ctx = new ShardDbContext();
            ctx.Database.SetCommandTimeout(300);
            return ctx.BiotaPropertiesInt.AsNoTracking().Where(p => p.Type == type && p.Value >= minValue)
                .Select(p => p.ObjectId).Take(take).ToList();
        }

        private static WorldObject NewScratch(uint wcid)
            => WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie(wcid), new ObjectGuid(nextGuid++));

        /// <summary>Forges the pair in memory and returns every way the built item disagrees with what the forge decided.</summary>
        private static List<string> ForgeAndCheck(WorldObject main, WorldObject feeder, ForgeMath.RollMode mode, Random rng, double sparkChance)
        {
            var config = ForgeMath.ForgeConfig.FromServerConfig();
            config.RollMode = mode;
            config.SparkChance = sparkChance;
            var m = ForgeWeaponReader.Read(main);
            var f = ForgeWeaponReader.Read(feeder);
            var outcome = ForgeMath.Forge(m, f, config, () => rng.NextDouble());
            var built = ForgeWeaponWriter.Build(main, feeder, outcome, config, NewScratch);
            var problems = new List<string>();
            if (built.Error != null)
            {
                problems.Add("build error: " + built.Error);
                return problems;
            }

            var item = built.Item;
            var back = ForgeWeaponReader.Read(item);

            if (TinkerReversal.Strip(item).Status != TinkerReversal.Status.Ok) problems.Add("result tinkers do not reverse");
            foreach (var line in ForgeMath.OrderFor(m.IsArmor))
            {
                var want = outcome.Result.Lines.TryGetValue(line, out var w) ? w : (double?)null;
                var got = back.Lines.TryGetValue(line, out var g) ? g : (double?)null;
                var tol = ForgeMath.IsWholeNumber(line) ? 0.5 : 1e-6;
                if (want.HasValue != got.HasValue || (want.HasValue && Math.Abs(want.Value - got.Value) > tol))
                    problems.Add($"{line}: forged {want?.ToString() ?? "none"}, item reads {got?.ToString() ?? "none"}");
            }
            if (back.TinkerCount != m.TinkerCount || back.TinkerLog != m.TinkerLog) problems.Add("tinkers not carried from the main weapon");
            foreach (var (line, levels) in outcome.Result.HoneLevels)
                if (back.HoneLevel(line) != levels) problems.Add($"hone {line}: {levels} -> {back.HoneLevel(line)}");
            if (!back.Spells.Keys.OrderBy(k => k).SequenceEqual(outcome.Result.Spells.Keys.OrderBy(k => k))) problems.Add("spell families differ");
            // the item's real spell book, not the reader's view of it: a reader that sees no spells would agree with itself
            var bookWant = outcome.Result.Spells.Values.Select(sp => (int)sp.SpellId).OrderBy(x => x).ToList();
            var bookGot = item.Biota.GetKnownSpellsIds(item.BiotaDatabaseLock).OrderBy(x => x).ToList();
            if (!bookWant.SequenceEqual(bookGot)) problems.Add($"spell book has {bookGot.Count} spells, forged {bookWant.Count}");
            // every spell of both inputs is accounted for: kept, or lost by a recorded pick
            var inputSpells = main.Biota.GetKnownSpellsIds(main.BiotaDatabaseLock).Concat(feeder.Biota.GetKnownSpellsIds(feeder.BiotaDatabaseLock)).Distinct().Count();
            if (inputSpells > 0 && m.Spells.Count + f.Spells.Count == 0) problems.Add($"the inputs carry {inputSpells} spells but the forge read none");
            if (bookGot.Count > 0) forgedWithSpells++;
            if (back.Group != m.Group) problems.Add($"group changed to {back.Group}");
            if (ForgeGroups.FindElementVariant(main.WeenieClassId, (DamageType)outcome.Result.Element).HasValue && back.Element != outcome.Result.Element)
                problems.Add($"element {back.Element} != rolled {outcome.Result.Element}");
            if (item.GetProperty(PropertyInt.Attuned) != (int)AttunedStatus.Attuned || item.GetProperty(PropertyInt.Bonded) != (int)BondedStatus.Bonded)
                problems.Add("not attuned and bonded");
            if (back.ForgeCount != Math.Max(m.ForgeCount, f.ForgeCount) + 1) problems.Add($"forge count {back.ForgeCount}");

            // tier, quality and grades
            if (back.Tier != Math.Max(m.Tier, f.Tier)) problems.Add($"tier {back.Tier} != {Math.Max(m.Tier, f.Tier)}");
            if (back.Quality != outcome.Result.Quality) problems.Add($"quality reads {back.Quality}, forged {outcome.Result.Quality}");
            foreach (var (key, grade) in outcome.Result.ZcGrades)
                if (!back.ZcGrades.TryGetValue(key, out var bg) || bg != grade) problems.Add($"grade {key}: forged {grade}");

            // no wield requirement of either input may be missing or weaker; arcane lore never lower
            var slots = new[]
            {
                (PropertyInt.WieldRequirements, PropertyInt.WieldSkillType, PropertyInt.WieldDifficulty),
                (PropertyInt.WieldRequirements2, PropertyInt.WieldSkillType2, PropertyInt.WieldDifficulty2),
                (PropertyInt.WieldRequirements3, PropertyInt.WieldSkillType3, PropertyInt.WieldDifficulty3),
                (PropertyInt.WieldRequirements4, PropertyInt.WieldSkillType4, PropertyInt.WieldDifficulty4),
            };
            List<(int, int?, int)> Reqs(WorldObject x) => slots.Where(s => (x.GetProperty(s.Item1) ?? 0) != 0 && x.GetProperty(s.Item3).HasValue)
                .Select(s => (x.GetProperty(s.Item1).Value, x.GetProperty(s.Item2), x.GetProperty(s.Item3).Value)).ToList();
            var have = Reqs(item);
            // a tier 11+ result re-derives its wield gates from the tier, so only the lower tiers are compared slot by slot
            if (back.Tier < 11)
                foreach (var need in Reqs(main).Concat(Reqs(feeder)))
                    if (!have.Any(h => h.Item1 == need.Item1 && h.Item2 == need.Item2 && h.Item3 >= need.Item3))
                        problems.Add($"requirement lost: kind {need.Item1} skill {need.Item2} {need.Item3}");
            if ((item.GetProperty(PropertyInt.ItemDifficulty) ?? 0) < Math.Max(main.GetProperty(PropertyInt.ItemDifficulty) ?? 0, feeder.GetProperty(PropertyInt.ItemDifficulty) ?? 0))
                problems.Add("arcane lore requirement lowered");

            if (ForgeWeaponReader.RefusalReason(item) != null) problems.Add("the result cannot be forged again: " + ForgeWeaponReader.RefusalReason(item));
            return problems;
        }

        private static void AssertNone(List<string> failures, int ran)
        {
            Assert.IsTrue(ran > 0, "nothing was tested");
            Assert.AreEqual(0, failures.Count, $"{failures.Count} of {ran} failed. First: {string.Join(" || ", failures.Take(5))}");
        }

        // ---------------------------------------------------------------- tests

        [TestMethod]
        public void Groups_AreTheApprovedMatrix()
        {
            Need();
            // spot checks from every part of the matrix the owner approved on 2026-09-30
            Assert.AreEqual(ForgeGroups.GetGroup(3879), ForgeGroups.GetGroup(30573), "Flaming Broad Sword and Frost Spada: Light Weapons sword");
            Assert.AreEqual("Light Weapons sword", ForgeGroups.GetGroup(3879));
            Assert.AreEqual("Finesse Weapons axe", ForgeGroups.GetGroup(3858), "Lightning Shou-ono");
            Assert.AreEqual("Two Handed Combat axe", ForgeGroups.GetGroup(41052), "Greataxe");
            Assert.AreEqual("Two Handed Combat spear", ForgeGroups.GetGroup(41041), "Magari Yari");
            Assert.AreEqual("Two Handed Combat mace", ForgeGroups.GetGroup(41057), "Great Star Mace");
            Assert.AreNotEqual(ForgeGroups.GetGroup(3879), ForgeGroups.GetGroup(3858), "a sword and an axe never share a group");
            Assert.AreEqual("Nether caster", ForgeGroups.GetGroup(43381), "Nether Sceptre");
            Assert.AreEqual("War caster", ForgeGroups.GetGroup(29259), "Acid Sceptre");
            Assert.IsNull(ForgeGroups.GetGroup(24100), "Sword of Frozen Fury is a quest weapon: no group");
            Assert.IsNull(ForgeGroups.GetGroup(6171), "Peerless Atlan Claw is in no loot table");
        }

        [TestMethod]
        public void ElementVariant_IsTheSameModelInTheOtherElement_OrNothing()
        {
            Need();
            Assert.AreEqual(3880u, ForgeGroups.FindElementVariant(3879, DamageType.Cold), "Flaming Broad Sword in cold is the Frost Broad Sword");
            Assert.AreEqual(3879u, ForgeGroups.FindElementVariant(3879, DamageType.Fire), "already that element: itself");
            Assert.IsNull(ForgeGroups.FindElementVariant(30556, DamageType.Nether), "a Hatchet has no nether version");
        }

        [TestMethod]
        public void TinkerReversal_OnRealTinkeredWeapons_ReversesOrRefuses_NeverThrows()
        {
            Need();
            var ids = IdsWithInt((int)PropertyInt.NumTimesTinkered, 1, 4000);
            int checkedCount = 0, ok = 0;
            var unexpected = new List<string>();
            foreach (var id in ids)
            {
                if (checkedCount >= 300) break;
                var wo = Load(id);
                if (wo == null || !ForgeWeaponReader.IsLootGenerated(wo)) continue;
                checkedCount++;
                var r = TinkerReversal.Strip(wo);   // verifies itself by replaying the game's own tinker scripts
                if (r.Status == TinkerReversal.Status.Ok) ok++;
                else if (r.Status == TinkerReversal.Status.UnknownEntry)
                    unexpected.Add($"{wo.Name} 0x{id:X8}: {r.Detail}");
            }
            Assert.IsTrue(checkedCount >= 50, $"only {checkedCount} tinkered loot weapons found");
            Assert.AreEqual(0, unexpected.Count, "tinker log entries with no script: " + string.Join(" || ", unexpected.Take(5)));
            Assert.IsTrue(ok >= checkedCount * 0.7, $"only {ok} of {checkedCount} reversed; expected about 87%");
        }

        [TestMethod]
        public void Forge_RealPairs_BuildExactlyWhatTheForgeDecided_InEveryRollMode()
        {
            Need();
            var rng = new Random(20261001);
            var buckets = new Dictionary<string, List<WorldObject>>();
            foreach (var id in IdsWithInt((int)PropertyInt.ItemWorkmanship, 1, 20000).OrderBy(_ => rng.Next()).Take(900))
            {
                var wo = Load(id);
                if (wo == null || ForgeWeaponReader.RefusalReason(wo) != null) continue;
                var g = ForgeGroups.GetGroup(wo.WeenieClassId);
                if (!buckets.TryGetValue(g, out var list)) buckets[g] = list = new List<WorldObject>();
                list.Add(wo);
            }
            var groups = buckets.Values.Where(l => l.Count >= 2).ToList();
            Assert.IsTrue(groups.Count >= 5, $"only {groups.Count} groups with two or more sampled weapons");

            var failures = new List<string>();
            var ran = 0;
            var modes = new[] { ForgeMath.RollMode.Pick, ForgeMath.RollMode.Between, ForgeMath.RollMode.BestOfTwo };
            for (var i = 0; i < 300; i++)
            {
                var list = groups[rng.Next(groups.Count)];
                var main = list[rng.Next(list.Count)];
                var feeder = list[rng.Next(list.Count)];
                if (main == feeder) continue;
                ran++;
                var problems = ForgeAndCheck(main, feeder, modes[i % modes.Length], rng, 0.25);
                if (problems.Count > 0)
                    failures.Add($"[{modes[i % modes.Length]}] {main.Name} + {feeder.Name}: {string.Join("; ", problems)}");
            }
            AssertNone(failures, ran);
            Assert.IsTrue(forgedWithSpells >= ran / 4, $"only {forgedWithSpells} of {ran} forged weapons carried a spell: spells are being dropped");
        }

        [TestMethod]
        public void Reader_SeesTheSpellsOnRealWeapons()
        {
            Need();
            int withBook = 0, read = 0, collided = 0;
            foreach (var wo in SampleForgeable(41, 600, 200))
            {
                var known = wo.Biota.GetKnownSpellsIds(wo.BiotaDatabaseLock);
                if (known.Count == 0) continue;
                withBook++;
                var state = ForgeWeaponReader.Read(wo);
                if (state.Spells.Count > 0) read++;
                if (state.Spells.Count < known.Count) collided++;
                foreach (var entry in state.Spells.Values)
                    Assert.IsTrue(known.Contains((int)entry.SpellId), $"{wo.Name}: read a spell it does not have");
            }
            Console.WriteLine($"{withBook} weapons with spells; all read: {read}; with two spells in one family (the lower is dropped): {collided}");
            Assert.IsTrue(withBook >= 20, $"only {withBook} sampled weapons carry spells");
            Assert.AreEqual(withBook, read, "a weapon with spells read as having none");
        }

        [TestMethod]
        public void Forge_Tier11Weapons_KeepQualityGradesAndTier()
        {
            Need();
            var t11 = new List<WorldObject>();
            foreach (var id in IdsWithInt((int)PropertyInt.WeaponAugScaleTier, 11, 500).Concat(IdsWithInt((int)PropertyInt.ZcTier, 11, 500)).Distinct())
            {
                var wo = Load(id);
                if (wo != null && ForgeWeaponReader.RefusalReason(wo) == null)
                    t11.Add(wo);
            }
            if (t11.Count == 0)
                Assert.Inconclusive("no forgeable tier 11+ weapon in this shard database");

            // a lower-tier partner for each group, so cross-tier forges are covered in both roles
            var wanted = t11.Select(w => ForgeGroups.GetGroup(w.WeenieClassId)).ToHashSet();
            var partners = new Dictionary<string, WorldObject>();
            foreach (var id in IdsWithInt((int)PropertyInt.ItemWorkmanship, 1, 20000))
            {
                if (partners.Count == wanted.Count) break;
                var wo = Load(id);
                if (wo == null) continue;
                var g = ForgeGroups.GetGroup(wo.WeenieClassId);
                if (g == null || !wanted.Contains(g) || partners.ContainsKey(g) || ZoneStatResolver.TierOf(wo) >= 11 || ForgeWeaponReader.RefusalReason(wo) != null) continue;
                partners[g] = wo;
            }

            var rng = new Random(11);
            var failures = new List<string>();
            var ran = 0;
            void Run(WorldObject main, WorldObject feeder)
            {
                foreach (var mode in new[] { ForgeMath.RollMode.Pick, ForgeMath.RollMode.Between })
                {
                    ran++;
                    var problems = ForgeAndCheck(main, feeder, mode, rng, 0.25);
                    if (problems.Count > 0)
                        failures.Add($"[{mode}] {main.Name} + {feeder.Name}: {string.Join("; ", problems)}");
                }
            }
            foreach (var a in t11)
            {
                var g = ForgeGroups.GetGroup(a.WeenieClassId);
                foreach (var b in t11.Where(x => x != a && ForgeGroups.GetGroup(x.WeenieClassId) == g))
                    Run(a, b);
                if (partners.TryGetValue(g, out var low))
                {
                    Run(a, low);
                    Run(low, a);
                }
            }
            AssertNone(failures, ran);
        }

        // ---------------------------------------------------------------- honing, dyes, unbinding, smithy weenies

        private static double? Stat(WorldObject wo, (StatType Type, int Idx) key)
            => key.Type == StatType.Int ? wo.GetProperty((PropertyInt)key.Idx) : wo.GetProperty((PropertyFloat)key.Idx);

        /// <summary>A random sample of real weapons the forge accepts (tinkered ones included).</summary>
        private static List<WorldObject> SampleForgeable(int seed, int scan, int want)
        {
            var rng = new Random(seed);
            var result = new List<WorldObject>();
            var ids = IdsWithInt((int)PropertyInt.NumTimesTinkered, 1, 2000).Concat(IdsWithInt((int)PropertyInt.ItemWorkmanship, 1, 20000))
                .Distinct().OrderBy(_ => rng.Next()).Take(scan);
            foreach (var id in ids)
            {
                if (result.Count >= want) break;
                var wo = Load(id);
                if (wo != null && ForgeWeaponReader.RefusalReason(wo) == null)
                    result.Add(wo);
            }
            return result;
        }

        [TestMethod]
        public void Hone_RealWeapons_EveryLine_AppliesExactly_AndComesOffExactly()
        {
            Need();
            var config = ForgeMath.ForgeConfig.FromServerConfig();
            var failures = new List<string>();
            int ran = 0, tinkered = 0;
            foreach (var wo in SampleForgeable(77, 700, 150))
            {
                var before = TinkerReversal.ForgeStats.ToDictionary(k => k, k => Stat(wo, k));
                var state = ForgeWeaponReader.Read(wo);
                if (state.TinkerCount > 0) tinkered++;
                foreach (var line in ForgeMath.HonableLines.Where(state.Lines.ContainsKey))
                {
                    ran++;
                    var tag = $"{wo.Name} 0x{wo.Guid.Full:X8} {line}";
                    var prop = ForgeWeaponWriter.LineProperty(line, wo is Caster);
                    var tol = prop.Type == StatType.Int ? 0.5 : 1e-6;
                    foreach (var levels in new[] { 1, 4, 2 })
                    {
                        var error = ForgeWeaponWriter.ApplyHone(wo, line, levels, config);
                        if (error != null) { failures.Add($"{tag} +{levels}: {error}"); continue; }
                        var back = ForgeWeaponReader.Read(wo);
                        if (back.HoneLevel(line) != levels || back.HoneTotal != levels) failures.Add($"{tag} +{levels}: reads +{back.HoneLevel(line)} of {back.HoneTotal}");
                        if (!back.Lines.TryGetValue(line, out var baseNow) || Math.Abs(baseNow - state.Lines[line]) > 1e-6) failures.Add($"{tag} +{levels}: base roll moved {state.Lines[line]} -> {baseNow}");
                        var stripped = TinkerReversal.Strip(wo);
                        if (stripped.Status != TinkerReversal.Status.Ok) { failures.Add($"{tag} +{levels}: tinkers no longer reverse"); continue; }
                        var want = ForgeMath.HonedValue(line, state.Lines[line], levels, config);
                        var got = stripped.Base[prop] ?? double.NaN;
                        if (!(Math.Abs(got - want) <= tol)) failures.Add($"{tag} +{levels}: untinkered value {got}, expected {want}");
                        if (back.TinkerCount != state.TinkerCount || back.TinkerLog != state.TinkerLog) failures.Add($"{tag} +{levels}: tinkers changed");
                        // honing never makes the line worse
                        var worse = ForgeMath.LowerIsBetter(line) ? got > state.Lines[line] + 1e-9 : got < state.Lines[line] - 1e-9;
                        if (worse) failures.Add($"{tag} +{levels}: got worse ({state.Lines[line]} -> {got})");
                    }

                    // all hone levels off again: every forge stat exactly as it was
                    var off = ForgeWeaponWriter.ApplyHone(wo, line, 0, config);
                    if (off != null) { failures.Add($"{tag} off: {off}"); continue; }
                    if (wo.GetProperty(PropertyString.ForgeHoneLevels) != null) failures.Add($"{tag}: hone record left behind");
                    foreach (var (key, value) in before)
                    {
                        var now = Stat(wo, key);
                        if (now.HasValue != value.HasValue || (value.HasValue && Math.Abs(now.Value - value.Value) > 1e-6))
                            failures.Add($"{tag}: {key.Type} {key.Idx} was {value?.ToString() ?? "none"}, now {now?.ToString() ?? "none"}");
                    }
                }
            }
            Console.WriteLine($"hone round trips: {ran} lines, on a sample with {tinkered} tinkered weapons");
            Assert.IsTrue(tinkered >= 10, $"only {tinkered} tinkered weapons in the sample");
            AssertNone(failures, ran);
        }

        [TestMethod]
        public void Hone_ThenForge_CarriesTheMainWeaponsLevels()
        {
            Need();
            var config = ForgeMath.ForgeConfig.FromServerConfig();
            var rng = new Random(5);
            var failures = new List<string>();
            var ran = 0;
            foreach (var group in SampleForgeable(91, 900, 200).GroupBy(w => ForgeGroups.GetGroup(w.WeenieClassId)).Where(g => g.Count() >= 2))
            {
                var pair = group.Take(2).ToList();
                var state = ForgeWeaponReader.Read(pair[0]);
                var lines = ForgeMath.HonableLines.Where(state.Lines.ContainsKey).Take(2).ToList();
                if (lines.Count == 0) continue;
                var honed = true;
                for (var i = 0; i < lines.Count; i++)
                    honed &= ForgeWeaponWriter.ApplyHone(pair[0], lines[i], i + 2, config) == null;
                if (!honed) { failures.Add($"{pair[0].Name}: could not be honed"); continue; }
                ran++;
                // ForgeAndCheck compares every hone level of the result against what the forge decided
                var problems = ForgeAndCheck(pair[0], pair[1], ForgeMath.RollMode.Pick, rng, 0);
                if (problems.Count > 0)
                    failures.Add($"{pair[0].Name} + {pair[1].Name}: {string.Join("; ", problems)}");
            }
            AssertNone(failures, ran);
        }

        [TestMethod]
        public void Dyes_EveryLootWeaponModel_HasColourFamilies_AndRollsInsideThem()
        {
            Need();
            var vibrant = ACE.Server.Services.PetMutationService.GetVibrantPalettePool().Select(p => p.PaletteId).ToHashSet();
            var setups = SampleForgeable(3, 600, 200).Select(w => w.SetupTableId).Distinct().ToList();
            var failures = new List<string>();
            var perFamily = new Dictionary<ForgeDyes.Family, int>();
            foreach (var setup in setups)
            {
                var families = ForgeDyes.ForSetup(setup);
                var all = families.Values.SelectMany(v => v).ToList();
                if (all.Count != all.Distinct().Count()) failures.Add($"0x{setup:X8}: a palette sits in two families");
                if (all.Any(p => !vibrant.Contains(p))) failures.Add($"0x{setup:X8}: a palette outside the vibrant pool");
                if (families.ContainsKey(ForgeDyes.Family.Any)) failures.Add($"0x{setup:X8}: 'Any' is not a family");
                if (ForgeDyes.Roll(setup, ForgeDyes.Family.Any, 0) == null || ForgeDyes.Roll(setup, ForgeDyes.Family.Any, 0.999999) == null)
                    failures.Add($"0x{setup:X8}: the any-colour dye rolled nothing");
                foreach (ForgeDyes.Family family in Enum.GetValues(typeof(ForgeDyes.Family)))
                {
                    if (family == ForgeDyes.Family.Any) continue;
                    var has = families.TryGetValue(family, out var pool) && pool.Count > 0;
                    if (has) perFamily[family] = (perFamily.TryGetValue(family, out var n) ? n : 0) + 1;
                    foreach (var roll in new[] { 0.0, 0.5, 0.999999 })
                    {
                        var picked = ForgeDyes.Roll(setup, family, roll);
                        if (has && (picked == null || !pool.Contains(picked.Value))) failures.Add($"0x{setup:X8} {family}: rolled outside its family");
                        if (!has && picked != null) failures.Add($"0x{setup:X8} {family}: rolled from an empty family");
                    }
                }
            }
            Console.WriteLine($"dye families over {setups.Count} weapon models: " + string.Join(", ", perFamily.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}")));
            AssertNone(failures, setups.Count);
        }

        [TestMethod]
        public void Hone_Tier11Weapons_SurvivesTheZoneControlReResolve()
        {
            Need();
            var config = ForgeMath.ForgeConfig.FromServerConfig();
            var failures = new List<string>();
            var ran = 0;
            var owned = new SortedSet<string>();
            foreach (var id in IdsWithInt((int)PropertyInt.WeaponAugScaleTier, 11, 500).Concat(IdsWithInt((int)PropertyInt.ZcTier, 11, 500)).Distinct())
            {
                var wo = Load(id);
                if (wo == null || ForgeWeaponReader.RefusalReason(wo) != null) continue;
                var state = ForgeWeaponReader.Read(wo);
                var resolved = ZoneStatResolver.Compute(wo);
                foreach (var line in ForgeMath.HonableLines.Where(state.Lines.ContainsKey))
                {
                    var prop = ForgeWeaponWriter.LineProperty(line, wo is Caster);
                    // a stat Zone Control writes itself would be reset on the next re-resolve
                    var zcOwns = resolved != null && (prop.Type == StatType.Int ? resolved.Ints.ContainsKey((PropertyInt)prop.Idx) : resolved.Floats.ContainsKey((PropertyFloat)prop.Idx));
                    if (zcOwns) owned.Add(line.ToString());

                    if (ForgeWeaponWriter.ApplyHone(wo, line, 3, config) != null) { failures.Add($"{wo.Name} {line}: could not hone"); continue; }
                    ran++;
                    var honed = Stat(wo, prop);
                    if (resolved != null)
                        ZoneStatResolver.Apply(wo, ZoneStatResolver.Compute(wo), true);
                    var after = Stat(wo, prop);
                    if (honed != after) failures.Add($"{wo.Name} {line}: honed {honed}, after re-resolve {after}");
                    if (ForgeWeaponReader.Read(wo).HoneLevel(line) != 3) failures.Add($"{wo.Name} {line}: hone level lost");
                    Console.WriteLine($"{wo.Name} (quality {state.Quality}) {line}: {state.Lines[line]} -> {honed} at +3");
                    ForgeWeaponWriter.ApplyHone(wo, line, 0, config);
                }
            }
            Console.WriteLine("lines Zone Control also writes: " + (owned.Count == 0 ? "none" : string.Join(", ", owned)));
            if (ran == 0) Assert.Inconclusive("no forgeable tier 11+ weapon in this shard database");
            AssertNone(failures, ran);
        }

        [TestMethod]
        public void Hone_Tier11Weapons_ScalesTheQualityDrivenDamage()
        {
            Need();
            var config = ForgeMath.ForgeConfig.FromServerConfig();
            var failures = new List<string>();
            int mods = 0, flats = 0, variances = 0;
            foreach (var id in IdsWithInt((int)PropertyInt.WeaponAugScaleTier, 11, 500))
            {
                var wo = Load(id);
                if (wo == null) continue;
                var combat = typeof(ACE.Server.Managers.WeaponScaling.WeaponScalingCombat);
                // a hone record written directly, so the admin-made test weapons (which the forge refuses) count too
                void Hone(string record) { if (record == null) wo.RemoveProperty(PropertyString.ForgeHoneLevels); else wo.SetProperty(PropertyString.ForgeHoneLevels, record); }

                if (wo is MissileLauncher || wo is Caster)
                {
                    bool Mod(out float m) => wo is Caster
                        ? ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.TryGetCasterElementalMod(wo, null, out m)
                        : ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.TryGetLauncherDamageMod(wo, null, out m);
                    Hone(null);
                    if (!Mod(out var before)) continue;
                    Hone($"{(int)ForgeMath.ForgeLine.DamageMod}:3:0");
                    Mod(out var after);
                    mods++;
                    var want = before * (1 + 3 * config.StepFor(ForgeMath.ForgeLine.DamageMod));
                    if (Math.Abs(after - want) > 1e-4) failures.Add($"{wo.Name}: modifier {before} -> {after}, expected {want}");
                    if (!ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.HoneScalesQuality(wo, ForgeMath.ForgeLine.DamageMod)) failures.Add($"{wo.Name}: damage modifier hone not recognised");
                    if (ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.HoneScalesQuality(wo, ForgeMath.ForgeLine.MaxDamage)) failures.Add($"{wo.Name}: a damage hone does nothing on a launcher or caster");
                    Console.WriteLine($"{wo.Name}: quality modifier {before:0.###} -> {after:0.###} at +3");
                }
                else
                {
                    Hone(null);
                    var before = ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.GetFloorBonus(wo);
                    if (before <= 0) continue;
                    var hadVariance = ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.TryGetEffectiveVariance(wo, out var v0);
                    Hone($"{(int)ForgeMath.ForgeLine.MaxDamage}:5:0;{(int)ForgeMath.ForgeLine.Variance}:2:0");
                    var after = ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.GetFloorBonus(wo);
                    flats++;
                    var want = before * (1 + 5 * config.StepFor(ForgeMath.ForgeLine.MaxDamage));
                    if (Math.Abs(after - want) > Math.Max(1e-3, want * 1e-5)) failures.Add($"{wo.Name}: quality damage {before} -> {after}, expected {want}");
                    if (hadVariance)
                    {
                        ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.TryGetEffectiveVariance(wo, out var v1);
                        variances++;
                        var wantV = v0 * (1 - 2 * config.StepFor(ForgeMath.ForgeLine.Variance));
                        if (Math.Abs(v1 - wantV) > 1e-9) failures.Add($"{wo.Name}: variance {v0} -> {v1}, expected {wantV}");
                    }
                    Console.WriteLine($"{wo.Name}: quality damage at the wield floor {before:0.#} -> {after:0.#} at +5{(hadVariance ? $", variance {v0:0.###} at +2 tighter" : "")}");
                }
                Hone(null);
            }
            if (mods + flats == 0) Assert.Inconclusive("no quality-scaled tier 11+ weapon resolved (weapon scaling off, or none in this shard)");
            Console.WriteLine($"checked {mods} launchers/casters, {flats} melee ({variances} with variance)");
            AssertNone(failures, mods + flats);
        }

        // ---------------------------------------------------------------- armour

        private static WorldObject LoadAny(uint biotaId)
            => WorldObjectFactory.CreateWorldObject(DatabaseManager.Shard.BaseDatabase.GetBiota(biotaId, true));

        [TestMethod]
        public void ArmorGroups_AreCoverageAndClass()
        {
            Need();
            WorldObject A(uint wcid) => NewScratch(wcid);
            string G(uint wcid) => ForgeGroups.GetGroup(A(wcid));
            var yoroiBreastplate = (uint)ACE.Server.Factories.Enum.WeenieClassName.breastplateyoroi;
            var celdonBreastplate = (uint)ACE.Server.Factories.Enum.WeenieClassName.breastplateceldon;
            var covenantBreastplate = (uint)ACE.Server.Factories.Enum.WeenieClassName.breastplatecovenant;
            var olthoiBreastplate = (uint)ACE.Server.Factories.Enum.WeenieClassName.ace37216_olthoibreastplate;
            var platemailHauberk = (uint)ACE.Server.Factories.Enum.WeenieClassName.hauberkplatemail;
            Assert.IsNotNull(G(yoroiBreastplate));
            Assert.AreEqual(G(yoroiBreastplate), G(celdonBreastplate), "armour type does not matter");
            Assert.AreNotEqual(G(yoroiBreastplate), G(covenantBreastplate), "Covenant stands alone");
            Assert.AreNotEqual(G(yoroiBreastplate), G(olthoiBreastplate), "Olthoi stands alone");
            Assert.AreNotEqual(G(covenantBreastplate), G(olthoiBreastplate), "and they do not mix with each other");
            Assert.AreNotEqual(G(yoroiBreastplate), G(platemailHauberk), "a breastplate and a hauberk cover different slots");

            // a tailored piece counts as what it covers now
            var reduced = A(platemailHauberk);
            reduced.ValidLocations = A(yoroiBreastplate).ValidLocations;
            Assert.AreEqual(G(yoroiBreastplate), ForgeGroups.GetGroup(reduced), "a hauberk reduced to the chest forges with breastplates");
            Console.WriteLine($"{G(yoroiBreastplate)} | {G(covenantBreastplate)} | {G(platemailHauberk)}");
        }

        [TestMethod]
        public void Armor_TinkerReversal_OnRealTinkeredArmor_ReversesOrRefuses()
        {
            Need();
            var rng = new Random(12);
            int checkedCount = 0, ok = 0;
            var byStatus = new Dictionary<TinkerReversal.Status, int>();
            var detail = new Dictionary<string, int>();
            foreach (var id in IdsWithInt((int)PropertyInt.NumTimesTinkered, 1, 60000).OrderBy(_ => rng.Next()))
            {
                if (checkedCount >= 400) break;
                var wo = LoadAny(id);
                if (wo == null || !ForgeWeaponReader.IsArmorKind(wo) || !ForgeWeaponReader.IsLootGenerated(wo) || ForgeGroups.GetGroup(wo) == null) continue;
                checkedCount++;
                var r = TinkerReversal.Strip(wo);
                byStatus[r.Status] = (byStatus.TryGetValue(r.Status, out var n) ? n : 0) + 1;
                if (r.Status == TinkerReversal.Status.Ok) ok++;
                else
                {
                    var key = r.Status + ": " + System.Text.RegularExpressions.Regex.Replace(r.Detail ?? "", @"\d+(\.\d+)?", "#");
                    detail[key] = (detail.TryGetValue(key, out var d) ? d : 0) + 1;
                }
            }
            Console.WriteLine($"tinkered loot armour checked: {checkedCount}; " + string.Join(", ", byStatus.Select(kv => $"{kv.Key} {kv.Value}")));
            foreach (var kv in detail.OrderByDescending(kv => kv.Value).Take(12)) Console.WriteLine($"  {kv.Value}x {kv.Key}");
            Assert.IsTrue(checkedCount >= 100, $"only {checkedCount} tinkered loot armour pieces found");
            Assert.IsTrue(ok >= checkedCount * 0.5, $"only {ok} of {checkedCount} reversed");
        }

        [TestMethod]
        public void Armor_RealPairs_BuildExactlyWhatTheForgeDecided()
        {
            Need();
            var rng = new Random(20261004);
            var buckets = new Dictionary<string, List<WorldObject>>();
            int loaded = 0, refused = 0;
            foreach (var id in IdsWithInt((int)PropertyInt.ArmorLevel, 1, 60000).OrderBy(_ => rng.Next()).Take(2500))
            {
                var wo = LoadAny(id);
                if (wo == null || !ForgeWeaponReader.IsArmorKind(wo)) continue;
                loaded++;
                if (ForgeWeaponReader.RefusalReason(wo) != null) { refused++; continue; }
                var g = ForgeGroups.GetGroup(wo);
                if (!buckets.TryGetValue(g, out var list)) buckets[g] = list = new List<WorldObject>();
                list.Add(wo);
            }
            var groups = buckets.Where(kv => kv.Value.Count >= 2).ToList();
            Console.WriteLine($"armour loaded {loaded}, refused {refused}, groups with pairs {groups.Count}: " + string.Join(" | ", groups.OrderByDescending(g => g.Value.Count).Select(g => $"{g.Key} ({g.Value.Count})")));
            Assert.IsTrue(groups.Count >= 8, $"only {groups.Count} armour groups with two or more sampled pieces");

            var failures = new List<string>();
            var ran = 0;
            var modes = new[] { ForgeMath.RollMode.Between, ForgeMath.RollMode.Pick, ForgeMath.RollMode.BestOfTwo };
            for (var i = 0; i < 400; i++)
            {
                var list = groups[rng.Next(groups.Count)].Value;
                var main = list[rng.Next(list.Count)];
                var feeder = list[rng.Next(list.Count)];
                if (main == feeder) continue;
                ran++;
                var problems = ForgeAndCheck(main, feeder, modes[i % modes.Length], rng, 0.25);
                // armour-specific: same coverage and look as the main piece, and never a hone (nothing on armour is honable)
                if (problems.Count > 0)
                    failures.Add($"[{modes[i % modes.Length]}] {main.Name} + {feeder.Name}: {string.Join("; ", problems)}");
            }
            AssertNone(failures, ran);
        }

        [TestMethod]
        public void DyeBottles_DrawTheIconOfTheirColourOption_UnlessToldToKeepTheirOwn()
        {
            Need();
            // The SQL builds every dye from the retail dye vial (8643) and picks its bottle by PaletteTemplate. This is the
            // table that file relies on, checked against the server's own rendering: template -> the icon a player sees.
            var bottles = new Dictionary<int, uint>
            {
                { 90, 0x06001DED }, { 14, 0x06001DE6 }, { 17, 0x06001DE7 }, { 8, 0x06001DE8 },
                { 77, 0x06001DEE }, { 2, 0x06001DE9 }, { 13, 0x06001DEB }, { 9, 0x06001DEC },
            };
            foreach (var (template, icon) in bottles)
            {
                var vial = NewScratch(8643);
                vial.PaletteTemplate = template;
                vial.CalculateObjDesc();
                Assert.AreEqual(icon, vial.IconId, $"template {template} draws icon 0x{vial.IconId:X8}, the SQL expects 0x{icon:X8}");
            }
            Assert.AreEqual(bottles.Count, bottles.Values.Distinct().Count(), "eight different bottles");

            // Amber and Rose have no bottle of their colour: IgnoreCloIcons keeps the icon the weenie stores.
            var own = NewScratch(8643);
            own.PaletteTemplate = 4;
            own.IconId = 0x060061C1;
            own.SetProperty(PropertyBool.IgnoreCloIcons, true);
            own.CalculateObjDesc();
            Assert.AreEqual(0x060061C1u, own.IconId, "with IgnoreCloIcons the stored icon is drawn");

            var replaced = NewScratch(8643);
            replaced.PaletteTemplate = 4;
            replaced.IconId = 0x060061C1;
            replaced.CalculateObjDesc();
            Assert.AreNotEqual(0x060061C1u, replaced.IconId, "without it the bottle of the colour option replaces the stored icon");
        }

        [TestMethod]
        public void Unbind_ThenWield_BindsAgain()
        {
            Need();
            var item = NewScratch(3879);
            ForgeWeaponWriter.Bind(item);
            Assert.AreEqual((int)AttunedStatus.Attuned, item.GetProperty(PropertyInt.Attuned));
            Assert.AreEqual((int)BondedStatus.Bonded, item.GetProperty(PropertyInt.Bonded));

            // what the unbinding oil leaves behind
            item.RemoveProperty(PropertyInt.Attuned);
            item.RemoveProperty(PropertyInt.Bonded);
            item.SetProperty(PropertyBool.ForgeRebindOnWield, true);

            ForgeService.OnWield(item, null);
            Assert.AreEqual((int)AttunedStatus.Attuned, item.GetProperty(PropertyInt.Attuned), "wielding binds it again");
            Assert.AreEqual((int)BondedStatus.Bonded, item.GetProperty(PropertyInt.Bonded));
            Assert.IsNull(item.GetProperty(PropertyBool.ForgeRebindOnWield), "the flag is spent");

            // an ordinary weapon is never bound by being wielded
            var plain = NewScratch(3879);
            ForgeService.OnWield(plain, null);
            Assert.IsNull(plain.GetProperty(PropertyInt.Attuned));
        }

        [TestMethod]
        public void Smithy_Weenies_AreInTheWorldDatabase_AndWiredToTheCode()
        {
            Need();
            if (DatabaseManager.World.GetCachedWeenie(78780410) == null)
                Assert.Inconclusive("the smithy SQL (Database/Updates/World/2026-10-03-00-Forge-Smithy.sql) has not been run on this world database");

            ACE.Entity.Models.Weenie W(uint id)
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(id);
                Assert.IsNotNull(weenie, $"weenie {id} is missing");
                return weenie;
            }
            int? I(uint id, PropertyInt p) => W(id).PropertiesInt != null && W(id).PropertiesInt.TryGetValue(p, out var v) ? v : null;
            bool? B(uint id, PropertyBool p) => W(id).PropertiesBool != null && W(id).PropertiesBool.TryGetValue(p, out var v) ? v : null;

            Assert.AreEqual(true, B(78780400, PropertyBool.ForgeSmith), "the smith (2026-10-02-00-Forge-Smith-NPC.sql)");
            Assert.AreEqual(true, B(78780401, PropertyBool.ForgeGrindstone));
            Assert.AreEqual(true, B(78780402, PropertyBool.ForgeDyeVat));

            var tools = new List<uint>();
            for (uint i = 0; i < 8; i++)
            {
                var id = 78780410 + i;
                tools.Add(id);
                Assert.AreEqual((int)ForgeService.ForgeTool.HoneStone, I(id, PropertyInt.ForgeTool));
                Assert.AreEqual((int)(i + 1), I(id, PropertyInt.ForgeToolArg));
                Assert.IsTrue(ForgeMath.IsHonable((ForgeMath.ForgeLine)(i + 1)), $"stone {id} hones a line that cannot be honed");
            }
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 8).ToList(), ForgeMath.HonableLines.Select(l => (int)l).ToList(), "one stone for every honable line");
            Assert.AreEqual((int)ForgeService.ForgeTool.Flux, I(78780418, PropertyInt.ForgeTool));
            Assert.AreEqual((int)ForgeService.ForgeTool.UnbindingOil, I(78780419, PropertyInt.ForgeTool));
            tools.Add(78780418); tools.Add(78780419);
            foreach (ForgeDyes.Family family in Enum.GetValues(typeof(ForgeDyes.Family)))
            {
                var id = 78780420 + (uint)family;
                tools.Add(id);
                Assert.AreEqual((int)ForgeService.ForgeTool.Dye, I(id, PropertyInt.ForgeTool));
                Assert.AreEqual((int)family, I(id, PropertyInt.ForgeToolArg));
            }

            foreach (var id in tools)
            {
                Assert.AreEqual(false, B(id, PropertyBool.IsSellable), $"{id} must not be sellable to vendors");
                Assert.AreEqual(id >= 78780420 ? 33025 | 2 | 4 | 16 : id == 78780419 ? 33025 | 2 | 4 : 33025, I(id, PropertyInt.TargetType), $"{id} must target weapons (dyes: also armour, clothing and yourself)");
                Assert.IsTrue((I(id, PropertyInt.Value) ?? 0) > 0, $"{id} has no price");
                foreach (var text in W(id).PropertiesString.Values)
                    Assert.IsTrue(text.All(c => c >= 32 && c < 127), $"{id}: non-ASCII text '{text}'");
            }

            var shop = W(78780403).PropertiesCreateList.Where(c => c.DestinationType == DestinationType.Shop).Select(c => c.WeenieClassId).ToList();
            CollectionAssert.AreEquivalent(tools, shop, "the apprentice sells exactly the forge tools");
            Assert.AreEqual(0, I(78780403, PropertyInt.MerchandiseItemTypes), "the apprentice buys nothing");

            // the dyes-only vendor, for a server that opens the dye vats before the forge
            var dyerShop = W(78780404).PropertiesCreateList.Where(c => c.DestinationType == DestinationType.Shop).Select(c => c.WeenieClassId).ToList();
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 10).Select(n => 78780420u + (uint)n).ToList(), dyerShop, "the dyer sells exactly the ten dyes");
            Assert.AreEqual(0, I(78780404, PropertyInt.MerchandiseItemTypes), "the dyer buys nothing");

            // A dye is drawn with the bottle of its colour option unless it carries IgnoreCloIcons, so the icon a player
            // sees must be the one stored: no dye may show a bottle of some other colour.
            foreach (ForgeDyes.Family family in Enum.GetValues(typeof(ForgeDyes.Family)))
            {
                var id = 78780420 + (uint)family;
                var dye = NewScratch(id);
                var stored = W(id).PropertiesDID[PropertyDataId.Icon];
                dye.CalculateObjDesc();
                Assert.AreEqual(stored, dye.IconId, $"{dye.Name} ({id}): the server draws icon 0x{dye.IconId:X8}, not its own 0x{stored:X8}");
            }
            Assert.AreEqual(10, tools.Skip(10).Select(id => W(id).PropertiesDID[PropertyDataId.Icon]).Distinct().Count(), "every dye has its own icon");
        }
    }
}
