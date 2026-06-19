using System.Collections.Generic;
using AditusBelli.Economy;
using AditusBelli.Map;
using AditusBelli.Teams;
using UnityEngine;

namespace AditusBelli.Buildings
{
    /// <summary>
    /// A placed building. Its definition (footprint, cost, effects) lives directly
    /// on the prefab — there is no separate BuildingDef asset. Starts as a
    /// construction site (semi-transparent) and becomes functional once villagers
    /// finish the work; blocks its footprint cells, contributes population to its
    /// owner's economy and acts as a drop-off when complete.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Building : MonoBehaviour
    {
        [Header("Definition")]
        public string displayName = "Building";
        public Vector2Int footprint = new Vector2Int(2, 2);
        public int woodCost = 50;
        [Tooltip("Total villager-seconds of work needed to finish construction.")]
        public float buildTime = 8f;
        public int populationProvided = 0;
        public bool isDropoff = false;
        [Tooltip("Unit prefabs this building can train (each needs a UnitStats).")]
        public GameObject[] trains;

        [Header("Runtime placement")]
        public Vector2Int originCell;
        public bool startCompleted;

        public static readonly List<Building> All = new();

        private SpriteRenderer _sr;
        private float _progress;
        private bool _complete;
        private bool _blocked;
        private bool _capContributed;
        private TeamEconomy _capEconomy; // economy the population cap was added to

        public bool IsComplete => _complete;
        public float Progress => _progress;

        private void Awake() => _sr = GetComponent<SpriteRenderer>();

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void Start()
        {
            BlockFootprint(true);
            if (startCompleted) CompleteInternal();
            else UpdateVisual();
        }

        /// <summary>Adds construction work (in villager-seconds).</summary>
        public void AddWork(float seconds)
        {
            if (_complete) return;
            _progress = Mathf.Clamp01(_progress + seconds / Mathf.Max(0.01f, buildTime));
            if (_progress >= 1f) CompleteInternal();
            else UpdateVisual();
        }

        private void CompleteInternal()
        {
            _complete = true;
            _progress = 1f;
            UpdateVisual();

            if (isDropoff && GetComponent<ResourceDropoff>() == null)
                gameObject.AddComponent<ResourceDropoff>();

            if (!_capContributed && populationProvided != 0)
            {
                var owner = GetComponent<Owner>();
                _capEconomy = (owner != null && TeamManager.Instance != null)
                    ? TeamManager.Instance.EconomyFor(owner.Team) : null;
                if (_capEconomy != null)
                {
                    _capEconomy.AddCap(populationProvided);
                    _capContributed = true;
                }
            }

            if (trains != null && trains.Length > 0 && GetComponent<UnitProducer>() == null)
            {
                var producer = gameObject.AddComponent<UnitProducer>();
                producer.trainable = trains;
            }
        }

        private void UpdateVisual()
        {
            if (_sr == null) return;
            Color c = _sr.color;
            c.a = _complete ? 1f : Mathf.Lerp(0.35f, 0.85f, _progress);
            _sr.color = c;
        }

        private void BlockFootprint(bool blocked)
        {
            GameGrid grid = GameGrid.Instance;
            if (grid == null) return;

            for (int dx = 0; dx < Mathf.Max(1, footprint.x); dx++)
            for (int dy = 0; dy < Mathf.Max(1, footprint.y); dy++)
                grid.SetWalkable(new Vector2Int(originCell.x + dx, originCell.y + dy), !blocked);

            _blocked = blocked;
        }

        private void OnDestroy()
        {
            if (_capContributed && _capEconomy != null)
                _capEconomy.RemoveCap(populationProvided);
            if (_blocked) BlockFootprint(false);
        }
    }
}
