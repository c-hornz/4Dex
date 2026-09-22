using System.Collections.Generic;
using System.Globalization;
using FourDex.InputMapping;
using UnityEngine;

namespace FourDex.Runtime
{
    /// <summary>
    /// SPEC §21's mapping entries: profile, comfort switches, W gains, and the head weights SPEC §5/§6's examples use.
    /// Every entry reads the <i>active</i> profile when shown or adjusted, so switching profile retargets them all.
    /// The full configuration of every binding stays in the profile asset's inspector (the CUSTOM profile).
    /// </summary>
    public static class MappingParameterList
    {
        public static List<ParameterEntry> For(HeadMappingDriver d)
        {
            var entries = new List<ParameterEntry>
            {
                new ParameterEntry("map: profile", () => d.ActiveName, (steps, _) => d.SetProfile(d.ActiveIndex + steps)),
                ParameterEntry.Toggle("map: HMD mapping", () => d.MappingEnabled, v => d.MappingEnabled = v),
                ParameterEntry.Toggle("map: freeze 4D view", () => d.Frozen, v => d.Frozen = v),
                ProfileFloat(d, "map: W rotation gain", p => p.WRotationGain, (p, v) => p.WRotationGain = v, 0.05f, 0f, 3f),
                ProfileFloat(d, "map: W move gain", p => p.WTranslationGain, (p, v) => p.WTranslationGain = v, 0.05f, 0f, 3f),
            };

            // SPEC's worked examples: pitch -> YZ/YW, roll -> XY/XW, lean -> Z/W.
            AddBinding(entries, d, InputSignal.HeadPitch, "pitch", OutputChannel.YZ, OutputChannel.YW);
            AddBinding(entries, d, InputSignal.HeadRoll, "roll", OutputChannel.XY, OutputChannel.XW);
            AddBinding(entries, d, InputSignal.HeadZ, "lean", OutputChannel.Z, OutputChannel.W);
            return entries;
        }

        private static void AddBinding(List<ParameterEntry> entries, HeadMappingDriver d, InputSignal signal, string name,
            OutputChannel own, OutputChannel partner)
        {
            entries.Add(new ParameterEntry($"map: {name} mode",
                () => Find(d, signal)?.Mode.ToString() ?? "-",
                (steps, _) =>
                {
                    InputBinding b = GetOrAdd(d, signal);
                    b.Mode = b.Mode == MappingMode.Delta ? MappingMode.Absolute : MappingMode.Delta;
                    Changed(d);
                }));
            entries.Add(Weight(d, signal, $"map: {name} -> {own} (seen)", own));
            entries.Add(Weight(d, signal, $"map: {name} -> {partner}", partner));
            entries.Add(new ParameterEntry($"map: {name} dead zone",
                () => Find(d, signal) is InputBinding b ? b.DeadZone.ToString("0.00", CultureInfo.InvariantCulture) : "-",
                (steps, coarse) =>
                {
                    InputBinding b = GetOrAdd(d, signal);
                    float step = (signal >= InputSignal.HeadX ? 0.005f : 0.5f) * (coarse ? 10f : 1f);
                    b.DeadZone = Mathf.Max(0f, b.DeadZone + steps * step);
                    Changed(d);
                }));
        }

        private static ParameterEntry Weight(HeadMappingDriver d, InputSignal signal, string label, OutputChannel channel) =>
            new ParameterEntry(label,
                () => Find(d, signal) is InputBinding b
                    ? MappingEngine.Get(b.Weights, channel).ToString("0.00", CultureInfo.InvariantCulture)
                    : "-",
                (steps, coarse) =>
                {
                    InputBinding b = GetOrAdd(d, signal);
                    OutputWeights w = b.Weights;
                    float v = Mathf.Clamp(MappingEngine.Get(w, channel) + steps * 0.05f * (coarse ? 10f : 1f), -5f, 5f);
                    MappingEngine.Set(ref w, channel, v);
                    b.Weights = w;
                    Changed(d);
                });

        private static ParameterEntry ProfileFloat(HeadMappingDriver d, string label, System.Func<MappingProfile, float> get,
            System.Action<MappingProfile, float> set, float step, float min, float max) =>
            new ParameterEntry(label,
                () => d.Active != null ? get(d.Active).ToString("0.00", CultureInfo.InvariantCulture) : "-",
                (steps, coarse) =>
                {
                    if (d.Active == null)
                        return;
                    set(d.Active, Mathf.Clamp(get(d.Active) + steps * step * (coarse ? 10f : 1f), min, max));
                    Changed(d);
                });

        private static InputBinding? Find(HeadMappingDriver d, InputSignal signal) => d.Active?.Find(signal);

        // Adjusting a weight the active profile has no binding for adds one, conventional (own weight 1) to start.
        private static InputBinding GetOrAdd(HeadMappingDriver d, InputSignal signal)
        {
            InputBinding? b = Find(d, signal);
            if (b != null || d.Active == null)
                return b ?? new InputBinding { Signal = signal };
            b = new InputBinding { Signal = signal, CurveReference = signal >= InputSignal.HeadX ? 0.2f : 30f };
            OutputWeights w = default;
            MappingEngine.Set(ref w, MappingEngine.Conventional(signal).Channel, 1f);
            b.Weights = w;
            d.Active.Bindings.Add(b);
            return b;
        }

        private static void Changed(HeadMappingDriver d)
        {
#if UNITY_EDITOR
            if (d.ActiveAsset != null)
                UnityEditor.EditorUtility.SetDirty(d.ActiveAsset);
#endif
        }
    }
}
