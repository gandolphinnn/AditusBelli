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
        public int amount = 100;

        public static readonly List<ResourceNode> All = new();

        private Vector2Int _cell;
        private bool _blocked;

        public bool IsDepleted => amount <= 0;

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

        /// <summary>Removes up to 'requested' whole units and returns how many were actually taken.</summary>
        public int Extract(int requested)
        {
            int taken = Mathf.Min(requested, amount);
            amount -= taken;
            if (amount <= 0) Deplete();
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
