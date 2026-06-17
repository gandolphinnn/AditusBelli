using System.Collections.Generic;
using AditusBelli.Map;
using UnityEngine;

namespace AditusBelli.Units
{
    /// <summary>
    /// Selectable, movable unit. Movement follows a grid path computed by the
    /// <see cref="GameGrid"/>; if no grid/path is available it falls back to a
    /// straight line toward the goal.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Unit : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 3f;
        public float arriveThreshold = 0.05f;

        [Header("Selection")]
        [Tooltip("Sprite shown under the unit when it is selected.")]
        public SpriteRenderer selectionIndicator;

        private readonly List<Vector3> _path = new();
        private int _pathIndex;
        private bool _hasTarget;

        public bool IsSelected { get; private set; }
        public bool IsMoving => _hasTarget;

        private void Awake() => SetSelected(false);

        private void OnEnable() => UnitSelectionManager.Register(this);
        private void OnDisable() => UnitSelectionManager.Unregister(this);

        public void SetSelected(bool value)
        {
            IsSelected = value;
            if (selectionIndicator != null)
                selectionIndicator.enabled = value;
        }

        public void MoveTo(Vector3 worldPosition)
        {
            worldPosition.z = transform.position.z;

            _path.Clear();
            _pathIndex = 0;

            GameGrid grid = GameGrid.Instance;
            if (grid != null)
            {
                List<Vector3> route = grid.FindPath(transform.position, worldPosition);
                if (route != null && route.Count > 0)
                {
                    _path.AddRange(route);
                    _hasTarget = true;
                    return;
                }
            }

            // Fallback: straight line toward the goal.
            _path.Add(worldPosition);
            _hasTarget = true;
        }

        private void Update()
        {
            if (!_hasTarget) return;

            Vector3 wp = _path[_pathIndex];
            wp.z = transform.position.z;

            transform.position = Vector3.MoveTowards(
                transform.position, wp, moveSpeed * Time.deltaTime);

            if ((transform.position - wp).sqrMagnitude <= arriveThreshold * arriveThreshold)
            {
                _pathIndex++;
                if (_pathIndex >= _path.Count) _hasTarget = false;
            }
        }
    }
}
