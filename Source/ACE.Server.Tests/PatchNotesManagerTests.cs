using System;
using ACE.Common;
using ACE.Server.Managers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    [TestClass]
    public class PatchNotesManagerTests
    {
        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            ConfigManager.Initialize(new MasterConfiguration
            {
                PatchNotes = new PatchNotesConfiguration
                {
                    PublicBaseUrl = "http://example.com:5002/",
                    MotdEnabled = true,
                    MotdTemplate = "Patch notes: {url}\nLast updated: {lastUpdated}",
                    MotdUseHostLocalTime = false,
                }
            });
        }

        [TestMethod]
        public void BuildPublicUrl_UsesHashRouter()
        {
            var url = PatchNotesManager.BuildPublicUrl("patch-notes");
            Assert.AreEqual("http://example.com:5002#/patch-notes", url);
        }

        [TestMethod]
        public void BuildPublicUrl_StripsExistingHashFromBase()
        {
            ConfigManager.Config.PatchNotes.PublicBaseUrl = "http://example.com#/old";
            var url = PatchNotesManager.BuildNoteUrl("my-slug");
            Assert.AreEqual("http://example.com#/patch-notes/my-slug", url);
            ConfigManager.Config.PatchNotes.PublicBaseUrl = "http://example.com:5002/";
        }

        [TestMethod]
        public void Slugify_NormalizesTitle()
        {
            var slug = PatchNotesManager.Slugify("Web Portal & Patch Notes Update!");
            StringAssert.StartsWith(slug, "web-portal");
            Assert.IsFalse(slug.Contains(' '));
            Assert.IsFalse(slug.Contains('&'));
        }

        [TestMethod]
        public void BuildMotdLines_ReplacesUrlAndLastUpdated()
        {
            var utc = new DateTime(2026, 6, 2, 16, 30, 0, DateTimeKind.Utc);
            var lines = PatchNotesManager.BuildMotdLines(utc);
            Assert.AreEqual(2, lines.Length);
            StringAssert.Contains(lines[0], "http://example.com:5002#/patch-notes");
            StringAssert.Contains(lines[1], "2026-06-02");
            StringAssert.Contains(lines[1], "UTC");
        }

        [TestMethod]
        public void BuildMotdLines_DisabledWhenMotdOff()
        {
            ConfigManager.Config.PatchNotes.MotdEnabled = false;
            var lines = PatchNotesManager.BuildMotdLines(DateTime.UtcNow);
            Assert.AreEqual(0, lines.Length);
            ConfigManager.Config.PatchNotes.MotdEnabled = true;
        }

        [TestMethod]
        public void PatchNotesDiscordResult_Factories()
        {
            var skipped = PatchNotesDiscordResult.Skipped("test");
            Assert.AreEqual("skipped", skipped.Status);
            var sent = PatchNotesDiscordResult.Sent(123);
            Assert.AreEqual("sent", sent.Status);
            Assert.AreEqual((ulong)123, sent.MessageId);
        }
    }
}
