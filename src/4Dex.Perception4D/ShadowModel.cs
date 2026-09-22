using System;

namespace FourDex.Perception
{
    /// <summary>
    /// Tuning for the 4D shadow field (SPEC §12-§14). Shadows are a perception system, not decoration: every channel
    /// here carries something about the occluder's position in W.
    /// </summary>
    [Serializable]
    public sealed class ShadowParameters
    {
        public bool Enabled = true;

        /// <summary>Darkening at full strength, 0..1.</summary>
        public float Darkness = 0.7f;

        /// <summary>
        /// W width over which a shadow stays coherent (SPEC §14). Coherence is 1 when focus sits on the occluder's W
        /// and falls away either side, so a shadow can be "focused" without moving toward its source.
        /// </summary>
        public float FocusWidth = 0.6f;

        /// <summary>|W gap| at which a shadow fades to nothing. Wider than the visibility bands, so unseen objects still cast.</summary>
        public float WRange = 3f;

        /// <summary>|W gap| at which the ana/kata hue reaches full strength.</summary>
        public float TintSaturationW = 1.5f;

        /// <summary>Occluder influence radius as a multiple of its 3D size.</summary>
        public float RadiusScale = 1.5f;

        /// <summary>How far the shadow reaches along the light, in metres: the bound on influence (ARCHITECTURE §9, risk 4).</summary>
        public float ReachMetres = 4f;

        /// <summary>How far the hue pulls the shadow colour toward the ana/kata palette. 0 turns colour off as a cue.</summary>
        public float TintStrength = 0.8f;

        /// <summary>Shadow flicker at zero coherence, in Hz: SPEC §14's shimmer from focus mismatch.</summary>
        public float MaxShimmerHz = 8f;

        /// <summary>Edge blur multiplier at zero coherence: an out-of-focus shadow has soft, uncertain edges.</summary>
        public float SoftnessAtZeroCoherence = 3f;

        public ShadowParameters Validated()
        {
            var p = (ShadowParameters)MemberwiseClone();
            p.Darkness = PerceptionParameters.Clamp01(p.Darkness);
            p.FocusWidth = MathF.Max(p.FocusWidth, 1e-3f);
            p.WRange = MathF.Max(p.WRange, 1e-3f);
            p.TintSaturationW = MathF.Max(p.TintSaturationW, 1e-3f);
            p.RadiusScale = MathF.Max(p.RadiusScale, 0f);
            p.ReachMetres = MathF.Max(p.ReachMetres, 0f);
            p.TintStrength = PerceptionParameters.Clamp01(p.TintStrength);
            p.MaxShimmerHz = MathF.Max(p.MaxShimmerHz, 0f);
            p.SoftnessAtZeroCoherence = MathF.Max(p.SoftnessAtZeroCoherence, 1f);
            return p;
        }
    }

    /// <summary>What one occluder's shadow should look like, from its signed W gap.</summary>
    public readonly struct ShadowChannels
    {
        public ShadowChannels(float strength, float coherence, float tint, float shimmerHz, float softness)
        {
            Strength = strength;
            Coherence = coherence;
            Tint = tint;
            ShimmerHz = shimmerHz;
            Softness = softness;
        }

        /// <summary>Occlusion strength, 0..1. Above 0 well beyond the visibility bands: unseen objects still cast (SPEC §13).</summary>
        public readonly float Strength;

        /// <summary>1 when focus sits on the occluder's W, falling away either side (SPEC §14).</summary>
        public readonly float Coherence;

        /// <summary>signed W → hue: -1 full kata, +1 full ana (SPEC §12).</summary>
        public readonly float Tint;

        /// <summary>Flicker rate from focus mismatch.</summary>
        public readonly float ShimmerHz;

        /// <summary>Edge blur multiplier: 1 is a crisp edge, higher is an uncertain one.</summary>
        public readonly float Softness;

        public bool Casts => Strength > 0f;
    }

    public static class ShadowModel
    {
        /// <param name="signedGap">Signed W distance from the focus slice to the occluder, as for visibility.</param>
        /// <param name="p">Expected already <see cref="ShadowParameters.Validated"/>.</param>
        public static ShadowChannels Evaluate(float signedGap, ShadowParameters p)
        {
            if (!p.Enabled)
                return default;

            float abs = MathF.Abs(signedGap);
            // Linear fade over a range far wider than the visibility bands, so an object that is not rendered at all
            // still shows up as a shadow: SPEC §13's whole interaction loop depends on this.
            float strength = PerceptionParameters.Clamp01(1f - abs / p.WRange);
            if (strength <= 0f)
                return default;

            float x = abs / p.FocusWidth;
            float coherence = MathF.Exp(-x * x);
            float sign = MathF.Sign(signedGap);

            return new ShadowChannels(
                strength,
                coherence,
                tint: sign * PerceptionParameters.Clamp01(abs / p.TintSaturationW),
                shimmerHz: (1f - coherence) * p.MaxShimmerHz,
                softness: 1f + (1f - coherence) * (p.SoftnessAtZeroCoherence - 1f));
        }
    }
}
