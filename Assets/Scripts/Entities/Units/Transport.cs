using System.Collections.Generic;
using AditusBelli.Combat;
using AditusBelli.Economy;
using AditusBelli.Map;
using UnityEngine;

namespace AditusBelli.Units
{
    /// <summary>
    /// Naval transport: carries land units across water. Units ordered to board walk to
    /// the shore beside the ship and are taken aboard (deactivated and parented to the
    /// ship); the ship then sails and, on reaching a target shore, drops them onto the
    /// nearest free land. Cross-domain approach is handled by the normal pathfinder, which
    /// routes each mover to the nearest reachable cell in its own domain (ship -> coastal
    /// water, unit -> the shore next to the ship). Sinking a loaded transport loses its
    /// cargo. While aboard, units don't count toward the population cap (a known quirk).
    /// </summary>
    [RequireComponent(typeof(Unit))]
    public class Transport : MonoBehaviour
    {
        public int capacity = 5;
        [Tooltip("How close (world units) a boarding unit must get to be taken aboard, and " +
                 "how close to the target shore before unloading.")]
        public float loadRange = 1.8f;
        [Tooltip("How often (seconds) inbound passengers re-path to the ship if it has moved.")]
        public float reissueInterval = 0.5f;

        private Unit _unit;
        private readonly List<GameObject> _cargo = new();
        private readonly List<Unit> _inbound = new();
        private bool _unloading;
        private Vector3 _unloadTarget;
        private float _reissueTimer;

        public int CargoCount => _cargo.Count;
        public int PendingCount => _inbound.Count;
        public int Capacity => capacity;

        /// <summary>True while there is still room counting both cargo and inbound passengers.</summary>
        public bool HasRoom => _cargo.Count + _inbound.Count < capacity;

        private void Awake() => _unit = GetComponent<Unit>();

        /// <summary>Orders a land unit to walk to the ship and board it (if there is room).</summary>
        public void Board(Unit u)
        {
            if (u == null || u == _unit || u.naval) return;
            if (!HasRoom || _inbound.Contains(u) || _cargo.Contains(u.gameObject)) return;

            // Drop whatever it was doing so it heads for the ship and isn't redirected.
            var villager = u.GetComponent<Villager>();
            if (villager != null) villager.StopTasks();
            var combatant = u.GetComponent<Combatant>();
            if (combatant != null) combatant.StopCombat();

            _inbound.Add(u);
            u.MoveTo(transform.position); // land unit routes to the shore nearest the ship
        }

        /// <summary>Sends the ship to the water nearest a shore and unloads its cargo there.</summary>
        public void UnloadAt(Vector3 worldTarget)
        {
            if (_cargo.Count == 0) return;
            _unloading = true;
            _unloadTarget = worldTarget;
            _unit.MoveTo(worldTarget); // naval pathfinder stops at the nearest water to the shore
        }

        private void Update()
        {
            TickInbound();
            TickUnload();
        }

        private void TickInbound()
        {
            if (_inbound.Count == 0) return;

            _reissueTimer -= Time.deltaTime;
            bool reissue = _reissueTimer <= 0f;
            if (reissue) _reissueTimer = reissueInterval;

            for (int i = _inbound.Count - 1; i >= 0; i--)
            {
                Unit u = _inbound[i];
                if (u == null) { _inbound.RemoveAt(i); continue; }

                if (Near(u.transform.position, transform.position, loadRange))
                {
                    _inbound.RemoveAt(i);
                    Load(u);
                }
                else if (reissue && !u.IsMoving)
                {
                    u.MoveTo(transform.position); // re-path (the ship may have moved, or it got stuck)
                }
            }
        }

        private void Load(Unit u)
        {
            u.Stop();
            u.transform.SetParent(transform, worldPositionStays: true);
            u.gameObject.SetActive(false); // unregisters from selection / entity / vision lists
            _cargo.Add(u.gameObject);
        }

        private void TickUnload()
        {
            if (!_unloading) return;

            bool arrived = !_unit.IsMoving || Near(transform.position, _unloadTarget, loadRange);
            if (!arrived) return;

            _unloading = false;
            UnloadAll();
        }

        private void UnloadAll()
        {
            GameGrid grid = GameGrid.Instance;
            List<Vector3> spots = grid != null ? FindLandSpots(grid, _cargo.Count) : null;

            int si = 0;
            foreach (GameObject go in _cargo)
            {
                if (go == null) continue;
                Vector3 spot = (spots != null && si < spots.Count) ? spots[si++] : transform.position;
                go.transform.SetParent(null, worldPositionStays: true);
                go.transform.position = new Vector3(spot.x, spot.y, 0f);
                go.SetActive(true);
            }
            _cargo.Clear();
        }

        /// <summary>Up to <paramref name="needed"/> free land cells on outward rings around the ship.</summary>
        private List<Vector3> FindLandSpots(GameGrid grid, int needed)
        {
            var spots = new List<Vector3>(needed);
            Vector3Int c = grid.WorldToCell(transform.position);
            var origin = new Vector2Int(c.x, c.y);
            for (int r = 1; r <= 12 && spots.Count < needed; r++)
            {
                for (int dy = -r; dy <= r && spots.Count < needed; dy++)
                for (int dx = -r; dx <= r && spots.Count < needed; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue; // ring outline only
                    var cell = new Vector2Int(origin.x + dx, origin.y + dy);
                    if (grid.IsWalkable(cell)) spots.Add(grid.CellCenter(cell));
                }
            }
            return spots;
        }

        private static bool Near(Vector3 a, Vector3 b, float range)
        {
            a.z = 0f;
            b.z = 0f;
            return (a - b).sqrMagnitude <= range * range;
        }
    }
}
