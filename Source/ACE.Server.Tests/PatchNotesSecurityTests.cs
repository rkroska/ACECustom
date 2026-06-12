using System.Collections.Generic;
using System.Reflection;
using ACE.Database;
using ACE.Database.Models.Auth;
using ACE.Server.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
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
            PatchNotesSearchCriteria captured = null;
            PatchNotesController.EnsureMigratedForTests = () => { };
            PatchNotesController.SearchForTests = criteria =>
            {
                captured = criteria;
                return new PagedResult<PatchNote>
                {
                    Items = new List<PatchNote>(),
                    TotalCount = 0,
                    Page = criteria.Page,
                    PageSize = criteria.PageSize,
                };
            };

            try
            {
                var controller = new PatchNotesController();
                var response = controller.List(new PatchNotesSearchCriteria { PublishedOnly = false });

                Assert.IsInstanceOfType(response, typeof(OkObjectResult));
                Assert.IsNotNull(captured);
                Assert.IsTrue(captured.PublishedOnly);
            }
            finally
            {
                PatchNotesController.SearchForTests = null;
                PatchNotesController.EnsureMigratedForTests = null;
            }
        }
    }
}
