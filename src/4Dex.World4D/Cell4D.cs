using System;
using FourDex.Math4D;

namespace FourDex.World
{
    /// <summary>
    /// Integer coordinates of one 4D grid cell. Holds no walls: those belong to <see cref="Maze4D"/>,
    /// which stores each shared face exactly once (SPEC §19).
    /// </summary>
    public readonly struct Cell4D : IEquatable<Cell4D>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;
        public readonly int W;

        public Cell4D(int x, int y, int z, int w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        /// <summary>Component by axis index 0..3 = X, Y, Z, W.</summary>
        public int this[int axis] => axis switch
        {
            0 => X,
            1 => Y,
            2 => Z,
            3 => W,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Axis must be 0..3 (X, Y, Z, W)."),
        };

        public Cell4D Neighbor(Direction4 dir)
        {
            int s = dir.Sign();
            return dir.Axis() switch
            {
                0 => new Cell4D(X + s, Y, Z, W),
                1 => new Cell4D(X, Y + s, Z, W),
                2 => new Cell4D(X, Y, Z + s, W),
                _ => new Cell4D(X, Y, Z, W + s),
            };
        }

        /// <summary>World-space centre, for cells of edge <paramref name="cellSize"/> with cell (0,0,0,0) centred on the origin.</summary>
        public Vec4 Center(float cellSize) => new Vec4(X, Y, Z, W) * cellSize;

        /// <summary>The cell whose centred cube of edge <paramref name="cellSize"/> contains <paramref name="point"/>.</summary>
        public static Cell4D Containing(Vec4 point, float cellSize) => new Cell4D(
            (int)MathF.Round(point.X / cellSize, MidpointRounding.AwayFromZero),
            (int)MathF.Round(point.Y / cellSize, MidpointRounding.AwayFromZero),
            (int)MathF.Round(point.Z / cellSize, MidpointRounding.AwayFromZero),
            (int)MathF.Round(point.W / cellSize, MidpointRounding.AwayFromZero));

        public bool Equals(Cell4D other) => X == other.X && Y == other.Y && Z == other.Z && W == other.W;
        public override bool Equals(object? obj) => obj is Cell4D other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
        public static bool operator ==(Cell4D a, Cell4D b) => a.Equals(b);
        public static bool operator !=(Cell4D a, Cell4D b) => !a.Equals(b);

        public override string ToString() => $"[{X}, {Y}, {Z}, {W}]";
    }
}
