using System;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics;
using ACE.Server.Physics.Animation;
using ACE.Server.WorldObjects.Entity;

namespace ACE.Server.WorldObjects
{
    public class Healer : WorldObject
    {
        // TODO: change structure / maxstructure to int,
        // cast to ushort at network level
        public ushort? UsesLeft
        {
            get => Structure;
            set => Structure = value;
        }

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Healer(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Healer(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
            ObjectDescriptionFlags |= ObjectDescriptionFlag.Healer;
        }

        public override void HandleActionUseOnTarget(Player healer, WorldObject target)
        {
            if (healer.GetCreatureSkill(Skill.Healing).AdvancementClass < SkillAdvancementClass.Trained)
            {
                healer.SendUseDoneEvent(WeenieError.YouArentTrainedInHealing);
                return;
            }

            if (healer.IsBusy || healer.Teleporting || healer.suicideInProgress)
            {
                healer.SendUseDoneEvent(WeenieError.YoureTooBusy);
                return;
            }

            var targetPlayer = target as Player;
            var targetPet = target as CombatPet;

            if (targetPlayer == null)
            {
                if (targetPet == null || targetPet.P_PetOwner != healer || !targetPet.IsInMotelOrEncounter())
                {
                    healer.SendUseDoneEvent(WeenieError.YouCantHealThat);
                    return;
                }
            }
            else if (targetPlayer.Teleporting)
            {
                healer.SendUseDoneEvent(WeenieError.YouCantHealThat);
                return;
            }

            if (healer.IsJumping)
            {
                healer.SendUseDoneEvent(WeenieError.YouCantDoThatWhileInTheAir);
                return;
            }

            // ensure same PKType, although PK and PKLite players can heal NPKs:
            // https://asheron.fandom.com/wiki/Player_Killer
            // https://asheron.fandom.com/wiki/Player_Killer_Lite

            if (targetPlayer != null && targetPlayer.PlayerKillerStatus != healer.PlayerKillerStatus && targetPlayer.PlayerKillerStatus != PlayerKillerStatus.NPK)
            {
                healer.SendWeenieErrorWithString(WeenieErrorWithString.YouFailToAffect_NotSamePKType, targetPlayer.Name);
                healer.SendUseDoneEvent();
                return;
            }

            var targetCreature = targetPlayer != null ? (Creature)targetPlayer : targetPet;

            // ensure target creature vital < MaxValue
            var vital = targetCreature.GetCreatureVital(BoosterEnum);

            if (vital.Current == vital.MaxValue)
            {
                switch (vital.Vital)
                {
                    case PropertyAttribute2nd.MaxHealth:
                        healer.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(healer.Session, WeenieErrorWithString._IsAtFullHealth, target.Name));
                        break;
                    case PropertyAttribute2nd.MaxStamina:
                        healer.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(healer.Session, $"{target.Name} is already at full stamina!"));
                        break;
                    case PropertyAttribute2nd.MaxMana:
                        healer.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(healer.Session, $"{target.Name} is already at full mana!"));
                        break;
                }
                healer.SendUseDoneEvent();
                return;
            }

            /*if (!healer.Equals(targetPlayer))
            {
                // perform moveto
                healer.CreateMoveToChain(target, (success) => DoHealMotion(healer, targetCreature, success));
            }
            else
                DoHealMotion(healer, targetCreature, true);*/

            // MoveTo is now handled in base Player_Use
            DoHealMotion(healer, targetCreature, true);
        }

        public static readonly float Healing_MaxMove = 5.0f;

        public void DoHealMotion(Player healer, Creature target, bool success)
        {
            var targetPlayer = target as Player;
            if (!success || target.IsDead || (targetPlayer != null && (targetPlayer.Teleporting || targetPlayer.suicideInProgress)))
            {
                healer.SendUseDoneEvent();
                return;
            }

            healer.IsBusy = true;

            var motionCommand = healer.Equals(target) ? MotionCommand.SkillHealSelf : MotionCommand.SkillHealOther;

            var motion = new Motion(healer, motionCommand);
            var currentStance = healer.CurrentMotionState.Stance;
            var animLength = MotionTable.GetAnimationLength(healer.MotionTableId, currentStance, motionCommand);

            var startPos = new Physics.Common.Position(healer.PhysicsObj.Position);

            var actionChain = new ActionChain();
            //actionChain.AddAction(healer, () => healer.EnqueueBroadcastMotion(motion));
            actionChain.AddAction(healer, ActionType.Healer_SendMotion, () => healer.SendMotionAsCommands(motionCommand, currentStance));
            actionChain.AddDelaySeconds(animLength);
            actionChain.AddAction(healer, ActionType.Healer_DoHealing, () =>
            {
                // check healing move distance cap
                var endPos = new Physics.Common.Position(healer.PhysicsObj.Position);
                var dist = startPos.Distance(endPos);

                //Console.WriteLine($"Dist: {dist}");

                // only PKs affected by these caps?
                if (dist < Healing_MaxMove || healer.PlayerKillerStatus == PlayerKillerStatus.NPK)
                    DoHealing(healer, target);
                else
                    healer.Session.Network.EnqueueSend(new GameMessageSystemChat("Your movement disrupted healing!", ChatMessageType.Broadcast));

                healer.IsBusy = false;

                healer.SendUseDoneEvent();
            });

            healer.EnqueueMotion(actionChain, MotionCommand.Ready);

            actionChain.EnqueueChain();

            healer.NextUseTime = DateTime.UtcNow.AddSeconds(animLength);
        }

        public void DoHealing(Player healer, Creature target)
        {
            var targetPlayer = target as Player;
            if (target.IsDead || (targetPlayer != null && targetPlayer.Teleporting)) return;

            var remainingMsg = "";

            if (!UnlimitedUse)
            {
                UsesLeft--;
                var s = UsesLeft == 1 ? "" : "s";
                remainingMsg = UsesLeft > 0 ? $" Your {Name} has {UsesLeft} use{s} left." : $" Your {Name} is used up.";

                Value -= StructureUnitValue;

                if (Value < 0) // fix negative value
                    Value = 0;
            }

            var stackSize = new GameMessagePublicUpdatePropertyInt(this, PropertyInt.Structure, UsesLeft.Value);
            var targetName = healer == target ? "yourself" : target.Name;

            var vital = target.GetCreatureVital(BoosterEnum);

            // skill check
            var difficulty = 0;
            var skillCheck = DoSkillCheck(healer, target, vital, ref difficulty);
            if (!skillCheck)
            {
                var failMsg = new GameMessageSystemChat($"You fail to heal {targetName}.{remainingMsg}", ChatMessageType.Broadcast);
                healer.Session.Network.EnqueueSend(failMsg, stackSize);
                if (healer != target && targetPlayer?.Session != null)
                    targetPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"{healer.Name} fails to heal you.", ChatMessageType.Broadcast));
                if (UsesLeft <= 0 && !UnlimitedUse)
                    healer.TryConsumeFromInventoryWithNetworking(this, 1);
                return;
            }

            // heal up
            var healAmount = GetHealAmount(healer, target, vital, out var critical, out var staminaCost);

            healer.UpdateVitalDelta(healer.Stamina, (int)-staminaCost);
            target.UpdateVitalDelta(vital, healAmount);
            if (vital.Vital == PropertyAttribute2nd.MaxHealth)
                target.DamageHistory.OnHeal(healAmount);

            //if (target.Fellowship != null)
            //target.Fellowship.OnVitalUpdate(target);

            var healingSkill = healer.GetCreatureSkill(Skill.Healing);
            Proficiency.OnSuccessUse(healer, healingSkill, difficulty);

            var crit = critical ? "expertly " : "";
            var message = new GameMessageSystemChat($"You {crit}heal {targetName} for {healAmount} {BoosterEnum.ToString()} points.{remainingMsg}", ChatMessageType.Broadcast);

            healer.Session.Network.EnqueueSend(message, stackSize);

            if (healer != target && targetPlayer?.Session != null)
                targetPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"{healer.Name} heals you for {healAmount} {BoosterEnum.ToString()} points.", ChatMessageType.Broadcast));

            if (UsesLeft <= 0 && !UnlimitedUse)
                healer.TryConsumeFromInventoryWithNetworking(this, 1);
        }

        /// <summary>
        /// Determines if healer successfully heals target for attempt
        /// </summary>
        public bool DoSkillCheck(Player healer, Creature target, CreatureVital vital, ref int difficulty)
        {
            // skill check:
            // (healing skill + healing kit boost) * trainedMod
            // vs. damage * 2 * combatMod
            var healingSkill = healer.GetCreatureSkill(Skill.Healing);
            var trainedMod = healingSkill.AdvancementClass == SkillAdvancementClass.Specialized ? 1.5f : 1.1f;

            var combatMod = healer.CombatMode == CombatMode.NonCombat ? 1.0f : 1.1f;

            var effectiveSkill = (int)Math.Round((healingSkill.Current + BoostValue) * trainedMod);
            difficulty = (int)Math.Round((vital.MaxValue - vital.Current) * 2 * combatMod);

            var skillCheck = SkillCheck.GetSkillChance(effectiveSkill, difficulty);
            return skillCheck > ThreadSafeRandom.Next(0.0f, 1.0f);
        }

        /// <summary>
        /// Returns the healing amount for this attempt
        /// </summary>
        public uint GetHealAmount(Player healer, Creature target, CreatureVital vital, out bool criticalHeal, out uint staminaCost)
        {
            // factors: healing skill, healing kit bonus, stamina, critical chance
            var healingSkill = healer.GetCreatureSkill(Skill.Healing).Current;
            var healBase = healingSkill * (float)HealkitMod.Value;

            // todo: determine applicable range from pcaps
            var healMin = healBase * 0.2f;      // ??
            var healMax = healBase * 0.5f;
            var healAmount = ThreadSafeRandom.Next(healMin, healMax);

            // verify healing boost comes from target instead of healer?
            // sounds like target in LumAugHealingRating...
            var ratingMod = target.GetHealingRatingMod();

            healAmount *= ratingMod;

            // If target is a combat pet in the motel/encounter, scale heal amount proportionally to pet health so kits remain effective
            float petHealScale = 1.0f;
            if (target is CombatPet pet && pet.IsInMotelOrEncounter() && healer.Health.MaxValue > 0)
            {
                petHealScale = Math.Min(8.0f, (float)pet.Health.MaxValue / healer.Health.MaxValue);
                if (petHealScale > 1.0f)
                    healAmount *= petHealScale;
            }

            // chance for critical healing
            criticalHeal = ThreadSafeRandom.Next(0.0f, 1.0f) < 0.1f;
            if (criticalHeal) healAmount *= 2;

            // cap to missing vital
            var missingVital = vital.MaxValue - vital.Current;
            if (healAmount > missingVital)
                healAmount = missingVital;

            // stamina check? On the Q&A board a dev posted that stamina directly effects the amount of damage you can heal
            // low stam = less vital healed. I don't have exact numbers for it. Working through forum archive.

            // stamina cost: 1 stamina per 5 vital healed (based on unscaled heal so pets don't drain 250+ stamina)
            var staminaHealAmount = petHealScale > 1.0f ? (healAmount / petHealScale) : healAmount;
            staminaCost = (uint)Math.Round(staminaHealAmount / 5.0f);
            if (staminaCost > healer.Stamina.Current)
            {
                staminaCost = healer.Stamina.Current;
                healAmount = staminaCost * 5 * (petHealScale > 1.0f ? petHealScale : 1.0f);
            }
            return (uint)Math.Round(healAmount);
        }
    }
}
