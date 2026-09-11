using System;
using System.Linq;
using System.Threading.Tasks;
using ACE.Server.Managers;
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

        [HttpGet("compatibility/{wcid}")]
        public IActionResult GetSpeciesCompatibility(uint wcid)
        {
            if (wcid == 0) return BadRequest("Invalid Weenie Class ID.");

            var result = VisualizerService.GetSpeciesCompatibility(wcid);
            return Ok(result);
        }

        [HttpGet("master-mutation-pool")]
        public IActionResult GetMasterMutationPool()
        {
            var pool = PetMutationService.GetMasterPalettePool();
            return Ok(pool);
        }

        [HttpGet("breeding-config")]
        public IActionResult GetBreedingConfig()
        {
            var config = new
            {
                baseMutationChance = ServerConfig.pet_breeding_base_mutation_chance.Value,
                potencyMutationChance = ServerConfig.pet_breeding_potency_mutation_chance.Value,
                mutationDecayRate = ServerConfig.pet_breeding_mutation_decay_rate.Value,
                mutationMinFloor = ServerConfig.pet_breeding_mutation_min_floor.Value,
                damageMutationStep = ServerConfig.pet_breeding_damage_mutation_step.Value,
                drMutationStep = ServerConfig.pet_breeding_dr_mutation_step.Value,
                critMutationStep = ServerConfig.pet_breeding_crit_mutation_step.Value,
                vitalityMutationStep = ServerConfig.pet_breeding_vitality_mutation_step.Value,
                potencyMutationStep = ServerConfig.pet_breeding_potency_mutation_step.Value,
                potencySoftCap = ServerConfig.pet_breeding_potency_soft_cap.Value,
                potencyHardCap = ServerConfig.pet_breeding_potency_hard_cap.Value,
                maxStatMutations = ServerConfig.pet_breeding_max_stat_mutations.Value,
                forceMutation = ServerConfig.pet_breeding_force_mutation.Value
            };
            return Ok(config);
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

            // Bound everything a client can put in the curation file.
            if (item.Rating < -1 || item.Rating > 1) return BadRequest("Rating must be -1, 0 or 1.");
            var name = (item.CreatureName ?? "").Trim();
            if (name.Length > 64) name = name.Substring(0, 64);

            var result = CurationService.AddOrUpdateCuration(item.CreatureWcid, name, item.TextureId, item.PaletteId, item.Rating);
            return Ok(result);
        }

        // The anonymous clear-cache endpoint was a free way to force every mesh and texture to
        // regenerate. The eviction service manages the cache; an admin can delete the folder.

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

            // This folder is served as static content, so what lands here must be an image and nothing
            // else: PNG signature checked, extension forced, size and count capped, decode guarded.
            const int maxBytes = 5 * 1024 * 1024;
            const int maxFiles = 500;

            var safeFilename = System.IO.Path.GetFileNameWithoutExtension(System.IO.Path.GetFileName(request.Filename));
            safeFilename = new string(safeFilename.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
            if (string.IsNullOrEmpty(safeFilename) || safeFilename.Length > 80)
                return BadRequest("Invalid filename.");
            safeFilename += ".png";

            var base64Data = request.DataUrl;
            if (base64Data.Contains(","))
                base64Data = base64Data.Split(',')[1];
            if (base64Data.Length > maxBytes * 4 / 3 + 4)
                return BadRequest("Screenshot too large.");

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64Data);
            }
            catch (FormatException)
            {
                return BadRequest("Invalid image data.");
            }

            if (bytes.Length > maxBytes)
                return BadRequest("Screenshot too large.");

            // PNG signature: 89 50 4E 47 0D 0A 1A 0A
            if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47
                || bytes[4] != 0x0D || bytes[5] != 0x0A || bytes[6] != 0x1A || bytes[7] != 0x0A)
                return BadRequest("Only PNG screenshots are accepted.");

            var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "wwwroot", "screenshots");
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            if (System.IO.Directory.GetFiles(dir).Length >= maxFiles)
                return StatusCode(507, "Screenshot storage is full.");

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
