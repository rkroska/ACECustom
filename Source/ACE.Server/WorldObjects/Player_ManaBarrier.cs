using System;
using System.Collections.Generic;
using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    public struct ManaBarrierResult
    {
        public uint AmountAbsorbed;
        public uint ManaSpent;
        public bool FullyAbsorbed;
    }

    public partial class Player
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

        public string GetManaBarrierSuffix(ManaBarrierResult result)
        {
            if (result.AmountAbsorbed == 0) return "";
            return $" (Mana Barrier absorbed {result.AmountAbsorbed})";
        }

        public float GetManaBarrierRatioMod()
        {
            // Absorption efficiency per tier:
            // Level 1: 1:1   (1 Mana per 1 Damage)   → ratio mod = 1.000
            // Level 2: 1.5:1 (1 Mana per 1.5 Damage) → ratio mod = 0.667
            // Level 3: 2:1   (1 Mana per 2 Damage)   → ratio mod = 0.500
            return (ActiveCharmLevel ?? 1) switch
            {
                1 => 1.000f,
                2 => 0.667f,
                3 => 0.500f,
                _ => 1.000f
            };
        }

        public ManaBarrierResult TryAbsorbWithManaBarrier(ref float amount, DamageType damageType)
        {
            var result = new ManaBarrierResult();

            if (!HasManaBarrier || amount <= 0 || Mana.Current <= 0)
                return result;

            // Look up ratio, default to 1:1 if not found
            if (!ManaBarrierRatios.TryGetValue(damageType, out var ratio))
                ratio = 1.0f;

            // Apply level-based scaling
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
