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
            Assert.IsNotNull(dir, "could not find ACE.sln above the test directory");
            return File.ReadAllText(Path.Combine(dir, Path.Combine(parts)));
        }
    }
}
