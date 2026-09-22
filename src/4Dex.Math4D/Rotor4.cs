using System;

namespace FourDex.Math4D
{
    /// <summary>
    /// A 4D rotation stored as a pair of unit quaternions, applied as <c>p' = L * p * conj(R)</c>
    /// with <c>p = w + x i + y j + z k</c> (ARCHITECTURE.md §4).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Why not a 4x4 matrix: repeated composition drifts off SO(4) and Gram-Schmidt repair silently
    /// perturbs the basis. Two unit quaternions renormalize exactly and independently, so every
    /// constructor renormalizes and drift cannot accumulate.
    /// </para>
    /// <para>
    /// <c>{L, R}</c> and <c>{-L, -R}</c> are the same rotation (SO(4) is double covered). Compare with
    /// <see cref="ApproxEquals"/>, never field by field.
    /// </para>
    /// <para>
    /// Generator table, derived by expanding the quaternion products. With <c>L = exp(a u)</c>,
    /// <c>R = exp(b u)</c> for one unit imaginary <c>u</c>, in the <see cref="Plane4"/> sign convention:
    /// <code>
    ///   u = i :  YZ by (a + b),   XW by (b - a)
    ///   u = j :  ZX by (a + b),   YW by (b - a)     (ZX = -XZ)
    ///   u = k :  XY by (a + b),   ZW by (b - a)
    /// </code>
    /// A pure 3D plane is <c>a = b = t/2</c>; a pure ana/kata plane is <c>a = -t/2, b = +t/2</c>.
    /// Each ana/kata plane shares a generator with exactly one ordinary 3D plane: XW with pitch (YZ),
    /// YW with yaw (XZ), ZW with roll (XY).
    /// </para>
    /// </remarks>
    public readonly struct Rotor4
    {
        /// <summary>Left-isoclinic half. Always unit length.</summary>
        public readonly Quat L;

        /// <summary>Right-isoclinic half. Always unit length.</summary>
        public readonly Quat R;

        public Rotor4(Quat left, Quat right)
        {
            L = left.Normalized();
            R = right.Normalized();
        }

        public static readonly Rotor4 Identity = new Rotor4(Quat.Identity, Quat.Identity);

        /// <summary>A simple rotation by <paramref name="angle"/> radians in one coordinate plane.</summary>
        public static Rotor4 FromPlane(Plane4 plane, float angle) => FromBivector(Bivector4.FromPlane(plane, angle));

        /// <summary>
        /// The exact exponential of a bivector: all six plane angles applied simultaneously, independent of order.
        /// </summary>
        public static Rotor4 FromBivector(Bivector4 b)
        {
            // so(4) splits into left and right su(2) halves. Solving the generator table for (a, b) per
            // generator gives each half's imaginary part; the XZ terms flip sign because j generates ZX.
            Quat left = Quat.ExpPure(
                0.5f * (b.YZ - b.XW),
                0.5f * (-b.XZ - b.YW),
                0.5f * (b.XY - b.ZW));
            Quat right = Quat.ExpPure(
                0.5f * (b.YZ + b.XW),
                0.5f * (-b.XZ + b.YW),
                0.5f * (b.XY + b.ZW));
            return new Rotor4(left, right);
        }

        /// <summary>
        /// The bivector whose exponential is this rotation: its six simultaneous plane angles. Inverse of
        /// <see cref="FromBivector"/>, for the rotation monitor (SPEC §20) and for measuring rotation rates.
        /// </summary>
        /// <remarks>
        /// Take each half's quaternion log, then undo <see cref="FromBivector"/>'s mixing of plane pairs. {L, R} and
        /// {-L, -R} are the same rotation with different logs; the one with the smaller total angle is returned, so
        /// small rotations give small angles. Exact inverse whenever the two halves' angles sum to less than pi.
        /// </remarks>
        public Bivector4 Log()
        {
            (float lx, float ly, float lz, float thetaL) = LogPure(L);
            (float rx, float ry, float rz, float thetaR) = LogPure(R);
            if (thetaL + thetaR > MathF.PI)
            {
                // Log of -q is (pi - theta) about the opposite axis.
                (lx, ly, lz) = Rescale(lx, ly, lz, thetaL, thetaL - MathF.PI);
                (rx, ry, rz) = Rescale(rx, ry, rz, thetaR, thetaR - MathF.PI);
            }

            // FromBivector: left = 0.5 (YZ - XW, -XZ - YW, XY - ZW), right = 0.5 (YZ + XW, -XZ + YW, XY + ZW).
            return new Bivector4(
                xy: lz + rz,
                xz: -(ly + ry),
                yz: lx + rx,
                xw: rx - lx,
                yw: ry - ly,
                zw: rz - lz);
        }

        // Imaginary part of log(q) for a unit quaternion, and its length theta in [0, pi].
        private static (float X, float Y, float Z, float Theta) LogPure(Quat q)
        {
            float s = MathF.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z);
            float theta = MathF.Atan2(s, q.W);
            // theta / sin(theta) -> 1 as theta -> 0; avoids 0/0 at identity.
            float k = s > 1e-7f ? theta / s : 1f;
            return (q.X * k, q.Y * k, q.Z * k, theta);
        }

        private static (float, float, float) Rescale(float x, float y, float z, float length, float newLength)
        {
            // Unreachable in practice: each half's theta is in [0, pi], so the flip needs both halves nonzero.
            if (length < 1e-7f)
                return (0f, 0f, 0f);
            float k = newLength / length;
            return (x * k, y * k, z * k);
        }

        public Vec4 Apply(Vec4 p) => (L * Quat.FromVec4(p) * R.Conjugate()).ToVec4();

        /// <summary>
        /// This rotation followed by <paramref name="next"/>: <c>p -> next.Apply(this.Apply(p))</c>.
        /// </summary>
        /// <remarks>
        /// <c>next.L (L p conj R) conj(next.R) = (next.L L) p conj(next.R R)</c>, so each half composes on
        /// its own — two quaternion products, then exact renormalization in the constructor.
        /// </remarks>
        public Rotor4 Then(Rotor4 next) => new Rotor4(next.L * L, next.R * R);

        /// <summary>Matrix-style composition: <c>(outer * inner).Apply(p) == outer.Apply(inner.Apply(p))</c>.</summary>
        public static Rotor4 operator *(Rotor4 outer, Rotor4 inner) => inner.Then(outer);

        /// <summary>Unit quaternions invert by conjugation, so the inverse is exact.</summary>
        public Rotor4 Inverse() => new Rotor4(L.Conjugate(), R.Conjugate());

        /// <summary>The four rotated axes, for display and shader upload. Never integrate orientation through it.</summary>
        public Basis4 ToBasis() => Basis4.FromRotor(this);

        /// <summary>Same rotation within tolerance, accounting for the <c>{L, R} = {-L, -R}</c> double cover.</summary>
        public bool ApproxEquals(Rotor4 other, float tolerance = 1e-5f) =>
            (L.ApproxEquals(other.L, tolerance) && R.ApproxEquals(other.R, tolerance)) ||
            (L.ApproxEquals(-other.L, tolerance) && R.ApproxEquals(-other.R, tolerance));

        public override string ToString() => $"L = {L}, R = {R}";
    }
}
