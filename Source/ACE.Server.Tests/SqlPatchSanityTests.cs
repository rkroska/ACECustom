using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    [TestClass]
    public class SqlPatchSanityTests
    {
        // Patches older than this predate the ASCII/LF rule and are not re-validated.
        private const string EncodingRuleCutoff = "2026-07-01";

        private static string[] GetUpdatePatchFiles(string root, string subDir, bool applyCutoff)
        {
            var updatesDir = Path.Combine(root, "Database", "Updates", subDir);
            if (!Directory.Exists(updatesDir))
                return Array.Empty<string>();

            var files = Directory.GetFiles(updatesDir, "*.sql", SearchOption.AllDirectories);
            if (applyCutoff)
                files = files.Where(f => string.Compare(Path.GetFileName(f), EncodingRuleCutoff, StringComparison.Ordinal) >= 0).ToArray();
            return files;
        }

        [TestMethod]
        public void WorldAndShardUpdates_AreStrictAsciiAndPureLf()
        {
            var root = FindRepoRoot();
            Assert.IsNotNull(root, "Could not locate repository root directory.");

            var files = GetUpdatePatchFiles(root, "World", applyCutoff: true)
                .Concat(GetUpdatePatchFiles(root, "Shard", applyCutoff: true))
                .ToArray();
            Assert.IsTrue(files.Length > 0, "No SQL patch files found to validate.");

            foreach (var file in files)
            {
                var bytes = File.ReadAllBytes(file);
                var nonAsciiIndices = bytes.Select((b, i) => new { b, i }).Where(x => x.b >= 128).ToList();
                Assert.AreEqual(0, nonAsciiIndices.Count,
                    $"File {Path.GetFileName(file)} contains {nonAsciiIndices.Count} non-ASCII bytes. First at offset {nonAsciiIndices.FirstOrDefault()?.i}.");

                var hasCr = Array.IndexOf(bytes, (byte)'\r') != -1;
                Assert.IsFalse(hasCr, $"File {Path.GetFileName(file)} contains CRLF line endings. All SQL patches must use LF only.");
            }
        }

        [TestMethod]
        public void LandblockInstanceInserts_DoNotIncludeGeneratedLandblockColumn()
        {
            var root = FindRepoRoot();
            Assert.IsNotNull(root, "Could not locate repository root directory.");

            // `landblock` is a GENERATED column on landblock_instance (Database/Base/WorldBase.sql);
            // listing it in an INSERT column list fails on MySQL/MariaDB. The column list is the
            // first parenthesised group after the table name; `landblock` must not appear in it
            // as a bare column (`landblock_instance` itself and `obj_Cell_Id` are fine).
            var pattern = new Regex(@"INSERT\s+INTO\s+`?landblock_instance`?\s*\(([^)]*)\)", RegexOptions.IgnoreCase);
            var columnPattern = new Regex(@"(?<![\w_])`?landblock`?(?![\w_])", RegexOptions.IgnoreCase);

            var files = GetUpdatePatchFiles(root, "World", applyCutoff: false);
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                foreach (Match match in pattern.Matches(text))
                {
                    var columnList = match.Groups[1].Value;
                    Assert.IsFalse(columnPattern.IsMatch(columnList),
                        $"File {Path.GetFileName(file)} attempts to INSERT into generated column 'landblock' of landblock_instance. MySQL derives this automatically from obj_Cell_Id.");
                }
            }
        }

        [TestMethod]
        public void LandblockColumnCheck_MatchesOffendingInsert()
        {
            // Guard against the regex silently matching nothing (the previous version targeted a
            // `position` table that has no generated column, so it never fired).
            var pattern = new Regex(@"INSERT\s+INTO\s+`?landblock_instance`?\s*\(([^)]*)\)", RegexOptions.IgnoreCase);
            var columnPattern = new Regex(@"(?<![\w_])`?landblock`?(?![\w_])", RegexOptions.IgnoreCase);

            var bad = "INSERT INTO `landblock_instance` (`guid`, `landblock`, `weenie_Class_Id`, `obj_Cell_Id`) VALUES (1, 2, 3, 4);";
            var badMatch = pattern.Match(bad);
            Assert.IsTrue(badMatch.Success && columnPattern.IsMatch(badMatch.Groups[1].Value), "Offending INSERT was not detected.");

            var good = "INSERT INTO `landblock_instance` (`guid`, `weenie_Class_Id`, `obj_Cell_Id`) VALUES (1, 2, 3);";
            var goodMatch = pattern.Match(good);
            Assert.IsTrue(goodMatch.Success && !columnPattern.IsMatch(goodMatch.Groups[1].Value), "Valid INSERT was flagged.");
        }

        private static string FindRepoRoot()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "ACECustom.sln")) && !File.Exists(Path.Combine(dir, "Source", "ACE.sln")))
            {
                var parent = Directory.GetParent(dir);
                dir = parent?.FullName;
            }
            return dir;
        }
    }
}
