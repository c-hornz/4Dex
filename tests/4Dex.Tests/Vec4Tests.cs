using System;
using FourDex.Math4D;
using Xunit;
using static FourDex.Tests.TestMath;

namespace FourDex.Tests
{
    public class Vec4Tests
    {
        [Fact]
        public void Arithmetic_IsComponentWise()
        {
            var a = new Vec4(1f, 2f, 3f, 4f);
            var b = new Vec4(-5f, 6f, 0.5f, -2f);

            Assert.Equal(new Vec4(-4f, 8f, 3.5f, 2f), a + b);
            Assert.Equal(new Vec4(6f, -4f, 2.5f, 6f), a - b);
            Assert.Equal(new Vec4(2f, 4f, 6f, 8f), a * 2f);
            Assert.Equal(new Vec4(2f, 4f, 6f, 8f), 2f * a);
            Assert.Equal(new Vec4(0.5f, 1f, 1.5f, 2f), a / 2f);
            Assert.Equal(new Vec4(-1f, -2f, -3f, -4f), -a);
        }

        [Fact]
        public void Dot_IncludesW()
        {
            var a = new Vec4(1f, 2f, 3f, 4f);
            var b = new Vec4(2f, 0f, -1f, 5f);
            Assert.Equal(1f * 2f + 2f * 0f + 3f * -1f + 4f * 5f, Vec4.Dot(a, b));
            Assert.Equal(0f, Vec4.Dot(Vec4.UnitX, Vec4.UnitW));
        }

        [Fact]
        public void Length_IsFourDimensionalEuclidean()
        {
            Assert.Equal(5f, new Vec4(0f, 3f, 0f, 4f).Length);
            Assert.Equal(2f, new Vec4(1f, 1f, 1f, 1f).Length);
            Assert.Equal(Vec4.Distance(Vec4.Zero, new Vec4(0f, 0f, 3f, 4f)), 5f);
        }

        [Fact]
        public void Normalized_HasUnitLengthAndSameDirection()
        {
            var v = new Vec4(3f, -1f, 7f, 2f);
            Vec4 n = v.Normalized();
            AssertApprox(1f, n.Length);
            AssertApprox(v.Length, Vec4.Dot(v, n));
        }

        [Fact]
        public void Normalized_ZeroVector_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => Vec4.Zero.Normalized());
        }

        [Fact]
        public void Indexer_AndAxis_FollowXYZWOrder()
        {
            var v = new Vec4(10f, 20f, 30f, 40f);
            for (int axis = 0; axis < 4; axis++)
            {
                Assert.Equal(10f * (axis + 1), v[axis]);
                Assert.Equal(1f, Vec4.Axis(axis)[axis]);
                Assert.Equal(1f, Vec4.Axis(axis).Length);
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => v[4]);
        }

        [Fact]
        public void WithW_ChangesOnlyW()
        {
            var v = new Vec4(1f, 2f, 3f, 4f);
            Assert.Equal(new Vec4(1f, 2f, 3f, -9f), v.WithW(-9f));
        }
    }

    public class QuatTests
    {
        private static readonly Quat I = new Quat(0f, 1f, 0f, 0f);
        private static readonly Quat J = new Quat(0f, 0f, 1f, 0f);
        private static readonly Quat K = new Quat(0f, 0f, 0f, 1f);
        private static readonly Quat MinusOne = new Quat(-1f, 0f, 0f, 0f);

        [Fact]
        public void HamiltonProduct_FollowsIJK()
        {
            Assert.Equal(K, I * J);
            Assert.Equal(I, J * K);
            Assert.Equal(J, K * I);
            Assert.Equal(-K, J * I);
            Assert.Equal(MinusOne, I * I);
            Assert.Equal(MinusOne, J * J);
            Assert.Equal(MinusOne, K * K);
        }

        [Fact]
        public void Vec4Embedding_PutsWInRealPart()
        {
            Quat q = Quat.FromVec4(new Vec4(1f, 2f, 3f, 4f));
            Assert.Equal(new Quat(4f, 1f, 2f, 3f), q);
            Assert.Equal(new Vec4(1f, 2f, 3f, 4f), q.ToVec4());
        }

        [Fact]
        public void ExpPure_IsUnitAndMatchesClosedForm()
        {
            Quat q = Quat.ExpPure(0.3f, -0.4f, 0f);  // |v| = 0.5
            AssertApprox(1f, q.Norm);
            AssertApprox(MathF.Cos(0.5f), q.W);
            AssertApprox(0.6f * MathF.Sin(0.5f), q.X);
            AssertApprox(-0.8f * MathF.Sin(0.5f), q.Y);
        }

        [Fact]
        public void ExpPure_TinyAngle_IsFiniteAndNearIdentity()
        {
            Quat q = Quat.ExpPure(1e-7f, 0f, 0f);
            Assert.True(float.IsFinite(q.X));
            Assert.True(q.ApproxEquals(Quat.Identity, 1e-6f));
            Assert.True(Quat.ExpPure(0f, 0f, 0f).ApproxEquals(Quat.Identity, 0f));
        }

        [Fact]
        public void UnitQuaternion_TimesConjugate_IsIdentity()
        {
            Quat q = Quat.ExpPure(0.7f, 0.2f, -1.1f);
            Assert.True((q * q.Conjugate()).ApproxEquals(Quat.Identity));
        }
    }
}
