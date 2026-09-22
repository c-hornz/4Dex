using FourDex.Math4D;
using FourDex.Player;
using FourDex.Runtime;
using FourDex.XR;
using NUnit.Framework;
using UnityEngine;

namespace FourDex.Tests.EditMode
{
    /// <summary>Desktop controls keep SPEC §7's three operations separate, and the reset restores a conventional view (§22).</summary>
    public class DesktopControlsTests
    {
        private static readonly DesktopSpeeds Speeds = DesktopSpeeds.Default;

        private static Player4DState StartedPlayer() =>
            new Player4DState(new Transform4(new Vec4(1f, 0f, 2f, 0.5f), Rotor4.FromPlane(Plane4.YW, 0.3f)), 0.25f);

        private static void AssertNear(Vector3 expected, Vector3 actual, float tolerance = 1e-4f) =>
            Assert.Less(Vector3.Distance(expected, actual), tolerance, $"Expected {expected}, got {actual}");

        [Test]
        public void AnaKata_MovesOnlyWorldW()
        {
            Player4DState p = StartedPlayer();
            Rotor4 orientation = p.Orientation;

            DesktopControls.Apply(p, null, Quaternion.identity, new DesktopCommands { AnaKata = 1f }, Speeds, 1f);

            Assert.AreEqual(0.5f + Speeds.AnaKata, p.Position.W, 1e-5f);
            Assert.AreEqual(new Vec4(1f, 0f, 2f, 0f), p.Position.WithW(0f));
            Assert.AreEqual(0.25f, p.FocusW);
            Assert.IsTrue(orientation.ApproxEquals(p.Orientation, 0f));
        }

        [Test]
        public void Focus_ChangesOnlyFocus()
        {
            Player4DState p = StartedPlayer();
            Transform4 pose = p.Pose;

            DesktopControls.Apply(p, null, Quaternion.identity,
                new DesktopCommands { Focus = -1f, FocusStep = 0.1f }, Speeds, 1f);

            Assert.AreEqual(0.25f - Speeds.Focus + 0.1f, p.FocusW, 1e-5f);
            Assert.AreEqual(pose.Position, p.Position);
            Assert.IsTrue(pose.Rotation.ApproxEquals(p.Orientation, 0f));
        }

        [Test]
        public void Rotations_ChangeOnlyOrientation()
        {
            Player4DState p = StartedPlayer();
            Vec4 position = p.Position;

            DesktopControls.Apply(p, null, Quaternion.identity,
                new DesktopCommands { XW = 1f, YW = -1f, ZW = 1f, XZ = 1f }, Speeds, 0.5f);

            Assert.AreEqual(position, p.Position);
            Assert.AreEqual(0.25f, p.FocusW);
            Assert.IsFalse(p.Orientation.ApproxEquals(Rotor4.FromPlane(Plane4.YW, 0.3f)));
        }

        [Test]
        public void RightArrow_TurnsTheViewRight()
        {
            // Right arrow sends XZ = -1. After a 90 degree turn, the player's forward (local Z) must point along
            // world +X, which is Unity's "right". This pins the Math4D-vs-Unity sign convention.
            var p = new Player4DState();
            var speeds = Speeds;
            speeds.Rotate = 90f;

            DesktopControls.Apply(p, null, Quaternion.identity, new DesktopCommands { XZ = -1f }, speeds, 1f);

            Vec4 forward = p.Orientation.Apply(Vec4.UnitZ);
            AssertNear(Vector3.right, forward.Xyz());
        }

        [Test]
        public void Walk_IsHeadRelative()
        {
            var p = new Player4DState();
            Quaternion lookingRight = Quaternion.Euler(0f, 90f, 0f);

            DesktopControls.Apply(p, null, lookingRight, new DesktopCommands { Walk = new Vector2(0f, 1f) }, Speeds, 1f);

            AssertNear(new Vector3(Speeds.Walk, 0f, 0f), p.Position.Xyz());
            Assert.AreEqual(0f, p.Position.W, 1e-6f);
        }

        [Test]
        public void Walk_AfterRotatingThroughW_CanCarryThePlayerAna()
        {
            // Rotating toward W changes where "forward" points (§7B), so walking afterwards moves through W (§7A).
            var p = new Player4DState();
            p.RotateLocal(Plane4.ZW, Mathf.PI / 2f);

            DesktopControls.Apply(p, null, Quaternion.identity, new DesktopCommands { Walk = new Vector2(0f, 1f) }, Speeds, 1f);

            Assert.AreEqual(Speeds.Walk, p.Position.W, 1e-4f);
            AssertNear(Vector3.zero, p.Position.Xyz());
        }

        [Test]
        public void Fast_MultipliesEveryRate()
        {
            var p = new Player4DState();
            DesktopControls.Apply(p, null, Quaternion.identity, new DesktopCommands { AnaKata = 1f, Fast = true }, Speeds, 1f);
            Assert.AreEqual(Speeds.AnaKata * Speeds.FastMultiplier, p.Position.W, 1e-5f);
        }

        [Test]
        public void ResetView_RestoresConventionalView_ButKeepsPosition()
        {
            Player4DState p = StartedPlayer();
            var head = new SimulatedHmdPoseSource();
            head.Look(40f, 20f);
            Vec4 position = p.Position;

            DesktopControls.Apply(p, head, head.YawRotation, new DesktopCommands { ResetView = true, AnaKata = 1f }, Speeds, 1f);

            Assert.IsTrue(p.Orientation.ApproxEquals(Rotor4.Identity, 0f));
            Assert.AreEqual(0f, p.FocusW);
            Assert.AreEqual(position, p.Position, "Reset must not also apply this frame's movement.");
            Assert.AreEqual(0f, head.Yaw);
            Assert.AreEqual(0f, head.Pitch);
        }

        [Test]
        public void ResetAll_HandsOffToTheOwner()
        {
            bool called = false;
            DesktopControls.Apply(StartedPlayer(), null, Quaternion.identity, new DesktopCommands { ResetAll = true },
                Speeds, 1f, () => called = true);
            Assert.IsTrue(called);
        }

        [Test]
        public void SimulatedHead_StandsAtEyeHeight_AndClampsPitch()
        {
            var head = new SimulatedHmdPoseSource(1.7f);
            head.Look(370f, 200f);

            Assert.IsTrue(head.TryGetPose(out HmdPose pose));
            Assert.AreEqual(new Vector3(0f, 1.7f, 0f), pose.Position);
            Assert.AreEqual(10f, head.Yaw, 1e-4f);
            Assert.AreEqual(89f, head.Pitch, 1e-4f);
            Assert.IsTrue(pose.IsTracked);
        }
    }
}
