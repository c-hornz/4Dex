using System;
using FourDex.Math4D;

namespace FourDex.World
{
    /// <summary>
    /// A rigid 4D body: a transform and a box of half-extents along its own local axes.
    /// </summary>
    /// <remarks>
    /// <c>HalfExtents.W</c> is the object's W thickness, a parameter rather than true hypersurface
    /// geometry (ARCHITECTURE §9, risk 1). Created only through <see cref="World4D.Add"/> so every object
    /// has a world-unique id.
    /// </remarks>
    public sealed class Object4D
    {
        private Vec4 _halfExtents;

        internal Object4D(int id, string name, Transform4 transform, Vec4 halfExtents)
        {
            Id = id;
            Name = name;
            Transform = transform;
            HalfExtents = halfExtents;
        }

        public int Id { get; }

        public string Name { get; }

        public Transform4 Transform { get; set; }

        public Vec4 HalfExtents
        {
            get => _halfExtents;
            set
            {
                if (value.X < 0f || value.Y < 0f || value.Z < 0f || value.W < 0f)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Half-extents must be non-negative.");
                _halfExtents = value;
            }
        }

        /// <summary>
        /// Tightest world-axis-aligned box around the rotated local box. A rotation through W trades
        /// X/Y/Z extent for W extent, so an object turned in XW gains W thickness.
        /// </summary>
        public Bounds4 WorldBounds()
        {
            // World half-extent on axis i = sum over local axes j of |component i of basis vector j| * h_j.
            Basis4 b = Transform.Basis;
            Vec4 h = _halfExtents;
            var extent = new float[4];
            for (int i = 0; i < 4; i++)
            {
                extent[i] =
                    MathF.Abs(b.X[i]) * h.X +
                    MathF.Abs(b.Y[i]) * h.Y +
                    MathF.Abs(b.Z[i]) * h.Z +
                    MathF.Abs(b.W[i]) * h.W;
            }
            return new Bounds4(Transform.Position, new Vec4(extent[0], extent[1], extent[2], extent[3]));
        }

        public override string ToString() => $"#{Id} {Name}: {Transform}";
    }
}
