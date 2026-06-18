using System.Collections.Generic;
using System.Text;
using AditusBelli.Buildings;
using AditusBelli.Combat;
using AditusBelli.Economy;
using AditusBelli.Teams;
using AditusBelli.Units;
using UnityEngine;
using UnityEngine.UI;

namespace AditusBelli.UI
{
    /// <summary>
    /// Bottom-left panel describing the current selection / inspection. Any entity
    /// can be inspected with a single click; only the player's own mobile units
    /// form a multi-unit selection. Replaces the old IMGUI SelectionInfoHud.
    /// </summary>
    public class SelectionPanel : MonoBehaviour
    {
        private RectTransform _panel;
        private Text _info;
        private RectTransform _hpBack;
        private RectTransform _hpFill;
        private Text _hpText;

        private void Start()
        {
            RectTransform root = HudController.Instance != null ? HudController.Instance.Root : null;
            if (root == null) { enabled = false; return; }

            _panel = UiFactory.Panel(root, "SelectionPanel", UiFactory.PanelColor);
            UiFactory.Place(_panel, new Vector2(0f, 0f), new Vector2(8f, 8f), new Vector2(320f, 96f));

            _info = UiFactory.Label(_panel, "Info", 14, TextAnchor.UpperLeft);
            UiFactory.Stretch(_info.rectTransform, 10f, 26f, 10f, 8f);

            _hpBack = UiFactory.Panel(_panel, "HpBar", UiFactory.BarBackColor);
            UiFactory.Place(_hpBack, new Vector2(0f, 0f), new Vector2(10f, 8f), new Vector2(300f, 14f));
            _hpFill = UiFactory.Image(_hpBack, "HpFill", new Color(0.4f, 0.85f, 0.45f, 0.95f)).rectTransform;
            UiFactory.SetBar(_hpFill, 1f);

            _hpText = UiFactory.Label(_hpBack, "HpText", 11, TextAnchor.MiddleCenter);
            UiFactory.Stretch(_hpText.rectTransform);

            _panel.gameObject.SetActive(false);
        }

        private void Update()
        {
            UnitSelectionManager sm = UnitSelectionManager.Instance;
            if (_panel == null || sm == null) return;

            string text = BuildInfo(sm);
            if (string.IsNullOrEmpty(text)) { _panel.gameObject.SetActive(false); return; }

            _panel.gameObject.SetActive(true);
            _info.text = text;

            Health h = PrimaryHealth(sm);
            bool showHp = h != null && h.Max > 0;
            _hpBack.gameObject.SetActive(showHp);
            if (showHp)
            {
                UiFactory.SetBar(_hpFill, h.Current / (float)h.Max);
                _hpText.text = $"HP {h.Current}/{h.Max}";
            }
        }

        /// <summary>The Health whose bar should be shown (single entity only).</summary>
        private static Health PrimaryHealth(UnitSelectionManager sm)
        {
            if (sm.Selected.Count == 1) return sm.Selected[0] != null ? sm.Selected[0].GetComponent<Health>() : null;
            if (sm.Selected.Count >= 2) return null;
            if (sm.SelectedBuilding != null) return sm.SelectedBuilding.GetComponent<Health>();
            if (sm.SelectedUnit != null) return sm.SelectedUnit.GetComponent<Health>();
            return null;
        }

        // -------- info text (ported from the former SelectionInfoHud) ----------

        private static string BuildInfo(UnitSelectionManager sm)
        {
            IReadOnlyList<Unit> selection = sm.Selected;

            if (selection.Count >= 2) return MultiInfo(selection);
            if (selection.Count == 1) return UnitInfo(selection[0]);

            if (sm.SelectedBuilding != null) return BuildingInfo(sm.SelectedBuilding);
            if (sm.SelectedNode != null)
                return $"<b>{sm.SelectedNode.resourceType} source</b>\nRemaining: {sm.SelectedNode.amount}";
            if (sm.SelectedUnit != null) return UnitInfo(sm.SelectedUnit);

            return null;
        }

        private static string UnitInfo(Unit u)
        {
            if (u == null) return null;

            var villager = u.GetComponent<Villager>();
            var combatant = u.GetComponent<Combatant>();
            string name = combatant != null ? "Soldier" : (villager != null ? "Villager" : "Unit");

            var info = new StringBuilder("<b>").Append(name).Append("</b>");
            AppendTeam(info, u.GetComponent<Owner>());

            if (villager != null)
                info.Append(villager.CarriedAmount > 0
                    ? $"\nCarrying: {villager.CarriedAmount}/{villager.CarryCapacity} {villager.CarriedType}"
                    : $"\nInventory empty (capacity {villager.CarryCapacity})");

            return info.ToString();
        }

        private static string BuildingInfo(Building b)
        {
            var info = new StringBuilder("<b>").Append(b.Def != null ? b.Def.displayName : "Building").Append("</b>");
            AppendTeam(info, b.GetComponent<Owner>());

            if (!b.IsComplete)
            {
                info.Append($"\nUnder construction: {Mathf.RoundToInt(b.Progress * 100f)}%");
            }
            else if (b.Def != null)
            {
                if (b.Def.isDropoff) info.Append("\nResource drop-off");
                if (b.Def.populationProvided > 0) info.Append($"\n+{b.Def.populationProvided} population");
            }

            return info.ToString();
        }

        private static string MultiInfo(IReadOnlyList<Unit> selection)
        {
            var totals = new int[4];
            foreach (Unit u in selection)
            {
                if (u == null) continue;
                var villager = u.GetComponent<Villager>();
                if (villager == null || villager.CarriedAmount <= 0) continue;
                totals[(int)villager.CarriedType] += villager.CarriedAmount;
            }

            var sb = new StringBuilder("<b>").Append(selection.Count).Append(" units selected</b>");
            string carried = CarriedSummary(totals);
            if (carried.Length > 0) sb.Append("\nCarrying: ").Append(carried);
            return sb.ToString();
        }

        private static void AppendTeam(StringBuilder sb, Owner owner)
        {
            if (owner != null && owner.Team != null) sb.Append($"  ({owner.Team.displayName})");
        }

        private static string CarriedSummary(int[] totals)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < totals.Length; i++)
            {
                if (totals[i] <= 0) continue;
                if (sb.Length > 0) sb.Append("   ");
                sb.Append(totals[i]).Append(' ').Append((ResourceType)i);
            }
            return sb.ToString();
        }
    }
}
