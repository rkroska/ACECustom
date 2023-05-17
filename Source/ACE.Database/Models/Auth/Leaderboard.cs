using System;
using System.Collections.Generic;

namespace ACE.Database.Models.Auth;

public partial class Leaderboard
{
    public ulong? Score { get; set; }

    public uint? Account { get; set; }

    public string Character { get; set; }

    public ulong LeaderboardId { get; set; }
}
