using System;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>Reads a source file for the tests that pin a statement order no unit test can reach.</summary>
    internal static class TestSource
    {
        public static string Read(params string[] parts)
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "ACE.sln")))
                dir = Directory.GetParent(dir)?.FullName;
            if (dir == null)
                Assert.Inconclusive("source tree not found next to the test binaries");
            return File.ReadAllText(Path.Combine(dir, Path.Combine(parts)));
        }
    }
}
