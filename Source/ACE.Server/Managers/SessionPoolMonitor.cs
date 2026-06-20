using System;
using System.Threading;

using log4net;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Server.Diagnostics;
using ACE.Server.Network;
using ACE.Server.Network.Enum;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Handlers;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Session pool health monitoring, stale-session cleanup, emergency shutdown, and Discord alerts.
    /// </summary>
    public static class SessionPoolMonitor
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SessionPoolMonitor));

        private static int emergencyTriggered;
        private static DateTime nextSummaryLogUtc = DateTime.MinValue;
        private static DateTime lastHighPoolDiscordUtc = DateTime.MinValue;
        private static long peakUnauthenticated;

        public static int GetEmergencyThreshold()
        {
            var max = (int)ConfigManager.Config.Server.Network.MaximumAllowedSessions;
            var configured = ServerConfig.session_pool_emergency_threshold.Value;
            if (configured > 0)
                return Math.Max(1, (int)Math.Min(configured, max));

            return Math.Max(1, max * 90 / 100);
        }

        public static bool ShouldRejectLoginEarly(int sessionCount)
        {
            var pct = (int)Math.Clamp(ServerConfig.session_pool_early_reject_threshold_percent.Value, 0, 100);
            if (pct <= 0)
                return false;

            var max = ConfigManager.Config.Server.Network.MaximumAllowedSessions;
            return sessionCount >= max * pct / 100;
        }

        public static bool IsUnderSessionStress(int sessionCount)
        {
            var max = ConfigManager.Config.Server.Network.MaximumAllowedSessions;
            return sessionCount > max * 80 / 100;
        }

        public static void OnSessionCreated(int activeCount)
        {
            Interlocked.Increment(ref ServerDiagnostics.SessionCreatedTotal);
        }

        public static void OnSessionRemoved(SessionTerminationReason reason)
        {
            Interlocked.Increment(ref ServerDiagnostics.SessionRemovedTotal);
            if ((int)reason >= 0 && (int)reason < ServerDiagnostics.SessionRemovedByReason.Length)
                Interlocked.Increment(ref ServerDiagnostics.SessionRemovedByReason[(int)reason]);
        }

        public static void SweepStaleSessions(Session[] sessionMap)
        {
            if (!ServerConfig.session_pool_stale_sweeper_enabled.Value)
                return;

            var unauthMaxAge = TimeSpan.FromSeconds(Math.Max(15, ServerConfig.session_pool_stale_unauth_max_age_seconds.Value));
            var stuckTermination = TimeSpan.FromSeconds(Math.Max(3, ServerConfig.session_pool_stuck_termination_max_age_seconds.Value));
            var now = DateTime.UtcNow;

            foreach (var session in sessionMap)
            {
                if (session == null)
                    continue;

                if (session.PendingTermination != null)
                {
                    if (session.AccountId != 0 || session.Player != null)
                        continue;

                    if (session.PendingTermination.TerminationStatus == SessionTerminationPhase.Initialized &&
                        now.Ticks - session.PendingTermination.TerminationStartTicks > stuckTermination.Ticks)
                    {
                        session.PendingTermination.TerminationStatus = SessionTerminationPhase.SessionWorkCompleted;
                        Interlocked.Increment(ref ServerDiagnostics.SessionStuckTerminationForced);
                    }

                    continue;
                }

                if (session.AccountId != 0 || session.Player != null)
                    continue;

                if (session.State != SessionState.AuthLoginRequest)
                    continue;

                if (now - session.CreatedAt <= unauthMaxAge)
                    continue;

                session.Terminate(SessionTerminationReason.StaleSessionSweeper);
                Interlocked.Increment(ref ServerDiagnostics.SessionStaleSweeperRemoved);
            }
        }

        public static void CheckEmergencyShutdown(int sessionCount, int authCount, int unauthCount)
        {
            if (!ServerConfig.session_pool_emergency_shutdown_enabled.Value)
                return;

            if (ServerManager.ShutdownInitiated)
                return;

            if (Interlocked.CompareExchange(ref emergencyTriggered, 1, 0) != 0)
                return;

            var threshold = GetEmergencyThreshold();
            if (sessionCount < threshold)
            {
                Interlocked.Exchange(ref emergencyTriggered, 0);
                return;
            }

            var shutdownSeconds = (uint)Math.Clamp(ServerConfig.session_pool_emergency_shutdown_seconds.Value, 60, 3600);

            log.Error($"[SESSION EMERGENCY] Session pool threshold reached: {sessionCount} active ({authCount} authenticated, {unauthCount} unauthenticated). Threshold={threshold}. Initiating {shutdownSeconds}s shutdown.");

            Interlocked.Increment(ref ServerDiagnostics.SessionPoolEmergencyShutdownTriggered);

            var willCloseWorld = ServerConfig.session_pool_emergency_close_world.Value && WorldManager.WorldStatus == WorldManager.WorldStatusState.Open;
            var discordMsg = willCloseWorld
                ? $"🔴 **SESSION POOL EMERGENCY**: Active sessions at **{sessionCount}** (threshold: **{threshold}**). **{unauthCount}** unauthenticated, **{authCount}** authenticated. Closing world and initiating **{shutdownSeconds}s** graceful shutdown."
                : $"🔴 **SESSION POOL EMERGENCY**: Active sessions at **{sessionCount}** (threshold: **{threshold}**). **{unauthCount}** unauthenticated, **{authCount}** authenticated. Initiating **{shutdownSeconds}s** graceful shutdown (world not closed).";
            SendDiscordAlert(discordMsg);

            if (willCloseWorld)
                WorldManager.Close(null);

            PlayerManager.BroadcastToAuditChannel(null,
                $"[SESSION EMERGENCY] Auto-shutdown triggered at {sessionCount} sessions ({unauthCount} unauthenticated). Server shutting down in {shutdownSeconds} seconds.");

            var minutes = shutdownSeconds / 60;
            var seconds = shutdownSeconds % 60;
            var timeText = minutes > 0
                ? $"{minutes} minute{(minutes == 1 ? "" : "s")}" + (seconds > 0 ? $" and {seconds} second{(seconds == 1 ? "" : "s")}" : "")
                : $"{seconds} second{(seconds == 1 ? "" : "s")}";

            PlayerManager.BroadcastToAll(new GameMessageSystemChat(
                $"Broadcast from System> ATTENTION - This server detected abnormal connection load and will shut down in {timeText}. Please log out safely.",
                ChatMessageType.WorldBroadcast));

            ServerManager.SetShutdownInterval(shutdownSeconds);
            ServerManager.BeginShutdown();
        }

        public static void MaybeLogPeriodicSummary(int sessionCount, int authCount, int unauthCount, int terminatingCount)
        {
            if (unauthCount > peakUnauthenticated)
                peakUnauthenticated = unauthCount;

            if (unauthCount > Interlocked.Read(ref ServerDiagnostics.SessionUnauthenticatedPeak))
                Interlocked.Exchange(ref ServerDiagnostics.SessionUnauthenticatedPeak, unauthCount);

            if (DateTime.UtcNow < nextSummaryLogUtc)
                return;

            nextSummaryLogUtc = DateTime.UtcNow.AddSeconds(60);

            var max = ConfigManager.Config.Server.Network.MaximumAllowedSessions;
            log.Info($"[SESSION] pool active={sessionCount}/{max} auth={authCount} unauth={unauthCount} terminating={terminatingCount} peakUnauth={peakUnauthenticated} poolFullRejects={Interlocked.Read(ref ServerDiagnostics.SessionLoginRejectedSessionPoolFull)} trackerPings={Interlocked.Read(ref ServerDiagnostics.TrackerPingHandled)}");

            if (sessionCount >= GetEmergencyThreshold() * 80 / 100 && DateTime.UtcNow - lastHighPoolDiscordUtc >= TimeSpan.FromMinutes(5))
            {
                lastHighPoolDiscordUtc = DateTime.UtcNow;
                var warnMsg = $"⚠️ **SESSION POOL HIGH**: **{sessionCount}** active (**{unauthCount}** unauthenticated, **{authCount}** authenticated). Threshold for emergency shutdown: **{GetEmergencyThreshold()}**.";
                SendDiscordAlert(warnMsg);
            }
        }

        private static void SendDiscordAlert(string message)
        {
            if (ServerConfig.discord_performance_level.Value < (long)DiscordLogLevel.Info)
                return;

            if (ConfigManager.Config.Chat.PerformanceAlertsChannelId <= 0)
                return;

            try
            {
                _ = DiscordChatManager.SendDiscordMessage("SESSION MONITOR", message, ConfigManager.Config.Chat.PerformanceAlertsChannelId);
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to send session pool Discord alert: {ex.Message}");
            }
        }
    }
}
