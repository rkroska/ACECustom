using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using System;
using System.Collections.Concurrent;

namespace ACE.Server.WorldObjects
{
    public partial class Player
    {
        // Shared across all player instances. We do this to ensure the ephemeral (non-db saved) state
        // of being in jail is still enforced even if the player logs out and back in during their sentence.
        private static ConcurrentDictionary<uint, DateTime> PlayersJailedUntil { get; } = new();

        /// <summary>
        /// Determines whether the player is currently serving a jail sentence.
        /// </summary>
        public bool IsInJail()
        {
            return PlayersJailedUntil.ContainsKey(Guid.Full);
        }

        /// <summary>
        /// Helper function to immediately apply the jail punishment to a player. 
        /// Applies tracking properties, ephemeral combat state overrides, and teleports them to the jail boundary.
        /// </summary>
        public void SendToJail()
        {
            TimeSpan jailTime = TimeSpan.FromSeconds(ServerConfig.ucm_jail_duration_seconds.Value);
            PlayersJailedUntil[Guid.Full] = DateTime.UtcNow.Add(jailTime);
            EnqueueEffectChain();
            Teleport(GetJailTeleportLocation());
            Session.Network.EnqueueSend(new GameMessageSystemChat($"Your are being punished. You are now in jail for {jailTime} and are attackable by other players.", ChatMessageType.Broadcast));
        }

        /// <summary>
        /// Ends a player's jail sentence and restores their original combat state and visual properties.
        /// Also clears them from the tracking dictionary and informs them that they are free.
        /// </summary>
        public void ReleaseFromJail()
        {
            PlayersJailedUntil.TryRemove(Guid.Full, out _);
            EnqueueEffectChain();
            Session.Network.EnqueueSend(new GameMessageSystemChat("Your punishment has concluded. You may now resume your adventures.", ChatMessageType.Broadcast));
        }

        /// <summary>
        /// Broadcasts an update to other players to see the new player.
        /// </summary>
        private void EnqueueEffectChain()
        {
            EnqueueBroadcast(false, new GameMessageDeleteObject(this));
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(.5);
            actionChain.AddAction(this, ActionType.PlayerTracking_DeCloakStep3, () =>
            {
                EnqueueBroadcast(false, new GameMessageCreateObject(this));
            });
            actionChain.EnqueueChain();
        }

        private Position GetJailTeleportLocation()
        {
            if (!Position.TryParse(ServerConfig.ucm_check_fail_teleport_location.Value, out Position failTeleLoc))
                failTeleLoc = GetDeathLocation();
            return failTeleLoc;
        }

        private Position GetJailCenterLocation()
        {
            if (!Position.TryParse(ServerConfig.ucm_check_jail_center_location.Value, out Position jailCenterLoc))
                jailCenterLoc = GetJailTeleportLocation();
            return jailCenterLoc;
        }

        /// <summary>
        /// Handles random starts of checks and timing out of active checks. For use by Player.Tick(). 
        /// Also enforces the active UCM Jails.
        /// </summary>
        public void TickJail()
        {
            if (!PlayersJailedUntil.TryGetValue(Guid.Full, out DateTime jailedUntil)) return;

            // Player has waited out their sentence and can be released.
            if (DateTime.UtcNow > jailedUntil)
            {
                ReleaseFromJail();
                return;
            }

            // Player is still serving their sentence, so enforce the jail boundaries.
            var center = GetJailCenterLocation();
            var size = ServerConfig.ucm_jail_size.Value;
            var offset = Location.GetOffset(center);
            if (Math.Abs(offset.X) > size / 2.0 || Math.Abs(offset.Y) > size / 2.0)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("You cannot leave the jail area until your punishment is complete!", ChatMessageType.Broadcast));
                Teleport(GetJailTeleportLocation());
            }
            return;
        }
    }
}
