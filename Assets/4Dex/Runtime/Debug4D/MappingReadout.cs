using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FourDex.InputMapping;

namespace FourDex.Runtime
{
    /// <summary>
    /// SPEC §20's input mapping monitor: each physical input and the 4D motion it produces, in the spec's own form,
    /// e.g. <c>HeadPitch +12.0 -> YZ -3.6 YW +3.6</c>. Only nonzero outputs are listed, so the line reads as a sentence.
    /// </summary>
    public static class MappingReadout
    {
        public static string Format(string profile, bool enabled, bool frozen, IReadOnlyList<Contribution> contributions)
        {
            var sb = new StringBuilder("MAPPING  ").Append(profile);
            if (!enabled)
                sb.Append("  (HMD mapping off)");
            else if (frozen)
                sb.Append("  (FROZEN)");

            foreach (Contribution c in contributions)
            {
                if (c.Shaped == 0f)
                    continue;
                bool angle = c.Signal <= InputSignal.HeadRoll;
                sb.Append("\n  ").Append(c.Signal).Append(' ').Append(Num(c.Input, angle)).Append(" ->");
                int start = sb.Length;
                for (int i = 0; i < 10; i++)
                {
                    var channel = (OutputChannel)i;
                    float v = MappingEngine.Get(c.Output, channel);
                    if (v > -1e-4f && v < 1e-4f)
                        continue;
                    sb.Append(' ').Append(channel).Append(' ').Append(Num(v, channel <= OutputChannel.ZW));
                }
                if (sb.Length == start)
                    sb.Append(" (ordinary only)");
                else if (c.Mode == MappingMode.Delta)
                    sb.Append("  per s");
            }
            return sb.ToString();
        }

        private static string Num(float v, bool degrees) =>
            v.ToString(degrees ? "+0.0;-0.0;+0.0" : "+0.00;-0.00;+0.00", CultureInfo.InvariantCulture);
    }
}
