using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AditusBelli.UI
{
    /// <summary>
    /// Small helpers to build legacy uGUI elements from code at runtime. The HUD
    /// is created programmatically (the scene itself is code-generated), so these
    /// keep the panel classes terse. Uses the built-in font to avoid any asset
    /// import step.
    /// </summary>
    public static class UiFactory
    {
        public static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.09f, 0.82f);
        public static readonly Color ButtonColor = new Color(0.20f, 0.24f, 0.30f, 0.95f);
        public static readonly Color BarBackColor = new Color(0f, 0f, 0f, 0.6f);

        private static Font _font;
        public static Font Font => _font != null
            ? _font
            : _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>A bare RectTransform with no graphic (groups children, never blocks raycasts).</summary>
        public static RectTransform Container(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>A solid-color panel (a spriteless Image renders as a filled rect).</summary>
        public static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return rt;
        }

        public static Image Image(Transform parent, string name, Color color)
        {
            RectTransform rt = Panel(parent, name, color);
            return rt.GetComponent<Image>();
        }

        public static Text Label(Transform parent, string name, int fontSize,
            TextAnchor anchor = TextAnchor.UpperLeft, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = Font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color ?? Color.white;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button Button(Transform parent, string name, string caption, UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            go.GetComponent<Image>().color = ButtonColor;

            var button = go.GetComponent<Button>();
            if (onClick != null) button.onClick.AddListener(onClick);

            Text label = Label(rt, "Label", 14, TextAnchor.MiddleCenter);
            label.text = caption;
            Stretch(label.rectTransform);
            return button;
        }

        /// <summary>
        /// A horizontal progress/health bar. Returns the fill RectTransform; drive
        /// it with <see cref="SetBar"/>. (Image fill modes need a sprite, so we
        /// resize the fill rect via anchors instead.)
        /// </summary>
        public static RectTransform Bar(Transform parent, string name, Color fillColor)
        {
            RectTransform back = Panel(parent, name, BarBackColor);
            RectTransform fill = Image(back, name + "Fill", fillColor).rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            return fill;
        }

        public static void SetBar(RectTransform fill, float t)
        {
            t = Mathf.Clamp01(t);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(t, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }

        // ----------------------------------------------------------- anchoring

        /// <summary>Pins a fixed-size element to a single anchor point with a pixel offset.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchorAndPivot,
            Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = anchorAndPivot;
            rt.anchorMax = anchorAndPivot;
            rt.pivot = anchorAndPivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Stretches an element to fill its parent with optional padding.</summary>
        public static void Stretch(RectTransform rt, float left = 0, float bottom = 0,
            float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }
    }
}
