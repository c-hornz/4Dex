using FourDex.Math4D;
using FourDex.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FourDex.Tests.EditMode
{
    /// <summary>
    /// The scene wrappers end to end: authored 4D objects register into the world, and the projection places
    /// them in the player's slice with the right signed DeltaW. OnEnable and LateUpdate don't run in edit mode,
    /// so the tests call Register and ProjectAll directly.
    /// </summary>
    public class ProjectionDriverTests
    {
        private GameObject _root;
        private World4DRegistry _registry;
        private Player4D _player;
        private ProjectionDriver _driver;
        private Transform _viewSpace;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Lab");
            _registry = _root.AddComponent<World4DRegistry>();
            _player = _root.AddComponent<Player4D>();
            _viewSpace = new GameObject("ViewSpace").transform;
            _viewSpace.SetParent(_root.transform, false);
            _driver = _root.AddComponent<ProjectionDriver>();
            Set(_driver, "_world", _registry);
            Set(_driver, "_player", _player);
            Set(_driver, "_viewSpace", _viewSpace);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        private Object4DBehaviour AddObject(string name, Vector4 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var obj = go.AddComponent<Object4DBehaviour>();
            Set(obj, "_position", position);
            _registry.Register(obj);
            return obj;
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, Vector4 value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).vector4Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssertNear(Vector3 expected, Vector3 actual, float tolerance = 1e-4f) =>
            Assert.Less(Vector3.Distance(expected, actual), tolerance, $"Expected {expected}, got {actual}");

        [Test]
        public void Register_AddsToWorld_AndUnregisterRemoves()
        {
            Object4DBehaviour a = AddObject("A", new Vector4(0f, 1f, 2f, 1f));
            Assert.NotNull(a.Body);
            Assert.AreEqual(1, _registry.World.Count);
            Assert.AreEqual(1f, a.Body.Transform.Position.W);

            _registry.Register(a); // idempotent
            Assert.AreEqual(1, _registry.World.Count);

            _registry.Unregister(a);
            Assert.IsNull(a.Body);
            Assert.AreEqual(0, _registry.World.Count);
            Assert.IsEmpty(_registry.Members);
        }

        [Test]
        public void LabObjects_SameXyz_DifferOnlyInSignedDeltaW()
        {
            // SPEC §23 lab: A, B, C at w = 0, +1, -1.
            Object4DBehaviour a = AddObject("A", new Vector4(0f, 1.5f, 2f, 0f));
            Object4DBehaviour b = AddObject("B", new Vector4(0f, 1.5f, 2f, 1f));
            Object4DBehaviour c = AddObject("C", new Vector4(0f, 1.5f, 2f, -1f));

            _driver.ProjectAll();

            foreach (Object4DBehaviour o in new[] { a, b, c })
            {
                AssertNear(new Vector3(0f, 1.5f, 2f), o.transform.position);
                AssertNear(Vector3.one, o.transform.localScale); // default half-extents 0.5
            }
            Assert.AreEqual(0f, a.DeltaW, 1e-6f);
            Assert.AreEqual(1f, b.DeltaW, 1e-6f);
            Assert.AreEqual(-1f, c.DeltaW, 1e-6f);
        }

        [Test]
        public void MovingAna_BringsTheAnaObjectToDeltaWZero()
        {
            // Milestone 2 acceptance, in miniature.
            Object4DBehaviour b = AddObject("B", new Vector4(0f, 1.5f, 2f, 1f));
            _player.State.MoveAnaKata(1f);

            _driver.ProjectAll();

            Assert.AreEqual(0f, b.DeltaW, 1e-6f);
            Assert.AreEqual(0f, b.LastProjection.W, 1e-6f);
        }

        [Test]
        public void ChangingFocus_ChangesDeltaW_ButMovesNothing()
        {
            // Milestone 1 acceptance, in miniature: focus is perception only (SPEC §7C).
            Object4DBehaviour b = AddObject("B", new Vector4(0f, 1.5f, 2f, 1f));
            _driver.ProjectAll();
            Vector3 before = b.transform.position;
            Vec4 playerBefore = _player.State.Position;

            _player.State.SetFocusW(1f);
            _driver.ProjectAll();

            Assert.AreEqual(0f, b.DeltaW, 1e-6f);
            Assert.AreEqual(1f, b.LastProjection.W, 1e-6f);
            AssertNear(before, b.transform.position, 1e-6f);
            Assert.AreEqual(playerBefore, _player.State.Position);
        }

        [Test]
        public void TurningThePlayer_MovesAndTurnsObjectsTheOtherWay()
        {
            // Math4D XZ by +90 turns the player's X axis toward Z, so something straight ahead (+Z) ends up on
            // the player's +X side, and its own forward points along the player's +X.
            Object4DBehaviour a = AddObject("A", new Vector4(0f, 0f, 2f, 0f));
            _player.State.RotateLocal(Plane4.XZ, Mathf.PI / 2f);

            _driver.ProjectAll();

            AssertNear(new Vector3(2f, 0f, 0f), a.transform.position);
            AssertNear(Vector3.right, a.transform.forward);
            AssertNear(Vector3.up, a.transform.up);
        }

        [Test]
        public void ProjectionIsExpressedInViewSpace()
        {
            _viewSpace.SetPositionAndRotation(new Vector3(10f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
            Object4DBehaviour a = AddObject("A", new Vector4(0f, 0f, 2f, 0f));

            _driver.ProjectAll();

            AssertNear(_viewSpace.TransformPoint(new Vector3(0f, 0f, 2f)), a.transform.position);
            AssertNear(_viewSpace.forward, a.transform.forward);
        }
    }
}
