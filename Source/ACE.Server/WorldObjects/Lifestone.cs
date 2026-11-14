using System;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    public class Lifestone : WorldObject
    {
        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Lifestone(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Lifestone(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        // ---- Personal Lifestone (2026-11-14): a stone raised by a player's item, usable by that player only -------------

        /// <summary>The only player who may attune to or tie this stone; null on every ordinary lifestone.</summary>
        public uint? PersonalLifestoneOwner
        {
            get => GetProperty(PropertyInstanceId.PersonalLifestoneOwner);
            set { if (!value.HasValue) RemoveProperty(PropertyInstanceId.PersonalLifestoneOwner); else SetProperty(PropertyInstanceId.PersonalLifestoneOwner, value.Value); }
        }

        /// <summary>Unix time the stone fades. TimeToRot normally removes it first; this backs that up when the landblock
        /// heartbeat has been slow (dormant), so a stale stone can never be used.</summary>
        public double? PersonalLifestoneExpiresAt;

        public bool IsPersonalLifestone => PersonalLifestoneOwner.HasValue;

        /// <summary>
        /// True when this player may attune to or tie this stone. Ordinary lifestones: always. A personal one: only its
        /// owner, and only while it has not faded. Sends the refusal itself.
        /// </summary>
        public bool CheckPersonalLifestoneAccess(Player player)
        {
            if (!IsPersonalLifestone)
                return true;

            if (PersonalLifestoneExpiresAt.HasValue && Time.GetUnixTime() >= PersonalLifestoneExpiresAt.Value)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("The lifestone has faded.", ChatMessageType.Broadcast));
                if (!IsDestroyed)
                    Destroy();
                return false;
            }

            if (player.Guid.Full != PersonalLifestoneOwner.Value)
            {
                var ownerName = PlayerManager.FindByGuid(PersonalLifestoneOwner.Value)?.Name;
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(ownerName != null
                    ? $"This lifestone is bound to {ownerName}. It will not answer you."
                    : "This lifestone is bound to someone else. It will not answer you.", ChatMessageType.Broadcast));
                return false;
            }

            return true;
        }

        private void SetEphemeralValues()
        {
            ObjectDescriptionFlags |= ObjectDescriptionFlag.LifeStone;

            RadarColor = ACE.Entity.Enum.RadarColor.LifeStone;
        }

        /// <summary>
        /// This is raised by Player.HandleActionUseItem.<para />
        /// The item does not exist in the players possession.<para />
        /// If the item was outside of range, the player will have been commanded to move using DoMoveTo before ActOnUse is called.<para />
        /// When this is called, it should be assumed that the player is within range.
        /// </summary>
        public override void ActOnUse(WorldObject worldObject)
        {
            if (!(worldObject is Player player))
                return;

            // Personal Lifestone: only its owner, only while it stands
            if (!CheckPersonalLifestoneAccess(player))
                return;

            var actionChain = new ActionChain();
            if (player.CombatMode != CombatMode.NonCombat)
            {
                var stanceTime = player.SetCombatMode(CombatMode.NonCombat);
                actionChain.AddDelaySeconds(stanceTime);

                player.LastUseTime += stanceTime;
            }

            actionChain.AddAction(this, ActionType.Lifestone_EnqueueSound, () => player.EnqueueBroadcast(new GameMessageSound(player.Guid, Sound.LifestoneOn, 1.0f)));

            player.LastUseTime += player.EnqueueMotion(actionChain, MotionCommand.Sanctuary);

            actionChain.AddAction(this, ActionType.Lifestone_Use, () =>
            {
                if (IsDestroyed)
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat("The lifestone has faded.", ChatMessageType.Broadcast));
                else if (player.IsWithinUseRadiusOf(this))
                {
                    player.Sanctuary = new Position(player.Location);
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat(GetProperty(PropertyString.UseMessage), ChatMessageType.Magic));
                    var newStamina = (uint)Math.Round(player.Stamina.Current / 2f);
                    player.UpdateVital(player.Stamina, newStamina);
                }
                else
                    player.Session.Network.EnqueueSend(new GameEventWeenieError(player.Session, WeenieError.YouHaveMovedTooFar));
            });

            actionChain.EnqueueChain();
        }
    }
}
