# Aditus Belli — World Generation

Design notes for the procedural world generator, written **from how it will be used**.
This is a draft meant to be edited — add details, change numbers, answer the open
questions. Values marked _(proposed)_ are my suggestions, not final.

---

## 1. Purpose

The generator builds the map a match is played on: **terrain**, **starting positions**
(one city center per player) and **resources**. It must be:

- **Deterministic** — the same inputs always produce the exact same world.
- **Tunable from a few high-level options** — the player picks a handful of choices, not
  raw noise parameters.
- **Fair & playable** — every player gets a viable start.

This document describes the generator's **inputs and behavior**, not its math (noise
details are summarized at the end).

---

## 2. How it will be used

At match setup the player makes a few choices; the generator builds the world from those
choices plus a seed; the match starts. Three ways to drive it:

- **Preset mode (default):** choose **Size**, **Type**, **Resources**, **Players**. A
  random **Seed** is rolled automatically.
- **Seed mode:** type a specific **Seed** (together with the same options) to reproduce an
  exact world — for sharing maps or comparing strategies.
- **Advanced mode (tuning only, in the WorldGen scene):** expose the raw knobs (noise
  scale, octaves, thresholds, falloff…) to design and calibrate the presets above.

For now we only build and tune this in the **WorldGen** scene. Wiring it into the actual
game (Game.unity) comes later.

---

## 3. Inputs

### 3.1 Seed
An integer. `same seed + same options ⇒ identical world` (terrain, resource scatter, start
positions). A "Randomize" button rolls a new seed.

### 3.2 Size — how big the map is (in tiles)
| Size   | Tiles     |
|--------|-----------|
| Small  | 150 × 150 |
| Medium | 275 × 275 |
| Large  | 400 × 400 |

### 3.3 Type — how land is distributed
| Type         | Meaning                          | Feel                          |
|--------------|----------------------------------|-------------------------------|
| Archipelago  | many small / medium islands      | naval, expansion, lots of water |
| Continents   | few but very large islands       | classic, some sea between     |
| Pangea       | a single huge island             | land war, water only at edges |

> **Scope:** only **Pangea** is implemented for now (simplest to build). **Archipelago**,
> **Continents** — and possibly other types not yet thought of — are planned for later;
> the type system is built so they can be added without reworking it.

### 3.4 Resources — how much is on the map
Gatherable nodes (wood / food / gold / stone). **Density-based** so bigger maps get
proportionally more.
| Resources | Amount   |
|-----------|----------|
| Scarce    | low      |
| Normal    | medium   |
| Abundant  | high     |

### 3.5 Players — how many starts to place
Total players = **1 human + (N − 1) AI**, **capped at 4** (1 human + up to 3 AI); the
human is always **slot 0**. Range 1–4 (1 allowed for solo testing). The generator places
**one city center per player**, spread apart on land. AI behavior is out of scope here —
the generator only creates their city-center position. Starting **villagers are not placed
by the generator**: they spawn at game start on free tiles next to each city center.

---

## 4. How presets map to the generator _(proposed)_

Presets are "recipes" over the low-level knobs the generator already has.

**Type → land shape**
| Type        | Noise scale       | Edge falloff   | Sea level | Result                |
|-------------|-------------------|----------------|-----------|-----------------------|
| Archipelago | small (high freq) | none / uniform | high      | many islands          |
| Continents  | medium            | mild           | medium    | a few big landmasses  |
| Pangea      | large (low freq)  | strong radial  | low–med   | one large central isle |

**Resources → node count**
`node count = density × (number of land tiles)`, then split across the four types.
| Resources | Density |
|-----------|---------|
| Scarce    | 0.5     |
| Normal    | 1.0     |
| Abundant  | 1.8     |

**Size → tile dimensions** — see the table in §3.2.

---

## 5. Placement rules

- **Sea border (Pangea):** Pangea is a single landmass **fully ringed by sea** — a radial
  falloff sinks the map edges to deep sea, so the island is completely surrounded by water.
  (Future types like Archipelago / Continents may instead let land run off the map edge.)
- **City centers:** one per player, placed on a clear land footprint, **as far apart as
  possible** (minimum spacing scales with map size), kept ~2 tiles away from the map edge.
  Each start has enough nearby walkable land.
- **Resources:** scattered on land, kept off city-center footprints and spaced apart. Each
  city center is **guaranteed a minimum nearby** (within ~12 tiles), scaled by the
  Resources setting — _Normal_ = 2 wood / 2 food / 1 gold / 1 stone; _Scarce_ ≈ ½;
  _Abundant_ ≈ 1.5×. Equal ratio between the four types otherwise.
- **Starting villagers:** spawned at **game start** (not by the generator) on free tiles
  next to each city center.

---

## 6. The WorldGen scene (tuning tool)

`WorldGen.unity` renders the terrain plus **markers** for city centers (per-player color)
and resources (per-type color), so a preset can be checked for look, fairness and
playability before it's used in a real match. It does not run gameplay. Parameters can be
changed live (in Play) to fine-tune.
It is used to fine-tune the parameters for every world type.
It should allow the developer to easily copy and paste all the parameters (the component's
context menu has **Copy Settings (JSON)** / **Paste Settings (JSON)** that round-trip every
parameter through the system clipboard).
For large maps, live tuning regenerates at a **reduced preview resolution** while you drag
a value, then snaps to full resolution shortly after you stop (or on **Regenerate**).

---

## 7. Determinism

Every random choice — terrain noise offsets, which land cells become city centers, where
resources scatter — is derived from the **seed**. A world is therefore fully reproducible
from `(seed, size, type, resources, players)`.

---

## 8. Open questions / to fill in  ✍️ (edit me)

Q: Resource densities and **per-type ratios** (e.g. more wood & food than gold & stone)?
A: for now lets just keep them the same ratio

Q: **Fairness guarantees** per start — e.g. each player gets at least X wood + Y food within
  N tiles of their city center?
A: Yes, depending on the resource density

Q: On *Archipelago*, must each player get their own island? Max players per island?
A: Only one player per island, so force the generator to consider the player count when generating the islands.

Q: Should *Pangea* still contain some inland lakes / a few mountain ranges?
A: It could but its not a forced condition

Q: Player-count min/max? Which slot is the human?
A: Keep the human in the first slot. For now lets just cap the max player-count to 4 (1 human and 3 bots)

Q: Starting villagers per player (currently 3) — fixed or configurable?
A: The villagers will not be spawned by the game generator. They will be spawned when the game starts in a random available tile next to the city center.

Q: Margin so nothing spawns right on the map edge?
A: The edge of the map is not forced to be see, an island is allowes to be cut by the edge. Just keep a couple of tiles of margin for the city center spawn point

Q: Should there be a "balanced/mirrored" option for competitive symmetry?
A: Not for the moment, maybe in the future

---

## 9. Relationship to the current implementation

Today `WorldMapGenerator` (in `Assets/Scripts/Map/`) exposes the **raw knobs** directly:
`width/height`, `worldType` (Islands/Continental/Lakes), `noiseScale`, `octaves`,
`persistence`, `lacunarity`, the elevation thresholds, `teamCount`, `resourceCount`.
`MatchLayout` already computes the per-team city centers (farthest-point on land) and the
scattered resources.

The reformulation adds a **preset front-end** on top:
- new enums `WorldSize {Small,Medium,Large}`, `WorldType {Pangea}` (only Pangea for now,
  replacing the old Islands/Continental/Lakes; extensible later),
  `ResourceAmount {Scarce,Normal,Abundant}`;
- a `playerCount` (= number of city centers, was `teamCount`);
- a step that **derives** dimensions from Size and the resource count from
  Resources (density × land tiles), while the noise/threshold knobs stay editable as the
  per-type "recipe" to tune and copy/paste.

Nothing in this rework touches Game.unity yet.
