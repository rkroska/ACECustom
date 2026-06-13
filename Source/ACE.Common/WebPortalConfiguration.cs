namespace ACE.Common
{
    public class WebPortalConfiguration
    {
        /// <summary>
        /// HTTP bind host. Empty = auto (Debug: *, Release: localhost). Use * or 0.0.0.0 to listen on all interfaces.
        /// </summary>
        public string BindHost { get; set; }

        /// <summary>HTTP port. 0 = auto (Debug: 5000, Release: 5001).</summary>
        public int BindPort { get; set; }

        /// <summary>
        /// Stable JWT signing secret (at least 32 characters). Required for production so portal logins survive server restarts.
        /// If empty, a random secret is generated each start and all portal sessions are invalidated on restart.
        /// </summary>
        public string JwtSecret { get; set; }
    }
}
