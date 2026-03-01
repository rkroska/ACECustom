using System;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    public static class SkillCheck
    {
        // ---------------------------------------------------------------
        // Core sigmoid formula – NO combat scaling applied here.
        // Non-combat callers (lockpicking, deception, healing, crafting,
        // mana conversion, magic defense) all use this directly.
        // ---------------------------------------------------------------
        public static double GetSkillChance(int skill, int difficulty, float factor = 0.03f)
        {
            var chance = 1.0 - (1.0 / (1.0 + Math.Exp(factor * (skill - difficulty))));
            return Math.Min(1.0, Math.Max(0.0, chance));
        }

        public static double GetSkillChance(uint skill, uint difficulty, float factor = 0.03f)
        {
            return GetSkillChance((int)skill, (int)difficulty, factor);
        }

        // ---------------------------------------------------------------
        // Magic combat – uses the wider 0.07 factor and magic scaling config.
        // ---------------------------------------------------------------
        public static double GetMagicSkillChance(int skill, int difficulty)
        {
            const float factor = 0.07f;
            bool scalingEnabled = ServerConfig.defense_scaling_magic_enabled.Value;
            double aggression = ServerConfig.defense_scaling_magic_agg.Value;
            return ApplyScalingAndCompute(skill, difficulty, factor, scalingEnabled, aggression);
        }

        // ---------------------------------------------------------------
        // Melee combat – uses melee scaling config.
        // ---------------------------------------------------------------
        public static double GetMeleeCombatSkillChance(int skill, int difficulty)
        {
            const float factor = 0.03f;
            bool scalingEnabled = ServerConfig.defense_scaling_melee_enabled.Value;
            double aggression = ServerConfig.defense_scaling_melee_agg.Value;
            return ApplyScalingAndCompute(skill, difficulty, factor, scalingEnabled, aggression);
        }

        public static double GetMeleeCombatSkillChance(uint skill, uint difficulty)
        {
            return GetMeleeCombatSkillChance((int)skill, (int)difficulty);
        }

        // ---------------------------------------------------------------
        // Missile combat – uses missile scaling config.
        // ---------------------------------------------------------------
        public static double GetMissileCombatSkillChance(int skill, int difficulty)
        {
            const float factor = 0.03f;
            bool scalingEnabled = ServerConfig.defense_scaling_missile_enabled.Value;
            double aggression = ServerConfig.defense_scaling_missile_agg.Value;
            return ApplyScalingAndCompute(skill, difficulty, factor, scalingEnabled, aggression);
        }

        public static double GetMissileCombatSkillChance(uint skill, uint difficulty)
        {
            return GetMissileCombatSkillChance((int)skill, (int)difficulty);
        }

        // ---------------------------------------------------------------
        // Shared scaling + sigmoid computation used by the combat helpers.
        // ---------------------------------------------------------------
        private static double ApplyScalingAndCompute(int skill, int difficulty, float factor, bool scalingEnabled, double aggression)
        {
            if (scalingEnabled)
            {
                var avgSkill = (skill + difficulty) / 2.0;
                var scalingFactor = Math.Pow(500.0 / Math.Max(500.0, avgSkill), aggression);
                factor = (float)(factor * scalingFactor);
            }

            var chance = 1.0 - (1.0 / (1.0 + Math.Exp(factor * (skill - difficulty))));
            return Math.Min(1.0, Math.Max(0.0, chance));
        }
    }
}
