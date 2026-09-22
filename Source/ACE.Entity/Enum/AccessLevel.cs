namespace ACE.Entity.Enum
{
    public enum AccessLevel
    {
        Player      = 0,
        Advocate    = 1,
        Sentinel    = 2,
        Envoy       = 3,
        Developer   = 4,
        Admin       = 5,

        // User-facing command access; kept as an alias so existing Player access values remain stable.
        User        = Player
    }
}
