using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.WorldObjects
{

    public class UCMChecker()
    {
        private const float UCMStatueSpawnDistance = 1.5f;
        public bool IsChecking { get; private set; } = false;
        private DateTime Timeout { get; set; }
        private List<WorldObject> Statues { get; } = [];
        private WorldObject CorrectStatue { get; set; }
        private Position CheckLocation { get; set; }
        private Random random { get; } = new();
        public DateTime LastTickTime { get; set; } = DateTime.UtcNow;
        public DateTime LastUCMCheckTime { get; set; } = DateTime.UnixEpoch;

        private static WorldObject MakeStatue()
        {
            WorldObject statue = WorldObjectFactory.CreateNewWorldObject((uint)ServerConfig.ucm_check_statue_wcid.Value);
            if (statue == null) return statue;

            statue.ItemType = ItemType.Misc;
            statue.ItemUseable = Usable.RemoteNeverWalk;
            statue.RadarColor = RadarColor.Yellow;
            statue.Ethereal = true;
            statue.Attackable = false;
            statue.Invincible = true;
            statue.AllowEdgeSlide = false;
            statue.Static = true;
            statue.IsFrozen = true;
            statue.Name = "Arcane Test Statue";
            return statue;
        }

        // Returns true if the UCM check was started.
        public bool Start(Player player)
        {
            if (IsChecking) return false;
            IsChecking = true;
            CheckLocation = new Position(player.Location);
            long secondsUntilTimeout = ServerConfig.ucm_check_timeout_seconds.Value;

            // N, E, S, W relative or absolute doesn't matter too much, we'll use absolute cardinal offsets
            // Assuming Location.Position.X/Y are coordinates
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
                var statue = MakeStatue();
                if (statue == null) continue;

                // Ensure the statues will clean up themselves as a backstop.
                statue.TimeToRot = secondsUntilTimeout + 10;

                var spawnLoc = new Position(CheckLocation);
                spawnLoc.PositionX += offsets[i].X;
                spawnLoc.PositionY += offsets[i].Y;
                // We don't set Z - our static will pop onto the landblock surface +/- a few units.

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
                Stop();
                return false;
            }

            Timeout = DateTime.UtcNow.AddSeconds(secondsUntilTimeout);
            LastUCMCheckTime = DateTime.UtcNow;

            string message = $"Your focus is being tested. Use the statue looking AWAY from you within {secondsUntilTimeout} seconds or suffer consequences!";
            player.Session.Network.EnqueueSend(new GameEventPopupString(player.Session, message));
            player.Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Broadcast));
            return true;
        }

        private void HandleSuccess(Player player)
        {
            player.Session.Network.EnqueueSend(new GameMessageSystemChat("You passed the focus test.", ChatMessageType.Broadcast));
            Stop();

        }
        private void HandleFailure(Player player, bool timeout = false)
        {
            string message = "You failed the focus test and have been punished!";
            player.Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Broadcast));
            player.Session.Network.EnqueueSend(new GameEventPopupString(player.Session, message));

            string reason = timeout ? "timed out" : "selected incorrectly";
            string auditMessage = $"[UCM Check] Player {player.Name} failed UCM check ({reason}) at {player.Location}.";
            PlayerManager.BroadcastToAuditChannel(player, auditMessage);
            Stop();

            // Try teleporting the player to the configured location.
            if (Position.TryParse(ServerConfig.ucm_check_fail_teleport_location.Value, out Position failTeleLoc))
            {
                player.Teleport(failTeleLoc);
                return;
            }

            // Fallback to what would happen if the player died.
            player.Teleport(player.GetDeathLocation());
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
            DateTime now = DateTime.UtcNow;
            TimeSpan sinceLastTick = now - LastTickTime;
            LastTickTime = now;

            if (!IsChecking)
            {
                // If player jumping or teleporting, don't start a check!
                if (player.IsJumping || player.Teleporting) return;

                // If not in a valid landblock, not eligible for a check.
                ushort landblockId = (ushort)player.Location.Landblock;
                if (!LandblockCollections.ValleyOfDeathLandblocks.Contains(landblockId) && !LandblockCollections.ThaelarynIslandLandblocks.Contains(landblockId)) return;

                // If not past the cooldown period since the last check, not eligible for a check.
                if (now < LastUCMCheckTime.AddSeconds(ServerConfig.ucm_check_cooldown_seconds.Value)) return;

                // Calculate the true probability of at least one trigger occurring over the elapsed time.
                // Formula: 1 - (1 - chancePerSecond)^TotalSeconds.
                // Note: ucm_check_spawn_chance is configured as a percentage between 0 and 1.
                double chancePerSec = Math.Clamp(ServerConfig.ucm_check_spawn_chance.Value, 0, 1);
                double probOverElapsed = 1.0 - Math.Pow(1.0 - chancePerSec, sinceLastTick.TotalSeconds);
                if (random.NextDouble() < probOverElapsed) Start(player);
                return;
            }

            if (now > Timeout)
            {
                HandleFailure(player, true);
                return;
            }

            if (CheckLocation != null && player.Location.Distance2D(CheckLocation) > UCMStatueSpawnDistance)
            {
                // Increment ForcePosition sequence to make client accept the move without a portal screen
                player.UpdatePosition(CheckLocation, true);
                player.Sequences.GetNextSequence(Network.Sequence.SequenceType.ObjectForcePosition);
                player.Session.Network.EnqueueSend(new GameMessageUpdatePosition(player, false));
            }
        }

        public void Stop()
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
