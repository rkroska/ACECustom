using System;
using System.Diagnostics;
using System.Reflection;

using log4net;

namespace ACE.Server.Utils
{
    public static class LogUtils
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        [Conditional("DEBUG")]
        public static void LogDebug(string message)
        {
            log.Debug(message);
        }

        [Conditional("DEBUG")]
        public static void LogDebug(string message, Exception exception)
        {
            log.Debug(message, exception);
        }
    }
}

