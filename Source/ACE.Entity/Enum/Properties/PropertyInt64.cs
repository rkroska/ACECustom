using System.ComponentModel;

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
    }
}
