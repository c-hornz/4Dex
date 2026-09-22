using System;

namespace FourDex.Perception
{
    /// <summary>Response curves for visibility against distance from focus (SPEC §8: Gaussian is "only a starting function").</summary>
    public enum VisibilityCurve
    {
        /// <summary><c>exp(-(x)^2)</c>, the SPEC §8 starting model.</summary>
        Gaussian,
        /// <summary><c>1 - x</c>, clamped. Reaches zero exactly at one focus width.</summary>
        Linear,
        /// <summary><c>1 - smoothstep(x)</c>. Flat near focus, flat again at one focus width.</summary>
        Smoothstep,
        /// <summary>1 inside one focus width, 0 outside. For checking the bands alone.</summary>
        Hard,
    }

    /// <summary>SPEC §11's banded occlusion model, by |W gap| against thresholds A &lt; B &lt; C.</summary>
    public enum OcclusionBand
    {
        /// <summary>|gap| &lt; A: normal or near-normal depth behaviour.</summary>
        Local,
        /// <summary>A ≤ |gap| &lt; B: phase-rendered, partially transmissive, depth aware.</summary>
        NearW,
        /// <summary>B ≤ |gap| &lt; C: sparse density, edge/interference representation.</summary>
        FarW,
        /// <summary>|gap| ≥ C: not rendered.</summary>
        Outside,
    }

    /// <summary>
    /// Every perceptual mapping's tuning, as plain data. Held by a Unity asset for live editing (SPEC §21);
    /// kept engine-free so the mapping is tested without Unity.
    /// </summary>
    [Serializable]
    public sealed class PerceptionParameters
    {
        /// <summary>W distance over which visibility falls off: sigma for Gaussian, the zero point for the others.</summary>
        public float FocusWidth = 0.35f;

        public VisibilityCurve Curve = VisibilityCurve.Gaussian;

        /// <summary>Band thresholds on |W gap| (SPEC §11). Kept ordered by <see cref="Validated"/>.</summary>
        public float BandA = 0.15f;
        public float BandB = 0.6f;
        public float BandC = 1.5f;

        /// <summary>Surface fill in NEAR_W at zero visibility; rises to 1 as visibility does.</summary>
        public float NearMinDensity = 0.35f;

        /// <summary>Surface fill entering FAR_W, fading to 0 at C.</summary>
        public float FarDensity = 0.25f;

        /// <summary>Phase stripes per metre at the focus edge, plus <see cref="PhaseFrequencyPerW"/> per W unit of gap.</summary>
        public float PhaseBaseFrequency = 2f;
        public float PhaseFrequencyPerW = 6f;

        /// <summary>Stripe travel in stripes per second. The sign comes from ana/kata.</summary>
        public float PhaseVelocity = 0.6f;

        /// <summary>Largest phase modulation, reached as visibility reaches 0. In focus it is 0, so geometry is stable.</summary>
        public float PhaseAmplitude = 0.6f;

        /// <summary>Swap which way ana and kata phases travel, to test whether the convention matters (SPEC §15).</summary>
        public bool InvertPhaseDirection;

        /// <summary>|W gap| at which the ana/kata tint reaches full strength.</summary>
        public float TintSaturationW = 1f;

        /// <summary>A copy with thresholds ordered and widths positive, so a bad inspector value degrades rather than breaks.</summary>
        public PerceptionParameters Validated()
        {
            var p = (PerceptionParameters)MemberwiseClone();
            p.FocusWidth = MathF.Max(p.FocusWidth, 1e-4f);
            p.BandA = MathF.Max(p.BandA, 0f);
            p.BandB = MathF.Max(p.BandB, p.BandA);
            p.BandC = MathF.Max(p.BandC, p.BandB);
            p.NearMinDensity = Clamp01(p.NearMinDensity);
            p.FarDensity = Clamp01(p.FarDensity);
            p.TintSaturationW = MathF.Max(p.TintSaturationW, 1e-4f);
            p.PhaseAmplitude = Clamp01(p.PhaseAmplitude);
            return p;
        }

        internal static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>
    /// What the renderer should show for one object: every channel is derived from its signed W gap, so every
    /// visual effect carries W information (SPEC §27).
    /// </summary>
    public readonly struct PerceptionChannels
    {
        public PerceptionChannels(OcclusionBand band, float signedGap, float visibility, float density, float coherence,
            float phaseFrequency, float phaseVelocity, float phaseAmplitude, float tint, float edgeWeight)
        {
            Band = band;
            SignedGap = signedGap;
            Visibility = visibility;
            Density = density;
            Coherence = coherence;
            PhaseFrequency = phaseFrequency;
            PhaseVelocity = phaseVelocity;
            PhaseAmplitude = phaseAmplitude;
            Tint = tint;
            EdgeWeight = edgeWeight;
        }

        public readonly OcclusionBand Band;

        /// <summary>Signed W distance from the focus slice to the nearest part of the object. + is ana.</summary>
        public readonly float SignedGap;

        /// <summary>The response curve's value, 1 in focus.</summary>
        public readonly float Visibility;

        /// <summary>absW → density: fraction of the surface drawn.</summary>
        public readonly float Density;

        /// <summary>focus error → coherence: 1 is stable, 0 is maximal shimmer.</summary>
        public readonly float Coherence;

        /// <summary>absW → phaseFrequency, stripes per metre.</summary>
        public readonly float PhaseFrequency;

        /// <summary>signedW → phaseDirection: stripes per second; + for ana, - for kata (unless inverted).</summary>
        public readonly float PhaseVelocity;

        public readonly float PhaseAmplitude;

        /// <summary>signedW → palette: -1 full kata, +1 full ana. Colour is one redundant cue, never the only one (SPEC §9).</summary>
        public readonly float Tint;

        /// <summary>1 in FAR_W: draw the silhouette rather than the faces (SPEC §11).</summary>
        public readonly float EdgeWeight;

        public override string ToString() =>
            $"{Band} gap {SignedGap:+0.000;-0.000;+0.000} V {Visibility:0.00} density {Density:0.00}";
    }

    public static class PerceptionModel
    {
        /// <summary>
        /// Signed W distance from the focus slice to an object occupying <c>[centerW - halfExtentW, centerW + halfExtentW]</c>
        /// in the viewer's W. Zero while the slice passes through the object, so W thickness is perceptible:
        /// a thick object stays in focus across a wider range of focus.
        /// </summary>
        public static float SignedGap(float centerW, float halfExtentW, float focusW)
        {
            float d = centerW - focusW;
            float outside = MathF.Abs(d) - MathF.Max(halfExtentW, 0f);
            return outside <= 0f ? 0f : MathF.Sign(d) * outside;
        }

        public static float Visibility(float absGap, PerceptionParameters p)
        {
            float x = absGap / p.FocusWidth;
            switch (p.Curve)
            {
                case VisibilityCurve.Linear:
                    return PerceptionParameters.Clamp01(1f - x);
                case VisibilityCurve.Smoothstep:
                    float t = PerceptionParameters.Clamp01(x);
                    return 1f - t * t * (3f - 2f * t);
                case VisibilityCurve.Hard:
                    return x < 1f ? 1f : 0f;
                default:
                    return MathF.Exp(-x * x);
            }
        }

        public static OcclusionBand Classify(float absGap, PerceptionParameters p) =>
            absGap < p.BandA ? OcclusionBand.Local :
            absGap < p.BandB ? OcclusionBand.NearW :
            absGap < p.BandC ? OcclusionBand.FarW :
            OcclusionBand.Outside;

        /// <param name="p">Expected already <see cref="PerceptionParameters.Validated"/>; call that once per frame, not per object.</param>
        public static PerceptionChannels Evaluate(float signedGap, PerceptionParameters p)
        {
            float abs = MathF.Abs(signedGap);
            float v = Visibility(abs, p);
            OcclusionBand band = Classify(abs, p);

            float density = band switch
            {
                OcclusionBand.Local => 1f,
                OcclusionBand.NearW => p.NearMinDensity + (1f - p.NearMinDensity) * v,
                // Fades to nothing at C, so crossing into OUTSIDE is continuous rather than a pop.
                OcclusionBand.FarW => p.FarDensity * (p.BandC > p.BandB ? 1f - (abs - p.BandB) / (p.BandC - p.BandB) : 0f),
                _ => 0f,
            };

            // Sign carries ana vs kata into two non-colour channels (phase direction) and one colour channel (tint).
            float sign = MathF.Sign(signedGap);
            float direction = p.InvertPhaseDirection ? -sign : sign;

            return new PerceptionChannels(
                band,
                signedGap,
                v,
                density,
                coherence: v,
                phaseFrequency: p.PhaseBaseFrequency + p.PhaseFrequencyPerW * abs,
                phaseVelocity: direction * p.PhaseVelocity,
                phaseAmplitude: p.PhaseAmplitude * (1f - v),
                tint: sign * PerceptionParameters.Clamp01(abs / p.TintSaturationW),
                edgeWeight: band == OcclusionBand.FarW ? 1f : 0f);
        }
    }
}
