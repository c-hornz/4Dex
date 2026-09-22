using UnityEngine;
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

        public static Font Font => _font != null ? _font : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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

        public static Image CreateImage(RectTransform parent, string name, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.color = color;
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
