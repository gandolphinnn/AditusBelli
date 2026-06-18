using System.Collections.Generic;
using AditusBelli.Economy;
using UnityEngine;

namespace AditusBelli.Map
{
    /// <summary>A resource node to place: a cell and which resource it yields.</summary>
    public struct ResourcePlacement
    {
        public Vector2Int cell; // centered cell coords (match the painted tilemap / GameGrid)
        public ResourceType type;
    }

    /// <summary>Inputs to <see cref="MatchLayout.Build"/>.</summary>
    public struct MatchLayoutSettings
    {
        public int seed;
        public int playerCount;
        public float resourceDensityPer100; // resource nodes per 100 buildable tiles
        public int footprint;                // city-center footprint (cells)
        public int edgeMargin;               // keep city centers this many tiles from the edge
        public int fairnessRadius;           // radius (tiles) for guaranteed nearby resources
        public int[] guaranteedPerCcByType;  // length 4, indexed by (int)ResourceType
    }

    /// <summary>
    /// Deterministic match placement derived from a generated <see cref="WorldMap"/>:
    /// one city center per player and resource nodes, all placed on **buildable** land
    /// (Plain/Hill only — never on Beach, so nothing hugs the shoreline). City centers are
    /// spaced apart (with a minimum separation, but not pushed to the map extremes) and a
    /// minimum of resources is guaranteed near each base for fairness. Pure — no scene/
    /// gameplay deps beyond the terrain model. Output cells are in centered coordinates.
    /// </summary>
    public class MatchLayout
    {
        private const int Spacing = 2; // min gap (cells, Chebyshev) between resource nodes

        public IReadOnlyList<Vector2Int> CityCenters { get; }
        public IReadOnlyList<ResourcePlacement> Resources { get; }

        private MatchLayout(List<Vector2Int> cityCenters, List<ResourcePlacement> resources)
        {
            CityCenters = cityCenters;
            Resources = resources;
        }

        public static MatchLayout Build(WorldMap map, MatchLayoutSettings s)
        {
            int w = map.Width, h = map.Height;
            int halfX = w / 2, halfY = h / 2;
            int fp = Mathf.Max(1, s.footprint);
            int margin = Mathf.Max(0, s.edgeMargin);
            var rng = new System.Random(unchecked(s.seed * 73856093) ^ 0x632be5ab);

            var land = new List<Vector2Int>();         // buildable cells (index coords)
            var ccCandidates = new List<Vector2Int>(); // clear buildable footprints, off the edge
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!map.Get(x, y).IsBuildable()) continue;
                land.Add(new Vector2Int(x, y));
                if (x >= margin && y >= margin &&
                    x + fp - 1 <= w - 1 - margin && y + fp - 1 <= h - 1 - margin &&
                    FootprintBuildable(map, x, y, fp))
                    ccCandidates.Add(new Vector2Int(x, y));
            }

            List<Vector2Int> ccIndex = PickCityCenters(ccCandidates, Mathf.Max(0, s.playerCount), rng);

            // "Blocked" cells can't take a resource (city-center footprints + spacing rings).
            var blocked = new HashSet<int>();
            foreach (Vector2Int cc in ccIndex) BlockBox(blocked, cc.x - 1, cc.y - 1, fp + 1, w, h);

            var resIndex = new List<(Vector2Int cell, ResourceType type)>();

            // Fairness: guaranteed nodes near each city center.
            int[] guaranteed = s.guaranteedPerCcByType ?? new int[4];
            int radius = Mathf.Max(fp + 1, s.fairnessRadius);
            foreach (Vector2Int cc in ccIndex)
                PlaceGuaranteed(map, blocked, resIndex, cc, radius, guaranteed, w, h, rng);

            // Scatter the remainder up to the density target.
            int target = Mathf.Max(Mathf.RoundToInt(s.resourceDensityPer100 / 100f * land.Count), resIndex.Count);
            Shuffle(land, rng);
            var types = new[] { ResourceType.Food, ResourceType.Wood, ResourceType.Gold, ResourceType.Stone };
            int ti = 0;
            foreach (Vector2Int c in land)
            {
                if (resIndex.Count >= target) break;
                if (blocked.Contains(c.y * w + c.x)) continue;
                resIndex.Add((c, types[ti % types.Length]));
                ti++;
                BlockBox(blocked, c.x - Spacing, c.y - Spacing, Spacing * 2 + 1, w, h);
            }

            // Convert to centered coords for output.
            var cityCenters = new List<Vector2Int>(ccIndex.Count);
            foreach (Vector2Int c in ccIndex) cityCenters.Add(new Vector2Int(c.x - halfX, c.y - halfY));

            var resources = new List<ResourcePlacement>(resIndex.Count);
            foreach ((Vector2Int cell, ResourceType type) r in resIndex)
                resources.Add(new ResourcePlacement
                {
                    cell = new Vector2Int(r.cell.x - halfX, r.cell.y - halfY),
                    type = r.type,
                });

            return new MatchLayout(cityCenters, resources);
        }

        private static void PlaceGuaranteed(WorldMap map, HashSet<int> blocked,
            List<(Vector2Int, ResourceType)> result, Vector2Int cc, int radius, int[] guaranteed,
            int w, int h, System.Random rng)
        {
            var nearby = new List<Vector2Int>();
            int r2 = radius * radius;
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > r2) continue;
                int x = cc.x + dx, y = cc.y + dy;
                if (x < 0 || x >= w || y < 0 || y >= h) continue;
                if (!map.Get(x, y).IsBuildable()) continue;
                if (blocked.Contains(y * w + x)) continue;
                nearby.Add(new Vector2Int(x, y));
            }
            Shuffle(nearby, rng);

            int ni = 0;
            for (int t = 0; t < 4 && t < guaranteed.Length; t++)
            {
                int need = guaranteed[t], got = 0;
                while (got < need && ni < nearby.Count)
                {
                    Vector2Int c = nearby[ni++];
                    if (blocked.Contains(c.y * w + c.x)) continue;
                    result.Add((c, (ResourceType)t));
                    BlockBox(blocked, c.x - Spacing, c.y - Spacing, Spacing * 2 + 1, w, h);
                    got++;
                }
            }
        }

        /// <summary>
        /// Picks city centers that are spaced apart by a minimum separation but otherwise
        /// random — they are NOT pushed to the map extremes. The separation relaxes if the
        /// buildable area is too small to satisfy it.
        /// </summary>
        private static List<Vector2Int> PickCityCenters(List<Vector2Int> candidates, int count,
            System.Random rng)
        {
            var chosen = new List<Vector2Int>();
            if (candidates.Count == 0 || count == 0) return chosen;

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (Vector2Int c in candidates)
            {
                if (c.x < minX) minX = c.x;
                if (c.x > maxX) maxX = c.x;
                if (c.y < minY) minY = c.y;
                if (c.y > maxY) maxY = c.y;
            }
            float span = Mathf.Max(maxX - minX, maxY - minY);
            float minSep = Mathf.Max(3f, span * 0.3f); // spaced, but not maximal

            chosen.Add(candidates[rng.Next(candidates.Count)]);
            int guard = count * 60;
            while (chosen.Count < count && guard-- > 0)
            {
                bool placed = false;
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    Vector2Int cand = candidates[rng.Next(candidates.Count)];
                    if (FarEnough(cand, chosen, minSep)) { chosen.Add(cand); placed = true; break; }
                }
                if (!placed) minSep = Mathf.Max(3f, minSep * 0.8f); // ease spacing and retry
            }
            return chosen;
        }

        private static bool FarEnough(Vector2Int c, List<Vector2Int> chosen, float minSep)
        {
            float m2 = minSep * minSep;
            foreach (Vector2Int ch in chosen)
            {
                long dx = c.x - ch.x, dy = c.y - ch.y;
                if (dx * dx + dy * dy < m2) return false;
            }
            return true;
        }

        private static bool FootprintBuildable(WorldMap map, int x, int y, int fp)
        {
            for (int dy = 0; dy < fp; dy++)
            for (int dx = 0; dx < fp; dx++)
                if (!map.Get(x + dx, y + dy).IsBuildable()) return false;
            return true;
        }

        private static void BlockBox(HashSet<int> blocked, int x0, int y0, int size, int w, int h)
        {
            for (int dy = 0; dy < size; dy++)
            for (int dx = 0; dx < size; dx++)
            {
                int x = x0 + dx, y = y0 + dy;
                if (x >= 0 && x < w && y >= 0 && y < h) blocked.Add(y * w + x);
            }
        }

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
