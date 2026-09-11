using System;

using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers.Rifts
{
    /// <summary>
    /// Rift monster scaling. Deliberately self-contained: mimics the prestige approach (geometric max-health
    /// growth + additive DamageRating) but reads only <see cref="RiftConfig"/> — no prestige tiers, clamps or
    /// server-config properties are consulted. Applied at spawn on top of whatever the retail/Zone-Control
    /// spawn path already did (rift variations are negative, so prestige scaling never engages).
    /// </summary>
    public static class RiftScaling
    {
        public static double GetHPModifier(RiftConfig cfg, int tier)
        {
            if (tier <= 0) return 1.0;
            return Math.Pow(cfg.HpGrowth, tier);
        }

        public static int GetDamageRating(RiftConfig cfg, int tier)
        {
            if (tier <= 0) return 0;
            return (int)Math.Round(cfg.DamageRatingPerTier * tier);
        }

        /// <summary>
        /// Scale a freshly-spawned rift monster (per-instance biota — despawn naturally reverts; instance
        /// landblocks are discarded whole when the run ends).
        /// </summary>
        public static void Apply(Creature creature, RiftRun run, RiftConfig cfg, bool isGuardian = false)
        {
            if (creature == null || creature is Player)
                return;

            // A weenie-level ForceEndgameSystems flag would make GetEffectiveVariation report a forced
            // prestige variation instead of the run's negative one, splitting this mob off from the rift's
            // zone resolution and live combat reads. Strip it for this instance.
            if (creature.GetProperty(PropertyBool.ForceEndgameSystems) == true)
                creature.RemoveProperty(PropertyBool.ForceEndgameSystems);

            var hpMod = GetHPModifier(cfg, run.Tier);
            if (isGuardian)
                hpMod *= Math.Max(1.0, cfg.GuardianHpMult);

            if (hpMod != 1.0)
            {
                var maxBefore = creature.Health.MaxValue;
                var healthPct = maxBefore > 0 ? (float)creature.Health.Current / maxBefore : 1f;
                creature.Health.StartingValue = (uint)Math.Clamp(Math.Round(creature.Health.StartingValue * hpMod), 1, uint.MaxValue);
                var maxAfter = creature.Health.MaxValue;
                creature.Health.Current = (uint)Math.Clamp((uint)Math.Round(healthPct * maxAfter), 0u, maxAfter);
            }

            var rating = GetDamageRating(cfg, run.Tier);
            if (isGuardian)
                rating += (int)Math.Round(cfg.GuardianDamageRatingBonus);

            if (rating != 0)
            {
                var existing = creature.GetProperty(PropertyInt.DamageRating) ?? 0;
                creature.SetProperty(PropertyInt.DamageRating, existing + rating);
            }
        }
    }
}
