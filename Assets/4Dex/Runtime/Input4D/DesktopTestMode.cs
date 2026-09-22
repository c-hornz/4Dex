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
            "MOVE  WASD walk   R / F ana / kata   Shift fast\n" +
            "HEAD  Alt+arrows look   RMB-drag or M capture   Q / E roll   T / G lean\n" +
            "ROTATE  J/L XW   U/O YW   I/K ZW   Left/Right XZ      FOCUS  [ ]  or wheel   SURVEY  hold Space\n" +
            "MAPPING  P profile   X freeze   C recenter      PANELS  Tab parameters   V W-slice   H this help\n" +
            "RESET  Backspace view + focus + CLASSIC mapping   Shift+Backspace everything";

        [SerializeField] private XRRig? _rig;
        [SerializeField] private Player4D? _player;
        [SerializeField] private CoordinateHUD? _hud;
        [SerializeField] private RuntimeParameterPanel? _panel;
        [SerializeField] private WSliceViewer? _sliceViewer;
        [SerializeField] private HeadMappingDriver? _mapping;
        [SerializeField] private ProjectionDriver? _projection;
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

        private bool _mouseCaptured;

        /// <summary>
        /// Locks and hides the cursor, and lets mouse look work without holding a button. M toggles it. Useful where
        /// the Game view receives mouse movement but not button presses.
        /// </summary>
        public bool MouseCaptured
        {
            get => _mouseCaptured;
            set
            {
                _mouseCaptured = value;
                Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !value;
            }
        }

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
                _hud.Help = BuildHelp;
        }

        private void OnDisable()
        {
            if (_rig != null && _simulatedHead != null && _previousSource != null)
                _rig.PoseSource = _previousSource;
            _simulatedHead = null;
            _previousSource = null;
            MouseCaptured = false; // never leave the cursor locked after play stops
            if (_hud != null)
                _hud.Help = null;
        }

        private void Update()
        {
            if (_player == null)
                return;

            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
                MouseCaptured = !MouseCaptured;

            _lastCommands = ReadDevices(Keyboard.current, Mouse.current, _simulatedHead != null,
                _lookSensitivity, _focusWheelStep, MouseCaptured);
            Quaternion headYaw = _simulatedHead != null
                ? _simulatedHead.YawRotation
                : Quaternion.Euler(0f, _rig != null ? _rig.LastPose.Rotation.eulerAngles.y : 0f, 0f);

            DesktopControls.Apply(_player.State, _simulatedHead, headYaw, _lastCommands, _speeds, Time.deltaTime,
                _player.ResetToStart);
            // SPEC §22: the emergency reset also returns the head mapping to conventional, recentred.
            if ((_lastCommands.ResetView || _lastCommands.ResetAll) && _mapping != null)
                _mapping.ReturnToConventional();

            DriveMapping(Keyboard.current, _mapping);
            // SPEC §10: survey is temporary by design, so it is held, not toggled.
            if (_projection != null)
                _projection.SurveyEngaged = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            if (_hud != null && Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
                _hud.HelpVisible = !_hud.HelpVisible;

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

        /// <summary>P cycles the mapping profile, X toggles freeze, C recentres. Static so tests can drive it.</summary>
        public static void DriveMapping(Keyboard? k, HeadMappingDriver? mapping)
        {
            if (k == null || mapping == null)
                return;
            if (k.pKey.wasPressedThisFrame)
                mapping.SetProfile(mapping.ActiveIndex + (k.shiftKey.isPressed ? -1 : 1));
            if (k.xKey.wasPressedThisFrame)
                mapping.Frozen = !mapping.Frozen;
            if (k.cKey.wasPressedThisFrame)
                mapping.Recenter();
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
        private string BuildHelp() =>
            (IsSimulatingHead ? "(head: simulated, mouse look on" : "(head: real HMD, mouse look off") +
            (MouseCaptured ? ", cursor captured)\n" : ")\n") +
            Legend + "\n" + DescribeInput(Keyboard.current, Mouse.current, _lastCommands);

        /// <summary>Turns the current device state into commands. Static so tests can drive it with simulated devices.</summary>
        /// <param name="readLook">Mouse look applies only while the head is simulated.</param>
        /// <param name="captureMouse">
        /// With the cursor captured, dragging needs no button: some setups deliver mouse movement to the Game view
        /// but not its buttons.
        /// </param>
        public static DesktopCommands ReadDevices(Keyboard? k, Mouse? m, bool readLook, float lookSensitivity,
            float focusWheelStep, bool captureMouse = false)
        {
            var c = new DesktopCommands();

            if (k != null)
            {
                c.Walk = new Vector2(Axis(k.dKey, k.aKey), Axis(k.wKey, k.sKey));
                if (c.Walk.sqrMagnitude > 1f)
                    c.Walk.Normalize();
                c.AnaKata = Axis(k.rKey, k.fKey);
                c.HeadRoll = Axis(k.qKey, k.eKey);
                c.HeadLean = Axis(k.tKey, k.gKey);
                c.XW = Axis(k.lKey, k.jKey);
                c.YW = Axis(k.oKey, k.uKey);
                c.ZW = Axis(k.iKey, k.kKey);
                // Alt makes the arrows look around instead: keyboard head look, for when mouse input never arrives
                // in the Game view. Plain arrows keep turning the 4D view in XZ.
                if (k.altKey.isPressed)
                    c.LookKeys = new Vector2(Axis(k.rightArrowKey, k.leftArrowKey), Axis(k.upArrowKey, k.downArrowKey));
                else
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
                if (readLook && (captureMouse || m.rightButton.isPressed))
                {
                    Vector2 delta = m.delta.ReadValue();
                    c.LookDegrees = new Vector2(delta.x, -delta.y) * lookSensitivity;
                }
            }

            return c;
        }

        /// <summary>
        /// Three monitor lines: the keys the Input System reports as held (by their layout name), the mouse's buttons
        /// and movement, and the nonzero 4D commands they produced this frame.
        /// </summary>
        /// <remarks>
        /// The mouse line answers the question the keys line cannot: whether a drag is reaching the game at all, and
        /// whether it turned into head look. No look with the button down and movement arriving means the head is not
        /// being simulated, not that the mouse is being missed.
        /// </remarks>
        public static string DescribeInput(Keyboard? k, Mouse? m, in DesktopCommands c)
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

            sb.Append("\nMOUSE ");
            if (m == null)
            {
                sb.Append(" (no mouse)");
            }
            else
            {
                int buttons = 0;
                if (m.leftButton.isPressed) { sb.Append(" LMB"); buttons++; }
                if (m.rightButton.isPressed) { sb.Append(" RMB"); buttons++; }
                if (m.middleButton.isPressed) { sb.Append(" MMB"); buttons++; }
                if (buttons == 0)
                    sb.Append(" (no buttons)");

                Vector2 delta = m.delta.ReadValue();
                sb.Append("  move ").Append(Pair(delta.x, delta.y));
                float wheel = m.scroll.ReadValue().y;
                if (wheel != 0f)
                    sb.Append("  wheel ").Append(wheel.ToString("+0;-0;0"));
                sb.Append("  look ").Append(Pair(c.LookDegrees.x, c.LookDegrees.y)).Append(" deg");
            }

            sb.Append("\nCMD ");
            int start = sb.Length;
            AppendIf(sb, "walk", c.Walk.x, c.Walk.y);
            AppendIf(sb, "anaKata", c.AnaKata);
            AppendIf(sb, "look", c.LookKeys.x, c.LookKeys.y);
            AppendIf(sb, "headRoll", c.HeadRoll);
            AppendIf(sb, "headLean", c.HeadLean);
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

        private static string Pair(float x, float y) => $"({x:+0.0;-0.0;0}, {y:+0.0;-0.0;0})";

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
