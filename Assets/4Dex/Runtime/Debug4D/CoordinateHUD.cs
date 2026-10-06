using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FourDex.Perception;
using FourDex.Player;
using FourDex.XR;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace FourDex.Runtime
{
    /// <summary>
    /// Head-locked coordinate readout, visible in the headset and on the desktop mirror (SPEC §20).
    /// Put it on the XR camera; it builds its own panel (see <see cref="DebugPanel"/>).
    /// </summary>
    /// <remarks>Head-locked is uncomfortable for content but right for a debug readout that must always be findable.</remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CoordinateHUD : MonoBehaviour
    {
        [SerializeField] private Player4D? _player;
        [SerializeField] private XRRig? _rig;

        [Tooltip("Optional: adds focusWidth and one line per 4D object (signed W gap, band, visibility).")]
        [SerializeField] private ProjectionDriver? _projection;

        [Tooltip("Optional: adds the input mapping monitor (SPEC §20): profile, and each head input with the 4D motion it drives.")]
        [SerializeField] private HeadMappingDriver? _mapping;

        [Tooltip("Top-centre of the panel in the camera's frame, in metres; the panel grows downward from here. About 20 degrees below the line of sight: glance down to read it, and it does not cover the lab.")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, -0.3f, 1.2f);

        [Tooltip("Text refreshes per second. Reformatting every frame allocates for no readable gain.")]
        [SerializeField, Min(1f)] private float _refreshHz = 10f;

        [SerializeField] private int _fontSize = 18;

        [Tooltip("Most object lines to list, nearest in W first. A maze has dozens of walls.")]
        [SerializeField, Min(1)] private int _maxObjectLines = 8;

        [Tooltip("Top-left corner of the help panel (key legend, input monitor), metres in the camera's frame: up and left, clear of the lab.")]
        [SerializeField] private Vector3 _helpOffset = new Vector3(-0.62f, 0.45f, 1.2f);

        // Canvas units are millimetres (DebugPanel).
        private const float PanelWidth = 900f;
        private const float Padding = 14f;

        private Text? _text;
        private Text? _helpText;
        private float _nextRefresh;
        private float _lastSampleTime = -1f;
        private readonly RotationMonitor _rotation = new RotationMonitor();
        private readonly FrameMonitor _frames = new FrameMonitor();
        private readonly FrameTiming[] _timings = new FrameTiming[1];

        public string CurrentText => _text != null ? _text.text : string.Empty;

        /// <summary>Supplies extra lines under the readout at each refresh. Null for none.</summary>
        public Func<string>? Footer { get; set; }

        /// <summary>Supplies the separate help panel's text (desktop key legend, input monitor). Null hides it.</summary>
        public Func<string>? Help { get; set; }

        public bool HelpVisible { get; set; } = true;

        public string CurrentHelpText => _helpText != null ? _helpText.text : string.Empty;

        private void Update()
        {
            // Every frame, unlike the text: a frame-time readout refreshed at 10 Hz would miss what it measures.
            _frames.Sample(Time.unscaledDeltaTime);

            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + 1f / _refreshHz;
            Refresh();
        }

        public void Refresh()
        {
            Text text = EnsurePanel();
            HmdPose? head = _rig != null && _rig.HasPose ? _rig.LastPose : (HmdPose?)null;
            float survey = _projection != null ? _projection.SurveyAmount : 0f;
            // The effective width: during survey it is the widened one the objects are being rendered with.
            float? focusWidth = _projection != null && _projection.Perception != null
                ? PerceptionModel.FocusWidthAt(_projection.Perception.Parameters.Validated(), survey)
                : (float?)null;
            Player4DState? state = _player != null ? _player.State : null;
            if (state != null)
            {
                // Sampled at the HUD's refresh rate: rate is measured over the interval since the last refresh.
                float now = Time.unscaledTime;
                _rotation.Sample(state.Orientation, _lastSampleTime < 0f ? 0f : now - _lastSampleTime);
                _lastSampleTime = now;
            }
            string readout = CoordinateReadout.Format(state, head, focusWidth, ObjectLines(), state != null ? _rotation : null, survey);
            // At the top, not the bottom: the object list below it is dozens of lines in a maze, and a frame-cost
            // line pushed off the end of the panel is one you never read.
            readout = PerfLine() + "\n" + readout;
            if (_mapping != null)
                readout += "\n" + MappingReadout.Format(_mapping.ActiveName, _mapping.MappingEnabled, _mapping.Frozen, _mapping.Contributions);
            string? footer = Footer?.Invoke();
            text.text = string.IsNullOrEmpty(footer) ? readout : readout + "\n\n" + footer;

            // Fit the panel to the text, so a footer never spills past the background.
            var panel = (RectTransform)text.rectTransform.parent;
            panel.sizeDelta = new Vector2(PanelWidth, text.preferredHeight + 2f * Padding);
            DebugPanel.FitInView(panel, GetComponent<Camera>());

            RefreshHelp();
        }

        /// <summary>
        /// What the frame is costing, and what it is spending it on. In the headset no profiler window is visible,
        /// so this is the only place the cost can be read while wearing it.
        /// </summary>
        /// <remarks>
        /// The worst frame of the last second matters as much as the mean: a spike is felt, an average is not. Batch
        /// and SetPass counts come from the editor's own statistics, so they are absent in a build.
        /// </remarks>
        private string PerfLine()
        {
            var sb = new System.Text.StringBuilder("FRAME ");
            sb.Append(_frames.MeanMs.ToString("0.0", CultureInfo.InvariantCulture)).Append(" ms (")
              .Append(_frames.Fps.ToString("0", CultureInfo.InvariantCulture)).Append(" fps)   worst 1 s ")
              .Append(_frames.WorstMs.ToString("0.0", CultureInfo.InvariantCulture)).Append(" ms");
            if (_projection != null && _projection.World != null)
                sb.Append("   objects ").Append(_projection.World.Members.Count);

            // Which side the frame is spent on. Needs Frame Timing Stats in Player Settings; without it the counters
            // read zero, so the split is simply left off rather than shown as a pair of lies.
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timings) > 0 &&
                (_timings[0].cpuFrameTime > 0d || _timings[0].gpuFrameTime > 0d))
            {
                sb.Append("   cpu ").Append(_timings[0].cpuFrameTime.ToString("0.0", CultureInfo.InvariantCulture))
                  .Append("   gpu ").Append(_timings[0].gpuFrameTime.ToString("0.0", CultureInfo.InvariantCulture))
                  .Append(" ms");
            }
            return sb.ToString();
        }

        private void RefreshHelp()
        {
            string? help = HelpVisible ? Help?.Invoke() : null;
            if (string.IsNullOrEmpty(help))
            {
                if (_helpText != null)
                    _helpText.rectTransform.parent.gameObject.SetActive(false);
                return;
            }
            if (_helpText == null)
            {
                RectTransform canvas = DebugPanel.CreateCanvas(transform, "Help Canvas", _helpOffset, 0f,
                    new Vector2(PanelWidth, 120f), new Vector2(0f, 1f));
                _helpText = DebugPanel.CreateText(canvas, "Text", _fontSize - 1, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.85f));
                DebugPanel.Stretch(_helpText.rectTransform, canvas, Padding);
            }
            var helpPanel = (RectTransform)_helpText.rectTransform.parent;
            helpPanel.gameObject.SetActive(true);
            _helpText.text = help;
            helpPanel.sizeDelta = new Vector2(PanelWidth, _helpText.preferredHeight + 2f * Padding);
            DebugPanel.FitInView(helpPanel, GetComponent<Camera>());
        }

        private IEnumerable<(string, PerceptionChannels, ShadowChannels)>? ObjectLines()
        {
            if (_projection == null || _projection.Perception == null || _projection.World == null)
                return null;
            var lines = new List<(string, PerceptionChannels, ShadowChannels)>();
            // A maze has dozens of walls: list the ones nearest in W, which are the ones being perceived, and say
            // how many were left out rather than filling the panel.
            IEnumerable<Object4DBehaviour> nearest = _projection.World.Members;
            int total = _projection.World.Members.Count;
            if (total > _maxObjectLines)
                nearest = _projection.World.Members.OrderBy(m => Mathf.Abs(m.WGap)).Take(_maxObjectLines);

            foreach (Object4DBehaviour member in nearest)
            {
                // The lab names objects "A  w=0 ..." with a double space after the label; anything else is just
                // trimmed to something that fits the column, e.g. the maze's "Wall 0101 PosW".
                string name = member.name;
                int doubleSpace = name.IndexOf("  ", StringComparison.Ordinal);
                string label = doubleSpace > 0 ? name.Substring(0, doubleSpace)
                    : name.Length > 16 ? name.Substring(0, 16)
                    : name;
                lines.Add((label, member.Channels, member.Shadow));
            }
            if (total > lines.Count)
                lines.Add(($"(+{total - lines.Count} more)", default, default));
            return lines;
        }

        private Text EnsurePanel()
        {
            if (_text != null)
                return _text;

            RectTransform canvas = DebugPanel.CreateCanvas(transform, "CoordinateHUD Canvas", _offset, 0f,
                new Vector2(PanelWidth, 250f), new Vector2(0.5f, 1f));
            _text = DebugPanel.CreateText(canvas, "Text", _fontSize, TextAnchor.UpperLeft, Color.white);
            DebugPanel.Stretch(_text.rectTransform, canvas, Padding);
            return _text;
        }
    }
}
