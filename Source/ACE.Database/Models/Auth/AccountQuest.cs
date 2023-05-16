using System;
using System.Collections.Generic;

namespace ACE.Database.Models.Auth;

public partial class AccountQuest
{
    public uint AccountId { get; set; }

    public string Quest { get; set; }

    public uint? NumTimesCompleted { get; set; }
}
