using System;
using FourDex.Perception;
using Xunit;
using static FourDex.Tests.TestMath;

namespace FourDex.Tests
{
    /// <summary>SPEC §12-§14: shadows carry W information, unseen objects still cast, and focus sharpens them.</summary>
    public class ShadowTests
    {
        private static ShadowParameters Defaults() => new ShadowParameters().Validated();

        [Fact]
        public void HiddenOccluder_StillCasts()
        {
            // SPEC §13: an object past band C (1.5 by default) is not rendered at all, and must still throw an
            // ana-signed shadow. The spec's own example sits at w = +1.4, which is only just inside the band.
            var perception = new PerceptionParameters().Validated();
            Assert.Equal(OcclusionBand.Outside, PerceptionModel.Evaluate(1.6f, perception).Band);
            Assert.Equal(0f, PerceptionModel.Evaluate(1.6f, perception).Density);
            Assert.True(PerceptionModel.Evaluate(1.4f, perception).Density < 0.05f, "and at 1.4 it is barely there");

            ShadowChannels s = ShadowModel.Evaluate(1.6f, Defaults());
            Assert.True(s.Casts);
            Assert.True(s.Tint > 0f, "and it reads as ana");
        }

        [Fact]
        public void Strength_FallsWithWDistance_AndStopsAtTheRange()
        {
            ShadowParameters p = Defaults();
            Assert.Equal(1f, ShadowModel.Evaluate(0f, p).Strength);
            Assert.True(ShadowModel.Evaluate(1f, p).Strength > ShadowModel.Evaluate(2f, p).Strength);
            Assert.False(ShadowModel.Evaluate(p.WRange + 0.01f, p).Casts);
        }

        [Fact]
        public void FocusingOnTheOccluder_SharpensItsShadow()
        {
            // SPEC §14: shadows can be focused without moving toward their source. Here the occluder sits at w = +1
            // and the focus sweeps toward it; the gap is what the player's focus leaves.
            ShadowParameters p = Defaults();
            ShadowChannels far = ShadowModel.Evaluate(1f, p);   // focus at 0
            ShadowChannels near = ShadowModel.Evaluate(0.25f, p); // focus at 0.75
            ShadowChannels onIt = ShadowModel.Evaluate(0f, p);    // focus at 1

            Assert.True(near.Coherence > far.Coherence);
            Assert.Equal(1f, onIt.Coherence);
            Assert.True(near.Softness < far.Softness, "edges get crisper");
            Assert.True(near.ShimmerHz < far.ShimmerHz, "and it stops shimmering");
            Assert.Equal(0f, onIt.ShimmerHz);
        }

        [Fact]
        public void AnaAndKata_ShadowsDifferOnlyInSign()
        {
            ShadowParameters p = Defaults();
            for (float gap = 0.2f; gap < p.WRange; gap += 0.2f)
            {
                ShadowChannels ana = ShadowModel.Evaluate(gap, p);
                ShadowChannels kata = ShadowModel.Evaluate(-gap, p);
                AssertApprox(ana.Strength, kata.Strength);
                AssertApprox(ana.Coherence, kata.Coherence);
                Assert.True(ana.Tint > 0f && kata.Tint < 0f);
            }
        }

        [Fact]
        public void Disabled_CastsNothing()
        {
            var p = new ShadowParameters { Enabled = false }.Validated();
            Assert.False(ShadowModel.Evaluate(0f, p).Casts);
        }

        [Fact]
        public void Validated_FixesBadValues_WithoutTouchingTheOriginal()
        {
            var raw = new ShadowParameters { Darkness = 5f, FocusWidth = 0f, SoftnessAtZeroCoherence = 0.2f };
            ShadowParameters p = raw.Validated();
            Assert.Equal(1f, p.Darkness);
            Assert.True(p.FocusWidth > 0f);
            Assert.True(p.SoftnessAtZeroCoherence >= 1f);
            Assert.Equal(5f, raw.Darkness);
        }
    }
}
