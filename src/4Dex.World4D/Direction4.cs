using System;
using System.Collections.Generic;

namespace FourDex.World
{
    /// <summary>
    /// The eight cardinal directions of a 4D grid cell (SPEC §1). <c>PosW</c> is ana, <c>NegW</c> is kata.
    /// </summary>
    /// <remarks>Ordered so that <c>(int)dir / 2</c> is the axis and <c>(int)dir % 2 == 0</c> means positive.</remarks>
    public enum Direction4
    {
        PosX,
        NegX,
        PosY,
        NegY,
        PosZ,
        NegZ,
        PosW,
        NegW,
    }

    public static class Direction4Extensions
    {
        public static IReadOnlyList<Direction4> All { get; } = new[]
        {
            Direction4.PosX, Direction4.NegX,
            Direction4.PosY, Direction4.NegY,
            Direction4.PosZ, Direction4.NegZ,
            Direction4.PosW, Direction4.NegW,
        };

        /// <summary>Axis index 0..3 = X, Y, Z, W.</summary>
        public static int Axis(this Direction4 dir) => (int)Checked(dir) / 2;

        public static bool IsPositive(this Direction4 dir) => (int)Checked(dir) % 2 == 0;

        /// <summary>+1 or -1 along <see cref="Axis"/>.</summary>
        public static int Sign(this Direction4 dir) => dir.IsPositive() ? 1 : -1;

        public static Direction4 Opposite(this Direction4 dir) => (Direction4)((int)Checked(dir) ^ 1);

        public static Direction4 FromAxis(int axis, bool positive)
        {
            if (axis < 0 || axis > 3)
                throw new ArgumentOutOfRangeException(nameof(axis), axis, "Axis must be 0..3 (X, Y, Z, W).");
            return (Direction4)(axis * 2 + (positive ? 0 : 1));
        }

        private static Direction4 Checked(Direction4 dir)
        {
            if (dir < Direction4.PosX || dir > Direction4.NegW)
                throw new ArgumentOutOfRangeException(nameof(dir), dir, null);
            return dir;
        }
    }
}
