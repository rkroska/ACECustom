namespace ACE.Common
{
    public class PatchNotesConfiguration
    {
        public string PublicBaseUrl { get; set; } = "http://localhost:5001/";

        public bool MotdEnabled { get; set; } = true;

        /// <summary>
        /// Tokens: {url}, {lastUpdated}, {lastUpdatedUtc}, {lastUpdatedRelative}.
        /// {lastUpdated} uses MotdTimeZoneId, else host local time when MotdUseHostLocalTime is true, else UTC.
        /// </summary>
        public string MotdTemplate { get; set; } = "Patch notes: {url}\nLast updated: {lastUpdated}";

        /// <summary>
        /// Optional Windows or IANA timezone id (e.g. "Central Standard Time" or "America/Chicago").
        /// When set, in-game {lastUpdated} uses this zone for all players.
        /// </summary>
        public string MotdTimeZoneId { get; set; }

        /// <summary>
        /// When MotdTimeZoneId is empty, format {lastUpdated} using the ACE server's local timezone.
        /// </summary>
        public bool MotdUseHostLocalTime { get; set; } = true;

        public long DiscordChannelId { get; set; }

        public bool DiscordEnabled { get; set; } = true;
    }
}
