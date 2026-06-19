# Aditus Belli — Session Handoffs

## Handoff — phase7-worldgen [25f782ea-4848-4c89-8b83-d9699275207b] — 2026-06-19 02:10

### ✅ Done & verified (committed on `develop`, confirmed in Play by the user)
- **Phase 7 — real uGUI HUD** (`6c91002`): runtime Canvas built by `HudController` +
  `UiFactory` (`Assets/Scripts/UI/`). Panels: `ResourceBar`, `SelectionPanel` (+HP bar),
  `CommandPanel` (Build/Train/Cancel buttons + Q/C/H/B hotkeys), `SelectionBox`,
  `GameOverOverlay`. EventSystem uses `InputSystemUIInputModule` + `AssignDefaultActions()`.
  `HudController.IsPointerOverUi` guards `UnitSelectionManager`/`BuildingPlacer`. All
  old `OnGUI` deleted.
- **Phase 7 — minimap** (`03db896`): `Assets/Scripts/UI/Minimap.cs`, Texture2D blips +
  camera-viewport rect + click/drag navigate. `GameGrid` got `CellBounds`/`WorldBounds`/
  `WorldToNormalized`/`NormalizedToWorld`; `RtsCameraController.CenterOn`.
- **Phase 7 — fog of war** (`01c211d`): `Assets/Scripts/Map/VisibilityModel.cs` +
  `FogOfWar.cs` (3-state, re-fogs, hides enemy renderers/colliders out of sight; F toggles).
  Minimap is fog-aware.
- **World generator (standalone)** (`4f29a74`, `fa79fb6`): seed-based fractal-Perlin
  terrain; **Pangea = single landmass fully ringed by sea** (radial `islandFalloff`),
  size-relative noise; `MatchLayout` = N city centers (spaced, on Plain/Hill, off edge) +
  scattered resources on Plain/Hill with a per-base fairness minimum; beaches only near
  water (circular `beachWaterRadius`); preview markers; `WorldGen.unity` tuning scene via
  `Aditus Belli ▸ 3. Build World Generator Scene`.
- **Git identity for this repo set** to `gandolphinnn <gandolfiluca03@gmail.com>` (LOCAL
  only; global stays `Luca Gandolfi`). Repo now has a remote `github.com/gandolphinnn/AditusBelli`
  (Git LFS configured). **Always commit here as gandolphinnn.**

### 🚧 In progress / incomplete — UNCOMMITTED & NOT YET Play-verified
A large batch of changes is on disk but **not committed and not yet confirmed compiling/
working in Play** (user ran /handoff before testing). Needs: recompile (Console clean) →
re-run **BOTH** `Aditus Belli ▸ 2. Build Starter Scene` AND `▸ 3. Build World Generator
Scene` → Play-test both → then commit as gandolphinnn.
- **Recipe-in-code architecture**: `Assets/Scripts/Map/Generation/WorldRecipe.cs` —
  `struct WorldRecipe` + static `WorldRecipes` (`Dictionary<WorldType,WorldRecipe>`,
  hardcoded). `WorldRecipes.Pangea` holds the user's tuned values.
- **Generator split into 3** (`Assets/Scripts/Map/Generation/`): `WorldGeneratorBase`
  (abstract, all shared engine + `seed`/`size`/`resources`/`playerCount`), `WorldMapGenerator`
  (GAME: only Seed+Presets incl. `worldType`; recipe from `WorldRecipes`), `WorldGenTuner`
  (WORLDGEN sandbox: every terrain param individually + `Copy recipe as C#` + markers/fit-
  camera; defaults = tuned values).
- **Generation scripts moved** to `Assets/Scripts/Map/Generation/` (TerrainType, WorldMap,
  WorldMapGenerator, MatchLayout, WorldRecipe, WorldGeneratorBase, WorldGenTuner).
  Namespace kept `AditusBelli.Map`.
- **Game scene now procedural**: `GameGrid.BuildModel` derives walkability from the
  generator's terrain; new `Assets/Scripts/Game/MatchSetup.cs` places per-team Town Centers
  + scattered resources + 3 villagers/team + centers camera; `BuildStarterScene` rewritten
  (no more flat map / walls / fixed economy / fixed spawns; Game size=Small, 2 players).
- **DontSave bloat fix**: generator marks its runtime tilemap/markers/textures
  `HideFlags.DontSave` so saving a scene no longer bakes them (an earlier `fa79fb6`
  WorldGen.unity ballooned to ~759k lines).
- **`WORLD_GEN.md` edits** (Pangea-only-for-now note, fairness, copy/paste) — uncommitted.
- **Staged but uncommitted**: `SampleScene.unity` deletion (user deleted it on purpose —
  include in next commit).

### ⏭️ Next steps
1. User recompiles + rebuilds BOTH scenes + Play-tests. Fix any compile/runtime issues.
2. Commit the whole uncommitted batch **as gandolphinnn** (include SampleScene deletion).
3. **Cleanup** (deferred to avoid risk): prune dead methods in `AditusBelliSetup`
   (`BuildWalls/BuildEnemy/BuildEconomy/SpawnUnits/CreateBuilding/CreateResourceNode/
   CreateWallDef/CreateEnemyCampDef/CreateOrLoadDiamondTile` + `MapSize` const) and remove
   the one-off `Aditus Belli ▸ Move Generation Scripts To Generation Folder` menu command.
4. Then gameplay: enemy AI / economy / attack waves (enemy is currently passive — a TC +
   3 villagers, no army → DEFEAT won't trigger); later add world types Archipelago/Continents.

### 🧠 Key context & decisions
- **Tuning workflow**: WorldGen scene = sandbox (`WorldGenTuner`, tune live in Play); when
  happy → right-click component ▸ **Copy recipe as C#** → paste the entry into
  `WorldRecipes.cs`. Game reads recipes from there by `worldType`. No ScriptableObject —
  recipes are hardcoded in C# on purpose (user's choice).
- **C# 9.0 is the max** (Unity 6 / Mono / .NET Standard 2.1) — no file-scoped namespaces,
  global usings, records-required, collection expressions, etc.
- **Moving assets while Unity is open is unreliable** (it reconciles them back) — move via
  `AssetDatabase.MoveAsset` (the Move-Generation-Scripts menu command does this). Same
  reason: change Unity settings/scenes via editor scripts, not by editing files by hand.
- **Generated content must be `HideFlags.DontSave`** so it never bakes into a saved scene.
- **Pangea tuned recipe** (in `WorldRecipes.Pangea` + `WorldGenTuner` defaults): noiseScale
  22, octaves 5, persistence 0.5, lacunarity 1.46, islandFalloff 3.5, levels
  0.134/0.214/0.323/0.582/0.746, beachWaterRadius 11. WorldGen seed -1038437759.
- **Build workflow**: exit Play → recompile → re-run the relevant Build menu command (the
  scene is code-generated; Claude can't drive the editor GUI) → Play.
- **TerrainType**: `IsWalkable` = Beach/Plain/Hill; `IsBuildable` = Plain/Hill only
  (placements stay off the shoreline).
- **Cross-session memory**: `C:\Users\Luca\.claude\projects\D--Personale-AditusBelli\memory\`
  (`MEMORY.md` + `world-map-generator.md`). (An older note used the wrong path `D--\memory\`.)
- **Conventions** (`CLAUDE.md`): code English-only (conversation is Italian); new Input
  System only; isometric sort axis `(0,1,-0.26)`. Docs: `ROADMAP.md`, `GAME_IDEA.md`,
  `WORLD_GEN.md`.

## Handoff — aditus-belli-phases-1-6 [4fa1a8e3-49c8-474a-a02c-91b7972b5d34] — 2026-06-18 01:27

### ✅ Done & verified
Built **Aditus Belli**, an isometric 2D RTS (Age of Empires 2-style), in Unity
`6000.5.0f1` (URP Universal 2D, new Input System). ROADMAP phases 1–6 are
implemented, **verified in Play by the user**, and committed on branch `develop`
(local only, **no remote**). Commits: Phase1 `140e20d`, Phase2 `8eb8773`,
Phase3 `722202d`, Phase4 `e1afb57`, Phase5 `1164305`, Phase6 `4c556e3`.

- **Phase 1 — grid + pathfinding** (`Assets/Scripts/Map/`): `GridModel` (pure
  walkability grid), `Pathfinder` (8-dir A*, no corner cutting), `GameGrid`
  (bridge: world↔cell, `FindPath`), `GridObstacle` (unused now).
- **Phase 2 — economy** (`Assets/Scripts/Economy/`): `ResourceType`,
  `PlayerResources`, `ResourceNode`, `ResourceDropoff`, `Villager` (gather→carry→
  deposit, integer-exact), `ResourceHud`; `Assets/Scripts/UI/SelectionInfoHud`.
- **Phase 3 — buildings** (`Assets/Scripts/Buildings/`): `BuildingDef` (SO),
  `Building` (construction site → complete), `BuildingPlacer` (press **H** = House),
  `PlayerPopulation`.
- **Phase 4 — training**: `UnitDef` (SO), `UnitProducer` (queue, cost, pop gating,
  rally). `ProductionHud`. Hotkeys with a producer selected: **Q** train, **C** cancel.
- **Phase 5 — combat + teams**: `Assets/Scripts/Teams/` (`TeamDef` editable
  name/color/start-resources, `TeamManager`, `Owner` per entity, N factions),
  `Assets/Scripts/Combat/` (`Health`, melee `Combatant`). Soldier trained from a
  buildable **Barracks** (press **B**). Selection rule: **single-click inspects ANY
  entity**; only the player's own **mobile** units are multi-selectable (Shift /
  box-select). Right-click an enemy = attack.
- **Phase 6 (1+2)**: `Assets/Scripts/Game/MatchManager` (conquest VICTORY/DEFEAT
  overlay, freezes match), defensive enemy AI (`Combatant.guardRadius` leash —
  enemies engage/chase near their post then return), **40×40 map**, **gold + stone**
  mines, pathfinding fix (units never straight-line through obstacles), wall
  redesigned (longer, off-axis gap at y=3-4).

### 🚧 In progress / incomplete
- **Gold & stone are gathered but not spent anywhere** (units cost food, buildings
  cost wood). Economic gap to close.
- **Enemy is purely defensive**: no economy, no attack waves. DEFEAT is wired but
  won't trigger in normal play.
- **All HUD is IMGUI (`OnGUI`) placeholder** — no real uGUI, no minimap, no fog of war.

### ⏭️ Next steps
User was choosing the next direction (recommendation was 1 then 2):
1. **Give gold/stone a use** (quick win): e.g. soldier also costs gold, barracks also
   costs stone. (`BuildingDef`/`UnitDef` only have `woodCost`/`foodCost` today — would
   need to add gold/stone cost fields and check them in `BuildingPlacer`/`UnitProducer`.)
2. **Economic AI + attack waves** (Phase 6.3).
3. **Real uGUI HUD + minimap + fog of war** (Phase 7).
4. **More unit/building types** (e.g. ranged archer — `Combatant` is melee-only).

### 🧠 Key context & decisions
- **Repo**: `D:\Personale\AditusBelli`, own git repo, branch `develop`, **no remote**.
  The shell default cwd is `D:\`, so git needs `cd /d/Personale/AditusBelli && …`.
- **Scene is generated from code, not hand-built.** Editor menu in
  `Assets/Editor/AditusBelliSetup.cs`: `Aditus Belli ▸ 1. Configure Project Settings`
  and `Aditus Belli ▸ 2. Build Starter Scene` (builds `Assets/Scenes/Game.unity`,
  procedurally generates sprite PNGs, creates SO assets under `Assets/Data/`).
- **Workflow after a code change**: exit Play (Unity won't recompile during Play) →
  let it recompile (Console clean) → re-run **Build Starter Scene** if components/
  layout changed → Play. Tell the user to do this; Claude can't drive the editor GUI.
- **Conventions** (see `CLAUDE.md` at repo root): code English-only (conversation is
  Italian); change Unity settings via Editor scripts, not by hand-editing
  `ProjectSettings/*.asset` while the editor is open; new Input System only;
  isometric transparency sort axis `(0,1,-0.26)`; buildings train units via
  `BuildingDef.trains`; single-click inspect / own-mobile-only multi-select.
- **Controls**: middle-drag = pan, scroll = zoom (keyboard/edge pan are commented out
  in `RtsCameraController`). Left-click select/inspect; Shift/box multi-select own
  units; right-click = move/gather/build/attack/rally. H=house, B=barracks,
  Q=train, C=cancel.
- **Starting resources** come from the local player's `TeamDef`
  (`Assets/Data/Teams/Player.asset`, editable in Inspector). `TeamManager.localPlayer`
  points to it. Units tinted by team color (white base sprites); buildings keep type colors.
- **Pathfinding**: `Unit.MoveTo` → `GameGrid.FindPath` (A*). With a grid it follows a
  real path or stays put — never cuts through walls. Walls/buildings block their
  footprint cells in `Building.Start` (`BlockFootprint`).
- **Gotchas**: `MatchManager` sets `Time.timeScale = 0` on game-over and resets it to 1
  in `Awake` (timescale persists across editor play sessions otherwise). Destroyed
  units are pruned from selection each frame (`UnitSelectionManager.Update`).
- **Other docs**: `ROADMAP.md` (phased plan + inspirations: AoE2, Mindustry, Polytopia),
  `GAME_IDEA.md` (design ideas: story mode historical periods + skirmish).
- **Future targets**: multiplayer (lockstep) and WebGL build are wanted later;
  architecture kept compatible (sim logic separated from rendering, command-based input).
- There is a cross-session memory note at
  `C:\Users\Luca\.claude\projects\D--\memory\aditus-belli-project.md`.
