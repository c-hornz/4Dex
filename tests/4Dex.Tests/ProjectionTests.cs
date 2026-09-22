using System;
using FourDex.Math4D;
using Xunit;
using static FourDex.Tests.TestMath;

namespace FourDex.Tests
{
    public class ProjectionTests
    {
        private static readonly SliceProjection Slice = new SliceProjection();

        private static Vec4 AsVec4(Projected4 p) => new Vec4(p.X, p.Y, p.Z, p.W);

        [Fact]
        public void UnrotatedViewer_ProjectsToOffsetFromViewer()
        {
            var viewer = new Transform4(new Vec4(1f, 2f, 3f, 4f), Rotor4.Identity);
            Projected4 p = Slice.Project(new Vec4(2f, 2f, 5f, 1f), viewer);
            AssertApprox(new Vec4(1f, 0f, 2f, -3f), AsVec4(p));
        }

        [Fact]
        public void SignedW_DistinguishesAnaFromKata()
        {
            var viewer = Transform4.Identity;
            Assert.True(Slice.Project(new Vec4(0f, 0f, 0f, 2f), viewer).W > 0f);
            Assert.True(Slice.Project(new Vec4(0f, 0f, 0f, -2f), viewer).W < 0f);
        }

        [Fact]
        public void DeltaW_IsSignedOffsetFromFocus()
        {
            var p = new Projected4(0f, 0f, 0f, 1.5f);
            Assert.Equal(0f, p.DeltaW(1.5f));
            Assert.Equal(1f, p.DeltaW(0.5f));
            Assert.Equal(-1f, p.DeltaW(2.5f));
        }

        [Fact]
        public void MovingAnaTowardAnObject_DrivesItsWOffsetToZero()
        {
            // SPEC §24 Milestone 2 acceptance, at the math level.
            var obj = new Vec4(0f, 0f, 2f, 3f);
            float previous = float.PositiveInfinity;
            for (float w = 0f; w <= 3f; w += 0.5f)
            {
                var viewer = new Transform4(new Vec4(0f, 0f, 0f, w), Rotor4.Identity);
                float offset = MathF.Abs(Slice.Project(obj, viewer).W);
                Assert.True(offset < previous);
                previous = offset;
            }
            AssertApprox(0f, previous);
        }

        [Fact]
        public void RotatingTowardW_BringsAnaGeometryIntoTheVisibleSlice()
        {
            // SPEC §24 Milestone 3: the perceived 3-space rotates through W while the viewer stays put.
            var obj = new Vec4(0f, 0f, 0f, 5f);   // straight ana of the viewer: invisible along X, Y, Z
            var viewer = new Transform4(Vec4.Zero, Rotor4.Identity);
            AssertApprox(new Vec4(0f, 0f, 0f, 5f), AsVec4(Slice.Project(obj, viewer)));

            Transform4 turned = viewer.RotatedLocal(Rotor4.FromPlane(Plane4.XW, MathF.PI / 2f));
            AssertApprox(new Vec4(5f, 0f, 0f, 0f), AsVec4(Slice.Project(obj, turned)));
            Assert.Equal(0f, turned.Position.W);
        }

        [Fact]
        public void Projection_EqualsDotsWithViewerBasis()
        {
            // ARCHITECTURE.md §5 defines projection as d . basis; check the implementation agrees.
            var rng = new Random(30);
            for (int trial = 0; trial < 200; trial++)
            {
                var viewer = new Transform4(RandomVec4(rng), RandomRotor(rng));
                Vec4 point = RandomVec4(rng);
                Vec4 viaBasis = viewer.Basis.ToLocal(point - viewer.Position);
                AssertApprox(viaBasis, AsVec4(Slice.Project(point, viewer)), 1e-3f);
            }
        }
    }
}
