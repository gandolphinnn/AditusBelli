using System.Collections.Generic;
using System.Text;
using AditusBelli.Buildings;
using UnityEngine;
using UnityEngine.UI;

namespace AditusBelli.UI
{
    /// <summary>
    /// The build menu: while <see cref="BuildingPlacer"/>'s menu is open (toggled with
    /// B), it lists the buildable buildings with their number-key shortcut (1-9, then
    /// 0), name and wood cost. Display only — selection is by number key, handled in
    /// BuildingPlacer.
    /// </summary>
    public class BuildMenu : MonoBehaviour
    {
        private RectTransform _panel;
        private Text _text;
        private BuildingPlacer _placer;

        private void Start()
        {
            RectTransform root = HudController.Instance != null ? HudController.Instance.Root : null;
            if (root == null) { enabled = false; return; }

            _panel = UiFactory.Panel(root, "BuildMenu", UiFactory.PanelColor);
            UiFactory.Place(_panel, new Vector2(1f, 0f), new Vector2(-8f, 184f), new Vector2(280f, 280f));

            _text = UiFactory.Label(_panel, "Text", 15, TextAnchor.UpperLeft);
            UiFactory.Stretch(_text.rectTransform, 12f, 10f, 12f, 10f);

            _panel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_placer == null) _placer = BuildingPlacer.Instance;
            bool open = _placer != null && _placer.IsMenuOpen;
            if (_panel.gameObject.activeSelf != open) _panel.gameObject.SetActive(open);
            if (!open) return;

            var sb = new StringBuilder("<b>Build</b>  <size=11>(Esc to close)</size>\n\n");
            IReadOnlyList<GameObject> list = _placer.Buildable;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var b = list[i] != null ? list[i].GetComponent<Building>() : null;
                    if (b == null) continue;
                    string key = i < 9 ? (i + 1).ToString() : "0";
                    sb.Append($"<b>{key}</b>   {b.displayName}   <color=#B6885A>{b.woodCost}W</color>\n");
                }
            }
            _text.text = sb.ToString();
        }
    }
}
