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

        BankedPyreals         = 9004,
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
        BankedLegendaryKeys   = 9015,
        LumAugDurationCount   = 9016,
        LumAugSpecializeCount = 9017,
        LumAugSummonCount     = 9018,
        BankedEnlightenedCoins = 9020,
        BankedMythicalKeys     = 9021,
        LumAugMeleeCount       = 9022,
        LumAugMissileCount     = 9023,
        LumAugMeleeDefenseCount = 9024,
        LumAugMissileDefenseCount = 9025,
        LumAugMagicDefenseCount = 9026,
        BankedWeaklyEnlightenedCoins = 9027,

        // 50000+ block (matches the PropertyInt/PropertyBool charm-system ids)
        /// <summary>Charm of the Triune Weave: each point grants +1 Creature, +1 Item, and +1 Life augmentation on top of the (capped) gem-bought counts.</summary>
        TriuneWeaveCount       = 50000,
        // School charm counters. Each adds to its own school's effective augmentation count at
        // bonus-application sites, which is how a player passes the 4,000 cap: the cap bounds what
        // luminance can buy, and a charm is a separate path bought with Prestige Coins. Purchases,
        // the caps themselves and progression gates all still read the raw LumAug counts.
        /// <summary>Charm of the Battlemage's Wrath (War).</summary>
        BattlemagesWrathCharmCount = 50001,
        /// <summary>Charm of the Nether Veil (Void).</summary>
        NetherVeilCharmCount    = 50002,
        /// <summary>Charm of Crashing Steel (Melee).</summary>
        CrashingSteelCharmCount = 50003,
        /// <summary>Charm of the True Shot (Missile).</summary>
        TrueShotCharmCount      = 50004,

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
