using System;
using System.Collections.Generic;
using System.Linq;
using FourDex.Math4D;
using Xunit;
using static FourDex.Tests.TestMath;

namespace FourDex.Tests
{
    public class RotorTests
    {
        public static IEnumerable<object[]> AllPlanes => Plane4Extensions.All.Select(p => new object[] { p });

        [Theory]
        [MemberData(nameof(AllPlanes))]
        public void QuarterTurn_TurnsFirstAxisOntoSecond(Plane4 plane)
        {
            var (from, to) = plane.Axes();
            Rotor4 r = Rotor4.FromPlane(plane, MathF.PI / 2f);

            AssertApprox(Vec4.Axis(to), r.Apply(Vec4.Axis(from)));
            AssertApprox(-Vec4.Axis(from), r.Apply(Vec4.Axis(to)));

            for (int axis = 0; axis < 4; axis++)
            {
                if (axis != from && axis != to)
                    AssertApprox(Vec4.Axis(axis), r.Apply(Vec4.Axis(axis)));
            }
        }

        [Theory]
        [MemberData(nameof(AllPlanes))]
        public void PlaneRotation_MatchesIndependentGivensRotation(Plane4 plane)
        {
            var rng = new Random(1000 + (int)plane);
            for (int trial = 0; trial < 200; trial++)
            {
                float angle = RandomRange(rng, -2f * MathF.PI, 2f * MathF.PI);
                Vec4 v = RandomVec4(rng);
                AssertApprox(GivensRotate(v, plane, angle), Rotor4.FromPlane(plane, angle).Apply(v), 1e-4f);
            }
        }

        [Fact]
        public void Rotation_PreservesLength()
        {
            var rng = new Random(1);
            for (int trial = 0; trial < 500; trial++)
            {
                Vec4 v = RandomVec4(rng);
                AssertApprox(v.Length, RandomRotor(rng).Apply(v).Length, 1e-4f * MathF.Max(1f, v.Length));
            }
        }

        [Fact]
        public void Then_MatchesSequentialApplication()
        {
            var rng = new Random(2);
            for (int trial = 0; trial < 200; trial++)
            {
                Rotor4 first = RandomRotor(rng), second = RandomRotor(rng);
                Vec4 v = RandomVec4(rng);
                Vec4 expected = second.Apply(first.Apply(v));
                AssertApprox(expected, first.Then(second).Apply(v), 1e-4f);
                AssertApprox(expected, (second * first).Apply(v), 1e-4f);
            }
        }

        [Fact]
        public void Composition_IsNotCommutative_ForPlanesSharingAnAxis()
        {
            Rotor4 xy = Rotor4.FromPlane(Plane4.XY, 0.8f);
            Rotor4 xw = Rotor4.FromPlane(Plane4.XW, 0.8f);
            Assert.False(xy.Then(xw).ApproxEquals(xw.Then(xy), 1e-3f));
        }

        [Fact]
        public void Composition_Commutes_ForAbsolutelyPerpendicularPlanes()
        {
            // XY and ZW share no axis — the 4D-only case of two rotations that don't interfere.
            Rotor4 xy = Rotor4.FromPlane(Plane4.XY, 0.8f);
            Rotor4 zw = Rotor4.FromPlane(Plane4.ZW, -1.3f);
            Assert.True(xy.Then(zw).ApproxEquals(zw.Then(xy)));
        }

        [Fact]
        public void SamePlane_AnglesAdd()
        {
            Rotor4 combined = Rotor4.FromPlane(Plane4.YW, 0.4f).Then(Rotor4.FromPlane(Plane4.YW, 0.9f));
            Assert.True(combined.ApproxEquals(Rotor4.FromPlane(Plane4.YW, 1.3f)));
        }

        [Fact]
        public void FullTurn_IsIdentityOnVectors_EvenThoughHalvesAreNegated()
        {
            Rotor4 r = Rotor4.FromPlane(Plane4.XY, 2f * MathF.PI);
            // Each half is exp(pi k) = -1: the double cover in action.
            Assert.True(r.L.ApproxEquals(-Quat.Identity));
            Assert.True(r.ApproxEquals(Rotor4.Identity));
            var v = new Vec4(1f, 2f, 3f, 4f);
            AssertApprox(v, r.Apply(v), 1e-5f);
        }

        [Fact]
        public void ApproxEquals_TreatsNegatedPairAsSameRotation()
        {
            Rotor4 r = RandomRotor(new Random(3));
            var negated = new Rotor4(-r.L, -r.R);
            Assert.True(r.ApproxEquals(negated));
            var v = new Vec4(0.3f, -2f, 5f, 1f);
            AssertApprox(r.Apply(v), negated.Apply(v));
        }

        [Theory]
        [MemberData(nameof(AllPlanes))]
        public void FromBivector_WithOnePlane_EqualsFromPlane(Plane4 plane)
        {
            Assert.True(Rotor4.FromBivector(Bivector4.FromPlane(plane, 1.1f)).ApproxEquals(Rotor4.FromPlane(plane, 1.1f)));
        }

        [Fact]
        public void FromBivector_IsAOneParameterGroup()
        {
            // exp(sB) exp(tB) = exp((s+t)B): what makes per-frame weighted mappings integrate consistently.
            var rng = new Random(4);
            for (int trial = 0; trial < 100; trial++)
            {
                Bivector4 b = RandomBivector(rng, 1f);
                float s = RandomRange(rng, -1f, 1f), t = RandomRange(rng, -1f, 1f);
                Rotor4 stepped = Rotor4.FromBivector(b * s).Then(Rotor4.FromBivector(b * t));
                Assert.True(stepped.ApproxEquals(Rotor4.FromBivector(b * (s + t)), 1e-4f));
            }
        }

        [Fact]
        public void FromBivector_PerpendicularPlanes_EqualsSequentialRotation()
        {
            // For commuting planes, "simultaneous" and "sequential" must agree exactly.
            var b = new Bivector4(0.7f, 0f, 0f, 0f, 0f, -0.4f);  // XY and ZW
            Rotor4 sequential = Rotor4.FromPlane(Plane4.XY, 0.7f).Then(Rotor4.FromPlane(Plane4.ZW, -0.4f));
            Assert.True(Rotor4.FromBivector(b).ApproxEquals(sequential));
        }

        [Fact]
        public void AnaKataPlanes_ArePairedWithOneThreeDPlaneEach()
        {
            // ARCHITECTURE.md §4: each W plane shares a quaternion generator with exactly one 3D plane.
            // Left-only exp(u t) rotates both planes of its pair by t at once (a left-isoclinic rotation).
            float t = 0.6f;
            var cases = new (Quat left, Plane4 threeD, float threeDAngle, Plane4 anaKata, float anaKataAngle)[]
            {
                (Quat.ExpPure(t, 0f, 0f), Plane4.YZ, t, Plane4.XW, -t),
                (Quat.ExpPure(0f, t, 0f), Plane4.XZ, -t, Plane4.YW, -t),
                (Quat.ExpPure(0f, 0f, t), Plane4.XY, t, Plane4.ZW, -t),
            };

            var rng = new Random(5);
            foreach (var c in cases)
            {
                var isoclinic = new Rotor4(c.left, Quat.Identity);
                for (int trial = 0; trial < 20; trial++)
                {
                    Vec4 v = RandomVec4(rng);
                    Vec4 expected = GivensRotate(GivensRotate(v, c.threeD, c.threeDAngle), c.anaKata, c.anaKataAngle);
                    AssertApprox(expected, isoclinic.Apply(v), 1e-4f);
                }
            }
        }
    }
}
