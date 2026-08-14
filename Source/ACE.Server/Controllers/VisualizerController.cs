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
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetMesh(uint wcid, [FromQuery] string paletteId = null, [FromQuery] int hue = 0)
        {
            if (wcid == 0) return BadRequest("Invalid Weenie Class ID.");

            uint parsedPaletteId = 0;
            if (!string.IsNullOrEmpty(paletteId))
            {
                if (paletteId.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    uint.TryParse(paletteId.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out parsedPaletteId);
                }
                else
                {
                    uint.TryParse(paletteId, out parsedPaletteId);
                }
            }

            var gltfBytes = await VisualizerService.GetMeshGltfBytesAsync(wcid, parsedPaletteId, hue);
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

        [HttpGet("surfaces/{wcid}")]
        public IActionResult GetCreatureSurfaces(uint wcid)
        {
            if (wcid == 0) return BadRequest("Invalid Weenie Class ID.");

            var result = VisualizerService.GetCreatureSurfaces(wcid);
            return Ok(result);
        }

        [HttpGet("texture-library")]
        public IActionResult GetTextureLibrary([FromQuery] string category = "all")
        {
            var result = VisualizerService.GetTextureLibrary(category);
            return Ok(result);
        }

        [HttpGet("similar-textures/{textureId}")]
        public IActionResult GetSimilarTextures(uint textureId)
        {
            var result = VisualizerService.GetSimilarTextures(textureId);
            return Ok(result);
        }

        [HttpPost("curation")]
        public IActionResult SubmitCuration([FromBody] CurationItemDto item)
        {
            if (item == null || item.CreatureWcid == 0) return BadRequest("Invalid Curation Data.");

            var result = CurationService.AddOrUpdateCuration(item.CreatureWcid, item.CreatureName, item.TextureId, item.PaletteId, item.Rating);
            return Ok(result);
        }

        [HttpPost("clear-cache")]
        public IActionResult ClearCache()
        {
            VisualizerService.ClearCache();
            return Ok(new { success = true, message = "Visualizer cache cleared successfully." });
        }

        [HttpGet("curation/{wcid}")]
        public IActionResult GetCurations(uint wcid)
        {
            if (wcid == 0)
            {
                return Ok(CurationService.GetAllCurations());
            }
            return Ok(CurationService.GetCurationsForCreature(wcid));
        }

        [HttpGet("palette/similar/{paletteId}")]
        public IActionResult GetSimilarPalettes(string paletteId, [FromQuery] int count = 6)
        {
            if (string.IsNullOrEmpty(paletteId)) return BadRequest("Invalid Palette ID.");

            uint palId = 0;
            if (paletteId.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                uint.TryParse(paletteId.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out palId);
            }
            else
            {
                uint.TryParse(paletteId, out palId);
            }

            if (palId == 0) return BadRequest("Invalid Palette ID.");

            return Ok(VisualizerService.GetSimilarPalettes(palId, count));
        }

        [HttpGet("curated-pool/{wcid}")]
        public IActionResult GetCuratedPool(uint wcid, [FromQuery] string family = "all")
        {
            return Ok(VisualizerService.GetCuratedMutationPool(wcid, family));
        }

        [HttpGet("particle-emitter/{id}")]
        public IActionResult GetParticleEmitter(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest("Invalid Emitter ID.");

            uint emitterId = 0;
            if (id.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                uint.TryParse(id.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out emitterId);
            }
            else
            {
                uint.TryParse(id, System.Globalization.NumberStyles.HexNumber, null, out emitterId);
            }

            if (emitterId == 0) return BadRequest("Invalid Emitter ID.");

            var result = VisualizerService.GetParticleEmitterDto(emitterId);
            if (result == null) return NotFound($"ParticleEmitter 0x{emitterId:X8} not found.");

            return Ok(result);
        }

        [HttpGet("creature-particles/{wcid}")]
        public IActionResult GetCreatureParticles(uint wcid)
        {
            if (wcid == 0) return BadRequest("Invalid Weenie Class ID.");

            var result = VisualizerService.GetCreatureParticleEmitters(wcid);
            return Ok(result);
        }

        [HttpGet("texture/{id}.png")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetTexture(string id, [FromQuery] uint wcid = 0, [FromQuery] string paletteId = null, [FromQuery] float shade = 0.5f, [FromQuery] int hue = 0, [FromQuery] int slot = -1)
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

            uint parsedPaletteId = 0;
            if (!string.IsNullOrEmpty(paletteId))
            {
                if (paletteId.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    uint.TryParse(paletteId.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out parsedPaletteId);
                }
                else
                {
                    uint.TryParse(paletteId, out parsedPaletteId);
                }
            }

            var pngBytes = await VisualizerService.GetTexturePngBytesAsync(textureId, wcid, parsedPaletteId, shade, hue, slot);
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

        [HttpGet("palette/smart-pool/{wcid}")]
        public IActionResult GetSmartPalettePool(uint wcid, [FromQuery] string family = "all")
        {
            if (wcid == 0) return BadRequest("Invalid Weenie Class ID.");

            var result = VisualizerService.GetSmartPalettePool(wcid, family);
            return Ok(result);
        }

        [HttpPost("save-screenshot")]
        public async Task<IActionResult> SaveScreenshot([FromBody] ScreenshotRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.DataUrl) || string.IsNullOrEmpty(request.Filename))
                return BadRequest("Invalid request data.");

            var safeFilename = System.IO.Path.GetFileName(request.Filename);
            if (string.IsNullOrEmpty(safeFilename))
                return BadRequest("Invalid filename.");

            var base64Data = request.DataUrl;
            if (base64Data.Contains(","))
                base64Data = base64Data.Split(',')[1];

            var bytes = Convert.FromBase64String(base64Data);
            var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "wwwroot", "screenshots");
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var path = System.IO.Path.Combine(dir, safeFilename);
            await System.IO.File.WriteAllBytesAsync(path, bytes);

            return Ok(new { path = $"/screenshots/{safeFilename}" });
        }

        [HttpGet("search-creatures")]
        public IActionResult SearchCreatures([FromQuery] string query)
        {
            var results = VisualizerService.SearchCreatures(query);
            return Ok(results);
        }

        [HttpGet("species-presets")]
        public IActionResult GetSpeciesPresets()
        {
            var results = VisualizerService.GetSpeciesList();
            return Ok(results);
        }
    }

    public class ScreenshotRequest
    {
        public string DataUrl { get; set; }
        public string Filename { get; set; }
    }
}
