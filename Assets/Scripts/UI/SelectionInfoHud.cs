using System.Collections.Generic;
using System.Text;
using AditusBelli.Buildings;
using AditusBelli.Combat;
using AditusBelli.Economy;
using AditusBelli.Teams;
using AditusBelli.Units;
using UnityEngine;

namespace AditusBelli.UI
{
    /// <summary>
    /// Bottom-left panel describing the current selection / inspection. Any entity
    /// (own or enemy unit, resource node, building) can be inspected with a single
    /// click; only the player's own mobile units form a multi-unit selection.
    /// IMGUI placeholder until the proper uGUI HUD (Phase 7).
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

            var rect = new Rect(8, Screen.height - 84, 420, 76);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, rect.height - 12), text, _style);
        }

        private static string BuildInfo(UnitSelectionManager sm)
        {
            IReadOnlyList<Unit> selection = sm.Selected;

            if (selection.Count >= 2) return MultiInfo(selection);
            if (selection.Count == 1) return UnitInfo(selection[0]);

            if (sm.SelectedBuilding != null) return BuildingInfo(sm.SelectedBuilding);
            if (sm.SelectedNode != null)
                return $"{sm.SelectedNode.resourceType} source\nRemaining: {sm.SelectedNode.amount}";
            if (sm.SelectedUnit != null) return UnitInfo(sm.SelectedUnit);

            return null;
        }

        private static string UnitInfo(Unit u)
        {
            if (u == null) return null;

            var villager = u.GetComponent<Villager>();
            var combatant = u.GetComponent<Combatant>();
            string name = combatant != null ? "Soldier" : (villager != null ? "Villager" : "Unit");

            var info = new StringBuilder(name);
            AppendTeam(info, u.GetComponent<Owner>());

            if (villager != null)
                info.Append(villager.CarriedAmount > 0
                    ? $"\nCarrying: {villager.CarriedAmount}/{villager.CarryCapacity} {villager.CarriedType}"
                    : $"\nInventory empty (capacity {villager.CarryCapacity})");

            AppendHealth(info, u.GetComponent<Health>());
            return info.ToString();
        }

        private static string BuildingInfo(Building b)
        {
            var info = new StringBuilder(b.Def != null ? b.Def.displayName : "Building");
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

            AppendHealth(info, b.GetComponent<Health>());
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

            var sb = new StringBuilder();
            sb.Append(selection.Count).Append(" units selected");
            string carried = CarriedSummary(totals);
            if (carried.Length > 0) sb.Append("\nCarrying: ").Append(carried);
            return sb.ToString();
        }

        private static void AppendTeam(StringBuilder sb, Owner owner)
        {
            if (owner != null && owner.Team != null) sb.Append($"  ({owner.Team.displayName})");
        }

        private static void AppendHealth(StringBuilder sb, Health h)
        {
            if (h != null) sb.Append($"\nHP {h.Current}/{h.Max}");
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
