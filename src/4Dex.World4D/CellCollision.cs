using System;
using FourDex.Math4D;

namespace FourDex.World
{
    /// <summary>
    /// Moves a point through a <see cref="Maze4D"/>, stopped by closed faces (SPEC §19, Milestone 8). The player is a
    /// sphere of <c>radius</c> in all four axes, so a wall blocks in W exactly as it blocks in X, Y or Z.
    /// </summary>
    /// <remarks>
    /// Resolved one axis at a time, so a move into a wall keeps the components that are free: you slide along a wall
    /// instead of stopping dead, and a diagonal move into a corner still gets you round it.
    /// </remarks>
    public static class CellCollision
    {
        /// <summary>
        /// Where a move from <paramref name="from"/> toward <paramref name="to"/> actually ends up.
        /// </summary>
        /// <param name="cellSize">Edge length of a cell. Cell (0,0,0,0) is centred on the origin.</param>
        /// <param name="radius">How close the player's centre may come to a closed face.</param>
        public static Vec4 Resolve(Maze4D maze, float cellSize, Vec4 from, Vec4 to, float radius)
        {
            if (maze == null)
                throw new ArgumentNullException(nameof(maze));
            if (cellSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Cell size must be positive.");

            float clearance = MathF.Min(MathF.Max(radius, 0f), 0.49f * cellSize);
            var result = new float[4];
            for (int axis = 0; axis < 4; axis++)
                result[axis] = from[axis];

            // Each axis in turn, from the position already resolved on the earlier axes.
            for (int axis = 0; axis < 4; axis++)
            {
                var current = new Vec4(result[0], result[1], result[2], result[3]);
                result[axis] = ResolveAxis(maze, cellSize, clearance, current, axis, to[axis]);
            }

            return new Vec4(result[0], result[1], result[2], result[3]);
        }

        /// <summary>The cell a point is in, for a maze whose cell (0,0,0,0) is centred on the origin.</summary>
        public static Cell4D CellAt(Vec4 point, float cellSize) => Cell4D.Containing(point, cellSize);

        private static float ResolveAxis(Maze4D maze, float cellSize, float clearance, Vec4 from, int axis, float target)
        {
            float start = from[axis];
            if (target == start)
                return start;

            bool positive = target > start;
            var direction = Direction4Extensions.FromAxis(axis, positive);
            Cell4D cell = CellAt(from, cellSize);

            // Walk cell by cell toward the target. Each step asks one question: may I leave this cell that way?
            // Checking every face in between is what stops a fast move from tunnelling through a wall.
            for (int step = 0; step < 512; step++)
            {
                // Face of the current cell in the direction of travel, and how close the centre may come to it.
                float face = (cell[axis] + (positive ? 0.5f : -0.5f)) * cellSize;
                float limit = positive ? face - clearance : face + clearance;

                bool reachesFace = positive ? target > limit : target < limit;
                if (!reachesFace)
                    return target;

                if (!maze.Contains(cell) || maze.HasWall(cell, direction))
                    return limit;

                cell = cell.Neighbor(direction);
            }

            return target;
        }
    }
}
