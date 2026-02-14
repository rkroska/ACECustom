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

            Vector3 targetDir = (Location.Indoors == target.Location.Indoors) ?
                GetDirection(Location.ToGlobal(), target.Location.ToGlobal()) :
                GetDirection(Location.Pos, target.Location.Pos);

            targetDir.Z = 0.0f;
            targetDir = Vector3.Normalize(targetDir);
            
            // get the 2D angle between these vectors
            return GetAngle(currentDir, targetDir);
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
        /// Returns the 2D angle between 2 vectors
        /// </summary>
        private static float GetAngle(Vector3 a, Vector3 b)
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
        private static Vector3 GetDirection(Vector3 self, Vector3 target)
        {
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
                Console.WriteLine($"{Name}.MoveTo({target.Name}, {runRate}) - CurPos: {Location} - DestPos: {AttackTarget.Location} - TargetDist: {Vector3.Distance(Location.ToGlobal(), AttackTarget.Location.ToGlobal())}");

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
            Motion motion = (AttackTarget != null) ?
                // move to object
                GetMoveToMotion(AttackTarget, RunRate):
                // move to position
                GetMoveToPosition(Home, RunRate, 1.0f);

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

                    var moveToManager = PhysicsObj?.MovementManager?.MoveToManager;
                    if (moveToManager?.IsStuck(2.5f) ?? false)
                    {
                        moveToManager?.CancelMoveTo(WeenieError.ActionCancelled);
                        EnqueueBroadcastMotion(new Motion(CurrentMotionState.Stance, MotionCommand.Ready));
                        return;
                    }

                    AddMoveToTick();
                }
            });
            actionChain.EnqueueChain();
        }

        public Motion GetMoveToPosition(ACE.Entity.Position position, float runRate = 1.0f, float? walkRunThreshold = null, float? speed = null)
        {
            // TODO: change parameters to accept an optional MoveToParameters

            var motion = new Motion(this, position)
            {
                MovementType = MovementType.MoveToPosition
            };

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
    }
}
