using FourDex.Perception;
using Xunit;
using static FourDex.Tests.TestMath;

namespace FourDex.Tests
{
    /// <summary>SPEC §10 survey mode, and Milestone 5's acceptance: neighbours become perceptible, signed cues preserved.</summary>
    public class SurveyTests
    {
        private static PerceptionParameters Defaults() => new PerceptionParameters().Validated();

        [Fact]
        public void ZeroSurvey_IsExactlyNormalPerception()
        {
            PerceptionParameters p = Defaults();
            for (float gap = -2f; gap <= 2f; gap += 0.05f)
            {
                PerceptionChannels normal = PerceptionModel.Evaluate(gap, p);
                PerceptionChannels zero = PerceptionModel.Evaluate(gap, p, 0f);
                Assert.Equal(normal.Band, zero.Band);
                Assert.Equal(normal.Density, zero.Density);
                Assert.Equal(normal.Coherence, zero.Coherence);
            }
        }

        [Fact]
        public void Survey_RevealsGeometryThatIsOtherwiseOutside()
        {
            // The lab's E and F at w = +/-2 are 1.8 from focus: OUTSIDE normally.
            PerceptionParameters p = Defaults();
            Assert.Equal(OcclusionBand.Outside, PerceptionModel.Evaluate(1.8f, p).Band);

            PerceptionChannels surveyed = PerceptionModel.Evaluate(1.8f, p, 1f);
            Assert.NotEqual(OcclusionBand.Outside, surveyed.Band);
            Assert.True(surveyed.Density > 0f);
        }

        [Fact]
        public void Survey_RaisesNeighbourDensity_ButNotCoherence()
        {
            // A surveyed neighbour must read as an echo, not as local geometry: it gets denser and more visible,
            // but it keeps shimmering and carrying phase, because its real focus error has not changed.
            PerceptionParameters p = Defaults();
            PerceptionChannels normal = PerceptionModel.Evaluate(0.8f, p);
            PerceptionChannels surveyed = PerceptionModel.Evaluate(0.8f, p, 1f);

            Assert.True(surveyed.Density > normal.Density);
            Assert.True(surveyed.Visibility > normal.Visibility);
            Assert.Equal(normal.Coherence, surveyed.Coherence);
            Assert.Equal(normal.PhaseAmplitude, surveyed.PhaseAmplitude);
        }

        [Fact]
        public void Survey_PreservesSignedCues()
        {
            // Milestone 5 acceptance: ana and kata stay distinguishable while surveying, by more than colour.
            PerceptionParameters p = Defaults();
            for (float gap = 0.2f; gap < 2.5f; gap += 0.1f)
            {
                PerceptionChannels ana = PerceptionModel.Evaluate(gap, p, 1f);
                PerceptionChannels kata = PerceptionModel.Evaluate(-gap, p, 1f);
                Assert.True(ana.PhaseVelocity > 0f && kata.PhaseVelocity < 0f, $"phase direction at {gap}");
                Assert.True(ana.Tint > 0f && kata.Tint < 0f, $"tint at {gap}");
                Assert.Equal(ana.Density, kata.Density);
            }
        }

        [Fact]
        public void Survey_NeverMakesANeighbourLocal()
        {
            PerceptionParameters p = Defaults();
            Assert.Equal(OcclusionBand.NearW, PerceptionModel.Evaluate(p.BandA + 0.01f, p, 1f).Band);
            (float a, float b, float c) = PerceptionModel.Bands(p, 1f);
            Assert.Equal(p.BandA, a);
            AssertApprox(p.BandB * p.SurveyBandScale, b);
            AssertApprox(p.BandC * p.SurveyBandScale, c);
        }

        [Fact]
        public void Ramp_EasesInOverTheRampTime_AndBackOut()
        {
            var ramp = new SurveyRamp { Engaged = true };
            ramp.Update(0.125f, 0.25f);
            AssertApprox(0.5f, ramp.Amount);           // eased midpoint of smoothstep
            ramp.Update(0.2f, 0.25f);
            Assert.Equal(1f, ramp.Amount);

            ramp.Engaged = false;
            ramp.Update(0.05f, 0.25f);
            Assert.True(ramp.Amount < 1f && ramp.Amount > 0f);
            ramp.Update(1f, 0.25f);
            Assert.Equal(0f, ramp.Amount);             // released: back to normal perception
        }

        [Fact]
        public void Ramp_WithZeroTime_Switches()
        {
            var ramp = new SurveyRamp { Engaged = true };
            ramp.Update(0.001f, 0f);
            Assert.Equal(1f, ramp.Amount);
        }
    }
}
