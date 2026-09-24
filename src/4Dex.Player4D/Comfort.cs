using System;

namespace FourDex.Player
{
    /// <summary>Whether ordinary turning sweeps or jumps in steps (SPEC §22).</summary>
    public enum TurnMode
    {
        /// <summary>Continuous: turning follows the control at a rate.</summary>
        Smooth,
        /// <summary>Fixed steps on each press. Less of the optical flow that makes turning uncomfortable.</summary>
        Snap,
    }

    /// <summary>
    /// SPEC §22's comfort controls. Experimental mappings are allowed to be disorienting; these exist so a person can
    /// stay in the headset while trying them, not to stop them being tried.
    /// </summary>
    [Serializable]
    public sealed class ComfortParameters
    {
        public bool VignetteEnabled = true;

        /// <summary>How far the view closes in at full motion, 0..1. 0.6 leaves a comfortable tunnel.</summary>
        public float VignetteStrength = 0.6f;

        /// <summary>Walking speed (m/s) that counts as full motion for the vignette.</summary>
        public float SpeedReference = 2f;

        /// <summary>Turn rate (degrees/s) that counts as full motion.</summary>
        public float TurnReference = 90f;

        /// <summary>
        /// Rate of movement through W (W units/s) that counts as full motion. Its own reference because travelling
        /// ana is nothing like walking, and may well need a stronger vignette.
        /// </summary>
        public float WRateReference = 1f;

        /// <summary>Seconds for the vignette to follow a change in motion. Too fast is itself distracting.</summary>
        public float ResponseSeconds = 0.25f;

        public TurnMode TurnMode = TurnMode.Smooth;

        /// <summary>Degrees per press in snap mode.</summary>
        public float SnapDegrees = 30f;

        public ComfortParameters Validated()
        {
            var p = (ComfortParameters)MemberwiseClone();
            p.VignetteStrength = Clamp01(p.VignetteStrength);
            p.SpeedReference = MathF.Max(p.SpeedReference, 1e-3f);
            p.TurnReference = MathF.Max(p.TurnReference, 1e-3f);
            p.WRateReference = MathF.Max(p.WRateReference, 1e-3f);
            p.ResponseSeconds = MathF.Max(p.ResponseSeconds, 0f);
            p.SnapDegrees = MathF.Max(p.SnapDegrees, 1f);
            return p;
        }

        internal static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>How fast the player is moving right now, in the three ways that provoke discomfort.</summary>
    public readonly struct MotionSample
    {
        public MotionSample(float speed, float turnRateDegrees, float wRate)
        {
            Speed = speed;
            TurnRateDegrees = turnRateDegrees;
            WRate = wRate;
        }

        /// <summary>Metres per second through XYZ.</summary>
        public readonly float Speed;

        /// <summary>Degrees per second of view rotation, in every plane.</summary>
        public readonly float TurnRateDegrees;

        /// <summary>W units per second: ana/kata travel.</summary>
        public readonly float WRate;
    }

    /// <summary>
    /// Drives the comfort vignette: how open the view is, 1 fully open down to <c>1 - VignetteStrength</c> at full
    /// motion, eased so it never snaps.
    /// </summary>
    public sealed class VignetteModel
    {
        /// <summary>1 is fully open. What the renderer scales its aperture by.</summary>
        public float Aperture { get; private set; } = 1f;

        /// <summary>The unsmoothed target, for the readout: how much motion is being registered, 0..1.</summary>
        public float Motion { get; private set; }

        public void Update(in MotionSample motion, ComfortParameters p, float dt)
        {
            if (!p.VignetteEnabled)
            {
                Motion = 0f;
                Aperture = 1f;
                return;
            }

            // Whichever kind of motion is worst decides, rather than the sum: walking while turning should not
            // close the view twice as far as either alone.
            Motion = ComfortParameters.Clamp01(MathF.Max(
                MathF.Abs(motion.Speed) / p.SpeedReference,
                MathF.Max(MathF.Abs(motion.TurnRateDegrees) / p.TurnReference, MathF.Abs(motion.WRate) / p.WRateReference)));

            float target = 1f - p.VignetteStrength * Motion;
            float blend = p.ResponseSeconds > 0f ? 1f - MathF.Exp(-MathF.Max(dt, 0f) / p.ResponseSeconds) : 1f;
            Aperture += (target - Aperture) * blend;
        }

        public void Reset()
        {
            Aperture = 1f;
            Motion = 0f;
        }

        /// <summary>Set the aperture directly, bypassing the easing: for previews and tests.</summary>
        public void Force(float aperture)
        {
            Aperture = ComfortParameters.Clamp01(aperture);
            Motion = 1f - Aperture;
        }
    }

    /// <summary>Turning, either swept or stepped (SPEC §22 snap vs smooth).</summary>
    public static class ComfortTurn
    {
        /// <summary>
        /// Radians to turn this frame.
        /// </summary>
        /// <param name="held">Turn control held, -1..1. Used in Smooth mode.</param>
        /// <param name="pressed">Turn control pressed this frame, -1, 0 or +1. Used in Snap mode.</param>
        /// <param name="degreesPerSecond">Smooth turn rate.</param>
        public static float Step(float held, int pressed, float degreesPerSecond, ComfortParameters p, float dt)
        {
            const float toRadians = MathF.PI / 180f;
            return p.TurnMode == TurnMode.Snap
                ? pressed * p.SnapDegrees * toRadians
                : held * degreesPerSecond * toRadians * MathF.Max(dt, 0f);
        }
    }
}
