using AditusBelli.Buildings;
using AditusBelli.Entities;
using UnityEngine;

namespace AditusBelli.Combat
{
    /// <summary>
    /// A stationary building weapon (guard tower): auto-acquires the nearest hostile
    /// <see cref="Entity"/> within range and damages it on a cooldown. Instant hit
    /// (no projectile art yet). Only fires once the building it sits on is complete.
    /// </summary>
    public class Turret : MonoBehaviour
    {
        public int attackDamage = 8;
        public float range = 7f;
        public float attackCooldown = 1f;

        private Entity _owner;
        private Building _building;
        private Entity _target;
        private float _cooldown;
        private float _scanTimer;

        private void Awake()
        {
            _owner = GetComponent<Entity>();
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

        private bool OutOfRange(Entity h) =>
            ((Vector2)(h.transform.position - transform.position)).sqrMagnitude > range * range;

        private void Acquire()
        {
            _scanTimer -= Time.deltaTime;
            if (_scanTimer > 0f) return;
            _scanTimer = 0.3f;

            Entity best = null;
            float bestSq = range * range;
            foreach (Entity h in Entity.All)
            {
                if (h == null || !h.IsAlive || h.gameObject == gameObject) continue;
                if (!_owner.IsHostileTo(h)) continue;

                float sq = ((Vector2)(h.transform.position - transform.position)).sqrMagnitude;
                if (sq <= bestSq) { bestSq = sq; best = h; }
            }
            _target = best;
        }
    }
}
