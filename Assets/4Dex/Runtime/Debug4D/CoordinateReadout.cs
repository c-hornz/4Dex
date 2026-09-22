using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FourDex.Math4D;
using FourDex.Perception;
using FourDex.Player;
using FourDex.XR;
using UnityEngine;

namespace FourDex.Runtime
{
    /// <summary>
    /// The text of the coordinate HUD (SPEC §20): player x/y/z/w, focus, the four basis vectors, and the raw
    /// head pose. Kept separate from the display so it can be tested and reused, e.g. by a desktop panel.
    /// </summary>
    public static class CoordinateReadout
    {
        // Below this, a basis component or W offset prints as exactly zero rather than as float noise.
        private const float Epsilon = 5e-4f;

        /// <param name="objects">Optional per-object perception state, one line each: the M1 check that focus picks the right object.</param>
        public static string Format(Player4DState? player, HmdPose? head, float? focusWidth = null,
            IEnumerable<(string Name, PerceptionChannels Channels)>? objects = null)
        {
            var sb = new StringBuilder(512);

            if (player == null)
            {
                sb.Append("PLAYER  none\n");
            }
            else
            {
                Vec4 p = player.Position;
                sb.Append("POSITION  x ").Append(Num(p.X))
                  .Append("  y ").Append(Num(p.Y))
                  .Append("  z ").Append(Num(p.Z))
                  .Append("  w ").Append(Num(p.W)).Append('\n');

                sb.Append("FOCUS  focusW ").Append(Num(player.FocusW))
                  .Append(' ').Append(FocusSide(player.FocusW))
                  .Append("   focusWidth ").Append(focusWidth.HasValue ? Num(focusWidth.Value) : "n/a")
                  .Append('\n');

                // Where each of the player's own axes points in world coordinates. W' is "ana" as the player sees it.
                Basis4 b = player.Orientation.ToBasis();
                AppendAxis(sb, "X'", b.X);
                AppendAxis(sb, "Y'", b.Y);
                AppendAxis(sb, "Z'", b.Z);
                AppendAxis(sb, "W'", b.W);
                sb.Append("  orthonormality error ")
                  .Append(b.OrthonormalityError().ToString("0.0e+0", CultureInfo.InvariantCulture)).Append('\n');
            }

            if (objects != null)
            {
                foreach ((string name, PerceptionChannels c) in objects)
                {
                    sb.Append("  ").Append(name.PadRight(3)).Append(" gap ").Append(Num(c.SignedGap))
                      .Append(' ').Append(BandLabel(c.Band))
                      .Append("  V ").Append(c.Visibility.ToString("0.00", CultureInfo.InvariantCulture))
                      .Append('\n');
                }
            }

            if (head is HmdPose h)
            {
                Vector3 e = h.Rotation.eulerAngles;
                sb.Append("HMD  pos ").Append(Num(h.Position.x)).Append(' ').Append(Num(h.Position.y)).Append(' ').Append(Num(h.Position.z))
                  .Append("  yaw ").Append(Deg(e.y)).Append(" pitch ").Append(Deg(e.x)).Append(" roll ").Append(Deg(e.z))
                  .Append(h.IsTracked ? "  tracked" : "  NOT TRACKED");
            }
            else
            {
                sb.Append("HMD  no pose");
            }

            return sb.ToString();
        }

        public static string BandLabel(OcclusionBand band) => band switch
        {
            OcclusionBand.Local => "LOCAL  ",
            OcclusionBand.NearW => "NEAR_W ",
            OcclusionBand.FarW => "FAR_W  ",
            _ => "OUTSIDE",
        };

        /// <summary>Focus is measured along the player's own W: positive is ana of the player.</summary>
        public static string FocusSide(float focusW) =>
            focusW > Epsilon ? "(ana)" : focusW < -Epsilon ? "(kata)" : "(own slice)";

        private static void AppendAxis(StringBuilder sb, string label, Vec4 v) =>
            sb.Append("  ").Append(label).Append(" (")
              .Append(Num(v.X)).Append(' ').Append(Num(v.Y)).Append(' ')
              .Append(Num(v.Z)).Append(' ').Append(Num(v.W)).Append(")\n");

        // Always signed and fixed-width, so ana/kata signs are never lost and columns stay put while values change.
        private static string Num(float v)
        {
            if (Mathf.Abs(v) < Epsilon)
                v = 0f;
            return v.ToString("+0.000;-0.000;+0.000", CultureInfo.InvariantCulture);
        }

        // Euler angles arrive as 0..360; show them as -180..180 so small head turns read as small numbers.
        private static string Deg(float degrees)
        {
            float wrapped = Mathf.DeltaAngle(0f, degrees);
            return wrapped.ToString("+000.0;-000.0;+000.0", CultureInfo.InvariantCulture);
        }
    }
}
