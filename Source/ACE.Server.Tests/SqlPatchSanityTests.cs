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
        [TestMethod]
        public void WorldUpdates_AreStrictAsciiAndPureLf()
        {
            var root = FindRepoRoot();
            Assert.IsNotNull(root, "Could not locate repository root directory.");

            var updatesDir = Path.Combine(root, "Database", "Updates", "World");
            if (!Directory.Exists(updatesDir))
                return;

            var allFiles = Directory.GetFiles(updatesDir, "*.sql", SearchOption.AllDirectories);
            var files = allFiles.Where(f => string.Compare(Path.GetFileName(f), "2026-07-01", StringComparison.Ordinal) >= 0).ToArray();
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
        public void PositionInserts_DoNotIncludeGeneratedLandblockColumn()
        {
            var root = FindRepoRoot();
            Assert.IsNotNull(root, "Could not locate repository root directory.");

            var updatesDir = Path.Combine(root, "Database", "Updates", "World");
            if (!Directory.Exists(updatesDir))
                return;

            var pattern = new Regex(@"INSERT\s+INTO\s+`?position`?\s*\([^)]*\blandblock\b", RegexOptions.IgnoreCase);

            var files = Directory.GetFiles(updatesDir, "*.sql", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                var match = pattern.Match(text);
                Assert.IsFalse(match.Success,
                    $"File {Path.GetFileName(file)} attempts to INSERT into generated column 'landblock'. MySQL derives this automatically from obj_Cell_Id.");
            }
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
