using AditusBelli.Buildings;
using AditusBelli.Teams;
using UnityEngine;

namespace AditusBelli.Combat
{
    /// <summary>
    /// A stationary building weapon (guard tower): auto-acquires the nearest hostile
    /// <see cref="Health"/> within range and damages it on a cooldown. Instant hit
    /// (no projectile art yet). Only fires once the building it sits on is complete.
    /// </summary>
    [RequireComponent(typeof(Owner))]
    public class Turret : MonoBehaviour
    {
        public int attackDamage = 8;
        public float range = 7f;
        public float attackCooldown = 1f;

        private Owner _owner;
        private Building _building;
        private Health _target;
        private float _cooldown;
        private float _scanTimer;

        private void Awake()
        {
            _owner = GetComponent<Owner>();
            _building = GetComponent<Building>();
        }

        private void Update()
        {
            if (_building != null && !_building.IsComplete) return; // dormant while under construction
            if (_owner == null) return;

            if (_cooldown > 0f) _cooldown -= Time.deltaTime;

            if (_target == null || !_target.IsAlive || OutOfRange(_target))
            {
                _target = null;
                Acquire();
                if (_target == null) return;
            }

            if (_cooldown <= 0f)
            {
                _target.TakeDamage(attackDamage);
                _cooldown = attackCooldown;
            }
        }

        private bool OutOfRange(Health h) =>
            ((Vector2)(h.transform.position - transform.position)).sqrMagnitude > range * range;

        private void Acquire()
        {
            _scanTimer -= Time.deltaTime;
            if (_scanTimer > 0f) return;
            _scanTimer = 0.3f;

            Health best = null;
            float bestSq = range * range;
            foreach (Health h in Health.All)
            {
                if (h == null || !h.IsAlive || h.gameObject == gameObject) continue;
                var otherOwner = h.GetComponent<Owner>();
                if (otherOwner == null || !_owner.IsHostileTo(otherOwner)) continue;

                float sq = ((Vector2)(h.transform.position - transform.position)).sqrMagnitude;
                if (sq <= bestSq) { bestSq = sq; best = h; }
            }
            _target = best;
        }
    }
}
