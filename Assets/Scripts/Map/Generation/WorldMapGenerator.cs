using System.Collections.Generic;
using System.Globalization;
using AditusBelli.Economy;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AditusBelli.Map
{
    /// <summary>How big the map is.</summary>
    public enum WorldSize { Small, Medium, Large }

    /// <summary>How much gatherable resource is on the map.</summary>
    public enum ResourceAmount { Scarce, Normal, Abundant }

    /// <summary>
    /// Runtime, seed-based procedural world generator. From a few high-level presets
    /// (size, type, resources, players) plus tunable noise/threshold "recipe" knobs it
    /// generates a <see cref="WorldMap"/>, paints it onto the child ground tilemap, and
    /// computes a <see cref="MatchLayout"/> (one city center per player + scattered
    /// resources). In the WorldGen preview scene it draws markers for that layout so a
    /// preset can be tuned visually. See WORLD_GEN.md. Change fields and press Play (live
    /// tuning), or use the context menu (Regenerate / Copy / Paste settings).
    /// </summary>
    [RequireComponent(typeof(Grid))]
    public class WorldMapGenerator : MonoBehaviour
    {
        [Header("Seed")]
        public int seed = 12345;
        [Tooltip("Pick a fresh random seed every time the map is generated.")]
        public bool randomizeSeed = false;

        [Header("Presets")]
        public WorldSize size = WorldSize.Medium;
        [Tooltip("Only Pangea is implemented for now (see WORLD_GEN.md).")]
        public WorldType worldType = WorldType.Pangea;
        public ResourceAmount resources = ResourceAmount.Normal;
        [Tooltip("1 human (slot 0) + up to 3 AI. One city center per player.")]
        [Range(2, 4)] public int playerCount = 2;

        [Header("Recipe source")]
        [Tooltip("ON (game): read the terrain recipe from code (WorldRecipes) by world type. " +
                 "OFF (WorldGen sandbox): tune the recipe fields below, then 'Copy recipe as C#'.")]
        public bool useRecipeFromCode = false;

        [Header("Pangea shape")]
        [Tooltip("Higher = larger central landmass. The map edge is always sea.")]
        [Range(1f, 6f)] public float islandFalloff = 3.5f;

        [Header("Noise recipe")]
        [Tooltip("Feature size, calibrated to a 100-tile map; auto-scales with Size so the " +
                 "landmass stays coherent at any size.")]
        [Min(1f)] public float noiseScale = 30f;
        [Range(1, 8)] public int octaves = 5;
        [Range(0f, 1f)] public float persistence = 0.5f;
        [Range(1f, 4f)] public float lacunarity = 2f;

        [Header("Elevation thresholds (must ascend)")]
        [Range(0f, 1f)] public float deepSeaLevel = 0.30f;
        [Range(0f, 1f)] public float seaLevel = 0.38f;
        [Range(0f, 1f)] public float beachLevel = 0.43f;
        [Range(0f, 1f)] public float plainLevel = 0.68f;
        [Range(0f, 1f)] public float hillLevel = 0.85f;
        [Tooltip("Beach is kept only within this many tiles of water; inland sand becomes " +
                 "Plain. 0 = allow inland beaches.")]
        [Range(0, 15)] public int beachWaterRadius = 3;

        [Header("Preview")]
        [Tooltip("Draw city-center / resource markers (tuning only; off for gameplay).")]
        public bool drawLayoutMarkers = false;
        [Tooltip("Center and zoom the main camera to frame the whole map after generating.")]
        public bool fitCameraToMap = true;

        private const int PreviewDim = 128;     // reduced resolution while live-tuning
        private const float SettleDelay = 0.4f;  // seconds of no change before a full-res rebuild
        private const int Footprint = 2;         // city-center footprint
        private const int EdgeMargin = 2;        // keep city centers off the map edge
        private const int FairnessRadius = 12;   // guaranteed resources within this radius of a base

        // Per-band terrain colors (index by TerrainType).
        private static readonly Color32[] Palette =
        {
            new Color32(18, 38, 84, 255),   // DeepSea
            new Color32(40, 92, 168, 255),  // Sea
            new Color32(222, 206, 150, 255),// Beach
            new Color32(92, 158, 74, 255),  // Plain
            new Color32(120, 138, 70, 255), // Hill
            new Color32(132, 126, 120, 255),// Mountain
        };

        // Placeholder per-player colors used only for preview markers.
        private static readonly Color32[] PlayerMarkerColors =
        {
            new Color32(70, 150, 255, 255),  // player (slot 0)
            new Color32(255, 70, 60, 255),   // AI
            new Color32(70, 220, 110, 255),
            new Color32(245, 210, 50, 255),
        };

        private Grid _grid;
        private Tilemap _tilemap;
        private WorldMap _map;
        private MatchLayout _layout;
        private Tile[] _tiles;

        private bool _dirty;
        private bool _pendingFull;
        private float _settle;

        // Preview-marker pool.
        private Transform _markersRoot;
        private Sprite _markerSprite;
        private readonly List<Marker> _markers = new();

        private class Marker
        {
            public GameObject root;
            public SpriteRenderer front;
            public SpriteRenderer halo;
        }

        public WorldMap Map => _map;
        public MatchLayout Layout => _layout;
        public TerrainType Get(int x, int y) => _map != null ? _map.Get(x, y) : TerrainType.DeepSea;
        public bool IsWalkable(int x, int y) => Get(x, y).IsWalkable();

        /// <summary>Cell rectangle covered by the map, in the centered coords it paints with.</summary>
        public RectInt CenteredCellBounds => _map != null
            ? new RectInt(-_map.Width / 2, -_map.Height / 2, _map.Width, _map.Height)
            : new RectInt(0, 0, 1, 1);

        /// <summary>Terrain walkability at a centered cell (used by GameGrid).</summary>
        public bool IsWalkableWorldCell(Vector2Int centeredCell)
        {
            if (_map == null) return true;
            return _map.Get(centeredCell.x + _map.Width / 2, centeredCell.y + _map.Height / 2).IsWalkable();
        }

        /// <summary>Generate once at full resolution if it hasn't been (for GameGrid, any Awake order).</summary>
        public void EnsureGenerated()
        {
            if (_map == null) GenerateInternal(true);
        }

        private void Awake() => GenerateInternal(true);

        // Live tuning while playing: a field change regenerates at reduced resolution next
        // frame (debounced), then snaps to full resolution once changes settle.
        private void OnValidate() => _dirty = true;

        private void Update()
        {
            if (_dirty)
            {
                _dirty = false;
                GenerateInternal(false);
                _pendingFull = true;
                _settle = SettleDelay;
            }
            else if (_pendingFull)
            {
                _settle -= Time.deltaTime;
                if (_settle <= 0f) { _pendingFull = false; GenerateInternal(true); }
            }
        }

        [ContextMenu("Regenerate")]
        public void Generate()
        {
            _pendingFull = false;
            GenerateInternal(true);
        }

        [ContextMenu("Copy Settings (JSON)")]
        public void CopySettings()
        {
            GUIUtility.systemCopyBuffer = JsonUtility.ToJson(this, true);
            Debug.Log("[WorldMapGenerator] Settings copied to clipboard.");
        }

        [ContextMenu("Paste Settings (JSON)")]
        public void PasteSettings()
        {
            string json = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(json)) { Debug.LogWarning("[WorldMapGenerator] Clipboard is empty."); return; }
            JsonUtility.FromJsonOverwrite(json, this);
            Generate();
            Debug.Log("[WorldMapGenerator] Settings pasted from clipboard.");
        }

        /// <summary>Copies the current recipe as a ready-to-paste WorldRecipes dictionary entry.</summary>
        [ContextMenu("Copy recipe as C#")]
        public void CopyRecipeAsCSharp()
        {
            WorldRecipe r = CurrentRecipe();
            var c = CultureInfo.InvariantCulture;
            string s =
                $"{{ WorldType.{worldType}, new WorldRecipe {{ " +
                $"noiseScale = {r.noiseScale.ToString(c)}f, octaves = {r.octaves}, " +
                $"persistence = {r.persistence.ToString(c)}f, lacunarity = {r.lacunarity.ToString(c)}f, " +
                $"islandFalloff = {r.islandFalloff.ToString(c)}f, " +
                $"deepSeaLevel = {r.deepSeaLevel.ToString(c)}f, seaLevel = {r.seaLevel.ToString(c)}f, " +
                $"beachLevel = {r.beachLevel.ToString(c)}f, plainLevel = {r.plainLevel.ToString(c)}f, " +
                $"hillLevel = {r.hillLevel.ToString(c)}f, beachWaterRadius = {r.beachWaterRadius} }} }},";
            GUIUtility.systemCopyBuffer = s;
            Debug.Log("[WorldMapGenerator] Recipe copied as C#:\n" + s);
        }

        private WorldRecipe CurrentRecipe() => new WorldRecipe
        {
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
            beachWaterRadius = beachWaterRadius,
        };

        private void GenerateInternal(bool fullRes)
        {
            _grid = GetComponent<Grid>();
            if (_tilemap == null) _tilemap = GetComponentInChildren<Tilemap>(true);
            if (_tilemap == null) _tilemap = CreateTerrainTilemap();
            // The painted terrain is rebuilt every run; never serialize it into the scene
            // (otherwise saving bakes thousands of tiles + the runtime textures, bloating it).
            _tilemap.gameObject.hideFlags = HideFlags.DontSave;

            if (randomizeSeed) seed = new System.Random().Next(int.MinValue, int.MaxValue);

            // Game reads the hand-tuned recipe from code; WorldGen tunes the fields below.
            WorldRecipe r = useRecipeFromCode ? WorldRecipes.For(worldType) : CurrentRecipe();

            int dim = Dimension(fullRes);
            _map = WorldMap.Generate(new WorldGenSettings
            {
                seed = seed,
                width = dim,
                height = dim,
                worldType = worldType,
                // Scale features with the map so the macro shape is size-independent.
                noiseScale = r.noiseScale * dim / 100f,
                octaves = r.octaves,
                persistence = r.persistence,
                lacunarity = r.lacunarity,
                islandFalloff = r.islandFalloff,
                deepSeaLevel = r.deepSeaLevel,
                seaLevel = r.seaLevel,
                beachLevel = r.beachLevel,
                plainLevel = r.plainLevel,
                hillLevel = r.hillLevel,
                beachWaterRadius = r.beachWaterRadius,
            });

            _layout = MatchLayout.Build(_map, new MatchLayoutSettings
            {
                seed = seed,
                playerCount = playerCount,
                resourceDensityPer100 = ResourceDensity(),
                footprint = Footprint,
                edgeMargin = EdgeMargin,
                fairnessRadius = FairnessRadius,
                guaranteedPerCcByType = GuaranteedPerCc(),
            });

            EnsureTiles();
            Paint();

            if (drawLayoutMarkers) DrawMarkers();
            else ClearMarkers();

            // Only re-frame on full-resolution builds, so the camera doesn't jump while
            // live-tuning at preview resolution.
            if (fullRes && fitCameraToMap) FitCamera();
        }

        // ----------------------------------------------------------- preset → values

        private int Dimension(bool fullRes)
        {
            int d = size switch
            {
                WorldSize.Small => 150,
                WorldSize.Medium => 275,
                WorldSize.Large => 400,
                _ => 275,
            };
            return fullRes ? d : Mathf.Min(d, PreviewDim);
        }

        // Nodes per 100 buildable (Plain/Hill) tiles. Lowered across the board.
        private float ResourceDensity() => resources switch
        {
            ResourceAmount.Scarce => 0.15f,
            ResourceAmount.Normal => 0.30f,
            ResourceAmount.Abundant => 0.60f,
            _ => 0.30f,
        };

        private int[] GuaranteedPerCc()
        {
            float f = resources switch
            {
                ResourceAmount.Scarce => 0.5f,
                ResourceAmount.Abundant => 1.5f,
                _ => 1.0f,
            };
            var g = new int[4];
            g[(int)ResourceType.Food] = Mathf.RoundToInt(2 * f);
            g[(int)ResourceType.Wood] = Mathf.RoundToInt(2 * f);
            g[(int)ResourceType.Gold] = Mathf.RoundToInt(1 * f);
            g[(int)ResourceType.Stone] = Mathf.RoundToInt(1 * f);
            return g;
        }

        // ------------------------------------------------------------------ painting

        private Tilemap CreateTerrainTilemap()
        {
            var go = new GameObject("GeneratedTerrain") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            var map = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortOrder = TilemapRenderer.SortOrder.TopRight;
            return map;
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

        // ---------------------------------------------------------- preview markers

        private void DrawMarkers()
        {
            if (_layout == null) { ClearMarkers(); return; }
            EnsureMarkerInfra();

            int needed = _layout.CityCenters.Count + _layout.Resources.Count;
            while (_markers.Count < needed) _markers.Add(NewMarker());

            int idx = 0;
            for (int p = 0; p < _layout.CityCenters.Count; p++)
            {
                Color32 col = PlayerMarkerColors[p % PlayerMarkerColors.Length];
                SetMarker(_markers[idx++], _layout.CityCenters[p], col, 2.6f, 24);
            }
            foreach (ResourcePlacement r in _layout.Resources)
                SetMarker(_markers[idx++], r.cell, ResourceMarkerColor(r.type), 1.1f, 20);

            for (; idx < _markers.Count; idx++) _markers[idx].root.SetActive(false);
        }

        private void ClearMarkers()
        {
            foreach (Marker m in _markers) if (m?.root != null) m.root.SetActive(false);
        }

        private void EnsureMarkerInfra()
        {
            if (_markerSprite == null) _markerSprite = MakeDiamondSprite(new Color32(255, 255, 255, 255));
            if (_markersRoot == null)
            {
                // Drop any markers baked into the scene by an older build, then make a
                // fresh non-serialized root.
                Transform existing = transform.Find("LayoutMarkers");
                if (existing != null)
                {
                    if (Application.isPlaying) Destroy(existing.gameObject);
                    else DestroyImmediate(existing.gameObject);
                }

                var go = new GameObject("LayoutMarkers") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(transform, false);
                _markersRoot = go.transform;
            }
        }

        private Marker NewMarker()
        {
            var root = new GameObject("Marker") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(_markersRoot, false);
            var front = root.AddComponent<SpriteRenderer>();
            front.sprite = _markerSprite;

            // Dark halo behind the colored front for contrast on any terrain.
            var haloGo = new GameObject("Halo") { hideFlags = HideFlags.DontSave };
            haloGo.transform.SetParent(root.transform, false);
            haloGo.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            var halo = haloGo.AddComponent<SpriteRenderer>();
            halo.sprite = _markerSprite;
            halo.color = new Color(0f, 0f, 0f, 0.85f);

            return new Marker { root = root, front = front, halo = halo };
        }

        private void SetMarker(Marker m, Vector2Int centeredCell, Color32 color, float scale, int order)
        {
            m.root.SetActive(true);
            m.root.transform.position = _grid.GetCellCenterWorld(new Vector3Int(centeredCell.x, centeredCell.y, 0));
            m.root.transform.localScale = new Vector3(scale, scale, 1f);
            m.front.color = color;
            m.front.sortingOrder = order;
            m.halo.sortingOrder = order - 1;
        }

        private static Color32 ResourceMarkerColor(ResourceType type) => type switch
        {
            ResourceType.Food => new Color32(235, 60, 60, 255),
            ResourceType.Wood => new Color32(70, 205, 85, 255),
            ResourceType.Gold => new Color32(250, 215, 45, 255),
            ResourceType.Stone => new Color32(215, 215, 225, 255),
            _ => new Color32(255, 255, 255, 255),
        };

        // ----------------------------------------------------------------- camera

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

        // ------------------------------------------------------------------ tiles

        private void EnsureTiles()
        {
            if (_tiles != null) return;
            _tiles = new Tile[Palette.Length];
            for (int i = 0; i < Palette.Length; i++) _tiles[i] = MakeDiamondTile(Palette[i]);
        }

        private static Tile MakeDiamondTile(Color32 color)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.hideFlags = HideFlags.DontSave;
            tile.sprite = MakeDiamondSprite(color);
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }

        private static Sprite MakeDiamondSprite(Color32 color)
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
            tex.hideFlags = HideFlags.DontSave;

            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 256f);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
