using System.IO;
using AditusBelli.Buildings;
using AditusBelli.CameraControl;
using AditusBelli.Economy;
using AditusBelli.Map;
using AditusBelli.UI;
using AditusBelli.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace AditusBelli.EditorTools
{
    /// <summary>
    /// Project setup utilities, runnable from the "Aditus Belli" menu.
    /// They configure Unity settings and build a starter scene from inside the
    /// editor (no hand-editing of ProjectSettings files while the editor runs).
    /// </summary>
    public static class AditusBelliSetup
    {
        private const string ArtDir = "Assets/Art/Generated";
        private const string SceneDir = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const int MapSize = 24;

        [MenuItem("Aditus Belli/1. Configure Project Settings")]
        public static void ConfigureProject()
        {
            // Isometric sprite sorting: sprites lower on screen draw in front.
            GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
            GraphicsSettings.transparencySortAxis = new Vector3(0f, 1f, -0.26f);

            EnsureTag("Unit");
            EnsureTag("Selectable");
            EnsureLayer("Units");
            EnsureLayer("Ground");

            AssetDatabase.SaveAssets();
            Debug.Log("[AditusBelli] Settings configured: isometric sort axis, tags and layers.");
        }

        [MenuItem("Aditus Belli/2. Build Starter Scene")]
        public static void BuildStarterScene()
        {
            ConfigureProject();
            EnsureFolder(ArtDir);
            EnsureFolder(SceneDir);

            Tile grassA = CreateOrLoadDiamondTile("iso_grass_a", new Color32(96, 158, 74, 255));
            Tile grassB = CreateOrLoadDiamondTile("iso_grass_b", new Color32(108, 170, 84, 255));

            if (grassA == null || grassA.sprite == null || grassB == null || grassB.sprite == null)
            {
                Debug.LogError("[AditusBelli] Invalid tiles or sprites: scene not built. Run the command again.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Orthographic camera ---
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f, 1f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<UniversalAdditionalCameraData>();
            camGo.AddComponent<RtsCameraController>();

            // --- 2D global light (useful once we switch to "lit" materials) ---
            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            // --- Isometric grid + ground tilemap ---
            var gridGo = new GameObject("Grid");
            var grid = gridGo.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSize = new Vector3(1f, 0.5f, 1f);

            var groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(gridGo.transform);
            var tilemap = groundGo.AddComponent<Tilemap>();
            var renderer2D = groundGo.AddComponent<TilemapRenderer>();
            renderer2D.sortOrder = TilemapRenderer.SortOrder.TopRight;

            int half = MapSize / 2;
            var positions = new Vector3Int[MapSize * MapSize];
            var tiles = new TileBase[MapSize * MapSize];
            int idx = 0;
            for (int x = 0; x < MapSize; x++)
            for (int y = 0; y < MapSize; y++)
            {
                positions[idx] = new Vector3Int(x - half, y - half, 0);
                tiles[idx] = ((x + y) % 2 == 0) ? grassA : grassB;
                idx++;
            }
            tilemap.SetTiles(positions, tiles);
            tilemap.RefreshAllTiles();
            tilemap.CompressBounds();

            // Logical grid + pathfinding bridge (auto-finds the ground tilemap child).
            gridGo.AddComponent<GameGrid>();

            // Walls: completed "Wall" buildings forming a barrier with a gap.
            BuildWalls(grid);

            // --- Units + selection system ---
            GameObject unitPrefab = BuildUnitPrefab();
            SpawnUnits(unitPrefab, grid);

            var systemsGo = new GameObject("Game Systems");
            systemsGo.AddComponent<UnitSelectionManager>();

            var resources = systemsGo.AddComponent<PlayerResources>();
            resources.startWood = 150; // enough to try construction right away
            resources.startFood = 50;

            systemsGo.AddComponent<PlayerPopulation>();
            systemsGo.AddComponent<ResourceHud>();
            systemsGo.AddComponent<SelectionInfoHud>();

            var placer = systemsGo.AddComponent<BuildingPlacer>();
            placer.houseDef = CreateHouseDef();

            // Economy: drop-off building and resource nodes.
            BuildEconomy(grid);

            // Force re-serialization: without marking dirty, SaveScene may write
            // the tilemap still empty (native tile data is not flushed otherwise).
            EditorUtility.SetDirty(tilemap);
            EditorUtility.SetDirty(groundGo);
            EditorSceneManager.MarkSceneDirty(scene);

            int painted = tilemap.GetUsedTilesCount();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            SceneView.FrameLastActiveSceneView();

            Debug.Log($"[AditusBelli] Scene created: {ScenePath}. " +
                      $"Tiles painted: {painted} (expected {MapSize * MapSize}). Press Play.");
        }

        // ----------------------------------------------------------------- art

        private static Tile CreateOrLoadDiamondTile(string name, Color32 color)
        {
            string spritePath = $"{ArtDir}/{name}.png";
            string tilePath = $"{ArtDir}/{name}.asset";

            if (!File.Exists(spritePath))
            {
                Texture2D tex = MakeDiamondTexture(256, 128, color);
                File.WriteAllBytes(spritePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceSynchronousImport);
            }

            // PPU 256 on a 256x128 texture => 1.0 x 0.5 unit tile, matching the isometric cell.
            var importer = (TextureImporter)AssetImporter.GetAtPath(spritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 256f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePixelsPerUnit = 256f;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

            var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                AssetDatabase.CreateAsset(tile, tilePath);
            }
            else
            {
                tile.sprite = sprite;
                EditorUtility.SetDirty(tile);
            }
            AssetDatabase.SaveAssets();
            return tile;
        }

        private static Texture2D MakeDiamondTexture(int w, int h, Color32 color)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var clear = new Color32(0, 0, 0, 0);
            var px = new Color32[w * h];
            float cx = w / 2f;
            float cy = h / 2f;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = Mathf.Abs(x + 0.5f - cx) / (w / 2f);
                float ny = Mathf.Abs(y + 0.5f - cy) / (h / 2f);
                px[y * w + x] = (nx + ny <= 1f) ? color : clear;
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // --------------------------------------------------------------- units

        private static GameObject BuildUnitPrefab()
        {
            EnsureFolder("Assets/Prefabs");
            const string prefabPath = "Assets/Prefabs/Unit.prefab";

            Sprite body = CreateOrLoadSprite(
                "unit_body",
                () => MakeCircleTexture(64, new Color32(70, 120, 210, 255), new Color32(28, 44, 92, 255)),
                ppu: 80f);
            Sprite ring = CreateOrLoadSprite(
                "selection_ring",
                () => MakeRingTexture(96, 48),
                ppu: 96f);

            var go = new GameObject("Unit");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = body;
            sr.sortingOrder = 2; // above the ground (tilemap at 0)

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.3f;

            var unit = go.AddComponent<Unit>();
            go.AddComponent<Villager>(); // starting units are villagers in this phase

            var ringGo = new GameObject("SelectionRing");
            ringGo.transform.SetParent(go.transform);
            ringGo.transform.localPosition = new Vector3(0f, -0.22f, 0f);
            var ringSr = ringGo.AddComponent<SpriteRenderer>();
            ringSr.sprite = ring;
            ringSr.sortingOrder = 1; // above the ground, below the body
            ringSr.color = new Color(0.4f, 1f, 0.55f, 0.9f);

            unit.selectionIndicator = ringSr;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void BuildWalls(Grid grid)
        {
            BuildingDef wallDef = CreateWallDef();
            var container = new GameObject("Walls");

            // Vertical wall at x = 5, with a gap at y = 0 so units must funnel through.
            int[] wallYs = { -3, -2, -1, 1, 2, 3 };
            foreach (int y in wallYs)
                CreateBuilding(wallDef, new Vector3Int(5, y, 0), grid,
                    completed: true, scale: 1f, parent: container.transform);
        }

        private static BuildingDef CreateWallDef()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Buildings");

            Sprite sprite = CreateOrLoadSprite(
                "obstacle_rock",
                () => MakeDiamondTexture(256, 128, new Color32(90, 84, 78, 255)),
                ppu: 256f);

            const string path = "Assets/Data/Buildings/Wall.asset";
            var def = AssetDatabase.LoadAssetAtPath<BuildingDef>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<BuildingDef>();
                AssetDatabase.CreateAsset(def, path);
            }

            def.displayName = "Wall";
            def.footprint = new Vector2Int(1, 1);
            def.woodCost = 5;
            def.buildTime = 3f;
            def.populationProvided = 0;
            def.isDropoff = false;
            def.sprite = sprite;
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static BuildingDef CreateHouseDef()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Buildings");

            Sprite houseSprite = CreateOrLoadSprite(
                "building_house",
                () => MakeDiamondTexture(512, 256, new Color32(150, 96, 70, 255)),
                ppu: 256f);

            const string path = "Assets/Data/Buildings/House.asset";
            var def = AssetDatabase.LoadAssetAtPath<BuildingDef>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<BuildingDef>();
                AssetDatabase.CreateAsset(def, path);
            }

            def.displayName = "House";
            def.footprint = new Vector2Int(2, 2);
            def.woodCost = 50;
            def.buildTime = 8f;
            def.populationProvided = 5;
            def.isDropoff = false;
            def.sprite = houseSprite;
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static BuildingDef CreateTownCenterDef()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Buildings");

            Sprite sprite = CreateOrLoadSprite(
                "building_town_center",
                () => MakeDiamondTexture(512, 256, new Color32(200, 170, 120, 255)),
                ppu: 256f);

            const string path = "Assets/Data/Buildings/TownCenter.asset";
            var def = AssetDatabase.LoadAssetAtPath<BuildingDef>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<BuildingDef>();
                AssetDatabase.CreateAsset(def, path);
            }

            def.displayName = "Town Center";
            def.footprint = new Vector2Int(2, 2);
            def.woodCost = 0;
            def.buildTime = 1f;
            def.populationProvided = 0;
            def.isDropoff = true;
            def.sprite = sprite;
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static void CreateBuilding(BuildingDef def, Vector3Int originCell, Grid grid,
            bool completed, float scale, Transform parent)
        {
            var go = new GameObject(def.displayName);
            if (parent != null) go.transform.SetParent(parent);

            Vector3 a = grid.GetCellCenterWorld(originCell);
            Vector3 b = grid.GetCellCenterWorld(new Vector3Int(
                originCell.x + Mathf.Max(1, def.footprint.x) - 1,
                originCell.y + Mathf.Max(1, def.footprint.y) - 1, 0));
            Vector3 center = (a + b) * 0.5f;
            center.z = 0f;
            go.transform.position = center;
            if (!Mathf.Approximately(scale, 1f))
                go.transform.localScale = new Vector3(scale, scale, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = def.sprite;
            sr.sortingOrder = 2;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.4f * Mathf.Max(1, Mathf.Max(def.footprint.x, def.footprint.y));

            var building = go.AddComponent<Building>();
            building.def = def;
            building.originCell = new Vector2Int(originCell.x, originCell.y);
            building.startCompleted = completed;
        }

        private static void BuildEconomy(Grid grid)
        {
            // Town Center: completed 2x2 building acting as a resource drop-off.
            CreateBuilding(CreateTownCenterDef(), new Vector3Int(-3, 0, 0), grid,
                completed: true, scale: 1f, parent: null);

            // Trees (Wood).
            Sprite treeSprite = CreateOrLoadSprite(
                "res_tree",
                () => MakeCircleTexture(64, new Color32(46, 110, 56, 255), new Color32(24, 60, 30, 255)),
                ppu: 70f);
            var forest = new GameObject("Forest");
            Vector3Int[] trees =
            {
                new Vector3Int(-7, 1, 0), new Vector3Int(-7, 0, 0), new Vector3Int(-7, -1, 0),
                new Vector3Int(-6, 1, 0), new Vector3Int(-6, 0, 0), new Vector3Int(-6, -1, 0),
            };
            foreach (Vector3Int c in trees)
                CreateResourceNode(forest.transform, treeSprite, grid, c, ResourceType.Wood, 100, "Tree");

            // Berry bushes (Food).
            Sprite bushSprite = CreateOrLoadSprite(
                "res_bush",
                () => MakeCircleTexture(48, new Color32(196, 64, 78, 255), new Color32(110, 30, 40, 255)),
                ppu: 90f);
            var bushes = new GameObject("Berries");
            Vector3Int[] bushCells =
            {
                new Vector3Int(-3, 3, 0), new Vector3Int(-2, 3, 0), new Vector3Int(-1, 3, 0),
            };
            foreach (Vector3Int c in bushCells)
                CreateResourceNode(bushes.transform, bushSprite, grid, c, ResourceType.Food, 75, "Bush");
        }

        private static void CreateResourceNode(Transform parent, Sprite sprite, Grid grid,
            Vector3Int cell, ResourceType type, int amount, string label)
        {
            var go = new GameObject($"{label}_{cell.x}_{cell.y}");
            go.transform.SetParent(parent);
            Vector3 p = grid.GetCellCenterWorld(cell);
            p.z = 0f;
            go.transform.position = p;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 2;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.4f;

            var node = go.AddComponent<ResourceNode>();
            node.resourceType = type;
            node.amount = amount;
        }

        private static void SpawnUnits(GameObject prefab, Grid grid)
        {
            Vector3Int[] cells =
            {
                new Vector3Int(0, 0, 0),
                new Vector3Int(2, 1, 0),
                new Vector3Int(-2, 2, 0),
                new Vector3Int(1, -2, 0),
                new Vector3Int(-1, -1, 0),
                new Vector3Int(3, -1, 0),
            };

            foreach (Vector3Int cell in cells)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Vector3 p = grid.GetCellCenterWorld(cell);
                p.z = 0f;
                go.transform.position = p;
            }
        }

        private static Sprite CreateOrLoadSprite(string name, System.Func<Texture2D> make, float ppu)
        {
            string path = $"{ArtDir}/{name}.png";

            if (!File.Exists(path))
            {
                Texture2D tex = make();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePixelsPerUnit = ppu;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Texture2D MakeCircleTexture(int size, Color32 fill, Color32 outline)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color32(0, 0, 0, 0);
            var px = new Color32[size * size];
            float c = size / 2f;
            float rOuter = size / 2f - 1f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c;
                float dy = y + 0.5f - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= rOuter * 0.82f) px[y * size + x] = fill;
                else if (d <= rOuter) px[y * size + x] = outline;
                else px[y * size + x] = clear;
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeRingTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var white = new Color32(255, 255, 255, 255);
            var clear = new Color32(0, 0, 0, 0);
            var px = new Color32[w * h];
            float cx = w / 2f;
            float cy = h / 2f;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = (x + 0.5f - cx) / (w / 2f);
                float ny = (y + 0.5f - cy) / (h / 2f);
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                px[y * w + x] = (r >= 0.78f && r <= 1.0f) ? white : clear;
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------- helpers

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void EnsureTag(string tag)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0) return;

            var so = new SerializedObject(assets[0]);
            SerializedProperty tags = so.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            so.ApplyModifiedProperties();
        }

        private static void EnsureLayer(string layer)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0) return;

            var so = new SerializedObject(assets[0]);
            SerializedProperty layers = so.FindProperty("layers");

            // User layers start at index 8 (0-7 are reserved by Unity).
            for (int i = 8; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == layer) return;

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty el = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(el.stringValue))
                {
                    el.stringValue = layer;
                    so.ApplyModifiedProperties();
                    return;
                }
            }
            Debug.LogWarning($"[AditusBelli] No free layer slot for '{layer}'.");
        }
    }
}
