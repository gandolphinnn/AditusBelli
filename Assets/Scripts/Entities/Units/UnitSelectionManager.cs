using System.Collections.Generic;
using AditusBelli.Buildings;
using AditusBelli.Combat;
using AditusBelli.Economy;
using AditusBelli.Entities;
using AditusBelli.Teams;
using AditusBelli.UI;
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

        private static readonly List<Unit> AllUnitsList = new();
        private readonly List<Unit> _selected = new();

        public static UnitSelectionManager Instance { get; private set; }
        public ResourceNode SelectedNode { get; private set; }
        public Building SelectedBuilding { get; private set; }
        public Unit SelectedUnit { get; private set; } // single inspected unit (any owner), not command-selected
        public IReadOnlyList<Unit> Selected => _selected;

        // Drag state, read by the HUD SelectionBox to draw the box-select rectangle.
        public bool IsDragging => _dragging;
        public Vector2 DragStart => _dragStart;

        /// <summary>All registered units (any owner). Used by the minimap and fog of war.</summary>
        public static IReadOnlyList<Unit> AllUnits => AllUnitsList;
        public static int UnitCountForTeam(TeamDef team)
        {
            if (team == null) return AllUnitsList.Count;
            int count = 0;
            foreach (Unit u in AllUnitsList)
            {
                if (u == null) continue;
                if (u.Team == team) count++;
            }
            return count;
        }

        private Camera _cam;
        private Vector2 _dragStart;
        private bool _dragging;

        public static void Register(Unit u) { if (!AllUnitsList.Contains(u)) AllUnitsList.Add(u); }

        public static void Unregister(Unit u)
        {
            AllUnitsList.Remove(u);
            if (Instance != null) Instance._selected.Remove(u); // drop destroyed units from the selection
        }

        private void Awake()
        {
            Instance = this;
            _cam = Camera.main;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            _selected.RemoveAll(u => u == null); // drop any destroyed units from the selection

            if (_cam == null) _cam = Camera.main;
            var mouse = Mouse.current;
            if (_cam == null || mouse == null) return;
            if (BuildingPlacer.IsActive) return; // building placement consumes input

            // Clicks that start over the HUD belong to the HUD, not the world.
            bool overUi = HudController.IsPointerOverUi;

            if (mouse.leftButton.wasPressedThisFrame && !overUi)
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

            if (mouse.rightButton.wasPressedThisFrame && !overUi)
                HandleMoveCommand(mouse.position.ReadValue());
        }

        private static bool ShiftHeld()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        }

        private static bool IsLocalPlayerUnit(Unit u)
        {
            if (u == null) return false;
            return TeamManager.Instance != null && u.Team == TeamManager.Instance.LocalPlayer;
        }

        private static bool IsMobile(Unit u)
        {
            var combatant = u.GetComponent<Combatant>();
            return combatant == null || combatant.mobile;
        }

        // Only the player's own mobile units can be multi-selected (Shift / box).
        private static bool IsMultiSelectable(Unit u) => IsLocalPlayerUnit(u) && IsMobile(u);

        private static bool IsEnemy(Entity e)
        {
            return e != null && e.Team != null && TeamManager.Instance != null &&
                   e.Team != TeamManager.Instance.LocalPlayer;
        }

        private void HandleSingleClick(Vector2 screenPos)
        {
            Vector3 world = _cam.ScreenToWorldPoint(screenPos);
            Collider2D hit = Physics2D.OverlapPoint(world);
            Unit unit = hit != null ? hit.GetComponentInParent<Unit>() : null;

            // Shift only toggles multi-selection of the player's own mobile units.
            if (ShiftHeld())
            {
                if (unit != null && IsMultiSelectable(unit))
                {
                    SelectedNode = null;
                    SelectedBuilding = null;
                    SelectedUnit = null;
                    if (unit.IsSelected) Deselect(unit);
                    else Select(unit);
                }
                return;
            }

            // Plain single click: clear, then inspect whatever is under the cursor.
            ClearSelection();

            if (unit != null)
            {
                if (IsMultiSelectable(unit)) Select(unit); // own mobile unit: also command-selectable
                else SelectedUnit = unit;                  // any other unit (enemy, immobile): inspect only
                return;
            }

            ResourceNode node = hit != null ? hit.GetComponentInParent<ResourceNode>() : null;
            if (node != null) { SelectedNode = node; return; }

            Building building = hit != null ? hit.GetComponentInParent<Building>() : null;
            if (building != null) SelectedBuilding = building;
        }

        private void HandleBoxSelect(Vector2 a, Vector2 b)
        {
            SelectedNode = null;
            SelectedBuilding = null;
            SelectedUnit = null;
            if (!ShiftHeld()) ClearSelection();

            Rect rect = ScreenRect(a, b);
            foreach (Unit u in AllUnitsList)
            {
                if (!IsMultiSelectable(u)) continue;
                Vector2 sp = _cam.WorldToScreenPoint(u.transform.position);
                if (rect.Contains(sp)) Select(u);
            }
        }

        private void HandleMoveCommand(Vector2 screenPos)
        {
            Vector3 world = _cam.ScreenToWorldPoint(screenPos);

            if (_selected.Count == 0)
            {
                // No units selected: right-click sets a production building's rally point.
                if (SelectedBuilding != null)
                {
                    var producer = SelectedBuilding.GetComponent<UnitProducer>();
                    if (producer != null) producer.SetRallyPoint(world);
                }
                return;
            }

            Collider2D hit = Physics2D.OverlapPoint(world);

            // Right-clicking an enemy entity orders an attack.
            Entity enemy = hit != null ? hit.GetComponentInParent<Entity>() : null;
            if (enemy != null && IsEnemy(enemy))
            {
                foreach (Unit u in _selected)
                {
                    var combatant = u.GetComponent<Combatant>();
                    if (combatant != null) combatant.AttackTarget(enemy);
                    else u.MoveTo(enemy.transform.position);
                }
                return;
            }

            // Right-clicking a resource node sends villagers to gather it.
            ResourceNode node = hit != null ? hit.GetComponentInParent<ResourceNode>() : null;
            if (node != null)
            {
                foreach (Unit u in _selected)
                {
                    var villager = u.GetComponent<Villager>();
                    if (villager != null) villager.GatherFrom(node);
                    else u.MoveTo(node.transform.position);
                }
                return;
            }

            // Right-clicking an unfinished building sends villagers to build it.
            Building building = hit != null ? hit.GetComponentInParent<Building>() : null;
            if (building != null && !building.IsComplete)
            {
                foreach (Unit u in _selected)
                {
                    var villager = u.GetComponent<Villager>();
                    if (villager != null) villager.BuildAt(building);
                    else u.MoveTo(building.transform.position);
                }
                return;
            }

            // Otherwise: plain move order in formation, cancelling current tasks.
            world.z = 0f;
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

                var villager = _selected[i].GetComponent<Villager>();
                if (villager != null) villager.StopTasks();
                var combatant = _selected[i].GetComponent<Combatant>();
                if (combatant != null) combatant.StopCombat();
                _selected[i].MoveTo(world + offset);
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
            foreach (Unit u in _selected)
                if (u != null) u.SetSelected(false);
            _selected.Clear();
            SelectedNode = null;
            SelectedBuilding = null;
            SelectedUnit = null;
        }

        private static Rect ScreenRect(Vector2 a, Vector2 b)
        {
            float xMin = Mathf.Min(a.x, b.x);
            float yMin = Mathf.Min(a.y, b.y);
            return new Rect(xMin, yMin, Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }
    }
}
