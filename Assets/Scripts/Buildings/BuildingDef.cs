using UnityEngine;

namespace AditusBelli.Buildings
{
    /// <summary>Data definition for a building type (footprint, cost, effects).</summary>
    [CreateAssetMenu(menuName = "Aditus Belli/Building Definition", fileName = "BuildingDef")]
    public class BuildingDef : ScriptableObject
    {
        public string displayName = "Building";
        public Vector2Int footprint = new Vector2Int(2, 2);
        public int woodCost = 50;

        [Tooltip("Total villager-seconds of work needed to finish construction.")]
        public float buildTime = 8f;

        public int populationProvided = 0;
        public bool isDropoff = false;
        public Sprite sprite;
    }
}
