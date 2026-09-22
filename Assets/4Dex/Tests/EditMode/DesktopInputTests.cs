using System;
using FourDex.Runtime;
using FourDex.Perception;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace FourDex.Tests.EditMode
{
    /// <summary>
    /// Key-to-command bindings, driven through a simulated keyboard. Covers the half of desktop mode that
    /// DesktopControlsTests cannot: whether each key actually produces its command.
    /// </summary>
    public class DesktopInputTests : InputTestFixture
    {
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        private DesktopCommands Held(Func<Keyboard, KeyControl> key)
        {
            Press(key(_keyboard));
            DesktopCommands c = DesktopTestMode.ReadDevices(_keyboard, null, false, 0f, 0f);
            Release(key(_keyboard));
            return c;
        }

        [Test]
        public void R_IsAna_F_IsKata()
        {
            Assert.AreEqual(1f, Held(k => k.rKey).AnaKata);
            Assert.AreEqual(-1f, Held(k => k.fKey).AnaKata);
        }

        [Test]
        public void EveryBindingProducesItsCommand()
        {
            Assert.AreEqual(1f, Held(k => k.wKey).Walk.y);
            Assert.AreEqual(-1f, Held(k => k.sKey).Walk.y);
            Assert.AreEqual(-1f, Held(k => k.aKey).Walk.x);
            Assert.AreEqual(1f, Held(k => k.dKey).Walk.x);
            Assert.AreEqual(1f, Held(k => k.lKey).XW);
            Assert.AreEqual(-1f, Held(k => k.jKey).XW);
            Assert.AreEqual(1f, Held(k => k.oKey).YW);
            Assert.AreEqual(-1f, Held(k => k.uKey).YW);
            Assert.AreEqual(1f, Held(k => k.iKey).ZW);
            Assert.AreEqual(-1f, Held(k => k.kKey).ZW);
            Assert.AreEqual(1f, Held(k => k.leftArrowKey).XZ);
            Assert.AreEqual(-1f, Held(k => k.rightArrowKey).XZ);
            Assert.AreEqual(1f, Held(k => k.rightBracketKey).Focus);
            Assert.AreEqual(-1f, Held(k => k.leftBracketKey).Focus);
        }

        [Test]
        public void AnaKataKeys_ProduceNoOtherCommand()
        {
            DesktopCommands c = Held(k => k.rKey);
            Assert.AreEqual(0f, c.Focus);
            Assert.AreEqual(0f, c.XW + c.YW + c.ZW + c.XZ);
            Assert.AreEqual(0f, c.Walk.sqrMagnitude);
        }

        [Test]
        public void Monitor_ShowsHeldKeyAndItsCommand()
        {
            Press(_keyboard.rKey);
            DesktopCommands c = DesktopTestMode.ReadDevices(_keyboard, null, false, 0f, 0f);

            string text = DesktopTestMode.DescribeInput(_keyboard, null, c);

            StringAssert.Contains("KEYS  R", text);
            StringAssert.Contains("anaKata +", text);
        }

        private void Tap(KeyControl key, RuntimeParameterPanel panel, WSliceViewer viewer, DesktopTestMode.KeyRepeat repeat, float now)
        {
            Press(key);
            DesktopTestMode.DrivePanels(_keyboard, panel, viewer, repeat, now);
            Release(key);
        }

        [Test]
        public void PanelKeys_ShowSelectAndEdit_AndVTogglesTheSliceViewer()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<PerceptionSettings>();
            var camera = new UnityEngine.GameObject("Camera", typeof(UnityEngine.Camera));
            try
            {
                var panel = camera.AddComponent<RuntimeParameterPanel>();
                var viewer = camera.AddComponent<WSliceViewer>();
                var so = new UnityEditor.SerializedObject(panel);
                so.FindProperty("_settings").objectReferenceValue = settings;
                so.ApplyModifiedPropertiesWithoutUndo();
                var repeat = new DesktopTestMode.KeyRepeat();
                float width = settings.Parameters.FocusWidth;

                // Hidden panel ignores edit keys.
                Tap(_keyboard.equalsKey, panel, viewer, repeat, 0f);
                Assert.AreEqual(width, settings.Parameters.FocusWidth);

                Tap(_keyboard.tabKey, panel, viewer, repeat, 1f);
                Assert.IsTrue(panel.Visible);

                Tap(_keyboard.equalsKey, panel, viewer, repeat, 2f);
                Assert.AreEqual(width + 0.01f, settings.Parameters.FocusWidth, 1e-5f, "= must raise the selected value");
                Tap(_keyboard.minusKey, panel, viewer, repeat, 3f);
                Assert.AreEqual(width, settings.Parameters.FocusWidth, 1e-5f);

                Tap(_keyboard.downArrowKey, panel, viewer, repeat, 4f);
                Assert.AreEqual(1, panel.Selected);

                Assert.IsTrue(viewer.Visible);
                Tap(_keyboard.vKey, panel, viewer, repeat, 5f);
                Assert.IsFalse(viewer.Visible);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camera);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void KeyRepeat_FiresOnPress_ThenAfterADelay_ThenSteadily()
        {
            var repeat = new DesktopTestMode.KeyRepeat();
            Press(_keyboard.equalsKey);
            Assert.IsTrue(repeat.Fired(_keyboard.equalsKey, 10f), "press fires");
            InputSystem.Update(); // next frame: still held, no longer "pressed this frame"
            Assert.IsFalse(repeat.Fired(_keyboard.equalsKey, 10.1f), "no repeat before the delay");
            Assert.IsTrue(repeat.Fired(_keyboard.equalsKey, 10.4f), "repeats after the delay");
            Assert.IsFalse(repeat.Fired(_keyboard.equalsKey, 10.42f));
            Assert.IsTrue(repeat.Fired(_keyboard.equalsKey, 10.47f), "then steadily");
            Release(_keyboard.equalsKey);
            Assert.IsFalse(repeat.Fired(_keyboard.equalsKey, 11f));
        }

        [Test]
        public void Monitor_SaysNone_WhenIdle()
        {
            string text = DesktopTestMode.DescribeInput(_keyboard, null, default);
            StringAssert.Contains("KEYS  (none)", text);
            StringAssert.Contains("CMD  (none)", text);
            StringAssert.Contains("MOUSE  (no mouse)", text);
        }

        [Test]
        public void RightDrag_TurnsIntoHeadLook_OnlyWhileTheHeadIsSimulated()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            Press(mouse.rightButton);
            Set(mouse.delta, new UnityEngine.Vector2(20f, 10f));

            // Simulated head: the drag becomes look. Positive x turns right; screen-up is a negative pitch, which
            // the source treats as looking up.
            DesktopCommands look = DesktopTestMode.ReadDevices(_keyboard, mouse, readLook: true, 0.1f, 0.1f);
            Assert.AreEqual(2f, look.LookDegrees.x, 1e-4f);
            Assert.AreEqual(-1f, look.LookDegrees.y, 1e-4f);

            // Real HMD: the same drag produces nothing, because the headset owns the head.
            DesktopCommands none = DesktopTestMode.ReadDevices(_keyboard, mouse, readLook: false, 0.1f, 0.1f);
            Assert.AreEqual(UnityEngine.Vector2.zero, none.LookDegrees);
        }

        [Test]
        public void AltArrows_LookAround_AndPlainArrowsStillRotateTheView()
        {
            // The keyboard fallback for setups where mouse input never reaches the Game view.
            Press(_keyboard.altKey);
            Press(_keyboard.upArrowKey);
            DesktopCommands look = DesktopTestMode.ReadDevices(_keyboard, null, true, 0f, 0f);
            Assert.AreEqual(1f, look.LookKeys.y, "Alt+Up looks up");
            Assert.AreEqual(0f, look.XZ, "and does not turn the 4D view");
            Release(_keyboard.altKey);

            DesktopCommands rotate = DesktopTestMode.ReadDevices(_keyboard, null, true, 0f, 0f);
            Assert.AreEqual(0f, rotate.LookKeys.y);
            Release(_keyboard.upArrowKey);

            Press(_keyboard.leftArrowKey);
            Assert.AreEqual(1f, DesktopTestMode.ReadDevices(_keyboard, null, true, 0f, 0f).XZ, "plain arrows still rotate");
            Release(_keyboard.leftArrowKey);
        }

        [Test]
        public void KeyboardLook_TurnsTheSimulatedHead()
        {
            var head = new FourDex.XR.SimulatedHmdPoseSource();
            var speeds = DesktopSpeeds.Default;
            var commands = new DesktopCommands { LookKeys = new UnityEngine.Vector2(1f, 1f) };

            DesktopControls.Apply(new FourDex.Player.Player4DState(), head, UnityEngine.Quaternion.identity, commands, speeds, 1f);

            Assert.AreEqual(speeds.HeadLook, head.Yaw, 1e-3f, "turns right");
            Assert.AreEqual(-speeds.HeadLook, head.Pitch, 1e-3f, "and looks up");
        }

        [Test]
        public void CapturedCursor_LooksWithoutHoldingAButton()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            Set(mouse.delta, new UnityEngine.Vector2(10f, 0f));

            Assert.AreEqual(0f, DesktopTestMode.ReadDevices(_keyboard, mouse, true, 0.1f, 0f).LookDegrees.x);
            Assert.AreEqual(1f, DesktopTestMode.ReadDevices(_keyboard, mouse, true, 0.1f, 0f, captureMouse: true).LookDegrees.x, 1e-4f);
        }

        [Test]
        public void Monitor_ShowsTheMouse_SoADeadDragCanBeToldApartFromADeadHead()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            Press(mouse.rightButton);
            Set(mouse.delta, new UnityEngine.Vector2(12f, -4f));

            DesktopCommands c = DesktopTestMode.ReadDevices(_keyboard, mouse, readLook: true, 0.1f, 0.1f);
            string text = DesktopTestMode.DescribeInput(_keyboard, mouse, c);

            StringAssert.Contains("MOUSE  RMB", text);
            StringAssert.Contains("move (+12.0, -4.0)", text);
            StringAssert.Contains("look (+1.2, +0.4) deg", text);
        }
    }
}
