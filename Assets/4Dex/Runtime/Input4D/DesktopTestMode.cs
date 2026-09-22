using System.Text;
using FourDex.XR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
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
    /// The HUD shows the keys the Input System sees and the commands they produced (a minimal SPEC §20 input
    /// monitor), so "a key does nothing" can be told apart from "the key never arrived".
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
            "RESET  Backspace view + focus   Shift+Backspace everything\n" +
            "PANELS  Tab parameters (Up/Down, - / =)   V W-slice view";

        [SerializeField] private XRRig? _rig;
        [SerializeField] private Player4D? _player;
        [SerializeField] private CoordinateHUD? _hud;
        [SerializeField] private RuntimeParameterPanel? _panel;
        [SerializeField] private WSliceViewer? _sliceViewer;
        [SerializeField] private HeadMode _headMode = HeadMode.Auto;
        [SerializeField] private DesktopSpeeds _speeds = DesktopSpeeds.Default;

        [Tooltip("Degrees of head turn per pixel of mouse movement.")]
        [SerializeField] private float _lookSensitivity = 0.15f;

        [Tooltip("focusW step per mouse-wheel notch.")]
        [SerializeField] private float _focusWheelStep = 0.1f;

        private SimulatedHmdPoseSource? _simulatedHead;
        private IHmdPoseSource? _previousSource;
        private DesktopCommands _lastCommands;
        private readonly KeyRepeat _repeat = new KeyRepeat();

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
                _hud.Footer = BuildFooter;
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

            _lastCommands = ReadDevices(Keyboard.current, Mouse.current, _simulatedHead != null,
                _lookSensitivity, _focusWheelStep);
            Quaternion headYaw = _simulatedHead != null
                ? _simulatedHead.YawRotation
                : Quaternion.Euler(0f, _rig != null ? _rig.LastPose.Rotation.eulerAngles.y : 0f, 0f);

            DesktopControls.Apply(_player.State, _simulatedHead, headYaw, _lastCommands, _speeds, Time.deltaTime,
                _player.ResetToStart);

            DrivePanels(Keyboard.current, _panel, _sliceViewer, _repeat, Time.unscaledTime);
        }

        /// <summary>
        /// Panel keys, separate from the 4D commands: they change what is shown or tuned, never the player state.
        /// Static so tests can drive it with a simulated keyboard.
        /// </summary>
        public static void DrivePanels(Keyboard? k, RuntimeParameterPanel? panel, WSliceViewer? viewer, KeyRepeat repeat, float now)
        {
            if (k == null)
                return;
            if (viewer != null && k.vKey.wasPressedThisFrame)
                viewer.Visible = !viewer.Visible;
            if (panel == null)
                return;
            if (k.tabKey.wasPressedThisFrame)
                panel.Toggle();
            if (!panel.Visible)
                return;

            bool coarse = k.shiftKey.isPressed;
            if (repeat.Fired(k.downArrowKey, now)) panel.MoveSelection(+1);
            if (repeat.Fired(k.upArrowKey, now)) panel.MoveSelection(-1);
            if (repeat.Fired(k.equalsKey, now)) panel.Adjust(+1, coarse);
            if (repeat.Fired(k.minusKey, now)) panel.Adjust(-1, coarse);
        }

        /// <summary>Press fires once; holding fires again after a pause, then steadily, like a text cursor.</summary>
        public sealed class KeyRepeat
        {
            private const float Delay = 0.35f;
            private const float Interval = 0.06f;
            private readonly System.Collections.Generic.Dictionary<KeyControl, float> _next = new System.Collections.Generic.Dictionary<KeyControl, float>();

            public bool Fired(KeyControl key, float now)
            {
                if (key.wasPressedThisFrame)
                {
                    _next[key] = now + Delay;
                    return true;
                }
                if (key.isPressed && _next.TryGetValue(key, out float next) && now >= next)
                {
                    _next[key] = now + Interval;
                    return true;
                }
                return false;
            }
        }

        // Pulled by the HUD at its own refresh rate, so the monitor costs nothing between refreshes.
        private string BuildFooter() =>
            (IsSimulatingHead ? "" : "(head: real HMD)\n") + Legend + "\n" + DescribeInput(Keyboard.current, _lastCommands);

        /// <summary>Turns the current device state into commands. Static so tests can drive it with simulated devices.</summary>
        /// <param name="readLook">Mouse look applies only while the head is simulated.</param>
        public static DesktopCommands ReadDevices(Keyboard? k, Mouse? m, bool readLook, float lookSensitivity, float focusWheelStep)
        {
            var c = new DesktopCommands();

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
                    c.FocusStep = Mathf.Sign(wheel) * focusWheelStep;
                if (readLook && m.rightButton.isPressed)
                {
                    Vector2 delta = m.delta.ReadValue();
                    c.LookDegrees = new Vector2(delta.x, -delta.y) * lookSensitivity;
                }
            }

            return c;
        }

        /// <summary>
        /// Two monitor lines: the keys the Input System reports as held (by their layout name), and the
        /// nonzero 4D commands they produced this frame.
        /// </summary>
        public static string DescribeInput(Keyboard? k, in DesktopCommands c)
        {
            var sb = new StringBuilder("KEYS ");
            int held = 0;
            if (k != null)
            {
                foreach (KeyControl key in k.allKeys)
                {
                    if (key == null || !key.isPressed)
                        continue;
                    sb.Append(' ').Append(string.IsNullOrEmpty(key.displayName) ? key.name : key.displayName);
                    held++;
                }
            }
            if (k == null)
                sb.Append(" (no keyboard)");
            else if (held == 0)
                sb.Append(" (none)");

            sb.Append("\nCMD ");
            int start = sb.Length;
            AppendIf(sb, "walk", c.Walk.x, c.Walk.y);
            AppendIf(sb, "anaKata", c.AnaKata);
            AppendIf(sb, "XW", c.XW);
            AppendIf(sb, "YW", c.YW);
            AppendIf(sb, "ZW", c.ZW);
            AppendIf(sb, "XZ", c.XZ);
            AppendIf(sb, "focus", c.Focus + c.FocusStep);
            if (c.Fast) sb.Append("  fast");
            if (c.ResetView) sb.Append("  RESET view");
            if (c.ResetAll) sb.Append("  RESET all");
            if (sb.Length == start)
                sb.Append(" (none)");
            return sb.ToString();
        }

        private static void AppendIf(StringBuilder sb, string label, float value)
        {
            if (value != 0f)
                sb.Append("  ").Append(label).Append(value > 0f ? " +" : " -");
        }

        private static void AppendIf(StringBuilder sb, string label, float x, float y)
        {
            if (x != 0f || y != 0f)
                sb.Append("  ").Append(label).Append(" (").Append(x.ToString("+0;-0;0")).Append(", ").Append(y.ToString("+0;-0;0")).Append(')');
        }

        private static float Axis(KeyControl positive, KeyControl negative) =>
            (positive.isPressed ? 1f : 0f) - (negative.isPressed ? 1f : 0f);
    }
}
