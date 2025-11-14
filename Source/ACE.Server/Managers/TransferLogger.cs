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
        public const string TransferTypeGroundDrop = "Ground Drop";
        public const string TransferTypeChestDeposit = "Chest Deposit";
        public const string TransferTypeChestWithdrawal = "Chest Withdrawal";
        
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
                var adminPlayers = PlayerManager.GetAllOnline()
                    .Where(p => p.Account != null && (uint)p.Account.AccessLevel >= (uint)AccessLevel.Advocate);

                foreach (var admin in adminPlayers)
                {
                    if (admin.Session?.Network == null)
                        continue;

                    // Note: This is called from background threads but appears to work safely in practice
                    // If cross-thread issues arise, consider marshaling to main thread
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
        private static volatile bool _databaseMigrated = false;
        private static volatile bool _trackedItemsInitialized = false;
        private static readonly object _migrationLock = new object();
        private static readonly object _initLock = new object();

        /// <summary>
        /// Initialize transfer monitoring system from database on server startup
        /// </summary>
        public static void InitializeTransferMonitoring()
        {
            if (_trackedItemsInitialized) return;
            
            lock (_initLock)
            {
                if (_trackedItemsInitialized) return;
                
                try
                {
                    log.Info("Initializing transfer monitoring system from database...");
                    
                    EnsureDatabaseMigrated();
                    LoadConfigurationFromDatabase();
                    GetTrackedItems();
                    
                    _trackedItemsInitialized = true;
                    log.Info($"Loaded {Config.TrackedItems?.Count ?? 0} tracked items and configuration from database");
                }
                catch (Exception ex)
                {
                    log.Error($"Error initializing transfer monitoring system: {ex.Message}");
                }
            }
        }

        public static void EnsureDatabaseMigrated()
        {
            if (_databaseMigrated) return;

            lock (_migrationLock)
            {
                if (_databaseMigrated) return;
                
                try
                {
                    using (var context = new ShardDbContext())
                    {
                        log.Info("Running database migration for all transfer monitoring tables...");
                        
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
                }
            }
        }

        private static void CreateTransferLogsTable(ShardDbContext context)
        {
            try
            {
                // Check if table exists by trying to select from it
                context.Database.ExecuteSqlRaw("SELECT 1 FROM transfer_logs LIMIT 1");
                log.Info("transfer_logs table exists - checking for IP address columns...");
                
                // Check and add missing columns individually (MySQL-safe approach)
                var connection = context.Database.GetDbConnection();
                connection.Open();
                using var command = connection.CreateCommand();
                
                // Check if columns exist and add them individually
                var columnsToAdd = new[]
                {
                    ("FromAccountCreatedDate", "datetime(6) DEFAULT NULL"),
                    ("ToAccountCreatedDate", "datetime(6) DEFAULT NULL"),
                    ("FromCharacterCreatedDate", "datetime(6) DEFAULT NULL"),
                    ("ToCharacterCreatedDate", "datetime(6) DEFAULT NULL"),
                    ("AdditionalData", "varchar(1000) DEFAULT NULL"),
                    ("FromPlayerIP", "varchar(45) DEFAULT NULL"),
                    ("ToPlayerIP", "varchar(45) DEFAULT NULL")
                };
                
                foreach (var (columnName, columnType) in columnsToAdd)
                {
                    command.CommandText = $@"
                        SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                        WHERE TABLE_SCHEMA = DATABASE() 
                        AND TABLE_NAME = 'transfer_logs' 
                        AND COLUMN_NAME = '{columnName}'";
                    
                    var exists = Convert.ToInt32(command.ExecuteScalar()) > 0;
                    if (!exists)
                    {
                        command.CommandText = $"ALTER TABLE `transfer_logs` ADD COLUMN `{columnName}` {columnType}";
                        command.ExecuteNonQuery();
                    }
                }
                
                // Check and modify Quantity column to bigint
                command.CommandText = @"
                    SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() 
                    AND TABLE_NAME = 'transfer_logs' 
                    AND COLUMN_NAME = 'Quantity'";
                
                var quantityType = command.ExecuteScalar()?.ToString();
                if (quantityType != "bigint")
                {
                    command.CommandText = "ALTER TABLE `transfer_logs` MODIFY COLUMN `Quantity` bigint(20) NOT NULL";
                    command.ExecuteNonQuery();
                }
                
                connection.Close();
                log.Info("Missing columns added to transfer_logs and Quantity migrated to bigint");
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
                            `Quantity` bigint(20) NOT NULL,
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
                log.Info("transfer_summaries table exists - ensuring unique index");
                EnsureTransferSummaryUniqueIndex(context);
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
                            UNIQUE KEY `idx_transfer_summary_unique` (`FromPlayerName`,`ToPlayerName`,`TransferType`),
                            KEY `IX_transfer_summaries_FromPlayerName` (`FromPlayerName`),
                            KEY `IX_transfer_summaries_ToPlayerName` (`ToPlayerName`),
                            KEY `IX_transfer_summaries_LastTransfer` (`LastTransfer`),
                            KEY `IX_transfer_summaries_IsSuspicious` (`IsSuspicious`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                    
                    log.Info("transfer_summaries table created successfully");
                    EnsureTransferSummaryUniqueIndex(context);
                }
                catch (Exception ex)
                {
                    log.Info($"Table may already exist: {ex.Message}");
                }
            }
        }

        private static void EnsureTransferSummaryUniqueIndex(ShardDbContext context)
        {
            try
            {
                var connection = context.Database.GetDbConnection();
                var openedHere = false;
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    connection.Open();
                    openedHere = true;
                }

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT COUNT(*) FROM information_schema.statistics 
                    WHERE table_schema = DATABASE() 
                    AND table_name = 'transfer_summaries' 
                    AND index_name = 'idx_transfer_summary_unique'";
                var indexExists = Convert.ToInt32(command.ExecuteScalar()) > 0;

                if (!indexExists)
                {
                    log.Info("Unique index missing, cleaning duplicates and creating index...");
                    context.Database.ExecuteSqlRaw(@"
                        DELETE t1 FROM transfer_summaries t1
                        INNER JOIN transfer_summaries t2
                            ON t1.FromPlayerName = t2.FromPlayerName
                           AND t1.ToPlayerName = t2.ToPlayerName
                           AND t1.TransferType = t2.TransferType
                           AND t1.Id > t2.Id");

                    context.Database.ExecuteSqlRaw(@"
                        CREATE UNIQUE INDEX `idx_transfer_summary_unique`
                        ON `transfer_summaries` (`FromPlayerName`,`ToPlayerName`,`TransferType`)");
                }
                else
                {
                    log.Info("Unique index already exists on transfer_summaries table");
                }

                if (openedHere)
                {
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error ensuring unique index on transfer_summaries: {ex.Message}");
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
                return DateTimeOffset.FromUnixTimeSeconds(creationTimestamp.Value).UtcDateTime;
            }
            
            return null;
        }

        public static void LogBankTransfer(Player fromPlayer, string toPlayerName, string itemName, long quantity, string transferType)
        {
            try
            {
                // Get the destination player if they're online
                var toPlayer = PlayerManager.GetAllPlayers().FirstOrDefault(p => p.Name == toPlayerName) as Player;
                
                if (ShouldSkipLogging(fromPlayer, toPlayer, toPlayerName))
                {
                    log.Info("Skipping logging due to blacklist");
                    return;
                }

                // Ensure database is migrated before logging
                EnsureDatabaseMigrated();

                // Check master switch first
                if (!Config.EnableTransferLogging)
                {
                    log.Debug("Skipping logging - transfer logging disabled");
                    return;
                }

                // For bank transfers, always log regardless of item tracking settings
                // For other transfer types, respect item tracking settings
                var requiresExplicitTracking = !string.Equals(transferType, TransferTypeBankTransfer, StringComparison.OrdinalIgnoreCase) &&
                                              (!Config.EnableItemTracking ||
                                               (!Config.TrackAllItems && !ShouldTrackItem(itemName)));
                if (requiresExplicitTracking)
                {
                    log.Debug($"Skipping logging for untracked item: {itemName}");
                    return;
                }

                // Get destination account name - check online player first, then database for offline players
                string toPlayerAccountName = toPlayer?.Account?.AccountName;
                DateTime? toAccountCreatedDate = toPlayer?.Account?.CreateTime;
                if (string.IsNullOrEmpty(toPlayerAccountName))
                {
                    try
                    {
                        using (var context = new ShardDbContext())
                        {
                            var character = context.Character.FirstOrDefault(c => c.Name == toPlayerName);
                            if (character != null)
                            {
                                var account = DatabaseManager.Authentication.GetAccountById(character.AccountId);
                                if (account != null)
                                {
                                    toPlayerAccountName = account.AccountName;
                                    toAccountCreatedDate = account.CreateTime;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Error looking up offline player account for {toPlayerName}: {ex.Message}");
                    }
                }
                toPlayerAccountName ??= "Unknown"; // Fallback if all lookups fail

                var transferLog = new TransferLog
                {
                    TransferType = transferType,
                    FromPlayerName = fromPlayer.Name,
                    FromPlayerAccount = fromPlayer.Account?.AccountName ?? "Unknown",
                    ToPlayerName = toPlayerName,
                    ToPlayerAccount = toPlayerAccountName,
                    ItemName = itemName,
                    Quantity = quantity,
                    Timestamp = DateTime.UtcNow,
                    FromAccountCreatedDate = fromPlayer.Account?.CreateTime,
                    ToAccountCreatedDate = toAccountCreatedDate,
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
                if (!Config.EnableTransferLogging)
                {
                    log.Info("Skipping direct give logging - transfer logging disabled");
                    return;
                }
                
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
                    log.Debug($"Skipping logging for untracked item: {item.Name}");
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
                if (!Config.EnableTransferLogging)
                {
                    log.Info("Skipping trade logging - transfer logging disabled");
                    return;
                }
                
                if (ShouldSkipLogging(player1, player2))
                {
                    log.Info("Skipping logging due to blacklist");
                    return;
                }

                // Ensure database is migrated before logging
                EnsureDatabaseMigrated();

            // Only log if item tracking is enabled
            if (!Config.EnableItemTracking)
            {
                log.Info("Skipping trade logging - item tracking disabled");
                return;
            }

            // Log each tracked item from player1 to player2
            foreach (var item in player1Escrow)
            {
                // Only log if this specific item should be tracked
                if (!ShouldTrackItem(item.Name))
                {
                    log.Debug($"Skipping untracked item in trade: {item.Name}");
                    continue;
                }
            var transferLog = new TransferLog
            {
                    TransferType = TransferTypeTrade,
                FromPlayerName = player1.Name,
                FromPlayerAccount = player1.Account?.AccountName ?? "Unknown",
                ToPlayerName = player2.Name,
                ToPlayerAccount = player2.Account?.AccountName ?? "Unknown",
                    ItemName = item.Name,
                    Quantity = item.StackSize ?? 1,
                Timestamp = DateTime.UtcNow,
                    FromAccountCreatedDate = player1.Account?.CreateTime,
                    ToAccountCreatedDate = player2.Account?.CreateTime,
                    FromCharacterCreatedDate = GetCharacterCreationDate(player1),
                    ToCharacterCreatedDate = GetCharacterCreationDate(player2),
                    AdditionalData = $"Trade Value: {CalculateItemValue(item, item.StackSize ?? 1)}",
                    FromPlayerIP = GetPlayerIP(player1),
                    ToPlayerIP = GetPlayerIP(player2)
                };

                // Process in background thread to prevent blocking the game thread
                Task.Run(() => ProcessTransferLogBackground(transferLog));
            }

            // Log each tracked item from player2 to player1
            foreach (var item in player2Escrow)
            {
                // Only log if this specific item should be tracked
                if (!ShouldTrackItem(item.Name))
                {
                    log.Debug($"Skipping untracked item in trade: {item.Name}");
                    continue;
                }
            var reverseTransferLog = new TransferLog
            {
                    TransferType = TransferTypeTrade,
                FromPlayerName = player2.Name,
                FromPlayerAccount = player2.Account?.AccountName ?? "Unknown",
                ToPlayerName = player1.Name,
                ToPlayerAccount = player1.Account?.AccountName ?? "Unknown",
                    ItemName = item.Name,
                    Quantity = item.StackSize ?? 1,
                Timestamp = DateTime.UtcNow,
                    FromAccountCreatedDate = player2.Account?.CreateTime,
                    ToAccountCreatedDate = player1.Account?.CreateTime,
                    FromCharacterCreatedDate = GetCharacterCreationDate(player2),
                    ToCharacterCreatedDate = GetCharacterCreationDate(player1),
                    AdditionalData = $"Trade Value: {CalculateItemValue(item, item.StackSize ?? 1)}",
                    FromPlayerIP = GetPlayerIP(player2),
                    ToPlayerIP = GetPlayerIP(player1)
                };

                // Process in background thread to prevent blocking the game thread
                Task.Run(() => ProcessTransferLogBackground(reverseTransferLog));
            }
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
            LogGroundPickup(player, item, item.StackSize ?? 1);
        }

        public static void LogGroundPickup(Player player, WorldObject item, int quantity)
        {
            try
            {
                if (!Config.EnableTransferLogging)
                {
                    log.Info("Skipping ground pickup logging - transfer logging disabled");
                    return;
                }
                
                // Only log if item tracking is enabled and this item should be tracked
                if (!Config.EnableItemTracking)
                {
                    log.Info("Skipping ground pickup logging - item tracking disabled");
                    return;
                }

                if (!ShouldTrackItem(item.Name))
                {
                    log.Debug($"Skipping ground pickup logging for untracked item: {item.Name}");
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
                    Quantity = quantity,
                    Timestamp = DateTime.UtcNow,
                    FromAccountCreatedDate = null, // Ground doesn't have account creation date
                    ToAccountCreatedDate = player.Account?.CreateTime,
                    FromCharacterCreatedDate = null, // Ground doesn't have character creation date
                    ToCharacterCreatedDate = GetCharacterCreationDate(player),
                    AdditionalData = $"Location: {player.Location?.ToLOCString()}",
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

        public static void LogGroundPickupWithData(string playerName, string playerAccount, string itemName, int itemQuantity, 
            DateTime? accountCreatedDate, DateTime? characterCreatedDate, string playerIP)
        {
            try
            {
                if (!Config.EnableTransferLogging)
                {
                    log.Info("Skipping ground pickup logging - transfer logging disabled");
                    return;
                }
                
                if (!Config.EnableItemTracking)
                {
                    log.Info("Skipping ground pickup logging - item tracking disabled");
                    return;
                }

                if (!ShouldTrackItem(itemName))
                {
                    log.Debug($"Skipping ground pickup logging for untracked item: {itemName}");
                    return;
                }

                EnsureDatabaseMigrated();

                var transferLog = new TransferLog
                {
                    TransferType = TransferTypeGroundPickup,
                    FromPlayerName = "Ground",
                    FromPlayerAccount = "Ground",
                    ToPlayerName = playerName,
                    ToPlayerAccount = playerAccount,
                    ItemName = itemName,
                    Quantity = itemQuantity,
                    Timestamp = DateTime.UtcNow,
                    FromAccountCreatedDate = null,
                    ToAccountCreatedDate = accountCreatedDate,
                    FromCharacterCreatedDate = null,
                    ToCharacterCreatedDate = characterCreatedDate,
                    FromPlayerIP = null,
                    ToPlayerIP = playerIP
                };

                Task.Run(() => ProcessTransferLogBackground(transferLog));
            }
            catch (Exception ex)
            {
                log.Error($"Ground pickup logging failed: {ex.Message}");
                log.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        public static void LogGroundDrop(Player player, WorldObject item)
        {
            try
            {
                if (!Config.EnableTransferLogging)
                {
                    log.Info("Skipping ground drop logging - transfer logging disabled");
                    return;
                }
                
                if (!Config.EnableItemTracking)
                {
                    log.Info("Skipping ground drop logging - item tracking disabled");
                    return;
                }

                if (!ShouldTrackItem(item.Name))
                {
                    log.Debug($"Skipping ground drop logging for untracked item: {item.Name}");
                    return;
                }

                EnsureDatabaseMigrated();

                var transferLog = new TransferLog
                {
                    TransferType = TransferTypeGroundDrop,
                    FromPlayerName = player.Name,
                    FromPlayerAccount = player.Account?.AccountName ?? "Unknown",
                    ToPlayerName = "Ground",
                    ToPlayerAccount = "Ground",
                    ItemName = item.Name,
                    Quantity = item.StackSize ?? 1,
                    Timestamp = DateTime.UtcNow,
                    FromAccountCreatedDate = player.Account?.CreateTime,
                    ToAccountCreatedDate = null,
                    FromCharacterCreatedDate = GetCharacterCreationDate(player),
                    ToCharacterCreatedDate = null,
                    AdditionalData = $"Location: {player.Location?.ToLOCString()}",
                    FromPlayerIP = GetPlayerIP(player),
                    ToPlayerIP = null
                };

                Task.Run(() => ProcessTransferLogBackground(transferLog));
            }
            catch (Exception ex)
            {
                log.Error($"Ground drop logging failed: {ex.Message}");
                log.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        public static void LogChestDeposit(Player player, WorldObject item, Container chest)
        {
            try
            {
                if (!Config.EnableTransferLogging)
                {
                    log.Info("Skipping chest deposit logging - transfer logging disabled");
                    return;
                }
                
                if (!Config.EnableItemTracking)
                {
                    log.Info("Skipping chest deposit logging - item tracking disabled");
                    return;
                }

                if (!ShouldTrackItem(item.Name))
                {
                    log.Debug($"Skipping chest deposit logging for untracked item: {item.Name}");
                    return;
                }

                EnsureDatabaseMigrated();

                var transferLog = new TransferLog
                {
                    TransferType = TransferTypeChestDeposit,
                    FromPlayerName = player.Name,
                    FromPlayerAccount = player.Account?.AccountName ?? "Unknown",
                    ToPlayerName = $"Chest:{chest.Name}",
                    ToPlayerAccount = $"Chest:{chest.Guid.Full:X8}",
                    ItemName = item.Name,
                    Quantity = item.StackSize ?? 1,
                    Timestamp = DateTime.UtcNow,
                    FromAccountCreatedDate = player.Account?.CreateTime,
                    ToAccountCreatedDate = null,
                    FromCharacterCreatedDate = GetCharacterCreationDate(player),
                    ToCharacterCreatedDate = null,
                    AdditionalData = $"Container: {chest.Name} (0x{chest.Guid.Full:X8}) @ {chest.Location?.ToLOCString()}",
                    FromPlayerIP = GetPlayerIP(player),
                    ToPlayerIP = null
                };

                Task.Run(() => ProcessTransferLogBackground(transferLog));
            }
            catch (Exception ex)
            {
                log.Error($"Chest deposit logging failed: {ex.Message}");
                log.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        public static void LogChestWithdrawal(Player player, WorldObject item, Container chest)
        {
            LogChestWithdrawal(player, item, chest, item.StackSize ?? 1);
        }

        public static void LogChestWithdrawal(Player player, WorldObject item, Container chest, int quantity)
        {
            try
            {
                if (!Config.EnableTransferLogging)
                {
                    log.Info("Skipping chest withdrawal logging - transfer logging disabled");
                    return;
                }
                
                if (!Config.EnableItemTracking)
                {
                    log.Info("Skipping chest withdrawal logging - item tracking disabled");
                    return;
                }

                if (!ShouldTrackItem(item.Name))
                {
                    log.Debug($"Skipping chest withdrawal logging for untracked item: {item.Name}");
                    return;
                }

                EnsureDatabaseMigrated();

                var transferLog = new TransferLog
                {
                    TransferType = TransferTypeChestWithdrawal,
                    FromPlayerName = $"Chest:{chest.Name}",
                    FromPlayerAccount = $"Chest:{chest.Guid.Full:X8}",
                    ToPlayerName = player.Name,
                    ToPlayerAccount = player.Account?.AccountName ?? "Unknown",
                    ItemName = item.Name,
                    Quantity = quantity,
                    Timestamp = DateTime.UtcNow,
                    FromAccountCreatedDate = null,
                    ToAccountCreatedDate = player.Account?.CreateTime,
                    FromCharacterCreatedDate = null,
                    ToCharacterCreatedDate = GetCharacterCreationDate(player),
                    AdditionalData = $"Container: {chest.Name} (0x{chest.Guid.Full:X8}) @ {chest.Location?.ToLOCString()}",
                    FromPlayerIP = null,
                    ToPlayerIP = GetPlayerIP(player)
                };

                Task.Run(() => ProcessTransferLogBackground(transferLog));
            }
            catch (Exception ex)
            {
                log.Error($"Chest withdrawal logging failed: {ex.Message}");
                log.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        private static bool ShouldSkipLogging(Player fromPlayer, Player toPlayer, string toPlayerName = null)
        {
            lock (_lock)
            {
                var toName = toPlayer?.Name ?? toPlayerName ?? "";
                return _blacklistedPlayers.Contains(fromPlayer.Name) ||
                       _blacklistedPlayers.Contains(toName) ||
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
                
                // Use atomic SQL to avoid race conditions in concurrent updates
                var sql = @"
                    INSERT INTO transfer_summaries (
                        FromPlayerName, FromPlayerAccount, ToPlayerName, ToPlayerAccount, 
                        TransferType, TotalTransfers, TotalQuantity, TotalValue, 
                        SuspiciousTransfers, IsSuspicious, FirstTransfer, LastTransfer, CreatedDate, UpdatedDate
                    ) VALUES (
                        {0}, {1}, {2}, {3},
                        {4}, 1, {5}, 0,
                        0, 0, {6}, {6}, {6}, {6}
                    )
                    ON DUPLICATE KEY UPDATE
                        TotalTransfers = TotalTransfers + 1,
                        TotalQuantity = TotalQuantity + {5},
                        LastTransfer = {6},
                        UpdatedDate = {6},
                        FirstTransfer = CASE WHEN FirstTransfer = '1970-01-01 00:00:00' THEN {6} ELSE FirstTransfer END";
                
                context.Database.ExecuteSqlRaw(sql,
                    transferLog.FromPlayerName,
                    transferLog.FromPlayerAccount ?? (object)DBNull.Value,
                    transferLog.ToPlayerName,
                    transferLog.ToPlayerAccount ?? (object)DBNull.Value,
                    transferLog.TransferType,
                    transferLog.Quantity,
                    transferLog.Timestamp);
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
            if (item.Name.Contains("Pyreal", StringComparison.OrdinalIgnoreCase))
                return quantity;
            if (item.Name.Contains("Chunk", StringComparison.OrdinalIgnoreCase))
                return quantity * ChunkValue;
            if (item.Name.Contains("Ingot", StringComparison.OrdinalIgnoreCase))
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
                if (Config.EnableAdminNotifications)
                {
                    var transferType = transferLog.TransferType.ToUpper().Replace(" ", "_");
                    var adminMessage = $"{transferType}: {transferLog.FromPlayerName} -> {transferLog.ToPlayerName}: {transferLog.ItemName} x{transferLog.Quantity}";
                    
                    // Add location data for chest, ground drop, and ground pickup transfers
                    if (!string.IsNullOrEmpty(transferLog.AdditionalData) && 
                        (transferLog.TransferType == TransferTypeChestDeposit || 
                         transferLog.TransferType == TransferTypeChestWithdrawal ||
                         transferLog.TransferType == TransferTypeGroundDrop ||
                         transferLog.TransferType == TransferTypeGroundPickup))
                    {
                        adminMessage += $" | {transferLog.AdditionalData}";
                    }
                    
                    SendAdminMessage(adminMessage);
                }
            }
            catch (Exception ex)
            {
                log.Error($"Background transfer processing failed: {ex.Message}");
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

        public static List<TransferLog> GetRecentTransfers(int days)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            return DatabaseManager.Shard.BaseDatabase.GetRecentTransfers(cutoffDate);
        }

        public static List<TransferLog> GetTransferPatterns(string playerName, int days)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            return DatabaseManager.Shard.BaseDatabase.GetTransferHistory(playerName, cutoffDate);
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

        public static void UpdateEnableTransferLogging(bool enabled)
        {
            Config.EnableTransferLogging = enabled;
            SaveConfigurationToDatabase();
        }

        public static void UpdateEnableAdminNotifications(bool enabled)
        {
            Config.EnableAdminNotifications = enabled;
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

