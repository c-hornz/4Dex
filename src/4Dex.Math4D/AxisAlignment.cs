using System;
using System.Collections.Generic;
using System.Text;

namespace FourDex.Math4D
{
    /// <summary>
    /// The axis-aligned orientations: every rotation that sends each axis onto some world axis, signed. A viewer in
    /// one of these sees whole world axes as their right/up/forward/ana, which is what makes a 4D orientation
    /// nameable, and so recoverable after turning through W.
    /// </summary>
    /// <remarks>
    /// These are the rotational symmetries of the tesseract: signed 4x4 permutation matrices with determinant +1,
    /// 4! x 2^4 / 2 = 192 of them. They are generated here by closing the identity under 90 degree turns in the six
    /// coordinate planes, so they come out as rotors directly and no matrix-to-rotor conversion is needed.
    /// </remarks>
    public static class AxisAlignment
    {
        private static readonly string[] AxisNames = { "X", "Y", "Z", "W" };

        private static Rotor4[]? _all;

        /// <summary>All 192 axis-aligned orientations, the identity first.</summary>
        public static IReadOnlyList<Rotor4> All => _all ??= Generate();

        private static Rotor4[] Generate()
        {
            var found = new List<Rotor4> { Rotor4.Identity };
            var quarterTurns = new List<Rotor4>();
            foreach (Plane4 plane in Plane4Extensions.All)
                quarterTurns.Add(Rotor4.FromPlane(plane, MathF.PI / 2f));

            // Breadth-first closure. Quarter turns in the coordinate planes generate the whole group.
            for (int next = 0; next < found.Count; next++)
            {
                foreach (Rotor4 turn in quarterTurns)
                {
                    Rotor4 candidate = turn.Then(found[next]);
                    if (!found.Exists(r => r.ApproxEquals(candidate, 1e-4f)))
                        found.Add(candidate);
                }
            }
            return found.ToArray();
        }

        /// <summary>The axis-aligned orientation closest to <paramref name="orientation"/>.</summary>
        /// <remarks>
        /// Closest means the largest trace of <c>Aᵀ B</c>: the sum over axes of how well each of the viewer's axes
        /// already points where it would after aligning. That is the standard nearest-rotation measure.
        /// </remarks>
        public static Rotor4 Nearest(Rotor4 orientation)
        {
            Basis4 current = orientation.ToBasis();
            Rotor4 best = Rotor4.Identity;
            float bestScore = float.NegativeInfinity;
            foreach (Rotor4 candidate in All)
            {
                Basis4 b = candidate.ToBasis();
                float score = Vec4.Dot(current.X, b.X) + Vec4.Dot(current.Y, b.Y) +
                              Vec4.Dot(current.Z, b.Z) + Vec4.Dot(current.W, b.W);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best;
        }

        /// <summary>
        /// The turn, in the viewer's own planes, that takes <paramref name="from"/> to <paramref name="to"/>:
        /// <c>from</c> rotated locally by <c>exp(result)</c> is <c>to</c>.
        /// </summary>
        public static Bivector4 LocalTurn(Rotor4 from, Rotor4 to) => to.Then(from.Inverse()).Log();

        /// <summary>Total turn angle in radians between two orientations: the length of the turn between them.</summary>
        public static float Angle(Rotor4 from, Rotor4 to)
        {
            Bivector4 b = LocalTurn(from, to);
            return MathF.Sqrt(b.XY * b.XY + b.XZ * b.XZ + b.YZ * b.YZ + b.XW * b.XW + b.YW * b.YW + b.ZW * b.ZW);
        }

        /// <summary>
        /// Where each of the viewer's axes points in world terms, e.g. <c>"right +X  up +Y  fwd +W  ana -Z"</c>.
        /// Each axis is named by the world axis it is closest to, so it is exact only for an aligned orientation.
        /// </summary>
        public static string Describe(Rotor4 orientation)
        {
            Basis4 b = orientation.ToBasis();
            var sb = new StringBuilder();
            sb.Append("right ").Append(Nearest(b.X))
              .Append("  up ").Append(Nearest(b.Y))
              .Append("  fwd ").Append(Nearest(b.Z))
              .Append("  ana ").Append(Nearest(b.W));
            return sb.ToString();
        }

        private static string Nearest(Vec4 v)
        {
            int axis = 0;
            for (int i = 1; i < 4; i++)
            {
                if (MathF.Abs(v[i]) > MathF.Abs(v[axis]))
                    axis = i;
            }
            return (v[axis] >= 0f ? "+" : "-") + AxisNames[axis];
        }
    }
}
