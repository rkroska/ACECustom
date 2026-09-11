using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Globalization;

using ACE.Common;
using ACE.Database;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Managers;
using System.Net;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        private static readonly Position MarketplaceDrop = DatabaseManager.World.GetCachedWeenie("portalmarketplace")?.GetPosition(PositionType.Destination) ?? new Position(0x016C01BC, 49.206f, -31.935f, 0.005f, 0, 0, -0.707107f, 0.707107f);

        /// <summary>
        /// Teleports the player to position
        /// </summary>
        /// <param name="positionType">PositionType to be teleported to</param>
        /// <returns>true on success (position is set) false otherwise</returns>
        public bool TeleToPosition(PositionType positionType)
        {
            var position = GetPosition(positionType);

            if (position != null)
            {
                var teleportDest = new Position(position);
                AdjustDungeon(teleportDest);
                //Console.WriteLine($"Player tele to: {teleportDest}");
                Teleport(teleportDest);
                return true;
            }

            return false;
        }

        private const float RecallMoveThreshold = 8.0f;
        public const float RecallMoveThresholdSq = RecallMoveThreshold * RecallMoveThreshold;

        public bool TooBusyToRecall
        {
            get => IsBusy || suicideInProgress;     // recalls could be started from portal space?
        }

        public void HandleActionTeleToHouse()
        {
            if (IsOlthoiPlayer)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.OlthoiCanOnlyRecallToLifestone));
                return;
            }

            if (PKTimerActive)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouHaveBeenInPKBattleTooRecently));
                return;
            }

            if (RecallsDisabled)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.ExitTrainingAcademyToUseCommand));
                return;
            }

            if (TooBusyToRecall)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YoureTooBusy));
                return;
            }

            var house = House ?? GetAccountHouse();

            if (house == null)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouMustOwnHouseToUseCommand));
                return;
            }

            if (CombatMode != CombatMode.NonCombat)
            {
                // this should be handled by a different thing, probably a function that forces player into peacemode
                var updateCombatMode = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.CombatMode, (int)CombatMode.NonCombat);
                SetCombatMode(CombatMode.NonCombat);
                Session.Network.EnqueueSend(updateCombatMode);
            }

            EnqueueBroadcast(new GameMessageSystemChat($"{Name} is recalling home.", ChatMessageType.Recall), LocalBroadcastRange, ChatMessageType.Recall);

            SendMotionAsCommands(MotionCommand.HouseRecall, MotionStance.NonCombat);

            var startPos = new Position(Location);

            // Wait for animation
            var actionChain = new ActionChain();

            // Then do teleport
            var animLength = DatManager.PortalDat.ReadFromDat<MotionTable>(MotionTableId).GetAnimationLength(MotionCommand.HouseRecall);
            actionChain.AddDelaySeconds(animLength);
            IsBusy = true;
            actionChain.AddAction(this, ActionType.PlayerLocation_TeleportToHouse, () =>
            {
                IsBusy = false;
                var endPos = new Position(Location);
                if (startPos.SquaredDistanceTo(endPos) > RecallMoveThresholdSq)
                {
                    Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouHaveMovedTooFar));
                    return;
                }
                Teleport(house.SlumLord.Location);
            });

            actionChain.EnqueueChain();
        }

        /// <summary>
        /// Handles teleporting a player to the lifestone (/ls or /lifestone command)
        /// </summary>
        public void HandleActionTeleToLifestone()
        {
            if (PKTimerActive)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouHaveBeenInPKBattleTooRecently));
                return;
            }

            if (RecallsDisabled)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.ExitTrainingAcademyToUseCommand));
                return;
            }

            if (TooBusyToRecall)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YoureTooBusy));
                return;
            }

            if (Sanctuary == null)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Your spirit has not been attuned to a sanctuary location.", ChatMessageType.Broadcast));
                return;
            }

            // FIXME(ddevec): I should probably make a better interface for this
            UpdateVital(Mana, Mana.Current / 2);

            if (CombatMode != CombatMode.NonCombat)
            {
                // this should be handled by a different thing, probably a function that forces player into peacemode
                var updateCombatMode = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.CombatMode, (int)CombatMode.NonCombat);
                SetCombatMode(CombatMode.NonCombat);
                Session.Network.EnqueueSend(updateCombatMode);
            }

            EnqueueBroadcast(new GameMessageSystemChat($"{Name} is recalling to the lifestone.", ChatMessageType.Recall), LocalBroadcastRange, ChatMessageType.Recall);

            SendMotionAsCommands(MotionCommand.LifestoneRecall, MotionStance.NonCombat);

            var startPos = new Position(Location);

            // Wait for animation
            ActionChain lifestoneChain = new();

            // Then do teleport
            IsBusy = true;
            lifestoneChain.AddDelaySeconds(DatManager.PortalDat.ReadFromDat<MotionTable>(MotionTableId).GetAnimationLength(MotionCommand.LifestoneRecall));
            lifestoneChain.AddAction(this, ActionType.PlayerLocation_TeleportToLifestone, () =>
            {
                IsBusy = false;
                var endPos = new Position(Location);
                if (startPos.SquaredDistanceTo(endPos) > RecallMoveThresholdSq)
                {
                    Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouHaveMovedTooFar));
                    return;
                }

                Teleport(Sanctuary);
            });

            lifestoneChain.EnqueueChain();
        }

        public void HandleActionTeleToMarketPlace()
        {
            if (IsOlthoiPlayer)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.OlthoiCanOnlyRecallToLifestone));
                return;
            }

            if (PKTimerActive)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouHaveBeenInPKBattleTooRecently));
                return;
            }

            if (RecallsDisabled)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.ExitTrainingAcademyToUseCommand));
                return;
            }

            if (TooBusyToRecall)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YoureTooBusy));
                return;
            }

            if (CombatMode != CombatMode.NonCombat)
            {
                // this should be handled by a different thing, probably a function that forces player into peacemode
                var updateCombatMode = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.CombatMode, (int)CombatMode.NonCombat);
                SetCombatMode(CombatMode.NonCombat);
                Session.Network.EnqueueSend(updateCombatMode);
            }

            EnqueueBroadcast(new GameMessageSystemChat($"{Name} is recalling to the marketplace.", ChatMessageType.Recall), LocalBroadcastRange, ChatMessageType.Recall);

            SendMotionAsCommands(MotionCommand.MarketplaceRecall, MotionStance.NonCombat);

            var startPos = new Position(Location);

            // TODO: (OptimShi): Actual animation length is longer than in retail. 18.4s
            // float mpAnimationLength = MotionTable.GetAnimationLength((uint)MotionTableId, MotionCommand.MarketplaceRecall);
            // mpChain.AddDelaySeconds(mpAnimationLength);
            ActionChain mpChain = new();
            mpChain.AddDelaySeconds(14);

            // Then do teleport
            IsBusy = true;
            mpChain.AddAction(this, ActionType.PlayerLocation_TeleportToMarketplace, () =>
            {
                IsBusy = false;
                var endPos = new Position(Location);
                if (startPos.SquaredDistanceTo(endPos) > RecallMoveThresholdSq)
                {
                    Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouHaveMovedTooFar));
                    return;
                }

                Teleport(MarketplaceDrop);
            });

            // Set the chain to run
            mpChain.EnqueueChain();
        }

        public void HandleActionRecallAllegianceHometown()
        {
            if (IsOlthoiPlayer)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.OlthoiCanOnlyRecallToLifestone));
                return;
            }

            if (PKTimerActive)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouHaveBeenInPKBattleTooRecently));
                return;
            }

            if (RecallsDisabled)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.ExitTrainingAcademyToUseCommand));
                return;
            }

            if (TooBusyToRecall)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YoureTooBusy));
                return;
            }

            // check if player is in an allegiance
            if (!VerifyRecallAllegianceHometown())
                return;

            if (CombatMode != CombatMode.NonCombat)
            {
                // this should be handled by a different thing, probably a function that forces player into peacemode
                var updateCombatMode = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.CombatMode, (int)CombatMode.NonCombat);
                SetCombatMode(CombatMode.NonCombat);
                Session.Network.EnqueueSend(updateCombatMode);
            }

            EnqueueBroadcast(new GameMessageSystemChat($"{Name} is going to the Allegiance hometown.", ChatMessageType.Recall), LocalBroadcastRange, ChatMessageType.Recall);

            SendMotionAsCommands(MotionCommand.AllegianceHometownRecall, MotionStance.NonCombat);

            var startPos = new Position(Location);

            // Wait for animation
            var actionChain = new ActionChain();

            // Then do teleport
            IsBusy = true;
            var animLength = DatManager.PortalDat.ReadFromDat<MotionTable>(MotionTableId).GetAnimationLength(MotionCommand.AllegianceHometownRecall);
            actionChain.AddDelaySeconds(animLength);
            actionChain.AddAction(this, ActionType.PlayerLocation_TeleportToAllegianceHometown, () =>
            {
                IsBusy = false;
                var endPos = new Position(Location);
                if (startPos.SquaredDistanceTo(endPos) > RecallMoveThresholdSq)
                {
                    Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouHaveMovedTooFar));
                    return;
                }

                // re-verify
                if (!VerifyRecallAllegianceHometown())
                    return;

                Teleport(Allegiance.Sanctuary);
            });

            actionChain.EnqueueChain();
        }

        private bool VerifyRecallAllegianceHometown()
        {
            if (Allegiance == null)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouAreNotInAllegiance));
                return false;
            }

            if (Allegiance.Sanctuary == null)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YourAllegianceDoesNotHaveHometown));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Recalls you to your allegiance's Mansion or Villa
        /// </summary>
        public void HandleActionTeleToMansion()
        {
            //Console.WriteLine($"{Name}.HandleActionTeleToMansion()");

            if (IsOlthoiPlayer)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.OlthoiCanOnlyRecallToLifestone));
                return;
            }

            if (PKTimerActive)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouHaveBeenInPKBattleTooRecently));
                return;
            }

            if (RecallsDisabled)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.ExitTrainingAcademyToUseCommand));
                return;
            }

            if (TooBusyToRecall)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YoureTooBusy));
                return;
            }

            var allegianceHouse = VerifyTeleToMansion();

            if (allegianceHouse == null)
                return;

            if (CombatMode != CombatMode.NonCombat)
            {
                // this should be handled by a different thing, probably a function that forces player into peacemode
                var updateCombatMode = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.CombatMode, (int)CombatMode.NonCombat);
                SetCombatMode(CombatMode.NonCombat);
                Session.Network.EnqueueSend(updateCombatMode);
            }

            EnqueueBroadcast(new GameMessageSystemChat($"{Name} is recalling to the Allegiance housing.", ChatMessageType.Recall), LocalBroadcastRange, ChatMessageType.Recall);

            SendMotionAsCommands(MotionCommand.HouseRecall, MotionStance.NonCombat);

            var startPos = new Position(Location);

            // Wait for animation
            var actionChain = new ActionChain();

            // Then do teleport
            var animLength = DatManager.PortalDat.ReadFromDat<MotionTable>(MotionTableId).GetAnimationLength(MotionCommand.HouseRecall);
            actionChain.AddDelaySeconds(animLength);

            IsBusy = true;
            actionChain.AddAction(this, ActionType.PlayerLocation_TeleportToAllegianceMansion, () =>
            {
                IsBusy = false;
                var endPos = new Position(Location);
                if (startPos.SquaredDistanceTo(endPos) > RecallMoveThresholdSq)
                {
                    Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouHaveMovedTooFar));
                    return;
                }

                // re-verify
                allegianceHouse = VerifyTeleToMansion();

                if (allegianceHouse == null)
                    return;

                Teleport(allegianceHouse.SlumLord.Location);
            }); 

            actionChain.EnqueueChain();
        }

        private House VerifyTeleToMansion()
        {
            // check if player is in an allegiance
            if (Allegiance == null)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouAreNotInAllegiance));
                return null;
            }

            var allegianceHouse = Allegiance.GetHouse();

            if (allegianceHouse == null)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YourMonarchDoesNotOwnAMansionOrVilla));
                return null;
            }

            if (allegianceHouse.HouseType < HouseType.Villa)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YourMonarchsHouseIsNotAMansionOrVilla));
                return null;
            }

            // ensure allegiance housing has allegiance permissions enabled
            if (allegianceHouse.MonarchId == null)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YourMonarchHasClosedTheMansion));
                return null;
            }

            return allegianceHouse;
        }

        private static List<Position> pkArenaLocs =
        [
            new Position(DatabaseManager.World.GetCachedWeenie("portalpkarenanew1")?.GetPosition(PositionType.Destination) ?? new Position(0x00660117, 30, -50, 0.005f, 0, 0,  0.000000f,  1.000000f)),
            new Position(DatabaseManager.World.GetCachedWeenie("portalpkarenanew2")?.GetPosition(PositionType.Destination) ?? new Position(0x00660106, 10,   0, 0.005f, 0, 0, -0.947071f,  0.321023f)),
            new Position(DatabaseManager.World.GetCachedWeenie("portalpkarenanew3")?.GetPosition(PositionType.Destination) ?? new Position(0x00660103, 30, -30, 0.005f, 0, 0, -0.699713f,  0.714424f)),
            new Position(DatabaseManager.World.GetCachedWeenie("portalpkarenanew4")?.GetPosition(PositionType.Destination) ?? new Position(0x0066011E, 50,   0, 0.005f, 0, 0, -0.961021f, -0.276474f)),
            new Position(DatabaseManager.World.GetCachedWeenie("portalpkarenanew5")?.GetPosition(PositionType.Destination) ?? new Position(0x00660127, 60, -30, 0.005f, 0, 0,  0.681639f,  0.731689f)),
        ];

        public void HandleActionTeleToPkArena()
        {
            //Console.WriteLine($"{Name}.HandleActionTeleToPkArena()");

            if (PlayerKillerStatus != PlayerKillerStatus.PK)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.OnlyPKsMayUseCommand));
                return;
            }

            if (PKTimerActive)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouHaveBeenInPKBattleTooRecently));
                return;
            }

            if (RecallsDisabled)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.ExitTrainingAcademyToUseCommand));
                return;
            }

            if (TooBusyToRecall)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YoureTooBusy));
                return;
            }

            if (CombatMode != CombatMode.NonCombat)
            {
                // this should be handled by a different thing, probably a function that forces player into peacemode
                var updateCombatMode = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.CombatMode, (int)CombatMode.NonCombat);
                SetCombatMode(CombatMode.NonCombat);
                Session.Network.EnqueueSend(updateCombatMode);
            }

            EnqueueBroadcast(new GameMessageSystemChat($"{Name} is going to the PK Arena.", ChatMessageType.Recall), LocalBroadcastRange, ChatMessageType.Recall);

            SendMotionAsCommands(MotionCommand.PKArenaRecall, MotionStance.NonCombat);

            var startPos = new Position(Location);

            // Wait for animation
            var actionChain = new ActionChain();

            // Then do teleport
            var animLength = DatManager.PortalDat.ReadFromDat<MotionTable>(MotionTableId).GetAnimationLength(MotionCommand.PKArenaRecall);
            actionChain.AddDelaySeconds(animLength);

            IsBusy = true;
            actionChain.AddAction(this, ActionType.PlayerLocation_TeleportToPKArena, () =>
            {
                IsBusy = false;
                var endPos = new Position(Location);
                if (startPos.SquaredDistanceTo(endPos) > RecallMoveThresholdSq)
                {
                    Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouHaveMovedTooFar));
                    return;
                }

                var rng = ThreadSafeRandom.Next(0, pkArenaLocs.Count - 1);
                var loc = pkArenaLocs[rng];

                Teleport(loc);
            });

            actionChain.EnqueueChain();
        }

        private static List<Position> pklArenaLocs =
        [
            new Position(DatabaseManager.World.GetCachedWeenie("portalpklarenanew1")?.GetPosition(PositionType.Destination) ?? new Position(0x00670117, 30, -50, 0.005f, 0, 0,  0.000000f,  1.000000f)),
            new Position(DatabaseManager.World.GetCachedWeenie("portalpklarenanew2")?.GetPosition(PositionType.Destination) ?? new Position(0x00670106, 10,   0, 0.005f, 0, 0, -0.947071f,  0.321023f)),
            new Position(DatabaseManager.World.GetCachedWeenie("portalpklarenanew3")?.GetPosition(PositionType.Destination) ?? new Position(0x00670103, 30, -30, 0.005f, 0, 0, -0.699713f,  0.714424f)),
            new Position(DatabaseManager.World.GetCachedWeenie("portalpklarenanew4")?.GetPosition(PositionType.Destination) ?? new Position(0x0067011E, 50,   0, 0.005f, 0, 0, -0.961021f, -0.276474f)),
            new Position(DatabaseManager.World.GetCachedWeenie("portalpklarenanew5")?.GetPosition(PositionType.Destination) ?? new Position(0x00670127, 60, -30, 0.005f, 0, 0,  0.681639f,  0.731689f)),
        ];

        public void HandleActionTeleToPklArena()
        {
            //Console.WriteLine($"{Name}.HandleActionTeleToPkLiteArena()");

            if (IsOlthoiPlayer)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.OlthoiCanOnlyRecallToLifestone));
                return;
            }

            if (PlayerKillerStatus != PlayerKillerStatus.PKLite)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.OnlyPKLiteMayUseCommand));
                return;
            }

            if (PKTimerActive)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouHaveBeenInPKBattleTooRecently));
                return;
            }

            if (RecallsDisabled)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.ExitTrainingAcademyToUseCommand));
                return;
            }

            if (TooBusyToRecall)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YoureTooBusy));
                return;
            }

            if (CombatMode != CombatMode.NonCombat)
            {
                // this should be handled by a different thing, probably a function that forces player into peacemode
                var updateCombatMode = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.CombatMode, (int)CombatMode.NonCombat);
                SetCombatMode(CombatMode.NonCombat);
                Session.Network.EnqueueSend(updateCombatMode);
            }

            EnqueueBroadcast(new GameMessageSystemChat($"{Name} is going to the PKL Arena.", ChatMessageType.Recall), LocalBroadcastRange, ChatMessageType.Recall);

            SendMotionAsCommands(MotionCommand.PKArenaRecall, MotionStance.NonCombat);

            var startPos = new Position(Location);

            // Wait for animation
            var actionChain = new ActionChain();

            // Then do teleport
            var animLength = DatManager.PortalDat.ReadFromDat<MotionTable>(MotionTableId).GetAnimationLength(MotionCommand.PKArenaRecall);
            actionChain.AddDelaySeconds(animLength);

            IsBusy = true;
            actionChain.AddAction(this, ActionType.PlayerLocation_TeleportToPKLArena, () =>
            {
                IsBusy = false;
                var endPos = new Position(Location);
                if (startPos.SquaredDistanceTo(endPos) > RecallMoveThresholdSq)
                {
                    Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouHaveMovedTooFar));
                    return;
                }

                var rng = ThreadSafeRandom.Next(0, pklArenaLocs.Count - 1);
                var loc = pklArenaLocs[rng];

                Teleport(loc);
            });

            actionChain.EnqueueChain();
        }

        public void SendMotionAsCommands(MotionCommand motionCommand, MotionStance motionStance)
        {
            if (FastTick)
            {
                var actionChain = new ActionChain();
                EnqueueMotionAction(actionChain, [motionCommand], 1.0f, motionStance);
                actionChain.EnqueueChain();
            }
            else
            {
                var motion = new Motion(motionStance, MotionCommand.Ready);
                motion.MotionState.AddCommand(this, motionCommand);
                EnqueueBroadcastMotion(motion);
            }
        }

        public DateTime LastTeleportTime;





        public void DoPreTeleportHide()
        {
            if (Teleporting) return;
            PlayParticleEffect(PlayScript.Hide, Guid);
        }



        /// <summary>
        /// Prevent message spam
        /// </summary>
        public double? LastPortalTeleportTimestampError;

        public override void OnTeleportComplete()
        {
            bool shouldCallBase = true;
            try
            {
                int nonexemptCount = 0;
                var endpoint = this.Session.EndPoint;
                var ipAllowsUnlimited = ConfigManager.Config.Server.Network.AllowUnlimitedSessionsFromIPAddresses.Contains(endpoint.Address.ToString());
                if(!ipAllowsUnlimited)
                {
                    var players = PlayerManager.GetAllOnline();
                    foreach (var p in players.Where(x => x.Session.EndPoint.Address.Equals(endpoint.Address)))
                    {
                        var lb = p.CurrentLandblock;
                        if (lb != null && Landblock.connectionExemptLandblocks.Contains(lb.Id.Landblock))
                            continue;

                        // Only omit from the IP cap when mid-teleport (no landblock yet). Recovered sessions (e.g. ForceEndPortalSpaceStuck) clear Teleporting but can briefly still have null landblock — they must count.
                        if (lb == null && p.Teleporting)
                        {
                            log.Warn($"[PORTAL SPACE] {Name} (0x{Guid}) OnTeleportComplete IP check: peer {p.Name} (0x{p.Guid}) has CurrentLandblock=null while Teleporting — skipping count");
                            continue;
                        }

                        if (p.IsPlussed)
                            continue;

                        if (++nonexemptCount > ConfigManager.Config.Server.Network.MaximumAllowedSessionsPerIPAddress)
                        {
                            log.Warn($"[PORTAL SPACE] {Name} (0x{Guid}) OnTeleportComplete booting peer {p.Name} (0x{p.Guid}) — " +
                                $"nonexemptCount={nonexemptCount}, limit={ConfigManager.Config.Server.Network.MaximumAllowedSessionsPerIPAddress}, " +
                                $"IP={endpoint.Address}, peer landblock={lb?.Id.Raw:X8}");
                            p.SendMessage($"Booting due to exceeding {ConfigManager.Config.Server.Network.MaximumAllowedSessionsPerIPAddress} allowed outside of exempt areas.");
                            p.Session.LogOffPlayer();
                        }
                    }
                }

                if (CurrentLandblock != null && !CurrentLandblock.CreateWorldObjectsCompleted && Teleporting)
                {
                    log.Warn($"[PORTAL SPACE] {Name} (0x{Guid}) OnTeleportComplete deferred — CreateWorldObjectsCompleted=false, " +
                        $"Landblock={CurrentLandblock.Id.Raw:X8}, Teleporting={Teleporting}");
                    shouldCallBase = false;
                    // If the critical landblock resources haven't been loaded yet, we keep the player in the pink bubble state
                    // We'll check periodically to see when it's safe to let them materialize in
                    var actionChain = new ActionChain();
                    actionChain.AddDelaySeconds(0.1);
                    actionChain.AddAction(this, ActionType.PlayerLocation_OnTeleportComplete, OnTeleportComplete);
                    actionChain.EnqueueChain();
                    return;
                }

                CheckMonsters();
                CheckHouse();

                // hijacking this for both start/end on portal teleport
                if (LastTeleportStartTimestamp == LastPortalTeleportTimestamp)
                    LastPortalTeleportTimestamp = Time.GetUnixTime();
            }
            catch (Exception ex)
            {
                log.Error($"[PORTAL SPACE] {Name} (0x{Guid}) OnTeleportComplete EXCEPTION — forcing materialization. " +
                    $"Landblock: {CurrentLandblock?.Id.Raw:X8}, Teleporting: {Teleporting}", ex);
            }
            finally
            {
                if (shouldCallBase)
                {
                    base.OnTeleportComplete();
                    SchedulePostTeleportVisibilityReconcile();
                }
            }
        }

        public void SendTeleportedViaMagicMessage(WorldObject itemCaster, Spell spell)
        {
            if (itemCaster == null || itemCaster is Gem)
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You have been teleported.", ChatMessageType.Magic));
            else if (this != itemCaster && itemCaster is not Gem && itemCaster is not Switch && !(itemCaster.GetProperty(PropertyBool.NpcInteractsSilently) ?? false))
                Session.Network.EnqueueSend(new GameMessageSystemChat($"{itemCaster.Name} teleports you with {spell.Name}.", ChatMessageType.Magic));
            //else if (itemCaster is Gem)
            //    Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.ITeleported));
        }

        public void NotifyLandblocks()
        {
            // the original implementations of this were done on landblock heartbeat,
            // with checks for players in the current landblock, as well as adjacent outdoor landblocks

            // for performance reasons, this is being reimplemented in the reverse manner,
            // with players notifying landblocks of their activity

            // notify current landblock of player activity
            if (CurrentLandblock != null)
                CurrentLandblock?.SetActive();
        }

        public static readonly float RunFactor = 1.5f;

        /// <summary>
        /// Returns the amount of time for player to rotate by the # of degrees
        /// from the input angle, using the omega speed from its MotionTable
        /// </summary>
        public override float GetRotateDelay(float angle)
        {
            return base.GetRotateDelay(angle) / RunFactor;
        }

        /// <summary>
        /// A list of landblocks the player cannot relog directly into
        /// 
        /// If a regular player logs out in one of these landblocks,
        /// they will be transported back to the lifestone when they log back in.
        /// </summary>
        /// <summary>
        /// A list of landblocks the player cannot relog directly into
        /// 
        /// If a regular player logs out in one of these landblocks,
        /// they will be transported back to the lifestone when they log back in.
        ///
        /// 2026-08-31: was a HashSet; now carries the FRIENDLY NAME as data rather than as a
        /// comment, so /nolog list and the plugin can show what each landblock actually is.
        /// Names are the curated ones that were already here - they beat deriving a name from
        /// the portal that lands there, which gives things like "Cow Level" for Tainted Grotto.
        /// </summary>
        private static readonly Dictionary<ushort, string> NoLog_Landblocks = new()
        {
            // https://asheron.fandom.com/wiki/Special:Search?query=Lifestone+on+Relog%3A+Yes+
            // https://docs.google.com/spreadsheets/d/122xOw3IKCezaTDjC_hggWSVzYJ_9M_zUUtGEXkwNXfs/edit#gid=846612575

            { 0x0002, "Viamontian Garrison" },
            { 0x0007, "Town Network" },
            { 0x0056, "Augmentation Realm Main Level" },
            { 0x005F, "Tanada House of Pancakes (Seasonal)" },
            { 0x0067, "PKL Arena" },
            { 0x006D, "Augmentation Realm Upper Level" },
            { 0x007D, "Augmentation Realm Lower Level" },
            { 0x00AB, "Derethian Combat Arena" },
            { 0x00AC, "Derethian Combat Arena" },
            { 0x00C3, "Blighted Putrid Moarsman Tunnels" },
            { 0x00D7, "Jester's Prison" },
            { 0x00EA, "Mhoire Armory" },
            { 0x015D, "Mountain Cavern" },
            { 0x027F, "East Fork Dam Hive" },
            { 0x03A7, "Mount Elyrii Hive" },
            { 0x5764, "Oubliette of Mhoire Castle" },
            { 0x634C, "Tainted Grotto" },
            { 0x6544, "Greater Battle Dungeon" },
            { 0x6651, "Hoshino Tower" },
            { 0x7E04, "Thug Hideout" },
            { 0x8A04, "Night Club (Seasonal Anniversary)" },
            { 0x8B04, "Frozen Wight Lair" },
            { 0x9EE5, "Northwatch Castle Black Market" },
            { 0xB5F0, "Aerfalle's Sanctum" },
            { 0xF92F, "Freebooter Keep Black Market" },
            { 0x00B0, "Colosseum Arena One" },
            { 0x00B1, "Colosseum Arena Two" },
            { 0x00B2, "Colosseum Arena Three" },
            { 0x00B3, "Colosseum Arena Four" },
            { 0x00B4, "Colosseum Arena Five" },
            { 0x00B6, "Colosseum Arena Mini-Bosses" },
            { 0x5954, "Catacombs of Torment" },
            { 0x5960, "Gauntlet Arena One (Celestial Hand)" },
            { 0x5961, "Gauntlet Arena Two (Celestial Hand)" },
            { 0x5962, "Gauntlet Arena One (Eldritch Web)" },
            { 0x5963, "Gauntlet Arena Two (Eldritch Web)" },
            { 0x5964, "Gauntlet Arena One (Radiant Blood)" },
            { 0x5965, "Gauntlet Arena Two (Radiant Blood)" },
            { 0x79E9, "Bloodstone Factory" },
            { 0x654C, "Enchanted Mnemosyne (undocumented - resolved from its portal)" },
        };

        // -- no-log OVERRIDES (2026-08-31) ---------------------------------------------
        //
        // The list above stays the RETAIL SEED and is still the whole story on a server that authors
        // nothing. On top of it sits one ordinary server property, ServerConfig.nolog_landblocks, so
        // no-log areas can be edited live (/nolog, or /modifystring) instead of needing a recompile.
        //
        // Two things the seed cannot express and the overrides can:
        //   - a VARIATION. The seed is base-landblock-only by construction; an override can name a
        //     specific variation, or "all" for base plus every variation.
        //   - REMOVAL. A minus token suppresses an entry, including one from the seed, so a server can
        //     opt out of a retail no-log area without editing this file.
        //
        // NULL AND 0 BOTH MEAN BASE. VariationManager.NormalizeBase is the single normalizer and it is
        // used on BOTH sides - here and in the /nolog command that writes the string. Do not normalize
        // with anything else: GetEffectiveVariation looks similar but also applies the
        // ForceEndgameSystems dev override, so a dev with that set would author entries that could
        // never match at login.

        /// <summary>Pseudo-variation keys inside a parsed override set. Real variations are 1 or above.</summary>
        private const int NoLogBaseKey = -1;   // the base landblock (VariationId null or 0)
        private const int NoLogAllKey = -2;    // base AND every variation

        private static readonly object NoLogParseLock = new();
        /// <summary>One parsed override set, published as a single reference so a reader never pairs a new
        /// raw string with stale dictionaries (writers replace the whole object under NoLogParseLock).</summary>
        private sealed class NoLogOverrides
        {
            public readonly string Raw;
            public readonly Dictionary<ushort, HashSet<int>> Adds;
            public readonly Dictionary<ushort, HashSet<int>> Removes;
            public NoLogOverrides(string raw, Dictionary<ushort, HashSet<int>> adds, Dictionary<ushort, HashSet<int>> removes)
            { Raw = raw; Adds = adds; Removes = removes; }
        }
        private static volatile NoLogOverrides _noLog = new(null, new(), new());

        /// <summary>
        /// Parse ServerConfig.nolog_landblocks on demand, caching against the exact string it was built
        /// from - so an edit through /nolog, /modifystring or a direct DB change is picked up on the next
        /// login with no invalidation hook to forget. Parsing is total: a malformed token is skipped,
        /// never thrown, because this runs on the login path.
        /// </summary>
        private static void EnsureNoLogOverrides()
        {
            var raw = ServerConfig.nolog_landblocks?.Value ?? "";
            if (_noLog.Raw == raw)
                return;

            lock (NoLogParseLock)
            {
                if (_noLog.Raw == raw)
                    return;

                var adds = new Dictionary<ushort, HashSet<int>>();
                var removes = new Dictionary<ushort, HashSet<int>>();

                foreach (var token in raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!TryParseNoLogToken(token, out var landblock, out var key, out var suppress))
                        continue;
                    var target = suppress ? removes : adds;
                    if (!target.TryGetValue(landblock, out var set))
                        target[landblock] = set = new HashSet<int>();
                    set.Add(key);
                }

                _noLog = new NoLogOverrides(raw, adds, removes);
            }
        }

        /// <summary>
        /// One override token: [+|-]HEX[:base|all|variation]. Shared by the parser above and by the
        /// /nolog command, so the two can never disagree about what a token means.
        /// </summary>
        public static bool TryParseNoLogToken(string token, out ushort landblock, out int variationKey, out bool suppress)
        {
            landblock = 0;
            variationKey = NoLogBaseKey;
            suppress = false;

            if (string.IsNullOrWhiteSpace(token))
                return false;

            token = token.Trim();
            if (token.StartsWith("-", StringComparison.Ordinal)) { suppress = true; token = token.Substring(1).Trim(); }
            else if (token.StartsWith("+", StringComparison.Ordinal)) { token = token.Substring(1).Trim(); }

            var parts = token.Split(':');
            var lbText = parts[0].Trim();
            if (lbText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                lbText = lbText.Substring(2);
            if (!ushort.TryParse(lbText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out landblock))
                return false;

            if (parts.Length == 1)
                return true;                                     // no variation given = base only
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
                return false;                                    // '0002:' or '0002:all:x' are not tokens

            var varText = parts[1].Trim();
            if (varText.Equals("base", StringComparison.OrdinalIgnoreCase)) { variationKey = NoLogBaseKey; return true; }
            if (varText.Equals("all", StringComparison.OrdinalIgnoreCase)) { variationKey = NoLogAllKey; return true; }
            if (!int.TryParse(varText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return false;
            if (v < 0)
                return false;   // -1 / -2 would alias the base / all pseudo-keys

            variationKey = NoLogVariationKey(v);
            return true;
        }

        /// <summary>A raw variation to its lookup key, collapsing the null/0 base bucket.</summary>
        public static int NoLogVariationKey(int? variation)
        {
            var normalized = VariationManager.NormalizeBase(variation);
            return normalized.HasValue ? normalized.Value : NoLogBaseKey;
        }

        /// <summary>The token an override entry is written as - the inverse of TryParseNoLogToken.</summary>
        public static string NoLogTokenFor(ushort landblock, int variationKey, bool suppress)
        {
            var scope = variationKey == NoLogBaseKey ? "base"
                : variationKey == NoLogAllKey ? "all"
                : variationKey.ToString(CultureInfo.InvariantCulture);
            return (suppress ? "-" : "") + landblock.ToString("X4") + ":" + scope;
        }

        private static bool NoLogSetMatches(Dictionary<ushort, HashSet<int>> map, ushort landblock, int variationKey)
            => map.TryGetValue(landblock, out var set) && (set.Contains(NoLogAllKey) || set.Contains(variationKey));

        /// <summary>
        /// Is this (landblock, variation) a no-log area? Precedence: an explicit suppress override beats
        /// everything, then an add override, then the retail seed - which only ever covers the BASE
        /// landblock. Safe to call before the Player object exists.
        /// </summary>
        public static bool IsNoLogArea(ushort landblock, int? variation)
        {
            EnsureNoLogOverrides();
            return IsNoLogArea(_noLog, landblock, variation);
        }

        /// <summary>Same test against one captured snapshot, so a caller walking several sets sees one configuration.</summary>
        private static bool IsNoLogArea(NoLogOverrides o, ushort landblock, int? variation)
        {
            var key = NoLogVariationKey(variation);

            if (NoLogSetMatches(o.Removes, landblock, key))
                return false;
            if (NoLogSetMatches(o.Adds, landblock, key))
                return true;

            return key == NoLogBaseKey && NoLog_Landblocks.ContainsKey(landblock);
        }

        /// <summary>The base-bucket scope key, for callers that need to test scope without a magic number.</summary>
        public static int NoLogBaseScopeKey => NoLogBaseKey;

        /// <summary>True when this exact entry comes from the built-in retail list rather than an override.</summary>
        public static bool IsNoLogSeed(ushort landblock, int variationKey)
            => variationKey == NoLogBaseKey && NoLog_Landblocks.ContainsKey(landblock);

        /// <summary>Every no-log entry in force, seed and overrides merged - for /nolog list.</summary>
        public static List<(ushort Landblock, int VariationKey)> NoLogEntries()
        {
            EnsureNoLogOverrides();
            var o = _noLog;

            var result = new List<(ushort, int)>();

            foreach (var lb in NoLog_Landblocks.Keys)
                if (IsNoLogArea(o, lb, null))
                    result.Add((lb, NoLogBaseKey));

            foreach (var kvp in o.Adds)
                foreach (var key in kvp.Value)
                {
                    if (IsNoLogSeed(kvp.Key, key))
                        continue;                                 // already listed from the seed
                    if (NoLogSetMatches(o.Removes, kvp.Key, key))
                        continue;
                    result.Add((kvp.Key, key));
                }

            result.Sort((a, b) => a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : a.Item2.CompareTo(b.Item2));
            return result;
        }

        /// <summary>The friendly name of a built-in no-log landblock, or empty for anything else.</summary>
        public static string NoLogSeedName(ushort landblock)
            => NoLog_Landblocks.TryGetValue(landblock, out var n) ? n : "";

        /// <summary>Human label for a variation key, matching the token vocabulary.</summary>
        public static string NoLogScopeName(int variationKey)
            => variationKey == NoLogBaseKey ? "base"
             : variationKey == NoLogAllKey ? "all"
             : "variation " + variationKey.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Called when a player first logs in
        /// </summary>
        public static void HandleNoLogLandblock(Biota biota, out bool playerWasMovedFromNoLogLandblock)
        {
            playerWasMovedFromNoLogLandblock = false;

            // NOTE: Admin and Sentinel are exempt - upstream behaviour, so a dev working inside a
            // dungeon is not thrown to their lifestone on every relog. It also means no-log CANNOT be
            // tested on an admin character; use a Player-level one (verified 2026-08-31).
            if (biota.WeenieType == WeenieType.Sentinel || biota.WeenieType == WeenieType.Admin) return;

            if (!biota.PropertiesPosition.TryGetValue(PositionType.Location, out var location))
                return;

            var landblock = (ushort)(location.ObjCellId >> 16);

            // 2026-08-31: the retail seed PLUS any authored overrides, and variation-aware. The old
            // "if (location.VariationId.HasValue) return;" guard is GONE - a variation can now be a
            // no-log area in its own right (owner). Base-only behaviour is unchanged when nothing is
            // authored, because the seed is only ever consulted for the base bucket.
            if (!IsNoLogArea(landblock, location.VariationId))
                return;

            // No lifestone = nowhere to send them, so they stay put. Worth a warning: the area IS
            // no-log and the player is silently getting away with it.
            if (!biota.PropertiesPosition.TryGetValue(PositionType.Sanctuary, out var lifestone))
            {
                log.Warn($"[NOLOG] {landblock:X4} is a no-log area but the character has no lifestone - cannot move them.");
                return;
            }

            location.ObjCellId = lifestone.ObjCellId;
            location.PositionX = lifestone.PositionX;
            location.PositionY = lifestone.PositionY;
            location.PositionZ = lifestone.PositionZ;
            location.RotationX = lifestone.RotationX;
            location.RotationY = lifestone.RotationY;
            location.RotationZ = lifestone.RotationZ;
            location.RotationW = lifestone.RotationW;
            location.VariationId = lifestone.VariationId;

            playerWasMovedFromNoLogLandblock = true;

            return;
        }
    }
}
