using System;

namespace FourDex.Runtime
{
    /// <summary>
    /// Maps viewer-frame W onto the slice viewer's horizontal axis: 0 is the left edge (kata), 1 the right (ana),
    /// and the player's own W sits at 0.5. Pure, so the chart's geometry is tested apart from its drawing.
    /// </summary>
    public static class WSliceLayout
    {
        /// <summary>An interval on the axis, in 0..1, and whether it ran off either end.</summary>
        public readonly struct Span
        {
            public Span(float start, float end, bool clippedKata, bool clippedAna)
            {
                Start = start;
                End = end;
                ClippedKata = clippedKata;
                ClippedAna = clippedAna;
            }

            public readonly float Start;
            public readonly float End;
            public readonly bool ClippedKata;
            public readonly bool ClippedAna;
        }

        public static float ToUnit(float w, float halfRange) => Clamp01(0.5f + 0.5f * w / MathF.Max(halfRange, 1e-4f));

        /// <summary><c>[center - halfExtent, center + halfExtent]</c> on the axis, clipped to the visible range.</summary>
        public static Span Interval(float center, float halfExtent, float halfRange)
        {
            float lo = center - MathF.Max(halfExtent, 0f);
            float hi = center + MathF.Max(halfExtent, 0f);
            return new Span(ToUnit(lo, halfRange), ToUnit(hi, halfRange), lo < -halfRange, hi > halfRange);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
