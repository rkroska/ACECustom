using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using log4net;

using ACE.Common;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Network.Enum;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

using Biota = ACE.Entity.Models.Biota;

namespace ACE.Server.Managers
{
    public static class PlayerManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly ReaderWriterLockSlim playersLock = new ReaderWriterLockSlim();
        private static readonly Dictionary<uint, Player> onlinePlayers = new Dictionary<uint, Player>();
        private static readonly Dictionary<uint, OfflinePlayer> offlinePlayers = new Dictionary<uint, OfflinePlayer>();

        /// <summary>accountId -> character guids (online and offline), so an account lookup is bounded by the account's
        /// character count instead of a scan of every player on the shard (CodeRabbit #520). Guarded by playersLock.</summary>
        private static readonly Dictionary<uint, HashSet<uint>> accountCharacters = new Dictionary<uint, HashSet<uint>>();

        /// <summary>Caller holds playersLock in write mode.</summary>
        private static void IndexAccountCharacter(IPlayer player)
        {
            var accountId = player?.Account?.AccountId;
            if (accountId == null)
                return;

            if (!accountCharacters.TryGetValue(accountId.Value, out var guids))
            {
                guids = new HashSet<uint>();
                accountCharacters[accountId.Value] = guids;
            }
            guids.Add(player.Guid.Full);
        }

        /// <summary>Caller holds playersLock in write mode.</summary>
        private static void UnindexAccountCharacter(IPlayer player)
        {
            var accountId = player?.Account?.AccountId;
            if (accountId == null || !accountCharacters.TryGetValue(accountId.Value, out var guids))
                return;

            guids.Remove(player.Guid.Full);
            if (guids.Count == 0)
                accountCharacters.Remove(accountId.Value);
        }

        /// <summary>
        /// OfflinePlayers will be saved to the database every 1 hour
        /// </summary>
        private static readonly TimeSpan databaseSaveInterval = TimeSpan.FromHours(1);

        /// <summary>
        /// Timestamp of the last offline save check. Updated every hour regardless of whether saves were needed.
        /// Thread-safe: Tick() is called from single-threaded WorldManager.UpdateWorld() loop.
        /// </summary>
        private static DateTime lastOfflineSaveCheck = DateTime.MinValue;

        /// <summary>
        /// This will load all the players from the database into the OfflinePlayers dictionary. It should be called before WorldManager is initialized.
        /// </summary>
        public static void Initialize()
        {
            var results = DatabaseManager.Shard.BaseDatabase.GetAllPlayerBiotasInParallel();

            Parallel.ForEach(results, result =>
            {
                try
                {
                    var offlinePlayer = new OfflinePlayer(result);

                    lock (offlinePlayers)
                        offlinePlayers[offlinePlayer.Guid.Full] = offlinePlayer;
                }
                catch (Exception ex)
                {
                    log.Error($"[PLAYERMANAGER] Failed to initialize OfflinePlayer for Biota.Id={result.Id}: {ex}");
                }
            });

            playersLock.EnterWriteLock();
            try
            {
                foreach (var offlinePlayer in offlinePlayers.Values)
                    IndexAccountCharacter(offlinePlayer);
            }
            finally
            {
                playersLock.ExitWriteLock();
            }
        }

        private static readonly LinkedList<Player> playersPendingLogoff = new LinkedList<Player>();

        public static void AddPlayerToLogoffQueue(Player player)
        {
            if (!playersPendingLogoff.Contains(player))
                playersPendingLogoff.AddLast(player);
        }

        public static void Tick()
        {
            // Database Save - only check once per hour
            if (lastOfflineSaveCheck + databaseSaveInterval <= DateTime.UtcNow)
            {
                var now = DateTime.UtcNow;
                log.Debug("[PLAYERMANAGER] Performing hourly offline save check");
                try
                {
                    SaveOfflinePlayersWithChanges();
                }
                catch (Exception ex)
                {
                    log.Error($"[PLAYERMANAGER] Hourly offline save check threw: {ex}");
                }
                finally
                {
                    lastOfflineSaveCheck = now; // Always update timestamp
                }
            }

            var currentUnixTime = Time.GetUnixTime();

            while (playersPendingLogoff.Count > 0)
            {
                var first = playersPendingLogoff.First.Value;

                if (first.LogoffTimestamp <= currentUnixTime)
                {
                    playersPendingLogoff.RemoveFirst();
                    first.LogOut_Inner();
                    first.Session.logOffRequestTime = DateTime.UtcNow;
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Queues a background task to save any offline players that have ChangesDetected.
        /// Actual persistence is performed by PerformOfflinePlayerSaves() on the DB worker.
        /// </summary>
        public static void SaveOfflinePlayersWithChanges()
        {

            // Check if there are actually players with changes to save
            var playersWithChanges = 0;
            
            playersLock.EnterReadLock();
            try
            {
                playersWithChanges = offlinePlayers.Values.Count(p => p.ChangesDetected);
            }
            finally
            {
                playersLock.ExitReadLock();
            }

            // Only queue the save if there are actually changes to save
            if (playersWithChanges > 0)
            {
                log.Info($"[PLAYERMANAGER] Queuing offline save for {playersWithChanges} players with changes");
                DatabaseManager.Shard.QueueOfflinePlayerSaves(success =>
                {
                    if (success)
                        log.Info($"[PLAYERMANAGER] Offline save tasks dispatched for {playersWithChanges} players");
                    else
                        log.Warn("[PLAYERMANAGER] Offline save task dispatch failed (reflection or invocation issue).");
                });
            }
            else
            {
                log.Debug("[PLAYERMANAGER] No offline players with changes to save");
            }
        }

        /// <summary>
        /// Internal method to actually perform the offline player saves.
        /// This is called by the queue system.
        /// </summary>
        internal static void PerformOfflinePlayerSaves()
        {
            log.Info("[PLAYERMANAGER] Performing offline save operation");
            
            var playersToSave = new List<OfflinePlayer>();
            
            playersLock.EnterReadLock();
            try
            {
                playersToSave = offlinePlayers.Values.Where(p => p.ChangesDetected).ToList();
            }
            finally
            {
                playersLock.ExitReadLock();
            }

            if (playersToSave.Count > 0)
            {
                log.Info($"[PLAYERMANAGER] Enqueuing saves for {playersToSave.Count} offline players with changes");
                
                // Save each player with changes
                foreach (var player in playersToSave)
                {
                    try
                    {
                        // enqueue actual DB save with completion callback to ensure retry on failure
                        player.SaveBiotaToDatabase(true, result =>
                        {
                            if (!result)
                            {
                                // Re-flag for retry on failure
                                playersLock.EnterWriteLock();
                                try { player.ChangesDetected = true; } finally { playersLock.ExitWriteLock(); }
                                log.Error($"[PLAYERMANAGER] Offline save failed for {player.Name} ({player.Guid.Full}); will retry next cycle");
                            }
                            else
                            {
                                log.Debug($"[PLAYERMANAGER] Saved offline player: {player.Name}");
                            }
                        });
                        log.Debug($"[PLAYERMANAGER] Enqueued save for offline player: {player.Name}");
                    }
                    catch (Exception ex)
                    {
                        log.Error($"[PLAYERMANAGER] Failed to enqueue save for offline player {player.Name} ({player.Guid.Full}): {ex}");
                    }
                }
                
                log.Info($"[PLAYERMANAGER] Enqueued saves for {playersToSave.Count} offline players");
            }
            else
            {
                log.Debug("[PLAYERMANAGER] No offline players with changes to save");
            }
        }
        

        /// <summary>
        /// This would be used when a new player is created after the server has started.
        /// When a new Player is created, they're created in an offline state, and then set to online shortly after as the login sequence continues.
        /// </summary>
        public static void AddOfflinePlayer(Player player)
        {
            playersLock.EnterWriteLock();
            try
            {
                var offlinePlayer = new OfflinePlayer(player.Biota);

                // CodeRabbit #520: a replaced entry must not leave its old account mapping behind
                if (offlinePlayers.TryGetValue(offlinePlayer.Guid.Full, out var previous))
                    UnindexAccountCharacter(previous);

                offlinePlayers[offlinePlayer.Guid.Full] = offlinePlayer;
                IndexAccountCharacter(offlinePlayer);
            }
            finally
            {
                playersLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// This will return null if the player wasn't found.
        /// </summary>
        public static OfflinePlayer GetOfflinePlayer(ObjectGuid guid)
        {
            return GetOfflinePlayer(guid.Full);
        }

        /// <summary>
        /// This will return null if the player wasn't found.
        /// </summary>
        public static OfflinePlayer GetOfflinePlayer(uint guid)
        {
            playersLock.EnterReadLock();
            try
            {
                if (offlinePlayers.TryGetValue(guid, out var value))
                    return value;
            }
            finally
            {
                playersLock.ExitReadLock();
            }

            return null;
        }

        /// <summary>
        /// This will return null of the name was not found.
        /// </summary>
        public static OfflinePlayer GetOfflinePlayer(string name)
        {
            var admin = "+" + name;

            playersLock.EnterReadLock();
            try
            {
                var offlinePlayer = offlinePlayers.Values.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || p.Name.Equals(admin, StringComparison.OrdinalIgnoreCase));

                if (offlinePlayer != null)
                    return offlinePlayer;
            }
            finally
            {
                playersLock.ExitReadLock();
            }

            return null;
        }

        public static List<IPlayer> GetAllPlayers()
        {
            var offlinePlayers = GetAllOffline();
            var onlinePlayers = GetAllOnline();

            var allPlayers = new List<IPlayer>();

            allPlayers.AddRange(offlinePlayers);
            allPlayers.AddRange(onlinePlayers);

            return allPlayers;
        }

        /// <summary>
        /// Returns all players (online and offline) that match the given predicate, searching online players first for performance.
        /// </summary>
        public static List<IPlayer> FindAllPlayers(Func<IPlayer, bool> predicate)
        {
            var results = new List<IPlayer>();
            
            playersLock.EnterReadLock();
            try
            {
                // Search online players first (smaller collection, faster)
                var onlineMatches = onlinePlayers.Values.Where(predicate);
                results.AddRange(onlineMatches);
                
                // Then search offline players
                var offlineMatches = offlinePlayers.Values.Where(predicate);
                results.AddRange(offlineMatches);
            }
            finally
            {
                playersLock.ExitReadLock();
            }
            
            return results;
        }

        /// <summary>
        /// Returns the first player (online or offline) that matches the given predicate, searching online players first for performance.
        /// </summary>
        public static IPlayer FindFirstPlayer(Func<IPlayer, bool> predicate)
        {
            playersLock.EnterReadLock();
            try
            {
                // Search online players first (smaller collection, faster)
                var onlineMatch = onlinePlayers.Values.FirstOrDefault(predicate);
                if (onlineMatch != null)
                    return onlineMatch;
                
                // Only search offline players if not found online
                return offlinePlayers.Values.FirstOrDefault(predicate);
            }
            finally
            {
                playersLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Returns the first player (online or offline) that matches the given name, searching online players first for performance.
        /// Handles admin names with + prefix and case-insensitive matching.
        /// </summary>
        public static IPlayer FindFirstPlayerByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            
            playersLock.EnterReadLock();
            try
            {
                var normalizedName = name.Trim();
                
                // Search online players first (smaller collection, faster)
                var onlinePlayer = onlinePlayers.Values.FirstOrDefault(p => 
                    p.Name.TrimStart('+').Equals(normalizedName.TrimStart('+'), StringComparison.OrdinalIgnoreCase));
                
                if (onlinePlayer != null)
                    return onlinePlayer;
                
                // Only search offline players if not found online
                var offlinePlayer = offlinePlayers.Values.FirstOrDefault(p => 
                    p.Name.TrimStart('+').Equals(normalizedName.TrimStart('+'), StringComparison.OrdinalIgnoreCase) && 
                    !p.IsPendingDeletion);
                
                return offlinePlayer;
            }
            finally
            {
                playersLock.ExitReadLock();
            }
        }

        public static int GetOfflineCount()
        {
            playersLock.EnterReadLock();
            try
            {
                return offlinePlayers.Count;
            }
            finally
            {
                playersLock.ExitReadLock();
            }
        }

        public static List<OfflinePlayer> GetAllOffline()
        {
            playersLock.EnterReadLock();
            try
            {
                return new List<OfflinePlayer>(offlinePlayers.Values);
            }
            finally
            {
                playersLock.ExitReadLock();
            }
        }

        public static int GetOnlineCount()
        {
            playersLock.EnterReadLock();
            try
            {
                return onlinePlayers.Count;
            }
            finally
            {
                playersLock.ExitReadLock();
            }
        }

        /// <summary>
        /// This will return null if the player wasn't found.
        /// </summary>
        public static Player GetOnlinePlayer(ObjectGuid guid)
        {
            return GetOnlinePlayer(guid.Full);
        }

        /// <summary>
        /// This will return null if the player wasn't found.
        /// </summary>
        public static Player GetOnlinePlayer(uint guid)
        {
            playersLock.EnterReadLock();
            try
            {
                if (onlinePlayers.TryGetValue(guid, out var value))
                    return value;
            }
            finally
            {
                playersLock.ExitReadLock();
            }

            return null;
        }

        /// <summary>
        /// This will return null of the name was not found.
        /// </summary>
        public static Player GetOnlinePlayer(string name)
        {
            var admin = "+" + name;

            playersLock.EnterReadLock();
            try
            {
                var onlinePlayer = onlinePlayers.Values.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || p.Name.Equals(admin, StringComparison.OrdinalIgnoreCase));

                if (onlinePlayer != null)
                    return onlinePlayer;
            }
            finally
            {
                playersLock.ExitReadLock();
            }

            return null;
        }

        public static List<Player> GetAllOnline()
        {
            playersLock.EnterReadLock();
            try
            {
                return new List<Player>(onlinePlayers.Values);
            }
            finally
            {
                playersLock.ExitReadLock();
            }
        }


        /// <summary>
        /// This will return true if the player was successfully added.
        /// It will return false if the player was not found in the OfflinePlayers dictionary (which should never happen), or player already exists in the OnlinePlayers dictionary (which should never happen).
        /// This will always be preceded by a call to GetOfflinePlayer()
        /// </summary>
        public static bool SwitchPlayerFromOfflineToOnline(Player player)
        {
            playersLock.EnterWriteLock();
            try
            {
                if (!offlinePlayers.Remove(player.Guid.Full, out var offlinePlayer))
                    return false; // This should never happen

                if (offlinePlayer.ChangesDetected)
                    player.ChangesDetected = true;

                player.Allegiance = offlinePlayer.Allegiance;
                player.AllegianceNode = offlinePlayer.AllegianceNode;

                if (!onlinePlayers.TryAdd(player.Guid.Full, player))
                    return false;   // the guid is already online: still in a dictionary, the account index stays as it is
            }
            finally
            {
                playersLock.ExitWriteLock();
            }

            AllegianceManager.LoadPlayer(player);

            ApplyAccountGag(player);   // account-wide gag, 2026-09-13

            player.SendFriendStatusUpdates();

            return true;
        }

        /// <summary>
        /// This will return true if the player was successfully added.
        /// It will return false if the player was not found in the OnlinePlayers dictionary (which should never happen), or player already exists in the OfflinePlayers dictionary (which should never happen).
        /// </summary>
        public static bool SwitchPlayerFromOnlineToOffline(Player player)
        {
            playersLock.EnterWriteLock();
            try
            {
                if (!onlinePlayers.Remove(player.Guid.Full, out _))
                    return false; // This should never happen

                var offlinePlayer = new OfflinePlayer(player.Biota);

                offlinePlayer.Allegiance = player.Allegiance;
                offlinePlayer.AllegianceNode = player.AllegianceNode;
                
                // Transfer save state to offline player for login blocking
                offlinePlayer.SaveInProgress = player.SaveInProgress;
                offlinePlayer.LastRequestedDatabaseSave = player.LastRequestedDatabaseSave;

                if (!offlinePlayers.TryAdd(offlinePlayer.Guid.Full, offlinePlayer))
                    return false;   // the guid is already offline: still in a dictionary, the account index stays as it is
            }
            finally
            {
                playersLock.ExitWriteLock();
            }

            player.SendFriendStatusUpdates(false);
            player.CleanupPrestigeEffects();
            player.CleanupZoneBoundaryEffects();
            player.HandleAllegianceOnLogout();

            return true;
        }

        /// <summary>
        /// Called when a character is initially deleted on the character select screen
        /// </summary>
        public static void HandlePlayerDelete(uint characterGuid)
        {
            AllegianceManager.HandlePlayerDelete(characterGuid);

            HouseManager.HandlePlayerDelete(characterGuid);
        }

        /// <summary>
        /// This will return true if the player was successfully found and removed from the OfflinePlayers dictionary.
        /// It will return false if the player was not found in the OfflinePlayers dictionary (which should never happen).
        /// </summary>
        public static bool ProcessDeletedPlayer(uint guid)
        {
            playersLock.EnterWriteLock();
            try
            {
                if (!offlinePlayers.Remove(guid, out var offlinePlayer))
                    return false; // This should never happen

                UnindexAccountCharacter(offlinePlayer);
            }
            finally
            {
                playersLock.ExitWriteLock();
            }

            return true;
        }


        /// <summary>
        /// This will return null if the name was not found.
        /// </summary>
        public static IPlayer FindByName(string name)
        {
            return FindByName(name, out _);
        }

        /// <summary>
        /// This will return null if the name was not found.
        /// </summary>
        public static IPlayer FindByName(string name, out bool isOnline)
        {
            playersLock.EnterReadLock();
            try
            {
                var onlinePlayer = onlinePlayers.Values.FirstOrDefault(p => p.Name.TrimStart('+').Equals(name.TrimStart('+'), StringComparison.OrdinalIgnoreCase));

                if (onlinePlayer != null)
                {
                    isOnline = true;
                    return onlinePlayer;
                }

                isOnline = false;

                var offlinePlayer = offlinePlayers.Values.FirstOrDefault(p => p.Name.TrimStart('+').Equals(name.TrimStart('+'), StringComparison.OrdinalIgnoreCase) && !p.IsPendingDeletion);

                if (offlinePlayer != null)
                    return offlinePlayer;
            }
            finally
            {
                playersLock.ExitReadLock();
            }

            return null;
        }

        /// <summary>
        /// This will return null if the guid was not found.
        /// </summary>
        public static IPlayer FindByGuid(ObjectGuid guid)
        {
            return FindByGuid(guid, out _);
        }

        /// <summary>
        /// This will return null if the guid was not found.
        /// </summary>
        public static IPlayer FindByGuid(ObjectGuid guid, out bool isOnline)
        {
            return FindByGuid(guid.Full, out isOnline);
        }

        /// <summary>
        /// This will return null if the guid was not found.
        /// </summary>
        public static IPlayer FindByGuid(uint guid)
        {
            return FindByGuid(guid, out _);
        }

        /// <summary>
        /// This will return null if the guid was not found.
        /// </summary>
        public static IPlayer FindByGuid(uint guid, out bool isOnline)
        {
            playersLock.EnterReadLock();
            try
            {
                if (onlinePlayers.TryGetValue(guid, out var onlinePlayer))
                {
                    isOnline = true;
                    return onlinePlayer;
                }

                isOnline = false;

                if (offlinePlayers.TryGetValue(guid, out var offlinePlayer))
                    return offlinePlayer;
            }
            finally
            {
                playersLock.ExitReadLock();
            }

            return null;
        }


        /// <summary>
        /// Returns a list of all players who are under a monarch
        /// </summary>
        /// <param name="monarch">The monarch of an allegiance</param>
        public static List<IPlayer> FindAllByMonarch(ObjectGuid monarch)
        {
            var results = new List<IPlayer>();

            playersLock.EnterReadLock();
            try
            {
                var onlinePlayersResult = onlinePlayers.Values.Where(p => p.MonarchId == monarch.Full);
                var offlinePlayersResult = offlinePlayers.Values.Where(p => p.MonarchId == monarch.Full);

                results.AddRange(onlinePlayersResult);
                results.AddRange(offlinePlayersResult);
            }
            finally
            {
                playersLock.ExitReadLock();
            }

            return results;
        }


        /// <summary>
        /// This will return a list of Players that have this guid as a friend.
        /// </summary>
        public static List<Player> GetOnlineInverseFriends(ObjectGuid guid)
        {
            var results = new List<Player>();

            playersLock.EnterReadLock();
            try
            {
                foreach (var player in onlinePlayers.Values)
                {
                    if (player.Character.HasAsFriend(guid.Full, player.CharacterDatabaseLock))
                        results.Add(player);
                }
            }
            finally
            {
                playersLock.ExitReadLock();
            }

            return results;
        }


        /// <summary>
        /// Broadcasts GameMessage to all online sessions.
        /// </summary>
        public static void BroadcastToAll(OutboundGameMessage msg)
        {
            foreach (var player in GetAllOnline())
                player.Session.Network.EnqueueSend(msg);
        }

        public static void BroadcastToAuditChannel(Player issuer, string message, ACE.Common.DiscordLogLevel requiredLevel = ACE.Common.DiscordLogLevel.Info)
        {
            if (issuer != null)
            { 
                BroadcastToChannel(Channel.Audit, issuer, message, true, true);
                if (ConfigManager.Config.Chat != null && ACE.Server.Managers.ServerConfig.discord_audit_level.Value >= (long)requiredLevel)
                    _ = DiscordChatManager.SendDiscordMessage(issuer.Name, message, ConfigManager.Config.Chat.AdminAuditId);
            }
            else
            {
                BroadcastToChannelFromConsole(Channel.Audit, message);
                if (ConfigManager.Config.Chat != null && ACE.Server.Managers.ServerConfig.discord_audit_level.Value >= (long)requiredLevel)
                    _ = DiscordChatManager.SendDiscordMessage("Console", message, ConfigManager.Config.Chat.AdminAuditId);
            }
                

            //if (PropertyManager.GetBool("log_audit", true))
                //log.Info($"[AUDIT] {(issuer != null ? $"{issuer.Name} says on the Audit channel: " : "")}{message}");

            //LogBroadcastChat(Channel.Audit, issuer, message);
        }

        public static void BroadcastToChannel(Channel channel, Player sender, string message, bool ignoreSquelch = false, bool ignoreActive = false)
        {
            if ((sender.ChannelsActive.HasValue && sender.ChannelsActive.Value.HasFlag(channel)) || ignoreActive)
            {
                var onlinePlayers = GetAllOnline();
                foreach (var player in onlinePlayers)
                {
                    if ((player.ChannelsActive ?? 0).HasFlag(channel))
                    {
                        if (!player.SquelchManager.Squelches.Contains(sender) || ignoreSquelch)
                            player.Session.Network.EnqueueSend(new GameEventChannelBroadcast(player.Session, channel, sender.Guid == player.Guid ? "" : sender.Name, message));
                    }
                }

                LogBroadcastChat(channel, sender, message);
            }
        }

        public static void LogBroadcastChat(Channel channel, WorldObject sender, string message)
        {
            switch (channel)
            {
                case Channel.Abuse:
                    if (!ServerConfig.chat_log_abuse.Value)
                        return;
                    break;
                case Channel.Admin:
                    if (!ServerConfig.chat_log_admin.Value)
                        return;
                    break;
                case Channel.AllBroadcast: // using this to sub in for a WorldBroadcast channel which isn't technically a channel
                    if (!ServerConfig.chat_log_global.Value)
                        return;
                    break;
                case Channel.Audit:
                    if (!ServerConfig.chat_log_audit.Value)
                        return;
                    break;
                case Channel.Advocate1:
                case Channel.Advocate2:
                case Channel.Advocate3:
                    if (!ServerConfig.chat_log_advocate.Value)
                        return;
                    break;
                case Channel.Debug:
                    if (!ServerConfig.chat_log_debug.Value)
                        return;
                    break;
                case Channel.Fellow:
                case Channel.FellowBroadcast:
                    if (!ServerConfig.chat_log_fellow.Value)
                        return;
                    break;
                case Channel.Help:
                    if (!ServerConfig.chat_log_help.Value)
                        return;
                    break;
                case Channel.Olthoi:
                    if (!ServerConfig.chat_log_olthoi.Value)
                        return;
                    break;
                case Channel.QA1:
                case Channel.QA2:
                    if (!ServerConfig.chat_log_qa.Value)
                        return;
                    break;
                case Channel.Sentinel:
                    if (!ServerConfig.chat_log_sentinel.Value)
                        return;
                    break;

                case Channel.SocietyCelHanBroadcast:
                case Channel.SocietyEldWebBroadcast:
                case Channel.SocietyRadBloBroadcast:
                    if (!ServerConfig.chat_log_society.Value)
                        return;
                    break;

                case Channel.AllegianceBroadcast:
                case Channel.CoVassals:
                case Channel.Monarch:
                case Channel.Patron:
                case Channel.Vassals:
                    if (!ServerConfig.chat_log_allegiance.Value)
                        return;
                    break;

                case Channel.AlArqas:
                case Channel.Holtburg:
                case Channel.Lytelthorpe:
                case Channel.Nanto:
                case Channel.Rithwic:
                case Channel.Samsur:
                case Channel.Shoushi:
                case Channel.Yanshi:
                case Channel.Yaraq:
                    if (!ServerConfig.chat_log_townchans.Value)
                        return;
                    break;

                default:
                    return;
            }

            if (channel != Channel.AllBroadcast)
                log.Info($"[CHAT][{channel.ToString().ToUpper()}] {(sender != null ? sender.Name : "[SYSTEM]")} says on the {channel} channel, \"{message}\"");
            else
                log.Info($"[CHAT][GLOBAL] {(sender != null ? sender.Name : "[SYSTEM]")} issued a world broadcast, \"{message}\"");
        }

        public static void BroadcastToChannelFromConsole(Channel channel, string message)
        {
            var onlinePlayers = GetAllOnline();
            foreach (var player in onlinePlayers)
            {
                if ((player.ChannelsActive ?? 0).HasFlag(channel))
                    player.Session.Network.EnqueueSend(new GameEventChannelBroadcast(player.Session, channel, "CONSOLE", message));
            }

            LogBroadcastChat(channel, null, message);
        }

        public static void BroadcastToChannelFromEmote(Channel channel, string message)
        {
            var onlinePlayers = GetAllOnline();
            foreach (var player in onlinePlayers)
            {
                if ((player.ChannelsActive ?? 0).HasFlag(channel))
                    player.Session.Network.EnqueueSend(new GameEventChannelBroadcast(player.Session, channel, "EMOTE", message));
            }
        }

        public const double DefaultGagSeconds = 300;

        /// <summary>
        /// Gags a character for <paramref name="durationSeconds"/> of WALL-CLOCK time (2026-09-13: it used to count down
        /// only while the player was online, one heartbeat at a time, so a long gag on a player who logged out never
        /// ran out). GagTimestamp + GagDuration is the expiry; Player.GagsTick enforces it on every heartbeat.
        /// </summary>
        public static bool GagPlayer(Player issuer, string playerName, double durationSeconds = DefaultGagSeconds, string reason = null)
        {
            var player = FindByName(playerName);

            if (player == null)
                return false;

            if (durationSeconds <= 0)
                durationSeconds = DefaultGagSeconds;

            var timestamp = Common.Time.GetUnixTime();

            // ACCOUNT-WIDE (owner 2026-09-13): a gag by character name used to follow that one character, so switching
            // characters walked around it. Every character on the account gets the same gag now; a character created
            // later inherits it at login (ApplyAccountGag).
            var characters = GetAccountCharacters(player);
            foreach (var character in characters)
            {
                character.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.IsGagged, true);
                character.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.GagTimestamp, timestamp);
                character.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.GagDuration, durationSeconds);
                character.SaveBiotaToDatabase();

                // an online character hears about it now, not on the next heartbeat
                if (character is Player online)
                    online.NotifyGagged();
            }

            var reasonText = string.IsNullOrWhiteSpace(reason) ? "" : $" Reason: {reason.Trim()}";
            var othersText = characters.Count > 1 ? $" and {characters.Count - 1} other character(s) on the account" : "";
            BroadcastToAuditChannel(issuer, $"{issuer.Name} has gagged {player.Name}{othersText} for {FormatDuration(durationSeconds)}.{reasonText}");

            return true;
        }

        /// <summary>Every character (online and offline) on the same account as <paramref name="player"/>, the player itself included.
        /// Falls back to just the player when the account is unknown.</summary>
        public static List<IPlayer> GetAccountCharacters(IPlayer player)
        {
            var result = new List<IPlayer>();
            var accountId = player?.Account?.AccountId;
            if (accountId == null)
            {
                if (player != null) result.Add(player);
                return result;
            }

            playersLock.EnterReadLock();
            try
            {
                if (accountCharacters.TryGetValue(accountId.Value, out var guids))
                {
                    foreach (var guid in guids)
                    {
                        if (onlinePlayers.TryGetValue(guid, out var online))
                            result.Add(online);
                        else if (offlinePlayers.TryGetValue(guid, out var offline))
                            result.Add(offline);
                    }
                }
            }
            finally
            {
                playersLock.ExitReadLock();
            }

            if (result.Count == 0)
                result.Add(player);
            return result;
        }

        /// <summary>
        /// Login half of the account-wide gag: if any other character on the account carries an active gag that
        /// outlasts this character's own, copy it over. Covers a character created after the gag and any gag set
        /// before gags became account-wide.
        /// </summary>
        public static void ApplyAccountGag(Player player)
        {
            if (player?.Account == null)
                return;

            var now = Common.Time.GetUnixTime();
            var ownExpiry = player.IsGagged ? player.GagTimestamp + player.GagDuration : 0;
            IPlayer source = null;
            double sourceTimestamp = 0, sourceDuration = 0, bestExpiry = ownExpiry;

            foreach (var other in GetAccountCharacters(player))
            {
                if (ReferenceEquals(other, player))
                    continue;
                if (!(other.GetProperty(ACE.Entity.Enum.Properties.PropertyBool.IsGagged) ?? false))
                    continue;

                var ts = other.GetProperty(ACE.Entity.Enum.Properties.PropertyFloat.GagTimestamp) ?? 0;
                var dur = other.GetProperty(ACE.Entity.Enum.Properties.PropertyFloat.GagDuration) ?? 0;
                var expiry = ts + dur;
                if (expiry > now && expiry > bestExpiry)
                {
                    bestExpiry = expiry;
                    source = other;
                    sourceTimestamp = ts;
                    sourceDuration = dur;
                }
            }

            if (source == null)
            {
                // No sibling gag to inherit: an expired gag of our own is cleared now rather than on the first
                // heartbeat, so the first chat line after login is not refused (CodeRabbit #520).
                if (player.IsGagged && ownExpiry <= now)
                {
                    player.IsGagged = false;
                    player.GagTimestamp = 0;
                    player.GagDuration = 0;
                    player.SaveBiotaToDatabase();
                }
                return;
            }

            player.IsGagged = true;
            player.GagTimestamp = sourceTimestamp;
            player.GagDuration = sourceDuration;
            player.SaveBiotaToDatabase();
            log.Info($"[GAG] {player.Name} inherited the account gag from {source.Name} at login: {FormatDuration(bestExpiry - now)} remaining.");
        }

        /// <summary>"1 day 2 hours 5 minutes" - the largest three units that are non-zero, seconds only under a minute.</summary>
        public static string FormatDuration(double seconds)
        {
            if (Math.Ceiling(seconds) < 60)
                return $"{(int)Math.Ceiling(seconds)} second{((int)Math.Ceiling(seconds) == 1 ? "" : "s")}";

            var total = (long)Math.Round(seconds);
            var days = total / 86400; total %= 86400;
            var hours = total / 3600; total %= 3600;
            var minutes = (long)Math.Ceiling(total / 60.0);
            if (minutes == 60) { hours++; minutes = 0; }
            if (hours == 24) { days++; hours = 0; }

            var parts = new List<string>();
            if (days > 0) parts.Add($"{days} day{(days == 1 ? "" : "s")}");
            if (hours > 0) parts.Add($"{hours} hour{(hours == 1 ? "" : "s")}");
            if (minutes > 0) parts.Add($"{minutes} minute{(minutes == 1 ? "" : "s")}");
            return string.Join(" ", parts);
        }

        public static bool UnGagPlayer(Player issuer, string playerName)
        {
            var player = FindByName(playerName);

            if (player == null)
                return false;

            // The audit line counts characters whose gag was actually cleared, not every character on the
            // account (CodeRabbit #520).
            var clearedOthers = 0;
            var targetWasGagged = false;

            var characters = GetAccountCharacters(player);
            foreach (var character in characters)
            {
                var wasGagged = character.GetProperty(ACE.Entity.Enum.Properties.PropertyBool.IsGagged) ?? false;
                if (!wasGagged)
                    continue;   // nothing to clear, and an offline save would delay that character's next login

                character.RemoveProperty(ACE.Entity.Enum.Properties.PropertyBool.IsGagged);
                character.RemoveProperty(ACE.Entity.Enum.Properties.PropertyFloat.GagTimestamp);
                character.RemoveProperty(ACE.Entity.Enum.Properties.PropertyFloat.GagDuration);
                character.SaveBiotaToDatabase();

                if (character.Guid.Full == player.Guid.Full)
                    targetWasGagged = true;
                else
                    clearedOthers++;

                if (character is Player online)
                    online.NotifyUngagged();
            }

            if (!targetWasGagged && clearedOthers == 0)
            {
                BroadcastToAuditChannel(issuer, $"{issuer.Name} ran ungag on {player.Name}, but no character on the account was gagged.");
                return true;
            }

            if (targetWasGagged)
            {
                var othersText = clearedOthers > 0 ? $" and {clearedOthers} other character(s) on the account" : "";
                BroadcastToAuditChannel(issuer, $"{issuer.Name} has ungagged {player.Name}{othersText}.");
            }
            else
                BroadcastToAuditChannel(issuer, $"{issuer.Name} has ungagged {clearedOthers} character(s) on {player.Name}'s account.");

            return true;
        }

        public static void BootAllPlayers()
        {
            foreach (var player in GetAllOnline().Where(p => p.Session.AccessLevel < AccessLevel.Advocate))
                player.Session.Terminate(SessionTerminationReason.WorldClosed, new GameMessageBootAccount(" because the world is now closed"), null, "The world is now closed");
        }

        public static void UpdatePKStatusForAllPlayers(string worldType, bool enabled)
        {
            switch (worldType)
            {
                case "pk_server":
                    if (enabled)
                    {
                        foreach (var player in GetAllOnline())
                            player.SetPlayerKillerStatus(PlayerKillerStatus.PK, true);

                        foreach (var player in GetAllOffline())
                        {
                            player.SetProperty(PropertyInt.PlayerKillerStatus, (int)PlayerKillerStatus.NPK);
                            player.SetProperty(PropertyFloat.MinimumTimeSincePk, 0);
                        }

                        var msg = $"This world has been changed to a Player Killer world. All players will become Player Killers in {ServerConfig.pk_respite_timer.Value} seconds.";
                        BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
                        LogBroadcastChat(Channel.AllBroadcast, null, msg);
                    }
                    else
                    {
                        foreach (var player in GetAllOnline())
                            player.SetPlayerKillerStatus(PlayerKillerStatus.NPK, true);

                        foreach (var player in GetAllOffline())
                        {
                            player.SetProperty(PropertyInt.PlayerKillerStatus, (int)PlayerKillerStatus.NPK);
                            player.SetProperty(PropertyFloat.MinimumTimeSincePk, 0);
                        }

                        var msg = "This world has been changed to a Non Player Killer world. All players are now Non-Player Killers.";
                        BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
                        LogBroadcastChat(Channel.AllBroadcast, null, msg);
                    }
                    break;
                case "pkl_server":
                    if (ServerConfig.pk_server.Value)
                        return;
                    if (enabled)
                    {
                        foreach (var player in GetAllOnline())
                            player.SetPlayerKillerStatus(PlayerKillerStatus.PKLite, true);

                        foreach (var player in GetAllOffline())
                        {
                            player.SetProperty(PropertyInt.PlayerKillerStatus, (int)PlayerKillerStatus.NPK);
                            player.SetProperty(PropertyFloat.MinimumTimeSincePk, 0);
                        }

                        var msg = $"This world has been changed to a Player Killer Lite world. All players will become Player Killer Lites in {ServerConfig.pk_respite_timer.Value} seconds.";
                        BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
                        LogBroadcastChat(Channel.AllBroadcast, null, msg);
                    }
                    else
                    {
                        foreach (var player in GetAllOnline())
                            player.SetPlayerKillerStatus(PlayerKillerStatus.NPK, true);

                        foreach (var player in GetAllOffline())
                        {
                            player.SetProperty(PropertyInt.PlayerKillerStatus, (int)PlayerKillerStatus.NPK);
                            player.SetProperty(PropertyFloat.MinimumTimeSincePk, 0);
                        }

                        var msg = "This world has been changed to a Non Player Killer world. All players are now Non-Player Killers.";
                        BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
                        LogBroadcastChat(Channel.AllBroadcast, null, msg);
                    }
                    break;
            }
        }

        public static bool IsAccountAtMaxCharacterSlots(string accountName)
        {
            var slotsAvailable = (int)ServerConfig.max_chars_per_account.Value;
            var onlinePlayersTotal = 0;
            var offlinePlayersTotal = 0;

            playersLock.EnterReadLock();
            try
            {
                onlinePlayersTotal = onlinePlayers.Count(a => a.Value.Account.AccountName.Equals(accountName, StringComparison.OrdinalIgnoreCase));
                offlinePlayersTotal = offlinePlayers.Count(a => a.Value.Account.AccountName.Equals(accountName, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                playersLock.ExitReadLock();
            }

            return (onlinePlayersTotal + offlinePlayersTotal) >= slotsAvailable;
        }
    }
}
