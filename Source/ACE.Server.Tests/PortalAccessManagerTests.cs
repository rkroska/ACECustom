using System.Linq;
using ACE.Server.Managers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    [TestClass]
    public class PortalAccessManagerTests
    {
        [TestMethod]
        public void PageDefinitions_IncludesPatchNotesAndAuditLog()
        {
            var keys = PortalAccessManager.PageDefinitions.Select(p => p.Key).ToHashSet();
            Assert.IsTrue(keys.Contains(PortalPages.PatchNotes));
            Assert.IsTrue(keys.Contains(PortalPages.PatchNotesAdmin));
            Assert.IsTrue(keys.Contains(PortalPages.AuditLog));
        }

        [TestMethod]
        public void PatchNotes_DefaultMinLevel_IsPublic()
        {
            var patchNotes = PortalAccessManager.PageDefinitions.First(p => p.Key == PortalPages.PatchNotes);
            Assert.AreEqual(PortalAccessManager.DefaultPublicPageMinLevel, patchNotes.DefaultMinLevel);
        }

        [TestMethod]
        public void AuditLog_DefaultMinLevel_IsRestricted()
        {
            var audit = PortalAccessManager.PageDefinitions.First(p => p.Key == PortalPages.AuditLog);
            Assert.AreEqual(PortalAccessManager.DefaultRestrictedPageMinLevel, audit.DefaultMinLevel);
        }

        [TestMethod]
        public void GetMinLevel_UnknownKey_FallsBackToRestrictedDefault()
        {
            PortalAccessManager.Initialize();
            var level = PortalAccessManager.GetMinLevel("nonexistent-page-key");
            Assert.AreEqual(PortalAccessManager.DefaultRestrictedPageMinLevel, level);
        }
    }
}
