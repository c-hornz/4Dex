using System;
using FourDex.XR;
using NUnit.Framework;
using UnityEngine;

namespace FourDex.Tests.EditMode
{
    /// <summary>ARCHITECTURE §3 / SPEC §25: the camera gets the physical pose unmodified, whatever the mapping consumers do.</summary>
    public class XRRigTests
    {
        private sealed class FakePoseSource : IHmdPoseSource
        {
            public HmdPose? Next;

            public bool TryGetPose(out HmdPose pose)
            {
                pose = Next.GetValueOrDefault();
                return Next.HasValue;
            }
        }

        private GameObject _root;
        private Camera _camera;
        private XRRig _rig;
        private FakePoseSource _source;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Rig");
            var head = new GameObject("Head");
            head.transform.SetParent(_root.transform, false);
            _camera = head.AddComponent<Camera>();
            _rig = _root.AddComponent<XRRig>(); // Awake runs in edit mode too, and finds the child camera.
            _source = new FakePoseSource();
            _rig.PoseSource = _source;
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_root);

        private static HmdPose SomePose() =>
            new HmdPose(new Vector3(0.1f, 1.7f, -0.3f), Quaternion.Euler(15f, 40f, -5f), true);

        [Test]
        public void CameraGetsThePoseUnmodified()
        {
            HmdPose pose = SomePose();
            _source.Next = pose;

            _rig.SampleAndApply();

            Assert.AreEqual(pose.Position, _camera.transform.localPosition);
            Assert.AreEqual(pose.Rotation, _camera.transform.localRotation);
            Assert.IsTrue(_rig.HasPose);
        }

        [Test]
        public void ConsumersReceiveTheSamePose_AfterTheCameraMoved()
        {
            HmdPose pose = SomePose();
            _source.Next = pose;
            Vector3 cameraPositionSeenByConsumer = Vector3.negativeInfinity;
            HmdPose received = default;
            _rig.PoseSampled += p =>
            {
                received = p;
                cameraPositionSeenByConsumer = _camera.transform.localPosition;
            };

            _rig.SampleAndApply();

            Assert.AreEqual(pose.Position, received.Position);
            Assert.AreEqual(pose.Rotation, received.Rotation);
            Assert.AreEqual(pose.Position, cameraPositionSeenByConsumer, "Camera must be written before consumers run.");
        }

        [Test]
        public void ThrowingConsumer_CannotCostHeadTracking()
        {
            HmdPose pose = SomePose();
            _source.Next = pose;
            _rig.PoseSampled += _ => throw new InvalidOperationException("broken 4D mapping");

            Assert.Throws<InvalidOperationException>(() => _rig.SampleAndApply());

            Assert.AreEqual(pose.Position, _camera.transform.localPosition);
            Assert.AreEqual(pose.Rotation, _camera.transform.localRotation);
        }

        [Test]
        public void NoPose_HoldsTheLastCameraPose()
        {
            HmdPose pose = SomePose();
            _source.Next = pose;
            _rig.SampleAndApply();

            _source.Next = null;
            bool notified = false;
            _rig.PoseSampled += _ => notified = true;
            _rig.SampleAndApply();

            Assert.AreEqual(pose.Position, _camera.transform.localPosition);
            Assert.IsFalse(notified);
        }
    }
}
