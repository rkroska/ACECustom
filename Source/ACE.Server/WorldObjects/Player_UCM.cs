using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using System;
using ACE.Common.Extensions;

namespace ACE.Server.WorldObjects
{
    public enum UcmStage
    {
        None,
        Basic,
        BotDetection
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
        /// True when the active <see cref="UcmStage.Basic"/> prompt is the advanced math variant.
        /// Cleared on escalation, so a bot detection failure still carries the full sentence.
        /// </summary>
        private bool CurrentQuestionIsAdvanced { get; set; }

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

            // While the toggle is on, the hard question replaces the simple one entirely.
            bool isAdvanced = ServerConfig.ucm_advanced_math_enabled.Value;
            bool isRightAnswerForm;
            string question;

            if (isAdvanced)
            {
                question = BuildAdvancedQuestion(out isRightAnswerForm);
            }
            else
            {
                // Generate math problem using single digits (0-9)
                int a = RNG.Next(0, 10);
                int b = RNG.Next(0, 10);
                int correctAnswer = a + b;

                isRightAnswerForm = RNG.Next(0, 2) == 0;
                int shownAnswer = isRightAnswerForm ? correctAnswer : (correctAnswer + 1);
                question = $"Is {a} + {b} = {shownAnswer}?";
            }

            TimeSpan timeout = TimeSpan.FromSeconds(ServerConfig.ucm_check_timeout_seconds.Value);
            string message = $"{question}\n\nYou have {timeout.GetFriendlyLongString()} to respond.";
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
                    TimeSpan responseTime = DateTime.UtcNow - (UcmPromptStartedAtUtc ?? DateTime.MaxValue);
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
            CurrentQuestionIsAdvanced = isAdvanced;
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

            var passed = Self.GetProperty(PropertyInt.TimesUcmCheckPassed) ?? 0;
            if (passed < int.MaxValue)
                Self.SetProperty(PropertyInt.TimesUcmCheckPassed, passed + 1);

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

            // Captured before EndActiveCheckLocked clears the stage state.
            bool advancedMathFail = CurrentStage == UcmStage.Basic && CurrentQuestionIsAdvanced;

            // The focus test stamps feed titles and leaderboards, so the joke question does not award them.
            if (CurrentStage == UcmStage.Basic && !advancedMathFail)
            {
                _ = reason switch
                {
                    UCMCheckFailReason.WrongAnswer => Self.QuestManager.StampFirst("focus_test_math_is_hard"),
                    UCMCheckFailReason.TimedOut => Self.QuestManager.StampFirst("focus_test_bathroom_break"),
                    UCMCheckFailReason.LoggedOut => Self.QuestManager.StampFirst("focus_test_tactical_dc"),
                    _ => 0
                };
            }

            string message = advancedMathFail ? PickAdvancedMathInsult() : "You failed the focus test and have been punished!";
            Self.Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Broadcast));

            var failElapsed = FormatUcmResponseSeconds(UcmPromptStartedAtUtc);
            var stageTag = advancedMathFail ? $"{CurrentStage} / AdvancedMath" : CurrentStage.ToString();
            PlayerManager.BroadcastToAuditChannel(Self, $"[UCM Check] Player {Self.Name} failed UCM check ({GetUCMCheckFailReasonString(reason)}, Stage: {stageTag}) at {Self.Location}.{failElapsed}");
            EndActiveCheckLocked();

            if (advancedMathFail)
            {
                Self.SendToJail(TimeSpan.FromSeconds(ServerConfig.ucm_advanced_math_jail_seconds.Value),
                                countsTowardTotal: false);
                PlayerManager.BroadcastToAll(new GameMessageSystemChat($"{Self.Name} could not do basic math and has been jailed.", ChatMessageType.Broadcast));
            }
            else
            {
                Self.SendToJail();
                PlayerManager.BroadcastToAll(new GameMessageSystemChat($"{Self.Name} failed a focus check and was sent to jail!", ChatMessageType.Broadcast));
            }
        }

        /// <summary>
        /// Performs steps to finish the check regardless of pass/fail/abort.
        /// While typically called by pass/fail, it can also be used directly to abort without a pass/fail occurring.
        /// </summary>
        private void EndActiveCheckLocked()
        {
            CurrentStage = UcmStage.None;
            CurrentQuestionIsAdvanced = false;
            ActiveUcmConfirmationContextId = null;
            UcmPromptStartedAtUtc = null;
        }

        /// <summary>
        /// Starts a specific advanced check stage.
        /// </summary>
        private void StartAdvancedCheckLocked(UcmStage stageToStart)
        {
            if (!IsChecking) return;

            // Update state. Escalation leaves the joke question behind: bot detection is a genuine
            // check, so a failure here carries the full sentence even while advanced math is enabled.
            CurrentStage = stageToStart;
            CurrentQuestionIsAdvanced = false;
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

        /// <summary>
        /// Sent to a player who fails the advanced math question. The joke is that the question
        /// they just failed was anything but basic.
        /// </summary>
        private static readonly string[] AdvancedMathInsults =
        [
            "You failed. That was BASIC math, friend. Basic. Take a moment to reflect on it.",
            "Wrong. Somewhere a math teacher just felt a chill.",
            "Incorrect. Perhaps stick to hitting things with a stick.",
            "Failed. Your abacus is missing beads.",
            "Nope. Basic numbers are clearly not for everyone.",
            "Wrong answer. Counting to ten must be quite the adventure for you.",
            "Incorrect. Do not fret, basic arithmetic is only most of mathematics.",
            "Failed. A drudge could have worked that one out, and drudges cannot even count.",
        ];

        private string PickAdvancedMathInsult() => AdvancedMathInsults[RNG.Next(0, AdvancedMathInsults.Length)];

        private static readonly string[] QuestionNames =
        [
            "John", "Bob", "Marta", "Elysa", "Torvin", "Nalani", "Gerrik", "Sila",
        ];

        private static readonly string[] QuestionPlaces =
        [
            "Holtburg", "Shoushi", "Yaraq", "Cragstone", "Arwic", "Rithwic", "Samsur", "Al-Arqas",
        ];

        /// <summary>
        /// Speed pairs whose harmonic mean is a whole number, for the round trip question.
        /// Scaled at use so the speeds read plausibly.
        /// </summary>
        private static readonly (long Slow, long Fast)[] HarmonicSpeedPairs =
        [
            (2, 6), (3, 6), (4, 12), (6, 12), (5, 20), (3, 15), (10, 15), (6, 30), (8, 24), (12, 36),
        ];

        private string PickName() => QuestionNames[RNG.Next(0, QuestionNames.Length)];

        private string PickPlace() => QuestionPlaces[RNG.Next(0, QuestionPlaces.Length)];

        /// <summary>Two distinct entries from the given pool.</summary>
        private (string First, string Second) PickTwo(string[] pool)
        {
            int i = RNG.Next(0, pool.Length);
            int j = RNG.Next(0, pool.Length - 1);
            if (j >= i) j++;
            return (pool[i], pool[j]);
        }

        /// <summary>
        /// Builds an integer-exact question that almost nobody can answer in their head, either a
        /// raw arithmetic one or a word problem. The value shown is the true answer half the time,
        /// so a blind guess stays a coin flip.
        /// </summary>
        /// <param name="shownIsCorrect">True when the value displayed is the true answer.</param>
        private string BuildAdvancedQuestion(out bool shownIsCorrect)
        {
            long correct;
            string template;            // composite format string; {0} is the value being asserted
            long? trapAnswer = null;    // the wrong answer this particular question invites, if any
            long? shownMaxValue = null; // upper bound the asserted value must respect, if any

            switch (RNG.Next(0, 11))
            {
                case 0:
                {
                    long x = RNG.Next(1000, 10000);
                    long y = RNG.Next(1000, 10000);
                    correct = x * y;
                    template = $"Is {x} * {y} = {{0}}?";
                    break;
                }
                case 1:
                {
                    long b = RNG.Next(3, 20);
                    int e = RNG.Next(7, 18);
                    correct = ModPow(b, e, 1000);
                    template = $"Is {b} ^ {e} mod 1000 = {{0}}?";
                    shownMaxValue = 999;    // a value of 1000+ could never be a mod 1000 result
                    break;
                }
                case 2:
                {
                    int n = RNG.Next(11, 18);
                    correct = Factorial(n);
                    template = $"Is {n}! = {{0}}?";
                    break;
                }
                case 3:
                {
                    int n = RNG.Next(40, 60);
                    correct = Fibonacci(n);
                    template = $"Is the {Ordinal(n)} Fibonacci number = {{0}}?";
                    break;
                }
                case 4:
                {
                    int n = RNG.Next(20, 34);
                    int k = RNG.Next(6, 13);
                    correct = Binomial(n, k);
                    template = $"Is {n} choose {k} = {{0}}?";
                    break;
                }
                case 5:
                {
                    int limit = RNG.Next(150, 400);
                    correct = SumOfPrimesBelow(limit);
                    template = $"Is the sum of all primes below {limit} = {{0}}?";
                    break;
                }
                case 6:
                {
                    // Catch-up. Built from k and g so the meeting lands on a whole hour:
                    // the leader covers v1 * (t + h) and the chaser v2 * t, which agree by construction.
                    long h = RNG.Next(2, 7);        // head start, in hours
                    long k = RNG.Next(2, 9);
                    long g = RNG.Next(2, 10);       // difference in speed
                    long v1 = k * g;
                    long v2 = v1 + g;
                    long t = h * k;                 // hours the chaser rides before catching up
                    correct = v2 * t;
                    trapAnswer = v1 * t;            // forgetting the head start
                    var (leader, chaser) = PickTwo(QuestionNames);
                    string town = PickPlace();
                    template = $"{leader} rides out of {town} at {v1} mph. {h} hours later {chaser} sets out along the same road at {v2} mph. Is {chaser} exactly {{0}} miles from {town} at the moment {chaser} catches {leader}?";
                    break;
                }
                case 7:
                {
                    // Work rate. Total effort is a * t1 = a * b * m, so b workers take a * m hours.
                    long a = RNG.Next(3, 16);
                    long b = RNG.Next(3, 16);
                    while (b == a) b = RNG.Next(3, 16);
                    long m = RNG.Next(2, 9);
                    long t1 = b * m;
                    correct = a * m;
                    template = $"It takes {a} drudges {t1} hours to dig a tunnel. Working at that same rate, is the time for {b} drudges to dig the same tunnel {{0}} hours?";
                    break;
                }
                case 8:
                {
                    // Closing on each other. Distance is built as t * (v1 + v2) so they meet on the hour.
                    long t = RNG.Next(2, 10);
                    long v1 = RNG.Next(8, 40);
                    long v2 = RNG.Next(8, 40);
                    long apart = t * (v1 + v2);
                    correct = v1 * t;
                    if (apart % 2 == 0) trapAnswer = apart / 2;   // assuming they meet in the middle
                    var (townA, townB) = PickTwo(QuestionPlaces);
                    template = $"{townA} and {townB} are {apart} miles apart. A caravan leaves {townA} at {v1} mph and at the same moment another leaves {townB} at {v2} mph, each bound for the other town. Is the point where they meet {{0}} miles from {townA}?";
                    break;
                }
                case 9:
                {
                    // Round trip average speed: the harmonic mean, not the arithmetic one, and the
                    // distance cancels out entirely. Pairs are chosen so the true answer is whole.
                    var pair = HarmonicSpeedPairs[RNG.Next(0, HarmonicSpeedPairs.Length)];
                    // Scale so the faster leg lands between 12 and 60 mph rather than walking pace.
                    long maxScale = Math.Max(1, 60 / pair.Fast);
                    long minScale = Math.Min(maxScale, Math.Max(1, (12 + pair.Fast - 1) / pair.Fast));
                    long scale = RNG.NextInt64(minScale, maxScale + 1);
                    long outbound = pair.Slow * scale;
                    long inbound = pair.Fast * scale;
                    if (RNG.Next(0, 2) == 0) (outbound, inbound) = (inbound, outbound);
                    long miles = RNG.Next(2, 21) * 6;   // deliberate red herring; it cancels
                    correct = 2 * outbound * inbound / (outbound + inbound);
                    if ((outbound + inbound) % 2 == 0) trapAnswer = (outbound + inbound) / 2;
                    string rider = PickName();
                    template = $"{rider} rides {miles} miles out at {outbound} mph and returns along the same road at {inbound} mph. Is {rider}'s average speed for the whole journey {{0}} mph?";
                    break;
                }
                default:
                {
                    long x = RNG.Next(200, 900);
                    long y = RNG.Next(20, 90);
                    long d = RNG.Next(3, 13);
                    long product = x * y;
                    long c = RNG.Next(50, 1000);
                    c += ((product - c) % d + d) % d;    // nudge c so the division comes out exact
                    correct = (product - c) / d;
                    template = $"Is ({x} * {y} - {c}) / {d} = {{0}}?";
                    break;
                }
            }

            shownIsCorrect = RNG.Next(0, 2) == 0;
            long shown = correct;

            if (!shownIsCorrect)
            {
                if (trapAnswer is long trap && trap > 0 && trap != correct)
                {
                    // The specific mistake this question invites, which is far crueller than a shift.
                    shown = trap;
                }
                else
                {
                    // Shift by roughly a thousandth so the wrong value keeps the same digit count and
                    // cannot be spotted by shape alone.
                    long magnitude = Math.Max(1, correct / 1000);
                    long delta = RNG.NextInt64(1, magnitude + 1);
                    shown = RNG.Next(0, 2) == 0 ? correct + delta : correct - delta;
                    if (shown < 0 || shown == correct) shown = correct + delta;
                    // Keep the decoy inside the question's valid result range, so an impossible
                    // value cannot give the answer away by shape alone.
                    if (shownMaxValue is long max && shown > max) shown = correct - delta;
                    if (shown < 0) shown = correct == 0 ? 1 : correct - 1;
                }
            }

            return string.Format(template, shown);
        }

        private static long ModPow(long value, int exponent, long modulus)
        {
            long result = 1;
            long b = value % modulus;
            for (int i = 0; i < exponent; i++)
                result = result * b % modulus;
            return result;
        }

        private static long Factorial(int n)
        {
            long result = 1;
            for (int i = 2; i <= n; i++)
                result *= i;
            return result;
        }

        private static long Fibonacci(int n)
        {
            long a = 0, b = 1;
            for (int i = 0; i < n; i++)
                (a, b) = (b, a + b);
            return a;
        }

        private static long Binomial(int n, int k)
        {
            if (k > n - k) k = n - k;
            long result = 1;
            // Each step holds an exact binomial coefficient, so the division never truncates.
            for (int i = 0; i < k; i++)
                result = result * (n - i) / (i + 1);
            return result;
        }

        private static long SumOfPrimesBelow(int limit)
        {
            long sum = 0;
            for (int candidate = 2; candidate < limit; candidate++)
            {
                bool isPrime = true;
                for (int divisor = 2; divisor * divisor <= candidate; divisor++)
                {
                    if (candidate % divisor != 0) continue;
                    isPrime = false;
                    break;
                }
                if (isPrime) sum += candidate;
            }
            return sum;
        }

        private static string Ordinal(int n)
        {
            if (n % 100 is >= 11 and <= 13) return $"{n}th";
            return (n % 10) switch
            {
                1 => $"{n}st",
                2 => $"{n}nd",
                3 => $"{n}rd",
                _ => $"{n}th",
            };
        }

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
