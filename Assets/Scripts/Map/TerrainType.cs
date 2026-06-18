namespace AditusBelli.Map
{
    /// <summary>
    /// Elevation bands of the generated world, ordered low (water) to high (peaks).
    /// </summary>
    public enum TerrainType
    {
        DeepSea = 0,
        Sea = 1,
        Beach = 2,
        Plain = 3,
        Hill = 4,
        Mountain = 5,
    }

    public static class TerrainTypeExtensions
    {
        /// <summary>
        /// Whether land units can walk on this terrain. Used once the generated map
        /// is wired into gameplay (water and peaks block movement).
        /// </summary>
        public static bool IsWalkable(this TerrainType t) =>
            t == TerrainType.Beach || t == TerrainType.Plain || t == TerrainType.Hill;
    }
}
