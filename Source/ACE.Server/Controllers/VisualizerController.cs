using System;
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
        [HttpGet("mesh/{wcid}.gltf")]
        public async Task<IActionResult> GetMesh(uint wcid)
        {
            if (wcid == 0) return BadRequest("Invalid Weenie Class ID.");

            var gltfBytes = await VisualizerService.GetMeshGltfBytesAsync(wcid);
            if (gltfBytes == null) return NotFound($"Mesh for Weenie {wcid} not found.");

            return File(gltfBytes, "model/gltf+json", $"{wcid}.gltf");
        }

        [HttpGet("texture/{id}.png")]
        public async Task<IActionResult> GetTexture(uint id)
        {
            if (id == 0) return BadRequest("Invalid Texture ID.");

            var pngBytes = await VisualizerService.GetTexturePngBytesAsync(id);
            if (pngBytes == null) return NotFound($"Texture {id} not found.");

            return File(pngBytes, "image/png");
        }

        [HttpGet("palette/{id}.png")]
        public async Task<IActionResult> GetPalette(uint id)
        {
            if (id == 0) return BadRequest("Invalid Palette ID.");

            var pngBytes = await VisualizerService.GetPalettePngBytesAsync(id);
            if (pngBytes == null) return NotFound($"Palette {id} not found.");

            return File(pngBytes, "image/png");
        }

        [HttpPost("save-screenshot")]
        public async Task<IActionResult> SaveScreenshot([FromBody] ScreenshotRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.DataUrl) || string.IsNullOrEmpty(request.Filename))
                return BadRequest("Invalid request data.");

            var base64Data = request.DataUrl;
            if (base64Data.Contains(","))
                base64Data = base64Data.Split(',')[1];

            var bytes = Convert.FromBase64String(base64Data);
            var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "wwwroot", "screenshots");
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var path = System.IO.Path.Combine(dir, request.Filename);
            await System.IO.File.WriteAllBytesAsync(path, bytes);

            return Ok(new { path = $"/screenshots/{request.Filename}" });
        }
    }

    public class ScreenshotRequest
    {
        public string DataUrl { get; set; }
        public string Filename { get; set; }
    }
}
