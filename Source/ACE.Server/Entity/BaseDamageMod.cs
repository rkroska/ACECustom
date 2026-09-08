using System;

using ACE.Entity.Enum.Properties;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    public class BaseDamageMod
    {
        public BaseDamage BaseDamage;

        public float DamageBonus   = 0.0f;    // blood drinker
        public float DamageMod     = 1.0f;    // for missile launchers (+113% yumis = 2.13)
        public float VarianceMod   = 1.0f;

        public int ElementalBonus = 0;

        public float MaxDamage
        {
            get
            {
                var maxDamage = (BaseDamage.MaxDamage + DamageBonus + ElementalBonus) * DamageMod;

                if (BaseDamage.MaxDamage >= 0)
                    maxDamage = Math.Max(0, maxDamage);
                else
                    maxDamage = Math.Min(0, maxDamage);

                return maxDamage;   
            }
        }

        public float MinDamage => MaxDamage * (1.0f - BaseDamage.Variance * VarianceMod);

        public Range Range => new(MinDamage, MaxDamage);

        public BaseDamageMod(BaseDamage baseDamage)
        {
            BaseDamage = baseDamage;
        }

        public BaseDamageMod(BaseDamage baseDamage, Creature wielder, WorldObject weapon)
        {
            BaseDamage = baseDamage;

            if (weapon == null)
                return;

            DamageBonus += weapon.EnchantmentManager.GetDamageBonus();
            VarianceMod *= weapon.EnchantmentManager.GetVarianceMod();

            // Weapon aug-scaling: a stamped T11+ launcher's quality roll GRADES the damage
            // modifier, then the weapon's TIER scales it by however many tier steps the wielder's
            // item augs have actually unlocked (replace semantics — launchers scale through the
            // mod, never a flat term); authored DamageMod is the fallback whenever the system is
            // off or the launcher is unstamped legacy.
            // (zone lock: outside authored areas the graded launcher mod is suppressed - the
            // authored DamageMod fallback IS the base-stats behaviour the lock lands on)
            var baseDamageMod = !Managers.ZoneControl.ZoneControlManager.WeaponPowerSuppressed(weapon, wielder)
                    && Managers.WeaponScaling.WeaponScalingCombat.TryGetLauncherDamageMod(weapon, wielder as Player, out var gradedMod)
                ? gradedMod
                : (float)(weapon.GetProperty(PropertyFloat.DamageMod) ?? 1.0f);

            DamageMod = baseDamageMod + weapon.EnchantmentManager.GetDamageMod();

            if (weapon.IsEnchantable)
            {
                // factor in wielder auras for enchantable weapons
                DamageBonus += wielder.EnchantmentManager.GetDamageBonus();
                VarianceMod *= wielder.EnchantmentManager.GetVarianceMod();

                DamageMod += wielder.EnchantmentManager.GetDamageMod();
            }

        }
    }
}
