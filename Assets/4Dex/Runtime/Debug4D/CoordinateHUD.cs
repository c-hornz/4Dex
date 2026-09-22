using FourDex.XR;
using UnityEngine;
using UnityEngine.UI;

namespace FourDex.Runtime
{
    /// <summary>
    /// Head-locked coordinate readout, visible in the headset and on the desktop mirror (SPEC §20).
    /// Put it on the XR camera. It builds its own world-space canvas, so it needs no prefab or font asset.
    /// </summary>
    /// <remarks>
    /// World space because screen-space overlay canvases are not rendered to the HMD. Head-locked is
    /// uncomfortable for content but right for a debug readout that must always be findable.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CoordinateHUD : MonoBehaviour
    {
        [SerializeField] private Player4D? _player;
        [SerializeField] private XRRig? _rig;

        [Tooltip("Panel centre in the camera's frame, in metres. About 20 degrees below the line of sight: glance down to read it, and it does not cover the lab.")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, -0.45f, 1.2f);

        [Tooltip("Text refreshes per second. Reformatting every frame allocates for no readable gain.")]
        [SerializeField, Min(1f)] private float _refreshHz = 10f;

        [SerializeField] private int _fontSize = 22;

        // 1 canvas unit = 1 mm, so sizes below read as millimetres at the panel.
        private const float MetresPerCanvasUnit = 0.001f;
        private static readonly Vector2 PanelSize = new Vector2(760f, 250f);

        private Text? _text;
        private float _nextRefresh;

        public string CurrentText => _text != null ? _text.text : string.Empty;

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
            text.text = CoordinateReadout.Format(_player != null ? _player.State : null, head);
        }

        private Text EnsurePanel()
        {
            if (_text != null)
                return _text;

            var canvasGo = new GameObject("CoordinateHUD Canvas", typeof(RectTransform));
            var canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.SetParent(transform, false);
            canvasRect.localPosition = _offset;
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one * MetresPerCanvasUnit;
            canvasRect.sizeDelta = PanelSize;

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = GetComponent<Camera>();

            var background = new GameObject("Background", typeof(RectTransform)).AddComponent<Image>();
            Stretch(background.rectTransform, canvasRect, 0f);
            background.color = new Color(0f, 0f, 0f, 0.65f);
            background.raycastTarget = false;

            _text = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
            Stretch(_text.rectTransform, canvasRect, 14f);
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = _fontSize;
            _text.color = Color.white;
            _text.alignment = TextAnchor.UpperLeft;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.supportRichText = false;
            _text.raycastTarget = false;
            return _text;
        }

        private static void Stretch(RectTransform child, RectTransform parent, float padding)
        {
            child.SetParent(parent, false);
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.offsetMin = new Vector2(padding, padding);
            child.offsetMax = new Vector2(-padding, -padding);
        }
    }
}
