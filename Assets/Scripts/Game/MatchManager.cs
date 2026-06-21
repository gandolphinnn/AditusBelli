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

        /// <summary>True once the match outcome is settled (read by the HUD overlay).</summary>
        public bool IsDecided => _decided;

        /// <summary>Whether the settled outcome is a win for the local player.</summary>
        public bool IsVictory => _victory;

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

            TeamManager tm = TeamManager.Instance;
            TeamDef local = tm != null ? tm.LocalPlayer : null;
            if (local == null) return;

            // Player side = the local team and its allies; enemy side = everyone hostile
            // to it. Conquest ends when one side has no buildings left.
            int playerSide = 0;
            int enemySide = 0;
            foreach (Building b in Building.AllBuildings)
            {
                if (b == null || b.Team == null) continue; // unowned (e.g. neutral walls)
                if (tm.AreEnemies(local, b.Team)) enemySide++;
                else playerSide++;
            }

            if (playerSide == 0) Decide(false);
            else if (enemySide == 0) Decide(true);
        }

        private void Decide(bool victory)
        {
            _decided = true;
            _victory = victory;
            Time.timeScale = 0f; // freeze the match
        }
    }
}
