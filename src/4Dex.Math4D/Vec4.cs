using System;
using System.Globalization;

namespace FourDex.Math4D
{
    /// <summary>
    /// A point or direction in 4D Euclidean space, <c>P = (x, y, z, w)</c>.
    /// <c>+W</c> is ana, <c>-W</c> is kata.
    /// </summary>
    /// <remarks>
    /// Not <c>UnityEngine.Vector4</c>: Math4D must compile without Unity (SPEC §3),
    /// and Unity's Vector4 is a shader-data type, not a geometric one.
    /// </remarks>
    public readonly struct Vec4 : IEquatable<Vec4>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly float W;

        public Vec4(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static readonly Vec4 Zero = new Vec4(0f, 0f, 0f, 0f);
        public static readonly Vec4 UnitX = new Vec4(1f, 0f, 0f, 0f);
        public static readonly Vec4 UnitY = new Vec4(0f, 1f, 0f, 0f);
        public static readonly Vec4 UnitZ = new Vec4(0f, 0f, 1f, 0f);
        public static readonly Vec4 UnitW = new Vec4(0f, 0f, 0f, 1f);

        /// <summary>Unit vector along axis 0..3 = X, Y, Z, W.</summary>
        public static Vec4 Axis(int axis) => axis switch
        {
            0 => UnitX,
            1 => UnitY,
            2 => UnitZ,
            3 => UnitW,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Axis must be 0..3 (X, Y, Z, W)."),
        };

        /// <summary>Component by axis index 0..3 = X, Y, Z, W.</summary>
        public float this[int axis] => axis switch
        {
            0 => X,
            1 => Y,
            2 => Z,
            3 => W,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Axis must be 0..3 (X, Y, Z, W)."),
        };

        public float LengthSquared => X * X + Y * Y + Z * Z + W * W;

        public float Length => MathF.Sqrt(LengthSquared);

        public Vec4 Normalized()
        {
            float length = Length;
            if (!(length > 0f))
                throw new InvalidOperationException("Cannot normalize a zero-length (or NaN) Vec4.");
            return this / length;
        }

        /// <summary>Same point with its W replaced — moving through ana/kata only (SPEC §7A).</summary>
        public Vec4 WithW(float w) => new Vec4(X, Y, Z, w);

        public static float Dot(Vec4 a, Vec4 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

        public static float Distance(Vec4 a, Vec4 b) => (a - b).Length;

        public static Vec4 operator +(Vec4 a, Vec4 b) => new Vec4(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);
        public static Vec4 operator -(Vec4 a, Vec4 b) => new Vec4(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);
        public static Vec4 operator -(Vec4 v) => new Vec4(-v.X, -v.Y, -v.Z, -v.W);
        public static Vec4 operator *(Vec4 v, float s) => new Vec4(v.X * s, v.Y * s, v.Z * s, v.W * s);
        public static Vec4 operator *(float s, Vec4 v) => v * s;
        public static Vec4 operator /(Vec4 v, float s) => new Vec4(v.X / s, v.Y / s, v.Z / s, v.W / s);

        /// <summary>Per-component comparison within an absolute tolerance.</summary>
        public bool ApproxEquals(Vec4 other, float tolerance = 1e-5f) =>
            MathF.Abs(X - other.X) <= tolerance &&
            MathF.Abs(Y - other.Y) <= tolerance &&
            MathF.Abs(Z - other.Z) <= tolerance &&
            MathF.Abs(W - other.W) <= tolerance;

        public bool Equals(Vec4 other) => X == other.X && Y == other.Y && Z == other.Z && W == other.W;
        public override bool Equals(object? obj) => obj is Vec4 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
        public static bool operator ==(Vec4 a, Vec4 b) => a.Equals(b);
        public static bool operator !=(Vec4 a, Vec4 b) => !a.Equals(b);

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:F4}, {1:F4}, {2:F4}, {3:F4})", X, Y, Z, W);
    }
}
