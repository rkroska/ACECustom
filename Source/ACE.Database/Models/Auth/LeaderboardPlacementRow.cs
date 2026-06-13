using System.ComponentModel.DataAnnotations.Schema;

namespace ACE.Database.Models.Auth;

/// <summary>
/// Keyless query result: viewer placement on a leaderboard (ROW_NUMBER ordering matches top-list semantics).
/// </summary>
public class LeaderboardPlacementRow
{
    public long PlacementRank { get; set; }

    public ulong? Score { get; set; }

    public uint? Account { get; set; }

    public string Character { get; set; }

    [Column("LeaderboardID")]
    public ulong LeaderboardId { get; set; }
}
