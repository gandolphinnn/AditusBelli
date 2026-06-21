---
description: Reflect manual Unity Inspector edits (prefabs/assets/scene) back into the AditusBelliSetup.cs generator so re-running Build Starter Scene reproduces them.
argument-hint: "[optional asset path to limit the sync, e.g. Assets/Prefabs/Buildings/Dock.prefab]"
allowed-tools: Bash(git status:*), Bash(git diff:*), Glob, Grep, Read, Edit
---

# Sync AditusBelliSetup.cs from Unity

`Assets/Editor/AditusBelliSetup.cs` is the **source of truth**: its menu commands
regenerate every prefab, ScriptableObject, sprite and scene from hardcoded literals.
When the user tweaks a value directly in the Unity **Inspector**, that edit is saved
into the asset's serialized YAML (`.prefab` / `.asset` / `.unity`) but **not** into the
generator. The next `Aditus Belli ▸ 2. Build Starter Scene` would then overwrite the
manual tweak with the stale literal.

**Your job:** read the changed Unity asset files, extract their current serialized
values, and update the matching literals in `AditusBelliSetup.cs` so the generator
reproduces the current state. You are back-porting Inspector edits into the generator.

This is value reconciliation only — **do not run Unity, do not commit**, and do not
restructure the generator. Only change literal values that have a clear home.

## Procedure

1. **Find what changed.** Run `git status --short` and `git diff` over the Unity asset
   trees: `Assets/Prefabs/**`, `Assets/Data/**`, `Assets/Scenes/Game.unity`,
   `Assets/Art/Generated/**/*.meta`. If `$ARGUMENTS` names a path, limit to it.
   The changed files are your work list. (If the tree is clean, say so and stop —
   there is nothing to sync.)
2. **Read each changed asset's YAML** and extract the serialized values of the fields
   listed in the mapping below. Unity serializes public fields by their C# name;
   bools are `0/1`, vectors are `{x: , y: }`, colors are `{r: , g: , b: , a: }`.
   Inherited base-class fields (from `Entity`) appear inside the derived component's
   block (e.g. `team`, `applyTeamColor`, `maxHealth` live in the `Unit`/`Building`
   block, since `Unit`/`Building` inherit `Entity`).
3. **Update the matching literal** in `AditusBelliSetup.cs` with `Edit`. Change only
   the value; keep names, comments and structure intact.
4. **Report** a table of every synced change (`file → field: old → new`) and a
   separate list of drift you could **not** sync (see "Cannot be synced").

## Where each value lives in the generator

Map each changed asset to the function/call that builds it, then to the literal.

| Asset file | Generator location | Tunable literals |
|---|---|---|
| `Prefabs/Units/Unit.prefab` (Villager) | `BuildUnitPrefab` body | `unit.maxHealth`, collider `radius`, `stats.{displayName,foodCost,woodCost,trainTime,populationCost}`, `unit.moveSpeed`/`naval` (on the `Unit` block) |
| `Prefabs/Units/Soldier.prefab` | `BuildSoldierPrefab` body | `unit.maxHealth`, collider `radius`, `stats.*`, `Combatant.{attackDamage,attackRange,attackCooldown,aggroRange,mobile,guardRadius}` |
| `Prefabs/Units/Ship.prefab` | `BuildShipPrefab` body | `unit.maxHealth`, `unit.naval`, collider `radius`, `stats.*` |
| `Prefabs/Buildings/*.prefab` | the `BuildBuildingPrefab("<Name>", …)` **call** in `BuildStarterScene` | call args: `footprint`, `woodCost`, `buildTime`, `population`, `dropoff`, `maxHealth`, `requiresAdjacentWater`, `turretRange`/`turretDamage`/`turretCooldown` (GuardTower); plus `displayName` (2nd arg). |
| `Data/Teams/Player.asset`, `Enemy.asset` | the `CreateTeamDef("<Name>", …)` call | args: `color`, `startFood`, `startWood`, `startGold`, `startStone`, `basePopulation` |
| `Art/Generated/*.png.meta` | the `CreateOrLoadSprite(… ppu: N)` call | `ppu` (from `spritePixelsPerUnit` in the .meta) |
| `Scenes/Game.unity` | `BuildStarterScene` literals | camera `orthographicSize`, `RtsCameraController.{minOrthoSize,maxOrthoSize}`, `WorldMapGenerator.{worldType,size,resources,playerCount,seed}`, `TeamManager.localPlayer`, `BuildingPlacer.buildable` (order/contents), `MatchSetup.{villagersPerTeam,woodAmount,foodAmount,goldAmount,stoneAmount}` |
| `Scenes/WorldGen.unity` | `BuildWorldGeneratorScene` literals | the `WorldGenTuner` fields (seed/size/resources/playerCount/markers/camera) — note: terrain recipe values belong in `WorldRecipes.cs`, not here. |

Field-name notes:
- In a building prefab, `woodCost`/`maxHealth`/`displayName` are in the **`Building`**
  block. In a unit prefab they are split: `maxHealth` is in the **`Unit`** block
  (from `Entity`), while `displayName`/`foodCost`/`woodCost`/`trainTime`/
  `populationCost` are in the **`UnitStats`** block.
- `isDropoff`/`requiresAdjacentWater`/`naval`/`mobile`/`applyTeamColor` are bools → `0/1`.
- `footprint` and `originCell` are `{x, y}`. `originCell`/`startCompleted` are runtime
  placement state, **not** generator literals — ignore them.

## Cannot be synced (flag these for manual handling, don't guess)

- **Structural changes**: a component added/removed in the Inspector, a new child
  GameObject, a brand-new prefab, or a reordered hierarchy. The generator code must be
  edited by hand — report exactly what you saw.
- **Reference fields**: `team`, `trains`, `selectionIndicator` (object refs by GUID).
  Report if a `trains` list changed, but don't rewrite the refs.
- **Sprite/texture colors**: `MakeDiamondTexture`/`MakeCircleTexture` colors are baked
  into the PNG; you can't read a color back out of a PNG. Only `ppu` (from the .meta)
  is syncable. Flag suspected color edits.

## Output

End with two sections:
- **Synced** — a table `asset → field: old → new` for every literal you changed.
- **Needs manual attention** — unsyncable drift, with the file and what changed.

Then remind the user this only touched `AditusBelliSetup.cs`; nothing was committed and
Unity was not run. After their next compile + `Build Starter Scene`, regenerating will
reproduce the synced values.
