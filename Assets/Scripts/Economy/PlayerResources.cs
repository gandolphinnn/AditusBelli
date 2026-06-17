using AditusBelli.Teams;
using UnityEngine;

namespace AditusBelli.Economy
{
    /// <summary>
    /// The local player's resource stockpile. Starting amounts come from the
    /// linked team (or the fallback fields if no team is set).
    /// </summary>
    public class PlayerResources : MonoBehaviour
    {
        public static PlayerResources Instance { get; private set; }

        [Tooltip("If set, starting resources come from this team's data.")]
        public TeamDef teamDef;

        [Header("Starting resources (fallback if no team set)")]
        public int startFood;
        public int startWood;
        public int startGold;
        public int startStone;

        private readonly int[] _amounts = new int[4];

        private void Awake()
        {
            Instance = this;

            if (teamDef != null)
            {
                _amounts[(int)ResourceType.Food] = teamDef.startFood;
                _amounts[(int)ResourceType.Wood] = teamDef.startWood;
                _amounts[(int)ResourceType.Gold] = teamDef.startGold;
                _amounts[(int)ResourceType.Stone] = teamDef.startStone;
            }
            else
            {
                _amounts[(int)ResourceType.Food] = startFood;
                _amounts[(int)ResourceType.Wood] = startWood;
                _amounts[(int)ResourceType.Gold] = startGold;
                _amounts[(int)ResourceType.Stone] = startStone;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public int Get(ResourceType type) => _amounts[(int)type];

        public void Add(ResourceType type, int amount) => _amounts[(int)type] += amount;

        public bool TrySpend(ResourceType type, int amount)
        {
            if (_amounts[(int)type] < amount) return false;
            _amounts[(int)type] -= amount;
            return true;
        }
    }
}
