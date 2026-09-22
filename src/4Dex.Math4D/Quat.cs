using System;
using System.Globalization;

namespace FourDex.Math4D
{
    /// <summary>
    /// Quaternion <c>q = W + X i + Y j + Z k</c>. <b>W is the real part.</b>
    /// </summary>
    /// <remarks>
    /// This is not a 3D rotation type. It exists as one half of a <see cref="Rotor4"/>,
    /// and as the embedding of a 4D point: <c>(x, y, z, w) -> w + x i + y j + z k</c>.
    /// </remarks>
    public readonly struct Quat : IEquatable<Quat>
    {
        public readonly float W;
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Quat(float w, float x, float y, float z)
        {
            W = w;
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly Quat Identity = new Quat(1f, 0f, 0f, 0f);

        /// <summary>Embeds a 4D point with the project convention: the point's W becomes the real part.</summary>
        public static Quat FromVec4(Vec4 p) => new Quat(p.W, p.X, p.Y, p.Z);

        public Vec4 ToVec4() => new Vec4(X, Y, Z, W);

        public Quat Conjugate() => new Quat(W, -X, -Y, -Z);

        public float NormSquared => W * W + X * X + Y * Y + Z * Z;

        public float Norm => MathF.Sqrt(NormSquared);

        public Quat Normalized()
        {
            float norm = Norm;
            if (!(norm > 0f))
                throw new InvalidOperationException("Cannot normalize a zero-length (or NaN) Quat.");
            float inv = 1f / norm;
            return new Quat(W * inv, X * inv, Y * inv, Z * inv);
        }

        /// <summary>
        /// <c>exp(vx i + vy j + vz k) = cos|v| + (v/|v|) sin|v|</c>. Exact, and always a unit quaternion.
        /// </summary>
        public static Quat ExpPure(float vx, float vy, float vz)
        {
            float angle = MathF.Sqrt(vx * vx + vy * vy + vz * vz);
            // sin(a)/a, with its Taylor series near zero so small per-frame rotations don't divide 0 by 0.
            float sinc = angle < 1e-4f ? 1f - angle * angle / 6f : MathF.Sin(angle) / angle;
            return new Quat(MathF.Cos(angle), vx * sinc, vy * sinc, vz * sinc);
        }

        /// <summary>Hamilton product: <c>ij = k, jk = i, ki = j, i² = j² = k² = -1</c>.</summary>
        public static Quat operator *(Quat a, Quat b) => new Quat(
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z,
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W);

        public static Quat operator -(Quat q) => new Quat(-q.W, -q.X, -q.Y, -q.Z);

        public static float Dot(Quat a, Quat b) => a.W * b.W + a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public bool ApproxEquals(Quat other, float tolerance = 1e-5f) =>
            MathF.Abs(W - other.W) <= tolerance &&
            MathF.Abs(X - other.X) <= tolerance &&
            MathF.Abs(Y - other.Y) <= tolerance &&
            MathF.Abs(Z - other.Z) <= tolerance;

        public bool Equals(Quat other) => W == other.W && X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object? obj) => obj is Quat other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(W, X, Y, Z);
        public static bool operator ==(Quat a, Quat b) => a.Equals(b);
        public static bool operator !=(Quat a, Quat b) => !a.Equals(b);

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0:F4} + {1:F4}i + {2:F4}j + {3:F4}k", W, X, Y, Z);
    }
}
