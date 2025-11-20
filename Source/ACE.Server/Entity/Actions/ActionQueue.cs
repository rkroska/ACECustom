using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ACE.Server.Entity.Actions
{
    public class ActionQueue : IActor
    {
        private ConcurrentQueue<IAction> Queue { get; } = new ConcurrentQueue<IAction>();
        private ConcurrentDictionary<ActionType, int> CountByQueueItemType { get; } = new ConcurrentDictionary<ActionType, int>();

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Action queue throttle monitoring
        private int actionQueueThrottleWarningCount = 0;
        private DateTime lastActionQueueThrottleWarning = DateTime.MinValue;
        
        // Performance tracking (runtime configurable)
        private readonly Stopwatch sw = new Stopwatch();
        private static readonly Dictionary<string, ActionStats> actionStats = new Dictionary<string, ActionStats>();
        private static DateTime lastStatsReport = DateTime.UtcNow;
        private static DateTime lastDiscordAlert = DateTime.MinValue;
        private static int discordAlertsThisMinute = 0;
        
        private class ActionStats
        {
            public long Count;
            public long TotalMs;
            public long MaxMs;
            public string LastSlowTarget;
            public DateTime LastOccurrence;
        }

        public void RunActions()
        {
            if (Queue.IsEmpty) return;

            // CONFIGURABLE: Enable/disable performance tracking
            var enableTracking = ACE.Server.Managers.PropertyManager.GetBool("action_queue_tracking_enabled", false);
            
            // CONFIGURABLE: Performance thresholds (in milliseconds) - validated to prevent invalid values
            var trackThresholdMs = Math.Max(1, ACE.Server.Managers.PropertyManager.GetLong("action_queue_track_threshold_ms", 10));
            var warnThresholdMs = Math.Max(trackThresholdMs, ACE.Server.Managers.PropertyManager.GetLong("action_queue_warn_threshold_ms", 100));
            var reportIntervalMinutes = Math.Max(1, ACE.Server.Managers.PropertyManager.GetLong("action_queue_report_interval_minutes", 5));
            var discordMaxAlertsPerMinute = Math.Max(0, ACE.Server.Managers.PropertyManager.GetLong("action_queue_discord_max_alerts_per_minute", 3));

            // Throttle action processing to prevent cascade failures during high load
            // During mass spawns or combat, 500+ actions can queue and cause multi-second freezes
            // Process max 300 per tick to maintain responsiveness
            // Tuning: Lower = safer (200-250), Higher = faster queue clearing (300-400)
            // Increased from 250 to 300 based on production queue spikes during events
            // Configurable via: /modifylong action_queue_throttle_limit <value> (min: 50, recommended: 250-400)
            var throttleValue = (int)ACE.Server.Managers.PropertyManager.GetLong("action_queue_throttle_limit", 300);
            var actionThrottleLimit = Math.Max(50, throttleValue); // Enforce minimum of 50 to prevent server lockup
            var originalQueueSize = Queue.Count;
            var count = Math.Min(originalQueueSize, actionThrottleLimit);

            Dictionary<ActionType, int> processedActionsThisTick = [];
            for (int i = 0; i < count; i++)
            {
                if (Queue.TryDequeue(out var result))
                {
                    CountByQueueItemType.AddOrUpdate(result.Type, 0, (key, oldValue) => Math.Max(oldValue - 1, 0));
                    processedActionsThisTick.TryAdd(result.Type, 0);
                    processedActionsThisTick[result.Type]++;

                    // Track performance if enabled
                    if (enableTracking)
                        sw.Restart();

                    Tuple<IActor, IAction> enqueue = result.Act();

                    // Record performance metrics if enabled
                    if (enableTracking)
                    {
                        sw.Stop();
                        var elapsedMs = sw.Elapsed.TotalMilliseconds;
                        
                        if (elapsedMs >= trackThresholdMs)
                        {
                            TrackActionPerformance(result, elapsedMs, trackThresholdMs, warnThresholdMs, discordMaxAlertsPerMinute);
                        }
                    }

                    enqueue?.Item1.EnqueueAction(enqueue.Item2);
                }
            }
            
            // Periodic stats report (if tracking enabled)
            if (enableTracking && (DateTime.UtcNow - lastStatsReport).TotalMinutes >= reportIntervalMinutes)
            {
                ReportActionStats();
                lastStatsReport = DateTime.UtcNow;
            }
            
            // Alert if throttle is consistently maxed out (queue saturation)
            // Only alert after 3+ consecutive ticks of saturation to filter out temporary bursts
            if (originalQueueSize >= actionThrottleLimit)
            {
                actionQueueThrottleWarningCount++;
                
                // Only warn if saturated for 3+ consecutive ticks AND 60 seconds since last warning
                // This filters out expected temporary bursts (1-2 ticks) while catching sustained issues
                if (actionQueueThrottleWarningCount >= 3 && DateTime.UtcNow - lastActionQueueThrottleWarning > TimeSpan.FromSeconds(60))
                {
                    var remainingActions = Queue.Count;
                    var warningMsg = $"[PERFORMANCE] ActionQueue throttle saturated for {actionQueueThrottleWarningCount} consecutive ticks! Processed {count} actions, {remainingActions} remain queued. Original queue size: {originalQueueSize}. Consider increasing limit from {actionThrottleLimit}.";
                    var actionsProcessed = processedActionsThisTick
                        .OrderByDescending(kvp => kvp.Value)
                        .Select(kvp => $" - {kvp.Value}x {kvp.Key}");
                    warningMsg += "\nActions just processed:\n" + string.Join("\n", actionsProcessed);
                    var actionsRemaining = CountByQueueItemType
                        .Where(kvp => kvp.Value > 0)
                        .OrderByDescending(kvp => kvp.Value)
                        .Select(kvp => $" - {kvp.Value}x {kvp.Key}");
                    warningMsg += "\nActions remaining:\n" + string.Join("\n", actionsRemaining);

                    log.Warn(warningMsg);
                    
                    // Send to Discord if configured
                    if (ACE.Common.ConfigManager.Config.Chat.EnableDiscordConnection && ACE.Common.ConfigManager.Config.Chat.PerformanceAlertsChannelId > 0)
                    {
                        try
                        {
                            ACE.Server.Managers.DiscordChatManager.SendDiscordMessage("⚠️ SERVER", warningMsg, ACE.Common.ConfigManager.Config.Chat.PerformanceAlertsChannelId);
                        }
                        catch (Exception ex)
                        {
                            log.Error($"Failed to send ActionQueue throttle warning to Discord: {ex.Message}");
                        }
                    }
                    
                    lastActionQueueThrottleWarning = DateTime.UtcNow;
                    // Don't reset counter here - let it continue tracking consecutive saturations
                }
            }
            else
            {
                // Reset counter when not saturated
                actionQueueThrottleWarningCount = 0;
            }
        }

        public void EnqueueAction(IAction action) 
        {
            CountByQueueItemType.AddOrUpdate(action.Type, 1, (key, oldValue) => oldValue + 1);
            Queue.Enqueue(action);
        }

        public void Clear()
        {
            Queue.Clear();
        }

        public int Count()
        {
            return Queue.Count;
        }
        
        /// <summary>
        /// Tracks performance of slow actions and aggregates statistics
        /// </summary>
        private void TrackActionPerformance(IAction result, double elapsedMs, long trackThreshold, long warnThreshold, long discordMaxAlerts)
        {
            string methodName = "Unknown";
            string targetInfo = "";
            string additionalContext = "";
            
            if (result is ActionEventDelegate actionEventDelegate)
            {
                methodName = actionEventDelegate.Action.Method.Name;
                
                // Try to get WorldObject target
                if (actionEventDelegate.Action.Target is WorldObjects.WorldObject wo)
                {
                    targetInfo = $"0x{wo.Guid}:{wo.Name}";
                }
                
                // For lambda functions, try to extract closure information (best effort)
                if (methodName.Contains("<") && methodName.Contains(">"))
                {
                    try
                    {
                        // This is a compiler-generated lambda - try to get more context
                        var target = actionEventDelegate.Action.Target;
                        if (target != null)
                        {
                            var targetType = target.GetType();
                            
                            // Try to extract declaring type/method
                            var declaringType = actionEventDelegate.Action.Method.DeclaringType;
                            if (declaringType != null)
                            {
                                var typeName = declaringType.Name;
                                // Clean up compiler-generated names
                                if (typeName.Contains("<"))
                                {
                                    var outerType = declaringType.DeclaringType;
                                    if (outerType != null)
                                        typeName = outerType.Name;
                                }
                                additionalContext = $" [{typeName}]";
                            }
                            
                            // For InboundMessageManager, try to find Session/Player info using reflection
                            var fields = targetType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            foreach (var field in fields)
                            {
                                try
                                {
                                    if (field.Name.Contains("session") || field.Name.Contains("Session"))
                                    {
                                        var session = field.GetValue(target) as Network.Session;
                                        if (session?.Player != null)
                                        {
                                            targetInfo = $"Player: {session.Player.Name}";
                                            break;
                                        }
                                    }
                                }
                                catch
                                {
                                    // Ignore individual field reflection errors
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Reflection failed - continue without enhanced context (not critical)
                        if (log.IsDebugEnabled)
                            log.Debug($"Failed to extract lambda context: {ex.Message}");
                    }
                }
            }
            
            // Aggregate stats (thread-safe)
            lock (actionStats)
            {
                if (!actionStats.ContainsKey(methodName))
                    actionStats[methodName] = new ActionStats();
                
                var stats = actionStats[methodName];
                stats.Count++;
                stats.TotalMs += (long)elapsedMs;
                stats.MaxMs = Math.Max(stats.MaxMs, (long)elapsedMs);
                stats.LastOccurrence = DateTime.UtcNow;
                
                if (elapsedMs >= warnThreshold && !string.IsNullOrEmpty(targetInfo))
                    stats.LastSlowTarget = targetInfo;
            }
            
            // RATE-LIMITED WARNING: Only if exceeds warn threshold
            if (elapsedMs >= warnThreshold)
            {
                // Always log locally
                var fullMethodName = methodName + additionalContext;
                log.Warn($"[ACTION] Slow action: {fullMethodName} took {elapsedMs:N0}ms{(string.IsNullOrEmpty(targetInfo) ? "" : $" - {targetInfo}")}");
                
                // Rate-limited Discord alerts
                SendRateLimitedDiscordAlert(fullMethodName, elapsedMs, targetInfo, discordMaxAlerts);
            }
        }
        
        /// <summary>
        /// Sends Discord alerts with rate limiting to prevent API throttling
        /// </summary>
        private void SendRateLimitedDiscordAlert(string methodName, double elapsedMs, string targetInfo, long maxAlertsPerMinute)
        {
            var now = DateTime.UtcNow;
            
            // Reset counter every minute
            if ((now - lastDiscordAlert).TotalMinutes >= 1)
            {
                discordAlertsThisMinute = 0;
            }
            
            // Check if we're under rate limit
            if (discordAlertsThisMinute >= maxAlertsPerMinute)
                return;
            
            // Check Discord is configured
            if (!ACE.Common.ConfigManager.Config.Chat.EnableDiscordConnection || 
                ACE.Common.ConfigManager.Config.Chat.PerformanceAlertsChannelId <= 0)
                return;
            
            try
            {
                var msg = $"⚠️ **Slow Action**: `{methodName}` took **{elapsedMs:N0}ms**";
                if (!string.IsNullOrEmpty(targetInfo))
                    msg += $"\nTarget: `{targetInfo}`";
                
                ACE.Server.Managers.DiscordChatManager.SendDiscordMessage("PERFORMANCE", msg, 
                    ACE.Common.ConfigManager.Config.Chat.PerformanceAlertsChannelId);
                
                discordAlertsThisMinute++;
                lastDiscordAlert = now;
            }
            catch (Exception ex)
            {
                log.Error($"Failed to send slow action alert to Discord: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Reports aggregated performance statistics periodically
        /// </summary>
        private void ReportActionStats()
        {
            lock (actionStats)
            {
                if (actionStats.Count == 0)
                    return;
                
                var topOffenders = actionStats
                    .OrderByDescending(kvp => kvp.Value.TotalMs)
                    .Take(10)
                    .ToList();
                
                log.Info("=== ACTION QUEUE PERFORMANCE REPORT ===");
                log.Info("Top 10 actions by total time spent:");
                
                foreach (var kvp in topOffenders)
                {
                    var stats = kvp.Value;
                    var avgMs = stats.Count > 0 ? stats.TotalMs / stats.Count : 0;
                    log.Info($"  {kvp.Key}: Count={stats.Count:N0}, Avg={avgMs:N1}ms, Max={stats.MaxMs:N0}ms, Total={stats.TotalMs:N0}ms");
                    
                    if (!string.IsNullOrEmpty(stats.LastSlowTarget))
                        log.Info($"    Last slow target: {stats.LastSlowTarget}");
                }
                
                log.Info("=== END REPORT ===");
                
                // Clear stats for next period
                actionStats.Clear();
            }
        }
    }
}
