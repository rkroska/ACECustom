using System;

namespace ACE.Database.Models.Auth;

public partial class PatchNote
{
    public int Id { get; set; }

    public string Slug { get; set; } = "";

    public string Title { get; set; } = "";

    public string Summary { get; set; }

    public string Body { get; set; } = "";

    public string Status { get; set; } = PatchNoteStatus.Draft;

    public DateTime? PublishedAt { get; set; }

    public uint? PublishedByAccountId { get; set; }

    public bool PostToDiscord { get; set; } = true;

    public long? DiscordMessageId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public static class PatchNoteStatus
{
    public const string Draft = "draft";
    public const string Published = "published";
}
