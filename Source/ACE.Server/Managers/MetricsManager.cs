using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using System;
using System.Diagnostics.Metrics;
using System.Text;

namespace ACE.Server.Managers
{
    public static class MetricsManager
    {
        public const string MetricNamespace = "ACE.Metrics";
        private static MeterProvider meterProvider;
        private static readonly Meter metrics = new(MetricNamespace);

        // Publically exposed metrics should follow.

        public static readonly Histogram<double> actionLatencies = metrics.CreateHistogram<double>(
            "queued-action-latencies",
            "Microseconds",
            "Latency of executing a queue entry in microseconds."
        );

        /// <summary>
        /// Initializes the OpenTelemetry MeterProvider and begins pushing to an OLTP provider.
        /// This method may be called at application startup. If it is not called, metrics will be blackholed.
        /// </summary>
        public static void StartMetricsPipeline(string endpoint, string instanceId, string apiToken)
        {
            if (meterProvider != null) return;

            // Generate the Base64 Auth Header
            // Format is "Authorization=Basic <Base64(InstanceId:Token)>"
            string authString = $"{instanceId}:{apiToken}";
            string base64Auth = Convert.ToBase64String(Encoding.ASCII.GetBytes(authString));
            string headers = $"Authorization=Basic {base64Auth}";

            meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(MetricNamespace)
                .AddMeter("MySqlConnector")
                .AddMeter("System.Net.Http")
                .AddMeter("System.Net.NameResolution")
                .AddRuntimeInstrumentation()
                .AddOtlpExporter((exporterOptions, metricReaderOptions) =>
                {
                    exporterOptions.Endpoint = new Uri(endpoint);
                    exporterOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
                    exporterOptions.Headers = headers;
                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 10000; // 10s
                })
                .Build();
        }

        /// <summary>
        /// Flushes any buffered metrics and gracefully shuts down the MeterProvider.
        /// This should be called when the application is shutting down.
        /// </summary>
        public static void Shutdown()
        {
            meterProvider?.Dispose();
            meterProvider = null;
        }
    }
}
