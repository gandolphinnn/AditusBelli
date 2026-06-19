using AditusBelli.Economy;
using AditusBelli.Teams;
using AditusBelli.Units;
using UnityEngine;
using UnityEngine.UI;

namespace AditusBelli.UI
{
    /// <summary>
    /// Top resource/population bar for the local player. Replaces the old IMGUI
    /// ResourceHud.
    /// </summary>
    public class ResourceBar : MonoBehaviour
    {
        private Text _text;

        private void Start()
        {
            RectTransform root = HudController.Instance != null ? HudController.Instance.Root : null;
            if (root == null) { enabled = false; return; }

            RectTransform panel = UiFactory.Panel(root, "ResourceBar", UiFactory.PanelColor);
            UiFactory.Place(panel, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(560f, 34f));

            _text = UiFactory.Label(panel, "Text", 16, TextAnchor.MiddleCenter);
            UiFactory.Stretch(_text.rectTransform, 10f, 0f, 10f, 0f);
        }

        private void Update()
        {
            TeamManager tm = TeamManager.Instance;
            TeamEconomy econ = tm != null ? tm.LocalEconomy : null;
            if (_text == null || econ == null) return;

            string text =
                Seg("#7BC86C", $"Food {econ.Get(ResourceType.Food)}") + "    " +
                Seg("#B6885A", $"Wood {econ.Get(ResourceType.Wood)}") + "    " +
                Seg("#E6C83C", $"Gold {econ.Get(ResourceType.Gold)}") + "    " +
                Seg("#B0B0BC", $"Stone {econ.Get(ResourceType.Stone)}") + "    " +
                Seg("#FFFFFF", $"Pop {UnitSelectionManager.UnitCountForTeam(tm.LocalPlayer)}/{econ.Cap}");

            _text.text = text;
        }

        private static string Seg(string hex, string body) => $"<color={hex}>{body}</color>";
    }
}
