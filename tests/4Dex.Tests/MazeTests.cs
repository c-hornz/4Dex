using System;
using System.Linq;
using FourDex.Math4D;
using FourDex.World;
using Xunit;
using static FourDex.Tests.TestMath;

namespace FourDex.Tests
{
    public class MazeTests
    {
        [Fact]
        public void Direction_OppositeIsAnInvolution_OnTheSameAxis()
        {
            foreach (Direction4 dir in Direction4Extensions.All)
            {
                Assert.Equal(dir, dir.Opposite().Opposite());
                Assert.Equal(dir.Axis(), dir.Opposite().Axis());
                Assert.Equal(-dir.Sign(), dir.Opposite().Sign());
                Assert.Equal(dir, Direction4Extensions.FromAxis(dir.Axis(), dir.IsPositive()));
            }
        }

        [Fact]
        public void Cell_HasEightDistinctNeighbors_AndEachStepReverses()
        {
            var c = new Cell4D(3, -1, 7, 2);
            var neighbors = Direction4Extensions.All.Select(c.Neighbor).ToList();

            Assert.Equal(8, neighbors.Distinct().Count());
            foreach (Direction4 dir in Direction4Extensions.All)
            {
                Cell4D n = c.Neighbor(dir);
                Assert.Equal(c, n.Neighbor(dir.Opposite()));
                // Exactly one coordinate moved, by one, on the direction's own axis.
                for (int axis = 0; axis < 4; axis++)
                    Assert.Equal(axis == dir.Axis() ? c[axis] + dir.Sign() : c[axis], n[axis]);
            }
        }

        [Fact]
        public void Cell_AnaAndKata_MoveOnlyW()
        {
            var c = new Cell4D(1, 2, 3, 4);
            Assert.Equal(new Cell4D(1, 2, 3, 5), c.Neighbor(Direction4.PosW));
            Assert.Equal(new Cell4D(1, 2, 3, 3), c.Neighbor(Direction4.NegW));
        }

        [Fact]
        public void Cell_ContainingItsOwnCenter_RoundTrips()
        {
            var rng = new Random(50);
            for (int trial = 0; trial < 200; trial++)
            {
                var c = new Cell4D(rng.Next(-20, 20), rng.Next(-20, 20), rng.Next(-20, 20), rng.Next(-20, 20));
                const float size = 2.5f;
                Assert.Equal(c, Cell4D.Containing(c.Center(size), size));
                // Anywhere strictly inside the cell's cube maps to the same cell.
                Vec4 jitter = RandomVec4(rng, 0.49f * size);
                Assert.Equal(c, Cell4D.Containing(c.Center(size) + jitter, size));
            }
        }

        [Fact]
        public void NewMaze_IsFullyWalled()
        {
            var maze = new Maze4D(2, 2, 2, 2);
            Assert.Equal(16, maze.CellCount);
            foreach (Cell4D c in maze.Cells())
                Assert.Empty(maze.OpenNeighbors(c));
        }

        [Fact]
        public void SharedWall_IsSeenIdenticallyFromBothSides()
        {
            // SPEC §25 "maze neighbor consistency": toggle every interior face in a random pattern, then check
            // every cell's view of every face against its neighbour's view of the same face.
            var rng = new Random(51);
            var maze = new Maze4D(3, 2, 3, 3);
            foreach (Cell4D c in maze.Cells())
            {
                foreach (Direction4 dir in Direction4Extensions.All)
                {
                    if (maze.Contains(c.Neighbor(dir)))
                        maze.SetWall(c, dir, rng.Next(2) == 0);
                }
            }

            foreach (Cell4D c in maze.Cells())
            {
                foreach (Direction4 dir in Direction4Extensions.All)
                {
                    Cell4D n = c.Neighbor(dir);
                    if (maze.Contains(n))
                        Assert.Equal(maze.HasWall(c, dir), maze.HasWall(n, dir.Opposite()));
                    else
                        Assert.True(maze.HasWall(c, dir), $"Boundary face {c} toward {dir} must be walled.");
                }
            }
        }

        [Fact]
        public void OpeningAnaFace_ConnectsBothCells_AndNothingElse()
        {
            var maze = new Maze4D(2, 2, 2, 2);
            var kata = new Cell4D(1, 0, 1, 0);
            var ana = kata.Neighbor(Direction4.PosW);

            maze.SetWall(kata, Direction4.PosW, false);

            Assert.Equal(new[] { ana }, maze.OpenNeighbors(kata));
            Assert.Equal(new[] { kata }, maze.OpenNeighbors(ana));

            // Closing it from the other side closes the same face.
            maze.SetWall(ana, Direction4.NegW, true);
            Assert.Empty(maze.OpenNeighbors(kata));
        }

        [Fact]
        public void Boundary_CannotBeOpened_ButClosingItIsHarmless()
        {
            var maze = new Maze4D(2, 1, 1, 1);
            var edge = new Cell4D(1, 0, 0, 0);

            Assert.Throws<InvalidOperationException>(() => maze.SetWall(edge, Direction4.PosX, false));
            Assert.Throws<InvalidOperationException>(() => maze.SetWall(edge, Direction4.NegW, false));
            maze.SetWall(edge, Direction4.PosX, true);
            Assert.True(maze.HasWall(edge, Direction4.PosX));
        }

        [Fact]
        public void CellsOutsideTheMaze_AreRejected()
        {
            var maze = new Maze4D(2, 2, 2, 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => maze.HasWall(new Cell4D(0, 0, 0, 2), Direction4.NegW));
            Assert.Throws<ArgumentOutOfRangeException>(() => maze.SetWall(new Cell4D(-1, 0, 0, 0), Direction4.PosX, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Maze4D(2, 2, 0, 2));
        }
    }
}
