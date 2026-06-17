using System.Collections.Generic;
using UnityEngine;

namespace AditusBelli.Economy
{
    /// <summary>A building where gatherers deposit the resources they carry.</summary>
    public class ResourceDropoff : MonoBehaviour
    {
        public static readonly List<ResourceDropoff> All = new();

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public static ResourceDropoff Nearest(Vector3 position)
        {
            ResourceDropoff best = null;
            float bestSq = float.MaxValue;
            foreach (ResourceDropoff d in All)
            {
                if (d == null) continue;
                float sq = (d.transform.position - position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = d; }
            }
            return best;
        }
    }
}
