using System.Text;
using AditusBelli.Economy;
using AditusBelli.Units;
using UnityEngine;

namespace AditusBelli.UI
{
    /// <summary>
    /// Bottom-left panel describing the current selection: a single villager's
    /// carried inventory, a resource node's remaining amount, or a summary for a
    /// multi-unit selection. IMGUI placeholder until the proper uGUI HUD (Phase 7).
    /// </summary>
    public class SelectionInfoHud : MonoBehaviour
    {
        private GUIStyle _style;

        private void OnGUI()
        {
            UnitSelectionManager sm = UnitSelectionManager.Instance;
            if (sm == null) return;

            string text = BuildInfo(sm);
            if (string.IsNullOrEmpty(text)) return;

            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };

            var rect = new Rect(8, Screen.height - 64, 420, 56);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, rect.height - 12), text, _style);
        }

        private static string BuildInfo(UnitSelectionManager sm)
        {
            ResourceNode node = sm.SelectedNode;
            if (node != null)
                return $"{node.resourceType} source\nRemaining: {Mathf.CeilToInt(node.amount)}";

            var selection = sm.Selected;
            if (selection.Count == 0) return null;

            if (selection.Count == 1)
            {
                var villager = selection[0].GetComponent<Villager>();
                if (villager == null) return "Unit selected";

                if (villager.CarriedAmount > 0.01f)
                    return $"Villager\nCarrying: {Mathf.FloorToInt(villager.CarriedAmount)}/" +
                           $"{Mathf.RoundToInt(villager.CarryCapacity)} {villager.CarriedType}";

                return $"Villager\nInventory empty (capacity {Mathf.RoundToInt(villager.CarryCapacity)})";
            }

            // Multiple units: count + aggregated carried amounts per resource type.
            var totals = new int[4];
            foreach (Unit u in selection)
            {
                var villager = u.GetComponent<Villager>();
                if (villager == null || villager.CarriedAmount <= 0.01f) continue;
                totals[(int)villager.CarriedType] += Mathf.FloorToInt(villager.CarriedAmount);
            }

            var sb = new StringBuilder();
            sb.Append(selection.Count).Append(" units selected");
            string carried = CarriedSummary(totals);
            if (carried.Length > 0) sb.Append("\nCarrying: ").Append(carried);
            return sb.ToString();
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
