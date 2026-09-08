using System;
using System.Collections.Generic;
using ACE.Database.Models.World;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class CasterWcids
    {
        private static readonly ChanceTable<WeenieClassName> T1_T2_Chances = new ChanceTable<WeenieClassName>()
        {
            (WeenieClassName.orb,     0.25f ),
            (WeenieClassName.sceptre, 0.25f ),
            (WeenieClassName.staff,   0.25f ),
            (WeenieClassName.wand,    0.25f ),
        };

        private static readonly ChanceTable<WeenieClassName> T3_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.orb,                    0.17f ),
            ( WeenieClassName.sceptre,                0.17f ),
            ( WeenieClassName.staff,                  0.17f ),
            ( WeenieClassName.wand,                   0.17f ),
            ( WeenieClassName.wandslashing,           0.02f ),
            ( WeenieClassName.wandpiercing,           0.02f ),
            ( WeenieClassName.wandblunt,              0.02f ),
            ( WeenieClassName.wandacid,               0.02f ),
            ( WeenieClassName.wandfire,               0.02f ),
            ( WeenieClassName.wandfrost,              0.02f ),
            ( WeenieClassName.wandelectric,           0.02f ),
            ( WeenieClassName.ace43381_nethersceptre, 0.02f ),
            ( WeenieClassName.ace31819_slashingbaton, 0.02f ),
            ( WeenieClassName.ace31825_piercingbaton, 0.02f ),
            ( WeenieClassName.ace31821_bluntbaton,    0.02f ),
            ( WeenieClassName.ace31820_acidbaton,     0.02f ),
            ( WeenieClassName.ace31823_firebaton,     0.02f ),
            ( WeenieClassName.ace31824_frostbaton,    0.02f ),
            ( WeenieClassName.ace31822_electricbaton, 0.02f ),
            ( WeenieClassName.ace43382_netherbaton,   0.02f ),
        };

        private static readonly ChanceTable<WeenieClassName> T4_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.orb,                    0.13f ),
            ( WeenieClassName.sceptre,                0.13f ),
            ( WeenieClassName.staff,                  0.13f ),
            ( WeenieClassName.wand,                   0.13f ),
            ( WeenieClassName.wandslashing,           0.03f ),
            ( WeenieClassName.wandpiercing,           0.03f ),
            ( WeenieClassName.wandblunt,              0.03f ),
            ( WeenieClassName.wandacid,               0.03f ),
            ( WeenieClassName.wandfire,               0.03f ),
            ( WeenieClassName.wandfrost,              0.03f ),
            ( WeenieClassName.wandelectric,           0.03f ),
            ( WeenieClassName.ace43381_nethersceptre, 0.03f ),
            ( WeenieClassName.ace31819_slashingbaton, 0.03f ),
            ( WeenieClassName.ace31825_piercingbaton, 0.03f ),
            ( WeenieClassName.ace31821_bluntbaton,    0.03f ),
            ( WeenieClassName.ace31820_acidbaton,     0.03f ),
            ( WeenieClassName.ace31823_firebaton,     0.03f ),
            ( WeenieClassName.ace31824_frostbaton,    0.03f ),
            ( WeenieClassName.ace31822_electricbaton, 0.03f ),
            ( WeenieClassName.ace43382_netherbaton,   0.03f ),
        };

        private static readonly ChanceTable<WeenieClassName> T5_T6_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.orb,                    0.05f ),
            ( WeenieClassName.sceptre,                0.05f ),
            ( WeenieClassName.staff,                  0.05f ),
            ( WeenieClassName.wand,                   0.05f ),
            ( WeenieClassName.wandslashing,           0.05f ),
            ( WeenieClassName.wandpiercing,           0.05f ),
            ( WeenieClassName.wandblunt,              0.05f ),
            ( WeenieClassName.wandacid,               0.05f ),
            ( WeenieClassName.wandfire,               0.05f ),
            ( WeenieClassName.wandfrost,              0.05f ),
            ( WeenieClassName.wandelectric,           0.05f ),
            ( WeenieClassName.ace43381_nethersceptre, 0.05f ),
            ( WeenieClassName.ace31819_slashingbaton, 0.05f ),
            ( WeenieClassName.ace31825_piercingbaton, 0.05f ),
            ( WeenieClassName.ace31821_bluntbaton,    0.05f ),
            ( WeenieClassName.ace31820_acidbaton,     0.05f ),
            ( WeenieClassName.ace31823_firebaton,     0.05f ),
            ( WeenieClassName.ace31824_frostbaton,    0.05f ),
            ( WeenieClassName.ace31822_electricbaton, 0.05f ),
            ( WeenieClassName.ace43382_netherbaton,   0.05f ),
        };

        private static readonly ChanceTable<WeenieClassName> T7_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.orb,                    0.04f ),
            ( WeenieClassName.sceptre,                0.04f ),
            ( WeenieClassName.staff,                  0.04f ),
            ( WeenieClassName.wand,                   0.04f ),
            ( WeenieClassName.wandslashing,           0.045f ),
            ( WeenieClassName.wandpiercing,           0.045f ),
            ( WeenieClassName.wandblunt,              0.045f ),
            ( WeenieClassName.wandacid,               0.045f ),
            ( WeenieClassName.wandfire,               0.045f ),
            ( WeenieClassName.wandfrost,              0.045f ),
            ( WeenieClassName.wandelectric,           0.045f ),
            ( WeenieClassName.ace43381_nethersceptre, 0.045f ),
            ( WeenieClassName.ace31819_slashingbaton, 0.045f ),
            ( WeenieClassName.ace31825_piercingbaton, 0.045f ),
            ( WeenieClassName.ace31821_bluntbaton,    0.045f ),
            ( WeenieClassName.ace31820_acidbaton,     0.045f ),
            ( WeenieClassName.ace31823_firebaton,     0.045f ),
            ( WeenieClassName.ace31824_frostbaton,    0.045f ),
            ( WeenieClassName.ace31822_electricbaton, 0.045f ),
            ( WeenieClassName.ace43382_netherbaton,   0.045f ),
            ( WeenieClassName.ace37223_slashingstaff, 0.015f ),
            ( WeenieClassName.ace37222_piercingstaff, 0.015f ),
            ( WeenieClassName.ace37225_bluntstaff,    0.015f ),
            ( WeenieClassName.ace37224_acidstaff,     0.015f ),
            ( WeenieClassName.ace37220_firestaff,     0.015f ),
            ( WeenieClassName.ace37221_froststaff,    0.015f ),
            ( WeenieClassName.ace37219_electricstaff, 0.015f ),
            ( WeenieClassName.ace43383_netherstaff,   0.015f ),
        };

        private static readonly ChanceTable<WeenieClassName> T8_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.orb,                    0.036f ),
            ( WeenieClassName.sceptre,                0.036f ),
            ( WeenieClassName.staff,                  0.036f ),
            ( WeenieClassName.wand,                   0.036f ),
            ( WeenieClassName.wandslashing,           0.036f ),
            ( WeenieClassName.wandpiercing,           0.036f ),
            ( WeenieClassName.wandblunt,              0.036f ),
            ( WeenieClassName.wandacid,               0.036f ),
            ( WeenieClassName.wandfire,               0.036f ),
            ( WeenieClassName.wandfrost,              0.036f ),
            ( WeenieClassName.wandelectric,           0.036f ),
            ( WeenieClassName.ace43381_nethersceptre, 0.036f ),
            ( WeenieClassName.ace31819_slashingbaton, 0.036f ),
            ( WeenieClassName.ace31825_piercingbaton, 0.036f ),
            ( WeenieClassName.ace31821_bluntbaton,    0.036f ),
            ( WeenieClassName.ace31820_acidbaton,     0.036f ),
            ( WeenieClassName.ace31823_firebaton,     0.036f ),
            ( WeenieClassName.ace31824_frostbaton,    0.036f ),
            ( WeenieClassName.ace31822_electricbaton, 0.036f ),
            ( WeenieClassName.ace43382_netherbaton,   0.036f ),
            ( WeenieClassName.ace37223_slashingstaff, 0.035f ),
            ( WeenieClassName.ace37222_piercingstaff, 0.035f ),
            ( WeenieClassName.ace37225_bluntstaff,    0.035f ),
            ( WeenieClassName.ace37224_acidstaff,     0.035f ),
            ( WeenieClassName.ace37220_firestaff,     0.035f ),
            ( WeenieClassName.ace37221_froststaff,    0.035f ),
            ( WeenieClassName.ace37219_electricstaff, 0.035f ),
            ( WeenieClassName.ace43383_netherstaff,   0.035f ),
        };

        private static readonly ChanceTable<WeenieClassName> T9_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.orb,                    0.036f ),
            ( WeenieClassName.sceptre,                0.036f ),
            ( WeenieClassName.staff,                  0.036f ),
            ( WeenieClassName.wand,                   0.036f ),
            ( WeenieClassName.wandslashing,           0.036f ),
            ( WeenieClassName.wandpiercing,           0.036f ),
            ( WeenieClassName.wandblunt,              0.036f ),
            ( WeenieClassName.wandacid,               0.036f ),
            ( WeenieClassName.wandfire,               0.036f ),
            ( WeenieClassName.wandfrost,              0.036f ),
            ( WeenieClassName.wandelectric,           0.036f ),
            ( WeenieClassName.ace43381_nethersceptre, 0.036f ),
            ( WeenieClassName.ace31819_slashingbaton, 0.036f ),
            ( WeenieClassName.ace31825_piercingbaton, 0.036f ),
            ( WeenieClassName.ace31821_bluntbaton,    0.036f ),
            ( WeenieClassName.ace31820_acidbaton,     0.036f ),
            ( WeenieClassName.ace31823_firebaton,     0.036f ),
            ( WeenieClassName.ace31824_frostbaton,    0.036f ),
            ( WeenieClassName.ace31822_electricbaton, 0.036f ),
            ( WeenieClassName.ace43382_netherbaton,   0.036f ),
            ( WeenieClassName.ace37223_slashingstaff, 0.035f ),
            ( WeenieClassName.ace37222_piercingstaff, 0.035f ),
            ( WeenieClassName.ace37225_bluntstaff,    0.035f ),
            ( WeenieClassName.ace37224_acidstaff,     0.035f ),
            ( WeenieClassName.ace37220_firestaff,     0.035f ),
            ( WeenieClassName.ace37221_froststaff,    0.035f ),
            ( WeenieClassName.ace37219_electricstaff, 0.035f ),
            ( WeenieClassName.ace43383_netherstaff,   0.035f ),
        };

        // T10 is the LAST authored row, so it is what every T11-T25 zone actually rolls: Roll() clamps the
        // tier to 1-10 and TierTable.Entry clamps again to the table length. Everything endgame sees is here.
        //
        // NO PLAIN CASTERS (owner 2026-08-26, "remove all plain orbs and wands from our loot table").
        // orb / sceptre / staff / wand (2366 / 2548 / 2547 / 2472) carry no W_DamageType at all, so
        // ZoneLootMutator's Rending card finds an empty candidate pool and they can NEVER take a rend -
        // they drop at T11+ as strictly worse versions of the elemental casters below. Only this T10 row
        // was touched; T1-T9 keep their plain casters, which is the low-tier levelling content and is
        // unreachable from T11+ by the clamp above.
        //
        // WEIGHTS: ChanceTable.VerifyTable logs an error unless the table sums to 1.0, and a short table
        // does NOT throw - Roll() falls through its loop and returns the LAST entry with chance > 0, so a
        // 0.144 gap would have silently dumped 14.4 pct of all caster drops onto netherstaff. The removed
        // 0.144 is therefore redistributed in the original 0.036 : 0.035 proportion, rounded to values that
        // sum EXACTLY to 1.0 in the decimal arithmetic VerifyTable uses: the sixteen wands/batons go
        // 0.036 -> 0.042 (16 x 0.042 = 0.672) and the eight staves 0.035 -> 0.041 (8 x 0.041 = 0.328).
        private static readonly ChanceTable<WeenieClassName> T10_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.wandslashing,           0.042f ),
            ( WeenieClassName.wandpiercing,           0.042f ),
            ( WeenieClassName.wandblunt,              0.042f ),
            ( WeenieClassName.wandacid,               0.042f ),
            ( WeenieClassName.wandfire,               0.042f ),
            ( WeenieClassName.wandfrost,              0.042f ),
            ( WeenieClassName.wandelectric,           0.042f ),
            ( WeenieClassName.ace43381_nethersceptre, 0.042f ),
            ( WeenieClassName.ace31819_slashingbaton, 0.042f ),
            ( WeenieClassName.ace31825_piercingbaton, 0.042f ),
            ( WeenieClassName.ace31821_bluntbaton,    0.042f ),
            ( WeenieClassName.ace31820_acidbaton,     0.042f ),
            ( WeenieClassName.ace31823_firebaton,     0.042f ),
            ( WeenieClassName.ace31824_frostbaton,    0.042f ),
            ( WeenieClassName.ace31822_electricbaton, 0.042f ),
            ( WeenieClassName.ace43382_netherbaton,   0.042f ),
            ( WeenieClassName.ace37223_slashingstaff, 0.041f ),
            ( WeenieClassName.ace37222_piercingstaff, 0.041f ),
            ( WeenieClassName.ace37225_bluntstaff,    0.041f ),
            ( WeenieClassName.ace37224_acidstaff,     0.041f ),
            ( WeenieClassName.ace37220_firestaff,     0.041f ),
            ( WeenieClassName.ace37221_froststaff,    0.041f ),
            ( WeenieClassName.ace37219_electricstaff, 0.041f ),
            ( WeenieClassName.ace43383_netherstaff,   0.041f ),
        };

        private static readonly List<ChanceTable<WeenieClassName>> casterTiers = new List<ChanceTable<WeenieClassName>>()
        {
            T1_T2_Chances,
            T1_T2_Chances,
            T3_Chances,
            T4_Chances,
            T5_T6_Chances,
            T5_T6_Chances,
            T7_Chances,
            T8_Chances,
            T9_Chances,
            T10_Chances,
        };

        public static WeenieClassName Roll(int tier)
        {
            tier = Math.Clamp(tier, 1, 10);
            return TierTable.Entry(casterTiers, tier).Roll();
        }

        private static readonly HashSet<WeenieClassName> _combined = new HashSet<WeenieClassName>();

        static CasterWcids()
        {
            foreach (var casterTier in casterTiers)
            {
                foreach (var entry in casterTier)
                    _combined.Add(entry.result);
            }
        }

        public static bool Contains(WeenieClassName wcid)
        {
            return _combined.Contains(wcid);
        }
    }
}
