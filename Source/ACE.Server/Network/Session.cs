using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;

using log4net;

using ACE.Common;
using ACE.Common.Performance;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.DatLoader;
using ACE.Entity.Enum;
using ACE.Server.WorldObjects;
using ACE.Server.Managers;
using ACE.Server.Network.Enum;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.GameMessages;
using ACE.Server.Network.Managers;
using ACE.Server.Entity;

namespace ACE.Server.Network
{
    public class Session
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public IPEndPoint EndPoint { get; }

        public NetworkSession Network { get; set; }

        public uint GameEventSequence { get; set; }

        public SessionState State { get; set; }

        public uint AccountId { get; private set; }

        public string Account { get; private set; }

        public string LoggingIdentifier { get; private set; } = "Unverified";

        public AccessLevel AccessLevel { get; private set; }

        public List<LoginCharacter> Characters { get; } = new List<LoginCharacter>();

        public Player Player { get; private set; }


        public DateTime logOffRequestTime;

        public DateTime lastCharacterSelectPingReply;

        public DateTime lastMyQuestsCommandTime;

        public SessionTerminationDetails PendingTermination { get; set; } = null;

        public string BootSessionReason { get; private set; }

        public bool DatWarnCell;
        public bool DatWarnPortal;
        public bool DatWarnLanguage;

        /// <summary>
        /// This boolean is set to true if GameMessageDDDBeginDDD is sent to the client. Used to determine when response is needed for DDD_EndDDD
        /// </summary>
        public bool BeginDDDSent;
        /// <summary>
        /// The time at which the BeginDDD message was sent to the client. Used to determine when to start processing dddDataQueue initially.
        /// </summary>
        public DateTime BeginDDDSentTime;
        /// <summary>
        /// Queue for data files missing at time of connection (Portal/Cell/Language DAT), and data files requested by client (Cell DAT)
        /// </summary>
        private ConcurrentQueue<(uint DatFileId, DatDatabaseType DatDatabaseType)> dddDataQueue;
        /// <summary>
        /// The rate at which ProcessDDDQueue executes (and sends DDD patch data out to client)
        /// </summary>
        private static readonly RateLimiter dddDataQueueRateLimiter = new RateLimiter(1000, TimeSpan.FromMinutes(1));

        /// <summary>
        /// Rate limiter for /passwd command
        /// </summary>
        public DateTime LastPassTime { get; set; }


        /// <summary>
        /// Rate limiter for /myquests command
        /// </summary>
        public DateTime LastMyQuestsCommandTime
        {
            get => lastMyQuestsCommandTime;
            set => lastMyQuestsCommandTime = value;
        }

        public Session(ConnectionListener connectionListener, IPEndPoint endPoint, ushort clientId, ushort serverId)
        {
            EndPoint = endPoint;
            Network = new NetworkSession(this, connectionListener, clientId, serverId);
        }


        private bool CheckState(ClientPacket packet)
        {
            if (packet.Header.HasFlag(PacketHeaderFlags.LoginRequest) && State != SessionState.AuthLoginRequest)
                return false;

            if (packet.Header.HasFlag(PacketHeaderFlags.ConnectResponse) && State != SessionState.AuthConnectResponse)
                return false;

            if (packet.Header.HasFlag(PacketHeaderFlags.AckSequence | PacketHeaderFlags.TimeSync | PacketHeaderFlags.EchoRequest | PacketHeaderFlags.Flow) && State == SessionState.AuthLoginRequest)
                return false;

            return true;
        }

        public void ProcessPacket(ClientPacket packet)
        {
            if (!CheckState(packet))
                return;

            Network.ProcessPacket(packet);
        }

        public bool inExemptLandblock()
        {
            if (Player != null)
            {
                if (Player.CurrentLandblock != null && Landblock.connectionExemptLandblocks.Contains(Player.CurrentLandblock.Id.Landblock))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// This will send outgoing packets as well as the final logoff message.
        /// </summary>
        public void TickOutbound()
        {
            // Check if the player has been booted
            if (PendingTermination != null)
            {
                if (PendingTermination.TerminationStatus == SessionTerminationPhase.Initialized)
                {
                    State = SessionState.TerminationStarted;
                    Network.Update(); // boot messages may need sending
                    if (DateTime.UtcNow.Ticks > PendingTermination.TerminationEndTicks)
                        PendingTermination.TerminationStatus = SessionTerminationPhase.SessionWorkCompleted;
                }
                return;
            }

            if (State == SessionState.TerminationStarted)
                return;

            // Checks if the session has stopped responding.
            if (DateTime.UtcNow.Ticks >= Network.TimeoutTick)
            {
                // The Session has reached a timeout.  Send the client the error disconnect signal, and then drop the session
                Terminate(SessionTerminationReason.NetworkTimeout);
                return;
            }

            Network.Update();

            // Live server seemed to take about 6 seconds. 4 seconds is nice because it has smooth animation, and saves the user 2 seconds every logoff
            // This could be made 0 for instant logoffs.
            if (logOffRequestTime != DateTime.MinValue && logOffRequestTime.AddSeconds(4) <= DateTime.UtcNow)
                SendFinalLogOffMessages();

            // This section deviates from known retail pcaps/behavior, but appears to be the least harmful way to work around something that seemingly didn't occur to players using ThwargLauncher connecting to retail servers.
            // In order to prevent the launcher from thinking the session is dead, we will send a Ping Response every 100 seconds, this will in effect make the client appear active to the launcher and allow players to create characters in peace.
            if (State == SessionState.AuthConnected) // TODO: why is this needed? Why didn't retail have this problem? Is this fuzzy memory?
            {
                if (lastCharacterSelectPingReply == DateTime.MinValue)
                    lastCharacterSelectPingReply = DateTime.UtcNow.AddSeconds(100);
                else if (DateTime.UtcNow > lastCharacterSelectPingReply)
                {
                    Network.EnqueueSend(new GameEventPingResponse(this));
                    lastCharacterSelectPingReply = DateTime.UtcNow.AddSeconds(100);
                }
            }
            else if (lastCharacterSelectPingReply != DateTime.MinValue)
                lastCharacterSelectPingReply = DateTime.MinValue;

            ProcessDDDQueue();
        }


        public void SetAccount(uint accountId, string account, AccessLevel accountAccesslevel)
        {
            AccountId = accountId;
            Account = account;
            AccessLevel = accountAccesslevel;
        }

        public void UpdateCharacters(IEnumerable<LoginCharacter> characters)
        {
            Characters.Clear();

            Characters.AddRange(characters);

            CheckCharactersForDeletion();
        }

        public void CheckCharactersForDeletion()
        {
            for (int i = Characters.Count - 1; i > 0; i--)
            {
                if (Characters[i].DeleteTime > 0 && Time.GetUnixTime() > Characters[i].DeleteTime)
                {
                    //Characters[i].IsDeleted = true;
                    DatabaseManager.Shard.GetCharacter(Characters[i].Id, x =>
                    {
                        x.IsDeleted = true;
                        DatabaseManager.Shard.SaveCharacter(x, new ReaderWriterLockSlim(), null);

                        PlayerManager.ProcessDeletedPlayer(x.Id);

                        //Characters.RemoveAt(i);
                    });                    
                }
            }
        }

        public void InitSessionForWorldLogin()
        {
            GameEventSequence = 1;
        }

        public void SetAccessLevel(AccessLevel accountAccesslevel)
        {
            AccessLevel = accountAccesslevel;
        }

        public void SetPlayer(Player player)
        {
            Player = player;
        }


        /// <summary>
        /// Log off the player normally
        /// </summary>
        public void LogOffPlayer(bool forceImmediate = false)
        {
            if (Player == null) return;

            // Character database objects are not cached. Each session gets a new character entity and dbContext from ShardDatabase.
            // To ensure the latest version of the character is saved before any new logins pull these records again, we queue a save here if necessary, at the instant logoff is requested.
            if (Player.CharacterChangesDetected)
                Player.SaveCharacterToDatabase(true);

            if (logOffRequestTime == DateTime.MinValue)
            {
                var result = Player.LogOut(false, forceImmediate);

                if (result)
                    logOffRequestTime = DateTime.UtcNow;
            }
        }

        private void SendFinalLogOffMessages()
        {
            // If we still exist on a landblock, we can't exit yet.
            if (Player?.CurrentLandblock != null)
                return;

            logOffRequestTime = DateTime.MinValue;

            // It's possible for a character change to happen from a GameActionSetCharacterOptions message.
            // This message can be received/processed by the server AFTER LogOfPlayer has been called.
            // What that means is, we could end up with Character changes after the Character has been saved from the initial LogOff request.
            // To make sure we commit these additional changes (if any), we check again here
            if (Player?.CharacterChangesDetected ?? false)
                Player?.SaveCharacterToDatabase(true);

            Player = null;

            if (!ServerManager.ShutdownInProgress)
            {
                Network.EnqueueSend(new GameMessageCharacterLogOff());

                CheckCharactersForDeletion();

                Network.EnqueueSend(new GameMessageCharacterList(Characters, this));

                GameMessageServerName serverNameMessage = new GameMessageServerName(ConfigManager.Config.Server.WorldName, PlayerManager.GetOnlineCount(), (int)ConfigManager.Config.Server.Network.MaximumAllowedSessions);
                Network.EnqueueSend(serverNameMessage);
            }

            State = SessionState.AuthConnected;
        }

        public void Terminate(SessionTerminationReason reason, GameMessage message = null, ServerPacket packet = null, string extraReason = "")
        {
            // TODO: graceful SessionTerminationReason.AccountBooted handling

            if (packet != null)
            {
                Network.EnqueueSend(packet);
            }
            if (message != null)
            {
                Network.EnqueueSend(message);
            }
            PendingTermination = new SessionTerminationDetails()
            {
                ExtraReason = extraReason,
                Reason = reason
            };
        }

        public void DropSession()
        {
            if (PendingTermination == null || PendingTermination.TerminationStatus != SessionTerminationPhase.SessionWorkCompleted) return;

            if (PendingTermination.Reason != SessionTerminationReason.PongSentClosingConnection)
            {
                var reason = PendingTermination.Reason;
                string reas = (reason != SessionTerminationReason.None) ? $", Reason: {reason.GetDescription()}" : "";
                if (!string.IsNullOrWhiteSpace(PendingTermination.ExtraReason))
                {
                    reas = reas + ", " + PendingTermination.ExtraReason;
                }
                if (WorldManager.WorldStatus == WorldManager.WorldStatusState.Open)
                    log.Info($"Session {Network?.ClientId}\\{EndPoint} dropped. Account: {Account}, Player: {Player?.Name}{reas}");
                else
                    log.Debug($"Session {Network?.ClientId}\\{EndPoint} dropped. Account: {Account}, Player: {Player?.Name}{reas}");
            }

            if (Player != null)
            {
                LogOffPlayer();

                // We don't want to set the player to null here. Because the player is still on the network, it may still enqueue work onto it's session.
                // Some network message objects will reference session.Player in their construction. If we set Player to null here, we'll throw exceptions in those cases.

                // At this point, if the player was on a landblock, they'll still exist on that landblock until the logout animation completes (~6s).
            }

            NetworkManager.RemoveSession(this);

            // This is a temp fix to mark the Session.Network portion of the Session as released
            // What this means is that we will release any network related resources, as well as avoid taking on additional resources
            // In the future, we should set Network to null and funnel Network communication through Session, instead of accessing Session.Network directly.
            Network.ReleaseResources();
        }


        public void SendCharacterError(CharacterError error)
        {
            Network.EnqueueSend(new GameMessageCharacterError(error));
        }

        /// <summary>
        /// Sends a broadcast message to the player
        /// </summary>
        public void WorldBroadcast(string broadcastMessage)
        {
            var worldBroadcastMessage = new GameMessageSystemChat(broadcastMessage, ChatMessageType.WorldBroadcast);
            Network.EnqueueSend(worldBroadcastMessage);
        }

        /// <summary>
        /// This will enqueue a file to be sent by ProcessDDDQueue.
        /// </summary>
        public bool AddToDDDQueue(uint datFileId, DatDatabaseType datDatabaseType)
        {
            dddDataQueue ??= new ConcurrentQueue<(uint, DatDatabaseType)>();
            dddDataQueue.Enqueue((datFileId, datDatabaseType));
            return true;
        }

        /// <summary>
        /// This will Network.EnqueueSend queued data files from DDDManager/DDDHandler.
        /// </summary>
        private void ProcessDDDQueue()
        {
            if (dddDataQueue == null || dddDataQueueRateLimiter.GetSecondsToWaitBeforeNextEvent() > 0)
                return;

            if (BeginDDDSentTime != DateTime.MinValue && DateTime.UtcNow < BeginDDDSentTime.AddSeconds(5))
                return;

            BeginDDDSentTime = DateTime.MinValue;

            if (dddDataQueue.TryDequeue(out var dataFile))
            {
                Network.EnqueueSend(new GameMessageDDDDataMessage(dataFile.DatFileId, dataFile.DatDatabaseType));
                dddDataQueueRateLimiter.RegisterEvent();
            }
        }
    }
}
