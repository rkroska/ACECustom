using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using System;
using ACE.Common.Extensions;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Manages the logic for random "focus checks" that can occur for players in certain areas after combat.
    /// This class is thread-safe.
    /// </summary>
    public class UCMChecker(Player playerInstance)
    {
        private readonly System.Threading.Lock _lock = new();
        private readonly Player Self = playerInstance;
        private Random RNG { get; } = new();
        private bool IsChecking { get; set; } = false;
        private DateTime Timeout { get; set; } = DateTime.UnixEpoch;
        private DateTime LastTickTime { get; set; } = DateTime.UtcNow;
        private DateTime LastUCMCheckTime { get; set; } = DateTime.UnixEpoch;
        /// <summary>Context id of the active UCM <see cref="ConfirmationType.Yes_No"/> dialog, if any.</summary>
        private uint? ActiveUcmConfirmationContextId { get; set; }
        /// <summary>UTC time the current prompt was shown (for audit / bot heuristics).</summary>
        private DateTime? UcmPromptStartedAtUtc { get; set; }

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
        public bool Start()
        {
            using var scope = _lock.EnterScope();
            return StartLocked();
        }

        /// <summary>
        /// Attempts to start a UCM check and returns true if it was started successfully.
        /// The lock must be held to call this method.
        /// </summary>
        private bool StartLocked()
        {
            if (IsChecking) return false;

            // Generate math problem using single digits (0-9)
            int a = RNG.Next(0, 10);
            int b = RNG.Next(0, 10);
            int correctAnswer = a + b;

            bool isRightAnswerForm = RNG.Next(0, 2) == 0;
            int shownAnswer = isRightAnswerForm ? correctAnswer : (correctAnswer + 1);

            TimeSpan timeout = TimeSpan.FromSeconds(ServerConfig.ucm_check_timeout_seconds.Value);
            string message = $"Is {a} + {b} = {shownAnswer}?\n\nYou have {timeout.GetFriendlyLongString()} to respond.";
            var ucmConfirmation = new Confirmation_Custom(Self.Guid, (response, timeout) =>
            {
                // This callback is executed asynchronously.
                // The lock has been released at this point.
                if (timeout)
                {
                    FailActiveCheck(JailReason.TimedOut);
                }
                else if (response == isRightAnswerForm)
                {
                    PassActiveCheck();
                }
                else
                {
                    FailActiveCheck(JailReason.WrongAnswer);
                }
            });
            bool enqueued = Self.ConfirmationManager.EnqueueSend(ucmConfirmation, message, timeout.TotalSeconds);

            if (!enqueued) return false;

            var startUtc = DateTime.UtcNow;
            ActiveUcmConfirmationContextId = ucmConfirmation.ContextId;
            IsChecking = true;
            UcmPromptStartedAtUtc = startUtc;
            LastUCMCheckTime = startUtc;
            Timeout = startUtc.Add(timeout);
            return true;
        }

        /// <summary>
        /// If a check is active, passes it.
        /// </summary>
        public void PassActiveCheck()
        {
            using var scope = _lock.EnterScope();
            PassActiveCheckLocked();
        }

        /// <summary>
        /// If a check is active, passes it.
        /// The lock must be held to call this method.
        /// </summary>
        private void PassActiveCheckLocked()
        {
            if (!IsChecking) return;
            Self.Session.Network.EnqueueSend(new GameMessageSystemChat("You passed the focus test.", ChatMessageType.Broadcast));
            var passElapsed = FormatUcmResponseSeconds(UcmPromptStartedAtUtc);
            PlayerManager.BroadcastToAuditChannel(Self, $"[UCM Check] Player {Self.Name} passed UCM check at {Self.Location}.{passElapsed}");
            IsChecking = false;
            ActiveUcmConfirmationContextId = null;
            UcmPromptStartedAtUtc = null;

        }

        /// <summary>
        /// If a check is active, fails it.
        /// </summary>
        public void FailActiveCheck(JailReason reason)
        {
            using var scope = _lock.EnterScope();
            FailActiveCheckLocked(reason);
        }

        /// <summary>
        /// If a check is active, fails it.
        /// The lock must be held to call this method.
        /// </summary>
        private void FailActiveCheckLocked(JailReason reason)
        {
            if (!IsChecking) return;

            // Tick timeout: confirmation is still registered; dismiss so the scheduled EnqueueAbort does not send a duplicate timeout message.
            if (ActiveUcmConfirmationContextId is uint ctx)
                Self.ConfirmationManager.TryDismissConfirmation(ConfirmationType.Yes_No, ctx);

            string message = "You failed the focus test and have been punished!";
            Self.Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Broadcast));

            var failElapsed = FormatUcmResponseSeconds(UcmPromptStartedAtUtc);
            PlayerManager.BroadcastToAuditChannel(Self, $"[UCM Check] Player {Self.Name} failed UCM check ({reason}) at {Self.Location}.{failElapsed}");
            IsChecking = false;
            ActiveUcmConfirmationContextId = null;
            UcmPromptStartedAtUtc = null;
            Self.SendToJail(reason);
            PlayerManager.BroadcastToAll(new GameMessageSystemChat($"{Self.Name} failed a UCM check ({reason}) and was sent to jail!", ChatMessageType.Broadcast));
        }

        /// <summary>
        /// Handles random starts of checks and timing out of active checks. For use by Player.Tick(). 
        /// Also enforces the active UCM Jails.
        /// </summary>
        public void Tick()
        {
            using var scope = _lock.EnterScope();
            DateTime now = DateTime.UtcNow;
            TimeSpan sinceLastTick = now - LastTickTime;
            LastTickTime = now;

            if (!IsChecking)
            {
                // Player must have been in combat recently.
                if (now > Self.LastCombatActionTime.AddSeconds(ServerConfig.ucm_check_combat_eligibility_seconds.Value)) return;

                // If not past the cooldown period since the last check, not eligible for a check.
                if (now < LastUCMCheckTime.AddSeconds(ServerConfig.ucm_check_cooldown_seconds.Value)) return;

                // If not in a valid landblock, not eligible for a check.
                ushort landblockId = (ushort)Self.Location.Landblock;
                if (!LandblockCollections.ValleyOfDeathLandblocks.Contains(landblockId) && !LandblockCollections.ThaelarynIslandLandblocks.Contains(landblockId)) return;

                // Calculate the true probability of at least one trigger occurring over the elapsed time.
                // Formula: 1 - (1 - chancePerSecond)^TotalSeconds.
                // Note: ucm_check_spawn_chance is configured as a percentage between 0 and 1.
                double chancePerSec = Math.Clamp(ServerConfig.ucm_check_spawn_chance.Value, 0, 1);
                double probOverElapsed = 1.0 - Math.Pow(1.0 - chancePerSec, sinceLastTick.TotalSeconds);
                if (RNG.NextDouble() < probOverElapsed) StartLocked();
                return;
            }

            if (now > Timeout)
            {
                FailActiveCheckLocked(JailReason.TimedOut);
                return;
            }
        }

        /// <summary>
        /// Returns a human-readable string for the given jail reason, for use in audit messages.
        /// </summary>
        /// <param name="reason"></param>
        /// <returns></returns>
        public string GetJailReasonString(JailReason reason) => reason switch
        {
            JailReason.WrongAnswer => "selected incorrectly",
            JailReason.TimedOut => "timed out",
            JailReason.LoggedOut => "logged out",
            JailReason.SentByAdmin => "sent by admin",
            _ => "an unknown reason"
        };

        /// <summary>Audit suffix: seconds from prompt shown to outcome (UTC).</summary>
        private static string FormatUcmResponseSeconds(DateTime? promptStartedUtc)
        {
            if (!promptStartedUtc.HasValue)
                return string.Empty;

            TimeSpan elapsed = DateTime.UtcNow - promptStartedUtc.Value;
            return $" Response time: {elapsed.GetFriendlyString()}.";
        }
    }

    public partial class Player
    {
        public UCMChecker UCMChecker { get; }
    }
}
