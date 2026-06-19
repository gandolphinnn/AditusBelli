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
    /// an optional rally point the new unit walks to.
    /// </summary>
    [RequireComponent(typeof(Building))]
    public class UnitProducer : MonoBehaviour
    {
        public UnitDef[] trainable;

        private readonly List<UnitDef> _queue = new();
        private Building _building;
        private TeamDef _ownerTeam;
        private float _progress;
        private Vector3 _rallyPoint;
        private bool _hasRally;

        public int QueueCount => _queue.Count;
        public float Progress => _progress;
        public bool HasRally => _hasRally;
        public UnitDef FirstTrainable => (trainable != null && trainable.Length > 0) ? trainable[0] : null;

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

        /// <summary>Queues a unit if complete, within the population cap and affordable.</summary>
        public bool Enqueue(UnitDef def)
        {
            if (def == null || _building == null || !_building.IsComplete) return false;

            TeamEconomy econ = TeamManager.Instance != null ? TeamManager.Instance.EconomyFor(_ownerTeam) : null;
            if (econ != null)
            {
                if (UnitSelectionManager.UnitCountForTeam(_ownerTeam) + _queue.Count + def.populationCost > econ.Cap)
                    return false; // would exceed the population cap

                if (econ.Get(ResourceType.Food) < def.foodCost || econ.Get(ResourceType.Wood) < def.woodCost)
                    return false; // not enough resources
                econ.TrySpend(ResourceType.Food, def.foodCost);
                econ.TrySpend(ResourceType.Wood, def.woodCost);
            }

            _queue.Add(def);
            return true;
        }

        /// <summary>Removes the last queued unit and refunds its cost.</summary>
        public void CancelLast()
        {
            int last = _queue.Count - 1;
            if (last < 0) return;

            UnitDef def = _queue[last];
            _queue.RemoveAt(last);

            TeamEconomy econ = TeamManager.Instance != null ? TeamManager.Instance.EconomyFor(_ownerTeam) : null;
            if (econ != null)
            {
                econ.Add(ResourceType.Food, def.foodCost);
                econ.Add(ResourceType.Wood, def.woodCost);
            }

            if (_queue.Count == 0) _progress = 0f;
        }

        private void Update()
        {
            if (_queue.Count == 0) return;

            UnitDef def = _queue[0];
            _progress += Time.deltaTime / Mathf.Max(0.01f, def.trainTime);
            if (_progress < 1f) return;

            _progress = 0f;
            _queue.RemoveAt(0);
            Spawn(def);
        }

        private void Spawn(UnitDef def)
        {
            if (def.prefab == null) return;

            Vector3 spawn = ComputeSpawnPoint();
            GameObject go = Instantiate(def.prefab, spawn, Quaternion.identity);

            var spawnedOwner = go.GetComponent<Owner>();
            if (spawnedOwner != null && _ownerTeam != null) spawnedOwner.team = _ownerTeam;

            if (_hasRally)
            {
                var unit = go.GetComponent<Unit>();
                if (unit != null) unit.MoveTo(_rallyPoint);
            }
        }

        private Vector3 ComputeSpawnPoint()
        {
            GameGrid grid = GameGrid.Instance;
            if (grid == null || _building == null || _building.def == null)
                return transform.position;

            Vector2Int o = _building.originCell;
            Vector2Int s = _building.def.footprint;

            // First free cell on the ring around the footprint.
            for (int dy = -1; dy <= s.y; dy++)
            for (int dx = -1; dx <= s.x; dx++)
            {
                bool interior = dx >= 0 && dx < s.x && dy >= 0 && dy < s.y;
                if (interior) continue;
                var cell = new Vector2Int(o.x + dx, o.y + dy);
                if (grid.IsWalkable(cell)) return grid.CellCenter(cell);
            }
            return transform.position;
        }
    }
}
