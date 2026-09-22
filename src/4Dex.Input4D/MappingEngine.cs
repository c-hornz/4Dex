using System;
using System.Collections.Generic;
using FourDex.Math4D;

namespace FourDex.InputMapping
{
    /// <summary>The ten things an input can drive: six rotation planes, four translation axes.</summary>
    public enum OutputChannel { XY, XZ, YZ, XW, YW, ZW, X, Y, Z, W }

    /// <summary>What one binding produced this frame, for the input mapping monitor (SPEC §20).</summary>
    public readonly struct Contribution
    {
        public Contribution(InputSignal signal, MappingMode mode, float input, float shaped, OutputWeights output)
        {
            Signal = signal;
            Mode = mode;
            Input = input;
            Shaped = shaped;
            Output = output;
        }

        public readonly InputSignal Signal;
        public readonly MappingMode Mode;

        /// <summary>Raw offset from neutral.</summary>
        public readonly float Input;

        /// <summary>After dead zone, curve, gain, inversion and clamp.</summary>
        public readonly float Shaped;

        /// <summary>Degrees / metres for Absolute; degrees / metres per second for Delta.</summary>
        public readonly OutputWeights Output;
    }

    /// <summary>This frame's motion, in the player's own planes and axes.</summary>
    public readonly struct MappingStep
    {
        public MappingStep(Bivector4 rotation, Vec4 translation)
        {
            Rotation = rotation;
            Translation = translation;
        }

        /// <summary>Radians, applied simultaneously (one bivector), since one head motion can feed several planes.</summary>
        public readonly Bivector4 Rotation;

        /// <summary>Metres, including W.</summary>
        public readonly Vec4 Translation;

        public bool IsZero => Translation == Vec4.Zero && Rotation.XY == 0f && Rotation.XZ == 0f && Rotation.YZ == 0f &&
                              Rotation.XW == 0f && Rotation.YW == 0f && Rotation.ZW == 0f;
    }

    /// <summary>
    /// The one generalized mapping system (SPEC §6). Turns physical offsets into 4D motion according to a profile;
    /// engine-free and stateless apart from each Absolute binding's previous value.
    /// </summary>
    public sealed class MappingEngine
    {
        private const float DegToRad = MathF.PI / 180f;

        private readonly Dictionary<InputBinding, float> _previous = new Dictionary<InputBinding, float>();
        private readonly List<Contribution> _contributions = new List<Contribution>();

        public IReadOnlyList<Contribution> Contributions => _contributions;

        /// <summary>
        /// Forget Absolute bindings' previous values, so the next step starts from wherever the head is now, with no
        /// jump. Call on recentre and on profile switch.
        /// </summary>
        public void Reset()
        {
            _previous.Clear();
            _contributions.Clear();
        }

        public MappingStep Step(MappingProfile profile, in HeadOffsets offsets, float dt)
        {
            _contributions.Clear();
            var total = new float[10];

            foreach (InputBinding binding in profile.Bindings)
            {
                if (binding == null || !binding.Enabled)
                    continue;

                float input = offsets[binding.Signal];
                float shaped = binding.Shape(input);

                float amount;
                if (binding.Mode == MappingMode.Absolute)
                {
                    float previous = _previous.TryGetValue(binding, out float p) ? p : shaped;
                    amount = shaped - previous;
                    _previous[binding] = shaped;
                }
                else
                {
                    amount = shaped * MathF.Max(dt, 0f);
                }

                OutputWeights step = Route(binding, amount, profile);
                for (int i = 0; i < 10; i++)
                    total[i] += Get(step, (OutputChannel)i);

                _contributions.Add(new Contribution(binding.Signal, binding.Mode, input, shaped, Route(binding, shaped, profile)));
            }

            return new MappingStep(
                new Bivector4(
                    total[(int)OutputChannel.XY] * DegToRad, total[(int)OutputChannel.XZ] * DegToRad, total[(int)OutputChannel.YZ] * DegToRad,
                    total[(int)OutputChannel.XW] * DegToRad, total[(int)OutputChannel.YW] * DegToRad, total[(int)OutputChannel.ZW] * DegToRad),
                new Vec4(total[(int)OutputChannel.X], total[(int)OutputChannel.Y], total[(int)OutputChannel.Z], total[(int)OutputChannel.W]));
        }

        /// <summary>
        /// The input's own conventional channel, and the sign that makes the 4D view turn or move <i>with</i> the head.
        /// Unity yaw right is Math4D XZ negative (XZ + turns X toward Z, a left turn); looking up turns Z toward Y,
        /// which is YZ negative; Unity roll + turns X toward Y, which is XY positive.
        /// </summary>
        public static (OutputChannel Channel, float FollowSign) Conventional(InputSignal signal) => signal switch
        {
            InputSignal.HeadYaw => (OutputChannel.XZ, -1f),
            InputSignal.HeadPitch => (OutputChannel.YZ, -1f),
            InputSignal.HeadRoll => (OutputChannel.XY, 1f),
            InputSignal.HeadX => (OutputChannel.X, 1f),
            InputSignal.HeadY => (OutputChannel.Y, 1f),
            _ => (OutputChannel.Z, 1f),
        };

        /// <summary>Motion for an input value (linear in it), applying the perceived-fraction rule and the W gains.</summary>
        public static OutputWeights Route(InputBinding binding, float value, MappingProfile profile)
        {
            OutputWeights w = binding.Weights;
            var o = new OutputWeights();
            for (int i = 0; i < 10; i++)
                Set(ref o, (OutputChannel)i, Get(w, (OutputChannel)i) * value);

            // Own channel: the camera already shows all of it, so the 4D view follows by the unperceived remainder.
            (OutputChannel own, float follow) = Conventional(binding.Signal);
            Set(ref o, own, (1f - Get(w, own)) * follow * value);

            o.XW *= profile.WRotationGain;
            o.YW *= profile.WRotationGain;
            o.ZW *= profile.WRotationGain;
            o.W *= profile.WTranslationGain;
            return o;
        }

        public static float Get(in OutputWeights o, OutputChannel c) => c switch
        {
            OutputChannel.XY => o.XY, OutputChannel.XZ => o.XZ, OutputChannel.YZ => o.YZ,
            OutputChannel.XW => o.XW, OutputChannel.YW => o.YW, OutputChannel.ZW => o.ZW,
            OutputChannel.X => o.X, OutputChannel.Y => o.Y, OutputChannel.Z => o.Z,
            _ => o.W,
        };

        public static void Set(ref OutputWeights o, OutputChannel c, float v)
        {
            switch (c)
            {
                case OutputChannel.XY: o.XY = v; break;
                case OutputChannel.XZ: o.XZ = v; break;
                case OutputChannel.YZ: o.YZ = v; break;
                case OutputChannel.XW: o.XW = v; break;
                case OutputChannel.YW: o.YW = v; break;
                case OutputChannel.ZW: o.ZW = v; break;
                case OutputChannel.X: o.X = v; break;
                case OutputChannel.Y: o.Y = v; break;
                case OutputChannel.Z: o.Z = v; break;
                default: o.W = v; break;
            }
        }
    }

    /// <summary>
    /// SPEC §6's initial profiles, as data over the one engine. Starting points to tune, not conclusions. Rotation
    /// weights on non-own planes in Delta mode are degrees per second per degree of offset.
    /// </summary>
    public static class MappingPresets
    {
        public const string Classic = "CLASSIC";
        public const string HeadTilt = "HEAD_TILT";
        public const string HeadTranslate = "HEAD_TRANSLATE";
        public const string Hybrid = "HYBRID";
        public const string Full4D = "FULL_4D";
        public const string Custom = "CUSTOM";

        public static IReadOnlyList<string> Names { get; } = new[] { Classic, HeadTilt, HeadTranslate, Hybrid, Full4D, Custom };

        public static MappingProfile Create(string name) => name switch
        {
            // HMD purely conventional; ana/kata comes from the keyboard/controller.
            Classic => new MappingProfile { Name = Classic },

            // Hold a tilt to turn through W: pitch steers YW, roll steers XW. Looking around stays normal.
            HeadTilt => Profile(HeadTilt,
                Bind(InputSignal.HeadPitch, MappingMode.Delta, 5f, new OutputWeights { YZ = 1f, YW = 1f }),
                Bind(InputSignal.HeadRoll, MappingMode.Delta, 5f, new OutputWeights { XY = 1f, XW = 1f })),

            // Lean forward / back to glide ana / kata; 0.1 m of lean past the dead zone is 0.15 W per second.
            HeadTranslate => Profile(HeadTranslate,
                Bind(InputSignal.HeadZ, MappingMode.Delta, 0.03f, new OutputWeights { Z = 1f, W = 1.5f })),

            // SPEC §6's own example weights: familiar motion mostly normal, partly leaking into W. Absolute, so it
            // returns when the head does.
            Hybrid => Profile(Hybrid,
                Bind(InputSignal.HeadPitch, MappingMode.Absolute, 0f, new OutputWeights { YZ = 0.7f, YW = 0.3f }),
                Bind(InputSignal.HeadRoll, MappingMode.Absolute, 0f, new OutputWeights { XY = 1f, XW = 0.5f }),
                Bind(InputSignal.HeadZ, MappingMode.Absolute, 0f, new OutputWeights { Z = 0.8f, W = 0.2f })),

            // Every head axis also steers its W partner plane (ARCHITECTURE §4 pairing: pitch-XW, yaw-YW, roll-ZW).
            Full4D => Profile(Full4D,
                Bind(InputSignal.HeadPitch, MappingMode.Delta, 5f, new OutputWeights { YZ = 1f, XW = 1f }),
                Bind(InputSignal.HeadYaw, MappingMode.Delta, 8f, new OutputWeights { XZ = 1f, YW = 1f }),
                Bind(InputSignal.HeadRoll, MappingMode.Delta, 5f, new OutputWeights { XY = 1f, ZW = 1f }),
                Bind(InputSignal.HeadZ, MappingMode.Delta, 0.03f, new OutputWeights { Z = 1f, W = 1.5f })),

            // All six inputs, conventional, for editing into anything.
            _ => Profile(Custom,
                Bind(InputSignal.HeadYaw, MappingMode.Delta, 0f, new OutputWeights { XZ = 1f }),
                Bind(InputSignal.HeadPitch, MappingMode.Delta, 0f, new OutputWeights { YZ = 1f }),
                Bind(InputSignal.HeadRoll, MappingMode.Delta, 0f, new OutputWeights { XY = 1f }),
                Bind(InputSignal.HeadX, MappingMode.Delta, 0f, new OutputWeights { X = 1f }),
                Bind(InputSignal.HeadY, MappingMode.Delta, 0f, new OutputWeights { Y = 1f }),
                Bind(InputSignal.HeadZ, MappingMode.Delta, 0f, new OutputWeights { Z = 1f })),
        };

        private static MappingProfile Profile(string name, params InputBinding[] bindings) =>
            new MappingProfile { Name = name, Bindings = new List<InputBinding>(bindings) };

        private static InputBinding Bind(InputSignal signal, MappingMode mode, float deadZone, OutputWeights weights) =>
            new InputBinding
            {
                Signal = signal,
                Mode = mode,
                DeadZone = deadZone,
                CurveReference = signal >= InputSignal.HeadX ? 0.2f : 30f,
                Weights = weights,
            };
    }
}
