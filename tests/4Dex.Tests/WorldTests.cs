using System;
using System.Linq;
using FourDex.Math4D;
using FourDex.World;
using Xunit;
using static FourDex.Tests.TestMath;

namespace FourDex.Tests
{
    public class WorldTests
    {
        private static readonly Vec4 Cube = new Vec4(0.5f, 0.5f, 0.5f, 0.5f);

        [Fact]
        public void Add_AssignsUniqueIds_ThatAreNotReusedAfterRemove()
        {
            var world = new World4D();
            Object4D a = world.Add("A", Transform4.Identity, Cube);
            Object4D b = world.Add("B", Transform4.Identity, Cube);
            Assert.NotEqual(a.Id, b.Id);

            Assert.True(world.Remove(a.Id));
            Assert.False(world.Remove(a.Id));
            Object4D c = world.Add("C", Transform4.Identity, Cube);

            Assert.NotEqual(a.Id, c.Id);
            Assert.Equal(2, world.Count);
            Assert.False(world.TryGet(a.Id, out _));
            Assert.True(world.TryGet(c.Id, out Object4D found));
            Assert.Same(c, found);
        }

        [Fact]
        public void Worlds_AreIndependent()
        {
            var first = new World4D();
            var second = new World4D();
            first.Add("A", Transform4.Identity, Cube);
            Assert.Equal(0, second.Count);
        }

        [Fact]
        public void NegativeHalfExtents_AreRejected()
        {
            var world = new World4D();
            Assert.Throws<ArgumentOutOfRangeException>(() => world.Add("bad", Transform4.Identity, new Vec4(1f, 1f, 1f, -0.1f)));
            Object4D ok = world.Add("ok", Transform4.Identity, Cube);
            Assert.Throws<ArgumentOutOfRangeException>(() => ok.HalfExtents = new Vec4(-1f, 0f, 0f, 0f));
            Assert.Equal(1, world.Count);
        }

        [Fact]
        public void UnrotatedBounds_AreTheLocalBox()
        {
            var world = new World4D();
            var position = new Vec4(1f, 2f, 3f, -1f);
            var half = new Vec4(1f, 0.5f, 0.25f, 0.1f);
            Object4D obj = world.Add("slab", new Transform4(position, Rotor4.Identity), half);

            Bounds4 bounds = obj.WorldBounds();
            AssertApprox(position, bounds.Center);
            AssertApprox(half, bounds.HalfExtents);
        }

        [Fact]
        public void QuarterTurnInXW_TradesXExtentForWThickness()
        {
            // A long, W-thin slab turned 90 degrees through ana becomes X-thin and W-long: rotating toward W
            // really does move geometry into W rather than being a cosmetic effect (SPEC §1).
            var world = new World4D();
            var half = new Vec4(1f, 0.5f, 0.25f, 0.1f);
            var rotation = Rotor4.FromPlane(Plane4.XW, MathF.PI / 2f);
            Object4D obj = world.Add("slab", new Transform4(Vec4.Zero, rotation), half);

            AssertApprox(new Vec4(0.1f, 0.5f, 0.25f, 1f), obj.WorldBounds().HalfExtents);
        }

        [Fact]
        public void RotatedBounds_ContainEveryCornerOfTheBox()
        {
            var rng = new Random(60);
            var world = new World4D();
            for (int trial = 0; trial < 100; trial++)
            {
                var half = new Vec4(
                    RandomRange(rng, 0f, 2f), RandomRange(rng, 0f, 2f), RandomRange(rng, 0f, 2f), RandomRange(rng, 0f, 2f));
                var transform = new Transform4(RandomVec4(rng), RandomRotor(rng));
                Object4D obj = world.Add("box", transform, half);
                Bounds4 bounds = obj.WorldBounds();

                // 16 corners of a tesseract; the tight AABB must contain all of them, with at least one on each face.
                var corners = Enumerable.Range(0, 16).Select(bits => transform.TransformPoint(new Vec4(
                    (bits & 1) != 0 ? half.X : -half.X,
                    (bits & 2) != 0 ? half.Y : -half.Y,
                    (bits & 4) != 0 ? half.Z : -half.Z,
                    (bits & 8) != 0 ? half.W : -half.W))).ToList();

                var grown = new Bounds4(bounds.Center, bounds.HalfExtents + new Vec4(1e-4f, 1e-4f, 1e-4f, 1e-4f));
                Assert.All(corners, corner => Assert.True(grown.Contains(corner), $"{corner} outside {bounds}"));
                for (int axis = 0; axis < 4; axis++)
                {
                    float reach = corners.Max(corner => MathF.Abs(corner[axis] - bounds.Center[axis]));
                    AssertApprox(bounds.HalfExtents[axis], reach, 1e-4f);
                }
            }
        }

        [Fact]
        public void ObjectsSeparatedOnlyInW_DoNotOverlap()
        {
            // SPEC §23 lab layout: coincident in XYZ, one unit apart in W. They must not collide.
            var world = new World4D();
            Object4D a = world.Add("A", new Transform4(Vec4.Zero, Rotor4.Identity), new Vec4(0.5f, 0.5f, 0.5f, 0.2f));
            Object4D b = world.Add("B", new Transform4(new Vec4(0f, 0f, 0f, 1f), Rotor4.Identity), new Vec4(0.5f, 0.5f, 0.5f, 0.2f));
            Object4D c = world.Add("C", new Transform4(new Vec4(0f, 0f, 0f, -1f), Rotor4.Identity), new Vec4(0.5f, 0.5f, 0.5f, 0.2f));

            Assert.False(Collision4D.MayOverlap(a, b));
            Assert.False(Collision4D.MayOverlap(a, c));

            // Thicken A through W until it reaches B and C.
            a.HalfExtents = new Vec4(0.5f, 0.5f, 0.5f, 0.9f);
            Assert.True(Collision4D.MayOverlap(a, b));
            Assert.True(Collision4D.MayOverlap(a, c));
            Assert.Equal(new[] { b }, Collision4D.ObjectsAt(world, new Vec4(0f, 0f, 0f, 1.1f)));
            Assert.Equal(new[] { "A", "B" }, Collision4D.ObjectsAt(world, new Vec4(0f, 0f, 0f, 0.85f)).Select(o => o.Name).OrderBy(n => n));
        }
    }
}
