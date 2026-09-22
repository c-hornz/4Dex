using System;

namespace FourDex.Math4D
{
    /// <summary>
    /// The four axes of a rotated frame, expressed in world coordinates: <see cref="X"/> is where the
    /// frame's local X axis points, and so on. These are the columns of the rotation matrix.
    /// </summary>
    /// <remarks>
    /// A derived, read-only view of a <see cref="Rotor4"/>. It is what the orientation HUD shows (SPEC §20)
    /// and what gets uploaded to shaders. Orientation is always integrated on the rotor, never here.
    /// </remarks>
    public readonly struct Basis4
    {
        public readonly Vec4 X;
        public readonly Vec4 Y;
        public readonly Vec4 Z;
        public readonly Vec4 W;

        private Basis4(Vec4 x, Vec4 y, Vec4 z, Vec4 w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static readonly Basis4 Identity = new Basis4(Vec4.UnitX, Vec4.UnitY, Vec4.UnitZ, Vec4.UnitW);

        public static Basis4 FromRotor(Rotor4 rotor) => new Basis4(
            rotor.Apply(Vec4.UnitX),
            rotor.Apply(Vec4.UnitY),
            rotor.Apply(Vec4.UnitZ),
            rotor.Apply(Vec4.UnitW));

        /// <summary>Axis 0..3 = X, Y, Z, W.</summary>
        public Vec4 this[int axis] => axis switch
        {
            0 => X,
            1 => Y,
            2 => Z,
            3 => W,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Axis must be 0..3 (X, Y, Z, W)."),
        };

        /// <summary>Frame coordinates to world: a weighted sum of the axes.</summary>
        public Vec4 ToWorld(Vec4 local) => X * local.X + Y * local.Y + Z * local.Z + W * local.W;

        /// <summary>World to frame coordinates. An orthonormal basis inverts by transpose, i.e. dot products.</summary>
        public Vec4 ToLocal(Vec4 world) => new Vec4(
            Vec4.Dot(world, X),
            Vec4.Dot(world, Y),
            Vec4.Dot(world, Z),
            Vec4.Dot(world, W));

        /// <summary>
        /// Largest entry of <c>|BᵀB - I|</c>. A few times 1e-7 is healthy for floats; anything growing over
        /// time means orientation is being integrated somewhere other than the rotor.
        /// </summary>
        public float OrthonormalityError()
        {
            float worst = 0f;
            for (int i = 0; i < 4; i++)
            {
                for (int j = i; j < 4; j++)
                {
                    float expected = i == j ? 1f : 0f;
                    worst = MathF.Max(worst, MathF.Abs(Vec4.Dot(this[i], this[j]) - expected));
                }
            }
            return worst;
        }

        public override string ToString() => $"X {X}  Y {Y}  Z {Z}  W {W}";
    }
}
