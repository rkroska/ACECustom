namespace ACE.Server.Managers
{
    public class PatchNotesDiscordResult
    {
        /// <summary>not_requested | sent | skipped | failed</summary>
        public string Status { get; set; }

        public string Message { get; set; }

        public ulong? MessageId { get; set; }

        public static PatchNotesDiscordResult NotRequested() => new()
        {
            Status = "not_requested",
            Message = "Discord post was not requested for this note."
        };

        public static PatchNotesDiscordResult Sent(ulong messageId) => new()
        {
            Status = "sent",
            Message = "Posted to Discord.",
            MessageId = messageId
        };

        public static PatchNotesDiscordResult Skipped(string message) => new()
        {
            Status = "skipped",
            Message = message
        };

        public static PatchNotesDiscordResult AlreadyPosted(ulong messageId) => new()
        {
            Status = "already_posted",
            Message = "Already posted to Discord.",
            MessageId = messageId
        };

        public static PatchNotesDiscordResult Failed(string message) => new()
        {
            Status = "failed",
            Message = message
        };
    }
}
