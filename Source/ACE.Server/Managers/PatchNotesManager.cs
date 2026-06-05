using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ACE.Common;
using ACE.Database;
using ACE.Database.Models.Auth;
using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using Microsoft.EntityFrameworkCore;

namespace ACE.Server.Managers
{
    public static class PatchNotesManager
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(PatchNotesManager));

        private static volatile bool _databaseMigrated;
        private static readonly object _migrationLock = new();

        private static readonly object _metaLock = new();
        private static DateTime? _cachedLastUpdated;
        private static DateTime _cacheExpiresUtc = DateTime.MinValue;

        public static string PublicListUrl => BuildPublicUrl("patch-notes");

        /// <summary>
        /// Creates ace_auth.patch_notes if missing (same SQL as Database/Updates/Authentication/2026-05-31-01-Patch-Notes.sql).
        /// </summary>
        public static void EnsureDatabaseMigrated()
        {
            if (_databaseMigrated) return;

            lock (_migrationLock)
            {
                if (_databaseMigrated) return;

                try
                {
                    using var context = new AuthDbContext();
                    try
                    {
                        context.Database.ExecuteSqlRaw("SELECT 1 FROM patch_notes LIMIT 1");
                        _databaseMigrated = true;
                        return;
                    }
                    catch
                    {
                        log.Info("Creating patch_notes table...");
                    }

                    context.Database.ExecuteSqlRaw(@"
                        CREATE TABLE IF NOT EXISTS `patch_notes` (
                          `id` INT NOT NULL AUTO_INCREMENT,
                          `slug` VARCHAR(128) NOT NULL,
                          `title` VARCHAR(255) NOT NULL,
                          `summary` VARCHAR(1000) NULL,
                          `body` MEDIUMTEXT NOT NULL,
                          `status` VARCHAR(16) NOT NULL DEFAULT 'draft',
                          `published_at` DATETIME(6) NULL,
                          `published_by_account_id` INT UNSIGNED NULL,
                          `post_to_discord` TINYINT(1) NOT NULL DEFAULT 1,
                          `discord_message_id` BIGINT NULL,
                          `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
                          `updated_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
                          PRIMARY KEY (`id`),
                          UNIQUE KEY `UX_patch_notes_slug` (`slug`),
                          KEY `IX_patch_notes_status_published_at` (`status`, `published_at`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");

                    log.Info("patch_notes table ready");
                    _databaseMigrated = true;
                }
                catch (Exception ex)
                {
                    log.Error($"patch_notes migration failed: {ex.Message}");
                }
            }
        }

        public static void InvalidateMetaCache()
        {
            lock (_metaLock)
            {
                _cacheExpiresUtc = DateTime.MinValue;
            }
        }

        public static DateTime? GetLastUpdatedUtc()
        {
            lock (_metaLock)
            {
                if (DateTime.UtcNow < _cacheExpiresUtc && _cachedLastUpdated.HasValue)
                    return _cachedLastUpdated;

                _cachedLastUpdated = PatchNotesDatabase.GetLastPublishedAt();
                _cacheExpiresUtc = DateTime.UtcNow.AddMinutes(1);
                return _cachedLastUpdated;
            }
        }

        /// <summary>
        /// Public portal URLs use HashRouter (#/path). Path-only links break routing and duplicate the URL bar.
        /// </summary>
        public static string BuildPublicUrl(string path)
        {
            var baseUrl = ConfigManager.Config.PatchNotes?.PublicBaseUrl ?? "http://localhost:5001/";
            var hashIndex = baseUrl.IndexOf('#');
            if (hashIndex >= 0)
                baseUrl = baseUrl[..hashIndex];
            baseUrl = baseUrl.TrimEnd('/');

            path = path?.TrimStart('/') ?? "";
            var hashPath = string.IsNullOrEmpty(path) ? "/patch-notes" : $"/{path}";
            return $"{baseUrl}#{hashPath}";
        }

        public static string BuildNoteUrl(string slug) => BuildPublicUrl($"patch-notes/{slug}");

        public static string Slugify(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return $"patch-{DateTime.UtcNow:yyyy-MM-dd}";

            var slug = title.Trim().ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-").Trim('-');

            if (slug.Length > 100)
                slug = slug[..100].TrimEnd('-');

            return string.IsNullOrEmpty(slug) ? $"patch-{DateTime.UtcNow:yyyy-MM-dd}" : slug;
        }

        public static string EnsureUniqueSlug(string baseSlug, int? excludeId = null)
        {
            var slug = baseSlug;
            var suffix = 2;
            while (PatchNotesDatabase.SlugExists(slug, excludeId))
            {
                slug = $"{baseSlug}-{suffix}";
                suffix++;
            }
            return slug;
        }

        public static async Task<(PatchNote Note, PatchNotesDiscordResult Discord)> PublishAsync(int id, uint? accountId)
        {
            var note = PatchNotesDatabase.GetById(id);
            if (note == null)
                throw new InvalidOperationException("Patch note not found.");

            var now = DateTime.UtcNow;
            note.Status = PatchNoteStatus.Published;
            note.PublishedAt = now;
            note.PublishedByAccountId = accountId;
            note.UpdatedAt = now;

            PatchNotesDatabase.Update(note);
            InvalidateMetaCache();

            var discord = note.PostToDiscord
                ? await PostToDiscordAsync(note)
                : PatchNotesDiscordResult.NotRequested();

            return (note, discord);
        }

        public static PatchNote Unpublish(int id)
        {
            var note = PatchNotesDatabase.GetById(id);
            if (note == null)
                throw new InvalidOperationException("Patch note not found.");

            note.Status = PatchNoteStatus.Draft;
            note.PublishedAt = null;
            note.UpdatedAt = DateTime.UtcNow;

            PatchNotesDatabase.Update(note);
            InvalidateMetaCache();
            return note;
        }

        public static async Task<PatchNotesDiscordResult> PostToDiscordAsync(PatchNote note)
        {
            if (note.DiscordMessageId is > 0)
                return PatchNotesDiscordResult.AlreadyPosted((ulong)note.DiscordMessageId.Value);

            var config = ConfigManager.Config.PatchNotes;
            if (config == null)
            {
                log.Warn("[PatchNotes] Discord skipped: PatchNotes section missing from Config.js.");
                return PatchNotesDiscordResult.Skipped("PatchNotes is not configured in Config.js.");
            }

            if (!config.DiscordEnabled)
            {
                log.Warn("[PatchNotes] Discord skipped: PatchNotes.DiscordEnabled is false.");
                return PatchNotesDiscordResult.Skipped("PatchNotes.DiscordEnabled is false in Config.js.");
            }

            if (config.DiscordChannelId <= 0)
            {
                log.Warn("[PatchNotes] Discord skipped: PatchNotes.DiscordChannelId is 0 or unset.");
                return PatchNotesDiscordResult.Skipped("Set PatchNotes.DiscordChannelId in Config.js and restart the server.");
            }

            if (!DiscordChatManager.IsDiscordConnectionEnabled)
            {
                log.Warn("[PatchNotes] Discord skipped: Chat.EnableDiscordConnection is false.");
                return PatchNotesDiscordResult.Skipped("Discord connection is disabled (Chat.EnableDiscordConnection).");
            }

            if (!DiscordChatManager.IsDiscordClientReady)
            {
                log.Warn("[PatchNotes] Discord skipped: bot is not connected yet.");
                return PatchNotesDiscordResult.Skipped("Discord bot is offline or still connecting. Check server logs and Discord token.");
            }

            var url = BuildNoteUrl(note.Slug);
            var description = !string.IsNullOrWhiteSpace(note.Summary)
                ? note.Summary.Trim()
                : "New patch notes are available.";

            var messageId = await DiscordChatManager.SendDiscordChannelEmbedAsync(note.Title, description, url, config.DiscordChannelId);
            if (!messageId.HasValue)
            {
                log.Error($"[PatchNotes] Discord post failed for note {note.Id} ({note.Slug}) to channel {config.DiscordChannelId}.");
                return PatchNotesDiscordResult.Failed("Discord post failed. Verify channel ID, bot permissions, and server logs.");
            }

            note.DiscordMessageId = (long)messageId.Value;
            PatchNotesDatabase.Update(note);
            log.Info($"[PatchNotes] Posted to Discord channel {config.DiscordChannelId}, message {messageId.Value}.");
            return PatchNotesDiscordResult.Sent(messageId.Value);
        }

        public static void SendMotdToPlayer(Player player)
        {
            var config = ConfigManager.Config.PatchNotes;
            if (config == null || !config.MotdEnabled)
                return;

            var lastUpdated = GetLastUpdatedUtc();
            if (!lastUpdated.HasValue)
                return;

            foreach (var line in BuildMotdLines(lastUpdated.Value))
            {
                player.Session?.Network?.EnqueueSend(new GameMessageSystemChat(line, ChatMessageType.System));
            }
        }

        public static string[] BuildMotdLines(DateTime? lastUpdatedUtc = null)
        {
            var config = ConfigManager.Config.PatchNotes;
            if (config == null || !config.MotdEnabled)
                return Array.Empty<string>();

            var lastUpdated = lastUpdatedUtc ?? GetLastUpdatedUtc();
            if (!lastUpdated.HasValue)
                return Array.Empty<string>();

            var utc = DateTime.SpecifyKind(lastUpdated.Value, DateTimeKind.Utc);
            var template = config.MotdTemplate ?? "Patch notes: {url}\nLast updated: {lastUpdated}";
            var text = template
                .Replace("{url}", PublicListUrl, StringComparison.OrdinalIgnoreCase)
                .Replace("{lastUpdated}", FormatMotdTimestamp(utc, config), StringComparison.OrdinalIgnoreCase)
                .Replace("{lastUpdatedUtc}", FormatUtcTimestamp(utc), StringComparison.OrdinalIgnoreCase)
                .Replace("{lastUpdatedRelative}", FormatRelativeTimestamp(utc), StringComparison.OrdinalIgnoreCase);

            return text.Replace("\r\n", "\n").Split('\n');
        }

        public static string FormatMotdTimestamp(DateTime utc, PatchNotesConfiguration config)
        {
            var tz = ResolveMotdTimeZone(config);
            if (tz == null)
                return FormatUtcTimestamp(utc);

            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            var offset = tz.GetUtcOffset(local);
            var offsetLabel = $"UTC{(offset >= TimeSpan.Zero ? "+" : "-")}{offset:hh\\:mm}";
            return $"{local.ToString("yyyy-MM-dd h:mm tt", CultureInfo.InvariantCulture)} ({offsetLabel})";
        }

        private static TimeZoneInfo ResolveMotdTimeZone(PatchNotesConfiguration config)
        {
            if (!string.IsNullOrWhiteSpace(config.MotdTimeZoneId))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(config.MotdTimeZoneId.Trim());
                }
                catch (Exception ex)
                {
                    log.Warn($"[PatchNotes] Invalid MotdTimeZoneId '{config.MotdTimeZoneId}': {ex.Message}. Falling back.");
                }
            }

            if (config.MotdUseHostLocalTime)
                return TimeZoneInfo.Local;

            return null;
        }

        private static string FormatUtcTimestamp(DateTime utc) =>
            $"{utc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC";

        private static string FormatRelativeTimestamp(DateTime utc)
        {
            var span = DateTime.UtcNow - utc;
            if (span < TimeSpan.Zero)
                span = TimeSpan.Zero;

            if (span.TotalMinutes < 1)
                return "just now";
            if (span.TotalMinutes < 60)
                return $"{(int)span.TotalMinutes} minute{((int)span.TotalMinutes == 1 ? "" : "s")} ago";
            if (span.TotalHours < 24)
                return $"{(int)span.TotalHours} hour{((int)span.TotalHours == 1 ? "" : "s")} ago";
            if (span.TotalDays < 7)
                return $"{(int)span.TotalDays} day{((int)span.TotalDays == 1 ? "" : "s")} ago";

            return FormatUtcTimestamp(utc);
        }
    }
}
