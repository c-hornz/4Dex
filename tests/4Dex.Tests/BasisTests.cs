using System;
using FourDex.Math4D;
using Xunit;
using static FourDex.Tests.TestMath;

namespace FourDex.Tests
{
    public class BasisTests
    {
        [Fact]
        public void IdentityRotor_GivesUnitAxes()
        {
            Basis4 b = Rotor4.Identity.ToBasis();
            for (int axis = 0; axis < 4; axis++)
                AssertApprox(Vec4.Axis(axis), b[axis]);
            Assert.Equal(0f, b.OrthonormalityError());
        }

        [Fact]
        public void Axes_AreWhereTheRotorSendsEachUnitAxis()
        {
            Rotor4 r = RandomRotor(new Random(10));
            Basis4 b = r.ToBasis();
            for (int axis = 0; axis < 4; axis++)
                AssertApprox(r.Apply(Vec4.Axis(axis)), b[axis]);
        }

        [Fact]
        public void ToWorld_MatchesRotorApply_AndToLocalInvertsIt()
        {
            var rng = new Random(11);
            for (int trial = 0; trial < 200; trial++)
            {
                Rotor4 r = RandomRotor(rng);
                Basis4 b = r.ToBasis();
                Vec4 local = RandomVec4(rng);

                Vec4 world = b.ToWorld(local);
                AssertApprox(r.Apply(local), world, 1e-4f);
                AssertApprox(local, b.ToLocal(world), 1e-4f);
                AssertApprox(r.Inverse().Apply(world), b.ToLocal(world), 1e-4f);
            }
        }

        [Fact]
        public void XWQuarterTurn_SendsViewerXAxisToWorldAna()
        {
            Basis4 b = Rotor4.FromPlane(Plane4.XW, MathF.PI / 2f).ToBasis();
            AssertApprox(Vec4.UnitW, b.X);
            AssertApprox(-Vec4.UnitX, b.W);
            AssertApprox(Vec4.UnitY, b.Y);
            AssertApprox(Vec4.UnitZ, b.Z);
        }

        [Fact]
        public void OrthonormalityError_IsNearZero_ForAHealthyBasis()
        {
            Basis4 healthy = RandomRotor(new Random(12)).ToBasis();
            Assert.True(healthy.OrthonormalityError() < 1e-5f, $"error {healthy.OrthonormalityError()}");
        }
    }
}
