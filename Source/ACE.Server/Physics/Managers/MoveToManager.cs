using ACE.Entity.Enum;
using ACE.Server.Physics.Combat;
using ACE.Server.Physics.Common;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace ACE.Server.Physics.Animation
{
    public class MoveToManager
    {
        private MovementType MovementType;
        private Position? SoughtPosition;
        private Position? CurrentTargetPosition;
        private Position? StartingPosition;
        private MovementParameters MovementParams = new();
        private float PreviousHeading;
        private float PreviousDistance;
        private double PreviousDistanceTime;
        private float OriginalDistance;
        private double OriginalDistanceTime;
        private double LastSuccessfulAction;
        private double LastTickTime;
        private uint TopLevelObjectID;
        private float SoughtObjectRadius;
        private float SoughtObjectHeight;
        private uint CurrentCommand;
        private uint AuxCommand;
        private bool MovingAway;
        public bool Initialized;
        public List<MovementNode> PendingActions = [];
        private readonly PhysicsObj PhysicsObj;
        private WeenieObject WeenieObj;
        public bool AlwaysTurn;

        public MoveToManager(PhysicsObj obj, WeenieObject wobj)
        {
            PhysicsObj = obj;
            WeenieObj = wobj;
            InitializeLocalVars();
        }

        public void InitializeLocalVars()
        {
            MovementType = MovementType.Invalid;

            MovementParams.DistanceToObject = 0;
            MovementParams.ContextID = 0;

            PreviousDistanceTime = PhysicsTimer.CurrentTime;
            OriginalDistanceTime = PhysicsTimer.CurrentTime;

            PreviousHeading = 0.0f;

            LastTickTime = PhysicsTimer.CurrentTime;
            LastSuccessfulAction = PhysicsTimer.CurrentTime;
            CurrentCommand = 0;
            AuxCommand = 0;
            MovingAway = false;
            Initialized = false;

            SoughtPosition = new Position();
            CurrentTargetPosition = new Position();

            TopLevelObjectID = 0;
            SoughtObjectRadius = 0;
            SoughtObjectHeight = 0;
        }

        /// <summary>
        /// Returns true if the manager is currently active.
        /// </summary>
        public bool IsActive() => CurrentCommand != 0 || PendingActions.Count > 0 || MovementType != MovementType.Invalid;

        /// <summary>
        /// Returns true if the last successful action was long ago (stuck).
        /// A custom stuckThreshold can be provided, otherwise a default of 5 seconds will be used.
        /// </summary>
        public bool IsStuck(double stuckThresholdSeconds = 5.0f)
        {
            if (!IsActive()) return false;
            // Compare against LastTickTime to ensure we only check against valid simulation opportunities
            if (LastTickTime > LastSuccessfulAction + stuckThresholdSeconds) return true;
            return false;
        }

        /// <summary>
        /// Resets the stuck flag, preventing "IsStuck" from triggering again immediately.
        /// </summary>
        public void ResetStuck()
        {
            LastSuccessfulAction = PhysicsTimer.CurrentTime;
        }

        /// <summary>
        /// Adds a node to the PendingActions list.
        /// If the list is empty, also marks the last successful action as now (for stuckness detection).
        /// </summary>
        private void AddPendingAction(MovementNode node)
        {
            if (PendingActions.Count == 0) LastSuccessfulAction = PhysicsTimer.CurrentTime;
            PendingActions.Add(node);
        }

        public WeenieError PerformMovement(MovementStruct mvs)
        {
            CancelMoveTo(WeenieError.ActionCancelled);
            PhysicsObj.unstick_from_object();
            switch (mvs.Type)
            {
                case MovementType.MoveToObject:
                    MoveToObject(mvs.TopLevelId, mvs.Radius, mvs.Height, mvs.Params);
                    break;
                case MovementType.MoveToPosition:
                    MoveToPosition(mvs.Position, mvs.Params);
                    break;
                case MovementType.TurnToObject:
                    TurnToObject(mvs.TopLevelId, mvs.Params);
                    break;
                case MovementType.TurnToHeading:
                    TurnToHeading(mvs.Params);
                    break;
            }
            // server - movement/anim update
            return WeenieError.None;
        }

        private void MoveToObject(uint topLevelID, float radius, float height, MovementParameters movementParams)
        {
            PhysicsObj.StopCompletely(false);

            StartingPosition = new Position(PhysicsObj.Position);
            SoughtObjectRadius = radius;
            SoughtObjectHeight = height;
            MovementType = MovementType.MoveToObject;
            TopLevelObjectID = topLevelID;

            MovementParams = new MovementParameters(movementParams);

            Initialized = false;
            if (PhysicsObj.ID != topLevelID)
            {
                PhysicsObj.set_target(0, TopLevelObjectID, 0.5f, 0.0f);
                return;
            }
            CleanUp();
            PhysicsObj.StopCompletely(false);
        }

        private void MoveToObject_Internal(Position targetPosition, Position interpolatedPosition)
        {
            SoughtPosition = new Position(interpolatedPosition);
            CurrentTargetPosition = new Position(targetPosition);

            var iHeading = PhysicsObj.Position.heading(interpolatedPosition);
            var heading = iHeading - PhysicsObj.get_heading();
            var dist = GetCurrentDistance();

            if (Math.Abs(heading) < PhysicsGlobals.EPSILON)
                heading = 0.0f;
            if (heading < -PhysicsGlobals.EPSILON)
                heading += 360.0f;

            HoldKey holdKey = HoldKey.Invalid;
            uint motionID = 0;
            bool moveAway = false;
            MovementParams.get_command(dist, heading, ref motionID, ref holdKey, ref moveAway);

            if (motionID != 0)
            {
                AddTurnToHeadingNode(iHeading);
                AddMoveToPositionNode();
            }
            if (MovementParams.UseFinalHeading)
            {
                var dHeading = iHeading + MovementParams.DesiredHeading;
                if (dHeading >= 360.0f)
                    dHeading -= 360.0f;
                AddTurnToHeadingNode(dHeading);
            }
            Initialized = true;
            BeginNextNode();
        }

        private void MoveToPosition(Position position, MovementParameters movementParams)
        {
            PhysicsObj.StopCompletely(false);

            CurrentTargetPosition = new Position(position);
            SoughtObjectRadius = 0.0f;

            var distance = GetCurrentDistance();
            var headingDiff = PhysicsObj.Position.heading(position) - PhysicsObj.get_heading();

            if (Math.Abs(headingDiff) < PhysicsGlobals.EPSILON)
                headingDiff = 0.0f;
            if (headingDiff < -PhysicsGlobals.EPSILON)
                headingDiff += 360.0f;

            HoldKey holdKey = HoldKey.Invalid;
            uint command = 0;
            bool moveAway = false;
            movementParams.get_command(distance, headingDiff, ref command, ref holdKey, ref moveAway);

            if (command != 0)
            {
                AddTurnToHeadingNode(PhysicsObj.Position.heading(position));
                AddMoveToPositionNode();
            }

            if (MovementParams.UseFinalHeading)
                AddTurnToHeadingNode(movementParams.DesiredHeading);

            SoughtPosition = new Position(position);
            StartingPosition = new Position(PhysicsObj.Position);

            MovementType = MovementType.MoveToPosition;

            MovementParams = new MovementParameters(movementParams)
            {
                //var flags = (MovementParamFlags)0xFFFFFF7F;     // unset Sticky?
                //MovementParams.Flags = MovementParams.Flags & flags;
                Sticky = false
            };

            BeginNextNode();
        }

        private void TurnToObject( uint topLevelID, MovementParameters movementParams)
        {
            if (movementParams.StopCompletely)
                PhysicsObj.StopCompletely(false);

            MovementType = MovementType.TurnToObject;

            CurrentTargetPosition?.Frame.set_heading(movementParams.DesiredHeading);

            TopLevelObjectID = topLevelID;
            MovementParams = new MovementParameters(movementParams);

            if (PhysicsObj.ID != topLevelID)
            {
                Initialized = false;
                PhysicsObj.set_target(0, topLevelID, 0.5f, 0.0f);
                return;
            }
            CleanUp();
            PhysicsObj.StopCompletely(false);
        }

        private void TurnToObject_Internal(Position targetPosition)
        {
            CurrentTargetPosition = new Position(targetPosition); // ref?

            var targetHeading = PhysicsObj.Position.heading(CurrentTargetPosition);
            var soughtHeading = SoughtPosition?.Frame.get_heading() ?? 0f;
            var heading = (targetHeading + soughtHeading) % 360.0f;
            SoughtPosition?.Frame.set_heading(heading);

            AddPendingAction(new MovementNode(MovementType.TurnToHeading, heading));
            Initialized = true;

            BeginNextNode();
        }

        private void TurnToHeading(MovementParameters movementParams)
        {
            if (movementParams.StopCompletely)
                PhysicsObj.StopCompletely(false);

            MovementParams = new MovementParameters(movementParams)
            {
                Sticky = false
            };

            SoughtPosition?.Frame.set_heading(movementParams.DesiredHeading);
            MovementType = MovementType.TurnToHeading;

            AddPendingAction(new MovementNode(MovementType.TurnToHeading, movementParams.DesiredHeading));
        }

        private void AddMoveToPositionNode()
        {
            AddPendingAction(new MovementNode(MovementType.MoveToPosition));
        }

        private void AddTurnToHeadingNode(float heading)
        {
            AddPendingAction(new MovementNode(MovementType.TurnToHeading, heading));
        }

        private void BeginNextNode()
        {
            if (PendingActions.Count > 0)
            {
                var pendingAction = PendingActions.First();

                switch (pendingAction.Type)
                {
                    case MovementType.MoveToPosition:
                        BeginMoveForward();
                        break;
                    case MovementType.TurnToHeading:
                        BeginTurnToHeading();
                        break;
                }
            }
            else
            {
                if (MovementParams.Sticky)
                {
                    var soughtObjectRadius = SoughtObjectRadius;
                    var soughtObjectHeight = SoughtObjectHeight;
                    var topLevelObjectID = TopLevelObjectID;

                    // unsets sticky flag
                    CleanUpAndCallWeenie(WeenieError.None);

                    PhysicsObj.get_position_manager().StickTo(topLevelObjectID, soughtObjectRadius, soughtObjectHeight);
                }
                else
                    CleanUpAndCallWeenie(WeenieError.None);
            }
        }

        private void BeginMoveForward()
        {
            // We are about to perform a new action, so reset the timer for stuckness.
            LastSuccessfulAction = PhysicsTimer.CurrentTime;

            var dist = GetCurrentDistance();
            var heading = PhysicsObj.Position.heading(CurrentTargetPosition) - PhysicsObj.get_heading();
            if (Math.Abs(heading) < PhysicsGlobals.EPSILON)
                heading = 0.0f;
            if (heading < -PhysicsGlobals.EPSILON)
                heading += 360.0f;

            uint motion = 0;
            bool moveAway = false;
            HoldKey holdKey = HoldKey.Invalid;
            MovementParams.get_command(dist, heading, ref motion, ref holdKey, ref moveAway);

            if (motion == 0)
            {
                RemovePendingActionsHead();
                BeginNextNode();
                return;
            }

            var movementParams = new MovementParameters
            {
                HoldKeyToApply = holdKey,
                CancelMoveTo = false,
                Speed = MovementParams.Speed
            };

            var result = _DoMotion(motion, movementParams);
            if (result != WeenieError.None)
            {
                CancelMoveTo(result);
                return;
            }

            CurrentCommand = motion;
            MovingAway = moveAway;
            MovementParams.HoldKeyToApply = holdKey;
            PreviousDistance = dist;
            PreviousDistanceTime = PhysicsTimer.CurrentTime;
            OriginalDistance = dist;
            OriginalDistanceTime = PhysicsTimer.CurrentTime;
        }

        /// <summary>
        /// Main iterator function for movement
        /// Can only be called when PhysicsObj is non-null.
        /// </summary>
        private void HandleMoveToPosition()
        {
            var curPos = new Position(PhysicsObj!.Position);
            var movementParams = new MovementParameters
            {
                CancelMoveTo = false,
                Speed = MovementParams.Speed,
                HoldKeyToApply = MovementParams.HoldKeyToApply
            };

            if (!PhysicsObj.IsAnimating)
            {
                var heading = MovementParameters.get_desired_heading(CurrentCommand, MovingAway) + curPos.heading(CurrentTargetPosition);
                if (heading >= 360.0f) heading -= 360.0f;

                var diff = heading - PhysicsObj.get_heading();

                if (Math.Abs(diff) < PhysicsGlobals.EPSILON) diff = 0.0f;
                if (diff < -PhysicsGlobals.EPSILON)
                    diff += 360.0f;

                if (diff > 20.0f && diff < 340.0f)
                {
                    uint motionID = diff >= 180.0f ? (uint)MotionCommand.TurnLeft : (uint)MotionCommand.TurnRight;
                    if (motionID != AuxCommand)
                    {
                        _DoMotion(motionID, movementParams);
                        AuxCommand = motionID;
                    }
                }
                else
                {
                    // custom: sync for server ticrate
                    if (AuxCommand != 0)
                        PhysicsObj.set_heading(heading, true);

                    stop_aux_command(movementParams);
                }
            }
            else
                stop_aux_command(movementParams);

            var dist = GetCurrentDistance();

            if (CheckProgressMade(dist))
            {
                // custom for low monster update rate
                var inRange = false;

                if (!MovementParams.UseSpheres)
                {
                    if (dist < 1.0f && PreviousDistance < dist)
                        inRange = true;

                    PreviousDistance = dist;
                    PreviousDistanceTime = PhysicsTimer.CurrentTime;
                }

                LastSuccessfulAction = PhysicsTimer.CurrentTime;
                if (MovingAway && dist >= MovementParams.MinDistance || !MovingAway && dist <= MovementParams.DistanceToObject || inRange)
                {
                    PendingActions.RemoveAt(0);
                    _StopMotion(CurrentCommand, movementParams);

                    CurrentCommand = 0;
                    stop_aux_command(movementParams);

                    BeginNextNode();
                }
                else if (StartingPosition != null)
                {
                    if (StartingPosition.Distance(PhysicsObj.Position) > MovementParams.FailDistance)
                        CancelMoveTo(WeenieError.YouChargedTooFar);
                }
            }

            if (TopLevelObjectID != 0 && MovementType != MovementType.Invalid)
            {
                var velocity = PhysicsObj.get_velocity();
                var velocityLength = velocity.Length();
                if (velocityLength > 0.1f)
                {
                    var time = dist / velocityLength;
                    if (Math.Abs(time - PhysicsObj.get_target_quantum()) > 1.0f)
                        PhysicsObj.set_target_quantum(time);
                }
            }
        }

        /// <summary>
        /// Starts a new and discrete turn to heading node
        /// Turning while moving forward is handled in HandleMoveToPosition
        /// </summary>
        private void BeginTurnToHeading()
        {
            if (PhysicsObj.IsAnimating && !AlwaysTurn) return;

            // We are about to perform a new action, so reset the timer for stuckness.
            LastSuccessfulAction = PhysicsTimer.CurrentTime;

            var pendingAction = PendingActions[0];
            var headingDiff = heading_diff(pendingAction.Heading, PhysicsObj.get_heading(), (uint)MotionCommand.TurnRight);
            uint motionID = 0;

            if (headingDiff <= 180.0f)
            {
                if (headingDiff > PhysicsGlobals.EPSILON)
                    motionID = (uint)MotionCommand.TurnRight;
                else
                {
                    RemovePendingActionsHead();
                    BeginNextNode();
                    return;
                }
            }
            else
            {
                if (headingDiff + PhysicsGlobals.EPSILON <= 360.0f)
                    motionID = (uint)MotionCommand.TurnLeft;
                else
                {
                    RemovePendingActionsHead();
                    BeginNextNode();
                    return;
                }
            }

            var movementParams = new MovementParameters
            {
                CancelMoveTo = false,
                Speed = MovementParams.Speed,    // only for turning, too fast?
                                                 //movementParams.Speed = 1.0f;    // commented out before?
                HoldKeyToApply = MovementParams.HoldKeyToApply
            };

            var result = _DoMotion(motionID, movementParams);

            if (result != WeenieError.None)
            {
                CancelMoveTo(result);
                return;
            }

            CurrentCommand = motionID;
            PreviousHeading = headingDiff;
        }

        /// <summary>
        /// Main iterator function for turning
        /// Can only be called when PhysicsObj is non-null.
        /// </summary>
        private void HandleTurnToHeading()
        {
            if (CurrentCommand != (uint)MotionCommand.TurnRight && CurrentCommand != (uint)MotionCommand.TurnLeft)
            {
                BeginTurnToHeading();
                return;
            }

            var heading = PhysicsObj!.get_heading();
            var pendingAction = PendingActions[0];
            if (heading_greater(heading, pendingAction.Heading, CurrentCommand))
            {
                LastSuccessfulAction = PhysicsTimer.CurrentTime;
                PhysicsObj.set_heading(pendingAction.Heading, true);

                RemovePendingActionsHead();

                var movementParams = new MovementParameters
                {
                    CancelMoveTo = false,
                    HoldKeyToApply = MovementParams.HoldKeyToApply
                };

                _StopMotion(CurrentCommand, movementParams);

                CurrentCommand = 0;
                BeginNextNode();
                return;
            }
            else
            {
                var diff = heading_diff(heading, PreviousHeading, CurrentCommand);
                if (diff > PhysicsGlobals.EPSILON && diff < 180.0f)
                    LastSuccessfulAction = PhysicsTimer.CurrentTime;
            }

            PreviousHeading = heading;
        }

        public void HandleUpdateTarget(TargetInfo targetInfo)
        {
            if (TopLevelObjectID != targetInfo.ObjectID)
                return;

            if (Initialized)
            {
                if (targetInfo.Status == TargetStatus.OK)
                {
                    if (MovementType == MovementType.MoveToObject)
                    {
                        SoughtPosition = new Position(targetInfo.InterpolatedPosition);
                        CurrentTargetPosition = new Position(targetInfo.TargetPosition);
                        PreviousDistance = float.MaxValue;
                        PreviousDistanceTime = PhysicsTimer.CurrentTime;
                        OriginalDistance = float.MaxValue;
                        OriginalDistanceTime = PhysicsTimer.CurrentTime;
                    }
                }
                else
                    CancelMoveTo(WeenieError.ObjectGone);
            }
            else if (TopLevelObjectID == PhysicsObj.ID)
            {
                SoughtPosition = new Position(PhysicsObj.Position);
                CurrentTargetPosition = new Position(PhysicsObj.Position);
                CleanUpAndCallWeenie(WeenieError.None);
            }
            else if (targetInfo.Status == TargetStatus.OK)
            {
                if (MovementType == MovementType.MoveToObject)
                    MoveToObject_Internal(targetInfo.TargetPosition, targetInfo.InterpolatedPosition);
                else if (MovementType == MovementType.TurnToObject)
                    TurnToObject_Internal(targetInfo.TargetPosition);
            }
            else
                CancelMoveTo(WeenieError.NoObject);
        }

        private bool CheckProgressMade(float currDistance)
        {
            var deltaTime = PhysicsTimer.CurrentTime - PreviousDistanceTime;

            if (deltaTime > 1.0f)
            {
                var diffDist = MovingAway ? currDistance - PreviousDistance : PreviousDistance - currDistance;

                // Improved stuck detection with better thresholds
                var progressRate = diffDist / deltaTime;
                var minProgressRate = 0.25f;
                
                // Adjust threshold based on movement type and distance
                if (MovementType == MovementType.MoveToObject)
                {
                    // For object movement, be more lenient at longer distances
                    if (currDistance > 10.0f)
                        minProgressRate = 0.15f;
                    else if (currDistance > 5.0f)
                        minProgressRate = 0.20f;
                }

                if (progressRate < minProgressRate)
                    return false;

                PreviousDistance = currDistance;
                PreviousDistanceTime = PhysicsTimer.CurrentTime;

                var dOrigDist = MovingAway ? currDistance - OriginalDistance : OriginalDistance - currDistance;
                var originalProgressRate = dOrigDist / (PhysicsTimer.CurrentTime - OriginalDistanceTime);

                if (originalProgressRate < minProgressRate)
                    return false;
            }
            return true;
        }

        public void CancelMoveTo(WeenieError retval)
        {
            if (MovementType == MovementType.Invalid) return;
            CurrentCommand = 0;
            PendingActions.Clear();
            CleanUpAndCallWeenie(retval);
        }

        private void CleanUp()
        {
            var movementParams = new MovementParameters
            {
                HoldKeyToApply = MovementParams.HoldKeyToApply,
                CancelMoveTo = false
            };

            if (PhysicsObj != null)
            {
                if (CurrentCommand != 0)
                    _StopMotion(CurrentCommand, movementParams);

                if (AuxCommand != 0)
                    _StopMotion(AuxCommand, movementParams);

                if (TopLevelObjectID != 0 && MovementType != MovementType.Invalid)
                    PhysicsObj.clear_target();
            }
            InitializeLocalVars();
        }

        private void CleanUpAndCallWeenie(WeenieError status)
        {
            CleanUp();
            PhysicsObj.StopCompletely(false);
            WeenieObj.OnMoveComplete(status);
        }

        private float GetCurrentDistance()
        {
            if (!MovementParams.Flags.HasFlag(MovementParamFlags.UseSpheres))
                return PhysicsObj.Position.Distance(CurrentTargetPosition);

            return (float)Position.CylinderDistance(PhysicsObj.GetRadius(), PhysicsObj.GetHeight(), PhysicsObj.Position,
                SoughtObjectRadius, SoughtObjectHeight, CurrentTargetPosition);
        }

        public void HitGround()
        {
            if (MovementType != MovementType.Invalid)
                BeginNextNode();
        }

        private void RemovePendingActionsHead()
        {
            if (PendingActions.Count > 0)
                PendingActions.RemoveAt(0);
        }

        public void SetWeenieObject(WeenieObject wobj)
        {
            WeenieObj = wobj;
        }

        /// <summary>
        /// Main Physics Tick entry point. Called by the Physics engine every quantum.
        /// </summary>
        public void UseTime()
        {
            if (!PhysicsObj.TransientState.HasFlag(TransientStateFlags.Contact))
                return;

            if (PendingActions.Count == 0)
                return;

            var pendingAction = PendingActions.First();

            if (TopLevelObjectID != 0 || MovementType != MovementType.Invalid || Initialized)
            {
                switch (pendingAction.Type)
                {
                    case MovementType.MoveToPosition:
                        HandleMoveToPosition();
                        break;
                    case MovementType.TurnToHeading:
                        HandleTurnToHeading();
                        break;
                }
            }

            // There was valid work to be done and we had the opportunity to do it.
            LastTickTime = PhysicsTimer.CurrentTime;
        }

        private WeenieError _DoMotion(uint motion, MovementParameters movementParams)
        {
            var minterp = PhysicsObj.get_minterp();
            if (minterp == null)
                return WeenieError.NoMotionInterpreter;

            minterp.adjust_motion(ref motion, ref movementParams.Speed, movementParams.HoldKeyToApply);

            return minterp.DoInterpretedMotion(motion, movementParams);
        }

        private WeenieError _StopMotion(uint motion, MovementParameters movementParams)
        {
            var minterp = PhysicsObj.get_minterp();
            if (minterp == null)
                return WeenieError.NoMotionInterpreter;

            minterp.adjust_motion(ref motion, ref movementParams.Speed, movementParams.HoldKeyToApply);

            return minterp.StopInterpretedMotion(motion, movementParams);
        }

        private static float heading_diff(float h1, float h2, uint motion)
        {
            var result = h1 - h2;

            if (Math.Abs(result) < PhysicsGlobals.EPSILON)
                result = 0.0f;
            if (result < -PhysicsGlobals.EPSILON)
                result += 360.0f;
            if (result > PhysicsGlobals.EPSILON && motion != (uint)MotionCommand.TurnRight)
                result = 360.0f - result;
            return result;
        }

        private static bool heading_greater(float h1, float h2, uint motion)
        {
            /*var less = Math.Abs(x - y) <= 180.0f ? x < y : y < x;
            var result = (less || x == y) == false;
            if (motion != 0x6500000D)
                result = !result;
            return result;*/
            var diff = Math.Abs(h1 - h2);

            float v1, v2;

            if (diff <= 180.0f)
            {
                v1 = h2;
                v2 = h1;
            }
            else
            {
                v1 = h1;
                v2 = h2;
            }

            var result = (v2 > v1);

            if (motion != (uint)MotionCommand.TurnRight)
                result = !result;

            return result;
        }

        public bool is_moving_to()
        {
            return MovementType != MovementType.Invalid;
        }

        private void stop_aux_command(MovementParameters movementParams)
        {
            if (AuxCommand != 0)
            {
                _StopMotion(AuxCommand, movementParams);
                AuxCommand = 0;
            }
        }
    }
}
