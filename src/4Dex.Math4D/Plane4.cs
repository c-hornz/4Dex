using System;
using System.Collections.Generic;

namespace FourDex.Math4D
{
    /// <summary>
    /// The six coordinate rotation planes of 4D space (SPEC §4).
    /// </summary>
    /// <remarks>
    /// Sign convention, used everywhere in Math4D: rotating plane <c>AB</c> by <c>+t</c>
    /// turns axis A toward axis B, i.e. <c>e_A -> cos(t) e_A + sin(t) e_B</c>.
    /// XW, YW and ZW are the ana/kata planes: they rotate the viewed 3-space through W.
    /// </remarks>
    public enum Plane4
    {
        XY,
        XZ,
        YZ,
        XW,
        YW,
        ZW,
    }

    public static class Plane4Extensions
    {
        public static IReadOnlyList<Plane4> All { get; } = new[]
        {
            Plane4.XY, Plane4.XZ, Plane4.YZ, Plane4.XW, Plane4.YW, Plane4.ZW,
        };

        /// <summary>Axis indices (0..3 = X, Y, Z, W). A <c>+t</c> rotation turns <c>From</c> toward <c>To</c>.</summary>
        public static (int From, int To) Axes(this Plane4 plane) => plane switch
        {
            Plane4.XY => (0, 1),
            Plane4.XZ => (0, 2),
            Plane4.YZ => (1, 2),
            Plane4.XW => (0, 3),
            Plane4.YW => (1, 3),
            Plane4.ZW => (2, 3),
            _ => throw new ArgumentOutOfRangeException(nameof(plane), plane, null),
        };

        /// <summary>True for XW, YW and ZW — the planes that rotate through ana/kata.</summary>
        public static bool InvolvesW(this Plane4 plane) =>
            plane == Plane4.XW || plane == Plane4.YW || plane == Plane4.ZW;
    }
}
