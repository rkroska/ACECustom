using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Present Bot (owner 2026-10-01): a test-shard tool that makes the admin's own character spam-open Mine, Mine, Mine!
    /// presents the way players do, through the REAL use path (Player.HandleActionUseItem - the same call a click sends,
    /// no keystrokes), so the busy hold, the cooldown and the prize roll all run exactly as in game.
    /// It counts actual prize ROLLS by watching the player's cooldown quest solve count (each roll stamps it once), and
    /// flags any 10 s window holding more than one roll - the event is meant to allow at most one roll per 10 s.
    /// Modes: same = hammer one present until it is gone (what players do); nearest = cycle every present in reach.
    /// The character is made unkillable for the run (restored after) so a death roll does not end the test.
    /// Gated by present_bot_enabled (default off).
    /// </summary>
    public static class PresentBot
    {
        public const uint PresentWcid = 777706000;
        public const string EventName = "mineminemine";
        public const string CooldownQuest = "mineminemine_cooldown";

        private const double TickSeconds = 0.25;      // owner 10-01: 4 uses a second
        private const double ReportSeconds = 10;
        // the cooldown is 10 s, but quest times are whole unix seconds, so two LEGIT rolls can land ~9.1 s apart;
        // closer than 9 s = the bug
        private const double MinGapSeconds = 9;
        private const float SearchRange = 40f;         // same mode: how far to look for the next present
        private const float NearbyRange = 6f;          // nearest mode: presents cycled through

        private class Run
        {
            public Player Player;
            public string Mode;
            public bool StopRequested;
            public string StopReason;

            public uint TargetGuid;
            public int CycleIdx;

            public DateTime Started = DateTime.UtcNow;
            public DateTime LastReport = DateTime.UtcNow;
            public DateTime? EventOnAt;
            public bool EventWasOn;

            public int Uses, Rolls, WindowUses, WindowRolls, MaxRollsPer10s, BadWindows, Events, EventRolls;
            public int LastSolves;
            public readonly Queue<DateTime> RollTimes = new Queue<DateTime>();

            public bool SavedUnkillable;
        }

        private static readonly ConcurrentDictionary<uint, Run> Runs = new ConcurrentDictionary<uint, Run>();

        public static bool IsRunning(Player player) => player != null && Runs.ContainsKey(player.Guid.Full);

        public static void Start(Player player, string mode)
        {
            if (player == null) return;
            if (IsRunning(player))
            {
                Say(player, "already running - /presentbot stop first.");
                return;
            }

            var run = new Run
            {
                Player = player,
                Mode = mode,
                LastSolves = Solves(player),
                SavedUnkillable = player.IsUnkillable,
            };
            player.IsUnkillable = true;
            Runs[player.Guid.Full] = run;

            Say(player, $"started, mode {mode}, {1 / TickSeconds:0} uses a second. Event '{EventName}' is " +
                        $"{(EventOn() ? "ON" : "off - waiting for it")}. You are unkillable until /presentbot stop.");
            Schedule(run, 0);
        }

        public static void Stop(Player player, string reason)
        {
            if (player == null || !Runs.TryGetValue(player.Guid.Full, out var run)) return;
            lock (run) { run.StopRequested = true; run.StopReason = reason; }
        }

        public static void Status(Player player)
        {
            if (player == null) return;
            if (!Runs.TryGetValue(player.Guid.Full, out var run))
            {
                Say(player, $"not running. Event '{EventName}' is {(EventOn() ? "ON" : "off")}. /presentbot start [same|nearest]");
                return;
            }
            lock (run) Say(player, Summary(run, "status"));
        }

        // =========================================================================================================
        // Math mode (owner 10-01): are the ODDS right? One present is built in memory (never enters the world) and its
        // own EmoteManager.GetEmoteSet - the exact pick a click makes - is asked N times. Each pick is grouped by what it
        // pays, and set against what the database thresholds say it should pay. No cooldown, no rewards, no event needed.
        // =========================================================================================================

        public const int MathMaxSamples = 200_000;

        public static void RunMath(Player player, int samples)
        {
            if (player == null) return;
            samples = System.Math.Clamp(samples, 1_000, MathMaxSamples);

            var present = Factories.WorldObjectFactory.CreateNewWorldObject(PresentWcid);
            if (present == null)
            {
                Say(player, $"math: weenie {PresentWcid} not found - is the event imported? (/clearcache after an import)");
                return;
            }

            try
            {
                var emotes = present.Biota.PropertiesEmote ?? new List<ACE.Entity.Models.PropertiesEmote>();

                // the prize sets sit on whichever cooldown branch holds MORE than one set: QuestSuccess since the
                // UpdateQuest fix (10-01), QuestFailure in the older InqQuest build that live ran
                bool IsPrizeOn(EmoteCategory c) => emotes.Count(e => e.Category == c && e.Quest == CooldownQuest) > 1;
                var category = IsPrizeOn(EmoteCategory.QuestSuccess) ? EmoteCategory.QuestSuccess
                             : IsPrizeOn(EmoteCategory.QuestFailure) ? EmoteCategory.QuestFailure
                             : (EmoteCategory?)null;
                if (category == null)
                {
                    Say(player, "math: no prize sets found on the present (no cooldown branch with more than one set).");
                    return;
                }

                var prizeSets = emotes.Where(e => e.Category == category && e.Quest == CooldownQuest).OrderBy(e => e.Probability).ToList();

                // what the DATABASE says: each set's share is its threshold minus the one below it
                var expected = new Dictionary<string, double>();
                var prev = 0.0;
                foreach (var set in prizeSets)
                {
                    var key = Key(set);
                    expected[key] = (expected.TryGetValue(key, out var v) ? v : 0) + (set.Probability - prev);
                    prev = set.Probability;
                }

                // the 84 Wicked Wares items (1% split 84 ways) read as ONE line: any single-item key below 0.05% joins it
                const string Wicked = "a Wicked Wares item (any of them)";
                var bucket = expected.Keys.ToDictionary(k => k, k => k.StartsWith("item ") && expected[k] < 0.0005 ? Wicked : k);
                expected = expected.GroupBy(kv => bucket[kv.Key]).ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

                // what the SERVER picks: the real GetEmoteSet, N times
                var observed = new Dictionary<string, int>();
                var none = 0;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                for (var i = 0; i < samples; i++)
                {
                    var pick = present.EmoteManager.GetEmoteSet(category.Value, CooldownQuest);
                    if (pick == null) { none++; continue; }
                    var key = Key(pick);
                    if (bucket.TryGetValue(key, out var b)) key = b;
                    observed[key] = (observed.TryGetValue(key, out var n) ? n : 0) + 1;
                }
                sw.Stop();

                Say(player, $"math: {samples:N0} picks on {category} '{CooldownQuest}' ({prizeSets.Count} prize sets, top threshold {prizeSets.Last().Probability}) in {sw.ElapsedMilliseconds} ms. observed vs DATABASE:");
                foreach (var kv in expected.OrderByDescending(kv => kv.Value))
                {
                    var got = observed.TryGetValue(kv.Key, out var c) ? c : 0;
                    var exp = kv.Value * samples;
                    var oneIn = got > 0 ? $" (1 in {samples / (double)got:N0})" : "";
                    Say(player, $"  {kv.Key}: {got:N0}{oneIn} - database says {exp:N0} ({kv.Value * 100:0.###}%)");
                }
                foreach (var kv in observed.Where(kv => !expected.ContainsKey(kv.Key)))
                    Say(player, $"  !! {kv.Key}: {kv.Value:N0} - NOT in the database table");
                if (none > 0)
                    Say(player, $"  !! {none:N0} picks returned NOTHING (a roll above the top threshold)");
            }
            finally
            {
                present.Destroy();
            }
        }

        // =========================================================================================================
        // Crowd mode (owner 10-01: "30 players spamming presents"). N hidden stand-ins - in-memory creatures built from
        // the present weenie (no AI, not attackable), each with ITS OWN cooldown quest - use presents through the
        // present's own click path (EmoteManager.OnUse, what OnActivate runs once the player-side checks pass), 4 a
        // second each, spamming one present until it is gone like players do. The crowd runs back-to-back events: start,
        // time the real jackpot, wait for the field to clear, start again. Harsher than real players (no walking, no
        // "too busy"), and gifts are skipped (Give needs a player) - the jackpot still really stops the event.
        // =========================================================================================================

        public const int CrowdDefaultBots = 40, CrowdDefaultEvents = 20;   // owner 10-01: 40 stand-ins
        private const int JackpotOneIn = 1500;                              // owner 10-01: the jackpot odds the summary quotes
        private const double NoWinnerSeconds = 595;                         // the 10-minute no-winner timer, minus a margin
        private const double DeathReturnSeconds = 15;        // a killed stand-in comes back like a player recalling in
        private const double CrowdRestSeconds = 15;            // between events: the field despawns, then a fresh start
        private const double CrowdEventTimeoutSeconds = 1200;  // a 20-minute event is stopped and counted as a timeout
        private const ushort EventLandblock = 0x016C;
        private const int EventVariation = 5;

        private class Crowd
        {
            public Player Owner;
            public readonly List<Creature> Bots = new List<Creature>();
            public readonly HashSet<uint> BotGuids = new HashSet<uint>();
            public readonly Dictionary<uint, uint> Target = new Dictionary<uint, uint>();
            public readonly Dictionary<uint, int> LastSolves = new Dictionary<uint, int>();
            public readonly Dictionary<uint, Queue<DateTime>> RollTimes = new Dictionary<uint, Queue<DateTime>>();
            public readonly Random Rng = new Random();

            public int EventsWanted, EventsDone, Timeouts;
            public bool Running;                      // an event the crowd started is on
            public DateTime RestUntil = DateTime.MinValue;
            public DateTime EventStart;
            public DateTime? PresentsAt;
            public DateTime LastReport = DateTime.UtcNow;

            public long Uses, Rolls, EventRolls, BadRolls, Deaths;
            public ACE.Entity.Position Spot;                                          // where stand-ins stand (the owner's spot at start)
            public readonly Dictionary<int, DateTime> DeadSince = new Dictionary<int, DateTime>();  // bot index -> when it died
            public double BotSeconds;                 // bot-seconds spent with presents on the ground
            public readonly List<(double fromPresents, double fromStart, long rolls)> Results = new List<(double, double, long)>();

            public bool StopRequested;
            public string StopReason;
            public bool SavedUnkillable;
        }

        private static readonly ConcurrentDictionary<uint, Crowd> Crowds = new ConcurrentDictionary<uint, Crowd>();

        public static bool IsCrowdRunning(Player player) => player != null && Crowds.ContainsKey(player.Guid.Full);

        public static void StartCrowd(Player player, int bots, int events)
        {
            if (player == null) return;
            if (IsCrowdRunning(player) || IsRunning(player))
            {
                Say(player, "crowd: a bot run is already going - /presentbot stop first.");
                return;
            }
            var loc = player.Location;
            if (loc == null || loc.LandblockId.Landblock != EventLandblock || loc.Variation != EventVariation)
            {
                Say(player, $"crowd: stand in Marketplace v{EventVariation} (landblock 0x{EventLandblock:X4}) - the presents only spawn while it is loaded.");
                return;
            }
            if (EventOn())
            {
                Say(player, $"crowd: '{EventName}' is already ON - /event stop {EventName} first, so every event is timed from its start.");
                return;
            }

            bots = System.Math.Clamp(bots, 1, 100);
            events = System.Math.Clamp(events, 1, 100);
            var crowd = new Crowd { Owner = player, EventsWanted = events };

            crowd.Spot = new ACE.Entity.Position(loc);
            for (var i = 0; i < bots; i++)
            {
                var bot = SpawnBot(crowd, i);
                if (bot == null)
                {
                    Say(player, $"crowd: could not place stand-in {i + 1} (weenie {PresentWcid}).");
                    break;
                }
                crowd.Bots.Add(bot);
            }

            if (crowd.Bots.Count == 0)
            {
                Say(player, "crowd: no stand-ins could be placed.");
                return;
            }

            // the death prize is a spell PROJECTILE fired at a stand-in - and the stand-ins stand on you
            crowd.SavedUnkillable = player.IsUnkillable;
            player.IsUnkillable = true;
            Crowds[player.Guid.Full] = crowd;
            Say(player, $"crowd: {crowd.Bots.Count} stand-ins, {1 / TickSeconds:0} uses a second each, {events} event(s). " +
                        $"Stay in Marketplace v{EventVariation}. /presentbot stop ends it.");
            ScheduleCrowd(crowd, 0);
        }

        /// <summary>One stand-in at the crowd's spot: hidden, no corpse, its own fresh cooldown quest.</summary>
        private static Creature SpawnBot(Crowd crowd, int index)
        {
            if (!(Factories.WorldObjectFactory.CreateNewWorldObject(PresentWcid) is Creature bot))
                return null;
            bot.Name = $"Crowd Bot {index + 1:00}";
            bot.Visibility = true;                     // never sent to clients - nobody can see or click a stand-in
            bot.NoCorpse = true;                       // the death prize kills stand-ins - leave no corpses on the owner's spot
            bot.Location = new ACE.Entity.Position(crowd.Spot);
            if (!bot.EnterWorld())
            {
                bot.Destroy();
                return null;
            }
            crowd.BotGuids.Add(bot.Guid.Full);
            crowd.LastSolves[bot.Guid.Full] = 0;
            crowd.RollTimes[bot.Guid.Full] = new Queue<DateTime>();
            return bot;
        }

        public static void StopCrowd(Player player, string reason)
        {
            if (player == null || !Crowds.TryGetValue(player.Guid.Full, out var crowd)) return;
            lock (crowd) { crowd.StopRequested = true; crowd.StopReason = reason; }
        }

        private static void ScheduleCrowd(Crowd crowd, double delay)
        {
            var chain = new ActionChain();
            chain.AddDelaySeconds(delay);
            chain.AddAction(crowd.Owner, ActionType.PresentBot_Tick, () => CrowdTick(crowd));
            chain.EnqueueChain();
        }

        private static void CrowdTick(Crowd crowd)
        {
            var owner = crowd.Owner;
            lock (crowd)
            {
                if (owner == null || owner.IsDestroyed || owner.Session == null) { FinishCrowd(crowd, "owner gone"); return; }
                if (crowd.StopRequested) { FinishCrowd(crowd, crowd.StopReason ?? "stopped"); return; }

                var lb = owner.CurrentLandblock;
                if (lb == null || lb.Id.Landblock != EventLandblock || lb.VariationId != EventVariation)
                {
                    FinishCrowd(crowd, $"you left Marketplace v{EventVariation}");
                    return;
                }

                var now = DateTime.UtcNow;
                var on = EventOn();

                // 0. the death prize KILLS a stand-in (creatures can't be made immune - SpellProjectile checks Invincible
                //    on players only). Like a real player recalling back: it returns 15 s later, as a fresh stand-in.
                for (var i = 0; i < crowd.Bots.Count; i++)
                {
                    var b = crowd.Bots[i];
                    if (b != null && !b.IsDead && !b.IsDestroyed) continue;
                    if (!crowd.DeadSince.ContainsKey(i))
                    {
                        crowd.DeadSince[i] = now;
                        crowd.Deaths++;
                    }
                    else if ((now - crowd.DeadSince[i]).TotalSeconds >= DeathReturnSeconds)
                    {
                        if (b != null && !b.IsDestroyed) b.Destroy();
                        var fresh = SpawnBot(crowd, i);
                        if (fresh != null)
                        {
                            crowd.Bots[i] = fresh;
                            crowd.DeadSince.Remove(i);
                        }
                    }
                }

                // 1. rolls per stand-in (each roll stamps that stand-in's own cooldown quest once)
                foreach (var bot in crowd.Bots)
                {
                    if (bot == null || bot.IsDestroyed || bot.IsDead) continue;
                    var g = bot.Guid.Full;
                    var q = crowd.RollTimes[g];
                    while (q.Count > 0 && (now - q.Peek()).TotalSeconds >= MinGapSeconds) q.Dequeue();

                    var solves = bot.QuestManager?.GetQuest(CooldownQuest)?.NumTimesCompleted ?? 0;
                    var fresh = System.Math.Max(0, solves - crowd.LastSolves[g]);
                    crowd.LastSolves[g] = solves;
                    for (var i = 0; i < fresh; i++)
                    {
                        q.Enqueue(now);
                        crowd.Rolls++;
                        crowd.EventRolls++;
                        if (q.Count > 1) crowd.BadRolls++;
                    }
                }

                // 2. the event cycle
                if (crowd.Running && !on)
                {
                    // the jackpot (or someone) stopped it
                    var fromStart = (now - crowd.EventStart).TotalSeconds;
                    var fromPresents = crowd.PresentsAt.HasValue ? (now - crowd.PresentsAt.Value).TotalSeconds : 0;
                    crowd.Results.Add((fromPresents, fromStart, crowd.EventRolls));
                    crowd.EventsDone++;
                    crowd.Running = false;
                    crowd.RestUntil = now.AddSeconds(CrowdRestSeconds);
                    Say(owner, $"crowd: event {crowd.EventsDone}/{crowd.EventsWanted} ENDED {fromPresents:0} s after the presents appeared " +
                               $"({fromStart:0} s after the start), {crowd.EventRolls:N0} rolls.");
                }
                else if (crowd.Running && (now - crowd.EventStart).TotalSeconds > CrowdEventTimeoutSeconds)
                {
                    crowd.Timeouts++;
                    Say(owner, $"crowd: event {crowd.EventsDone + 1} hit the {CrowdEventTimeoutSeconds / 60:0}-minute cap with {crowd.EventRolls:N0} rolls - stopping it.");
                    EventManager.StopEvent(EventName, null, null);
                    crowd.Running = false;
                    crowd.EventsDone++;
                    crowd.RestUntil = now.AddSeconds(CrowdRestSeconds);
                }

                if (!crowd.Running)
                {
                    if (crowd.EventsDone >= crowd.EventsWanted) { FinishCrowd(crowd, "all events done"); return; }
                    if (now >= crowd.RestUntil && !on)
                    {
                        EventManager.StartEvent(EventName, null, null);
                        crowd.Running = true;
                        crowd.EventStart = now;
                        crowd.PresentsAt = null;
                        crowd.EventRolls = 0;
                        crowd.Target.Clear();
                    }
                }
                else
                {
                    // 3. every stand-in uses its present (a fresh random one once its present is gone)
                    var presents = lb.GetWorldObjectsForDiagnostics()
                        .Where(wo => wo != null && !wo.IsDestroyed && wo.WeenieClassId == PresentWcid && !crowd.BotGuids.Contains(wo.Guid.Full))
                        .ToList();
                    if (presents.Count > 0)
                    {
                        crowd.PresentsAt ??= now;
                        crowd.BotSeconds += crowd.Bots.Count * TickSeconds;
                        var byGuid = presents.ToDictionary(p => p.Guid.Full);
                        foreach (var bot in crowd.Bots)
                        {
                            if (bot == null || bot.IsDestroyed || bot.IsDead) continue;
                            if (!crowd.Target.TryGetValue(bot.Guid.Full, out var tg) || !byGuid.TryGetValue(tg, out var present))
                            {
                                present = presents[crowd.Rng.Next(presents.Count)];
                                crowd.Target[bot.Guid.Full] = present.Guid.Full;
                            }
                            crowd.Uses++;
                            present.EmoteManager.OnUse(bot);
                        }
                    }
                }

                if ((now - crowd.LastReport).TotalSeconds >= 30)
                {
                    crowd.LastReport = now;
                    if (crowd.Running)
                        Say(owner, $"crowd: event {crowd.EventsDone + 1} running {(now - crowd.EventStart).TotalSeconds:0} s, {crowd.EventRolls:N0} rolls so far, " +
                                   $"{crowd.DeadSince.Count} stand-in(s) dead right now ({DeathReturnSeconds:0} s to return)" +
                                   (crowd.BadRolls > 0 ? $" - !! {crowd.BadRolls} extra roll(s) inside {MinGapSeconds:0} s so far" : "") + ".");
                }
            }

            ScheduleCrowd(crowd, TickSeconds);
        }

        private static void FinishCrowd(Crowd crowd, string reason)
        {
            Crowds.TryRemove(crowd.Owner?.Guid.Full ?? 0, out _);

            if (crowd.Running && EventOn())
                EventManager.StopEvent(EventName, null, null);

            foreach (var bot in crowd.Bots)
                if (bot != null && !bot.IsDestroyed) bot.Destroy();

            var owner = crowd.Owner;
            if (owner == null || owner.IsDestroyed) return;
            owner.IsUnkillable = crowd.SavedUnkillable;

            // an event that ran to the 10-minute no-winner timer ended WITHOUT a jackpot (owner 10-01): the timer fires
            // 600 s after the presents start, so anything at or past ~595 s after they appeared is that ending
            var ended = crowd.Results.Where(r => r.rolls > 0).ToList();
            var jackpots = ended.Where(r => r.fromPresents < NoWinnerSeconds).ToList();
            var noWinner = ended.Count - jackpots.Count;
            Say(owner, $"crowd stopped ({reason}): {crowd.Bots.Count} stand-ins, {ended.Count} event(s): {jackpots.Count} won by the jackpot, " +
                       $"{noWinner} ran out the 10 minutes (no winner), {crowd.Timeouts} timeout(s), {crowd.Deaths:N0} stand-in deaths.");
            if (ended.Count > 0)
            {
                var secs = ended.Select(r => r.fromPresents).OrderBy(x => x).ToList();
                var median = secs[secs.Count / 2];
                Say(owner, $"  event length after the presents appeared: median {median:0} s, average {secs.Average():0} s, " +
                           $"shortest {secs.First():0} s, longest {secs.Last():0} s.");
                Say(owner, $"  each event: {string.Join(", ", ended.Select(r => $"{r.fromPresents:0}s/{r.rolls}r{(r.fromPresents >= NoWinnerSeconds ? "(no winner)" : "")}"))}");
                // rolls per jackpot over ALL rolls: a no-winner event's rolls still count toward finding one
                var perJackpot = jackpots.Count > 0 ? ended.Sum(r => (double)r.rolls) / jackpots.Count : 0;
                Say(owner, jackpots.Count > 0
                    ? $"  rolls per jackpot: {perJackpot:0} (the odds say {JackpotOneIn:N0} at 1 in {JackpotOneIn:N0}; a no-winner event's rolls count too)."
                    : $"  no jackpot at all across {ended.Sum(r => r.rolls):N0} rolls (the odds say 1 in {JackpotOneIn:N0}).");
            }
            var perBot10s = crowd.BotSeconds > 0 ? crowd.Rolls / crowd.BotSeconds * 10 : 0;
            Say(owner, $"  {crowd.Uses:N0} uses, {crowd.Rolls:N0} rolls = {perBot10s:0.00} roll per stand-in per 10 s (the cooldown allows at most ~1). " +
                       (crowd.BadRolls == 0 ? "Cooldown HELD for every stand-in." : $"!! {crowd.BadRolls} extra roll(s) slipped inside {MinGapSeconds:0} s."));
        }

        /// <summary>Groups a prize set by what it does: JACKPOT / DEATH / the coins, Luminance and items it hands out.</summary>
        private static string Key(ACE.Entity.Models.PropertiesEmote set)
        {
            var acts = set.PropertiesEmoteAction ?? new List<ACE.Entity.Models.PropertiesEmoteAction>();
            if (acts.Any(a => a.Type == (uint)EmoteType.StopEvent)) return "JACKPOT";
            if (acts.Any(a => a.Type == (uint)EmoteType.CastSpellInstant)) return "death";

            var parts = new List<string>();
            foreach (var a in acts)
            {
                if (a.Type == (uint)EmoteType.Give && a.WeenieClassId.HasValue)
                {
                    var amount = a.StackSize ?? 1;
                    parts.Add(a.WeenieClassId == 300004 ? $"{amount} coins" : $"item {a.WeenieClassId} x{System.Math.Max(1, amount)}");
                }
                else if (a.Type == (uint)EmoteType.IncrementInt64Stat && a.Amount64.HasValue)
                    parts.Add($"{a.Amount64.Value / 1_000_000_000}B lum");
            }
            if (parts.Count == 0) return "nothing";
            // the 84 Wicked Wares items and the other one-off items read as one line each - keep coins/lum separate
            return string.Join(" + ", parts);
        }

        private static void Schedule(Run run, double delay)
        {
            var chain = new ActionChain();
            chain.AddDelaySeconds(delay);
            chain.AddAction(run.Player, ActionType.PresentBot_Tick, () => Tick(run));
            chain.EnqueueChain();
        }

        private static void Tick(Run run)
        {
            var player = run.Player;
            if (player == null || player.IsDestroyed || player.Session == null)
            {
                Finish(run, "player gone");
                return;
            }

            lock (run)
            {
                if (run.StopRequested)
                {
                    Finish(run, run.StopReason ?? "stopped");
                    return;
                }

                var now = DateTime.UtcNow;

                // 1. count rolls: every prize roll stamps the cooldown quest once
                while (run.RollTimes.Count > 0 && (now - run.RollTimes.Peek()).TotalSeconds >= MinGapSeconds)
                    run.RollTimes.Dequeue();

                var solves = Solves(player);
                var newRolls = Math.Max(0, solves - run.LastSolves);
                run.LastSolves = solves;
                if (newRolls > 0)
                {
                    run.Rolls += newRolls;
                    run.WindowRolls += newRolls;
                    run.EventRolls += newRolls;
                    for (var i = 0; i < newRolls; i++)
                        run.RollTimes.Enqueue(now);

                    // 2. the rule under test: no two rolls closer than MinGapSeconds - flagged once per offending roll
                    if (run.RollTimes.Count > run.MaxRollsPer10s)
                        run.MaxRollsPer10s = run.RollTimes.Count;
                    if (run.RollTimes.Count > 1)
                    {
                        run.BadWindows++;
                        Say(player, $"!! {run.RollTimes.Count} rolls inside {MinGapSeconds:0} s - the cooldown let extra rolls through.");
                    }
                }

                // 3. the event: note when it starts and ends (a jackpot, or a manual stop)
                var on = EventOn();
                if (on && !run.EventWasOn)
                {
                    run.EventOnAt = now;
                    run.EventRolls = 0;
                    run.Events++;
                    Say(player, $"event #{run.Events} is ON - opening presents.");
                }
                else if (!on && run.EventWasOn)
                {
                    var secs = run.EventOnAt.HasValue ? (now - run.EventOnAt.Value).TotalSeconds : 0;
                    Say(player, $"event #{run.Events} ENDED after {secs:0} s (from when the event switched on; presents need ~10 s to appear). " +
                                $"This character rolled {run.EventRolls} time(s) in it.");
                }
                run.EventWasOn = on;

                // 4. use a present, exactly as a click would
                if (on)
                {
                    var target = PickTarget(run);
                    if (target != null)
                    {
                        run.Uses++;
                        run.WindowUses++;
                        player.HandleActionUseItem(target.Guid.Full);
                    }
                }

                // 5. every 10 s: what happened
                if ((now - run.LastReport).TotalSeconds >= ReportSeconds)
                {
                    Say(player, $"last {ReportSeconds:0} s: {run.WindowUses} uses sent, {run.WindowRolls} roll(s). " +
                                $"Totals: {run.Uses} uses, {run.Rolls} rolls, most rolls inside {MinGapSeconds:0} s = {run.MaxRollsPer10s}.");
                    run.WindowUses = 0;
                    run.WindowRolls = 0;
                    run.LastReport = now;
                }
            }

            Schedule(run, TickSeconds);
        }

        private static WorldObject PickTarget(Run run)
        {
            var player = run.Player;
            var lb = player.CurrentLandblock;
            if (lb == null) return null;

            if (run.Mode == "same" && run.TargetGuid != 0)
            {
                var cur = lb.GetObject(run.TargetGuid);
                if (cur != null && !cur.IsDestroyed && cur.WeenieClassId == PresentWcid)
                    return cur;
            }

            var presents = lb.GetWorldObjectsForDiagnostics()
                .Where(wo => wo != null && !wo.IsDestroyed && wo.WeenieClassId == PresentWcid && wo.PhysicsObj != null)
                .Select(wo => (wo, dist: player.GetCylinderDistance(wo)))
                .Where(x => x.dist <= (run.Mode == "nearest" ? NearbyRange : SearchRange))
                .OrderBy(x => x.dist)
                .Select(x => x.wo)
                .ToList();

            if (presents.Count == 0) return null;

            if (run.Mode == "nearest")
                return presents[run.CycleIdx++ % presents.Count];

            run.TargetGuid = presents[0].Guid.Full;
            return presents[0];
        }

        private static void Finish(Run run, string reason)
        {
            Runs.TryRemove(run.Player?.Guid.Full ?? 0, out _);
            if (run.Player != null && !run.Player.IsDestroyed)
            {
                run.Player.IsUnkillable = run.SavedUnkillable;
                Say(run.Player, Summary(run, $"stopped ({reason})"));
            }
        }

        private static string Summary(Run run, string head)
        {
            var mins = (DateTime.UtcNow - run.Started).TotalMinutes;
            var verdict = run.BadWindows == 0
                ? $"cooldown HELD: never two rolls inside {MinGapSeconds:0} s."
                : $"cooldown BROKEN: {run.BadWindows} extra roll(s) inside {MinGapSeconds:0} s (max {run.MaxRollsPer10s} at once).";
            return $"{head}: {mins:0.0} min, mode {run.Mode}, {run.Uses} uses, {run.Rolls} rolls, {run.Events} event start(s). {verdict}";
        }

        private static int Solves(Player player) => player?.QuestManager?.GetQuest(CooldownQuest)?.NumTimesCompleted ?? 0;

        private static bool EventOn() => EventManager.IsEventStarted(EventName, null, null);

        private static void Say(Player player, string text)
        {
            player?.Session?.Network.EnqueueSend(new GameMessageSystemChat("[PresentBot] " + text, ChatMessageType.Broadcast));
        }
    }
}
