using System.Collections.Generic;
using FourDex.Math4D;

namespace FourDex.World
{
    /// <summary>
    /// Milestone 0 stub: conservative broad-phase queries on world-axis-aligned bounds only. A rotated
    /// object's bounds over-approximate its box, so these can report contact an exact test would reject.
    /// Exact oriented-box tests and maze-wall collision arrive with the cells milestone (SPEC §24, M8).
    /// </summary>
    public static class Collision4D
    {
        public static bool MayOverlap(Object4D a, Object4D b) => a.WorldBounds().Overlaps(b.WorldBounds());

        /// <summary>Objects whose world bounds contain <paramref name="point"/>.</summary>
        public static IEnumerable<Object4D> ObjectsAt(World4D world, Vec4 point)
        {
            foreach (Object4D obj in world.Objects)
            {
                if (obj.WorldBounds().Contains(point))
                    yield return obj;
            }
        }
    }
}
