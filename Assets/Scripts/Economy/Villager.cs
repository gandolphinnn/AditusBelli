using AditusBelli.Units;
using UnityEngine;

namespace AditusBelli.Economy
{
    /// <summary>
    /// Gatherer behaviour: walk to a resource node, harvest up to the carry
    /// capacity, return to the nearest drop-off, deposit, and repeat until the
    /// node is depleted (then move to the nearest node of the same type).
    /// Drives movement through the <see cref="Unit"/> component.
    /// </summary>
    [RequireComponent(typeof(Unit))]
    public class Villager : MonoBehaviour
    {
        public float gatherRate = 5f;      // units per second
        public float carryCapacity = 10f;
        public float interactRange = 1.2f; // world distance to count as "at" a target

        private enum State { Idle, ToResource, Gathering, ToDropoff }

        private Unit _unit;
        private State _state = State.Idle;
        private ResourceNode _node;
        private float _carried;
        private ResourceType _carriedType;

        public float CarriedAmount => _carried;
        public ResourceType CarriedType => _carriedType;
        public float CarryCapacity => carryCapacity;

        private void Awake() => _unit = GetComponent<Unit>();

        public void GatherFrom(ResourceNode node)
        {
            if (node == null) return;
            _node = node;
            _state = State.ToResource;
            _unit.MoveTo(node.transform.position);
        }

        public void StopGathering()
        {
            _state = State.Idle;
            _node = null;
        }

        private void Update()
        {
            switch (_state)
            {
                case State.ToResource: TickToResource(); break;
                case State.Gathering: TickGathering(); break;
                case State.ToDropoff: TickToDropoff(); break;
            }
        }

        private void TickToResource()
        {
            if (_node == null || _node.IsDepleted)
            {
                if (_carried > 0f) GoDeposit();
                else _state = State.Idle;
                return;
            }

            if (_unit.IsMoving) return;
            _state = Near(_node.transform.position) ? State.Gathering : State.Idle;
        }

        private void TickGathering()
        {
            if (_node == null || _node.IsDepleted) { GoDeposit(); return; }

            _carriedType = _node.resourceType;
            _carried += _node.Extract(gatherRate * Time.deltaTime);

            if (_carried >= carryCapacity || _node == null || _node.IsDepleted)
                GoDeposit();
        }

        private void TickToDropoff()
        {
            ResourceDropoff drop = ResourceDropoff.Nearest(transform.position);
            if (drop == null) { _state = State.Idle; return; }
            if (_unit.IsMoving) return;

            if (!Near(drop.transform.position)) { _state = State.Idle; return; }

            Deposit();

            if (_node != null && !_node.IsDepleted)
            {
                _state = State.ToResource;
                _unit.MoveTo(_node.transform.position);
                return;
            }

            ResourceNode next = NearestNode(_carriedType);
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
            ResourceDropoff drop = ResourceDropoff.Nearest(transform.position);
            if (drop == null) { _state = State.Idle; return; }
            _state = State.ToDropoff;
            _unit.MoveTo(drop.transform.position);
        }

        private void Deposit()
        {
            int whole = Mathf.FloorToInt(_carried);
            if (whole > 0 && PlayerResources.Instance != null)
                PlayerResources.Instance.Add(_carriedType, whole);
            _carried -= whole;
        }

        private bool Near(Vector3 p)
        {
            Vector3 a = transform.position;
            a.z = 0f;
            p.z = 0f;
            return (a - p).sqrMagnitude <= interactRange * interactRange;
        }

        private ResourceNode NearestNode(ResourceType type)
        {
            ResourceNode best = null;
            float bestSq = float.MaxValue;
            foreach (ResourceNode n in ResourceNode.All)
            {
                if (n == null || n.IsDepleted || n.resourceType != type) continue;
                float sq = (n.transform.position - transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = n; }
            }
            return best;
        }
    }
}
