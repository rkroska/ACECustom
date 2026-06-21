using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ACE.Database.Models.Auth;
using ACE.Entity.Enum;
using Microsoft.EntityFrameworkCore;

namespace ACE.Server.Managers
{
    public static class PortalPages
    {
        public const string Characters = "characters";
        public const string Leaderboards = "leaderboards";
        public const string Players = "players";
        public const string Map = "map";
        public const string CombatCalculator = "combat-calculator";
        public const string Properties = "properties";
        public const string Lookup = "lookup";
        public const string Items = "items";
        public const string Stamps = "stamps";
        public const string QuestBuilder = "quest-builder";
        public const string Weenie = "weenie";
        public const string Console = "console";
        public const string Params = "params";
        public const string Events = "events";
        public const string PortalSecurity = "portal-security";
        public const string AuditLog = "audit-log";
        public const string PatchNotes = "patch-notes";
        public const string PatchNotesAdmin = "patch-notes-admin";
        public const string CorpseFinder = "corpse-finder";
    }

    public sealed class PortalPageDefinition
    {
        public string Key { get; }
        public string Label { get; }
        public string Route { get; }
        public string Section { get; }
        public int DefaultMinLevel { get; }

        public PortalPageDefinition(string key, string label, string route, string section, int defaultMinLevel)
        {
            Key = key;
            Label = label;
            Route = route;
            Section = section;
            DefaultMinLevel = defaultMinLevel;
        }
    }

    /// <summary>
    /// Per-page minimum access levels for the web portal.
    /// Level 0 = all authenticated users; level N requires user access level >= N.
    /// Overrides are stored in ace_auth.portal_page_access and apply immediately (no restart).
    /// </summary>
    public static class PortalAccessManager
    {
        public const int DefaultRestrictedPageMinLevel = 4;
        public const int DefaultPublicPageMinLevel = 0;

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(PortalAccessManager));
        private static readonly object _lock = new();
        private static volatile bool _databaseMigrated;
        private static readonly object _migrationLock = new();
        private static bool _initialized;
        private static bool _hasDatabaseOverrides;
        private static Dictionary<string, int> _minLevels = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates ace_auth.portal_page_access if missing (same SQL as Database/Updates/Authentication/2026-05-31-00-Portal-Page-Access.sql).
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
                        context.Database.ExecuteSqlRaw("SELECT 1 FROM portal_page_access LIMIT 1");
                        _databaseMigrated = true;
                        return;
                    }
                    catch
                    {
                        log.Info("Creating portal_page_access table...");
                    }

                    context.Database.ExecuteSqlRaw(@"
                        CREATE TABLE IF NOT EXISTS `portal_page_access` (
                          `page_key` VARCHAR(64) NOT NULL,
                          `min_level` TINYINT UNSIGNED NOT NULL,
                          `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                          PRIMARY KEY (`page_key`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");

                    log.Info("portal_page_access table ready");
                    _databaseMigrated = true;
                }
                catch (Exception ex)
                {
                    log.Error($"portal_page_access migration failed: {ex.Message}");
                }
            }
        }

        public static IReadOnlyList<PortalPageDefinition> PageDefinitions { get; } = new List<PortalPageDefinition>
        {
            new(PortalPages.Characters, "Characters", "/characters", "Player", DefaultPublicPageMinLevel),
            new(PortalPages.Leaderboards, "Leaderboards", "/leaderboards", "Player", DefaultPublicPageMinLevel),
            new(PortalPages.PatchNotes, "Patch Notes", "/patch-notes", "Player", DefaultPublicPageMinLevel),
            new(PortalPages.Players, "Player List", "/players", "Monitoring", DefaultRestrictedPageMinLevel),
            new(PortalPages.AuditLog, "Audit Log", "/audit", "Monitoring", DefaultRestrictedPageMinLevel),
            new(PortalPages.Map, "World Map", "/map", "Monitoring", DefaultRestrictedPageMinLevel),
            new(PortalPages.CombatCalculator, "Combat Calculator", "/combat-calculator", "Content Tools", DefaultRestrictedPageMinLevel),
            new(PortalPages.Properties, "Property Explorer", "/properties", "Content Tools", DefaultRestrictedPageMinLevel),
            new(PortalPages.Lookup, "Lookup Tables", "/lookup", "Content Tools", DefaultRestrictedPageMinLevel),
            new(PortalPages.Items, "Item Search", "/items", "Content Tools", DefaultRestrictedPageMinLevel),
            new(PortalPages.Stamps, "Stamp Search", "/stamps", "Content Tools", DefaultRestrictedPageMinLevel),
            new(PortalPages.QuestBuilder, "Quest Builder", "/quest-builder", "Content Tools", DefaultRestrictedPageMinLevel),
            new(PortalPages.Weenie, "Weenie Editor", "/weenie", "Content Tools", DefaultRestrictedPageMinLevel),
            new(PortalPages.Console, "Console", "/console", "Server Management", DefaultRestrictedPageMinLevel),
            new(PortalPages.Params, "Server Params", "/params", "Server Management", DefaultRestrictedPageMinLevel),
            new(PortalPages.Events, "Server Events", "/events", "Server Management", DefaultRestrictedPageMinLevel),
            new(PortalPages.PortalSecurity, "Portal Security", "/portal-security", "Server Management", DefaultRestrictedPageMinLevel),
            new(PortalPages.PatchNotesAdmin, "Patch Notes", "/patch-notes/manage", "Server Management", DefaultRestrictedPageMinLevel),
            new(PortalPages.CorpseFinder, "Corpse Finder", "/corpse-finder", "Monitoring", DefaultRestrictedPageMinLevel),
        };

        /// <summary>True when at least one page level has been saved to the database.</summary>
        public static bool HasDatabaseOverrides
        {
            get
            {
                EnsureInitialized();
                return _hasDatabaseOverrides;
            }
        }

        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized)
                    return;

                ReloadFromDatabase();
                _initialized = true;
                log.Info($"Portal page access initialized (database overrides: {_hasDatabaseOverrides})");
            }
        }

        private static void EnsureInitialized()
        {
            if (!_initialized)
                Initialize();
        }

        private static void ReloadFromDatabase()
        {
            _minLevels = BuildDefaults();
            _hasDatabaseOverrides = false;

            try
            {
                EnsureDatabaseMigrated();

                using var context = new AuthDbContext();
                if (!TableExists(context))
                {
                    log.Warn("portal_page_access table is unavailable; using code defaults.");
                    TryImportLegacyJsonIntoMemory();
                    return;
                }

                var rows = context.PortalPageAccess.AsNoTracking().ToList();
                if (rows.Count == 0)
                {
                    if (TryImportLegacyJsonToDatabase(context))
                        rows = context.PortalPageAccess.AsNoTracking().ToList();
                }

                ApplyRows(rows);
            }
            catch (Exception ex)
            {
                log.Error("Failed to load portal page access from database; using code defaults.", ex);
            }
        }

        private static bool TableExists(AuthDbContext context)
        {
            try
            {
                context.Database.ExecuteSqlRaw("SELECT 1 FROM portal_page_access LIMIT 1");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ApplyRows(IEnumerable<ACE.Database.Models.Auth.PortalPageAccess> rows)
        {
            var validKeys = PageDefinitions.Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.PageKey) || !validKeys.Contains(row.PageKey))
                    continue;

                if (!IsValidLevel(row.MinLevel))
                    continue;

                _minLevels[row.PageKey] = row.MinLevel;
                _hasDatabaseOverrides = true;
            }
        }

        private static Dictionary<string, int> BuildDefaults()
        {
            return PageDefinitions.ToDictionary(p => p.Key, p => p.DefaultMinLevel, StringComparer.OrdinalIgnoreCase);
        }

        public static int GetMinLevel(string pageKey)
        {
            EnsureInitialized();
            if (_minLevels.TryGetValue(pageKey, out var level))
                return level;

            var def = PageDefinitions.FirstOrDefault(p => string.Equals(p.Key, pageKey, StringComparison.OrdinalIgnoreCase));
            return def?.DefaultMinLevel ?? DefaultRestrictedPageMinLevel;
        }

        public static bool CanAccess(AccessLevel userLevel, string pageKey)
        {
            return (int)userLevel >= GetMinLevel(pageKey);
        }

        public static Dictionary<string, bool> GetAccessMap(AccessLevel userLevel)
        {
            EnsureInitialized();
            return PageDefinitions.ToDictionary(
                p => p.Key,
                p => CanAccess(userLevel, p.Key),
                StringComparer.OrdinalIgnoreCase);
        }

        public static List<PortalPageAccessDto> GetPageAccessList(AccessLevel userLevel)
        {
            EnsureInitialized();
            return PageDefinitions.Select(p => new PortalPageAccessDto
            {
                Key = p.Key,
                Label = p.Label,
                Route = p.Route,
                Section = p.Section,
                MinLevel = GetMinLevel(p.Key),
                CanAccess = CanAccess(userLevel, p.Key),
            }).ToList();
        }

        public static bool UpdateLevels(Dictionary<string, int> updates, out string? error)
        {
            EnsureInitialized();
            error = null;

            if (updates == null || updates.Count == 0)
            {
                error = "No updates provided.";
                return false;
            }

            var validKeys = PageDefinitions.Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (key, level) in updates)
            {
                if (!validKeys.Contains(key))
                {
                    error = $"Unknown page key: {key}";
                    return false;
                }

                if (!IsValidLevel(level))
                {
                    error = $"Invalid access level {level} for page '{key}'. Must be 0-5.";
                    return false;
                }
            }

            lock (_lock)
            {
                var merged = new Dictionary<string, int>(_minLevels, StringComparer.OrdinalIgnoreCase);

                foreach (var (key, level) in updates)
                    merged[key] = level;

                try
                {
                    EnsureDatabaseMigrated();

                    using var context = new AuthDbContext();
                    if (!TableExists(context))
                    {
                        error = "portal_page_access table is unavailable. Check server logs and database permissions.";
                        return false;
                    }

                    var now = DateTime.UtcNow;
                    foreach (var (key, level) in updates)
                    {
                        var canonicalKey = PageDefinitions.First(p =>
                            string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)).Key;

                        var row = context.PortalPageAccess.FirstOrDefault(r =>
                            r.PageKey == canonicalKey);

                        if (row == null)
                        {
                            context.PortalPageAccess.Add(new ACE.Database.Models.Auth.PortalPageAccess
                            {
                                PageKey = canonicalKey,
                                MinLevel = (byte)level,
                                UpdatedAt = now,
                            });
                        }
                        else
                        {
                            row.MinLevel = (byte)level;
                            row.UpdatedAt = now;
                        }
                    }

                    context.SaveChanges();
                    _minLevels = merged;
                    _hasDatabaseOverrides = true;
                    log.Info($"Portal page access updated ({updates.Count} page(s)) in database");
                    return true;
                }
                catch (Exception ex)
                {
                    log.Error("Failed to save portal page access to database", ex);
                    error = "Failed to save configuration.";
                    return false;
                }
            }
        }

        private static bool IsValidLevel(int level) => level >= 0 && level <= 5;

        /// <summary>One-time import from legacy portal_page_access.json if present.</summary>
        private static bool TryImportLegacyJsonToDatabase(AuthDbContext context)
        {
            var legacy = TryReadLegacyJsonPages();
            if (legacy == null || legacy.Count == 0)
                return false;

            var now = DateTime.UtcNow;
            foreach (var (key, level) in legacy)
            {
                context.PortalPageAccess.Add(new ACE.Database.Models.Auth.PortalPageAccess
                {
                    PageKey = key,
                    MinLevel = (byte)level,
                    UpdatedAt = now,
                });
            }

            context.SaveChanges();
            log.Info($"Imported {legacy.Count} portal page access row(s) from legacy JSON into database.");
            return true;
        }

        private static void TryImportLegacyJsonIntoMemory()
        {
            var legacy = TryReadLegacyJsonPages();
            if (legacy == null)
                return;

            foreach (var (key, level) in legacy)
                _minLevels[key] = level;

            log.Info($"Loaded {legacy.Count} portal page access override(s) from legacy JSON (not persisted — apply SQL migration).");
        }

        private static Dictionary<string, int>? TryReadLegacyJsonPages()
        {
            foreach (var path in GetLegacyJsonPaths())
            {
                if (!File.Exists(path))
                    continue;

                try
                {
                    var json = File.ReadAllText(path);
                    var stored = JsonSerializer.Deserialize<PortalAccessConfig>(json, JsonOptions);
                    if (stored?.Pages == null || stored.Pages.Count == 0)
                        continue;

                    var validKeys = PageDefinitions.Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                    foreach (var (key, level) in stored.Pages)
                    {
                        if (validKeys.Contains(key) && IsValidLevel(level))
                            result[key] = level;
                    }

                    if (result.Count > 0)
                    {
                        log.Info($"Found legacy portal page access JSON at {path}");
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    log.Warn($"Could not read legacy portal page access JSON at {path}: {ex.Message}");
                }
            }

            return null;
        }

        private static IEnumerable<string> GetLegacyJsonPaths()
        {
            var envPath = Environment.GetEnvironmentVariable("ACE_PORTAL_PAGE_ACCESS_PATH");
            if (!string.IsNullOrWhiteSpace(envPath))
                yield return Path.GetFullPath(envPath.Trim());

            yield return "/ace/Config/portal_page_access.json";

            var baseDir = Path.GetDirectoryName(typeof(PortalAccessManager).Assembly.Location) ?? AppContext.BaseDirectory;
            yield return Path.Combine(baseDir, "Config", "portal_page_access.json");
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private sealed class PortalAccessConfig
        {
            public Dictionary<string, int> Pages { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }
    }

    public sealed class PortalPageAccessDto
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Route { get; set; } = "";
        public string Section { get; set; } = "";
        public int MinLevel { get; set; }
        public bool CanAccess { get; set; }
    }
}
