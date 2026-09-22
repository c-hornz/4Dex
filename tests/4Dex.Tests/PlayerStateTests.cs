using System;
using FourDex.Math4D;
using FourDex.Player;
using Xunit;
using static FourDex.Tests.TestMath;

namespace FourDex.Tests
{
    /// <summary>
    /// SPEC §25's player invariants, and SPEC §7's rule that move, rotate and focus stay three independent operations.
    /// </summary>
    public class PlayerStateTests
    {
        private static Player4DState RandomPlayer(Random rng) =>
            new Player4DState(new Transform4(RandomVec4(rng), RandomRotor(rng)), RandomRange(rng, -3f, 3f));

        [Fact]
        public void MovingFocusW_NeverChangesPositionW()
        {
            var rng = new Random(70);
            for (int trial = 0; trial < 200; trial++)
            {
                Player4DState player = RandomPlayer(rng);
                Transform4 before = player.Pose;

                player.SetFocusW(RandomRange(rng, -5f, 5f));
                player.AdjustFocusW(RandomRange(rng, -1f, 1f));

                // Exact equality: focus must not even round-trip through the pose.
                Assert.Equal(before.Position, player.Position);
                Assert.True(before.Rotation.ApproxEquals(player.Orientation, 0f));
            }
        }

        [Fact]
        public void MovingPositionW_NeverChangesFocusW()
        {
            var rng = new Random(71);
            for (int trial = 0; trial < 200; trial++)
            {
                Player4DState player = RandomPlayer(rng);
                float focus = player.FocusW;

                player.MoveAnaKata(RandomRange(rng, -5f, 5f));
                player.MoveWorld(RandomVec4(rng));
                player.MoveLocal(RandomVec4(rng));

                Assert.Equal(focus, player.FocusW);
            }
        }

        [Fact]
        public void Rotating_ChangesNeitherPositionNorFocus()
        {
            var rng = new Random(72);
            Player4DState player = RandomPlayer(rng);
            Vec4 position = player.Position;
            float focus = player.FocusW;

            foreach (Plane4 plane in Plane4Extensions.All)
                player.RotateLocal(plane, RandomRange(rng, -MathF.PI, MathF.PI));
            player.ResetOrientation();

            Assert.Equal(position, player.Position);
            Assert.Equal(focus, player.FocusW);
            Assert.True(player.Orientation.ApproxEquals(Rotor4.Identity, 0f));
        }

        [Fact]
        public void MoveAnaKata_ChangesOnlyWorldW()
        {
            var player = new Player4DState(new Transform4(new Vec4(1f, 2f, 3f, 4f), Rotor4.FromPlane(Plane4.XW, 0.7f)));
            player.MoveAnaKata(-1.5f);
            AssertApprox(new Vec4(1f, 2f, 3f, 2.5f), player.Position);
        }

        [Fact]
        public void AfterQuarterTurnInXW_WalkingLocalX_MovesAna()
        {
            // Rotating toward W (§7B) does not move the player, but it does change where "forward" points,
            // so walking afterwards (§7A) can carry the player through W.
            var player = new Player4DState();
            player.RotateLocal(Plane4.XW, MathF.PI / 2f);
            AssertApprox(Vec4.Zero, player.Position);

            player.MoveLocal(new Vec4(2f, 0f, 0f, 0f));
            AssertApprox(new Vec4(0f, 0f, 0f, 2f), player.Position, 1e-5f);
        }

        [Fact]
        public void NonFiniteInput_IsRejected_AndLeavesStateUntouched()
        {
            var player = new Player4DState(Transform4.Identity, 0.5f);
            Assert.Throws<ArgumentOutOfRangeException>(() => player.SetFocusW(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => player.MoveAnaKata(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => player.RotateLocal(Plane4.YW, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Player4DState(Transform4.Identity, float.NegativeInfinity));

            Assert.Equal(0.5f, player.FocusW);
            Assert.Equal(Vec4.Zero, player.Position);
        }
    }
}
