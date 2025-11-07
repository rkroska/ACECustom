using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.WorldObjects.Managers;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Summonable monsters combat AI
    /// </summary>
    public partial class CombatPet : Pet
    {
        // Store augmentation bonuses directly (not as enchantments)
        private float _itemAugAttackMod = 0f;
        private float _itemAugDefenseMod = 0f;
        private int _itemAugDamageBonus = 0;
        private int _itemAugArmorBonus = 0;
        private float _lifeAugProtectionRating = 0f;

        // Store all imbued effects from PetDevice (gem)
        private ImbuedEffectType _gemImbuedEffects = ImbuedEffectType.Undef;

        // Public getters for debug command
        public float ItemAugAttackMod => _itemAugAttackMod;
        public float ItemAugDefenseMod => _itemAugDefenseMod;
        public int ItemAugDamageBonus => _itemAugDamageBonus;
        public int ItemAugArmorBonus => _itemAugArmorBonus;
        public float LifeAugProtectionRating => _lifeAugProtectionRating;
        public ImbuedEffectType GemImbuedEffects => _gemImbuedEffects;

        /// <summary>
        /// Checks if the pet has a specific imbued effect from the gem
        /// </summary>
        public bool HasGemImbuedEffect(ImbuedEffectType type)
        {
            return _gemImbuedEffects.HasFlag(type);
        }

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public CombatPet(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public CombatPet(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
        }

        public override bool? Init(Player player, PetDevice petDevice)
        {
            var success = base.Init(player, petDevice);

            if (success == null || !success.Value)
                return success;

            SetCombatMode(CombatMode.Melee);
            MonsterState = State.Awake;
            IsAwake = true;

            // copy ratings from pet device
            if (petDevice != null)
            {
                DamageRating = petDevice.GearDamage;
                DamageResistRating = petDevice.GearDamageResist;
                CritDamageRating = petDevice.GearCritDamage;
                CritDamageResistRating = petDevice.GearCritDamageResist;
                CritRating = petDevice.GearCrit;
                CritResistRating = petDevice.GearCritResist;
            }

            // copy all augmentation counts from player (for damage scaling)
            LuminanceAugmentMeleeCount = player.LuminanceAugmentMeleeCount;
            LuminanceAugmentMissileCount = player.LuminanceAugmentMissileCount;
            LuminanceAugmentWarCount = player.LuminanceAugmentWarCount;
            LuminanceAugmentVoidCount = player.LuminanceAugmentVoidCount;

            // Apply summoning augmentation bonuses: +1 to all attributes and +1 to all skills per augmentation level
            var augCount = (int)(player.LuminanceAugmentSummonCount ?? 0);
            if (augCount > 0)
            {
                // Apply +1 to all 6 attributes per augmentation level (Base and Current)
                // Modifying StartingValue affects both Base (Ranks + StartingValue) and Current (derived from Base)
                var allAttributes = new[] 
                { 
                    PropertyAttribute.Strength, 
                    PropertyAttribute.Endurance, 
                    PropertyAttribute.Coordination, 
                    PropertyAttribute.Quickness, 
                    PropertyAttribute.Focus, 
                    PropertyAttribute.Self 
                };

                foreach (var attr in allAttributes)
                {
                    var creatureAttr = Attributes[attr];
                    creatureAttr.StartingValue = (uint)(creatureAttr.StartingValue + augCount);
                }

                // Apply +1 to all valid skills per augmentation level (InitLevel)
                foreach (var skill in SkillHelper.ValidSkills)
                {
                    var creatureSkill = GetCreatureSkill(skill);
                    if (creatureSkill != null)
                    {
                        creatureSkill.InitLevel = (uint)(creatureSkill.InitLevel + augCount);
                    }
                }
            }

            // Store item augmentation bonuses directly (not as enchantments)
            var itemAugCount = (long)(player.LuminanceAugmentItemCount ?? 0);
            if (itemAugCount > 0)
            {
                // Calculate weapon attack/defense mod: +0.20 base + (I × scaling factor)
                var itemAugPercentage = GetItemAugPercentageRating(itemAugCount);
                _itemAugAttackMod = 0.20f + itemAugPercentage;
                _itemAugDefenseMod = 0.20f + itemAugPercentage;

                // Calculate base damage: +20 × (1 + I × scaling factor)
                _itemAugDamageBonus = (int)(20.0f * (1.0f + itemAugPercentage));

                // Calculate armor bonus: +200 base + (200 × scaling factor)
                // At level 1 item aug: +200 base, then scaling on top
                _itemAugArmorBonus = 200 + (int)(200.0f * itemAugPercentage);
            }

            // Store life augmentation protection rating directly (not as enchantments)
            var lifeAugCount = (long)(player.LuminanceAugmentLifeCount ?? 0);
            if (lifeAugCount > 0)
            {
                // Calculate protection rating using diminishing returns formula
                _lifeAugProtectionRating = GetLifeAugProtectRating(lifeAugCount);
            }

            // Apply all imbued effects from PetDevice (gem) to the summon's weapon or body
            // This includes armor rending and all damage-type rending (slash, pierce, bludgeon, fire, cold, acid, electric, nether)
            // Note: Defense imbues (Melee Defense, Missile Defense, Magic Defense) are excluded
            if (petDevice != null)
            {
                var gemImbuedEffects = petDevice.GetImbuedEffects();
                
                // Exclude defense imbues (Melee Defense, Missile Defense, Magic Defense)
                var excludedDefenseImbues = ImbuedEffectType.MeleeDefense | ImbuedEffectType.MissileDefense | ImbuedEffectType.MagicDefense;
                var filteredImbuedEffects = gemImbuedEffects & ~excludedDefenseImbues;
                
                _gemImbuedEffects = filteredImbuedEffects;

                if (filteredImbuedEffects != ImbuedEffectType.Undef)
                {
                    // Try to apply to weapon first
                    var weapon = GetEquippedMeleeWeapon();
                    if (weapon != null)
                    {
                        // Apply imbued effects to the weapon
                        ApplyImbuedEffectsToWorldObject(weapon, filteredImbuedEffects);
                    }
                    else
                    {
                        // No weapon - apply to the creature itself (for body parts)
                        ApplyImbuedEffectsToWorldObject(this, filteredImbuedEffects);
                    }
                }
            }

            // are CombatPets supposed to attack monsters that are in the same faction as the pet owner?
            // if not, there are a couple of different approaches to this
            // the easiest way for the code would be to simply set Faction1Bits for the CombatPet to match the pet owner's
            // however, retail pcaps did not contain Faction1Bits for CombatPets

            // doing this the easiest way for the code here, and just removing during appraisal
            Faction1Bits = player.Faction1Bits;

            // Ensure pet spawns at full health
            Health.Current = Health.MaxValue;
            Stamina.Current = Stamina.MaxValue;
            Mana.Current = Mana.MaxValue;

            return true;
        }

        public override void HandleFindTarget()
        {
            var creature = AttackTarget as Creature;

            if (creature == null || creature.IsDead || !IsVisibleTarget(creature))
                FindNextTarget();
        }

        public override bool FindNextTarget()
        {
            var nearbyMonsters = GetNearbyMonsters();
            if (nearbyMonsters.Count == 0)
            {
                //Console.WriteLine($"{Name}.FindNextTarget(): empty");
                return false;
            }

            // get nearest monster
            var nearest = BuildTargetDistance(nearbyMonsters, true);

            if (nearest[0].Distance > VisualAwarenessRangeSq)
            {
                //Console.WriteLine($"{Name}.FindNextTarget(): next object out-of-range (dist: {Math.Round(Math.Sqrt(nearest[0].Distance))})");
                return false;
            }

            AttackTarget = nearest[0].Target;

            //Console.WriteLine($"{Name}.FindNextTarget(): {AttackTarget.Name}");

            return true;
        }

        /// <summary>
        /// Returns a list of attackable monsters in this pet's visible targets
        /// </summary>
        public List<Creature> GetNearbyMonsters()
        {
            var monsters = new List<Creature>();
            var listOfCreatures = PhysicsObj.ObjMaint.GetVisibleTargetsValuesOfTypeCreature();

            foreach (var creature in listOfCreatures)
            {
                // why does this need to be in here?
                if (creature.IsDead || !creature.Attackable || creature.Visibility)
                {
                    //Console.WriteLine($"{Name}.GetNearbyMonsters(): refusing to add dead creature {creature.Name} ({creature.Guid})");
                    continue;
                }

                // combat pets do not aggro monsters belonging to the same faction as the pet owner?
                if (SameFaction(creature))
                {
                    // unless the pet owner or the pet is being retaliated against?
                    if (!creature.HasRetaliateTarget(P_PetOwner) && !creature.HasRetaliateTarget(this))
                        continue;
                }

                monsters.Add(creature);
            }

            return monsters;
        }

        public override void Sleep()
        {
            // pets dont really go to sleep, per say
            // they keep scanning for new targets,
            // which is the reverse of the current ACE jurassic park model

            return;  // empty by default
        }

        /// <summary>
        /// Override GetEffectiveAttackSkill (no changes to attack modifier - item augs don't affect attack mod)
        /// </summary>
        public override uint GetEffectiveAttackSkill()
        {
            var creatureSkill = GetCreatureSkill(GetCurrentAttackSkill());
            if (creatureSkill == null)
                return 0;

            var attackSkill = creatureSkill.Current;

            // Get the base offense mod (may or may not include wielder enchantments depending on enchantability)
            // GetWeaponOffenseModifier is a static method in WorldObject_Weapon (partial class of WorldObject)
            var offenseMod = WorldObject.GetWeaponOffenseModifier(this);

            return (uint)Math.Round(attackSkill * offenseMod);
        }

        /// <summary>
        /// Override GetEffectiveDefenseSkill to ALWAYS apply item augmentation defense mod,
        /// regardless of whether the weapon is enchantable
        /// </summary>
        public override uint GetEffectiveDefenseSkill(CombatType combatType)
        {
            var defenseSkill = combatType == CombatType.Missile ? Skill.MissileDefense : Skill.MeleeDefense;
            var defenseMod = defenseSkill == Skill.MissileDefense ? GetWeaponMissileDefenseModifier(this) : GetWeaponMeleeDefenseModifier(this);
            var burdenMod = GetBurdenMod();

            var imbuedEffectType = defenseSkill == Skill.MissileDefense ? ImbuedEffectType.MissileDefense : ImbuedEffectType.MeleeDefense;
            var defenseImbues = GetDefenseImbues(imbuedEffectType);

            var stanceMod = 1.0f; // Combat pets don't have stance mods

            // ALWAYS apply item augmentation defense mod directly, regardless of weapon type or enchantability
            // Only apply for melee defense (missile defense uses GetWeaponMissileDefenseModifier which may have different logic)
            if (combatType == CombatType.Melee)
            {
                defenseMod += _itemAugDefenseMod;
            }

            var effectiveDefense = (uint)Math.Round(GetCreatureSkill(defenseSkill).Current * defenseMod * burdenMod * stanceMod + defenseImbues);

            if (IsExhausted) effectiveDefense = 0;

            return effectiveDefense;
        }

        /// <summary>
        /// Override GetBaseDamage to ALWAYS apply item augmentation bonuses,
        /// regardless of whether the summon has a weapon or if the weapon is enchantable
        /// </summary>
        public new BaseDamageMod GetBaseDamage(PropertiesBodyPart attackPart)
        {
            if (CurrentAttack == CombatType.Missile && GetMissileAmmo() != null)
                return GetMissileDamage();

            BaseDamageMod baseDamageMod;

            // use weapon damage for every attack?
            var weapon = GetEquippedMeleeWeapon();
            if (weapon != null)
            {
                // Get weapon damage mod (may or may not include wielder enchantments depending on enchantability)
                baseDamageMod = weapon.GetDamageMod(this);
            }
            else
            {
                // Body part damage
                if (attackPart == null)
                {
                    // Fallback to default damage if attackPart is null
                    var baseDamage = new BaseDamage(0, 0.0f);
                    baseDamageMod = new BaseDamageMod(baseDamage);
                }
                else
                {
                    var maxDamage = attackPart.DVal;
                    var variance = attackPart.DVar;

                    var baseDamage = new BaseDamage(maxDamage, variance);
                    baseDamageMod = new BaseDamageMod(baseDamage);
                }
            }

            // ALWAYS apply item augmentation damage bonus directly, regardless of weapon type or enchantability
            baseDamageMod.DamageBonus += _itemAugDamageBonus;

            // Melee/Missile/War/Void augs are stored on the pet at Init() and automatically applied:
            // - Melee/Missile augs: Applied by DamageEvent.DoCalculateDamage() which checks LuminanceAugmentMeleeCount/MissileCount
            // - War/Void augs: Applied by SpellProjectile.cs which checks LuminanceAugmentWarCount/VoidCount

            return baseDamageMod;
        }

        /// <summary>
        /// Override GetResistanceMod to apply life augmentation protection rating directly
        /// </summary>
        public new float GetResistanceMod(DamageType damageType, WorldObject attacker, WorldObject weapon, float weaponResistanceMod = 1.0f)
        {
            // Get existing protection mod from enchantments
            var existingProtectionMod = EnchantmentManager.GetProtectionResistanceMod(damageType);
            
            // Apply life augmentation protection rating
            if (_lifeAugProtectionRating > 0f)
            {
                var lifeAugProtectionMod = 1.0f - _lifeAugProtectionRating;
                
                // If no existing protection (null/1.0), use 1.0 as base then apply life aug
                // Otherwise, combine existing protection with life aug (multiplicative)
                if (existingProtectionMod >= 1.0f)
                {
                    // No existing protection - use 1.0 as base, then apply life aug
                    existingProtectionMod = lifeAugProtectionMod;
                }
                else
                {
                    // Existing protection - combine with life aug (multiplicative)
                    existingProtectionMod *= lifeAugProtectionMod;
                }
            }
            
            // Get base resistance mod (this will use our modified protection mod)
            // We need to temporarily override the protection mod, then call base
            // Actually, we need to get the full base resistance mod and then apply our protection
            var ignoreMagicResist = (weapon?.IgnoreMagicResist ?? false) || (attacker?.IgnoreMagicResist ?? false);
            
            if (ignoreMagicResist)
            {
                if (!(attacker is Player) || !(this is Player) || PropertyManager.GetDouble("ignore_magic_resist_pvp_scalar").Item == 1.0)
                    return weaponResistanceMod;
            }
            
            var vulnMod = EnchantmentManager.GetVulnerabilityResistanceMod(damageType);
            var naturalResistMod = GetNaturalResistance(damageType);
            
            // Use our combined protection mod (existing + life aug)
            var protMod = existingProtectionMod;
            
            // protection mod becomes either life protection or natural resistance,
            // whichever is more powerful (more powerful = lower value here)
            if (protMod > naturalResistMod)
                protMod = naturalResistMod;
            
            // Apply vulnerability mod
            var resistanceMod = protMod * vulnMod * weaponResistanceMod;
            
            return resistanceMod;
        }

        /// <summary>
        /// Applies imbued effects from the gem to a WorldObject (weapon or creature)
        /// </summary>
        private static void ApplyImbuedEffectsToWorldObject(WorldObject target, ImbuedEffectType imbuedEffects)
        {
            if (target == null || imbuedEffects == ImbuedEffectType.Undef)
                return;

            // Get existing imbued effects
            var existingEffects = (ImbuedEffectType)(
                (target.GetProperty(PropertyInt.ImbuedEffect) ?? 0) |
                (target.GetProperty(PropertyInt.ImbuedEffect2) ?? 0) |
                (target.GetProperty(PropertyInt.ImbuedEffect3) ?? 0) |
                (target.GetProperty(PropertyInt.ImbuedEffect4) ?? 0) |
                (target.GetProperty(PropertyInt.ImbuedEffect5) ?? 0));

            // Combine with gem's imbued effects
            var combinedEffects = existingEffects | imbuedEffects;

            // Apply to the first available ImbuedEffect slot
            if (target.GetProperty(PropertyInt.ImbuedEffect) == null)
            {
                target.SetProperty(PropertyInt.ImbuedEffect, (int)combinedEffects);
            }
            else
            {
                // Combine with existing first slot
                var existingFirst = (ImbuedEffectType)(target.GetProperty(PropertyInt.ImbuedEffect) ?? 0);
                target.SetProperty(PropertyInt.ImbuedEffect, (int)(existingFirst | imbuedEffects));
            }

            // Set icon underlay if there's a primary imbue effect
            if (RecipeManager.IconUnderlay.TryGetValue(imbuedEffects, out var iconUnderlay))
            {
                target.IconUnderlayId = iconUnderlay;
            }
        }

        /// <summary>
        /// Gets the item augmentation percentage rating using the same scaling formula as players
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetItemAugPercentageRating(long itemAugAmt)
        {
            float bonus = 0;
            for (int x = 0; x < itemAugAmt; x++)
            {
                if (x < 100)
                {
                    bonus += 0.01f;
                }
                else if (x < 150)
                {
                    bonus += 0.0075f;
                }
                else if (x < 200)
                {
                    bonus += 0.005625f;
                }
                else if (x < 250)
                {
                    bonus += 0.004218f;
                }
                else if (x < 300)
                {
                    bonus += 0.003164f;
                }
                else if (x < 350)
                {
                    bonus += 0.002373f;
                }
                else if (x < 400)
                {
                    bonus += 0.001779f;
                }
                else if (x < 450)
                {
                    bonus += 0.001334f;
                }
                else
                {
                    bonus += 0.00100f;
                }
            }
            return bonus;
        }

        /// <summary>
        /// Gets the life augmentation protection rating using the same diminishing returns formula as players
        /// </summary>
        private static float GetLifeAugProtectRating(long lifeAugAmt)
        {
            float bonus = 0;
            for (int x = 0; x < lifeAugAmt; x++)
            {
                if (x < 10)
                {
                    bonus += 0.01f;
                }
                else if (x < 30)
                {
                    bonus += 0.005f;
                }
                else if (x < 50)
                {
                    bonus += 0.0025f;
                }
                else if (x < 70)
                {
                    bonus += 0.00125f;
                }
                else if (x < 100)
                {
                    bonus += 0.000625f;
                }
                else if (x < 120)
                {
                    bonus += 0.000312f;
                }
                else if (x < 150)
                {
                    bonus += 0.000156f;
                }
                else if (x < 175)
                {
                    bonus += 0.000078f;
                }
                else if (x < 200)
                {
                    bonus += 0.000039f;
                }
                else if (x < 225)
                {
                    bonus += 0.0000195f;
                }
                else
                {
                    bonus += 0.0000100f;
                }
            }
            return bonus;
        }
    }
}
