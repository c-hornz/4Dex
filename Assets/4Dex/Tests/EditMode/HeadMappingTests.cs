using System.Collections.Generic;
using FourDex.InputMapping;
using FourDex.Math4D;
using FourDex.Runtime;
using FourDex.XR;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FourDex.Tests.EditMode
{
    /// <summary>
    /// Milestone 4: a configuration change alone makes head pitch drive ordinary pitch, YW rotation, or a weighted
    /// mix, and the comfort controls (freeze, return to conventional) work (SPEC §6, §22, §24).
    /// </summary>
    public class HeadMappingTests
    {
        private GameObject _root;
        private Player4D _player;
        private HeadMappingDriver _driver;
        private readonly List<Object> _assets = new List<Object>();

        private const float Eye = 1.6f;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Rig");
            _player = _root.AddComponent<Player4D>();
            _driver = _root.AddComponent<HeadMappingDriver>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            foreach (Object a in _assets)
                Object.DestroyImmediate(a);
            _assets.Clear();
        }

        private InputMappingProfile Asset(MappingProfile profile)
        {
            var asset = ScriptableObject.CreateInstance<InputMappingProfile>();
            asset.Profile = profile;
            _assets.Add(asset);
            return asset;
        }

        private void UseProfiles(params MappingProfile[] profiles)
        {
            var so = new SerializedObject(_driver);
            so.FindProperty("_player").objectReferenceValue = _player;
            SerializedProperty list = so.FindProperty("_profiles");
            list.arraySize = profiles.Length;
            for (int i = 0; i < profiles.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = Asset(profiles[i]);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static MappingProfile PitchTo(string name, float yz, float yw) => new MappingProfile
        {
            Name = name,
            Bindings = { new InputBinding { Signal = InputSignal.HeadPitch, Mode = MappingMode.Absolute, Weights = new OutputWeights { YZ = yz, YW = yw } } },
        };

        // Looking up by `degrees` from neutral. Unity pitch is positive looking down.
        private static HmdPose LookingUp(float degrees) =>
            new HmdPose(new Vector3(0f, Eye, 0f), Quaternion.Euler(-degrees, 0f, 0f), true);

        private void Nod(float degrees)
        {
            _driver.Apply(LookingUp(0f), 1f / 60f); // neutral
            _driver.Apply(LookingUp(degrees), 1f / 60f);
        }

        private static void AssertAngles(Bivector4 expectedDegrees, Bivector4 actualRadians, float toleranceDeg = 0.05f)
        {
            foreach (Plane4 plane in Plane4Extensions.All)
                Assert.AreEqual(expectedDegrees[plane], actualRadians[plane] * Mathf.Rad2Deg, toleranceDeg, plane.ToString());
        }

        [Test]
        public void SameHeadPitch_ThreeConfigurations_NoCodeChange()
        {
            // The Milestone 4 acceptance criterion, as one test: only the selected profile changes between the three.
            UseProfiles(
                MappingPresets.Create(MappingPresets.Classic),
                PitchTo("PITCH_TO_YW", yz: 1f, yw: 1f),
                PitchTo("WEIGHTED", yz: 0.7f, yw: 0.3f));

            // 1. Ordinary pitch: the camera shows the nod; the 4D view does not move at all.
            Nod(10f);
            Assert.IsTrue(_player.State.Orientation.ApproxEquals(Rotor4.Identity, 1e-6f));

            // 2. Pitch drives YW rotation, and looking up is still ordinary.
            _player.ResetToStart();
            _driver.SetProfile(1);
            Nod(10f);
            AssertAngles(new Bivector4(0f, 0f, 0f, 0f, 10f, 0f), _player.State.Orientation.Log());

            // 3. Weighted, SPEC §6's example: 7 degrees perceived as pitch (the view follows 3), 3 degrees of YW.
            _player.ResetToStart();
            _driver.SetProfile(2);
            Nod(10f);
            AssertAngles(new Bivector4(0f, 0f, -3f, 0f, 3f, 0f), _player.State.Orientation.Log());
        }

        [Test]
        public void HeadRotation_TurnsAboutTheEye()
        {
            UseProfiles(PitchTo("W", yz: 1f, yw: 1f));
            Vec4 eye = new Vec4(0f, Eye, 0f, 0f);
            Vec4 eyeBefore = _player.State.Pose.TransformPoint(eye);

            Nod(20f);

            Vec4 eyeAfter = _player.State.Pose.TransformPoint(eye);
            Assert.Less((eyeAfter - eyeBefore).Length, 1e-4f, "the eye must stay put in 4D");
        }

        [Test]
        public void Freeze_StopsHeadInput_AndUnfreezingDoesNotJump()
        {
            UseProfiles(PitchTo("W", yz: 1f, yw: 1f));
            _driver.Frozen = true;
            Nod(10f);
            Assert.IsTrue(_player.State.Orientation.ApproxEquals(Rotor4.Identity, 1e-6f));

            _driver.Frozen = false;
            _driver.Apply(LookingUp(10f), 1f / 60f); // head held still: nothing new happens
            Assert.IsTrue(_player.State.Orientation.ApproxEquals(Rotor4.Identity, 1e-6f), "unfreezing must not apply the frozen motion");
            _driver.Apply(LookingUp(12f), 1f / 60f);
            AssertAngles(new Bivector4(0f, 0f, 0f, 0f, 2f, 0f), _player.State.Orientation.Log());
        }

        [Test]
        public void ReturnToConventional_SelectsClassic()
        {
            UseProfiles(PitchTo("W", 1f, 1f), MappingPresets.Create(MappingPresets.Classic));
            _driver.Frozen = true;
            _driver.ReturnToConventional();
            Assert.AreEqual(MappingPresets.Classic, _driver.ActiveName);
            Assert.IsFalse(_driver.Frozen);
        }

        [Test]
        public void Offsets_AreMeasuredFromNeutral_InTheNeutralFacing()
        {
            UseProfiles(MappingPresets.Create(MappingPresets.Classic));
            // Neutral: facing 90 degrees right. Then lean 0.1 m along that facing (tracking +X) and look up 5.
            Quaternion facingRight = Quaternion.Euler(0f, 90f, 0f);
            _driver.Apply(new HmdPose(new Vector3(0f, Eye, 0f), facingRight, true), 0f);

            HeadOffsets o = _driver.Offsets(new HmdPose(new Vector3(0.1f, Eye, 0f), facingRight * Quaternion.Euler(-5f, 0f, 0f), true));

            Assert.AreEqual(0.1f, o.Z, 1e-4f, "lean along the neutral facing reads as forward");
            Assert.AreEqual(0f, o.X, 1e-4f);
            Assert.AreEqual(5f, o.Pitch, 1e-3f);
            Assert.AreEqual(0f, o.Yaw, 1e-3f);
        }

        [Test]
        public void MappingMonitor_ReadsLikeTheSpec()
        {
            var engine = new MappingEngine();
            MappingProfile p = PitchTo("WEIGHTED", 0.7f, 0.3f);
            engine.Step(p, new HeadOffsets { Pitch = 12f }, 0f);

            string text = MappingReadout.Format(p.Name, true, false, engine.Contributions);

            StringAssert.Contains("MAPPING  WEIGHTED", text);
            StringAssert.Contains("HeadPitch +12.0 -> YZ -3.6 YW +3.6", text);
        }
    }
}
