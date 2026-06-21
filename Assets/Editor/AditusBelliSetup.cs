using System.IO;
using AditusBelli.Buildings;
using AditusBelli.CameraControl;
using AditusBelli.Combat;
using AditusBelli.Economy;
using AditusBelli.Game;
using AditusBelli.Map;
using AditusBelli.Teams;
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
    /// Units and buildings are prefabs that carry their own data (UnitStats /
    /// Building) — there are no ScriptableObject definitions.
    /// </summary>
    public static class AditusBelliSetup
    {
        private const string ArtDir = "Assets/Art/Generated";
        private const string SceneDir = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string WorldGenScenePath = "Assets/Scenes/WorldGen.unity";

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

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Orthographic camera (wide zoom range for the larger procedural map) ---
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f, 1f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var camCtl = camGo.AddComponent<RtsCameraController>();
            camCtl.minOrthoSize = 2f;
            camCtl.maxOrthoSize = 40f;

            // --- 2D global light ---
            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            // --- Isometric grid (terrain is generated procedurally at runtime) ---
            var gridGo = new GameObject("Grid");
            var grid = gridGo.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSize = new Vector3(1f, 0.5f, 1f);

            gridGo.AddComponent<GameGrid>();   // walkability derived from the generated terrain
            gridGo.AddComponent<FogOfWar>();   // fog overlay (built at runtime)

            // Procedural world generator: match params here, terrain recipe read from code.
            var generator = gridGo.AddComponent<WorldMapGenerator>();
            generator.worldType = WorldType.Pangea;
            generator.size = WorldSize.Medium;
            generator.resources = ResourceAmount.Abundant;
            generator.playerCount = 2;
            generator.seed = -1038437759; // preserved from the tuned WorldGen scene

            // Teams (faction data is editable on these assets in the Inspector). The
            // enemy gets a generous base population so its AI can field escalating
            // waves without having to manage houses; the player builds houses normally.
            TeamDef playerTeam = CreateTeamDef("Player", new Color(0.35f, 0.55f, 0.95f), 200, 300, 100, 100, 8);
            TeamDef enemyTeam = CreateTeamDef("Enemy", new Color(0.90f, 0.35f, 0.30f), 200, 300, 100, 100, 40);

            // Unit prefabs (production stats live on a UnitStats component on the prefab).
            GameObject villagerPrefab = BuildUnitPrefab(playerTeam);
            GameObject soldierPrefab = BuildSoldierPrefab(playerTeam);
            GameObject shipPrefab = BuildShipPrefab(playerTeam);

            // Building sprites (colored isometric diamonds; 512x256 = 2x2 cells, 256x128 = 1x1).
            Sprite tcSprite = CreateOrLoadSprite("building_town_center",
                () => MakeDiamondTexture(512, 256, new Color32(200, 170, 120, 255)), ppu: 256f);
            Sprite houseSprite = CreateOrLoadSprite("building_house",
                () => MakeDiamondTexture(512, 256, new Color32(150, 96, 70, 255)), ppu: 256f);
            Sprite warehouseSprite = CreateOrLoadSprite("building_warehouse",
                () => MakeDiamondTexture(512, 256, new Color32(110, 150, 160, 255)), ppu: 256f);
            Sprite barracksSprite = CreateOrLoadSprite("building_barracks",
                () => MakeDiamondTexture(512, 256, new Color32(120, 125, 135, 255)), ppu: 256f);
            Sprite towerSprite = CreateOrLoadSprite("building_guard_tower",
                () => MakeDiamondTexture(256, 128, new Color32(100, 110, 145, 255)), ppu: 256f);
            Sprite wallSprite = CreateOrLoadSprite("building_wall",
                () => MakeDiamondTexture(256, 128, new Color32(110, 104, 98, 255)), ppu: 256f);
            Sprite dockSprite = CreateOrLoadSprite("building_dock",
                () => MakeDiamondTexture(512, 256, new Color32(70, 110, 150, 255)), ppu: 256f);

            // Building prefabs (their definition lives on the Building component).
            GameObject townCenterPrefab = BuildBuildingPrefab("TownCenter", "Town Center", tcSprite,
                new Vector2Int(2, 2), woodCost: 0, buildTime: 1f, population: 0, dropoff: true,
                maxHealth: 300, trains: new[] { villagerPrefab });
            GameObject housePrefab = BuildBuildingPrefab("House", "House", houseSprite,
                new Vector2Int(2, 2), woodCost: 50, buildTime: 8f, population: 5, dropoff: false,
                maxHealth: 200, trains: null);
            GameObject warehousePrefab = BuildBuildingPrefab("Warehouse", "Warehouse", warehouseSprite,
                new Vector2Int(2, 2), woodCost: 60, buildTime: 6f, population: 0, dropoff: true,
                maxHealth: 200, trains: null);
            GameObject barracksPrefab = BuildBuildingPrefab("Barracks", "Barracks", barracksSprite,
                new Vector2Int(2, 2), woodCost: 175, buildTime: 12f, population: 0, dropoff: false,
                maxHealth: 500, trains: new[] { soldierPrefab });
            GameObject towerPrefab = BuildBuildingPrefab("GuardTower", "Guard Tower", towerSprite,
                new Vector2Int(1, 1), woodCost: 75, buildTime: 10f, population: 0, dropoff: false,
                maxHealth: 250, trains: null, turretRange: 7f, turretDamage: 8, turretCooldown: 1f);
            GameObject wallPrefab = BuildBuildingPrefab("Wall", "Wall", wallSprite,
                new Vector2Int(1, 1), woodCost: 5, buildTime: 3f, population: 0, dropoff: false,
                maxHealth: 600, trains: null);
            GameObject dockPrefab = BuildBuildingPrefab("Dock", "Dock", dockSprite,
                new Vector2Int(2, 2), woodCost: 100, buildTime: 12f, population: 0, dropoff: false,
                maxHealth: 250, trains: new[] { shipPrefab }, requiresAdjacentWater: true);

            // Resource sprites used by the runtime match setup.
            Sprite woodSprite = CreateOrLoadSprite("res_tree",
                () => MakeCircleTexture(64, new Color32(46, 110, 56, 255), new Color32(24, 60, 30, 255)), ppu: 70f);
            Sprite foodSprite = CreateOrLoadSprite("res_bush",
                () => MakeCircleTexture(48, new Color32(196, 64, 78, 255), new Color32(110, 30, 40, 255)), ppu: 90f);
            Sprite goldSprite = CreateOrLoadSprite("res_gold",
                () => MakeCircleTexture(56, new Color32(230, 200, 60, 255), new Color32(120, 100, 20, 255)), ppu: 80f);
            Sprite stoneSprite = CreateOrLoadSprite("res_stone",
                () => MakeCircleTexture(56, new Color32(150, 150, 160, 255), new Color32(70, 70, 80, 255)), ppu: 80f);

            // --- Game systems ---
            var systemsGo = new GameObject("Game Systems");
            systemsGo.AddComponent<UnitSelectionManager>();
            systemsGo.AddComponent<HudController>(); // builds the runtime uGUI HUD

            // TeamManager owns the per-team economies (resources + population),
            // seeded from each TeamDef on first use. No standalone player economy.
            var teamManager = systemsGo.AddComponent<TeamManager>();
            teamManager.teams = new[] { playerTeam, enemyTeam };
            teamManager.localPlayer = playerTeam;

            systemsGo.AddComponent<MatchManager>();

            var placer = systemsGo.AddComponent<BuildingPlacer>();
            // Build menu order (keys 1-9, then 0).
            placer.buildable = new[]
            {
                housePrefab, warehousePrefab, barracksPrefab, towerPrefab, wallPrefab, dockPrefab,
            };

            // Runtime match setup: one city center per team, scattered resources, and
            // starting villagers, all placed on the generated land.
            var match = systemsGo.AddComponent<MatchSetup>();
            match.townCenterDef = townCenterPrefab;
            match.barracksDef = barracksPrefab;
            match.villagerPrefab = villagerPrefab;
            match.villagersPerTeam = 3;
            match.woodSprite = woodSprite;
            match.foodSprite = foodSprite;
            match.goldSprite = goldSprite;
            match.stoneSprite = stoneSprite;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            SceneView.FrameLastActiveSceneView();

            Debug.Log($"[AditusBelli] Scene created: {ScenePath}. Terrain and match are generated at Play. Press Play.");
        }

        [MenuItem("Aditus Belli/3. Build World Generator Scene")]
        public static void BuildWorldGeneratorScene()
        {
            ConfigureProject();
            EnsureFolder(SceneDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Orthographic camera with a wide zoom range so big maps fit on screen.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 20f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 1f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var camCtl = camGo.AddComponent<RtsCameraController>();
            camCtl.minOrthoSize = 2f;
            camCtl.maxOrthoSize = 300f;

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            // Isometric grid + empty ground tilemap (the generator paints it at runtime).
            var gridGo = new GameObject("Grid");
            var grid = gridGo.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSize = new Vector3(1f, 0.5f, 1f);

            var groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(gridGo.transform);
            groundGo.AddComponent<Tilemap>();
            groundGo.AddComponent<TilemapRenderer>().sortOrder = TilemapRenderer.SortOrder.TopRight;

            var generator = gridGo.AddComponent<WorldGenTuner>();
            generator.seed = -1038437759;   // preserved from the tuned WorldGen scene
            generator.size = WorldSize.Medium;
            generator.resources = ResourceAmount.Medium;
            generator.playerCount = 2;
            generator.drawLayoutMarkers = true; // visualize city-center / resource placement
            generator.fitCameraToMap = true;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, WorldGenScenePath); // not registered in Build Settings
            SceneView.FrameLastActiveSceneView();

            Debug.Log($"[AditusBelli] World generator scene created: {WorldGenScenePath}. " +
                      "Press Play to generate, then tweak the WorldMapGenerator and re-Play " +
                      "(or right-click it > Regenerate).");
        }

        // ----------------------------------------------------------------- art

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

        private static GameObject BuildUnitPrefab(TeamDef team)
        {
            EnsureFolder("Assets/Prefabs/Units");
            const string prefabPath = "Assets/Prefabs/Units/Unit.prefab";

            // White body so the team color tint defines the unit's color.
            Sprite body = CreateOrLoadSprite(
                "villager_body",
                () => MakeCircleTexture(64, new Color32(235, 235, 235, 255), new Color32(40, 40, 40, 255)),
                ppu: 80f);
            Sprite ring = CreateOrLoadSprite("selection_ring", () => MakeRingTexture(96, 48), ppu: 96f);

            var go = new GameObject("Unit");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = body;
            sr.sortingOrder = 2; // above the ground (tilemap at 0)

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.3f;

            var unit = go.AddComponent<Unit>(); // Unit is an Entity: owns team + HP
            unit.team = team;
            unit.applyTeamColor = true;
            unit.maxHealth = 25;
            go.AddComponent<Villager>();

            var stats = go.AddComponent<UnitStats>();
            stats.displayName = "Villager";
            stats.foodCost = 50;
            stats.woodCost = 0;
            stats.trainTime = 6f;
            stats.populationCost = 1;

            unit.selectionIndicator = AddSelectionRing(go, ring);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject BuildSoldierPrefab(TeamDef team)
        {
            EnsureFolder("Assets/Prefabs/Units");
            const string prefabPath = "Assets/Prefabs/Units/Soldier.prefab";

            // White square so the team color tint defines the soldier's color.
            Sprite body = CreateOrLoadSprite(
                "soldier_body",
                () => MakeSquareTexture(64, new Color32(235, 235, 235, 255), new Color32(40, 40, 40, 255)),
                ppu: 80f);
            Sprite ring = CreateOrLoadSprite("selection_ring", () => MakeRingTexture(96, 48), ppu: 96f);

            var go = new GameObject("Soldier");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = body;
            sr.sortingOrder = 2;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.32f;

            var unit = go.AddComponent<Unit>(); // Unit is an Entity: owns team + HP
            unit.team = team;
            unit.applyTeamColor = true;
            unit.maxHealth = 40;

            go.AddComponent<Combatant>();

            var stats = go.AddComponent<UnitStats>();
            stats.displayName = "Soldier";
            stats.foodCost = 60;
            stats.woodCost = 0;
            stats.trainTime = 8f;
            stats.populationCost = 1;

            unit.selectionIndicator = AddSelectionRing(go, ring);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static SpriteRenderer AddSelectionRing(GameObject parent, Sprite ring)
        {
            var ringGo = new GameObject("SelectionRing");
            ringGo.transform.SetParent(parent.transform);
            ringGo.transform.localPosition = new Vector3(0f, -0.22f, 0f);
            var ringSr = ringGo.AddComponent<SpriteRenderer>();
            ringSr.sprite = ring;
            ringSr.sortingOrder = 1; // above the ground, below the body
            ringSr.color = new Color(0.4f, 1f, 0.55f, 0.9f);
            return ringSr;
        }

        // ----------------------------------------------------------- buildings

        private static GameObject BuildBuildingPrefab(string assetName, string displayName, Sprite sprite,
            Vector2Int footprint, int woodCost, float buildTime, int population, bool dropoff, int maxHealth,
            GameObject[] trains, float turretRange = 0f, int turretDamage = 0, float turretCooldown = 1f,
            bool requiresAdjacentWater = false)
        {
            EnsureFolder("Assets/Prefabs/Buildings");
            string prefabPath = $"Assets/Prefabs/Buildings/{assetName}.prefab";

            var go = new GameObject(assetName);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 2;

            go.AddComponent<CircleCollider2D>().radius = 0.4f * Mathf.Max(1, Mathf.Max(footprint.x, footprint.y));

            var building = go.AddComponent<Building>(); // Building is an Entity: owns team + HP
            building.applyTeamColor = false; // buildings keep their type color; team is set at spawn
            building.maxHealth = maxHealth;
            building.displayName = displayName;
            building.footprint = footprint;
            building.woodCost = woodCost;
            building.buildTime = buildTime;
            building.populationProvided = population;
            building.isDropoff = dropoff;
            building.requiresAdjacentWater = requiresAdjacentWater;
            building.trains = trains;

            if (turretRange > 0f)
            {
                var turret = go.AddComponent<Turret>();
                turret.range = turretRange;
                turret.attackDamage = turretDamage;
                turret.attackCooldown = turretCooldown;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ----------------------------------------------------------------- ships

        private static GameObject BuildShipPrefab(TeamDef team)
        {
            EnsureFolder("Assets/Prefabs/Units");
            const string prefabPath = "Assets/Prefabs/Units/Ship.prefab";

            // White hull so the team color tint defines the ship's color.
            Sprite body = CreateOrLoadSprite(
                "ship_body",
                () => MakeEllipseTexture(64, new Color32(235, 235, 235, 255), new Color32(40, 40, 40, 255)),
                ppu: 70f);
            Sprite ring = CreateOrLoadSprite("selection_ring", () => MakeRingTexture(96, 48), ppu: 96f);

            var go = new GameObject("Ship");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = body;
            sr.sortingOrder = 2;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.34f;

            var unit = go.AddComponent<Unit>(); // Unit is an Entity: owns team + HP
            unit.naval = true; // travels over open water
            unit.team = team;
            unit.applyTeamColor = true;
            unit.maxHealth = 60;

            var stats = go.AddComponent<UnitStats>();
            stats.displayName = "Ship";
            stats.foodCost = 0;
            stats.woodCost = 50;
            stats.trainTime = 10f;
            stats.populationCost = 1;

            unit.selectionIndicator = AddSelectionRing(go, ring);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ----------------------------------------------------------- data defs

        private static TeamDef CreateTeamDef(string name, Color color, int food, int wood, int gold, int stone,
            int basePopulation)
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Teams");

            string path = $"Assets/Data/Teams/{name}.asset";
            var def = AssetDatabase.LoadAssetAtPath<TeamDef>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<TeamDef>();
                AssetDatabase.CreateAsset(def, path);
            }

            def.displayName = name;
            def.color = color;
            def.startFood = food;
            def.startWood = wood;
            def.startGold = gold;
            def.startStone = stone;
            def.basePopulation = basePopulation;
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            return def;
        }

        // -------------------------------------------------------- sprite tools

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

        private static Texture2D MakeSquareTexture(int size, Color32 fill, Color32 outline)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color32(0, 0, 0, 0);
            var px = new Color32[size * size];
            int margin = Mathf.Max(1, size / 10);
            int border = Mathf.Max(2, size / 14);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool outside = x < margin || x >= size - margin || y < margin || y >= size - margin;
                if (outside) { px[y * size + x] = clear; continue; }

                bool onBorder = x < margin + border || x >= size - margin - border ||
                                y < margin + border || y >= size - margin - border;
                px[y * size + x] = onBorder ? outline : fill;
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // A horizontally-elongated ellipse (a boat hull seen from above).
        private static Texture2D MakeEllipseTexture(int size, Color32 fill, Color32 outline)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color32(0, 0, 0, 0);
            var px = new Color32[size * size];
            float cx = size / 2f, cy = size / 2f;
            float a = size / 2f - 1f;  // horizontal radius (full width)
            float b = size / 3.2f;     // vertical radius (narrower -> hull-like)

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - cx) / a;
                float dy = (y + 0.5f - cy) / b;
                float d = dx * dx + dy * dy;
                if (d <= 0.6724f) px[y * size + x] = fill;       // 0.82^2
                else if (d <= 1f) px[y * size + x] = outline;
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
