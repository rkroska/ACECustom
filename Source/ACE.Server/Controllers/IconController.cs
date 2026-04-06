using ACE.Server.Services;
using ACE.Server.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACE.Server.Controllers
{
    [AllowAnonymous] // Allow icons to be loaded without auth (optional, but convenient for previews)
    [ApiController]
    [Route("api/[controller]")]
    public class IconController : BaseController
    {
        [HttpGet("{iconId}")]
        [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Any)]
        public IActionResult GetIcon(uint iconId, [FromQuery] uint? underlay = null, [FromQuery] uint? overlay = null, [FromQuery] uint? overlaySecondary = null, [FromQuery] uint? uiEffects = null)
        {
            if (iconId == 0) return NotFound();

            var pngBytes = IconService.GetIcon(iconId, underlay, overlay, overlaySecondary, uiEffects);
            if (pngBytes == null) return NotFound();

            return File(pngBytes, "image/png");
        }
    }
}
