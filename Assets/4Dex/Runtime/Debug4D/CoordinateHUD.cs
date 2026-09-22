using System;
using System.Collections.Generic;
using FourDex.Perception;
using FourDex.XR;
using UnityEngine;
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

        [Tooltip("Top-centre of the panel in the camera's frame, in metres; the panel grows downward from here. About 20 degrees below the line of sight: glance down to read it, and it does not cover the lab.")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, -0.3f, 1.2f);

        [Tooltip("Text refreshes per second. Reformatting every frame allocates for no readable gain.")]
        [SerializeField, Min(1f)] private float _refreshHz = 10f;

        [SerializeField] private int _fontSize = 18;

        // Canvas units are millimetres (DebugPanel).
        private const float PanelWidth = 900f;
        private const float Padding = 14f;

        private Text? _text;
        private float _nextRefresh;

        public string CurrentText => _text != null ? _text.text : string.Empty;

        /// <summary>Supplies extra lines under the readout at each refresh, e.g. the desktop key legend and input monitor. Null for none.</summary>
        public Func<string>? Footer { get; set; }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + 1f / _refreshHz;
            Refresh();
        }

        public void Refresh()
        {
            Text text = EnsurePanel();
            HmdPose? head = _rig != null && _rig.HasPose ? _rig.LastPose : (HmdPose?)null;
            float? focusWidth = _projection != null && _projection.Perception != null
                ? _projection.Perception.Parameters.FocusWidth
                : (float?)null;
            string readout = CoordinateReadout.Format(_player != null ? _player.State : null, head, focusWidth, ObjectLines());
            string? footer = Footer?.Invoke();
            text.text = string.IsNullOrEmpty(footer) ? readout : readout + "\n\n" + footer;

            // Fit the panel to the text, so a footer never spills past the background.
            var panel = (RectTransform)text.rectTransform.parent;
            panel.sizeDelta = new Vector2(PanelWidth, text.preferredHeight + 2f * Padding);
        }

        private IEnumerable<(string, PerceptionChannels)>? ObjectLines()
        {
            if (_projection == null || _projection.Perception == null || _projection.World == null)
                return null;
            var lines = new List<(string, PerceptionChannels)>();
            foreach (Object4DBehaviour member in _projection.World.Members)
            {
                // First word of the GameObject name: the lab names objects "A  w=0 ...", "B  w=+1 ...".
                string name = member.name;
                int space = name.IndexOf(' ');
                lines.Add((space > 0 ? name.Substring(0, space) : name, member.Channels));
            }
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
