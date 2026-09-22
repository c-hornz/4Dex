using FourDex.Math4D;
using FourDex.World;
using Xunit;
using static FourDex.Tests.TestMath;

namespace FourDex.Tests
{
    /// <summary>Milestone 8: walls stop the player, open faces let them through, in ordinary directions and in W.</summary>
    public class CellCollisionTests
    {
        private const float Size = 3f;
        private const float Radius = 0.3f;

        /// <summary>A 2x1x2x2 maze, fully walled; tests open the faces they need.</summary>
        private static Maze4D Maze() => new Maze4D(2, 1, 2, 2);

        private static Vec4 Centre(int x, int y, int z, int w) => new Cell4D(x, y, z, w).Center(Size);

        private static Vec4 Move(Maze4D maze, Vec4 from, Vec4 delta) =>
            CellCollision.Resolve(maze, Size, from, from + delta, Radius);

        [Fact]
        public void ClosedFace_StopsShortOfIt()
        {
            Maze4D maze = Maze();
            Vec4 result = Move(maze, Centre(0, 0, 0, 0), new Vec4(10f, 0f, 0f, 0f));

            // The face between cell 0 and 1 is at +1.5; the centre stops a radius short.
            AssertApprox(1.5f - Radius, result.X);
            AssertApprox(0f, result.Z);
        }

        [Fact]
        public void OpenFace_LetsThePlayerThrough()
        {
            Maze4D maze = Maze();
            maze.SetWall(new Cell4D(0, 0, 0, 0), Direction4.PosX, false);

            Vec4 result = Move(maze, Centre(0, 0, 0, 0), new Vec4(3f, 0f, 0f, 0f));

            AssertApprox(3f, result.X, 1e-4f);
            Assert.Equal(new Cell4D(1, 0, 0, 0), CellCollision.CellAt(result, Size));
        }

        [Fact]
        public void AnaKata_IsBlockedAndOpenedLikeAnyOtherDirection()
        {
            // The Milestone 8 acceptance criterion, in miniature: W is navigated exactly as X, Y and Z are.
            Maze4D maze = Maze();
            Vec4 start = Centre(0, 0, 0, 0);

            AssertApprox(1.5f - Radius, Move(maze, start, new Vec4(0f, 0f, 0f, 5f)).W);

            maze.SetWall(new Cell4D(0, 0, 0, 0), Direction4.PosW, false);
            Vec4 through = Move(maze, start, new Vec4(0f, 0f, 0f, 3f));
            AssertApprox(3f, through.W, 1e-4f);
            Assert.Equal(new Cell4D(0, 0, 0, 1), CellCollision.CellAt(through, Size));
        }

        [Fact]
        public void MovingIntoAWall_StillSlidesAlongIt()
        {
            Maze4D maze = Maze();
            maze.SetWall(new Cell4D(0, 0, 0, 0), Direction4.PosZ, false);

            // Into the +X wall and along the open +Z face at once: X is clamped, Z goes through.
            Vec4 result = Move(maze, Centre(0, 0, 0, 0), new Vec4(5f, 0f, 3f, 0f));

            AssertApprox(1.5f - Radius, result.X);
            AssertApprox(3f, result.Z, 1e-4f);
        }

        [Fact]
        public void FastMoves_CannotTunnelThroughAWall()
        {
            // Two cells open, the third face closed: a huge step must still stop at the far wall.
            Maze4D maze = new Maze4D(4, 1, 1, 1);
            maze.SetWall(new Cell4D(0, 0, 0, 0), Direction4.PosX, false);
            maze.SetWall(new Cell4D(1, 0, 0, 0), Direction4.PosX, false);

            Vec4 result = Move(maze, Centre(0, 0, 0, 0), new Vec4(1000f, 0f, 0f, 0f));

            AssertApprox(2f * Size + 1.5f - Radius, result.X, 1e-3f);
        }

        [Fact]
        public void TheOuterBoundary_AlwaysHolds()
        {
            Maze4D maze = Maze();
            Vec4 result = Move(maze, Centre(0, 0, 0, 0), new Vec4(-50f, 0f, 0f, -50f));
            AssertApprox(-1.5f + Radius, result.X);
            AssertApprox(-1.5f + Radius, result.W);
        }

        [Fact]
        public void MovementWithinACell_IsUntouched()
        {
            Maze4D maze = Maze();
            Vec4 start = Centre(0, 0, 0, 0);
            Vec4 target = start + new Vec4(0.4f, 0.2f, -0.3f, 0.1f);
            AssertApprox(target, CellCollision.Resolve(maze, Size, start, target, Radius));
        }

        [Fact]
        public void ADoorwayChain_CanBeWalked_IncludingThroughW()
        {
            // Hand-authored path: +X, then +W, then +Z. Walking it should end in the far cell.
            Maze4D maze = Maze();
            maze.SetWall(new Cell4D(0, 0, 0, 0), Direction4.PosX, false);
            maze.SetWall(new Cell4D(1, 0, 0, 0), Direction4.PosW, false);
            maze.SetWall(new Cell4D(1, 0, 0, 1), Direction4.PosZ, false);

            Vec4 p = Centre(0, 0, 0, 0);
            p = Move(maze, p, new Vec4(Size, 0f, 0f, 0f));
            p = Move(maze, p, new Vec4(0f, 0f, 0f, Size));
            p = Move(maze, p, new Vec4(0f, 0f, Size, 0f));

            Assert.Equal(new Cell4D(1, 0, 1, 1), CellCollision.CellAt(p, Size));
        }
    }
}
