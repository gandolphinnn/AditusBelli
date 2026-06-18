using UnityEngine;
using UnityEngine.InputSystem;

namespace AditusBelli.CameraControl
{
    /// <summary>
    /// RTS-style orthographic camera. For now the only pan mechanism is the
    /// middle-mouse drag; zoom is on the scroll wheel.
    /// Keyboard and screen-edge panning are kept commented out below so they can
    /// be re-enabled later if wanted.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class RtsCameraController : MonoBehaviour
    {
        // --- Keyboard / screen-edge pan is disabled for now ---
        // [Header("Pan")]
        // [Tooltip("Scroll speed in units/second (at the reference zoom).")]
        // public float panSpeed = 12f;
        // [Tooltip("Thickness in pixels of the screen-edge band that triggers panning.")]
        // public float edgePanBorder = 12f;
        // public bool edgePanEnabled = true;
        // [Tooltip("Reference orthographic size for the pan speed.")]
        // public float referenceOrthoSize = 6f;

        [Header("Zoom")]
        public float zoomStep = 1f;
        public float minOrthoSize = 2f;
        public float maxOrthoSize = 16f;

        private Camera _cam;
        private Vector3 _dragOrigin;
        private bool _dragging;

        private void Awake() => _cam = GetComponent<Camera>();

        /// <summary>Re-centers the camera over a world position (used by the minimap).</summary>
        public void CenterOn(Vector3 world)
        {
            transform.position = new Vector3(world.x, world.y, transform.position.z);
        }

        private void Update()
        {
            // Pan is middle-mouse-drag only for now; keyboard/edge panning is off.
            // HandleKeyboardAndEdgePan();
            HandleMiddleMouseDrag();
            HandleZoom();
        }

        /*
        // Keyboard (WASD / arrows) and screen-edge panning. Disabled for now;
        // re-enable by uncommenting this method, its call in Update(), and the
        // Pan fields above.
        private void HandleKeyboardAndEdgePan()
        {
            Vector2 move = Vector2.zero;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
            }

            var mouse = Mouse.current;
            if (edgePanEnabled && mouse != null && Application.isFocused)
            {
                Vector2 mp = mouse.position.ReadValue();
                if (mp.x <= edgePanBorder) move.x -= 1f;
                else if (mp.x >= Screen.width - edgePanBorder) move.x += 1f;
                if (mp.y <= edgePanBorder) move.y -= 1f;
                else if (mp.y >= Screen.height - edgePanBorder) move.y += 1f;
            }

            if (move == Vector2.zero) return;

            move = Vector2.ClampMagnitude(move, 1f);
            // Keep the pan perceptually constant at every zoom level.
            float zoomFactor = _cam.orthographicSize / referenceOrthoSize;
            transform.position += (Vector3)(move * (panSpeed * zoomFactor * Time.deltaTime));
        }
        */

        private void HandleMiddleMouseDrag()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.middleButton.wasPressedThisFrame)
            {
                _dragOrigin = _cam.ScreenToWorldPoint(mouse.position.ReadValue());
                _dragging = true;
            }
            if (mouse.middleButton.wasReleasedThisFrame)
                _dragging = false;

            if (_dragging && mouse.middleButton.isPressed)
            {
                Vector3 current = _cam.ScreenToWorldPoint(mouse.position.ReadValue());
                transform.position += _dragOrigin - current;
            }
        }

        private void HandleZoom()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            float delta = Mathf.Sign(scroll) * zoomStep;
            _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize - delta, minOrthoSize, maxOrthoSize);
        }
    }
}
