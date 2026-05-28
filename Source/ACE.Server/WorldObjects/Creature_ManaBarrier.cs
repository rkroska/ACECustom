using System;
using ACE.Entity.Enum;

namespace ACE.Server.WorldObjects
{
    /// <summary>Stores the result of a Mana Barrier absorption attempt.</summary>
    public struct ManaBarrierResult
    {
        public uint AmountAbsorbed;
        public uint ManaSpent;
        public bool FullyAbsorbed;
    }

    public partial class Creature
    {
        // Mana Barrier ratios live in CharmSettingsManager.ManaBarrier.
        // ratio  = mana cost per 1 damage (global, all elements)
        // t1/t2/t3 = damage absorbed per 1 mana (tier divisor — higher = more efficient)
        // Edit at runtime: /charm manabarrier ratio <val>  |  /charm manabarrier t1/t2/t3 <val>

        // ── Shared damage number formatter ─────────────────────────────────────
        /// <summary>
        /// Formats a damage value based on the player's DamageNumberFormat setting.
        /// 0 = vanilla (no commas), 1 = commas, 2 = short K/M/B/T/Q (exact with commas below 10K)
        /// </summary>
        public static string FormatDamage(ulong value, int mode)
        {
            if (mode == 0)                                 return $"{value}";
            if (mode == 1)                                 return $"{value:N0}";
            // mode 2: short — exact with commas below 10K, abbreviated above
            if (value < 10_000)                            return $"{value:N0}";
            if (value >= 1_000_000_000_000_000UL)          return $"{value / 1_000_000_000_000_000.0:0.#}Q";
            if (value >= 1_000_000_000_000UL)              return $"{value / 1_000_000_000_000.0:0.#}T";
            if (value >= 1_000_000_000UL)                  return $"{value / 1_000_000_000.0:0.#}B";
            if (value >= 1_000_000UL)                      return $"{value / 1_000_000.0:0.#}M";
            return $"{value / 1_000.0:0.#}K";
        }

        public virtual float GetManaBarrierRatioMod()
        {
            // Monsters use a fixed 1:1 ratio (1 mana per 1 damage absorbed)
            return 1.0f;
        }

        public string GetManaShieldSuffix(ManaBarrierResult result, bool isSpell = false)
        {
            if (result.AmountAbsorbed == 0) return "";
            var cur = Mana?.Current ?? 0;
            var max = Mana?.MaxValue ?? 0;
            return $" [Barrier Remaining: {cur:N0}/{max:N0}]";
        }

        public ManaBarrierResult TryAbsorbWithManaBarrier(ref float amount, DamageType damageType)
        {
            var result = new ManaBarrierResult();

            // Global kill-switch — /charm manabarrier false|off disables the charm server-wide
            if (!CharmSettingsManager.ManaBarrier.Enabled)
                return result;

            if (!HasManaBarrier || amount <= 0 || Mana == null || Mana.Current <= 0)
                return result;

            // Read the single global ratio from CharmSettingsManager (tunable via /charm manabarrier ratio <value>)
            var ratio = CharmSettingsManager.ManaBarrier.Ratio;

            // Apply tier scaling: t = damage absorbed per mana, so divide to get mana cost per damage
            ratio /= GetManaBarrierRatioMod();

            // Ratio of 0.0 means bypass
            if (ratio <= 0)
                return result;

            // Calculate potential absorption
            float damageToAbsorb = amount;
            uint manaRequired = (uint)Math.Ceiling(damageToAbsorb * ratio);

            if (manaRequired > Mana.Current)
            {
                // Partial absorption based on available mana
                manaRequired = (uint)Mana.Current;
                damageToAbsorb = manaRequired / ratio;
            }

            if (damageToAbsorb > 0)
            {
                result.AmountAbsorbed = (uint)Math.Round(damageToAbsorb);
                result.ManaSpent = manaRequired;

                UpdateVitalDelta(Mana, (int)-result.ManaSpent);
                amount -= damageToAbsorb;

                if (Math.Round(amount) <= 0)
                {
                    amount = 0;
                    result.FullyAbsorbed = true;
                }
            }

            return result;
        }

        // Overload for uint (physical combat uses uint)
        public ManaBarrierResult TryAbsorbWithManaBarrier(ref uint amount, DamageType damageType)
        {
            float fAmount = amount;
            var result = TryAbsorbWithManaBarrier(ref fAmount, damageType);
            amount = (uint)Math.Round(fAmount);
            return result;
        }
    }
}
