using AditusBelli.Teams;

namespace AditusBelli.Economy
{
    /// <summary>
    /// One faction's economic state: a resource stockpile (Food/Wood/Gold/Stone) and
    /// a population cap (base + building-provided). One instance exists per team,
    /// seeded from its <see cref="TeamDef"/>. Pure data — no Unity lifecycle; owned
    /// and handed out by <see cref="TeamManager"/>.
    /// </summary>
    public class TeamEconomy
    {
        private readonly int[] _amounts = new int[4]; // indexed by ResourceType
        private readonly int _baseCap;
        private int _buildingCap;

        public TeamEconomy(TeamDef team)
        {
            if (team == null) return;
            _amounts[(int)ResourceType.Food] = team.startFood;
            _amounts[(int)ResourceType.Wood] = team.startWood;
            _amounts[(int)ResourceType.Gold] = team.startGold;
            _amounts[(int)ResourceType.Stone] = team.startStone;
            _baseCap = team.basePopulation;
        }

        /// <summary>Population headroom: team base cap plus what its buildings provide.</summary>
        public int Cap => _baseCap + _buildingCap;

        public int Get(ResourceType type) => _amounts[(int)type];

        public void Add(ResourceType type, int amount) => _amounts[(int)type] += amount;

        public bool TrySpend(ResourceType type, int amount)
        {
            if (_amounts[(int)type] < amount) return false;
            _amounts[(int)type] -= amount;
            return true;
        }

        public void AddCap(int amount) => _buildingCap += amount;
        public void RemoveCap(int amount) => _buildingCap -= amount;
    }
}
