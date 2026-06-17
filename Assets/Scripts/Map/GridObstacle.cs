using UnityEngine;

namespace AditusBelli.Map
{
    /// <summary>
    /// Marks this object's footprint as non-walkable on the <see cref="GameGrid"/>
    /// at startup. Buildings will reuse the same idea with larger footprints.
    /// </summary>
    public class GridObstacle : MonoBehaviour
    {
        [Tooltip("Footprint size in cells, anchored at this object's cell.")]
        public Vector2Int size = new Vector2Int(1, 1);

        private void Start()
        {
            GameGrid grid = GameGrid.Instance;
            if (grid == null) return;

            Vector3Int origin = grid.WorldToCell(transform.position);
            for (int dx = 0; dx < Mathf.Max(1, size.x); dx++)
            for (int dy = 0; dy < Mathf.Max(1, size.y); dy++)
                grid.SetWalkable(new Vector2Int(origin.x + dx, origin.y + dy), false);
        }
    }
}
