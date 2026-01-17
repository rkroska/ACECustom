using System;
using System.Collections.Generic;
using ACE.Entity.Enum;
using ACE.Server.Physics.Common;
using ACE.Server.Physics.Combat;

#nullable enable

namespace ACE.Server.Physics.Animation
{
    public class MovementManager(PhysicsObj obj, WeenieObject? wobj)
    {
        public MotionInterp MotionInterpreter = new(obj, wobj);
        public MoveToManager MoveToManager = new(obj, wobj);
        public PhysicsObj PhysicsObj = obj;
        public WeenieObject WeenieObj = wobj;

        public void CancelMoveTo(WeenieError error)
        {
            MoveToManager.CancelMoveTo(error);
        }

        public void EnterDefaultState()
        {
            MotionInterpreter.enter_default_state();
        }

        public void HandleExitWorld()
        {
            MotionInterpreter.HandleExitWorld();
        }

        public void HandleUpdateTarget(TargetInfo targetInfo)
        {
            MoveToManager.HandleUpdateTarget(targetInfo);
        }

        public void HitGround()
        {
            MotionInterpreter.HitGround();
            MoveToManager.HitGround();
        }

        public InterpretedMotionState InqInterpretedMotionState()
        {
            return MotionInterpreter.InterpretedState;
        }


        public bool IsMovingTo()
        {
            if (MoveToManager == null) return false;

            return MoveToManager.is_moving_to();
        }

        public void LeaveGround()
        {
            MotionInterpreter.LeaveGround();
        }


        public void MotionDone(bool success)
        {
            MotionInterpreter.MotionDone(success);
        }

        public WeenieError PerformMovement(MovementStruct mvs)
        {
            PhysicsObj.set_active(true);

            switch (mvs.Type)
            {
                case MovementType.RawCommand:
                case MovementType.InterpretedCommand:
                case MovementType.StopRawCommand:
                case MovementType.StopInterpretedCommand:
                case MovementType.StopCompletely:
                    return MotionInterpreter.PerformMovement(mvs);

                case MovementType.MoveToObject:
                case MovementType.MoveToPosition:
                case MovementType.TurnToObject:
                case MovementType.TurnToHeading:
                    return MoveToManager.PerformMovement(mvs);

                default:
                    return WeenieError.GeneralMovementFailure;
            }
        }

        public void ReportExhaustion()
        {
            MotionInterpreter.ReportExhaustion();
        }

        public void SetWeenieObject(WeenieObject wobj)
        {
            WeenieObj = wobj;
            MotionInterpreter.SetWeenieObject(wobj);
            MoveToManager.SetWeenieObject(wobj);
        }

        public void UseTime()
        {
            MoveToManager.UseTime();
        }

        public MotionInterp get_minterp()
        {
            return MotionInterpreter;
        }

    }
}
