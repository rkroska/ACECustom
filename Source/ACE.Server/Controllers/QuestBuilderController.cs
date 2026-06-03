using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using ACE.Server.Managers;
using ACE.Server.Managers.QuestBuilder;
using ACE.Server.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACE.Server.Web.Controllers
{
    [ApiController]
    [Route("api/quest-builder")]
    public class QuestBuilderController : BaseController
    {
        [HttpGet("templates")]
        public IActionResult GetTemplates()
        {
            if (!HasPortalAccess(PortalPages.QuestBuilder)) return Forbid();
            return Ok(QuestBuilderTemplates.List());
        }

        [HttpGet("template/{id}")]
        public IActionResult GetTemplate(string id)
        {
            if (!HasPortalAccess(PortalPages.QuestBuilder)) return Forbid();
            var next = QuestBuilderCompiler.FindNextWcid();
            if (next == 0)
                return BadRequest(new { message = "No free WCIDs in range 78780090-78780199." });
            return Ok(QuestBuilderTemplates.Create(id, next));
        }

        [HttpGet("next-wcid")]
        public IActionResult GetNextWcid([FromQuery] uint start = 78780090, [FromQuery] uint end = 78780199)
        {
            if (!HasPortalAccess(PortalPages.QuestBuilder)) return Forbid();
            var wcid = QuestBuilderCompiler.FindNextWcid(start, end);
            if (wcid == 0)
                return Ok(new NextWcidResultDto { Wcid = 0, RangeStart = start, RangeEnd = end });
            return Ok(new NextWcidResultDto { Wcid = wcid, RangeStart = start, RangeEnd = end });
        }

        [HttpGet("creature-search")]
        public IActionResult SearchCreatures([FromQuery] string q, [FromQuery] int limit = 40)
        {
            if (!HasPortalAccess(PortalPages.QuestBuilder)) return Forbid();
            return Ok(QuestBuilderCreatureSearch.Search(q, limit));
        }

        [HttpGet("creature/{wcid}")]
        public IActionResult GetCreature(uint wcid)
        {
            if (!HasPortalAccess(PortalPages.QuestBuilder)) return Forbid();
            var result = QuestBuilderCreatureSearch.Get(wcid);
            if (result == null)
                return NotFound(new { message = $"No creature weenie found for WCID {wcid}." });
            return Ok(result);
        }

        [HttpPost("validate")]
        public IActionResult Validate([FromBody] QuestPackageDto package)
        {
            if (!HasPortalAccess(PortalPages.QuestBuilder)) return Forbid();
            return Ok(QuestBuilderCompiler.Validate(package));
        }

        /// <summary>Probe endpoint (portal session required).</summary>
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            if (!HasPortalAccess(PortalPages.QuestBuilder)) return Forbid();
            return Ok(new { ok = true });
        }

        /// <summary>Capability flags for the Quest Builder UI.</summary>
        [HttpGet("capabilities")]
        public IActionResult GetCapabilities()
        {
            if (!HasPortalAccess(PortalPages.QuestBuilder)) return Forbid();
            return Ok(new
            {
                importNpc = true,
                importStamp = true,
                updateOnlyExport = true,
                actorShellExport = true,
            });
        }

        [HttpGet("import/npc/{wcid}")]
        public IActionResult ImportFromNpc(uint wcid, [FromQuery] bool includeRelated = true)
        {
            if (!HasPortalAccess(PortalPages.QuestBuilder)) return Forbid();
            var result = QuestBuilderImporter.ImportFromNpcWcid(wcid, includeRelated);
            if (!result.Ok)
                return BadRequest(new { message = result.Message, warnings = result.Warnings });
            return Ok(result);
        }

        [HttpGet("import/stamp")]
        public IActionResult ImportFromStamp([FromQuery] string name)
        {
            if (!HasPortalAccess(PortalPages.QuestBuilder)) return Forbid();
            var result = QuestBuilderImporter.ImportFromStampName(name);
            if (!result.Ok)
                return BadRequest(new { message = result.Message, warnings = result.Warnings });
            return Ok(result);
        }

        [HttpPost("import/package")]
        public IActionResult ImportPackage([FromBody] QuestPackageDto package)
        {
            if (!HasPortalAccess(PortalPages.QuestBuilder)) return Forbid();
            if (package?.Actors == null || package.Actors.Count == 0)
                return BadRequest(new { message = "Package needs at least one actor." });
            return Ok(new QuestImportResultDto
            {
                Ok = true,
                Message = "Package loaded from JSON.",
                Package = package,
            });
        }

        [HttpPost("export")]
        public IActionResult Export([FromBody] QuestPackageDto package, [FromQuery] bool updateOnly = false)
        {
            if (!HasPortalAccess(PortalPages.QuestBuilder)) return Forbid();
            try
            {
                var result = QuestBuilderCompiler.Export(package, updateOnly);
                using var ms = new MemoryStream();
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    foreach (var file in result.Files)
                    {
                        var entry = zip.CreateEntry(file.FileName, CompressionLevel.Optimal);
                        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                        writer.Write(file.Content);
                    }
                    var readme = zip.CreateEntry("README.md", CompressionLevel.Optimal);
                    using (var writer = new StreamWriter(readme.Open(), Encoding.UTF8))
                        writer.Write(result.Readme);
                }

                var safeName = string.IsNullOrWhiteSpace(package?.Package) ? "quest_package" : package.Package;
                foreach (var c in Path.GetInvalidFileNameChars())
                    safeName = safeName.Replace(c, '_');

                return File(ms.ToArray(), "application/zip", $"{safeName}.zip");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
