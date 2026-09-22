using System;
using System.Collections.Generic;
using System.Globalization;
using FourDex.Perception;
using UnityEngine;

namespace FourDex.Runtime
{
    /// <summary>One adjustable value on the runtime panel: a label, how to show it, and how to step it.</summary>
    public sealed class ParameterEntry
    {
        private readonly Func<string> _display;
        private readonly Action<int, bool> _adjust;

        public ParameterEntry(string label, Func<string> display, Action<int, bool> adjust)
        {
            Label = label;
            _display = display;
            _adjust = adjust;
        }

        public string Label { get; }

        public string Display => _display();

        /// <param name="steps">+1 / -1 per press.</param>
        /// <param name="coarse">Ten times the step, for sweeping quickly.</param>
        public void Adjust(int steps, bool coarse) => _adjust(steps, coarse);

        public static ParameterEntry Float(string label, Func<float> get, Action<float> set, float step, float min, float max,
            string format = "0.00") =>
            new ParameterEntry(label,
                () => get().ToString(format, CultureInfo.InvariantCulture),
                (steps, coarse) => set(Mathf.Clamp(get() + steps * step * (coarse ? 10f : 1f), min, max)));

        public static ParameterEntry Toggle(string label, Func<bool> get, Action<bool> set) =>
            new ParameterEntry(label, () => get() ? "on" : "off", (steps, _) => { if (steps != 0) set(!get()); });

        public static ParameterEntry Cycle<T>(string label, Func<T> get, Action<T> set) where T : struct, Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            return new ParameterEntry(label, () => get().ToString(), (steps, _) =>
            {
                int i = Array.IndexOf(values, get());
                int n = values.Length;
                set(values[((i + steps) % n + n) % n]);
            });
        }
    }

    /// <summary>
    /// The SPEC §21 experiment parameters that exist so far, bound to a PerceptionSettings asset. Later milestones add
    /// their own (survey width, shadow ripple, mapping gains) by appending entries here.
    /// </summary>
    public static class PerceptionParameterList
    {
        public static List<ParameterEntry> For(PerceptionSettings s)
        {
            // Read s.Parameters on every access, never a copy of the reference: Unity may replace that object when the
            // asset is edited in the inspector, and a stale reference would edit an orphan the scene never reads.
            PerceptionParameters P() => s.Parameters;
            // Every set marks the asset dirty so an edit in play mode is saved with the project: tuned values persist.
            void Changed() => MarkDirty(s);

            return new List<ParameterEntry>
            {
                ParameterEntry.Float("focusWidth", () => P().FocusWidth, v => { P().FocusWidth = v; Changed(); }, 0.01f, 0.01f, 5f),
                ParameterEntry.Cycle("curve", () => P().Curve, v => { P().Curve = v; Changed(); }),
                ParameterEntry.Float("band A (LOCAL)", () => P().BandA, v => { P().BandA = v; Changed(); }, 0.01f, 0f, 5f),
                ParameterEntry.Float("band B (NEAR_W)", () => P().BandB, v => { P().BandB = v; Changed(); }, 0.01f, 0f, 5f),
                ParameterEntry.Float("band C (FAR_W)", () => P().BandC, v => { P().BandC = v; Changed(); }, 0.01f, 0f, 5f),
                ParameterEntry.Float("near min density", () => P().NearMinDensity, v => { P().NearMinDensity = v; Changed(); }, 0.01f, 0f, 1f),
                ParameterEntry.Float("far density", () => P().FarDensity, v => { P().FarDensity = v; Changed(); }, 0.01f, 0f, 1f),
                ParameterEntry.Float("far face fill", () => s.FarFaceFill, v => { s.FarFaceFill = v; Changed(); }, 0.01f, 0f, 1f),
                ParameterEntry.Float("phase base freq", () => P().PhaseBaseFrequency, v => { P().PhaseBaseFrequency = v; Changed(); }, 0.1f, 0f, 20f),
                ParameterEntry.Float("phase freq per W", () => P().PhaseFrequencyPerW, v => { P().PhaseFrequencyPerW = v; Changed(); }, 0.25f, 0f, 40f),
                ParameterEntry.Float("phase velocity", () => P().PhaseVelocity, v => { P().PhaseVelocity = v; Changed(); }, 0.05f, 0f, 5f),
                ParameterEntry.Float("phase amplitude", () => P().PhaseAmplitude, v => { P().PhaseAmplitude = v; Changed(); }, 0.01f, 0f, 1f),
                ParameterEntry.Toggle("invert phase dir", () => P().InvertPhaseDirection, v => { P().InvertPhaseDirection = v; Changed(); }),
                ParameterEntry.Float("tint strength", () => s.TintStrength, v => { s.TintStrength = v; Changed(); }, 0.05f, 0f, 1f),
                ParameterEntry.Float("tint saturation W", () => P().TintSaturationW, v => { P().TintSaturationW = v; Changed(); }, 0.05f, 0.05f, 5f),
                ParameterEntry.Float("survey width", () => P().SurveyWidth, v => { P().SurveyWidth = v; Changed(); }, 0.05f, 0.05f, 5f),
                ParameterEntry.Float("survey band scale", () => P().SurveyBandScale, v => { P().SurveyBandScale = v; Changed(); }, 0.1f, 1f, 6f),
                ParameterEntry.Float("survey ramp s", () => P().SurveyRampSeconds, v => { P().SurveyRampSeconds = v; Changed(); }, 0.05f, 0f, 3f),
                ParameterEntry.Float("max shimmer Hz", () => s.MaxShimmerHz, v => { s.MaxShimmerHz = v; Changed(); }, 1f, 0f, 60f, "0"),
                ParameterEntry.Float("dither cells / m", () => s.DitherCellsPerMetre, v => { s.DitherCellsPerMetre = v; Changed(); }, 5f, 10f, 400f, "0"),
            };
        }

        private static void MarkDirty(PerceptionSettings s)
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(s);
#endif
        }
    }
}
