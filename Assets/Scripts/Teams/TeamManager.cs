using UnityEngine;

namespace AditusBelli.Teams
{
    /// <summary>
    /// Holds the teams in play and which one the local player controls.
    /// The team list and the local-player reference are set up in the editor.
    /// </summary>
    public class TeamManager : MonoBehaviour
    {
        public static TeamManager Instance { get; private set; }

        public TeamDef[] teams;
        public TeamDef localPlayer;

        public TeamDef LocalPlayer => localPlayer;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
