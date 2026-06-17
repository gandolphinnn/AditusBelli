using System.Collections.Generic;
using AditusBelli.Map;
using UnityEngine;

namespace AditusBelli.Economy
{
    /// <summary>
    /// A harvestable resource source (tree, bush, mine...). Blocks its cell while
    /// present and frees it when depleted.
    /// </summary>
    public class ResourceNode : MonoBehaviour
    {
        public ResourceType resourceType = ResourceType.Wood;
        public float amount = 100f;

        public static readonly List<ResourceNode> All = new();

        private Vector2Int _cell;
        private bool _blocked;

        public bool IsDepleted => amount <= 0f;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void Start()
        {
            GameGrid grid = GameGrid.Instance;
            if (grid == null) return;

            Vector3Int c = grid.WorldToCell(transform.position);
            _cell = new Vector2Int(c.x, c.y);
            grid.SetWalkable(_cell, false);
            _blocked = true;
        }

        /// <summary>Removes up to 'requested' units and returns the amount actually taken.</summary>
        public float Extract(float requested)
        {
            float taken = Mathf.Min(requested, amount);
            amount -= taken;
            if (amount <= 0f) Deplete();
            return taken;
        }

        private void Deplete()
        {
            if (_blocked)
            {
                GameGrid grid = GameGrid.Instance;
                if (grid != null) grid.SetWalkable(_cell, true);
                _blocked = false;
            }
            Destroy(gameObject);
        }
    }
}
