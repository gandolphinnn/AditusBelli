using UnityEngine;

namespace AditusBelli.Units
{
    /// <summary>Data definition for a trainable unit (cost, train time, prefab).</summary>
    [CreateAssetMenu(menuName = "Aditus Belli/Unit Definition", fileName = "UnitDef")]
    public class UnitDef : ScriptableObject
    {
        public string displayName = "Unit";
        public int foodCost = 50;
        public int woodCost = 0;
        public float trainTime = 6f;
        public int populationCost = 1;
        public GameObject prefab;
    }
}
