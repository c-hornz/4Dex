using System.Linq;
using FourDex.Perception;
using FourDex.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FourDex.Tests.EditMode
{
    /// <summary>Milestone 1 in the scene: focus selects which object is coherent, without moving anything.</summary>
    public class PerceptionDriverTests
    {
        private GameObject _root;
        private World4DRegistry _registry;
        private Player4D _player;
        private ProjectionDriver _driver;
        private PerceptionSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Lab");
            _registry = _root.AddComponent<World4DRegistry>();
            _player = _root.AddComponent<Player4D>();
            _driver = _root.AddComponent<ProjectionDriver>();
            _settings = ScriptableObject.CreateInstance<PerceptionSettings>();
            var so = new SerializedObject(_driver);
            so.FindProperty("_world").objectReferenceValue = _registry;
            so.FindProperty("_player").objectReferenceValue = _player;
            so.FindProperty("_perception").objectReferenceValue = _settings;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_settings);
        }

        private Object4DBehaviour AddObject(string name, float w, Vector4 halfExtents, Vector3 planeXwDegrees = default)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_root.transform, false);
            var obj = go.AddComponent<Object4DBehaviour>();
            go.AddComponent<PhaseRenderer>();
            var so = new SerializedObject(obj);
            so.FindProperty("_position").vector4Value = new Vector4(0f, 1.5f, 2f, w);
            so.FindProperty("_halfExtents").vector4Value = halfExtents;
            so.FindProperty("_rotationDegrees").FindPropertyRelative("XW").floatValue = planeXwDegrees.x;
            so.ApplyModifiedPropertiesWithoutUndo();
            _registry.Register(obj);
            return obj;
        }

        private static readonly Vector4 Thin = new Vector4(0.2f, 0.2f, 0.2f, 0.2f);

        [Test]
        public void FocusSelectsTheCoherentObject_WithoutMovingAnything()
        {
            // The Milestone 1 acceptance criterion.
            Object4DBehaviour a = AddObject("A", 0f, Thin);
            Object4DBehaviour b = AddObject("B", 1f, Thin);
            _driver.ProjectAll();
            Vector3 aPosition = a.transform.position, bPosition = b.transform.position;

            Assert.AreEqual(1f, a.Channels.Coherence, 1e-6f);
            Assert.Less(b.Channels.Coherence, 0.1f);

            _player.State.SetFocusW(1f);
            _driver.ProjectAll();

            Assert.AreEqual(1f, b.Channels.Coherence, 1e-6f, "focus on w=+1 must make B maximally coherent");
            Assert.Less(a.Channels.Coherence, 0.1f);
            Assert.AreEqual(aPosition, a.transform.position);
            Assert.AreEqual(bPosition, b.transform.position);
            Assert.AreEqual(0f, _player.State.Position.W);
        }

        [Test]
        public void AnaAndKataObjects_GetOppositeSigns()
        {
            Object4DBehaviour ana = AddObject("B", 1f, Thin);
            Object4DBehaviour kata = AddObject("C", -1f, Thin);
            _driver.ProjectAll();

            Assert.Greater(ana.WGap, 0f);
            Assert.Less(kata.WGap, 0f);
            Assert.Greater(ana.Channels.PhaseVelocity, 0f);
            Assert.Less(kata.Channels.PhaseVelocity, 0f);
            Assert.AreEqual(ana.Channels.Density, kata.Channels.Density, 1e-6f);
        }

        [Test]
        public void OutsideBand_HidesTheRenderer_AndComingBackShowsIt()
        {
            Object4DBehaviour far = AddObject("Far", 3f, Thin);
            var renderer = far.GetComponent<Renderer>();

            _driver.ProjectAll();
            Assert.AreEqual(OcclusionBand.Outside, far.Channels.Band);
            Assert.IsFalse(renderer.enabled);

            _player.State.MoveAnaKata(3f);
            _driver.ProjectAll();
            Assert.AreEqual(OcclusionBand.Local, far.Channels.Band);
            Assert.IsTrue(renderer.enabled);
        }

        [Test]
        public void ChannelsReachTheMaterialPropertyBlock()
        {
            Object4DBehaviour b = AddObject("B", 0.5f, Thin);
            _driver.ProjectAll();

            var block = new MaterialPropertyBlock();
            b.GetComponent<Renderer>().GetPropertyBlock(block);
            Assert.AreEqual(b.Channels.Density, block.GetFloat("_Density"), 1e-6f);
            Assert.AreEqual(b.Channels.PhaseVelocity, block.GetFloat("_PhaseVelocity"), 1e-6f);
            Assert.AreEqual(b.Channels.Tint, block.GetFloat("_Tint"), 1e-6f);
        }

        [Test]
        public void WideObject_TurnedIntoW_ReachesAcrossSlices()
        {
            // A long X bar at w = +1, turned 90 degrees in XW: its 1.0 X half-extent now lies along W, so it reaches
            // back to w = 0 and the slice passes through it. Rotation through W really moves extent into W.
            var bar = new Vector4(1f, 0.1f, 0.1f, 0.05f);
            Object4DBehaviour flat = AddObject("Flat", 1f, bar);
            Object4DBehaviour turned = AddObject("Turned", 1f, bar, new Vector3(90f, 0f, 0f));
            _driver.ProjectAll();

            Assert.Greater(flat.WGap, 0.9f);
            Assert.AreEqual(0f, turned.WGap, 1e-4f);
            Assert.AreEqual(OcclusionBand.Local, turned.Channels.Band);
        }

        [Test]
        public void Survey_RevealsHiddenNeighbours_WithSignedCues_AndReleaseRestores()
        {
            // Milestone 5 acceptance: neighbouring W geometry becomes perceptible while signed W cues are preserved.
            Object4DBehaviour ana = AddObject("E", 2f, Thin);
            Object4DBehaviour kata = AddObject("F", -2f, Thin);
            Object4DBehaviour local = AddObject("A", 0f, Thin);
            _driver.ProjectAll();
            Assert.IsFalse(ana.GetComponent<Renderer>().enabled, "outside normal perception");
            Assert.IsFalse(kata.GetComponent<Renderer>().enabled);

            _driver.ForceSurvey(1f);
            _driver.ProjectAll();

            Assert.IsTrue(ana.GetComponent<Renderer>().enabled, "survey must reveal it");
            Assert.IsTrue(kata.GetComponent<Renderer>().enabled);
            Assert.Greater(ana.Channels.Density, 0f);
            Assert.Greater(ana.Channels.PhaseVelocity, 0f, "ana still reads as ana");
            Assert.Less(kata.Channels.PhaseVelocity, 0f, "kata still reads as kata");
            Assert.Less(ana.Channels.Coherence, 0.1f, "an echo, not local: it keeps shimmering");
            Assert.AreEqual(1f, local.Channels.Coherence, 1e-6f, "the local slice is unaffected");
            Assert.AreEqual(0f, _player.State.FocusW, "survey is perception only; focus does not move");

            _driver.ForceSurvey(0f);
            _driver.ProjectAll();
            Assert.IsFalse(ana.GetComponent<Renderer>().enabled, "releasing survey restores normal focus behaviour");
        }

        [Test]
        public void HiddenObjects_StillCastShadows_WithTheirAnaKataSign()
        {
            // Milestone 6 acceptance: an object outside the visible W focus produces a shadow response, and that
            // response follows its signed W distance.
            Object4DBehaviour hiddenAna = AddObject("E", 2f, Thin);
            Object4DBehaviour hiddenKata = AddObject("F", -2f, Thin);
            AddObject("A", 0f, Thin);

            _driver.ProjectAll();

            Assert.AreEqual(OcclusionBand.Outside, hiddenAna.Channels.Band);
            Assert.IsFalse(hiddenAna.GetComponent<Renderer>().enabled, "not rendered");

            var ana = _driver.Occluders.First(o => o.Name == "E");
            var kata = _driver.Occluders.First(o => o.Name == "F");
            var local = _driver.Occluders.First(o => o.Name == "A");
            Assert.Greater(ana.Channels.Strength, 0f, "but it still casts");
            Assert.Greater(ana.Channels.Tint, 0f);
            Assert.Less(kata.Channels.Tint, 0f);
            Assert.AreEqual(ana.Channels.Strength, kata.Channels.Strength, 1e-6f);
            Assert.Greater(local.Channels.Strength, ana.Channels.Strength, "nearer in W casts more strongly");
            Assert.Greater(ana.Channels.Softness, local.Channels.Softness, "and its edges are less certain");
        }

        [Test]
        public void FocusingOnAHiddenOccluder_SharpensItsShadow()
        {
            // SPEC §14: shadows respond to focus, without the player moving.
            AddObject("E", 2f, Thin);
            _driver.ProjectAll();
            var far = _driver.Occluders.First(o => o.Name == "E").Channels;

            _player.State.SetFocusW(2f);
            _driver.ProjectAll();
            var focused = _driver.Occluders.First(o => o.Name == "E").Channels;

            Assert.Greater(focused.Coherence, far.Coherence);
            Assert.Less(focused.Softness, far.Softness);
            Assert.Less(focused.ShimmerHz, far.ShimmerHz);
            Assert.AreEqual(0f, _player.State.Position.W, "focus only; the player has not moved");
        }

        [Test]
        public void WithoutSettings_ObjectsArePlacedButNotPerceptionRendered()
        {
            var so = new SerializedObject(_driver);
            so.FindProperty("_perception").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            Object4DBehaviour far = AddObject("Far", 3f, Thin);

            _driver.ProjectAll();

            Assert.IsTrue(far.GetComponent<Renderer>().enabled);
            Assert.AreEqual(3f - 0.2f, far.WGap, 1e-5f);
        }
    }
}
