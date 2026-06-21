using System.Collections.Generic;
using AditusBelli.Economy;
using AditusBelli.Map;
using AditusBelli.Teams;
using AditusBelli.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AditusBelli.Buildings
{
    /// <summary>
    /// Drives building placement. Press B to open the build menu, then a number key
    /// (1-9, then 0) to pick a building from <see cref="buildable"/>. A ghost preview
    /// snaps to the grid — green when the footprint is free, affordable and (for docks)
    /// next to water, red otherwise. Left-click places a construction site (spending
    /// its cost); right-click or Esc cancels.
    /// Placement stays "active" until the placing mouse button is released, so the
    /// selection system (suppressed while active) never sees the click.
    /// </summary>
    public class BuildingPlacer : MonoBehaviour
    {
        public static BuildingPlacer Instance { get; private set; }
        public static bool IsActive { get; private set; }

        [Tooltip("Building prefabs the player can construct, in menu order (keys 1-9, then 0).")]
        public GameObject[] buildable;

        private Camera _cam;
        private GameObject _placing;
        private GameObject _ghost;
        private SpriteRenderer _ghostSr;
        private bool _awaitingRelease;
        private bool _menuOpen;

        /// <summary>Whether the build menu is currently open (read by the HUD).</summary>
        public bool IsMenuOpen => _menuOpen;
        public IReadOnlyList<GameObject> Buildable => buildable;

        private void Awake()
        {
            Instance = this;
            _cam = Camera.main;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            IsActive = false;
        }

        public void BeginPlacement(GameObject prefab)
        {
            if (prefab == null) return;
            _placing = prefab;
            _menuOpen = false;
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
                HandleMenu(keyboard);
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

            Vector2Int footprint = BuildPlacement.Footprint(_placing);
            var def = _placing.GetComponent<Building>();
            bool needsWater = def != null && def.requiresAdjacentWater;
            TeamEconomy econ = TeamManager.Instance != null ? TeamManager.Instance.LocalEconomy : null;
            bool valid = BuildPlacement.FootprintFree(grid, origin, footprint) &&
                         BuildPlacement.CanAfford(econ, _placing) &&
                         (!needsWater || BuildPlacement.HasAdjacentWater(grid, origin, footprint));

            Vector3 center = BuildPlacement.FootprintCenter(grid, origin, footprint);
            _ghost.transform.position = center;
            _ghostSr.color = valid
                ? new Color(0.4f, 1f, 0.5f, 0.5f)
                : new Color(1f, 0.4f, 0.4f, 0.5f);

            // Ignore the click that pressed a Build button (it's over the HUD).
            if (mouse.leftButton.wasPressedThisFrame && valid && !HudController.IsPointerOverUi)
            {
                TeamDef owner = TeamManager.Instance != null ? TeamManager.Instance.LocalPlayer : null;
                BuildPlacement.PlaceConstructionSite(grid, _placing, origin, owner, econ);
                _placing = null;
                _awaitingRelease = true;            // keep IsActive until the click is released
                if (_ghost != null) _ghost.SetActive(false);
            }
        }

        private void HandleMenu(Keyboard keyboard)
        {
            if (keyboard == null) return;

            if (keyboard.bKey.wasPressedThisFrame) { _menuOpen = !_menuOpen; return; }
            if (!_menuOpen) return;
            if (keyboard.escapeKey.wasPressedThisFrame) { _menuOpen = false; return; }

            int idx = SelectedIndex(keyboard);
            if (idx >= 0 && buildable != null && idx < buildable.Length)
                BeginPlacement(buildable[idx]); // also closes the menu
        }

        // Number-key layout: 1-9 then 0 (the 10th slot).
        private static int SelectedIndex(Keyboard kb)
        {
            if (kb.digit1Key.wasPressedThisFrame) return 0;
            if (kb.digit2Key.wasPressedThisFrame) return 1;
            if (kb.digit3Key.wasPressedThisFrame) return 2;
            if (kb.digit4Key.wasPressedThisFrame) return 3;
            if (kb.digit5Key.wasPressedThisFrame) return 4;
            if (kb.digit6Key.wasPressedThisFrame) return 5;
            if (kb.digit7Key.wasPressedThisFrame) return 6;
            if (kb.digit8Key.wasPressedThisFrame) return 7;
            if (kb.digit9Key.wasPressedThisFrame) return 8;
            if (kb.digit0Key.wasPressedThisFrame) return 9;
            return -1;
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
