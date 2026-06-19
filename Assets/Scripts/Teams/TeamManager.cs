using System.Collections.Generic;
using AditusBelli.Economy;
using UnityEngine;

namespace AditusBelli.Teams
{
    /// <summary>
    /// Holds the teams in play and which one the local player controls, and owns the
    /// per-team economies. The team list and the local-player reference are set up in
    /// the editor; each team's <see cref="TeamEconomy"/> is created lazily on first
    /// access and seeded from its <see cref="TeamDef"/>.
    /// </summary>
    public class TeamManager : MonoBehaviour
    {
        public static TeamManager Instance { get; private set; }

        public TeamDef[] teams;
        public TeamDef localPlayer;

        private readonly Dictionary<TeamDef, TeamEconomy> _economies = new();

        public TeamDef LocalPlayer => localPlayer;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>The economy for a team, created (and seeded from the team data) on demand.</summary>
        public TeamEconomy EconomyFor(TeamDef team)
        {
            if (team == null) return null;
            if (!_economies.TryGetValue(team, out TeamEconomy econ))
            {
                econ = new TeamEconomy(team);
                _economies[team] = econ;
            }
            return econ;
        }

        /// <summary>The local player's economy (used by the HUD and player commands).</summary>
        public TeamEconomy LocalEconomy => EconomyFor(localPlayer);
    }
}
