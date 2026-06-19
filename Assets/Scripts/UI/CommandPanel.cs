using AditusBelli.Buildings;
using AditusBelli.Units;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AditusBelli.UI
{
    /// <summary>
    /// Bottom-right command panel: the production queue read-out (Train / Cancel,
    /// shown only when a producer building is selected) and the controls hint.
    /// Building construction is driven by the separate build menu (B); Q/C train and
    /// cancel here.
    /// </summary>
    public class CommandPanel : MonoBehaviour
    {
        private RectTransform _productionGroup;
        private Text _trainLabel;
        private Text _queueText;
        private RectTransform _progressFill;
        private Text _hint;

        private void Start()
        {
            RectTransform root = HudController.Instance != null ? HudController.Instance.Root : null;
            if (root == null) { enabled = false; return; }

            RectTransform panel = UiFactory.Panel(root, "CommandPanel", UiFactory.PanelColor);
            UiFactory.Place(panel, new Vector2(1f, 0f), new Vector2(-8f, 8f), new Vector2(360f, 132f));

            // Production sub-group (visible only when a producer building is selected).
            _productionGroup = UiFactory.Container(panel, "Production");
            UiFactory.Stretch(_productionGroup);

            Button train = UiFactory.Button(_productionGroup, "TrainBtn", "Train", TrainClicked);
            UiFactory.Place(train.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(220f, 28f));
            _trainLabel = train.GetComponentInChildren<Text>();

            Button cancel = UiFactory.Button(_productionGroup, "CancelBtn", "Cancel", CancelClicked);
            UiFactory.Place(cancel.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(240f, -8f), new Vector2(108f, 28f));

            _queueText = UiFactory.Label(_productionGroup, "Queue", 12, TextAnchor.UpperLeft);
            UiFactory.Place(_queueText.rectTransform,
                new Vector2(0f, 1f), new Vector2(12f, -42f), new Vector2(336f, 18f));

            RectTransform progBack = UiFactory.Panel(_productionGroup, "Progress", UiFactory.BarBackColor);
            UiFactory.Place(progBack, new Vector2(0f, 1f), new Vector2(12f, -64f), new Vector2(336f, 12f));
            _progressFill = UiFactory.Image(progBack, "ProgressFill", new Color(0.45f, 0.7f, 1f, 0.95f)).rectTransform;
            UiFactory.SetBar(_progressFill, 0f);

            _hint = UiFactory.Label(panel, "Hint", 12, TextAnchor.LowerLeft, new Color(0.85f, 0.9f, 1f, 1f));
            UiFactory.Place(_hint.rectTransform,
                new Vector2(0f, 0f), new Vector2(12f, 6f), new Vector2(336f, 18f));
        }

        private void Update()
        {
            // Q/C hotkeys (only meaningful with a producer selected).
            UnitProducer producer = SelectedProducer();
            Keyboard kb = Keyboard.current;
            if (producer != null && kb != null)
            {
                if (kb.qKey.wasPressedThisFrame) producer.Enqueue(producer.FirstTrainable);
                if (kb.cKey.wasPressedThisFrame) producer.CancelLast();
            }

            RefreshProduction(producer);
            RefreshHint();
        }

        private void RefreshProduction(UnitProducer producer)
        {
            bool show = producer != null && producer.FirstTrainable != null;
            _productionGroup.gameObject.SetActive(show);
            if (!show) return;

            UnitStats stats = producer.FirstTrainable.GetComponent<UnitStats>();
            _trainLabel.text = stats != null
                ? $"Train {stats.displayName} ({stats.foodCost}F)"
                : "Train";

            string queue = $"Queue: {producer.QueueCount}";
            if (producer.QueueCount > 0) queue += $"   training {Mathf.RoundToInt(producer.Progress * 100f)}%";
            queue += producer.HasRally ? "   rally set" : "   right-click to rally";
            _queueText.text = queue;

            UiFactory.SetBar(_progressFill, producer.QueueCount > 0 ? producer.Progress : 0f);
        }

        private void RefreshHint()
        {
            _hint.text = BuildingPlacer.IsActive
                ? "Placing: left-click to build, right-click / Esc to cancel"
                : "B: build menu     Q: train     C: cancel";
        }

        private void TrainClicked()
        {
            UnitProducer producer = SelectedProducer();
            if (producer != null) producer.Enqueue(producer.FirstTrainable);
        }

        private void CancelClicked()
        {
            UnitProducer producer = SelectedProducer();
            if (producer != null) producer.CancelLast();
        }

        private static UnitProducer SelectedProducer()
        {
            UnitSelectionManager sm = UnitSelectionManager.Instance;
            Building b = sm != null ? sm.SelectedBuilding : null;
            if (b == null || !b.IsComplete) return null;
            return b.GetComponent<UnitProducer>();
        }
    }
}
