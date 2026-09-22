using System;
using FourDex.Math4D;
using Xunit;
using static FourDex.Tests.TestMath;

namespace FourDex.Tests
{
    /// <summary>
    /// The SPEC §25 invariants that belong to the math core. The focusW / position.w invariants
    /// live with the player state once it exists; they are not Math4D's to enforce.
    /// </summary>
    public class InvariantTests
    {
        [Fact]
        public void PureXWRotation_LeavesYAndZUntouched()
        {
            var rng = new Random(40);
            for (int trial = 0; trial < 500; trial++)
            {
                Vec4 v = RandomVec4(rng);
                float angle = RandomRange(rng, -2f * MathF.PI, 2f * MathF.PI);
                Vec4 r = Rotor4.FromPlane(Plane4.XW, angle).Apply(v);

                AssertApprox(v.Y, r.Y, 1e-4f);
                AssertApprox(v.Z, r.Z, 1e-4f);
                // The XW part is a 2D rotation: its own length is conserved.
                AssertApprox(v.X * v.X + v.W * v.W, r.X * r.X + r.W * r.W, 1e-2f);
            }
        }

        [Fact]
        public void RotationThenInverse_RecoversOriginalVector()
        {
            var rng = new Random(41);
            for (int trial = 0; trial < 500; trial++)
            {
                Rotor4 r = RandomRotor(rng);
                Vec4 v = RandomVec4(rng);
                AssertApprox(v, r.Inverse().Apply(r.Apply(v)), 1e-4f);
                Assert.True(r.Then(r.Inverse()).ApproxEquals(Rotor4.Identity, 1e-5f));
            }
        }

        [Fact]
        public void Basis_StaysOrthonormal_After100kRandomCompositions()
        {
            // Small per-step rotations in every plane, like continuous head-driven input for ~30 min at 60 Hz.
            var rng = new Random(42);
            Rotor4 orientation = Rotor4.Identity;
            for (int step = 0; step < 100_000; step++)
                orientation = orientation.Then(Rotor4.FromBivector(RandomBivector(rng, 0.05f)));

            float error = orientation.ToBasis().OrthonormalityError();
            Assert.True(error < 1e-5f, $"Orthonormality error after 1e5 compositions: {error}");
            AssertApprox(1f, orientation.L.Norm);
            AssertApprox(1f, orientation.R.Norm);
        }
    }
}
