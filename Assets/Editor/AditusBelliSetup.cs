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
    /// </summary>
    public static class AditusBelliSetup
    {
        private const string ArtDir = "Assets/Art/Generated";
        private const string SceneDir = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const int MapSize = 40;

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

            // Teams (faction data is editable on these assets in the Inspector).
            TeamDef playerTeam = CreateTeamDef("Player", new Color(0.35f, 0.55f, 0.95f), 200, 300, 100, 100);
            TeamDef enemyTeam = CreateTeamDef("Enemy", new Color(0.90f, 0.35f, 0.30f), 200, 300, 100, 100);

            // Walls: neutral, indestructible obstacles forming a barrier with a gap.
            BuildWalls(grid);

            // --- Units + prefabs ---
            GameObject unitPrefab = BuildUnitPrefab(playerTeam);
            SpawnUnits(unitPrefab, grid);
            GameObject soldierPrefab = BuildSoldierPrefab(playerTeam);

            UnitDef villagerDef = CreateVillagerDef(unitPrefab);
            UnitDef soldierDef = CreateSoldierDef(soldierPrefab);

            // --- Game systems ---
            var systemsGo = new GameObject("Game Systems");
            systemsGo.AddComponent<UnitSelectionManager>();

            var resources = systemsGo.AddComponent<PlayerResources>();
            resources.teamDef = playerTeam; // starting resources come from the team

            systemsGo.AddComponent<PlayerPopulation>();
            systemsGo.AddComponent<ResourceHud>();
            systemsGo.AddComponent<SelectionInfoHud>();
            systemsGo.AddComponent<ProductionHud>();

            var teamManager = systemsGo.AddComponent<TeamManager>();
            teamManager.teams = new[] { playerTeam, enemyTeam };
            teamManager.localPlayer = playerTeam;

            systemsGo.AddComponent<MatchManager>();

            var placer = systemsGo.AddComponent<BuildingPlacer>();
            placer.houseDef = CreateHouseDef();
            placer.barracksDef = CreateBarracksDef(soldierDef);

            // Economy: Town Center (drop-off + villager training) and resource nodes.
            BuildEconomy(grid, villagerDef, playerTeam);

            // A small enemy outpost to fight.
            BuildEnemy(grid, enemyTeam, soldierPrefab);

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

        private static GameObject BuildUnitPrefab(TeamDef team)
        {
            EnsureFolder("Assets/Prefabs");
            const string prefabPath = "Assets/Prefabs/Unit.prefab";

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

            var unit = go.AddComponent<Unit>();
            go.AddComponent<Villager>();

            var owner = go.AddComponent<Owner>();
            owner.team = team;
            owner.applyTeamColor = true;

            var health = go.AddComponent<Health>();
            health.maxHealth = 25;

            unit.selectionIndicator = AddSelectionRing(go, ring);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject BuildSoldierPrefab(TeamDef team)
        {
            EnsureFolder("Assets/Prefabs");
            const string prefabPath = "Assets/Prefabs/Soldier.prefab";

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

            var unit = go.AddComponent<Unit>();

            var owner = go.AddComponent<Owner>();
            owner.team = team;
            owner.applyTeamColor = true;

            var health = go.AddComponent<Health>();
            health.maxHealth = 40;

            go.AddComponent<Combatant>();

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

        // ----------------------------------------------------------- buildings

        private static void BuildWalls(Grid grid)
        {
            BuildingDef wallDef = CreateWallDef();
            var container = new GameObject("Walls");

            // A long wall at x = 5 with a 2-cell gap up at y = 3..4 (off the direct
            // base-to-enemy line), so a correct route must visibly detour around it.
            for (int y = -5; y <= 5; y++)
            {
                if (y == 3 || y == 4) continue; // the gap
                CreateBuilding(wallDef, new Vector3Int(5, y, 0), grid,
                    completed: true, scale: 1f, parent: container.transform);
            }
        }

        private static void BuildEconomy(Grid grid, UnitDef villagerDef, TeamDef playerTeam)
        {
            // Town Center: completed 2x2 drop-off that also trains villagers.
            BuildingDef tcDef = CreateTownCenterDef();
            tcDef.trains = new[] { villagerDef };
            EditorUtility.SetDirty(tcDef);
            AssetDatabase.SaveAssets();

            CreateBuilding(tcDef, new Vector3Int(-3, 0, 0), grid,
                completed: true, scale: 1f, parent: null, team: playerTeam);

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

            // Gold mines.
            Sprite goldSprite = CreateOrLoadSprite(
                "res_gold",
                () => MakeCircleTexture(56, new Color32(230, 200, 60, 255), new Color32(120, 100, 20, 255)),
                ppu: 80f);
            var goldNodes = new GameObject("GoldMines");
            Vector3Int[] goldCells =
            {
                new Vector3Int(-10, 3, 0), new Vector3Int(-11, 3, 0), new Vector3Int(-10, 2, 0),
            };
            foreach (Vector3Int c in goldCells)
                CreateResourceNode(goldNodes.transform, goldSprite, grid, c, ResourceType.Gold, 150, "Gold");

            // Stone mines.
            Sprite stoneSprite = CreateOrLoadSprite(
                "res_stone",
                () => MakeCircleTexture(56, new Color32(150, 150, 160, 255), new Color32(70, 70, 80, 255)),
                ppu: 80f);
            var stoneNodes = new GameObject("StoneMines");
            Vector3Int[] stoneCells =
            {
                new Vector3Int(-10, -3, 0), new Vector3Int(-11, -3, 0), new Vector3Int(-10, -2, 0),
            };
            foreach (Vector3Int c in stoneCells)
                CreateResourceNode(stoneNodes.transform, stoneSprite, grid, c, ResourceType.Stone, 150, "Stone");
        }

        private static void BuildEnemy(Grid grid, TeamDef enemyTeam, GameObject soldierPrefab)
        {
            var container = new GameObject("Enemy");

            // Enemy camp (a destructible building) on the right side of the map.
            CreateBuilding(CreateEnemyCampDef(), new Vector3Int(12, 0, 0), grid,
                completed: true, scale: 1f, parent: container.transform, team: enemyTeam);

            // A couple of enemy soldiers guarding it (defensive, leashed to their post).
            Vector3Int[] guards = { new Vector3Int(11, 2, 0), new Vector3Int(11, -2, 0) };
            foreach (Vector3Int cell in guards)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(soldierPrefab);
                go.transform.SetParent(container.transform);
                Vector3 p = grid.GetCellCenterWorld(cell);
                p.z = 0f;
                go.transform.position = p;

                var owner = go.GetComponent<Owner>();
                if (owner != null) owner.team = enemyTeam;

                var combatant = go.GetComponent<Combatant>();
                if (combatant != null)
                {
                    combatant.mobile = true;    // defends but does not roam
                    combatant.guardRadius = 7f; // leashed to its post
                }
            }
        }

        private static Building CreateBuilding(BuildingDef def, Vector3Int originCell, Grid grid,
            bool completed, float scale, Transform parent, TeamDef team = null)
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

            if (team != null)
            {
                var owner = go.AddComponent<Owner>();
                owner.team = team;
                owner.applyTeamColor = false; // buildings keep their type color
            }

            if (def.maxHealth > 0)
            {
                var health = go.AddComponent<Health>();
                health.maxHealth = def.maxHealth;
            }

            var building = go.AddComponent<Building>();
            building.def = def;
            building.originCell = new Vector2Int(originCell.x, originCell.y);
            building.startCompleted = completed;
            return building;
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

        // ----------------------------------------------------------- data defs

        private static BuildingDef CreateWallDef() =>
            CreateBuildingDef("Wall", "obstacle_rock", new Color32(90, 84, 78, 255), 256, 128,
                new Vector2Int(1, 1), woodCost: 5, buildTime: 3f, population: 0, dropoff: false, maxHealth: 0);

        private static BuildingDef CreateHouseDef() =>
            CreateBuildingDef("House", "building_house", new Color32(150, 96, 70, 255), 512, 256,
                new Vector2Int(2, 2), woodCost: 50, buildTime: 8f, population: 5, dropoff: false, maxHealth: 200);

        private static BuildingDef CreateTownCenterDef() =>
            CreateBuildingDef("TownCenter", "building_town_center", new Color32(200, 170, 120, 255), 512, 256,
                new Vector2Int(2, 2), woodCost: 0, buildTime: 1f, population: 0, dropoff: true, maxHealth: 300,
                displayName: "Town Center");

        private static BuildingDef CreateBarracksDef(UnitDef soldierDef)
        {
            BuildingDef def = CreateBuildingDef("Barracks", "building_barracks", new Color32(120, 125, 135, 255),
                512, 256, new Vector2Int(2, 2), woodCost: 175, buildTime: 12f, population: 0, dropoff: false,
                maxHealth: 500);
            def.trains = new[] { soldierDef };
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static BuildingDef CreateEnemyCampDef() =>
            CreateBuildingDef("EnemyCamp", "building_enemy_camp", new Color32(120, 50, 50, 255), 512, 256,
                new Vector2Int(2, 2), woodCost: 0, buildTime: 1f, population: 0, dropoff: false, maxHealth: 400,
                displayName: "Enemy Camp");

        private static BuildingDef CreateBuildingDef(string assetName, string spriteName, Color32 color,
            int spriteW, int spriteH, Vector2Int footprint, int woodCost, float buildTime, int population,
            bool dropoff, int maxHealth, string displayName = null)
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Buildings");

            Sprite sprite = CreateOrLoadSprite(spriteName,
                () => MakeDiamondTexture(spriteW, spriteH, color), ppu: 256f);

            string path = $"Assets/Data/Buildings/{assetName}.asset";
            var def = AssetDatabase.LoadAssetAtPath<BuildingDef>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<BuildingDef>();
                AssetDatabase.CreateAsset(def, path);
            }

            def.displayName = displayName ?? assetName;
            def.footprint = footprint;
            def.woodCost = woodCost;
            def.buildTime = buildTime;
            def.populationProvided = population;
            def.isDropoff = dropoff;
            def.maxHealth = maxHealth;
            def.trains = null;
            def.sprite = sprite;
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static UnitDef CreateVillagerDef(GameObject prefab) =>
            CreateUnitDef("Villager", prefab, foodCost: 50, trainTime: 6f);

        private static UnitDef CreateSoldierDef(GameObject prefab) =>
            CreateUnitDef("Soldier", prefab, foodCost: 60, trainTime: 8f);

        private static UnitDef CreateUnitDef(string assetName, GameObject prefab, int foodCost, float trainTime)
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Units");

            string path = $"Assets/Data/Units/{assetName}.asset";
            var def = AssetDatabase.LoadAssetAtPath<UnitDef>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<UnitDef>();
                AssetDatabase.CreateAsset(def, path);
            }

            def.displayName = assetName;
            def.foodCost = foodCost;
            def.woodCost = 0;
            def.trainTime = trainTime;
            def.populationCost = 1;
            def.prefab = prefab;
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static TeamDef CreateTeamDef(string name, Color color, int food, int wood, int gold, int stone)
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
