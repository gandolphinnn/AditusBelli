using System.Collections.Generic;
using AditusBelli.Buildings;
using AditusBelli.Combat;
using AditusBelli.Economy;
using AditusBelli.Entities;
using AditusBelli.Map;
using AditusBelli.Teams;
using AditusBelli.Units;
using UnityEngine;

namespace AditusBelli.Game
{
    /// <summary>
    /// An AI player. It runs under the exact same rules as the human: its own
    /// <see cref="TeamEconomy"/> (resources + population cap), its own fog of war via
    /// <see cref="TeamVision"/> (it only acts on what it has seen), and the normal
    /// building/production pipeline. Each think tick it, in priority order: keeps
    /// villagers gathering the resource it needs and building its construction sites,
    /// raises population with houses, grows its villager economy, follows a build order
    /// (barracks, expansion warehouses, docks), defends with towers/walls when
    /// threatened, trains and commits attack waves against scouted enemies, sails ships,
    /// and keeps a scout exploring the fog. Diplomacy (who is an enemy) comes from
    /// <see cref="TeamManager"/>, so the same bot works in free-for-all or team games.
    /// </summary>
    public class BotPlayer : MonoBehaviour
    {
        /// <summary>Building prefabs the bot may construct (the same set the player has).</summary>
        [System.Serializable]
        public struct Loadout
        {
            public GameObject townCenter;
            public GameObject house;
            public GameObject warehouse;
            public GameObject barracks;
            public GameObject guardTower;
            public GameObject wall;
            public GameObject dock;
        }

        [Header("Cadence")]
        [Tooltip("Seconds between AI decisions.")]
        public float thinkInterval = 1f;
        [Tooltip("Grace period before the AI starts training its army.")]
        public float startupDelay = 20f;

        [Header("Economy")]
        [Tooltip("Villagers the bot grows its economy toward.")]
        public int targetVillagers = 14;
        [Tooltip("Max villagers assigned to each construction site.")]
        public int maxBuildersPerSite = 3;
        [Tooltip("Build a house once population usage reaches (cap - this).")]
        public int popBuffer = 3;
        [Tooltip("Build a barracks once the bot has at least this many villagers.")]
        public int barracksAfterVillagers = 4;

        [Header("Expansion")]
        public bool enableExpansion = true;
        [Tooltip("Build a warehouse near a known resource farther than this (world units) from any drop-off.")]
        public float expansionDistance = 16f;

        [Header("Defense")]
        [Tooltip("Enemies seen within this radius (world units) of any building count as a threat.")]
        public float baseDefenseRadius = 14f;
        [Tooltip("Guard towers the bot keeps near its base even when not threatened.")]
        public int proactiveTowers = 2;
        [Tooltip("Total wall segments the bot will lay as a partial barrier.")]
        public int desiredWalls = 6;

        [Header("Military")]
        public int firstWaveSize = 3;
        public int waveSizeIncrement = 2;
        public int maxWaveSize = 12;
        [Tooltip("Leash radius for soldiers mustering at the base (they defend within it).")]
        public float musterLeash = 9f;

        [Header("Naval")]
        public bool enableNaval = true;
        [Tooltip("Ships the bot keeps for sea presence and scouting (ships have no weapon yet).")]
        public int targetShips = 2;
        [Tooltip("Min wood before the bot invests in a dock.")]
        public int dockMinWood = 150;

        [Header("Scouting")]
        public bool enableScouting = true;
        [Tooltip("Don't dedicate a scout until the bot has at least this many villagers.")]
        public int minVillagersForScout = 3;

        [Header("Debug")]
        public bool verbose = false;

        // --- references / state ---
        private TeamDef _team;
        private TeamEconomy _econ;
        private GameGrid _grid;
        private TeamVision _vision;
        private Building _townCenter;
        private Loadout _loadout;
        private Vector3 _homeWorld;

        private float _thinkTimer;
        private float _clock;
        private int _waveSize;
        private bool _threatened;
        private Vector3 _threatPos;

        private Villager _scout;

        // cached building names (read from the prefabs so matching can't drift)
        private string _nameTownCenter, _nameHouse, _nameWarehouse, _nameBarracks, _nameTower, _nameWall, _nameDock;

        // scratch rebuilt each think
        private readonly List<Villager> _villagers = new();
        private readonly List<Combatant> _soldiers = new();
        private readonly List<Unit> _ships = new();
        private readonly List<Building> _myBuildings = new();
        private readonly List<Building> _sites = new();
        private readonly List<Entity> _visibleEnemies = new();

        // persistent knowledge (survives re-fogging, like a player's memory)
        private readonly HashSet<ResourceNode> _knownNodes = new();
        private readonly HashSet<Building> _knownEnemyBuildings = new();
        private readonly List<Combatant> _attackers = new();

        public void Init(TeamDef team, Building townCenter, Loadout loadout)
        {
            _team = team;
            _townCenter = townCenter;
            _loadout = loadout;
            _waveSize = firstWaveSize;
            if (townCenter != null) _homeWorld = townCenter.transform.position;

            _nameTownCenter = townCenter != null ? townCenter.displayName : NameOf(loadout.townCenter);
            _nameHouse = NameOf(loadout.house);
            _nameWarehouse = NameOf(loadout.warehouse);
            _nameBarracks = NameOf(loadout.barracks);
            _nameTower = NameOf(loadout.guardTower);
            _nameWall = NameOf(loadout.wall);
            _nameDock = NameOf(loadout.dock);
        }

        private static string NameOf(GameObject prefab)
        {
            var b = prefab != null ? prefab.GetComponent<Building>() : null;
            return b != null ? b.displayName : null;
        }

        private void Update()
        {
            if (_team == null || Time.timeScale == 0f) return; // idle before setup or once the match ends

            _clock += Time.deltaTime;
            _thinkTimer -= Time.deltaTime;
            if (_thinkTimer > 0f) return;
            _thinkTimer = thinkInterval;

            Think();
        }

        private void Think()
        {
            if (_grid == null) _grid = GameGrid.Instance;
            if (_vision == null) _vision = TeamVision.Instance;
            if (_econ == null && TeamManager.Instance != null) _econ = TeamManager.Instance.EconomyFor(_team);
            if (_grid == null || _econ == null) return;

            Refresh();
            ComputeThreat();

            ManageWorkers();
            ManagePopulation();
            ManageEconomy();
            ManageBuildOrder();
            ManageMilitary();
            ManageDefense();
            ManageNaval();
        }

        // --------------------------------------------------------------- sensing

        private void Refresh()
        {
            _villagers.Clear();
            _soldiers.Clear();
            _ships.Clear();
            foreach (Unit u in UnitSelectionManager.AllUnits)
            {
                if (u == null || u.Team != _team) continue;
                if (u.naval) { _ships.Add(u); continue; }
                var combatant = u.GetComponent<Combatant>();
                if (combatant != null) { _soldiers.Add(combatant); continue; }
                var villager = u.GetComponent<Villager>();
                if (villager != null) _villagers.Add(villager);
            }

            _myBuildings.Clear();
            _sites.Clear();
            foreach (Building b in Building.AllBuildings)
            {
                if (b == null || b.Team != _team) continue;
                _myBuildings.Add(b);
                if (!b.IsComplete) _sites.Add(b);
            }

            // Keep a working town center reference (rebuild logic relies on it).
            if (_townCenter == null || !_townCenter.IsComplete) _townCenter = FindComplete(_nameTownCenter);
            if (_townCenter != null) _homeWorld = _townCenter.transform.position;

            // Remember resource nodes we can currently see; forget depleted ones.
            _knownNodes.RemoveWhere(n => n == null || n.IsDepleted);
            foreach (ResourceNode n in ResourceNode.All)
            {
                if (n == null || n.IsDepleted) continue;
                if (_vision == null || _vision.IsVisible(_team, n.transform.position)) _knownNodes.Add(n);
            }

            // Enemies currently in sight + remembered enemy buildings (for marching to a base we've scouted).
            _visibleEnemies.Clear();
            _knownEnemyBuildings.RemoveWhere(b => b == null || !b.IsAlive);
            foreach (Entity e in Entity.All)
            {
                if (e == null || !e.IsAlive || !IsEnemy(e)) continue;
                if (_vision != null && !_vision.IsVisible(_team, e.transform.position)) continue;
                _visibleEnemies.Add(e);
            }
            foreach (Building b in Building.AllBuildings)
            {
                if (b == null || !b.IsAlive || !IsEnemy(b)) continue;
                if (_vision == null || _vision.IsVisible(_team, b.transform.position)) _knownEnemyBuildings.Add(b);
            }
        }

        private void ComputeThreat()
        {
            _threatened = false;
            float r2 = baseDefenseRadius * baseDefenseRadius;
            foreach (Entity e in _visibleEnemies)
            {
                if (e == null) continue;
                foreach (Building b in _myBuildings)
                {
                    if (b == null) continue;
                    if (((Vector2)(e.transform.position - b.transform.position)).sqrMagnitude <= r2)
                    {
                        _threatened = true;
                        _threatPos = e.transform.position;
                        return;
                    }
                }
            }
        }

        // --------------------------------------------------------------- economy

        private void ManageWorkers()
        {
            UpdateScout();

            int activeBuilders = 0;
            foreach (Villager v in _villagers)
                if (v != null && v.IsBuilding) activeBuilders++;
            int wantBuilders = _sites.Count == 0 ? 0 : Mathf.Min(_villagers.Count, maxBuildersPerSite * _sites.Count);

            foreach (Villager v in _villagers)
            {
                if (v == null || v == _scout || v.IsBusy) continue; // skip the scout and anyone already tasked

                if (activeBuilders < wantBuilders)
                {
                    Building site = NearestUnfinished(v.transform.position);
                    if (site != null) { v.BuildAt(site); activeBuilders++; continue; }
                }
                AssignGather(v);
            }
        }

        private void AssignGather(Villager v)
        {
            ResourceType want = MostNeededResource();
            ResourceNode node = NearestKnownNode(v.transform.position, want)
                                ?? NearestKnownNode(v.transform.position, null);
            if (node != null) v.GatherFrom(node);
            // else: nothing of value is known yet — the scout will uncover more.
        }

        // Buildings cost wood, units cost food (+ wood for ships); gold/stone are unused,
        // so the bot only ever chases food and wood, keeping them roughly balanced.
        private ResourceType MostNeededResource() =>
            _econ.Get(ResourceType.Wood) <= _econ.Get(ResourceType.Food) ? ResourceType.Wood : ResourceType.Food;

        private void ManagePopulation()
        {
            int popUsed = UnitSelectionManager.UnitCountForTeam(_team);
            if (popUsed + popBuffer < _econ.Cap) return;        // plenty of headroom
            if (CountOwned(_nameHouse, true) > 0) return;       // a house is already up or building
            TryBuild(_loadout.house, HomeCell(), 2, 10);
        }

        private void ManageEconomy()
        {
            if (_townCenter == null) return;
            var producer = _townCenter.GetComponent<UnitProducer>();
            if (producer == null) return;

            GameObject villager = producer.FirstTrainable;
            if (villager == null) return;
            if (_villagers.Count + producer.QueueCount < targetVillagers)
                producer.Enqueue(villager); // gated by food + population, so it self-paces
        }

        private void ManageBuildOrder()
        {
            // Replace a lost town center so the economy can recover (same as a player rebuilding).
            if (CountOwned(_nameTownCenter, true) == 0)
                TryBuild(_loadout.townCenter, HomeCell(), 0, 12);

            // Barracks: the gateway to an army.
            if (_villagers.Count >= barracksAfterVillagers && CountOwned(_nameBarracks, true) == 0)
                TryBuild(_loadout.barracks, HomeCell(), 3, 10);

            // Expansion: a warehouse next to a resource that is a long walk from any drop-off.
            if (enableExpansion && CountSites(_nameWarehouse) == 0)
            {
                ResourceNode far = FarResource();
                if (far != null) TryBuild(_loadout.warehouse, Cell(far.transform.position), 1, 4);
            }

            // Dock: only once the economy is comfortable and we have a barracks running.
            if (enableNaval && _econ.Get(ResourceType.Wood) >= dockMinWood &&
                CountOwned(_nameBarracks, true) > 0 && CountOwned(_nameDock, true) == 0)
                TryBuild(_loadout.dock, HomeCell(), 2, 14, needsWater: true);
        }

        private void ManageMilitary()
        {
            Building barracks = FindComplete(_nameBarracks);
            var producer = barracks != null ? barracks.GetComponent<UnitProducer>() : null;

            // Soldiers not committed to an attack are "mustering": they hold (and defend)
            // near the base behind a leash — widened to the whole defense zone when threatened.
            _attackers.RemoveAll(c => c == null);
            float leash = _threatened ? baseDefenseRadius : musterLeash;
            int mustering = 0;
            foreach (Combatant c in _soldiers)
            {
                if (c == null || _attackers.Contains(c)) continue;
                c.guardRadius = leash;
                mustering++;
            }

            // Keep the queue topped up toward the next wave (gated by the bot's own economy).
            if (_clock >= startupDelay && producer != null)
            {
                GameObject soldier = producer.FirstTrainable;
                if (soldier != null && mustering + producer.QueueCount < _waveSize)
                    producer.Enqueue(soldier);
            }

            // Commit a wave only when strong enough AND we actually know where an enemy is.
            if (mustering >= _waveSize && HasKnownEnemyTarget())
            {
                foreach (Combatant c in _soldiers)
                {
                    if (c == null || _attackers.Contains(c)) continue;
                    c.guardRadius = 0f; // release the leash so it can march across the map
                    c.StopCombat();
                    _attackers.Add(c);
                }
                _waveSize = Mathf.Min(maxWaveSize, _waveSize + waveSizeIncrement);
                if (verbose) Debug.Log($"[{_team.displayName}] launching a wave of {_attackers.Count}.");
            }

            // Point attackers at the nearest scouted enemy; recall any that have run out of targets.
            for (int i = _attackers.Count - 1; i >= 0; i--)
            {
                Combatant c = _attackers[i];
                if (c == null) { _attackers.RemoveAt(i); continue; }
                if (c.HasTarget) continue;

                Entity target = NearestKnownEnemy(c.transform.position);
                if (target != null) c.AttackTarget(target);
                else { c.guardRadius = leash; _attackers.RemoveAt(i); } // nothing known: back to mustering
            }
        }

        private void ManageDefense()
        {
            if (_threatened)
            {
                // A tower facing the threat, then a short wall barricade between base and enemy.
                if (CountSites(_nameTower) == 0 && CountOwned(_nameTower, false) < proactiveTowers + 2)
                    TryBuild(_loadout.guardTower, CellToward(_threatPos, 4f), 0, 6);

                if (desiredWalls > 0 && CountOwned(_nameWall, true) < desiredWalls && CountSites(_nameWall) < 2)
                    TryBuild(_loadout.wall, CellToward(_threatPos, 3f), 0, 4);
                return;
            }

            // Peacetime: keep a couple of towers up near the base once the economy can spare it.
            if (_villagers.Count >= 6 && CountSites(_nameTower) == 0 && CountOwned(_nameTower, false) < proactiveTowers)
                TryBuild(_loadout.guardTower, HomeCell(), 2, 6);
        }

        private void ManageNaval()
        {
            if (!enableNaval) return;
            Building dock = FindComplete(_nameDock);
            if (dock == null) return;

            var producer = dock.GetComponent<UnitProducer>();
            if (producer != null)
            {
                GameObject ship = producer.FirstTrainable;
                if (ship != null && _ships.Count + producer.QueueCount < targetShips)
                    producer.Enqueue(ship);
            }

            // Ships have no weapon yet — use them to patrol/scout the sea, extending vision.
            foreach (Unit s in _ships)
            {
                if (s == null || s.IsMoving) continue;
                if (TryGetNavalFrontier(s.transform.position, out Vector3 water)) s.MoveTo(water);
            }
        }

        // --------------------------------------------------------------- scouting

        private void UpdateScout()
        {
            if (!enableScouting) return;
            if (_scout == null && _villagers.Count >= minVillagersForScout)
            {
                _scout = FirstIdleVillager();
                if (_scout != null) _scout.StopTasks();
            }
            if (_scout == null) return;

            var unit = _scout.GetComponent<Unit>();
            if (unit == null) { _scout = null; return; }
            if (unit.IsMoving) return; // still travelling to its last frontier

            if (_vision != null && _vision.TryGetFrontier(_team, unit.transform.position, out Vector3 frontier))
            {
                unit.MoveTo(frontier);
                if (!unit.IsMoving) _scout = null; // unreachable frontier: give the villager back to the economy
            }
            else
            {
                _scout = null; // everything reachable is explored
            }
        }

        private Villager FirstIdleVillager()
        {
            foreach (Villager v in _villagers)
                if (v != null && !v.IsBusy) return v;
            return null;
        }

        // --------------------------------------------------------------- queries

        private bool IsEnemy(Entity e) =>
            e != null && TeamManager.Instance != null && TeamManager.Instance.AreEnemies(_team, e.Team);

        private bool HasKnownEnemyTarget() => _visibleEnemies.Count > 0 || _knownEnemyBuildings.Count > 0;

        private Building NearestUnfinished(Vector3 from)
        {
            Building best = null;
            float bestSq = float.MaxValue;
            foreach (Building b in _sites)
            {
                if (b == null) continue;
                float sq = (b.transform.position - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = b; }
            }
            return best;
        }

        private ResourceNode NearestKnownNode(Vector3 from, ResourceType? type)
        {
            ResourceNode best = null;
            float bestSq = float.MaxValue;
            foreach (ResourceNode n in _knownNodes)
            {
                if (n == null || n.IsDepleted) continue;
                if (type.HasValue && n.resourceType != type.Value) continue;
                float sq = (n.transform.position - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = n; }
            }
            return best;
        }

        private ResourceNode FarResource()
        {
            foreach (ResourceNode n in _knownNodes)
            {
                if (n == null || n.IsDepleted) continue;
                ResourceDropoff drop = ResourceDropoff.Nearest(n.transform.position, _team);
                if (drop == null) continue;
                if ((drop.transform.position - n.transform.position).sqrMagnitude > expansionDistance * expansionDistance)
                    return n;
            }
            return null;
        }

        private Entity NearestKnownEnemy(Vector3 from)
        {
            Entity best = null;
            float bestSq = float.MaxValue;
            foreach (Building b in _knownEnemyBuildings)
            {
                if (b == null || !b.IsAlive) continue;
                float sq = (b.transform.position - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = b; }
            }
            foreach (Entity e in _visibleEnemies)
            {
                if (e == null || !e.IsAlive) continue;
                float sq = (e.transform.position - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = e; }
            }
            return best;
        }

        // --------------------------------------------------------------- building

        private bool TryBuild(GameObject prefab, Vector2Int anchor, int minR, int maxR, bool needsWater = false)
        {
            if (prefab == null || _grid == null || _econ == null) return false;
            if (!BuildPlacement.CanAfford(_econ, prefab)) return false;

            Vector2Int fp = BuildPlacement.Footprint(prefab);
            if (!BuildPlacement.TryFindSpot(_grid, anchor, fp, needsWater, minR, maxR, out Vector2Int origin))
                return false;

            Building b = BuildPlacement.PlaceConstructionSite(_grid, prefab, origin, _team, _econ);
            if (b != null && verbose) Debug.Log($"[{_team.displayName}] building {b.displayName} at {origin}.");
            return b != null;
        }

        private int CountOwned(string name, bool includeSites)
        {
            if (name == null) return 0;
            int n = 0;
            foreach (Building b in _myBuildings)
                if (b != null && b.displayName == name && (includeSites || b.IsComplete)) n++;
            return n;
        }

        private int CountSites(string name)
        {
            if (name == null) return 0;
            int n = 0;
            foreach (Building b in _sites)
                if (b != null && b.displayName == name) n++;
            return n;
        }

        private Building FindComplete(string name)
        {
            if (name == null) return null;
            foreach (Building b in _myBuildings)
                if (b != null && b.IsComplete && b.displayName == name) return b;
            return null;
        }

        // --------------------------------------------------------------- geometry

        private Vector2Int HomeCell() => Cell(_homeWorld);

        private Vector2Int Cell(Vector3 world)
        {
            Vector3Int c = _grid.WorldToCell(world);
            return new Vector2Int(c.x, c.y);
        }

        /// <summary>A cell <paramref name="distance"/> world units from home toward a target.</summary>
        private Vector2Int CellToward(Vector3 target, float distance)
        {
            Vector3 dir = target - _homeWorld;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.up;
            dir.Normalize();
            return Cell(_homeWorld + dir * distance);
        }

        /// <summary>Nearest reachable, not-yet-explored open-water cell (a sea scouting target).</summary>
        private bool TryGetNavalFrontier(Vector3 from, out Vector3 world)
        {
            world = from;
            Vector3Int c0 = _grid.WorldToCell(from);
            RectInt cb = _grid.CellBounds;
            int maxR = cb.width + cb.height;
            for (int r = 1; r <= maxR; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                    var cell = new Vector2Int(c0.x + dx, c0.y + dy);
                    if (!cb.Contains(cell)) continue;
                    if (!_grid.IsWalkable(cell, true)) continue; // must be open water
                    Vector3 center = _grid.CellCenter(cell);
                    if (_vision != null && _vision.Get(_team, center) != Visibility.Unseen) continue;
                    world = center;
                    return true;
                }
            }
            return false;
        }
    }
}
