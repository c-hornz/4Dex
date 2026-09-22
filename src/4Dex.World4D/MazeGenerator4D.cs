using System;
using System.Collections.Generic;

namespace FourDex.World
{
    /// <summary>How a maze is carved (SPEC §19: generation works on the 4D adjacency graph).</summary>
    [Serializable]
    public sealed class MazeGenerationOptions
    {
        /// <summary>Same seed, same maze: a generated maze can be described by one number.</summary>
        public int Seed = 1;

        /// <summary>
        /// How much the carve prefers ana/kata over ordinary directions, 0..1. At 0.5 every direction is equally
        /// likely; higher makes routes leave the W slice more often, which is the point of a 4D maze.
        /// </summary>
        public float WBias = 0.5f;

        /// <summary>
        /// Extra walls knocked through after carving. 0 leaves a perfect maze (exactly one route between any two
        /// cells); more adds loops, which makes the maze easier and the W structure more obvious.
        /// </summary>
        public int ExtraOpenings;
    }

    /// <summary>
    /// Carves a maze over a 4D grid: a randomised depth-first walk of the adjacency graph, where ana and kata are
    /// simply two more of a cell's eight neighbours.
    /// </summary>
    /// <remarks>
    /// The walk visits every cell exactly once, so the result is connected by construction and, before extra
    /// openings, has exactly one route between any two cells. Walls stay consistent because <see cref="Maze4D"/>
    /// stores each shared face once.
    /// </remarks>
    public static class MazeGenerator4D
    {
        public static Maze4D Generate(int sizeX, int sizeY, int sizeZ, int sizeW, MazeGenerationOptions? options = null)
        {
            options ??= new MazeGenerationOptions();
            var maze = new Maze4D(sizeX, sizeY, sizeZ, sizeW);
            var random = new Random(options.Seed);
            var visited = new HashSet<Cell4D>();
            var stack = new Stack<Cell4D>();
            var candidates = new List<Direction4>(8);

            var start = new Cell4D(0, 0, 0, 0);
            visited.Add(start);
            stack.Push(start);

            while (stack.Count > 0)
            {
                Cell4D cell = stack.Peek();
                candidates.Clear();
                foreach (Direction4 direction in Direction4Extensions.All)
                {
                    Cell4D neighbour = cell.Neighbor(direction);
                    if (maze.Contains(neighbour) && !visited.Contains(neighbour))
                        candidates.Add(direction);
                }

                if (candidates.Count == 0)
                {
                    stack.Pop(); // dead end: back up and try the last cell with somewhere left to go
                    continue;
                }

                Direction4 chosen = Choose(candidates, options.WBias, random);
                maze.SetWall(cell, chosen, false);
                Cell4D next = cell.Neighbor(chosen);
                visited.Add(next);
                stack.Push(next);
            }

            OpenExtras(maze, options.ExtraOpenings, random);
            return maze;
        }

        /// <summary>Weighted pick: W directions carry <paramref name="wBias"/>, the others carry the remainder.</summary>
        private static Direction4 Choose(List<Direction4> candidates, float wBias, Random random)
        {
            float bias = wBias < 0f ? 0f : wBias > 1f ? 1f : wBias;
            float total = 0f;
            foreach (Direction4 direction in candidates)
                total += Weight(direction, bias);

            double roll = random.NextDouble() * total;
            foreach (Direction4 direction in candidates)
            {
                roll -= Weight(direction, bias);
                if (roll <= 0d)
                    return direction;
            }
            return candidates[candidates.Count - 1];
        }

        private static float Weight(Direction4 direction, float wBias) => direction.Axis() == 3 ? wBias : 1f - wBias;

        /// <summary>Knocks through interior walls at random, turning the perfect maze into one with loops.</summary>
        private static void OpenExtras(Maze4D maze, int count, Random random)
        {
            if (count <= 0)
                return;

            var closed = new List<(Cell4D Cell, Direction4 Direction)>();
            foreach (Cell4D cell in maze.Cells())
            {
                foreach (Direction4 direction in Direction4Extensions.All)
                {
                    // Positive faces only, so each shared face is offered once.
                    if (!direction.IsPositive() || !maze.Contains(cell.Neighbor(direction)) || !maze.HasWall(cell, direction))
                        continue;
                    closed.Add((cell, direction));
                }
            }

            for (int i = 0; i < count && closed.Count > 0; i++)
            {
                int index = random.Next(closed.Count);
                (Cell4D cell, Direction4 direction) = closed[index];
                closed.RemoveAt(index);
                maze.SetWall(cell, direction, false);
            }
        }

        /// <summary>True if every cell can be reached from every other: the property a maze must not lose.</summary>
        public static bool IsConnected(Maze4D maze)
        {
            if (maze == null)
                throw new ArgumentNullException(nameof(maze));

            var visited = new HashSet<Cell4D>();
            var queue = new Queue<Cell4D>();
            var start = new Cell4D(0, 0, 0, 0);
            visited.Add(start);
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                foreach (Cell4D neighbour in maze.OpenNeighbors(queue.Dequeue()))
                {
                    if (visited.Add(neighbour))
                        queue.Enqueue(neighbour);
                }
            }
            return visited.Count == maze.CellCount;
        }

        /// <summary>How many of the maze's open faces cross W: the measure of how four-dimensional a route is.</summary>
        public static int CountOpenings(Maze4D maze, bool throughWOnly)
        {
            int open = 0;
            foreach (Cell4D cell in maze.Cells())
            {
                foreach (Direction4 direction in Direction4Extensions.All)
                {
                    if (!direction.IsPositive() || !maze.Contains(cell.Neighbor(direction)))
                        continue;
                    if (maze.HasWall(cell, direction))
                        continue;
                    if (!throughWOnly || direction.Axis() == 3)
                        open++;
                }
            }
            return open;
        }
    }
}
