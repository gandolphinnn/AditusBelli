using AditusBelli.Economy;
using AditusBelli.Map;
using AditusBelli.Teams;
using AditusBelli.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AditusBelli.Buildings
{
    /// <summary>
    /// Drives building placement. Press H to start placing a house: a ghost
    /// preview snaps to the grid, green when the footprint is free and affordable,
    /// red otherwise. Left-click places a construction site (spending its cost);
    /// right-click or Esc cancels. The buildable types are prefabs that carry their
    /// own <see cref="Building"/> definition.
    /// Placement stays "active" until the placing mouse button is released, so the
    /// selection system (suppressed while active) never sees the click.
    /// </summary>
    public class BuildingPlacer : MonoBehaviour
    {
        public static bool IsActive { get; private set; }

        [Tooltip("House building prefab (carries its Building definition).")]
        public GameObject houseDef;
        [Tooltip("Barracks building prefab (carries its Building definition).")]
        public GameObject barracksDef;

        private Camera _cam;
        private GameObject _placing;
        private GameObject _ghost;
        private SpriteRenderer _ghostSr;
        private bool _awaitingRelease;

        private void Awake() => _cam = Camera.main;
        private void OnDestroy() => IsActive = false;

        public void BeginPlacement(GameObject prefab)
        {
            if (prefab == null) return;
            _placing = prefab;
            _awaitingRelease = false;
            IsActive = true;
            EnsureGhost();
            var sr = prefab.GetComponent<SpriteRenderer>();
            _ghostSr.sprite = sr != null ? sr.sprite : null;
            _ghost.SetActive(true);
        }

        private void EndPlacement()
        {
            _placing = null;
            _awaitingRelease = false;
            IsActive = false;
            if (_ghost != null) _ghost.SetActive(false);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (!IsActive)
            {
                if (keyboard != null)
                {
                    if (keyboard.hKey.wasPressedThisFrame && houseDef != null) BeginPlacement(houseDef);
                    else if (keyboard.bKey.wasPressedThisFrame && barracksDef != null) BeginPlacement(barracksDef);
                }
                return;
            }

            // After placing, keep active until the mouse button is released so the
            // selection system stays suppressed for the whole click.
            if (_awaitingRelease)
            {
                if (mouse == null || !mouse.leftButton.isPressed) EndPlacement();
                return;
            }

            if (_placing == null) return;
            if (_cam == null) _cam = Camera.main;
            GameGrid grid = GameGrid.Instance;
            if (_cam == null || grid == null || mouse == null) return;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { EndPlacement(); return; }
            if (mouse.rightButton.wasPressedThisFrame) { EndPlacement(); return; }

            Vector3 world = _cam.ScreenToWorldPoint(mouse.position.ReadValue());
            Vector3Int c = grid.WorldToCell(world);
            var origin = new Vector2Int(c.x, c.y);

            Vector2Int footprint = Footprint(_placing);
            bool valid = FootprintFree(grid, origin, footprint) && CanAfford(_placing);

            Vector3 center = FootprintCenter(grid, origin, footprint);
            center.z = 0f;
            _ghost.transform.position = center;
            _ghostSr.color = valid
                ? new Color(0.4f, 1f, 0.5f, 0.5f)
                : new Color(1f, 0.4f, 0.4f, 0.5f);

            // Ignore the click that pressed a Build button (it's over the HUD).
            if (mouse.leftButton.wasPressedThisFrame && valid && !HudController.IsPointerOverUi)
            {
                PlaceAt(grid, origin, _placing);
                _placing = null;
                _awaitingRelease = true;            // keep IsActive until the click is released
                if (_ghost != null) _ghost.SetActive(false);
            }
        }

        private void PlaceAt(GameGrid grid, Vector2Int origin, GameObject prefab)
        {
            var def = prefab.GetComponent<Building>();
            TeamManager.Instance?.LocalEconomy?.TrySpend(ResourceType.Wood, def != null ? def.woodCost : 0);

            Vector3 center = FootprintCenter(grid, origin, Footprint(prefab));
            center.z = 0f;
            GameObject go = Instantiate(prefab, center, Quaternion.identity);

            var owner = go.GetComponent<Owner>();
            if (owner != null) owner.team = TeamManager.Instance != null ? TeamManager.Instance.LocalPlayer : null;

            var building = go.GetComponent<Building>();
            if (building != null)
            {
                building.originCell = origin;
                building.startCompleted = false; // construction site: villagers build it
            }
        }

        private static Vector2Int Footprint(GameObject prefab)
        {
            var b = prefab != null ? prefab.GetComponent<Building>() : null;
            return b != null ? b.footprint : new Vector2Int(1, 1);
        }

        private static bool CanAfford(GameObject prefab)
        {
            var b = prefab != null ? prefab.GetComponent<Building>() : null;
            TeamEconomy econ = TeamManager.Instance != null ? TeamManager.Instance.LocalEconomy : null;
            return econ == null || b == null || econ.Get(ResourceType.Wood) >= b.woodCost;
        }

        private static bool FootprintFree(GameGrid grid, Vector2Int origin, Vector2Int size)
        {
            for (int dx = 0; dx < Mathf.Max(1, size.x); dx++)
            for (int dy = 0; dy < Mathf.Max(1, size.y); dy++)
                if (!grid.IsWalkable(new Vector2Int(origin.x + dx, origin.y + dy))) return false;
            return true;
        }

        private static Vector3 FootprintCenter(GameGrid grid, Vector2Int origin, Vector2Int size)
        {
            Vector3 a = grid.CellCenter(origin);
            Vector3 b = grid.CellCenter(new Vector2Int(origin.x + Mathf.Max(1, size.x) - 1,
                                                       origin.y + Mathf.Max(1, size.y) - 1));
            return (a + b) * 0.5f;
        }

        private void EnsureGhost()
        {
            if (_ghost != null) return;
            _ghost = new GameObject("BuildGhost");
            _ghostSr = _ghost.AddComponent<SpriteRenderer>();
            _ghostSr.sortingOrder = 5;
            _ghost.SetActive(false);
        }
    }
}
