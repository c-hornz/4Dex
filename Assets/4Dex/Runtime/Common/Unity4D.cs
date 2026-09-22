using System;
using FourDex.Math4D;
using UnityEngine;

namespace FourDex.Runtime
{
    /// <summary>
    /// The one place Math4D types meet Unity types. 4D (x, y, z) maps component-for-component onto Unity
    /// (x, y, z): y is up, z is forward. Only the 3D part of a Vec4 ever becomes a Unity position.
    /// </summary>
    /// <remarks>
    /// Unity is left-handed, so a Math4D XZ rotation by +t (X toward Z) is a Unity yaw of -t. Converting
    /// through basis vectors rather than angles, as <c>ProjectionDriver</c> does, makes that sign irrelevant.
    /// </remarks>
    public static class Unity4D
    {
        public static Vec4 ToVec4(this Vector4 v) => new Vec4(v.x, v.y, v.z, v.w);

        public static Vector4 ToVector4(this Vec4 v) => new Vector4(v.X, v.Y, v.Z, v.W);

        public static Vector3 Xyz(this Vec4 v) => new Vector3(v.X, v.Y, v.Z);
    }

    /// <summary>
    /// Inspector-friendly 4D orientation: an angle in degrees for each of the six planes.
    /// </summary>
    /// <remarks>
    /// Read as one bivector and exponentiated, so the six angles act simultaneously rather than in an
    /// order. For a single nonzero plane that is exactly a rotation by that angle in that plane.
    /// </remarks>
    [Serializable]
    public struct PlaneAngles
    {
        public float XY;
        public float XZ;
        public float YZ;
        public float XW;
        public float YW;
        public float ZW;

        public Rotor4 ToRotor()
        {
            const float toRadians = Mathf.Deg2Rad;
            return Rotor4.FromBivector(new Bivector4(
                XY * toRadians, XZ * toRadians, YZ * toRadians,
                XW * toRadians, YW * toRadians, ZW * toRadians));
        }
    }
}
