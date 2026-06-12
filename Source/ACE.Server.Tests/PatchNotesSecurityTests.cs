using System.Reflection;
using ACE.Database;
using ACE.Database.Models.Auth;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    /// <summary>
    /// Static security invariants for patch notes (no DB required).
    /// Live API checks: scripts/portal-smoke.ps1 (admin routes require auth).
    /// </summary>
    [TestClass]
    public class PatchNotesSecurityTests
    {
        [TestMethod]
        public void SearchCriteria_DefaultsToPublishedOnly()
        {
            var criteria = new PatchNotesSearchCriteria();
            Assert.IsTrue(criteria.PublishedOnly);
        }

        [TestMethod]
        public void GetBySlug_PublishedOnly_IsDefaultParameter()
        {
            // PatchNotesDatabase.GetBySlug(slug, publishedOnly: true) is the default.
            // Public PatchNotesController.GetBySlug passes publishedOnly: true explicitly.
            var method = typeof(PatchNotesDatabase).GetMethod(nameof(PatchNotesDatabase.GetBySlug));
            Assert.IsNotNull(method);
            var publishedOnlyParam = method.GetParameters()[1];
            Assert.AreEqual("publishedOnly", publishedOnlyParam.Name);
            Assert.AreEqual(true, publishedOnlyParam.DefaultValue);
        }

        [TestMethod]
        public void PatchNoteStatus_PublishedConstant_IsLowercase()
        {
            // Search filters compare against PatchNoteStatus.Published string constant.
            Assert.AreEqual("published", PatchNoteStatus.Published);
            Assert.AreEqual("draft", PatchNoteStatus.Draft);
        }

        [TestMethod]
        public void PublicListEndpoint_ForcesPublishedOnly_InController()
        {
            var criteria = new PatchNotesSearchCriteria { PublishedOnly = false };
            criteria.PrepareForPublicList();
            Assert.IsTrue(criteria.PublishedOnly);
        }
    }
}
