using System.Collections.Generic;
using FourDex.Player;

namespace FourDex.Runtime
{
    /// <summary>
    /// SPEC §22's comfort controls in the runtime panel, so they can be changed from inside the headset: that is
    /// where you find out a mapping is uncomfortable, and taking the headset off to fix it defeats the purpose.
    /// </summary>
    public static class ComfortParameterList
    {
        public static List<ParameterEntry> For(ComfortSettings s)
        {
            ComfortParameters P() => s.Parameters;
            void Changed()
            {
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(s);
#endif
            }

            return new List<ParameterEntry>
            {
                ParameterEntry.Toggle("cmf: vignette", () => P().VignetteEnabled, v => { P().VignetteEnabled = v; Changed(); }),
                ParameterEntry.Float("cmf: vignette strength", () => P().VignetteStrength, v => { P().VignetteStrength = v; Changed(); }, 0.05f, 0f, 1f),
                ParameterEntry.Float("cmf: walk reference", () => P().SpeedReference, v => { P().SpeedReference = v; Changed(); }, 0.1f, 0.1f, 10f),
                ParameterEntry.Float("cmf: turn reference", () => P().TurnReference, v => { P().TurnReference = v; Changed(); }, 5f, 5f, 360f, "0"),
                ParameterEntry.Float("cmf: W rate reference", () => P().WRateReference, v => { P().WRateReference = v; Changed(); }, 0.1f, 0.1f, 10f),
                ParameterEntry.Float("cmf: vignette response s", () => P().ResponseSeconds, v => { P().ResponseSeconds = v; Changed(); }, 0.05f, 0f, 2f),
                ParameterEntry.Cycle("cmf: turn mode", () => P().TurnMode, v => { P().TurnMode = v; Changed(); }),
                ParameterEntry.Float("cmf: snap degrees", () => P().SnapDegrees, v => { P().SnapDegrees = v; Changed(); }, 5f, 5f, 90f, "0"),
            };
        }
    }
}
