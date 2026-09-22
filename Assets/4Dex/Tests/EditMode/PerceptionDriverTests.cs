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
