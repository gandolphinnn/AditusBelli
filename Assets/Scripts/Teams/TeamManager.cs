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

        // ----------------------------------------------------------- diplomacy

        /// <summary>
        /// Two teams are enemies when they are distinct and not allied. Alliance is
        /// sharing an <see cref="TeamDef.allianceGroup"/> greater than 0; group 0 means
        /// "unaligned" — hostile to everyone else (the default free-for-all). A null
        /// team is neutral (never an enemy).
        /// </summary>
        public bool AreEnemies(TeamDef a, TeamDef b)
        {
            if (a == null || b == null || a == b) return false;
            return !(a.allianceGroup > 0 && a.allianceGroup == b.allianceGroup);
        }

        /// <summary>Two distinct teams sharing an alliance group greater than 0.</summary>
        public bool AreAllies(TeamDef a, TeamDef b) =>
            a != null && b != null && a != b && a.allianceGroup > 0 && a.allianceGroup == b.allianceGroup;
    }
}
