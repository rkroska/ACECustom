using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.WorldObjects
{

    public class UCMChecker()
    {
        private const uint UCMStatueWcid = 79790003;
        public bool IsChecking { get; private set; } = false;
        private DateTime Timeout { get; set; }
        private List<WorldObject> Statues { get; } = [];
        private WorldObject CorrectStatue { get; set; }
        private Position CheckLocation { get; set; }
        private Random random { get; } = new();

        public void Start(Player player)
        {
            if (IsChecking) return;

            IsChecking = true;
            CheckLocation = new Position(player.Location);
            Timeout = DateTime.UtcNow.AddSeconds(ServerConfig.ucmCheckTimeoutSeconds.Value);

            // N, E, S, W relative or absolute doesn't matter too much, we'll use absolute cardinal offsets
            // Assuming Location.Position.X/Y are coordinates
            const float UCMStatueSpawnDistance = 1.0f;
            var offsets = new[]
            {
                new { X = 0.0f, Y = UCMStatueSpawnDistance }, // North
                new { X = UCMStatueSpawnDistance, Y = 0.0f }, // East
                new { X = 0.0f, Y = -UCMStatueSpawnDistance },// South
                new { X = -UCMStatueSpawnDistance, Y = 0.0f } // West
            };

            int correctIndex = random.Next(4);
            for (int i = 0; i < 4; i++)
            {
                var statue = WorldObjectFactory.CreateNewWorldObject(UCMStatueWcid);
                if (statue == null) continue;

                var spawnLoc = new Position(CheckLocation);
                spawnLoc.PositionX += offsets[i].X;
                spawnLoc.PositionY += offsets[i].Y;
                spawnLoc.PositionZ += 1;

                if (i == correctIndex)
                {
                    // Look away
                    var dirAway = spawnLoc.Pos - CheckLocation.Pos;
                    spawnLoc.Rotate(dirAway);
                    CorrectStatue = statue;
                }
                else
                {
                    // Look at player
                    var dirToPlayer = CheckLocation.Pos - spawnLoc.Pos;
                    spawnLoc.Rotate(dirToPlayer);
                }

                statue.Location = spawnLoc;
                statue.Location.LandblockId = new LandblockId(statue.Location.GetCell());

                if (statue.EnterWorld())
                {
                    Statues.Add(statue);
                }
                else
                {
                    statue.Destroy();
                }
            }

            if (Statues.Count < 4 || CorrectStatue == null || CorrectStatue.IsDestroyed)
            {
                CleanupUCMCheck();
                return;
            }

            player.Session.Network.EnqueueSend(new GameMessageSystemChat("Your focus is being tested. Use the statue looking AWAY from you within 60 seconds or suffer consequences!", ChatMessageType.Broadcast));
        }

        private void HandleSuccess(Player player)
        {
            player.Session.Network.EnqueueSend(new GameMessageSystemChat("You passed the focus test.", ChatMessageType.Broadcast));
            CleanupUCMCheck();

        }
        private void HandleFailure(Player player, bool timeout = false)
        {
            player.Session.Network.EnqueueSend(new GameMessageSystemChat("You failed the focus test!", ChatMessageType.Broadcast));
            string reason = timeout ? "timed out" : "selected incorrectly";
            string auditMessage = $"[UCM Check] Player {player.Name} failed UCM check ({reason}).";
            PlayerManager.BroadcastToAuditChannel(player, auditMessage);
            CleanupUCMCheck();
            if (Position.TryParse(ServerConfig.ucmCheckFailTeleportLocation.Value, out Position failTeleLoc))
            {
                player.Teleport(failTeleLoc);
            }
            else if (player.Sanctuary != null)
            {
                player.Teleport(player.Sanctuary);
            }
        }

        // Returns true if the GUID belongs to an active statue, and false otherwise.
        public bool HandleActionUseItem(Player player, uint itemGuid)
        {
            if (!IsChecking) return false;

            if (!Statues.Any(s => s != null && s.Guid.Full == itemGuid)) return false;

            if (CorrectStatue != null && itemGuid == CorrectStatue.Guid.Full)
            {
                HandleSuccess(player);
            }
            else
            {
                HandleFailure(player);
            }
            return true;
        }

        public void Tick(Player player)
        {
            if (!IsChecking) return;

            if (DateTime.UtcNow > Timeout)
            {
                HandleFailure(player, true);
                return;
            }

            if (CheckLocation != null && player.Location.Distance2D(CheckLocation) > 1.0f)
            {
                // Increment ForcePosition sequence to make client accept the move without a portal screen
                player.UpdatePosition(CheckLocation, true);
                player.Sequences.GetNextSequence(Network.Sequence.SequenceType.ObjectForcePosition);
                player.Session.Network.EnqueueSend(new GameMessageUpdatePosition(player, false));
            }
        }

        private void CleanupUCMCheck()
        {
            IsChecking = false;
            CheckLocation = null;
            foreach (var statue in Statues)
            {
                if (statue == null) continue;
                if (!statue.IsDestroyed) statue.Destroy();
            }
            Statues.Clear();
            CorrectStatue = null;
        }
    }

    public partial class Player
    {
        public UCMChecker UCMChecker { get; } = new();
    }
}
