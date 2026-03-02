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

        /// <summary>
        /// Spawns the WorldObject for the statue and returns it.
        /// </summary>
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

        /// <summary>
        /// Attempts to start a UCM check and returns true if it was started successfully.
        /// </summary>
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

        /// <summary>
        /// If a check is active, passes it.
        /// </summary>
        private void PassActiveCheck(Player player)
        {
            if (!IsChecking) return;
            player.Session.Network.EnqueueSend(new GameMessageSystemChat("You passed the focus test.", ChatMessageType.Broadcast));
            PlayerManager.BroadcastToAuditChannel(player, $"[UCM Check] Player {player.Name} passed UCM check at {player.Location}.");
            Stop();

        }
        /// <summary>
        /// If a check is active, fails it.
        /// </summary>
        public void FailActiveCheck(Player player, string reason, bool doTeleport = true)
        {
            if (!IsChecking) return;
            string message = "You failed the focus test and have been punished!";
            player.Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Broadcast));
            player.Session.Network.EnqueueSend(new GameEventPopupString(player.Session, message));

            PlayerManager.BroadcastToAuditChannel(player, $"[UCM Check] Player {player.Name} failed UCM check ({reason}) at {player.Location}.");
            Stop();

            if (doTeleport)
            {
                // Try teleporting the player to the configured location.
                // Fallback to what would happen if the player died.
                if (Position.TryParse(ServerConfig.ucm_check_fail_teleport_location.Value, out Position failTeleLoc))
                {
                    player.Teleport(failTeleLoc);
                }
                else
                {
                    player.Teleport(player.GetDeathLocation());
                }
            }

        }

        /// <summary>
        /// Returns true if the GUID belongs to an active statue, and false otherwise.
        /// </summary>
        public bool HandleActionUseItem(Player player, uint itemGuid)
        {
            if (!IsChecking) return false;

            if (!Statues.Any(s => s != null && s.Guid.Full == itemGuid)) return false;

            if (CorrectStatue != null && itemGuid == CorrectStatue.Guid.Full)
            {
                PassActiveCheck(player);
            }
            else
            {
                FailActiveCheck(player, "selected incorrectly");
            }
            return true;
        }

        /// <summary>
        /// Handles random starts of checks and timing out of active checks. For use by Player.Tick(). 
        /// </summary>
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
                FailActiveCheck(player, "timed out");
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

        /// <summary>
        /// Stops an active check and resets state.
        /// </summary>
        private void Stop()
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
