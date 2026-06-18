using UnityEngine;
using UnityEngine.Tilemaps;

namespace AditusBelli.Map
{
    /// <summary>
    /// Runtime, seed-based procedural world map. Generates a <see cref="WorldMap"/>
    /// from the Inspector parameters and paints it onto the child ground tilemap with
    /// one colored diamond tile per terrain band. Standalone for now (not wired into
    /// pathfinding/gameplay): use it in the World Generator preview scene. Change the
    /// fields and press Play, or right-click the component and pick "Regenerate".
    /// </summary>
    [RequireComponent(typeof(Grid))]
    public class WorldMapGenerator : MonoBehaviour
    {
        [Header("Seed")]
        public int seed = 12345;
        [Tooltip("Pick a fresh random seed every time the map is generated.")]
        public bool randomizeSeed = false;

        [Header("Size (cells)")]
        [Min(8)] public int width = 80;
        [Min(8)] public int height = 80;

        [Header("World")]
        public WorldType worldType = WorldType.Islands;
        [Tooltip("Islands only: higher = larger central landmass.")]
        [Range(0.5f, 6f)] public float islandFalloff = 3f;

        [Header("Noise")]
        [Min(1f)] public float noiseScale = 22f;
        [Range(1, 8)] public int octaves = 5;
        [Range(0f, 1f)] public float persistence = 0.5f;
        [Range(1f, 4f)] public float lacunarity = 2f;

        [Header("Elevation thresholds (must ascend)")]
        [Range(0f, 1f)] public float deepSeaLevel = 0.30f;
        [Range(0f, 1f)] public float seaLevel = 0.42f;
        [Range(0f, 1f)] public float beachLevel = 0.46f;
        [Range(0f, 1f)] public float plainLevel = 0.66f;
        [Range(0f, 1f)] public float hillLevel = 0.84f;

        [Header("View")]
        [Tooltip("Center and zoom the main camera to frame the whole map after generating.")]
        public bool fitCameraToMap = true;

        // Per-band colors (index by TerrainType).
        private static readonly Color32[] Palette =
        {
            new Color32(18, 38, 84, 255),   // DeepSea
            new Color32(40, 92, 168, 255),  // Sea
            new Color32(222, 206, 150, 255),// Beach
            new Color32(92, 158, 74, 255),  // Plain
            new Color32(120, 138, 70, 255), // Hill
            new Color32(132, 126, 120, 255),// Mountain
        };

        private Grid _grid;
        private Tilemap _tilemap;
        private WorldMap _map;
        private Tile[] _tiles;

        public WorldMap Map => _map;
        public TerrainType Get(int x, int y) => _map != null ? _map.Get(x, y) : TerrainType.DeepSea;
        public bool IsWalkable(int x, int y) => Get(x, y).IsWalkable();

        private bool _dirty;

        private void Awake() => GenerateInternal(fitCameraToMap);

        // Live tuning while playing: changing any field in the Inspector flags a
        // rebuild that is applied on the next frame. The flag debounces it, so dragging
        // a slider regenerates once per frame instead of many times per change.
        private void OnValidate() => _dirty = true;

        private void Update()
        {
            if (_dirty) GenerateInternal(false); // keep the camera put while fine-tuning
        }

        [ContextMenu("Regenerate")]
        public void Generate() => GenerateInternal(fitCameraToMap);

        private void GenerateInternal(bool fitCamera)
        {
            _dirty = false;
            _grid = GetComponent<Grid>();
            if (_tilemap == null) _tilemap = GetComponentInChildren<Tilemap>();
            if (_tilemap == null)
            {
                Debug.LogError("[WorldMapGenerator] No child Tilemap found to paint.");
                return;
            }

            if (randomizeSeed) seed = new System.Random().Next(int.MinValue, int.MaxValue);

            var settings = new WorldGenSettings
            {
                seed = seed,
                width = width,
                height = height,
                worldType = worldType,
                noiseScale = noiseScale,
                octaves = octaves,
                persistence = persistence,
                lacunarity = lacunarity,
                islandFalloff = islandFalloff,
                deepSeaLevel = deepSeaLevel,
                seaLevel = seaLevel,
                beachLevel = beachLevel,
                plainLevel = plainLevel,
                hillLevel = hillLevel,
            };

            _map = WorldMap.Generate(settings);
            EnsureTiles();
            Paint();
            if (fitCamera) FitCamera();
        }

        private void Paint()
        {
            _tilemap.ClearAllTiles();

            int w = _map.Width, h = _map.Height;
            int halfX = w / 2, halfY = h / 2;

            var positions = new Vector3Int[w * h];
            var tiles = new TileBase[w * h];
            int i = 0;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                positions[i] = new Vector3Int(x - halfX, y - halfY, 0);
                tiles[i] = _tiles[(int)_map.Get(x, y)];
                i++;
            }

            _tilemap.SetTiles(positions, tiles);
            _tilemap.RefreshAllTiles();
        }

        private void FitCamera()
        {
            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic) return;

            int w = _map.Width, h = _map.Height;
            int halfX = w / 2, halfY = h / 2;
            Vector3 a = _grid.GetCellCenterWorld(new Vector3Int(-halfX, -halfY, 0));
            Vector3 b = _grid.GetCellCenterWorld(new Vector3Int(w - 1 - halfX, -halfY, 0));
            Vector3 c = _grid.GetCellCenterWorld(new Vector3Int(-halfX, h - 1 - halfY, 0));
            Vector3 d = _grid.GetCellCenterWorld(new Vector3Int(w - 1 - halfX, h - 1 - halfY, 0));

            float minX = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x));
            float maxX = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
            float minY = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y));
            float maxY = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));

            cam.transform.position = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f,
                cam.transform.position.z);
            float aspect = cam.aspect > 0.01f ? cam.aspect : 1.78f;
            cam.orthographicSize = Mathf.Max((maxY - minY) * 0.5f, (maxX - minX) * 0.5f / aspect) * 1.1f;
        }

        private void EnsureTiles()
        {
            if (_tiles != null) return;
            _tiles = new Tile[Palette.Length];
            for (int i = 0; i < Palette.Length; i++) _tiles[i] = MakeDiamondTile(Palette[i]);
        }

        private static Tile MakeDiamondTile(Color32 color)
        {
            const int w = 256, h = 128; // matches the game's iso cell (PPU 256 -> 1x0.5)
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var clear = new Color32(0, 0, 0, 0);
            var px = new Color32[w * h];
            float cx = w / 2f, cy = h / 2f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = Mathf.Abs(x + 0.5f - cx) / (w / 2f);
                float ny = Mathf.Abs(y + 0.5f - cy) / (h / 2f);
                px[y * w + x] = (nx + ny <= 1f) ? color : clear;
            }
            tex.SetPixels32(px);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 256f);
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }
    }
}
