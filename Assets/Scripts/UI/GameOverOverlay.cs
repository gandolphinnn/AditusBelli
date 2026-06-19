using AditusBelli.Game;
using UnityEngine;
using UnityEngine.UI;

namespace AditusBelli.UI
{
    /// <summary>
    /// Full-screen VICTORY / DEFEAT overlay shown when <see cref="MatchManager"/>
    /// decides the match. Replaces the old IMGUI label.
    /// </summary>
    public class GameOverOverlay : MonoBehaviour
    {
        private RectTransform _overlay;
        private Text _text;
        private MatchManager _match;

        private void Start()
        {
            RectTransform root = HudController.Instance != null ? HudController.Instance.Root : null;
            if (root == null) { enabled = false; return; }

            _match = FindAnyObjectByType<MatchManager>();

            _overlay = UiFactory.Panel(root, "GameOverOverlay", new Color(0f, 0f, 0f, 0.55f));
            UiFactory.Stretch(_overlay);

            _text = UiFactory.Label(_overlay, "Result", 64, TextAnchor.MiddleCenter);
            _text.fontStyle = FontStyle.Bold;
            UiFactory.Stretch(_text.rectTransform);

            _overlay.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_overlay == null || _match == null) return;

            bool decided = _match.IsDecided;
            if (_overlay.gameObject.activeSelf != decided) _overlay.gameObject.SetActive(decided);
            if (!decided) return;

            _text.text = _match.IsVictory ? "VICTORY" : "DEFEAT";
            _text.color = _match.IsVictory ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.45f, 0.4f);
        }
    }
}
