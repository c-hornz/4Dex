using System;
using System.Globalization;

namespace FourDex.Math4D
{
    /// <summary>
    /// Rotation angles in radians for all six planes, taken <b>simultaneously</b>.
    /// Signs follow <see cref="Plane4"/>: <c>+angle</c> turns the plane's first axis toward its second.
    /// </summary>
    /// <remarks>
    /// Plane rotations do not commute, so applying six rotations one after another depends on an
    /// arbitrary order. <see cref="Rotor4.FromBivector"/> exponentiates the whole bivector at once,
    /// which is order-free. That matters for weighted input mappings (SPEC §6), where one head
    /// motion contributes to several planes in the same frame.
    /// </remarks>
    public readonly struct Bivector4
    {
        public readonly float XY;
        public readonly float XZ;
        public readonly float YZ;
        public readonly float XW;
        public readonly float YW;
        public readonly float ZW;

        public Bivector4(float xy, float xz, float yz, float xw, float yw, float zw)
        {
            XY = xy;
            XZ = xz;
            YZ = yz;
            XW = xw;
            YW = yw;
            ZW = zw;
        }

        public static readonly Bivector4 Zero = new Bivector4(0f, 0f, 0f, 0f, 0f, 0f);

        public static Bivector4 FromPlane(Plane4 plane, float angle) => plane switch
        {
            Plane4.XY => new Bivector4(angle, 0f, 0f, 0f, 0f, 0f),
            Plane4.XZ => new Bivector4(0f, angle, 0f, 0f, 0f, 0f),
            Plane4.YZ => new Bivector4(0f, 0f, angle, 0f, 0f, 0f),
            Plane4.XW => new Bivector4(0f, 0f, 0f, angle, 0f, 0f),
            Plane4.YW => new Bivector4(0f, 0f, 0f, 0f, angle, 0f),
            Plane4.ZW => new Bivector4(0f, 0f, 0f, 0f, 0f, angle),
            _ => throw new ArgumentOutOfRangeException(nameof(plane), plane, null),
        };

        public float this[Plane4 plane] => plane switch
        {
            Plane4.XY => XY,
            Plane4.XZ => XZ,
            Plane4.YZ => YZ,
            Plane4.XW => XW,
            Plane4.YW => YW,
            Plane4.ZW => ZW,
            _ => throw new ArgumentOutOfRangeException(nameof(plane), plane, null),
        };

        public static Bivector4 operator +(Bivector4 a, Bivector4 b) => new Bivector4(
            a.XY + b.XY, a.XZ + b.XZ, a.YZ + b.YZ, a.XW + b.XW, a.YW + b.YW, a.ZW + b.ZW);

        public static Bivector4 operator *(Bivector4 b, float s) => new Bivector4(
            b.XY * s, b.XZ * s, b.YZ * s, b.XW * s, b.YW * s, b.ZW * s);

        public static Bivector4 operator *(float s, Bivector4 b) => b * s;

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "XY {0:F4}, XZ {1:F4}, YZ {2:F4}, XW {3:F4}, YW {4:F4}, ZW {5:F4}", XY, XZ, YZ, XW, YW, ZW);
    }
}
