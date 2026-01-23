using System;

using ACE.Entity.Enum;

namespace ACE.Entity
{
    public class AttackFrameParams(uint motionTableId, MotionStance stance, MotionCommand motion) : IEquatable<AttackFrameParams>
    {
        public uint MotionTableId = motionTableId;
        public MotionStance Stance = stance;
        public MotionCommand Motion = motion;

        public bool Equals(AttackFrameParams? attackFrameParams)
        {
            if (attackFrameParams == null) return false;
            return MotionTableId == attackFrameParams.MotionTableId &&
                Stance == attackFrameParams.Stance &&
                Motion == attackFrameParams.Motion;
        }
        public override bool Equals(object? obj)
        {
            if (obj == null) return false;
            if (obj is not AttackFrameParams attackFrameParams) return false;
            return Equals(attackFrameParams);
        }
        public override int GetHashCode()
        {
            int hash = 0;

            hash = (hash * 397) ^ MotionTableId.GetHashCode();
            hash = (hash * 397) ^ Stance.GetHashCode();
            hash = (hash * 397) ^ Motion.GetHashCode();

            return hash;
        }
    }
}
