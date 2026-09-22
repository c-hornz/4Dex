using System;
using FourDex.Math4D;
using Xunit;
using static FourDex.Tests.TestMath;

namespace FourDex.Tests
{
    public class TransformTests
    {
        [Fact]
        public void TransformPoint_RoundTripsThroughInverse()
        {
            var rng = new Random(20);
            for (int trial = 0; trial < 200; trial++)
            {
                var t = new Transform4(RandomVec4(rng), RandomRotor(rng));
                Vec4 local = RandomVec4(rng);
                AssertApprox(local, t.InverseTransformPoint(t.TransformPoint(local)), 1e-4f);
            }
        }

        [Fact]
        public void TransformPoint_RotatesThenTranslates()
        {
            var t = new Transform4(new Vec4(1f, 2f, 3f, 4f), Rotor4.FromPlane(Plane4.XY, MathF.PI / 2f));
            AssertApprox(new Vec4(1f, 3f, 3f, 4f), t.TransformPoint(Vec4.UnitX));
        }

        [Fact]
        public void Directions_IgnorePosition()
        {
            var t = new Transform4(new Vec4(5f, 5f, 5f, 5f), Rotor4.FromPlane(Plane4.ZW, 0.3f));
            Vec4 d = new Vec4(0f, 1f, 2f, 0f);
            AssertApprox(t.Rotation.Apply(d), t.TransformDirection(d));
            AssertApprox(d, t.InverseTransformDirection(t.TransformDirection(d)));
        }

        [Fact]
        public void Translation_NeverChangesOrientation()
        {
            Rotor4 r = RandomRotor(new Random(21));
            var t = new Transform4(Vec4.Zero, r);
            Assert.True(t.Translated(new Vec4(0f, 0f, 0f, 3f)).Rotation.ApproxEquals(r, 0f));
            Assert.True(t.TranslatedLocal(new Vec4(1f, 2f, 3f, 4f)).Rotation.ApproxEquals(r, 0f));
        }

        [Fact]
        public void Rotation_NeverChangesPosition_IncludingW()
        {
            // SPEC §7B: rotating toward W does not move the player through W.
            var start = new Vec4(1f, -2f, 3f, 0.75f);
            var t = new Transform4(start, Rotor4.Identity);
            foreach (Plane4 plane in Plane4Extensions.All)
            {
                Assert.Equal(start, t.RotatedLocal(Rotor4.FromPlane(plane, 0.9f)).Position);
                Assert.Equal(start, t.RotatedWorld(Rotor4.FromPlane(plane, 0.9f)).Position);
            }
        }

        [Fact]
        public void TranslatedLocal_MovesAlongTheViewersOwnAxes()
        {
            // After a quarter turn in XW, the viewer's "forward along local X" is world ana.
            var t = new Transform4(Vec4.Zero, Rotor4.FromPlane(Plane4.XW, MathF.PI / 2f));
            AssertApprox(new Vec4(0f, 0f, 0f, 2f), t.TranslatedLocal(new Vec4(2f, 0f, 0f, 0f)).Position);
        }

        [Fact]
        public void PureWTranslation_ChangesOnlyW()
        {
            var t = new Transform4(new Vec4(1f, 2f, 3f, 4f), RandomRotor(new Random(22)));
            Vec4 moved = t.Translated(new Vec4(0f, 0f, 0f, -1.5f)).Position;
            Assert.Equal(new Vec4(1f, 2f, 3f, 2.5f), moved);
        }

        [Fact]
        public void RotatedLocal_AppliesInTheViewersFrame_RotatedWorld_InTheWorldFrame()
        {
            Rotor4 orientation = Rotor4.FromPlane(Plane4.XY, MathF.PI / 2f);
            Rotor4 delta = Rotor4.FromPlane(Plane4.XW, MathF.PI / 2f);
            var t = new Transform4(Vec4.Zero, orientation);

            // Local: rotate the viewer's X toward the viewer's W, then orient. Viewer X ends at world W.
            AssertApprox(Vec4.UnitW, t.RotatedLocal(delta).TransformDirection(Vec4.UnitX));
            // World: orient first (X -> world Y), then world XW leaves world Y alone.
            AssertApprox(Vec4.UnitY, t.RotatedWorld(delta).TransformDirection(Vec4.UnitX));
        }
    }
}
