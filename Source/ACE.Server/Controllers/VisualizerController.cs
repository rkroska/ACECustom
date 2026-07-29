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
        public async Task<IActionResult> GetMesh(uint wcid, [FromQuery] uint paletteId = 0, [FromQuery] int hue = 0)
        {
            if (wcid == 0) return BadRequest("Invalid Weenie Class ID.");

            var gltfBytes = await VisualizerService.GetMeshGltfBytesAsync(wcid, paletteId, hue);
            if (gltfBytes == null) return NotFound($"Mesh for Weenie {wcid} not found.");

            return File(gltfBytes, "model/gltf+json", $"{wcid}.gltf");
        }

        [HttpGet("species-palettes/{wcid}")]
        public IActionResult GetSpeciesPalettes(uint wcid)
        {
            if (wcid == 0) return BadRequest("Invalid Weenie Class ID.");

            var result = VisualizerService.GetSpeciesPalettes(wcid);
            return Ok(result);
        }

        [HttpGet("texture-replacements/{wcid}")]
        public IActionResult GetTextureReplacements(uint wcid)
        {
            if (wcid == 0) return BadRequest("Invalid Weenie Class ID.");

            var result = VisualizerService.GetTextureReplacements(wcid);
            return Ok(result);
        }

        [HttpGet("texture/{id}.png")]
        public async Task<IActionResult> GetTexture(string id, [FromQuery] uint wcid = 0, [FromQuery] uint paletteId = 0, [FromQuery] float shade = 0.5f, [FromQuery] int hue = 0)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest("Invalid Texture ID.");

            uint textureId = 0;
            if (id.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!uint.TryParse(id.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out textureId))
                    return BadRequest("Invalid Texture ID format.");
            }
            else
            {
                if (!uint.TryParse(id, System.Globalization.NumberStyles.HexNumber, null, out textureId))
                {
                    if (!uint.TryParse(id, out textureId))
                        return BadRequest("Invalid Texture ID format.");
                }
            }

            if (textureId == 0) return BadRequest("Invalid Texture ID.");

            var pngBytes = await VisualizerService.GetTexturePngBytesAsync(textureId, wcid, paletteId, shade, hue);
            if (pngBytes == null) return NotFound($"Texture {textureId} not found.");

            return File(pngBytes, "image/png");
        }

        [HttpGet("palette/{id}.png")]
        public async Task<IActionResult> GetPalette(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest("Invalid Palette ID.");

            uint paletteId = 0;
            if (id.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!uint.TryParse(id.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out paletteId))
                    return BadRequest("Invalid Palette ID format.");
            }
            else
            {
                if (!uint.TryParse(id, System.Globalization.NumberStyles.HexNumber, null, out paletteId))
                {
                    if (!uint.TryParse(id, out paletteId))
                        return BadRequest("Invalid Palette ID format.");
                }
            }

            if (paletteId == 0) return BadRequest("Invalid Palette ID.");

            var pngBytes = await VisualizerService.GetPalettePngBytesAsync(paletteId);
            if (pngBytes == null) return NotFound($"Palette {paletteId} not found.");

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
