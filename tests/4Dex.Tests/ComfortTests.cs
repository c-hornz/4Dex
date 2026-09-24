using System;
using FourDex.Player;
using Xunit;
using static FourDex.Tests.TestMath;

namespace FourDex.Tests
{
    /// <summary>SPEC §22: the vignette closes with motion and the turn can be stepped instead of swept.</summary>
    public class ComfortTests
    {
        private static ComfortParameters Defaults() => new ComfortParameters().Validated();

        private static VignetteModel Settled(in MotionSample motion, ComfortParameters p)
        {
            var model = new VignetteModel();
            for (int i = 0; i < 200; i++)
                model.Update(motion, p, 1f / 60f);
            return model;
        }

        [Fact]
        public void StandingStill_LeavesTheViewOpen()
        {
            VignetteModel model = Settled(new MotionSample(0f, 0f, 0f), Defaults());
            AssertApprox(1f, model.Aperture, 1e-4f);
            Assert.Equal(0f, model.Motion);
        }

        [Fact]
        public void Motion_ClosesTheViewToTheConfiguredStrength()
        {
            ComfortParameters p = Defaults();
            VignetteModel walking = Settled(new MotionSample(p.SpeedReference, 0f, 0f), p);
            AssertApprox(1f - p.VignetteStrength, walking.Aperture, 1e-3f);

            VignetteModel half = Settled(new MotionSample(0.5f * p.SpeedReference, 0f, 0f), p);
            AssertApprox(1f - 0.5f * p.VignetteStrength, half.Aperture, 1e-3f);
        }

        [Fact]
        public void TurningAndAnaKata_CloseItToo_EachOnItsOwnScale()
        {
            ComfortParameters p = Defaults();
            AssertApprox(1f - p.VignetteStrength, Settled(new MotionSample(0f, p.TurnReference, 0f), p).Aperture, 1e-3f);
            AssertApprox(1f - p.VignetteStrength, Settled(new MotionSample(0f, 0f, p.WRateReference), p).Aperture, 1e-3f);
        }

        [Fact]
        public void WalkingWhileTurning_DoesNotCloseItTwice()
        {
            // The worst kind of motion decides; motions do not add up into a closed view.
            ComfortParameters p = Defaults();
            VignetteModel both = Settled(new MotionSample(p.SpeedReference, p.TurnReference, 0f), p);
            AssertApprox(1f - p.VignetteStrength, both.Aperture, 1e-3f);
        }

        [Fact]
        public void ItEases_RatherThanSnapping()
        {
            ComfortParameters p = Defaults();
            var model = new VignetteModel();
            model.Update(new MotionSample(10f, 0f, 0f), p, 1f / 60f);
            Assert.True(model.Aperture > 0.95f, "one frame of motion must not slam it shut");
            Assert.Equal(1f, model.Motion, 3);
        }

        [Fact]
        public void Disabled_StaysOpenHoweverFastYouMove()
        {
            var p = new ComfortParameters { VignetteEnabled = false }.Validated();
            Assert.Equal(1f, Settled(new MotionSample(50f, 720f, 9f), p).Aperture);
        }

        [Fact]
        public void SmoothTurn_IsRateTimesTime()
        {
            ComfortParameters p = Defaults();
            AssertApprox(45f * MathF.PI / 180f, ComfortTurn.Step(1f, 0, 45f, p, 1f));
            AssertApprox(0f, ComfortTurn.Step(0f, 1, 45f, p, 1f), 0f);
        }

        [Fact]
        public void SnapTurn_IsOneFixedStepPerPress_AndIgnoresHolding()
        {
            var p = new ComfortParameters { TurnMode = TurnMode.Snap, SnapDegrees = 30f }.Validated();
            AssertApprox(30f * MathF.PI / 180f, ComfortTurn.Step(0f, 1, 45f, p, 1f));
            AssertApprox(-30f * MathF.PI / 180f, ComfortTurn.Step(0f, -1, 45f, p, 1f));
            Assert.Equal(0f, ComfortTurn.Step(1f, 0, 45f, p, 1f), 6);
        }
    }
}
