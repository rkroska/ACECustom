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
        private static readonly TimeSpan StartupRetryDelay = TimeSpan.FromSeconds(5);

        private static Timer heartbeat;
        private static int online;            // set once AnnounceOnline ran
        private static int offlineAnnounced;  // set once shutdown began; stops heartbeat writes and pending startup posts

        // heartbeat and the final clean/stopped write share this lock, so a heartbeat already running when
        // shutdown starts can't overwrite the final state (Timer.Dispose doesn't wait for running callbacks)
        private static readonly object stateLock = new object();

        // one status post at a time, so an online post still in flight finishes before the offline post goes out
        private static readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);

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
            heartbeat = new Timer(_ => Heartbeat(), null, HeartbeatInterval, HeartbeatInterval);

            var message = BuildOnlineMessage(previousState, previousUtc, worldOpen);
            log.Info($"[ServerStatus] Startup. Previous run ended: {previousState ?? "unknown"}.");

            if (ChannelId <= 0)
                return;

            // the Discord client connects and loads its guilds in the background during startup, so the channel
            // may not resolve yet: keep retrying until it sends, the deadline passes, or shutdown begins
            _ = Task.Run(async () =>
            {
                var deadline = DateTime.UtcNow + StartupDiscordWait;
                try
                {
                    while (Volatile.Read(ref offlineAnnounced) == 0)
                    {
                        if (DiscordChatManager.IsDiscordClientReady)
                        {
                            await sendLock.WaitAsync();
                            try
                            {
                                if (Volatile.Read(ref offlineAnnounced) != 0)
                                    return;
                                if (await DiscordChatManager.SendDiscordChannelMessageAsync(message, ChannelId) != null)
                                    return;
                            }
                            finally
                            {
                                sendLock.Release();
                            }
                        }

                        if (DateTime.UtcNow >= deadline)
                        {
                            log.Warn($"[ServerStatus] Online post not sent: channel {ChannelId} not available within {StartupDiscordWait.TotalMinutes} minutes.");
                            return;
                        }
                        await Task.Delay(StartupRetryDelay);
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"[ServerStatus] Online post failed: {ex.Message}");
                }
            });
        }

        private static void Heartbeat()
        {
            lock (stateLock)
            {
                if (Volatile.Read(ref offlineAnnounced) == 0)
                    WriteState(StateRunning);
            }
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
            lock (stateLock)
            {
                if (Interlocked.Exchange(ref offlineAnnounced, 1) != 0)
                    return;
                heartbeat?.Dispose();
                WriteState(state);
            }
            Post(message, wait: true);
        }

        private static void Post(string message, bool wait)
        {
            if (ChannelId <= 0)
                return;

            var send = Task.Run(async () =>
            {
                await sendLock.WaitAsync();
                try
                {
                    await DiscordChatManager.SendDiscordChannelMessageAsync(message, ChannelId);
                }
                catch (Exception ex)
                {
                    log.Error($"[ServerStatus] Post failed: {ex.Message}");
                }
                finally
                {
                    sendLock.Release();
                }
            });

            // shutdown posts get a short wait (queue + send) so they leave before the process exits
            if (wait)
                send.Wait(ShutdownSendWait + ShutdownSendWait);
        }
    }
}
