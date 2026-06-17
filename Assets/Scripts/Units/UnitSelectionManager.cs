using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AditusBelli.Units
{
    /// <summary>
    /// RTS-style selection and commands (new Input System):
    /// - left click: select the single unit under the cursor;
    /// - Shift + click: add/remove from the selection;
    /// - left drag: box selection;
    /// - right click: order the selected units to move.
    /// </summary>
    public class UnitSelectionManager : MonoBehaviour
    {
        [Tooltip("Minimum drag (px) above which a click becomes a box selection.")]
        public float dragThreshold = 8f;
        [Tooltip("Distance between units in the arrival formation.")]
        public float formationSpacing = 0.6f;

        public Color boxFill = new Color(0.3f, 0.85f, 0.45f, 0.15f);
        public Color boxBorder = new Color(0.4f, 0.95f, 0.55f, 0.9f);

        private static readonly List<Unit> AllUnits = new();
        private readonly List<Unit> _selected = new();

        private Camera _cam;
        private Vector2 _dragStart;
        private bool _dragging;

        public static void Register(Unit u) { if (!AllUnits.Contains(u)) AllUnits.Add(u); }
        public static void Unregister(Unit u) => AllUnits.Remove(u);

        private void Awake() => _cam = Camera.main;

        private void Update()
        {
            if (_cam == null) _cam = Camera.main;
            var mouse = Mouse.current;
            if (_cam == null || mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                _dragStart = mouse.position.ReadValue();
                _dragging = true;
            }

            if (_dragging && mouse.leftButton.wasReleasedThisFrame)
            {
                _dragging = false;
                Vector2 end = mouse.position.ReadValue();
                if (Vector2.Distance(_dragStart, end) < dragThreshold)
                    HandleSingleClick(end);
                else
                    HandleBoxSelect(_dragStart, end);
            }

            if (mouse.rightButton.wasPressedThisFrame)
                HandleMoveCommand(mouse.position.ReadValue());
        }

        private static bool ShiftHeld()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        }

        private void HandleSingleClick(Vector2 screenPos)
        {
            Vector3 world = _cam.ScreenToWorldPoint(screenPos);
            Collider2D hit = Physics2D.OverlapPoint(world);
            Unit unit = hit != null ? hit.GetComponentInParent<Unit>() : null;

            if (!ShiftHeld()) ClearSelection();

            if (unit == null) return;

            if (unit.IsSelected && ShiftHeld()) Deselect(unit);
            else Select(unit);
        }

        private void HandleBoxSelect(Vector2 a, Vector2 b)
        {
            if (!ShiftHeld()) ClearSelection();

            Rect rect = ScreenRect(a, b);
            foreach (Unit u in AllUnits)
            {
                Vector2 sp = _cam.WorldToScreenPoint(u.transform.position);
                if (rect.Contains(sp)) Select(u);
            }
        }

        private void HandleMoveCommand(Vector2 screenPos)
        {
            if (_selected.Count == 0) return;

            Vector3 center = _cam.ScreenToWorldPoint(screenPos);
            center.z = 0f;

            int count = _selected.Count;
            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
            for (int i = 0; i < count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                var offset = new Vector3(
                    (col - (cols - 1) * 0.5f) * formationSpacing,
                    (row - (cols - 1) * 0.5f) * formationSpacing * 0.5f, // compressed for isometric
                    0f);
                _selected[i].MoveTo(center + offset);
            }
        }

        private void Select(Unit u)
        {
            if (_selected.Contains(u)) return;
            _selected.Add(u);
            u.SetSelected(true);
        }

        private void Deselect(Unit u)
        {
            if (_selected.Remove(u)) u.SetSelected(false);
        }

        private void ClearSelection()
        {
            foreach (Unit u in _selected) u.SetSelected(false);
            _selected.Clear();
        }

        private static Rect ScreenRect(Vector2 a, Vector2 b)
        {
            float xMin = Mathf.Min(a.x, b.x);
            float yMin = Mathf.Min(a.y, b.y);
            return new Rect(xMin, yMin, Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

        private void OnGUI()
        {
            if (!_dragging) return;
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 cur = mouse.position.ReadValue();
            if (Vector2.Distance(_dragStart, cur) < dragThreshold) return;

            Rect r = ScreenRect(_dragStart, cur);
            // Input origin is bottom-left; GUI origin is top-left.
            var guiRect = new Rect(r.xMin, Screen.height - r.yMax, r.width, r.height);
            DrawBox(guiRect);
        }

        private void DrawBox(Rect r)
        {
            Color old = GUI.color;

            GUI.color = boxFill;
            GUI.DrawTexture(r, Texture2D.whiteTexture);

            GUI.color = boxBorder;
            const float t = 1.5f;
            GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMin, r.yMax - t, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMin, r.yMin, t, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t, r.yMin, t, r.height), Texture2D.whiteTexture);

            GUI.color = old;
        }
    }
}
