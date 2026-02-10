using System;
using ACE.Server.Managers;

namespace ACE.Server.WorldObjects
{
    public static class SkillCheck
    {
        public static double GetSkillChance(int skill, int difficulty, float factor = 0.03f)
        {
            // Determine defense type and check if scaling is enabled for it
            bool scalingEnabled = false;
            double aggression = 1.0;
            
            if (Math.Abs(factor - 0.07f) < 0.001f)
            {
                // Magic defense (factor = 0.07)
                scalingEnabled = ServerConfig.defense_scaling_magic_enabled.Value;
                aggression = ServerConfig.defense_scaling_magic_agg.Value;
            }
            else if (Math.Abs(factor - 0.03f) < 0.001f)
            {
                // Melee/Missile defense (factor = 0.03)
                // Note: Can't distinguish between melee and missile, they share the same factor
                // Use melee settings as both should be configured the same
                scalingEnabled = ServerConfig.defense_scaling_melee_enabled.Value;
                aggression = ServerConfig.defense_scaling_melee_agg.Value;
            }
            
            // Apply dynamic factor scaling if enabled for this defense type
            if (scalingEnabled)
            {
                var avgSkill = (skill + difficulty) / 2.0;
                var scalingFactor = Math.Pow(500.0 / Math.Max(500.0, avgSkill), aggression);
                factor = (float)(factor * scalingFactor);
            }
            
            // Original sigmoid formula (now with potentially modified factor)
            var chance = 1.0 - (1.0 / (1.0 + Math.Exp(factor * (skill - difficulty))));

            return Math.Min(1.0, Math.Max(0.0, chance));
        }

        public static double GetSkillChance(uint skill, uint difficulty, float factor = 0.03f)
        {
            return GetSkillChance((int)skill, (int)difficulty, factor);
        }

        public static double GetMagicSkillChance(int skill, int difficulty)
        {
            return GetSkillChance(skill, difficulty, 0.07f);
        }
    }
}
