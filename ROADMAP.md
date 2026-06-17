# Aditus Belli — Roadmap

Goal: build a simplified **Age of Empires 2**-style real-time strategy game. The
target is a recognizable core loop — *gather → build → train → fight* — with a
small amount of content first (1 civilization, ~4 unit types, ~5 buildings,
2-3 resources), expanding afterward.

Placeholder art is fine until the systems work: **mechanics first, art and
animation later**. Each phase should end with something runnable and testable in
Play mode.

## Architectural foundations

These cut across all phases and are set up as soon as they are first needed:

1. **Logical grid separated from rendering.** AoE2 is tile-based: the map needs a
   logical model (per-tile walkability and occupancy) independent of the visual
   tilemap. Pathfinding, building placement and combat all depend on it.
2. **Data-driven definitions (ScriptableObjects).** Units, buildings and resources
   are described by data assets (costs, HP, stats), not hardcoded, so adding
   content is configuration rather than code.

## Out of scope (for now)

- Multiplayer / lockstep determinism (would shape the architecture, not worth the
  complexity for single-player yet).
- Full civilization roster and complete tech tree.
- Final art, animation and audio.

## Phases

- **Phase 0 — Prototype base (done).** Isometric map, RTS camera, unit selection
  (single / box / shift), basic movement.

- **Phase 1 — Logical grid + pathfinding.** `GridModel` (walkability/occupancy),
  world↔cell conversion, grid **A\*** pathfinding; units route around obstacles.
  The backbone everything else builds on.

- **Phase 2 — Economy: resources + gatherers.** Resource types (start with Wood +
  Food, then Gold/Stone), resource nodes (trees, bushes/animals), a Villager that
  gathers over time, carries a load, returns to a drop-off building and credits the
  player's resources. Resource HUD bar.

- **Phase 3 — Buildings + construction.** Grid placement (footprint + validity),
  villager-driven construction (build progress), Town Center, House (population
  cap), drop-off buildings. Population system (current/cap).

- **Phase 4 — Unit production.** Training from buildings with resource costs, a
  production queue, rally points, population gating.

- **Phase 5 — Combat.** HP, melee and ranged attacks (projectiles), automatic
  target acquisition, attack-move, death; destructible buildings. Basic stats
  (attack / armor / range / rate of fire).

- **Phase 6 — Opponent + win condition.** A second player and a simple AI (at least
  an enemy base to attack). Conquest victory. This is when it becomes *a game*.

- **Phase 7 — Fog of war, minimap, UI.** Exploration and line of sight, a minimap,
  a usable command card and selection panel.

- **Phase 8 — Ages + tech tree.** Age advancement (Dark → Feudal → Castle →
  Imperial) unlocking buildings/units, plus a few upgrades. Where it starts to feel
  like AoE2.

- **Phase 9 — Content + polish.** More units/buildings, 1-2 civilizations with
  bonuses, animation, audio, save/load.

## Priorities

Phases 1→5 are the core gameplay loop and come first. Phase 6 turns it into a game.
Phases 7→9 add depth and AoE2 identity.
