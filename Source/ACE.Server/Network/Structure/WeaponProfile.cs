using System;
using System.IO;

using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

namespace ACE.Server.Network.Structure
{
    /// <summary>
    /// Handles the info for weapon appraisal panel
    /// </summary>
    public class WeaponProfile
    {
        public WorldObject Weapon;

        public DamageType DamageType;
        public uint WeaponTime;             // the weapon speed
        public Skill WeaponSkill;
        public uint Damage;                 // max damage
        public double DamageVariance;       // damage range %
        public double DamageMod;            // the damage modifier of the weapon
        public double WeaponLength;         // ??
        public double MaxVelocity;          // the power of the weapon (this affects range)
        public double WeaponOffense;        // the attack skill bonus of the weapon
        public double WeaponDefense;
        public uint MaxVelocityEstimated;   // ??

        public int Enchantment_WeaponTime;
        public int Enchantment_Damage;
        public double Enchantment_DamageVariance;
        public double Enchantment_DamageMod;
        public double Enchantment_WeaponOffense;

        public double Enchantment_WeaponDefense;    // gets sent elsewhere, calculating here for consistency

        public WeaponProfile(WorldObject weapon, Player examiner = null)
        {
            Weapon = weapon;

            WeaponDefense = GetWeaponDefense(weapon);

            if (weapon is Caster)
                return;

            DamageType = (DamageType)(weapon.GetProperty(PropertyInt.DamageType) ?? 0);
            //if (DamageType == 0)
                //Console.WriteLine($"Warning: WeaponProfile undefined damage type for {weapon.Name} ({weapon.Guid})");

            WeaponTime = GetWeaponSpeed(weapon);
            WeaponSkill = (Skill)(weapon.GetProperty(PropertyInt.WeaponSkill) ?? 0);
            Damage = GetDamage(weapon);
            DamageVariance = GetDamageVariance(weapon);

            // Weapon aug-scaling: fold the scaling term into the displayed range. WIELDED = the
            // wielder's live value; UNWIELDED = the EXAMINER's own value (owner 2026-08-03), so a
            // drop in a corpse or pack reads as what it would do in YOUR hands — floored at the
            // tier's wield-floor value, which no real wielder can be below. (Superseded: showing
            // every examiner the tier floor, owner 2026-08-01 — honest, but it made two drops
            // looted at different aug counts read identically and confused the owner repeatedly.)
            // In combat the term is a flat post-roll add (variance never touches it), so the
            // reported variance is re-derived to keep the displayed MIN true: min = staticMin +
            // term, max = staticMax + term — without this the client applies the weapon's
            // variance to the whole and understates min ~2x.
            var augTerm = weapon.Wielder is Player wielderPlayer
                ? (int)ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.GetFlatBonus(weapon, wielderPlayer)
                : (int)ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.GetExamineBonus(weapon, examiner);
            if (augTerm > 0)
            {
                if (ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.TryGetEffectiveVariance(weapon, out var vEff))
                {
                    // Scheme C: combat rolls the WHOLE envelope down from max by the quality-
                    // tightened family variance — display exactly that so examine = what you roll.
                    Damage = (uint)(Damage + augTerm);
                    Enchantment_Damage += augTerm;   // green "buffed" coloring client-side
                    DamageVariance = vEff;
                }
                else
                {
                    var trueMin = Damage * (1.0 - DamageVariance) + augTerm;
                    Damage = (uint)(Damage + augTerm);
                    Enchantment_Damage += augTerm;   // green "buffed" coloring client-side
                    DamageVariance = Damage > 0 ? Math.Max(0.0, 1.0 - trueMin / Damage) : 0.0;
                }
            }
            DamageMod = GetDamageMultiplier(weapon, examiner);
            WeaponLength = weapon.GetProperty(PropertyFloat.WeaponLength) ?? 1.0f;
            MaxVelocity = weapon.MaximumVelocity ?? 1.0f;
            WeaponOffense = GetWeaponOffense(weapon);
            //MaxVelocityEstimated = (uint)Math.Round(MaxVelocity);   // not found in pcaps?
        }

        /// <summary>
        /// Returns the weapon max damage, with enchantments factored in
        /// </summary>
        public uint GetDamage(WorldObject weapon)
        {
            var baseDamage = weapon.GetProperty(PropertyInt.Damage) ?? 0;
            var damageBonus = weapon.EnchantmentManager.GetDamageBonus();
            var auraDamageBonus = weapon.Wielder != null && (weapon.WeenieType != WeenieType.Ammunition || ServerConfig.show_ammo_buff.Value) ? weapon.Wielder.EnchantmentManager.GetDamageBonus() : 0;
            Enchantment_Damage = weapon.IsEnchantable ? damageBonus + auraDamageBonus : damageBonus;
            // (the aug-scaling term is folded in by the ctor AFTER variance is known, so the
            // displayed min can be corrected to the true post-roll value)
            return (uint)Math.Max(0, baseDamage + Enchantment_Damage);
        }

        /// <summary>
        /// Returns the weapon speed, with enchantments factored in
        /// </summary>
        public uint GetWeaponSpeed(WorldObject weapon)
        {
            var baseSpeed = weapon.GetProperty(PropertyInt.WeaponTime) ?? 0;   // safe to assume defaults here?
            var speedMod = weapon.EnchantmentManager.GetWeaponSpeedMod();
            var auraSpeedMod = weapon.Wielder != null ? weapon.Wielder.EnchantmentManager.GetWeaponSpeedMod() : 0;
            Enchantment_WeaponTime = weapon.IsEnchantable ? speedMod + auraSpeedMod : speedMod;
            return (uint)Math.Max(0, baseSpeed + Enchantment_WeaponTime);
        }

        /// <summary>
        /// Returns the weapon damage variance, with enchantments factored in
        /// </summary>
        public float GetDamageVariance(WorldObject weapon)
        {
            // are there any spells which modify damage variance?
            var baseVariance = weapon.GetProperty(PropertyFloat.DamageVariance) ?? 0.0f;   // safe to assume defaults here?
            var varianceMod = weapon.EnchantmentManager.GetVarianceMod();
            var auraVarianceMod = weapon.Wielder != null ? weapon.Wielder.EnchantmentManager.GetVarianceMod() : 1.0f;
            Enchantment_DamageVariance = weapon.IsEnchantable ? varianceMod * auraVarianceMod : varianceMod;
            return (float)(baseVariance * Enchantment_DamageVariance);
        }

        /// <summary>
        /// Returns the weapon damage multiplier, with enchantments factored in
        /// </summary>
        public float GetDamageMultiplier(WorldObject weapon, Player examiner = null)
        {
            // Weapon aug-scaling: stamped T11+ launchers display the quality-GRADED, TIER-SCALED
            // damage modifier (same resolver combat uses — replace semantics, authored value
            // fallback). WIELDED reads the wielder's augs; UNWIELDED reads the examiner's, which
            // is what finally makes two different-tier bows appraise differently — on 2026-08-06
            // the owner compared a T11 and a T13 bow, saw identical +300% panels, and reasonably
            // concluded the weapons were identical.
            var holder = (weapon.Wielder as Player) ?? examiner;
            var baseMultiplier = ACE.Server.Managers.WeaponScaling.WeaponScalingCombat.TryGetLauncherDamageMod(weapon, holder, out var gradedMod)
                ? gradedMod
                : weapon.GetProperty(PropertyFloat.DamageMod) ?? 1.0f;
            var damageMod = weapon.EnchantmentManager.GetDamageMod();
            var auraDamageMod = weapon.Wielder != null ? weapon.Wielder.EnchantmentManager.GetDamageMod() : 0.0f;
            Enchantment_DamageMod = weapon.IsEnchantable ? damageMod + auraDamageMod : damageMod;
            return (float)(baseMultiplier + Enchantment_DamageMod);
        }

        /// <summary>
        /// Returns the attack bonus %, with enchantments factored in
        /// </summary>
        public float GetWeaponOffense(WorldObject weapon)
        {
            if (weapon is Ammunition) return 1.0f;

            var baseOffense = weapon.GetProperty(PropertyFloat.WeaponOffense) ?? 1.0f;
            var offenseMod = !weapon.IsRanged ? weapon.EnchantmentManager.GetAttackMod(): 0.0f;
            var auraOffenseMod = weapon.Wielder != null && !weapon.IsRanged ? weapon.Wielder.EnchantmentManager.GetAttackMod() : 0.0f;
            Enchantment_WeaponOffense = weapon.IsEnchantable ? offenseMod + auraOffenseMod : offenseMod;
            return (float)(baseOffense + Enchantment_WeaponOffense);
        }

        /// <summary>
        /// Returns the defense bonus %, with enchantments factored in
        /// </summary>
        public float GetWeaponDefense(WorldObject weapon)
        {
            if (weapon is Ammunition) return 1.0f;

            var baseDefense = weapon.GetProperty(PropertyFloat.WeaponDefense) ?? 1.0f;

            // TODO: Resolve this issue a better way?
            // Because of the way ACE handles default base values in recipe system (or rather the lack thereof)
            // we need to check the following weapon properties to see if they're below expected minimum and adjust accordingly
            // The issue is that the recipe system likely added 0.01 to 0 instead of 1, which is what *should* have happened.
            if (weapon.WeaponDefense.HasValue && weapon.WeaponDefense.Value > 0 && weapon.WeaponDefense.Value < 1 && ((weapon.GetProperty(PropertyInt.ImbueStackingBits) ?? 0) & 4) != 0)
                baseDefense += 1;

            var defenseMod = weapon.EnchantmentManager.GetDefenseMod();
            var auraDefenseMod = weapon.Wielder != null ? weapon.Wielder.EnchantmentManager.GetDefenseMod() : 0.0f;
            Enchantment_WeaponDefense = weapon.IsEnchantable ? defenseMod + auraDefenseMod : defenseMod;
            return (float)(baseDefense + Enchantment_WeaponDefense);
        }
    }

    public static class WeaponProfileExtensions
    {
        /// <summary>
        /// Writes the weapon appraisal info to the network stream
        /// </summary>
        public static void Write(this BinaryWriter writer, WeaponProfile profile)
        {
            writer.Write((uint)profile.DamageType);
            writer.Write(profile.WeaponTime);
            writer.Write((uint)profile.WeaponSkill);
            writer.Write(profile.Damage);
            writer.Write(profile.DamageVariance);
            writer.Write(profile.DamageMod);
            writer.Write(profile.WeaponLength);
            writer.Write(profile.MaxVelocity);
            writer.Write(profile.WeaponOffense);
            writer.Write(profile.MaxVelocityEstimated);
        }
    }

}
