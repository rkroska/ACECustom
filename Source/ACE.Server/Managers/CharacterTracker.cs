using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Server.WorldObjects;
using log4net;
using Microsoft.EntityFrameworkCore;

namespace ACE.Server.Managers
{
	/// <summary>
	/// Character login/logout tracking for investigating multi-character violations.
	/// IP addresses are collected for security purposes and retained for investigation.
	/// Note: IP addresses are considered PII under privacy regulations (GDPR, CCPA, etc.)
	/// Consider implementing data retention policies and anonymization as required by your jurisdiction.
	/// </summary>
	public static class CharacterTracker
	{
		private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		private static volatile bool _databaseMigrated = false;
		private static readonly object _migrationLock = new object();
		
		// Semaphore to limit concurrent database operations (max 20 at a time)
		private static readonly SemaphoreSlim _dbSemaphore = new SemaphoreSlim(20, 20);
		
		// Per-character locks to ensure INSERT completes before UPDATE for same character
		private static readonly ConcurrentDictionary<uint, SemaphoreSlim> _characterLocks = new ConcurrentDictionary<uint, SemaphoreSlim>();
		
		// Time window for matching login records on logout (prevents matching old crashed sessions)
		// Set to 7 days to accommodate long play sessions while still filtering ancient crash records
		private static readonly TimeSpan LoginRecordMatchWindow = TimeSpan.FromDays(7);
		
		// Data retention settings
		private static readonly int DataRetentionDays = 90;
		private static DateTime _lastCleanupCheck = DateTime.MinValue;
		private static readonly object _cleanupLock = new object();

		/// <summary>
		/// Ensure the char_tracker table exists in the database
		/// </summary>
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
						log.Info("Running database migration for char_tracker table...");
						if (CreateCharTrackerTable(context))
						{
							log.Info("char_tracker table migration completed successfully");
							_databaseMigrated = true;
							
							// Run initial cleanup check on startup
							Task.Run(() => CheckAndRunCleanup());
						}
						else
						{
							log.Error("char_tracker table migration failed - table creation was unsuccessful");
						}
					}
				}
				catch (Exception ex)
				{
					log.Error($"Database migration failed for char_tracker: {ex.Message}");
					log.Error($"Stack trace: {ex.StackTrace}");
				}
			}
		}

		/// <summary>
		/// Check if cleanup is needed and run it (called periodically)
		/// </summary>
		private static void CheckAndRunCleanup()
		{
			lock (_cleanupLock)
			{
				// Only check once per day
				if ((DateTime.UtcNow - _lastCleanupCheck).TotalDays < 1)
					return;

				_lastCleanupCheck = DateTime.UtcNow;

				try
				{
					log.Info($"Running automatic char_tracker cleanup (retention: {DataRetentionDays} days)...");
					var deletedCount = DeleteOldRecords(DataRetentionDays);
					log.Info($"Automatic cleanup completed: {deletedCount} records deleted");
				}
				catch (Exception ex)
				{
					log.Error($"Error during automatic char_tracker cleanup: {ex.Message}");
				}
			}
		}

		private static bool CreateCharTrackerTable(ShardDbContext context)
		{
			try
			{
				// Check if table exists by trying to select from it
				context.Database.ExecuteSqlRaw("SELECT 1 FROM char_tracker LIMIT 1");
				log.Info("char_tracker table already exists");
				return true;
			}
			catch (Exception)
			{
				// Table doesn't exist, create it
				log.Info("Creating char_tracker table...");
				try
				{
					context.Database.ExecuteSqlRaw(@"
						CREATE TABLE `char_tracker` (
							`Id` int(11) NOT NULL AUTO_INCREMENT,
							`CharacterId` int(10) unsigned NOT NULL,
							`AccountName` varchar(255) DEFAULT NULL,
							`CharacterName` varchar(255) DEFAULT NULL,
							`LoginIP` varchar(50) DEFAULT NULL,
							`LoginTimestamp` datetime(6) NOT NULL,
							`ConnectionDuration` int(11) NOT NULL DEFAULT '0',
							`Landblock` varchar(50) DEFAULT NULL,
							PRIMARY KEY (`Id`),
							KEY `IX_char_tracker_CharacterId` (`CharacterId`),
							KEY `IX_char_tracker_AccountName` (`AccountName`),
							KEY `IX_char_tracker_CharacterName` (`CharacterName`),
							KEY `IX_char_tracker_LoginIP` (`LoginIP`),
							KEY `IX_char_tracker_LoginTimestamp` (`LoginTimestamp`)
						) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");

					log.Info("char_tracker table created successfully");
					return true;
				}
				catch (Exception ex)
				{
					log.Error($"Failed to create char_tracker table: {ex.Message}");
					return false;
				}
			}
		}

		/// <summary>
		/// Get or create a per-character lock to ensure INSERT completes before UPDATE
		/// </summary>
		private static SemaphoreSlim GetCharacterLock(uint characterId)
		{
			return _characterLocks.GetOrAdd(characterId, _ => new SemaphoreSlim(1, 1));
		}

		/// <summary>
		/// Extract IP address from EndPoint
		/// </summary>
		private static string GetPlayerIP(Player player)
		{
			var endPoint = player?.Session?.EndPoint;
			return endPoint?.Address.ToString();
		}

		/// <summary>
		/// Log a character login event
		/// </summary>
		public static void LogCharacterLogin(Player player)
		{
			try
			{
				if (player == null)
				{
					log.Warn("LogCharacterLogin called with null player");
					return;
				}

				// Ensure database is migrated before logging
				EnsureDatabaseMigrated();
				
				// Check if daily cleanup is needed (throttled to once per day)
				Task.Run(() => CheckAndRunCleanup());

				var charTracker = new CharTracker
				{
					CharacterId = player.Character.Id,
					AccountName = player.Account?.AccountName ?? "Unknown",
					CharacterName = player.Name,
					LoginIP = GetPlayerIP(player),
					LoginTimestamp = DateTime.UtcNow,
					ConnectionDuration = 0,
					Landblock = player.Location != null ? $"0x{player.Location.Landblock:X4}" : null
				};

				// Store login timestamp in player for later use on logout
				if (player.Session != null)
				{
					player.Session.LoginTime = DateTime.UtcNow;
				}

				// Process in background thread to prevent blocking the game thread
				Task.Run(() => SaveCharTrackerRecord(charTracker));

				log.Debug($"Character login logged: {player.Name} (Account: {charTracker.AccountName}, IP: {charTracker.LoginIP})");
			}
			catch (Exception ex)
			{
				log.Error($"Error logging character login for {player?.Name}: {ex.Message}");
				log.Error($"Stack trace: {ex.StackTrace}");
			}
		}

		/// <summary>
		/// Update character tracker record with logout time
		/// </summary>
		public static void LogCharacterLogout(Player player)
		{
			try
			{
				if (player == null)
				{
					log.Warn("LogCharacterLogout called with null player");
					return;
				}

				// Ensure database is migrated before logging
				EnsureDatabaseMigrated();

				// Get login time, handling cases where it wasn't set properly
				DateTime loginTime = player.Session?.LoginTime ?? DateTime.MinValue;
				
				// If LoginTime is invalid (never set or MinValue), skip the update
				if (loginTime == DateTime.MinValue || loginTime > DateTime.UtcNow)
				{
					log.Warn($"Invalid LoginTime for character {player.Name}, cannot calculate connection duration");
					return;
				}

				int connectionDuration = (int)(DateTime.UtcNow - loginTime).TotalSeconds;
				
				// Sanity check: duration should be positive and reasonable (not more than 30 days)
				if (connectionDuration < 0 || connectionDuration > 2592000)
				{
					log.Warn($"Invalid connection duration calculated for {player.Name}: {connectionDuration}s");
					return;
				}

				// Process in background thread
				Task.Run(() => UpdateCharTrackerRecord(player.Character.Id, connectionDuration));

				log.Debug($"Character logout logged: {player.Name} (Duration: {connectionDuration}s)");
			}
			catch (Exception ex)
			{
				log.Error($"Error logging character logout for {player?.Name}: {ex.Message}");
				log.Error($"Stack trace: {ex.StackTrace}");
			}
		}

		private static async Task SaveCharTrackerRecord(CharTracker record)
		{
			var characterLock = GetCharacterLock(record.CharacterId);
			
			// Acquire character-specific lock first to ensure ordering
			await characterLock.WaitAsync().ConfigureAwait(false);
			try
			{
				// Then acquire global semaphore to limit total concurrent operations
				await _dbSemaphore.WaitAsync().ConfigureAwait(false);
				try
				{
					using (var context = new ShardDbContext())
					{
						context.CharTracker.Add(record);
						await context.SaveChangesAsync().ConfigureAwait(false);
					}
				}
				catch (Exception ex)
				{
					log.Error($"Error saving char_tracker record: {ex.Message}");
					log.Error($"Stack trace: {ex.StackTrace}");
				}
				finally
				{
					_dbSemaphore.Release();
				}
			}
			finally
			{
				characterLock.Release();
			}
		}

		private static async Task UpdateCharTrackerRecord(uint characterId, int connectionDuration)
		{
			var characterLock = GetCharacterLock(characterId);
			
			// Acquire character-specific lock first to ensure INSERT has completed
			await characterLock.WaitAsync().ConfigureAwait(false);
			try
			{
				// Then acquire global semaphore to limit total concurrent operations
				await _dbSemaphore.WaitAsync().ConfigureAwait(false);
				try
				{
					using (var context = new ShardDbContext())
					{
						// Find the most recent login record for this character with duration still at 0
						// Only match records within time window to avoid updating old crashed sessions
						var cutoffTime = DateTime.UtcNow.Subtract(LoginRecordMatchWindow);
						var record = context.CharTracker
							.Where(c => c.CharacterId == characterId 
								&& c.ConnectionDuration == 0 
								&& c.LoginTimestamp > cutoffTime)
							.OrderByDescending(c => c.LoginTimestamp)
							.FirstOrDefault();

						if (record != null)
						{
							record.ConnectionDuration = connectionDuration;
							await context.SaveChangesAsync().ConfigureAwait(false);
							log.Debug($"Updated char_tracker record ID {record.Id} with duration {connectionDuration}s");
						}
						else
						{
							log.Warn($"Could not find login record to update for character ID {characterId}");
						}
					}
				}
				catch (Exception ex)
				{
					log.Error($"Error updating char_tracker record: {ex.Message}");
					log.Error($"Stack trace: {ex.StackTrace}");
				}
				finally
				{
					_dbSemaphore.Release();
				}
			}
			finally
			{
				characterLock.Release();
				// Note: We don't cleanup the semaphore here to avoid race conditions with concurrent logins
				// The memory impact is negligible (~48 bytes per unique character)
			}
		}

		/// <summary>
		/// Anonymize IP addresses older than specified days for privacy compliance
		/// </summary>
		/// <param name="olderThanDays">Delete IPs from records older than this many days</param>
		/// <returns>Number of records anonymized</returns>
		public static int AnonymizeOldIPAddresses(int olderThanDays)
		{
			try
			{
				EnsureDatabaseMigrated();

				var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);

				using (var context = new ShardDbContext())
				{
					var rowsAffected = context.Database.ExecuteSqlRaw(
						"UPDATE char_tracker SET LoginIP = NULL WHERE LoginTimestamp < {0} AND LoginIP IS NOT NULL",
						cutoffDate);

					log.Info($"Anonymized {rowsAffected} char_tracker IP addresses older than {olderThanDays} days");
					return rowsAffected;
				}
			}
			catch (Exception ex)
			{
				log.Error($"Error anonymizing char_tracker IP addresses: {ex.Message}");
				return 0;
			}
		}

		/// <summary>
		/// Delete records older than specified days to manage data retention
		/// </summary>
		/// <param name="olderThanDays">Delete records older than this many days</param>
		/// <returns>Number of records deleted</returns>
		public static int DeleteOldRecords(int olderThanDays)
		{
			try
			{
				EnsureDatabaseMigrated();

				var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);

				using (var context = new ShardDbContext())
				{
					var rowsAffected = context.Database.ExecuteSqlRaw(
						"DELETE FROM char_tracker WHERE LoginTimestamp < {0}",
						cutoffDate);

					log.Info($"Deleted {rowsAffected} char_tracker records older than {olderThanDays} days");
					return rowsAffected;
				}
			}
			catch (Exception ex)
			{
				log.Error($"Error deleting old char_tracker records: {ex.Message}");
				return 0;
			}
		}
	}
}

