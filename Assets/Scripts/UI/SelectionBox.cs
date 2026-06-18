using AditusBelli.Units;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AditusBelli.UI
{
    /// <summary>
    /// Draws the left-drag box-selection rectangle as a uGUI overlay, driven by
    /// the drag state on <see cref="UnitSelectionManager"/>. Replaces the old
    /// IMGUI DrawBox.
    /// </summary>
    public class SelectionBox : MonoBehaviour
    {
        private const float BorderThickness = 2f;

        private Canvas _canvas;
        private RectTransform _box;

        private void Start()
        {
            RectTransform root = HudController.Instance != null ? HudController.Instance.Root : null;
            if (root == null) { enabled = false; return; }
            _canvas = root.GetComponent<Canvas>();

            UnitSelectionManager sm = UnitSelectionManager.Instance;
            Color fill = sm != null ? sm.boxFill : new Color(0.3f, 0.85f, 0.45f, 0.15f);
            Color border = sm != null ? sm.boxBorder : new Color(0.4f, 0.95f, 0.55f, 0.9f);

            _box = UiFactory.Container(root, "SelectionBox");
            _box.anchorMin = _box.anchorMax = _box.pivot = Vector2.zero; // bottom-left origin (matches Input System)

            Image fillImg = UiFactory.Image(_box, "Fill", fill);
            fillImg.raycastTarget = false; // purely visual, never blocks clicks
            UiFactory.Stretch(fillImg.rectTransform);

            Edge(border, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, BorderThickness)); // top
            Edge(border, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, BorderThickness)); // bottom
            Edge(border, new Vector2(0, 0), new Vector2(0, 1), new Vector2(BorderThickness, 0)); // left
            Edge(border, new Vector2(1, 0), new Vector2(1, 1), new Vector2(BorderThickness, 0)); // right

            _box.gameObject.SetActive(false);
        }

        private void Edge(Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            Image img = UiFactory.Image(_box, "Edge", color);
            img.raycastTarget = false;
            RectTransform rt = img.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f,
                                   anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
        }

        private void Update()
        {
            UnitSelectionManager sm = UnitSelectionManager.Instance;
            Mouse mouse = Mouse.current;
            if (_box == null || sm == null || mouse == null || !sm.IsDragging)
            {
                Hide();
                return;
            }

            Vector2 start = sm.DragStart;
            Vector2 cur = mouse.position.ReadValue();
            if (Vector2.Distance(start, cur) < sm.dragThreshold) { Hide(); return; }

            float sf = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            float xMin = Mathf.Min(start.x, cur.x);
            float yMin = Mathf.Min(start.y, cur.y);

            _box.anchoredPosition = new Vector2(xMin / sf, yMin / sf);
            _box.sizeDelta = new Vector2(Mathf.Abs(start.x - cur.x) / sf, Mathf.Abs(start.y - cur.y) / sf);

            if (!_box.gameObject.activeSelf) _box.gameObject.SetActive(true);
        }

        private void Hide()
        {
            if (_box != null && _box.gameObject.activeSelf) _box.gameObject.SetActive(false);
        }
    }
}
