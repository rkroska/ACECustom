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
                offlinePlayers[offlinePlayer.Guid.Full] = offlinePlayer;
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
                    return false;
            }
            finally
            {
                playersLock.ExitWriteLock();
            }

            AllegianceManager.LoadPlayer(player);

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
                    return false;
            }
            finally
            {
                playersLock.ExitWriteLock();
            }

            player.SendFriendStatusUpdates(false);
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
        public static void BroadcastToAll(GameMessage msg)
        {
            foreach (var player in GetAllOnline())
                player.Session.Network.EnqueueSend(msg);
        }

        public static void BroadcastToAuditChannel(Player issuer, string message)
        {
            if (issuer != null)
            { 
                BroadcastToChannel(Channel.Audit, issuer, message, true, true);
                DiscordChatManager.SendDiscordMessage(issuer.Name, message, ConfigManager.Config.Chat.AdminAuditId);
            }
            else
            {
                BroadcastToChannelFromConsole(Channel.Audit, message);
                DiscordChatManager.SendDiscordMessage("Console", message, ConfigManager.Config.Chat.AdminAuditId);
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
                    if (!PropertyManager.GetBool("chat_log_abuse"))
                        return;
                    break;
                case Channel.Admin:
                    if (!PropertyManager.GetBool("chat_log_admin"))
                        return;
                    break;
                case Channel.AllBroadcast: // using this to sub in for a WorldBroadcast channel which isn't technically a channel
                    if (!PropertyManager.GetBool("chat_log_global"))
                        return;
                    break;
                case Channel.Audit:
                    if (!PropertyManager.GetBool("chat_log_audit"))
                        return;
                    break;
                case Channel.Advocate1:
                case Channel.Advocate2:
                case Channel.Advocate3:
                    if (!PropertyManager.GetBool("chat_log_advocate"))
                        return;
                    break;
                case Channel.Debug:
                    if (!PropertyManager.GetBool("chat_log_debug"))
                        return;
                    break;
                case Channel.Fellow:
                case Channel.FellowBroadcast:
                    if (!PropertyManager.GetBool("chat_log_fellow"))
                        return;
                    break;
                case Channel.Help:
                    if (!PropertyManager.GetBool("chat_log_help"))
                        return;
                    break;
                case Channel.Olthoi:
                    if (!PropertyManager.GetBool("chat_log_olthoi"))
                        return;
                    break;
                case Channel.QA1:
                case Channel.QA2:
                    if (!PropertyManager.GetBool("chat_log_qa"))
                        return;
                    break;
                case Channel.Sentinel:
                    if (!PropertyManager.GetBool("chat_log_sentinel"))
                        return;
                    break;

                case Channel.SocietyCelHanBroadcast:
                case Channel.SocietyEldWebBroadcast:
                case Channel.SocietyRadBloBroadcast:
                    if (!PropertyManager.GetBool("chat_log_society"))
                        return;
                    break;

                case Channel.AllegianceBroadcast:
                case Channel.CoVassals:
                case Channel.Monarch:
                case Channel.Patron:
                case Channel.Vassals:
                    if (!PropertyManager.GetBool("chat_log_allegiance"))
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
                    if (!PropertyManager.GetBool("chat_log_townchans"))
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

        public static bool GagPlayer(Player issuer, string playerName)
        {
            var player = FindByName(playerName);

            if (player == null)
                return false;

            player.SetProperty(ACE.Entity.Enum.Properties.PropertyBool.IsGagged, true);
            player.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.GagTimestamp, Common.Time.GetUnixTime());
            player.SetProperty(ACE.Entity.Enum.Properties.PropertyFloat.GagDuration, 300);

            player.SaveBiotaToDatabase();

            BroadcastToAuditChannel(issuer, $"{issuer.Name} has gagged {player.Name} for five minutes.");

            return true;
        }

        public static bool UnGagPlayer(Player issuer, string playerName)
        {
            var player = FindByName(playerName);

            if (player == null)
                return false;

            player.RemoveProperty(ACE.Entity.Enum.Properties.PropertyBool.IsGagged);
            player.RemoveProperty(ACE.Entity.Enum.Properties.PropertyFloat.GagTimestamp);
            player.RemoveProperty(ACE.Entity.Enum.Properties.PropertyFloat.GagDuration);

            player.SaveBiotaToDatabase();

            BroadcastToAuditChannel(issuer, $"{issuer.Name} has ungagged {player.Name}.");

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

                        var msg = $"This world has been changed to a Player Killer world. All players will become Player Killers in {PropertyManager.GetDouble("pk_respite_timer")} seconds.";
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
                    if (PropertyManager.GetBool("pk_server"))
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

                        var msg = $"This world has been changed to a Player Killer Lite world. All players will become Player Killer Lites in {PropertyManager.GetDouble("pk_respite_timer")} seconds.";
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
            var slotsAvailable = (int)PropertyManager.GetLong("max_chars_per_account");
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
