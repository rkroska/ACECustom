using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Server.WorldObjects;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Entity.Enum;
using log4net;
using Microsoft.EntityFrameworkCore;

namespace ACE.Server.Managers
{
    public static class TransferLogger
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly object _lock = new object();
        private static readonly HashSet<string> _blacklistedPlayers = new HashSet<string>();
        private static readonly HashSet<string> _blacklistedAccounts = new HashSet<string>();
        
        // Transfer type constants
        public const string TransferTypeBankTransfer = "Bank Transfer";
        public const string TransferTypeDirectGive = "Direct Give";
        public const string TransferTypeTrade = "Trade";
        public const string TransferTypeGroundPickup = "Ground Pickup";
        
        // Value calculation constants
        private const int DefaultItemValue = 100;
        private const int ChunkValue = 1000;
        private const int IngotValue = 500;
        
        // Display limits
        private const int MaxTransferLogDisplayCount = 20;
        private const int MaxTransferPatternDisplayCount = 20;
        
        // Retry and delay constants
        private const int DatabaseRetryDelayMs = 100;

        private static void SendAdminMessage(string message)
        {
            try
            {
                var adminPlayers = PlayerManager.GetAllPlayers()
                    .Where(p => p.Account != null && (uint)p.Account.AccessLevel >= (uint)AccessLevel.Advocate)
                    .OfType<Player>()
                    .ToList();

                foreach (var admin in adminPlayers)
                {
                    admin.Session.Network.EnqueueSend(new GameMessageSystemChat($"[TRANSFER LOG] {message}", ChatMessageType.System));
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error sending admin message: {ex.Message}");
            }
        }

        // Configuration
        public static TransferMonitoringConfig Config { get; private set; } = TransferMonitoringConfig.Default;
        private static bool _databaseMigrated = false;
        private static bool _trackedItemsInitialized = false;

        /// <summary>
        /// Initialize transfer monitoring system from database on server startup
        /// </summary>
        public static void InitializeTransferMonitoring()
        {
            if (_trackedItemsInitialized) return;
            
            try
            {
                log.Info("Initializing transfer monitoring system from database...");
                
                // Ensure all database tables exist with correct schema
                EnsureDatabaseMigrated();
                
                // Load configuration from database
                LoadConfigurationFromDatabase();
                
                // Load tracked items from database
                GetTrackedItems(); // This loads from DB and updates Config.TrackedItems
                
                _trackedItemsInitialized = true;
                log.Info($"Loaded {Config.TrackedItems?.Count ?? 0} tracked items and configuration from database");
            }
            catch (Exception ex)
            {
                log.Error($"Error initializing transfer monitoring system: {ex.Message}");
                // Continue with defaults if database is not available yet
            }
        }

        public static void EnsureDatabaseMigrated()
        {
            if (_databaseMigrated) return;

            try
            {
                using (var context = new ShardDbContext())
                {
                    log.Info("Running database migration for all transfer monitoring tables...");
                    
                    // Create all transfer monitoring tables
                    CreateTransferLogsTable(context);
                    CreateTransferSummariesTable(context);
                    CreateTrackedItemsTable(context);
                    CreateTransferMonitoringConfigsTable(context);
                    CreateBankCommandBlacklistTable(context);
                    
                    log.Info("Database migration completed successfully");
                    _databaseMigrated = true;
                }
            }
            catch (Exception ex)
            {
                log.Error($"Database migration failed: {ex.Message}");
                // Don't set _databaseMigrated = true so we can retry later
            }
        }

        private static void CreateTransferLogsTable(ShardDbContext context)
        {
            try
            {
                // Check if table exists by trying to select from it
                context.Database.ExecuteSqlRaw("SELECT 1 FROM transfer_logs LIMIT 1");
                log.Info("transfer_logs table exists - checking for IP address columns...");
                
                // Try to add missing columns to existing table (will fail silently if they already exist)
                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        ALTER TABLE `transfer_logs` 
                        ADD COLUMN IF NOT EXISTS `FromAccountCreatedDate` datetime(6) DEFAULT NULL AFTER `Timestamp`,
                        ADD COLUMN IF NOT EXISTS `ToAccountCreatedDate` datetime(6) DEFAULT NULL AFTER `FromAccountCreatedDate`,
                        ADD COLUMN IF NOT EXISTS `FromCharacterCreatedDate` datetime(6) DEFAULT NULL AFTER `ToAccountCreatedDate`,
                        ADD COLUMN IF NOT EXISTS `ToCharacterCreatedDate` datetime(6) DEFAULT NULL AFTER `FromCharacterCreatedDate`,
                        ADD COLUMN IF NOT EXISTS `FromPlayerIP` varchar(45) DEFAULT NULL AFTER `AdditionalData`,
                        ADD COLUMN IF NOT EXISTS `ToPlayerIP` varchar(45) DEFAULT NULL AFTER `FromPlayerIP`;");
                    
                    log.Info("Missing columns added to transfer_logs");
                }
                catch (Exception ex)
                {
                    log.Info($"Missing columns may already exist: {ex.Message}");
                }
            }
            catch
            {
                log.Info("Creating transfer_logs table with IP address columns...");
                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        CREATE TABLE `transfer_logs` (
                            `Id` int(11) NOT NULL AUTO_INCREMENT,
                            `TransferType` varchar(255) NOT NULL,
                            `FromPlayerName` varchar(255) NOT NULL,
                            `FromPlayerAccount` varchar(255) DEFAULT NULL,
                            `ToPlayerName` varchar(255) NOT NULL,
                            `ToPlayerAccount` varchar(255) DEFAULT NULL,
                            `ItemName` varchar(255) NOT NULL,
                            `Quantity` int(11) NOT NULL,
                            `Timestamp` datetime(6) NOT NULL,
                            `FromAccountCreatedDate` datetime(6) DEFAULT NULL,
                            `ToAccountCreatedDate` datetime(6) DEFAULT NULL,
                            `FromCharacterCreatedDate` datetime(6) DEFAULT NULL,
                            `ToCharacterCreatedDate` datetime(6) DEFAULT NULL,
                            `AdditionalData` varchar(1000) DEFAULT NULL,
                            `FromPlayerIP` varchar(45) DEFAULT NULL,
                            `ToPlayerIP` varchar(45) DEFAULT NULL,
                            PRIMARY KEY (`Id`),
                            KEY `IX_transfer_logs_FromPlayerName` (`FromPlayerName`),
                            KEY `IX_transfer_logs_ToPlayerName` (`ToPlayerName`),
                            KEY `IX_transfer_logs_Timestamp` (`Timestamp`),
                            KEY `IX_transfer_logs_FromPlayerAccount` (`FromPlayerAccount`),
                            KEY `IX_transfer_logs_ToPlayerAccount` (`ToPlayerAccount`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                    
                    log.Info("transfer_logs table created successfully");
                }
                catch (Exception ex)
                {
                    log.Info($"Table may already exist: {ex.Message}");
                }
            }
        }

        private static void CreateTransferSummariesTable(ShardDbContext context)
        {
            try
            {
                context.Database.ExecuteSqlRaw("SELECT 1 FROM transfer_summaries LIMIT 1");
                log.Info("transfer_summaries table exists");
            }
            catch
            {
                log.Info("Creating transfer_summaries table...");
                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        CREATE TABLE `transfer_summaries` (
                            `Id` int(11) NOT NULL AUTO_INCREMENT,
                            `FromPlayerName` varchar(255) NOT NULL,
                            `FromPlayerAccount` varchar(255) DEFAULT NULL,
                            `ToPlayerName` varchar(255) NOT NULL,
                            `ToPlayerAccount` varchar(255) DEFAULT NULL,
                            `TransferType` varchar(255) NOT NULL,
                            `TotalTransfers` int(11) NOT NULL DEFAULT '0',
                            `TotalQuantity` bigint(20) NOT NULL DEFAULT '0',
                            `TotalValue` bigint(20) NOT NULL DEFAULT '0',
                            `FirstTransfer` datetime(6) NOT NULL,
                            `LastTransfer` datetime(6) NOT NULL,
                            `SuspiciousTransfers` int(11) NOT NULL DEFAULT '0',
                            `IsSuspicious` tinyint(1) NOT NULL DEFAULT '0',
                            `CreatedDate` datetime(6) NOT NULL,
                            `UpdatedDate` datetime(6) NOT NULL,
                            PRIMARY KEY (`Id`),
                            KEY `IX_transfer_summaries_FromPlayerName` (`FromPlayerName`),
                            KEY `IX_transfer_summaries_ToPlayerName` (`ToPlayerName`),
                            KEY `IX_transfer_summaries_LastTransfer` (`LastTransfer`),
                            KEY `IX_transfer_summaries_IsSuspicious` (`IsSuspicious`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                    
                    log.Info("transfer_summaries table created successfully");
                }
                catch (Exception ex)
                {
                    log.Info($"Table may already exist: {ex.Message}");
                }
            }
        }



        private static void CreateTrackedItemsTable(ShardDbContext context)
        {
            try
            {
                context.Database.ExecuteSqlRaw("SELECT 1 FROM tracked_items LIMIT 1");
                log.Info("tracked_items table exists");
            }
            catch
            {
                log.Info("Creating tracked_items table...");
                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        CREATE TABLE `tracked_items` (
                            `Id` int(11) NOT NULL AUTO_INCREMENT,
                            `ItemName` varchar(255) NOT NULL,
                            `CreatedDate` datetime(6) NOT NULL,
                            `UpdatedDate` datetime(6) NOT NULL,
                            `IsActive` tinyint(1) NOT NULL DEFAULT '1',
                            PRIMARY KEY (`Id`),
                            UNIQUE KEY `IX_tracked_items_ItemName` (`ItemName`),
                            KEY `IX_tracked_items_IsActive` (`IsActive`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                    
                    log.Info("tracked_items table created successfully");
                }
                catch (Exception ex)
                {
                    log.Info($"Table may already exist: {ex.Message}");
                }
            }
        }

        private static void CreateTransferMonitoringConfigsTable(ShardDbContext context)
        {
            try
            {
                context.Database.ExecuteSqlRaw("SELECT 1 FROM transfer_monitoring_configs LIMIT 1");
                log.Info("transfer_monitoring_configs table exists");
            }
            catch
            {
                log.Info("Creating transfer_monitoring_configs table...");
                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        CREATE TABLE `transfer_monitoring_configs` (
                            `Id` int(11) NOT NULL AUTO_INCREMENT,
                            `SuspiciousTransferThreshold` int(11) NOT NULL DEFAULT '100000',
                            `TimeWindowHours` int(11) NOT NULL DEFAULT '24',
                            `PatternDetectionThreshold` int(11) NOT NULL DEFAULT '10',
                            `EnableTransferLogging` tinyint(1) NOT NULL DEFAULT '1',
                            `EnableSuspiciousDetection` tinyint(1) NOT NULL DEFAULT '1',
                            `EnableAdminNotifications` tinyint(1) NOT NULL DEFAULT '1',
                            `EnableTransferSummaries` tinyint(1) NOT NULL DEFAULT '1',
                            `EnableTransferLogs` tinyint(1) NOT NULL DEFAULT '1',
                            `EnableItemTracking` tinyint(1) NOT NULL DEFAULT '1',
                            `TrackAllItems` tinyint(1) NOT NULL DEFAULT '0',
                            `CreatedDate` datetime(6) NOT NULL,
                            `UpdatedDate` datetime(6) NOT NULL,
                            PRIMARY KEY (`Id`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                    
                    log.Info("transfer_monitoring_configs table created successfully");
                    
                    // Insert default configuration
                    context.Database.ExecuteSqlRaw(@"
                        INSERT INTO `transfer_monitoring_configs` (
                            `SuspiciousTransferThreshold`, `TimeWindowHours`, `PatternDetectionThreshold`,
                            `EnableTransferLogging`, `EnableSuspiciousDetection`, `EnableAdminNotifications`,
                            `EnableTransferSummaries`, `EnableTransferLogs`, `EnableItemTracking`, `TrackAllItems`,
                            `CreatedDate`, `UpdatedDate`
                        ) VALUES (
                            100000, 24, 10, 1, 1, 1, 1, 1, 1, 0, NOW(), NOW()
                        )");
                    
                    log.Info("Default transfer monitoring configuration inserted");
                }
                catch (Exception ex)
                {
                    log.Info($"Table may already exist: {ex.Message}");
                }
            }
        }

        private static void CreateBankCommandBlacklistTable(ShardDbContext context)
        {
            try
            {
                context.Database.ExecuteSqlRaw("SELECT 1 FROM transfer_blacklist LIMIT 1");
                log.Info("transfer_blacklist table exists");
            }
            catch
            {
                log.Info("Creating transfer_blacklist table...");
                try
                {
                    context.Database.ExecuteSqlRaw(@"
                        CREATE TABLE `transfer_blacklist` (
                            `Id` int(11) NOT NULL AUTO_INCREMENT,
                            `PlayerName` varchar(255) NOT NULL,
                            `AccountName` varchar(255) DEFAULT NULL,
                            `Reason` varchar(255) NOT NULL,
                            `AddedBy` varchar(255) NOT NULL,
                            `CreatedDate` datetime(6) NOT NULL,
                            `ExpiryDate` datetime(6) DEFAULT NULL,
                            `IsActive` tinyint(1) NOT NULL DEFAULT '1',
                            PRIMARY KEY (`Id`),
                            KEY `IX_transfer_blacklist_PlayerName` (`PlayerName`),
                            KEY `IX_transfer_blacklist_AccountName` (`AccountName`),
                            KEY `IX_transfer_blacklist_IsActive` (`IsActive`),
                            KEY `IX_transfer_blacklist_ExpiryDate` (`ExpiryDate`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                    
                    log.Info("transfer_blacklist table created successfully");
                }
                catch (Exception ex)
                {
                    log.Info($"Table may already exist: {ex.Message}");
                }
            }
        }

        // Legacy properties for backward compatibility
        public static int SuspiciousTransferThreshold => Config.SuspiciousTransferThreshold;
        public static int SuspiciousTransferTimeWindowHours => Config.TimeWindowHours;
        public static int PatternDetectionThreshold => Config.PatternDetectionThreshold;

        /// <summary>
        /// Extract IP address from EndPoint, removing port information
        /// </summary>
        private static string GetPlayerIP(Player player)
        {
            if (player?.Session?.EndPoint == null)
                return null;

            var endPointString = player.Session.EndPoint.ToString();
            var colonIndex = endPointString.LastIndexOf(':');
            
            if (colonIndex > 0)
            {
                return endPointString.Substring(0, colonIndex);
            }
            
            return endPointString;
        }

        private static DateTime? GetCharacterCreationDate(Player player)
        {
            if (player == null)
                return null;

            // PropertyInt.CreationTimestamp (98) contains the Unix timestamp
            var creationTimestamp = player.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.CreationTimestamp);
            if (creationTimestamp.HasValue)
            {
                // Convert Unix timestamp to DateTime
                return DateTimeOffset.FromUnixTimeSeconds(creationTimestamp.Value).DateTime;
            }
            
            return null;
        }

        public static void LogBankTransfer(Player fromPlayer, string toPlayerName, string itemName, long quantity, string transferType)
        {
            try
            {
                log.Info($"LogBankTransfer called: {fromPlayer.Name} -> {toPlayerName}, {itemName} x{quantity}");
                
                // Get the destination player if they're online
                var toPlayer = PlayerManager.GetAllPlayers().FirstOrDefault(p => p.Name == toPlayerName) as Player;
                
                if (ShouldSkipLogging(fromPlayer, toPlayer))
                {
                    log.Info("Skipping logging due to blacklist");
                    return;
                }

                // Ensure database is migrated before logging
                EnsureDatabaseMigrated();

                // Only log if item tracking is enabled and this item should be tracked
                if (!Config.EnableItemTracking || (!Config.TrackAllItems && !ShouldTrackItem(itemName)))
                {
                    log.Info($"Skipping logging for untracked item: {itemName}");
                    return;
                }

                var transferLog = new TransferLog
                {
                    TransferType = transferType,
                    FromPlayerName = fromPlayer.Name,
                    FromPlayerAccount = fromPlayer.Account?.AccountName ?? "Unknown",
                    ToPlayerName = toPlayerName,
                    ToPlayerAccount = toPlayer?.Account?.AccountName ?? "Unknown",
                    ItemName = itemName,
                    Quantity = (int)quantity,
                    Timestamp = DateTime.UtcNow,
                    FromAccountCreatedDate = fromPlayer.Account?.CreateTime,
                    ToAccountCreatedDate = toPlayer?.Account?.CreateTime,
                    FromCharacterCreatedDate = GetCharacterCreationDate(fromPlayer),
                    ToCharacterCreatedDate = GetCharacterCreationDate(toPlayer),
                    AdditionalData = null,
                    FromPlayerIP = GetPlayerIP(fromPlayer),
                    ToPlayerIP = GetPlayerIP(toPlayer)
                };

                // Process in background thread to prevent blocking the game thread
                Task.Run(() => ProcessTransferLogBackground(transferLog));
            }
            catch (DbUpdateException ex)
            {
                log.Error($"Database error during transfer logging: {ex.Message}");
                // Continue without logging - don't break the transfer
            }
            catch (InvalidOperationException ex)
            {
                log.Error($"Invalid operation during transfer logging: {ex.Message}");
                // Continue without logging - don't break the transfer
            }
            catch (Exception ex)
            {
                log.Error($"Unexpected error during transfer logging: {ex.Message}");
                log.Error($"Stack trace: {ex.StackTrace}");
                // Continue without logging - don't break the transfer
            }
        }

        public static void LogDirectGive(Player fromPlayer, Player toPlayer, WorldObject item, int quantity)
        {
            try
            {
                log.Info($"LogDirectGive called: {fromPlayer.Name} -> {toPlayer.Name}, {item.Name} x{quantity}");
                
                if (ShouldSkipLogging(fromPlayer, toPlayer))
                {
                    log.Info("Skipping logging due to blacklist");
                    return;
                }

                // Ensure database is migrated before logging
                EnsureDatabaseMigrated();

                // Only log if item tracking is enabled and this item should be tracked
                if (!Config.EnableItemTracking || !ShouldTrackItem(item.Name))
                {
                    log.Info($"Skipping logging for untracked item: {item.Name}");
                    return;
                }

                var transferLog = new TransferLog
                {
                    TransferType = TransferTypeDirectGive,
                    FromPlayerName = fromPlayer.Name,
                    FromPlayerAccount = fromPlayer.Account?.AccountName ?? "Unknown",
                    ToPlayerName = toPlayer.Name,
                    ToPlayerAccount = toPlayer.Account?.AccountName ?? "Unknown",
                    ItemName = item.Name,
                    Quantity = quantity,
                    Timestamp = DateTime.UtcNow,
                    FromAccountCreatedDate = fromPlayer.Account?.CreateTime,
                    ToAccountCreatedDate = toPlayer.Account?.CreateTime,
                    FromCharacterCreatedDate = GetCharacterCreationDate(fromPlayer),
                    ToCharacterCreatedDate = GetCharacterCreationDate(toPlayer),
                    AdditionalData = $"Item GUID: {item.Guid}",
                    FromPlayerIP = GetPlayerIP(fromPlayer),
                    ToPlayerIP = GetPlayerIP(toPlayer)
                };

                // Process in background thread to prevent blocking the game thread
                Task.Run(() => ProcessTransferLogBackground(transferLog));
            }
            catch (Exception ex)
            {
                log.Error($"Direct give logging failed: {ex.Message}");
                log.Error($"Stack trace: {ex.StackTrace}");
                // Continue without logging - don't break the transfer
            }
        }

        public static void LogTrade(Player player1, Player player2, List<WorldObject> player1Escrow, List<WorldObject> player2Escrow)
        {
            try
            {
                log.Info($"LogTrade called: {player1.Name} <-> {player2.Name}, {player1Escrow.Count} items each");
                
                if (ShouldSkipLogging(player1, player2))
                {
                    log.Info("Skipping logging due to blacklist");
                    return;
                }

                // Ensure database is migrated before logging
                EnsureDatabaseMigrated();

            // Only log if item tracking is enabled and any traded items should be tracked
            if (!Config.EnableItemTracking)
            {
                log.Info("Skipping trade logging - item tracking disabled");
                return;
            }

            var hasTrackedItems = player1Escrow.Any(item => ShouldTrackItem(item.Name)) || 
                                 player2Escrow.Any(item => ShouldTrackItem(item.Name));

            if (!hasTrackedItems)
            {
                log.Info($"Skipping trade logging - no tracked items in trade");
                return;
                }

            var player1Value = player1Escrow.Sum(item => CalculateItemValue(item, item.StackSize ?? 1));
            var player2Value = player2Escrow.Sum(item => CalculateItemValue(item, item.StackSize ?? 1));

            var transferLog = new TransferLog
            {
                TransferType = TransferTypeTrade,
                FromPlayerName = player1.Name,
                FromPlayerAccount = player1.Account?.AccountName ?? "Unknown",
                ToPlayerName = player2.Name,
                ToPlayerAccount = player2.Account?.AccountName ?? "Unknown",
                ItemName = $"Trade ({player1Escrow.Count} items)",
                Quantity = player1Escrow.Count,
                Timestamp = DateTime.UtcNow,
                FromAccountCreatedDate = player1.Account?.CreateTime,
                ToAccountCreatedDate = player2.Account?.CreateTime,
                FromCharacterCreatedDate = GetCharacterCreationDate(player1),
                ToCharacterCreatedDate = GetCharacterCreationDate(player2),
                AdditionalData = $"Player1 Value: {player1Value}, Player2 Value: {player2Value}",
                FromPlayerIP = GetPlayerIP(player1),
                ToPlayerIP = GetPlayerIP(player2)
            };

            // Log reverse transfer for player2
            var reverseTransferLog = new TransferLog
            {
                TransferType = TransferTypeTrade,
                FromPlayerName = player2.Name,
                FromPlayerAccount = player2.Account?.AccountName ?? "Unknown",
                ToPlayerName = player1.Name,
                ToPlayerAccount = player1.Account?.AccountName ?? "Unknown",
                ItemName = $"Trade ({player2Escrow.Count} items)",
                Quantity = player2Escrow.Count,
                Timestamp = DateTime.UtcNow,
                FromAccountCreatedDate = player2.Account?.CreateTime,
                ToAccountCreatedDate = player1.Account?.CreateTime,
                FromCharacterCreatedDate = GetCharacterCreationDate(player2),
                ToCharacterCreatedDate = GetCharacterCreationDate(player1),
                AdditionalData = $"Player1 Value: {player1Value}, Player2 Value: {player2Value}",
                FromPlayerIP = GetPlayerIP(player2),
                ToPlayerIP = GetPlayerIP(player1)
            };
            
            // Process in background thread to prevent blocking the game thread
            Task.Run(() => ProcessTradeLogBackground(transferLog, reverseTransferLog, player1, player2, player1Value, player2Value));
            }
            catch (Exception ex)
            {
                log.Error($"Trade logging failed: {ex.Message}");
                log.Error($"Stack trace: {ex.StackTrace}");
                // Continue without logging - don't break the trade
            }
        }

        public static void LogGroundPickup(Player player, WorldObject item)
        {
            try
            {
                log.Info($"LogGroundPickup called: {player.Name} picked up {item.Name} x{item.StackSize ?? 1}");
                
                // Only log if item tracking is enabled and this item should be tracked
                if (!Config.EnableItemTracking)
                {
                    log.Info("Skipping ground pickup logging - item tracking disabled");
                    return;
                }

                if (!ShouldTrackItem(item.Name))
                {
                    log.Info($"Skipping ground pickup logging for untracked item: {item.Name}");
                    return;
                }

                // Ensure database is migrated before logging
                EnsureDatabaseMigrated();

                var transferLog = new TransferLog
                {
                    TransferType = TransferTypeGroundPickup,
                    FromPlayerName = "Ground",
                    FromPlayerAccount = "Ground",
                    ToPlayerName = player.Name,
                    ToPlayerAccount = player.Account?.AccountName ?? "Unknown",
                    ItemName = item.Name,
                    Quantity = item.StackSize ?? 1,
                    Timestamp = DateTime.UtcNow,
                    FromAccountCreatedDate = null, // Ground doesn't have account creation date
                    ToAccountCreatedDate = player.Account?.CreateTime,
                    FromCharacterCreatedDate = null, // Ground doesn't have character creation date
                    ToCharacterCreatedDate = GetCharacterCreationDate(player),
                    FromPlayerIP = null, // Ground doesn't have IP
                    ToPlayerIP = GetPlayerIP(player)
                };

                // Process in background to avoid blocking the main thread
                Task.Run(() => ProcessTransferLogBackground(transferLog));
            }
            catch (Exception ex)
            {
                log.Error($"Ground pickup logging failed: {ex.Message}");
                log.Error($"Stack trace: {ex.StackTrace}");
                // Continue without logging - don't break the pickup
            }
        }

        private static bool ShouldSkipLogging(Player fromPlayer, Player toPlayer)
        {
            lock (_lock)
            {
                return _blacklistedPlayers.Contains(fromPlayer.Name) ||
                       _blacklistedPlayers.Contains(toPlayer?.Name ?? "") ||
                       _blacklistedAccounts.Contains(fromPlayer.Account?.AccountName ?? "") ||
                       _blacklistedAccounts.Contains(toPlayer?.Account?.AccountName ?? "");
            }
        }







        private static void UpdateTransferSummary(TransferLog transferLog)
        {
            try
            {
                // Ensure database is migrated before accessing tables
                EnsureDatabaseMigrated();
                
                using var context = new ShardDbContext();
                var summary = context.TransferSummaries
                    .FirstOrDefault(s => s.FromPlayerName == transferLog.FromPlayerName && 
                                        s.ToPlayerName == transferLog.ToPlayerName && 
                                        s.TransferType == transferLog.TransferType);

                if (summary == null)
                {
                    summary = new TransferSummary
                    {
                        FromPlayerName = transferLog.FromPlayerName,
                        FromPlayerAccount = transferLog.FromPlayerAccount,
                        ToPlayerName = transferLog.ToPlayerName,
                        ToPlayerAccount = transferLog.ToPlayerAccount,
                        TransferType = transferLog.TransferType,
                        TotalTransfers = 0,
                        TotalQuantity = 0,
                        TotalValue = 0,
                        SuspiciousTransfers = 0,
                        CreatedDate = DateTime.UtcNow
                    };
                    context.TransferSummaries.Add(summary);
                }

                // Update totals
                summary.TotalTransfers++;
                summary.TotalQuantity += transferLog.Quantity;
                summary.LastTransfer = transferLog.Timestamp;

                if (summary.FirstTransfer == DateTime.MinValue)
                    summary.FirstTransfer = transferLog.Timestamp;

                summary.UpdatedDate = DateTime.UtcNow;
                context.SaveChanges();
            }
            catch (Exception ex)
            {
                log.Error($"Error updating transfer summary: {ex.Message}");
                log.Error($"Inner exception: {ex.InnerException?.Message}");
                log.Error($"Stack trace: {ex.StackTrace}");
            }
        }


        private static long CalculateItemValue(WorldObject item, int quantity)
        {
            // Simplified value calculation - could be enhanced with actual item value logic
            if (item.Name.Contains("Pyreal"))
                return quantity;
            if (item.Name.Contains("Chunk"))
                return quantity * ChunkValue;
            if (item.Name.Contains("Ingot"))
                return quantity * IngotValue;
            
            return quantity * DefaultItemValue;
        }

        private static bool ShouldTrackItem(string itemName)
        {
            if (!Config.EnableItemTracking)
                return false;

            if (Config.TrackAllItems)
                return true;

            if (Config.TrackedItems == null || Config.TrackedItems.Count == 0)
                return false;

            // Check for exact matches or partial matches (case-insensitive)
            return Config.TrackedItems.Any(trackedItem => 
                string.Equals(itemName, trackedItem, StringComparison.OrdinalIgnoreCase) ||
                itemName.Contains(trackedItem, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsTrackedItemTransfer(TransferLog transferLog)
        {
            return ShouldTrackItem(transferLog.ItemName);
        }


        // Background processing methods to prevent server lag
        private static void ProcessTransferLogBackground(TransferLog transferLog)
        {
            try
            {
                // Ensure database is migrated before processing
                EnsureDatabaseMigrated();

                // Save to database (background thread)
                DatabaseManager.Shard.BaseDatabase.SaveTransferLog(transferLog);

                // Update monitoring system (background thread)
                TransferMonitor.RecordTransfer(transferLog);

                // Update transfer summary (background thread)
                UpdateTransferSummary(transferLog);

                // Send admin notification (background thread)
                var adminMessage = $"📦 {transferLog.FromPlayerName} -> {transferLog.ToPlayerName}: {transferLog.ItemName} x{transferLog.Quantity}";
                
                SendAdminMessage(adminMessage);
            }
            catch (Exception ex)
            {
                log.Error($"Background transfer processing failed: {ex.Message}");
                log.Error($"Stack trace: {ex.StackTrace}");
            }
        }


        private static void ProcessTradeLogBackground(TransferLog transferLog, TransferLog reverseTransferLog, Player player1, Player player2, long player1Value, long player2Value)
        {
            try
            {
                // Ensure database is migrated before processing
                EnsureDatabaseMigrated();

                // Save both transfers to database (background thread)
                DatabaseManager.Shard.BaseDatabase.SaveTransferLog(transferLog);
                DatabaseManager.Shard.BaseDatabase.SaveTransferLog(reverseTransferLog);

                // Update monitoring system for both transfers (background thread)
                TransferMonitor.RecordTransfer(transferLog);
                TransferMonitor.RecordTransfer(reverseTransferLog);

                // Update transfer summaries for both transfers (background thread)
                UpdateTransferSummary(transferLog);
                UpdateTransferSummary(reverseTransferLog);

                // Send admin notification (background thread)
                var adminMessage = $"🤝 TRADE: {player1.Name} <-> {player2.Name}: {transferLog.Quantity} items each (Values: {player1Value:N0} <-> {player2Value:N0})";
                
                SendAdminMessage(adminMessage);
            }
            catch (Exception ex)
            {
                log.Error($"Background trade processing failed: {ex.Message}");
                log.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        // Configuration methods
        public static void SetSuspiciousTransferThreshold(int threshold)
        {
            Config.SuspiciousTransferThreshold = threshold;
        }

        public static void SetSuspiciousTransferTimeWindow(int hours)
        {
            Config.TimeWindowHours = hours;
        }

        public static void SetPatternDetectionThreshold(int threshold)
        {
            Config.PatternDetectionThreshold = threshold;
        }

        // Item tracking methods
        public static void AddTrackedItem(string itemName)
        {
            try
            {
                // Ensure database is migrated before accessing tables
                EnsureDatabaseMigrated();
                
                using var context = new ShardDbContext();
                
                // Check if item already exists in database (case-insensitive)
                var existingItem = context.TrackedItems
                    .AsEnumerable()
                    .FirstOrDefault(t => string.Equals(t.ItemName, itemName, StringComparison.OrdinalIgnoreCase));
                
                if (existingItem == null)
                {
                    // Add to database
                    var trackedItem = new TrackedItem
                    {
                        ItemName = itemName,
                        CreatedDate = DateTime.UtcNow,
                        UpdatedDate = DateTime.UtcNow,
                        IsActive = true
                    };
                    context.TrackedItems.Add(trackedItem);
                    context.SaveChanges();
                    
                    log.Info($"Added '{itemName}' to tracked items database");
                }
                else if (!existingItem.IsActive)
                {
                    // Reactivate existing item
                    existingItem.IsActive = true;
                    existingItem.UpdatedDate = DateTime.UtcNow;
                    context.SaveChanges();
                    
                    log.Info($"Reactivated '{itemName}' in tracked items database");
                }
                
                // Update in-memory config for immediate use
                if (Config.TrackedItems == null)
                    Config.TrackedItems = new List<string>();
                
                if (!Config.TrackedItems.Any(item => string.Equals(item, itemName, StringComparison.OrdinalIgnoreCase)))
                {
                    Config.TrackedItems.Add(itemName);
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error adding tracked item '{itemName}': {ex.Message}");
            }
        }

        public static void RemoveTrackedItem(string itemName)
        {
            try
            {
                // Ensure database is migrated before accessing tables
                EnsureDatabaseMigrated();
                
                using var context = new ShardDbContext();
                
                // Find and deactivate item in database (case-insensitive)
                var existingItem = context.TrackedItems
                    .AsEnumerable()
                    .FirstOrDefault(t => string.Equals(t.ItemName, itemName, StringComparison.OrdinalIgnoreCase));
                
                if (existingItem != null && existingItem.IsActive)
                {
                    existingItem.IsActive = false;
                    existingItem.UpdatedDate = DateTime.UtcNow;
                    context.SaveChanges();
                    
                    log.Info($"Removed '{itemName}' from tracked items database");
                }
                
                // Remove from in-memory config
                if (Config.TrackedItems != null)
                {
                    Config.TrackedItems.RemoveAll(item => string.Equals(item, itemName, StringComparison.OrdinalIgnoreCase));
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error removing tracked item '{itemName}': {ex.Message}");
            }
        }

        public static List<string> GetTrackedItems()
        {
            try
            {
                // Ensure database is migrated before accessing tables
                EnsureDatabaseMigrated();
                
                using var context = new ShardDbContext();
                
                // Load active tracked items from database
                var dbTrackedItems = context.TrackedItems
                    .Where(t => t.IsActive)
                    .Select(t => t.ItemName)
                    .ToList();
                
                // Update in-memory config to match database
                Config.TrackedItems = dbTrackedItems;
                
                return dbTrackedItems;
            }
            catch (Exception ex)
            {
                log.Error($"Error loading tracked items: {ex.Message}");
                // Fallback to in-memory config
                return Config.TrackedItems?.ToList() ?? new List<string>();
            }
        }

        public static void SetTrackAllItems(bool trackAll)
        {
            Config.TrackAllItems = trackAll;
        }

        public static void SetItemTrackingEnabled(bool enabled)
        {
            Config.EnableItemTracking = enabled;
        }

        // Blacklist management
        public static void AddPlayerToBlacklist(string playerName)
        {
            lock (_lock)
            {
                _blacklistedPlayers.Add(playerName);
            }
        }

        public static void RemovePlayerFromBlacklist(string playerName)
        {
            lock (_lock)
            {
                _blacklistedPlayers.Remove(playerName);
            }
        }

        public static void AddAccountToBlacklist(string accountName)
        {
            lock (_lock)
            {
                _blacklistedAccounts.Add(accountName);
            }
        }

        public static void RemoveAccountFromBlacklist(string accountName)
        {
            lock (_lock)
            {
                _blacklistedAccounts.Remove(accountName);
            }
        }

        public static List<string> GetBlacklistedPlayers()
        {
            lock (_lock)
            {
                return _blacklistedPlayers.ToList();
            }
        }

        public static List<string> GetBlacklistedAccounts()
        {
            lock (_lock)
            {
                return _blacklistedAccounts.ToList();
            }
        }

        // Query methods
        public static List<TransferLog> GetTransferHistory(string playerName, int days)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            return DatabaseManager.Shard.BaseDatabase.GetTransferHistory(playerName, cutoffDate);
        }

        public static List<TransferLog> GetSuspiciousTransfers(int days)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            return DatabaseManager.Shard.BaseDatabase.GetSuspiciousTransfers(cutoffDate);
        }

        public static List<TransferLog> GetTransferPatterns(string playerName, int days)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            return DatabaseManager.Shard.BaseDatabase.GetTransferPatterns(playerName, cutoffDate);
        }

        // Configuration management methods
        public static void LoadConfigurationFromDatabase()
        {
            try
            {
                EnsureDatabaseMigrated();
                
                using var context = new ShardDbContext();
                var dbConfig = context.TransferMonitoringConfigs.FirstOrDefault();
                
                if (dbConfig != null)
                {
                    // Update in-memory config with database values
                    Config.SuspiciousTransferThreshold = dbConfig.SuspiciousTransferThreshold;
                    Config.TimeWindowHours = dbConfig.TimeWindowHours;
                    Config.PatternDetectionThreshold = dbConfig.PatternDetectionThreshold;
                    Config.EnableTransferLogging = dbConfig.EnableTransferLogging;
                    Config.EnableSuspiciousDetection = dbConfig.EnableSuspiciousDetection;
                    Config.EnableAdminNotifications = dbConfig.EnableAdminNotifications;
                    Config.EnableTransferSummaries = dbConfig.EnableTransferSummaries;
                    Config.EnableTransferLogs = dbConfig.EnableTransferLogs;
                    Config.EnableItemTracking = dbConfig.EnableItemTracking;
                    Config.TrackAllItems = dbConfig.TrackAllItems;
                    
                    log.Info("Transfer monitoring configuration loaded from database");
                }
                else
                {
                    log.Warn("No transfer monitoring configuration found in database, using defaults");
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error loading configuration from database: {ex.Message}");
            }
        }

        public static void SaveConfigurationToDatabase()
        {
            try
            {
                EnsureDatabaseMigrated();
                
                using var context = new ShardDbContext();
                var dbConfig = context.TransferMonitoringConfigs.FirstOrDefault();
                
                if (dbConfig == null)
                {
                    // Create new config record
                    dbConfig = new TransferMonitoringConfigDb
                    {
                        CreatedDate = DateTime.UtcNow,
                        UpdatedDate = DateTime.UtcNow
                    };
                    context.TransferMonitoringConfigs.Add(dbConfig);
                }
                else
                {
                    dbConfig.UpdatedDate = DateTime.UtcNow;
                }
                
                // Update database config with current in-memory values
                dbConfig.SuspiciousTransferThreshold = Config.SuspiciousTransferThreshold;
                dbConfig.TimeWindowHours = Config.TimeWindowHours;
                dbConfig.PatternDetectionThreshold = Config.PatternDetectionThreshold;
                dbConfig.EnableTransferLogging = Config.EnableTransferLogging;
                dbConfig.EnableSuspiciousDetection = Config.EnableSuspiciousDetection;
                dbConfig.EnableAdminNotifications = Config.EnableAdminNotifications;
                dbConfig.EnableTransferSummaries = Config.EnableTransferSummaries;
                dbConfig.EnableTransferLogs = Config.EnableTransferLogs;
                dbConfig.EnableItemTracking = Config.EnableItemTracking;
                dbConfig.TrackAllItems = Config.TrackAllItems;
                
                context.SaveChanges();
                log.Info("Transfer monitoring configuration saved to database");
            }
            catch (Exception ex)
            {
                log.Error($"Error saving configuration to database: {ex.Message}");
            }
        }

        // Individual config update methods
        public static void UpdateSuspiciousTransferThreshold(int threshold)
        {
            Config.SuspiciousTransferThreshold = threshold;
            SaveConfigurationToDatabase();
        }

        public static void UpdateTimeWindowHours(int hours)
        {
            Config.TimeWindowHours = hours;
            SaveConfigurationToDatabase();
        }

        public static void UpdatePatternDetectionThreshold(int threshold)
        {
            Config.PatternDetectionThreshold = threshold;
            SaveConfigurationToDatabase();
        }

        public static void UpdateEnableItemTracking(bool enabled)
        {
            Config.EnableItemTracking = enabled;
            SaveConfigurationToDatabase();
        }

        public static void UpdateTrackAllItems(bool trackAll)
        {
            Config.TrackAllItems = trackAll;
            SaveConfigurationToDatabase();
        }

        // Bank Command Blacklist Management
        public static bool IsPlayerBankBlacklisted(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
                return false;
                
            try
            {
                EnsureDatabaseMigrated();
                
                using var context = new ShardDbContext();
                var now = DateTime.UtcNow;
                
                return context.BankCommandBlacklist
                    .Where(b => b.PlayerName.ToUpper() == playerName.ToUpper() &&
                                b.IsActive &&
                                (b.ExpiryDate == null || b.ExpiryDate > now))
                    .Any();
            }
            catch (Exception ex)
            {
                log.Error($"Error checking bank blacklist for {playerName}: {ex.Message}");
                return false;
            }
        }

        public static bool IsAccountBankBlacklisted(string accountName)
        {
            if (string.IsNullOrWhiteSpace(accountName))
                return false;
                
            try
            {
                EnsureDatabaseMigrated();
                
                using var context = new ShardDbContext();
                var now = DateTime.UtcNow;
                
                return context.BankCommandBlacklist
                    .Where(b => b.AccountName.ToUpper() == accountName.ToUpper() &&
                                b.IsActive &&
                                (b.ExpiryDate == null || b.ExpiryDate > now))
                    .Any();
            }
            catch (Exception ex)
            {
                log.Error($"Error checking account bank blacklist for {accountName}: {ex.Message}");
                return false;
            }
        }

        public static void AddPlayerToBankBlacklist(string playerName, string reason, string addedBy, DateTime? expiryDate = null)
        {
            try
            {
                EnsureDatabaseMigrated();
                
                using var context = new ShardDbContext();
                
                // Check if already blacklisted
                var existing = context.BankCommandBlacklist
                    .Where(b => b.PlayerName.ToUpper() == playerName.ToUpper() &&
                                b.IsActive)
                    .FirstOrDefault();
                
                if (existing != null)
                {
                    log.Warn($"Player {playerName} is already on bank blacklist");
                    return;
                }
                
                var blacklistEntry = new BankCommandBlacklist
                {
                    PlayerName = playerName,
                    AccountName = null,
                    Reason = reason,
                    AddedBy = addedBy,
                    CreatedDate = DateTime.UtcNow,
                    ExpiryDate = expiryDate,
                    IsActive = true
                };
                
                context.BankCommandBlacklist.Add(blacklistEntry);
                context.SaveChanges();
                
                log.Info($"Added {playerName} to bank command blacklist: {reason}");
            }
            catch (Exception ex)
            {
                log.Error($"Error adding {playerName} to bank blacklist: {ex.Message}");
            }
        }

        public static void AddAccountToBankBlacklist(string accountName, string reason, string addedBy, DateTime? expiryDate = null)
        {
            try
            {
                EnsureDatabaseMigrated();
                
                using var context = new ShardDbContext();
                
                // Check if already blacklisted
                var existing = context.BankCommandBlacklist
                    .Where(b => b.AccountName.ToUpper() == accountName.ToUpper() &&
                                b.IsActive)
                    .FirstOrDefault();
                
                if (existing != null)
                {
                    log.Warn($"Account {accountName} is already on bank blacklist");
                    return;
                }
                
                var blacklistEntry = new BankCommandBlacklist
                {
                    PlayerName = null,
                    AccountName = accountName,
                    Reason = reason,
                    AddedBy = addedBy,
                    CreatedDate = DateTime.UtcNow,
                    ExpiryDate = expiryDate,
                    IsActive = true
                };
                
                context.BankCommandBlacklist.Add(blacklistEntry);
                context.SaveChanges();
                
                log.Info($"Added account {accountName} to bank command blacklist: {reason}");
            }
            catch (Exception ex)
            {
                log.Error($"Error adding account {accountName} to bank blacklist: {ex.Message}");
            }
        }

        public static void RemovePlayerFromBankBlacklist(string playerName)
        {
            try
            {
                EnsureDatabaseMigrated();
                
                using var context = new ShardDbContext();
                
                var entries = context.BankCommandBlacklist
                    .Where(b => b.PlayerName.ToUpper() == playerName.ToUpper() &&
                                b.IsActive)
                    .ToList();
                
                foreach (var entry in entries)
                {
                    entry.IsActive = false;
                }
                
                context.SaveChanges();
                log.Info($"Removed {playerName} from bank command blacklist");
            }
            catch (Exception ex)
            {
                log.Error($"Error removing {playerName} from bank blacklist: {ex.Message}");
            }
        }

        public static void RemoveAccountFromBankBlacklist(string accountName)
        {
            try
            {
                EnsureDatabaseMigrated();
                
                using var context = new ShardDbContext();
                
                var entries = context.BankCommandBlacklist
                    .Where(b => b.AccountName.ToUpper() == accountName.ToUpper() &&
                                b.IsActive)
                    .ToList();
                
                foreach (var entry in entries)
                {
                    entry.IsActive = false;
                }
                
                context.SaveChanges();
                log.Info($"Removed account {accountName} from bank command blacklist");
            }
            catch (Exception ex)
            {
                log.Error($"Error removing account {accountName} from bank blacklist: {ex.Message}");
            }
        }

        public static List<BankCommandBlacklist> GetBankBlacklistedPlayers()
        {
            try
            {
                EnsureDatabaseMigrated();
                
                using var context = new ShardDbContext();
                var now = DateTime.UtcNow;
                
                return context.BankCommandBlacklist
                    .Where(b => b.IsActive && (b.ExpiryDate == null || b.ExpiryDate > now))
                    .OrderBy(b => b.PlayerName ?? b.AccountName)
                    .ToList();
            }
            catch (Exception ex)
            {
                log.Error($"Error getting bank blacklisted players: {ex.Message}");
                return new List<BankCommandBlacklist>();
            }
        }


    }
}

