using System.Collections.Generic;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace ACE.Server.Controllers
{
    [ApiController]
    [Route("api/portal-access")]
    public class PortalAccessController : BaseController
    {
        [HttpGet("pages")]
        public IActionResult GetPages()
        {
            PortalAccessManager.EnsureDatabaseMigrated();

            if (!HasPortalAccess(PortalPages.PortalSecurity))
                return Forbid();

            return Ok(new
            {
                pages = PortalAccessManager.GetPageAccessList(CurrentAccessLevel),
                canEdit = CurrentAccessLevel == AccessLevel.Admin,
                userAccessLevel = (int)CurrentAccessLevel,
                hasCustomLevels = PortalAccessManager.HasDatabaseOverrides,
                storage = "database",
            });
        }

        [HttpPut("pages")]
        public IActionResult UpdatePages([FromBody] UpdatePortalAccessRequest request)
        {
            PortalAccessManager.EnsureDatabaseMigrated();

            if (CurrentAccessLevel != AccessLevel.Admin)
                return Forbid();

            if (request?.Levels == null || request.Levels.Count == 0)
                return BadRequest(new { message = "No page levels provided." });

            if (!PortalAccessManager.UpdateLevels(request.Levels, out var error))
                return BadRequest(new { message = error });

            return Ok(new
            {
                pages = PortalAccessManager.GetPageAccessList(CurrentAccessLevel),
                canEdit = true,
                userAccessLevel = (int)CurrentAccessLevel,
                pageAccess = PortalAccessManager.GetAccessMap(CurrentAccessLevel),
                hasCustomLevels = true,
                storage = "database",
            });
        }

        public sealed class UpdatePortalAccessRequest
        {
            public Dictionary<string, int> Levels { get; set; } = new();
        }
    }
}
