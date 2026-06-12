using System;
using System.Linq;
using System.Threading.Tasks;
using ACE.Database;
using ACE.Database.Models.Auth;
using ACE.Server.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACE.Server.Web.Controllers
{
    [ApiController]
    [Route("api/patch-notes")]
    public class PatchNotesController : BaseController
    {
        /// <summary>Test seam: capture criteria passed to search without hitting ace_auth.</summary>
        internal static System.Func<PatchNotesSearchCriteria, PagedResult<PatchNote>> SearchForTests;

        /// <summary>Test seam: skip DB migration during unit tests.</summary>
        internal static System.Action EnsureMigratedForTests;

        private static void EnsurePatchNotesReady()
        {
            if (EnsureMigratedForTests != null)
                EnsureMigratedForTests();
            else
                PatchNotesManager.EnsureDatabaseMigrated();
        }

        private static PagedResult<PatchNote> SearchPatchNotes(PatchNotesSearchCriteria criteria)
        {
            if (SearchForTests != null)
                return SearchForTests(criteria);
            return PatchNotesDatabase.Search(criteria);
        }

        [HttpGet("meta", Order = 0)]
        [AllowAnonymous]
        public IActionResult GetMeta()
        {
            if (!ServerConfig.enable_web_portal.Value)
                return PortalDisabled();

            try
            {
                PatchNotesManager.EnsureDatabaseMigrated();
                var lastUpdated = PatchNotesManager.GetLastUpdatedUtc();
                return Ok(new PatchNotesMetaDto
                {
                    PublicUrl = PatchNotesManager.PublicListUrl,
                    LastUpdatedAt = lastUpdated
                });
            }
            catch (Exception ex)
            {
                return PatchNotesError(ex);
            }
        }

        [HttpGet(Order = 1)]
        [AllowAnonymous]
        public IActionResult List([FromQuery] PatchNotesSearchCriteria criteria)
        {
            if (!ServerConfig.enable_web_portal.Value)
                return PortalDisabled();

            try
            {
                EnsurePatchNotesReady();
                criteria.PrepareForPublicList();
                var result = SearchPatchNotes(criteria);
                return Ok(MapPaged(result, MapPublic));
            }
            catch (Exception ex)
            {
                return PatchNotesError(ex);
            }
        }

        [HttpGet("{slug}", Order = 2)]
        [AllowAnonymous]
        public IActionResult GetBySlug(string slug)
        {
            if (!ServerConfig.enable_web_portal.Value)
                return PortalDisabled();

            if (string.Equals(slug, "meta", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(slug, "admin", StringComparison.OrdinalIgnoreCase))
                return NotFound();

            try
            {
                PatchNotesManager.EnsureDatabaseMigrated();
                var note = PatchNotesDatabase.GetBySlug(slug, publishedOnly: true);
                if (note == null)
                    return NotFound();

                return Ok(MapPublic(note));
            }
            catch (Exception ex)
            {
                return PatchNotesError(ex);
            }
        }

        [HttpGet("admin/all", Order = 0)]
        public IActionResult AdminList([FromQuery] PatchNotesSearchCriteria criteria)
        {
            if (!HasPortalAccess(PortalPages.PatchNotesAdmin))
                return Forbid();

            PatchNotesManager.EnsureDatabaseMigrated();
            criteria.PublishedOnly = false;
            criteria.Normalize();
            var result = PatchNotesDatabase.Search(criteria);
            return Ok(MapPaged(result, MapAdmin));
        }

        [HttpGet("admin/{id:int}")]
        public IActionResult AdminGet(int id)
        {
            if (!HasPortalAccess(PortalPages.PatchNotesAdmin))
                return Forbid();

            var note = PatchNotesDatabase.GetById(id);
            if (note == null)
                return NotFound();

            return Ok(MapAdmin(note));
        }

        [HttpPost("admin")]
        public IActionResult AdminCreate([FromBody] PatchNoteWriteDto dto)
        {
            if (!HasPortalAccess(PortalPages.PatchNotesAdmin))
                return Forbid();

            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { message = "Title is required." });

            var slug = string.IsNullOrWhiteSpace(dto.Slug)
                ? PatchNotesManager.Slugify(dto.Title)
                : PatchNotesManager.Slugify(dto.Slug);
            slug = PatchNotesManager.EnsureUniqueSlug(slug);

            var note = new PatchNote
            {
                Slug = slug,
                Title = dto.Title.Trim(),
                Summary = dto.Summary?.Trim(),
                Body = dto.Body ?? "",
                Status = PatchNoteStatus.Draft,
                PostToDiscord = dto.PostToDiscord
            };

            PatchNotesDatabase.Create(note);
            return Ok(MapAdmin(note));
        }

        [HttpPut("admin/{id:int}")]
        public IActionResult AdminUpdate(int id, [FromBody] PatchNoteWriteDto dto)
        {
            if (!HasPortalAccess(PortalPages.PatchNotesAdmin))
                return Forbid();

            var note = PatchNotesDatabase.GetById(id);
            if (note == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { message = "Title is required." });

            if (!string.IsNullOrWhiteSpace(dto.Slug))
            {
                var slug = PatchNotesManager.Slugify(dto.Slug);
                if (!string.Equals(slug, note.Slug, StringComparison.OrdinalIgnoreCase))
                {
                    if (PatchNotesDatabase.SlugExists(slug, id))
                        return BadRequest(new { message = "Slug is already in use." });
                    note.Slug = slug;
                }
            }

            note.Title = dto.Title.Trim();
            note.Summary = dto.Summary?.Trim();
            note.Body = dto.Body ?? "";
            note.PostToDiscord = dto.PostToDiscord;

            if (note.Status == PatchNoteStatus.Published)
                note.UpdatedAt = DateTime.UtcNow;

            PatchNotesDatabase.Update(note);
            PatchNotesManager.InvalidateMetaCache();

            return Ok(MapAdmin(note));
        }

        [HttpPost("admin/{id:int}/publish")]
        public async Task<IActionResult> AdminPublish(int id)
        {
            if (!HasPortalAccess(PortalPages.PatchNotesAdmin))
                return Forbid();

            try
            {
                var (note, discord) = await PatchNotesManager.PublishAsync(id, CurrentAccountId);
                return Ok(new PatchNotePublishResponseDto
                {
                    Note = MapAdmin(note),
                    Discord = MapDiscord(discord)
                });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("admin/{id:int}/discord")]
        public async Task<IActionResult> AdminPostDiscord(int id)
        {
            if (!HasPortalAccess(PortalPages.PatchNotesAdmin))
                return Forbid();

            var note = PatchNotesDatabase.GetById(id);
            if (note == null)
                return NotFound(new { message = "Patch note not found." });

            if (!string.Equals(note.Status, PatchNoteStatus.Published, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Publish the note before posting to Discord." });

            var discord = await PatchNotesManager.PostToDiscordAsync(note);
            return Ok(new PatchNotePublishResponseDto
            {
                Note = MapAdmin(note),
                Discord = MapDiscord(discord)
            });
        }

        [HttpPost("admin/{id:int}/unpublish")]
        public IActionResult AdminUnpublish(int id)
        {
            if (!HasPortalAccess(PortalPages.PatchNotesAdmin))
                return Forbid();

            try
            {
                var note = PatchNotesManager.Unpublish(id);
                return Ok(MapAdmin(note));
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpDelete("admin/{id:int}")]
        public IActionResult AdminDelete(int id)
        {
            if (!HasPortalAccess(PortalPages.PatchNotesAdmin))
                return Forbid();

            var note = PatchNotesDatabase.GetById(id);
            if (note == null)
                return NotFound();

            if (note.Status == PatchNoteStatus.Published)
                return BadRequest(new { message = "Unpublish before deleting a published note." });

            PatchNotesDatabase.Delete(id);
            return Ok();
        }

        private IActionResult PortalDisabled() =>
            StatusCode(503, new { message = "The Web Portal is currently disabled by the server administrator." });

        private IActionResult PatchNotesError(Exception ex)
        {
            var correlationId = Guid.NewGuid().ToString();
            Log.Error($"[Correlation ID: {correlationId}] Patch notes request failed", ex);
            return StatusCode(500, new
            {
                message = "Patch notes are temporarily unavailable. Ensure ace_auth.patch_notes exists or check server logs.",
                correlationId
            });
        }

        private static PagedResultDto<TOut> MapPaged<TIn, TOut>(PagedResult<TIn> source, Func<TIn, TOut> map) =>
            new()
            {
                Items = source.Items.Select(map).ToList(),
                TotalCount = source.TotalCount,
                Page = source.Page,
                PageSize = source.PageSize,
                TotalPages = source.TotalPages
            };

        private static PatchNotePublicDto MapPublic(PatchNote n) => new()
        {
            Id = n.Id,
            Slug = n.Slug,
            Title = n.Title,
            Summary = n.Summary,
            Body = n.Body,
            PublishedAt = n.PublishedAt,
            UpdatedAt = n.UpdatedAt,
            PublicUrl = PatchNotesManager.BuildNoteUrl(n.Slug)
        };

        private static PatchNotesDiscordResultDto MapDiscord(PatchNotesDiscordResult r) => new()
        {
            Status = r.Status,
            Message = r.Message,
            MessageId = r.MessageId
        };

        private static PatchNoteAdminDto MapAdmin(PatchNote n) => new()
        {
            Id = n.Id,
            Slug = n.Slug,
            Title = n.Title,
            Summary = n.Summary,
            Body = n.Body,
            Status = n.Status,
            PublishedAt = n.PublishedAt,
            PublishedByAccountId = n.PublishedByAccountId,
            PostToDiscord = n.PostToDiscord,
            DiscordMessageId = n.DiscordMessageId,
            CreatedAt = n.CreatedAt,
            UpdatedAt = n.UpdatedAt,
            PublicUrl = PatchNotesManager.BuildNoteUrl(n.Slug)
        };

        public class PatchNotesMetaDto
        {
            public string PublicUrl { get; set; }
            public DateTime? LastUpdatedAt { get; set; }
        }

        public class PagedResultDto<T>
        {
            public System.Collections.Generic.List<T> Items { get; set; } = new();
            public int TotalCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
            public int TotalPages { get; set; }
        }

        public class PatchNotePublicDto
        {
            public int Id { get; set; }
            public string Slug { get; set; }
            public string Title { get; set; }
            public string Summary { get; set; }
            public string Body { get; set; }
            public DateTime? PublishedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public string PublicUrl { get; set; }
        }

        public class PatchNoteAdminDto : PatchNotePublicDto
        {
            public string Status { get; set; }
            public uint? PublishedByAccountId { get; set; }
            public bool PostToDiscord { get; set; }
            public long? DiscordMessageId { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public class PatchNoteWriteDto
        {
            public string Title { get; set; }
            public string Slug { get; set; }
            public string Summary { get; set; }
            public string Body { get; set; }
            public bool PostToDiscord { get; set; } = true;
        }

        public class PatchNotePublishResponseDto
        {
            public PatchNoteAdminDto Note { get; set; }
            public PatchNotesDiscordResultDto Discord { get; set; }
        }

        public class PatchNotesDiscordResultDto
        {
            public string Status { get; set; }
            public string Message { get; set; }
            public ulong? MessageId { get; set; }
        }
    }
}
