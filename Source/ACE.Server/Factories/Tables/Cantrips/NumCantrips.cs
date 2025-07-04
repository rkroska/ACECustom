using ACE.Database.Models.World;
using ACE.Server.Factories.Entity;
using System;
using System.Collections.Generic;

namespace ACE.Server.Factories.Tables
{
    public static class NumCantrips
    {
        private static readonly ChanceTable<int> T1_NumCantrips = new ChanceTable<int>()
        {
            ( 0, 0.95f ),
            ( 1, 0.05f ),
        };

        private static readonly ChanceTable<int> T2_NumCantrips = new ChanceTable<int>()
        {
            ( 0, 0.90f ),
            ( 1, 0.10f ),
        };

        private static readonly ChanceTable<int> T3_NumCantrips = new ChanceTable<int>()
        {
            ( 0, 0.725f ),
            ( 1, 0.250f ),
            ( 2, 0.025f ),
        };

        private static readonly ChanceTable<int> T4_NumCantrips = new ChanceTable<int>()
        {
            ( 0, 0.62f ),
            ( 1, 0.32f ),
            ( 2, 0.055f ),
            ( 3, 0.005f ),
        };

        private static readonly ChanceTable<int> T5_NumCantrips = new ChanceTable<int>()
        {
            ( 0, 0.40f ),
            ( 1, 0.42f ),
            ( 2, 0.155f ),
            ( 3, 0.024f ),
            ( 4, 0.001f ),
        };

        private static readonly ChanceTable<int> T6_NumCantrips = new ChanceTable<int>()
        {
            ( 0, 0.25f ),
            ( 1, 0.40f ),
            ( 2, 0.25f ),
            ( 3, 0.08f ),
            ( 4, 0.019f ),
            ( 5, 0.001f ),
        };

        private static readonly ChanceTable<int> T7_T8_NumCantrips = new ChanceTable<int>()
        {
            ( 1, 0.81f ),
            ( 2, 0.17f ),
            ( 3, 0.016f ),
            ( 4, 0.004f ),
        };

        private static readonly ChanceTable<int> T9_NumCantrips = new ChanceTable<int>()
        {
            ( 1, 0.0f ),
            ( 2, 0.81f ),
            ( 3, 0.023f ),
            ( 4, 0.010f ),
        };

        private static readonly ChanceTable<int> T10_NumCantrips = new ChanceTable<int>()
        {
            ( 1, 0.0f ),
            ( 2, 0.70f ),
            ( 3, 0.18f ),
            ( 4, 0.12f ),
        };

        private static readonly List<ChanceTable<int>> numCantrips = new List<ChanceTable<int>>()
        {
            T1_NumCantrips,
            T2_NumCantrips,
            T3_NumCantrips,
            T4_NumCantrips,
            T5_NumCantrips,
            T6_NumCantrips,
            T7_T8_NumCantrips,
            T7_T8_NumCantrips,
            T9_NumCantrips,
            T10_NumCantrips
        };

        public static int RollNumCantrips(TreasureDeath profile)
        {
            var tier = Math.Clamp(profile.Tier, 1, 10);
            return numCantrips[tier - 1].Roll(profile.LootQualityMod);
        }

        private static readonly ChanceTable<int> T1_T2_CantripLevel = new ChanceTable<int>()
        {
            ( 1, 1.0f )
        };

        private static readonly ChanceTable<int> T3_CantripLevel = new ChanceTable<int>()
        {
            ( 1, 0.97f ),
            ( 2, 0.03f ),
        };

        private static readonly ChanceTable<int> T4_CantripLevel = new ChanceTable<int>()
        {
            ( 1, 0.90f ),
            ( 2, 0.10f ),
        };

        private static readonly ChanceTable<int> T5_CantripLevel = new ChanceTable<int>()
        {
            ( 1, 0.85f ),
            ( 2, 0.15f ),
        };

        private static readonly ChanceTable<int> T6_CantripLevel = new ChanceTable<int>()
        {
            ( 1, 0.80f ),
            ( 2, 0.20f ),
        };

        private static readonly ChanceTable<int> T7_CantripLevel = new ChanceTable<int>()
        {
            ( 1, 0.15f ),
            ( 2, 0.60f ),
            ( 3, 0.25f )
        };

        private static readonly ChanceTable<int> T8_CantripLevel = new ChanceTable<int>()
        {
            ( 1, 0.02f ),
            ( 2, 0.46f ),
            ( 3, 0.42f ),
            ( 4, 0.10f )
        };

        private static readonly ChanceTable<int> T9_CantripLevel = new ChanceTable<int>()
        {
            ( 1, 0.0f ),
            ( 2, 0.40f ),
            ( 3, 0.48f ),
            ( 4, 0.12f )
        };

        private static readonly ChanceTable<int> T10_CantripLevel = new ChanceTable<int>()
        {
            ( 1, 0.0f ),
            ( 2, 0.35f ),
            ( 3, 0.45f ),
            ( 4, 0.20f )
        };

        private static readonly List<ChanceTable<int>> cantripLevels = new List<ChanceTable<int>>()
        {
            T1_T2_CantripLevel,
            T1_T2_CantripLevel,
            T3_CantripLevel,
            T4_CantripLevel,
            T5_CantripLevel,
            T6_CantripLevel,
            T7_CantripLevel,
            T8_CantripLevel,
            T9_CantripLevel,
            T10_CantripLevel
        };

        public static int RollCantripLevel(TreasureDeath profile)
        {
            var tier = Math.Clamp(profile.Tier, 1, 10);
            return cantripLevels[tier - 1].Roll(profile.LootQualityMod);
        }
    }
}
