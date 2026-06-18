using UnityEngine;

namespace AditusBelli.Map
{
    /// <summary>Per-cell sight state for the fog of war.</summary>
    public enum Visibility : byte
    {
        Unseen = 0,   // never seen — fully hidden
        Explored = 1, // seen before but not currently in sight — dimmed, no live entities
        Visible = 2,  // currently in sight
    }

    /// <summary>
    /// Pure logical visibility grid (no Unity rendering), mirroring
    /// <see cref="GridModel"/>'s coordinate layout. Addressed in tilemap cells.
    /// </summary>
    public class VisibilityModel
    {
        public int OriginX { get; }
        public int OriginY { get; }
        public int Width { get; }
        public int Height { get; }

        private readonly Visibility[] _state;

        public VisibilityModel(int originX, int originY, int width, int height)
        {
            OriginX = originX;
            OriginY = originY;
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            _state = new Visibility[Width * Height];
        }

        public bool InBounds(int x, int y) =>
            x >= OriginX && x < OriginX + Width && y >= OriginY && y < OriginY + Height;

        public int Index(int x, int y) => (x - OriginX) + (y - OriginY) * Width;

        public Visibility Get(int x, int y) => InBounds(x, y) ? _state[Index(x, y)] : Visibility.Unseen;
        public Visibility Get(Vector2Int c) => Get(c.x, c.y);

        /// <summary>Demotes every currently-visible cell to explored (called each tick before re-revealing).</summary>
        public void DowngradeVisibleToExplored()
        {
            for (int i = 0; i < _state.Length; i++)
                if (_state[i] == Visibility.Visible) _state[i] = Visibility.Explored;
        }

        public void Reveal(int x, int y)
        {
            if (InBounds(x, y)) _state[Index(x, y)] = Visibility.Visible;
        }
    }
}
