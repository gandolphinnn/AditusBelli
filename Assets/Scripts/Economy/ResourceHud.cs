using AditusBelli.Units;
using UnityEngine;

namespace AditusBelli.Economy
{
    /// <summary>
    /// Minimal IMGUI resource + population bar. Placeholder until a proper uGUI
    /// HUD (Phase 7).
    /// </summary>
    public class ResourceHud : MonoBehaviour
    {
        private GUIStyle _style;

        private void OnGUI()
        {
            PlayerResources r = PlayerResources.Instance;
            if (r == null) return;

            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };

            string text =
                $"Food {r.Get(ResourceType.Food)}     Wood {r.Get(ResourceType.Wood)}     " +
                $"Gold {r.Get(ResourceType.Gold)}     Stone {r.Get(ResourceType.Stone)}";

            PlayerPopulation pop = PlayerPopulation.Instance;
            if (pop != null)
                text += $"     Pop {UnitSelectionManager.UnitCount}/{pop.Cap}";

            GUI.Box(new Rect(8, 8, 470, 30), GUIContent.none);
            GUI.Label(new Rect(16, 12, 470, 22), text, _style);
        }
    }
}
