namespace ACE.Entity.Enum
{
    /// <summary>
    /// Bit flags for custom targeting (stored as <see cref="Properties.PropertyInt.TargetingFlags"/>).
    /// Replaces embedding pseudo <see cref="CreatureType"/> values 996–999 in friend/foe strings.
    /// </summary>
    [System.Flags]
    public enum CustomTargetingBehavior : int
    {
        None = 0,

        /// <summary>Former pseudo-type 998 — hostile to all creature types.</summary>
        AttackAll = 1 << 0,

        /// <summary>Former pseudo-type 999 — hostile to outsiders and all players; not auto-hostile to same concrete <see cref="CreatureType"/>.</summary>
        AttackNonSelf = 1 << 1,

        /// <summary>Former pseudo-type 997 in friend lists — treat all players as friends.</summary>
        FriendlyToPlayers = 1 << 2,

        /// <summary>Former pseudo-type 996 in friend lists — quest-stamp friends (uses <see cref="Properties.PropertyString.FriendlyQuestString"/>).</summary>
        FriendlyToQuestPlayer = 1 << 3,

        /// <summary>Former pseudo-type 997 in foe lists — hostile to all players (unless explicitly made a friend).</summary>
        HostileToAllPlayers = 1 << 4,
    }
}
