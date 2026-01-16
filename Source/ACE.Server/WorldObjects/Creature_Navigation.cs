using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics;
using ACE.Server.Physics.Animation;
using ACE.Server.Physics.Common;
using ACE.Server.Physics.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Creature navigation / position / rotation
    /// </summary>
    partial class Creature
    {
        /// <summary>
        /// Returns the 3D distance between this creature and target
        /// </summary>
        public float GetDistance(WorldObject target)
        {
            return Location.DistanceTo(target.Location);
        }

        /// <summary>
        /// Returns the 2D angle between current direction
        /// and position from an input target
        /// </summary>
        public float GetAngle(WorldObject target)
        {
            var currentDir = Location.GetCurrentDir();

            var targetDir = Vector3.Zero;
            if (Location.Indoors == target.Location.Indoors)
                targetDir = GetDirection(Location.ToGlobal(), target.Location.ToGlobal());
            else
                targetDir = GetDirection(Location.Pos, target.Location.Pos);

            targetDir.Z = 0.0f;
            targetDir = Vector3.Normalize(targetDir);
            
            // get the 2D angle between these vectors
            return GetAngle(currentDir, targetDir);
        }

        public float GetAngle_Physics(WorldObject target)
        {
            var currentDir = GetCurrentDir_Physics();

            var targetDir = Vector3.Zero;
            if (Location.Indoors == target.Location.Indoors)
                targetDir = GetDirection(Location.ToGlobal(), target.Location.ToGlobal());
            else
                targetDir = GetDirection(Location.Pos, target.Location.Pos);

            targetDir.Z = 0.0f;
            targetDir = Vector3.Normalize(targetDir);

            // get the 2D angle between these vectors
            return GetAngle(currentDir, targetDir);
        }

        public Vector3 GetCurrentDir_Physics()
        {
            return Vector3.Normalize(Vector3.Transform(Vector3.UnitY, PhysicsObj.Position.Frame.Orientation));
        }

        public float GetAngle_Physics2(WorldObject target)
        {
            return PhysicsObj.Position.heading_diff(target.PhysicsObj.Position);
        }

        /// <summary>
        /// Returns the 2D angle between current direction
        /// and rotation from an input position
        /// </summary>
        public float GetAngle(ACE.Entity.Position position)
        {
            var currentDir = Location.GetCurrentDir();
            var targetDir = position.GetCurrentDir();

            // get the 2D angle between these vectors
            return GetAngle(currentDir, targetDir);
        }

        /// <summary>
        /// Returns the 2D angle of the input vector
        /// </summary>
        public static float GetAngle(Vector3 dir)
        {
            var rads = Math.Atan2(dir.Y, dir.X);
            if (double.IsNaN(rads)) return 0.0f;

            var angle = rads * 57.2958f;
            return (float)angle;
        }

        /// <summary>
        /// Returns the 2D angle between 2 vectors
        /// </summary>
        public static float GetAngle(Vector3 a, Vector3 b)
        {
            var cosTheta = a.Dot2D(b);
            var rads = Math.Acos(cosTheta);
            if (double.IsNaN(rads)) return 0.0f;

            var angle = rads * (180.0f / Math.PI);
            return (float)angle;
        }

        /// <summary>
        /// Returns a normalized 2D vector from self to target
        /// </summary>
        public Vector3 GetDirection(Vector3 self, Vector3 target)
        {
            var target2D = new Vector3(self.X, self.Y, 0);
            var self2D = new Vector3(target.X, target.Y, 0);

            return Vector3.Normalize(target - self);
        }

        /// <summary>
        /// Sends a TurnToObject command to the client
        /// </summary>
        public void TurnToObject(WorldObject target, bool stopCompletely = true)
        {
            var turnToMotion = new Motion(this, target, MovementType.TurnToObject);

            if (!stopCompletely)
                turnToMotion.MoveToParameters.MovementParameters &= ~MovementParams.StopCompletely;

            EnqueueBroadcastMotion(turnToMotion);
        }

        /// <summary>
        /// Starts rotating a creature from its current direction
        /// so that it eventually is facing the target position
        /// </summary>
        /// <returns>The amount of time in seconds for the rotation to complete</returns>
        public virtual float Rotate(WorldObject target)
        {
            if (target == null || target.Location == null)
                return 0.0f;

            // send network message to start turning creature
            TurnToObject(target);

            var angle = GetAngle(target);
            //Console.WriteLine("Angle: " + angle);

            // estimate time to rotate to target
            var rotateDelay = GetRotateDelay(angle);
            //Console.WriteLine("RotateTime: " + rotateTime);

            // update server object rotation on completion
            // TODO: proper incremental rotation
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(rotateDelay);
            actionChain.AddAction(this, ActionType.CreatureNavigation_Rotate, () =>
            {
                if (target == null || target.Location == null)
                    return;

                //var matchIndoors = Location.Indoors == target.Location.Indoors;

                //var globalLoc = matchIndoors ? Location.ToGlobal() : Location.Pos;
                //var targetLoc = matchIndoors ? target.Location.ToGlobal() : target.Location.Pos;
                var globalLoc = Location.ToGlobal();
                var targetLoc = target.Location.ToGlobal();

                var targetDir = GetDirection(globalLoc, targetLoc);

                Location.Rotate(targetDir);
            });
            actionChain.EnqueueChain();

            return rotateDelay;
        }

        /// <summary>
        /// Returns the amount of time for this creature to rotate by the # of degrees
        /// from the input angle, using the omega speed from its MotionTable
        /// </summary>
        public virtual float GetRotateDelay(float angle)
        {
            var turnSpeed = MotionTable.GetTurnSpeed(MotionTableId);
            if (turnSpeed == 0.0f) return 0.0f;

            var rotateTime = Math.PI / turnSpeed / 180.0f * angle;
            return (float)rotateTime;
        }

        /// <summary>
        /// Returns the amount of time for this creature to rotate
        /// towards its target, based on the omega speed from its MotionTable
        /// </summary>
        public float GetRotateDelay(WorldObject target)
        {
            var angle = GetAngle(target);
            return GetRotateDelay(angle);
        }

        /// <summary>
        /// Starts rotating a creature from its current direction
        /// so that it eventually is facing the rotation from the input position
        /// Used by the emote system, which has the target rotation stored in positions
        /// </summary>
        /// <returns>The amount of time in seconds for the rotation to complete</returns>
        public float TurnTo(ACE.Entity.Position position)
        {
            var frame = new AFrame(position.Pos, position.Rotation);
            var heading = frame.get_heading();

            // send network message to start turning creature
            var turnToMotion = new Motion(this, position, heading);
            EnqueueBroadcastMotion(turnToMotion);

            var angle = GetAngle(position);
            //Console.WriteLine("Angle: " + angle);

            // estimate time to rotate to target
            var rotateDelay = GetRotateDelay(angle);
            //Console.WriteLine("RotateTime: " + rotateTime);

            // update server object rotation on completion
            // TODO: proper incremental rotation
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(rotateDelay);
            actionChain.AddAction(this, ActionType.CreatureNavigation_TurnToPosition, () =>
            {
                var targetDir = position.GetCurrentDir();
                Location.Rotate(targetDir);
                PhysicsObj.Position.Frame.Orientation = Location.Rotation;
            });
            actionChain.EnqueueChain();

            return rotateDelay;
        }

        /// <summary>
        /// Returns the amount of time for this creature to rotate
        /// towards the rotation from the input position, based on the omega speed from its MotionTable
        /// Used by the emote system, which has the target rotation stored in positions
        /// </summary>
        /// <param name="position">Only the rotation information from this position is used here</param>
        public float GetRotateDelay(ACE.Entity.Position position)
        {
            var angle = GetAngle(position);
            return GetRotateDelay(angle);
        }

        /// <summary>
        /// This is called by the monster AI system for ranged attacks
        /// It is mostly a duplicate of Rotate(), and should be refactored eventually...
        /// It sets CurrentMotionState and AttackTarget here
        /// </summary>
        public float TurnTo(WorldObject target, bool debug = false)
        {
            if (DebugMove)
                Console.WriteLine($"{Name}.TurnTo({target.Name})");

            if (this is Player) return 0.0f;

            var turnToMotion = new Motion(this, target, MovementType.TurnToObject);
            EnqueueBroadcastMotion(turnToMotion);

            CurrentMotionState = turnToMotion;

            AttackTarget = target;
            var rotateDelay = EstimateTurnTo();
            if (debug)
                Console.WriteLine("TurnTime = " + rotateDelay);
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(rotateDelay);
            actionChain.AddAction(this, ActionType.CreatureNavigation_TurnToTarget, () =>
            {
                // fix me: in progress turn
                //var targetDir = GetDirection(Location.ToGlobal(), target.Location.ToGlobal());
                //Location.Rotate(targetDir);
                if (debug)
                    Console.WriteLine("Finished turning - " + rotateDelay + "s");
            });
            actionChain.EnqueueChain();
            return rotateDelay;
        }

        /// <summary>
        /// Used by the monster AI system to start turning / running towards a target
        /// </summary>
        public virtual void MoveTo(WorldObject target, float runRate = 1.0f)
        {
            if (DebugMove)
                Console.WriteLine($"{Name}.MoveTo({target.Name}, {runRate}) - CurPos: {Location.ToLOCString()} - DestPos: {AttackTarget.Location.ToLOCString()} - TargetDist: {Vector3.Distance(Location.ToGlobal(), AttackTarget.Location.ToGlobal())}");

            var motion = GetMoveToMotion(target, runRate);

            CurrentMotionState = motion;

            EnqueueBroadcastMotion(motion);
        }

        public Motion GetMoveToMotion(WorldObject target, float runRate)
        {
            var motion = new Motion(this, target, MovementType.MoveToObject);
            motion.MoveToParameters.MovementParameters |= MovementParams.CanCharge | MovementParams.FailWalk | MovementParams.UseFinalHeading | MovementParams.Sticky | MovementParams.MoveAway;
            motion.MoveToParameters.WalkRunThreshold = 1.0f;

            if (runRate > 0)
                motion.RunRate = runRate;
            else
                motion.MoveToParameters.MovementParameters &= ~MovementParams.CanRun;

            return motion;
        }

        public virtual void BroadcastMoveTo(Player player)
        {
            Motion motion = null;

            if (AttackTarget != null)
            {
                // move to object
                motion = GetMoveToMotion(AttackTarget, RunRate);
            }
            else
            {
                // move to position
                motion = GetMoveToPosition(Home, RunRate, 1.0f);
            }

            player.Session.Network.EnqueueSend(new GameMessageUpdateMotion(this, motion));
        }

        /// <summary>
        /// Sends a network message for moving a creature to a new position
        /// </summary>
        public void MoveTo(ACE.Entity.Position position, float runRate = 1.0f, bool setLoc = true, float? walkRunThreshold = null, float? speed = null)
        {
            // build and send MoveToPosition message to client
            var motion = GetMoveToPosition(position, runRate, walkRunThreshold, speed);
            EnqueueBroadcastMotion(motion);

            if (!setLoc) return;

            // start executing MoveTo iterator on server
            if (!PhysicsObj.IsMovingOrAnimating)
                PhysicsObj.UpdateTime = PhysicsTimer.CurrentTime;

            var mvp = new MovementParameters(motion.MoveToParameters);
            PhysicsObj.MoveToPosition(new Physics.Common.Position(position), mvp);

            AddMoveToTick();
        }

        private void AddMoveToTick()
        {
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(monsterTickInterval);
            actionChain.AddAction(this, ActionType.CreatureNavigation_AddMoveToTick, () =>
            {
                if (!IsDead && PhysicsObj?.MovementManager?.MoveToManager != null && PhysicsObj.IsMovingTo())
                {
                    PhysicsObj.update_object();
                    UpdatePosition_SyncLocation();
                    SendUpdatePosition();

                    if (PhysicsObj?.MovementManager?.MoveToManager?.FailProgressCount < 5)
                    {
                        AddMoveToTick();
                    }
                    else
                    {
                        if (PhysicsObj?.MovementManager?.MoveToManager != null)
                        {
                            PhysicsObj.MovementManager.MoveToManager.CancelMoveTo(WeenieError.ActionCancelled);
                            PhysicsObj.MovementManager.MoveToManager.FailProgressCount = 0;
                        }
                        EnqueueBroadcastMotion(new Motion(CurrentMotionState.Stance, MotionCommand.Ready));
                    }

                    //Console.WriteLine($"{Name}.Position: {Location}");
                }
            });
            actionChain.EnqueueChain();
        }

        public Motion GetMoveToPosition(ACE.Entity.Position position, float runRate = 1.0f, float? walkRunThreshold = null, float? speed = null)
        {
            // TODO: change parameters to accept an optional MoveToParameters

            var motion = new Motion(this, position);
            motion.MovementType = MovementType.MoveToPosition;
            //motion.Flag |= MovementParams.CanCharge | MovementParams.FailWalk | MovementParams.UseFinalHeading | MovementParams.MoveAway;
            if (walkRunThreshold != null)
                motion.MoveToParameters.WalkRunThreshold = walkRunThreshold.Value;
            if (speed != null)
                motion.MoveToParameters.Speed = speed.Value;

            // always use final heading?
            var frame = new AFrame(position.Pos, position.Rotation);
            motion.MoveToParameters.DesiredHeading = frame.get_heading();
            motion.MoveToParameters.MovementParameters |= MovementParams.UseFinalHeading;
            motion.MoveToParameters.DistanceToObject = 0.6f;

            if (runRate > 0)
                motion.RunRate = runRate;
            else
                motion.MoveToParameters.MovementParameters &= ~MovementParams.CanRun;

            return motion;
        }

        /// <summary>
        /// Unified Teleport method for all creatures (Player and Monster/NPC).
        /// Handles visual effects, physics state changes, networking, and safety checks.
        /// </summary>
        public virtual void Teleport(ACE.Entity.Position _newPosition, bool fromPortal = false)
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
        /// Cleans up visibility of objects when switching variations.
        /// </summary>
        public virtual void HandleVariationChangeVisbilityCleanup(int? sourceVariation, int? destinationVariation)
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
        public virtual void DoTeleportPhysicsStateChanges()
        {
            bool broadcastUpdate = false;
            if (this is Player && !(Hidden ?? false)) { Hidden = true; broadcastUpdate = true; }
            if (!(IgnoreCollisions ?? false)) { IgnoreCollisions = true; broadcastUpdate = true; }
            if (ReportCollisions ?? false) { ReportCollisions = false; broadcastUpdate = true; }

            if (broadcastUpdate) EnqueueBroadcastPhysicsState();
        }

        /// <summary>
        /// Used by physics engine to actually update a creature/player position
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
                                log.Warn($"z-pos hacking detected for {Name}, lastGroundPos: {player.LastGroundPos.ToLOCString()} - requestPos: {newPosition.ToLOCString()}");
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
                player.RecordCast.Log($"CurPos: {Location.ToLOCString()}");

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
