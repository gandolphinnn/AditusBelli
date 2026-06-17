using UnityEngine;

namespace AditusBelli.Units
{
    /// <summary>
    /// Selectable, movable unit. For now movement is a straight line toward the
    /// target; pathfinding will be added later.
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

        private Vector3 _target;
        private bool _hasTarget;

        public bool IsSelected { get; private set; }

        private void Awake()
        {
            _target = transform.position;
            SetSelected(false);
        }

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
            _target = worldPosition;
            _hasTarget = true;
        }

        private void Update()
        {
            if (!_hasTarget) return;

            transform.position = Vector3.MoveTowards(
                transform.position, _target, moveSpeed * Time.deltaTime);

            if ((transform.position - _target).sqrMagnitude <= arriveThreshold * arriveThreshold)
                _hasTarget = false;
        }
    }
}
