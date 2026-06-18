# Aditus Belli — Session Handoffs

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
