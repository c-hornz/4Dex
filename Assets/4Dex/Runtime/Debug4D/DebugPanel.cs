using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace FourDex.Runtime
{
    /// <summary>
    /// Builds the head-locked, world-space debug panels (HUD, W-slice viewer, parameter panel) at runtime, so none of
    /// them needs a prefab or font asset. World space because screen-space overlays are not rendered to the HMD.
    /// </summary>
    /// <remarks>1 canvas unit = 1 mm, so every size and position passed here reads as millimetres at the panel.</remarks>
    internal static class DebugPanel
    {
        public const float MetresPerUnit = 0.001f;

        private static Font? _font;
        private static Material? _material;

        public static Font Font => _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>
        /// The material every debug panel draws with: ordinary UI, except that it ignores the depth buffer.
        /// </summary>
        /// <remarks>
        /// The panels are world-space, about a metre in front of the eyes, because a screen-space overlay is never
        /// rendered to an HMD. That puts them inside the world, so in a corridor or a small room the wall you are
        /// standing next to is nearer than the panel and covers it. A readout you cannot read where the geometry is
        /// tight is worst exactly where it is wanted, so these draw on top of everything.
        /// </remarks>
        public static Material Material
        {
            get
            {
                if (_material != null)
                    return _material;
                _material = new Material(Shader.Find("UI/Default"))
                {
                    name = "Debug4D Panel (always on top)",
                    // Created at runtime and shared by every panel; never an asset, so never saved into a scene.
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _material.SetInt(ZTestId, (int)CompareFunction.Always);
                return _material;
            }
        }

        private static readonly int ZTestId = Shader.PropertyToID("unity_GUIZTestMode");

        /// <param name="localPosition">Where <paramref name="pivot"/> sits in the camera's frame, in metres.</param>
        /// <param name="yawDegrees">Turn toward the viewer, for panels placed off to one side.</param>
        public static RectTransform CreateCanvas(Transform camera, string name, Vector3 localPosition, float yawDegrees,
            Vector2 sizeMm, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(camera, false);
            rect.pivot = pivot;
            rect.localPosition = localPosition;
            rect.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            rect.localScale = Vector3.one * MetresPerUnit;
            rect.sizeDelta = sizeMm;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera.GetComponent<Camera>();

            Image background = CreateImage(rect, "Background", new Color(0f, 0f, 0f, 0.65f));
            Stretch(background.rectTransform, rect, 0f);
            return rect;
        }

        /// <summary>
        /// Shrinks a panel until it fits the camera's view, and no further. Panels grow with their contents — the
        /// readout grows a line per object, the W slice a row — so any fixed size is wrong in some scene, and a
        /// readout running off the bottom of the view is as useless as one hidden behind a wall.
        /// </summary>
        /// <remarks>
        /// Vertical only: these panels are far taller than they are wide, and the view is wider than it is tall.
        /// The floor stops a very long panel shrinking to an unreadable size; past that it is better to clip and let
        /// the per-object line caps do their work.
        /// </remarks>
        public static void FitInView(RectTransform panel, Camera? camera, float minScale = 0.45f)
        {
            if (panel == null || camera == null)
                return;

            float distance = Mathf.Abs(panel.localPosition.z);
            float height = panel.sizeDelta.y;
            if (distance < 1e-3f || height < 1e-3f)
                return;

            // What the camera can see at the panel's distance, with a small margin so nothing sits on the very edge.
            float halfHeight = 0.95f * distance * Mathf.Tan(0.5f * camera.fieldOfView * Mathf.Deg2Rad);
            float top = panel.localPosition.y;
            float scale = MetresPerUnit;

            // The pivot is where the panel hangs from; the rest of it extends above and below that point.
            float below = panel.pivot.y * height;
            if (below > 0f)
                scale = Mathf.Min(scale, (top + halfHeight) / below);
            float above = (1f - panel.pivot.y) * height;
            if (above > 0f)
                scale = Mathf.Min(scale, (halfHeight - top) / above);

            scale = Mathf.Clamp(scale, minScale * MetresPerUnit, MetresPerUnit);
            panel.localScale = Vector3.one * scale;
        }

        public static Image CreateImage(RectTransform parent, string name, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.color = color;
            image.material = Material;
            image.raycastTarget = false;
            return image;
        }

        public static Text CreateText(RectTransform parent, string name, int fontSize, TextAnchor alignment, Color color)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            text.rectTransform.SetParent(parent, false);
            text.font = Font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.material = Material;
            text.raycastTarget = false;
            return text;
        }

        public static void Stretch(RectTransform child, RectTransform parent, float padding)
        {
            child.SetParent(parent, false);
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.offsetMin = new Vector2(padding, padding);
            child.offsetMax = new Vector2(-padding, -padding);
        }

        /// <summary>Places a child by its top-left corner, in mm from the parent's top-left.</summary>
        public static void PlaceTopLeft(RectTransform child, float x, float y, float width, float height)
        {
            child.anchorMin = child.anchorMax = new Vector2(0f, 1f);
            child.pivot = new Vector2(0f, 1f);
            child.anchoredPosition = new Vector2(x, -y);
            child.sizeDelta = new Vector2(width, height);
        }
    }
}
