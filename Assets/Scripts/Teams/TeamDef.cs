using UnityEngine;

namespace AditusBelli.Teams
{
    /// <summary>
    /// Editor-editable data for a faction: display name, color and starting
    /// resources. Edit these on the asset in the Inspector (not during Play).
    /// </summary>
    [CreateAssetMenu(menuName = "Aditus Belli/Team", fileName = "Team")]
    public class TeamDef : ScriptableObject
    {
        public string displayName = "Team";
        public Color color = Color.white;

        [Header("Starting resources")]
        public int startFood = 200;
        public int startWood = 200;
        public int startGold = 100;
        public int startStone = 100;

        [Header("Population")]
        [Tooltip("Population headroom before any buildings (houses add on top).")]
        public int basePopulation = 8;
    }
}
