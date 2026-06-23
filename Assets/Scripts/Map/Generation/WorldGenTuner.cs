using System.Globalization;
using UnityEngine;

namespace AditusBelli.Map
{
    /// <summary>
    /// WorldGen sandbox generator. Instead of a world-type preset it exposes every terrain
    /// parameter individually, for hand-tuning a recipe live in Play. When the look is good,
    /// use "Copy recipe as C#" and paste the produced entry into <see cref="WorldRecipes"/>
    /// so the game (<see cref="WorldMapGenerator"/>) can use it. Pick the world type to tune
    /// (Pangea or Big Islands).
    /// </summary>
    [RequireComponent(typeof(Grid))]
    public class WorldGenTuner : WorldGeneratorBase
    {
        [Header("World type")]
        [Tooltip("Which shaping pass to preview/tune: Pangea (one landmass) or Big Islands.")]
        public WorldType worldType = WorldType.Pangea;

        [Header("Shape (edge)")]
        [Tooltip("Higher = land reaches farther toward the map edge. The border is always sea.")]
        [Range(1f, 6f)] public float islandFalloff = 3.5f;

        [Header("Big Islands shape")]
        [Tooltip("Approx. number of islands across the map (lower = fewer, larger islands).")]
        [Range(1f, 8f)] public float islandScale = 3f;
        [Tooltip("Sea level of the island mask (higher = more open ocean, smaller islands).")]
        [Range(0f, 1f)] public float islandThreshold = 0.5f;

        [Header("Noise recipe")]
        [Tooltip("Feature size, calibrated to a 100-tile map; auto-scales with Size.")]
        [Min(1f)] public float noiseScale = 22f;
        [Range(1, 8)] public int octaves = 5;
        [Range(0f, 1f)] public float persistence = 0.5f;
        [Range(1f, 4f)] public float lacunarity = 1.46f;

        [Header("Elevation thresholds (must ascend)")]
        [Range(0f, 1f)] public float deepSeaLevel = 0.134f;
        [Range(0f, 1f)] public float seaLevel = 0.214f;
        [Range(0f, 1f)] public float beachLevel = 0.323f;
        [Range(0f, 1f)] public float plainLevel = 0.582f;
        [Range(0f, 1f)] public float hillLevel = 0.746f;
        [Tooltip("Beach is kept only within this many tiles of water; inland sand becomes " +
                 "Plain. 0 = allow inland beaches.")]
        [Range(0, 15)] public int beachWaterRadius = 11;

        [Header("Preview")]
        [Tooltip("Draw city-center / resource markers.")]
        public bool drawLayoutMarkers = true;
        [Tooltip("Center and zoom the main camera to frame the whole map after generating.")]
        public bool fitCameraToMap = true;

        protected override WorldType ShapeWorldType => worldType;
        protected override bool WantMarkers => drawLayoutMarkers;
        protected override bool WantFitCamera => fitCameraToMap;

        protected override WorldRecipe Recipe => new WorldRecipe
        {
            noiseScale = noiseScale,
            octaves = octaves,
            persistence = persistence,
            lacunarity = lacunarity,
            islandFalloff = islandFalloff,
            islandScale = islandScale,
            islandThreshold = islandThreshold,
            deepSeaLevel = deepSeaLevel,
            seaLevel = seaLevel,
            beachLevel = beachLevel,
            plainLevel = plainLevel,
            hillLevel = hillLevel,
            beachWaterRadius = beachWaterRadius,
        };

        /// <summary>Copies the current recipe as a ready-to-paste WorldRecipes entry.</summary>
        [ContextMenu("Copy recipe as C#")]
        public void CopyRecipeAsCSharp()
        {
            WorldRecipe r = Recipe;
            var c = CultureInfo.InvariantCulture;
            string s =
                $"{{ WorldType.{ShapeWorldType}, new WorldRecipe {{ " +
                $"noiseScale = {r.noiseScale.ToString(c)}f, octaves = {r.octaves}, " +
                $"persistence = {r.persistence.ToString(c)}f, lacunarity = {r.lacunarity.ToString(c)}f, " +
                $"islandFalloff = {r.islandFalloff.ToString(c)}f, " +
                $"islandScale = {r.islandScale.ToString(c)}f, islandThreshold = {r.islandThreshold.ToString(c)}f, " +
                $"deepSeaLevel = {r.deepSeaLevel.ToString(c)}f, seaLevel = {r.seaLevel.ToString(c)}f, " +
                $"beachLevel = {r.beachLevel.ToString(c)}f, plainLevel = {r.plainLevel.ToString(c)}f, " +
                $"hillLevel = {r.hillLevel.ToString(c)}f, beachWaterRadius = {r.beachWaterRadius} }} }},";
            GUIUtility.systemCopyBuffer = s;
            Debug.Log("[WorldGenTuner] Recipe copied as C#:\n" + s);
        }
    }
}
