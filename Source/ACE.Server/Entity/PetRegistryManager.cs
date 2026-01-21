using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum;
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
                                    `creature_type` INT UNSIGNED NULL,
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
            var creatureType = essence.GetProperty(PropertyInt.CapturedCreatureType);

            if (!wcid.HasValue || string.IsNullOrEmpty(creatureName))
            {
                player.SendMessage("This essence appears to be corrupted.");
                return;
            }

            var accountId = player.Account.AccountId;
            var creatureWcid = (uint)wcid.Value;

            // Check if already registered (by creature name, not WCID)
            if (IsPetRegistered(accountId, creatureName))
            {
                player.SendMessage($"You've already registered a {creatureName} in your Pet Log.");
                return;
            }

            // Register it
            if (RegisterPet(accountId, creatureWcid, creatureName, creatureType.HasValue ? (uint?)creatureType.Value : null))
            {
                var count = GetPetRegistryCount(accountId);
                player.SendMessage($"Registered: {creatureName}! Your Pet Log now has {count} unique species.");
                player.SendMessage("Use '/pets' anytime to review your collection.");
                
                log.Debug($"[PetRegistry] {player.Name} (Account: {accountId}) registered {creatureName} (WCID: {creatureWcid})");
                
                // Check if this is the first pet of this creature type - award QB
                if (creatureType.HasValue)
                {
                    var typeValue = (uint)creatureType.Value;
                    
                    // Check if account already has ANY pet of this type
                    // Note: We registered the pet above, so we need to check if count > 1
                    using (var context = new ShardDbContext())
                    {
                        var typeCount = context.PetRegistry
                            .Count(p => p.AccountId == accountId && p.CreatureType == typeValue);
                        
                        if (typeCount == 1) // This is the first of this type!
                        {
                            var typeName = ((CreatureType)typeValue).ToString();
                            player.QuestManager.Stamp($"CapturedEssence{typeName}");
                            player.SendMessage($"First {typeName} captured!", ChatMessageType.Broadcast);
                            
                            log.Debug($"[PetRegistry] {player.Name} captured their first {typeName} - QB awarded");
                        }
                    }
                }
                
                // Check milestone QB awards: 1, 5, 10, then every 25 (25, 50, 75, 100, 125, ...)
                bool isMilestone = count == 1 || count == 5 || count == 10 || (count >= 25 && count % 25 == 0);
                
                if (isMilestone)
                {
                    var questName = $"PetRegistry{count}";
                    
                    // Check if already awarded this milestone
                    if (!player.QuestManager.HasQuest(questName))
                    {
                        player.QuestManager.Stamp(questName);
                        player.SendMessage($"Milestone: {count} pets registered!", ChatMessageType.Broadcast);
                        
                        log.Debug($"[PetRegistry] {player.Name} reached milestone {count} pets - QB awarded");
                    }
                }
            }
            else
            {
                player.SendMessage("Failed to register the essence. Please try again.");
            }
        }

        /// <summary>
        /// Check if a creature name is already registered for an account
        /// </summary>
        public static bool IsPetRegistered(uint accountId, string creatureName)
        {
            using (var context = new ShardDbContext())
            {
                return context.PetRegistry.Any(p => p.AccountId == accountId && p.CreatureName == creatureName);
            }
        }

        /// <summary>
        /// Register a new pet to an account
        /// </summary>
        public static bool RegisterPet(uint accountId, uint wcid, string creatureName, uint? creatureType = null)
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
                        CreatureType = creatureType,
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
        /// Get unique pet count for an account (by distinct creature names)
        /// </summary>
        public static int GetPetRegistryCount(uint accountId)
        {
            using (var context = new ShardDbContext())
            {
                return context.PetRegistry
                    .Where(p => p.AccountId == accountId)
                    .Select(p => p.CreatureName)
                    .Distinct()
                    .Count();
            }
        }

        /// <summary>
        /// Get top accounts by unique pet count (for leaderboard) - counts distinct creature names
        /// Returns main character name (by most logins) instead of account name
        /// </summary>
        public static List<(uint AccountId, string CharacterName, int Count)> GetTopPets(int limit = 25)
        {
            using (var context = new ShardDbContext())
            {
                // Count distinct creature names per account
                var topAccounts = context.PetRegistry
                    .GroupBy(p => new { p.AccountId, p.CreatureName })
                    .Select(g => g.Key.AccountId)
                    .GroupBy(accountId => accountId)
                    .Select(g => new { AccountId = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(limit)
                    .ToList();

                // Get main character name (most logins) for each account
                var results = new List<(uint AccountId, string CharacterName, int Count)>();
                foreach (var entry in topAccounts)
                {
                    // Get character with most total_logins for this account
                    var mainCharacter = context.Character
                        .Where(c => c.AccountId == entry.AccountId && !c.IsDeleted)
                        .OrderByDescending(c => c.TotalLogins)
                        .Select(c => c.Name)
                        .FirstOrDefault() ?? "Unknown";
                    
                    results.Add((entry.AccountId, mainCharacter, entry.Count));
                }

                return results;
            }
        }

        /// <summary>
        /// Check if account has registered ANY pet of a specific creature type
        /// Used for "First Drudge", "First Banderling" QB awards
        /// </summary>
        public static bool HasCreatureType(uint accountId, uint creatureType)
        {
            using (var context = new ShardDbContext())
            {
                return context.PetRegistry.Any(p => p.AccountId == accountId && p.CreatureType == creatureType);
            }
        }
    }
}
