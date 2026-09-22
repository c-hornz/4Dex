using FourDex.Math4D;
using FourDex.Player;
using FourDex.Runtime;
using FourDex.XR;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FourDex.Tests.EditMode
{
    public class CoordinateHUDTests
    {
        [Test]
        public void Readout_ShowsSignedPositionAndFocusSide()
        {
            var player = new Player4DState(new Transform4(new Vec4(1f, -2f, 0.5f, -1.25f), Rotor4.Identity), 1f);

            string text = CoordinateReadout.Format(player, null);

            StringAssert.Contains("x +1.000  y -2.000  z +0.500  w -1.250", text);
            StringAssert.Contains("focusW +1.000 (ana)", text);
            StringAssert.Contains("focusWidth n/a", text);
            StringAssert.Contains("HMD  no pose", text);
        }

        [Test]
        public void Readout_FocusSide_KeepsAnaAndKataApart()
        {
            Assert.AreEqual("(ana)", CoordinateReadout.FocusSide(0.5f));
            Assert.AreEqual("(kata)", CoordinateReadout.FocusSide(-0.5f));
            Assert.AreEqual("(own slice)", CoordinateReadout.FocusSide(0f));
        }

        [Test]
        public void Readout_ShowsBasis_AfterRotationThroughW()
        {
            // XW by +90: the player's X' axis now points along world +W, and W' along world -X.
            var player = new Player4DState(new Transform4(Vec4.Zero, Rotor4.FromPlane(Plane4.XW, Mathf.PI / 2f)));

            string text = CoordinateReadout.Format(player, null);

            StringAssert.Contains("X' (+0.000 +0.000 +0.000 +1.000)", text);
            StringAssert.Contains("W' (-1.000 +0.000 +0.000 +0.000)", text);
            StringAssert.Contains("Y' (+0.000 +1.000 +0.000 +0.000)", text);
        }

        [Test]
        public void Readout_ShowsHeadPose_InSignedDegrees()
        {
            var head = new HmdPose(new Vector3(0f, 1.7f, 0f), Quaternion.Euler(0f, -30f, 0f), false);

            string text = CoordinateReadout.Format(null, head);

            StringAssert.Contains("pos +0.000 +1.700 +0.000", text);
            StringAssert.Contains("yaw -030.0", text);
            StringAssert.Contains("NOT TRACKED", text);
        }

        [Test]
        public void Hud_BuildsWorldSpacePanel_AndShowsThePlayer()
        {
            var cameraGo = new GameObject("Camera", typeof(Camera));
            var playerGo = new GameObject("Player");
            try
            {
                var player = playerGo.AddComponent<Player4D>();
                player.State.MoveAnaKata(2f);
                var hud = cameraGo.AddComponent<CoordinateHUD>();
                var so = new SerializedObject(hud);
                so.FindProperty("_player").objectReferenceValue = player;
                so.ApplyModifiedPropertiesWithoutUndo();

                hud.Refresh();

                StringAssert.Contains("w +2.000", hud.CurrentText);
                var canvas = cameraGo.GetComponentInChildren<Canvas>();
                Assert.NotNull(canvas);
                Assert.AreEqual(RenderMode.WorldSpace, canvas.renderMode);
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(playerGo);
            }
        }
    }
}
