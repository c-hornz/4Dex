using System.Collections.Generic;
using FourDex.World;
using Xunit;

namespace FourDex.Tests
{
    /// <summary>Milestone 9: a small procedural 4D maze, carved over the 4D adjacency graph (SPEC §19).</summary>
    public class MazeGeneratorTests
    {
        private static Maze4D Generate(int seed = 1, float wBias = 0.5f, int extra = 0) =>
            MazeGenerator4D.Generate(3, 1, 3, 2, new MazeGenerationOptions { Seed = seed, WBias = wBias, ExtraOpenings = extra });

        [Fact]
        public void EveryCellIsReachable()
        {
            // The invariant that makes a generated maze usable at all: no cell walled off from the rest.
            for (int seed = 1; seed <= 25; seed++)
                Assert.True(MazeGenerator4D.IsConnected(Generate(seed)), $"seed {seed}");
        }

        [Fact]
        public void EveryCellIsReachable_InAwkwardShapes()
        {
            foreach ((int x, int y, int z, int w) in new[] { (1, 1, 1, 1), (1, 1, 1, 6), (5, 1, 1, 1), (2, 2, 2, 2) })
            {
                Maze4D maze = MazeGenerator4D.Generate(x, y, z, w, new MazeGenerationOptions { Seed = 7 });
                Assert.True(MazeGenerator4D.IsConnected(maze), $"{x}x{y}x{z}x{w}");
            }
        }

        [Fact]
        public void APerfectMazeHasExactlyOneRouteBetweenAnyTwoCells()
        {
            // A spanning tree over N cells has N-1 edges. More would mean loops; fewer would mean a cell cut off.
            Maze4D maze = Generate();
            Assert.Equal(maze.CellCount - 1, MazeGenerator4D.CountOpenings(maze, throughWOnly: false));
        }

        [Fact]
        public void TheRouteUsesAnaAndKata()
        {
            // A 4D maze that never leaves the W slice would be a 3D maze. With two W levels, connecting them takes
            // at least one W opening, and in practice the carve uses several.
            for (int seed = 1; seed <= 10; seed++)
                Assert.True(MazeGenerator4D.CountOpenings(Generate(seed), throughWOnly: true) > 0, $"seed {seed}");
        }

        [Fact]
        public void WBias_MovesTheRouteIntoW()
        {
            int low = 0, high = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                low += MazeGenerator4D.CountOpenings(Generate(seed, wBias: 0.1f), throughWOnly: true);
                high += MazeGenerator4D.CountOpenings(Generate(seed, wBias: 0.9f), throughWOnly: true);
            }
            Assert.True(high > low, $"expected a W-biased carve to cross W more often ({high} vs {low})");
        }

        [Fact]
        public void SameSeed_SameMaze_AndDifferentSeedsDiffer()
        {
            Assert.Equal(Fingerprint(Generate(42)), Fingerprint(Generate(42)));
            Assert.NotEqual(Fingerprint(Generate(42)), Fingerprint(Generate(43)));
        }

        [Fact]
        public void ExtraOpenings_AddLoops_AndKeepItConnected()
        {
            Maze4D perfect = Generate(extra: 0);
            Maze4D braided = Generate(extra: 5);

            Assert.Equal(MazeGenerator4D.CountOpenings(perfect, false) + 5, MazeGenerator4D.CountOpenings(braided, false));
            Assert.True(MazeGenerator4D.IsConnected(braided));
        }

        [Fact]
        public void TheOuterBoundaryStaysWalled()
        {
            Maze4D maze = Generate(extra: 50);
            foreach (Cell4D cell in maze.Cells())
            {
                foreach (Direction4 direction in Direction4Extensions.All)
                {
                    if (!maze.Contains(cell.Neighbor(direction)))
                        Assert.True(maze.HasWall(cell, direction), $"{cell} {direction} is the edge of the world");
                }
            }
        }

        private static string Fingerprint(Maze4D maze)
        {
            var sb = new System.Text.StringBuilder();
            foreach (Cell4D cell in maze.Cells())
            {
                foreach (Direction4 direction in Direction4Extensions.All)
                    sb.Append(maze.HasWall(cell, direction) ? '#' : '.');
            }
            return sb.ToString();
        }
    }
}
