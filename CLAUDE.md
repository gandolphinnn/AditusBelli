# Aditus Belli

Isometric 2D real-time strategy / war-simulator game. Period-agnostic: the focus is
on the *systems* of warfare — espionage, resource management, and different approaches
to conflict — rather than a specific historical setting.

## Tech stack

- **Engine:** Unity `6000.5.0f1` (Unity 6.1).
- **Render pipeline:** URP 17.5, **Universal 2D** (2D Renderer; 2D lights, SRP Batcher).
- **View:** isometric 2D, orthographic camera. Tilemaps via `com.unity.2d.tilemap.extras`.
- **Input:** new Input System ONLY (`activeInputHandler: 1`). Use `UnityEngine.InputSystem`
  (`Mouse.current`, `Keyboard.current`, …). Never use the legacy `Input.*` API.
- **Color space:** Linear.

## Project layout

```
Assets/
  Editor/        Editor-only tools (AditusBelliSetup.cs: settings + scene builder)
  Scripts/
    Camera/      RtsCameraController
    Units/       Unit, UnitSelectionManager
  Art/Generated/ Procedurally generated sprites/tiles (PNG + Tile assets)
  Prefabs/       Unit.prefab
  Scenes/        Game.unity (the playable scene)
  Settings/      URP assets (from the template)
```

All C# lives under the `AditusBelli.*` namespace
(`AditusBelli.CameraControl`, `AditusBelli.Units`, `AditusBelli.EditorTools`).

## Rules / conventions

1. **Code is English-only.** All identifiers, comments, XML docs, and log messages
   must be written in English, ignore the language of the conversation.
2. **Change Unity settings via Editor scripts, not by hand-editing files.** While the
   editor is open, configure project settings through code (menu items using
   `UnityEditor` APIs), not by editing `ProjectSettings/*.asset` directly — Unity caches
   settings in memory and can overwrite external edits on quit/focus. Runtime scripts
   under `Assets/` are safe to add externally (Unity compiles them on focus).
3. **New Input System only** — see Tech stack above.
4. **Isometric sprite sorting.** Transparency sort axis is `(0, 1, -0.26)` (Custom Axis).
   Sprites must sit above the ground via `sortingOrder`: ground tilemap = 0,
   selection ring = 1, unit body = 2. Within the same order, the sort axis orders by Y.
5. **Building tilemaps from script.** After `SetTiles`, mark the tilemap and scene dirty
   (`EditorUtility.SetDirty` + `EditorSceneManager.MarkSceneDirty`) before `SaveScene`,
   otherwise the tiles are not flushed and the saved tilemap is empty.
6. **Selection vs inspection.** Every entity (any unit, building, resource node) is
   single-click *inspectable* (its info shows in the HUD). Only the player's own *mobile*
   units can be *multi-selected* (Shift+click and box-select) and commanded.

## Setup / how to run

The project is built from code via the **`Aditus Belli`** editor menu:

1. `Aditus Belli ▸ Setup ▸ 1. Configure Project Settings` — isometric sort axis, tags, layers.
2. `Aditus Belli ▸ Setup ▸ 2. Build Starter Scene` — generates tiles/sprites, the
   `Unit` prefab, and the playable `Assets/Scenes/Game.unity`, then registers it in
   Build Settings.

Then open `Game.unity` and press **Play**.

## Controls (current prototype)

- **Camera:** WASD / arrow keys or screen-edge to pan; middle-mouse drag to pan; scroll to zoom.
- **Selection:** left click to select; Shift+click to add/remove; left-drag for box selection;
  click empty ground to clear.
- **Command:** right click to move the selected units (they spread into a formation).

## Roadmap (next steps)

- Pathfinding (2D NavMesh or grid A*) so units avoid obstacles instead of moving straight.
- Obstacles / non-walkable tiles.
- Resources and gathering.
- Factions, fog of war, espionage systems.
