using UnityEngine;

namespace AditusBelli.Map
{
    /// <summary>
    /// Game-side world generator. The match is configured from high-level inputs only —
    /// seed (optional) plus presets (size, world type, resources, players); the terrain
    /// recipe is read from code (<see cref="WorldRecipes"/>) by world type. Tune the
    /// recipes in the WorldGen scene with <c>WorldGenTuner</c>.
    /// </summary>
    [RequireComponent(typeof(Grid))]
    public class WorldMapGenerator : WorldGeneratorBase
    {
        [Header("World type")]
        [Tooltip("Selects the hand-tuned recipe from WorldRecipes (Pangea or Big Islands).")]
        public WorldType worldType = WorldType.Pangea;

        protected override WorldRecipe Recipe => WorldRecipes.For(worldType);
        protected override WorldType ShapeWorldType => worldType;
    }
}
