using System;

namespace ACE.Server.Entity
{
    /// <summary>
    /// Pure math for pet potency / bond offense gate / bond strain (planned feature).
    /// Defaults match PET_POTENCY_AND_STRAIN.md; ServerConfig wiring lands with implementation.
    /// </summary>
    public static class PetPotencyMath
    {
        public const int DefaultActiveCap = 150;
        public const int DefaultBondDivisor = 10;
        public const int DefaultBondOffenseMinActive = 1;
        public const double DefaultDamagePerLevelPercent = 2.0;
        public const long DefaultCostBase = 10;
        public const double DefaultCostExponent = 1.0;
        public const int DefaultStrainThreshold = 50;
        public const double DefaultStrainPerPotencyLevel = 1.0;

        /// <summary>ceil(bond / divisor) with soft-start min when stored potency &gt; 0.</summary>
        public static int GetBondOffenseCap(
            int bondLevel,
            int bondDivisor = DefaultBondDivisor,
            int minActiveWhenStored = DefaultBondOffenseMinActive,
            bool hasStoredPotency = false,
            int activeCap = DefaultActiveCap)
        {
            if (bondLevel <= 0)
                return 0;

            if (bondDivisor < 1)
                bondDivisor = 1;

            var cap = (bondLevel + bondDivisor - 1) / bondDivisor;

            if (hasStoredPotency && bondLevel >= 1)
                cap = Math.Max(cap, minActiveWhenStored);

            if (activeCap > 0)
                cap = Math.Min(cap, activeCap);

            return cap;
        }

        public static int GetActivePotency(
            int storedPotency,
            int bondLevel,
            bool potencyEnabled = true,
            int bondDivisor = DefaultBondDivisor,
            int minActiveWhenStored = DefaultBondOffenseMinActive,
            int activeCap = DefaultActiveCap)
        {
            if (!potencyEnabled || storedPotency <= 0)
                return 0;

            var bondCap = GetBondOffenseCap(
                bondLevel,
                bondDivisor,
                minActiveWhenStored,
                hasStoredPotency: true,
                activeCap: activeCap);

            return Math.Min(storedPotency, bondCap);
        }

        public static int GetDormantPotency(int storedPotency, int activePotency)
        {
            if (storedPotency <= activePotency)
                return 0;
            return storedPotency - activePotency;
        }

        public static long GetUpgradeCost(
            int currentStored,
            long costBase = DefaultCostBase,
            double costExponent = DefaultCostExponent)
        {
            if (costBase < 0)
                costBase = 0;
            if (costExponent < 0)
                costExponent = 0;

            return (long)Math.Round(costBase * Math.Pow(currentStored + 1, costExponent));
        }

        public static long GetTotalUpgradeCost(
            int targetStored,
            long costBase = DefaultCostBase,
            double costExponent = DefaultCostExponent)
        {
            if (targetStored <= 0)
                return 0;

            long total = 0;
            for (var level = 0; level < targetStored; level++)
                total += GetUpgradeCost(level, costBase, costExponent);
            return total;
        }

        /// <summary>Body-part damage multiplier from active potency (+2%/level default).</summary>
        public static float GetBodyPartDamageMult(int activePotency, double damagePerLevelPercent = DefaultDamagePerLevelPercent)
        {
            if (activePotency <= 0 || damagePerLevelPercent <= 0)
                return 1.0f;

            return (float)(1.0 + activePotency * (damagePerLevelPercent / 100.0));
        }

        /// <summary>Scale an observed potency-0 hit to a projected hit at active potency (matchup preserved).</summary>
        public static int ProjectObservedHit(int baselineHitAtPotencyZero, int activePotency, double damagePerLevelPercent = DefaultDamagePerLevelPercent)
        {
            if (baselineHitAtPotencyZero <= 0)
                return 0;

            var mult = GetBodyPartDamageMult(activePotency, damagePerLevelPercent);
            return (int)Math.Round(baselineHitAtPotencyZero * mult, MidpointRounding.AwayFromZero);
        }

        public static int GetBondStrainRating(
            int activePotency,
            bool strainEnabled = false,
            int strainThreshold = DefaultStrainThreshold,
            double strainPerPotencyLevel = DefaultStrainPerPotencyLevel,
            int strainMaxRating = 0)
        {
            if (!strainEnabled || activePotency <= strainThreshold)
                return 0;

            var strain = (int)Math.Round((activePotency - strainThreshold) * strainPerPotencyLevel);
            if (strainMaxRating > 0)
                strain = Math.Min(strain, strainMaxRating);
            return Math.Max(0, strain);
        }

        /// <summary>Expected residue per kill: share × tier amount × multipliers.</summary>
        public static double GetExpectedResiduePerKill(
            double petDamageShare,
            double tierBaseAmount,
            double dropChanceMult = 1.0,
            double globalMult = 1.0)
        {
            petDamageShare = Math.Clamp(petDamageShare, 0.0, 1.0);
            if (tierBaseAmount < 0)
                tierBaseAmount = 0;
            if (dropChanceMult < 0)
                dropChanceMult = 0;
            if (globalMult < 0)
                globalMult = 0;

            return petDamageShare * tierBaseAmount * dropChanceMult * globalMult;
        }

        /// <summary>
        /// Converts fractional expected Savage Echo to an integer award (e.g. 2.4 → 2, then +1 with 40% chance).
        /// Pass <paramref name="randomUnit"/> in [0,1) for deterministic tests; omit for live RNG.
        /// </summary>
        public static int RoundResidueDropAmount(double expectedAmount, double? randomUnit = null)
        {
            if (expectedAmount <= 0)
                return 0;

            if (!double.IsFinite(expectedAmount))
                return expectedAmount > 0 ? int.MaxValue : 0;

            var d = (decimal)expectedAmount;
            var whole = (int)Math.Min(int.MaxValue, Math.Floor(d));
            var fraction = (double)(d - whole);
            if (fraction > 0 && whole < int.MaxValue)
            {
                var roll = randomUnit ?? Random.Shared.NextDouble();
                if (roll < fraction)
                    whole++;
            }

            return whole;
        }

        /// <summary>
        /// Expected Savage Echo from salvaging a spare captured essence.
        /// Siphoned and Hollow both yield <paramref name="salvageBase"/> echoes (flat, no level scaling).
        /// Shiny applies <paramref name="shinyMult"/> on top of the base.
        /// If <paramref name="creatureOverride"/> is &gt; 0 it replaces the base entirely
        /// (shiny multiplier still applies); used for per-creature DB overrides.
        /// </summary>
        public static double GetSalvageExpectedAmount(
            bool isShiny,
            long salvageBase = 1,
            double shinyMult = 10.0,
            int creatureOverride = 0)
        {
            if (salvageBase < 0) salvageBase = 0;
            if (shinyMult < 0) shinyMult = 0;

            var raw = creatureOverride > 0 ? (double)creatureOverride : (double)salvageBase;
            var expected = isShiny ? raw * shinyMult : raw;
            return Math.Max(0, expected);
        }
    }
}
