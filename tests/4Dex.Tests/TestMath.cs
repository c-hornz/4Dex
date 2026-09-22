using System;
using FourDex.Math4D;
using Xunit;

namespace FourDex.Tests
{
    /// <summary>Shared helpers: tolerant asserts and an independent reference rotation.</summary>
    internal static class TestMath
    {
        public const float Tolerance = 1e-5f;

        public static void AssertApprox(Vec4 expected, Vec4 actual, float tolerance = Tolerance)
        {
            Assert.True(expected.ApproxEquals(actual, tolerance), $"Expected {expected}, got {actual} (tolerance {tolerance}).");
        }

        public static void AssertApprox(float expected, float actual, float tolerance = Tolerance)
        {
            Assert.True(MathF.Abs(expected - actual) <= tolerance, $"Expected {expected}, got {actual} (tolerance {tolerance}).");
        }

        /// <summary>
        /// A plane rotation written directly as a Givens rotation, with no quaternions involved, so the rotor's
        /// generator table is checked against something derived independently.
        /// </summary>
        public static Vec4 GivensRotate(Vec4 v, Plane4 plane, float angle)
        {
            var (from, to) = plane.Axes();
            float[] c = { v.X, v.Y, v.Z, v.W };
            float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
            float a = c[from], b = c[to];
            c[from] = cos * a - sin * b;
            c[to] = sin * a + cos * b;
            return new Vec4(c[0], c[1], c[2], c[3]);
        }

        public static Vec4 RandomVec4(Random rng, float scale = 10f) => new Vec4(
            RandomRange(rng, -scale, scale),
            RandomRange(rng, -scale, scale),
            RandomRange(rng, -scale, scale),
            RandomRange(rng, -scale, scale));

        public static Bivector4 RandomBivector(Random rng, float maxAngle = MathF.PI) => new Bivector4(
            RandomRange(rng, -maxAngle, maxAngle),
            RandomRange(rng, -maxAngle, maxAngle),
            RandomRange(rng, -maxAngle, maxAngle),
            RandomRange(rng, -maxAngle, maxAngle),
            RandomRange(rng, -maxAngle, maxAngle),
            RandomRange(rng, -maxAngle, maxAngle));

        public static Rotor4 RandomRotor(Random rng) => Rotor4.FromBivector(RandomBivector(rng));

        public static float RandomRange(Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
