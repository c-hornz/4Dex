using FourDex.Perception;
using Xunit;
using static FourDex.Tests.TestMath;

namespace FourDex.Tests
{
    /// <summary>SPEC §15-§17: ripple carries W distance and side, echoes carry W extent, and the view's W lean shears shadows.</summary>
    public class ShadowRippleTests
    {
        private static ShadowParameters Defaults() => new ShadowParameters().Validated();

        [Fact]
        public void RippleFrequency_RisesWithWDistance()
        {
            ShadowParameters p = Defaults();
            Assert.Equal(p.RippleBaseFrequency, ShadowModel.Evaluate(0f, p).RippleFrequency);
            Assert.True(ShadowModel.Evaluate(1.5f, p).RippleFrequency > ShadowModel.Evaluate(0.5f, p).RippleFrequency);
        }

        [Fact]
        public void RippleDirection_CarriesTheSide_AndCanBeInverted()
        {
            ShadowParameters p = Defaults();
            Assert.True(ShadowModel.Evaluate(1f, p).RippleSpeed > 0f, "ana rings travel outward");
            Assert.True(ShadowModel.Evaluate(-1f, p).RippleSpeed < 0f, "kata rings travel inward");

            var inverted = new ShadowParameters { InvertRipple = true }.Validated();
            Assert.True(ShadowModel.Evaluate(1f, inverted).RippleSpeed < 0f);
            // Inverting the ripple must not quietly flip the colour cue as well.
            Assert.True(ShadowModel.Evaluate(1f, inverted).Tint > 0f);
        }

        [Fact]
        public void RippleAmplitude_FollowsOcclusionStrength()
        {
            ShadowParameters p = Defaults();
            ShadowChannels near = ShadowModel.Evaluate(0.5f, p);
            ShadowChannels far = ShadowModel.Evaluate(2.5f, p);
            AssertApprox(p.RippleAmplitude * near.Strength, near.RippleAmplitude);
            Assert.True(far.RippleAmplitude < near.RippleAmplitude);

            var off = new ShadowParameters { RippleAmplitude = 0f }.Validated();
            Assert.Equal(0f, ShadowModel.Evaluate(0.5f, off).RippleAmplitude);
        }

        [Fact]
        public void EchoSpread_ComesFromWExtent_AndConvergesAsFocusArrives()
        {
            // The lab's slab has a 1.5 half-extent through W; a thin object has almost none.
            ShadowParameters p = Defaults();
            float thick = ShadowModel.Evaluate(1f, p, 1.5f).EchoSpread;
            float thin = ShadowModel.Evaluate(1f, p, 0.05f).EchoSpread;
            Assert.True(thick > thin, "spread shows W thickness");

            float nearFocus = ShadowModel.Evaluate(0.1f, p, 1.5f).EchoSpread;
            Assert.True(nearFocus < thick, "echoes converge as focus approaches");
            AssertApprox(0f, ShadowModel.Evaluate(0f, p, 1.5f).EchoSpread, 1e-6f);
        }

        [Fact]
        public void Shear_IsProportionalToSignedGap_AndOppositeForAnaAndKata()
        {
            // SPEC §16: rotating the view through W slides shadows apart. The lean factor is supplied by the caller
            // and is zero in a conventional view, so this is the per-unit-lean part.
            ShadowParameters p = Defaults();
            AssertApprox(p.ShearScale * 1.2f, ShadowModel.Evaluate(1.2f, p).ShearPerLean);
            AssertApprox(-p.ShearScale * 1.2f, ShadowModel.Evaluate(-1.2f, p).ShearPerLean);
            Assert.Equal(0f, ShadowModel.Evaluate(0f, p).ShearPerLean);
        }

        [Fact]
        public void EchoCount_IsClampedToSomethingDrawable()
        {
            Assert.Equal(1, new ShadowParameters { EchoCount = 0 }.Validated().EchoCount);
            Assert.Equal(5, new ShadowParameters { EchoCount = 50 }.Validated().EchoCount);
        }
    }
}
