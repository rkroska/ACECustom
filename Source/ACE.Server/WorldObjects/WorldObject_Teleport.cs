using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics;
using ACE.Server.Physics.Common;
using System;

namespace ACE.Server.WorldObjects
{
    partial class WorldObject
    {

        /// <summary>
        /// Unified Teleport method for all world objects.
        /// Handles visual effects, physics state changes, networking, and safety checks.
        /// </summary>
        public void Teleport(ACE.Entity.Position _newPosition, bool fromPortal = false)
        {
            var player = this as Player; // null if not a player
            var newPosition = new ACE.Entity.Position(_newPosition);
            newPosition.PositionZ += 0.005f * (ObjScale ?? 1.0f);

            if (player != null && player.HandleFogBeforeTeleport(_newPosition))
                return;

            Teleporting = true;
            var timestamp = Time.GetUnixTime();
            SetProperty(PropertyFloat.LastTeleportStartTimestamp, timestamp);

            if (player != null)
                player.LastTeleportTime = DateTime.UtcNow;

            if (fromPortal)
                SetProperty(PropertyFloat.LastPortalTeleportTimestamp, timestamp);

            // check for changing varation - and remove anything from knownobjects that is not in the new variation
            try
            {
                HandleVariationChangeVisbilityCleanup(Location.Variation, newPosition.Variation);
            }
            catch (Exception e)
            {
                log.Warn(e);
            }

            player?.Session.Network.EnqueueSend(new GameMessagePlayerTeleport(player));

            // load quickly, but player can load into landblock before server is finished loading
            // send a "fake" update position to get the client to start loading asap,
            // also might fix some decal bugs
            var prevLoc = Location;
            Location = newPosition;
            SendUpdatePosition();
            Location = prevLoc;

            DoTeleportPhysicsStateChanges();

            // force out of hotspots
            PhysicsObj?.report_collision_end(true);

            if (player != null && player.UnderLifestoneProtection)
                player.LifestoneProtectionDispel();

            player?.HandlePreTeleportVisibility(newPosition);

            UpdatePosition(new ACE.Entity.Position(newPosition), true);
        }

        /// <summary>
        /// Finalizes teleportation by cleaning up physics flags and state.
        /// Should be called when the teleport animation/delay is fully complete.
        /// </summary>
        public virtual void OnTeleportComplete()
        {
            // set materialize physics state
            // this takes the player from pink bubbles -> fully materialized
            // Only re-enable collisions if not cloaked (admin/GM)
            if (CloakStatus != CloakStatus.On)
                ReportCollisions = true;

            IgnoreCollisions = false;
            Hidden = false;
            Teleporting = false;

            EnqueueBroadcastPhysicsState();
        }

        /// <summary>
        /// Cleans up visibility of objects when switching variations.
        /// </summary>
        public void HandleVariationChangeVisbilityCleanup(int? sourceVariation, int? destinationVariation)
        {
            if (this is not Player player) return;

            foreach (WorldObject knownObj in player.GetKnownObjects())
            {
                if (knownObj.PhysicsObj == null) continue;
                if (knownObj.Location == null) continue;
                if (knownObj.Location.Variation == destinationVariation) continue;

                knownObj.PhysicsObj.ObjMaint?.RemoveObject(PhysicsObj);
                PhysicsObj?.ObjMaint?.RemoveObject(knownObj.PhysicsObj);

                if (knownObj is Player knownPlayer) knownPlayer.RemoveTrackedObject(player, false);
                player.RemoveTrackedObject(knownObj, false);
            }
        }

        /// <summary>
        /// Updates physics flags (Hidden, IgnoreCollisions, ReportCollisions) for teleportation.
        /// Broadcasts updates only if values change.
        /// </summary>
        public void DoTeleportPhysicsStateChanges()
        {
            bool broadcastUpdate = false;
            if (this is Player && !(Hidden ?? false)) { Hidden = true; broadcastUpdate = true; }
            if (!(IgnoreCollisions ?? false)) { IgnoreCollisions = true; broadcastUpdate = true; }
            if (ReportCollisions ?? false) { ReportCollisions = false; broadcastUpdate = true; }

            if (broadcastUpdate) EnqueueBroadcastPhysicsState();
        }

        /// <summary>
        /// Used by physics engine to actually update a position
        /// Automatically notifies clients of updated position
        /// </summary>
        public bool UpdatePosition(ACE.Entity.Position newPosition, bool forceUpdate = false)
        {
            bool verifyContact = false;
            var player = this as Player;

            // possible bug: while teleporting, client can still send AutoPos packets from old landblock
            if (Teleporting && !forceUpdate) return false;

            if (!Teleporting && Location.Variation != null && newPosition.Variation == null) //do not wipe out the prior Variation unless teleporting
            {
                newPosition.Variation = Location.Variation;
            }

            // pre-validate movement
            if (player != null && !player.ValidateMovement(newPosition))
            {
                log.Error($"{Name}.UpdatePosition() - movement pre-validation failed from {Location} to {newPosition}, t: {Teleporting}");
                return false;
            }

            bool variationChange = Location.Variation != newPosition.Variation;

            var success = true;

            if (PhysicsObj != null)
            {
                var distSq = Location.SquaredDistanceTo(newPosition);

                if (distSq > PhysicsGlobals.EpsilonSq || variationChange)
                {
                    if (!Teleporting && player != null)
                    {
                        var blockDist = PhysicsObj.GetBlockDist(Location.Cell, newPosition.Cell);

                        // verify movement
                        if (distSq > Player.MaxSpeedSq && blockDist > 1)
                        {
                            log.Warn($"MOVEMENT SPEED: {Name} trying to move from {Location} to {newPosition}, speed: {Math.Sqrt(distSq)}");
                            return false;
                        }

                        // verify z-pos
                        // Simplified for base creature (or only for player if needed)
                        if (blockDist == 0 && player.LastGroundPos != null && newPosition.PositionZ - player.LastGroundPos.PositionZ > 10 && DateTime.UtcNow - player.LastJumpTime > TimeSpan.FromSeconds(1) && player.GetCreatureSkill(Skill.Jump).Current < 1000)
                            verifyContact = true;
                    }

                    var curCell = LScape.get_landcell(newPosition.Cell, newPosition.Variation);
                    if (curCell != null)
                    {
                        PhysicsObj.set_request_pos(newPosition.Pos, newPosition.Rotation, curCell, Location.LandblockId.Raw, newPosition.Variation);

                        if (player != null && player.FastTick)
                            success = PhysicsObj.update_object_server_new();
                        else
                            success = PhysicsObj.update_object_server();

                        if (PhysicsObj.CurCell == null && curCell.ID >> 16 != 0x18A)
                        {
                            PhysicsObj.CurCell = curCell;
                        }

                        if (verifyContact && player != null && player.IsJumping)
                        {
                            var blockDist = PhysicsObj.GetBlockDist(newPosition.Cell, player.LastGroundPos.Cell);

                            if (blockDist <= 1)
                            {
                                log.Warn($"z-pos hacking detected for {Name}, lastGroundPos: {player.LastGroundPos} - requestPos: {newPosition}");
                                Location = new ACE.Entity.Position(player.LastGroundPos);
                                //Sequences.GetNextSequence(SequenceType.ObjectForcePosition);
                                SendUpdatePosition();
                                return false;
                            }
                        }

                        player?.CheckMonsters();
                    }
                }
                else
                    PhysicsObj.Position.Frame.Orientation = newPosition.Rotation;
            }

            if (Teleporting && !forceUpdate) return true;

            if (!success) return false;

            var landblockUpdate = (Location.Cell >> 16 != newPosition.Cell >> 16) || variationChange;

            Location = new ACE.Entity.Position(newPosition);

            if (player != null && player.RecordCast.Enabled)
                player.RecordCast.Log($"CurPos: {Location}");

            if (player != null && (player.RequestedLocationBroadcast || DateTime.UtcNow - player.LastUpdatePosition >= Player.MoveToState_UpdatePosition_Threshold))
                SendUpdatePosition();
            else if (player != null)
                player.Session.Network.EnqueueSend(new GameMessageUpdatePosition(this));
            else
                SendUpdatePosition(); // Creature always sends?

            LandblockManager.RelocateObjectForPhysics(this, true);

            return landblockUpdate;
        }
    }
}
