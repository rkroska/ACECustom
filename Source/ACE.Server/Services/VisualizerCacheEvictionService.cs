using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace ACE.Server.Services
{
    public class VisualizerCacheEvictionService : BackgroundService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(VisualizerCacheEvictionService));

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            log.Info("[WEB PORTAL] Visualizer Cache Eviction background worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    VisualizerService.PerformEvictionIfNeeded();
                }
                catch (Exception ex)
                {
                    log.Error($"Error in Visualizer Cache Eviction worker: {ex.Message}");
                }

                // Run check once every hour
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            log.Info("[WEB PORTAL] Visualizer Cache Eviction background worker stopped.");
        }
    }
}
