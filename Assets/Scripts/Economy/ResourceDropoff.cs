using System.Collections.Generic;
using AditusBelli.Teams;
using UnityEngine;

namespace AditusBelli.Economy
{
    /// <summary>A building where gatherers deposit the resources they carry.</summary>
    public class ResourceDropoff : MonoBehaviour
    {
        public static readonly List<ResourceDropoff> All = new();

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        /// <summary>
        /// Nearest drop-off to <paramref name="position"/>. When <paramref name="team"/>
        /// is set, only drop-offs owned by that team count, so gatherers never deposit
        /// at an enemy's building.
        /// </summary>
        public static ResourceDropoff Nearest(Vector3 position, TeamDef team = null)
        {
            ResourceDropoff best = null;
            float bestSq = float.MaxValue;
            foreach (ResourceDropoff d in All)
            {
                if (d == null) continue;
                if (team != null)
                {
                    var owner = d.GetComponent<Owner>();
                    if (owner == null || owner.Team != team) continue;
                }
                float sq = (d.transform.position - position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = d; }
            }
            return best;
        }
    }
}
