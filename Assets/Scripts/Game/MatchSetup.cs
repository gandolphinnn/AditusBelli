using System.Collections.Generic;
using AditusBelli.Buildings;
using AditusBelli.Economy;
using AditusBelli.Map;
using AditusBelli.Teams;
using UnityEngine;

namespace AditusBelli.Game
{
    /// <summary>
    /// Sets up a match at runtime on the procedurally generated map: places one Town
    /// Center per team and the scattered resource nodes from the generator's
    /// <see cref="MatchLayout"/>, spawns the starting villagers next to each base, and
    /// centers the camera on the local player's base. Town Center and Barracks are
    /// prefabs that carry their own <see cref="Building"/> definition. Terrain +
    /// layout come from <see cref="WorldMapGenerator"/>.
    /// </summary>
    public class MatchSetup : MonoBehaviour
    {
        [Tooltip("Town Center building prefab (trains villagers, acts as a drop-off).")]
        public GameObject townCenterDef;
        [Tooltip("Barracks building prefab: enemy teams start with one and train soldiers from it.")]
        public GameObject barracksDef;
        [Tooltip("Villager prefab spawned next to each city center at game start.")]
        public GameObject villagerPrefab;
        [Min(0)] public int villagersPerTeam = 3;

        [Header("Resource sprites (by type)")]
        public Sprite woodSprite;
        public Sprite foodSprite;
        public Sprite goldSprite;
        public Sprite stoneSprite;

        [Header("Resource amounts (by type)")]
        public int woodAmount = 100;
        public int foodAmount = 75;
        public int goldAmount = 150;
        public int stoneAmount = 150;

        private void Start()
        {
            GameGrid grid = GameGrid.Instance;
            var generator = FindAnyObjectByType<WorldGeneratorBase>();
            if (grid == null || generator == null || townCenterDef == null)
            {
                Debug.LogError("[MatchSetup] Missing GameGrid, world generator or Town Center prefab.");
                return;
            }

            generator.EnsureGenerated();
            MatchLayout layout = generator.Layout;
            if (layout == null) return;

            TeamManager teamManager = TeamManager.Instance;
            TeamDef[] teams = teamManager != null ? teamManager.teams : null;
            var rng = new System.Random(generator.seed ^ 0x1d2c3b4a);

            int count = teams != null ? Mathf.Min(layout.CityCenters.Count, teams.Length) : 0;
            TeamDef local = teamManager != null ? teamManager.localPlayer : null;
            for (int i = 0; i < count; i++)
            {
                Vector2Int cc = layout.CityCenters[i];
                TeamDef team = teams[i];
                CreateBuilding(grid, cc, townCenterDef, team);
                SpawnVillagers(grid, cc, team, rng);
                if (team != local) SetupEnemy(grid, cc, team); // give AI opponents a barracks + brain
            }

            SpawnResources(grid, layout);
            CenterCamera(grid, layout, teams, teamManager);
        }

        /// <summary>Instantiates a building prefab at a cell origin, owned by a team, completed.</summary>
        private static Building CreateBuilding(GameGrid grid, Vector2Int origin, GameObject prefab, TeamDef team)
        {
            var def = prefab.GetComponent<Building>();
            int fx = Mathf.Max(1, def != null ? def.footprint.x : 1);
            int fy = Mathf.Max(1, def != null ? def.footprint.y : 1);

            Vector3 pos = FootprintCenter(grid, origin, fx, fy);
            GameObject go = Instantiate(prefab, pos, Quaternion.identity);

            var owner = go.GetComponent<Owner>();
            if (owner != null) owner.team = team;

            var building = go.GetComponent<Building>();
            building.originCell = origin;
            building.startCompleted = true;
            return building;
        }

        private void SetupEnemy(GameGrid grid, Vector2Int ccOrigin, TeamDef team)
        {
            if (barracksDef == null) return;

            var barracksBuilding = barracksDef.GetComponent<Building>();
            if (barracksBuilding == null) return;

            Vector2Int tcSize = TownCenterFootprint();
            if (!TryFindBuildable(grid, ccOrigin, tcSize, barracksBuilding.footprint, out Vector2Int barracksOrigin))
                return; // no room near the base; skip (the AI just won't have a barracks)

            Building barracks = CreateBuilding(grid, barracksOrigin, barracksDef, team);

            var aiGo = new GameObject($"EnemyAI ({team.displayName})");
            aiGo.AddComponent<EnemyAI>().Init(team, barracks);
        }

        /// <summary>
        /// Finds a free origin near the town center where the given footprint fits on
        /// walkable terrain and does not overlap the town-center footprint. Searches
        /// outward ring by ring. (Buildings have not blocked their cells yet at setup.)
        /// </summary>
        private static bool TryFindBuildable(GameGrid grid, Vector2Int tcOrigin, Vector2Int tcSize,
            Vector2Int size, out Vector2Int origin)
        {
            int fx = Mathf.Max(1, size.x);
            int fy = Mathf.Max(1, size.y);
            for (int r = 2; r <= 8; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue; // ring outline only
                    var o = new Vector2Int(tcOrigin.x + dx, tcOrigin.y + dy);
                    if (FootprintWalkable(grid, o, fx, fy) &&
                        !Overlaps(o, fx, fy, tcOrigin, tcSize.x, tcSize.y))
                    {
                        origin = o;
                        return true;
                    }
                }
            }
            origin = tcOrigin;
            return false;
        }

        private static bool FootprintWalkable(GameGrid grid, Vector2Int origin, int fx, int fy)
        {
            for (int dx = 0; dx < fx; dx++)
            for (int dy = 0; dy < fy; dy++)
                if (!grid.IsWalkable(new Vector2Int(origin.x + dx, origin.y + dy))) return false;
            return true;
        }

        private static bool Overlaps(Vector2Int aOrigin, int aw, int ah, Vector2Int bOrigin, int bw, int bh) =>
            aOrigin.x < bOrigin.x + bw && aOrigin.x + aw > bOrigin.x &&
            aOrigin.y < bOrigin.y + bh && aOrigin.y + ah > bOrigin.y;

        private void SpawnVillagers(GameGrid grid, Vector2Int ccOrigin, TeamDef team, System.Random rng)
        {
            if (villagerPrefab == null || villagersPerTeam <= 0) return;

            Vector2Int fp = TownCenterFootprint();
            int fx = fp.x, fy = fp.y;

            // Walkable cells near the base, excluding the city-center footprint.
            var candidates = new List<Vector2Int>();
            const int radius = 4;
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                var cell = new Vector2Int(ccOrigin.x + dx, ccOrigin.y + dy);
                bool onFootprint = cell.x >= ccOrigin.x && cell.x < ccOrigin.x + fx &&
                                   cell.y >= ccOrigin.y && cell.y < ccOrigin.y + fy;
                if (onFootprint || !grid.IsWalkable(cell)) continue;
                candidates.Add(cell);
            }
            Shuffle(candidates, rng);

            int n = Mathf.Min(villagersPerTeam, candidates.Count);
            for (int i = 0; i < n; i++)
            {
                Vector3 pos = grid.CellCenter(candidates[i]);
                pos.z = 0f;
                GameObject go = Instantiate(villagerPrefab, pos, Quaternion.identity);
                var owner = go.GetComponent<Owner>();
                if (owner != null) owner.team = team; // set before Owner.Start tints it
            }
        }

        private void SpawnResources(GameGrid grid, MatchLayout layout)
        {
            foreach (ResourcePlacement r in layout.Resources)
            {
                Sprite sprite = SpriteFor(r.type);
                if (sprite == null) continue;

                var go = new GameObject(r.type.ToString());
                Vector3 pos = grid.CellCenter(r.cell);
                pos.z = 0f;
                go.transform.position = pos;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = 2;

                go.AddComponent<CircleCollider2D>().radius = 0.4f;

                var node = go.AddComponent<ResourceNode>();
                node.resourceType = r.type;
                node.amount = AmountFor(r.type);
            }
        }

        private void CenterCamera(GameGrid grid, MatchLayout layout, TeamDef[] teams, TeamManager teamManager)
        {
            Camera cam = Camera.main;
            if (cam == null || teams == null || teamManager == null) return;

            int idx = 0;
            for (int i = 0; i < teams.Length; i++) if (teams[i] == teamManager.localPlayer) { idx = i; break; }
            if (idx >= layout.CityCenters.Count) return;

            Vector2Int fp = TownCenterFootprint();
            Vector3 c = FootprintCenter(grid, layout.CityCenters[idx], fp.x, fp.y);
            cam.transform.position = new Vector3(c.x, c.y, cam.transform.position.z);
        }

        /// <summary>The town-center prefab's footprint (clamped to at least 1x1).</summary>
        private Vector2Int TownCenterFootprint()
        {
            var b = townCenterDef != null ? townCenterDef.GetComponent<Building>() : null;
            return b != null
                ? new Vector2Int(Mathf.Max(1, b.footprint.x), Mathf.Max(1, b.footprint.y))
                : new Vector2Int(1, 1);
        }

        private static Vector3 FootprintCenter(GameGrid grid, Vector2Int origin, int fx, int fy)
        {
            Vector3 a = grid.CellCenter(origin);
            Vector3 b = grid.CellCenter(new Vector2Int(origin.x + fx - 1, origin.y + fy - 1));
            Vector3 center = (a + b) * 0.5f;
            center.z = 0f;
            return center;
        }

        private Sprite SpriteFor(ResourceType type) => type switch
        {
            ResourceType.Wood => woodSprite,
            ResourceType.Food => foodSprite,
            ResourceType.Gold => goldSprite,
            ResourceType.Stone => stoneSprite,
            _ => null,
        };

        private int AmountFor(ResourceType type) => type switch
        {
            ResourceType.Wood => woodAmount,
            ResourceType.Food => foodAmount,
            ResourceType.Gold => goldAmount,
            ResourceType.Stone => stoneAmount,
            _ => 100,
        };

        private static void Shuffle(List<Vector2Int> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
