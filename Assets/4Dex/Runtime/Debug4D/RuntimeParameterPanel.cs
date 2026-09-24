using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace FourDex.Runtime
{
    /// <summary>
    /// SPEC §21's runtime experiment panel, head-locked so it works in the headset: every perception parameter, with a
    /// cursor, adjustable without recompiling. Edits go straight into the PerceptionSettings and mapping profile
    /// assets, so they apply the same frame and persist as presets. Put it on the XR camera.
    /// </summary>
    /// <remarks>
    /// Reads no input itself. <c>DesktopTestMode</c> drives it from the keyboard today; Milestone 4's controller mapping
    /// can call the same three methods.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class RuntimeParameterPanel : MonoBehaviour
    {
        [SerializeField] private PerceptionSettings? _settings;

        [Tooltip("Optional: adds the mapping entries (profile, comfort switches, W gains, head weights).")]
        [SerializeField] private HeadMappingDriver? _mapping;

        [Tooltip("Optional: adds the SPEC §22 comfort entries (vignette, snap turning).")]
        [SerializeField] private ComfortSettings? _comfort;

        [Tooltip("Top-left corner in the camera's frame, metres. Right of the lab at eye level: it is tall, and nothing else is there.")]
        [SerializeField] private Vector3 _offset = new Vector3(0.5f, 0.05f, 1.15f);

        [SerializeField] private float _yawDegrees = 12f;
        [SerializeField] private bool _startVisible;

        private const float Width = 420f;
        private const float Padding = 14f;
        private const int FontSize = 17;

        private List<ParameterEntry>? _entries;
        private RectTransform? _canvas;
        private Text? _labels;
        private Text? _values;
        private const float ValueColumn = 250f;

        // Rows shown at once; the list scrolls to keep the cursor in view.
        private const int WindowRows = 20;

        public int Selected { get; private set; }

        public IReadOnlyList<ParameterEntry> Entries => _entries ??= BuildEntries();

        private List<ParameterEntry> BuildEntries()
        {
            var entries = new List<ParameterEntry>();
            if (_settings != null)
                entries.AddRange(PerceptionParameterList.For(_settings));
            if (_mapping != null)
                entries.AddRange(MappingParameterList.For(_mapping));
            if (_comfort != null)
                entries.AddRange(ComfortParameterList.For(_comfort));
            return entries;
        }

        public bool Visible
        {
            get => _canvas != null ? _canvas.gameObject.activeSelf : _startVisible;
            set
            {
                EnsurePanel().gameObject.SetActive(value);
                if (value)
                    Refresh();
            }
        }

        private void Start() => Visible = _startVisible;

        public void Toggle() => Visible = !Visible;

        public void MoveSelection(int delta)
        {
            int n = Entries.Count;
            if (n == 0)
                return;
            Selected = ((Selected + delta) % n + n) % n;
            Refresh();
        }

        public void Adjust(int steps, bool coarse)
        {
            if (Entries.Count == 0)
                return;
            Entries[Selected].Adjust(steps, coarse);
            Refresh();
        }

        public void Refresh()
        {
            RectTransform canvas = EnsurePanel();
            if (!canvas.gameObject.activeSelf)
                return;
            // Two columns: the built-in font is proportional, so padding with spaces would not line values up.
            _labels!.text = Column(labels: true);
            _values!.text = Column(labels: false);
            canvas.sizeDelta = new Vector2(Width, _labels.preferredHeight + 2f * Padding);
        }

        /// <summary>The panel's content as one string: header, then one line per parameter, cursor on the selected one.</summary>
        public string Format()
        {
            var sb = new StringBuilder("PARAMETERS    Up/Down, - / =, Shift x10\n");
            if (Entries.Count == 0)
                return sb.Append("  (nothing assigned)").ToString();
            for (int i = 0; i < Entries.Count; i++)
            {
                ParameterEntry e = Entries[i];
                sb.Append(i == Selected ? "> " : "   ").Append(e.Label).Append("  ").Append(e.Display).Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }

        private string Column(bool labels)
        {
            var sb = new StringBuilder(labels ? "PARAMETERS    Up/Down, - / =, Shift x10" : " ");
            if (Entries.Count == 0)
                return labels ? sb.Append("\n  (nothing assigned)").ToString() : sb.ToString();
            (int first, int last) = Window();
            if (first > 0)
                sb.Append('\n').Append(labels ? "   ..." : "");
            for (int i = first; i <= last; i++)
            {
                sb.Append('\n');
                if (labels)
                    sb.Append(i == Selected ? "> " : "   ").Append(Entries[i].Label);
                else
                    sb.Append(Entries[i].Display);
            }
            if (last < Entries.Count - 1)
                sb.Append('\n').Append(labels ? "   ..." : "");
            return sb.ToString();
        }

        private (int First, int Last) Window()
        {
            int n = Entries.Count;
            int first = Mathf.Clamp(Selected - WindowRows / 2, 0, Mathf.Max(n - WindowRows, 0));
            return (first, Mathf.Min(first + WindowRows, n) - 1);
        }

        private RectTransform EnsurePanel()
        {
            if (_canvas != null)
                return _canvas;
            _canvas = DebugPanel.CreateCanvas(transform, "RuntimeParameterPanel Canvas", _offset, _yawDegrees,
                new Vector2(Width, 300f), new Vector2(0f, 1f));
            _labels = DebugPanel.CreateText(_canvas, "Labels", FontSize, TextAnchor.UpperLeft, Color.white);
            DebugPanel.Stretch(_labels.rectTransform, _canvas, Padding);
            _values = DebugPanel.CreateText(_canvas, "Values", FontSize, TextAnchor.UpperLeft, new Color(1f, 0.92f, 0.35f));
            DebugPanel.Stretch(_values.rectTransform, _canvas, Padding);
            _values.rectTransform.offsetMin = new Vector2(ValueColumn, Padding);
            _canvas.gameObject.SetActive(_startVisible);
            return _canvas;
        }
    }
}
