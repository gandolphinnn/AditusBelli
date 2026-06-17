using System.Collections.Generic;
using UnityEngine;

namespace AditusBelli.Combat
{
    /// <summary>
    /// Hit points for a unit or building. Destroys the GameObject on death
    /// (buildings free their footprint via their own OnDestroy).
    /// </summary>
    public class Health : MonoBehaviour
    {
        public int maxHealth = 50;

        private int _current;

        public static readonly List<Health> All = new();

        public int Current => _current;
        public int Max => maxHealth;
        public bool IsAlive => _current > 0;

        private void Awake() => _current = maxHealth;
        private void OnEnable() => All.Add(this);

        /// <summary>Sets max and current HP at once (use when configuring at runtime).</summary>
        public void Init(int max)
        {
            maxHealth = max;
            _current = max;
        }
        private void OnDisable() => All.Remove(this);

        public void TakeDamage(int amount)
        {
            if (_current <= 0) return;
            _current -= Mathf.Max(0, amount);
            if (_current <= 0) Destroy(gameObject);
        }
    }
}
