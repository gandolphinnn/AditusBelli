using System.Collections.Generic;
using AditusBelli.Buildings;
using AditusBelli.Combat;
using AditusBelli.Economy;
using AditusBelli.Teams;
using AditusBelli.Units;
using UnityEngine;

namespace AditusBelli.Game
{
    /// <summary>
    /// Drives one enemy faction. It keeps its villagers gathering food into its own
    /// <see cref="TeamEconomy"/>, trains soldiers from its barracks through the normal
    /// <see cref="UnitProducer"/> (so it is paced by food and population just like the
    /// player), musters them near its base, and launches escalating attack waves at
    /// the player's buildings. Killing its villagers starves its economy; destroying
    /// its barracks stops production. The brain "thinks" on a fixed interval.
    /// </summary>
    public class EnemyAI : MonoBehaviour
    {
        [Tooltip("Seconds between AI decisions.")]
        public float thinkInterval = 1f;
        [Tooltip("Grace period before the AI starts training its army.")]
        public float startupDelay = 20f;
        [Tooltip("Soldiers mustered before the first wave is launched.")]
        public int firstWaveSize = 3;
        [Tooltip("Extra soldiers required by each subsequent wave.")]
        public int waveSizeIncrement = 2;
        [Tooltip("Largest a wave can grow to.")]
        public int maxWaveSize = 12;
        [Tooltip("Leash radius for soldiers still mustering (they defend within it).")]
        public float musterLeash = 9f;

        private TeamDef _team;
        private Building _barracks;
        private UnitProducer _producer;

        private float _thinkTimer;
        private float _clock;
        private int _waveSize;

        private readonly List<Combatant> _attackers = new();
        private readonly List<Combatant> _soldiers = new();  // scratch, rebuilt each think
        private readonly List<Villager> _villagers = new();   // scratch, rebuilt each think

        public void Init(TeamDef team, Building barracks)
        {
            _team = team;
            _barracks = barracks;
            _waveSize = firstWaveSize;
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
            _attackers.RemoveAll(c => c == null);
            CollectUnits();
            KeepVillagersGathering();

            // Soldiers not yet committed to an attack are "mustering": they hold near
            // the base behind a leash so they also defend it.
            int mustering = 0;
            foreach (Combatant c in _soldiers)
            {
                if (c == null || _attackers.Contains(c)) continue;
                c.guardRadius = musterLeash;
                mustering++;
            }

            if (_clock >= startupDelay) TrainSoldiers(mustering);
            if (mustering >= _waveSize) LaunchWave();
            RetargetAttackers();
        }

        private void CollectUnits()
        {
            _soldiers.Clear();
            _villagers.Clear();
            foreach (Unit u in UnitSelectionManager.AllUnits)
            {
                if (u == null) continue;
                var owner = u.GetComponent<Owner>();
                if (owner == null || owner.Team != _team) continue;

                var combatant = u.GetComponent<Combatant>();
                if (combatant != null) { _soldiers.Add(combatant); continue; }
                var villager = u.GetComponent<Villager>();
                if (villager != null) _villagers.Add(villager);
            }
        }

        private void KeepVillagersGathering()
        {
            foreach (Villager v in _villagers)
            {
                if (v == null || v.IsBusy) continue;
                // Soldiers cost food, so steer idle villagers onto food first.
                ResourceNode node = NearestNode(v.transform.position, ResourceType.Food)
                                    ?? NearestNode(v.transform.position, null);
                if (node != null) v.GatherFrom(node);
            }
        }

        private void TrainSoldiers(int mustering)
        {
            if (_producer == null && _barracks != null) _producer = _barracks.GetComponent<UnitProducer>();
            if (_producer == null) return; // barracks destroyed or not complete yet

            GameObject soldier = _producer.FirstTrainable;
            if (soldier == null) return;

            // Keep the queue topped up toward the next wave. Enqueue gates on the enemy
            // economy (food + population), so it paces itself and is a no-op when broke.
            if (mustering + _producer.QueueCount < _waveSize)
                _producer.Enqueue(soldier);
        }

        private void LaunchWave()
        {
            foreach (Combatant c in _soldiers)
            {
                if (c == null || _attackers.Contains(c)) continue;
                c.guardRadius = 0f; // release the leash so it can march across the map
                c.StopCombat();     // drop any local target; RetargetAttackers assigns the base
                _attackers.Add(c);
            }
            _waveSize = Mathf.Min(maxWaveSize, _waveSize + waveSizeIncrement);
        }

        private void RetargetAttackers()
        {
            foreach (Combatant c in _attackers)
            {
                if (c == null || c.HasTarget) continue;
                Health target = NearestHostileBuilding(c.transform.position)
                                ?? NearestHostileUnit(c.transform.position);
                if (target != null) c.AttackTarget(target);
            }
        }

        // --- queries ---

        private static ResourceNode NearestNode(Vector3 from, ResourceType? type)
        {
            ResourceNode best = null;
            float bestSq = float.MaxValue;
            foreach (ResourceNode n in ResourceNode.All)
            {
                if (n == null || n.IsDepleted) continue;
                if (type.HasValue && n.resourceType != type.Value) continue;
                float sq = (n.transform.position - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = n; }
            }
            return best;
        }

        private Health NearestHostileBuilding(Vector3 from)
        {
            Health best = null;
            float bestSq = float.MaxValue;
            foreach (Building b in Building.All)
            {
                if (b == null) continue;
                var owner = b.GetComponent<Owner>();
                if (owner == null || owner.Team == null || owner.Team == _team) continue; // neutral or own
                var h = b.GetComponent<Health>();
                if (h == null || !h.IsAlive) continue;
                float sq = (b.transform.position - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = h; }
            }
            return best;
        }

        private Health NearestHostileUnit(Vector3 from)
        {
            Health best = null;
            float bestSq = float.MaxValue;
            foreach (Unit u in UnitSelectionManager.AllUnits)
            {
                if (u == null) continue;
                var owner = u.GetComponent<Owner>();
                if (owner == null || owner.Team == null || owner.Team == _team) continue;
                var h = u.GetComponent<Health>();
                if (h == null || !h.IsAlive) continue;
                float sq = (u.transform.position - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = h; }
            }
            return best;
        }
    }
}
