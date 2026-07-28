using System.Threading.Tasks;
using ACE.Server.Services;
using ACE.Server.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACE.Server.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("api/[controller]")]
    public class VisualizerController : BaseController
    {
        [HttpGet("mesh/{wcid}")]
        public async Task<IActionResult> GetMesh(uint wcid)
        {
            if (wcid == 0) return BadRequest("Invalid Weenie Class ID.");

            var gltfBytes = await VisualizerService.GetMeshGltfBytesAsync(wcid);
            if (gltfBytes == null) return NotFound($"Mesh for Weenie {wcid} not found.");

            return File(gltfBytes, "model/gltf+json", $"{wcid}.gltf");
        }

        [HttpGet("texture/{id}")]
        public async Task<IActionResult> GetTexture(uint id)
        {
            if (id == 0) return BadRequest("Invalid Texture ID.");

            var pngBytes = await VisualizerService.GetTexturePngBytesAsync(id);
            if (pngBytes == null) return NotFound($"Texture {id} not found.");

            return File(pngBytes, "image/png");
        }

        [HttpGet("palette/{id}")]
        public async Task<IActionResult> GetPalette(uint id)
        {
            if (id == 0) return BadRequest("Invalid Palette ID.");

            var pngBytes = await VisualizerService.GetPalettePngBytesAsync(id);
            if (pngBytes == null) return NotFound($"Palette {id} not found.");

            return File(pngBytes, "image/png");
        }
    }
}
