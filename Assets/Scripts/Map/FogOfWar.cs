using System.Collections.Generic;
using AditusBelli.Buildings;
using AditusBelli.Entities;
using AditusBelli.Teams;
using AditusBelli.Units;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace AditusBelli.Map
{
    /// <summary>
    /// Standard 3-state fog of war (unseen / explored / visible). Maintains a
    /// <see cref="VisibilityModel"/> revealed each tick by the local player's units
    /// and buildings, renders it as a per-cell-tinted overlay tilemap aligned to the
    /// isometric grid, and hides enemy units/buildings that are not currently in
    /// sight. Re-fogs (demotes to explored) areas the player has left. No last-seen
    /// memory: enemies vanish when out of sight. Press F to toggle the fog.
    /// </summary>
    [RequireComponent(typeof(Grid))]
    public class FogOfWar : MonoBehaviour
    {
        public static FogOfWar Instance { get; private set; }

        [Tooltip("Sight radius (in cells) of the local player's units.")]
        public int unitVision = 6;
        [Tooltip("Sight radius (in cells) of the local player's buildings.")]
        public int buildingVision = 9;
        [Tooltip("How often the fog is recomputed, in seconds.")]
        public float refreshInterval = 0.1f;

        private static readonly Color UnseenColor = new Color(0f, 0f, 0f, 1f);
        private static readonly Color ExploredColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color VisibleColor = new Color(0f, 0f, 0f, 0f);

        private GameGrid _grid;
        private TeamDef _localTeam;
        private VisibilityModel _model;

        private Tilemap _fogMap;
        private TilemapRenderer _fogRenderer;
        private Visibility[] _pushed;

        private readonly Dictionary<GameObject, Hideable> _hideables = new();
        private float _timer;
        private bool _fogEnabled = true;

        private struct Hideable
        {
            public SpriteRenderer[] renderers;
            public Collider2D collider;
        }

        /// <summary>Current sight state at a world position (used by the minimap).</summary>
        public Visibility VisibilityAtWorld(Vector3 world)
        {
            if (!_fogEnabled || _model == null || _grid == null) return Visibility.Visible;
            Vector3Int c = _grid.WorldToCell(world);
            return _model.Get(c.x, c.y);
        }

        /// <summary>
        /// True if <paramref name="target"/> lies within a unit's sight radius of
        /// <paramref name="from"/> — the same circular cell radius (<see cref="unitVision"/>)
        /// used to uncover the fog. Geometry only, independent of whether the fog is shown.
        /// </summary>
        public bool IsWithinUnitSight(Vector3 from, Vector3 target)
        {
            if (_grid == null) return true;
            Vector3Int a = _grid.WorldToCell(from);
            Vector3Int b = _grid.WorldToCell(target);
            int dx = a.x - b.x, dy = a.y - b.y;
            return dx * dx + dy * dy <= unitVision * unitVision;
        }

        public bool IsEnabled => _fogEnabled;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            _grid = GameGrid.Instance;
            if (_grid == null) { enabled = false; return; }

            _localTeam = TeamManager.Instance != null ? TeamManager.Instance.LocalPlayer : null;

            RectInt cb = _grid.CellBounds;
            _model = new VisibilityModel(cb.xMin, cb.yMin, cb.width, cb.height);
            _pushed = new Visibility[cb.width * cb.height]; // defaults to Unseen, matching the initial fill

            BuildFogTilemap(cb);
            UpdateFog(); // reveal immediately so the start area isn't black for a frame
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) ToggleFog();
            if (!_fogEnabled) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = refreshInterval;
            UpdateFog();
        }

        // -------------------------------------------------------------- build

        private void BuildFogTilemap(RectInt cb)
        {
            var go = new GameObject("FogOverlay");
            go.transform.SetParent(transform, false);

            _fogMap = go.AddComponent<Tilemap>();
            _fogRenderer = go.AddComponent<TilemapRenderer>();
            _fogRenderer.sortOrder = TilemapRenderer.SortOrder.TopRight;
            _fogRenderer.sortingOrder = 10; // above units (2) and the build ghost (5)

            Tile tile = MakeFogTile();

            int count = cb.width * cb.height;
            var positions = new Vector3Int[count];
            var tiles = new TileBase[count];
            int i = 0;
            for (int y = cb.yMin; y < cb.yMax; y++)
            for (int x = cb.xMin; x < cb.xMax; x++)
            {
                positions[i] = new Vector3Int(x, y, 0);
                tiles[i] = tile;
                i++;
            }
            _fogMap.SetTiles(positions, tiles);

            // Allow per-cell tinting and start everything unseen (black).
            foreach (Vector3Int p in positions)
            {
                _fogMap.SetTileFlags(p, TileFlags.None);
                _fogMap.SetColor(p, UnseenColor);
            }
        }

        private static Tile MakeFogTile()
        {
            const int w = 256, h = 128; // matches the ground tile size (PPU 256 -> 1x0.5 cell)
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, // hard edges so diamonds tile without seams
                wrapMode = TextureWrapMode.Clamp,
            };
            var black = new Color32(0, 0, 0, 255);
            var clear = new Color32(0, 0, 0, 0);
            var px = new Color32[w * h];
            float cx = w / 2f, cy = h / 2f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = Mathf.Abs(x + 0.5f - cx) / (w / 2f);
                float ny = Mathf.Abs(y + 0.5f - cy) / (h / 2f);
                px[y * w + x] = (nx + ny <= 1f) ? black : clear;
            }
            tex.SetPixels32(px);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 256f);
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            tile.flags = TileFlags.None;
            return tile;
        }

        // ------------------------------------------------------------- update

        private void UpdateFog()
        {
            _model.DowngradeVisibleToExplored();
            RevealSources();
            PushColors();
            HideEnemies();
        }

        private void RevealSources()
        {
            foreach (Unit u in UnitSelectionManager.AllUnits)
            {
                if (u == null || !IsLocal(u)) continue;
                RevealAround(u.transform.position, unitVision);
            }
            foreach (Building b in Building.AllBuildings)
            {
                if (b == null || !IsLocal(b)) continue;
                RevealAround(b.transform.position, buildingVision);
            }
        }

        private void RevealAround(Vector3 world, int radius)
        {
            Vector3Int c = _grid.WorldToCell(world);
            int r2 = radius * radius;
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
                if (dx * dx + dy * dy <= r2) _model.Reveal(c.x + dx, c.y + dy);
        }

        private void PushColors()
        {
            for (int y = _model.OriginY; y < _model.OriginY + _model.Height; y++)
            for (int x = _model.OriginX; x < _model.OriginX + _model.Width; x++)
            {
                int idx = _model.Index(x, y);
                Visibility s = _model.Get(x, y);
                if (_pushed[idx] == s) continue;
                _pushed[idx] = s;
                _fogMap.SetColor(new Vector3Int(x, y, 0), ColorFor(s));
            }
        }

        private void HideEnemies()
        {
            foreach (Unit u in UnitSelectionManager.AllUnits)
                if (u != null && IsEnemy(u)) ApplyVisibility(u.gameObject);

            foreach (Building b in Building.AllBuildings)
                if (b != null && IsEnemy(b)) ApplyVisibility(b.gameObject);
        }

        private void ApplyVisibility(GameObject go)
        {
            Vector3Int c = _grid.WorldToCell(go.transform.position);
            bool visible = _model.Get(c.x, c.y) == Visibility.Visible;
            SetEntityVisible(go, visible);
        }

        private void SetEntityVisible(GameObject go, bool visible)
        {
            if (!_hideables.TryGetValue(go, out Hideable h))
            {
                h = new Hideable
                {
                    renderers = go.GetComponentsInChildren<SpriteRenderer>(true),
                    collider = go.GetComponent<Collider2D>(),
                };
                _hideables[go] = h;
            }

            foreach (SpriteRenderer r in h.renderers)
                if (r != null) r.enabled = visible;
            if (h.collider != null) h.collider.enabled = visible;
        }

        // -------------------------------------------------------------- toggle

        private void ToggleFog()
        {
            _fogEnabled = !_fogEnabled;
            if (_fogRenderer != null) _fogRenderer.enabled = _fogEnabled;

            if (!_fogEnabled)
            {
                // Reveal everything currently tracked so nothing stays hidden.
                foreach (Hideable h in _hideables.Values)
                {
                    foreach (SpriteRenderer r in h.renderers)
                        if (r != null) r.enabled = true;
                    if (h.collider != null) h.collider.enabled = true;
                }
            }
        }

        // -------------------------------------------------------------- helpers

        private bool IsLocal(Entity e) => e != null && e.Team != null && e.Team == _localTeam;

        private bool IsEnemy(Entity e) => e != null && e.Team != null && e.Team != _localTeam;

        private static Color ColorFor(Visibility s) => s switch
        {
            Visibility.Visible => VisibleColor,
            Visibility.Explored => ExploredColor,
            _ => UnseenColor,
        };
    }
}
