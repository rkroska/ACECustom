using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Common;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects.Managers;

using log4net;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Summonable monsters combat AI
    /// </summary>
    public partial class CombatPet : Pet
    {
        private static readonly ILog RecallBlockDbgLog = LogManager.GetLogger(typeof(CombatPet));

        private WeakReference<PetDevice> _summoningDevice;
        private ObjectGuid _summoningDeviceGuid = ObjectGuid.Invalid;

        /// <summary>
        /// The PetDevice that summoned this CombatPet (best-effort, in-memory).
        /// </summary>
        public PetDevice TryGetSummoningDevice()
        {
            if (_summoningDevice == null)
                return null;

            _summoningDevice.TryGetTarget(out var device);
            return device;
        }

        /// <summary>
        /// The GUID of the PetDevice that summoned this CombatPet.
        /// </summary>
        public ObjectGuid SummoningDeviceGuid => _summoningDeviceGuid;

        /// <summary>
        /// Diminishing-returns damage multiplier from summon augs (1.0 = no extra mitigation).
        /// Shared math for spell-projectile and melee/missile paths.
        /// </summary>
        private float GetSummonAugMitigationMultiplier(bool enabled, double maxRedRaw, double scaleRaw)
        {
            if (!enabled)
                return 1.0f;

            var maxRed = (float)maxRedRaw;
            if (maxRed <= 0)
                return 1.0f;

            maxRed = Math.Clamp(maxRed, 0f, 0.999f);

            var scale = scaleRaw;
            if (scale <= 0)
                return 1.0f;

            var aug = LuminanceAugmentSummonCount ?? 0;
            if (aug <= 0)
                return 1.0f;

            var towardCap = 1.0 - Math.Exp(-aug / scale);
            var reduction = maxRed * towardCap;
            return (float)(1.0 - reduction);
        }

        /// <summary>
        /// Diminishing-returns mitigation multiplier from the bond level on the summoning essence.
        /// This is intended to help combat pets keep up with endgame creature damage without changing monster tuning.
        /// </summary>
        private float GetBondMitigationMultiplier(bool enabled, double maxRedRaw, double scaleRaw)
        {
            if (!enabled || !ServerConfig.pet_bond_enabled.Value)
                return 1.0f;

            var maxRed = (float)maxRedRaw;
            if (maxRed <= 0)
                return 1.0f;

            maxRed = Math.Clamp(maxRed, 0f, 0.999f);

            var scale = scaleRaw;
            if (scale <= 0)
                return 1.0f;

            var device = TryGetSummoningDevice();
            if (device == null || !device.IsCombatPetDevice() || !device.IsPetBondAttuned)
                return 1.0f;

            var capRaw = (int)ServerConfig.pet_bond_level_cap.Value;
            var cap = capRaw <= 0 ? int.MaxValue : Math.Max(1, capRaw);

            var bond = device.PetBondLevel ?? 0;
            if (bond < 0)
                bond = 0;
            if (bond > cap)
                bond = cap;

            if (bond <= 0)
                return 1.0f;

            var towardCap = 1.0 - Math.Exp(-bond / scale);
            var reduction = maxRed * towardCap;
            return (float)(1.0 - reduction);
        }

        /// <summary>
        /// Multiplier applied to harmful spell projectile damage (after normal resists/absorb).
        /// Uses <see cref="LuminanceAugmentSummonCount"/> (copied from owner at summon) with diminishing returns; asymptotic cap from ServerConfig.
        /// </summary>
        public float GetSpellProjectileDamageTakenMultiplier()
        {
            return GetSummonAugMitigationMultiplier(
                ServerConfig.pet_combat_summon_aug_spell_mitigation_enabled.Value,
                ServerConfig.pet_combat_summon_aug_spell_mitigation_max.Value,
                ServerConfig.pet_combat_summon_aug_spell_mitigation_scale.Value);
        }

        /// <summary>
        /// Multiplier applied to melee/missile damage computed via <see cref="ACE.Server.Entity.DamageEvent"/> (after armor/resist pipeline).
        /// Independent toggles from spell projectile mitigation.
        /// </summary>
        public float GetPhysicalDamageTakenMultiplier()
        {
            // Multiply independent mitigations: e.g. 30% bond mitigation + 30% aug mitigation => 0.7 * 0.7 = 0.49 (51% total).
            var augMult = GetSummonAugMitigationMultiplier(
                ServerConfig.pet_combat_summon_aug_physical_mitigation_enabled.Value,
                ServerConfig.pet_combat_summon_aug_physical_mitigation_max.Value,
                ServerConfig.pet_combat_summon_aug_physical_mitigation_scale.Value);

            var bondMult = GetBondMitigationMultiplier(
                ServerConfig.pet_bond_physical_mitigation_enabled.Value,
                ServerConfig.pet_bond_physical_mitigation_max.Value,
                ServerConfig.pet_bond_physical_mitigation_scale.Value);

            var globalMult = (float)ServerConfig.pet_combat_physical_damage_taken_multiplier.Value;
            if (globalMult <= 0 || float.IsNaN(globalMult) || float.IsInfinity(globalMult))
                globalMult = 1.0f;

            return augMult * bondMult * globalMult;
        }

        // Store augmentation bonuses directly (not as enchantments)
        private float _itemAugAttackMod = 0f;
        private float _itemAugDefenseMod = 0f;
        private int _itemAugDamageBonus = 0;
        private int _itemAugArmorBonus = 0;
        private float _lifeAugProtectionRating = 0f;

        // Store all imbued effects from PetDevice (gem)
        private ImbuedEffectType _gemImbuedEffects = ImbuedEffectType.Undef;
        
        // Track where we applied imbued effects so we can remove them on resummon
        private WorldObject _previousImbuedTarget = null;
        private ImbuedEffectType _previousImbuedEffects = ImbuedEffectType.Undef;

        /// <summary>Unix time until which owner leash / idle follow recall is suppressed (after taking damage).</summary>
        private double _ownerFollowRecallBlockedUntilUnix;

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
            _meleeMotionDpsFactor = 1f;
            RadarBehavior = ACE.Entity.Enum.RadarBehavior.ShowAlways;
        }

        /// <summary>
        /// Per-hit melee damage multiplier so expected melee DPS matches the summon weenie baseline
        /// (accounts for motion-table swing length and attack-frame count). See <see cref="ConfigureMeleeMotionDpsNormalization"/>.
        /// </summary>
        private float _meleeMotionDpsFactor = 1f;

        internal float MeleeMotionDpsFactor => _meleeMotionDpsFactor;

        public override void Destroy(bool raiseNotifyOfDestructionEvent = true, bool fromLandblockUnload = false)
        {
            // Clean up imbued effects before destroying the pet
            // This ensures effects are removed even if the pet is destroyed without being resummoned
            if (_previousImbuedTarget != null && _previousImbuedEffects != ImbuedEffectType.Undef)
            {
                RemoveImbuedEffectsFromWorldObject(_previousImbuedTarget, _previousImbuedEffects);
                _previousImbuedTarget = null;
                _previousImbuedEffects = ImbuedEffectType.Undef;
            }

            base.Destroy(raiseNotifyOfDestructionEvent, fromLandblockUnload);
        }

        public override bool? Init(Player player, PetDevice petDevice)
        {
            // Before Pet.Init -> EnterWorld: weenie defaults are creature/gold on radar; clients never get a later blip update unless we set this now so the create packet includes RadarBlipColor.
            this.RadarColor = ACE.Entity.Enum.RadarColor.Pink;

            var success = base.Init(player, petDevice);

            if (success == null || !success.Value)
                return success;

            // Track which PetDevice summoned this CombatPet for bond XP attribution.
            if (petDevice != null)
            {
                _summoningDeviceGuid = petDevice.Guid;
                _summoningDevice = new WeakReference<PetDevice>(petDevice);
            }

            // Clear cached augmentation and gem imbue state between summons
            // This prevents old values from persisting when resummoning with different augs/gems
            _itemAugAttackMod = 0f;
            _itemAugDefenseMod = 0f;
            _itemAugDamageBonus = 0;
            _itemAugArmorBonus = 0;
            _lifeAugProtectionRating = 0f;
            _gemImbuedEffects = ImbuedEffectType.Undef;
            
            // Remove previously applied imbued effects from the previous target
            if (_previousImbuedTarget != null && _previousImbuedEffects != ImbuedEffectType.Undef)
            {
                RemoveImbuedEffectsFromWorldObject(_previousImbuedTarget, _previousImbuedEffects);
                _previousImbuedTarget = null;
                _previousImbuedEffects = ImbuedEffectType.Undef;
            }

            SetCombatMode(CombatMode.Melee);
            MonsterState = State.Awake;
            IsAwake = true;

            var bondMaxHealthBonus = 0;

            // Copy ratings from the summoning essence (Gear* on PetDevice). Only overwrite each combat rating when
            // that Gear* is present; assigning null would RemoveProperty and wipe the creature weenie defaults.
            if (petDevice != null)
            {
                static int ScaleRating(int value, double multRaw)
                {
                    var mult = multRaw;
                    if (mult <= 0 || double.IsNaN(mult) || double.IsInfinity(mult))
                        mult = 1.0;
                    var scaled = (long)Math.Round(value * mult, MidpointRounding.AwayFromZero);
                    if (scaled > int.MaxValue) return int.MaxValue;
                    if (scaled < int.MinValue) return int.MinValue;
                    return (int)scaled;
                }

                if (petDevice.GearDamage.HasValue)
                    DamageRating = petDevice.GearDamage;
                if (petDevice.GearDamageResist.HasValue)
                    DamageResistRating = petDevice.GearDamageResist;
                if (petDevice.GearCritDamage.HasValue)
                    CritDamageRating = petDevice.GearCritDamage;
                if (petDevice.GearCritDamageResist.HasValue)
                    CritDamageResistRating = petDevice.GearCritDamageResist;
                if (petDevice.GearCrit.HasValue)
                    CritRating = petDevice.GearCrit;
                if (petDevice.GearCritResist.HasValue)
                    CritResistRating = petDevice.GearCritResist;

                if (ServerConfig.pet_bond_enabled.Value && petDevice.IsCombatPetDevice() && petDevice.IsPetBondAttuned)
                {
                    var cap = (int)ServerConfig.pet_bond_level_cap.Value;
                    if (cap < 1)
                        cap = 1;

                    var bondLevel = petDevice.PetBondLevel ?? 0;
                    if (bondLevel < 0)
                        bondLevel = 0;
                    if (bondLevel > cap)
                        bondLevel = cap;

                    PetDevice.GetBondCombatStatBonuses(bondLevel, cap, out var bondDr, out var bondCdr, out var bondD, out var bondCd, out bondMaxHealthBonus);

                    DamageResistRating = (DamageResistRating ?? 0) + bondDr;
                    CritDamageResistRating = (CritDamageResistRating ?? 0) + bondCdr;
                    DamageRating = (DamageRating ?? 0) + bondD;
                    CritDamageRating = (CritDamageRating ?? 0) + bondCd;
                }

                // Apply configurable rating multipliers at summon-time so tuning can be done live via /modifydouble + resummon.
                if (DamageRating.HasValue)
                    DamageRating = ScaleRating(DamageRating.Value, ServerConfig.pet_combat_rating_mult_damage.Value);
                if (DamageResistRating.HasValue)
                    DamageResistRating = ScaleRating(DamageResistRating.Value, ServerConfig.pet_combat_rating_mult_damage_resist.Value);
                if (CritDamageRating.HasValue)
                    CritDamageRating = ScaleRating(CritDamageRating.Value, ServerConfig.pet_combat_rating_mult_crit_damage.Value);
                if (CritDamageResistRating.HasValue)
                    CritDamageResistRating = ScaleRating(CritDamageResistRating.Value, ServerConfig.pet_combat_rating_mult_crit_damage_resist.Value);
                if (CritRating.HasValue)
                    CritRating = ScaleRating(CritRating.Value, ServerConfig.pet_combat_rating_mult_crit.Value);
                if (CritResistRating.HasValue)
                    CritResistRating = ScaleRating(CritResistRating.Value, ServerConfig.pet_combat_rating_mult_crit_resist.Value);
            }

            // copy augmentation counts from player (for damage scaling)
            // Each non-summon track is capped by summoning aug count: effective = min(summon, owner track).
            // With 0 summoning augs, all of these stay 0.
            var summonAugCount = (long)(player.LuminanceAugmentSummonCount ?? 0);

            LuminanceAugmentSummonCount = summonAugCount;

            LuminanceAugmentMeleeCount = Math.Min(summonAugCount, player.LuminanceAugmentMeleeCount ?? 0);
            LuminanceAugmentMissileCount = Math.Min(summonAugCount, player.LuminanceAugmentMissileCount ?? 0);
            LuminanceAugmentWarCount = Math.Min(summonAugCount, player.LuminanceAugmentWarCount ?? 0);
            LuminanceAugmentVoidCount = Math.Min(summonAugCount, player.LuminanceAugmentVoidCount ?? 0);
            // Match Creature_Combat.GetEffectiveDefenseSkill: flat +MeleeD/+MissileD luminance aug counts (capped like other tracks).
            LuminanceAugmentMeleeDefenseCount = Math.Min(summonAugCount, player.LuminanceAugmentMeleeDefenseCount ?? 0);
            LuminanceAugmentMissileDefenseCount = Math.Min(summonAugCount, player.LuminanceAugmentMissileDefenseCount ?? 0);

            // Apply summoning augmentation bonuses: +attributes / +skills scaled by luminance Summon aug count × pet_combat_summon_aug_benefit_multiplier (default 1 = +1 per aug).
            var benefitMult = Math.Clamp(ServerConfig.pet_combat_summon_aug_benefit_multiplier.Value, 0.0, 10.0);
            var augBonusPoints = (int)Math.Round(summonAugCount * benefitMult, MidpointRounding.AwayFromZero);
            if (augBonusPoints > 0)
            {
                // Apply to all 6 attributes (Base and Current via StartingValue).
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
                    creatureAttr.StartingValue = (uint)(creatureAttr.StartingValue + augBonusPoints);
                }

                foreach (var skill in SkillHelper.ValidSkills)
                {
                    var creatureSkill = GetCreatureSkill(skill);
                    if (creatureSkill != null)
                        creatureSkill.InitLevel = (uint)(creatureSkill.InitLevel + augBonusPoints);
                }
            }

            // Store item augmentation bonuses directly (not as enchantments), capped by summoning aug count.
            var itemAugCount = (long)(player.LuminanceAugmentItemCount ?? 0);
            var itemAugEffective = Math.Min(summonAugCount, itemAugCount);
            if (itemAugEffective > 0)
            {
                // Calculate weapon attack/defense mod: +0.20 base + (I × scaling factor)
                var itemAugPercentage = GetItemAugPercentageRating(itemAugEffective);
                _itemAugAttackMod = 0.20f + itemAugPercentage;
                _itemAugDefenseMod = 0.20f + itemAugPercentage;

                // Calculate base damage: +20 × (1 + I × scaling factor)
                _itemAugDamageBonus = (int)(20.0f * (1.0f + itemAugPercentage));

                // Synthetic effective AL (armor pipeline only): tunable toward player-like buffed AL without scaling pet offense.
                var legacyArmor = 200.0 + 200.0 * itemAugPercentage;
                var armorMult = Math.Max(0.0, ServerConfig.pet_combat_item_aug_synthetic_armor_multiplier.Value);
                var linearArmor = Math.Max(0.0, ServerConfig.pet_combat_item_aug_synthetic_armor_per_effective_aug.Value) * itemAugEffective;
                var combinedArmor = legacyArmor * armorMult + linearArmor;
                var armorCap = ServerConfig.pet_combat_item_aug_synthetic_armor_max.Value;
                if (armorCap > 0 && combinedArmor > armorCap)
                    combinedArmor = armorCap;

                combinedArmor = Math.Clamp(combinedArmor, 0.0, int.MaxValue);
                _itemAugArmorBonus = (int)Math.Round(combinedArmor);
            }
            // Note: If itemAugEffective is 0, _itemAug* fields remain at 0 (already reset above)

            // Combat pet missile weapons: ensure Split Arrows is disabled so the pet can only attack one target per shot.
            // Split arrows are implemented in Creature_Missile via the weapon's SplitArrows/SplitArrowCount properties.
            var missileWeapon = GetEquippedMissileWeapon();
            if (missileWeapon != null)
            {
                missileWeapon.SplitArrows = false;
                missileWeapon.SplitArrowCount = 0;
                missileWeapon.SplitArrowRange = 0.0;
                missileWeapon.SplitArrowDamageMultiplier = 1.0;
            }

            // Store life augmentation protection rating directly (not as enchantments), capped by summoning aug count.
            var lifeAugCount = (long)(player.LuminanceAugmentLifeCount ?? 0);
            var lifeAugEffective = Math.Min(summonAugCount, lifeAugCount);
            if (lifeAugEffective > 0)
            {
                // Calculate protection rating using diminishing returns formula
                _lifeAugProtectionRating = GetLifeAugProtectRating(lifeAugEffective);
            }
            // Note: If lifeAugEffective is 0, _lifeAugProtectionRating remains at 0 (already reset above)

            // Apply all imbued effects from PetDevice (gem) to the summon's weapon or body
            // This includes armor rending and all damage-type rending (slash, pierce, bludgeon, fire, cold, acid, electric, nether)
            // Note: Defense imbues (Melee Defense, Missile Defense, Magic Defense) are excluded
            if (petDevice != null)
            {
                var gemImbuedEffects = RecipeManager.GetImbuedEffects(petDevice);
                
                // Exclude defense imbues (Melee Defense, Missile Defense, Magic Defense)
                var excludedDefenseImbues = ImbuedEffectType.MeleeDefense | ImbuedEffectType.MissileDefense | ImbuedEffectType.MagicDefense;
                var filteredImbuedEffects = gemImbuedEffects & ~excludedDefenseImbues;
                
                _gemImbuedEffects = filteredImbuedEffects;

                if (filteredImbuedEffects != ImbuedEffectType.Undef)
                {
                    // Try to apply to weapon first
                    var weapon = GetEquippedMeleeWeapon();
                    WorldObject target = weapon ?? (WorldObject)this;
                    
                    // Apply imbued effects to the weapon or creature
                    ApplyImbuedEffectsToWorldObject(target, filteredImbuedEffects);
                    
                    // Track where we applied effects so we can remove them on resummon
                    _previousImbuedTarget = target;
                    _previousImbuedEffects = filteredImbuedEffects;
                }
                // Note: If filteredImbuedEffects is Undef, _gemImbuedEffects remains Undef (already reset above)
            }

            if (petDevice != null && ServerConfig.pet_apply_capture_source_damage_type.Value)
            {
                var capDt = petDevice.GetProperty(PropertyInt.CapturedSourceDamageType);
                if (capDt.HasValue && capDt.Value != 0 && Enum.IsDefined(typeof(DamageType), capDt.Value))
                {
                    var dt = (DamageType)capDt.Value;
                    var meleeWeapon = GetEquippedMeleeWeapon();
                    if (meleeWeapon != null)
                        meleeWeapon.SetProperty(PropertyInt.DamageType, (int)dt);
                    else
                        SetProperty(PropertyInt.DamageType, (int)dt);
                }
            }

            // are CombatPets supposed to attack monsters that are in the same faction as the pet owner?
            // if not, there are a couple of different approaches to this
            // the easiest way for the code would be to simply set Faction1Bits for the CombatPet to match the pet owner's
            // however, retail pcaps did not contain Faction1Bits for CombatPets

            ConfigureMeleeMotionDpsNormalization();

            // doing this the easiest way for the code here, and just removing during appraisal
            Faction1Bits = player.Faction1Bits;

            if (bondMaxHealthBonus > 0)
                Health.StartingValue = (uint)Math.Min(uint.MaxValue, (ulong)Health.StartingValue + (uint)bondMaxHealthBonus);

            if (ServerConfig.pet_combat_unlimited_lifespan.Value)
                TimeToRot = -1;
            else
            {
                var tr = TimeToRot;
                var perAug = ServerConfig.pet_summon_lifespan_seconds_per_aug.Value;
                var perDurationAug = ServerConfig.pet_combat_lifespan_seconds_per_duration_aug.Value;
                var durationAugEffective = Math.Min(summonAugCount, player.LuminanceAugmentSpellDurationCount ?? 0);

                if (tr.HasValue && tr.Value > 0)
                {
                    var extraSeconds = 0.0;
                    if (summonAugCount > 0 && perAug > 0)
                        extraSeconds += summonAugCount * perAug;
                    if (durationAugEffective > 0 && perDurationAug > 0)
                        extraSeconds += durationAugEffective * perDurationAug;

                    if (extraSeconds > 0)
                        TimeToRot = tr.Value + (int)Math.Round(extraSeconds);
                }
            }

            // Ensure pet spawns at full health
            Health.Current = Health.MaxValue;
            Stamina.Current = Stamina.MaxValue;
            Mana.Current = Mana.MaxValue;

            return true;
        }

        /// <summary>
        /// When the combat pet deals physical damage (melee, cleave, or missile), arms the same recall/stow block as incoming damage.
        /// When <see cref="ServerConfig.pet_combat_damage_debug_chat"/> is enabled, tells the owner the pet dealt that damage.
        /// </summary>
        internal static void TryNotifyOwnerOutgoingPhysical(CombatPet pet, WorldObject target, float damage, DamageType damageType, string channel)
        {
            if (pet != null)
            {
                var dealt = (uint)Math.Max(0, Math.Round(damage));
                if (dealt > 0)
                    pet.ApplyOwnerFollowRecallBlockFromDamage(dealt, $"OutgoingPhysical:{channel}");
            }

            if (!ServerConfig.pet_combat_damage_debug_chat.Value || pet == null) return;

            var owner = pet.P_PetOwner;
            if (owner?.Session == null) return;

            var dmg = (uint)Math.Max(0, Math.Round(damage));
            if (dmg == 0) return;

            var tname = target?.Name ?? "?";
            owner.Session.Network.EnqueueSend(new GameMessageSystemChat(
                $"[Pet] {pet.Name} {channel} hit {tname} for {dmg} ({damageType}).", ChatMessageType.System));
        }

        /// <summary>
        /// When <see cref="ServerConfig.pet_combat_damage_debug_chat"/> is enabled, tells the owner the pet took damage from a spell projectile (health only).
        /// </summary>
        internal static void TryNotifyOwnerIncomingSpell(CombatPet pet, WorldObject source, DamageType damageType, uint damage, string spellName)
        {
            if (!ServerConfig.pet_combat_damage_debug_chat.Value || pet == null || damage == 0) return;

            var owner = pet.P_PetOwner;
            if (owner?.Session == null) return;

            var src = source?.Name ?? "?";
            var spell = string.IsNullOrEmpty(spellName) ? "?" : spellName;
            owner.Session.Network.EnqueueSend(new GameMessageSystemChat(
                $"[Pet] {pet.Name} took {damage} {damageType} from {src} ({spell}).", ChatMessageType.System));
        }

        /// <summary>True while <see cref="ServerConfig.pet_combat_recall_block_after_damage_seconds"/> window is active after damage.</summary>
        public bool IsOwnerFollowRecallBlocked()
        {
            var sec = ServerConfig.pet_combat_recall_block_after_damage_seconds.Value;
            if (sec <= 0)
                return false;

            return Time.GetUnixTime() < _ownerFollowRecallBlockedUntilUnix;
        }

        /// <summary>Seconds left in the recall/stow block, or 0 if none.</summary>
        public double GetRecallBlockRemainingSeconds()
        {
            var rem = _ownerFollowRecallBlockedUntilUnix - Time.GetUnixTime();
            return rem > 0 ? rem : 0;
        }

        /// <summary>
        /// Blocks stowing a combat pet by reusing its essence (or a passive essence that would dismiss it) while
        /// <see cref="ServerConfig.pet_combat_recall_block_after_damage_seconds"/> is active. Idle follow / leash already consult <see cref="IsOwnerFollowRecallBlocked"/>.
        /// </summary>
        public static bool TryDenyOwnerStowFromRecallBlock(Player player, CombatPet pet, string debugTag)
        {
            if (player?.Session == null || pet == null || !pet.IsOwnerFollowRecallBlocked())
                return false;

            var rem = pet.GetRecallBlockRemainingSeconds();
            var secShow = Math.Max(1, (int)Math.Ceiling(rem));
            player.Session.Network.EnqueueSend(new GameMessageSystemChat(
                $"Your pet was in combat recently; you cannot recall it for about {secShow} more second(s).",
                ChatMessageType.System));
            TraceRecallBlockStatic(pet, debugTag, $"essence_stow_denied remaining≈{rem:F1}s");
            return true;
        }

        /// <summary>
        /// Arms owner follow/leash recall suppression after combat pet HP loss or after the pet deals damage (same timer).
        /// Call from <see cref="TakeDamage"/>, spell projectile hits on the pet, and <see cref="TryNotifyOwnerOutgoingPhysical"/>.
        /// </summary>
        public void ApplyOwnerFollowRecallBlockFromDamage(uint dealt, string sourceTag)
        {
            if (dealt == 0)
                return;

            var blockSec = ServerConfig.pet_combat_recall_block_after_damage_seconds.Value;
            if (blockSec > 0)
            {
                var now = Time.GetUnixTime();
                _ownerFollowRecallBlockedUntilUnix = now + blockSec;

                if (ServerConfig.pet_combat_recall_block_debug.Value)
                    TraceRecallBlock(sourceTag, $"armed dealt={dealt} blockSec={blockSec} untilUnix={_ownerFollowRecallBlockedUntilUnix:F0} now={now:F0}");
            }
            else if (ServerConfig.pet_combat_recall_block_debug.Value)
                TraceRecallBlock(sourceTag, $"skipped_recall_block_seconds_is_0 dealt={dealt}");

            TryRefreshCombatEssenceSharedCooldownRingFromDamage();
        }

        /// <summary>
        /// Refreshes the summoning essence SharedCooldown ring after combat (outgoing or incoming damage).
        /// Duration is <see cref="ServerConfig.pet_combat_essence_damage_cooldown_ring_seconds"/> capped by
        /// <see cref="PetDevice.GetEssenceSharedCooldownCapSeconds"/> (essence <see cref="PropertyFloat.CooldownDuration"/> or 45s).
        /// Independent of recall-block duration and of <see cref="ServerConfig.pet_combat_recall_block_device_cooldown_visual"/>.
        /// </summary>
        private void TryRefreshCombatEssenceSharedCooldownRingFromDamage()
        {
            var ringSec = (float)ServerConfig.pet_combat_essence_damage_cooldown_ring_seconds.Value;
            if (ringSec <= 0)
                return;

            var owner = P_PetOwner;
            if (owner?.EnchantmentManager == null || SummoningDeviceGuid == ObjectGuid.Invalid)
                return;

            var device = owner.FindObject(SummoningDeviceGuid.Full, Player.SearchLocations.Everywhere) as PetDevice;
            device ??= TryGetSummoningDevice();
            if (device?.CooldownId == null)
                return;

            var cap = PetDevice.GetEssenceSharedCooldownCapSeconds(device);
            var duration = Math.Min(ringSec, cap);

            if (ServerConfig.pet_combat_recall_block_debug.Value && duration < ringSec)
                TraceRecallBlock("EssenceRingCap", $"ringSec={ringSec} cappedTo={duration:F1}");

            owner.EnchantmentManager.StartOrRefreshItemCooldown(device.Guid.Full, device.CooldownId.Value, duration);
        }

        internal static void TraceRecallBlockStatic(CombatPet pet, string stage, string detail)
        {
            if (pet == null || !ServerConfig.pet_combat_recall_block_debug.Value)
                return;

            pet.TraceRecallBlock(stage, detail);
        }

        private void TraceRecallBlock(string sourceTag, string detail)
        {
            if (!ServerConfig.pet_combat_recall_block_debug.Value)
                return;

            var msg =
                $"[PetRecallBlock] {sourceTag}: pet={Name} (0x{Guid.Full:X8}) blockedUntil={_ownerFollowRecallBlockedUntilUnix:F0} now={Time.GetUnixTime():F0} isBlocked={IsOwnerFollowRecallBlocked()} cfgSec={ServerConfig.pet_combat_recall_block_after_damage_seconds.Value} | {detail}";
            RecallBlockDbgLog.Info(msg);
        }

        public override uint TakeDamage(WorldObject source, DamageType damageType, float amount, bool crit = false)
        {
            var dealt = base.TakeDamage(source, damageType, amount, crit);

            // Lethal damage: Creature.TakeDamage calls Die() before returning; CombatPet.Die applies death cooldown on the essence.
            // Do not run recall-block + 5s ring refresh after that — it would replace the death cooldown (same CooldownId/caster).
            if (dealt > 0 && !IsDead)
                ApplyOwnerFollowRecallBlockFromDamage(dealt, "TakeDamage");

            if (dealt > 0 && !IsDead && ServerConfig.pet_combat_damage_debug_chat.Value)
            {
                var owner = P_PetOwner;
                if (owner?.Session != null)
                {
                    var src = source?.Name ?? "periodic effect";
                    owner.Session.Network.EnqueueSend(new GameMessageSystemChat(
                        $"[Pet] {Name} took {dealt} {damageType} from {src}.", ChatMessageType.System));
                }
            }

            return dealt;
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
                AttackTarget = null;
                //Console.WriteLine($"{Name}.FindNextTarget(): empty");
                return false;
            }

            // get nearest monster
            var nearest = BuildTargetDistance(nearbyMonsters, true);

            if (nearest[0].Distance > VisualAwarenessRangeSq)
            {
                AttackTarget = null;
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

            var leashRadius = (float)ServerConfig.pet_combat_leash_radius_m.Value;
            var leashEnabled = leashRadius > 0 && P_PetOwner?.PhysicsObj != null;

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

                if (leashEnabled && creature.GetCylinderDistance(P_PetOwner) > leashRadius)
                    continue;

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

            var creatureSkill = GetCreatureSkill(defenseSkill);
            if (creatureSkill == null)
                return 0;

            long lumAugDefense = 0;
            if (combatType == CombatType.Melee)
                lumAugDefense = LuminanceAugmentMeleeDefenseCount ?? 0;
            else if (combatType == CombatType.Missile)
                lumAugDefense = LuminanceAugmentMissileDefenseCount ?? 0;

            // Same structure as Creature.GetEffectiveDefenseSkill: skill × mods + imbues + flat luminance defense aug.
            var effectiveDefense = (uint)Math.Round(creatureSkill.Current * defenseMod * burdenMod * stanceMod + defenseImbues + lumAugDefense);

            if (IsExhausted) effectiveDefense = 0;

            return effectiveDefense;
        }

        /// <summary>
        /// Marks capture-skin weapons and clears their item damage fields for appraisal; see <see cref="PropertyBool.CombatPetCaptureSkinWeapon"/>.
        /// </summary>
        public static void StripVisualWeaponDamageStats(WorldObject item)
        {
            if (item == null)
                return;

            var t = item.ItemType;
            if (t != ItemType.MeleeWeapon && t != ItemType.MissileWeapon)
                return;

            item.SetProperty(PropertyBool.CombatPetCaptureSkinWeapon, true);
            item.SetProperty(PropertyInt.Damage, 0);
            item.SetProperty(PropertyFloat.DamageVariance, 0.0);
            item.SetProperty(PropertyFloat.DamageMod, 1.0f);
        }

        /// <summary>
        /// Override GetBaseDamage to ALWAYS apply item augmentation bonuses,
        /// regardless of whether the summon has a weapon or if the weapon is enchantable
        /// </summary>
        public override BaseDamageMod GetBaseDamage(PropertiesBodyPart attackPart)
        {
            if (CurrentAttack == CombatType.Missile && GetMissileAmmo() != null)
                return GetMissileDamage();

            BaseDamageMod baseDamageMod;

            var weapon = GetEquippedMeleeWeapon();
            // Capture-skin weapons are cosmetic: use body-part damage like unarmed (do not use item weapon stats).
            if (weapon != null && (weapon.GetProperty(PropertyBool.CombatPetCaptureSkinWeapon) ?? false))
            {
                if (attackPart == null)
                {
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
            else if (weapon != null)
            {
                baseDamageMod = weapon.GetDamageMod(this);
            }
            else
            {
                if (attackPart == null)
                {
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
        public override float GetResistanceMod(DamageType damageType, WorldObject attacker, WorldObject weapon, float weaponResistanceMod = 1.0f)
        {
            // If no life augmentation, just call base method
            if (_lifeAugProtectionRating <= 0f)
            {
                return base.GetResistanceMod(damageType, attacker, weapon, weaponResistanceMod);
            }
            
            // Get existing protection mod from enchantments
            var existingProtectionMod = EnchantmentManager.GetProtectionResistanceMod(damageType);
            
            // Calculate life augmentation protection modifier
            var lifeAugProtectionMod = 1.0f - _lifeAugProtectionRating;
            
            // Combine existing protection with life aug (multiplicative)
            // If no existing protection (>= 1.0), use life aug as the protection
            // Otherwise, combine existing protection with life aug
            var combinedProtectionMod = existingProtectionMod >= 1.0f 
                ? lifeAugProtectionMod 
                : existingProtectionMod * lifeAugProtectionMod;
            
            // Now we need to use the base class logic but with our combined protection mod
            // Since we can't easily inject it into the base method, we duplicate the minimal necessary logic
            var ignoreMagicResist = (weapon?.IgnoreMagicResist ?? false) || (attacker?.IgnoreMagicResist ?? false);
            
            if (ignoreMagicResist) return weaponResistanceMod;
            
            var vulnMod = EnchantmentManager.GetVulnerabilityResistanceMod(damageType);
            var naturalResistMod = GetNaturalResistance(damageType);
            
            // Use our combined protection mod (existing + life aug)
            var protMod = combinedProtectionMod;
            
            // protection mod becomes either life protection or natural resistance,
            // whichever is more powerful (more powerful = lower value here)
            if (protMod > naturalResistMod)
                protMod = naturalResistMod;
            
            // Note: Player augmentation resistance is handled in base class, but CombatPets aren't Players
            // so we skip that logic here
            
            // vulnerability mod becomes either life vuln or weapon resistance mod,
            // whichever is more powerful
            if (vulnMod < weaponResistanceMod)
                vulnMod = weaponResistanceMod;
            
            return protMod * vulnMod;
        }

        /// <summary>
        /// Applies imbued effects from the gem to a WorldObject (weapon or creature)
        /// </summary>
        private static void ApplyImbuedEffectsToWorldObject(WorldObject target, ImbuedEffectType imbuedEffects)
        {
            if (target == null || imbuedEffects == ImbuedEffectType.Undef)
                return;

            // Get existing imbued effects from all slots
            var existingEffects = (ImbuedEffectType)(
                (target.GetProperty(PropertyInt.ImbuedEffect) ?? 0) |
                (target.GetProperty(PropertyInt.ImbuedEffect2) ?? 0) |
                (target.GetProperty(PropertyInt.ImbuedEffect3) ?? 0) |
                (target.GetProperty(PropertyInt.ImbuedEffect4) ?? 0) |
                (target.GetProperty(PropertyInt.ImbuedEffect5) ?? 0));

            // Combine with gem's imbued effects
            var combinedEffects = existingEffects | imbuedEffects;

            // Always combine all existing slots with gem effects, regardless of which slots are populated
            // Apply to the first ImbuedEffect slot (combining all existing slots with gem effects)
            target.SetProperty(PropertyInt.ImbuedEffect, (int)combinedEffects);

            // Set icon underlay if there's a primary imbue effect
            if (RecipeManager.IconUnderlay.TryGetValue(imbuedEffects, out var iconUnderlay))
            {
                target.IconUnderlayId = iconUnderlay;
            }
        }

        /// <summary>
        /// Removes previously applied imbued effects from a WorldObject (weapon or creature)
        /// </summary>
        private static void RemoveImbuedEffectsFromWorldObject(WorldObject target, ImbuedEffectType effectsToRemove)
        {
            if (target == null || effectsToRemove == ImbuedEffectType.Undef)
                return;

            // Get existing imbued effects from all slots
            var existingEffects = (ImbuedEffectType)(
                (target.GetProperty(PropertyInt.ImbuedEffect) ?? 0) |
                (target.GetProperty(PropertyInt.ImbuedEffect2) ?? 0) |
                (target.GetProperty(PropertyInt.ImbuedEffect3) ?? 0) |
                (target.GetProperty(PropertyInt.ImbuedEffect4) ?? 0) |
                (target.GetProperty(PropertyInt.ImbuedEffect5) ?? 0));

            // Remove the effects we previously applied
            var remainingEffects = existingEffects & ~effectsToRemove;

            // Update the first ImbuedEffect slot with remaining effects
            target.SetProperty(PropertyInt.ImbuedEffect, (int)remainingEffects);
            
            // Clear icon underlay if we removed all effects
            if (remainingEffects == ImbuedEffectType.Undef)
            {
                target.IconUnderlayId = null;
            }
        }

        /// <summary>
        /// Gets the item augmentation percentage rating using the same scaling formula as players
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetItemAugPercentageRating(long itemAugAmt)
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
        public static float GetLifeAugProtectRating(long lifeAugAmt)
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

        protected override void Die(DamageHistoryInfo lastDamager, DamageHistoryInfo topDamager)
        {
            if (P_PetOwner?.Session != null)
            {
                P_PetOwner.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"[Pet] Your combat pet {Name} has died.",
                    ChatMessageType.System));
            }

            // Match combat ring path: refresh-in-place so combat pings and death do not stack duplicate enchantments.
            // Require Session: StartCooldown uses Session unconditionally; offline owner cannot receive cooldown packet anyway.
            var deathCooldownFromPet =
                ServerConfig.pet_summon_cooldown_on_pet_death_only.Value
                || ServerConfig.pet_combat_summon_skips_shared_cooldown.Value;

            if (deathCooldownFromPet && P_PetOwner != null && P_PetOwner.Session != null && SummoningDeviceGuid != ObjectGuid.Invalid)
            {
                var device = P_PetOwner.FindObject(SummoningDeviceGuid.Full, Player.SearchLocations.Everywhere) as PetDevice;
                device ??= TryGetSummoningDevice();
                if (device != null && device.CooldownId != null)
                {
                    var deathSeconds = (float)(device.CooldownDuration ?? 0);
                    var cap = PetDevice.GetEssenceSharedCooldownCapSeconds(device);
                    float duration;
                    if (deathSeconds > 0)
                        duration = Math.Min(deathSeconds, cap);
                    else
                    {
                        var ringSec = (float)ServerConfig.pet_combat_essence_damage_cooldown_ring_seconds.Value;
                        duration = ringSec > 0 ? Math.Min(ringSec, cap) : cap;
                    }
                    if (duration > 0)
                        P_PetOwner.EnchantmentManager.StartOrRefreshItemCooldown(device.Guid.Full, device.CooldownId.Value, duration);
                }
            }

            base.Die(lastDamager, topDamager);
        }
    }
}
