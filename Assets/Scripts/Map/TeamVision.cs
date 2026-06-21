using System.Collections.Generic;
using AditusBelli.Buildings;
using AditusBelli.Teams;
using AditusBelli.Units;
using UnityEngine;

namespace AditusBelli.Map
{
    /// <summary>
    /// Per-team line of sight — the single source of truth for "who can see what".
    /// Maintains one <see cref="VisibilityModel"/> per team, recomputed on a fixed
    /// interval by revealing around each team's own units and buildings (the same
    /// circular radii the player's fog uses). The local player's model is rendered by
    /// <see cref="FogOfWar"/>; the bots query their own model so they only act on what
    /// they have seen. Allied teams share line of sight (each reveal also uncovers the
    /// allies' models). Logic only — no rendering.
    /// </summary>
    public class TeamVision : MonoBehaviour
    {
        public static TeamVision Instance { get; private set; }

        [Tooltip("Sight radius (in cells) of a unit.")]
        public int unitVision = 6;
        [Tooltip("Sight radius (in cells) of a building.")]
        public int buildingVision = 9;
        [Tooltip("How often visibility is recomputed, in seconds.")]
        public float refreshInterval = 0.2f;

        private GameGrid _grid;
        private readonly Dictionary<TeamDef, VisibilityModel> _models = new();
        private readonly Dictionary<TeamDef, List<VisibilityModel>> _revealTargets = new();
        private float _timer;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = refreshInterval;
            Recompute();
        }

        // --------------------------------------------------------------- queries

        /// <summary>Sight state of <paramref name="team"/> at a world position (Unseen if unknown).</summary>
        public Visibility Get(TeamDef team, Vector3 world)
        {
            if (team == null || !EnsureGrid()) return Visibility.Visible;
            if (!_models.TryGetValue(team, out VisibilityModel m)) return Visibility.Unseen;
            Vector3Int c = _grid.WorldToCell(world);
            return m.Get(c.x, c.y);
        }

        public bool IsVisible(TeamDef team, Vector3 world) => Get(team, world) == Visibility.Visible;

        /// <summary>True if the team currently sees the position or has explored it before.</summary>
        public bool IsExplored(TeamDef team, Vector3 world) => Get(team, world) != Visibility.Unseen;

        /// <summary>The live visibility model for a team (created on demand). Used by the renderer.</summary>
        public VisibilityModel ModelForTeam(TeamDef team) => team != null && EnsureGrid() ? ModelFor(team) : null;

        /// <summary>
        /// Finds the nearest walkable, not-yet-explored cell to <paramref name="near"/> for
        /// <paramref name="team"/>, as a scouting target. Searches outward ring by ring so a
        /// scout naturally spirals outward. Returns false if the whole map is explored.
        /// </summary>
        public bool TryGetFrontier(TeamDef team, Vector3 near, out Vector3 world)
        {
            world = near;
            if (team == null || !EnsureGrid()) return false;

            VisibilityModel model = ModelFor(team);
            Vector3Int c0 = _grid.WorldToCell(near);
            var center = new Vector2Int(c0.x, c0.y);
            RectInt cb = _grid.CellBounds;
            int maxR = cb.width + cb.height;

            for (int r = 2; r <= maxR; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue; // ring outline only
                    int x = center.x + dx, y = center.y + dy;
                    if (!cb.Contains(new Vector2Int(x, y))) continue;
                    if (model.Get(x, y) != Visibility.Unseen) continue;
                    var cell = new Vector2Int(x, y);
                    if (!_grid.IsWalkable(cell)) continue;
                    world = _grid.CellCenter(cell);
                    return true;
                }
            }
            return false;
        }

        // -------------------------------------------------------------- compute

        /// <summary>Forces an immediate recompute (used by the renderer to avoid a black first frame).</summary>
        public void RecomputeNow()
        {
            if (EnsureGrid()) Recompute();
        }

        private void Recompute()
        {
            if (!EnsureGrid()) return;

            foreach (VisibilityModel m in _models.Values) m.DowngradeVisibleToExplored();

            foreach (Unit u in UnitSelectionManager.AllUnits)
            {
                if (u == null || u.Team == null) continue;
                RevealAround(u.Team, u.transform.position, unitVision);
            }
            foreach (Building b in Building.AllBuildings)
            {
                if (b == null || b.Team == null) continue;
                RevealAround(b.Team, b.transform.position, buildingVision);
            }
        }

        // Reveals into the source team's own model and every allied team's model, so
        // allies share line of sight.
        private void RevealAround(TeamDef team, Vector3 world, int radius)
        {
            List<VisibilityModel> targets = RevealTargetsFor(team);
            Vector3Int c = _grid.WorldToCell(world);
            int r2 = radius * radius;
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > r2) continue;
                int x = c.x + dx, y = c.y + dy;
                for (int i = 0; i < targets.Count; i++) targets[i].Reveal(x, y);
            }
        }

        /// <summary>
        /// Models a source team's sight reveals into: its own plus each allied team's.
        /// Cached because alliances are fixed for the match.
        /// </summary>
        private List<VisibilityModel> RevealTargetsFor(TeamDef team)
        {
            if (_revealTargets.TryGetValue(team, out List<VisibilityModel> list)) return list;

            list = new List<VisibilityModel> { ModelFor(team) };
            TeamManager tm = TeamManager.Instance;
            TeamDef[] teams = tm != null ? tm.teams : null;
            if (teams != null)
                foreach (TeamDef t in teams)
                    if (t != null && t != team && tm.AreAllies(team, t))
                        list.Add(ModelFor(t));

            _revealTargets[team] = list;
            return list;
        }

        private VisibilityModel ModelFor(TeamDef team)
        {
            if (!_models.TryGetValue(team, out VisibilityModel m))
            {
                RectInt cb = _grid.CellBounds;
                m = new VisibilityModel(cb.xMin, cb.yMin, cb.width, cb.height);
                _models[team] = m;
            }
            return m;
        }

        private bool EnsureGrid()
        {
            if (_grid == null) _grid = GameGrid.Instance;
            return _grid != null;
        }
    }
}
