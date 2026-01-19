using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;

using log4net;

using ACE.Common;
using ACE.Common.Performance;
using ACE.Database;
using ACE.Database.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.WorldObjects;
using ACE.Server.Network;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Managers;
using ACE.Server.Physics;
using ACE.Server.Physics.Common;

using Character = ACE.Database.Models.Shard.Character;
using Position = ACE.Entity.Position;
using System.Linq;
using ACE.Database.Models.Shard;
using Biota = ACE.Entity.Models.Biota;

namespace ACE.Server.Managers
{
    public static class WorldManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        // Discord throttling for login block alerts
        private static DateTime lastLoginBlockAlert = DateTime.MinValue;
        private static int loginBlockAlertsThisMinute = 0;

        private static readonly PhysicsEngine Physics;

        public static bool WorldActive { get; private set; }
        private static volatile bool pendingWorldStop;

        public enum WorldStatusState
        {
            Closed,
            Open
        }

        public static WorldStatusState WorldStatus { get; private set; } = WorldStatusState.Closed;

        public static readonly ActionQueue ActionQueue = new ActionQueue();
        public static readonly DelayManager DelayManager = new DelayManager();

        static WorldManager()
        {
            Physics = new PhysicsEngine(new SmartBox());
            Physics.Server = true;
        }

        public static void Initialize()
        {
            // CRITICAL: Register callback factory with SaveScheduler as the FIRST thing in initialization
            // This must be set before any saves can be requested, as SaveScheduler relies on it for
            // thread-safe callback execution on the world thread (e.g., login drain callbacks)
            if (SaveScheduler.EnqueueToWorldThread != null)
            {
                log.Warn("[WORLDMANAGER] SaveScheduler.EnqueueToWorldThread was already set - this should not happen");
            }
            
            SaveScheduler.EnqueueToWorldThread = (action) =>
            {
                ActionQueue.EnqueueAction(new ActionEventDelegate(
                    ActionType.ControlFlowDelay,
                    action));
            };
            
            log.Debug("[WORLDMANAGER] SaveScheduler.EnqueueToWorldThread initialized");

            // Wire up SerializedShardDatabase hooks to avoid circular dependencies
            SerializedShardDatabase.EnqueueToWorldThread = SaveScheduler.EnqueueToWorldThread;
            SerializedShardDatabase.PerformOfflinePlayerSavesHook = PlayerManager.PerformOfflinePlayerSaves;
            
            log.Debug("[WORLDMANAGER] SerializedShardDatabase hooks initialized");

            var thread = new Thread(() =>
            {
                UpdateWorld();
            });
            thread.Name = "World Manager";
            thread.Priority = ThreadPriority.AboveNormal;
            thread.Start();
            log.DebugFormat("ServerTime initialized to {0}", Timers.WorldStartLoreTime);
            log.DebugFormat($"Current maximum allowed sessions: {ConfigManager.Config.Server.Network.MaximumAllowedSessions}");

            log.Info($"World started and is currently {WorldStatus.ToString()}{(ServerConfig.world_closed.Value ? "" : " and will open automatically when server startup is complete.")}");
            if (WorldStatus == WorldStatusState.Closed)
                log.Info($"To open world to players, use command: world open");
        }

        internal static void Open(Player player)
        {
            WorldStatus = WorldStatusState.Open;
            PlayerManager.BroadcastToAuditChannel(player, "World is now open");
        }

        internal static void Close(Player player, bool bootPlayers = false)
        {
            WorldStatus = WorldStatusState.Closed;
            var msg = "World is now closed";
            if (bootPlayers)
                msg += ", and booting all online players.";

            PlayerManager.BroadcastToAuditChannel(player, msg);

            if (bootPlayers)
                PlayerManager.BootAllPlayers();
        }

        public static void PlayerEnterWorld(Session session, LoginCharacter character)
        {
            var offlinePlayer = PlayerManager.GetOfflinePlayer(character.Id);

            if (offlinePlayer == null)
            {
                log.Error($"PlayerEnterWorld requested for character.Id 0x{character.Id:X8} not found in PlayerManager OfflinePlayers.");
                return;
            }

            // Clear abandoned SaveInProgress from previous server boots before checking
            if (offlinePlayer.SaveInProgress &&
                offlinePlayer.SaveServerBootId != ServerRuntime.BootId)
            {
                log.Warn($"[LOGIN] Clearing abandoned SaveInProgress for {character.Name}");
                offlinePlayer.SaveInProgress = false;
                offlinePlayer.SaveServerBootId = null;
            }

            // Check if there are any pending or active saves for this character using authoritative save system
            if (SaveScheduler.Instance.HasPendingOrActiveSave(character.Id))
            {
                // Prevent multiple callbacks for the same session/character login attempt
                if (session.WaitingForLoginDrain)
                {
                    log.Debug($"[LOGIN] {character.Name} already waiting for saves to drain, ignoring duplicate PlayerEnterWorld call");
                    return;
                }

                session.WaitingForLoginDrain = true;
                var waitStartTime = DateTime.UtcNow;

                // Register callback to continue login when saves complete
                // This is silent - no client notification, no polling, no black screen delay
                SaveScheduler.Instance.OnSavesDrained(character.Id, () =>
                {
                    session.WaitingForLoginDrain = false;

                    // Verify session is still valid before continuing
                    if (session != null && session.Player == null && session.State != Network.Enum.SessionState.TerminationStarted)
                    {
                        var waitDuration = (DateTime.UtcNow - waitStartTime).TotalSeconds;
                        
                        // Alert if wait exceeded threshold (diagnostic only, no client notification)
                        if (waitDuration > 5.0)
                        {
                            SendLoginDrainWaitAlert(character.Name, waitDuration);
                        }

                        // Recursively call PlayerEnterWorld to proceed with login
                        PlayerEnterWorld(session, character);
                    }
                });
                return;
            }

            // No saves in flight, proceed immediately with login
            DatabaseManager.Shard.GetCharacter(character.Id, fullCharacter =>
            {
                var start = DateTime.UtcNow;
                DatabaseManager.Shard.GetPossessedBiotasInParallel(character.Id, biotas =>
                {
                    log.Debug($"GetPossessedBiotasInParallel for {character.Name} took {(DateTime.UtcNow - start).TotalMilliseconds:N0} ms, Queue Size: {DatabaseManager.Shard.QueueCount}");
                    ActionQueue.EnqueueAction(new ActionEventDelegate(ActionType.WorldManager_DoPlayerEnterWorld, () => DoPlayerEnterWorld(session, fullCharacter, offlinePlayer.Biota, biotas), ActionPriority.High));
                });
            });            
        }

        private static void DoPlayerEnterWorld(Session session, Character character, Biota playerBiota, PossessedBiotas possessedBiotas)
        {
            Player player;

            Player.HandleNoLogLandblock(playerBiota, out var playerLoggedInOnNoLogLandblock);

            var stripAdminProperties = false;
            var addAdminProperties = false;
            var addSentinelProperties = false;
            if (ConfigManager.Config.Server.Accounts.OverrideCharacterPermissions)
            {
                if (session.AccessLevel <= AccessLevel.Advocate) // check for elevated characters
                {
                    if (playerBiota.WeenieType == WeenieType.Admin || playerBiota.WeenieType == WeenieType.Sentinel) // Downgrade weenie
                    {
                        character.IsPlussed = false;
                        playerBiota.WeenieType = WeenieType.Creature;
                        stripAdminProperties = true;
                    }
                }
                else if (session.AccessLevel >= AccessLevel.Sentinel && session.AccessLevel <= AccessLevel.Envoy)
                {
                    if (playerBiota.WeenieType == WeenieType.Creature || playerBiota.WeenieType == WeenieType.Admin) // Up/downgrade weenie
                    {
                        character.IsPlussed = true;
                        playerBiota.WeenieType = WeenieType.Sentinel;
                        addSentinelProperties = true;
                    }
                }
                else // Developers and Admins
                {
                    if (playerBiota.WeenieType == WeenieType.Creature || playerBiota.WeenieType == WeenieType.Sentinel) // Up/downgrade weenie
                    {
                        character.IsPlussed = true;
                        playerBiota.WeenieType = WeenieType.Admin;
                        addAdminProperties = true;
                    }
                }
            }

            if (playerBiota.WeenieType == WeenieType.Admin)
                player = new Admin(playerBiota, possessedBiotas.Inventory, possessedBiotas.WieldedItems, character, session);
            else if (playerBiota.WeenieType == WeenieType.Sentinel)
                player = new Sentinel(playerBiota, possessedBiotas.Inventory, possessedBiotas.WieldedItems, character, session);
            else
                player = new Player(playerBiota, possessedBiotas.Inventory, possessedBiotas.WieldedItems, character, session);

            session.SetPlayer(player);

            // Clear abandoned SaveInProgress from previous server boots before world entry
            if (player.SaveInProgress &&
                player.SaveServerBootId != ServerRuntime.BootId)
            {
                log.Warn($"[LOGIN] Clearing abandoned SaveInProgress for {player.Name}");
                player.SaveInProgress = false;
                player.SaveStartTime = DateTime.MinValue;
                player.SaveServerBootId = null;
            }

            if (stripAdminProperties) // continue stripping properties
            {
                player.CloakStatus = CloakStatus.Undef;
                player.Attackable = true;
                player.SetProperty(PropertyBool.DamagedByCollisions, true);
                player.AdvocateLevel = null;
                player.ChannelsActive = null;
                player.ChannelsAllowed = null;
                player.Invincible = false;
                player.Cloaked = null;
                player.IgnoreHouseBarriers = false;
                player.IgnorePortalRestrictions = false;
                player.SafeSpellComponents = false;
                player.ReportCollisions = true;


                player.ChangesDetected = true;
                player.CharacterChangesDetected = true;
            }

            if (addSentinelProperties || addAdminProperties) // continue restoring properties to default
            {
                WorldObject weenie;

                if (addAdminProperties)
                    weenie = Factories.WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie("admin"), new ACE.Entity.ObjectGuid(ACE.Entity.ObjectGuid.Invalid.Full));
                else
                    weenie = Factories.WorldObjectFactory.CreateWorldObject(DatabaseManager.World.GetCachedWeenie("sentinel"), new ACE.Entity.ObjectGuid(ACE.Entity.ObjectGuid.Invalid.Full));

                if (weenie != null)
                {
                    player.CloakStatus = CloakStatus.Off;
                    player.Attackable = weenie.Attackable;
                    player.SetProperty(PropertyBool.DamagedByCollisions, false);
                    player.AdvocateLevel = weenie.GetProperty(PropertyInt.AdvocateLevel);
                    player.ChannelsActive = (Channel?)weenie.GetProperty(PropertyInt.ChannelsActive);
                    player.ChannelsAllowed = (Channel?)weenie.GetProperty(PropertyInt.ChannelsAllowed);
                    player.Invincible = false;
                    player.Cloaked = false;


                    player.ChangesDetected = true;
                    player.CharacterChangesDetected = true;
                }
            }

            // If the client is missing a location, we start them off in the starter town they chose
            if (session.Player.Location == null)
            {
                if (session.Player.Instantiation != null)
                    session.Player.Location = new Position(session.Player.Instantiation);
                else
                    session.Player.Location = new Position(0xA9B40019, 84, 7.1f, 94, 0, 0, -0.0784591f, 0.996917f);  // ultimate fallback
            }

            var olthoiPlayerReturnedToLifestone = session.Player.IsOlthoiPlayer && character.TotalLogins >= 1 && session.Player.LoginAtLifestone;
            if (olthoiPlayerReturnedToLifestone)
                session.Player.Location = new Position(session.Player.Sanctuary);

            //explicitly set the varation if the player has one saved in their playerBiota
            var savedLoc = playerBiota.GetPosition(PositionType.Location, new ReaderWriterLockSlim());
            if (savedLoc != null)
            {
                session.Player.Location.Variation = savedLoc.Variation;
            }
            else
            {
                log.Error($"Saved Player Biota location position does not exist for {session.Player.Name}, variation could not be found and set");
            }

            session.Player.PlayerEnterWorld();

            var success = LandblockManager.AddObject(session.Player, true);
            if (!success)
            {
                // send to lifestone, or fallback location
                var fixLoc = session.Player.Sanctuary ?? new Position(0xA9B40019, 84, 7.1f, 94, 0, 0, -0.0784591f, 0.996917f);

                log.Error($"WorldManager.DoPlayerEnterWorld: failed to spawn {session.Player.Name}, relocating to {fixLoc.ToLOCString()}");

                session.Player.Location = new Position(fixLoc);
                LandblockManager.AddObject(session.Player, true);

                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(5.0f);
                actionChain.AddAction(session.Player, ActionType.Landblock_TeleportPlayerAfterFailureToAdd, () =>
                {
                    if (session != null && session.Player != null)
                        session.Player.Teleport(fixLoc);
                });
                actionChain.EnqueueChain();
            }

            // These warnings are set by DDD_InterrogationResponse
            if ((session.DatWarnCell || session.DatWarnLanguage || session.DatWarnPortal) && ServerConfig.show_dat_warning.Value)
            {
                var msg = ServerConfig.dat_older_warning_msg.Value;
                var chatMsg = new GameMessageSystemChat(msg, ChatMessageType.System);
                session.Network.EnqueueSend(chatMsg);
            }

            var popup_header = ServerConfig.popup_header.Value;
            var popup_motd = ServerConfig.popup_motd.Value;
            var popup_welcome = player.IsOlthoiPlayer ? ServerConfig.popup_welcome_olthoi.Value : ServerConfig.popup_welcome.Value;

            if (character.TotalLogins <= 1)
            {
                if (player.IsOlthoiPlayer)
                    session.Network.EnqueueSend(new GameEventPopupString(session, AppendLines(popup_welcome, popup_motd)));
                else
                    session.Network.EnqueueSend(new GameEventPopupString(session, AppendLines(popup_header, popup_motd, popup_welcome)));
            }
            else if (!string.IsNullOrEmpty(popup_motd))
            {
                session.Network.EnqueueSend(new GameEventPopupString(session, AppendLines(popup_header, popup_motd)));
            }

            var info = "Welcome to Asheron's Call\n  powered by ACEmulator\n\nFor more information on commands supported by this server, type @acehelp\n";
            session.Network.EnqueueSend(new GameMessageSystemChat(info, ChatMessageType.Broadcast));

            var server_motd = ServerConfig.server_motd.Value;
            if (!string.IsNullOrEmpty(server_motd))
                session.Network.EnqueueSend(new GameMessageSystemChat($"{server_motd}\n", ChatMessageType.Broadcast));

            if (olthoiPlayerReturnedToLifestone)
                session.Network.EnqueueSend(new GameMessageSystemChat("You have returned to the Olthoi Queen to serve the hive.", ChatMessageType.Broadcast));
            else if (playerLoggedInOnNoLogLandblock) // see http://acpedia.org/wiki/Mount_Elyrii_Hive
                session.Network.EnqueueSend(new GameMessageSystemChat("The currents of portal space cannot return you from whence you came. Your previous location forbids login.", ChatMessageType.Broadcast));            
        }

        private static string AppendLines(params string[] lines)
        {
            var result = "";
            foreach (var line in lines)
                if (!string.IsNullOrEmpty(line))
                    result += $"{line}\n";

            return Regex.Replace(result, "\n$", "");
        }

        /// <summary>
        /// ACE allows for multi-threading with thread boundaries based on the "LandblockGroup" concept
        /// The risk of moving the player immediately is that the player may move onto another LandblockGroup, and thus, cross thread boundaries
        /// This will enqueue the work onto WorldManager making the teleport thread safe.
        /// Note that this work will be done on the next tick, not immediately, so be careful about your order of operations.
        /// If you must ensure order, pass your follow up work in with the argument actionToFollowUpWith. That work will be enqueued onto the Player.
        /// </summary>
        public static void ThreadSafeTeleport(Creature creature, Position newPosition, IAction actionToFollowUpWith = null, bool fromPortal = false)
        {
            EnqueueAction(new ActionEventDelegate(ActionType.WorldManager_ThreadSafeTeleport, () =>
            {
                creature.Teleport(newPosition, fromPortal);

                    if (actionToFollowUpWith != null)
                        EnqueueAction(actionToFollowUpWith);
                }));
        }

        public static void EnqueueAction(IAction action)
        {
            ActionQueue.EnqueueAction(action);
        }

        private static readonly RateLimiter updateGameWorldRateLimiter = new RateLimiter(60, TimeSpan.FromSeconds(1));

        /// <summary>
        /// Manages updating all entities on the world.
        ///  - Server-side command-line commands are handled in their own thread.
        ///  - Database I/O is handled in its own thread.
        ///  - Network commands come from their own listener threads, and are queued for each sessions which are then processed here.
        ///  - This thread does the rest of the work!
        /// </summary>
        private static void UpdateWorld()
        {
            log.DebugFormat("Starting UpdateWorld thread");

            // Preload landblocks before starting the world update loop
            // This ensures all world initialization happens on the UpdateWorld thread
            LandblockManager.PreloadConfigLandblocks();

            WorldActive = true;
            var worldTickTimer = new Stopwatch();

            while (!pendingWorldStop)
            {
                /*
                When it comes to thread safety for Landblocks and WorldObjects, ACE makes the following assumptions:

                 * Inbound ClientMessages and GameActions are handled on the main UpdateWorld thread.
                   - These actions may load Landblocks and modify other WorldObjects safely.

                 * PlayerEnterWorld queue is run on the main UpdateWorld thread.
                   - These actions may load Landblocks and modify other WorldObjects safely.

                 * Landblock Groups (calculated by LandblockManager) can be processed in parallel.

                 * Adjacent Landblocks will always be run on the same thread.

                 * Non-adjacent landblocks might be run on different threads.
                   - If two non-adjacent landblocks both touch the same landblock, and that landblock is active, they will be run on the same thread.

                 * Database results are returned from a task spawned in SerializedShardDatabase (via callback).
                   - Minimal processing should be done from the callback. Return as quickly as possible to let the database thread do database work.
                   - The processing of these results should be queued to an ActionQueue

                 * The only cases where it's acceptable for to create a new Task, Thread or Parallel loop are the following:
                   - Every scenario must be one where you don't care about breaking ACE
                   - DeveloperCommand Handlers
                */

                worldTickTimer.Restart();

                ServerPerformanceMonitor.RestartEvent(ServerPerformanceMonitor.MonitorType.PlayerManager_Tick);
                PlayerManager.Tick();
                ServerPerformanceMonitor.RegisterEventEnd(ServerPerformanceMonitor.MonitorType.PlayerManager_Tick);

                ServerPerformanceMonitor.RestartEvent(ServerPerformanceMonitor.MonitorType.NetworkManager_InboundClientMessageQueueRun);
                NetworkManager.InboundMessageQueue.RunActions();
                ServerPerformanceMonitor.RegisterEventEnd(ServerPerformanceMonitor.MonitorType.NetworkManager_InboundClientMessageQueueRun);

                // This will consist of PlayerEnterWorld actions, as well as other game world actions that require thread safety
                ServerPerformanceMonitor.RestartEvent(ServerPerformanceMonitor.MonitorType.actionQueue_RunActions);
                ActionQueue.RunActions();
                ServerPerformanceMonitor.RegisterEventEnd(ServerPerformanceMonitor.MonitorType.actionQueue_RunActions);

                ServerPerformanceMonitor.RestartEvent(ServerPerformanceMonitor.MonitorType.DelayManager_RunActions);
                DelayManager.RunActions();
                ServerPerformanceMonitor.RegisterEventEnd(ServerPerformanceMonitor.MonitorType.DelayManager_RunActions);

                ServerPerformanceMonitor.RestartEvent(ServerPerformanceMonitor.MonitorType.UpdateGameWorld);
                var gameWorldUpdated = UpdateGameWorld();
                ServerPerformanceMonitor.RegisterEventEnd(ServerPerformanceMonitor.MonitorType.UpdateGameWorld);

                int sessionCount = 0;
                try
                {
                    ServerPerformanceMonitor.RestartEvent(ServerPerformanceMonitor.MonitorType.NetworkManager_DoSessionWork);
                    sessionCount = NetworkManager.DoSessionWork();
                }
                catch (Exception ex)
                {
                    log.Error("Exception in NetworkManager.DoSessionWork", ex);
                }
                finally
                {
                    ServerPerformanceMonitor.RegisterEventEnd(ServerPerformanceMonitor.MonitorType.NetworkManager_DoSessionWork);
                }
                
                

                ServerPerformanceMonitor.Tick();

                // We only relax the CPU if our game world is able to update at the target rate.
                // We do not sleep if our game world just updated. This is to prevent the scenario where our game world can't keep up. We don't want to add further delays.
                // If our game world is able to keep up, it will not be updated on most ticks. It's on those ticks (between updates) that we will relax the CPU.
                if (!gameWorldUpdated)
                    Thread.Sleep(sessionCount == 0 ? 10 : 1); // Relax the CPU more if no sessions are connected

                Timers.PortalYearTicks += worldTickTimer.Elapsed.TotalSeconds;
            }

            // World has finished operations and concedes the thread to garbage collection
            WorldActive = false;
        }

        /// <summary>
        /// Projected to run at a reasonable rate for gameplay (30-60fps)
        /// </summary>
        public static bool UpdateGameWorld()
        {
            if (updateGameWorldRateLimiter.GetSecondsToWaitBeforeNextEvent() > 0)
                return false;

            updateGameWorldRateLimiter.RegisterEvent();

            ServerPerformanceMonitor.RestartCumulativeEvents();
            ServerPerformanceMonitor.RestartEvent(ServerPerformanceMonitor.MonitorType.UpdateGameWorld_Entire);

            LandblockManager.Tick(Timers.PortalYearTicks);

            HouseManager.Tick();

            ServerPerformanceMonitor.RegisterEventEnd(ServerPerformanceMonitor.MonitorType.UpdateGameWorld_Entire);
            ServerPerformanceMonitor.RegisterCumulativeEvents();

            return true;
        }

        /// <summary>
        /// Function to begin ending the operations inside of an active world.
        /// </summary>
        public static void StopWorld() { pendingWorldStop = true; }
        
        /// <summary>
        /// Sends a Discord alert if login drain wait exceeds threshold (5 seconds).
        /// This is diagnostic only - no client notification, no polling delays.
        /// </summary>
        private static void SendLoginDrainWaitAlert(string characterName, double waitDurationSeconds)
        {
            var now = DateTime.UtcNow;
            
            if ((now - lastLoginBlockAlert).TotalMinutes >= 1)
                loginBlockAlertsThisMinute = 0;
            
            var maxAlerts = ServerConfig.login_block_discord_max_alerts_per_minute.Value;
            if (maxAlerts <= 0 || loginBlockAlertsThisMinute >= maxAlerts)
                return;
            
            if (!ConfigManager.Config.Chat.EnableDiscordConnection || 
                ConfigManager.Config.Chat.PerformanceAlertsChannelId <= 0)
                return;
            
            try
            {
                var msg = $"⚠️ **LOGIN DRAIN WAIT**: `{characterName}` waited {waitDurationSeconds:F1}s for saves to drain before login. This is normal but indicates slow save operations.";
                
                DiscordChatManager.SendDiscordMessage("LOGIN DRAIN DIAGNOSTIC", msg, 
                    ConfigManager.Config.Chat.PerformanceAlertsChannelId);
                
                loginBlockAlertsThisMinute++;
                lastLoginBlockAlert = now;
            }
            catch (Exception ex)
            {
                log.Error($"Failed to send login drain wait alert to Discord: {ex.Message}");
            }
        }
    }
}
