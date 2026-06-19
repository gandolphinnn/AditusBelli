# Aditus Belli — Architecture

A file-by-file map of the codebase. For the tech stack, conventions and how to
build/run, see [`CLAUDE.md`](CLAUDE.md); for the phased plan, see
[`ROADMAP.md`](ROADMAP.md).

## Guiding principles

- **Logic separated from rendering.** The pure "model" classes (`GridModel`,
  `Pathfinder`, `WorldMap`, `MatchLayout`, `VisibilityModel`, `TeamEconomy`)
  have no Unity scene dependency, so they can be reasoned about (and tested) in
  isolation. The `MonoBehaviour`s are the bridge to the scene.
- **The scene is generated from code.** `Assets/Editor/AditusBelliSetup.cs`
  builds `Game.unity` (and `WorldGen.unity`), generates the placeholder art and
  the `ScriptableObject` data. After a structural change, re-run the editor menu
  rather than hand-editing the scene.
- **Data-driven definitions.** Units, buildings and teams are `ScriptableObject`
  assets under `Assets/Data/`, not hardcoded.
- **Registries via static lists / scene singletons.** `Unit.AllUnits`,
  `Building.All`, `Health.All`, `ResourceNode.All`, `ResourceDropoff.All`, and
  `Instance` singletons (`TeamManager`, `GameGrid`, `FogOfWar`, …) are how
  systems find each other.
- **Per-team economy.** Resources and population are tracked per faction by
  `TeamEconomy`, owned by `TeamManager`. The player and every AI use the same
  systems (`UnitProducer`, `Villager` deposits) against their own economy.

All code lives under the `AditusBelli.*` namespace. C# 9.0 is the maximum
language version (Unity 6 / Mono / .NET Standard 2.1).

---

## Documentation (root)

| File | Purpose |
|------|---------|
| `CLAUDE.md` | Stack, project layout, conventions, how to build/run. |
| `ROADMAP.md` | Phased plan (0→9) toward an AoE2-style RTS; inspirations and priorities. |
| `GAME_IDEA.md` | Game design ideas (historical story mode + skirmish). |
| `WORLD_GEN.md` | The procedural world generator and its tuning workflow. |
| `HANDOFF.md` | Session-to-session handoff log (state, next steps, decisions). |
| `ARCHITECTURE.md` | This file. |

## Editor (`Assets/Editor/`)

- **`AditusBelliSetup.cs`** — the project "builder". `Aditus Belli ▸` menu items
  configure project settings (isometric sort axis, tags, layers), procedurally
  generate sprite/tile PNGs, create the `ScriptableObject` assets (teams, units,
  buildings) and prefabs, and build the `Game.unity` and `WorldGen.unity`
  scenes. *Still contains dead helpers (`BuildWalls/BuildEnemy/BuildEconomy/
  SpawnUnits/…`) pending cleanup.*

## Map — logical grid, pathfinding, fog (`Assets/Scripts/Map/`)

- **`GridModel.cs`** — pure logical grid: per-cell walkability in tilemap
  coordinates (origin-offset). No Unity dependency.
- **`Pathfinder.cs`** — static 8-directional A* over `GridModel`, octile costs,
  **no corner cutting** through blocked tiles.
- **`GameGrid.cs`** — bridge between Unity's isometric `Grid` and `GridModel`:
  world↔cell conversion, builds walkability from the generated terrain (or the
  tilemap on flat prototypes), exposes world-space `FindPath`, and the bounds
  used by the minimap / fog.
- **`GridObstacle.cs`** — marks its footprint non-walkable at startup (precursor
  to building footprint blocking; little used now).
- **`VisibilityModel.cs`** — pure logical visibility grid (`Visibility` enum:
  Unseen/Explored/Visible), mirroring `GridModel`'s layout.
- **`FogOfWar.cs`** — 3-state fog of war: maintains the `VisibilityModel`
  revealed by local units/buildings, renders it as a tilemap overlay, hides
  out-of-sight enemies, re-fogs left areas. **F** toggles. Also exposes
  `IsWithinUnitSight` (used by villagers to gate auto-picking the next resource).

## Map / Generation — procedural world (`Assets/Scripts/Map/Generation/`)

- **`TerrainType.cs`** — elevation bands (DeepSea→Mountain) + `IsWalkable`
  (Beach/Plain/Hill) and `IsBuildable` (Plain/Hill) extensions.
- **`WorldMap.cs`** — **pure** terrain generator: from a seed, a grid of
  `TerrainType` via fractal Perlin (fBm), per-world-type shaping (Pangea = a
  single landmass ringed by sea), band classification, inland-beach removal.
- **`MatchLayout.cs`** — deterministic match placement: one city center per
  player (spaced, on buildable land, off the edge) + scattered resource nodes
  with a **per-base guaranteed minimum** (fairness). Output in centered coords.
- **`WorldRecipe.cs`** — `struct WorldRecipe` (terrain knobs) + the hardcoded
  `WorldRecipes` per world type (Pangea is tuned). The game reads recipes here.
- **`WorldGeneratorBase.cs`** — shared generation engine (abstract
  `MonoBehaviour`): per-match inputs (seed/size/resources/players), generates
  terrain + layout, paints the tilemap, preview markers, fits the camera; marks
  runtime content `HideFlags.DontSave` so it never bakes into a saved scene.
- **`WorldMapGenerator.cs`** — **game-side** generator: high-level presets only
  (seed + worldType/size/resources/players); terrain recipe comes from
  `WorldRecipes`. This is the one on the `Game` scene's grid.
- **`WorldGenTuner.cs`** — **sandbox** generator (WorldGen scene): exposes every
  terrain parameter for live tuning, with **"Copy recipe as C#"** to paste the
  result into `WorldRecipes`.

## Teams (`Assets/Scripts/Teams/`)

- **`TeamDef.cs`** — `ScriptableObject` faction data: name, color, starting
  resources, `basePopulation`.
- **`TeamManager.cs`** — the teams in play + the local player, and **owns the
  per-team economies** (`EconomyFor(team)` / `LocalEconomy`, created lazily and
  seeded from the `TeamDef`).
- **`Owner.cs`** — an entity's faction ownership (`IsHostileTo`), with optional
  team-color tinting.

## Economy (`Assets/Scripts/Economy/`)

- **`ResourceType.cs`** — resource enum (Food, Wood, Gold, Stone).
- **`TeamEconomy.cs`** — pure economic state of one team: resource stockpile +
  population cap (base + building-provided). Single source of truth, owned by
  `TeamManager`.
- **`ResourceNode.cs`** — a harvestable source (tree/mine): blocks its cell,
  depletes via `Extract`, destroys itself when empty.
- **`ResourceDropoff.cs`** — a building where gatherers deposit; `Nearest` is
  **team-aware** (gatherers only deposit at their own team's buildings).
- **`Villager.cs`** — villager behaviour: gather → carry → deposit → repeat, and
  construction. Deposits into its **own** team's economy, only auto-continues to
  the next node if it is within unit sight of where it was working, exposes
  `IsBusy`.

## Units (`Assets/Scripts/Units/`)

- **`UnitDef.cs`** — `ScriptableObject` for a trainable unit (cost, train time,
  population cost, prefab).
- **`Unit.cs`** — selectable/movable unit: follows an A* path from `GameGrid`
  (never straight-lines through obstacles); static `AllUnits` registry.
- **`UnitSelectionManager.cs`** — RTS selection and commands (new Input System):
  click = inspect, Shift/box = multi-select (own mobile units only), right-click
  = move / gather / build / attack / rally. Counts units per team.

## Buildings (`Assets/Scripts/Buildings/`)

- **`BuildingDef.cs`** — `ScriptableObject` for a building type (footprint,
  cost, build time, population provided, drop-off, HP, what it trains).
- **`Building.cs`** — a placed building: construction site → complete, blocks
  its footprint, and on completion becomes a drop-off / adds population cap (to
  the **owner's** economy) / adds a `UnitProducer`.
- **`UnitProducer.cs`** — unit production: a queue gated by cost and population
  of the **owning team's** economy (so the enemy trains from its own resources),
  spawns on a free adjacent cell, optional rally point.
- **`BuildingPlacer.cs`** — the player's building placement: **H** = House,
  **B** = Barracks; a ghost validates footprint + cost against the local
  economy; left-click places a construction site.

## Combat (`Assets/Scripts/Combat/`)

- **`Health.cs`** — hit points for units/buildings, static `All` registry,
  destroys the object at 0 HP.
- **`Combatant.cs`** — melee combat: approach and attack on cooldown,
  auto-acquire nearby enemies, `guardRadius` leash, `mobile` flag. Exposes
  `HasTarget` (used by the AI).

## Game (`Assets/Scripts/Game/`)

- **`MatchManager.cs`** — match outcome (conquest): VICTORY when no enemy
  buildings remain, DEFEAT when the player has none; shows an overlay and freezes
  the match (`timeScale = 0`).
- **`MatchSetup.cs`** — runtime setup on the generated map: one Town Center +
  villagers per team, scattered resources, centered camera; and for each enemy
  team a **Barracks + an `EnemyAI`** (via a generic `CreateBuilding` helper and a
  buildable-spot search near the base).
- **`EnemyAI.cs`** — the enemy brain: keeps villagers gathering food, trains
  soldiers from its barracks (through the normal `UnitProducer`), musters them at
  base, and launches **escalating attack waves** at the player's buildings. This
  is what makes DEFEAT reachable.

## UI — runtime uGUI HUD (`Assets/Scripts/UI/`)

- **`HudController.cs`** — HUD root: builds the Canvas + EventSystem
  (`InputSystemUIInputModule`) and attaches the panels; exposes `IsPointerOverUi`
  so gameplay input doesn't act through the HUD.
- **`UiFactory.cs`** — helpers to build uGUI elements from code (panels, labels,
  buttons, bars, anchoring); uses the built-in font.
- **`ResourceBar.cs`** — top bar with the local player's resources + population
  (reads `TeamManager.LocalEconomy`).
- **`SelectionPanel.cs`** — bottom-left panel describing the
  selection/inspection (unit, building, resource) + an HP bar.
- **`CommandPanel.cs`** — bottom-right command panel: Build/Train/Cancel buttons,
  production queue read-out, hint; Q/C hotkeys.
- **`SelectionBox.cs`** — draws the left-drag box-selection rectangle.
- **`GameOverOverlay.cs`** — full-screen VICTORY/DEFEAT overlay driven by
  `MatchManager`.
- **`Minimap.cs`** — top-right minimap: unit/building/resource blips into a
  `Texture2D`, the camera viewport rect, click/drag to re-center the camera;
  fog-aware.

## Camera (`Assets/Scripts/Camera/`)

- **`RtsCameraController.cs`** — orthographic RTS camera: middle-mouse-drag pan,
  scroll-wheel zoom, `CenterOn` for the minimap (keyboard/edge panning is
  commented out, re-enableable).

## Generated assets / data

- **`Assets/Scenes/Game.unity`** — the playable scene (regenerated by
  `▸ 2. Build Starter Scene`); **`WorldGen.unity`** — the generator tuning
  sandbox (`▸ 3`).
- **`Assets/Prefabs/Unit.prefab`** (villager) and **`Soldier.prefab`** — built
  from code.
- **`Assets/Data/`** — the `ScriptableObject`s: Teams (Player, Enemy), Units
  (Villager, Soldier), Buildings (TownCenter, House, Barracks; plus legacy
  `Wall`/`EnemyCamp` no longer referenced by the procedural flow).
- **`Assets/Art/Generated/`** — procedurally generated PNGs and Tiles (unit
  bodies, buildings, resources, selection ring, iso ground tiles).

## Known cleanup debt

Carried over and deferred (not bugs): the editor still has dead methods
(`BuildWalls/BuildEnemy/BuildEconomy/SpawnUnits/…`), and the `Wall.asset` /
`EnemyCamp.asset` data assets are no longer used by the procedural match flow.
