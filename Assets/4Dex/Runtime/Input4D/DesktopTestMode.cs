using FourDex.XR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace FourDex.Runtime
{
    /// <summary>
    /// Keyboard and mouse controls for developing without a headset. The keyboard drives the three SPEC §7
    /// operations and works with or without a headset. Mouse look replaces the head only when no XR device
    /// is active (or when forced), by swapping the rig's pose source; the camera path itself is unchanged.
    /// </summary>
    /// <remarks>
    /// A development tool, not the input mapping system: SPEC §6's configurable mappings arrive in Milestone 4.
    /// Key choices: each 4D rotation pair sits on one row or column (J/L, U/O, I/K) so its sign is spatial.
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)] // After XRRig samples (-100), before ProjectionDriver projects (+100).
    public sealed class DesktopTestMode : MonoBehaviour
    {
        public enum HeadMode
        {
            [Tooltip("Mouse look only when no XR device is active.")] Auto,
            [Tooltip("Always mouse look, even with a headset connected.")] AlwaysSimulate,
            [Tooltip("Never replace the head; keyboard 4D controls only.")] NeverSimulate,
        }

        public const string Legend =
            "DESKTOP  RMB-drag look   WASD walk   R / F ana / kata   Shift fast\n" +
            "ROTATE  J/L XW   U/O YW   I/K ZW   Left/Right XZ     FOCUS  [ ]  or wheel\n" +
            "RESET  Backspace view + focus   Shift+Backspace everything";

        [SerializeField] private XRRig? _rig;
        [SerializeField] private Player4D? _player;
        [SerializeField] private CoordinateHUD? _hud;
        [SerializeField] private HeadMode _headMode = HeadMode.Auto;
        [SerializeField] private DesktopSpeeds _speeds = DesktopSpeeds.Default;

        [Tooltip("Degrees of head turn per pixel of mouse movement.")]
        [SerializeField] private float _lookSensitivity = 0.15f;

        [Tooltip("focusW step per mouse-wheel notch.")]
        [SerializeField] private float _focusWheelStep = 0.1f;

        private SimulatedHmdPoseSource? _simulatedHead;
        private IHmdPoseSource? _previousSource;

        public bool IsSimulatingHead => _simulatedHead != null;

        private void OnEnable()
        {
            bool simulate = _headMode == HeadMode.AlwaysSimulate ||
                            (_headMode == HeadMode.Auto && !XRSettings.isDeviceActive);
            if (simulate && _rig != null)
            {
                _simulatedHead = new SimulatedHmdPoseSource();
                _previousSource = _rig.PoseSource;
                _rig.PoseSource = _simulatedHead;
            }
            if (_hud != null)
                _hud.Footer = (IsSimulatingHead ? "" : "(head: real HMD)\n") + Legend;
        }

        private void OnDisable()
        {
            if (_rig != null && _simulatedHead != null && _previousSource != null)
                _rig.PoseSource = _previousSource;
            _simulatedHead = null;
            _previousSource = null;
            if (_hud != null)
                _hud.Footer = null;
        }

        private void Update()
        {
            if (_player == null)
                return;

            DesktopCommands commands = ReadDevices();
            Quaternion headYaw = _simulatedHead != null
                ? _simulatedHead.YawRotation
                : Quaternion.Euler(0f, _rig != null ? _rig.LastPose.Rotation.eulerAngles.y : 0f, 0f);

            DesktopControls.Apply(_player.State, _simulatedHead, headYaw, commands, _speeds, Time.deltaTime,
                _player.ResetToStart);
        }

        private DesktopCommands ReadDevices()
        {
            var c = new DesktopCommands();
            Keyboard? k = Keyboard.current;
            Mouse? m = Mouse.current;

            if (k != null)
            {
                c.Walk = new Vector2(Axis(k.dKey, k.aKey), Axis(k.wKey, k.sKey));
                if (c.Walk.sqrMagnitude > 1f)
                    c.Walk.Normalize();
                c.AnaKata = Axis(k.rKey, k.fKey);
                c.XW = Axis(k.lKey, k.jKey);
                c.YW = Axis(k.oKey, k.uKey);
                c.ZW = Axis(k.iKey, k.kKey);
                // Math4D XZ + turns X toward Z, which is a turn to the left; Right arrow turns right.
                c.XZ = Axis(k.leftArrowKey, k.rightArrowKey);
                c.Focus = Axis(k.rightBracketKey, k.leftBracketKey);
                c.Fast = k.shiftKey.isPressed;
                bool reset = k.backspaceKey.wasPressedThisFrame;
                c.ResetAll = reset && c.Fast;
                c.ResetView = reset && !c.Fast;
            }

            if (m != null)
            {
                float wheel = m.scroll.ReadValue().y;
                if (wheel != 0f)
                    c.FocusStep = Mathf.Sign(wheel) * _focusWheelStep;
                if (_simulatedHead != null && m.rightButton.isPressed)
                {
                    Vector2 delta = m.delta.ReadValue();
                    c.LookDegrees = new Vector2(delta.x, -delta.y) * _lookSensitivity;
                }
            }

            return c;
        }

        private static float Axis(UnityEngine.InputSystem.Controls.KeyControl positive,
            UnityEngine.InputSystem.Controls.KeyControl negative) =>
            (positive.isPressed ? 1f : 0f) - (negative.isPressed ? 1f : 0f);
    }
}
