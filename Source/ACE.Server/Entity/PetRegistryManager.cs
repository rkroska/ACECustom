using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Database;
using ACE.Database.Models.Auth;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

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

                bool tableExists = false;
                try
                {
                    using (var context = new ShardDbContext())
                    {
                        // Check if table exists by trying to select from it
                        context.Database.ExecuteSqlRaw("SELECT 1 FROM pet_registry LIMIT 1");
                        tableExists = true;
                        log.Info("pet_registry table exists");
                    }
                }
                catch (Exception ex)
                {
                    // Table doesn't exist (or other error) - try to create it
                    log.Info($"pet_registry table check failed ({ex.GetType().Name}), attempting to create...");
                    try
                    {
                        using (var context = new ShardDbContext())
                        {
                            // Include is_shiny in PK to allow both shiny and non-shiny of same WCID
                            context.Database.ExecuteSqlRaw(@"
                                CREATE TABLE `pet_registry` (
                                    `account_id` INT UNSIGNED NOT NULL,
                                    `wcid` INT UNSIGNED NOT NULL,
                                    `creature_name` VARCHAR(100) NOT NULL,
                                    `creature_type` INT UNSIGNED NULL,
                                    `registered_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                    `is_shiny` TINYINT(1) NOT NULL DEFAULT 0,
                                    PRIMARY KEY (`account_id`, `wcid`, `is_shiny`),
                                    INDEX `IX_pet_registry_AccountId` (`account_id`)
                                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                            
                            tableExists = true;
                            log.Info("pet_registry table created successfully");
                        }
                    }
                    catch (Exception createEx)
                    {
                        log.Error($"Failed to create pet_registry table: {createEx.Message}");
                    }
                }

                // Only mark as created if table exists or was successfully created
                _tableCreated = tableExists;
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

            // Reject Hollow Essences (WCID 78780006) - they cannot be registered for QB
            if (essence.WeenieClassId == 78780006)
            {
                player.SendMessage("This hollow essence has already been registered. You cannot turn it in again.");
                return;
            }

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
            var capturedVariant = essence.GetProperty(PropertyInt.CapturedCreatureVariant);

            if (!wcid.HasValue || string.IsNullOrEmpty(creatureName))
            {
                player.SendMessage("This essence appears to be corrupted.");
                return;
            }

            var accountId = player.Account.AccountId;
            var creatureWcid = (uint)wcid.Value;
            var isShiny = capturedVariant.HasValue && capturedVariant.Value == (int)CreatureVariant.Shiny;

            // Check if already registered (by creature name AND shiny status)
            if (isShiny)
            {
                if (IsShinyPetRegistered(accountId, creatureName))
                {
                    player.SendMessage($"You've already registered a Shiny {creatureName} in your Pet Log.");
                    return;
                }
            }
            else
            {
                if (IsPetRegistered(accountId, creatureName))
                {
                    player.SendMessage($"You've already registered a {creatureName} in your Pet Log.");
                    return;
                }
            }

            // Register it with shiny flag
            if (RegisterPet(accountId, creatureWcid, creatureName, creatureType.HasValue ? (uint?)creatureType.Value : null, isShiny))
            {
                var count = GetPetRegistryCount(accountId);
                
                if (isShiny)
                    player.SendMessage($"Registered: Shiny {creatureName}! Your Pet Log now has {count} unique species.");
                else
                    player.SendMessage($"Registered: {creatureName}! Your Pet Log now has {count} unique species.");
                
                player.SendMessage("Use '/pets' anytime to review your collection.");
                
                log.Debug($"[PetRegistry] {player.Name} (Account: {accountId}) registered {(isShiny ? "Shiny " : "")}{creatureName} (WCID: {creatureWcid})");
                
                // Check if this is the first pet of this creature type - award QB
                if (creatureType.HasValue)
                {
                    var typeValue = (uint)creatureType.Value;
                    var typeName = ((CreatureType)typeValue).ToString();
                    
                    using (var context = new ShardDbContext())
                    {
                        // Check for first of this SPECIES (any, shiny or not)
                        var typeCount = context.PetRegistry
                            .Count(p => p.AccountId == accountId && p.CreatureType == typeValue);
                        
                        if (typeCount == 1) // This is the first of this species!
                        {
                            player.QuestManager.Stamp($"CapturedEssence{typeName}");
                            player.SendMessage($"First {typeName} captured!", ChatMessageType.Broadcast);
                            
                            log.Debug($"[PetRegistry] {player.Name} captured their first {typeName} - QB awarded");
                        }
                        
                        // Check for first SHINY of this SPECIES (additional QB)
                        if (isShiny)
                        {
                            var shinyTypeCount = context.PetRegistry
                                .Count(p => p.AccountId == accountId && p.CreatureType == typeValue && p.IsShiny);
                            
                            if (shinyTypeCount == 1) // This is the first SHINY of this species!
                            {
                                player.QuestManager.Stamp($"ShinyEssence{typeName}");
                                player.SendMessage($"First Shiny {typeName} captured!", ChatMessageType.Broadcast);
                                
                                log.Debug($"[PetRegistry] {player.Name} captured their first Shiny {typeName} - QB awarded");
                            }
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
        /// Check if a non-shiny creature name is already registered for an account
        /// </summary>
        public static bool IsPetRegistered(uint accountId, string creatureName)
        {
            using (var context = new ShardDbContext())
            {
                return context.PetRegistry.Any(p => p.AccountId == accountId && p.CreatureName == creatureName && !p.IsShiny);
            }
        }

        /// <summary>
        /// Check if a shiny creature name is already registered for an account
        /// </summary>
        public static bool IsShinyPetRegistered(uint accountId, string creatureName)
        {
            using (var context = new ShardDbContext())
            {
                return context.PetRegistry.Any(p => p.AccountId == accountId && p.CreatureName == creatureName && p.IsShiny);
            }
        }

        /// <summary>
        /// Register a new pet to an account (upsert to handle duplicate entries)
        /// </summary>
        public static bool RegisterPet(uint accountId, uint wcid, string creatureName, uint? creatureType = null, bool isShiny = false)
        {
            try
            {
                using (var context = new ShardDbContext())
                {
                    // Check for existing entry with same account/wcid/isShiny (composite key)
                    var existing = context.PetRegistry
                        .FirstOrDefault(p => p.AccountId == accountId && p.Wcid == wcid && p.IsShiny == isShiny);
                    
                    if (existing != null)
                    {
                        // Update existing entry (upsert)
                        existing.CreatureName = creatureName;
                        existing.CreatureType = creatureType;
                        existing.RegisteredAt = DateTime.UtcNow;
                    }
                    else
                    {
                        // Add new entry
                        context.PetRegistry.Add(new PetRegistry
                        {
                            AccountId = accountId,
                            Wcid = wcid,
                            CreatureName = creatureName,
                            CreatureType = creatureType,
                            RegisteredAt = DateTime.UtcNow,
                            IsShiny = isShiny
                        });
                    }
                    
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
        /// Get only shiny pets for a specific account, ordered by registration date.
        /// </summary>
        /// <param name="accountId">The account ID to retrieve shiny pets for.</param>
        /// <returns>A list of tuples containing WCID, creature name, and registration timestamp.</returns>
        public static List<(uint Wcid, string Name, DateTime RegisteredAt)> GetShinyRegistry(uint accountId)
        {
            using (var context = new ShardDbContext())
            {
                return context.PetRegistry
                    .Where(p => p.AccountId == accountId && p.IsShiny)
                    .OrderBy(p => p.RegisteredAt)
                    .Select(p => new ValueTuple<uint, string, DateTime>(p.Wcid, p.CreatureName, p.RegisteredAt))
                    .ToList();
            }
        }

        public static List<PetRegistryEntry> GetFullPetRegistry(uint accountId)
        {
             using (var context = new ShardDbContext())
            {
                return context.PetRegistry
                    .Where(p => p.AccountId == accountId)
                    .Select(p => new PetRegistryEntry
                    {
                        Wcid = p.Wcid,
                        CreatureName = p.CreatureName,
                        CreatureType = (CreatureType?)p.CreatureType,
                        IsShiny = p.IsShiny,
                        RegisteredAt = p.RegisteredAt
                    })
                    .ToList();
            }
        }

        public static List<PropertiesBookPageData> GenerateMonsterDexPages(Player player, int maxChars)
        {
            // Fallback if maxChars is invalid (e.g. 0 from empty property)
            if (maxChars < 100) maxChars = 1000;

            var registry = GetFullPetRegistry(player.Account.AccountId);
            
            // Group by Species (CreatureType)
            var speciesGroups = registry
                .GroupBy(r => r.CreatureType)
                .OrderBy(g => g.Key.HasValue ? g.Key.Value.ToString() : "Unknown"); 

            var pages = new List<PropertiesBookPageData>();

            foreach (var group in speciesGroups)
            {
                var speciesName = group.Key.HasValue ? System.Text.RegularExpressions.Regex.Replace(group.Key.Value.ToString(), "(\\B[A-Z])", " $1") : "Unknown";

                // Define Header and Legend
                var headerText = new System.Text.StringBuilder();
                headerText.Append($"Species: {speciesName}\n"); 
                headerText.Append("-----------------------------\n");
                headerText.Append("[X] Normal  [*] Shiny\n");
                headerText.Append("-----------------------------\n\n");
 
                // Initialize first page for this species
                var pageText = new System.Text.StringBuilder();
                pageText.Append(headerText);

                int pageNumber = 1;

                // Group by normalized name (remove "Shiny " prefix) to merge variants
                var variants = group
                    .Select(r => new { 
                        Original = r, 
                        NormalizedName = r.CreatureName.StartsWith("Shiny ") ? r.CreatureName.Substring(6) : r.CreatureName 
                    })
                    .GroupBy(x => x.NormalizedName)
                    .OrderBy(v => v.Key)
                    .ToList();

                foreach (var variant in variants)
                {
                    // Check if we have any entry that is NOT shiny (Normal)
                    bool hasNormal = variant.Any(x => !x.Original.IsShiny);
                    // Check if we have any entry that IS shiny
                    bool hasShiny = variant.Any(x => x.Original.IsShiny);

                    string normalIcon = hasNormal ? "[X]" : "[  ]";
                    string shinyIcon = hasShiny ? "[*]" : "[  ]";

                    string line = $"{normalIcon} {shinyIcon} {variant.Key}\n";

                    // Check if adding this line would exceed the limit
                    if (pageText.Length + line.Length > maxChars)
                    {
                        // Save current page
                        string title = pageNumber == 1 ? speciesName : $"{speciesName} ({pageNumber})";
                        
                        pages.Add(new PropertiesBookPageData
                        {
                            AuthorId = 0xFFFFFFFF,
                            AuthorName = title,
                            AuthorAccount = "",
                            IgnoreAuthor = true,
                            PageText = pageText.ToString()
                        });

                        // Start new page with header
                        pageText.Clear();
                        pageText.Append(headerText);
                        
                        pageNumber++;
                    }

                    pageText.Append(line);
                }

                // Add the final page for this species
                if (pageText.Length > 0)
                {
                    string title = pageNumber == 1 ? speciesName : $"{speciesName} ({pageNumber})";

                    pages.Add(new PropertiesBookPageData
                    {
                        AuthorId = 0xFFFFFFFF, // System
                        AuthorName = title,
                        AuthorAccount = "",
                        IgnoreAuthor = true,
                        PageText = pageText.ToString()
                    });
                }
            }

            if (pages.Count == 0)
            {
                 pages.Add(new PropertiesBookPageData
                {
                    AuthorId = 0xFFFFFFFF,
                    AuthorName = "Monster-Dex",
                    AuthorAccount = "",
                    IgnoreAuthor = true,
                    PageText = "No creatures captured yet!\n\nGo out and use a Siphon Lens to capture creature essences!"
                });
            }

            return pages;
        }

        /// <summary>
        /// Get unique pet count for an account (by distinct creature names)
        /// </summary>
        public static int GetPetRegistryCount(uint accountId)
        {
            using (var context = new ShardDbContext())
            {
                // Retrieve all names first, then normalize in memory since SQL might not handle complex substring logic identically or easily
                // OR use client-side evaluation if EF Core allows. 
                // For simplicity and correctness with "Shiny " string logic, fetching names is safe if lists aren't massive.
                // However, doing it in memory is safer for string manipulation.
                
                var allNames = context.PetRegistry
                    .Where(p => p.AccountId == accountId)
                    .Select(p => p.CreatureName)
                    .ToList();

                return allNames
                    .Select(n => n.StartsWith("Shiny ") ? n.Substring(6) : n)
                    .Distinct()
                    .Count();
            }
        }

        /// <summary>
        /// Character biota ids excluded from leaderboards via <see cref="PropertyBool.ExcludeFromLeaderboards"/> (9011).
        /// </summary>
        private static HashSet<uint> LoadExcludedFromLeaderboardCharacterIds(ShardDbContext context)
        {
            return context.BiotaPropertiesBool
                .AsNoTracking()
                .Where(b => b.Type == (ushort)PropertyBool.ExcludeFromLeaderboards && b.Value)
                .Select(b => b.ObjectId)
                .ToHashSet();
        }

        /// <summary>Staff and currently-banned accounts hidden from pet-species leaderboards.</summary>
        private static HashSet<uint> LoadLeaderboardHiddenAccountIds(ShardDbContext shardContext)
        {
            var hidden = LoadAccountIdsWithAnyExcludedCharacter(shardContext, LoadExcludedFromLeaderboardCharacterIds(shardContext));

            using var authContext = new AuthDbContext();
            var now = DateTime.UtcNow;
            var authHidden = authContext.Account
                .AsNoTracking()
                .Where(a => a.AccessLevel > 0 || (a.BanExpireTime != null && a.BanExpireTime > now))
                .Select(a => a.AccountId)
                .ToList();

            foreach (var accountId in authHidden)
                hidden.Add(accountId);

            return hidden;
        }

        /// <summary>Accounts that have at least one non-deleted excluded character (pet/QB-style account leaderboards hide the whole account).</summary>
        private static HashSet<uint> LoadAccountIdsWithAnyExcludedCharacter(ShardDbContext context, HashSet<uint> excludedCharacterObjectIds)
        {
            if (excludedCharacterObjectIds.Count == 0)
                return new HashSet<uint>();

            return context.Character
                .AsNoTracking()
                .Where(c => !c.IsDeleted && excludedCharacterObjectIds.Contains(c.Id))
                .Select(c => c.AccountId)
                .Distinct()
                .ToHashSet();
        }

        private static string MainCharacterNameHighestLogins(ShardDbContext context, uint accountId)
        {
            return LoadMainCharacterNames(context, new[] { accountId }).TryGetValue(accountId, out var name)
                ? name
                : "Unknown";
        }

        private static Dictionary<uint, string> LoadMainCharacterNames(ShardDbContext context, IEnumerable<uint> accountIds)
        {
            var ids = accountIds.Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<uint, string>();

            return context.Character
                .AsNoTracking()
                .Where(c => ids.Contains(c.AccountId) && !c.IsDeleted)
                .Select(c => new { c.AccountId, c.Name, c.TotalLogins })
                .ToList()
                .GroupBy(c => c.AccountId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(c => c.TotalLogins).Select(c => c.Name).FirstOrDefault() ?? "Unknown");
        }

        /// <summary>
        /// All accounts ordered by pet-species count (same rules as the leaderboard), excluding leaderboard-hidden accounts.
        /// </summary>
        private static List<(uint AccountId, int Count)> BuildOrderedPetSpeciesCounts(ShardDbContext context, bool shinyOnly, HashSet<uint> excludedAccounts)
        {
            var ordered = new List<(uint AccountId, int Count)>();

            if (shinyOnly)
            {
                var ranked = context.PetRegistry
                    .Where(p => p.IsShiny)
                    .GroupBy(p => new { p.AccountId, p.CreatureName })
                    .Select(g => g.Key.AccountId)
                    .GroupBy(accountId => accountId)
                    .Select(g => new { AccountId = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                foreach (var row in ranked)
                {
                    if (excludedAccounts.Contains(row.AccountId))
                        continue;
                    ordered.Add((row.AccountId, row.Count));
                }

                return ordered;
            }

            var rawData = context.PetRegistry
                .Select(p => new { p.AccountId, p.CreatureName })
                .ToList();

            var rankedNonShiny = rawData
                .Select(x => new
                {
                    x.AccountId,
                    NormalizedName = x.CreatureName.StartsWith("Shiny ") ? x.CreatureName.Substring(6) : x.CreatureName
                })
                .GroupBy(p => new { p.AccountId, p.NormalizedName })
                .Select(g => g.Key.AccountId)
                .GroupBy(accountId => accountId)
                .Select(g => new { AccountId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            foreach (var row in rankedNonShiny)
            {
                if (excludedAccounts.Contains(row.AccountId))
                    continue;
                ordered.Add((row.AccountId, row.Count));
            }

            return ordered;
        }

        private static bool IsStaffLeaderboardCharacter(string name) =>
            !string.IsNullOrEmpty(name) && name.StartsWith("+");

        /// <summary>
        /// Global rank on the pet or shiny species leaderboard for an account, if the account appears on that board.
        /// </summary>
        public static bool TryGetPetSpeciesPlacement(uint accountId, bool shinyOnly, out int rank, out int count, out string characterName)
        {
            rank = 0;
            count = 0;
            characterName = null;

            using (var context = new ShardDbContext())
            {
                var excludedAccounts = LoadLeaderboardHiddenAccountIds(context);
                var ordered = BuildOrderedPetSpeciesCounts(context, shinyOnly, excludedAccounts);
                var mainCharacters = LoadMainCharacterNames(context, ordered.Select(o => o.AccountId));

                var visibleRank = 0;
                for (var i = 0; i < ordered.Count; i++)
                {
                    if (!mainCharacters.TryGetValue(ordered[i].AccountId, out var mainCharacter))
                        mainCharacter = "Unknown";
                    if (IsStaffLeaderboardCharacter(mainCharacter))
                        continue;

                    visibleRank++;
                    if (ordered[i].AccountId != accountId)
                        continue;

                    rank = visibleRank;
                    count = ordered[i].Count;
                    characterName = mainCharacter;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get top accounts by unique pet count (for leaderboard) - counts distinct creature names
        /// Returns main character name (by most logins) instead of account name
        /// </summary>
        public static List<(uint AccountId, string CharacterName, int Count)> GetTopPets(int limit = 25)
        {
            using (var context = new ShardDbContext())
            {
                var excludedAccounts = LoadLeaderboardHiddenAccountIds(context);
                var ordered = BuildOrderedPetSpeciesCounts(context, shinyOnly: false, excludedAccounts);
                var mainCharacters = LoadMainCharacterNames(context, ordered.Select(o => o.AccountId));

                var results = new List<(uint AccountId, string CharacterName, int Count)>();
                foreach (var entry in ordered)
                {
                    if (results.Count >= limit)
                        break;

                    if (!mainCharacters.TryGetValue(entry.AccountId, out var mainCharacter))
                        mainCharacter = "Unknown";
                    if (IsStaffLeaderboardCharacter(mainCharacter))
                        continue;

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

        /// <summary>
        /// Get top accounts by shiny pet count (for leaderboard) - counts distinct shiny creature names
        /// Returns main character name (by most logins) instead of account name
        /// </summary>
        public static List<(uint AccountId, string CharacterName, int Count)> GetTopShinies(int limit = 25)
        {
            using (var context = new ShardDbContext())
            {
                var excludedAccounts = LoadLeaderboardHiddenAccountIds(context);
                var ordered = BuildOrderedPetSpeciesCounts(context, shinyOnly: true, excludedAccounts);
                var mainCharacters = LoadMainCharacterNames(context, ordered.Select(o => o.AccountId));

                var results = new List<(uint AccountId, string CharacterName, int Count)>();
                foreach (var entry in ordered)
                {
                    if (results.Count >= limit)
                        break;

                    if (!mainCharacters.TryGetValue(entry.AccountId, out var mainCharacter))
                        mainCharacter = "Unknown";
                    if (IsStaffLeaderboardCharacter(mainCharacter))
                        continue;

                    results.Add((entry.AccountId, mainCharacter, entry.Count));
                }

                return results;
            }
        }
    }
}
