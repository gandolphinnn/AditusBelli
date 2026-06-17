using AditusBelli.Teams;
using AditusBelli.Units;
using UnityEngine;

namespace AditusBelli.Combat
{
    /// <summary>
    /// Melee combat behaviour: approach a target and attack it on a cooldown.
    /// Auto-acquires nearby enemies when idle (so units defend themselves).
    /// Drives movement through the <see cref="Unit"/> component.
    /// </summary>
    [RequireComponent(typeof(Unit))]
    public class Combatant : MonoBehaviour
    {
        public int attackDamage = 6;
        public float attackRange = 1.6f;
        public float attackCooldown = 1f;
        public float aggroRange = 5f;

        [Tooltip("If false, the unit never moves: it only fights what is already within melee range.")]
        public bool mobile = true;

        private Unit _unit;
        private Owner _owner;
        private Health _target;
        private float _cooldown;
        private float _scanTimer;

        private void Awake()
        {
            _unit = GetComponent<Unit>();
            _owner = GetComponent<Owner>();
        }

        public void AttackTarget(Health target)
        {
            if (target != null) _target = target;
        }

        public void StopCombat() => _target = null;

        private void Update()
        {
            if (_cooldown > 0f) _cooldown -= Time.deltaTime;

            if (_target == null || !_target.IsAlive)
            {
                _target = null;
                AutoAcquire();
                return;
            }

            float dist = Vector2.Distance(transform.position, _target.transform.position);
            if (dist <= attackRange)
            {
                _unit.Stop();
                if (_cooldown <= 0f)
                {
                    _target.TakeDamage(attackDamage);
                    _cooldown = attackCooldown;
                }
            }
            else if (mobile)
            {
                if (!_unit.IsMoving) _unit.MoveTo(_target.transform.position);
            }
            else
            {
                _target = null; // immobile: never chase, only fight what is already in range
            }
        }

        private void AutoAcquire()
        {
            if (_unit.IsMoving || _owner == null) return; // don't get distracted mid-order

            _scanTimer -= Time.deltaTime;
            if (_scanTimer > 0f) return;
            _scanTimer = 0.4f;

            // Mobile units notice enemies from afar; immobile ones only react to
            // whatever is already within melee range.
            float range = mobile ? aggroRange : attackRange;

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

            if (best != null) _target = best;
        }
    }
}
