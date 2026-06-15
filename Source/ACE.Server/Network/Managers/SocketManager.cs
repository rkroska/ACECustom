using System;
using System.Collections.Generic;
using System.Net;

using log4net;

using ACE.Common;
using ACE.Server.Managers;

namespace ACE.Server.Network.Managers
{
    public static class SocketManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static ConnectionListener[] listeners;

        // Tracks the last-applied socket buffer state so ReconcileSocketBuffers() only touches sockets on change.
        private static bool? lastBufferEnabled;
        private static int lastBufferSize;

        private const int MinSocketBufferSize = 65536;       // 64 KB
        private const int MaxSocketBufferSize = 67108864;    // 64 MB

        public static void Initialize()
        {
            var hosts = new List<IPAddress>();

            try
            {
                var splits = ConfigManager.Config.Server.Network.Host.Split(",");

                foreach (var split in splits)
                    hosts.Add(IPAddress.Parse(split));
            }
            catch (Exception ex)
            {
                log.Error($"Unable to use {ConfigManager.Config.Server.Network.Host} as host due to: {ex}");
                log.Error("Using IPAddress.Any as host instead.");
                hosts.Clear();
                hosts.Add(IPAddress.Any);
            }

            listeners = new ConnectionListener[hosts.Count * 2];

            for (int i = 0; i < hosts.Count; i++)
            {
                listeners[(i * 2) + 0] = new ConnectionListener(hosts[i], ConfigManager.Config.Server.Network.Port);
                log.Info($"Binding ConnectionListener to {hosts[i]}:{ConfigManager.Config.Server.Network.Port}");

                listeners[(i * 2) + 1] = new ConnectionListener(hosts[i], ConfigManager.Config.Server.Network.Port + 1);
                log.Info($"Binding ConnectionListener to {hosts[i]}:{ConfigManager.Config.Server.Network.Port + 1}");

                listeners[(i * 2) + 1].Start();
                listeners[(i * 2) + 0].Start();

            }

            // Apply the configured socket buffer override (if enabled) once at startup.
            ReconcileSocketBuffers();
        }

        /// <summary>
        /// Applies the net_socket_buffer_enabled / net_socket_buffer_size config to all listener sockets.
        /// Cheap no-op when the desired state is unchanged, so it is safe to call periodically for live toggling.
        /// </summary>
        public static void ReconcileSocketBuffers()
        {
            if (listeners == null)
                return;

            var enabled = ServerConfig.net_socket_buffer_enabled.Value;
            var size = (int)Math.Clamp(ServerConfig.net_socket_buffer_size.Value, MinSocketBufferSize, MaxSocketBufferSize);

            if (lastBufferEnabled == enabled && lastBufferSize == size)
                return;

            foreach (var listener in listeners)
                listener?.ApplyBufferSettings(enabled, size);

            lastBufferEnabled = enabled;
            lastBufferSize = size;
        }
    }
}
