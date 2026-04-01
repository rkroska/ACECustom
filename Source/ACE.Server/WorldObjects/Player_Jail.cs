using ACE.Common.Extensions;
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

        // Denotes whether the player is eligible for stamps on exit.
        // Set to true on entering jail, and false on death or relog.
        bool EligibleForModelInmate = false;

        // Denotes whether the player can still give/get a stamp when killed.
        // Set to true on entering jail, and false the first time they are killed by a player or relog.
        bool EligibleForDeathStamps = false;

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
        /// If the player is already in jail, this will reset their sentence duration.
        /// </summary>
        public void SendToJail(JailReason reason)
        {
            TimeSpan jailTime = TimeSpan.FromSeconds(ServerConfig.ucm_jail_duration_seconds.Value);
            DateTime releaseTime = DateTime.UtcNow.Add(jailTime);

            if (!PlayersJailedUntil.TryAdd(Guid.Full, releaseTime))
            {
                PlayersJailedUntil[Guid.Full] = releaseTime;
                return;
            }

            // Apply jail effects (newly jailed).
            EligibleForModelInmate = true;
            EligibleForDeathStamps = true;
            RedrawPlayerWithUpdates();
            Teleport(GetJailTeleportLocation());
            GrantStampsOnEntry(reason);
            Session.Network.EnqueueSend(new GameMessageSystemChat($"You are being punished. You are now in jail for {jailTime.GetFriendlyLongString()} and are attackable by other players.", ChatMessageType.Broadcast));
        }

        /// <summary>
        /// Ends a player's jail sentence and restores their original combat state and visual properties.
        /// Also clears them from the tracking dictionary and informs them that they are free.
        /// </summary>
        public void ReleaseFromJail()
        {
            PlayersJailedUntil.TryRemove(Guid.Full, out _);
            RedrawPlayerWithUpdates();
            GrantStampsOnExit();
            Session.Network.EnqueueSend(new GameMessageSystemChat("Your punishment has concluded. You may now resume your adventures.", ChatMessageType.Broadcast));
        }

        public void OnDeathInJail(Player killingPlayer)
        {
            // Dying for any reason counts as failure to survive for exit-jail stamps.
            // However, benign deaths like suicides do not block the PK-related stamps below.
            EligibleForModelInmate = false;

            if (killingPlayer == null) return;

            // Suicide.
            if (killingPlayer == this)
            {
                QuestManager.Stamp("jail_early_retirement_denied");
                return;
            }

            if (!EligibleForDeathStamps) return;
            if (killingPlayer.IsInJail())
            {
                QuestManager.Stamp("jail_lost_the_yard_fight");
                killingPlayer.QuestManager.Stamp("jail_shanked_a_fool");
            }
            else
            {
                QuestManager.Stamp("jail_sitting_duck");
                killingPlayer.QuestManager.Stamp("jail_vigilante_justice");
            }
            EligibleForDeathStamps = false;
        }

        private void GrantStampsOnExit()
        {
            if (!EligibleForModelInmate) return;
            int totalGoodBehavior = QuestManager.Stamp("jail_model_inmate");
            if (totalGoodBehavior >= 5) QuestManager.StampFirst("jail_smooth_sentence");
            if (totalGoodBehavior >= 20) QuestManager.StampFirst("jail_rehabilitated");
        }

        private void GrantStampsOnEntry(JailReason reason)
        {
            // Overall Counter
            int totalJails = QuestManager.Stamp("jail_fresh_meat");
            if (totalJails >= 5) QuestManager.StampFirst("jail_the_usual_suspect");
            if (totalJails >= 10) QuestManager.StampFirst("jail_the_recidivist");
            if (totalJails >= 20) QuestManager.StampFirst("jail_cellmate_of_the_month");
            if (totalJails >= 50) QuestManager.StampFirst("jail_macroed_this_stamp");

            // Reason-Specific Counters
            string questName = reason switch
            {
                JailReason.WrongAnswer => "jail_math_is_hard",
                JailReason.TimedOut => "jail_alt_tabbed",
                JailReason.LoggedOut => "jail_tactical_dc",
                _ => ""
            };

            // Skip sending by admin for specific reason-stamps
            if (string.IsNullOrEmpty(questName)) return;

            int reasonCount = QuestManager.Stamp(questName);
            switch (reason)
            {
                case JailReason.WrongAnswer:
                    if (reasonCount >= 5) QuestManager.StampFirst("jail_stage_fright");
                    if (reasonCount >= 20) QuestManager.StampFirst("jail_shaky_fingers");
                    break;
                case JailReason.TimedOut:
                    if (reasonCount >= 5) QuestManager.StampFirst("jail_bathroom_break");
                    if (reasonCount >= 20) QuestManager.StampFirst("jail_asleep_at_the_wheel");
                    break;
                case JailReason.LoggedOut:
                    if (reasonCount >= 5) QuestManager.StampFirst("jail_vanishing_act");
                    if (reasonCount >= 20) QuestManager.StampFirst("jail_quitters_shame");
                    break;
            }
        }

        /// <summary>
        /// Broadcasts an update to other players to see the new player.
        /// </summary>
        private void RedrawPlayerWithUpdates()
        {
            EnqueueBroadcast(false, new GameMessageDeleteObject(this));
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(.25);
            actionChain.AddAction(this, ActionType.PlayerTracking_DeCloakStep3, () =>
            {
                EnqueueBroadcast(false, new GameMessageCreateObject(this));
            });
            actionChain.EnqueueChain();
        }
        private Position GetJailTeleportLocation()
        {
            if (Position.TryParse(ServerConfig.ucm_check_fail_teleport_location.Value, out Position failTeleLoc))
                return failTeleLoc;
            if (Position.TryParse(ServerConfig.ucm_check_jail_center_location.Value, out Position jailCenterLoc))
                return jailCenterLoc;
            return GetDeathLocation();
        }

        private Position GetJailCenterLocation()
        {
            if (Position.TryParse(ServerConfig.ucm_check_jail_center_location.Value, out Position jailCenterLoc))
                return jailCenterLoc;
            return GetJailTeleportLocation();
        }

        /// <summary>
        /// Handles random starts of checks and timing out of active checks. For use by Player.Tick().
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
            // We do not enforce Z boundary (vertical), it's just a 2D bounding box centered on the configured location.
            var center = GetJailCenterLocation();
            var size = ServerConfig.ucm_jail_size.Value;
            var offset = Location.GetOffset(center);
            if (Math.Abs(offset.X) > size / 2.0 || Math.Abs(offset.Y) > size / 2.0)
            {
                // Make sure not to count the tele location as a jail boundary violation.
                var teleLoc = GetJailTeleportLocation();
                if (Location.Distance2D(teleLoc) < 1.0) return;
                EligibleForModelInmate = false;
                QuestManager.StampFirst("jail_magic_bars");
                Session.Network.EnqueueSend(new GameMessageSystemChat("You cannot leave the jail area until your punishment is complete!", ChatMessageType.Broadcast));
                Teleport(teleLoc);
            }
            return;
        }
    }
}
