using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;
using Microsoft.EntityFrameworkCore;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Pet Registry System - manages account-based creature collection tracking
    /// </summary>
    public static class PetRegistryManager
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        private static volatile bool _tableCreated = false;
        private static readonly object _createLock = new object();

        /// <summary>
        /// Ensure the pet_registry table exists - call during server startup
        /// </summary>
        public static void EnsureTableCreated()
        {
            if (_tableCreated) return;

            lock (_createLock)
            {
                if (_tableCreated) return;

                try
                {
                    using (var context = new ShardDbContext())
                    {
                        // Check if table exists by trying to select from it
                        context.Database.ExecuteSqlRaw("SELECT 1 FROM pet_registry LIMIT 1");
                        log.Info("pet_registry table exists");
                    }
                }
                catch
                {
                    // Table doesn't exist, create it
                    log.Info("Creating pet_registry table...");
                    try
                    {
                        using (var context = new ShardDbContext())
                        {
                            context.Database.ExecuteSqlRaw(@"
                                CREATE TABLE `pet_registry` (
                                    `account_id` INT UNSIGNED NOT NULL,
                                    `wcid` INT UNSIGNED NOT NULL,
                                    `creature_name` VARCHAR(100) NOT NULL,
                                    `registered_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                    PRIMARY KEY (`account_id`, `wcid`),
                                    INDEX `IX_pet_registry_AccountId` (`account_id`)
                                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                            
                            log.Info("pet_registry table created successfully");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Failed to create pet_registry table: {ex.Message}");
                    }
                }

                _tableCreated = true;
            }
        }

        /// <summary>
        /// Register a captured essence to the player's account
        /// Called by EmoteManager when EmoteType.RegisterPetSkin is executed
        /// </summary>
        public static void RegisterEssence(Player player, WorldObject essence)
        {
            if (player == null || essence == null)
                return;

            // Validate it's a captured essence
            if (!MonsterCapture.IsCapturedAppearance(essence))
            {
                player.SendMessage("That doesn't appear to be a captured essence.");
                return;
            }

            // Extract creature info from the essence
            var wcid = essence.GetProperty(PropertyInt.CapturedCreatureWCID);
            var creatureName = essence.GetProperty(PropertyString.CapturedCreatureName);

            if (!wcid.HasValue || string.IsNullOrEmpty(creatureName))
            {
                player.SendMessage("This essence appears to be corrupted.");
                return;
            }

            var accountId = player.Account.AccountId;
            var creatureWcid = (uint)wcid.Value;

            // Check if already registered
            if (IsPetRegistered(accountId, creatureWcid))
            {
                player.SendMessage($"You've already registered {creatureName} in your Pet Log.");
                return;
            }

            // Register it
            if (RegisterPet(accountId, creatureWcid, creatureName))
            {
                var count = GetPetRegistryCount(accountId);
                player.SendMessage($"Registered: {creatureName}! Your Pet Log now has {count} unique species.");
                player.SendMessage("Use '/petslog' anytime to review your collection.");
                
                log.Debug($"[PetRegistry] {player.Name} (Account: {accountId}) registered {creatureName} (WCID: {creatureWcid})");
            }
            else
            {
                player.SendMessage("Failed to register the essence. Please try again.");
            }
        }

        /// <summary>
        /// Check if a WCID is already registered for an account
        /// </summary>
        public static bool IsPetRegistered(uint accountId, uint wcid)
        {
            using (var context = new ShardDbContext())
            {
                return context.PetRegistry.Any(p => p.AccountId == accountId && p.Wcid == wcid);
            }
        }

        /// <summary>
        /// Register a new pet to an account
        /// </summary>
        public static bool RegisterPet(uint accountId, uint wcid, string creatureName)
        {
            try
            {
                using (var context = new ShardDbContext())
                {
                    var entry = new PetRegistry
                    {
                        AccountId = accountId,
                        Wcid = wcid,
                        CreatureName = creatureName,
                        RegisteredAt = DateTime.UtcNow
                    };

                    context.PetRegistry.Add(entry);
                    context.SaveChanges();
                    return true;
                }
            }
            catch (Exception ex)
            {
                log.Error($"[PetRegistry] Failed to register pet: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get all registered pets for an account
        /// </summary>
        public static List<(uint Wcid, string Name, DateTime RegisteredAt)> GetPetRegistry(uint accountId)
        {
            using (var context = new ShardDbContext())
            {
                return context.PetRegistry
                    .Where(p => p.AccountId == accountId)
                    .OrderBy(p => p.RegisteredAt)
                    .Select(p => new ValueTuple<uint, string, DateTime>(p.Wcid, p.CreatureName, p.RegisteredAt))
                    .ToList();
            }
        }

        /// <summary>
        /// Get pet count for an account
        /// </summary>
        public static int GetPetRegistryCount(uint accountId)
        {
            using (var context = new ShardDbContext())
            {
                return context.PetRegistry.Count(p => p.AccountId == accountId);
            }
        }

        /// <summary>
        /// Get top accounts by pet count (for leaderboard)
        /// </summary>
        public static List<(uint AccountId, string AccountName, int Count)> GetTopPets(int limit = 25)
        {
            using (var context = new ShardDbContext())
            {
                var topAccounts = context.PetRegistry
                    .GroupBy(p => p.AccountId)
                    .Select(g => new { AccountId = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(limit)
                    .ToList();

                // Get account names from authentication database
                var results = new List<(uint AccountId, string AccountName, int Count)>();
                foreach (var entry in topAccounts)
                {
                    var accountName = DatabaseManager.Authentication.GetAccountById(entry.AccountId)?.AccountName ?? "Unknown";
                    results.Add((entry.AccountId, accountName, entry.Count));
                }

                return results;
            }
        }
    }
}
