using System;
using System.Linq;
using FourDex.InputMapping;
using FourDex.Math4D;
using FourDex.Player;
using Xunit;
using static FourDex.Tests.TestMath;

namespace FourDex.Tests
{
    /// <summary>SPEC §5/§6: the generalized mapping, its shaping, the perceived-fraction rule, and the presets.</summary>
    public class MappingTests
    {
        private const float Deg = MathF.PI / 180f;

        private static MappingProfile One(InputBinding b) => new MappingProfile { Bindings = { b } };

        private static HeadOffsets Pitch(float degrees) => new HeadOffsets { Pitch = degrees };

        [Fact]
        public void Classic_DoesNothing()
        {
            var engine = new MappingEngine();
            MappingStep s = engine.Step(MappingPresets.Create(MappingPresets.Classic),
                new HeadOffsets { Yaw = 30f, Pitch = 20f, Roll = 10f, Z = 0.2f }, 0.1f);
            Assert.True(s.IsZero);
        }

        [Fact]
        public void OwnPlaneWeightOne_IsOrdinary_SoNoWorldMotion()
        {
            // The camera already shows a full head pitch; YZ 1.0 means "perceive all of it", so the 4D view stays put.
            var b = new InputBinding { Signal = InputSignal.HeadPitch, Mode = MappingMode.Absolute, Weights = new OutputWeights { YZ = 1f } };
            var engine = new MappingEngine();
            engine.Step(One(b), Pitch(0f), 0f);
            Assert.True(engine.Step(One(b), Pitch(10f), 0f).IsZero);
        }

        [Fact]
        public void SpecExample_Pitch70YZ30YW_SplitsAsPerceivedFraction()
        {
            // Look up 10 degrees: the view follows 3 degrees in YZ (so 7 are perceived) and turns 3 degrees in YW.
            // Looking up is YZ negative in Math4D (it turns Z toward Y).
            var b = new InputBinding { Signal = InputSignal.HeadPitch, Mode = MappingMode.Absolute, Weights = new OutputWeights { YZ = 0.7f, YW = 0.3f } };
            var engine = new MappingEngine();
            engine.Step(One(b), Pitch(0f), 0f);

            MappingStep s = engine.Step(One(b), Pitch(10f), 0f);

            AssertApprox(-3f * Deg, s.Rotation.YZ);
            AssertApprox(3f * Deg, s.Rotation.YW);
            Assert.Equal(0f, s.Rotation.XW);
        }

        [Fact]
        public void Absolute_ReturnsWhenTheHeadReturns()
        {
            var b = new InputBinding { Signal = InputSignal.HeadPitch, Mode = MappingMode.Absolute, DeadZone = 2f, Exponent = 1.5f, Weights = new OutputWeights { YZ = 1f, YW = 1f } };
            var engine = new MappingEngine();
            var player = new Player4DState();
            foreach (float pitch in new[] { 0f, 5f, 12f, 25f, 12f, 3f, 0f })
                player.RotateLocal(Rotor4.FromBivector(engine.Step(One(b), Pitch(pitch), 1f / 60f).Rotation));
            Assert.True(player.Orientation.ApproxEquals(Rotor4.Identity, 1e-5f), $"left rotated: {player.Orientation}");
        }

        [Fact]
        public void Delta_Steers_RateTimesTime_AndKeepsTheRotation()
        {
            // HEAD_TILT-style: hold 10 degrees of pitch for one second at weight 1 -> 10 degrees of YW, kept afterwards.
            var b = new InputBinding { Signal = InputSignal.HeadPitch, Mode = MappingMode.Delta, Weights = new OutputWeights { YZ = 1f, YW = 1f } };
            var engine = new MappingEngine();
            float yw = 0f;
            for (int frame = 0; frame < 60; frame++)
                yw += engine.Step(One(b), Pitch(10f), 1f / 60f).Rotation.YW;
            AssertApprox(10f * Deg, yw, 1e-5f);

            Assert.True(engine.Step(One(b), Pitch(0f), 1f / 60f).IsZero, "back at neutral: no more motion");
        }

        [Fact]
        public void Shaping_DeadZone_Curve_Gain_Invert_Clamp()
        {
            var b = new InputBinding { DeadZone = 5f };
            Assert.Equal(0f, b.Shape(4f));
            AssertApprox(1f, b.Shape(6f), 1e-5f); // starts from zero past the dead zone, no jump
            AssertApprox(-1f, b.Shape(-6f), 1e-5f);

            var curve = new InputBinding { Exponent = 2f, CurveReference = 30f };
            AssertApprox(30f, curve.Shape(30f), 1e-4f);        // passes through the reference unchanged
            AssertApprox(7.5f, curve.Shape(15f), 1e-4f);       // softens small motions: 30 * 0.5^2

            var gained = new InputBinding { Gain = 2f, Invert = true, Clamp = 15f };
            AssertApprox(-10f, gained.Shape(5f), 1e-5f);
            AssertApprox(-15f, gained.Shape(20f), 1e-5f);      // clamped
        }

        [Fact]
        public void DisabledBinding_ContributesNothing()
        {
            MappingProfile p = MappingPresets.Create(MappingPresets.HeadTilt);
            foreach (InputBinding b in p.Bindings)
                b.Enabled = false;
            Assert.True(new MappingEngine().Step(p, Pitch(30f), 0.1f).IsZero);
        }

        [Fact]
        public void WRotationGain_ScalesOnlyTheWPlanes()
        {
            var b = new InputBinding { Signal = InputSignal.HeadPitch, Mode = MappingMode.Delta, Weights = new OutputWeights { YZ = 0.5f, YW = 1f } };
            var p = new MappingProfile { Bindings = { b }, WRotationGain = 0.25f };
            MappingStep s = new MappingEngine().Step(p, Pitch(10f), 1f);
            AssertApprox(2.5f * Deg, s.Rotation.YW);
            AssertApprox(-5f * Deg, s.Rotation.YZ); // own-plane remainder is not a W plane
        }

        [Fact]
        public void LeanForward_GlidesAna_WithHeadTranslate()
        {
            MappingProfile p = MappingPresets.Create(MappingPresets.HeadTranslate);
            MappingStep s = new MappingEngine().Step(p, new HeadOffsets { Z = 0.13f }, 1f);
            AssertApprox(1.5f * (0.13f - 0.03f), s.Translation.W, 1e-5f);
            Assert.Equal(0f, s.Translation.Z, 5); // own axis at weight 1: ordinary lean, no extra glide
        }

        [Fact]
        public void Reset_StartsAbsoluteBindingsFromTheCurrentPose_WithoutAJump()
        {
            var b = new InputBinding { Signal = InputSignal.HeadPitch, Mode = MappingMode.Absolute, Weights = new OutputWeights { YW = 1f, YZ = 1f } };
            var engine = new MappingEngine();
            engine.Step(One(b), Pitch(0f), 0f);
            engine.Reset();
            Assert.True(engine.Step(One(b), Pitch(20f), 0f).IsZero, "first step after reset must not jump");
            AssertApprox(1f * Deg, engine.Step(One(b), Pitch(21f), 0f).Rotation.YW);
        }

        [Fact]
        public void Contributions_ReportInputAndOutputs_ForTheMonitor()
        {
            var b = new InputBinding { Signal = InputSignal.HeadPitch, Mode = MappingMode.Absolute, Weights = new OutputWeights { YZ = 0.7f, YW = 0.3f } };
            var engine = new MappingEngine();
            engine.Step(One(b), Pitch(12f), 0f);
            Contribution c = engine.Contributions.Single();
            Assert.Equal(12f, c.Input);
            AssertApprox(3.6f, c.Output.YW, 1e-4f);
            AssertApprox(-3.6f, c.Output.YZ, 1e-4f);
        }

        [Fact]
        public void EveryPresetExists_AndHasItsName()
        {
            foreach (string name in MappingPresets.Names)
                Assert.Equal(name, MappingPresets.Create(name).Name);
            Assert.Empty(MappingPresets.Create(MappingPresets.Classic).Bindings);
            Assert.Equal(6, MappingPresets.Create(MappingPresets.Custom).Bindings.Count);
        }

        [Fact]
        public void RotateAboutEye_KeepsTheEyeFixedIn4D()
        {
            var rng = new Random(90);
            for (int trial = 0; trial < 100; trial++)
            {
                var player = new Player4DState(new Transform4(RandomVec4(rng), RandomRotor(rng)));
                var eye = new Vec4(0.1f, 1.6f, -0.05f, 0f);
                Vec4 eyeBefore = player.Pose.TransformPoint(eye);

                player.RotateLocalAbout(RandomRotor(rng), eye);

                AssertApprox(eyeBefore, player.Pose.TransformPoint(eye), 1e-4f);
            }
        }
    }
}
