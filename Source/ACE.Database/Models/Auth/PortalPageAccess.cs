using System;

namespace ACE.Database.Models.Auth;

public partial class PortalPageAccess
{
    public string PageKey { get; set; } = "";

    public byte MinLevel { get; set; }

    public DateTime UpdatedAt { get; set; }
}
