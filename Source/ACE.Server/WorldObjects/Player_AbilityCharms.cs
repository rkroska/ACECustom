using System;
using System.Collections.Generic;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    public struct AbilityCharmResult
    {
        public bool Applied;           // true = bonus was applied
        public uint StaminaSpent;
        public uint ManaSpent;
        public float DamageMultiplier; // 1.0 = no bonus applied
    }

    public partial class Player
    {
        // ── Heavy Swing / Heavy Draw tuning ──────────────────────────────────────────
        // Stamina cost: % of MAX stamina spent per hit (e.g. 0.20 = 20%)
        public const float HeavySwing_StaminaCostPct   = 0.20f;
        public const float HeavyDraw_StaminaCostPct    = 0.20f;

        // Stamina floor: % of MAX stamina. If current stamina is AT OR BELOW this value, skip bonus entirely
        // (stamina not spent, no bonus damage applied)
        public const float HeavySwing_StaminaFloorPct  = 0.10f;
        public const float HeavyDraw_StaminaFloorPct   = 0.10f;

        // Damage multiplier: 2.0 = 100% more damage = double damage
        public const float HeavySwing_DamageMultiplier = 2.0f;
        public const float HeavyDraw_DamageMultiplier  = 2.0f;

        // Regen penalty applied to the PLAYER's stamina regeneration while charm is active
        // 0.50 = stamina regen ticks at 50% of normal rate
        public const float HeavySwing_RegenPenaltyPct  = 0.50f;
        public const float HeavyDraw_RegenPenaltyPct   = 0.50f;
        
        // Focused Casting tuning
        public const float FocusedCasting_ManaCostPct     = 0.10f;
        public const float FocusedCasting_ManaFloorPct    = 0.10f;
        public const float FocusedCasting_DamageMultiplier = 2.0f;
        public const float FocusedCasting_RegenPenaltyPct = 0.50f;

        // Blood Letting tuning
        public const float BloodLetting_HealthCostPct      = 0.10f;
        public const float BloodLetting_HealthFloorPct     = 0.10f;
        public const float BloodLetting_DamageMultiplier   = 2.0f;
        public const float BloodLetting_RegenPenaltyPct    = 0.50f;

        public float GetCharmDamageMultiplier(float baseMultiplier)
        {
            var level = ActiveCharmLevel ?? 1;
            if (level <= 1) return baseMultiplier;

            // +0.5x multiplier per level (2.0 -> 2.5 -> 3.0)
            return baseMultiplier + (level - 1) * 0.5f;
        }

        public float GetCharmCostPct(float baseCost)
        {
            var level = ActiveCharmLevel ?? 1;
            if (level <= 1) return baseCost;

            // -2% cost per level (10% -> 8% -> 6%)
            return Math.Max(0.02f, baseCost - (level - 1) * 0.02f);
        }

        public AbilityCharmResult TryApplyHeavySwing()
        {
            var result = new AbilityCharmResult { DamageMultiplier = 1.0f };
            if (!HasHeavySwing) return result;

            var level = ActiveCharmLevel ?? 1;

            // Floor check — skip entirely if at or below threshold
            var floor = (uint)(Stamina.MaxValue * HeavySwing_StaminaFloorPct);
            if (Stamina.Current <= floor) return result;

            // How much to spend — cost is % of MAX stamina
            var cost = (uint)(Stamina.MaxValue * GetCharmCostPct(HeavySwing_StaminaCostPct));
            
            // Clamped so we never dip below the floor
            cost = Math.Min(cost, (uint)Math.Max(0, (int)Stamina.Current - (int)floor));
            if (cost < 1) return result;

            UpdateVitalDelta(Stamina, -(int)cost);
            result.Applied          = true;
            result.StaminaSpent     = cost;
            result.DamageMultiplier = GetCharmDamageMultiplier(HeavySwing_DamageMultiplier);
            return result;
        }

        public AbilityCharmResult TryApplyHeavyDraw()
        {
            var result = new AbilityCharmResult { DamageMultiplier = 1.0f };
            if (!HasHeavyDraw) return result;

            // Floor check — skip entirely if at or below threshold
            var floor = (uint)(Stamina.MaxValue * HeavyDraw_StaminaFloorPct);
            if (Stamina.Current <= floor) return result;

            // How much to spend — cost is % of MAX stamina
            var cost = (uint)(Stamina.MaxValue * GetCharmCostPct(HeavyDraw_StaminaCostPct));
            
            // Clamped so we never dip below the floor
            cost = Math.Min(cost, (uint)Math.Max(0, (int)Stamina.Current - (int)floor));
            if (cost < 1) return result;

            UpdateVitalDelta(Stamina, -(int)cost);
            result.Applied          = true;
            result.StaminaSpent     = cost;
            result.DamageMultiplier = GetCharmDamageMultiplier(HeavyDraw_DamageMultiplier);
            return result;
        }

        public AbilityCharmResult TryApplyFocusedCasting()
        {
            var result = new AbilityCharmResult { DamageMultiplier = 1.0f };
            if (!HasFocusedCasting) return result;

            // Floor check — skip entirely if at or below threshold
            var floor = (uint)(Mana.MaxValue * FocusedCasting_ManaFloorPct);
            if (Mana.Current <= floor) return result;

            // How much to spend — cost is % of MAX mana
            var cost = (uint)(Mana.MaxValue * GetCharmCostPct(FocusedCasting_ManaCostPct));
            
            // Clamped so we never dip below the floor
            cost = Math.Min(cost, (uint)Math.Max(0, (int)Mana.Current - (int)floor));
            if (cost < 1) return result;

            UpdateVitalDelta(Mana, -(int)cost);
            result.Applied          = true;
            result.ManaSpent        = cost;
            result.DamageMultiplier = GetCharmDamageMultiplier(FocusedCasting_DamageMultiplier);
            return result;
        }

        public AbilityCharmResult TryApplyBloodLetting()
        {
            var result = new AbilityCharmResult { DamageMultiplier = 1.0f };
            if (!HasBloodLetting) return result;

            // Floor check — skip entirely if at or below threshold
            var floor = (uint)(Health.MaxValue * BloodLetting_HealthFloorPct);
            if (Health.Current <= floor) return result;

            // How much to spend — cost is % of MAX health
            var cost = (uint)(Health.MaxValue * GetCharmCostPct(BloodLetting_HealthCostPct));
            
            // Clamped so we never dip below the floor
            cost = Math.Min(cost, (uint)Math.Max(0, (int)Health.Current - (int)floor));
            if (cost < 1) return result;

            UpdateVitalDelta(Health, -(int)cost);
            result.Applied          = true;
            // Note: AbilityCharmResult doesn't have HealthSpent, but we can reuse Applied + DamageMultiplier
            result.DamageMultiplier = GetCharmDamageMultiplier(BloodLetting_DamageMultiplier);

            // Visual effect - Disabled sound-heavy script (Red health down)
            // EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.HealthDownRed));

            return result;
        }
    }
}
