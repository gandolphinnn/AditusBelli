using UnityEngine;

namespace AditusBelli.Units
{
    /// <summary>
    /// Production stats for a unit, carried on its prefab (replaces the old
    /// <c>UnitDef</c> ScriptableObject). A <see cref="AditusBelli.Buildings.UnitProducer"/>
    /// reads these off the prefab before spawning; the data also rides along on the
    /// spawned instance, which is harmless.
    /// </summary>
    public class UnitStats : MonoBehaviour
    {
        public string displayName = "Unit";
        public int foodCost = 50;
        public int woodCost = 0;
        public float trainTime = 6f;
        public int populationCost = 1;
    }
}
