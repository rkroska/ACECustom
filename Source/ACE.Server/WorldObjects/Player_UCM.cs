using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using System;
using ACE.Common.Extensions;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.WorldObjects
{
    public enum UcmStage
    {
        None,
        Basic,
        BotDetection,
        BankCheck,
        EquipCheck
    }
    public enum UCMCheckFailReason
    {
        WrongAnswer,
        TimedOut,
        LoggedOut,
        SentByAdmin,
        SuspiciousSpeed,
    }

    /// <summary>
    /// Manages the logic for random "focus checks" that can occur for players in certain areas after combat.
    /// This class is thread-safe.
    /// </summary>
    public class UCMChecker(Player playerInstance)
    {
        private readonly System.Threading.Lock _lock = new();
        private readonly Player Self = playerInstance;
        private Random RNG { get; } = new();
        private bool IsChecking => CurrentStage != UcmStage.None;
        private DateTime Timeout { get; set; } = DateTime.UnixEpoch;
        private DateTime LastTickTime { get; set; } = DateTime.UtcNow;
        private DateTime LastUCMCheckTime { get; set; } = DateTime.UnixEpoch;
        /// <summary>Context id of the active UCM <see cref="ConfirmationType.Yes_No"/> dialog, if any.</summary>
        private uint? ActiveUcmConfirmationContextId { get; set; }
        /// <summary>UTC time the current prompt was shown (for audit / bot heuristics).</summary>
        private DateTime? UcmPromptStartedAtUtc { get; set; }
        /// <summary>The current active check stage.</summary>
        private UcmStage CurrentStage { get; set; } = UcmStage.None;

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
            var ucmBasicCheckConfirmation = new Confirmation_Custom(Self.Guid, (response, timedOut) =>
            {
                using var scope = _lock.EnterScope();
                if (!IsChecking || CurrentStage != UcmStage.Basic || !UcmPromptStartedAtUtc.HasValue)
                    return;

                if (timedOut)
                {
                    FailActiveCheckLocked(UCMCheckFailReason.TimedOut);
                }
                else if (response == isRightAnswerForm)
                {
                    // If extremely fast, trigger the random check.
                    TimeSpan responseTime = (UcmPromptStartedAtUtc ?? DateTime.MaxValue) - DateTime.UtcNow;
                    if (responseTime < TimeSpan.FromSeconds(1))
                    {
                        PlayerManager.BroadcastToAuditChannel(Self, $"[UCM Check] Player {Self.Name} was selected for an advanced UCM check (responded in {responseTime.GetFriendlyString() ?? "unknown time"}).");
                        StartAdvancedCheckLocked(UcmStage.BotDetection);
                        return;
                    }
                    // 5% chance to be randomly selected regardless of response time on basic check
                    if (RNG.NextDouble() < 0.05)
                    {
                        PlayerManager.BroadcastToAuditChannel(Self, $"[UCM Check] Player {Self.Name} was selected for an advanced UCM check (randomly chosen).");
                        StartAdvancedCheckLocked(UcmStage.BotDetection);
                        return;
                    }

                    PassActiveCheckLocked();
                }
                else
                {
                    FailActiveCheckLocked(UCMCheckFailReason.WrongAnswer);
                }
            });
            bool enqueued = Self.ConfirmationManager.EnqueueSend(ucmBasicCheckConfirmation, message, timeout.TotalSeconds);

            if (!enqueued) return false;

            var startUtc = DateTime.UtcNow;
            ActiveUcmConfirmationContextId = ucmBasicCheckConfirmation.ContextId;
            CurrentStage = UcmStage.Basic;
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
        /// If a check is active, finishes it.
        /// The lock must be held to call this method.
        /// </summary>
        private void PassActiveCheckLocked()
        {
            if (!IsChecking) return;

            int passCount = Self.QuestManager.Stamp("focus_test_passed");
            if (passCount >= 5) Self.QuestManager.StampFirst("focus_test_not_a_bot");
            if (passCount >= 10) Self.QuestManager.StampFirst("focus_test_staying_awake");
            if (passCount >= 20) Self.QuestManager.StampFirst("focus_test_actually_playing");
            if (passCount >= 50) Self.QuestManager.StampFirst("focus_test_unshakable_focus");

            Self.Session.Network.EnqueueSend(new GameMessageSystemChat("You passed the focus test.", ChatMessageType.Broadcast));
            var passElapsed = FormatUcmResponseSeconds(UcmPromptStartedAtUtc);
            var stageInfo = CurrentStage == UcmStage.Basic ? "" : $" (Stage: {CurrentStage})";
            PlayerManager.BroadcastToAuditChannel(Self, $"[UCM Check] Player {Self.Name} passed UCM check{stageInfo} at {Self.Location}.{passElapsed}");
            EndActiveCheckLocked();
        }

        /// <summary>
        /// If a check is active, fails it.
        /// </summary>
        public void FailActiveCheck(UCMCheckFailReason reason)
        {
            using var scope = _lock.EnterScope();
            FailActiveCheckLocked(reason);
        }

        /// <summary>
        /// If a check is active, fails it.
        /// The lock must be held to call this method.
        /// </summary>
        private void FailActiveCheckLocked(UCMCheckFailReason reason)
        {
            if (!IsChecking) return;

            // Tick timeout: confirmation is still registered; dismiss so the scheduled EnqueueAbort does not send a duplicate timeout message.
            if (ActiveUcmConfirmationContextId is uint ctx)
                Self.ConfirmationManager.TryDismissConfirmation(ConfirmationType.Yes_No, ctx);

            if (CurrentStage == UcmStage.Basic)
            {
                _ = reason switch
                {
                    UCMCheckFailReason.WrongAnswer => Self.QuestManager.StampFirst("focus_test_math_is_hard"),
                    UCMCheckFailReason.TimedOut => Self.QuestManager.StampFirst("focus_test_bathroom_break"),
                    UCMCheckFailReason.LoggedOut => Self.QuestManager.StampFirst("focus_test_tactical_dc"),
                    _ => 0
                };
            }

            string message = "You failed the focus test and have been punished!";
            Self.Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Broadcast));

            var failElapsed = FormatUcmResponseSeconds(UcmPromptStartedAtUtc);
            PlayerManager.BroadcastToAuditChannel(Self, $"[UCM Check] Player {Self.Name} failed UCM check ({GetUCMCheckFailReasonString(reason)}, Stage: {CurrentStage}) at {Self.Location}.{failElapsed}");
            EndActiveCheckLocked();
            Self.SendToJail();
            PlayerManager.BroadcastToAll(new GameMessageSystemChat($"{Self.Name} failed a focus check and was sent to jail!", ChatMessageType.Broadcast));
        }

        /// <summary>
        /// Performs steps to finish the check regardless of pass/fail/abort.
        /// While typically called by pass/fail, it can also be used directly to abort without a pass/fail occurring.
        /// </summary>
        private void EndActiveCheckLocked()
        {
            CurrentStage = UcmStage.None;
            ActiveUcmConfirmationContextId = null;
            UcmPromptStartedAtUtc = null;
        }

        /// <summary>
        /// Starts a specific advanced check stage.
        /// </summary>
        private void StartAdvancedCheckLocked(UcmStage stageToStart)
        {
            if (!IsChecking) return;

            // Update state
            CurrentStage = stageToStart;
            UcmPromptStartedAtUtc = DateTime.UtcNow;
            TimeSpan timeout = TimeSpan.FromSeconds(30); // They responded too quickly at first, so... fixed shorter interval.
            Timeout = DateTime.UtcNow.Add(timeout);

            string message = "";
            bool expectedAnswer = false;

            switch (stageToStart)
            {
                case UcmStage.BotDetection:
                    message = "Are you sure you're not a bot?\n\nAttempting to macro these prompts carries higher punishments.";
                    expectedAnswer = true; // Yes, I'm not a bot.
                    break;
                case UcmStage.BankCheck:
                    var bankCheck = GetBankCheckDetails();
                    message = $"Thanks, just to be sure, is your banked enlightened coins currently {bankCheck.shownValue}?";
                    expectedAnswer = bankCheck.isCorrect;
                    break;
                case UcmStage.EquipCheck:
                    var equip = GetEquipmentCheckDetails();
                    message = $"And are you wearing {equip.itemName} on your {equip.slotName} slot?";
                    expectedAnswer = equip.isCorrect;
                    break;
            }

            message += $"\n\nYou have {timeout.GetFriendlyLongString()} to respond.";

            UcmStage stageForCheck = stageToStart;
            var nextAdvancedCheckConfirmation = new Confirmation_Custom(Self.Guid, (response, timedOut) =>
            {
                using var scope = _lock.EnterScope();
                if (!IsChecking || CurrentStage != stageForCheck || !UcmPromptStartedAtUtc.HasValue)
                    return;

                if (timedOut)
                {
                    FailActiveCheckLocked(UCMCheckFailReason.TimedOut);
                }
                else if (response == expectedAnswer)
                {
                    // Answering too quickly on an advanced check is an automatic failure.
                    double secondsElapsed = (DateTime.UtcNow - UcmPromptStartedAtUtc.Value).TotalSeconds;
                    if (secondsElapsed < 1.0)
                    {
                        FailActiveCheckLocked(UCMCheckFailReason.SuspiciousSpeed);
                        return;
                    }

                    // They got it right, so advance to the next stage.
                    switch (CurrentStage)
                    {
                        case UcmStage.BotDetection:
                            StartAdvancedCheckLocked(UcmStage.BankCheck);
                            break;
                        case UcmStage.BankCheck:
                            StartAdvancedCheckLocked(UcmStage.EquipCheck);
                            break;
                        case UcmStage.EquipCheck:
                            PassActiveCheckLocked();
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    FailActiveCheckLocked(UCMCheckFailReason.WrongAnswer);
                }
            });

            // Dismiss the previous confirmation context if any (the math one already should be dismissed or completed)
            if (ActiveUcmConfirmationContextId.HasValue)
                Self.ConfirmationManager.TryDismissConfirmation(ConfirmationType.Yes_No, ActiveUcmConfirmationContextId.Value);

            if (Self.ConfirmationManager.EnqueueSend(nextAdvancedCheckConfirmation, message, timeout.TotalSeconds))
            {
                ActiveUcmConfirmationContextId = nextAdvancedCheckConfirmation.ContextId;
            }
            else
            {
                // Unexpectedly failed to start, so abort it.
                EndActiveCheckLocked();
            }
        }

        private (string shownValue, bool isCorrect) GetBankCheckDetails()
        {
            long trueCoins = Self.BankedEnlightenedCoins ?? 0;
            bool isCorrect = RNG.Next(0, 2) == 0;
            
            // 50% chance of being off-by-one.
            long shownValue = isCorrect ? trueCoins : (trueCoins + 1);
            
            // Format with commas for readability (e.g. "1,234,567")
            return (shownValue.ToString("N0"), isCorrect);
        }

        private (string slotName, string itemName, bool isCorrect) GetEquipmentCheckDetails()
        {
            var allSingleBits = new List<EquipMask>
            {
                EquipMask.HeadWear, EquipMask.ChestWear, EquipMask.AbdomenWear,
                EquipMask.UpperArmWear, EquipMask.LowerArmWear, EquipMask.HandWear,
                EquipMask.UpperLegWear, EquipMask.LowerLegWear, EquipMask.FootWear,
                EquipMask.ChestArmor, EquipMask.AbdomenArmor, EquipMask.UpperArmArmor,
                EquipMask.LowerArmArmor, EquipMask.UpperLegArmor, EquipMask.LowerLegArmor,
                EquipMask.NeckWear, EquipMask.WristWearLeft, EquipMask.WristWearRight,
                EquipMask.FingerWearLeft, EquipMask.FingerWearRight, EquipMask.MeleeWeapon,
                EquipMask.Shield, EquipMask.MissileWeapon, EquipMask.MissileAmmo,
                EquipMask.Held, EquipMask.TwoHanded, EquipMask.TrinketOne, EquipMask.Cloak,
                EquipMask.SigilOne, EquipMask.SigilTwo, EquipMask.SigilThree
            };

            var items = Self.EquippedObjects.Values.ToList();
            
            // If naked, handle as a "nothing" check
            if (items.Count == 0)
            {
                var slot = allSingleBits[RNG.Next(allSingleBits.Count)];
                bool isCorrectNaked = RNG.Next(0, 2) == 0;
                return (slot.ToFriendlyString(), isCorrectNaked ? "nothing" : "Silk Shirt", isCorrectNaked);
            }

            // Pick a random item the player HAS
            var targetItem = items[RNG.Next(items.Count)];
            var currentLoc = (EquipMask)(targetItem.CurrentWieldedLocation ?? EquipMask.None);

            // 50% chance to ask about its correct slot, 50% to ask about an incorrect one
            bool askCorrectSlot = RNG.Next(0, 2) == 0;
            EquipMask querySlot;

            if (askCorrectSlot)
            {
                // Pick a slot that this specific item DOES occupy
                var possibleSlots = allSingleBits.Where(b => (currentLoc & b) != 0).ToList();
                querySlot = possibleSlots.Count > 0 ? possibleSlots[RNG.Next(possibleSlots.Count)] : allSingleBits[RNG.Next(allSingleBits.Count)];
            }
            else
            {
                // Pick a slot that this specific item DOES NOT occupy
                var incorrectSlots = allSingleBits.Where(b => (currentLoc & b) == 0).ToList();
                querySlot = incorrectSlots.Count > 0 ? incorrectSlots[RNG.Next(incorrectSlots.Count)] : allSingleBits[RNG.Next(allSingleBits.Count)];
            }

            // Verify what is actually in that slot to determine the definitive correct answer
            var itemInQuerySlot = items.FirstOrDefault(i => 
                i.CurrentWieldedLocation != null && ((EquipMask)i.CurrentWieldedLocation & querySlot) != 0);
            
            // The answer is YES ONLY if the item in that slot has the same name as our target item
            bool actualAnswer = itemInQuerySlot?.Name == targetItem.Name;

            return (querySlot.ToFriendlyString(), targetItem.Name, actualAnswer);
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
                FailActiveCheckLocked(UCMCheckFailReason.TimedOut);
                return;
            }
        }

        /// <summary>
        /// Returns a human-readable string for the given jail reason, for use in audit messages.
        /// </summary>
        /// <param name="reason"></param>
        /// <returns></returns>
        public string GetUCMCheckFailReasonString(UCMCheckFailReason reason) => reason switch
        {
            UCMCheckFailReason.WrongAnswer => "selected incorrectly",
            UCMCheckFailReason.TimedOut => "timed out",
            UCMCheckFailReason.LoggedOut => "logged out",
            UCMCheckFailReason.SentByAdmin => "sent by admin",
            UCMCheckFailReason.SuspiciousSpeed => "responded too fast",
            _ => "unknown reason",
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
