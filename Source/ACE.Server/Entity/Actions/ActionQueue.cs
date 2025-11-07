// Uncomment this if you want to measure the time actions take to execute and report slow ones
//#define WRAP_AND_MEASURE_ACT_WITH_STOPWATCH

using System;
using System.Collections.Concurrent;

namespace ACE.Server.Entity.Actions
{
    public class ActionQueue : IActor
    {
        protected ConcurrentQueue<IAction> Queue { get; } = new ConcurrentQueue<IAction>();

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        #if WRAP_AND_MEASURE_ACT_WITH_STOPWATCH
        private readonly System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        #endif
        
        // Action queue throttle monitoring
        private int actionQueueThrottleWarningCount = 0;
        private DateTime lastActionQueueThrottleWarning = DateTime.MinValue;

        public void RunActions()
        {
            if (Queue.IsEmpty)
                return;

            // Throttle action processing to prevent cascade failures during high load
            // During mass spawns or combat, 500+ actions can queue and cause multi-second freezes
            // Process max 250 per tick to maintain responsiveness
            // Tuning: Lower = safer (150-200), Higher = faster queue clearing (250-300)
            // Set to 250 based on observed queue spikes up to 500 during spawn events
            const int actionThrottleLimit = 250;
            var originalQueueSize = Queue.Count;
            var count = Math.Min(originalQueueSize, actionThrottleLimit);

            for (int i = 0; i < count; i++)
            {
                if (Queue.TryDequeue(out var result))
                {
                    #if WRAP_AND_MEASURE_ACT_WITH_STOPWATCH
                    sw.Restart();
                    #endif

                    Tuple<IActor, IAction> enqueue = result.Act();

                    #if WRAP_AND_MEASURE_ACT_WITH_STOPWATCH
                    sw.Stop();

                    if (sw.Elapsed.TotalSeconds > 0.1)
                    {
                        if (result is ActionEventDelegate actionEventDelegate)
                        {
                            if (actionEventDelegate.Action.Target is WorldObjects.WorldObject worldObject)
                                log.Warn($"ActionQueue Act() took {sw.Elapsed.Milliseconds:N0}ms. Method.Name: {actionEventDelegate.Action.Method.Name}, Target: {actionEventDelegate.Action.Target} 0x{worldObject.Guid}:{worldObject.Name}");
                            else
                                log.Warn($"ActionQueue Act() took {sw.Elapsed.Milliseconds:N0}ms. Method.Name: {actionEventDelegate.Action.Method.Name}, Target: {actionEventDelegate.Action.Target}");
                        }
                        else
                            log.Warn($"ActionQueue Act() took {sw.Elapsed.Milliseconds:N0}ms.");
                    }
                    #endif

                    if (enqueue != null)
                        enqueue.Item1.EnqueueAction(enqueue.Item2);
                }
            }
            
            // Alert if throttle is consistently maxed out (queue saturation)
            if (originalQueueSize >= actionThrottleLimit)
            {
                actionQueueThrottleWarningCount++;
                
                // Warn every 60 seconds if consistently saturated
                if (DateTime.UtcNow - lastActionQueueThrottleWarning > TimeSpan.FromSeconds(60))
                {
                    var remainingActions = Queue.Count;
                    var warningMsg = $"[PERFORMANCE] ActionQueue throttle saturated! Processed {count} actions, {remainingActions} remain queued. Original queue size: {originalQueueSize}. Consider increasing limit from {actionThrottleLimit}. Warnings: {actionQueueThrottleWarningCount}";
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
                    actionQueueThrottleWarningCount = 0;
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
    }
}
