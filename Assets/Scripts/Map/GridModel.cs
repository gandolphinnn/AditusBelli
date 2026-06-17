using UnityEngine;

namespace AditusBelli.Map
{
    /// <summary>
    /// Pure logical grid storing per-cell walkability. Independent of Unity
    /// rendering so it can be used (and tested) without a scene.
    /// Cells are addressed in tilemap coordinates (which may be negative); the
    /// model is offset by an origin.
    /// </summary>
    public class GridModel
    {
        public int OriginX { get; }
        public int OriginY { get; }
        public int Width { get; }
        public int Height { get; }

        private readonly bool[] _walkable;

        public GridModel(int originX, int originY, int width, int height)
        {
            OriginX = originX;
            OriginY = originY;
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            _walkable = new bool[Width * Height];
        }

        public bool InBounds(int x, int y) =>
            x >= OriginX && x < OriginX + Width && y >= OriginY && y < OriginY + Height;

        public bool InBounds(Vector2Int c) => InBounds(c.x, c.y);

        public int Index(int x, int y) => (x - OriginX) + (y - OriginY) * Width;

        public bool IsWalkable(int x, int y) => InBounds(x, y) && _walkable[Index(x, y)];
        public bool IsWalkable(Vector2Int c) => IsWalkable(c.x, c.y);

        public void SetWalkable(int x, int y, bool value)
        {
            if (InBounds(x, y)) _walkable[Index(x, y)] = value;
        }

        public void SetWalkable(Vector2Int c, bool value) => SetWalkable(c.x, c.y, value);
    }
}
