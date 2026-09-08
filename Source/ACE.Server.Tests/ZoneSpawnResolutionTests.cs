using System;
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Regression cover for the 2026-07-30 generator spawn-snapshot bug.
    ///
    /// THE BUG: <c>GeneratorProfile.Spawn()</c> called <c>ZoneSpawnScaler.ApplyToSpawn(creature)</c>
    /// BEFORE the <c>Spawn_*</c> handlers assigned the creature a Location. Zone resolution keys on
    /// <c>Location.LandblockId.Landblock</c>, so every generator-spawned mob resolved landblock 0, matched
    /// no zone, and silently received no spawn snapshot at all — attributes, max health/stamina/mana, crit
    /// resist ratings, prop stamps and appearance were ALL skipped. Placed (landblock_instance) mobs were
    /// unaffected, because WorldObjectFactory sets Location before calling the same method.
    ///
    /// It hid for months because the only stats Tou Tou authored were live per-hit ones (attack_skill,
    /// damage_rating, attack_damage, percent_hp_base), which resolve during combat when Location IS set.
    /// The first per-variation Default to author attributes exposed it immediately.
    ///
    /// WHY THIS IS A SOURCE TEST, which is otherwise a smell: the bug is a statement-ORDERING contract
    /// that C# cannot express and no unit test can reach here — constructing a live <c>Creature</c> calls
    /// <c>GenerateNewFace()</c>, which dereferences <c>DatManager.PortalDat</c> and NREs without the DAT
    /// files this test environment does not have (the same reason GetCellTest/GetLandblockTest fail).
    /// The runtime defence is the null-Location guard inside ApplyToSpawn, which now logs loudly instead
    /// of doing nothing; this test is the cheap belt-and-braces that pins the ordering itself.
    /// </summary>
    [TestClass]
    public class ZoneSpawnResolutionTests
    {
        private static string ReadServerSource(string relativePath)
        {
            // walk up from the test bin dir to the repo root
            var dir = AppContext.BaseDirectory;
            for (var i = 0; i < 8 && dir != null; i++)
            {
                var candidate = Path.Combine(dir, "Source", "ACE.Server", relativePath);
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate);
                dir = Path.GetDirectoryName(dir);
            }
            Assert.Inconclusive($"Could not locate Source/ACE.Server/{relativePath} from {AppContext.BaseDirectory}");
            return null;
        }

        [TestMethod]
        public void GeneratorSpawn_AssignsLocationBeforeApplyingTheZoneSnapshot()
        {
            var src = ReadServerSource(Path.Combine("Entity", "GeneratorProfile.cs"));

            // isolate the creature-scaling block inside Spawn()
            var applyIdx = src.IndexOf("ZoneSpawnScaler.ApplyToSpawn(creature)", StringComparison.Ordinal);
            Assert.IsTrue(applyIdx > 0, "ApplyToSpawn call not found in GeneratorProfile - has the spawn path moved?");

            // the guard block opens the `if (wo is Creature creature ...)` body
            var blockIdx = src.LastIndexOf("wo is Creature creature", applyIdx, StringComparison.Ordinal);
            Assert.IsTrue(blockIdx > 0, "creature-scaling block not found ahead of the ApplyToSpawn call");

            var block = src.Substring(blockIdx, applyIdx - blockIdx);

            Assert.IsTrue(
                Regex.IsMatch(block, @"wo\.Location\s*=\s*new\s+ACE\.Entity\.Position\(\s*Generator\.Location\s*\)"),
                "GeneratorProfile.Spawn must assign a provisional Location from the generator BEFORE calling " +
                "ZoneSpawnScaler.ApplyToSpawn. Without it the creature resolves landblock 0, matches no zone, " +
                "and silently receives NO spawn snapshot (attributes, vitals, prop stamps, appearance). " +
                "The Spawn_* handlers overwrite this Location immediately, so it costs nothing.");
        }

        [TestMethod]
        public void ApplyToSpawn_HasTheNullLocationGuard()
        {
            var src = ReadServerSource(Path.Combine("Managers", "ZoneControl", "ZoneSpawnScaler.cs"));

            var applyIdx = src.IndexOf("public static void ApplyToSpawn(", StringComparison.Ordinal);
            Assert.IsTrue(applyIdx > 0, "ApplyToSpawn not found in ZoneSpawnScaler");
            // bound the search to THIS method: a guard or a resolve in a later method must not satisfy the test
            var bodyEnd = src.IndexOf("\n        public static ", applyIdx + 1, StringComparison.Ordinal);
            if (bodyEnd < 0) bodyEnd = src.Length;
            var body = src.Substring(applyIdx, bodyEnd - applyIdx);

            // the guard must sit before the first resolve, or it guards nothing
            var resolveIdx = body.IndexOf("ResolveForCreature", StringComparison.Ordinal);
            var guardIdx = body.IndexOf("creature.Location == null", StringComparison.Ordinal);

            Assert.IsTrue(guardIdx > 0,
                "ApplyToSpawn must reject a null Location loudly - a silent no-op here is invisible for months.");
            Assert.IsTrue(resolveIdx < 0 || guardIdx < resolveIdx,
                "the null-Location guard must come BEFORE the first zone resolve, or it guards nothing.");
        }
    }
}
