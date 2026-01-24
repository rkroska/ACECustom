using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using ACE.Database;
using ACE.Database.Models.Shard;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Manages creature blacklists for capture and shiny systems.
    /// Uses in-memory cache for fast lookups.
    /// </summary>
    public static class BlacklistManager
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static volatile bool _tableCreated = false;
        private static readonly object _createLock = new object();

        // In-memory cache: WCID -> (NoCapture, NoShiny)
        private static ConcurrentDictionary<uint, (bool NoCapture, bool NoShiny, string Reason)> _cache 
            = new ConcurrentDictionary<uint, (bool, bool, string)>();

        /// <summary>
        /// Ensure the creature_blacklist table exists and load cache - call during server startup
        /// </summary>
        public static void Initialize()
        {
            EnsureTableCreated();
            ReloadCache();
        }

        /// <summary>
        /// Ensure the creature_blacklist table exists
        /// </summary>
        private static void EnsureTableCreated()
        {
            if (_tableCreated) return;

            lock (_createLock)
            {
                if (_tableCreated) return;

                bool tableExists = false;
                try
                {
                    using (var context = new ShardDbContext())
                    {
                        // Check if table exists by trying to select from it
                        context.Database.ExecuteSqlRaw("SELECT 1 FROM creature_blacklist LIMIT 1");
                        tableExists = true;
                        log.Info("creature_blacklist table exists");
                    }
                }
                catch (Exception)
                {
                    // Table doesn't exist, create it
                    log.Info("Creating creature_blacklist table...");
                    try
                    {
                        using (var context = new ShardDbContext())
                        {
                            context.Database.ExecuteSqlRaw(@"
                                CREATE TABLE `creature_blacklist` (
                                    `wcid` INT UNSIGNED NOT NULL PRIMARY KEY,
                                    `no_capture` TINYINT(1) NOT NULL DEFAULT 0,
                                    `no_shiny` TINYINT(1) NOT NULL DEFAULT 0,
                                    `reason` VARCHAR(100) NULL,
                                    `added_by` VARCHAR(50) NULL,
                                    `added_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");

                            tableExists = true;
                            log.Info("creature_blacklist table created successfully");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Failed to create creature_blacklist table: {ex.Message}");
                    }
                }

                // Only mark as created if table exists or was successfully created
                _tableCreated = tableExists;
            }
        }

        /// <summary>
        /// Reload cache from database
        /// </summary>
        public static void ReloadCache()
        {
            try
            {
                using (var context = new ShardDbContext())
                {
                    var entries = context.CreatureBlacklist.ToList();
                    var newCache = new ConcurrentDictionary<uint, (bool, bool, string)>();

                    foreach (var entry in entries)
                    {
                        newCache[entry.Wcid] = (entry.NoCapture, entry.NoShiny, entry.Reason);
                    }

                    _cache = newCache;
                    log.Info($"[Blacklist] Loaded {entries.Count} blacklist entries into cache");
                }
            }
            catch (Exception ex)
            {
                log.Error($"[Blacklist] Failed to reload cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a WCID is blacklisted from capture
        /// </summary>
        public static bool IsNoCapture(uint wcid)
        {
            if (_cache.TryGetValue(wcid, out var entry))
                return entry.NoCapture;
            return false;
        }

        /// <summary>
        /// Check if a WCID is blacklisted from shiny variants
        /// </summary>
        public static bool IsNoShiny(uint wcid)
        {
            if (_cache.TryGetValue(wcid, out var entry))
                return entry.NoShiny;
            return false;
        }

        /// <summary>
        /// Add or update a blacklist entry
        /// </summary>
        public static bool AddBlacklist(uint wcid, bool noCapture, bool noShiny, string reason, string addedBy)
        {
            // If both flags are false, remove the entry entirely to prevent phantom rows
            if (!noCapture && !noShiny)
            {
                return RemoveBlacklist(wcid);
            }
            
            try
            {
                using (var context = new ShardDbContext())
                {
                    var existing = context.CreatureBlacklist.Find(wcid);
                    if (existing != null)
                    {
                        // Update existing
                        existing.NoCapture = noCapture;
                        existing.NoShiny = noShiny;
                        existing.Reason = reason;
                        existing.AddedBy = addedBy;
                        existing.AddedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        // Add new
                        context.CreatureBlacklist.Add(new CreatureBlacklist
                        {
                            Wcid = wcid,
                            NoCapture = noCapture,
                            NoShiny = noShiny,
                            Reason = reason,
                            AddedBy = addedBy,
                            AddedAt = DateTime.UtcNow
                        });
                    }
                    context.SaveChanges();
                }

                // Update cache
                _cache[wcid] = (noCapture, noShiny, reason);
                log.Debug($"[Blacklist] Added/Updated WCID {wcid}: NoCapture={noCapture}, NoShiny={noShiny}, Reason={reason}");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[Blacklist] Failed to add entry: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Remove a blacklist entry entirely
        /// </summary>
        public static bool RemoveBlacklist(uint wcid)
        {
            try
            {
                using (var context = new ShardDbContext())
                {
                    var existing = context.CreatureBlacklist.Find(wcid);
                    if (existing != null)
                    {
                        context.CreatureBlacklist.Remove(existing);
                        context.SaveChanges();
                    }
                }

                _cache.TryRemove(wcid, out _);
                log.Debug($"[Blacklist] Removed WCID {wcid}");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"[Blacklist] Failed to remove entry: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Update only the capture flag for a WCID
        /// </summary>
        public static bool SetNoCapture(uint wcid, bool noCapture, string reason, string addedBy)
        {
            if (_cache.TryGetValue(wcid, out var existing))
            {
                return AddBlacklist(wcid, noCapture, existing.NoShiny, reason ?? existing.Reason, addedBy);
            }
            return AddBlacklist(wcid, noCapture, false, reason, addedBy);
        }

        /// <summary>
        /// Update only the shiny flag for a WCID
        /// </summary>
        public static bool SetNoShiny(uint wcid, bool noShiny, string reason, string addedBy)
        {
            if (_cache.TryGetValue(wcid, out var existing))
            {
                return AddBlacklist(wcid, existing.NoCapture, noShiny, reason ?? existing.Reason, addedBy);
            }
            return AddBlacklist(wcid, false, noShiny, reason, addedBy);
        }

        /// <summary>
        /// Get all blacklist entries (for listing)
        /// </summary>
        public static List<(uint Wcid, bool NoCapture, bool NoShiny, string Reason)> GetAllEntries()
        {
            return _cache.Select(kvp => (kvp.Key, kvp.Value.NoCapture, kvp.Value.NoShiny, kvp.Value.Reason)).ToList();
        }

        /// <summary>
        /// Check status of a specific WCID
        /// </summary>
        public static (bool Exists, bool NoCapture, bool NoShiny, string Reason) CheckStatus(uint wcid)
        {
            if (_cache.TryGetValue(wcid, out var entry))
                return (true, entry.NoCapture, entry.NoShiny, entry.Reason);
            return (false, false, false, null);
        }
    }
}
