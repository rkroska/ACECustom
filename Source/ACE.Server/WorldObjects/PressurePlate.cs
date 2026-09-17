using System;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Activates an object based on collision
    /// </summary>
    public class PressurePlate : WorldObject
    {
        /// <summary>
        /// The last time this pressure plate was activated
        /// </summary>
        public DateTime LastUseTime;

        /// <summary>Retail behaviour, used whenever PropertyFloat.PressurePlateCooldown is unset.</summary>
        public const double DefaultPressurePlateCooldown = 2.0;

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public PressurePlate(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public PressurePlate(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
            if (UseSound == 0)
                UseSound = Sound.TriggerActivated;

            // Room Assign (2026-09-16): reading the room list when a room plate loads marks its rooms' landblock, so
            // logout holds and login moves work straight after a restart, before anyone has been sent to a room.
            ACE.Server.Managers.RoomAssignManager.GetRooms(WeenieClassId);
        }

        public override void SetLinkProperties(WorldObject wo)
        {
            wo.ActivationTarget = Guid.Full;
        }

        /// <summary>
        /// Called when a player runs over the pressure plate
        /// </summary>
        public override void OnCollideObject(WorldObject wo)
        {
            OnActivate(wo);
        }

        /// <summary>
        /// Activates the object linked to a pressure plate
        /// </summary>
        public override void OnActivate(WorldObject activator)
        {
            // handle monsters walking on pressure plates
            if (!(activator is Player player))
                return;

            // Room Assign plate (2026-09-16): the WEENIE carries a room list (string 9018), re-read from the cache on
            // every step so an /id upload applies live. It replaces the stock activation entirely - see RoomAssignManager.
            var rooms = ACE.Server.Managers.RoomAssignManager.GetRooms(WeenieClassId);
            if (rooms != null)
            {
                ACE.Server.Managers.RoomAssignManager.OnPlateStep(this, player, rooms);
                return;
            }

            // prevent continuous event stream
            // TODO: should this go in base.OnActivate()?
            //
            // The cooldown is per OBJECT, not per player, so a busy plate catches only the first
            // person through it in each window. PropertyFloat.PressurePlateCooldown (9056) makes it
            // tunable per weenie; UNSET keeps the 2 s retail default, so no existing plate changes
            // behaviour.
            // UNSET means retail default; an explicit 0 means genuinely no cooldown, so a plate can
            // catch several players in the same instant. Negatives are clamped rather than treated
            // as "unset" so a typo cannot silently restore the 2 s gate.
            //
            // A COOLDOWN OF 0 IS NOT SUFFICIENT ON ITS OWN. Every action in the plate's Activation
            // emote set must also be at delay 0. EmoteManager.Enqueue runs a zero-delay set
            // synchronously, so Nested unwinds and IsBusy clears before this returns; any non-zero
            // delay puts the set on an ActionChain instead and holds IsBusy for its duration, and
            // ExecuteEmoteSet drops a second player's activation while busy. So adding one delayed
            // action to the emote set silently reinstates the gate no matter what this property says.
            var currentTime = DateTime.UtcNow;
            var cooldown = PressurePlateCooldown ?? DefaultPressurePlateCooldown;
            if (cooldown < 0)
                cooldown = 0;

            if (cooldown > 0 && currentTime < LastUseTime + TimeSpan.FromSeconds(cooldown))
                return;

            LastUseTime = currentTime;

            player.EnqueueBroadcast(new GameMessageSound(player.Guid, UseSound));

            base.OnActivate(activator);
        }

        public override void ActOnUse(WorldObject wo)
        {
            // Do nothing
        }
    }
}
