using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using log4net;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Posts server status to a Discord channel (discord_server_status_channel) so players can tell a planned
    /// restart from a crash: online, restart scheduled / cancelled, offline (planned, or stopped on purpose),
    /// and back online after a crash.
    ///
    /// A crash can't be posted when it happens, because the process is gone. Instead a small state file next to
    /// the server says "running" (refreshed every minute) until a planned or deliberate stop overwrites it, so
    /// the next start knows whether the previous run crashed and when it was last seen alive.
    /// </summary>
    public static class ServerStatusAnnouncer
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public const string StateRunning = "running";
        public const string StateClean = "clean";      // planned shutdown finished
        public const string StateStopped = "stopped";  // stopped on purpose without the countdown (SIGTERM, watchdog)

        private static readonly string StateFile = Path.Combine(AppContext.BaseDirectory, "server_status.state");
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan StartupDiscordWait = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan ShutdownSendWait = TimeSpan.FromSeconds(5);

        private static Timer heartbeat;
        private static int online;            // set once AnnounceOnline ran
        private static int offlineAnnounced;

        private static long ChannelId => ServerConfig.discord_server_status_channel.Value;

        // ---------------------------------------------------------------- messages (pure, unit tested)

        /// <summary>Discord timestamp: shows in each reader's own time zone, plus "x minutes ago".</summary>
        private static string When(DateTime utc)
        {
            var unix = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
            return $"<t:{unix}:t> (<t:{unix}:R>)";
        }

        public static string BuildOnlineMessage(string previousState, DateTime? previousUtc, bool worldOpen)
        {
            string message;
            if (previousState == StateRunning)
                message = "\U0001F7E2 **Server is back online** after an unexpected stop (a crash, or a forced restart after a freeze)."
                    + (previousUtc.HasValue ? $" Last seen running {When(previousUtc.Value)}." : "");
            else
                message = "\U0001F7E2 **Server is online.**";

            if (!worldOpen)
                message += " The world is still closed to players.";
            return message;
        }

        public static string BuildRestartScheduledMessage(uint seconds, DateTime nowUtc)
        {
            var at = nowUtc.AddSeconds(seconds);
            var length = seconds >= 120 ? $"{seconds / 60} minutes" : $"{seconds} seconds";
            return $"\U0001F7E1 **Restart scheduled** in {length}, at {When(at)}. This is planned, not a crash.";
        }

        public const string RestartCancelledMessage = "\U0001F7E2 The scheduled restart was **cancelled**. The server stays online.";
        public const string OfflinePlannedMessage = "\U0001F534 **Server is offline** for a planned restart or maintenance. Not a crash.";
        public const string OfflineStoppedMessage = "\U0001F534 **Server is offline** - stopped on purpose by an admin or the watchdog, without the usual countdown. Not a crash.";

        // ---------------------------------------------------------------- state file

        public static (string State, DateTime? Utc) ParseState(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return (null, null);
            var parts = text.Trim().Split(' ', 2);
            DateTime? utc = null;
            if (parts.Length == 2 && DateTime.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
                utc = parsed;
            return (parts[0], utc);
        }

        private static (string State, DateTime? Utc) ReadState()
        {
            try
            {
                return File.Exists(StateFile) ? ParseState(File.ReadAllText(StateFile)) : (null, null);
            }
            catch (Exception ex)
            {
                log.Warn($"[ServerStatus] Could not read {StateFile}: {ex.Message}");
                return (null, null);
            }
        }

        private static void WriteState(string state)
        {
            try
            {
                var temp = StateFile + ".tmp";
                File.WriteAllText(temp, $"{state} {DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}");
                File.Move(temp, StateFile, true);
            }
            catch (Exception ex)
            {
                log.Warn($"[ServerStatus] Could not write {StateFile}: {ex.Message}");
            }
        }

        // ---------------------------------------------------------------- hooks

        /// <summary>Call once startup is done. Says whether the previous run crashed, then starts the heartbeat.</summary>
        public static void AnnounceOnline(bool worldOpen)
        {
            var (previousState, previousUtc) = ReadState();
            WriteState(StateRunning);
            Interlocked.Exchange(ref online, 1);
            heartbeat = new Timer(_ => WriteState(StateRunning), null, HeartbeatInterval, HeartbeatInterval);

            var message = BuildOnlineMessage(previousState, previousUtc, worldOpen);
            log.Info($"[ServerStatus] Startup. Previous run ended: {previousState ?? "unknown"}.");

            if (ChannelId <= 0)
                return;

            // the Discord client connects in the background during startup; give it time
            _ = Task.Run(async () =>
            {
                try
                {
                    var waited = TimeSpan.Zero;
                    while (!DiscordChatManager.IsDiscordClientReady && waited < StartupDiscordWait)
                    {
                        await Task.Delay(2000);
                        waited += TimeSpan.FromSeconds(2);
                    }
                    await DiscordChatManager.SendDiscordChannelMessageAsync(message, ChannelId);
                }
                catch (Exception ex)
                {
                    log.Error($"[ServerStatus] Online post failed: {ex.Message}");
                }
            });
        }

        public static void AnnounceRestartScheduled(uint seconds)
        {
            if (seconds > 0)
                Post(BuildRestartScheduledMessage(seconds, DateTime.UtcNow), wait: false);
        }

        public static void AnnounceRestartCancelled() => Post(RestartCancelledMessage, wait: false);

        /// <summary>Planned shutdown has saved everything and is about to exit.</summary>
        public static void AnnounceOfflinePlanned() => AnnounceOffline(StateClean, OfflinePlannedMessage);

        /// <summary>The process is exiting without a planned shutdown (SIGTERM, watchdog, Ctrl+C in some hosts).</summary>
        public static void AnnounceOfflineStopped() => AnnounceOffline(StateStopped, OfflineStoppedMessage);

        private static void AnnounceOffline(string state, string message)
        {
            // a start that never got online (e.g. the database was down) must not overwrite "running",
            // or a crash followed by a failed restart would be forgotten
            if (Volatile.Read(ref online) == 0)
                return;
            if (Interlocked.Exchange(ref offlineAnnounced, 1) != 0)
                return;
            heartbeat?.Dispose();
            WriteState(state);
            Post(message, wait: true);
        }

        private static void Post(string message, bool wait)
        {
            if (ChannelId <= 0)
                return;
            try
            {
                var send = DiscordChatManager.SendDiscordChannelMessageAsync(message, ChannelId);
                if (wait)
                    send.Wait(ShutdownSendWait);
            }
            catch (Exception ex)
            {
                log.Error($"[ServerStatus] Post failed: {ex.Message}");
            }
        }
    }
}
