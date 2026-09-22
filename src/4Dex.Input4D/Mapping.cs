using System;
using System.Collections.Generic;

namespace FourDex.InputMapping
{
    /// <summary>
    /// A physical input the mapping can read (SPEC §5: all six HMD signals, independently). Angles are degrees and
    /// positions metres, both relative to the recentred neutral head pose.
    /// </summary>
    public enum InputSignal
    {
        /// <summary>Turn right is +.</summary>
        HeadYaw,
        /// <summary>Look up is +.</summary>
        HeadPitch,
        /// <summary>Unity roll: + turns the head's X axis toward its Y (counter-clockwise as seen from behind).</summary>
        HeadRoll,
        /// <summary>Tracking-space right is +.</summary>
        HeadX,
        /// <summary>Up is +.</summary>
        HeadY,
        /// <summary>Tracking-space forward is +: leaning forward.</summary>
        HeadZ,
    }

    /// <summary>
    /// How a binding turns its input into motion (SPEC §6: "absolute versus delta behaviour").
    /// </summary>
    public enum MappingMode
    {
        /// <summary>
        /// Steer: the shaped offset from neutral is a rate, integrated every frame into the player's 4D state.
        /// Hold a tilt to keep rotating; returning to neutral stops, and the motion gained is kept.
        /// </summary>
        Delta,
        /// <summary>
        /// Follow: the shaped offset from neutral is an amount. Its frame-to-frame changes are applied, so returning
        /// the head to neutral returns the 4D view.
        /// </summary>
        Absolute,
    }

    /// <summary>
    /// Where one input's motion goes: a weight per rotation plane and per translation axis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The weight on the input's own conventional channel (yaw: XZ, pitch: YZ, roll: XY, HeadX/Y/Z: X/Y/Z) is the
    /// <b>perceived fraction</b>. The camera always follows the head in full (ARCHITECTURE §3), so 1 is ordinary
    /// behaviour, and below 1 the 4D view follows the head by the remainder, so less of the motion is perceived.
    /// SPEC's "pitch: 70% YZ + 30% YW" is then YZ 0.7, YW 0.3.
    /// </para>
    /// <para>Every other weight adds motion in that plane or axis per unit of input, + input giving + motion.</para>
    /// </remarks>
    [Serializable]
    public struct OutputWeights
    {
        public float XY, XZ, YZ, XW, YW, ZW;
        public float X, Y, Z, W;
    }

    /// <summary>One input routed to the 4D outputs, with its shaping (SPEC §6).</summary>
    [Serializable]
    public sealed class InputBinding
    {
        public InputSignal Signal;

        /// <summary>SPEC §22: individual HMD-derived mappings can be switched off.</summary>
        public bool Enabled = true;

        public MappingMode Mode = MappingMode.Delta;

        /// <summary>Offsets smaller than this (degrees or metres) are ignored; larger ones start from zero, not a jump.</summary>
        public float DeadZone;

        /// <summary>1 is linear. Above 1, small motions are softened and large ones kept: <c>ref * (x / ref)^exp</c>.</summary>
        public float Exponent = 1f;

        /// <summary>The input magnitude at which the curve passes through unchanged, for <see cref="Exponent"/>.</summary>
        public float CurveReference = 30f;

        public float Gain = 1f;

        public bool Invert;

        /// <summary>Largest shaped magnitude; 0 means no limit (SPEC §22 gain limiting).</summary>
        public float Clamp;

        public OutputWeights Weights;

        /// <summary>Dead zone, curve, gain, inversion and clamp, in that order.</summary>
        public float Shape(float x)
        {
            float magnitude = MathF.Abs(x) - MathF.Max(DeadZone, 0f);
            if (magnitude <= 0f)
                return 0f;
            if (Exponent != 1f && CurveReference > 0f)
                magnitude = CurveReference * MathF.Pow(magnitude / CurveReference, MathF.Max(Exponent, 0.01f));
            magnitude *= Gain;
            if (Clamp > 0f)
                magnitude = MathF.Min(magnitude, Clamp);
            float sign = MathF.Sign(x) * (Invert ? -1f : 1f);
            return sign * magnitude;
        }
    }

    /// <summary>A named mapping scheme: a preset over the one engine (SPEC §6), not a separate controller script.</summary>
    [Serializable]
    public sealed class MappingProfile
    {
        public string Name = "CUSTOM";

        public List<InputBinding> Bindings = new List<InputBinding>();

        /// <summary>Scales every XW/YW/ZW contribution: SPEC §22 "reduced W rotation gain".</summary>
        public float WRotationGain = 1f;

        /// <summary>Scales every W translation contribution.</summary>
        public float WTranslationGain = 1f;

        public InputBinding? Find(InputSignal signal) => Bindings.Find(b => b.Signal == signal);
    }

    /// <summary>The physical inputs for one frame, relative to the neutral pose.</summary>
    public struct HeadOffsets
    {
        public float Yaw, Pitch, Roll;
        public float X, Y, Z;

        public float this[InputSignal signal] => signal switch
        {
            InputSignal.HeadYaw => Yaw,
            InputSignal.HeadPitch => Pitch,
            InputSignal.HeadRoll => Roll,
            InputSignal.HeadX => X,
            InputSignal.HeadY => Y,
            _ => Z,
        };
    }
}
