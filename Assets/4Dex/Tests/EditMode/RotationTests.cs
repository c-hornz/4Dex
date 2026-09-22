using FourDex.Math4D;
using FourDex.Player;
using FourDex.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FourDex.Tests.EditMode
{
    /// <summary>
    /// Milestone 3: rotating through W turns the perceived 3D subspace without moving the player in W, and the
    /// rotation monitor reports it.
    /// </summary>
    public class RotationTests
    {
        private GameObject _root;
        private World4DRegistry _registry;
        private Player4D _player;
        private ProjectionDriver _driver;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Lab");
            _registry = _root.AddComponent<World4DRegistry>();
            _player = _root.AddComponent<Player4D>();
            _driver = _root.AddComponent<ProjectionDriver>();
            var so = new SerializedObject(_driver);
            so.FindProperty("_world").objectReferenceValue = _registry;
            so.FindProperty("_player").objectReferenceValue = _player;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        private Object4DBehaviour AddObject(Vector4 position, Vector4 halfExtents, float xyDegrees = 0f)
        {
            var go = new GameObject("Obj");
            go.transform.SetParent(_root.transform, false);
            var obj = go.AddComponent<Object4DBehaviour>();
            var so = new SerializedObject(obj);
            so.FindProperty("_position").vector4Value = position;
            so.FindProperty("_halfExtents").vector4Value = halfExtents;
            so.FindProperty("_rotationDegrees").FindPropertyRelative("XY").floatValue = xyDegrees;
            so.ApplyModifiedPropertiesWithoutUndo();
            _registry.Register(obj);
            return obj;
        }

        private static void AssertNear(Vector3 expected, Vector3 actual, float tolerance = 1e-4f) =>
            Assert.Less(Vector3.Distance(expected, actual), tolerance, $"Expected {expected}, got {actual}");

        [Test]
        public void RotatingThroughXW_TurnsThePerceivedSubspace_WithoutMovingPlayerW()
        {
            // The Milestone 3 acceptance criterion. B sits at x = +0.6, w = +1. After a quarter turn in XW, the
            // player's own X axis points along world +W: B's W offset is now seen as X, and its X offset as -W.
            Object4DBehaviour a = AddObject(new Vector4(0f, 1.45f, 2f, 0f), new Vector4(0.2f, 0.2f, 0.2f, 0.2f));
            Object4DBehaviour b = AddObject(new Vector4(0.6f, 1.45f, 2f, 1f), new Vector4(0.2f, 0.2f, 0.2f, 0.2f));
            float playerW = _player.State.Position.W;

            _player.State.RotateLocal(Plane4.XW, Mathf.PI / 2f);
            _driver.ProjectAll();

            Assert.AreEqual(playerW, _player.State.Position.W, "rotation must not move the player through W");
            AssertNear(new Vector3(1f, 1.45f, 2f), b.transform.position);
            Assert.AreEqual(-0.6f, b.LastProjection.W, 1e-4f);
            AssertNear(new Vector3(0f, 1.45f, 2f), a.transform.position, 1e-4f);
            Assert.AreEqual(0f, a.LastProjection.W, 1e-4f);
        }

        [Test]
        public void FootprintSize_IsExactForOrdinaryRotations()
        {
            Object4DBehaviour diamond = AddObject(new Vector4(0f, 1f, 2f, 0f), new Vector4(0.15f, 0.15f, 0.15f, 0.2f), 45f);
            _player.State.RotateLocal(Plane4.XZ, 0.7f);
            _driver.ProjectAll();
            AssertNear(new Vector3(0.3f, 0.3f, 0.3f), diamond.transform.localScale);
        }

        [Test]
        public void FootprintSize_ShowsWExtentTurnedIntoX()
        {
            // The lab's slab: 1.8 m wide in X and 3 m through W. After the player turns 90 degrees in XW, its W extent
            // lies along the player's X, so it must look 3 m wide, not keep its 1.8 m.
            Object4DBehaviour slab = AddObject(new Vector4(0f, 1f, 2.4f, 0f), new Vector4(0.9f, 0.04f, 0.25f, 1.5f));
            _driver.ProjectAll();
            Assert.AreEqual(1.8f, slab.transform.localScale.x, 1e-4f);

            _player.State.RotateLocal(Plane4.XW, Mathf.PI / 2f);
            _driver.ProjectAll();
            Assert.AreEqual(3f, slab.transform.localScale.x, 1e-3f);
            Assert.AreEqual(0.08f, slab.transform.localScale.y, 1e-4f);
            Assert.AreEqual(0.9f, slab.ViewerHalfExtentW, 1e-4f, "and its X extent now lies along the player's W");
        }

        [Test]
        public void HudReadout_ShowsRotationMonitor()
        {
            var state = new Player4DState();
            var monitor = new RotationMonitor(smoothing: 0f);
            monitor.Sample(state.Orientation, 0.1f);
            state.RotateLocal(Plane4.XW, Mathf.PI / 2f * 0.1f); // 9 degrees in 0.1 s
            monitor.Sample(state.Orientation, 0.1f);

            string text = CoordinateReadout.Format(state, null, null, null, monitor);

            StringAssert.Contains("ROTATION deg", text);
            StringAssert.Contains("XW +9.0", text);
            StringAssert.Contains("RATE deg/s", text);
            StringAssert.Contains("XW +90.0", text);
            StringAssert.Contains("YZ +0.0", text);
        }
    }
}
