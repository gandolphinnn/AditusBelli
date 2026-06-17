using UnityEngine;

namespace AditusBelli.Economy
{
    /// <summary>
    /// Player population cap. Current population (unit count) is reported by the
    /// HUD from the unit registry; this tracks the available cap.
    /// </summary>
    public class PlayerPopulation : MonoBehaviour
    {
        public static PlayerPopulation Instance { get; private set; }

        [Tooltip("Population headroom available before any buildings.")]
        public int baseCap = 8;

        private int _buildingCap;

        public int Cap => baseCap + _buildingCap;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void AddCap(int amount) => _buildingCap += amount;
        public void RemoveCap(int amount) => _buildingCap -= amount;
    }
}
