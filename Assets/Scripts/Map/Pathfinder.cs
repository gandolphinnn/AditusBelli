using System.Collections.Generic;
using UnityEngine;

namespace AditusBelli.Map
{
    /// <summary>
    /// A* pathfinding over a <see cref="GridModel"/>. 8-directional, with octile
    /// costs and no corner cutting through blocked tiles.
    /// The open set uses a linear min-scan, which is fine for current map sizes;
    /// it can be swapped for a binary heap if maps grow large.
    /// </summary>
    public static class Pathfinder
    {
        private const float SQRT2 = 1.41421356f;

        private static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1),
            new Vector2Int(-1, 1), new Vector2Int(-1, -1),
        };

        /// <summary>Returns the cell path from start to goal (inclusive), or null if none.</summary>
        public static List<Vector2Int> FindPath(GridModel grid, Vector2Int start, Vector2Int goal)
        {
            if (!grid.IsWalkable(start) || !grid.IsWalkable(goal)) return null;
            if (start == goal) return new List<Vector2Int> { start };

            int w = grid.Width;
            int count = grid.Width * grid.Height;

            var gScore = new float[count];
            var fScore = new float[count];
            var cameFrom = new int[count];
            var stateOf = new byte[count]; // 0 unseen, 1 open, 2 closed
            for (int i = 0; i < count; i++) { gScore[i] = float.MaxValue; cameFrom[i] = -1; }

            int startI = Local(start, grid);
            int goalI = Local(goal, grid);
            gScore[startI] = 0f;
            fScore[startI] = Heuristic(start, goal);
            stateOf[startI] = 1;

            var open = new List<int> { startI };

            while (open.Count > 0)
            {
                // Extract the node with the lowest fScore.
                int best = 0;
                for (int i = 1; i < open.Count; i++)
                    if (fScore[open[i]] < fScore[open[best]]) best = i;

                int currentI = open[best];
                open[best] = open[open.Count - 1];
                open.RemoveAt(open.Count - 1);

                if (currentI == goalI) return Reconstruct(cameFrom, currentI, grid);

                stateOf[currentI] = 2;
                var current = new Vector2Int(grid.OriginX + currentI % w, grid.OriginY + currentI / w);

                foreach (Vector2Int d in Dirs)
                {
                    Vector2Int n = current + d;
                    if (!grid.IsWalkable(n)) continue;

                    bool diagonal = d.x != 0 && d.y != 0;
                    if (diagonal &&
                        (!grid.IsWalkable(current.x + d.x, current.y) ||
                         !grid.IsWalkable(current.x, current.y + d.y)))
                        continue; // do not cut across a blocked corner

                    int nI = Local(n, grid);
                    if (stateOf[nI] == 2) continue;

                    float tentative = gScore[currentI] + (diagonal ? SQRT2 : 1f);
                    if (tentative >= gScore[nI]) continue;

                    cameFrom[nI] = currentI;
                    gScore[nI] = tentative;
                    fScore[nI] = tentative + Heuristic(n, goal);
                    if (stateOf[nI] != 1) { stateOf[nI] = 1; open.Add(nI); }
                }
            }

            return null; // no path
        }

        private static int Local(Vector2Int c, GridModel grid) =>
            (c.x - grid.OriginX) + (c.y - grid.OriginY) * grid.Width;

        private static float Heuristic(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            return (dx + dy) + (SQRT2 - 2f) * Mathf.Min(dx, dy); // octile distance
        }

        private static List<Vector2Int> Reconstruct(int[] cameFrom, int currentI, GridModel grid)
        {
            int w = grid.Width;
            var path = new List<Vector2Int>();
            while (currentI != -1)
            {
                path.Add(new Vector2Int(grid.OriginX + currentI % w, grid.OriginY + currentI / w));
                currentI = cameFrom[currentI];
            }
            path.Reverse();
            return path;
        }
    }
}
