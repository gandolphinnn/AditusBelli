using System.Collections.Generic;

namespace AditusBelli.Map
{
    /// <summary>
    /// Per-world-type generation "recipe": the terrain-shaping knobs (noise + elevation
    /// thresholds + shape) that are tuned by hand in the WorldGen sandbox and then written
    /// into <see cref="WorldRecipes"/> for the real game. Not the per-match settings
    /// (seed/size/resources/players) — those are chosen per game.
    /// </summary>
    public struct WorldRecipe
    {
        public float noiseScale;
        public int octaves;
        public float persistence;
        public float lacunarity;
        public float islandFalloff;
        public float deepSeaLevel;
        public float seaLevel;
        public float beachLevel;
        public float plainLevel;
        public float hillLevel;
        public int beachWaterRadius;
    }

    /// <summary>
    /// Hand-tuned generation recipes per world type, hardcoded here. Workflow: tune a type
    /// in the WorldGen scene, then use the generator's "Copy recipe as C#" menu and paste
    /// the produced entry into this dictionary. The game reads recipes via <see cref="For"/>.
    /// New world types get their parameters by adding an entry here.
    /// </summary>
    public static class WorldRecipes
    {
        private static readonly Dictionary<WorldType, WorldRecipe> Recipes = new()
        {
            // Tuned in WorldGen — replace with your final values via "Copy recipe as C#".
            {
                WorldType.Pangea, new WorldRecipe
                {
                    noiseScale = 22f,
                    octaves = 5,
                    persistence = 0.5f,
                    lacunarity = 1.46f,
                    islandFalloff = 3.5f,
                    deepSeaLevel = 0.134f,
                    seaLevel = 0.214f,
                    beachLevel = 0.323f,
                    plainLevel = 0.582f,
                    hillLevel = 0.746f,
                    beachWaterRadius = 11,
                }
            },
        };

        /// <summary>Recipe for a world type (falls back to Pangea if not yet defined).</summary>
        public static WorldRecipe For(WorldType type) =>
            Recipes.TryGetValue(type, out WorldRecipe r) ? r : Recipes[WorldType.Pangea];
    }
}
