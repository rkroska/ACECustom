using System;
using System.Collections.Generic;
using ACE.Database.Models.World;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class ManaStoneWcids
    {
        private static readonly ChanceTable<WeenieClassName> T1_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.manastoneminor,   0.75f ),
            ( WeenieClassName.manastonelesser,  0.25f ),
        };

        private static readonly ChanceTable<WeenieClassName> T2_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.manastoneminor,   0.25f ),
            ( WeenieClassName.manastonelesser,  0.50f ),
            ( WeenieClassName.manastone,        0.25f ),
        };

        private static readonly ChanceTable<WeenieClassName> T3_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.manastonelesser,  0.25f ),
            ( WeenieClassName.manastone,        0.50f ),
            ( WeenieClassName.manastonemedium,  0.25f ),
        };

        private static readonly ChanceTable<WeenieClassName> T4_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.manastone,        0.25f ),
            ( WeenieClassName.manastonemedium,  0.50f ),
            ( WeenieClassName.manastonegreater, 0.25f ),
        };

        private static readonly ChanceTable<WeenieClassName> T5_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.manastonemedium,  0.25f ),
            ( WeenieClassName.manastonegreater, 0.50f ),
            ( WeenieClassName.manastonemajor,   0.25f ),
        };

        private static readonly ChanceTable<WeenieClassName> T6_T8_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.manastonegreater, 0.25f ),
            ( WeenieClassName.manastonemajor,   0.75f ),
        };

        private static readonly ChanceTable<WeenieClassName> T9_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.manastonegreater, 0.25f ),
            ( WeenieClassName.manastonemajor,   0.75f ),
        };

        private static readonly ChanceTable<WeenieClassName> T10_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.manastonemassive, 0.25f ),
            ( WeenieClassName.manastonegreater,   0.75f ),
        };

        private static readonly List<ChanceTable<WeenieClassName>> manaStoneTiers = new List<ChanceTable<WeenieClassName>>()
        {
            T1_Chances,
            T2_Chances,
            T3_Chances,
            T4_Chances,
            T5_Chances,
            T6_T8_Chances,
            T6_T8_Chances,
            T6_T8_Chances,
            T9_Chances,
            T10_Chances
        };

        public static WeenieClassName Roll(TreasureDeath profile)
        {
            var tier = Math.Clamp(profile.Tier, 1, 10);
            // todo: verify t7 / t8 chances
            var table = manaStoneTiers[tier - 1];

            return table.Roll(profile.LootQualityMod);
        }
    }
}
