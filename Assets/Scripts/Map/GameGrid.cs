using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AditusBelli.Map
{
    /// <summary>
    /// Bridges Unity's isometric Grid/Tilemap with the logical <see cref="GridModel"/>
    /// and pathfinding. Owns the model, builds it from the ground tilemap, and
    /// exposes world-space pathfinding to gameplay code.
    /// </summary>
    [RequireComponent(typeof(Grid))]
    public class GameGrid : MonoBehaviour
    {
        public static GameGrid Instance { get; private set; }

        [Tooltip("Ground tilemap defining the walkable area. Auto-found in children if left empty.")]
        [SerializeField] private Tilemap groundTilemap;

        private Grid _grid;
        private GridModel _model;        // land walkability
        private GridModel _navalModel;   // open-water walkability (naval units)
        private Bounds _worldBounds;
        private bool _worldBoundsValid;

        private void Awake()
        {
            Instance = this;
            _grid = GetComponent<Grid>();
            BuildModel();
        }

        /// <summary>The cell rectangle covered by the grid (tilemap coordinates).</summary>
        public RectInt CellBounds => _model != null
            ? new RectInt(_model.OriginX, _model.OriginY, _model.Width, _model.Height)
            : new RectInt(0, 0, 1, 1);

        /// <summary>World-space AABB enclosing every cell (used to map the minimap/fog).</summary>
        public Bounds WorldBounds
        {
            get
            {
                if (!_worldBoundsValid) { _worldBounds = ComputeWorldBounds(); _worldBoundsValid = true; }
                return _worldBounds;
            }
        }

        /// <summary>Maps a world position to [0,1]x[0,1] inside <see cref="WorldBounds"/>.</summary>
        public Vector2 WorldToNormalized(Vector3 world)
        {
            Bounds b = WorldBounds;
            return new Vector2(
                Mathf.InverseLerp(b.min.x, b.max.x, world.x),
                Mathf.InverseLerp(b.min.y, b.max.y, world.y));
        }

        /// <summary>Inverse of <see cref="WorldToNormalized"/> (for minimap click-navigation).</summary>
        public Vector3 NormalizedToWorld(Vector2 n)
        {
            Bounds b = WorldBounds;
            return new Vector3(
                Mathf.Lerp(b.min.x, b.max.x, n.x),
                Mathf.Lerp(b.min.y, b.max.y, n.y), 0f);
        }

        private Bounds ComputeWorldBounds()
        {
            RectInt cb = CellBounds;
            Vector3 a = CellCenter(new Vector2Int(cb.xMin, cb.yMin));
            Vector3 b = CellCenter(new Vector2Int(cb.xMax - 1, cb.yMin));
            Vector3 c = CellCenter(new Vector2Int(cb.xMin, cb.yMax - 1));
            Vector3 d = CellCenter(new Vector2Int(cb.xMax - 1, cb.yMax - 1));

            var bounds = new Bounds();
            bounds.SetMinMax(
                new Vector3(Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x)),
                            Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y)), 0f),
                new Vector3(Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x)),
                            Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y)), 0f));
            bounds.Expand(new Vector3(1f, 0.5f, 0f)); // half-cell padding so edge blips aren't clipped
            return bounds;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildModel()
        {
            // Procedural maps: walkability comes from the generated terrain (sea/deep-sea/
            // mountain are blocked). EnsureGenerated guards Awake order.
            var generator = GetComponent<WorldGeneratorBase>();
            if (generator != null)
            {
                generator.EnsureGenerated();
                RectInt b = generator.CenteredCellBounds;
                _model = new GridModel(b.xMin, b.yMin, b.width, b.height);
                _navalModel = new GridModel(b.xMin, b.yMin, b.width, b.height);
                for (int y = b.yMin; y < b.yMax; y++)
                for (int x = b.xMin; x < b.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    _model.SetWalkable(x, y, generator.IsWalkableWorldCell(cell));
                    _navalModel.SetWalkable(x, y, generator.IsWaterWorldCell(cell));
                }
                return;
            }

            // Flat prototype maps: a cell is walkable if the ground tilemap has a tile.
            if (groundTilemap == null) groundTilemap = GetComponentInChildren<Tilemap>();
            if (groundTilemap == null)
            {
                Debug.LogError("[GameGrid] No ground tilemap or generator found.");
                _model = new GridModel(0, 0, 1, 1);
                _navalModel = new GridModel(0, 0, 1, 1);
                return;
            }

            BoundsInt b2 = groundTilemap.cellBounds;
            _model = new GridModel(b2.xMin, b2.yMin, b2.size.x, b2.size.y);
            _navalModel = new GridModel(b2.xMin, b2.yMin, b2.size.x, b2.size.y); // no water on flat maps
            for (int y = b2.yMin; y < b2.yMax; y++)
            for (int x = b2.xMin; x < b2.xMax; x++)
                _model.SetWalkable(x, y, groundTilemap.HasTile(new Vector3Int(x, y, 0)));
        }

        public Vector3Int WorldToCell(Vector3 world) => _grid.WorldToCell(world);

        public Vector3 CellCenter(Vector2Int cell) =>
            _grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));

        public bool IsWalkable(Vector2Int cell) => _model != null && _model.IsWalkable(cell);

        /// <summary>Walkability in a movement domain: land (default) or naval (open water).</summary>
        public bool IsWalkable(Vector2Int cell, bool naval)
        {
            GridModel model = naval ? _navalModel : _model;
            return model != null && model.IsWalkable(cell);
        }

        public void SetWalkable(Vector2Int cell, bool value) => _model?.SetWalkable(cell, value);

        /// <summary>
        /// Computes a path and returns world-space waypoints, excluding the start
        /// cell. Returns null if no route exists. Set <paramref name="naval"/> to
        /// route over open water instead of land.
        /// </summary>
        public List<Vector3> FindPath(Vector3 worldStart, Vector3 worldGoal, bool naval = false)
        {
            GridModel model = naval ? _navalModel : _model;
            if (model == null) return null;

            Vector3Int s3 = _grid.WorldToCell(worldStart);
            Vector3Int g3 = _grid.WorldToCell(worldGoal);
            var start = new Vector2Int(s3.x, s3.y);
            var goal = new Vector2Int(g3.x, g3.y);

            if (!model.IsWalkable(goal) && !TryNearestWalkable(model, goal, out goal)) return null;
            if (!model.IsWalkable(start) && !TryNearestWalkable(model, start, out start)) return null;

            List<Vector2Int> cells = Pathfinder.FindPath(model, start, goal);
            if (cells == null || cells.Count == 0) return null;

            var world = new List<Vector3>(cells.Count);
            for (int i = 1; i < cells.Count; i++) // skip the start cell
                world.Add(CellCenter(cells[i]));
            return world;
        }

        /// <summary>Searches outward (ring by ring) for the closest walkable cell in a model.</summary>
        private static bool TryNearestWalkable(GridModel model, Vector2Int from, out Vector2Int result, int maxRadius = 6)
        {
            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue; // ring outline only
                    var c = new Vector2Int(from.x + dx, from.y + dy);
                    if (model.IsWalkable(c)) { result = c; return true; }
                }
            }
            result = from;
            return false;
        }
    }
}
