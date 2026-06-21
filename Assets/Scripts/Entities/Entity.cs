using System.Collections.Generic;
using AditusBelli.Teams;
using UnityEngine;

namespace AditusBelli.Entities
{
    /// <summary>
    /// Base class for everything that lives on the battlefield as an owned, damageable
    /// object: units and buildings. Holds faction ownership (with optional team-color
    /// tinting) and hit points, and registers itself in <see cref="All"/> so combat and
    /// AI can find targets. Subclasses add their own behaviour; when they implement the
    /// Unity lifecycle methods they must call the protected base versions here.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public abstract class Entity : MonoBehaviour
    {
        [Header("Ownership")]
        public TeamDef team;
        [Tooltip("Tint the main sprite with the team color (use for units).")]
        public bool applyTeamColor = true;

        [Header("Health")]
        public int maxHealth;

        /// <summary>Every live entity (units + buildings). Used by combat and AI to find targets.</summary>
        public static readonly List<Entity> All = new();

        private int _current;

        /// <summary>Cached sprite renderer, available to subclasses (e.g. for construction fade).</summary>
        protected SpriteRenderer Sr { get; private set; }

        public TeamDef Team => team;
        public int Current => _current;
        public int Max => maxHealth;
        public bool IsAlive => _current > 0;

        /// <summary>True when this entity belongs to a different (non-null) team than the other.</summary>
        public bool IsHostileTo(Entity other) =>
            other != null && team != null && other.team != null && other.team != team;

        protected virtual void Awake()
        {
            Sr = GetComponent<SpriteRenderer>();
            _current = maxHealth;
        }

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        protected virtual void Start()
        {
            if (applyTeamColor && team != null && Sr != null) Sr.color = team.color;
        }

        protected virtual void OnDestroy() { }

        /// <summary>Sets max and current HP at once (use when configuring at runtime).</summary>
        public void Init(int max)
        {
            maxHealth = max;
            _current = max;
        }

        public void TakeDamage(int amount)
        {
            if (_current <= 0) return;
            _current -= Mathf.Max(0, amount);
            if (_current <= 0) Destroy(gameObject);
        }
    }
}
