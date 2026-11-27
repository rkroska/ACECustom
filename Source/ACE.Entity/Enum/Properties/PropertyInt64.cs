namespace ACE.Entity.Enum.Properties
{
    // No properties are sent to the client unless they featured an attribute.
    // SendOnLogin gets sent to players in the PlayerDescription event
    // AssessmentProperty gets sent in successful appraisal
    public enum PropertyInt64 : ushort
    {
        Undef               = 0,
        [SendOnLogin]
        TotalExperience     = 1,
        [SendOnLogin]
        AvailableExperience = 2,
        [AssessmentProperty]
        AugmentationCost    = 3,
        [AssessmentProperty]
        ItemTotalXp         = 4,
        [AssessmentProperty]
        ItemBaseXp          = 5,
        [SendOnLogin]
        AvailableLuminance  = 6,
        [SendOnLogin]
        MaximumLuminance    = 7,
        InteractionReqs     = 8,

        /* custom */
        AllegianceXPCached    = 9000,
        AllegianceXPGenerated = 9001,
        AllegianceXPReceived  = 9002,
        VerifyXp              = 9003,

        // Every bank balance goes to the owner's client (owner 2026-11-22): in the login packet ([SendOnLogin]) and on each
        // change, batched with XP (Player_Xp.QueueBankUpdate) - a plugin reads them the way it reads XP and luminance.
        [SendOnLogin]
        BankedPyreals        = 9004,
        [SendOnLogin]
        BankedLuminance       = 9005,
        QuestCount            = 9006,
        LumAugCreatureCount   = 9007,
        LumAugItemCount       = 9008,
        LumAugLifeCount       = 9009,
        LumAugVoidCount       = 9010,
        LumAugWarCount        = 9011,

        AllegianceLumCached   = 9012,
        AllegianceLumGenerated= 9013,
        AllegianceLumReceived = 9014,
        [SendOnLogin]
        BankedLegendaryKeys   = 9015,
        LumAugDurationCount   = 9016,
        LumAugSpecializeCount = 9017,
        LumAugSummonCount     = 9018,
        [SendOnLogin]
        BankedEnlightenedCoins = 9020,
        [SendOnLogin]
        BankedMythicalKeys     = 9021,
        LumAugMeleeCount       = 9022,
        LumAugMissileCount     = 9023,
        LumAugMeleeDefenseCount = 9024,
        LumAugMissileDefenseCount = 9025,
        LumAugMagicDefenseCount = 9026,
        [SendOnLogin]
        BankedWeaklyEnlightenedCoins = 9027,

        // 50000+ block (matches the PropertyInt/PropertyBool charm-system ids)
        /// <summary>RETIRED 2026-11-22 (Triune retire): was the Charm of the Triune Weave count (+1 Creature / Item / Life each).
        /// The login move (AugMove) adds it onto LumAugCreature / Item / Life and removes it; nothing grants it any more. A gate
        /// that still names it (old gear, quest checks, portals) reads the larger of "growth past T15" and the value the move
        /// took (Creature.GetInt64ForGate).</summary>
        TriuneWeaveCount       = 50000,
        // RETIRED 2026-11-22: the four school charm counters (War / Void / Melee / Missile). The login move adds each onto
        // its LumAug school and removes it. A gate naming one reads the larger of that school past 4,000 and the value the move
        // took (Creature.GetInt64ForGate).
        /// <summary>Charm of the Battlemage's Wrath (War).</summary>
        BattlemagesWrathCharmCount = 50001,
        /// <summary>Charm of the Nether Veil (Void).</summary>
        NetherVeilCharmCount    = 50002,
        /// <summary>Charm of Crashing Steel (Melee).</summary>
        CrashingSteelCharmCount = 50003,
        /// <summary>Charm of the True Shot (Missile).</summary>
        TrueShotCharmCount      = 50004,
        /// <summary>
        /// 64-bit flat kill XP (2026-09-27): XpOverride (PropertyInt 146) is int32 and tops out at 2,147,483,647,
        /// below a T11 boss's 5B. When set it wins over XpOverride; a Zone Control xp_kill still wins over both.
        /// 50600 is free in EVERY Property enum (50005 is PropertyInt.CharmLevel).
        /// </summary>
        XpOverride64           = 50600,
        /// <summary>
        /// Prestige Coin (19860000) bank balance (owner 2026-10-04): deposit / withdraw only - Bonded + Attuned, never transferable.
        /// 50601, free in EVERY Property enum - moved off 9028 before release (upstream ACE owns the 90xx range).
        /// </summary>
        [SendOnLogin]
        BankedPrestigeCoins    = 50601,
        /// <summary>
        /// Enlightenment Token (300000) bank balance (owner 2026-10-13): deposit / withdraw only, exactly like Prestige Coins -
        /// Bonded + Attuned, never transferable. 50602, free in EVERY Property enum.
        /// </summary>
        [SendOnLogin]
        BankedEnlightenmentTokens = 50602,
        /// <summary>
        /// Triune earning (owner 2026-11-01), on a CHARACTER: the start of the week its Triune kill count belongs to (Unix
        /// seconds of the last weekly reset). A kill in a newer week resets the count. 50650, free in EVERY Property enum.
        /// </summary>
        TriuneWeekStart        = 50650,
        // RETIRED 2026-11-22 (one stat, owner): the T11-T14 drip counters were never live (TEST only). Earned augs now go
        // straight onto LumAug*; the login move adds any held drip onto LumAug and removes these. 50660-50666, keep the ids.
        /// <summary>T11-T14 drip: Creature augs earned from kills.</summary>
        AugDripCreatureCount   = 50660,
        /// <summary>T11-T14 drip: Item augs earned from kills.</summary>
        AugDripItemCount       = 50661,
        /// <summary>T11-T14 drip: Life augs earned from kills.</summary>
        AugDripLifeCount       = 50662,
        /// <summary>T11-T14 drip: War augs from earned T11-T14 school gems.</summary>
        AugDripWarCount        = 50663,
        /// <summary>T11-T14 drip: Void augs from earned T11-T14 school gems.</summary>
        AugDripVoidCount       = 50664,
        /// <summary>T11-T14 drip: Melee augs from earned T11-T14 school gems.</summary>
        AugDripMeleeCount      = 50665,
        /// <summary>T11-T14 drip: Missile augs from earned T11-T14 school gems.</summary>
        AugDripMissileCount    = 50666,
        // Hunting rewards (owner 2026-11-22, Triune retire): T11-T25 own-tier kills earn rewards, each +1 to ONE of Creature /
        // Item / Life at random, from a budget of rolls per school per tier. PropertyInt64 50667-50677 (PropertyString 50667 is
        // the move record, a different table).
        // All of them are SENT to the owner's client (at login and whenever they change), so a plugin can draw the
        // "Hunting rewards" block live, the way it reads banked luminance - no command to poll.
        /// <summary>The tier the roll counts below belong to (the player's own tier when they last earned or logged in). 0 = none.</summary>
        [SendOnLogin]
        AugRollTier            = 50667,
        /// <summary>Rolls taken this tier that landed on (or were lost on) Creature.</summary>
        [SendOnLogin]
        AugRollsCreature       = 50668,
        /// <summary>Rolls taken this tier on Item.</summary>
        [SendOnLogin]
        AugRollsItem           = 50669,
        /// <summary>Rolls taken this tier on Life.</summary>
        [SendOnLogin]
        AugRollsLife           = 50670,
        /// <summary>Own-tier kills counted this week (a whole-number mirror of PropertyFloat.TriuneWeekKills). Read it as 0
        /// once the time in AugHuntWeekResetAt has passed - the count only starts again on the next kill.</summary>
        [SendOnLogin]
        AugHuntWeekKills       = 50671,
        /// <summary>When the week turns (Unix seconds, UTC).</summary>
        [SendOnLogin]
        AugHuntWeekResetAt     = 50672,
        /// <summary>Kills a week at the full pace (the first band ends here: 33,600 at the defaults).</summary>
        [SendOnLogin]
        AugHuntBand1Kills      = 50673,
        /// <summary>Kills a week after which nothing is earned (the second band ends here: 67,200 at the defaults).</summary>
        [SendOnLogin]
        AugHuntBand2Kills      = 50674,
        /// <summary>Kills per reward in the first band, rounded (134 at T15). 0 = no rewards here right now.</summary>
        [SendOnLogin]
        AugHuntKillsPerReward1 = 50675,
        /// <summary>Kills per reward in the second band, rounded (269 at T15). 0 = none.</summary>
        [SendOnLogin]
        AugHuntKillsPerReward2 = 50676,
        /// <summary>Rolls each school has this tier (500 at the defaults). 0 = no rewards here right now.</summary>
        [SendOnLogin]
        AugHuntRollsPerSchool  = 50677,
        // Levelled charm coin ledger (owner 2026-10-24): the PLAYER keeps the Prestige Coins put into each levelled charm
        // with Charm Infusion Gems; the level (1-100) is worked out from them by the charm's live price curve, so a retune
        // re-levels everyone without touching what they spent. The charm item only shows the level (PropertyInt.CharmLevel
        // is mirrored from this). Ids in this lane's 512xx block (until 2026-10-24 they held the level itself; charms were
        // never on the live server, so nothing is converted).
        /// <summary>Prestige Coins put into the Split Arrow charm (0 / unset = level 1).</summary>
        SplitArrowCharmCoins     = 51200,
        /// <summary>Prestige Coins put into the Chain charm.</summary>
        ChainCharmCoins          = 51201,
        /// <summary>Prestige Coins put into the Pandemic charm.</summary>
        PandemicCharmCoins       = 51202,
        /// <summary>Prestige Coins put into the Improved Cleave charm.</summary>
        ImprovedCleaveCharmCoins = 51203,
        /// <summary>Prestige Coins put into the Armor Breaker charm (dual wield, 2026-10-21).</summary>
        ArmorBreakerCharmCoins   = 51204,
        /// <summary>Prestige Coins put into the Shield Arc charm (shield, 2026-10-21).</summary>
        ShieldArcCharmCoins      = 51205,

        /// <summary>Blacksmithing: Unix time (seconds) until which PropertyInt.ForgeDyePreview is the colour drawn.</summary>
        ForgeDyePreviewUntil    = 52000,

        /// <summary>
        /// Server-only (no SendOnLogin): unassigned XP earned past the long.MaxValue cap of AvailableExperience, moved back
        /// into AvailableExperience as XP is spent (Player_Xp). 51300, free in EVERY Property enum.
        /// </summary>
        OverflowExperience      = 51300,

        /// <summary>
        /// Server-only (no SendOnLogin): QB Deeds - the character's QB (the account's quest-bonus
        /// count) as last read, saved at login and on every quest stamp. The Deeds level. 56000, free in EVERY Property enum,
        /// every branch and the shard DBs.
        /// </summary>
        DeedsQb          = 56000,

        /// <summary>
        /// Server-only: Personal Lifestone - unix time (seconds) when this character may use the item again.
        /// Real time, so the cooldown keeps running while logged out (a cooldown enchantment would only
        /// tick while online). 59100, free in EVERY Property enum, every branch and the DBs (2026-11-14).
        /// </summary>
        PersonalLifestoneNextUse = 59100,

        // Pet Bonding System (Combat Pet Devices) — [AssessmentProperty] for identify + plugin PublicUpdateProperty*
        /// <summary>Current bond XP toward next level on a PetDevice.</summary>
        [AssessmentProperty]
        PetBondXp              = 9050,
        /// <summary>Total lifetime bond XP on a PetDevice.</summary>
        [AssessmentProperty]
        PetBondXpTotal         = 9051,
        /// <summary>Character ID the PetDevice is attuned/bonded to (stable DB character id).</summary>
        [AssessmentProperty]
        PetBondAttunedCharacterId = 9052,
    }
}
