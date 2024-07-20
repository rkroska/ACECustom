using System.ComponentModel;

namespace ACE.Entity.Enum.Properties
{
    // properties marked as ServerOnly are properties we never saw in PCAPs, from here:
    // http://ac.yotesfan.com/ace_object/not_used_enums.php
    // source: @OptimShi
    // description attributes are used by the weenie editor for a cleaner display name
    public enum PropertyInt64 : ushort
    {
        Undef               = 0,
        [SendOnLogin]
        TotalExperience     = 1,
        [SendOnLogin]
        AvailableExperience = 2,
        AugmentationCost    = 3,
        ItemTotalXp         = 4,
        ItemBaseXp          = 5,
        [SendOnLogin]
        AvailableLuminance  = 6,
        [SendOnLogin]
        MaximumLuminance    = 7,
        InteractionReqs     = 8,

        /* custom */
        [ServerOnly]
        AllegianceXPCached    = 9000,
        [ServerOnly]
        AllegianceXPGenerated = 9001,
        [ServerOnly]
        AllegianceXPReceived  = 9002,
        [ServerOnly]
        VerifyXp              = 9003,

        [ServerOnly]
        BankedPyreals         = 9004,
        [ServerOnly]
        BankedLuminance       = 9005,
        [ServerOnly]
        QuestCount            = 9006,
        [ServerOnly]
        LumAugCreatureCount   = 9007,
        [ServerOnly]
        LumAugItemCount       = 9008,
        [ServerOnly]
        LumAugLifeCount       = 9009,
        [ServerOnly]
        LumAugVoidCount       = 9010,
        [ServerOnly]
        LumAugWarCount        = 9011,

        [ServerOnly]
        AllegianceLumCached   = 9012,
        [ServerOnly]
        AllegianceLumGenerated= 9013,
        [ServerOnly]
        AllegianceLumReceived = 9014,
        [ServerOnly]
        BankedLegendaryKeys   = 9015,
        [ServerOnly]
        LumAugDurationCount   = 9016,
        [ServerOnly]
        LumAugSpecializeCount = 9017,
        [ServerOnly]
        LumAugSummonCount     = 9018,
        [ServerOnly]
        BankedEnlightenedCoins = 9020,
        [ServerOnly]
        BankedMythicalKeys     = 9021,
        [ServerOnly]
        LumAugMeleeCount       = 9022,
        [ServerOnly]
        LumAugMissileCount     = 9023,
    }

    public static class PropertyInt64Extensions
    {
        public static string GetDescription(this PropertyInt64 prop)
        {
            var description = prop.GetAttributeOfType<DescriptionAttribute>();
            return description?.Description ?? prop.ToString();
        }
    }
}
