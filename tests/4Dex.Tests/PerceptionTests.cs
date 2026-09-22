using System;
using FourDex.Perception;
using Xunit;
using static FourDex.Tests.TestMath;

namespace FourDex.Tests
{
    /// <summary>SPEC §8 visibility, §9 signed channels, §11 bands, and the W-extent gap.</summary>
    public class PerceptionTests
    {
        private static PerceptionParameters Defaults() => new PerceptionParameters().Validated();

        [Theory]
        [InlineData(VisibilityCurve.Gaussian)]
        [InlineData(VisibilityCurve.Linear)]
        [InlineData(VisibilityCurve.Smoothstep)]
        [InlineData(VisibilityCurve.Hard)]
        public void EveryCurve_IsOneInFocus_AndNeverRisesWithDistance(VisibilityCurve curve)
        {
            var p = new PerceptionParameters { Curve = curve }.Validated();
            Assert.Equal(1f, PerceptionModel.Visibility(0f, p));
            float previous = 1f;
            for (float gap = 0f; gap < 3f; gap += 0.01f)
            {
                float v = PerceptionModel.Visibility(gap, p);
                Assert.InRange(v, 0f, 1f);
                Assert.True(v <= previous + 1e-6f, $"{curve} rose at gap {gap}");
                previous = v;
            }
        }

        [Fact]
        public void Gaussian_MatchesSpec8()
        {
            PerceptionParameters p = Defaults();
            float sigma = p.FocusWidth;
            AssertApprox(MathF.Exp(-1f), PerceptionModel.Visibility(sigma, p));
            AssertApprox(MathF.Exp(-4f), PerceptionModel.Visibility(2f * sigma, p));
        }

        [Fact]
        public void Bands_FollowTheThresholds()
        {
            PerceptionParameters p = Defaults();
            Assert.Equal(OcclusionBand.Local, PerceptionModel.Classify(0f, p));
            Assert.Equal(OcclusionBand.Local, PerceptionModel.Classify(p.BandA - 1e-4f, p));
            Assert.Equal(OcclusionBand.NearW, PerceptionModel.Classify(p.BandA, p));
            Assert.Equal(OcclusionBand.FarW, PerceptionModel.Classify(p.BandB, p));
            Assert.Equal(OcclusionBand.Outside, PerceptionModel.Classify(p.BandC, p));
        }

        [Fact]
        public void Outside_IsNotDrawn_AndFarWFadesOutContinuously()
        {
            PerceptionParameters p = Defaults();
            Assert.Equal(0f, PerceptionModel.Evaluate(p.BandC + 0.1f, p).Density);
            Assert.True(PerceptionModel.Evaluate(p.BandC - 1e-3f, p).Density < 0.01f);
            AssertApprox(p.FarDensity, PerceptionModel.Evaluate(p.BandB, p).Density, 1e-4f);
        }

        [Fact]
        public void InFocus_IsSolidAndStable()
        {
            PerceptionChannels c = PerceptionModel.Evaluate(0f, Defaults());
            Assert.Equal(OcclusionBand.Local, c.Band);
            Assert.Equal(1f, c.Density);
            Assert.Equal(1f, c.Coherence);
            Assert.Equal(0f, c.PhaseAmplitude);
            Assert.Equal(0f, c.Tint);
            Assert.Equal(0f, c.EdgeWeight);
        }

        [Fact]
        public void AnaAndKata_DifferOnlyInSign_AndInMoreThanColour()
        {
            // SPEC §8/§9: signed deltaW must stay recoverable, and colour must not be the only cue.
            PerceptionParameters p = Defaults();
            for (float gap = 0.05f; gap < p.BandC; gap += 0.05f)
            {
                PerceptionChannels ana = PerceptionModel.Evaluate(gap, p);
                PerceptionChannels kata = PerceptionModel.Evaluate(-gap, p);

                Assert.Equal(ana.Band, kata.Band);
                Assert.Equal(ana.Density, kata.Density);
                Assert.Equal(ana.Coherence, kata.Coherence);
                Assert.Equal(ana.PhaseFrequency, kata.PhaseFrequency);
                Assert.True(ana.PhaseVelocity > 0f && kata.PhaseVelocity < 0f, "phase direction must carry the sign");
                Assert.True(ana.Tint > 0f && kata.Tint < 0f);
            }
        }

        [Fact]
        public void InvertPhaseDirection_SwapsTheConvention_AndNothingElse()
        {
            var p = new PerceptionParameters { InvertPhaseDirection = true }.Validated();
            PerceptionChannels ana = PerceptionModel.Evaluate(0.5f, p);
            Assert.True(ana.PhaseVelocity < 0f);
            Assert.True(ana.Tint > 0f);
        }

        [Fact]
        public void FartherFromFocus_MeansFasterStripes_LessDensity_LessCoherence()
        {
            PerceptionParameters p = Defaults();
            PerceptionChannels near = PerceptionModel.Evaluate(0.2f, p);
            PerceptionChannels far = PerceptionModel.Evaluate(0.5f, p);
            Assert.True(far.PhaseFrequency > near.PhaseFrequency);
            Assert.True(far.Density < near.Density);
            Assert.True(far.Coherence < near.Coherence);
            Assert.True(far.PhaseAmplitude > near.PhaseAmplitude);
        }

        [Fact]
        public void Gap_IsZeroWhileTheSlicePassesThroughTheObject()
        {
            // The lab's slab spans W -1.5..+1.5: in focus from anywhere in that range.
            Assert.Equal(0f, PerceptionModel.SignedGap(0f, 1.5f, 1.2f));
            Assert.Equal(0f, PerceptionModel.SignedGap(0f, 1.5f, -1.5f));
            AssertApprox(-0.5f, PerceptionModel.SignedGap(0f, 1.5f, 2f));
            AssertApprox(0.5f, PerceptionModel.SignedGap(0f, 1.5f, -2f));
            // A thin object at w = +1 seen from focus 0 is ana by its near face.
            AssertApprox(0.8f, PerceptionModel.SignedGap(1f, 0.2f, 0f));
        }

        [Fact]
        public void Validated_OrdersBadThresholds_WithoutTouchingTheOriginal()
        {
            var raw = new PerceptionParameters { BandA = 1f, BandB = 0.5f, BandC = 0.2f, FocusWidth = 0f };
            PerceptionParameters p = raw.Validated();
            Assert.True(p.BandA <= p.BandB && p.BandB <= p.BandC);
            Assert.True(p.FocusWidth > 0f);
            Assert.Equal(0.5f, raw.BandB);
        }
    }
}
