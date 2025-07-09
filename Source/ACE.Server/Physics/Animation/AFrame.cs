using System;
using System.Numerics;
using ACE.Server.Physics.Common;
using ACE.Server.Physics.Extensions;

namespace ACE.Server.Physics.Animation
{
    public class AFrame: IEquatable<AFrame>
    {
        public Vector3 Origin;
        public Quaternion Orientation;

        public AFrame()
        {
            Origin = Vector3.Zero;
            Orientation = Quaternion.Identity;
        }

        public AFrame(Vector3 origin, Quaternion orientation)
        {
            Origin = origin;
            Orientation = orientation;
        }

        public AFrame(AFrame frame)
        {
            Origin = frame.Origin;
            Orientation = new Quaternion(frame.Orientation.X, frame.Orientation.Y, frame.Orientation.Z, frame.Orientation.W);
        }

        public AFrame(DatLoader.Entity.Frame frame)
        {
            Origin = frame.Origin;
            Orientation = new Quaternion(frame.Orientation.X, frame.Orientation.Y, frame.Orientation.Z, frame.Orientation.W);
        }


        public static AFrame Combine(AFrame a, AFrame b)
        {
            return new AFrame
            {
                Origin = a.Origin + Vector3.Transform(b.Origin, a.Orientation),
                Orientation = a.Orientation * b.Orientation
            };
        }


        public void Combine(AFrame a, AFrame b, Vector3 scale)
        {
            var scaledOrigin = b.Origin * scale;
            var transformedOrigin = Vector3.Transform(scaledOrigin, a.Orientation);
            Origin = a.Origin + transformedOrigin;
            Orientation = a.Orientation * b.Orientation;
        }


        public Vector3 GlobalToLocal(Vector3 point)
        {
            var offset = point - Origin;
            var rotate = GlobalToLocalVec(offset); 
            return rotate;
        }

        public Vector3 GlobalToLocalVec(Vector3 point)
        {
            var rotate = Matrix4x4.Transpose(Matrix4x4.CreateFromQuaternion(Orientation));
            return Vector3.Transform(point, rotate);
        }

        public void InterpolateRotation(AFrame from, AFrame to, float t)
        {
            Orientation = Quaternion.Lerp(from.Orientation, to.Orientation, t);
        }

        public bool IsValid()
        {
            return Origin.IsValid() && Orientation.IsValid();
        }

        public bool IsValidExceptForHeading()
        {
            return Origin.IsValid();
        }

        public Vector3 LocalToGlobal(Vector3 point)
        {
            return Origin + LocalToGlobalVec(point);
        }

        public Vector3 LocalToGlobalVec(Vector3 point)
        {
            return Vector3.Transform(point, Orientation);
        }

        public void GRotate(Vector3 rotation)
        {
            Orientation *= Quaternion.CreateFromYawPitchRoll(rotation.X, rotation.Y, rotation.Z);
            Orientation  = Quaternion.Normalize(Orientation);
        }

        public void Rotate(Vector3 rotation)
        {
            var angles = Vector3.Transform(rotation, Orientation);
            GRotate(angles);
        }

        public void Subtract(AFrame frame)
        {
            Origin -= Vector3.Transform(frame.Origin, frame.Orientation);
            //Orientation *= Quaternion.Conjugate(frame.Orientation);
            Orientation *= Quaternion.Inverse(frame.Orientation);
        }


        public float get_heading()
        {
            var matrix = Matrix4x4.CreateFromQuaternion(Orientation);
            var heading = (float)Math.Atan2(matrix.M22, matrix.M21);
            return (450.0f - heading.ToDegrees()) % 360.0f;
        }

        public Vector3 get_vector_heading()
        {
            var matrix = Matrix4x4.CreateFromQuaternion(Orientation);

            var heading = new Vector3();

            heading.X = matrix.M21;
            heading.Y = matrix.M22;
            heading.Z = matrix.M23;

            return heading;
        }


        public void set_heading(float degrees)
        {
            var rads = degrees * (MathF.PI / 180.0f); // Inline conversion to radians

            var sinRads = MathF.Sin(rads);
            var cosRads = MathF.Cos(rads);

            var matrix = Matrix4x4.CreateFromQuaternion(Orientation);
            var heading = new Vector3(sinRads, cosRads, matrix.M23 + matrix.M13);
            set_vector_heading(heading);
        }


        public void set_rotate(Quaternion orientation)
        {
            Orientation = Quaternion.Normalize(orientation);
        }

        public void set_vector_heading(Vector3 heading)
        {
            if (Vec.NormalizeCheckSmall(ref heading)) return;

            var zDeg = 450.0f - ((float)Math.Atan2(heading.Y, heading.X)).ToDegrees();
            var zRot = -(zDeg % 360.0f).ToRadians();

            var xRot = (float)Math.Asin(heading.Z);

            var rotate = Quaternion.CreateFromYawPitchRoll(xRot, 0, zRot);
            set_rotate(rotate);
        }


        public bool Equals(AFrame frame)
        {
            var originEpsilonEqual = Math.Abs(frame.Origin.X - Origin.X) <= PhysicsGlobals.EPSILON &&
                Math.Abs(frame.Origin.Y - Origin.Y) <= PhysicsGlobals.EPSILON &&
                Math.Abs(frame.Origin.Z - Origin.Z) <= PhysicsGlobals.EPSILON;

            if (!originEpsilonEqual) return false;

            var orientationEpsilonEqual = Math.Abs(frame.Orientation.X - Orientation.X) <= PhysicsGlobals.EPSILON &&
                Math.Abs(frame.Orientation.Y - Orientation.Y) <= PhysicsGlobals.EPSILON &&
                Math.Abs(frame.Orientation.Z - Orientation.Z) <= PhysicsGlobals.EPSILON &&
                Math.Abs(frame.Orientation.W - Orientation.W) <= PhysicsGlobals.EPSILON;

            return orientationEpsilonEqual;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AFrame);
        }
    }
}
