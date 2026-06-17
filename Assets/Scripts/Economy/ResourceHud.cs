using UnityEngine;

namespace AditusBelli.Economy
{
    /// <summary>
    /// Minimal IMGUI resource bar. Placeholder until a proper uGUI HUD (Phase 7).
    /// </summary>
    public class ResourceHud : MonoBehaviour
    {
        private GUIStyle _style;

        private void OnGUI()
        {
            PlayerResources r = PlayerResources.Instance;
            if (r == null) return;

            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };

            GUI.Box(new Rect(8, 8, 380, 30), GUIContent.none);
            GUI.Label(new Rect(16, 12, 380, 22),
                $"Food {r.Get(ResourceType.Food)}     Wood {r.Get(ResourceType.Wood)}     " +
                $"Gold {r.Get(ResourceType.Gold)}     Stone {r.Get(ResourceType.Stone)}",
                _style);
        }
    }
}
