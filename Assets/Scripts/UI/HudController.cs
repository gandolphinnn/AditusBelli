using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AditusBelli.UI
{
    /// <summary>
    /// Root of the runtime uGUI HUD. Builds the screen-space canvas and the input
    /// EventSystem, then attaches the individual HUD panels. The whole HUD is
    /// created from code (the scene is code-generated), so the scene builder only
    /// needs to add this one component.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        public static HudController Instance { get; private set; }

        private RectTransform _root;

        /// <summary>The canvas root every panel parents itself under.</summary>
        public RectTransform Root => _root;

        /// <summary>
        /// True when the mouse is over an interactive HUD element. Gameplay input
        /// (selection, commands, building placement) reads <c>Mouse.current</c>
        /// directly, so it must consult this to avoid acting through the HUD.
        /// </summary>
        public static bool IsPointerOverUi =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void Awake()
        {
            Instance = this;
            EnsureEventSystem();
            _root = BuildCanvas();

            // Panels build themselves under Root in their own Start().
            gameObject.AddComponent<ResourceBar>();
            gameObject.AddComponent<SelectionPanel>();
            gameObject.AddComponent<CommandPanel>();
            gameObject.AddComponent<BuildMenu>();
            gameObject.AddComponent<SelectionBox>();
            gameObject.AddComponent<GameOverOverlay>();
            gameObject.AddComponent<Minimap>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            // New Input System only: must use InputSystemUIInputModule, not the
            // legacy StandaloneInputModule (which throws under activeInputHandler 1).
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();
            // Created from code, the module has no actions wired up; this assigns the
            // built-in UI actions (point/click/...), without which clicks never register.
            module.AssignDefaultActions();
        }

        private static RectTransform BuildCanvas()
        {
            var go = new GameObject("HudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return (RectTransform)go.transform;
        }
    }
}
