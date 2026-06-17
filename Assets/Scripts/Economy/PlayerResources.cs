using UnityEngine;

namespace AditusBelli.Economy
{
    /// <summary>
    /// The local player's resource stockpile. Single-player singleton for now.
    /// </summary>
    public class PlayerResources : MonoBehaviour
    {
        public static PlayerResources Instance { get; private set; }

        [Header("Starting resources")]
        public int startFood;
        public int startWood;
        public int startGold;
        public int startStone;

        private readonly int[] _amounts = new int[4];

        private void Awake()
        {
            Instance = this;
            _amounts[(int)ResourceType.Food] = startFood;
            _amounts[(int)ResourceType.Wood] = startWood;
            _amounts[(int)ResourceType.Gold] = startGold;
            _amounts[(int)ResourceType.Stone] = startStone;
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
