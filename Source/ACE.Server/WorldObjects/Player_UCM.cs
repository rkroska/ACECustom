using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using System;

namespace ACE.Server.WorldObjects
{

    public class UCMChecker()
    {
        public bool IsChecking { get; private set; } = false;
        private DateTime Timeout { get; set; }
        private Random RNG { get; } = new();
        private DateTime LastTickTime { get; set; } = DateTime.UtcNow;
        private DateTime LastUCMCheckTime { get; set; } = DateTime.UnixEpoch;

        /// <summary>
        /// Attempts to start a UCM check and returns true if it was started successfully.
        /// </summary>
        public bool Start(Player player)
        {
            if (IsChecking) return false;
            IsChecking = true;
            long secondsUntilTimeout = ServerConfig.ucm_check_timeout_seconds.Value;

            Timeout = DateTime.UtcNow.AddSeconds(secondsUntilTimeout);

            // Generate math problem using single digits (0-9)
            int a = RNG.Next(0, 10);
            int b = RNG.Next(0, 10);
            int correctAnswer = a + b;

            bool isRightAnswerForm = RNG.Next(0, 2) == 0;
            int shownAnswer = isRightAnswerForm ? correctAnswer : (correctAnswer + 1);

            string message = $"Is {a} + {b} = {shownAnswer}?\n\nYou have {secondsUntilTimeout} seconds to respond.";
            bool enqueued = player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, (response, timeout) =>
            {
                if (timeout)
                {
                    FailActiveCheck(player, "timed out");
                }
                else if (response == isRightAnswerForm)
                {
                    PassActiveCheck(player);
                }
                else
                {
                    FailActiveCheck(player, "selected incorrectly");
                }
            }), message);

            if (!enqueued)
            {
                IsChecking = false;
                return false;
            }

            LastUCMCheckTime = DateTime.UtcNow;
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
            IsChecking = false;

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
            IsChecking = false;

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
        /// Handles random starts of checks and timing out of active checks. For use by Player.Tick(). 
        /// </summary>
        public void Tick(Player player)
        {
            DateTime now = DateTime.UtcNow;
            TimeSpan sinceLastTick = now - LastTickTime;
            LastTickTime = now;

            if (!IsChecking)
            {
                // Player must have been in combat recently.
                if (now > player.LastCombatActionTime.AddSeconds(ServerConfig.ucm_check_combat_eligibility_seconds.Value)) return;

                // If not past the cooldown period since the last check, not eligible for a check.
                if (now < LastUCMCheckTime.AddSeconds(ServerConfig.ucm_check_cooldown_seconds.Value)) return;

                // If not in a valid landblock, not eligible for a check.
                ushort landblockId = (ushort)player.Location.Landblock;
                if (!LandblockCollections.ValleyOfDeathLandblocks.Contains(landblockId) && !LandblockCollections.ThaelarynIslandLandblocks.Contains(landblockId)) return;

                // Calculate the true probability of at least one trigger occurring over the elapsed time.
                // Formula: 1 - (1 - chancePerSecond)^TotalSeconds.
                // Note: ucm_check_spawn_chance is configured as a percentage between 0 and 1.
                double chancePerSec = Math.Clamp(ServerConfig.ucm_check_spawn_chance.Value, 0, 1);
                double probOverElapsed = 1.0 - Math.Pow(1.0 - chancePerSec, sinceLastTick.TotalSeconds);
                if (RNG.NextDouble() < probOverElapsed) Start(player);
                return;
            }

            if (now > Timeout)
            {
                FailActiveCheck(player, "timed out");
                return;
            }
        }
    }

    public partial class Player
    {
        public UCMChecker UCMChecker { get; } = new();
    }
}
