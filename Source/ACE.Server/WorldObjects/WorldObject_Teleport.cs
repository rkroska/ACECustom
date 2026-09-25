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

            // Variation 0 == retail base. Since 2026-09-17 the landblock/physics caches normalize 0 to null themselves
            // (LandblockManager.GetLandblock), so an explicit 0 can no longer build an empty parallel landblock copy;
            // this earlier choke point stays so TeleportDestinationVariation and the destination Position carry null,
            // the one value the base layer is stored and compared as.
            if (newPosition.Variation == 0)
                newPosition.Variation = null;

            if (player != null && player.HandleFogBeforeTeleport(_newPosition))
                return;

            // Room Assign (2026-09-16): a player teleporting OUT of a room keeps it for the leave hold. Here - after the fog
            // deferral, so only the real teleport counts, and before UpdatePosition moves Location to the destination.
            if (player != null)
                RoomAssignManager.OnPlayerTeleportStart(player, newPosition);

            // After fog deferral path returns false: cleanup runs with the real teleport (not ~1s early on a no-op).
            player?.CleanupPrestigeEffects();
            player?.CleanupZoneBoundaryEffects();

            Teleporting = true;
            // Item 9: from here until the placement lands, visibility treats us as already in the destination layer.
            TeleportDestinationVariation = newPosition.Variation;
            HasTeleportDestination = true;
            var timestamp = Time.GetUnixTime();
            SetProperty(PropertyFloat.LastTeleportStartTimestamp, timestamp);

            player?.MarkPortalSpaceEntered();

            if (player != null)
                player.LastTeleportTime = DateTime.UtcNow;

            // A teleport interrupts any in-progress attack loop. A stale live MeleeTarget/MissileTarget
            // otherwise swallows every subsequent attack request silently (the "already in melee loop"
            // early-out in HandleActionTargetedMeleeAttack), which presents as "cannot attack anything".
            if (player != null && (player.MeleeTarget != null || player.MissileTarget != null || player.AttackTarget != null))
                player.OnAttackDone();

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

            // Variant review 2026-09-17: the physics-failure branch of UpdatePosition ("placement FAILED during teleport",
            // which also drops the destination-layer pin) returns before Location is replaced, so Location still being the
            // origin object means we never reached the destination. The two destination-layer steps below must not run
            // then, or the physics variation is pinned to a layer the object never reached and the origin layer is swept out
            // of view. UpdatePosition's return value is "landblock or layer changed", not this, hence the reference check.
            var locationAdvanced = !ReferenceEquals(Location, prevLoc);

            // The physics placement above runs cell-entry enumerations (handle_visible_cells etc.)
            // while the player still carries the ORIGIN variation, which can re-track origin-variation
            // objects right after the cleanup at the top of this method (ghost mobs after /tv).
            // Sweep again now that Location holds the destination variation.
            if (locationAdvanced && prevLoc.Variation != newPosition.Variation)
            {
                try
                {
                    HandleVariationChangeVisbilityCleanup(prevLoc.Variation, newPosition.Variation);
                }
                catch (Exception e)
                {
                    log.Warn(e);
                }
            }

            // Post-teleport invariant: a player's CurrentLandblock must be the destination landblock
            // INSTANCE (landblock id + variation). Any path that leaves it stale makes creatures in the
            // destination instance untargetable — Landblock.GetObject resolves from CurrentLandblock and
            // its same-variation adjacents, and the melee/missile handlers silently no-op on a miss.
            if (player != null)
            {
                var lb = player.CurrentLandblock;
                if (lb == null || lb.Id.Landblock != Location.LandblockId.Landblock || lb.VariationId != Location.Variation)
                {
                    log.Warn($"{Name}.Teleport() - stale CurrentLandblock after teleport: " +
                             $"lb={(lb == null ? "null" : $"0x{lb.Id.Landblock:X4} v={lb.VariationId?.ToString() ?? "null"}")} " +
                             $"vs Location 0x{Location.LandblockId.Landblock:X4} v={Location.Variation?.ToString() ?? "null"} - forcing relocation");
                    LandblockManager.RelocateObjectForPhysics(this, true);
                }
            }

            // Final authoritative variation reconcile (fixes v11-object leak after a same-cell /tv
            // swap). set_request_pos only re-fetches CurCell when it is null, so a same-cell variation
            // change can leave the physics Position.Variation stale at the ORIGIN; since
            // GetEffectiveVariationForVisibility falls back to Position.Variation when Location.Variation
            // is null/base, the player stays mis-classified and re-tracks origin-variation objects every
            // tick, defeating the earlier sweeps. Pin the physics variation to the destination and do a
            // last cleanup so no origin-variation object survives the transition.
            if (locationAdvanced && player != null && prevLoc.Variation != newPosition.Variation)
            {
                if (PhysicsObj?.Position != null)
                    PhysicsObj.Position.Variation = newPosition.Variation;
                try
                {
                    HandleVariationChangeVisbilityCleanup(prevLoc.Variation, newPosition.Variation);
                }
                catch (Exception e)
                {
                    log.Warn(e);
                }
            }
        }

        /// <summary>
        /// Finalizes teleportation by cleaning up physics flags and state.
        /// Should be called when the teleport animation/delay is fully complete.
        /// </summary>
        public virtual void OnTeleportComplete()
        {
            (this as Player)?.ClearPortalSpaceEntered();

            // set materialize physics state
            // this takes the player from pink bubbles -> fully materialized
            // Only re-enable collisions if not cloaked (admin/GM) - Ghost is cloaked too
            if (CloakStatus != CloakStatus.On && CloakStatus != CloakStatus.Ghost)
                ReportCollisions = true;

            IgnoreCollisions = false;
            Hidden = false;
            Teleporting = false;
            HasTeleportDestination = false;   // item 9: Location carries the destination from here on

            if (this is Player pl && Player.LogPortalJumpSuppressToConsole && log.IsDebugEnabled)
            {
                var po = pl.PhysicsObj;
                var ts = po?.TransientState;
                log.Debug(
                    $"[PortalJumpSuppress][OnTeleportComplete] player={pl.Name} guid=0x{pl.Guid.Full:X8} Teleporting=false -> jump gate lifted. " +
                    $"LocCell=0x{pl.Location?.Cell ?? 0:X8} var={pl.Location?.Variation?.ToString() ?? "null"} FastTick={pl.FastTick} " +
                    $"PhysVel=({po?.Velocity.X:F4},{po?.Velocity.Y:F4},{po?.Velocity.Z:F4}) " +
                    $"OnWalkable={ts?.HasFlag(TransientStateFlags.OnWalkable)} Contact={ts?.HasFlag(TransientStateFlags.Contact)} IsJumping={pl.IsJumping}");
            }

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
                // Normalized compare (0 and null are both "base"); a raw == would wrongly drop an
                // explicit-0 base object when destination is null, or vice versa.
                if (VariationManager.SameVariationForVisibility(knownObj.Location.Variation, destinationVariation)) continue;

                knownObj.PhysicsObj.ObjMaint?.RemoveObject(PhysicsObj);
                PhysicsObj?.ObjMaint?.RemoveObject(knownObj.PhysicsObj);

                if (knownObj is Player knownPlayer) knownPlayer.RemoveTrackedObject(player, false);
                player.RemoveTrackedObject(knownObj, false);
            }

            // Variant review 2026-09-12 (item 10): the sweep above walks only what WE know. A peer who knows us
            // one-way (we never tracked them) kept us in their view after the switch: our movement broadcasts
            // reached them for up to 25 s, then stopped with no DeleteObject - a frozen ghost of the switching
            // player until they relogged. Walk the players who know us too, and drop the ones now in another layer.
            if (PhysicsObj?.ObjMaint == null) return;
            foreach (var peer in PhysicsObj.ObjMaint.GetKnownPlayersValuesAsPlayer())
            {
                if (peer == null || peer == player || peer.PhysicsObj == null) continue;
                var peerVar = VariationManager.GetEffectiveVariationForVisibility(peer);
                if (VariationManager.SameVariationForVisibility(peerVar, destinationVariation)) continue;

                peer.PhysicsObj.ObjMaint?.RemoveObject(PhysicsObj);
                PhysicsObj.ObjMaint.RemoveObject(peer.PhysicsObj);
                peer.RemoveTrackedObject(player, false);
                player.RemoveTrackedObject(peer, false);
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

            // pre-validate movement (skip during forced server teleport; CurrentLandblock is often null mid-handoff)
            if (player != null && !(Teleporting && forceUpdate) && !player.ValidateMovement(newPosition))
            {
                log.Warn($"{Name}.UpdatePosition() - movement pre-validation failed from {Location} to {newPosition}, t: {Teleporting}");
                return false;
            }

            // Vaulted Dungeons (review 2026-09-24): a chamber is only for the account it belongs to. Walking, jumping or a crafted
            // position into someone else's chamber is refused like a wall - the client is sent back where it was.
            if (player != null && !Teleporting && newPosition.Cell != Location.Cell && RoomAssignManager.IsTrespass(player, newPosition))
            {
                // Force the position on the player's OWN client too: it ignores an update for itself unless this sequence moved
                // on (the same bump the PK Lite relocation uses to force its broadcast).
                Sequences.GetNextSequence(ACE.Server.Network.Sequence.SequenceType.ObjectForcePosition);
                SendUpdatePosition();
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
                    else if (player != null &&
                             ACE.Server.Diagnostics.LogRateLimiter.ShouldEmit($"updatepos_nullcell:{Guid.Full}", TimeSpan.FromSeconds(10), out _))
                    {
                        // Void-walk diagnostic (2026-07-18): the physics move used to be skipped here
                        // with NO trace while Location still advanced below — a player could end up
                        // "in" a landblock that was never created (F658 v11 void). The heartbeat
                        // [VoidHeal] guard relocates them; this records the leak's moment.
                        log.Warn($"[VoidHeal] {Name}.UpdatePosition: get_landcell NULL for cell {newPosition.Cell:X8} " +
                                 $"v={newPosition.Variation?.ToString() ?? "null"} - physics move skipped, Location will still advance.");
                    }
                }
                else
                    PhysicsObj.Position.Frame.Orientation = newPosition.Rotation;
            }

            if (Teleporting && !forceUpdate) return true;

            if (!success)
            {
                // During a forced teleport this leaves Location and CurrentLandblock at the ORIGIN while
                // the client is already mid-teleport — log loudly so a variation/instance desync is traceable.
                if (Teleporting && forceUpdate)
                {
                    log.Warn($"{Name}.UpdatePosition() - physics placement FAILED during teleport to {newPosition} " +
                             $"(v={newPosition.Variation?.ToString() ?? "null"}); Location/CurrentLandblock left at origin " +
                             $"(loc v={Location.Variation?.ToString() ?? "null"}, lb v={CurrentLandblock?.VariationId?.ToString() ?? "null"})");

                    // Variant review 2026-09-17: we are still physically at the origin, so the item-9 pin (visibility judges a
                    // teleport in flight as already in its destination layer) comes off here and Teleport() skips its
                    // destination-layer sweep and physics-variation pin; the object is judged by its origin Location again.
                    HasTeleportDestination = false;
                }
                return false;
            }

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

            // Variant-only teleports can keep the same Cell value while still requiring a landblock/visibility refresh.
            // Avoid relocating on the per-tick physics update path (Player.UpdateObjectPhysics), since that already queues relocation via movedObjects.
            if (landblockUpdate && (player == null || !player.InUpdate))
                LandblockManager.RelocateObjectForPhysics(this, true);

            return landblockUpdate;
        }
    }
}
