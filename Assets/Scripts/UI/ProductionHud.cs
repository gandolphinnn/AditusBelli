using AditusBelli.Buildings;
using AditusBelli.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AditusBelli.UI
{
    /// <summary>
    /// Training controls for the selected production building, plus the train
    /// (Q) and cancel (C) hotkeys. IMGUI placeholder until the proper uGUI HUD
    /// (Phase 7).
    /// </summary>
    public class ProductionHud : MonoBehaviour
    {
        private UnitProducer SelectedProducer()
        {
            UnitSelectionManager sm = UnitSelectionManager.Instance;
            Building b = sm != null ? sm.SelectedBuilding : null;
            if (b == null || !b.IsComplete) return null;
            return b.GetComponent<UnitProducer>();
        }

        private void Update()
        {
            UnitProducer producer = SelectedProducer();
            if (producer == null) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.qKey.wasPressedThisFrame) producer.Enqueue(producer.FirstTrainable);
            if (kb.cKey.wasPressedThisFrame) producer.CancelLast();
        }

        private void OnGUI()
        {
            UnitProducer producer = SelectedProducer();
            if (producer == null) return;

            UnitDef def = producer.FirstTrainable;
            if (def == null) return;

            string line1 = $"Press Q to train {def.displayName} ({def.foodCost} Food)    -    C to cancel";

            string line2 = $"Queue: {producer.QueueCount}";
            if (producer.QueueCount > 0)
                line2 += $"    -    training {Mathf.RoundToInt(producer.Progress * 100f)}%";
            line2 += producer.HasRally
                ? "    -    rally set (right-click to move it)"
                : "    -    right-click to set a rally point";

            GUI.Box(new Rect(8, 96, 480, 48), GUIContent.none);
            GUI.Label(new Rect(16, 100, 470, 20), line1);
            GUI.Label(new Rect(16, 120, 470, 20), line2);
        }
    }
}
