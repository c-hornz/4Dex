using System.Collections.Generic;
using System.Linq;
using FourDex.Perception;
using UnityEngine;
using UnityEngine.UI;

namespace FourDex.Runtime
{
    /// <summary>
    /// SPEC §20's W slice viewer: a head-locked chart of where everything sits along the player's W axis. One bar
    /// per object spans its W extent; the focus line and its LOCAL / NEAR_W / FAR_W bands are shaded around it.
    /// Put it on the XR camera, next to the HUD.
    /// </summary>
    /// <remarks>
    /// Axis is viewer-frame W, i.e. what the projection sees: kata to the left, ana to the right, the player at the
    /// centre. Bar brightness is the object's visibility, so the chart and the scene should always agree.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class WSliceViewer : MonoBehaviour
    {
        [SerializeField] private ProjectionDriver? _projection;
        [SerializeField] private Player4D? _player;

        [Tooltip("W shown either side of the player.")]
        [SerializeField, Min(0.1f)] private float _halfRange = 2f;

        [Tooltip("Top-right corner of the chart in the camera's frame, metres. Sits left of the HUD.")]
        [SerializeField] private Vector3 _offset = new Vector3(-0.47f, -0.3f, 1.15f);

        [SerializeField] private float _yawDegrees = -12f;
        [SerializeField, Min(1f)] private float _refreshHz = 20f;

        [Tooltip("Most bars to draw, nearest in W first: a maze has dozens of walls.")]
        [SerializeField, Min(1)] private int _maxRows = 12;

        // Chart geometry in mm.
        private const float Width = 520f;
        private const float LabelColumn = 44f;
        private const float PlotRight = Width - 16f;
        private const float Top = 34f;
        private const float RowHeight = 24f;
        private const float BarHeight = 14f;
        private const float AxisHeight = 52f; // tick labels, then the focus label under them
        private const int FontSize = 16;

        private static readonly Color BandLocal = new Color(1f, 1f, 1f, 0.16f);
        private static readonly Color BandNear = new Color(1f, 1f, 1f, 0.09f);
        private static readonly Color BandFar = new Color(1f, 1f, 1f, 0.04f);

        private RectTransform? _canvas;
        private Image? _bandFar, _bandNear, _bandLocal, _focusLine, _playerLine;
        private Text? _focusLabel, _header;
        private readonly List<Row> _rows = new List<Row>();
        private readonly List<Text> _ticks = new List<Text>();
        private float _nextRefresh;

        private sealed class Row
        {
            public Text Label = null!;
            public Image Bar = null!;
            public Text ClipKata = null!;
            public Text ClipAna = null!;
        }

        public bool Visible
        {
            get => _canvas == null || _canvas.gameObject.activeSelf;
            set
            {
                EnsureChart();
                _canvas!.gameObject.SetActive(value);
            }
        }

        public int RowCount => _rows.Count;

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + 1f / _refreshHz;
            Refresh();
        }

        public void Refresh()
        {
            EnsureChart();
            if (!_canvas!.gameObject.activeSelf || _projection == null || _projection.World == null || _player == null)
                return;

            IReadOnlyList<Object4DBehaviour> members = _projection.World.Members;
            if (members.Count > _maxRows)
                members = members.OrderBy(m => Mathf.Abs(m.LastProjection.W)).Take(_maxRows).ToList();
            PerceptionParameters? p = _projection.Perception != null ? _projection.Perception.Parameters.Validated() : null;
            float focus = _player.State.FocusW;

            while (_rows.Count < members.Count)
                _rows.Add(CreateRow(_rows.Count));
            for (int i = 0; i < _rows.Count; i++)
                SetRowActive(_rows[i], i < members.Count);

            float plotHeight = members.Count * RowHeight;
            _canvas.sizeDelta = new Vector2(Width, Top + plotHeight + AxisHeight);
            DebugPanel.FitInView(_canvas, GetComponent<Camera>());

            // Bands around focus: the chart shows *why* each object renders as it does.
            // Survey widens B and C, so the shading shows survey reaching further into ana and kata.
            if (p != null)
            {
                (float a, float b, float c) = PerceptionModel.Bands(p, _projection.SurveyAmount);
                PlaceSpan(_bandFar!, WSliceLayout.Interval(focus, c, _halfRange), plotHeight);
                PlaceSpan(_bandNear!, WSliceLayout.Interval(focus, b, _halfRange), plotHeight);
                PlaceSpan(_bandLocal!, WSliceLayout.Interval(focus, a, _halfRange), plotHeight);
            }
            _bandFar!.enabled = _bandNear!.enabled = _bandLocal!.enabled = p != null;

            PlaceLine(_playerLine!, WSliceLayout.ToUnit(0f, _halfRange), plotHeight);
            PlaceLine(_focusLine!, WSliceLayout.ToUnit(focus, _halfRange), plotHeight);
            _focusLabel!.text = "focus " + focus.ToString("+0.00;-0.00;+0.00");
            DebugPanel.PlaceTopLeft(_focusLabel.rectTransform, AxisX(WSliceLayout.ToUnit(focus, _halfRange)) - 60f, Top + plotHeight + 26f, 120f, 20f);

            for (int i = 0; i < members.Count; i++)
                UpdateRow(_rows[i], members[i], i);

            PlaceTicks(plotHeight);
        }

        private void UpdateRow(Row row, Object4DBehaviour member, int index)
        {
            float y = Top + index * RowHeight;
            // "A  w=0 ..." shortens at the double space; anything else is trimmed to fit the narrow label column.
            string name = member.name;
            int doubleSpace = name.IndexOf("  ", System.StringComparison.Ordinal);
            row.Label.text = doubleSpace > 0 ? name.Substring(0, doubleSpace)
                : name.Length > 9 ? name.Substring(0, 9)
                : name;
            DebugPanel.PlaceTopLeft(row.Label.rectTransform, 10f, y, LabelColumn - 10f, RowHeight);

            WSliceLayout.Span span = WSliceLayout.Interval(member.LastProjection.W, member.ViewerHalfExtentW, _halfRange);
            float x0 = AxisX(span.Start), x1 = AxisX(span.End);
            // Keep a sliver even for W-thin objects, so a zero-width object still shows where it is.
            float width = Mathf.Max(x1 - x0, 3f);
            DebugPanel.PlaceTopLeft(row.Bar.rectTransform, x0, y + (RowHeight - BarHeight) * 0.5f, width, BarHeight);

            PerceptionChannels c = member.Channels;
            bool hidden = c.Band == OcclusionBand.Outside;
            float grey = hidden ? 0.28f : Mathf.Lerp(0.4f, 1f, c.Visibility);
            row.Bar.color = new Color(grey, grey, grey, 1f);

            row.ClipKata.enabled = span.ClippedKata;
            row.ClipAna.enabled = span.ClippedAna;
            DebugPanel.PlaceTopLeft(row.ClipKata.rectTransform, LabelColumn - 2f, y, 14f, RowHeight);
            DebugPanel.PlaceTopLeft(row.ClipAna.rectTransform, PlotRight - 6f, y, 14f, RowHeight);
        }

        private void PlaceTicks(float plotHeight)
        {
            int whole = Mathf.FloorToInt(_halfRange);
            int needed = 2 * whole + 1;
            while (_ticks.Count < needed)
                _ticks.Add(DebugPanel.CreateText(_canvas!, "Tick", FontSize - 2, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.7f)));
            for (int i = 0; i < _ticks.Count; i++)
            {
                bool used = i < needed;
                _ticks[i].enabled = used;
                if (!used)
                    continue;
                int w = i - whole;
                _ticks[i].text = w == 0 ? "you" : w.ToString("+0;-0");
                DebugPanel.PlaceTopLeft(_ticks[i].rectTransform, AxisX(WSliceLayout.ToUnit(w, _halfRange)) - 25f, Top + plotHeight + 6f, 50f, 20f);
            }
        }

        private float AxisX(float unit) => LabelColumn + unit * (PlotRight - LabelColumn);

        private void PlaceSpan(Image image, WSliceLayout.Span span, float plotHeight) =>
            DebugPanel.PlaceTopLeft(image.rectTransform, AxisX(span.Start), Top, AxisX(span.End) - AxisX(span.Start), plotHeight);

        private void PlaceLine(Image image, float unit, float plotHeight) =>
            DebugPanel.PlaceTopLeft(image.rectTransform, AxisX(unit) - 1f, Top - 6f, 2f, plotHeight + 12f);

        private Row CreateRow(int index)
        {
            var row = new Row
            {
                Label = DebugPanel.CreateText(_canvas!, $"Row {index} label", FontSize, TextAnchor.MiddleLeft, Color.white),
                Bar = DebugPanel.CreateImage(_canvas!, $"Row {index} bar", Color.white),
                ClipKata = DebugPanel.CreateText(_canvas!, $"Row {index} <", FontSize, TextAnchor.MiddleCenter, Color.white),
                ClipAna = DebugPanel.CreateText(_canvas!, $"Row {index} >", FontSize, TextAnchor.MiddleCenter, Color.white),
            };
            row.ClipKata.text = "<";
            row.ClipAna.text = ">";
            return row;
        }

        private static void SetRowActive(Row row, bool active)
        {
            row.Label.gameObject.SetActive(active);
            row.Bar.gameObject.SetActive(active);
            row.ClipKata.gameObject.SetActive(active);
            row.ClipAna.gameObject.SetActive(active);
        }

        private void EnsureChart()
        {
            if (_canvas != null)
                return;

            _canvas = DebugPanel.CreateCanvas(transform, "WSliceViewer Canvas", _offset, _yawDegrees,
                new Vector2(Width, 200f), new Vector2(1f, 1f));
            // Drawn in creation order: bands under lines under bars.
            _bandFar = DebugPanel.CreateImage(_canvas, "Band FAR_W", BandFar);
            _bandNear = DebugPanel.CreateImage(_canvas, "Band NEAR_W", BandNear);
            _bandLocal = DebugPanel.CreateImage(_canvas, "Band LOCAL", BandLocal);
            _playerLine = DebugPanel.CreateImage(_canvas, "Player line", new Color(1f, 1f, 1f, 0.35f));
            _focusLine = DebugPanel.CreateImage(_canvas, "Focus line", new Color(1f, 0.92f, 0.35f, 0.95f));
            _header = DebugPanel.CreateText(_canvas, "Header", FontSize, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.8f));
            _header.text = "W SLICE    kata (left)  /  ana (right)";
            DebugPanel.PlaceTopLeft(_header.rectTransform, 10f, 8f, 300f, 20f);
            _focusLabel = DebugPanel.CreateText(_canvas, "Focus label", FontSize - 2, TextAnchor.UpperCenter, new Color(1f, 0.92f, 0.35f, 1f));
        }
    }
}
