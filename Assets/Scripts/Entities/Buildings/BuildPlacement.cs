using AditusBelli.Economy;
using AditusBelli.Map;
using AditusBelli.Teams;
using UnityEngine;

namespace AditusBelli.Buildings
{
    /// <summary>
    /// Team-agnostic building placement: footprint geometry, validity checks and the
    /// act of dropping a construction site owned by a given team, spending from that
    /// team's economy. Shared by the player's <see cref="BuildingPlacer"/> and the bot
    /// AI so both build under the exact same rules.
    /// </summary>
    public static class BuildPlacement
    {
        /// <summary>The prefab's footprint, clamped to at least 1x1.</summary>
        public static Vector2Int Footprint(GameObject prefab)
        {
            var b = prefab != null ? prefab.GetComponent<Building>() : null;
            return b != null
                ? new Vector2Int(Mathf.Max(1, b.footprint.x), Mathf.Max(1, b.footprint.y))
                : new Vector2Int(1, 1);
        }

        /// <summary>World-space center of a footprint placed at a cell origin (z = 0).</summary>
        public static Vector3 FootprintCenter(GameGrid grid, Vector2Int origin, Vector2Int size)
        {
            int fx = Mathf.Max(1, size.x), fy = Mathf.Max(1, size.y);
            Vector3 a = grid.CellCenter(origin);
            Vector3 b = grid.CellCenter(new Vector2Int(origin.x + fx - 1, origin.y + fy - 1));
            Vector3 c = (a + b) * 0.5f;
            c.z = 0f;
            return c;
        }

        /// <summary>True if every cell of the footprint is walkable (free of obstacles/buildings).</summary>
        public static bool FootprintFree(GameGrid grid, Vector2Int origin, Vector2Int size)
        {
            int fx = Mathf.Max(1, size.x), fy = Mathf.Max(1, size.y);
            for (int dx = 0; dx < fx; dx++)
            for (int dy = 0; dy < fy; dy++)
                if (!grid.IsWalkable(new Vector2Int(origin.x + dx, origin.y + dy))) return false;
            return true;
        }

        /// <summary>True if any cell on the ring around the footprint is open water (docks).</summary>
        public static bool HasAdjacentWater(GameGrid grid, Vector2Int origin, Vector2Int size)
        {
            int fx = Mathf.Max(1, size.x), fy = Mathf.Max(1, size.y);
            for (int dy = -1; dy <= fy; dy++)
            for (int dx = -1; dx <= fx; dx++)
            {
                bool interior = dx >= 0 && dx < fx && dy >= 0 && dy < fy;
                if (interior) continue;
                if (grid.IsWalkable(new Vector2Int(origin.x + dx, origin.y + dy), true)) return true;
            }
            return false;
        }

        /// <summary>True if the economy can pay the prefab's wood cost.</summary>
        public static bool CanAfford(TeamEconomy econ, GameObject prefab)
        {
            var b = prefab != null ? prefab.GetComponent<Building>() : null;
            return econ == null || b == null || econ.Get(ResourceType.Wood) >= b.woodCost;
        }

        /// <summary>
        /// Searches outward ring by ring from <paramref name="anchor"/> for a free footprint
        /// (optionally next to water). <paramref name="minR"/> = 0 also tries the anchor cell.
        /// </summary>
        public static bool TryFindSpot(GameGrid grid, Vector2Int anchor, Vector2Int size, bool needsWater,
            int minR, int maxR, out Vector2Int origin)
        {
            for (int r = Mathf.Max(0, minR); r <= maxR; r++)
            {
                if (r == 0)
                {
                    if (Valid(grid, anchor, size, needsWater)) { origin = anchor; return true; }
                    continue;
                }
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue; // ring outline only
                    var o = new Vector2Int(anchor.x + dx, anchor.y + dy);
                    if (Valid(grid, o, size, needsWater)) { origin = o; return true; }
                }
            }
            origin = anchor;
            return false;
        }

        private static bool Valid(GameGrid grid, Vector2Int origin, Vector2Int size, bool needsWater) =>
            FootprintFree(grid, origin, size) && (!needsWater || HasAdjacentWater(grid, origin, size));

        /// <summary>
        /// Spends the prefab's wood cost from <paramref name="econ"/> and instantiates a
        /// construction site at the cell origin, owned by <paramref name="team"/>. Returns
        /// the placed <see cref="Building"/>, or null if the economy can't afford it.
        /// </summary>
        public static Building PlaceConstructionSite(GameGrid grid, GameObject prefab, Vector2Int origin,
            TeamDef team, TeamEconomy econ)
        {
            if (grid == null || prefab == null) return null;

            var def = prefab.GetComponent<Building>();
            int cost = def != null ? def.woodCost : 0;
            if (econ != null && !econ.TrySpend(ResourceType.Wood, cost)) return null;

            Vector3 center = FootprintCenter(grid, origin, Footprint(prefab));
            GameObject go = Object.Instantiate(prefab, center, Quaternion.identity);

            var building = go.GetComponent<Building>();
            if (building != null)
            {
                building.team = team;
                building.originCell = origin;
                building.startCompleted = false; // construction site: villagers build it
            }
            return building;
        }
    }
}
