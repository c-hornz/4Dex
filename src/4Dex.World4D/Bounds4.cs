using System;
using FourDex.Math4D;

namespace FourDex.World
{
    /// <summary>An axis-aligned box in world 4-space: <c>Center ± HalfExtents</c> on each axis.</summary>
    public readonly struct Bounds4
    {
        public readonly Vec4 Center;
        public readonly Vec4 HalfExtents;

        public Bounds4(Vec4 center, Vec4 halfExtents)
        {
            if (halfExtents.X < 0f || halfExtents.Y < 0f || halfExtents.Z < 0f || halfExtents.W < 0f)
                throw new ArgumentOutOfRangeException(nameof(halfExtents), halfExtents, "Half-extents must be non-negative.");
            Center = center;
            HalfExtents = halfExtents;
        }

        public Vec4 Min => Center - HalfExtents;
        public Vec4 Max => Center + HalfExtents;

        public bool Contains(Vec4 p)
        {
            for (int i = 0; i < 4; i++)
            {
                if (MathF.Abs(p[i] - Center[i]) > HalfExtents[i])
                    return false;
            }
            return true;
        }

        /// <summary>Touching counts as overlapping.</summary>
        public bool Overlaps(Bounds4 other)
        {
            for (int i = 0; i < 4; i++)
            {
                if (MathF.Abs(Center[i] - other.Center[i]) > HalfExtents[i] + other.HalfExtents[i])
                    return false;
            }
            return true;
        }

        public override string ToString() => $"Center {Center}, HalfExtents {HalfExtents}";
    }
}
