using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using System;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Manages the logic for random "focus checks" that can occur for players in certain areas after combat.
    /// This class is thread-safe.
    /// </summary>
    public class UCMChecker()
    {
        private readonly System.Threading.Lock _lock = new();
        private Random RNG { get; } = new();
        private bool IsChecking { get; set; } = false;
        private DateTime Timeout { get; set; } = DateTime.UnixEpoch;
        private DateTime LastTickTime { get; set; } = DateTime.UtcNow;
        private DateTime LastUCMCheckTime { get; set; } = DateTime.UnixEpoch;

        /// <summary>
        /// Determines whether a check operation is currently in progress.
        /// </summary>
        public bool IsCheckInProgress()
        {
            using var scope = _lock.EnterScope();
            return IsChecking;
        }

        /// <summary>
        /// Attempts to start a UCM check and returns true if it was started successfully.
        /// </summary>
        public bool Start(Player player)
        {
            using var scope = _lock.EnterScope();
            return StartLocked(player);
        }

        /// <summary>
        /// Attempts to start a UCM check and returns true if it was started successfully.
        /// The lock must be held to call this method.
        /// </summary>
        private bool StartLocked(Player player)
        {
            if (IsChecking) return false;

            // Generate math problem using single digits (0-9)
            int a = RNG.Next(0, 10);
            int b = RNG.Next(0, 10);
            int correctAnswer = a + b;

            bool isRightAnswerForm = RNG.Next(0, 2) == 0;
            int shownAnswer = isRightAnswerForm ? correctAnswer : (correctAnswer + 1);

            long secondsUntilTimeout = ServerConfig.ucm_check_timeout_seconds.Value;
            string message = $"Is {a} + {b} = {shownAnswer}?\n\nYou have {secondsUntilTimeout} seconds to respond.";
            bool enqueued = player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Guid, (response, timeout) =>
            {
                // This callback is executed asynchronously.
                // The lock has been released at this point.
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

            if (!enqueued) return false;

            IsChecking = true;
            LastUCMCheckTime = DateTime.UtcNow;
            Timeout = DateTime.UtcNow.AddSeconds(secondsUntilTimeout);
            return true;
        }

        /// <summary>
        /// If a check is active, passes it.
        /// </summary>
        public void PassActiveCheck(Player player)
        {
            using var scope = _lock.EnterScope();
            PassActiveCheckLocked(player);
        }

        /// <summary>
        /// If a check is active, passes it.
        /// The lock must be held to call this method.
        /// </summary>
        private void PassActiveCheckLocked(Player player)
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
            using var scope = _lock.EnterScope();
            FailActiveCheckLocked(player, reason, doTeleport);
        }

        /// <summary>
        /// If a check is active, fails it.
        /// The lock must be held to call this method.
        /// </summary>
        private void FailActiveCheckLocked(Player player, string reason, bool doTeleport = true)
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
            using var scope = _lock.EnterScope();
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
                if (RNG.NextDouble() < probOverElapsed) StartLocked(player);
                return;
            }

            if (now > Timeout)
            {
                FailActiveCheckLocked(player, "timed out");
                return;
            }
        }
    }

    public partial class Player
    {
        public UCMChecker UCMChecker { get; } = new();
    }
}
