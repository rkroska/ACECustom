using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.DatLoader;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        private static readonly log4net.ILog zcLog = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // ── Charm redirect spell IDs ───────────────────────────────────────────
        private const uint SpellId_TectonicRiftsI  = 1789u;
        private const uint SpellId_TectonicRiftsII = 6196u;
        private const uint SpellId_RockyShrapnel   = 6152u;
        private const uint SpellId_RingOfAgony     = 2673u;
        private const uint SpellId_FlameRing       = (uint)ACE.Entity.Enum.SpellId.FlameRing;

        private readonly Dictionary<int, int> _explosiveArrowProcsPerAttack = new();

        // ── Explosive Arrow proc ring spells (Tier II, one per damage type) ────
        private const uint SpellId_NuhmudiraSpinesII   = 6192u; // Pierce
        private const uint SpellId_HorizonsBladesII    = 6190u; // Slash
        private const uint SpellId_CassiusRingOfFireII = 6191u; // Fire
        private const uint SpellId_HaloOfFrostII       = 6193u; // Cold
        private const uint SpellId_SearingDiscII       = 6189u; // Acid
        private const uint SpellId_EyeOfTheStormII     = 6194u; // Electric
        private const uint SpellId_CloudedSoulII       = 6195u; // Nether
        // Bludgeon uses SpellId_TectonicRiftsII (Tier II bludgeon ring)

        /// <summary>
        /// Maps an arrow's damage type to the matching Tier-II ring spell for the Explosive Arrow proc.
        /// The chosen spell controls both the visual ring animation and the damage type applied.
        /// </summary>
        private static uint GetRingSpellForDamageType(DamageType dt)
        {
            if ((dt & DamageType.Pierce)   != 0) return SpellId_NuhmudiraSpinesII;
            if ((dt & DamageType.Fire)     != 0) return SpellId_CassiusRingOfFireII;
            if ((dt & DamageType.Cold)     != 0) return SpellId_HaloOfFrostII;
            if ((dt & DamageType.Acid)     != 0) return SpellId_SearingDiscII;
            if ((dt & DamageType.Electric) != 0) return SpellId_EyeOfTheStormII;
            if ((dt & DamageType.Slash)    != 0) return SpellId_HorizonsBladesII;
            if ((dt & DamageType.Nether)   != 0) return SpellId_CloudedSoulII;
            if ((dt & DamageType.Bludgeon) != 0) return SpellId_TectonicRiftsII;
            return SpellId_FlameRing; // ultimate fallback
        }

        // TODO: get rid of this, only used for determining if TurnTo is required
        public enum TargetCategory
        {
            Undef,
            WorldObject,
            Wielded,
            Inventory,
            Self,
            Fellowship
        }

        public MagicState MagicState;

        /// <summary>
        /// The last spell projectile launched by this player
        /// to successfully collided with a target
        /// </summary>
        public Spell LastHitSpellProjectile;

        /// <summary>
        /// Limiter for switching between war and void magic
        /// </summary>
        public double LastSuccessCast_Time;
        public MagicSchool LastSuccessCast_School;

        public bool DebugSpell { get; set; }

        public string DebugDamageBuffer { get; set; }

        public RecordCast RecordCast { get; set; }

        /// <summary>
        /// Returns the magic skill associated with the magic school
        /// for the last collided spell projectile
        /// </summary>
        public Skill GetCurrentMagicSkill()
        {
            if (LastHitSpellProjectile == null)
                return Skill.WarMagic;  // this should never happen, but just in case

            switch (LastHitSpellProjectile.School)
            {
                case MagicSchool.WarMagic:
                default:
                    return Skill.WarMagic;
                case MagicSchool.LifeMagic:
                    return Skill.LifeMagic;
                case MagicSchool.CreatureEnchantment:
                    return Skill.CreatureEnchantment;
                case MagicSchool.ItemEnchantment:
                    return Skill.ItemEnchantment;
                case MagicSchool.VoidMagic:
                    return Skill.VoidMagic;
            }
        }

        /// <summary>
        /// Handles player targeted casting message
        /// </summary>
        /// <param name="builtInSpell">If TRUE, casting a built-in spell from a weapon</param>
        public void HandleActionCastTargetedSpell(uint targetGuid, uint spellId, WorldObject casterItem = null)
        {
            //Console.WriteLine($"{Name}.HandleActionCastTargetedSpell({targetGuid:X8}, {spellId}, {builtInSpell})");

            if (CombatMode != CombatMode.Magic)
            {
                //log.Error($"{Name}.HandleActionCastTargetedSpell({targetGuid:X8}, {spellId}, {casterItem?.Name}) - CombatMode mismatch {CombatMode}, LastCombatMode: {LastCombatMode}");

                if (LastCombatMode == CombatMode.Magic)
                    CombatMode = CombatMode.Magic;
                else
                {
                    SendUseDoneEvent();
                    return;
                }
            }

            if (FastTick && PhysicsObj.MovementManager.MotionInterpreter.InterpretedState.CurrentStyle != (uint)MotionStance.Magic)
            {
                log.Warn($"{Name} CombatMode: {CombatMode}, CurrentMotionState: {CurrentMotionState.Stance}.{CurrentMotionState.MotionState.ForwardCommand}, Physics: {(MotionStance)PhysicsObj.MovementManager.MotionInterpreter.InterpretedState.CurrentStyle}.{(MotionCommand)PhysicsObj.MovementManager.MotionInterpreter.InterpretedState.ForwardCommand}");
                ApplyPhysicsMotion(new Motion(MotionStance.Magic));
                SendUseDoneEvent(WeenieError.YoureTooBusy);
                return;
            }

            if (IsJumping)
            {
                SendUseDoneEvent(WeenieError.YouCantDoThatWhileInTheAir);
                return;
            }

            if (PKLogout)
            {
                SendUseDoneEvent(WeenieError.YouHaveBeenInPKBattleTooRecently);
                return;
            }

            if (IsBusy && MagicState.CanQueue)
            {
                MagicState.CastQueue = new CastQueue(CastQueueType.Targeted, targetGuid, spellId, casterItem);
                MagicState.CanQueue = false;
                return;
            }

            if (!VerifyBusy())
                return;

            // verify spell is contained in player's spellbook,
            // or in the weapon's spellbook in the case of built-in spells
            if (!VerifySpell(spellId, casterItem))
            {
                SendUseDoneEvent(WeenieError.MagicInvalidSpellType);
                return;
            }

            var spell = new Spell(spellId);
            var targetCategory = GetTargetCategory(targetGuid, spell, out var target);

            if (target == null || target.Teleporting)
            {
                SendUseDoneEvent(WeenieError.TargetNotAcquired);
                return;
            }

            // Tag the last combat action if the target is a creature and not self.
            // This is good enough, and is the easiest way to detect an attempt to cast on a mob.
            if (spell.IsHarmful && target is Creature && targetCategory != TargetCategory.Self)
            {
                LastCombatActionTime = DateTime.UtcNow;
            }

            MagicState.OnCastStart();
            MagicState.SetWindupParams(targetGuid, spell, casterItem);

            StartPos = new Physics.Common.Position(PhysicsObj.Position);

            if (RecordCast.Enabled)
                RecordCast.OnCastTargetedSpell(spell, target);

            if (targetCategory != TargetCategory.WorldObject && targetCategory != TargetCategory.Wielded)
            {
                if (!CreatePlayerSpell(target, targetCategory, spell, casterItem))
                    MagicState.OnCastDone();

                return;
            }

            // start turning
            if (!FastTick)
            {
                var rotateTarget = target;
                if (rotateTarget.WielderId != null)
                {
                    var wielded = CurrentLandblock?.GetObject(rotateTarget.WielderId.Value);
                    if (wielded != null)
                        rotateTarget = wielded;
                }

                var rotateTime = Rotate(rotateTarget);
                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(rotateTime);

                actionChain.AddAction(this, ActionType.PlayerMagic_FinishCast, () =>
                {
                    // ensure target still exists
                    targetCategory = GetTargetCategory(targetGuid, spell, out target);

                    if (target == null)
                    {
                        SendUseDoneEvent(WeenieError.TargetNotAcquired);
                        MagicState.OnCastDone();
                        return;
                    }

                    if (!CreatePlayerSpell(target, targetCategory, spell, casterItem))
                        MagicState.OnCastDone();
                });

                actionChain.EnqueueChain();
            }
            else
                TurnTo_Magic(target);
        }

        public void DoWindup(WindupParams windupParams, bool checkAngle)
        {
            //Console.WriteLine($"{Name}.DoWindup()");

            // ensure target still exists
            var targetCategory = GetTargetCategory(windupParams.TargetGuid, windupParams.Spell, out var target);

            if (target == null)
            {
                SendUseDoneEvent(WeenieError.TargetNotAcquired);
                MagicState.OnCastDone();
                return;
            }

            if (!checkAngle || IsWithinAngle(target))
            {
                if (!CreatePlayerSpell(target, targetCategory, windupParams.Spell, windupParams.CasterItem))
                    MagicState.OnCastDone();
            }
            else
            {
                // restart turn if required
                if (PhysicsObj.MovementManager.MotionInterpreter.InterpretedState.TurnCommand == 0)
                    TurnTo_Magic(target);
                else
                    MagicState.PendingTurnRelease = true;
            }
        }

        private TargetCategory GetTargetCategory(uint targetGuid, Spell spell, out WorldObject target)
        {
            // fellowship spell (all fellowship targets)
            if ((spell.Flags & SpellFlags.FellowshipSpell) != 0)
            {
                target = this;
                return TargetCategory.Fellowship;
            }

            // direct landblock object
            target = CurrentLandblock?.GetObject(targetGuid);

            if (target != null)
                return targetGuid == Guid.Full ? TargetCategory.Self : TargetCategory.WorldObject;

            // self-wielded
            target = GetEquippedItem(targetGuid);
            if (target != null)
                return TargetCategory.Inventory;

            // inventory item
            target = GetInventoryItem(targetGuid);
            if (target != null)
                return TargetCategory.Inventory;

            // other selectable wielded
            target = CurrentLandblock?.GetWieldedObject(targetGuid, true);
            if (target != null)
                return TargetCategory.Wielded;

            // known trade objects
            var tradePartner = GetKnownTradeObj(new ObjectGuid(targetGuid));
            if (tradePartner != null)
            {
                target = tradePartner.GetEquippedItem(targetGuid);
                if (target != null)
                    return TargetCategory.Wielded;

                target = tradePartner.GetInventoryItem(targetGuid);
                if (target != null)
                    return TargetCategory.Inventory;
            }

            return TargetCategory.Undef;
        }

        /// <summary>
        /// Handles player untargeted casting message
        /// </summary>
        public void HandleActionMagicCastUnTargetedSpell(uint spellId)
        {
            if (CombatMode != CombatMode.Magic)
            {
                //log.Error($"{Name}.HandleActionMagicCastUnTargetedSpell({spellId}) - CombatMode mismatch {CombatMode}, LastCombatMode {LastCombatMode}");

                if (LastCombatMode == CombatMode.Magic)
                    CombatMode = CombatMode.Magic;
                else
                {
                    SendUseDoneEvent();
                    return;
                }
            }

            if (FastTick && PhysicsObj.MovementManager.MotionInterpreter.InterpretedState.CurrentStyle != (uint)MotionStance.Magic)
            {
                //log.Warn($"{Name} CombatMode: {CombatMode}, CurrentMotionState: {CurrentMotionState.Stance}.{CurrentMotionState.MotionState.ForwardCommand}, Physics: {(MotionStance)PhysicsObj.MovementManager.MotionInterpreter.InterpretedState.CurrentStyle}.{(MotionCommand)PhysicsObj.MovementManager.MotionInterpreter.InterpretedState.ForwardCommand}");
                ApplyPhysicsMotion(new Motion(MotionStance.Magic));
                SendUseDoneEvent(WeenieError.YoureTooBusy);
                return;
            }

            if (IsJumping)
            {
                SendUseDoneEvent(WeenieError.YouCantDoThatWhileInTheAir);
                return;
            }

            if (PKLogout)
            {
                SendUseDoneEvent(WeenieError.YouHaveBeenInPKBattleTooRecently);
                return;
            }

            if (IsBusy && MagicState.CanQueue)
            {
                MagicState.CastQueue = new CastQueue(CastQueueType.Untargeted, 0, spellId, null);
                MagicState.CanQueue = false;
                return;
            }

            if (!VerifyBusy())
                return;

            // verify spell is contained in player's spellbook,
            // or in the weapon's spellbook in the case of built-in spells
            if (!VerifySpell(spellId))
                return;

            var spell = new Spell(spellId);

            if (spell.IsHarmful)
            {
                LastCombatActionTime = DateTime.UtcNow;
            }

            if (RecordCast.Enabled)
                RecordCast.OnCastUntargetedSpell(spell);

            MagicState.OnCastStart();

            StartPos = new Physics.Common.Position(PhysicsObj.Position);

            if (!CreatePlayerSpell(spell))
                MagicState.OnCastDone();
        }

        /// <summary>
        /// Verifies spell is contained in player's spellbook,
        /// or in the weapon's spellbook in the case of built-in spells
        /// </summary>
        /// <param name="builtInSpell">If TRUE, casting a built-in spell from a weapon</param>
        public bool VerifySpell(uint spellId, WorldObject casterItem = null)
        {
            if (casterItem != null)
                return IsWeaponSpell(spellId, casterItem);
            else
                return SpellIsKnown(spellId);

            // send error message?
        }

        /// <summary>
        /// Returns TRUE if the currently equipped casting implement
        /// has a built-in spell
        /// </summary>
        public bool IsWeaponSpell(uint spellId, WorldObject casterItem)
        {
            var caster = GetEquippedWand();

            if (casterItem != null)
                caster = casterItem;

            if (caster == null || caster.SpellDID == null)
                return false;

            return caster.SpellDID == spellId;
        }

        public enum CastingPreCheckStatus
        {
            CastFailed,
            InvalidPKStatus,
            Success
        }

        public static float Windup_MaxMove = 6.0f;
        public static float Windup_MaxMoveSq = Windup_MaxMove * Windup_MaxMove;

        public bool VerifyBusy()
        {
            if (IsBusy || Teleporting || suicideInProgress)
            {
                SendUseDoneEvent(WeenieError.YoureTooBusy);
                return false;
            }
            return true;
        }

        private static SpellSuppressionSchools ToSuppressionSchool(MagicSchool school)
        {
            return school switch
            {
                MagicSchool.WarMagic => SpellSuppressionSchools.WarMagic,
                MagicSchool.LifeMagic => SpellSuppressionSchools.LifeMagic,
                MagicSchool.ItemEnchantment => SpellSuppressionSchools.ItemEnchantment,
                MagicSchool.CreatureEnchantment => SpellSuppressionSchools.CreatureEnchantment,
                MagicSchool.VoidMagic => SpellSuppressionSchools.VoidMagic,
                _ => SpellSuppressionSchools.None
            };
        }

        private static string GetSuppressionSchoolName(SpellSuppressionSchools school)
        {
            return school switch
            {
                SpellSuppressionSchools.WarMagic => "War Magic",
                SpellSuppressionSchools.LifeMagic => "Life Magic",
                SpellSuppressionSchools.ItemEnchantment => "Item Enchantment",
                SpellSuppressionSchools.CreatureEnchantment => "Creature Enchantment",
                SpellSuppressionSchools.VoidMagic => "Void Magic",
                _ => "Magic"
            };
        }

        private static double GetSpellSuppressionRadiusSq(WorldObject source)
        {
            var configuredRadius = source.SpellSuppressionRadius;
            if (configuredRadius.HasValue && configuredRadius.Value > 0)
                return configuredRadius.Value * configuredRadius.Value;

            if (source is Creature creature && creature.VisualAwarenessRange > 0)
                return creature.VisualAwarenessRangeSq;

            return 0;
        }

        private static bool IsActiveSpellSuppressor(WorldObject source)
        {
            if (source == null || source.PhysicsObj == null)
                return false;

            if (source is Creature creature)
            {
                if (creature.IsDead)
                    return false;

                if (creature.IsAwake)
                    return true;
            }

            return source.IsPassiveSpellSuppressor == true;
        }

        private bool TryGetActiveSpellSuppressor(SpellSuppressionSchools schoolFlag, out WorldObject suppressor)
        {
            suppressor = null;

            if (schoolFlag == SpellSuppressionSchools.None || PhysicsObj?.ObjMaint == null)
                return false;

            var visibleObjects = PhysicsObj.ObjMaint.GetVisibleObjectsValuesWhere(o => o?.WeenieObj?.WorldObject != null);
            var nearestDistSq = double.MaxValue;

            foreach (var visibleObject in visibleObjects)
            {
                var source = visibleObject.WeenieObj.WorldObject;
                if (source == null || source == this)
                    continue;

                if ((source.SpellSuppressionSchools & schoolFlag) == 0)
                    continue;

                if (!IsActiveSpellSuppressor(source))
                    continue;

                var suppressionRadiusSq = GetSpellSuppressionRadiusSq(source);
                if (suppressionRadiusSq <= 0)
                    continue;

                var distSq = PhysicsObj.get_distance_sq_to_object(source.PhysicsObj, true);
                if (distSq > suppressionRadiusSq)
                    continue;

                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    suppressor = source;
                }
            }

            return suppressor != null;
        }

        private bool VerifySpellSchoolSuppression(Spell spell)
        {
            var schoolFlag = ToSuppressionSchool(spell.School);
            if (schoolFlag == SpellSuppressionSchools.None)
                return true;

            if (!TryGetActiveSpellSuppressor(schoolFlag, out var suppressor))
                return true;

            var schoolName = GetSuppressionSchoolName(schoolFlag);
            var msg = suppressor.SpellSuppressionMessage;

            if (string.IsNullOrWhiteSpace(msg))
                msg = $"{schoolName} is being suppressed by {suppressor.Name}.";
            else
            {
                msg = msg.Replace("{school}", schoolName, StringComparison.OrdinalIgnoreCase);
                msg = msg.Replace("{source}", suppressor.Name, StringComparison.OrdinalIgnoreCase);
            }

            Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Magic));
            SendUseDoneEvent(WeenieError.None);
            return false;
        }

        public bool IsValidSpell(Spell spell, bool isWeaponSpell = false)
        {
            if (spell.NotFound)
            {
                if (spell._spellBase == null)
                {
                    Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, $"SpellId {spell.Id} Invalid."));
                    SendUseDoneEvent(WeenieError.None);
                }
                else
                {
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"{spell.Name} spell not implemented, yet!", ChatMessageType.System));
                    SendUseDoneEvent(WeenieError.MagicInvalidSpellType);
                }
                return false;
            }
            if (!isWeaponSpell && !HasComponentsForSpell(spell))
            {
                SendUseDoneEvent(WeenieError.YouDontHaveAllTheComponents);
                return false;
            }

            return true;
        }

        public bool VerifySpellTarget(Spell spell, WorldObject target)
        {
            if (IsInvalidTarget(spell, target))
            {
                Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, $"{spell.Name} cannot be cast on {target.Name}."));
                SendUseDoneEvent(WeenieError.None);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Determines whether the target for the spell being cast is invalid
        /// </summary>
        protected bool IsInvalidTarget(Spell spell, WorldObject target)
        {
            var targetPlayer = target as Player;
            var targetCreature = target as Creature;

            // ensure target is enchantable
            if (!target.IsEnchantable)
                return true;

            // Self targeted spells should have a target of self
            if (spell.Flags.HasFlag(SpellFlags.SelfTargeted) && target != this)
                return true;

            // Invalidate non Item Enchantment spells cast against non Creatures or Players
            if (spell.School != MagicSchool.ItemEnchantment && targetCreature == null)
                return true;

            // Invalidate beneficial spells against Creature/Non-player targets
            if (targetCreature != null && targetPlayer == null && spell.IsBeneficial)
                return true;

            // check item spells
            if (targetCreature == null && target.WielderId != null)
            {
                var parent = CurrentLandblock?.GetObject(target.WielderId.Value) as Player;

                // Invalidate beneficial spells against monster wielded items
                if (parent == null && spell.IsBeneficial)
                    return true;

                // Invalidate harmful spells against player wielded items, depending on pk status
                if (parent != null && spell.IsHarmful && CheckPKStatusVsTarget(parent, spell) != null)
                    return true;
            }

            // verify target type for item enchantment
            if (spell.School == MagicSchool.ItemEnchantment && !VerifyNonComponentTargetType(spell, target))
            {
                if (spell.DispelSchool != MagicSchool.ItemEnchantment || !ServerConfig.item_dispel.Value)
                    return true;
            }

            // brittlemail / lure / other negative item spells cannot be cast with player as target

            // TODO: by end of retail, players couldn't cast any negative spells on themselves
            // this feature is currently in ace for dev testing...
            if (target == this && spell.IsNegativeRedirectable)
                return true;

            if (targetCreature != null && targetCreature != this && spell.NonComponentTargetType == ItemType.Creature && !CanDamage(targetCreature))
                return true;

            return false;
        }

        public bool VerifySpellRange(WorldObject target, TargetCategory targetCategory, Spell spell, WorldObject casterItem, uint magicSkill)
        {
            if (targetCategory != TargetCategory.WorldObject && targetCategory != TargetCategory.Wielded || target.Guid == Guid)
                return true;

            var targetLoc = target;
            if (targetLoc.WielderId != null)
                targetLoc = CurrentLandblock?.GetObject(targetLoc.WielderId.Value);

            float distanceTo = Location.Distance2D(targetLoc.Location);

            // Only do this if for some reason magicSkill wasn't passed in
            if (casterItem == null && magicSkill == 0)
            {
                // use init + ranks, same as acclient DetermineSpellRange -> InqSkillLevel
                // this is much lower than base, and omits things like attribute formula + base augs + enlightenment
                var playerSkill = GetCreatureSkill(spell.School);
                magicSkill = playerSkill.InitLevel + playerSkill.Ranks;
            }

            var maxRange = Math.Min(spell.BaseRangeConstant + magicSkill * spell.BaseRangeMod, MaxRadarRange_Outdoors);

            if (distanceTo > maxRange)
            {
                SendUseDoneEvent(WeenieError.MissileOutOfRange);
                return false;
            }

            // bootstrapping this function for indoor/outdoor check, since it is called both before and after windup
            if (spell.Flags.HasFlag(SpellFlags.NotIndoor))
            {
                if (Location.Indoors || target != null && target.Location.Indoors)
                {
                    SendUseDoneEvent(WeenieError.YourSpellCannotBeCastInside);
                    return false;
                }
            }
            if (spell.Flags.HasFlag(SpellFlags.NotOutdoor))
            {
                if (!Location.Indoors || target != null && !target.Location.Indoors)
                {
                    SendUseDoneEvent(WeenieError.YourSpellCannotBeCastOutside);
                    return false;
                }
            }
            return true;
        }

        public CastingPreCheckStatus GetCastingPreCheckStatus(Spell spell, uint magicSkill, bool isWeaponSpell)
        {
            var difficulty = spell.Power;

            var castingPreCheckStatus = CastingPreCheckStatus.CastFailed;

            if (magicSkill > 0 && magicSkill >= (int)difficulty - 50)
            {
                var chance = SkillCheck.GetMagicSkillChance((int)magicSkill, (int)difficulty);
                var rng = ThreadSafeRandom.Next(0.0f, 1.0f);
                if (chance > rng)
                    castingPreCheckStatus = CastingPreCheckStatus.Success;
            }

            // build-in spells never fizzle
            if (isWeaponSpell)
                castingPreCheckStatus = CastingPreCheckStatus.Success;

            // limit casting time between war and void
            if (spell.School == MagicSchool.VoidMagic && LastSuccessCast_School == MagicSchool.WarMagic ||
                spell.School == MagicSchool.WarMagic && LastSuccessCast_School == MagicSchool.VoidMagic)
            {
                // roll each time?
                var timeLimit = ThreadSafeRandom.Next(3.0f, 5.0f);

                if (Time.GetUnixTime() - LastSuccessCast_Time < timeLimit)
                {
                    var curType = spell.School == MagicSchool.WarMagic ? "War" : "Void";
                    var prevType = LastSuccessCast_School == MagicSchool.VoidMagic ? "Nether" : "Elemental";

                    Session.Network.EnqueueSend(new GameMessageSystemChat($"The {prevType} energies permeating your blood cause this {curType} magic to fail.", ChatMessageType.Magic));

                    castingPreCheckStatus = CastingPreCheckStatus.CastFailed;
                }
            }
            return castingPreCheckStatus;
        }

        public bool CalculateManaUsage(CastingPreCheckStatus castingPreCheckStatus, Spell spell, WorldObject target, WorldObject casterItem, out uint manaUsed)
        {
            manaUsed = 0;
            if (castingPreCheckStatus == CastingPreCheckStatus.Success)
                manaUsed = CalculateManaUsage(this, spell, target);
            else if (castingPreCheckStatus == CastingPreCheckStatus.CastFailed)
                manaUsed = 5;   // todo: verify with retail

            var currentMana = Mana.Current;
            if (casterItem != null)
            {
                //var caster = GetEquippedWand();
                currentMana = (uint)(casterItem.ItemCurMana ?? 0);
            }

            if (manaUsed > currentMana)
            {
                SendUseDoneEvent(WeenieError.YouDontHaveEnoughManaToCast);
                return false;
            }

            Proficiency.OnSuccessUse(this, GetCreatureSkill(Skill.ManaConversion), spell.PowerMod);

            return true;
        }

        public void DoSpellWords(Spell spell, bool isWeaponSpell)
        {
            spell.Formula.GetPlayerFormula(this);

            var spellWords = spell._spellBase.GetSpellWords(DatManager.PortalDat.SpellComponentsTable);
            if (!string.IsNullOrWhiteSpace(spellWords) && !isWeaponSpell)
                EnqueueBroadcast(new GameMessageHearSpeech(spellWords, GetNameWithSuffix(), Guid.Full, ChatMessageType.Spellcasting), LocalBroadcastRange, ChatMessageType.Spellcasting);
        }

        public static float CastSpeed = 2.0f;       // from retail pcaps, player animation speed for windup / first half of cast gesture

        public void DoWindupGestures(Spell spell, bool isWeaponSpell, ActionChain castChain)
        {
            if (spell.Flags.HasFlag(SpellFlags.FastCast) || isWeaponSpell)
                return;

            if (FastTick)
            {
                castChain.AddAction(this, ActionType.PlayerMagic_FastTick, () =>
                {
                    PhysicsObj.StopCompletely(false);

                    MagicState.TurnStarted = false;
                    MagicState.IsTurning = false;
                });
            }

            var windupTime = 0.0f;

            foreach (var windupGesture in spell.Formula.WindupGestures)
            {
                if (RecordCast.Enabled)
                {
                    castChain.AddAction(this, ActionType.PlayerMagic_RecordCast, () =>
                    {
                        var animLength = Physics.Animation.MotionTable.GetAnimationLength(MotionTableId, CurrentMotionState.Stance, windupGesture, CastSpeed);
                        RecordCast.Log($"Windup Gesture: {windupGesture}, Windup Time: {animLength}");
                    });
                }

                // don't mess with CurrentMotionState here?
                if (!FastTick)
                    windupTime = EnqueueMotionMagic(castChain, windupGesture, CastSpeed);

                /*Console.WriteLine($"{spell.Name}");
                Console.WriteLine($"Windup Gesture: " + windupGesture);
                Console.WriteLine($"Windup time: " + windupTime);
                Console.WriteLine("-------");*/
            }

            if (FastTick)
                windupTime = EnqueueMotionAction(castChain, spell.Formula.WindupGestures, CastSpeed, MotionStance.Magic, checkCasting: true);
        }

        public void DoCastGesture(Spell spell, WorldObject casterItem, ActionChain castChain)
        {
            MagicState.CastGesture = spell.Formula.CastGesture;

            if (casterItem != null)
            {
                //var caster = GetEquippedWand();
                if (casterItem.UseUserAnimation != 0)
                    MagicState.CastGesture = casterItem.UseUserAnimation;
            }

            if (RecordCast.Enabled)
            {
                castChain.AddAction(this, ActionType.PlayerMagic_RecordCast, () =>
                {
                    var animLength = Physics.Animation.MotionTable.GetAnimationLength(MotionTableId, CurrentMotionState.Stance, MagicState.CastGesture, CastSpeed);
                    RecordCast.Log($"Cast Gesture: {MagicState.CastGesture}, Cast Time: {animLength}");
                });
            }

            castChain.AddAction(this, ActionType.PlayerMagic_StartCastingGesture, () =>
            {
                if (!MagicState.IsCasting) return;

                MagicState.CastGestureStartTime = DateTime.UtcNow;

                if (FastTick)
                    PhysicsObj.StopCompletely(false);
            });

            if (MagicState.CastGesture == MotionCommand.Invalid)
                MagicState.CastGesture = MotionCommand.Ready;

            var castTime = 0.0f;
            if (FastTick)
                castTime = EnqueueMotion(castChain, MagicState.CastGesture, CastSpeed, true, null, true);
            else
                castTime = EnqueueMotionMagic(castChain, MagicState.CastGesture, CastSpeed);

            //Console.WriteLine($"Cast Gesture: " + MagicState.CastGesture);
            //Console.WriteLine($"Cast time: " + castTime);
        }

        // 20 from MoveToManager threshold?
        public static readonly float MaxAngle = 5;

        public void DoCastSpell(MagicState _state, bool checkAngle = true)
        {
            //Console.WriteLine("DoCastSpell");

            if (!MagicState.IsCasting)
                return;

            var state = _state?.CastSpellParams;

            if (state == null)
            {
                log.Warn($"{Name}.DoCastSpell(): null state detected");
                log.Warn(_state);

                // send UseDone?
                SendUseDoneEvent(WeenieError.BadCast);

                return;
            }

            DoCastSpell(state.Spell, state.CasterItem, state.MagicSkill, state.ManaUsed, state.Target, state.Status, checkAngle);
        }

        public bool IsWithinAngle(WorldObject target)
        {
            // TODO: investigate this more, difference for GetAngle() between ACE and ac physics engine
            var angle = 0.0f;
            if (target != this)
            {
                if (target.CurrentLandblock == null)
                {
                    FindObject(target.Guid.Full, SearchLocations.Everywhere, out _, out var rootOwner, out _);

                    if (rootOwner == null)
                        log.Error($"{Name}.IsWithinAngle({target.Name} ({target.Guid})) - couldn't find rootOwner");

                    else if (rootOwner != this)
                        angle = GetAngle(rootOwner);
                }
                else
                    angle = GetAngle(target);
            }

            //Console.WriteLine($"Angle: " + angle);
            var maxAngle = ServerConfig.spellcast_max_angle.Value;

            if (RecordCast.Enabled)
                RecordCast.Log($"DoCastSpell(angle={angle} vs. {maxAngle})");

            return angle <= maxAngle;
        }

        public void DoCastSpell(Spell spell, WorldObject casterItem, uint magicSkill, uint manaUsed, WorldObject target, CastingPreCheckStatus castingPreCheckStatus, bool checkAngle = true)
        {
            if (target != null)
            {
                // verify target still exists
                var targetCategory = GetTargetCategory(target.Guid.Full, spell, out target);

                if (target == null)
                {
                    SendWeenieError(WeenieError.TargetNotAcquired);
                    FinishCast();
                    return;
                }

                // do second rotate, if applicable
                // TODO: investigate this more, difference for GetAngle() between ACE and ac physics engine
                if (checkAngle && !IsWithinAngle(target))
                {
                    if (!FastTick)
                    {
                        var rotateTime = Rotate(target);

                        var actionChain = new ActionChain();
                        actionChain.AddDelaySeconds(rotateTime);
                        actionChain.AddAction(this, ActionType.PlayerMagic_DoCastSpell, () => DoCastSpell(spell, casterItem, magicSkill, manaUsed, target, castingPreCheckStatus, false));
                        actionChain.EnqueueChain();
                    }
                    else
                    {
                        if (PhysicsObj.MovementManager.MotionInterpreter.InterpretedState.TurnCommand == 0)
                            TurnTo_Magic(target);
                        else
                            MagicState.PendingTurnRelease = true;
                    }

                    return;
                }

                // verify spell range
                if (!VerifySpellRange(target, targetCategory, spell, casterItem, magicSkill))
                {
                    FinishCast();
                    return;
                }
            }

            if (IsDead)
            {
                FinishCast();
                return;
            }

            if (target != null && target is Player targetPlayer && Session.AccessLevel >= AccessLevel.Admin)
            {
                PlayerManager.BroadcastToAuditChannel(this, $"Admin {Name} cast {spell.Name} (ID: {spell.Id}) on {targetPlayer.Name}.");
            }

            DoCastSpell_Inner(spell, casterItem, manaUsed, target, castingPreCheckStatus);
        }

        public WorldObject TurnTarget;

        public void TurnTo_Magic(WorldObject target)
        {
            //Console.WriteLine($"{Name}.TurnTo_Magic()");
            TurnTarget = target;

            MagicState.TurnStarted = true;
            MagicState.IsTurning = true;

            if (FastTick)
            {
                if (ServerConfig.spellcast_max_angle.Value > 5.0f && IsWithinAngle(target))
                {
                    // emulate current gdle TurnTo - doesn't match retail, but some players may prefer this
                    OnMoveComplete_Magic(WeenieError.None);
                    return;
                }

                // verify cast radius before every automatic TurnTo after windup
                if (!VerifyCastRadius())
                    return;

                var stopCompletely = !MagicState.CastMotionDone;
                //var stopCompletely = true;

                CreateTurnToChain2(target, null, null, stopCompletely, MagicState.AlwaysTurn);

                MagicState.AlwaysTurn = false;
            }
        }

        private Physics.Common.Position StartPos { get; set; }

        private void DoCastSpell_Inner(Spell spell, WorldObject casterItem, uint manaUsed, WorldObject target, CastingPreCheckStatus castingPreCheckStatus, bool finishCast = true)
        {
            if (RecordCast.Enabled)
                RecordCast.Log($"DoCastSpell_Inner()");

            if (MagicState.CastMeter)
            {
                var gestureTime = Physics.Animation.MotionTable.GetAnimationLength(MotionTableId, CurrentMotionState.Stance, MagicState.CastGesture, CastSpeed);
                var castTime = DateTime.UtcNow - MagicState.CastGestureStartTime;
                var efficiency = 1.0f - (float)castTime.TotalSeconds / gestureTime;
                var msg = $"Cast efficiency: {efficiency * 100}%";
                Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
            }

            // consume mana
            var caster = casterItem ?? GetEquippedWand();  // TODO: persist this from the beginning, since this is done with delay

            var isWeaponSpell = casterItem != null;

            var itemCaster = isWeaponSpell ? caster : null;

            if (!isWeaponSpell)
                UpdateVitalDelta(Mana, -(int)manaUsed);
            else
            {
                if (itemCaster != null)
                    itemCaster.ItemCurMana -= (int)manaUsed;
                else
                    castingPreCheckStatus = CastingPreCheckStatus.CastFailed;
            }

            // consume spell components
            if (!isWeaponSpell)
                TryBurnComponents(spell);

            // check windup move distance cap
            var dist = StartPos.Distance(PhysicsObj.Position);

            // only PKs affected by these caps?
            if (dist > Windup_MaxMove && PlayerKillerStatus != PlayerKillerStatus.NPK)
            {
                //player.Session.Network.EnqueueSend(new GameEventWeenieError(player.Session, WeenieError.YouHaveMovedTooFar));
                Session.Network.EnqueueSend(new GameMessageSystemChat("Your movement disrupted spell casting!", ChatMessageType.Magic));

                EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.Fizzle, 0.5f));

                if (finishCast)
                    FinishCast();

                return;
            }

            var pk_error = CheckPKStatusVsTarget(target, spell);
            if (pk_error != null)
                castingPreCheckStatus = CastingPreCheckStatus.InvalidPKStatus;

            switch (castingPreCheckStatus)
            {
                case CastingPreCheckStatus.Success:

                    if ((spell.Flags & SpellFlags.FellowshipSpell) == 0)
                        CreatePlayerSpell(target, spell, isWeaponSpell);
                    else
                    {
                        var fellows = GetFellowshipTargets();
                        foreach (var fellow in fellows)
                            CreatePlayerSpell(fellow, spell, isWeaponSpell);
                    }

                    // handle self procs
                    if (spell.IsHarmful && target != this)
                        TryProcEquippedItems(this, this, true, caster);

                    break;

                case CastingPreCheckStatus.InvalidPKStatus:

                    if (spell.NumProjectiles > 0)
                        HandleCastSpell(spell, target, itemCaster, caster, isWeaponSpell);
                    break;

                default:
                    EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.Fizzle, 0.5f));
                    SendWeenieError(WeenieError.YourSpellFizzled);
                    break;
            }


            if (pk_error != null && spell.NumProjectiles == 0)
            {
                Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(Session, pk_error[0], target.Name));

                if (target is Player targetPlayer)
                    targetPlayer.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(targetPlayer.Session, pk_error[1], Name));
            }

            if (finishCast)
                FinishCast();
        }

        public void FinishCast()
        {
            var hasWindupGestures = MagicState.CastSpellParams?.HasWindupGestures ?? true;
            var castGesture = MagicState.CastGesture;

            if (FastTick)
                castGesture = hasWindupGestures ? CurrentMotionState.MotionState.ForwardCommand : MagicState.CastGesture;

            var selfTarget = !hasWindupGestures && MagicState.CastSpellParams.Target == this;

            MagicState.OnCastDone();

            IsBusy = true;

            var queue = ServerConfig.spellcast_recoil_queue.Value;

            if (queue)
                MagicState.CanQueue = true;

            if (FastTick)
            {
                var fastbuff = selfTarget && ServerConfig.fastbuff.Value;

                // return to magic ready stance
                var actionChain = new ActionChain();
                EnqueueMotion(actionChain, MotionCommand.Ready, 1.0f, true, castGesture, false, fastbuff);
                actionChain.AddAction(this, ActionType.PlayerMagic_ReturnToReadyStance, () =>
                {
                    IsBusy = false;
                    SendUseDoneEvent();

                    if (queue)
                        HandleCastQueue();

                    //Console.WriteLine("====================================");
                });
                actionChain.EnqueueChain();
            }
            else
            {
                // temporarily old version:

                // return to magic combat stance
                var returnStance = new Motion(MotionStance.Magic, MotionCommand.Ready, 1.0f);
                EnqueueBroadcastMotion(returnStance);

                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(1.0f);   // TODO: get actual recoil timing
                actionChain.AddAction(this, ActionType.PlayerMagic_ReturnToReadyStance, () => {

                    IsBusy = false;
                    SendUseDoneEvent();

                    if (queue)
                        HandleCastQueue();
                });
                actionChain.EnqueueChain();
            }

        }

        /// <summary>
        /// Called by the missile hit path (Player_Combat.DamageTarget) when
        /// <see cref="HasExplosiveArrowCharm"/> is active and an arrow successfully damages an enemy.
        /// Fires Ring of Exploding Magma as a server-side radius AOE centered on the caster,
        /// mirroring the ring spell AOE system used by Rocky Shrapnel / Ring of Agony.
        /// </summary>
        internal void TryApplyExplosiveArrowProc(Creature target, float arrowDamage, DamageType arrowDamageType, WorldObject projectile = null)
        {
            // Global kill-switch — /charm explosivearrow false|off disables the charm server-wide
            if (!CharmSettingsManager.ExplosiveArrow.Enabled)
                return;

            if (!HasExplosiveArrowCharm)
                return;

            var ea = CharmSettingsManager.ExplosiveArrow;

            // Enforce max arrows per shot limit if projectile has an attack sequence
            if (projectile != null && projectile.ProjectileAttackSequence.HasValue)
            {
                var seq = projectile.ProjectileAttackSequence.Value;
                _explosiveArrowProcsPerAttack.TryGetValue(seq, out var count);

                if (count >= ea.MaxArrows)
                    return; // Reached limit for this shot!

                // Keep the dictionary small: prune sequences older than the current attack sequence - 2
                if (_explosiveArrowProcsPerAttack.Count > 10)
                {
                    var keysToRemove = new List<int>();
                    foreach (var key in _explosiveArrowProcsPerAttack.Keys)
                    {
                        if (key < AttackSequence - 2)
                            keysToRemove.Add(key);
                    }
                    foreach (var key in keysToRemove)
                        _explosiveArrowProcsPerAttack.Remove(key);
                }

                _explosiveArrowProcsPerAttack[seq] = count + 1;
            }

            var spellId = GetRingSpellForDamageType(arrowDamageType);
            if (spellId == 0) return; // unsupported damage type — no matching ring spell

            var spell   = new Spell(spellId);

            ActiveCharmLevels.TryGetValue(CharmAbilityRegistry.ExplosiveArrowCharmAbilityId, out var level);
            if (level < 1) level = 1;

            // Read tier multipliers from CharmSettingsManager (tunable at runtime via /charm explosivearrow)
            float minMult, maxMult;
            if (level == 2)
            {
                minMult = ea.T2Min;
                maxMult = ea.T2Max;
            }
            else if (level == 3)
            {
                minMult = ea.T3Min;
                maxMult = ea.T3Max;
            }
            else
            {
                minMult = ea.T1Min;
                maxMult = ea.T1Max;
            }

            var flatDmg = (float)(arrowDamage * ThreadSafeRandom.Next(minMult, maxMult));

            // Delay between arrow hit and ring detonation (tunable via /charm explosivearrow delay <seconds>)
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(ea.Delay);
            actionChain.AddAction(this, ActionType.PlayerMagic_DoCastSpell, () =>
            {
                // Guard: player or target may have died/logged out/been destroyed during the delay.
                if (IsDead || IsDestroyed || target.IsDead || target.IsDestroyed || target.Location == null) return;

                // Use target's current position for both the visual and the damage origin
                // so the ring always detonates where the target actually is at fire time.
                CreateSpellProjectiles(spell, null, null, false, true, 0, originOverride: target);
                ApplyRingSpellAreaDamage(spell, target.Location,
                    radiusOverride: ea.Radius,
                    heightOverride: ea.Height,
                    flatDamage:     flatDmg,
                    scanOrigin:     this,    // Scan the player's reliable ObjMaint (always populated) instead of target's (empty on static dummies like Winning Idol)
                    fromProc:       true);   // CR-4: suppress War Magic proficiency tick for procs
            });
            actionChain.EnqueueChain();
        }

        /// <summary>
        /// Redirects Tectonic Rifts I/II to Rocky Shrapnel (priority) or Ring of Unspeakable Agony (fallback)
        /// when the corresponding charm is active and the spell is known. Called at the very start of both
        /// CreatePlayerSpell overloads so all downstream validation (range, components, mana) runs against
        /// the redirected spell rather than the original.
        /// </summary>
        private Spell TryRedirectTectonicRifts(Spell spell)
        {
            if (spell.Id != SpellId_TectonicRiftsI && spell.Id != SpellId_TectonicRiftsII)
                return spell;

            // Global kill-switch checks — /charm shrapnel false|off or /charm agony false|off
            if (HasShrapnelCharm && CharmSettingsManager.Shrapnel.Enabled && SpellIsKnown(SpellId_RockyShrapnel))
                return new Spell(SpellId_RockyShrapnel);   // Rocky Shrapnel — priority

            if (HasAgonyCharm && CharmSettingsManager.Agony.Enabled && SpellIsKnown(SpellId_RingOfAgony))
                return new Spell(SpellId_RingOfAgony);     // Ring of Unspeakable Agony — fallback

            return spell;
        }

        /// <summary>
        /// Method used for handling player targeted spell casts
        /// </summary>
        /// <param name="builtInSpell">If TRUE, casting a built-in spell from a weapon</param>
        private bool CreatePlayerSpell(WorldObject target, TargetCategory targetCategory, Spell spell, WorldObject casterItem)
        {
            var creatureTarget = target as Creature;

            spell = TryRedirectTectonicRifts(spell);

            // Ring of Agony and Shrapnel fire a 360° ring from the caster.  If cast through the
            // targeted path the selected enemy is passed down to CreateSpellProjectiles, causing
            // all ring projectiles to converge on that single creature instead of radiating
            // outward.  Route through the untargeted overload so target = null and the ring hits
            // everything in range as intended.
            if (spell.Id == SpellId_RingOfAgony || spell.Id == SpellId_RockyShrapnel)
                return CreatePlayerSpell(spell);

            if (!IsValidSpell(spell, casterItem != null))
                return false;

            if (!VerifySpellSchoolSuppression(spell))
                return false;

            if (!VerifySpellTarget(spell, target))
                return false;

            // if casting implement has spell built in,
            // use spellcraft from the item, instead of player's magic skill?
            var caster = casterItem ?? GetEquippedWand();
            var isWeaponSpell = casterItem != null && IsWeaponSpell(spell.Id, casterItem);

            // Grab player's skill level in the spell's Magic School
            var magicSkill = GetCreatureSkill(spell.School).Current;
            if (isWeaponSpell && caster.ItemSpellcraft != null)
                magicSkill = (uint)caster.ItemSpellcraft;

            // verify spell range
            if (!VerifySpellRange(target, targetCategory, spell, casterItem, magicSkill))
                return false;

            // get casting pre-check status
            var castingPreCheckStatus = GetCastingPreCheckStatus(spell, magicSkill, isWeaponSpell);

            // calculate mana usage
            if (!CalculateManaUsage(castingPreCheckStatus, spell, target, casterItem, out var manaUsed))
                return false;

            // spell words
            DoSpellWords(spell, isWeaponSpell);

            var spellChain = new ActionChain();
            //StartPos = new Physics.Common.Position(PhysicsObj.Position);

            // do wind-up gestures: fastcast has no windup (creature enchantments)
            DoWindupGestures(spell, isWeaponSpell, spellChain);

            // cast spell
            DoCastGesture(spell, casterItem, spellChain);

            MagicState.SetCastParams(spell, casterItem, magicSkill, manaUsed, target, castingPreCheckStatus);

            if (!FastTick)
                spellChain.AddAction(this, ActionType.PlayerMagic_DoCastSpell, () => DoCastSpell(MagicState));

            spellChain.EnqueueChain();

            return true;
        }

        public List<Player> GetFellowshipTargets()
        {
            if (Fellowship != null)
                return Fellowship.GetFellowshipMembers().Values.ToList();
            else
                return new List<Player>() { this };
        }

        private void CreatePlayerSpell(WorldObject target, Spell spell, bool isWeaponSpell)
        {
            var targetCreature = target as Creature;
            var targetPlayer = target as Player;

            LastSuccessCast_School = spell.School;
            LastSuccessCast_Time = Time.GetUnixTime();

            var caster = GetEquippedWand();

            var itemCaster = isWeaponSpell ? caster : null;

            // verify after windup, still consumes mana
            if (spell.MetaSpellType == SpellType.Dispel && !VerifyDispelPKStatus(this, target))
                return;

            switch (spell.School)
            {
                case MagicSchool.ItemEnchantment:

                    TryCastItemEnchantment_WithRedirects(spell, target, itemCaster);

                    // use target resistance?
                    Proficiency.OnSuccessUse(this, GetCreatureSkill(Skill.ItemEnchantment), spell.PowerMod);

                    if (spell.IsHarmful)
                    {
                        var playerRedirect = targetPlayer;
                        if (playerRedirect == null && target?.WielderId != null)
                            playerRedirect = CurrentLandblock?.GetObject(target.WielderId.Value) as Player;

                        if (playerRedirect != null)
                            UpdatePKTimers(this, playerRedirect);
                    }
                    break;

                default:

                    if (!spell.IsProjectile)
                    {
                        if (targetPlayer == null)
                            OnAttackMonster(targetCreature, spell.IsHarmful);

                        if (TryResistSpell(target, spell, itemCaster))
                            break;

                        if (targetCreature != null && targetCreature.NonProjectileMagicImmune)
                        {
                            Session.Network.EnqueueSend(new GameMessageSystemChat($"You fail to affect {targetCreature.Name} with {spell.Name}", ChatMessageType.Magic));
                            break;
                        }
                    }

                    HandleCastSpell(spell, target, itemCaster, caster, isWeaponSpell);

                    if (!spell.IsProjectile)
                    {
                        if (spell.IsHarmful)
                        {
                            if (targetCreature != null)
                                Proficiency.OnSuccessUse(this, GetCreatureSkill(spell.School), targetCreature.GetCreatureSkill(Skill.MagicDefense).Current);

                            // handle target procs
                            if (targetCreature != null && targetCreature != this)
                                TryProcEquippedItems(this, targetCreature, false, caster);

                            if (targetPlayer != null)
                                UpdatePKTimers(this, targetPlayer);
                        }
                        else
                            Proficiency.OnSuccessUse(this, GetCreatureSkill(spell.School), spell.PowerMod);
                    }

                    break;
            }
        }

        /// <summary>
        /// Method used for handling player untargeted spell casts
        /// </summary>
        private bool CreatePlayerSpell(Spell spell)
        {
            spell = TryRedirectTectonicRifts(spell);

            if (!IsValidSpell(spell))
                return false;

            if (!VerifySpellSchoolSuppression(spell))
                return false;

            // get player's current magic skill
            var magicSkill = GetCreatureSkill(spell.School).Current;

            var castingPreCheckStatus = GetCastingPreCheckStatus(spell, magicSkill, false);

            // calculate mana usage
            if (!CalculateManaUsage(castingPreCheckStatus, spell, null, null, out var manaUsed))
                return false;

            // begin spellcasting
            DoSpellWords(spell, false);

            var spellChain = new ActionChain();

            //StartPos = new Physics.Common.Position(PhysicsObj.Position);

            // do wind-up gestures: fastcast has no windup (creature enchantments)
            DoWindupGestures(spell, false, spellChain);

            // do cast gesture
            DoCastGesture(spell, null, spellChain);

            // cast untargeted spell
            MagicState.SetCastParams(spell, null, magicSkill, manaUsed, null, castingPreCheckStatus);

            if (!FastTick)
                spellChain.AddAction(this, ActionType.PlayerMagic_DoCastSpell, () => DoCastSpell(MagicState));

            spellChain.EnqueueChain();

            return true;
        }

        public void TryBurnComponents(Spell spell)
        {
            if (SafeSpellComponents || ServerConfig.safe_spell_comps.Value)
                return;

            // ILT: Infinite Casting Stone — passive bypass while charm is in inventory
            if (HasInfiniteCasting && CharmSettingsManager.InfiniteCasting.Enabled)
                return;

            var burned = spell.TryBurnComponents(this);
            if (burned.Count == 0) return;

            // decrement components
            for (var i = burned.Count - 1; i >= 0; i--)
            {
                var component = burned[i];

                if (!SpellFormula.SpellComponentsTable.SpellComponents.TryGetValue(component, out var spellComponent))
                {
                    log.Error($"{Name}.TryBurnComponents(): Couldn't find SpellComponent {component}");
                    continue;
                }

                var wcid = Spell.GetComponentWCID(component);
                if (wcid == 0) continue;

                var item = GetInventoryItemsOfWCID(wcid).FirstOrDefault();
                if (item == null)
                {
                    if (SpellComponentsRequired && ServerConfig.require_spell_comps.Value)
                        log.Warn($"{Name}.TryBurnComponents({spellComponent.Name}): not found in inventory");
                    else
                        burned.RemoveAt(i);

                    continue;
                }
                if (item.UnlimitedUse)
                {
                    burned.RemoveAt(i); // don't report infinite-use items as consumed
                    continue;
                }

                TryConsumeFromInventoryWithNetworking(item, 1);
            }

            if (burned.Count == 0)
                return;

            // send message to player
            var msg = Spell.GetConsumeString(burned);
            Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Magic));
        }

        /// <summary>
        /// Returns TRUE if the player has the required number of components to cast spell
        /// </summary>
        public bool HasComponentsForSpell(Spell spell)
        {
            spell.Formula.GetPlayerFormula(this);

            if (!SpellComponentsRequired || !ServerConfig.require_spell_comps.Value)
                return true;

            // ILT: Infinite Casting Stone — passive bypass while charm is in inventory
            if (HasInfiniteCasting && CharmSettingsManager.InfiniteCasting.Enabled)
                return true;

            var requiredComps = spell.Formula.GetRequiredComps();

            foreach (var kvp in requiredComps)
            {
                var wcid = kvp.Key;
                var required = kvp.Value;

                var available = GetNumInventoryItemsOfWCID(wcid);

                if (required > available)
                    return false;
            }
            return true;
        }

        public void HandleMotionDone_Magic(uint motionID, bool success)
        {
            //Console.WriteLine($"HandleMotionDone_Magic({(MotionCommand)motionID}, {success})");

            if (!FastTick || !MagicState.IsCasting) return;

            if (motionID == (uint)MagicState.CastGesture)
            {
                if (RecordCast.Enabled)
                    RecordCast.Log($"{Name}.HandleMotionDone_Magic({(MotionCommand)motionID}, {success}) - cast gesture done");

                MagicState.CastMotionDone = true;

                var actionChain = new ActionChain();
                actionChain.AddDelayForOneTick();
                actionChain.AddAction(this, ActionType.PlayerMagic_DoCastSpellOnMotionDone, () =>
                {
                    if (!MagicState.IsCasting)
                        return;

                    MagicState.AlwaysTurn = true;

                    DoCastSpell(MagicState);
                });
                actionChain.EnqueueChain();
            }
        }

        public void OnMoveComplete_Magic(WeenieError status)
        {
            //Console.WriteLine($"OnMoveComplete_Magic({status})");

            if (!FastTick || !MagicState.IsCasting || !MagicState.TurnStarted)
                return;

            // this occurs after the player is done turning
            // before the windup, or after the first half of the cast motion
            // either completed or cancelled

            if (RecordCast.Enabled)
                RecordCast.Log($"{Name}.OnMoveComplete_Magic({status}) - DoCastSpell");

            MagicState.IsTurning = false;

            var checkAngle = status != WeenieError.None;

            var actionChain = new ActionChain();
            actionChain.AddDelayForOneTick();
            actionChain.AddAction(this, ActionType.PlayerMagic_OnMoveComplete, () =>
            {
                if (!MagicState.IsCasting) return;

                if (!MagicState.CastMotionDone)
                    DoWindup(MagicState.WindupParams, checkAngle);
                else
                    DoCastSpell(MagicState, checkAngle);
            });
            actionChain.EnqueueChain();
        }

        public void FailCast(bool tryFizzle = true)
        {
            var parms = MagicState.CastSpellParams;

            var werror = WeenieError.None;

            if (parms != null && tryFizzle)
            {
                DoCastSpell_Inner(parms.Spell, parms.CasterItem, parms.ManaUsed, parms.Target, CastingPreCheckStatus.CastFailed, false);

                werror = WeenieError.YourSpellFizzled;
            }
            SendUseDoneEvent(werror);

            MagicState.OnCastDone();
        }

        public void OnTurnRelease()
        {
            MagicState.PendingTurnRelease = false;

            if (!MagicState.CastMotionDone)
                DoWindup(MagicState.WindupParams, true);
            else
                DoCastSpell(MagicState, true);
        }

        public bool VerifyCastRadius()
        {
            if (MagicState.CastGestureStartTime != DateTime.MinValue)
            {
                var dist = StartPos.Distance(PhysicsObj.Position);

                if (dist > Windup_MaxMove && PlayerKillerStatus != PlayerKillerStatus.NPK)
                {
                    FailCast();
                    return false;
                }
            }
            return true;
        }

        public void CheckTurn()
        {
            // verify cast radius while manually moving after windup
            if (!VerifyCastRadius())
                return;

            if (TurnTarget != null && IsWithinAngle(TurnTarget))
            {
                if (MagicState.PendingTurnRelease)
                    OnTurnRelease();
                else
                    PhysicsObj.StopCompletely(false);
            }
        }

        public void HandleCastQueue()
        {
            MagicState.CanQueue = false;

            if (MagicState.CastQueue != null)
            {
                if (MagicState.CastQueue.Type == CastQueueType.Targeted)
                    HandleActionCastTargetedSpell(MagicState.CastQueue.TargetGuid, MagicState.CastQueue.SpellId, MagicState.CastQueue.CasterItem);
                else
                    HandleActionMagicCastUnTargetedSpell(MagicState.CastQueue.SpellId);
            }
        }

        public static bool VerifyNonComponentTargetType(Spell spell, WorldObject target)
        {
            // untargeted spell projectiles
            if (target == null)
                return spell.NonComponentTargetType == ItemType.None;

            switch (spell.NonComponentTargetType)
            {
                case ItemType.Creature:
                    return target is Creature;

                // banes / lures
                case ItemType.Vestements:
                    //return target is Clothing || target.IsShield;
                    return target is Creature || target is Clothing || target.IsShield;

                case ItemType.Weapon:
                    //return target is MeleeWeapon || target is MissileLauncher;
                    return target is Creature || target is MeleeWeapon || target is MissileLauncher;

                case ItemType.Caster:
                    //return target is Caster;
                    return target is Creature || target is Caster;

                case ItemType.WeaponOrCaster:
                    //return target is MeleeWeapon || target is MissileLauncher || target is Caster;
                    return target is Creature || target is MeleeWeapon || target is MissileLauncher || target is Caster;

                case ItemType.Portal:

                    if (spell.MetaSpellType == SpellType.PortalRecall || spell.MetaSpellType == SpellType.PortalSummon)
                        return target is Creature;
                    else
                        return target is Portal;

                case ItemType.LockableMagicTarget:
                    return target is Door || target is Chest;

                // Essence Lull?
                case ItemType.Item:
                    return !(target is Creature);

                case ItemType.LifeStone:
                    return target is Lifestone;
            }

            //log.Error($"VerifyNonComponentTargetType({spell.Id} - {spell.Name}, {target.Name}) - unexpected NonComponentTargetType {spell.NonComponentTargetType}");
            return false;
        }

        /// <summary>
        /// Sends a chat message with respect to SquelchManager
        /// </summary>
        public void SendChatMessage(WorldObject source, string msg, ChatMessageType msgType)
        {
            if (Session == null)
                return;

            if (!SquelchManager.Squelches.Contains(source, msgType))
                Session.Network.EnqueueSend(new GameMessageSystemChat(msg, msgType));
        }

        // ── Ring AOE radius (meters). Scaled by AoeRangeMultiplier charm (PropertyFloat 9048). ──
        internal const float DefaultRingAoeRadius    =  5.8f;
        // ── Max vertical delta (meters) — prevents hitting creatures on a different floor. ──
        internal const float RingAoeMaxHeightDelta   =  7.0f;

        /// <summary>
        /// Applies one war-magic damage roll to every valid creature within the ring spell's
        /// effective radius.  Called immediately after the visual ring projectiles are spawned;
        /// the projectiles themselves are made non-damaging in SpellProjectile.OnCollideObject.
        /// <para>
        /// <paramref name="scanOrigin"/>: when provided, the creature visibility scan uses that object's
        /// ObjMaint instead of the caster's.  Used by proc paths (e.g. Explosive Arrow) where the
        /// detonation center is the target, not the caster.
        /// </para>
        /// <para>
        /// <paramref name="fromProc"/>: when true, suppresses the War Magic proficiency tick so proc
        /// hits do not grant magic skill XP.
        /// </para>
        /// <para>
        /// <paramref name="lifeProjectileDamage"/>: the vital amount the caster drained at cast time.
        /// Required for LifeProjectile ring spells (e.g. 3818 - Curse of Raven Fury), whose damage is
        /// derived from the drain rather than from the spell's Min/Max — see SpellProjectile.LifeProjectileDamage.
        /// </para>
        /// </summary>
        /// <summary>Element word for the Cast on Strike ring line. Mirrors SpellProjectile.ZcElementWord;
        /// the two message sites must read identically.</summary>
        private static string ZcElementWord(DamageType dt)
        {
            if (dt.HasFlag(DamageType.Slash)) return "slash";
            if (dt.HasFlag(DamageType.Pierce)) return "pierce";
            if (dt.HasFlag(DamageType.Bludgeon)) return "bludgeon";
            if (dt.HasFlag(DamageType.Acid)) return "acid";
            if (dt.HasFlag(DamageType.Cold)) return "cold";
            if (dt.HasFlag(DamageType.Electric)) return "electric";
            if (dt.HasFlag(DamageType.Fire)) return "fire";
            if (dt.HasFlag(DamageType.Nether)) return "nether";
            return "magic";
        }

        internal void ApplyRingSpellAreaDamage(Spell spell, Position centerOverride = null, float radiusOverride = 0f, float heightOverride = 0f, float flatDamage = 0f, WorldObject scanOrigin = null, bool fromProc = false, float lifeProjectileDamage = 0f, double procBaseDamage = 0, WorldObject procWeapon = null, double procVariance = 0)
        {
            var center = centerOverride ?? Location;
            if (center == null) return;

            // Determine proc count based on spell ID and chance
            var procCount = 1;
            if (spell.Id == SpellId_RockyShrapnel)
            {
                var roll = ThreadSafeRandom.Next(0.0f, 1.0f);
                var tripleChance = CharmSettingsManager.Shrapnel.TripleChance;
                var doubleChance = CharmSettingsManager.Shrapnel.DoubleChance;
                if (roll < tripleChance)
                    procCount = 3;
                else if (roll < tripleChance + doubleChance)
                    procCount = 2;
            }
            else if (spell.Id == SpellId_RingOfAgony)
            {
                var roll = ThreadSafeRandom.Next(0.0f, 1.0f);
                var tripleChance = CharmSettingsManager.Agony.TripleChance;
                var doubleChance = CharmSettingsManager.Agony.DoubleChance;
                if (roll < tripleChance)
                    procCount = 3;
                else if (roll < tripleChance + doubleChance)
                    procCount = 2;
            }
            else if (!fromProc)
            {
                var roll = ThreadSafeRandom.Next(0.0f, 1.0f);
                var tripleChance = SmartRingSettingsManager.TripleChance;
                var doubleChance = SmartRingSettingsManager.DoubleChance;
                if (roll < tripleChance)
                    procCount = 3;
                else if (roll < tripleChance + doubleChance)
                    procCount = 2;
            }

            // Ring radius — radiusOverride used by proc paths (e.g. Explosive Arrow); otherwise default scaled by charm.
            var radius = radiusOverride > 0f
                ? radiusOverride
                : (spell.Id == SpellId_RockyShrapnel
                    ? CharmSettingsManager.Shrapnel.Radius
                    : (spell.Id == SpellId_RingOfAgony
                        ? CharmSettingsManager.Agony.Radius
                        : SmartRingSettingsManager.Radius * (float)(GetProperty(PropertyFloat.AoeRangeMultiplier) ?? 1.0f)));

            var attackSkill   = GetCreatureSkill(spell.School);
            var magicSkill    = attackSkill.Current;
            var resistanceType = Creature.GetResistanceType(spell.DamageType);
            // PROC WEAPON WINS OVER THE EQUIPPED WAND. GetEquippedWand() is right for a hand-cast ring,
            // but a ring fired as a weapon proc comes off a dagger/bow/wand that is NOT in the wand slot,
            // so this returned NULL and every weapon-derived term silently vanished - above all the REND,
            // since GetWeaponResistanceModifier bails on a null weapon. Measured in game 2026-08-27: the
            // arc landed 56,378 and the ring 23,237 off the same Fire/Fire-Rending dagger, a 2.43x gap
            // that is exactly the T11 rend band. Heritage bonus and the crit-damage mod were lost the
            // same way.
            var weapon        = procWeapon ?? GetEquippedWand();

            var isLifeProjectile = spell.MetaSpellType == ACE.Entity.Enum.SpellType.LifeProjectile;

            // CR-2: use scanOrigin's ObjMaint when the detonation center differs from the caster
            // (e.g. Explosive Arrow proc — ring spawns on the target, not the caster).
            // Without this, enemies near the blast point that aren't in the caster's physics
            // visibility list would be silently skipped.
            var scanSource = scanOrigin ?? this;
            var visibleCreatures = scanSource.PhysicsObj.ObjMaint.GetKnownObjectsValuesAsCreature();
            var dbgHit = 0; var dbgResist = 0;

            // Compute attribute bonus once — Focus/Self don't change mid-cast.
            var attribBonus = 1.0f;
            attribBonus += SkillFormula.GetAttributeMod((int)Focus.Current) * SpellProjectile.DefaultSpellAttributeMult;
            attribBonus += SkillFormula.GetAttributeMod((int)Self.Current)  * SpellProjectile.DefaultSpellAttributeMult;

            // Pre-calculate target-independent combat ratings for optimization.
            var heritageMod = GetHeritageBonus(weapon) ? 1.05f : 1.0f;
            var baseDamageRatingMod = Creature.GetPositiveRatingMod(GetDamageRating());
            var critDamageRatingMod = Creature.GetPositiveRatingMod(GetCritDamageRating());
            var pkDamageRatingMod = Creature.GetPositiveRatingMod(GetPKDamageRating());

            foreach (var creature in visibleCreatures)
            {
                if (creature == null || creature == this) continue;
                if (!creature.IsAlive)                    continue;  // skip corpses from this or prior casts
                if (creature.Location == null)            continue;

                // Height and distance gate — heightOverride used by proc paths.
                var heightCap = heightOverride > 0f
                    ? heightOverride
                    : (spell.Id == SpellId_RockyShrapnel
                        ? CharmSettingsManager.Shrapnel.Height
                        : (spell.Id == SpellId_RingOfAgony
                            ? CharmSettingsManager.Agony.Height
                            : SmartRingSettingsManager.Height));
                var dz = Math.Abs(center.PositionZ - creature.Location.PositionZ);
                if (dz > heightCap) continue;
                if (center.Distance2D(creature.Location) > radius) continue;

                if (!CanDamage(creature)) continue;

                // PK status check (mirrors SpellProjectile.OnCollideObject).
                var pkError = CheckPKStatusVsTarget(creature, spell);
                if (pkError != null) continue;

                // Notify the creature that it was attacked (triggers aggro) whether or not it resisted.
                if (creature is not Player)
                    OnAttackMonster(creature, spell.IsHarmful);

                // Run the loop for multi-procs
                for (var procIdx = 0; procIdx < procCount; procIdx++)
                {
                    if (!creature.IsAlive) break; // target died on a previous proc!

                    // Resist check — sends the resist message automatically.  A resisted spell still
                    // lands if the caster's Overpower procs, matching SpellProjectile.CalculateDamage,
                    // which only bails on `resisted && !overpower`.
                    // For a Cast on Strike proc the WEAPON is the itemCaster, so the resist rolls
                    // against its ItemSpellcraft (the 9999 stamp) exactly as the arc path does via
                    // resistSource - passing null here rolled the PLAYER's own War/Void skill, the
                    // precise failure the spellcraft stamp exists to prevent (fixed 2026-08-28, the
                    // sixth everything-must-be-done-TWICE bug). Hand-cast rings keep null = own skill.
                    var resisted = TryResistSpell(creature, spell, fromProc && procBaseDamage > 0 ? weapon : null, true);
                    if (resisted && !(Overpower != null && Creature.GetOverpower(this, creature)))
                    {
                        dbgResist++;
                        continue;
                    }

                    // --- 1. Intercept Enchantment Projectiles (e.g., debuffs like Shroud of Darkness) ---
                    if (spell.MetaSpellType == ACE.Entity.Enum.SpellType.EnchantmentProjectile)
                    {
                        CreateEnchantment(creature, this, weapon, spell, false, fromProc);
                        DoSpellEffects(spell, this, creature, true);
                        dbgHit++;
                        continue;
                    }

                    // ── Damage calculation (parity with SpellProjectile.CalculateDamage) ──
                    var criticalHit      = false;
                    var critDamageBonus  = 0.0f;
                    var skillBonus       = 0.0f;
                    var isPvP            = creature is Player;

                    // Life projectiles (e.g. 3818 - Curse of Raven Fury) take their damage from the vital
                    // the caster drained at cast time, NOT from the spell's Min/Max — those are near-zero
                    // on such spells.  Mirrors SpellProjectile.CalculateDamage's LifeProjectile branch.
                    var lifeMagicDamage = isLifeProjectile ? lifeProjectileDamage * spell.DamageRatio : 0.0f;

                    // Crit chance — 10% base since 2026-08-29 (unified with melee/missile) + player
                    // crit rating, mitigated by target resist rating.
                    var critChance = GetWeaponMagicCritFrequency(weapon, this as Creature, attackSkill, creature);
                    if (ThreadSafeRandom.Next(0.0f, 1.0f) < critChance)
                    {
                        // AugmentationCriticalDefense check (PvP only — 5% per aug rank vs player attacker).
                        var critDefended = false;
                        if (creature is Player tgtPlayer && tgtPlayer.AugmentationCriticalDefense > 0)
                        {
                            var critDefChance = tgtPlayer.AugmentationCriticalDefense * 0.05f; // sourcePlayer != null → 5%
                            if (critDefChance > ThreadSafeRandom.Next(0.0f, 1.0f))
                                critDefended = true;
                        }

                        if (!critDefended)
                        {
                            criticalHit = true;
                            // UNIFIED CRIT MODEL (owner 2026-08-29, matching SpellProjectile): PvE
                            // crit = CritX x the fully composed base, computed AFTER the aug term
                            // below. Life: CritX x the drained base (0.5 coefficient gone). PvP
                            // keeps the retail halved-min rule.
                            // only the life / PvP branches use the mod here; the war/void PvE crit reads it
                            // once in the unified block below (review 2026-09-04: it was computed twice)
                            var earlyMod = isLifeProjectile || isPvP
                                ? GetWeaponCritDamageMod(weapon, this as Creature, attackSkill, creature)
                                : 0f;
                            critDamageBonus = isLifeProjectile
                                ? lifeMagicDamage * earlyMod
                                : (isPvP ? spell.MinDamage * 0.5f * earlyMod : 0f);
                        }
                    }

                    long baseDamage = 0;

                    if (isLifeProjectile)
                    {
                        // Luminance Life augment — added AFTER the crit bonus, matching SpellProjectile.
                        // Life projectiles get no skill-based damage bonus.
                        //
                        // EffectiveLifeAugCount, not the raw count: SpellProjectile.CalculateDamage -
                        // the path this whole method exists to mirror - reads the effective count, so
                        // reading the raw one here would put a Triune Weave holder's ring damage back
                        // out of step with their projectile damage. That is the same class of drift
                        // this method was written to fix.
                        //
                        // The War and Void branches below stay RAW on purpose: the Triune Weave only
                        // grants Creature, Item and Life, and the school charms deliberately add
                        // nothing to the capped War/Void stats.
                        if (EffectiveLifeAugCount >= 1)
                            lifeMagicDamage += EffectiveLifeAugCount;
                    }
                    else
                    {
                        // Skill-based damage bonus.
                        if (magicSkill > spell.Power)
                            skillBonus = spell.MinDamage * (magicSkill - spell.Power) / 1000.0f;

                        // Zone Control "Cast on Strike" ring slot: the authored B replaces the rolled
                        // spell base here exactly as it does on the projectile path. A ring's damage is
                        // applied by THIS method and never reaches SpellProjectile.CalculateDamage, so
                        // without this the ring fires on the spell's own base and the whole band is
                        // ignored - which is what the first in-game test was actually measuring.
                        baseDamage = procBaseDamage > 0
                            ? (long)Math.Round(procBaseDamage)
                            // unified crit: a PvE crit uses the MAX roll, matching SpellProjectile
                            : (criticalHit && !isPvP
                                ? spell.MaxDamage
                                : ThreadSafeRandom.Next(spell.MinDamage, spell.MaxDamage));

                        // Luminance augment — the pool MUST match the spell's school.  This previously
                        // added the War count unconditionally, which fed a caster's War pool into Void
                        // rings (e.g. Clouded Soul) and dropped their Void pool entirely.
                        //
                        // Effective counts, matching SpellProjectile: gems plus the school's charm.
                        long ringAugs = 0;
                        if (spell.School == MagicSchool.WarMagic)
                            ringAugs = EffectiveWarAugCount;
                        else if (spell.School == MagicSchool.VoidMagic)
                            ringAugs = EffectiveVoidAugCount;

                        // The proc aug cap (prop 9061), mirroring SpellProjectile.CalculateDamage:
                        // clamps the aug term for a Cast on Strike proc ONLY - a hand-cast ring is
                        // deliberately untouched. The ring path never read the cap before 2026-08-28
                        // (the fifth everything-must-be-done-TWICE bug on this card), so a capped
                        // weapon still delivered the full 0..17,150 term on every ring hit.
                        if (ringAugs > 0 && fromProc && procBaseDamage > 0)
                        {
                            // PER-SLOT since 2026-08-29: the ring reads its OWN cap (prop 9064), falling
                            // back to the old shared 9061 so pre-split test daggers keep theirs.
                            var ringAugCap = weapon?.GetProperty((ACE.Entity.Enum.Properties.PropertyFloat)ACE.Server.Managers.ZoneControl.ZoneLootMutator.ProcRingAugCapPropId)
                                ?? weapon?.GetProperty((ACE.Entity.Enum.Properties.PropertyFloat)ACE.Server.Managers.ZoneControl.ZoneLootMutator.ProcAugCapPropId);
                            if (ringAugCap.HasValue && ringAugCap.Value > 0)
                                ringAugs = Math.Min(ringAugs, (long)Math.Round(ringAugCap.Value));
                        }
                        if (ringAugs > 0)
                            baseDamage += ringAugs;
                    }

                    // Elemental modifier (wand element vs target).
                    var elementalMod = GetCasterElementalDamageModifier(weapon, this as Creature, creature, spell.DamageType);

                    // Slayer modifier — respects wand/creature slayer properties.
                    var slayerMod = GetWeaponCreatureSlayerModifier(weapon, this as Creature, creature);

                    // Weapon resistance mod — applies rending on wand to target resistance.
                    // The wand is deliberately NOT passed as the `weapon` arg below: SpellProjectile
                    // passes null there so a hollow wand's IgnoreMagicResist does not transfer to the
                    // spell and blanket-bypass the target's resistances.  Rending still applies — it
                    // rides in via weaponResistanceMod.
                    var weaponResistanceMod = GetWeaponResistanceModifier(weapon, this as Creature, attackSkill, spell.DamageType);
                    var resistanceMod = (float)Math.Max(0.0f, creature.GetResistanceMod(resistanceType, this, null, weaponResistanceMod));

                    // Void PvP modifier (matches SpellProjectile line ~602).
                    if (isPvP && spell.DamageType == DamageType.Nether)
                        resistanceMod *= (float)ServerConfig.void_pvp_modifier.Value;

                    // Absorb mod — Aegis shield / magic-absorbing items on target.
                    // Uses caster position as directional source (ring radiates from caster).
                    var absorbMod = SpellProjectile.GetAbsorbMod(this, creature);
                    if (isPvP && (creature.CombatMode == CombatMode.Melee || creature.CombatMode == CombatMode.Missile))
                    {
                        // Aegis is 72% effective in PvP (Forces of Nature patch).
                        absorbMod  = 1 - absorbMod;
                        absorbMod *= 0.72f;
                        absorbMod  = 1 - absorbMod;
                    }

                    // Damage selection:
                    // Per-hit variance on the WHOLE base - see SpellProjectile for the reasoning.
                    // Passed in rather than read off the weapon here, because this method is also the
                    // Explosive Arrow path and must not start reading Cast on Strike props for it.
                    if (procBaseDamage > 0 && procVariance > 0)
                    {
                        var v = Math.Clamp(procVariance, 0.0, 1.0);
                        baseDamage = (long)Math.Round(baseDamage * (1.0 - v * ThreadSafeRandom.Next(0.0f, 1.0f)));
                    }

                    // Cast on Strike: re-derive the crit bonus from the REPLACED base. Stock computes it
                    // from spell.MaxDamage, which for Cassius' Ring of Fire is 84 - so a crit added ~42
                    // to a ~9,440 base and a proc could not meaningfully crit at all. Crushing Blow was
                    // dead for the same reason: it feeds weaponCritDamageMod, which multiplied that same
                    // 84. Measured in game 2026-08-27: a CRIT ring hit landed 0.16 pct above a non-crit
                    // arc. Mirrors what the zone monster path already does after replacing a base.
                    //
                    // Placed AFTER the aug term so the crit scales with the whole base the hit uses, and
                    // BEFORE preModDamage, which is the only consumer.
                    // ══ UNIFIED CRIT (owner 2026-08-29): PvE war/void crit = CritX x the fully
                    // composed base (max roll or ring B, plus the aug term, post-variance/cap) -
                    // one formula for hand-casts and ring procs alike, replacing the 0.5f
                    // proc-only re-derive. mod = CritX - 1 (default 1.0 = retail's 2x).
                    if (criticalHit && !isLifeProjectile && !isPvP)
                        critDamageBonus = (baseDamage + skillBonus)
                            * GetWeaponCritDamageMod(weapon, this as Creature, attackSkill, creature);

                    var preModDamage = isLifeProjectile
                        ? lifeMagicDamage + critDamageBonus
                        : baseDamage + critDamageBonus + skillBonus;

                    var finalDamage = flatDamage > 0f
                        ? flatDamage
                        : preModDamage * elementalMod * slayerMod * resistanceMod * absorbMod * attribBonus;

                    // Sneak attack & heritage mods (mirrors SpellProjectile.DamageTarget).
                    if (flatDamage <= 0f)
                    {
                        var sneakAttackMod = GetSneakAttackMod(creature);
                        var damageRatingMod = Creature.AdditiveCombine(baseDamageRatingMod, heritageMod, sneakAttackMod);

                        var damageResistRatingMod = creature.GetDamageResistRatingMod(CombatType.Magic);

                        if (criticalHit)
                        {
                            var critDamageResistRatingMod = Creature.GetNegativeRatingMod(creature.GetCritDamageResistRating());

                            damageRatingMod = Creature.AdditiveCombine(damageRatingMod, critDamageRatingMod);
                            damageResistRatingMod = Creature.AdditiveCombine(damageResistRatingMod, critDamageResistRatingMod);
                        }

                        if (isPvP)
                        {
                            var pkDamageResistRatingMod = Creature.GetNegativeRatingMod(creature.GetPKDamageResistRating());

                            damageRatingMod = Creature.AdditiveCombine(damageRatingMod, pkDamageRatingMod);
                            damageResistRatingMod = Creature.AdditiveCombine(damageResistRatingMod, pkDamageResistRatingMod);
                        }

                        finalDamage *= damageRatingMod * damageResistRatingMod;
                    }

                    // Apply enrage damage reduction for the defender.
                    if (creature.IsEnraged)
                    {
                        var enrageReduction = creature.EnrageDamageReduction ?? 0.0f;
                        finalDamage *= (1.0f - enrageReduction);
                    }

                    // CombatPet mitigation — extra spell-only damage reduction from owner's summon aug.
                    if (finalDamage > 0 && spell.IsHarmful && creature is CombatPet combatPet)
                    {
                        var petMit = combatPet.GetSpellProjectileDamageTakenMultiplier();
                        if (petMit < 1.0f) finalDamage *= petMit;
                    }

                    if (finalDamage <= 0) continue;

                    // --- 2. Cloak Damage Reduction Proc ---
                    var equippedCloak = creature.EquippedCloak;
                    var percent = finalDamage / creature.Health.MaxValue;

                    if (equippedCloak != null && Cloak.HasDamageProc(equippedCloak) && Cloak.RollProc(equippedCloak, percent))
                    {
                        var reducedDamage = Cloak.GetReducedAmount(this, finalDamage);
                        Cloak.ShowMessage(creature, this, finalDamage, reducedDamage);
                        finalDamage = reducedDamage;
                        percent = finalDamage / creature.Health.MaxValue;
                    }

                    // [ZCPROC] diagnostic - see SpellProjectile for why. Fires only for our ring procs,
                    // and only with the zc_proc_diag server property on (off by default since 2026-09-04).
                    if (procBaseDamage > 0 && ServerConfig.zc_proc_diag.Value)
                        zcLog.Info($"[ZCPROC] ring spell={spell.Name} ({spell.Id}) B={baseDamage} " +
                                 $"weapon={(weapon?.Name ?? "NULL")} rendMod={weaponResistanceMod:F3} " +
                                 $"resistMod={resistanceMod:F4} attrib={attribBonus:F2} crit={criticalHit} " +
                                 $"final={finalDamage:F0} target={creature.Name}");

                    creature.TakeDamage(this, spell.DamageType, finalDamage, criticalHit);

                    // Only send "You hit X for Y" if the target survived
                    if (creature.IsAlive)
                    {
                        var displayAmt  = (uint)Math.Round(finalDamage);
                        var amtStr      = Creature.FormatDamage(displayAmt, DamageNumberFormat);
                        var pct         = (float)displayAmt / creature.Health.MaxValue;
                        string verb = null, plural = null;
                        Strings.GetAttackVerb(spell.DamageType, pct, ref verb, ref plural);
                        var critMsg     = criticalHit ? "Critical hit! " : "";

                        // Zone Control "Cast on Strike" reads with its OWN name and sentence here too.
                        // A ring proc never reaches SpellProjectile.DamageTarget - its damage is applied
                        // by this method - so the message had to be matched in BOTH places or the arc
                        // and the ring on the same weapon would print in two different formats, which is
                        // exactly what the first in-game test showed.
                        string zcRingName = null;
                        if (fromProc)
                            ACE.Server.Managers.ZoneControl.ZoneLootMutator.TryGetProcDisplayName(spell.Id, out zcRingName);

                        var attackerMsg = zcRingName != null
                            ? $"{critMsg}{zcRingName} hits {creature.Name} for {amtStr} {ZcElementWord(spell.DamageType)} damage."
                            : $"{critMsg}You {verb} {creature.Name} for {amtStr} points with {spell.Name}.";
                        if (!SquelchManager.Squelches.Contains(creature, ACE.Entity.Enum.ChatMessageType.Magic))
                            Session?.Network.EnqueueSend(new GameMessageSystemChat(attackerMsg, ACE.Entity.Enum.ChatMessageType.Magic));

                        // --- 3. Cloak Spell Proc ---
                        if (!fromProc && equippedCloak != null && Cloak.HasProcSpell(equippedCloak))
                        {
                            Cloak.TryProcSpell(creature, this, equippedCloak, percent);
                        }
                    }

                    dbgHit++;
                }
            }

            // Award one proficiency tick for the cast if at least one target was hit.
            if (dbgHit > 0 && !fromProc)
                Proficiency.OnSuccessUse(this, GetCreatureSkill(spell.School), spell.PowerMod);
        }
    }
}
