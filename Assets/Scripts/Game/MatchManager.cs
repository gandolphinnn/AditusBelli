using AditusBelli.Buildings;
using AditusBelli.Teams;
using UnityEngine;

namespace AditusBelli.Game
{
    /// <summary>
    /// Decides the match outcome (conquest): the player wins when no enemy-owned
    /// buildings remain, and loses when it has no buildings left. Shows a result
    /// overlay and freezes the match.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        public float graceTime = 1f;
        public float checkInterval = 0.5f;

        private float _timer;
        private bool _decided;
        private bool _victory;
        private GUIStyle _style;

        private void Awake()
        {
            Time.timeScale = 1f; // guard against a paused timescale persisting from a previous run
            _timer = graceTime;
        }

        private void Update()
        {
            if (_decided) return;

            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = checkInterval;

            TeamDef local = TeamManager.Instance != null ? TeamManager.Instance.LocalPlayer : null;
            if (local == null) return;

            int playerBuildings = 0;
            int enemyBuildings = 0;
            foreach (Building b in Building.All)
            {
                if (b == null) continue;
                var owner = b.GetComponent<Owner>();
                if (owner == null || owner.Team == null) continue; // neutral (e.g. walls)
                if (owner.Team == local) playerBuildings++;
                else enemyBuildings++;
            }

            if (playerBuildings == 0) Decide(false);
            else if (enemyBuildings == 0) Decide(true);
        }

        private void Decide(bool victory)
        {
            _decided = true;
            _victory = victory;
            Time.timeScale = 0f; // freeze the match
        }

        private void OnGUI()
        {
            if (!_decided) return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _style.normal.textColor = _victory ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.45f, 0.4f);

            GUI.Label(new Rect(0, Screen.height / 2f - 40f, Screen.width, 80f),
                _victory ? "VICTORY" : "DEFEAT", _style);
        }
    }
}
