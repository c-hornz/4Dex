using System;
using FourDex.Math4D;
using FourDex.Player;
using Xunit;
using static FourDex.Tests.TestMath;

namespace FourDex.Tests
{
    /// <summary>
    /// Navigation aids: ana/kata that keeps its meaning after turning through W, stepped turns, align-to-axes and
    /// settling into W slices.
    /// </summary>
    public class NavigationTests
    {
        private const float Quarter = MathF.PI / 2f;

        private static NavigationParameters Instant() => new NavigationParameters { TurnSeconds = 0f, SettleSeconds = 0f };

        // ---- ana/kata frame ----

        [Fact]
        public void ViewAnaKata_AfterHalfTurnThroughW_StillMovesTowardWhatIsShownAsAna()
        {
            var player = new Player4DState();
            player.RotateLocal(Plane4.XW, MathF.PI);

            player.MoveAnaKata(1f, WMoveFrame.View);

            // The viewer's W now points along world -W, so moving "ana" as shown lowers world W.
            AssertApprox(new Vec4(0f, 0f, 0f, -1f), player.Position, 1e-4f);
            // And in the viewer's own frame that is exactly +1 along W: toward what perception calls ana.
            AssertApprox(new Vec4(0f, 0f, 0f, 1f), player.Pose.InverseTransformDirection(player.Position), 1e-4f);
        }

        [Fact]
        public void WorldAnaKata_AfterHalfTurnThroughW_IsReversedFromTheView()
        {
            var player = new Player4DState();
            player.RotateLocal(Plane4.XW, MathF.PI);

            player.MoveAnaKata(1f, WMoveFrame.World);

            AssertApprox(new Vec4(0f, 0f, 0f, 1f), player.Position, 1e-4f);
            Assert.True(player.Pose.InverseTransformDirection(player.Position).W < -0.99f);
        }

        [Fact]
        public void ViewAnaKata_LeavesOrientationAndFocusAlone()
        {
            var rng = new Random(91);
            for (int trial = 0; trial < 100; trial++)
            {
                var player = new Player4DState(new Transform4(RandomVec4(rng), RandomRotor(rng)), RandomRange(rng, -2f, 2f));
                Rotor4 orientation = player.Orientation;
                float focus = player.FocusW;

                player.MoveViewAnaKata(RandomRange(rng, -3f, 3f));

                Assert.Equal(orientation, player.Orientation);
                Assert.Equal(focus, player.FocusW);
            }
        }

        // ---- axis alignment ----

        [Fact]
        public void AxisAlignedOrientations_AreTheTesseractsRotations()
        {
            Assert.Equal(192, AxisAlignment.All.Count);
            foreach (Rotor4 r in AxisAlignment.All)
            {
                Basis4 b = r.ToBasis();
                for (int axis = 0; axis < 4; axis++)
                {
                    Vec4 v = b[axis];
                    // Each axis lands on exactly one world axis, signed.
                    int ones = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        float a = MathF.Abs(v[i]);
                        Assert.True(a < 1e-4f || MathF.Abs(a - 1f) < 1e-4f, $"{r}: axis {axis} = {v}");
                        if (a > 0.5f) ones++;
                    }
                    Assert.Equal(1, ones);
                }
            }
        }

        [Fact]
        public void Nearest_OfAnAlignedOrientationNudgedOffAxis_IsThatOrientation()
        {
            var rng = new Random(92);
            foreach (Rotor4 aligned in AxisAlignment.All)
            {
                var nudge = new Bivector4(
                    RandomRange(rng, -0.2f, 0.2f), RandomRange(rng, -0.2f, 0.2f), RandomRange(rng, -0.2f, 0.2f),
                    RandomRange(rng, -0.2f, 0.2f), RandomRange(rng, -0.2f, 0.2f), RandomRange(rng, -0.2f, 0.2f));
                Rotor4 off = Rotor4.FromBivector(nudge).Then(aligned);

                Assert.True(AxisAlignment.Nearest(off).ApproxEquals(aligned, 1e-4f));
            }
        }

        [Fact]
        public void Describe_NamesWhereEachViewAxisPoints()
        {
            Assert.Equal("right +X  up +Y  fwd +Z  ana +W", AxisAlignment.Describe(Rotor4.Identity));
            // ZW +90: forward turns toward ana, so the player now faces world +W and their ana is world -Z.
            Assert.Equal("right +X  up +Y  fwd +W  ana -Z", AxisAlignment.Describe(Rotor4.FromPlane(Plane4.ZW, Quarter)));
        }

        // ---- stepped turns and align ----

        [Fact]
        public void StepTurns_PlayOutOverTime_AndArriveExactlyOnAxis()
        {
            var player = new Player4DState();
            var assist = new NavigationAssist();
            var p = new NavigationParameters { TurnSeconds = 0.3f };

            assist.StepTurn(player, Plane4.XW, +1, p);
            Assert.True(assist.IsTurning);
            Assert.True(player.Orientation.ApproxEquals(Rotor4.Identity)); // nothing until time passes

            // Uneven frames: the fractions must still compose to exactly one quarter turn.
            foreach (float dt in new[] { 0.016f, 0.05f, 0.011f, 0.1f, 0.2f })
                assist.Update(player, Vec4.Zero, 0f, p, dt);

            Assert.False(assist.IsTurning);
            Assert.True(player.Orientation.ApproxEquals(Rotor4.FromPlane(Plane4.XW, Quarter), 1e-5f));
            Assert.Equal(Vec4.Zero, player.Position);
        }

        [Fact]
        public void APressDuringATurn_FinishesItFirst_SoStepsNeverLandBetweenAxes()
        {
            var player = new Player4DState();
            var assist = new NavigationAssist();
            var p = new NavigationParameters { TurnSeconds = 0.3f };

            assist.StepTurn(player, Plane4.XW, +1, p);
            assist.Update(player, Vec4.Zero, 0f, p, 0.1f);
            assist.StepTurn(player, Plane4.YW, +1, p);
            assist.Update(player, Vec4.Zero, 0f, p, 1f);

            // The same two quarter turns applied whole: XW, then YW in the already-turned frame.
            var reference = new Player4DState();
            reference.RotateLocal(Plane4.XW, Quarter);
            reference.RotateLocal(Plane4.YW, Quarter);
            Assert.True(player.Orientation.ApproxEquals(reference.Orientation, 1e-5f));
            Assert.Contains(AxisAlignment.All, r => r.ApproxEquals(player.Orientation, 1e-4f));
        }

        [Fact]
        public void Align_FromAnyOrientation_EndsAxisAligned_WithPositionAndFocusUnchanged()
        {
            var rng = new Random(93);
            var p = Instant();
            for (int trial = 0; trial < 100; trial++)
            {
                var player = new Player4DState(new Transform4(RandomVec4(rng), RandomRotor(rng)), RandomRange(rng, -2f, 2f));
                Vec4 position = player.Position;
                float focus = player.FocusW;
                Rotor4 nearest = AxisAlignment.Nearest(player.Orientation);

                new NavigationAssist().Align(player, p);

                Assert.True(player.Orientation.ApproxEquals(nearest, 1e-4f), $"trial {trial}");
                Assert.Equal(position, player.Position);
                Assert.Equal(focus, player.FocusW);
            }
        }

        [Fact]
        public void Cancel_StopsATurnWhereItIs()
        {
            var player = new Player4DState();
            var assist = new NavigationAssist();
            var p = new NavigationParameters { TurnSeconds = 1f };

            assist.StepTurn(player, Plane4.ZW, -1, p);
            assist.Update(player, Vec4.Zero, 0f, p, 0.5f);
            assist.Cancel();
            Rotor4 halfway = player.Orientation;
            assist.Update(player, Vec4.Zero, 0f, p, 1f);

            Assert.False(assist.IsTurning);
            Assert.Equal(halfway, player.Orientation);
            Assert.True(player.Orientation.ApproxEquals(Rotor4.FromPlane(Plane4.ZW, -Quarter / 2f), 1e-5f));
        }

        // ---- settling into slices ----

        [Fact]
        public void AfterAnaKataStops_PositionSettlesToTheNearestWSlice()
        {
            var player = new Player4DState(new Transform4(new Vec4(0.3f, 0f, 0.4f, 0f), Rotor4.Identity));
            var assist = new NavigationAssist();
            var p = new NavigationParameters { SliceSpacing = 1f, SettleSeconds = 0.1f };

            player.MoveAnaKata(0.7f, WMoveFrame.View);
            assist.Update(player, Vec4.UnitW, 0f, p, 0.016f);
            for (int i = 0; i < 120; i++)
                assist.Update(player, Vec4.Zero, 0f, p, 0.016f);

            Assert.False(assist.IsSettling);
            AssertApprox(1f, player.Position.W, 1e-5f);
            // Only W settles in an open scene: x and z are left exactly where the player put them.
            Assert.Equal(0.3f, player.Position.X);
            Assert.Equal(0.4f, player.Position.Z);
        }

        [Fact]
        public void InAMaze_TheSettleFollowsTheAxisTheMoveWentAlong()
        {
            // Turned XW +90: the viewer's ana is world -X, so ana/kata in the view frame travels along world X.
            var player = new Player4DState(new Transform4(Vec4.Zero, Rotor4.FromPlane(Plane4.XW, Quarter)));
            var assist = new NavigationAssist();
            var p = Instant();

            player.MoveAnaKata(2.2f, WMoveFrame.View);
            Vec4 direction = player.Orientation.Apply(Vec4.UnitW);
            assist.Update(player, direction, 3f, p, 0.016f);
            assist.Update(player, Vec4.Zero, 3f, p, 0.016f);

            AssertApprox(new Vec4(-3f, 0f, 0f, 0f), player.Position, 1e-4f);
        }

        [Fact]
        public void WithSettlingOff_NothingMoves()
        {
            var player = new Player4DState(new Transform4(new Vec4(0f, 0f, 0f, 0.4f), Rotor4.Identity));
            var assist = new NavigationAssist();
            var p = new NavigationParameters { SettleToSlices = false };

            assist.Update(player, Vec4.UnitW, 0f, p, 0.016f);
            assist.Update(player, Vec4.Zero, 0f, p, 1f);

            Assert.Equal(0.4f, player.Position.W);
        }

        [Fact]
        public void Settling_GoesThroughTheMovementFilter()
        {
            var player = new Player4DState(new Transform4(new Vec4(0f, 0f, 0f, 0.4f), Rotor4.Identity));
            player.MovementFilter = (from, to) => from; // a wall in every direction
            var assist = new NavigationAssist();
            var p = Instant();

            assist.Update(player, Vec4.UnitW, 0f, p, 0.016f);
            assist.Update(player, Vec4.Zero, 0f, p, 0.016f);

            Assert.Equal(0.4f, player.Position.W);
            Assert.False(assist.IsSettling); // blocked, so it gives up rather than pushing every frame
        }
    }
}
