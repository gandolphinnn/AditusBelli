using UnityEngine;

namespace AditusBelli.Teams
{
    /// <summary>
    /// Faction ownership of an entity. Two entities are hostile when their teams
    /// differ. Optionally tints the entity's sprite with the team color.
    /// </summary>
    public class Owner : MonoBehaviour
    {
        public TeamDef team;

        [Tooltip("Tint the main sprite with the team color (use for units).")]
        public bool applyTeamColor = true;

        public TeamDef Team => team;

        public bool IsHostileTo(Owner other) =>
            other != null && team != null && other.team != null && other.team != team;

        private void Start()
        {
            if (!applyTeamColor || team == null) return;
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = team.color;
        }
    }
}
