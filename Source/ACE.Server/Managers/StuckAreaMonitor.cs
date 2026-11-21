using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using log4net;

using ACE.Entity.Enum;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Raises a [STUCK AREA] alert when many DIFFERENT players can't finish loading the same landblock: the server had
    /// to force them out of portal space (Player.ForceEndPortalSpaceStuck) because their client never said it had
    /// loaded. That pattern means something in the area is choking clients - 2026-11-20, Qalaba'r (9722): six players
    /// stuck for hours, reported by players before staff knew.
    ///
    /// One player stuck over and over in different places is their own client, not the area; that is not this alert,
    /// and it does not count as several players here.
    ///
    /// Where it goes:
    /// - the log, always ([STUCK AREA] warning);
    /// - Discord (discord_ops_alert_channel, when set): when an area starts, and once when that episode clears;
    /// - in game, the Audit channel (staff only): when an area starts, again every stuck_area_staff_reminder_minutes
    ///   while it is still happening, and once when it clears.
    /// It clears once nobody has been stuck there for stuck_area_clear_quiet_minutes (not when the count dips below
    /// the rule, which would call it over while players are still stuck). A once-a-minute check drives the reminders
    /// and the all-clear, since stuck loads don't come on a schedule. The decisions live in Tracker (no clock, no I/O,
    /// unit tested); this class feeds it and sends what it returns.
    /// </summary>
    public static class StuckAreaMonitor
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

        public readonly struct StuckEvent
        {
            public StuckEvent(DateTime utc, uint characterId, string name)
            {
                Utc = utc;
                CharacterId = characterId;
                Name = name;
            }

            public DateTime Utc { get; }
            public uint CharacterId { get; }
            public string Name { get; }
        }

        public enum CheckOutcome { None, Reminder, Cleared }

        public enum PostKind { Alert, Reminder, Cleared }

        /// <summary>One message to send: always logged; in game when ToStaff; to Discord when ToDiscord.</summary>
        public readonly struct Post
        {
            public Post(PostKind kind, string message, bool toStaff, bool toDiscord)
            {
                Kind = kind;
                Message = message;
                ToStaff = toStaff;
                ToDiscord = toDiscord;
            }

            public PostKind Kind { get; }
            public string Message { get; }
            public bool ToStaff { get; }
            public bool ToDiscord { get; }
        }

        /// <summary>The live settings, read once per call.</summary>
        public readonly struct Settings
        {
            public Settings(int minPlayers, TimeSpan window, TimeSpan discordCooldown, TimeSpan staffReminder, TimeSpan clearQuiet)
            {
                MinPlayers = minPlayers;
                Window = window;
                DiscordCooldown = discordCooldown;
                StaffReminder = staffReminder;
                ClearQuiet = clearQuiet;
            }

            public int MinPlayers { get; }               // 0 = off
            public TimeSpan Window { get; }
            public TimeSpan DiscordCooldown { get; }
            public TimeSpan StaffReminder { get; }        // zero = no in-game posts at all
            public TimeSpan ClearQuiet { get; }

            public static Settings FromServerConfig() => new Settings(
                (int)ServerConfig.stuck_area_min_players.Value,
                TimeSpan.FromMinutes(Math.Max(1, ServerConfig.stuck_area_window_minutes.Value)),
                TimeSpan.FromMinutes(Math.Max(0, ServerConfig.stuck_area_alert_cooldown_minutes.Value)),
                TimeSpan.FromMinutes(Math.Max(0, ServerConfig.stuck_area_staff_reminder_minutes.Value)),
                TimeSpan.FromMinutes(Math.Max(1, ServerConfig.stuck_area_clear_quiet_minutes.Value)));
        }

        // ---------------------------------------------------------------- rule and messages (pure, unit tested)

        /// <summary>
        /// The players stuck in the window ending at nowUtc, most stuck loads first, when there are at least minPlayers
        /// different ones; otherwise null. Drops events older than the window from the list it is given.
        /// </summary>
        public static List<(string Name, int Count)> Evaluate(List<StuckEvent> areaEvents, DateTime nowUtc, TimeSpan window, int minPlayers)
        {
            var players = PlayersInWindow(areaEvents, nowUtc, window);

            if (minPlayers <= 0)
                return null;

            return players.Count >= minPlayers ? players : null;
        }

        /// <summary>Every player stuck in the window, most stuck loads first. Drops events older than the window.</summary>
        public static List<(string Name, int Count)> PlayersInWindow(List<StuckEvent> areaEvents, DateTime nowUtc, TimeSpan window)
        {
            areaEvents.RemoveAll(e => nowUtc - e.Utc > window);

            return areaEvents
                .GroupBy(e => e.CharacterId)
                .Select(g => (Name: g.Last().Name, Count: g.Count()))
                .OrderByDescending(p => p.Count)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// What the once-a-minute check does for an active area. Cleared once nobody has been stuck there for quietPeriod
        /// (not when the player count dips below the rule: on 2026-11-20 that would have said "cleared" at 14:20 with
        /// players still stuck until 14:58). Reminder while it is still happening and the last in-game post is at least
        /// reminderInterval old (zero = no reminders). Else None.
        /// </summary>
        public static CheckOutcome DecideCheck(DateTime lastStuckUtc, TimeSpan quietPeriod, DateTime nowUtc, DateTime lastStaffPostUtc, TimeSpan reminderInterval)
        {
            if (nowUtc - lastStuckUtc >= quietPeriod)
                return CheckOutcome.Cleared;
            if (reminderInterval > TimeSpan.Zero && nowUtc - lastStaffPostUtc >= reminderInterval)
                return CheckOutcome.Reminder;
            return CheckOutcome.None;
        }

        private static string Where(ushort landblock, int? variation)
            => $"landblock {landblock:X4}" + (variation.HasValue && variation.Value != 0 ? $" (variation {variation.Value})" : "");

        /// <summary>The alert text: where, how many players, and each one's stuck loads (the most first). Plain ASCII.</summary>
        public static string BuildAlertMessage(ushort landblock, int? variation, IReadOnlyList<(string Name, int Count)> players, int windowMinutes, bool ongoing = false)
        {
            var list = string.Join(", ", players.Select(p => $"{p.Name} x{p.Count}"));
            return $"[STUCK AREA] {(ongoing ? "Still happening: " : "")}{players.Count} players could not finish loading {Where(landblock, variation)} in the last {windowMinutes} min: {list}. " +
                   "Clients there may be lagging or disconnecting. The [PORTAL STUCK DIAG] log lines show what each was last sent.";
        }

        /// <summary>The all-clear, once nobody has been stuck in the area for the quiet period. Plain ASCII.</summary>
        public static string BuildClearedMessage(ushort landblock, int? variation, int quietMinutes, DateTime lastStuckUtc)
            => $"[OK] [STUCK AREA] {Where(landblock, variation)} has cleared: nobody stuck loading there for {quietMinutes} min (last at {lastStuckUtc:HH:mm} UTC).";

        // ---------------------------------------------------------------- per-area state (no clock, no I/O; unit tested)

        /// <summary>
        /// Every area's recent stuck loads and which areas are in an episode. Not thread-safe on its own: the static
        /// monitor holds one behind a lock. Callers pass the time and settings, so tests can play out a whole episode.
        /// </summary>
        public sealed class Tracker
        {
            private readonly struct AreaKey : IEquatable<AreaKey>
            {
                public AreaKey(ushort landblock, int? variation)
                {
                    Landblock = landblock;
                    Variation = variation;
                }

                public ushort Landblock { get; }
                public int? Variation { get; }

                public bool Equals(AreaKey other) => Landblock == other.Landblock && Variation == other.Variation;
                public override bool Equals(object obj) => obj is AreaKey other && Equals(other);
                public override int GetHashCode() => HashCode.Combine(Landblock, Variation);
            }

            /// <summary>An area that met the rule and has not cleared yet.</summary>
            private sealed class ActiveArea
            {
                public DateTime LastStaffPost;
                public DateTime LastStuck;
                public bool AnnouncedToDiscord;   // its all-clear goes to Discord only if its start did
            }

            private readonly Dictionary<AreaKey, List<StuckEvent>> events = new Dictionary<AreaKey, List<StuckEvent>>();
            // when the last episode Discord was told about cleared: the Discord cooldown runs from there (CodeRabbit #617),
            // so a long episode that clears and comes straight back doesn't post again at once
            private readonly Dictionary<AreaKey, DateTime> lastDiscordClear = new Dictionary<AreaKey, DateTime>();
            private readonly Dictionary<AreaKey, ActiveArea> active = new Dictionary<AreaKey, ActiveArea>();

            public bool HasActiveAreas => active.Count > 0;

            /// <summary>How many stuck loads are kept for an area (tests: history stays within the window).</summary>
            internal int EventCountFor(ushort landblock, int? variation)
                => events.TryGetValue(new AreaKey(landblock, variation), out var list) ? list.Count : 0;

            /// <summary>A player could not finish loading this area. Returns the alert to send when an episode starts.</summary>
            public List<Post> OnStuck(ushort landblock, int? variation, uint characterId, string name, DateTime nowUtc, Settings settings)
            {
                var posts = new List<Post>();
                if (settings.MinPlayers <= 0)
                    return posts;   // off; the next check drops any episode without an all-clear

                var key = new AreaKey(landblock, variation);

                // forget areas nobody has been stuck in for a whole window (active ones are cleared by the check)
                foreach (var stale in events.Where(kv => !active.ContainsKey(kv.Key) && (kv.Value.Count == 0 || nowUtc - kv.Value[^1].Utc > settings.Window)).Select(kv => kv.Key).ToList())
                    events.Remove(stale);

                if (!events.TryGetValue(key, out var list))
                    events[key] = list = new List<StuckEvent>();
                list.Add(new StuckEvent(nowUtc, characterId, name));

                if (active.TryGetValue(key, out var current))
                {
                    current.LastStuck = nowUtc;   // still happening: the check posts the reminders and the all-clear
                    PlayersInWindow(list, nowUtc, settings.Window);   // keep only the window, however long the episode
                    return posts;
                }

                var players = Evaluate(list, nowUtc, settings.Window, settings.MinPlayers);
                if (players == null)
                    return posts;

                // an area that clears and comes straight back still tells staff in game, but Discord only once the cooldown
                // has run since the last episode it announced cleared
                var toDiscord = !lastDiscordClear.TryGetValue(key, out var lastClear) || nowUtc - lastClear >= settings.DiscordCooldown;

                active[key] = new ActiveArea { LastStaffPost = nowUtc, LastStuck = nowUtc, AnnouncedToDiscord = toDiscord };

                posts.Add(new Post(PostKind.Alert, BuildAlertMessage(landblock, variation, players, (int)settings.Window.TotalMinutes),
                    settings.StaffReminder > TimeSpan.Zero, toDiscord));
                return posts;
            }

            /// <summary>The once-a-minute check: reminders for areas still happening, all-clears for areas that went quiet.</summary>
            public List<Post> OnCheck(DateTime nowUtc, Settings settings)
            {
                var posts = new List<Post>();

                // switched off mid-episode: stop quietly. An all-clear here would claim the area is fine while it may not be.
                if (settings.MinPlayers <= 0)
                {
                    active.Clear();
                    return posts;
                }

                var toStaff = settings.StaffReminder > TimeSpan.Zero;
                foreach (var (key, area) in active.ToList())
                {
                    var list = events.TryGetValue(key, out var l) ? l : new List<StuckEvent>();

                    // prune every check, whatever happens next: with reminders off nothing else would (CodeRabbit #617).
                    // LastStuck is kept apart, so pruning never changes when the area clears.
                    var players = PlayersInWindow(list, nowUtc, settings.Window);

                    switch (DecideCheck(area.LastStuck, settings.ClearQuiet, nowUtc, area.LastStaffPost, settings.StaffReminder))
                    {
                        case CheckOutcome.Reminder:
                            if (players.Count == 0)
                                break;   // quiet for a whole window but not yet the quiet period: nothing to list
                            area.LastStaffPost = nowUtc;
                            posts.Add(new Post(PostKind.Reminder,
                                BuildAlertMessage(key.Landblock, key.Variation, players, (int)settings.Window.TotalMinutes, ongoing: true),
                                toStaff, toDiscord: false));
                            break;

                        case CheckOutcome.Cleared:
                            active.Remove(key);
                            if (area.AnnouncedToDiscord)
                                lastDiscordClear[key] = nowUtc;
                            if (list.Count == 0)
                                events.Remove(key);
                            posts.Add(new Post(PostKind.Cleared,
                                BuildClearedMessage(key.Landblock, key.Variation, (int)settings.ClearQuiet.TotalMinutes, area.LastStuck),
                                toStaff, area.AnnouncedToDiscord));
                            break;
                    }
                }
                return posts;
            }
        }

        // ---------------------------------------------------------------- hooks and sending

        private static readonly object sync = new object();
        private static readonly Tracker tracker = new Tracker();
        private static Timer checkTimer;

        /// <summary>Call when the server forces a player out of portal space because their load never finished.</summary>
        public static void Record(Player player)
        {
            try
            {
                var landblock = player?.CurrentLandblock;
                if (landblock == null)
                    return;

                // base world is null or 0 depending on the path that loaded it: one key for both
                var variation = VariationManager.NormalizeBase(landblock.VariationId);

                List<Post> posts;
                lock (sync)
                    posts = tracker.OnStuck(landblock.Id.Landblock, variation, player.Guid.Full, player.Name, DateTime.UtcNow, Settings.FromServerConfig());

                if (posts.Count > 0)
                    EnsureCheckTimer();
                Send(posts);
            }
            catch (Exception ex)
            {
                log.Error($"[STUCK AREA] monitor failed: {ex.Message}");
            }
        }

        private static void EnsureCheckTimer()
        {
            if (Volatile.Read(ref checkTimer) != null)
                return;
            var timer = new Timer(_ => Check(), null, CheckInterval, CheckInterval);
            if (Interlocked.CompareExchange(ref checkTimer, timer, null) != null)
                timer.Dispose();
        }

        private static void Check()
        {
            try
            {
                List<Post> posts;
                lock (sync)
                    posts = tracker.OnCheck(DateTime.UtcNow, Settings.FromServerConfig());
                Send(posts);
            }
            catch (Exception ex)
            {
                log.Error($"[STUCK AREA] check failed: {ex.Message}");
            }
        }

        private static void Send(List<Post> posts)
        {
            foreach (var post in posts)
            {
                log.Warn(post.Message);
                if (post.ToStaff)
                    PostToStaff(post.Message);
                if (post.ToDiscord)
                    PostToDiscord(post.Message);
            }
        }

        /// <summary>
        /// The Audit channel in game, as CONSOLE: every logged-in staff member with the channel on sees it. Not through
        /// BroadcastToAuditChannel, which also copies to the admin audit Discord channel (the ops channel already has it).
        /// </summary>
        private static void PostToStaff(string message)
        {
            try
            {
                PlayerManager.BroadcastToChannelFromConsole(Channel.Audit, message);
            }
            catch (Exception ex)
            {
                log.Error($"[STUCK AREA] in-game post failed: {ex.Message}");
            }
        }

        private static void PostToDiscord(string message)
        {
            var channel = ServerConfig.discord_ops_alert_channel.Value;
            if (channel <= 0)
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await DiscordChatManager.SendDiscordChannelMessageAsync(message, channel);
                }
                catch (Exception ex)
                {
                    log.Error($"[STUCK AREA] Discord post failed: {ex.Message}");
                }
            });
        }
    }
}
