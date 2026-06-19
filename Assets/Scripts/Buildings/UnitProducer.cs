using System.Collections.Generic;
using AditusBelli.Economy;
using AditusBelli.Map;
using AditusBelli.Teams;
using AditusBelli.Units;
using UnityEngine;

namespace AditusBelli.Buildings
{
    /// <summary>
    /// Lets a building train units: a production queue with resource cost,
    /// population gating, timed production, spawning at a free adjacent cell, and
    /// an optional rally point the new unit walks to. Trainables are unit prefabs;
    /// their cost/time come from the <see cref="UnitStats"/> on each prefab.
    /// </summary>
    [RequireComponent(typeof(Building))]
    public class UnitProducer : MonoBehaviour
    {
        public GameObject[] trainable;

        private readonly List<GameObject> _queue = new();
        private Building _building;
        private TeamDef _ownerTeam;
        private float _progress;
        private Vector3 _rallyPoint;
        private bool _hasRally;

        public int QueueCount => _queue.Count;
        public float Progress => _progress;
        public bool HasRally => _hasRally;
        public GameObject FirstTrainable => (trainable != null && trainable.Length > 0) ? trainable[0] : null;

        private void Awake()
        {
            _building = GetComponent<Building>();
            var owner = GetComponent<Owner>();
            _ownerTeam = owner != null ? owner.Team : null;
        }

        public void SetRallyPoint(Vector3 worldPos)
        {
            worldPos.z = 0f;
            _rallyPoint = worldPos;
            _hasRally = true;
        }

        /// <summary>Queues a unit prefab if complete, within the population cap and affordable.</summary>
        public bool Enqueue(GameObject prefab)
        {
            if (prefab == null || _building == null || !_building.IsComplete) return false;

            UnitStats stats = prefab.GetComponent<UnitStats>();
            if (stats == null) return false;

            TeamEconomy econ = TeamManager.Instance != null ? TeamManager.Instance.EconomyFor(_ownerTeam) : null;
            if (econ != null)
            {
                if (UnitSelectionManager.UnitCountForTeam(_ownerTeam) + _queue.Count + stats.populationCost > econ.Cap)
                    return false; // would exceed the population cap

                if (econ.Get(ResourceType.Food) < stats.foodCost || econ.Get(ResourceType.Wood) < stats.woodCost)
                    return false; // not enough resources
                econ.TrySpend(ResourceType.Food, stats.foodCost);
                econ.TrySpend(ResourceType.Wood, stats.woodCost);
            }

            _queue.Add(prefab);
            return true;
        }

        /// <summary>Removes the last queued unit and refunds its cost.</summary>
        public void CancelLast()
        {
            int last = _queue.Count - 1;
            if (last < 0) return;

            GameObject prefab = _queue[last];
            _queue.RemoveAt(last);

            UnitStats stats = prefab != null ? prefab.GetComponent<UnitStats>() : null;
            TeamEconomy econ = TeamManager.Instance != null ? TeamManager.Instance.EconomyFor(_ownerTeam) : null;
            if (econ != null && stats != null)
            {
                econ.Add(ResourceType.Food, stats.foodCost);
                econ.Add(ResourceType.Wood, stats.woodCost);
            }

            if (_queue.Count == 0) _progress = 0f;
        }

        private void Update()
        {
            if (_queue.Count == 0) return;

            GameObject prefab = _queue[0];
            UnitStats stats = prefab != null ? prefab.GetComponent<UnitStats>() : null;
            float trainTime = stats != null ? stats.trainTime : 1f;

            _progress += Time.deltaTime / Mathf.Max(0.01f, trainTime);
            if (_progress < 1f) return;

            _progress = 0f;
            _queue.RemoveAt(0);
            Spawn(prefab);
        }

        private void Spawn(GameObject prefab)
        {
            if (prefab == null) return;

            Vector3 spawn = ComputeSpawnPoint(prefab);
            GameObject go = Instantiate(prefab, spawn, Quaternion.identity);

            var spawnedOwner = go.GetComponent<Owner>();
            if (spawnedOwner != null && _ownerTeam != null) spawnedOwner.team = _ownerTeam;

            if (_hasRally)
            {
                var unit = go.GetComponent<Unit>();
                if (unit != null) unit.MoveTo(_rallyPoint);
            }
        }

        private Vector3 ComputeSpawnPoint(GameObject prefab)
        {
            GameGrid grid = GameGrid.Instance;
            if (grid == null || _building == null) return transform.position;

            // Naval units must appear on adjacent water; land units on adjacent land.
            var unit = prefab != null ? prefab.GetComponent<Unit>() : null;
            bool naval = unit != null && unit.naval;

            Vector2Int o = _building.originCell;
            Vector2Int s = _building.footprint;

            // First free cell on the ring around the footprint.
            for (int dy = -1; dy <= s.y; dy++)
            for (int dx = -1; dx <= s.x; dx++)
            {
                bool interior = dx >= 0 && dx < s.x && dy >= 0 && dy < s.y;
                if (interior) continue;
                var cell = new Vector2Int(o.x + dx, o.y + dy);
                if (grid.IsWalkable(cell, naval)) return grid.CellCenter(cell);
            }
            return transform.position;
        }
    }
}
