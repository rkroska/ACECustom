using System;
using System.Collections.Generic;
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
        // Customizable absorption ratios: Costs X Mana per 1 Damage
        // Value of 1.0 = 1 Mana per 1 Damage (1:1)
        // Value of 0.5 = 1 Mana per 2 Damage (2:1)
        public static readonly Dictionary<DamageType, float> ManaBarrierRatios = new()
        {
            // Melee/Missile (Consistent 1:1)
            { DamageType.Slash,     1.0f },
            { DamageType.Pierce,    1.0f },
            { DamageType.Bludgeon,  1.0f },

            // Elemental/War Magic (Consistent 1:1)
            { DamageType.Fire,      1.0f },
            { DamageType.Cold,      1.0f },
            { DamageType.Acid,      1.0f },
            { DamageType.Electric,  1.0f },
            { DamageType.Health,    1.0f },

            // Specialized/Void (Consistent 1:1)
            { DamageType.Nether,    1.0f },
            { DamageType.Undef,     1.0f },
        };

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

            if (!HasManaBarrier || amount <= 0 || Mana == null || Mana.Current <= 0)
                return result;

            // Look up ratio, default to 1:1 if not found
            if (!ManaBarrierRatios.TryGetValue(damageType, out var ratio))
                ratio = 1.0f;

            // Apply level-based or property-based scaling
            ratio *= GetManaBarrierRatioMod();

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
