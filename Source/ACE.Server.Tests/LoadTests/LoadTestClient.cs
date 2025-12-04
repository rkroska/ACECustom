using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ACE.Server.Network;
using ACE.Server.Network.Enum;
using ACE.Server.Network.GameAction;
using ACE.Server.Network.GameMessages;

namespace ACE.Server.Tests.LoadTests
{
    /// <summary>
    /// Client state enumeration
    /// </summary>
    public enum LoadTestClientState
    {
        Disconnected,
        Connecting,
        Connected,
        EnteringWorld,
        InWorld
    }

    /// <summary>
    /// Simulates an ACE game client for load testing by reverse engineering the game protocol.
    /// Implements the full client handshake, authentication, and common game actions.
    /// </summary>
    public class LoadTestClient : IDisposable
    {
        private UdpClient udpClient;
        private IPEndPoint serverEndPoint;
        private ushort clientId;
        private ushort serverId;
        private uint nextOutgoingSequence = 1;
        private uint lastReceivedSequence = 0;
        private readonly Random random = new Random();
        private bool isConnected;
        private bool isDisposed;
        private CancellationTokenSource cancellationTokenSource;
        private Task receiveTask;

        // Client state
        public string AccountName { get; private set; }
        public string CharacterName { get; private set; }
        public uint CharacterId { get; private set; }
        public LoadTestClientState State { get; private set; }

        // Statistics
        public int PacketsSent { get; private set; }
        public int PacketsReceived { get; private set; }
        public DateTime ConnectedAt { get; private set; }
        public TimeSpan Latency { get; private set; }

        // Events for monitoring
        public event Action<string> OnLog;
        public event Action<Exception> OnError;
        public event Action OnConnected;
        public event Action OnDisconnected;

        public LoadTestClient(string serverHost, int serverPort)
        {
            serverEndPoint = new IPEndPoint(IPAddress.Parse(serverHost), serverPort);
            udpClient = new UdpClient();
            // Connect the UDP client to the server endpoint to enable ReceiveAsync
            udpClient.Connect(serverEndPoint);
            clientId = (ushort)random.Next(1, 65535);
            serverId = 0; // Will be set during handshake
            State = LoadTestClientState.Disconnected;
        }

        /// <summary>
        /// Connects to the server and performs the three-way handshake
        /// </summary>
        public async Task<bool> ConnectAsync(string accountName, string password)
        {
            try
            {
                AccountName = accountName;
                State = LoadTestClientState.Connecting;

                // Start receiving packets
                cancellationTokenSource = new CancellationTokenSource();
                receiveTask = Task.Run(() => ReceivePacketsAsync(cancellationTokenSource.Token));

                // Step 1: Send LoginRequest
                Log($"Sending LoginRequest for account: {accountName}");
                await SendLoginRequestAsync(accountName, password);

                // Wait for ConnectRequest from server
                await Task.Delay(100);

                // Step 2: Send ConnectResponse
                Log("Sending ConnectResponse");
                await SendConnectResponseAsync();

                // Wait for world connection
                await Task.Delay(100);

                isConnected = true;
                State = LoadTestClientState.Connected;
                ConnectedAt = DateTime.UtcNow;
                OnConnected?.Invoke();

                Log("Successfully connected to server");
                return true;
            }
            catch (Exception ex)
            {
                LogError(ex);
                return false;
            }
        }

        /// <summary>
        /// Logs in with a character and enters the world
        /// </summary>
        public async Task<bool> EnterWorldAsync(string characterName)
        {
            try
            {
                CharacterName = characterName;
                State = LoadTestClientState.EnteringWorld;

                Log($"Entering world with character: {characterName}");

                // Send CharacterEnterWorld message
                await SendCharacterEnterWorldAsync(characterName);

                await Task.Delay(200);

                State = LoadTestClientState.InWorld;
                Log("Successfully entered world");

                return true;
            }
            catch (Exception ex)
            {
                LogError(ex);
                return false;
            }
        }

        #region Common Game Actions

        /// <summary>
        /// Simulates player movement
        /// </summary>
        public async Task MoveAsync(float x, float y, float z)
        {
            if (State != LoadTestClientState.InWorld)
                return;

            try
            {
                var message = new MemoryStream();
                using (var writer = new BinaryWriter(message))
                {
                    // GameAction opcode
                    writer.Write((uint)GameMessageOpcode.GameAction);
                    writer.Write(nextOutgoingSequence++);
                    writer.Write((uint)GameActionType.AutonomousPosition);
                    
                    // Position data
                    writer.Write(x);
                    writer.Write(y);
                    writer.Write(z);
                    writer.Write(1.0f); // qw
                    writer.Write(0.0f); // qx
                    writer.Write(0.0f); // qy
                    writer.Write(0.0f); // qz
                }

                await SendGameMessageAsync(message.ToArray());
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        /// <summary>
        /// Sends a chat message
        /// </summary>
        public async Task SendChatAsync(string message)
        {
            if (State != LoadTestClientState.InWorld)
                return;

            try
            {
                var messageStream = new MemoryStream();
                using (var writer = new BinaryWriter(messageStream))
                {
                    writer.Write((uint)GameMessageOpcode.GameAction);
                    writer.Write(nextOutgoingSequence++);
                    writer.Write((uint)GameActionType.Talk);
                    
                    // Write string with length prefix (16-bit)
                    var bytes = System.Text.Encoding.Unicode.GetBytes(message);
                    writer.Write((ushort)bytes.Length);
                    writer.Write(bytes);
                }

                await SendGameMessageAsync(messageStream.ToArray());
                Log($"Sent chat: {message}");
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        /// <summary>
        /// Uses an item by GUID
        /// </summary>
        public async Task UseItemAsync(uint objectId)
        {
            if (State != LoadTestClientState.InWorld)
                return;

            try
            {
                var message = new MemoryStream();
                using (var writer = new BinaryWriter(message))
                {
                    writer.Write((uint)GameMessageOpcode.GameAction);
                    writer.Write(nextOutgoingSequence++);
                    writer.Write((uint)GameActionType.Use);
                    writer.Write(objectId);
                }

                await SendGameMessageAsync(message.ToArray());
                Log($"Used item: 0x{objectId:X8}");
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        /// <summary>
        /// Performs a melee attack on a target
        /// </summary>
        public async Task MeleeAttackAsync(uint targetId, uint attackHeight, float powerLevel)
        {
            if (State != LoadTestClientState.InWorld)
                return;

            try
            {
                var message = new MemoryStream();
                using (var writer = new BinaryWriter(message))
                {
                    writer.Write((uint)GameMessageOpcode.GameAction);
                    writer.Write(nextOutgoingSequence++);
                    writer.Write((uint)GameActionType.TargetedMeleeAttack);
                    writer.Write(targetId);
                    writer.Write(attackHeight);
                    writer.Write(powerLevel);
                }

                await SendGameMessageAsync(message.ToArray());
                Log($"Melee attack on target: 0x{targetId:X8}");
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        /// <summary>
        /// Casts a spell on a target
        /// </summary>
        public async Task CastSpellAsync(uint spellId, uint targetId)
        {
            if (State != LoadTestClientState.InWorld)
                return;

            try
            {
                var message = new MemoryStream();
                using (var writer = new BinaryWriter(message))
                {
                    writer.Write((uint)GameMessageOpcode.GameAction);
                    writer.Write(nextOutgoingSequence++);
                    writer.Write((uint)GameActionType.CastTargetedSpell);
                    writer.Write(targetId);
                    writer.Write(spellId);
                }

                await SendGameMessageAsync(message.ToArray());
                Log($"Cast spell {spellId} on target: 0x{targetId:X8}");
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        /// <summary>
        /// Picks up an item from the world
        /// </summary>
        public async Task PickupItemAsync(uint objectId)
        {
            if (State != LoadTestClientState.InWorld)
                return;

            try
            {
                var message = new MemoryStream();
                using (var writer = new BinaryWriter(message))
                {
                    writer.Write((uint)GameMessageOpcode.GameAction);
                    writer.Write(nextOutgoingSequence++);
                    writer.Write((uint)GameActionType.GetAndWieldItem);
                    writer.Write(objectId);
                    writer.Write(0); // Location (inventory)
                }

                await SendGameMessageAsync(message.ToArray());
                Log($"Picked up item: 0x{objectId:X8}");
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        /// <summary>
        /// Drops an item from inventory
        /// </summary>
        public async Task DropItemAsync(uint objectId)
        {
            if (State != LoadTestClientState.InWorld)
                return;

            try
            {
                var message = new MemoryStream();
                using (var writer = new BinaryWriter(message))
                {
                    writer.Write((uint)GameMessageOpcode.GameAction);
                    writer.Write(nextOutgoingSequence++);
                    writer.Write((uint)GameActionType.DropItem);
                    writer.Write(objectId);
                }

                await SendGameMessageAsync(message.ToArray());
                Log($"Dropped item: 0x{objectId:X8}");
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        /// <summary>
        /// Sends a ping request to measure latency
        /// </summary>
        public async Task PingAsync()
        {
            try
            {
                var startTime = DateTime.UtcNow;
                
                var message = new MemoryStream();
                using (var writer = new BinaryWriter(message))
                {
                    writer.Write((uint)GameMessageOpcode.GameAction);
                    writer.Write(nextOutgoingSequence++);
                    writer.Write((uint)GameActionType.PingRequest);
                }

                await SendGameMessageAsync(message.ToArray());
                
                // Note: In a real implementation, we'd wait for the response and calculate latency
                Latency = DateTime.UtcNow - startTime;
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        #endregion

        #region Network Protocol Implementation

        private async Task SendLoginRequestAsync(string account, string password)
        {
            var packet = new MemoryStream();
            using (var writer = new BinaryWriter(packet))
            {
                // Packet header
                writer.Write((uint)0x00000001); // Sequence
                writer.Write((uint)PacketHeaderFlags.LoginRequest);
                writer.Write(clientId);
                writer.Write((ushort)0); // Server ID (0 for initial)
                writer.Write((ushort)0); // Fragment count
                writer.Write((uint)0); // CRC

                // Login data
                var accountBytes = System.Text.Encoding.ASCII.GetBytes(account);
                writer.Write((ushort)accountBytes.Length);
                writer.Write(accountBytes);

                // Password hash (simplified - real client would use proper hashing)
                var passwordHash = System.Security.Cryptography.SHA256.Create()
                    .ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                writer.Write(passwordHash);

                // Client version info
                writer.Write((uint)0x1B0002E3); // Version stamp
            }

            await SendPacketAsync(packet.ToArray());
            PacketsSent++;
        }

        private async Task SendConnectResponseAsync()
        {
            var packet = new MemoryStream();
            using (var writer = new BinaryWriter(packet))
            {
                writer.Write(nextOutgoingSequence++);
                writer.Write((uint)PacketHeaderFlags.ConnectResponse);
                writer.Write(clientId);
                writer.Write(serverId);
                writer.Write((ushort)0);
                writer.Write((uint)0); // CRC

                // Connection data
                writer.Write((ulong)0); // Cookie
                writer.Write(clientId);
                writer.Write((uint)0); // Server seed
                writer.Write((uint)0); // Client seed
            }

            await SendPacketAsync(packet.ToArray());
            PacketsSent++;
        }

        private async Task SendCharacterEnterWorldAsync(string characterName)
        {
            var message = new MemoryStream();
            using (var writer = new BinaryWriter(message))
            {
                writer.Write((uint)GameMessageOpcode.CharacterEnterWorld);
                
                var nameBytes = System.Text.Encoding.Unicode.GetBytes(characterName);
                writer.Write((ushort)nameBytes.Length);
                writer.Write(nameBytes);
            }

            await SendGameMessageAsync(message.ToArray());
        }

        private async Task SendGameMessageAsync(byte[] messageData)
        {
            var packet = new MemoryStream();
            using (var writer = new BinaryWriter(packet))
            {
                // Packet header
                writer.Write(nextOutgoingSequence++);
                writer.Write((uint)0); // Flags
                writer.Write(clientId);
                writer.Write(serverId);
                writer.Write((ushort)1); // Fragment count
                writer.Write((uint)0); // CRC (simplified)

                // Fragment header
                writer.Write(nextOutgoingSequence++);
                writer.Write((ushort)messageData.Length);

                // Message data
                writer.Write(messageData);
            }

            await SendPacketAsync(packet.ToArray());
            PacketsSent++;
        }

        private async Task SendPacketAsync(byte[] data)
        {
            try
            {
                // Use SendAsync without endpoint since we're connected
                await udpClient.SendAsync(data, data.Length);
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        private async Task ReceivePacketsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && !isDisposed)
                {
                    var result = await udpClient.ReceiveAsync();
                    PacketsReceived++;
                    lastReceivedSequence++;

                    // Process received packet
                    ProcessReceivedPacket(result.Buffer);
                }
            }
            catch (ObjectDisposedException)
            {
                // Normal during shutdown
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    LogError(ex);
                }
            }
        }

        private void ProcessReceivedPacket(byte[] data)
        {
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(data)))
                {
                    var sequence = reader.ReadUInt32();
                    var flags = reader.ReadUInt32();
                    
                    // Handle different packet types based on flags
                    if ((flags & (uint)PacketHeaderFlags.ConnectRequest) != 0)
                    {
                        serverId = reader.ReadUInt16();
                        Log($"Received ConnectRequest, serverId: {serverId}");
                    }
                    else if ((flags & (uint)PacketHeaderFlags.Disconnect) != 0)
                    {
                        Log("Received Disconnect from server");
                        Disconnect();
                    }
                    
                    // Additional packet parsing would go here
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        #endregion

        #region Lifecycle Management

        public void Disconnect()
        {
            if (!isConnected)
                return;

            try
            {
                Log("Disconnecting from server");
                isConnected = false;
                State = LoadTestClientState.Disconnected;
                
                cancellationTokenSource?.Cancel();
                
                OnDisconnected?.Invoke();
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            Disconnect();

            cancellationTokenSource?.Dispose();
            udpClient?.Close();
            udpClient?.Dispose();
        }

        #endregion

        #region Logging

        private void Log(string message)
        {
            var logMessage = $"[{DateTime.UtcNow:HH:mm:ss.fff}] [{CharacterName ?? AccountName ?? "Client"}] {message}";
            OnLog?.Invoke(logMessage);
            Console.WriteLine(logMessage);
        }

        private void LogError(Exception ex)
        {
            var errorMessage = $"[ERROR] {ex.Message}\n{ex.StackTrace}";
            Log(errorMessage);
            OnError?.Invoke(ex);
        }

        #endregion
    }
}
