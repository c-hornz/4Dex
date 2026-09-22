using System;
using System.Collections.Generic;

namespace FourDex.World
{
    /// <summary>
    /// A bounded 4D grid of cells and the walls between them. Cells span <c>[0, Size)</c> on each axis.
    /// </summary>
    /// <remarks>
    /// Each cell stores only its four positive-direction faces; a cell's <c>NegX</c> wall <i>is</i> its
    /// -X neighbour's <c>PosX</c> wall. Two neighbours therefore cannot disagree about the face they share,
    /// because there is only one bit for it (SPEC §19). The outer boundary is always walled.
    /// Starts fully walled; carving passages is the generator's or level author's job.
    /// </remarks>
    public sealed class Maze4D
    {
        private readonly bool[] _open; // [cellIndex * 4 + axis]: is the positive face on that axis open?

        public int SizeX { get; }
        public int SizeY { get; }
        public int SizeZ { get; }
        public int SizeW { get; }

        public Maze4D(int sizeX, int sizeY, int sizeZ, int sizeW)
        {
            if (sizeX < 1 || sizeY < 1 || sizeZ < 1 || sizeW < 1)
                throw new ArgumentOutOfRangeException(nameof(sizeX), "Every maze dimension must be at least 1.");
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            SizeW = sizeW;
            _open = new bool[CellCount * 4];
        }

        public int CellCount => SizeX * SizeY * SizeZ * SizeW;

        public bool Contains(Cell4D c) =>
            c.X >= 0 && c.X < SizeX &&
            c.Y >= 0 && c.Y < SizeY &&
            c.Z >= 0 && c.Z < SizeZ &&
            c.W >= 0 && c.W < SizeW;

        public IEnumerable<Cell4D> Cells()
        {
            for (int w = 0; w < SizeW; w++)
            for (int z = 0; z < SizeZ; z++)
            for (int y = 0; y < SizeY; y++)
            for (int x = 0; x < SizeX; x++)
                yield return new Cell4D(x, y, z, w);
        }

        /// <summary>True if movement from <paramref name="cell"/> toward <paramref name="dir"/> is blocked.</summary>
        public bool HasWall(Cell4D cell, Direction4 dir)
        {
            RequireInside(cell);
            return !TryFaceIndex(cell, dir, out int face) || !_open[face];
        }

        /// <summary>
        /// Open or close the face between <paramref name="cell"/> and its neighbour in <paramref name="dir"/>.
        /// Both cells see the change, since they share one face.
        /// </summary>
        public void SetWall(Cell4D cell, Direction4 dir, bool wall)
        {
            RequireInside(cell);
            if (!TryFaceIndex(cell, dir, out int face))
            {
                if (!wall)
                    throw new InvalidOperationException($"Cannot open the maze boundary: {cell} toward {dir}.");
                return;
            }
            _open[face] = !wall;
        }

        /// <summary>Neighbouring cells reachable from <paramref name="cell"/> in one step.</summary>
        public IEnumerable<Cell4D> OpenNeighbors(Cell4D cell)
        {
            foreach (Direction4 dir in Direction4Extensions.All)
            {
                if (!HasWall(cell, dir))
                    yield return cell.Neighbor(dir);
            }
        }

        // Canonicalise to the face owned by whichever of the two cells sits on the negative side.
        private bool TryFaceIndex(Cell4D cell, Direction4 dir, out int face)
        {
            Cell4D owner = dir.IsPositive() ? cell : cell.Neighbor(dir);
            Cell4D other = dir.IsPositive() ? cell.Neighbor(dir) : cell;
            if (!Contains(owner) || !Contains(other))
            {
                face = -1;
                return false;
            }
            face = Index(owner) * 4 + dir.Axis();
            return true;
        }

        private int Index(Cell4D c) => ((c.W * SizeZ + c.Z) * SizeY + c.Y) * SizeX + c.X;

        private void RequireInside(Cell4D cell)
        {
            if (!Contains(cell))
                throw new ArgumentOutOfRangeException(nameof(cell), cell, $"Cell is outside the {SizeX}x{SizeY}x{SizeZ}x{SizeW} maze.");
        }
    }
}
