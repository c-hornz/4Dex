using System;
using FourDex.Math4D;
using FourDex.Player;
using Xunit;
using static FourDex.Tests.TestMath;

namespace FourDex.Tests
{
    /// <summary>Rotor4.Log (the inverse of FromBivector) and the rotation monitor built on it.</summary>
    public class RotationLogTests
    {
        private static void AssertApprox(Bivector4 expected, Bivector4 actual, float tolerance = 1e-4f)
        {
            foreach (Plane4 plane in Plane4Extensions.All)
                Assert.True(MathF.Abs(expected[plane] - actual[plane]) <= tolerance,
                    $"{plane}: expected {expected[plane]}, got {actual[plane]} (full: {actual})");
        }

        [Fact]
        public void Log_OfIdentity_IsZero() => AssertApprox(Bivector4.Zero, Rotor4.Identity.Log(), 0f);

        [Fact]
        public void Log_OfEverySimplePlaneRotation_RecoversThatPlaneAlone()
        {
            foreach (Plane4 plane in Plane4Extensions.All)
            {
                foreach (float angle in new[] { 0.3f, -1.2f, 2.5f })
                    AssertApprox(Bivector4.FromPlane(plane, angle), Rotor4.FromPlane(plane, angle).Log());
            }
        }

        [Fact]
        public void Log_InvertsFromBivector_ForModerateAngles()
        {
            var rng = new Random(80);
            for (int trial = 0; trial < 500; trial++)
            {
                Bivector4 b = RandomBivector(rng, 0.5f);
                AssertApprox(b, Rotor4.FromBivector(b).Log());
            }
        }

        [Fact]
        public void FromBivectorOfLog_RecoversAnyRotation()
        {
            // Large rotations may have a different log than the one that built them, but it must be the same rotation.
            var rng = new Random(81);
            for (int trial = 0; trial < 500; trial++)
            {
                Rotor4 r = RandomRotor(rng);
                Assert.True(Rotor4.FromBivector(r.Log()).ApproxEquals(r, 1e-4f), $"trial {trial}: {r}");
            }
        }

        [Fact]
        public void Log_IsTheSameForBothCoverSigns()
        {
            Rotor4 r = Rotor4.FromBivector(new Bivector4(0.2f, -0.4f, 0.1f, 0.6f, -0.3f, 0.5f));
            var flipped = new Rotor4(-r.L, -r.R);
            AssertApprox(r.Log(), flipped.Log());
        }

        [Fact]
        public void Monitor_ReportsRateInThePlayersOwnPlanes()
        {
            // Turn 90 degrees in ZW first, then spin in XW at 1 rad/s in the player's own planes. The monitor must
            // report XW 1 rad/s, not the world-frame mixture that turning through ZW first would produce.
            var player = new Player4DState();
            player.RotateLocal(Plane4.ZW, MathF.PI / 2f);
            var monitor = new RotationMonitor(smoothing: 0f);
            monitor.Sample(player.Orientation, 0.1f);

            player.RotateLocal(Plane4.XW, 0.1f);
            monitor.Sample(player.Orientation, 0.1f);

            AssertApprox(Bivector4.FromPlane(Plane4.XW, 1f), monitor.Rate, 1e-3f);
        }

        [Fact]
        public void Monitor_OrientationIsTheLog_AndRateDecaysWhenStill()
        {
            var player = new Player4DState();
            var monitor = new RotationMonitor(smoothing: 0.1f);
            monitor.Sample(player.Orientation, 0.1f);
            player.RotateLocal(Plane4.YW, 0.3f);
            monitor.Sample(player.Orientation, 0.1f);

            AssertApprox(Bivector4.FromPlane(Plane4.YW, 0.3f), monitor.Orientation);
            Assert.True(monitor.Rate.YW > 0f);

            for (int i = 0; i < 50; i++)
                monitor.Sample(player.Orientation, 0.1f);
            Assert.True(MathF.Abs(monitor.Rate.YW) < 1e-4f, $"rate should settle to 0 when still, got {monitor.Rate.YW}");
        }

        [Fact]
        public void Monitor_IgnoresZeroTimeSteps()
        {
            var monitor = new RotationMonitor(0f);
            monitor.Sample(Rotor4.Identity, 0.1f);
            monitor.Sample(Rotor4.FromPlane(Plane4.XY, 0.5f), 0f);
            AssertApprox(Bivector4.Zero, monitor.Rate, 0f);
        }
    }
}
