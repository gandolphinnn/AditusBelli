using System.Collections.Generic;
using AditusBelli.Economy;
using AditusBelli.Map;
using UnityEngine;

namespace AditusBelli.Buildings
{
    /// <summary>
    /// A placed building. Starts as a construction site (semi-transparent) and
    /// becomes functional once villagers finish the work. Blocks its footprint
    /// cells; contributes population and acts as a drop-off when complete.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Building : MonoBehaviour
    {
        public BuildingDef def;
        public Vector2Int originCell;
        public bool startCompleted;

        public static readonly List<Building> All = new();

        private SpriteRenderer _sr;
        private float _progress;
        private bool _complete;
        private bool _blocked;
        private bool _capContributed;

        public bool IsComplete => _complete;
        public float Progress => _progress;
        public BuildingDef Def => def;

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
            if (_complete || def == null) return;
            _progress = Mathf.Clamp01(_progress + seconds / Mathf.Max(0.01f, def.buildTime));
            if (_progress >= 1f) CompleteInternal();
            else UpdateVisual();
        }

        private void CompleteInternal()
        {
            _complete = true;
            _progress = 1f;
            UpdateVisual();

            if (def.isDropoff && GetComponent<ResourceDropoff>() == null)
                gameObject.AddComponent<ResourceDropoff>();

            if (!_capContributed && def.populationProvided != 0 && PlayerPopulation.Instance != null)
            {
                PlayerPopulation.Instance.AddCap(def.populationProvided);
                _capContributed = true;
            }

            if (def.trains != null && def.trains.Length > 0 && GetComponent<UnitProducer>() == null)
            {
                var producer = gameObject.AddComponent<UnitProducer>();
                producer.trainable = def.trains;
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
            if (grid == null || def == null) return;

            for (int dx = 0; dx < Mathf.Max(1, def.footprint.x); dx++)
            for (int dy = 0; dy < Mathf.Max(1, def.footprint.y); dy++)
                grid.SetWalkable(new Vector2Int(originCell.x + dx, originCell.y + dy), !blocked);

            _blocked = blocked;
        }

        private void OnDestroy()
        {
            if (_capContributed && PlayerPopulation.Instance != null)
                PlayerPopulation.Instance.RemoveCap(def.populationProvided);
            if (_blocked) BlockFootprint(false);
        }
    }
}
