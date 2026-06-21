using AditusBelli.Buildings;
using AditusBelli.Entities;
using AditusBelli.Map;
using AditusBelli.Teams;
using AditusBelli.Units;
using UnityEngine;

namespace AditusBelli.Economy
{
    /// <summary>
    /// Villager behaviour: gathers resources (walk → harvest → return → deposit →
    /// repeat) and constructs buildings (walk → add work until complete). Drives
    /// movement through the <see cref="Unit"/> component.
    /// </summary>
    [RequireComponent(typeof(Unit))]
    public class Villager : MonoBehaviour
    {
        public float gatherRate = 5f;      // resource units per second
        public int carryCapacity = 10;
        public float buildRate = 1f;       // build-seconds added per second
        public float interactRange = 2.2f; // reach for nodes, drop-offs and buildings

        private enum State { Idle, ToResource, Gathering, ToDropoff, ToBuild, Building }

        private Unit _unit;
        private Entity _owner;
        private State _state = State.Idle;
        private ResourceNode _node;
        private Building _buildTarget;
        private int _carried;
        private float _gatherAccumulator;
        private ResourceType _carriedType;
        private Vector3 _lastNodePosition; // where this villager last gathered (sight origin)

        public int CarriedAmount => _carried;
        public ResourceType CarriedType => _carriedType;
        public int CarryCapacity => carryCapacity;

        /// <summary>True while the villager has a task (gathering, depositing or building).</summary>
        public bool IsBusy => _state != State.Idle;

        private void Awake()
        {
            _unit = GetComponent<Unit>();
            _owner = GetComponent<Entity>();
            _lastNodePosition = transform.position;
        }

        public void GatherFrom(ResourceNode node)
        {
            if (node == null) return;
            _buildTarget = null;
            _node = node;
            _state = State.ToResource;
            _unit.MoveTo(node.transform.position);
        }

        public void BuildAt(Building building)
        {
            if (building == null || building.IsComplete) return;
            _node = null;
            _buildTarget = building;
            _state = State.ToBuild;
            _unit.MoveTo(building.transform.position);
        }

        public void StopTasks()
        {
            _state = State.Idle;
            _node = null;
            _buildTarget = null;
        }

        private void Update()
        {
            switch (_state)
            {
                case State.ToResource: TickToResource(); break;
                case State.Gathering: TickGathering(); break;
                case State.ToDropoff: TickToDropoff(); break;
                case State.ToBuild: TickToBuild(); break;
                case State.Building: TickBuilding(); break;
            }
        }

        // --- Gathering ---

        private void TickToResource()
        {
            if (_node == null || _node.IsDepleted)
            {
                if (_carried > 0f) GoDeposit();
                else _state = State.Idle;
                return;
            }

            if (_unit.IsMoving) return;
            _state = Near(_node.transform.position, interactRange) ? State.Gathering : State.Idle;
        }

        private void TickGathering()
        {
            if (_node == null || _node.IsDepleted) { GoDeposit(); return; }

            _carriedType = _node.resourceType;
            _lastNodePosition = transform.position; // remember where we emptied it from

            // Gather whole units at the gather rate, so resource totals stay exact.
            _gatherAccumulator += gatherRate * Time.deltaTime;
            int units = Mathf.FloorToInt(_gatherAccumulator);
            if (units > 0)
            {
                int request = Mathf.Min(units, carryCapacity - _carried);
                if (request > 0)
                {
                    int taken = _node.Extract(request);
                    _carried += taken;
                    _gatherAccumulator -= taken;
                }
            }

            if (_carried >= carryCapacity || _node == null || _node.IsDepleted)
                GoDeposit();
        }

        private void TickToDropoff()
        {
            ResourceDropoff drop = ResourceDropoff.Nearest(transform.position, _owner != null ? _owner.Team : null);
            if (drop == null) { _state = State.Idle; return; }
            if (_unit.IsMoving) return;

            if (!Near(drop.transform.position, interactRange)) { _state = State.Idle; return; }

            Deposit();

            if (_node != null && !_node.IsDepleted)
            {
                _state = State.ToResource;
                _unit.MoveTo(_node.transform.position);
                return;
            }

            // Auto-continue to another source only if one is within the villager's
            // sight range (the fog-uncover radius) of where the last node was emptied.
            ResourceNode next = NearestNodeInSight(_carriedType, _lastNodePosition);
            if (next != null)
            {
                _node = next;
                _state = State.ToResource;
                _unit.MoveTo(next.transform.position);
            }
            else
            {
                _state = State.Idle;
            }
        }

        private void GoDeposit()
        {
            ResourceDropoff drop = ResourceDropoff.Nearest(transform.position, _owner != null ? _owner.Team : null);
            if (drop == null) { _state = State.Idle; return; }
            _state = State.ToDropoff;
            _unit.MoveTo(drop.transform.position);
        }

        private void Deposit()
        {
            if (_carried > 0 && _owner != null && TeamManager.Instance != null)
                TeamManager.Instance.EconomyFor(_owner.Team)?.Add(_carriedType, _carried);
            _carried = 0;
        }

        // --- Construction ---

        private void TickToBuild()
        {
            if (_buildTarget == null || _buildTarget.IsComplete) { _state = State.Idle; return; }
            if (_unit.IsMoving) return;
            _state = Near(_buildTarget.transform.position, interactRange) ? State.Building : State.Idle;
        }

        private void TickBuilding()
        {
            if (_buildTarget == null || _buildTarget.IsComplete) { _state = State.Idle; return; }
            _buildTarget.AddWork(buildRate * Time.deltaTime);
            if (_buildTarget.IsComplete) _state = State.Idle;
        }

        // --- Helpers ---

        private bool Near(Vector3 p, float range)
        {
            Vector3 a = transform.position;
            a.z = 0f;
            p.z = 0f;
            return (a - p).sqrMagnitude <= range * range;
        }

        /// <summary>
        /// Nearest non-depleted node of the given type whose position is within unit
        /// sight (the fog-uncover radius) of <paramref name="sightOrigin"/>. Distance is
        /// still measured from the villager so it walks to the closest eligible source.
        /// With no fog of war present, the sight filter is skipped (any node qualifies).
        /// </summary>
        private ResourceNode NearestNodeInSight(ResourceType type, Vector3 sightOrigin)
        {
            FogOfWar fog = FogOfWar.Instance;
            ResourceNode best = null;
            float bestSq = float.MaxValue;
            foreach (ResourceNode n in ResourceNode.All)
            {
                if (n == null || n.IsDepleted || n.resourceType != type) continue;
                if (fog != null && !fog.IsWithinUnitSight(sightOrigin, n.transform.position)) continue;
                float sq = (n.transform.position - transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = n; }
            }
            return best;
        }
    }
}
