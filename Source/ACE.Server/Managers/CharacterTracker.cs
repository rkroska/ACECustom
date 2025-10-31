using System;
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
	public static class CharacterTracker
	{
		private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		private static volatile bool _databaseMigrated = false;
		private static readonly object _migrationLock = new object();
		
		// Semaphore to limit concurrent database operations (max 20 at a time)
		private static readonly SemaphoreSlim _dbSemaphore = new SemaphoreSlim(20, 20);

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
						CreateCharTrackerTable(context);
						log.Info("char_tracker table migration completed successfully");
						_databaseMigrated = true;
					}
				}
				catch (Exception ex)
				{
					log.Error($"Database migration failed for char_tracker: {ex.Message}");
				}
			}
		}

		private static void CreateCharTrackerTable(ShardDbContext context)
		{
			try
			{
				// Check if table exists by trying to select from it
				context.Database.ExecuteSqlRaw("SELECT 1 FROM char_tracker LIMIT 1");
				log.Info("char_tracker table already exists");
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
				}
				catch (Exception ex)
				{
					log.Error($"Failed to create char_tracker table: {ex.Message}");
				}
			}
		}

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

		private static async Task UpdateCharTrackerRecord(uint characterId, int connectionDuration)
		{
			// Small delay to reduce race condition with INSERT
			await Task.Delay(100).ConfigureAwait(false);
			
			await _dbSemaphore.WaitAsync().ConfigureAwait(false);
			try
			{
				using (var context = new ShardDbContext())
				{
					// Find the most recent login record for this character with duration still at 0
					var record = context.CharTracker
						.Where(c => c.CharacterId == characterId && c.ConnectionDuration == 0)
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
						log.Warn($"Could not find login record to update for character ID {characterId} (may have already been updated or INSERT pending)");
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
	}
}

