# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# Farm Fury: Arcade

A Pac-Man-style maze arcade game set in the Farm Fury universe. Farm animals navigate
farm-themed mazes at night, collecting crops while evading (or chasing) Harvest Robots. See
`FarmFury_Arcade_GDD_v1.docx` (kept alongside this project, not in this repo) for the full game
design document — this file only covers what's needed to work in the Unity project itself.

## Stack

- **Engine:** Unity 6000.5.0f1 (Unity 6). The GDD specifies Unity 2022 LTS, but only Unity 6 was
  installed when this project was scaffolded; package versions were taken from Unity's own
  bundled 2D template for this exact editor build to guarantee compatibility. Revisit if the
  team standardizes on 2022 LTS later.
- **Render pipeline:** Universal Render Pipeline (URP), 2D Renderer, `Assets/Settings/`.
- **Input:** Input System package exclusively (`activeInputHandler = 1`) — no legacy
  `UnityEngine.Input` calls anywhere; use `UnityEngine.InputSystem` APIs (`Keyboard.current`,
  `Pointer.current`, etc.).
- **Architecture:** single scene (`Assets/_Project/Scenes/Game.unity`), data-driven levels via
  ScriptableObjects. No per-level scenes — `GameManager.LoadLevel(int)` swaps content in place.

## Folder structure

```
Assets/_Project/
  Scripts/
    Core/        Managers (GameManager, DataManager, SaveManager, ScoreManager,
                  SceneController, TileMapRenderer, PowerPelletManager, ChaseScoreManager,
                  CharacterManager, ComboSystem, UnlockManager, SceneTransitionManager,
                  AudioManager, DailyChallengeManager, LeaderboardManager), GameState enum,
                  Phase1Test/Phase2Test/Phase3Test/Phase4Test/Phase5Test
    Data/        ScriptableObject definitions + enums (LevelData, CharacterData, RobotData,
                  ChallengeType, ...)
    Gameplay/    Movement, input, collectibles, warp tunnels, animation, PlayerHealth,
                  CharacterBase, WaterTile
    Enemies/     Robot AI: RobotBase + 6 subclasses, RobotAI, RobotSpawner, RobotVisual, RobotState
    Abilities/   AbilityBase + 8 character abilities, EggHazard, ShockwaveEffect, WoollyClone
    UI/          Real uGUI screen controllers (Phase 5) — see "Screens & scene flow" below.
    Utilities/   Singleton<T>, PlaceholderSprite, CameraShake
    Editor/      Phase1-5ProjectBuilder, ArtWiringBuilder, UIBuilderHelpers (see below)
  ScriptableObjects/Resources/{Levels,Characters,Robots}   ScriptableObject assets
  Prefabs/{Characters,Robots,Blocks,Abilities,UI}
  Sprites/{Characters,Robots,Environment,UI}, Audio/{Music,SFX}   real art and audio landed here
                                                              (see "Art status" below)
  Resources/TMP Settings.asset, TextMesh Pro/Resources/     TMP essentials (see "TextMeshPro
                                                              bootstrap" below)
  Scenes/Game.unity
```

`Tools/maze-designer.html` (repo root, sibling of `Assets/`) — a standalone web page, not a Unity
asset, for hand-designing maze layouts; see the `Phase2ProjectBuilder`/`BuildLevelData01` entry
under "Editor tooling" below for how its export feeds into `LevelData_01`.

**Why `ScriptableObjects/Resources/...` and not just `ScriptableObjects/...`:** `DataManager`
loads all level/character/robot data via `Resources.LoadAll<T>`, which requires the assets to
live under a folder literally named `Resources` (a build-time requirement, not just an Editor
one). If build size ever becomes a concern, swap `Resources.LoadAll` for Addressables inside
`DataManager` — its public API (`GetLevelData`, `GetCharacterData`, `GetRobotData`) doesn't need
to change either way.

## Architecture

### Managers (`Scripts/Core`)

All singletons live on one `GameManagers` GameObject in `Game.unity` so they persist for the
life of the app: `GameManager`, `DataManager`, `SaveManager`, `ScoreManager`. `SceneController`
and `TileMapRenderer` are also on that GameObject but aren't singletons (nothing needs to reach
them by static reference — `GameManager` fetches `SceneController` via `GetComponent` in `Awake`).

- **GameManager** — current `GameState`/level/character, crops-remaining tracking, delegates
  score to `ScoreManager` (its own `AddScore`/`GetCurrentScore` just forward to it). `EndLevel(true)`
  also triggers `UnlockManager.CheckUnlocksOnLevelComplete`. **A maze isn't endless**: `GameManager.
  MaxRespawns` (3) caps how many times the player can die in one maze — `NotifyPlayerDeath()`
  increments `DeathCountThisMaze` and, once it exceeds the cap (the 4th death), **no longer ends
  the run immediately** (see "Monetisation" below) — it raises `OnReviveOffered` and returns
  `false`, and `PlayerHealth.DeathSequence` waits on `ReviveDecisionPending` before deciding
  whether to respawn (if the player paid to revive) or skip the respawn and leave the character
  faded out (if they declined/couldn't afford it — `EndLevel(false)` fires from `DeclineRevive`
  in that case, same end state as before this feature existed). `GameManager.
  LevelTimeLimitSeconds` (120) is checked every `Update()` — once `GetElapsedSeconds()` reaches it
  while `Playing`, `EndLevel(false)` fires directly (no revive offer for a timeout, only for the
  respawn cap). Either path sets `GameState.LevelFailed`, which `GameplayHUD`'s state-watcher
  reacts to by showing `LevelFailedScreen` ("Try Again" — see its own section under Screens
  below). `GameplayHUD`'s timer text now **counts down** from `LevelTimeLimitSeconds`
  (`RefreshTimerText`) instead of counting up elapsed time. In the last `TimerWarningThresholdSeconds`
  (15s) it also pulses red (`Color.Lerp` between the normal colour and `TimerWarningColor` on a
  sine wave, same "pulse" convention `GameplayHUD`'s ability-ready flash already used) — added per
  feedback that the timeout ending a run felt "random" with no warning; the countdown text alone
  was easy to miss while focused on the maze.
- **DataManager** — loads all ScriptableObject data at startup via `Resources.LoadAll`.
- **SaveManager** — PlayerPrefs-backed persistence (highest level, coins, star ratings, character
  unlocks). Cluck and Bessie auto-unlock on first run (starter characters per the GDD).
- **ScoreManager** — per-maze and lifetime score, `OnScoreChanged` event, combo multiplier slot
  (always 1 — this is a score multiplier slot, unrelated to Phase 4's character-swap `ComboSystem`).
- **TileMapRenderer** — reads `LevelData.MazeLayout`, instantiates the tile-id-appropriate
  prefab per cell, exposes `GridToWorld`/`WorldToGrid`/`IsWalkable`/`ClearMaze`, plus Phase 4's
  `SetTemporaryWalkable`/`GetWallAt`/`DestroyWallAt` (see Abilities below).
- **SceneController** — delegates maze rendering to `TileMapRenderer`, player spawning to
  `CharacterManager.SpawnInitialCharacter`, and robot spawning to `RobotSpawner`.
  `LoadLevelContent` spawns whichever `CharacterManager.ActiveCharacter` the player last swapped
  to, not a hardcoded Cluck — `CharacterManager` is a singleton that persists for the app's
  lifetime, so `ActiveCharacter` already survives `ClearActiveCharacter()` destroying the previous
  level's GameObject; `LoadLevelContent` previously ignored that and always respawned Cluck,
  silently reverting any swap on every level load/retry. Still defaults to Cluck (the enum's first
  value) the very first time a level is ever loaded in a session, since there's no prior swap yet.
- **CharacterManager** (Phase 4) — owns all character spawning/swapping; see Characters & Abilities.
- **ComboSystem** (Phase 4) — the 8 character-swap combos; see Characters & Abilities.
- **UnlockManager** (Phase 4) — checks `CharacterData.unlockLevel` against mazes-completed on
  level complete and unlocks via `SaveManager`.

### Data-driven levels (`Scripts/Data`)

`LevelData`, `CharacterData`, `RobotData` are ScriptableObjects. The maze grid is the single
source of truth for what's on each cell — tile ids follow this convention:

| id | meaning |
|---|---|
| 0 | empty ground (walkable) |
| 1 | wall (blocks movement) |
| 2 | crop kernel spawn point |
| 3 | vegetable spawn point |
| 4 | power pellet spawn point |
| 5 | warp tunnel edge |
| 6 | robot factory (walkable, decorative until robot AI exists) |
| 7 | player start (walkable, decorative — `LevelData.playerStartPosition` is the actual spawn source) |
| 8 | water (blocks everyone except a character with `CharacterData.canCrossWater` — Ducky) |

`LevelData.waterTeleportRows` pairs id-8 tiles per row, same convention as `warpTunnelRows`, but
walking onto water is **not** an automatic teleport — see Characters & Abilities (SkipShotAbility).

**Per-world art (`TileMapRenderer.MazeArtSet`):** wall/ground/warp-tunnel prefabs, the gameplay
backdrop, crop prefabs, and the power-pellet sprite all used to be single fields shared globally
across every level — fine when only CornField existed, wrong once World 2 (VegPatch) needed its
own look. `TileMapRenderer` now holds a `List<MazeArtSet>` keyed by `MazeType`, resolved per
`RenderMaze` call via `ResolveArtSet` (falls back to index 0 — always CornField — if a `MazeType`
has no entry yet, e.g. an authored-but-unwired World 3). `Phase2ProjectBuilder.WireScene` builds
the full list; `ArtWiringBuilder.SetBackdropSprite`/`SetPelletSprite` mutate individual entries
afterward once art is uploaded, adding a bare entry if needed rather than requiring the whole list
rebuilt. `groundPrefab` is shared across both current worlds (`Ground_CornField`'s soil reads fine
for a vegetable patch too — no dedicated VegPatch ground art exists).

Two other per-world behaviors live on `MazeArtSet`:
- **`bonusPickupPrefab`/`bonusPickupCount`** — the per-world THEMED bonus, extra pickups scattered
  on random walkable cells on top of whatever else is already there, not tied to any grid tile id
  (Orchard's cherry, Wheat's grain sack, both x10; CornField and VegPatch have none of their own).
  Collected via `CropCollector` but deliberately does **not** call `GameManager.
  NotifyCropCollected` — it's bonus, invisible to `LevelData.totalCropsRequired`, and never blocks
  level completion. **Separate from the coin** below — `TileMapRenderer.SpawnScatteredPickups` is
  a shared helper `RenderMaze` calls twice per maze (once for this field, once for the coin), so a
  maze can carry both independently. `SpawnScatteredPickups` excludes crop/vegetable/power-pellet
  cells (tile ids 2-4) AND warp tunnel cells (tile id 5) from its candidate list — a bonus pickup
  landing on the same cell as a crop/pellet tile used to visually double up (worse once Orchard's
  crop-apple sprite was enlarged to match the power pellet's scale — see the crop/pellet scale note
  under "Per-world art" below — since the larger apple would swallow a smaller bonus cherry sharing
  its cell), and one on a warp tile read as oddly placed and risked being collected before the
  player could even see it.
- **`TileMapRenderer.universalCoinPrefab`/`coinsPerMaze`** — NOT part of `MazeArtSet`, a single
  scene-level field spawned on every maze regardless of world, guaranteeing a `Pickup_Coin` exists
  on every level. Used to be CornField's own `MazeArtSet.bonusPickupPrefab` entry instead (the only
  world with one — VegPatch had no bonus pickup configured at all, Orchard/Wheat's bonus slot was
  already spoken for), which meant only CornField's 25 levels ever spawned a coin; caught and fixed
  per feedback that every level should have one. CornField's own `bonusPickupPrefab` entry was
  removed from `Phase2ProjectBuilder.WireScene`'s `MazeArtSet` list once this was added (it was also
  `Pickup_Coin`), so CornField levels still get exactly one coin, not two.
- **`useRandomVegetableQuota`/`vegetableQuota`** — VegPatch-only: ignores the grid's own tile-id-2-
  vs-3 split and instead randomly picks `vegetableQuota` (10) of the maze's crop-eligible cells to
  render as the vegetable (cabbage) prefab, the rest as the kernel (carrot) prefab. Guarantees an
  exact cabbage count per level regardless of how a level was hand-drawn or generated;
  `totalCropsRequired` is unaffected since it's the same total pickup count either way.

Every power pellet in a maze now shows **one single sprite per world** (`MazeArtSet.pelletSprite`
— sunflower-glow for CornField, apple for VegPatch) rather than the old 3-way Sunflower/GoldenWheat/
Rainbow visual split. `RollPelletTier`'s random tier (and the "only 1 non-Sunflower per maze" cap)
still drives `PowerPelletManager.GetDuration` (5s/9.5s/17s — see its own doc comment further down
for the full tuning history) and
`SpawnCollectEffectIfRare`/`PlayRarePelletPickupSfx` exactly as before — only the sprite stopped
varying by tier, so the duration/effect variety is invisible up front but still felt.

Crop/vegetable/pellet/warp-tunnel positions are **not** stored as separate arrays — an earlier
pass had `CropPlacement[]`/`PowerPelletPlacement[]` fields on `LevelData`, but these were removed
in favor of scanning the grid for tile ids 2–5, since the GDD's own convention table already
encodes this and keeping both would mean two sources of truth. `LevelData.warpTunnelRows` is
metadata only (which rows contain at least one tile id 5, used by `Phase2Test`'s SKIP check) —
`TileMapRenderer.RenderMaze` doesn't consult it; it derives pairing itself while scanning the grid.

**`TileMapRenderer.PairWarpTunnels` pairs same-row tiles first, then pairs whatever's left over by
same-column** (`PairByAxis` run twice — `y` then `x` — on a shared "remaining" list, removing each
matched pair before the next pass). This two-pass approach replaced two earlier, narrower attempts,
both of which silently stranded some warp tiles (touching them did nothing —
`WarpTunnel.PairedWarp` stayed null, and `OnTriggerEnter2D`'s own null guard no-opped the contact):
1. **Row-only** (the original approach) — every tile grouped strictly by `y` regardless of `x`.
   Works for the classic left/right-edge case (`x=0` and `x=mazeWidth-1` in the same row), but a
   genuinely *vertical* pair — e.g. `LevelData_02`'s tiles at `(8,0)` and `(8,8)`, sharing a column
   but on two different rows — could never land in the same bucket; each was stranded alone.
2. **Edge-column-vs-edge-row classification** (the first fix) — guessed each tile's pairing axis
   from whether it sat on a left/right-edge column (`x=0`/`x=mazeWidth-1`, pair by row) or not (pair
   by column). This broke a *different* case several algorithmically generated levels actually use:
   two tiles sharing a row but at **non-edge** columns (e.g. `(1,8)` and `(9,8)`, two openings
   through the same top wall) — the classifier routed both into column pairing since neither sits at
   `x=0`/`x=11`, and since each is alone in its own column, both were stranded.

Row-first-then-column needs no per-tile axis guessing — it just tries the two conventions a maze
can actually use (same-row, or same-column) in turn until every tile is paired, so both cases above
resolve correctly regardless of which columns/rows the tiles happen to sit at. Any tile still
unpaired after both passes logs `[TileMapRenderer] Warp tile at (x,y) has no row-mate or
column-mate to pair with`.

**`LevelData_08`'s maze had a genuine hand-authoring slip**, found via a full 50-level pairing audit
(offline script, same row-then-column logic): two of its 4 warp tiles, `(0,2)` and `(10,8)`, had
neither a row-mate nor a column-mate at all — dead on arrival regardless of which pairing algorithm
ran. `(0,2)` was fixed by adding a real partner at `(11,2)` (which needed `(10,2)` opened from wall
to floor first, so the destination isn't a walled-in dead end — verified this adds no 2x2-open
block and only ever adds connectivity). `(10,8)` has no equally clean fix: its only valid vertical
partner row (`y=0`) already holds this maze's *other* pair's tile at `(7,0)` — adding a second `y=0`
tile there would make the row-pass greedily pair `(7,0)` with the new tile instead of each with its
real partner (`(7,6)`/`(10,8)`), breaking both pairs to fix one. Reverted to a plain wall instead,
matching the rest of that border row — it never worked before either way, so removing it changes
nothing a player would notice.

**Why `LevelData.mazeLayoutFlat` instead of a raw `int[,]`:** Unity's serializer doesn't support
multi-dimensional arrays — a field declared `int[,]` silently fails to persist. The grid is
stored as a flat `int[]` (row-major) and exposed as `int[,]` through the `MazeLayout` property
(and written via `SetMazeLayout`). Always go through `MazeLayout`/`SetMazeLayout`, never touch
`mazeLayoutFlat` directly.

### Movement (`Scripts/Gameplay`)

**Hold-to-move, not auto-run.** `GridMovement` only advances while `InputController` reports a
direction currently held — releasing it stops the character immediately, wherever she is (mid-tile
is fine). Switching direction, including a full 180° reversal to escape a robot, takes effect the
instant it's pressed — no cooldown, no "must be at an intersection" gating. This replaced an
earlier auto-run-until-blocked model (queue a direction once, keep moving until a wall or an
explicit new queue, with 180° reversal specifically blocked in a plain 2-neighbour corridor,
matching classic Pac-Man's "no U-turn mid-corridor" rule) after it read as unresponsive in
practice: a queued turn only got re-evaluated at the next full cell reached, which could be
several tiles away, so pressing a direction looked like it did nothing until she happened to
reach a spot where that direction was already valid.

`InputController` tracks "what's currently held" as a single static value
(`InputController.CurrentHeldDirection`) plus a change event (`OnHeldDirectionChanged`) —
`GridMovement` just reads it, both on the event and by re-syncing in `OnEnable` (so a freshly
spawned/swapped character immediately reflects whatever's actually held right now, rather than
starting stopped even if the player never released the button). Keyboard (WASD/arrows) and the
on-screen D-pad (`UI/DirectionalPadController`) both have true press/release semantics, tracked in
a shared "currently held" stack — most-recently-pressed wins if two are held at once (tapping Down
while still holding Right switches to Down immediately; releasing Down reverts to Right if it's
still held). A completed swipe has no physical "hold" to release, so it sets the direction directly
or, and that direction persists until overridden by a keyboard/D-pad press or another swipe — the
one place the old "flick and go" convention was intentionally kept, since a touch swipe genuinely
isn't a hold gesture. **`DirectionalPadController` wires `PointerDown` → `InputController
.PressDirection`, and both `PointerUp` and `PointerExit` → `ReleaseDirection`** (via `EventTrigger`,
not `Button.onClick` — `onClick` only fires on release, which would make even starting to move
read as delayed). `PointerExit` releasing too means dragging a finger off the button while still
pressed also stops the character, rather than leaving a direction "stuck" held forever if the
release happens outside the button's bounds. `OnAbilityActivateInput` (Space) and
`OnSwapMenuToggleInput` (Tab) are unrelated static events on the same `InputController`, unchanged
by any of this — `AbilityBase` subscribes directly to `OnAbilityActivateInput`, safe because
`CharacterManager` guarantees only one character GameObject (and so only one subscriber) exists at
a time, destroying the old one before creating the new one on every swap.

**Kinematic Rigidbody2D gotcha:** Cluck's `Rigidbody2D` is Kinematic (so `GridMovement` can drive
it via `transform.position`) with **`useFullKinematicContacts = true`** set explicitly. Without
this, Unity's 2D physics does not fire trigger callbacks between a Kinematic body and a plain
static collider (crops, power pellets, warp tunnels — none of which have their own Rigidbody2D).
This bit us once already; if new pickup/trigger types stop firing `OnTriggerEnter2D` unexpectedly,
check this setting first before assuming a logic bug. Every robot prefab carries the same
Kinematic + `useFullKinematicContacts` Rigidbody2D for the same reason (so robots can trigger
`WarpTunnel`, which has no Rigidbody2D of its own).

**Camera:** `Utilities/CameraFollow` (added post-Phase-5, attached to Main Camera) tracks
`CharacterManager.Instance.ActiveCharacterObject` every `LateUpdate` — read live, never cached,
same convention as `RobotBase.playerMovement`, since swapping characters destroys/recreates that
GameObject. Clamped to the maze bounds via `TileMapRenderer.MazeWidth`/`MazeHeight` * `CellSize`
(0 before a level loads, in which case `ClampToMazeBounds` no-ops) so the camera never shows past
the maze edges. `ClampToMazeBounds` has a dedicated branch for when the camera's half-width/
half-height exceeds the maze's own extent (`minX > maxX`): it centers on the maze's midpoint
instead of running `Mathf.Clamp` — a plain clamp would collapse to `minX` (pinning the camera off
to one side) in that case.

`orthographicSize` is **not** a fixed value — `ApplyOrthographicSizeForAspect` (called every
`LateUpdate`, before the follow logic) derives it purely from `CameraFollow.CellScreenHeightFraction`
(0.105, a fixed tile-size-to-screen-height ratio) and `TileMapRenderer.CellSize` — deliberately
**not** from the camera's `aspect` at all (an earlier aspect-driven formula controlled visible
*columns* instead, which left height uncontrolled and only ~50% of a tall maze's rows ever on
screen). Every device therefore renders each tile at the exact same on-screen size and shows the
same number of rows; a wider device just reveals more columns (and more `GameplayBackdrop` bleed
at the sides) rather than changing zoom. This also means tile size is completely independent of
maze width/height — resizing the maze (see below) never requires retuning the camera.
`LevelData_01` itself is **12×9, hardcoded as the fixed size for every maze** (`Phase2ProjectBuilder.
BuildLevelData01`'s `width`/`height` consts — not a per-level choice) — reduced from an earlier
14×16 without compensating by enlarging tiles (unlike that 14×16's own 28×31→14×16 halving, which
doubled tile size specifically to keep the board's total footprint the same); this time the board
is deliberately smaller on screen. `CameraFollow` adapts automatically to whatever
`mazeWidth`/`mazeHeight` a level declares, since `ClampToMazeBounds` reads them live each frame.
`Utilities/CameraShake` (Bessie's Ground Slam feedback) runs in `LateUpdate` with
`[DefaultExecutionOrder(100)]` so it executes *after* `CameraFollow` and adds its jitter on top of
that frame's follow position via `transform.position +=`, rather than caching an absolute
"resting" position — a stale resting position would snap the camera back to wherever it was at
scene load every time a shake ended.

A `GameplayBackdrop` SpriteRenderer (`World1_Cornfield.png` — swapped from `Wheatfield_background.png`
per a gameplay review, since `LevelData_01` is `MazeType.CornField` and this ties the backdrop to
the world it's actually set in; sorting order `-5`, centered on the maze) fills the space around
the board — see "Art status" below. Sized in `ArtWiringBuilder.WireGameplayBackdrop` to cover
whichever is bigger: the maze's own world footprint, or the camera's view width (derived from the
same `CellScreenHeightFraction` formula × `CameraFollow.MaxSupportedAspect`, the widest landscape
aspect worth planning for) — plus a 1.6x safety margin — uniformly scaled so the art's own aspect
ratio is always preserved (never non-uniformly stretched/zoomed).

### Robot AI (`Scripts/Enemies`)

`RobotBase` is an AI-driven analogue of `GridMovement` — same continuous move-to-next-cell-centre
algorithm, but the next direction comes from `RobotAI.GetNextDirection`/`ComputeDesiredDirection`
instead of player input. It deliberately does **not** reuse `GridMovement`, since that component
reads `InputController.CurrentHeldDirection`; giving robots that component too would make every
robot obey player input.

**State machine:** `Chase` ↔ `Scatter` alternate on a 20s/5s cycle (`RobotBase.
ChaseDurationSeconds`/`ScatterDurationSeconds`, hardcoded constants on the shared base class, not
per-robot `RobotData` fields or per-level config — every robot on every level reverses from
hunting into dispersing-toward-its-scatter-corner on the exact same 20s/5s timing) (paused while
Vulnerable/Defeated, resumed from where it left off). `PowerPelletManager.OnPowerStateChanged` flips every
listening robot to/from `Vulnerable`; a hit while Vulnerable (`RegisterHit()`) decrements health,
and health reaching zero triggers a brief `Defeated` pause → **disappears** (`RobotBase.Disappear()`
disables its `SpriteRenderer` + `Collider2D`) rather than pathfinding back to the factory as visible
"eyes" — playtest feedback called the old walk-back "floating eyes" a bug, not a feature. A
disappeared robot sets `IsPermanentlyDefeated = true` and stays gone for the rest of **this maze**;
`RobotSpawner.ResetAllRobotsToFactory()` (the player-death reset) checks this flag and skips
reviving it, so dying doesn't bring a defeated robot back either. The only thing that brings it back
is the next `GameManager.LoadLevel` — `RobotSpawner.SpawnLevelRobots` destroys and recreates every
robot fresh on every level load. `RobotState.Returning` is now dead/unreachable code (kept in the
enum only because other switches still reference it harmlessly) — nothing transitions into it
anymore. `PlayerHealth` calls `RegisterHit()` on contact with a Vulnerable robot, and starts its own
death sequence on contact with a Chase/Scatter robot (a Defeated robot's disabled collider means no
contact is possible with it at all).

**Per-robot targeting** (`GetTargetPosition()`, used only in Chase — Scatter/Vulnerable/Returning
targets are resolved generically by `RobotBase.ResolveTarget()`):

| Robot | AI | Target |
|---|---|---|
| Harvester | Direct pursuit (Blinky) | Player's grid cell |
| Scout | Predictive interception (Pinky) | 4 tiles ahead of player facing |
| Patrol | Flanking (Inky) | Player + (player − Harvester) vector; needs a `HarvesterRobot` in scene, falls back to direct pursuit if none |
| Drifter | Distance-based (Clyde) | Player if >8 tiles away, else its own scatter corner |
| Heavy | Direct pursuit, 2 hits to defeat, 0.7x speed | Same as Harvester |
| Drone | Wall-ignoring straight-line pursuit, 0.5x speed | Player's grid cell, but via its own `ComputeDesiredDirection` override (bounds-only, no `RobotAI`) |

**Why Drone doesn't use `RobotAI`:** `RobotAI.GetValidDirections` is always wall-respecting by
design (it's the shared pathing helper every other robot uses). Drone overrides
`IsWalkableForThisRobot`/`ComputeDesiredDirection` so a cell only needs to be in-bounds AND not on
the maze's outer border ring to count as walkable — an interior wall is fine, the border wall
isn't — so it picks the closest-to-target direction among all 4 regardless of interior walls,
while still being physically unable to fly off the playable board. "Through walls" is really just
"interior walls don't factor into its direction choice"; the border still does.

**Scatter corners:** `RobotSpawner.GetScatterCorner` assigns each `RobotType` one of the four maze
corners (inset 1 tile from the border), classic-arcade style. `DrifterRobot`'s "retreat" target
when close to the player reuses the same field (`scatterCornerPosition`).

**Anti-loop targeting (`RobotAI.GetNextDirection`):** at each intersection, every walkable
non-reversing direction's neighbour cell is scored by its REAL shortest-path (BFS) distance to the
current target, and the robot **always commits to whichever candidate has the true-shortest
distance** — deterministic-greedy, not a weighted random roll. `RobotAI.ComputeDistances` runs a
real BFS from the target every call (maze is only ~100 cells, called once per robot per
intersection arrival — negligible cost); falls back to straight-line distance only for a target
cell BFS can't reach (shouldn't happen in practice — targets are always real walkable cells — but a
per-robot Chase target like Scout's "N tiles ahead of facing" projection can land on a wall).
`RobotBase`'s short rolling history of its last 6 occupied cells (`_recentCells`, cleared on
`Initialize`/`ResetToFactory`) only ever breaks **ties** — among the candidates that share the
best (lowest) BFS distance, a non-recent one is preferred over a recently-visited one; any random
pick left after that is only among genuinely equally-good options, never a worse one. `DroneRobot`
bypasses `RobotAI` entirely (see above) so none of this applies to it — straight-line distance is
already correct for a robot that ignores interior walls.

This used to weight by straight-line (Euclidean) distance to the target — that reads fine in open
areas, but in a maze with long corridors, continuing straight always scored as "closer" by
straight-line distance even at intersections where turning onto a perpendicular corridor was the
actual shorter (or only) route, causing robots to bounce off a corridor's ends and permanently
oscillate within one row/column. Swapping to real BFS distance (as above) fixed the *math*, but the
first version of that fix still picked a direction via a weighted-random roll (each candidate's
distance mapped to a weight via `1/(1+dist^2)`, one picked by rolling against the total) rather than
committing to the actual best one — which meant RNG could still choose a strictly WORSE candidate
on any given call (a best option 1 tile away vs. a worse one 3 tiles away still had roughly a
1-in-6 chance of the worse pick every single visit). In a busy area with many intersections (a
level's top row, several robots crossing it constantly), that reads exactly like "stuck looping in
the top row" even though the underlying distance math was already correct — reported again after
the BFS fix had already shipped. A `RecentCellWeightPenalty` constant was tried at both 0.15 and
0.05 to narrow those odds, but a weight penalty can only ever discount a candidate's chance of being
picked, never remove it. Replacing the weighted roll with a hard "take the best, break ties with
recency, randomize only among ties" rule (the current behaviour, described above) removes that risk
entirely.

**Fleeing (Vulnerable state):** `RobotBase.GetFleeTarget` used to project a target 10 tiles away
from the player in the opposite direction — a straight-line point that could land outside the maze
entirely and fed the same straight-line bias `GetNextDirection` had, so a fleeing robot could get
stuck in a row exactly like a chasing one. It now calls `RobotAI.FindFarthestCell(playerPos, maze)`
— a BFS from the player's position that returns the single walkable cell with the greatest real
shortest-path distance — so the flee target is always a real, reachable point, genuinely the
farthest corner of the maze from the player. `RobotBase.HandlePowerStateChanged` still reverses the
robot's `CurrentDirection` on the spot the instant a power pellet activates (the classic
"frightened" U-turn cue) and restores whichever state (Chase/Scatter) it was in before once the
countdown ends, so the visible beat is: catch → visibly reverse and flee toward the real farthest
point the moment a pellet is eaten → resume hunting once the pellet's duration (`PowerPelletManager
.GetDuration` — 5s for Sunflower, the common tier) runs out.

**Art status:** `RobotVisual` swaps in a real `RobotEyes.png` sprite for the brief Defeated pause
before a robot disappears (see "Art status" below and the state-machine note above — there's no
longer a Returning phase to show eyes during), but Vulnerable still swaps the placeholder
`SpriteRenderer` colour (blue, flashing white in the last 2s) since no dedicated vulnerable sprite
has been uploaded yet. Replace with a real `Robot_Vulnerable_Walk` sprite swap when that art lands.
Harvester and Scout now have full 4-direction art (Left/Right added for Harvester, Back added for
Scout — see "Art status" below); Drone now has art too (a single symmetric sprite, same for every
facing — see "Art status" below), and Heavy's art was deleted from the project (also see "Art
status") and is deliberately excluded from `Phase3ProjectBuilder`'s auto-assigned robot roster as
a result.

**Chain scoring & power state:** `PowerPelletManager` (Core) owns the single global "frightened"
countdown (`IsPowerActive`, `TimeRemaining`, `ActivatePower(duration)`, `OnPowerStateChanged`)
and duration-per-tier lookup (`GetDuration`: Sunflower 5s / Golden Wheat 9.5s / Rainbow 17s — see
its doc comment for the tuning history: halved from an original 8/15/30 GDD spec per feedback that
the vulnerable window was too long, the two rare tiers got +2s back on top of that so they still
felt meaningfully more valuable, then Sunflower's own base further went 4s → 5s in a later pass).
**`ActivatePower` only ever EXTENDS an already-running countdown, never shortens it** —
`TimeRemaining = IsPowerActive ? Mathf.Max(TimeRemaining, duration) : duration`. It used to
unconditionally overwrite `TimeRemaining` with the new pellet's own duration regardless of what was
already active; since a maze typically has several plain Sunflower pellets (5s) alongside its one
capped rare pellet (GoldenWheat 9.5s / Rainbow 17s), eating a Sunflower partway through an
already-running rare-tier window reset the timer down to a flat 5s, cutting the rare pellet's real
duration short well before it should have expired.
`ChaseScoreManager` (Core) tracks `ChainCount` across one power activation (200/400/800/1600,
+5000 for all 4) and resets when `PowerPelletManager`'s countdown ends.

### Characters & Abilities (`Scripts/Gameplay/CharacterBase`, `Scripts/Core/CharacterManager`, `Scripts/Abilities`)

**`CharacterManager`** owns every character GameObject. Only one exists at a time — swapping
destroys the current one and instantiates the target prefab at the same grid cell/facing
(`SpawnCharacterObject`), then fires `OnCharacterChanged` and `ComboSystem.RegisterCharacterSwap`.
`CanSwapTo` only checks unlock status — the 1-coin cost (free if the player has 0) is deducted by
`ChooseCharacterScreen` on selection, so affordability never actually blocks a swap. A short fade-in
coroutine on the new sprite runs on `CharacterManager` itself (not the character), so it survives
a rapid second swap instead of throwing on a destroyed `SpriteRenderer` — always null-check `sr`
in that coroutine if you touch it.

**`CharacterBase`** is the per-character identity/hub component (`CharacterType`, `CharacterData`),
present on every character prefab alongside `GridMovement`/`CropCollector`/`CharacterAnimator`/
`PlayerHealth`/its unique `AbilityBase` subclass. `Initialize(data)` applies speed and
`canCrossWater` to `GridMovement` and pushes `CharacterData` into `CharacterAnimator`.

**`AbilityBase`** (`TryActivate`/`UpdateCooldown`/`Execute`) subscribes directly to
`InputController.OnAbilityActivateInput` (see the single-subscriber note above). Each character's
ability:

| Character | Ability | Effect | Cooldown |
|---|---|---|---|
| Cluck | EggDrop | 1 egg at her current position; any robot walking over it is instantly defeated | 10s |
| Bessie | GroundSlam | Instantly defeats every robot within 2 tiles at cast, shockwave + camera shake, then the zone lingers 3s defeating any robot that wanders in afterward | 10s |
| Percy | BounceRoll | Rolls 3 tiles forward (9 if buffed) in his current facing direction, instantly defeating any robot touched; stops early at a wall | 10s |
| Woolly | TripleClone | Spawns 2 AI clones (`WoollyClone`) that wander/collect crops for 10s | 10s |
| Ducky | SkipShot | Teleports across an adjacent unused water tile pair — once per pair per maze | 10s (this is the real gate now too — see the note below on why it went from a 2s debounce to matching everyone else) |
| Horace | RearKick | Nearest robot within 3 tiles (Manhattan) knocked back 4 tiles, instantly defeated on landing | 10s |
| Gerald | PuffUp | Pulsates between normal size and 2x scale for 3s, instantly defeats any robot touched throughout the pulse, half speed, can't use warp tunnels | 10s |
| Billy | HeadbuttThrough | Permanently destroys the next 3 walls he hits | 10s |

**All 8 cooldowns were unified to 10s** in a later gameplay pass (previously a spread from 2s
Ducky to 45s Gerald) — `Phase4ProjectBuilder.BuildCharacterData`'s per-character cooldown literals
and the matching ability-component `totalCooldown` values it sets in `BuildCharacterPrefab`/
`AddCharacterBaseAndAbilityToCluck` were all changed to `10f` together (both must stay in sync, per
the existing convention). Ducky's Skip Shot cooldown going from 2s to 10s does mean the button
re-arms much slower now, even though her real limiter was always the once-per-water-pair rule
(`WaterTile.Used`), not the cooldown — confirmed as an intentional tradeoff, not an oversight.

**Every ability-created robot hazard now defeats on contact, not just stuns** (`ForceDefeat`,
bypassing the Vulnerable requirement — same convention `PuffUpAbility` already used) — a later
gameplay rule change: a deployed ability effect a robot runs through should kill it outright.
Applies to `EggHazard`, `GroundSlamAbility`, `RobotBase.KnockBack` (used by `RearKickAbility`,
which dropped its now-unused stun-duration parameter accordingly), and (once `BounceRollAbility`
was reworked into a forward roll — see below) `BounceRollAbility` itself via its own
`OnTriggerEnter2D`. Ducky's `SkipShot` is now the only character ability with no robot-facing
hazard at all.

**`BounceRollAbility` (Percy) reworked from wall-phasing to a forward roll-and-kill.** The original
version armed a "next wall hit becomes temporarily walkable" window (see the old "Wall mutation"
description this replaced, further down); per feedback it was replaced entirely with a rolling dash:
on activation, Percy rolls `RollTilesBase` (3, or `RollTilesBuffed` = 9 if Earthquake Roll/Kick and
Roll buffed the *next* activation — see the combo table below, which now buffs roll distance
instead of wall-phase count) tiles in his current facing direction, moving one tile at a time
(`RollSecondsPerTile` = 0.12s each) and stopping early if a wall blocks the way. Any `RobotBase` his
trigger touches during the roll is `ForceDefeat()`'d regardless of state. `Movement.enabled` (his
`GridMovement`) is set `false` for the roll's duration and re-enabled the instant it ends, so normal
hold-to-move control resumes automatically under whatever direction the player is currently
holding — `GridMovement.OnEnable` already re-syncs to live input on its own, so no separate
"continue walking" logic was needed. Facing direction is read from a new `GridMovement.
LastFacingDirection` property, not `CurrentDirection` — `CurrentDirection` resets to `Direction.None`
the instant no direction is held, which would make the roll always default to Down when activated
while Percy is stationary; `LastFacingDirection` persists the last real direction so the roll fires
correctly in any of the 4 directions regardless of whether he's currently moving.

Percy's own `SpriteRenderer` swaps to `trailPrefab`'s sprite (`Percy_effect.png`, a curled-up
rolling-ball pose) for the roll's duration instead of instantiating that prefab as a separate
trailing child — the earlier version left Percy's normal walk-cycle sprite visible with a faint
trail object barely noticeable behind him, which didn't read as "he became a rolling ball." His
`CharacterAnimator` component is disabled for the roll (it drives the same `SpriteRenderer` every
frame from `GridMovement`'s direction, and would otherwise overwrite the swapped sprite on the very
next frame) and re-enabled alongside `Movement` when the roll ends. `trailPrefab`'s field name/
serialized reference was kept as-is even though it's no longer instantiated — only its
`SpriteRenderer.sprite` is read now — since renaming would drop the existing wired reference on
Percy's prefab (Unity matches serialized fields by name).

**Gerald's `PuffUpAbility` now pulsates rather than holding a flat inflated size.** Was an instant,
fixed 3x scale held for 5s; per feedback that read as "too big." Now scales via a sine wave between
normal size and `ScaleMultiplier` (2x) over `PuffDurationSeconds` (3s), completing
`PulseCyclesOverDuration` (3) full swell-then-shrink cycles — a visible "breathing" rhythm rather
than a static inflated state. The robot-defeat-on-touch behaviour applies throughout the whole
pulse, not gated to the scale peak.

**Robot mechanics abilities lean on** (`RobotBase`, additive Phase 4): `Stun(duration)`/
`IsStunned` (freezes state-cycle + movement, ignored by Defeated/Returning "eyes" — still used by
`ComboSystem`'s Full Fury, the one remaining stun-only effect), `KnockBack(direction, tiles)`/
`IsKnockedBack` (coroutine slide, stops early at a wall, then calls `ForceDefeat()` — no longer
takes a stun-duration parameter), `ForceDefeat()` (bypasses the Vulnerable-state requirement
`RegisterHit` has — used by `PuffUpAbility`, `EggHazard`, `GroundSlamAbility`, and `KnockBack`).
`RobotVisual` tints stunned/knocked-back robots with a dark flicker, same placeholder-colour
convention as Vulnerable/Defeated (knocked-back robots are mid-slide only briefly before landing
into `ForceDefeat`, so that tint is on-screen only for the slide itself now).

**Wall mutation** lives on `TileMapRenderer`, not `LevelData` — `SetTemporaryWalkable(cell, bool)`
overrides a single cell's walkability without touching the maze asset. Its only genuinely temporary
caller used to be Percy's old wall-phase ability (arm a cell walkable, revert after 2s); now that
`BounceRollAbility` has been reworked into a forward roll that never phases through walls (see
above), `SetTemporaryWalkable` is only used as the permanent backing for `DestroyWallAt(cell)`
(Billy, and Gerald's Iron Stampede buff — removes the spawned wall GameObject and never reverts).
Kept general (still takes a `walkable` bool, not `DestroyWallAt`-specific) in case a future ability
wants a genuinely temporary override again. `GetWallAt(cell)` returns the wall GameObject for
tinting — currently unused (its only caller was the old wall-phase glow), kept as public API rather
than removed.

**`playerMovement` in `RobotBase`/subclasses is a live property, not a cached field** — it reads
`CharacterManager.Instance.ActiveCharacterObject` every access. A cached `FindFirstObjectByType`
result (the Phase 3 approach) would go stale the instant the player swaps characters, since that
destroys and recreates the GameObject. The property keeps the exact identifier name every
subclass already used, so no subclass code needed to change.

**`ComboSystem`** tracks character-use order for the current maze (reset by `SceneController` on
`LoadLevelContent`) and detects 8 combos on `CharacterManager.OnCharacterChanged`:

| Combo | Trigger | Effect (consumed on the named ability's *next* activation) |
|---|---|---|
| Feather Storm | Cluck → Woolly | Woolly's clones drop eggs as they walk |
| Earthquake Roll | Bessie → Percy | Percy's next Bounce Roll travels 9 tiles instead of 3 |
| Skip Shatter | Ducky → Woolly | Ducky's next SkipShot spawns 2 wool clones at the destination |
| Double Slam | Bessie → Bessie (2nd+ activation via swap) | Ground Slam radius doubles to 4 tiles |
| Crossfire | Billy → Horace | Rear Kick knockback doubles to 8 tiles |
| Iron Stampede | Bessie → Gerald | Puff Up also destroys walls Gerald is adjacent to |
| Kick and Roll | Horace → Percy | Same buff as Earthquake Roll (9-tile roll, identical effect per GDD) |
| Full Fury | 5+ distinct characters used this maze | Immediate: every robot stunned 5s (not a "next use" buff) |

Buffs are stored as one-shot `Pending*` flags **on `ComboSystem` itself**, not on the ability
instance — a flag on e.g. `BounceRollAbility` would be lost the moment Percy is swapped away
(his GameObject is destroyed). Each affected ability calls the matching `Consume*` method at the
top of its `Execute()`.

**`ChooseCharacterScreen`** (`Scripts/UI`) is the real uGUI character-swap panel — see "Screens &
scene flow" below for its full description. The original Phase 4 `CharacterSwapUI` (`OnGUI`,
"functional even if not polished" per spec) was retired once this replaced it. Toggled by Tab
(via the same `InputController.OnSwapMenuToggleInput` event) or Pause's Swap Character button.

### Monetisation (`Scripts/Core/GameManager.cs`, `AdManager.cs`, `Scripts/UI/RevivePromptController.cs`)

The GDD's Section 11 describes 7 revenue streams (ads, IAP, cosmetic store, season pass,
cross-promotion). Coin economy, ad-SDK infrastructure (all 4 rewarded placements + interstitial),
and IAP plumbing (Remove Ads + 5 coin packs, a minimal Shop screen, Restore Purchases) now exist;
cosmetic store and season pass don't exist. A full build-order plan for the remaining phases was
reviewed and is followed — see "Ad mediation (LevelPlay + AdMob)" and "IAP plumbing" below for what's
actually built vs. still pending. The GDD's
per-level coin formula (1/level, 5/new-best, 25/daily) was deliberately **not** adopted — the
actual formula (`BaseCoinsPerLevel` 10 + `CoinsPerStar` 5 × stars, in `GameManager.
ComputeLevelResult`) predates this review and was kept as-is by choice, not an oversight. "10 coins
per boss defeated" has nothing to attach to — no boss-level feature exists — and was deliberately
left out rather than stubbed.

Two coin-spend features exist:

- **Revival on death (5 coins, `GameManager.ReviveCoinsCost`)** — the 4th death this maze (the one
  that used to call `EndLevel(false)` immediately, see the GameManager bullet above) now raises
  `GameManager.OnReviveOffered` instead and sets `ReviveDecisionPending = true`, freezing
  `Time.timeScale` (same freeze `PauseGame` uses, so robots don't roam while the faded-out
  character waits). `GameplayHUD` (subscribed in its existing `OnEnable`/`OnDisable`, alongside its
  other event subscriptions) shows `RevivePromptController`'s overlay (`Scripts/UI/
  RevivePromptController.cs`, built under `GameplayScreen` in `Phase5ProjectBuilder.
  BuildGameplayHUD`). Real art now wired: `PanelArt` uses a hanging wood-sign background
  ("Revive Prompt panel background.png") that bakes "Revive for X coins?" directly into its own
  bottom slot — the earlier version had a separate runtime coin-icon/cost-text row (`CostRow`,
  `costText`) rendering that same message on top of a plain placeholder background, which is now
  redundant and was removed (`costText` is left unwired; `RevivePromptController.Show` already
  null-checks it). Revive/Decline buttons use `Yes.png`/`No.png` (their own baked-in "Yes"/"No"
  labels — the buttons' auto-generated TMP labels are destroyed at build time, same "icon art
  replaces auto-label" convention every other icon-only button in this project uses). The panel's
  box size was originally tuned for an earlier ~2048x1940 near-square version of the background art;
  when it was replaced with a 666x375 wide banner, the box wasn't updated to match — and
  `ArtWiringBuilder.SetImageSprite` always sets `Image.Type.Sliced`, which **ignores
  `preserveAspect` entirely**, so the banner was being force-stretched into the wrong (too-tall)
  proportions until the box's own `sizeDelta` was corrected to match the art's real aspect ratio
  (and enlarged, 900x852 → 1300x731, per feedback it read as too small) — getting the box aspect
  right makes the forced Sliced stretch uniform, which is visually equivalent to `preserveAspect`
  actually working. The Revive button is disabled up front if `SaveManager.CoinBalance <
  ReviveCoinsCost`, rather than letting the player tap it and find out. Accepting
  (`GameManager.AcceptRevive`) spends the coins and resets `DeathCountThisMaze` back to
  exactly `MaxRespawns` (not below it) — one paid extra life, not a free refill of all 3 — then
  unfreezes time and lets `PlayerHealth.DeathSequence`'s `WaitUntil(() => !ReviveDecisionPending)`
  fall through to a normal respawn. Declining (`GameManager.DeclineRevive`) unfreezes time and
  calls `EndLevel(false)` itself, same end state as before this feature existed. If nothing is
  subscribed to `OnReviveOffered` (e.g. a test harness with no `GameplayHUD`),
  `RequestRevivePrompt` auto-declines rather than leaving `ReviveDecisionPending` stuck `true`
  forever with nothing able to resolve it — a real deadlock risk otherwise, since
  `PlayerHealth`'s coroutine would `WaitUntil` on a flag nothing ever flips back.
- **Skip ability cooldown (3 coins, `GameplayHUD.SkipCooldownCoinsCost`)** — a small button sits
  just left of the ability cooldown ring, built alongside it in `BuildGameplayHUD`. Now shows
  `Btn_skipcooldown.png` (a coin + fast-forward icon) instead of just an auto-generated "-3" text
  label — the numeric label is kept on top of the icon rather than replaced by it, so the cost still
  reads clearly. Hidden/disabled whenever the active
  ability isn't on cooldown; while it is, `GameplayHUD.HandleAbilityCooldownChanged` (already
  firing every frame during a cooldown, via `AbilityBase.UpdateCooldown`) re-checks
  `SaveManager.CoinBalance >= SkipCooldownCoinsCost` on every tick, not just once when the
  cooldown started — a coin pickup mid-cooldown makes the button tappable immediately rather than
  needing the cooldown to restart first. Tapping it spends the coins then calls the new
  `AbilityBase.SkipCooldown()`, which zeroes `CooldownRemaining` by feeding it back through
  `UpdateCooldown` — this reuses that method's existing zero-crossing logic (`OnCooldownChanged`
  fire, `PlayPowerReadySfx`) so a paid skip looks and sounds identical to the cooldown finishing
  naturally, rather than needing its own duplicate "ability just became ready" logic.

Both spends go through the existing `SaveManager.SpendCoins`/`AddCoins` — no new economy plumbing,
just two new call sites.

**Coin balance display** — `GameplayHUD` now shows a running coin-balance chip (`CoinBalanceChip`,
below `ScoreText`; `Coin_Balance_Chip.png` bakes its own coin icon into the left half, a
`CoinBalanceText` TMP label sits over the right half) that polls `SaveManager.CoinBalance` every
frame (`RefreshCoinBalanceText`, same convention as `RefreshTimerText`/`AnimateScoreTowardTarget` —
`SaveManager` has no change event to hook). This is a genuinely new element, not a reskin:
`SaveManager.CoinBalance` previously had **no on-screen display anywhere** — the only places a coin
count ever appeared were the Revive prompt's baked-in cost text and the skip-cooldown button's "-3"
label, neither of which shows the player's actual running total.

### Ad mediation (LevelPlay + AdMob) (`Scripts/Core/AdManager.cs`)

Unity's **Ads Mediation (LevelPlay)** package (`com.unity.services.levelplay`) is installed,
mediating **AdMob** as its network — chosen over AdMob-direct specifically because a previous app
of the developer's had thin ad fill on iOS from a single network, and fill from any one network
gets worse under child-directed treatment (below), which excludes most personalized/programmatic
demand. Mediation gives multiple networks a chance to fill each request, directly addressing that
failure mode. Unity IAP (`com.unity.purchasing`) is also installed alongside this — see "IAP
plumbing" below for what it's wired to.

**Every ad request is treated as child-directed (COPPA)** — the GDD's "8-45" target audience pulls
in under-13 users, and this was the deliberate, simpler-to-implement-correctly choice over runtime
per-user age-gating. Set in two places: the Unity Gaming Services project-level "Will this app be
targeted to children" toggle (`Project Settings > Services`, must be Yes, not the default No), and
`AdManager.Start()` calling `LevelPlay.SetMetaData("is_child_directed", "true")` /
`SetMetaData("is_deviceid_optout", "true")` **before** `LevelPlay.Init()` — both are required,
LevelPlay's SDK-level metadata doesn't inherit from the dashboard-level toggle automatically.

**`AdManager`** (parallel to `AudioManager` — one singleton on `GameManagers`, owns all SDK
interaction so gameplay code never touches `Unity.Services.LevelPlay` directly) wraps:
- **Rewarded ads** (`LevelPlayRewardedAd`) — load/show/auto-reload (reloads immediately in
  `OnAdClosed`, so `IsRewardedAdReady` stays accurate for UI to poll before offering a "Watch Ad"
  option). `ShowRewardedAd(placementName, onResult)`'s callback only reports success if
  `OnAdRewarded` actually fired, not just that the ad was shown and closed — a player can close a
  rewarded ad before it finishes, which should never grant the reward.
- **Interstitial ads** (`LevelPlayInterstitialAd`) — same load/show/auto-reload shape.
  `NotifyLevelLoaded()`, called from `GameManager.LoadLevel` on every level transition, drives a
  rolling counter (`SaveManager.LevelsSinceLastInterstitial`) that shows an interstitial every
  `interstitialLevelInterval` (6, configurable) levels and resets the counter either way (a
  skipped/unready ad doesn't mean the next check fires immediately). Structurally guaranteed to
  never fire mid-`GameState.Playing`, since `LoadLevel` only ever runs at a level transition.

**`SaveManager` gained `AdsRemoved`** (bool, persisted — `NotifyLevelLoaded` already early-outs on
it; `IAPManager`'s Remove Ads purchase now sets it, see "IAP plumbing" below) **and
`LevelsSinceLastInterstitial`** (the rolling counter above, exposed via a getter +
`SetLevelsSinceLastInterstitial` setter rather than a plain settable property, since the
increment-vs-reset-to-0 decision belongs to `AdManager`, not `SaveManager`).

**Both platforms are fully configured** with real LevelPlay app keys and Rewarded/Interstitial
placement IDs, obtained via Unity's newer **"Placements"** dashboard flow (`cloud.unity.com`
Monetization section) — this replaced the older ironSource-style "Ad units + Instances" dashboard
flow around 2026-08-11, mid-project; if you're reading older notes/screenshots that mention
"Instances" or a `platform.supersonic.com` URL, they predate that migration. Config values are set
via `SceneCleanupBuilder.WireAdManagerConfig` (`Farm Fury Arcade > Wire AdManager Config`) rather
than hand-edited in the Inspector, since they arrived piecemeal (Android confirmed, then iOS
separately) — the tool only overwrites a field when a non-empty value is passed in, so re-running
it after only one platform's values are known never clobbers the other platform's already-set
fields back to empty.

**All 4 rewarded ad placements from the plan are now built.** No `Debug`-vs-`Release` build-config
split exists yet for ad unit IDs — `AdManager.enableTestSuite` (LevelPlay's in-app test-ad UI,
`SetMetaData("is_test_suite", "enable")`) is a single Inspector bool toggled by hand, not swapped
automatically per build type; must be turned off before cutting a real release build.

**"Continue after death"** — `RevivePromptController` gained a third button (`watchAdButton`,
between Revive and Decline) alongside the existing 5-coin-revive/decline pair, hidden entirely
unless `AdManager.IsRewardedAdReady` (same never-show-a-dead-button rule the coin-affordability
check already used). Tapping it calls `AdManager.ShowRewardedAd("continue_after_death", ...)`; the
callback only grants the revive if the SDK actually confirms the reward fired (a closed-early/failed
ad leaves the prompt exactly as it was, so the player can still fall back to coins or Decline).
`GameManager.AcceptReviveViaAd()` is a sibling to `AcceptRevive()` — both now funnel through a
shared private `GrantRevive()` (reset `DeathCountThisMaze` to `MaxRespawns`, unfreeze time) so the ad
path grants the identical one-more-life effect without spending coins. The once-per-maze cap needs
no extra flag — the prompt itself only ever fires on the death that exceeds `MaxRespawns`, same
trigger condition already gating it. Uses real art: `WatchAd.png` (baked-in "Watch Ad" label, same
label-baked-into-the-button-art convention as `Yes.png`/`No.png`) wired via `ArtWiringBuilder`'s
`BtnWatchAd` constant.

**"Double coins earned"** — `LevelCompleteController` gained a "2x Coins (Watch Ad)" button on
`LevelComplete.png`'s shelf, below the score/stars. `GameManager.ClaimDoubleCoinsViaAd()` (a sibling
to `AcceptRevive`/`AcceptReviveViaAd`'s pattern) pays out a second copy of that completion's
`LastLevelResult.coinsEarned` once `AdManager` confirms the reward — an additive top-up, not a
retroactive rewrite of `coinsEarned` itself. `GameManager.DoubleCoinsClaimed` (reset every
`EndLevel(true)`) stops it being claimed twice for the same completion. Uses real icon art
(`DoubleCoins.png`, a coin-stack + ×2 badge) with the "2x Coins (Watch Ad)" text rendered at runtime
on top — icon-only, not baked-in-label, since the button is small and square.

**"Extra ability charge" / "skip cooldown via ad"** — per the plan doc's own text, these two listed
placements are literally the same feature ("(d) same button, ad as the free alternative to spending
coins"), not two separate ones. Built as one: a Watch Ad button in `GameplayHUD` sitting just left of
the existing 3-coin skip-cooldown button, calling `AbilityBase.SkipCooldown()` via
`AdManager.ShowRewardedAd("skip_cooldown_via_ad", ...)` only on a confirmed reward. Shown/hidden
every tick alongside the coin button (`HandleAbilityCooldownChanged`), gated on both "on cooldown"
and `AdManager.IsRewardedAdReady`. No dedicated square icon art exists yet — `WatchAd.png` is a wide
512x214 plaque banner sized for the revive prompt, and would squash unreadable in this small HUD
slot — so it keeps a plain "AD" text label for now, same convention the coin button's "-3" used
before `Btn_skipcooldown.png` existed.

### IAP plumbing (`Scripts/Core/IAPManager.cs`, `Scripts/UI/ShopController.cs`)

Monetisation Build Plan Phase 3. `com.unity.purchasing` (5.4.2) uses the newer async
`UnityIAPServices`/`StoreController` API (`Connect()`, `FetchProducts()`, `PurchaseProduct()`,
`RestoreTransactions()`, event-based `OnPurchasePending`/`OnPurchaseFailed`/etc.) — not the older
`IStoreListener`/`ConfigurationBuilder` pattern older Unity IAP docs/tutorials describe. If you're
reading old sample code that implements `IStoreListener.ProcessPurchase`, it's for a different
package version; follow the pattern in `Library/PackageCache/com.unity.purchasing@.../Samples~/01
BuyingConsumables/` instead.

**`IAPManager`** (parallel to `AdManager`/`AudioManager` — singleton on `GameManagers`, owns all SDK
interaction) connects on `Start()` and fetches the product catalogue once connected: 1
non-consumable (`remove_ads`, $4.99, grants `RemoveAdsBonusCoins` = 100 per the GDD's "Includes 100
bonus coins") + 5 consumables (`coins_100`/`500`/`1500`/`5000`/`15000`, prices exactly matching GDD
Section 11's table). `HandlePurchasePending` grants the effect (`SaveManager.AddCoins`/`AdsRemoved`)
and calls `ConfirmPurchase` — same grant-then-confirm order the package's own sample uses.
`GetPriceString(productId)` returns the real localized price once the store connection resolves
metadata, falling back to the GDD's static price table until then (or forever, in the Editor/dev
builds where no real store is configured) — UI should never show a blank price.

**Remove Ads does NOT disable rewarded-ad availability** — the GDD is explicit ("Still shows
rewarded ad prompts as an option"), so only `AdManager.NotifyLevelLoaded`'s interstitial path checks
`SaveManager.AdsRemoved`; none of the 4 rewarded placements above do. `SaveManager.AdsRemoved`
already existed before this phase (added alongside the interstitial in Phase 2, per its own doc
comment above) — no new persistence needed.

**`ShopController`** replaced the old "Store is coming in Phase 6!" placeholder panel (same root
GameObject name, `StoreComingSoonOverlay`, kept for scene-path stability even though the content
changed) — a plain 2x3 grid of the 6 products, a status text row, and a close button. **This is
deliberately not the full cosmetics Store** the GDD's Section 11 describes (hats/skins/trails/
themes) — that's Phase 4 scope and doesn't exist yet; this screen is only the Phase 3 purchase
surface for coin packs + Remove Ads. No purchase-card art exists yet (per the plan doc's own "Art
needed" list — zero art exists here), so every button is a plain text label
(`"{displayName}\n{price}"`), same "text label until art lands" convention every other unart'd
placeholder button in this project uses. Reached via a new **Main Menu "Shop" button** (bottom-
centre, between Play and Settings — also no dedicated icon art yet). Remove Ads' button disables
itself once `SaveManager.AdsRemoved` is already true (a non-consumable can't be purchased twice).

**Restore Purchases** now fills Settings' 2x3 grid's previously-empty 6th cell (see the Settings
section under "Landing/Gameplay-HUD cleanup" below for that grid's own history) — a plain
`CreateButtonPlaqueCell` (a new sibling to `CreateTogglePlaqueCell`, same visual convention, Button
instead of Toggle) calling `IAPManager.RestorePurchases`, with its own label swapping
"Restore Purchases" → "Restoring..." → "Restored!"/back to idle as the one feedback mechanism (no
toast system exists in this project yet).

**Still not built** (Phase 3's own "Technical needed" list, none of it is code): the 6 products
aren't registered in App Store Connect/Google Play Console yet, so `IAPManager.Connect()` is
expected to fail gracefully with a logged warning in the Editor and in any build without real store
config — same "infrastructure ready, real config later" pattern `AdManager` already established for
its own ad unit IDs. No sandbox test accounts exist yet either. Purchase-card art and a "Thank you"
confirmation toast (both listed as Phase 3 "Art needed," the toast marked optional in the plan doc)
are unbuilt.

### Screens & scene flow (`Scripts/UI`, Phase 5)

**Still single-scene** (see the architecture note at the top) — "scene transitions" are Canvas
panels being shown/hidden, not `SceneManager.LoadScene`. Every top-level screen is a direct child
of the existing `Canvas` GameObject (built by `Phase1ProjectBuilder`, upgraded by
`Phase5ProjectBuilder` to `CanvasScaler.ScaleWithScreenSize`, 1920×1080 reference, 0.5 match —
the "scale properly for different screen sizes" requirement) and is mutually exclusive with every
other top-level screen.

**Flow:** Main Menu → Level Select → Gameplay HUD → Level Complete → Level Select (Skip button) or
Level Failed → Gameplay (Retry)/Main Menu. Main Menu's Play button (`MainMenuController`) calls
`SceneTransitionManager.ShowOnly(levelSelectScreen)` directly — Level Select (see its own section
below) is where a level actually gets picked and `GameManager.LoadLevel` + `ShowOnly(gameplayScreen)`
happen. There is no intermediate "World Map" step and no "VS" matchup screen/countdown. (An
intermediate `WorldMapController` screen existed here through the 2026-07-31 mockup pass but was
removed later — see "Removed: World Map screen" below. A `MatchupScreenController` screen existed
even earlier and was removed separately — see "Removed: Matchup screen" below.)
**`SceneTransitionManager`** (`Core`) is the single place this is orchestrated: `ShowOnly
(GameObject)` deactivates every screen in its `screenRoots` array and activates just the target,
wrapped in a black-`CanvasGroup` fade (`TransitionTo(Action swapScreens)` is the more general form
`ShowOnly` is built on, for cases — none currently — that need a custom swap instead of "hide all,
show one"). Screen controllers never call `SetActive` on each other directly; they call
`SceneTransitionManager.Instance.ShowOnly(targetScreen)`.

**Overlays are NOT in `screenRoots`** — Pause, Settings, the Store "coming soon" placeholder, and
New Character Unlock layer on top of whatever's currently showing (almost always Gameplay or
Level Complete) rather than going through `SceneTransitionManager.ShowOnly`, and manage their own
`SetActive` directly (instant show/hide, no fade). Originally this also meant "dim gameplay, don't
replace it" (a semi-transparent black overlay) — Pause (and Choose Character, which layers on top
of Pause) dropped that in the 2026-07-31 mockup pass in favour of `World1_Cornfield.png` as an
opaque backdrop, so gameplay is no longer visible behind either while paused. Settings' backdrop
was always opaque regardless of which screen opened it.

**Removed: World Map screen.** An intermediate `WorldMapController` screen used to sit between Main
Menu and Level Select — just `Map.png` background art with bottom-corner Play/Home icon buttons,
tapping Play opened Level Select. It was deleted entirely (not just unlinked): `WorldMapController.cs`
and its prefab-only `LevelMarker.cs`/`LevelMarker.prefab` (the earlier scrolling level-marker strip
this screen's `BuildLevelMarkerPrefab` step still built but never wired — see the old "known gap"
note this section used to carry) are gone from the repo. `MainMenuController.playButton` now calls
`SceneTransitionManager.ShowOnly(levelSelectScreen)` directly. `Phase5ProjectBuilder` no longer
builds `WorldMapScreen` or the `LevelMarker` prefab (removed from `screenRoots` and
`WireCrossReferences`), `ArtWiringBuilder` no longer references `Map.png` or wires a
`WorldMapScreen/PlayButton`/`HomeButton` (the constant and both wiring calls were deleted — `Map.png`
never existed on disk in the first place, this wasn't a case of removing working art), and
`Phase5Test`/`LevelSelectTest` drive Main Menu → Level Select directly instead of via World Map.
Reason: Level Select's own world-badge carousel already fully replaced what World Map's `Map.png`
art was showing, making the extra tap-through redundant. If you're reading older commit history or
design notes that mention "World Map" as a live screen, they predate this removal.

**Removed: Matchup screen.** The Phase 5 "VS" card screen (`MatchupScreenController`, shown between
World Map and Gameplay — character card vs. up to 3 robot cards, plus a 3-2-1-GO countdown) was
deleted entirely after playtesting — it read as tonally mismatched with the rest of the game.
Navigation from World Map (and, after World Map's own removal above, from Main Menu) calls
`GameManager.LoadLevel` + `SceneTransitionManager.ShowOnly(gameplayScreen)` directly; there is no
countdown replacement. `Phase5ProjectBuilder` no longer builds a `MatchupScreen` (removed from
`screenRoots` and `WireCrossReferences`), `ArtWiringBuilder` no longer wires `matchup.png` or its
buttons (the file itself is unused now — left on disk, not deleted), and `Phase5Test`'s
navigation check calls `GameManager.LoadLevel`/`ShowOnly` directly instead of driving a Matchup
Play button. If you're reading older commit history or design notes that mention "Matchup," they
predate this removal.

**Landing/Gameplay-HUD cleanup (post-Phase-5):** once real art landed, screens got stripped down
from their original Phase 5 layouts:

- **Main Menu** (`MainMenuController`) was cut down to two icon buttons directly on `landing.png`
  (which already bakes in the "FARM FURY ARCADE" logo) — `PlayButton` bottom-left → Level Select,
  `SettingsButton` bottom-right → the Settings overlay. The old vertical button stack (Character
  Roster/Daily Challenge/Store/Leaderboards) and its duplicate "Title" text are gone, along with
  the `MainMenuScreen/Content` vertical group they lived in. Character Roster/Daily Challenge/
  Leaderboards still get built by `Phase5ProjectBuilder.BuildAll` and still work — they just have no
  entry point from Main Menu, so reaching them today means calling `SceneTransitionManager.ShowOnly`
  on them directly (nothing currently does). `CharacterRosterScreen`/`LeaderboardsScreen` keep their
  own `mainMenuScreen` back-reference for their "Back" buttons regardless. Store regained an entry
  point later (Monetisation Phase 3's IAP plumbing) — a third `ShopButton`, bottom-centre between
  Play and Settings, opens `ShopController` (see "IAP plumbing" above); Main Menu is a three-button
  screen again, not two, as of that change.
- **World Map** at this point in the project's history had similarly lost its top-left `HomeButton`
  + horizontally-scrolling level-marker strip (`LevelMarker`/`StarDisplay`, built via
  `CreateHorizontalScrollView`) in favour of the same bottom-left/right icon-button convention as
  Main Menu. The screen itself, and this marker-strip infrastructure, are gone now — see "Removed:
  World Map screen" above.
- **Gameplay HUD** (`GameplayHUD`) lost its `SwapButton` — Tab (`ChooseCharacterScreen.ToggleOpen`)
  still triggers it directly via `InputController`, so removing the button didn't remove the
  feature. `AbilityButton` survives as the character portrait itself (see below) and does have a
  cooldown ring again as of a later gameplay review — `AbilityCooldownRing`, a radial-filled
  (`Image.Type.Filled`, `Radial360`) Image behind the portrait, `fillAmount` driven by
  `GameplayHUD.HandleAbilityCooldownChanged` in lockstep with the portrait's existing grey-out-on-
  cooldown tint (empty right after use, full once ready). No dedicated ring art exists yet —
  `PlaceholderSprite`'s plain square still shows the radial sweep correctly, it just won't look
  like a ring until real art replaces it. Once the cooldown reaches zero, the portrait also starts a
  continuous flash (`GameplayHUD.StartReadyFlash`/`ReadyFlashRoutine`, pulsing toward a bright gold
  `AbilityFlashColor` via `Mathf.Sin(Time.unscaledTime * FlashCyclesPerSecond * 2π)`) rather than
  just sitting at a static "ready" colour, so the player gets a clear "you can use this now" cue
  instead of having to notice the ring quietly finished filling. Stopped (`StopReadyFlash`) on
  ability use, character swap, and `OnDisable`, so a leftover flash coroutine never runs against a
  portrait that no longer belongs to the active ability. The character portrait's own background is
  now a round `PlaceholderSprite.GetCircle`-generated circle (a new sibling method to `Get()`, same
  cache-per-colour convention, transparent outside the radius) rather than a solid square — the
  actual character art moved to a separate non-interactive `PortraitArt` child inset inside the
  button (`GameplayHUD.characterPortrait` now points at this child, not the button's own Image),
  since swapping the button's own Image to a real (rectangular) character sprite would have
  overwritten the round shape entirely.
  `SoundButton`/`HomeButton` were later removed too (per playtest feedback) — both are reachable via
  Pause instead (Settings' music/SFX toggles, Pause's own Quit button) — leaving just a single
  `160x160` `PauseButton` (originally bottom-left, matching the Main Menu's Play/Settings buttons;
  swapped to bottom-right in a later pass — see the device-frame-review bullet below for why). A
  vacant `Btn_plaque.png` backdrop ("SideBackdrop") used to run down the right side as a
  placeholder for future writing/navigation — removed entirely after review, since it had no
  behaviour and read as an oversized, unexplained button. `ScoreText`/`TimerText` were later pulled
  further in from the screen edges (an original inset sat above/outside the backdrop art's own
  safe-area guide once viewed on a device frame), enlarged, and given the `Bangers SDF` cartoon font
  (`ArtWiringBuilder.WireGameplayFont` — bundled with TMP's own Examples & Extras, already has a
  correctly-generated SDF material unlike `Inter-Regular SDF`'s broken shader, so no
  import/generation step needed). `LevelText` (the level name header) was removed outright — it
  duplicated what the Level Select tile the player just tapped already established. An on-screen
  **directional pad** (`UI/DirectionalPadController`, originally right side, diamond layout —
  `up`/`down`/`left`/`right.png`, each already a complete rounded button with no separate
  background needed; swapped to the left side in a later pass, see below)
  was added as a touch-friendly alternative to keyboard/swipe; each button calls
  `InputController.PressDirection`/`ReleaseDirection`, the same press/release API keyboard uses,
  so `GridMovement` needs no awareness that a third input source exists.
- Three always-on `OnGUI` debug overlays (`Phase1Test`/`Phase2Test`/`Phase3Test`/`Phase4Test` manual
  test buttons, independent of their `runOnStart` flag) used to render on top of every screen in
  every Play session. `Editor/SceneCleanupBuilder.DisableDebugTestOverlays` (`Farm Fury Arcade >
  Disable Debug Test Overlays`) deactivates all 6 test GameObjects (`Phase1Test` through
  `Phase5Test`, plus `LevelSelectTest`) — safe to re-run, and also de-duplicates them via
  `DedupeAndDisable<T>()`. **This de-dup step matters more than it looks**: `Phase2/3/4/5
  ProjectBuilder` each look up their own test object with a Find-or-create check before adding one,
  and `GameObject.Find` only matches **active** objects — once `DisableDebugTestOverlays` deactivates
  one, a later re-run of that phase's `BuildAll` couldn't find it and silently spawned a fresh
  active duplicate every time. This actually happened during one long session of repeated rebuilds
  and was the real cause of a persistent "black tiles" debug-overlay bug that looked like it kept
  "coming back after being fixed" — it wasn't coming back, a new one was being created each rebuild.
  `Phase2ProjectBuilder`/`Phase3ProjectBuilder`/`Phase4ProjectBuilder` (matching the fix
  `Phase5ProjectBuilder` already had for `Phase5Test`/`LevelSelectTest`) now look up their test
  object via `Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(t => !EditorUtility.
  IsPersistent(t.gameObject))` instead of `GameObject.Find`, so a disabled instance is still found
  and reused. **Still re-run `Disable Debug Test Overlays` after any batch of `PhaseNProjectBuilder.
  BuildAll` calls anyway** — it's the only step that actually collapses duplicates already created by
  older code, and it's cheap/idempotent. Re-enable a specific test (Inspector checkbox, or its
  `ContextMenu`) to run its manual test battery again.

**Gameplay → Level Complete/Failed is push-triggered, not pulled:** `GameplayHUD.Update()` polls
`GameManager.CurrentState` every frame (it keeps running during Pause, since `Time.timeScale`
freezing doesn't stop `Update()`, only physics/animation driven by scaled time) and calls
`ShowOnly` the instant it observes `LevelComplete` or `LevelFailed`. This is the single place that
reacts to level-end state, rather than every possible level-ending action (crop collection,
pause-menu quit, a future timer) separately knowing which screen to show.

**`GameManager.LastLevelResult`** (a struct, not persisted) is computed once in
`EndLevel(true)` and read by `LevelCompleteController` — it does not recompute anything. Star
thresholds: 1 star for completing at all, 2 at 75% of `LevelData.ComputeMaxPossibleScoreEstimate()`,
3 at 95%. That estimate (guaranteed collection score + power-pellet count × 8000 [the max
200/400/800/1600/+5000 chain] + a flat 500 time-bonus cap + 500 perfect-run cap) is a **deliberate
approximation** for star-threshold purposes, not a tuned "perfect play" number — the real
achievable robot-chain bonus depends on how many robots happen to be near the player when each
pellet triggers, which varies run to run. Revisit with real playtest data once more than 2 levels
exist. Coins earned: `10 + stars × 5` (also a placeholder formula, easy to retune in one place —
`GameManager.ComputeLevelResult`).

**Score breakdown categories** (`ScoreManager.CropPoints`/`RobotPoints`) are tracked *alongside*
`CurrentMazeScore`, not instead of it — `CropCollector` calls `AddCropPoints`, `ChaseScoreManager`
calls `AddRobotPoints`, both still route through `AddPoints` internally so the running total stays
correct. Time/perfect bonuses are folded into `CurrentMazeScore` inside `ComputeLevelResult` itself
(via a plain `AddPoints` call) so the HUD's live score and the final `LevelResult.totalScore` never
disagree.

**`ComboSystem.CombosTriggeredThisMaze`** is a simple ordered name list, reset with everything
else in `ComboSystem.ResetForNewMaze()`. It (and the crop/robot/time/perfect-bonus breakdown, and
coins-earned) is no longer displayed anywhere — `LevelCompleteController` was simplified to a
Canva mockup (2026-07-31) that only has room for stars + a score readout on `LevelComplete.png`'s
own wooden shelf (see "Art status" below); the underlying data these used to read is all still
computed (`GameManager.LastLevelResult`, `ScoreManager`'s breakdown fields, `SaveManager`'s coin
balance) in case a future screen wants to surface it again, only the display was removed.

**`UnlockManager.LastUnlockedBatch`** is how `LevelCompleteController` knows to show
`NewCharacterUnlockScreen` automatically after the star/score celebration finishes, without
`UnlockManager` needing to know anything about UI — it just records what
`CheckUnlocksOnLevelComplete` unlocked on its most recent call.

**`GameManager.JustUnlockedWorldIndex`** is the same idea for worlds — set (or reset to null) at
the top of every `EndLevel(true)` call, non-null only when the completed level is a world's gate
level (its own index is the last level of a world) at 2+ stars, that world's `LevelData` actually
exists (World 3/4 have no next-world content yet in some cases), and `SaveManager.
HasSeenWorldUnlock(world)` is still false. That last check is the load-bearing one: an earlier
version compared this level's stars-before-this-call to stars-after, which meant the celebration
silently never fired for anyone who'd already 2-starred a gate level *some other way* than this
exact `EndLevel` transition (replaying an already-qualifying level, or `SceneCleanupBuilder`'s "Set
3 Stars on all levels" debug tool writing stars directly via `SaveManager`, bypassing `EndLevel`
entirely) — the world was genuinely unlocked, but the player had never actually seen the
celebration for it. `HasSeenWorldUnlock`/`SetWorldUnlockSeen` (persisted PlayerPrefs flags, same
one-shot convention as `IsCharacterUnlocked`) fix that: the celebration fires the first time it's
actually shown, regardless of how the stars themselves got there, and `SetWorldUnlockSeen` is
called immediately so replaying the gate level afterward never re-fires it.

`LevelCompleteController`'s celebration sequence reads this after the star/score/character-unlock
beats: if set, it shows **`NewWorldUnlockScreen`** — the world's badge sprite
(`LevelSelectController.GetWorldSignSprite`) bursts in with an overshoot pop, pulses
(enlarges/shrinks) a couple of times, then waits for the player to **tap the screen** before
calling its `onComplete` callback, which `LevelCompleteController` uses to automatically show Level
Select in its world-select state (the newly unlocked badge already renders coloured/tappable there,
since save data was updated before this overlay even showed). The tap gate replaced an original
fixed-hold-then-auto-advance design after playtesting read the whole beat as "nothing happened, it
was very fast" — by the time a player's eye caught the badge, the screen had already moved on.
`NewCharacterUnlockScreen` now uses the same tap-to-dismiss convention (see its own bullet under
"Art status" — it originally auto-dismissed on a fixed timer, unlike this screen, but was later
changed to match). If both a character and a world unlock on the same completion,
`LevelCompleteController` waits for the character card's own dismissal (via an optional
`onDismissed` callback `NewCharacterUnlockScreen.Show` now accepts) before starting the world-unlock
beat, so the two overlays never visually collide.

**The badge's own size was silently broken the whole time, same root cause as
`NewCharacterUnlockScreen`'s card** (see that bullet): built via `CreateImage`, whose width/height
arguments only set a `LayoutElement` a plain (non-`LayoutGroup`) parent panel never reads, so
`worldBadgeRect.sizeDelta` stayed at Unity's default 100x100 regardless of the 850x850 requested —
the burst-in/pulse animation itself was working correctly the whole time, it was just animating up
to a 100x100 target, which is why the badge always rendered tiny no matter how the animation looked
in code review. Fixed by setting `sizeDelta` explicitly. Two more additions per a later benchmark
mockup: a shared **"World Unlocked" wood-sign banner** (`WorldUnlockedBanner`, `WorldUnlocked.png`
— reused across all 4 worlds, unlike the per-world badge below it) above the badge, and the flat
solid-black background replaced with the just-unlocked world's own gameplay backdrop sprite shown
at `BackgroundAlpha` (55%) behind everything (`Background`, first child so it draws behind the
banner/badge/hint) — looked up per-world via `TileMapRenderer.GetOrAddArtSet((MazeType)world).
backdropSprite`, so it works for all 4 worlds generically with no per-world special-casing.
`NewWorldUnlockScreen.Show` gained a `worldBackdropSprite` parameter accordingly;
`LevelCompleteController` fetches it via `FindFirstObjectByType<TileMapRenderer>()` before calling
`Show`.

**`ChooseCharacterScreen`** (real uGUI, `Scripts/UI/ChooseCharacterScreen.cs` +
`CharacterSelectCard.cs`) replaced the Phase 4 `CharacterSwapUI` `OnGUI` panel. Not a
`SceneTransitionManager` screen — like Pause/Settings, it's an overlay shown/hidden directly
(`Show()`/`ToggleOpen()`), temporarily taking Pause's place on top of Gameplay and handing back to
it afterward (`pauseMenuScreen` back-reference). Background is `World1_Cornfield.png` (same
backdrop as Pause) with `Logo.png` top-left and a round back button bottom-left (Btn_home.png
substituted for the mockup's unmatched triangle icon — see `CreateRoundBackButton`'s doc comment
in `Phase5ProjectBuilder`), per a 2026-07-31 Canva mockup. One `CharacterSelectCard` per
`CharacterData.GetAllCharacterData()` entry sits in a `CardCarouselController` (the same
component Level Select's world picker uses — see that section above), each showing
`CharacterData.selectCardArt` — a dedicated framed "animal card" image per character
(`Sprites/UI/{Name}_{Species}.png`, own wood-frame border baked in, distinct from the plain
`portraitSprite` front sprite used elsewhere) — or a placeholder square for any character without
one. Locked cards show a "LOCKED" overlay and their `Button.interactable` is `false`; the active
character's card is also non-interactable (can't "swap" to the character already active) — a
non-interactable `Button` also means the carousel can't re-centre onto that card via tap, only via
drag. `CharacterSelectCard.activeHighlight` (an 85%-opaque yellow square behind the centred card)
was removed per feedback that it read as a distracting yellow background block rather than a subtle
highlight — the field is left null-safe (its `SetActive` call is already guarded) rather than
stripped from the script, so nothing else needed to change. Flick to cycle which card is centred; tapping the
centred card pops it (scale, `SetAsLastSibling`, `carousel.enabled = false` first — see
`CardCarouselController`'s gotcha above), deducts the same 1-coin cost `CharacterSwapUI` used to
(free if the player has 0), calls `CharacterManager.SwapCharacter`, then auto-closes back to
Pause. Tab still toggles it too, via the same `InputController.OnSwapMenuToggleInput` event
`CharacterSwapUI` used — nothing else needed to change to preserve that shortcut.

### Level Select (`Scripts/UI/LevelSelectController.cs`, `CardCarouselController.cs`, `LevelTileController.cs`, `LockedHintPanel.cs`, `Scripts/Utilities/UnlockProgression.cs`)

Reached from Main Menu's Play button. Two states on one screen (`LevelSelectScreen`):

- **World select** — a horizontally flickable carousel (`CardCarouselController`, see below) of
  world badges, one per currently-unlocked world (`LevelSelectController.IsWorldAvailable`: world
  0/Corn Field always available, world N unlocks once the last level of world N-1 has 2+ stars —
  the same threshold `UnlockProgression` gates level access on, so a badge is never shown for a
  world whose levels are actually still locked). Each badge is a single self-contained sprite
  (`CornfieldSign`/`VegetablePatchSign`/`OrchardSign`/`WheatfieldSign.png` — shield shape, rope
  ties, and the world's name all baked into one image) set via
  `LevelSelectController.worldSignSprites[world]` — no separate background+text-overlay
  composition. `LevelSelectController.LockedWorldTint` (multiply-tint on a locked badge) was
  lightened from `(0.35,0.35,0.35)` to `(0.65,0.65,0.65)` per feedback that the original read as
  almost black — locked worlds should still be clearly visible, just dimmed. Flicking cycles which
  badge is centred (full scale); tapping the already-centred badge shrinks-and-fades it in place
  while a small persistent `CurrentWorldIndicator` badge (same sprite, same convention) fades in
  top-left of the screen, then reveals that world's tile grid (`LevelSelectController.RevealWorld`).
  Tapping the indicator again returns to world select. **`LevelSelectScreen` deliberately has no
  top-left `LogoImage`** (unlike Settings/Pause/Choose Character/Level Complete) — one was added in
  an earlier pass without noticing it shares the exact same anchor/inset as `CurrentWorldIndicator`
  and clashes with it; removed again.
- **Tile grid** — a 4-column `GridLayoutGroup` (cell size `128x128`, shrunk from `150x150`,
  `padding.top = 16` for clear space below the header banner; one `LevelTileController` per level in
  that world, `UnlockProgression.LevelsPerWorld` = 25 per world) inside a vertical `ScrollRect`
  whose own top offset was later corrected from `200px` to `420px` — `TitleImage` ("SELECT LEVEL")
  is anchored 40px down with a 320px height, so its real bottom edge sits at `360px` from the
  screen top, 160px past where the old 200px reserve let scroll content start; the grid's first
  row was rendering crowded right against/under the banner as a result. 420px clears it with
  ~60px of breathing room. Auto-scrolled to centre the highest-unlocked level on open — **except**
  when arriving via `OpenLevelSelectForLevel` (Level Complete's Play button, see below), which
  instead snaps to the very top of the world's grid (`ScrollToTop`, no tween) rather than centring
  the newly-unlocked level. Centring the just-unlocked level on every return from Level Complete
  meant the scroll position moved further down every time a level was finished, which read as an
  unexpected/unpredictable downward jump; a normal world-badge tap still centres on the current
  level as before (`_scrollToTopOnNextReveal`, a one-shot flag consumed by `RevealWorld`, is what
  distinguishes the two call paths).

  **The grid used to be effectively unscrollable past its first ~1 row** ("shoots back", can't
  reach lower-numbered — i.e. more recently unlocked — levels): the grid's own container GameObject
  (`LevelSelectController.PopulateLevelGrid`'s `section`) used to rely on an explicit
  `LayoutElement` with a computed `preferredWidth`/`preferredHeight` (rows × cellSize + spacing +
  padding), on the assumption that the parent `Content`'s `VerticalLayoutGroup` — which has
  `childControlHeight/Width = false` (see `CreateVerticalScrollView`) — would "read this section's
  size directly" from that `LayoutElement` instead of forcing one. That assumption was wrong:
  Unity's `HorizontalOrVerticalLayoutGroup.GetChildSizes`, when `controlSize` (i.e.
  `childControlHeight`) is false, reads the child's raw `RectTransform.sizeDelta` directly and
  never calls `LayoutUtility.GetPreferredSize` at all — so the `LayoutElement` was silently
  ignored, and `section`'s `sizeDelta` stayed at Unity's GameObject-creation default (100×100)
  forever. `Content`'s `ContentSizeFitter` therefore only ever grew tall enough for a 100px-tall
  child regardless of how many rows of tiles were actually inside it — confirmed via an Edit-mode
  diagnostic (`SceneCleanupBuilder.DiagnoseLevelSelectScrollRange`, which calls `PopulateLevelGrid`
  directly via reflection and measures `Content` vs `Viewport` height with no Play mode needed —
  headless Play mode reliably hangs at Play-mode entry in this environment, before any game code
  runs, so this sidesteps that entirely). Fixed by setting `section.sizeDelta` directly instead of
  relying on a `LayoutElement` a `childControlHeight=false` parent will never actually read. Tile
  sprites are fully state-driven
  (`LevelTileController.spriteLocked/spriteUnlocked/sprite1Star/sprite2Stars/sprite3Stars`, wired
  from `LevelTile_Locked/unlocked-notplayed/1Star/2Stars/3Stars.png`) — no code changes needed
  when new levels are authored, a slot with no real `LevelData` just renders locked. Tapping a
  locked tile shows `LockedHintPanel` (a 2s auto-dismissing toast, `UnlockProgression.
  GetUnlockHint`); tapping an unlocked tile calls `GameManager.LoadLevel` +
  `SceneTransitionManager.ShowOnly(gameplayScreen)` directly, no Matchup-screen revival (see
  "Removed: Matchup screen") and no World Map step in between (see "Removed: World Map screen").

**`CardCarouselController`** (generic, also reused by `ChooseCharacterScreen` — see below) drives
any horizontal "front card enlarged" picker: a continuous float offset (in item-index units) maps
to each item's `anchoredPosition`/`localScale` every frame (centred item at full scale, items
further away shrunk). Items are arranged along a **true circular arc**, not a flat linear x-offset
with a separate y-dip bolted on (the original approach, which read as "linear" per feedback) —
`itemSpacing` is the arc-length distance between adjacent items, converted to an angle via
`itemSpacing / arcRadius`, then `x = arcRadius * sin(angle)`, `y = -arcRadius * (1 - cos(angle))`
gives both the horizontal spread and the downward dip from a single circle, so items curve away
and dip down together rather than sliding on a straight line. `arcRadius` defaults to `2800` (tuned
for Level Select's large `810x855` world badges, with `itemSpacing = 600` — tightened from an
original `730` [a ~32px non-overlapping gap] per feedback that badges still read as too far apart;
at 600 adjacent badges' edges overlap by roughly 97px, drawn correctly since `CardCarouselController`
already draws the nearest-to-centre item last/on top); `ChooseCharacterScreen`'s much smaller
cards override both (`itemSpacing = 220`, `arcRadius = 900`) so the curve reads clearly at that
scale instead of nearly flat. Dragging moves the offset directly; releasing snaps to
the nearest integer index. Tapping the already-centred item invokes the owning screen's selection
callback; tapping any other visible item just re-centres the carousel on it first, so a stray tap
mid-flick can't accidentally commit to the wrong item. The owning screen instantiates/destroys the
item GameObjects itself (`SetItems`/`ClearItems`) — the carousel only ever positions whatever
RectTransforms it's handed. **Gotcha:** the carousel's own `Update()` re-applies every item's scale
from its distance-to-centre every frame — a caller that also wants to scale/shrink an item via its
own coroutine (`LevelSelectController.RevealWorld`'s shrink-to-indicator, `ChooseCharacterScreen.
SelectRoutine`'s pop-scale) **must** set `carousel.enabled = false` first, or the carousel's
per-frame layout pass fights the coroutine for the same `RectTransform.localScale` and the
animation never visibly happens. Re-enabled at the top of the next `ShowWorldSelect`/`Refresh`
call, not inside the animation routine itself, since the screen closes/re-populates right after
either way.

**Daily Challenge** (`DailyChallengeManager`): today's `ChallengeType` is seeded from
`DateTime.UtcNow` (`"yyyy-MM-dd").GetHashCode()`), so every player sees the same challenge on a
given day. "Modified maze layout" per the GDD is content-authoring scope, not engineering scope —
it reuses `LevelData` index `DailyChallengeLevelIndex` (0) and overlays a rule/objective rather
than generating a distinct maze. `CheckCompletionOnLevelEnd` (called from `GameManager.EndLevel`)
only actually awards anything when playing that specific level index and the objective's met —
`CharacterLocked`'s "only certain characters allowed" is checked after the fact
(`ComboSystem.DistinctCharactersUsedCount <= 1`), not enforced by blocking the swap UI during play;
a stricter version would need `CharacterManager.CanSwapTo` to know about the active challenge.

**Leaderboards are local-only** (`LeaderboardManager`, per spec — "cloud sync in Phase 6"),
reading/writing through `SaveManager`'s per-level best score/time (`GetLevelBestScore`/
`GetLevelBestTime`, both already max/min-tracked there) and a few overall rollups
(`GetTotalCombosTriggered`, `GetCharactersMasteredCount` — the latter approximated as "unlocked
count" since the GDD text available to this phase doesn't define "mastered" any more precisely).

**`AudioManager`** now has real clips wired (see "Art status") — `PlayMusic`
crossfades between two looping `AudioSource`s, `PlaySFX` round-robins a pooled array via
`PlayOneShot`, both respect `SaveManager.MusicOn/SfxOn/MusicVolume/SfxVolume`. Wire remaining
`AudioClip`s into `CharacterData`/`LevelData`/`RobotData`/UI prefabs and call these same methods
once art/audio lands — nothing here should need to change shape.

### TextMeshPro bootstrap

No `com.unity.textmeshpro` package reference exists in `Packages/manifest.json` — in this Unity
version TMP ships bundled directly inside `com.unity.ugui@2.5.0` (`TMPro.TMP_Text` etc. live under
that package's `Runtime/TMP`). Originally its essential font/settings (normally brought in via
**Window > TextMeshPro > Import TMP Essential Resources**) hadn't been imported into `Assets`, so
`Phase5ProjectBuilder.EnsureTMPEssentials()` found the one SDF font asset available at the time —
bundled as URP samples under `Library/PackageCache/com.unity.render-pipelines.core@.../Samples~/
Common/TextMesh Pro/` — and copied `TMP Settings.asset` + `Fonts & Materials/` into
`Assets/Resources/` and `Assets/TextMesh Pro/Resources/`, pointing `TMP_Settings.defaultFontAsset`
at that copied `Inter-Regular SDF.asset`.

**The standard Essential Resources package has since actually been imported** (via the real
Editor menu), which also pulled in the proper `TMP_SDF*.shader` files and a correctly-configured
`LiberationSans SDF` font/material under `Assets/TextMesh Pro/`. `TMP_Settings.defaultFontAsset`
in **both** copies of `TMP Settings.asset` now points at `LiberationSans SDF`, not
`Inter-Regular SDF` — do not point it back. `Inter-Regular SDF.asset`'s embedded default material
has its shader set to `SamplesLit_Inter.shadergraph` (a demo "Lit" shader bundled with the URP
samples this font was copied from), which is missing the `_Stencil` property TMP's UI masking
needs — using it throws `Material ... doesn't have _Stencil property` at runtime. Don't repair
that material or repoint anything at it; use `LiberationSans SDF` for anything needing a default
font, and only give a `TextMeshProUGUI` an explicit custom font if it has a real, correctly-shaded
SDF material of its own.

**Known footgun:** there are two separate `TMP Settings.asset` files — one under `Assets/
Resources/` (from the original custom bootstrap) and one under `Assets/TextMesh Pro/Resources/`
(from the standard import) — both named identically and both living under a folder literally
named `Resources`, so `Resources.Load<TMP_Settings>("TMP Settings")` can return either one
depending on Unity's internal resolution order. If they ever drift out of sync (e.g. one gets
reimported/overwritten and the other doesn't), you'll see nondeterministic TMP behaviour —
verify **both** files agree (same `assetVersion`, same `m_defaultFontAsset`) any time TMP
essentials get touched again, rather than assuming a fix to one file is sufficient.

`EnsureTMPEssentials()` still runs once per `BuildAll` (checks whether `Assets/Resources/
TMP Settings.asset` already exists first) and is safe to re-run, but since the real Essential
Resources package is now present, its practical effect going forward is a no-op.

### Editor tooling (`Scripts/Editor`)

All ProjectBuilders are safe to re-run and use `[MenuItem]` entries under **Farm Fury Arcade**.
`UIBuilderHelpers` (not a builder itself) is a shared static toolkit `Phase5ProjectBuilder` uses
for programmatic uGUI construction (panels, buttons, text, toggles, sliders, a horizontal
`ScrollRect`, a 3-star row) — there's no visual Editor access in this Claude Code session, so
every screen is built via `AddComponent` calls the same way Phase 1-4's prefabs are, and it leans
on `VerticalLayoutGroup`/`HorizontalLayoutGroup`/`ContentSizeFitter` auto-sizing rather than
hand-computed `RectTransform` offsets everywhere, to keep ~10 screens' worth of construction code
tractable — correctness and scaling over pixel-perfect placement, the same trade-off every earlier
phase made for art (solid-colour placeholders instead of real sprites).

- **`Phase1ProjectBuilder`** (`Phase 1 > Build Game Scene (resets to Phase 1 baseline)`) — builds
  `Game.unity` from scratch: camera, Global Light 2D, Canvas/EventSystem, content parents, and a
  bare `GameManagers` (`GameManager`/`DataManager`/`SaveManager`/`SceneController` only — **no**
  `ScoreManager`/`TileMapRenderer`/`InputController`, no Cluck prefab wiring). **This is
  destructive** to any Phase 2+ content already in the scene — it now shows a confirmation dialog
  if it detects `ScoreManager` already present, but don't run it casually. Also has
  `Phase 1 > Run Play Mode Verification`, a batch-mode-friendly helper that opens the scene,
  runs Play mode for a few seconds (enough for `Phase1Test`/`Phase2Test` to finish their
  coroutines), then exits Play mode and the Editor — used for headless verification via
  `-executeMethod ... -logFile ...` (no `-quit`, since Play mode needs the Editor's update loop).
- **`Phase2ProjectBuilder`** (`Phase 2 > Build All`) — builds all placeholder prefabs (Cluck,
  walls, ground, crops, power pellet, warp tunnel), regenerates `LevelData_01` as a full
  procedural **12×9** maze (this game's fixed, hardcoded maze size for every level — not a
  per-level choice; see "Camera" above for why resizing it never requires retuning the camera),
  creates `CharacterData_Cluck`, and rewires `Game.unity` with
  `ScoreManager`/`TileMapRenderer`/`InputController` plus the Cluck prefab reference.
  **Idempotent** — safe to re-run after any prefab/data change instead of touching the scene by
  hand. **`LevelData_01`'s maze is now a fixed, hand-authored 12×9 layout, not procedurally
  generated at all.** `BuildLevelData01` parses a hardcoded array of row-strings (`Phase2ProjectBuilder
  .Rows`, one tile-id digit per character, top row first) directly into the grid via `ParseRows` —
  no RNG, no carving algorithm. Player start, the robot factory box, and warp-tunnel rows are no
  longer separate hardcoded coordinate constants either — `BuildLevelData01` scans the parsed grid
  itself for tile ids 5 (warp)/6 (factory)/7 (player start) and derives `playerStartPosition`,
  `robotFactoryPosition` (the factory tiles' own centre — currently a single cell at (6,5)), and
  `warpTunnelRows` from whatever's actually painted, so none of it needs to be kept in sync by hand
  when the maze changes. `Phase3ProjectBuilder.UpdateLevelData01Robots` reads `robotFactoryPosition`
  the same way (no hardcoded spawn coordinate of its own) for the same reason.

  **Why hand-authored instead of procedural:** two separate procedural approaches were tried and
  both produced technically-valid-but-bad-looking mazes. First, a recursive-backtracker carved only
  the left half and mirrored it onto the right half — a real bug, not a style choice: every carved
  room column is unconditionally open, so the mirror seam placed two always-open columns directly
  adjacent with no wall between them, producing a permanent 2-tile-wide corridor and reading as
  "wide open, not a real maze." Fixed by carving the full width with no mirror — but that surfaced a
  second problem: the fixed seed it happened to use produced a technically-valid spanning tree that
  *still* read as open floor (an entire row connected via all 4 of its horizontal connectors at
  once, cells next to the factory box left open on multiple sides and merging into it). **The
  correct verification metric is NOT "longest unbroken run of open cells in a row/column"** — a
  long single-tile-wide corridor spanning a full row/column is completely normal (real Pac-Man
  mazes have those); the actual defect is any passage more than 1 tile wide in BOTH dimensions at
  once, i.e. a fully-open 2x2 block of cells outside a deliberate room like the factory box. A
  best-of-200k-seeds search against that corrected metric did produce a genuinely good maze — but
  by that point hand authorship had already proven better for getting the *exact* intended shape
  (rather than "structurally valid, closest match to a scored heuristic"), so the project moved to
  hand design entirely rather than keep tuning the generator.

  **How to design/replace `LevelData_01`'s maze:** use `Tools/maze-designer.html` (a self-contained
  static web page, not a Unity asset — open it directly in a browser, no server needed). It's a
  click-to-paint grid editor using the exact same tile-id convention this project's maze data uses
  (0 ground, 1 wall, 2 crop, 3 vegetable, 4 pellet, 5 warp edge, 6 factory, 7 player start, 8
  water), with a live-updating export panel (a `WIDTH=`/`HEIGHT=`/`FLAT=` text block) you copy and
  hand to Claude Code to paste directly into `Phase2ProjectBuilder.Rows`/`ParseRows` — no manual
  transcription, no risk of a wrong-cell guess. `TileMapRenderer.ConfigurePelletTier` caps the whole
  maze to at most 1 "rare" (non-Sunflower) power pellet regardless of how many id-4 tiles are
  painted (`_rarePelletsSpawned`, reset per `RenderMaze` call — any tier roll beyond the first rare
  one falls back to Sunflower). `width`/`height` went 28×31 → 14×16 → 12×9 across earlier passes for
  two different reasons: the first halving doubled tile size to keep the board's total footprint
  the same (tiles at 28×31 read as too small); the second (12×9, the current fixed size) deliberately
  did **not** compensate by enlarging tiles — the board is physically smaller on screen on purpose,
  showing more `GameplayBackdrop` art around it. `SceneCleanupBuilder.FitGameplayCameraToMaze`'s
  orthographic size (`8`) is a stale Editor-time-only initial value — `CameraFollow
  .ApplyOrthographicSizeForAspect` overrides it every frame at runtime regardless (see "Camera"
  above), so this constant doesn't need to track the maze's actual size.
- **`Phase3ProjectBuilder`** (`Phase 3 > Build All`) — the one you actually want day to day now.
  Builds the 6 robot prefabs + `RobotData` assets, adds `PlayerHealth` to the existing Cluck
  prefab, gives `LevelData_01` its 2 spec'd robot spawns (Harvester@2s, Scout@6s, both at
  `robotFactoryPosition` — read from the maze, not hardcoded, see above), creates
  `LevelData_RobotTest` (levelNumber -1, out of Level Select's visible range — a smaller 20×20
  maze with 3 robots: Harvester/Scout/Patrol) for isolated multi-robot testing, assigns
  `LevelData_05`'s (levelNumber 4, a real player-facing level — see below) robot spawns via the
  same difficulty curve every other real level gets, and wires `RobotSpawner`/
  `PowerPelletManager`/`ChaseScoreManager` onto `GameManagers`. Also disables `Phase1Test`'s and
  `Phase2Test`'s `runOnStart` (see below for why). **Idempotent** — safe to re-run. Depends on
  Phase 2's prefabs/`LevelData_01` already existing.
- **`Phase4ProjectBuilder`** (`Phase 4 > Build All`) — the one you actually want day to day now.
  Builds `CharacterData` for all 8 characters, adds `CharacterBase`+`EggDropAbility` to the
  existing Cluck prefab, builds the 7 remaining character prefabs plus every ability's sub-prefab
  (`Egg`, `Shockwave`, `BounceTrail`, `WoollyClone`, `WaterTile` — the `WaterTile` prefab is built
  but no longer stamped onto `LevelData_01`; `UpdateLevelData01Water()` still exists but is no
  longer called — the water gate it used to add rendered as a plain blue placeholder square, since
  no real water art was ever uploaded, and read as an invisible wall/bug rather than a Ducky-only
  crossing), and wires `CharacterManager`/`ComboSystem`/`UnlockManager`/`CameraShake` into `Game.unity`
  (`ChooseCharacterScreen`, the Phase 5 replacement for Phase 4's `CharacterSwapUI`, is wired by
  `Phase5ProjectBuilder` instead). Also disables `Phase3Test`'s `runOnStart` (see below).
  **Idempotent** — safe to re-run. Depends on Phase 2 (Cluck prefab, `LevelData_01`).
  **Gotcha this phase actually hit:** setting fields via `SerializedObject` directly on the
  `GameObject` `PrefabUtility.SaveAsPrefabAsset` just returned does **not** reliably persist in
  this Unity version — 7 of 8 character prefabs came out with unset ability references and the
  default `totalCooldown` despite the code visibly running against them. Fixed by round-tripping
  through `PrefabUtility.LoadPrefabContents` → modify → `SaveAsPrefabAsset` →
  `UnloadPrefabContents` instead (same pattern `AddCharacterBaseAndAbilityToCluck` already used
  successfully). If a future prefab-field edit silently doesn't stick, check this first.
- **`Phase5ProjectBuilder`** (`Phase 5 > Build All`) — the one you actually want day to day now.
  Runs `EnsureTMPEssentials()` first (see above), then builds every UI screen under `Canvas`
  (Main Menu, Gameplay HUD + combo banner, Pause, Settings, Store "coming soon", Level Complete +
  New Character Unlock, Level Failed, Character Roster + `RosterCard` prefab, Leaderboards, Level
  Select), wires `SceneTransitionManager`/`AudioManager`/
  `DailyChallengeManager`/`LeaderboardManager` onto `GameManagers`, and disables `Phase4Test`'s
  `runOnStart`. Rebuilds the whole UI hierarchy from scratch every run (`RemoveExistingUIScreens`
  destroys everything under `Canvas` first) rather than trying to patch an existing one — diffing
  hand-built vs. previous-run hierarchies isn't practical. **Idempotent** in the sense that
  re-running always produces the same result, but NOT incremental. Depends on Phase 1
  (`Canvas`/`EventSystem`/`GameManagers`) and Phase 2 (`InputController`).
  **Two real gotchas this phase hit, both in scripts, not the editor tool itself** (see the
  matching commit history / `PowerPelletManager`-era section above for the Phase 4 prefab-field
  one): (1) a `button.onClick.AddListener(...)` call made directly from editor-script code (as
  opposed to inside a `MonoBehaviour.Awake()`) does not survive a scene save/reload — UnityEvent's
  non-persistent listeners aren't serialized — so even a single-button overlay needs a real
  component (`SimpleClosePanel`) wiring its listener in `Awake()`. (2) `GameObject.Find` only
  searches **active** GameObjects; since most screens are inactive most of the time by design
  (that's the whole point of `ShowOnly`), `Phase5Test` had to look screens up via
  `canvasTransform.Find(name)` instead, which works on inactive children.
- **`ArtWiringBuilder`** (`Farm Fury Arcade > Wire Uploaded Art`) — not a PhaseNProjectBuilder and
  not meant to be part of a "rebuild everything" workflow. Wires whatever's currently under
  `Assets/_Project/Sprites/...` into the specific prefab/UI fields listed in "Art status" above
  (characters, robots, crop/pellet prefabs, maze wall/ground/warp-tunnel prefabs,
  `TileMapRenderer`'s 3 pellet-tier sprites, a `GameplayBackdrop` SpriteRenderer behind the maze,
  `CharacterData`/`RobotData.portraitSprite`, UI screen backgrounds/buttons, `GameplayHUD`'s
  sound-icon sprites, the Level Complete coin icon) — it does
  not regenerate prefabs or screens from scratch, only sets sprite references (and configures each
  new texture's import settings — Sprite type, `spritePixelsPerUnit` = texture width, alpha
  transparency — the first time it sees a path). Safe to re-run; re-running with the same art
  already wired is a no-op except where it intentionally always re-applies (e.g. sprite
  references). No longer has a "Reposition Main Menu Buttons" entry — Main Menu has no `Content`
  group to reposition since the landing-page cleanup (see "Landing/Gameplay-HUD cleanup"
  above); re-run `Phase5ProjectBuilder.BuildAll` if Main Menu ever needs rebuilding from scratch.
  Also no longer wires `matchup.png`/its buttons (see "Removed: Matchup screen").
- **`SceneCleanupBuilder`** (`Farm Fury Arcade > Disable Debug Test Overlays` /
  `Farm Fury Arcade > Fit Gameplay Camera To Maze` / `Farm Fury Arcade > Debug > Reset All
  Progress (Testing)` / `Farm Fury Arcade > Wire AdManager Config`) — small targeted scene-hygiene
  fixes that are neither "wire art" nor "rebuild a phase's content." `DisableDebugTestOverlays`
  deactivates (and de-duplicates) the 5 `Phase*Test` GameObjects; `FitGameplayCameraToMaze` (renamed
  from `ZoomInGameplayCamera` — it now does the opposite) sets the Main Camera's `orthographicSize`
  to `8` so the whole board fits on screen and ensures a `CameraFollow` component exists.
  `ResetAllProgressForTesting` wipes every level/world/character-unlock PlayerPrefs key via
  `SaveManager.ResetAllProgressKeys()` — a static method split out of the existing (instance)
  `ResetAllProgress` specifically so it can run from Edit mode with no live `SaveManager` instance
  (`Singleton<T>` only ever assigns `Instance` from a real scene `Awake()`, so the instance method
  needs Play mode first — this static half only touches `PlayerPrefs`, nothing instance-state-
  dependent). `WireAdManagerConfig` sets `AdManager`'s LevelPlay app-key/placement-ID fields on the
  scene's `AdManager` component (see "Ad mediation" above) — only overwrites a field when a
  non-empty value is passed in, so it's safe to re-run as new platform values arrive piecemeal
  without clobbering ones already set. All four entry points are safe to re-run.

`Phase1Test.cs`/`Phase2Test.cs`/`Phase3Test.cs`/`Phase4Test.cs`/`Phase5Test.cs` (`Scripts/Core`) are verification
harnesses, not gameplay — each auto-runs a battery of checks on `Start()` and logs `PASS`/`FAIL`/
`INFO`/`SKIP`/`WARN`, plus an `OnGUI` debug overlay with manual buttons for interactive testing.
Safe to delete once a real UI/test framework exists. **Only the newest test's `runOnStart` is
left enabled** — each phase's builder disables the previous one's when it wires the scene,
because they all independently call `GameManager.Instance.LoadLevel(0)` in `Start()`, which
destroys and recreates the Cluck GameObject; with more than one auto-running, their coroutines
race on that reload and end up sharing/losing track of the player instance mid-test (first
surfaced as a flaky wrong-respawn-position failure once `Phase3Test`'s longer, more state-heavy
battery was added — see `Phase3ProjectBuilder`'s `DisableRunOnStart`). Each new test's own run
already exercises the previous phases' functionality as a side effect. Re-enable `runOnStart` on
an older test manually (or use its `ContextMenu`/`OnGUI` button) if you need to isolate a
regression in an earlier phase.

**`Phase5Test.runOnStart` is currently disabled** (`false` in the scene, not the phase-builder
default) — it was found auto-running concurrently with a manual Play-mode session and racing the
same `GameManager`/`CharacterManager`/`SceneController` singletons the same way two auto-running
tests race each other (see above), corrupting maze/character state that looked like a rendering
bug but wasn't. Re-enable it (Inspector checkbox on the `Phase5Test` GameObject, or its
`ContextMenu`) only when you specifically want the automated battery to run — never while also
manually playing through the game in the same session.

**Batch-mode timing gotcha (Phase 5 onward):** Play mode's first few frames in batch mode can
coincide with Unity's own one-time asset-indexing startup work, which stalls `Update()` ticks for
several real wall-clock seconds — verified via temporary logging, a nominally-instant ~0.5s screen
fade took ~4s real time the *first* time a transition ran, then ~0.5s for every one after. That's
a batch-mode Editor artifact, not something a real player's session hits, so `Phase5Test` polls
`SceneTransitionManager.IsTransitioning`/`GameManager.CurrentState`/a target screen's
`activeSelf` to completion (bounded by a generous timeout) instead of guessing a fixed wait —
prefer that pattern over a fixed `WaitForSecondsRealtime` for any future timing-sensitive check.

**Phase 3 verification specifics:** `Phase3Test` covers spawn timing/delay, Harvester pursuit
(logged as `INFO` not `FAIL` if distance doesn't shrink in the 1s sample window — it can still be
mid-"exit the factory upward" per spec, which briefly increases distance before it turns toward
the player), Chase-state contact killing the player, death/respawn (position reset, robots reset
to factory, score preserved), power-pellet-driven Vulnerable state, hit-defeats-a-1-health-robot,
the 200/400/800/1600(+5000) chain scoring sequence, power-state auto-expiry, and the second
robot's delayed spawn. It uses short custom power-pellet durations (1.5–1.6s) rather than the
real 8s Sunflower value to keep the whole battery inside the batch-mode Play window. The full 20s
Chase↔Scatter cycle isn't practical to assert there either — logged as `INFO`, same convention as
`Phase2Test`'s reversal-timing check.

**Phase 4 verification specifics:** `Phase4Test` drives most checks by calling
`TryActivate()`/`SwapCharacter()` directly rather than waiting on real input or full cooldowns —
Egg Drop spawns 3 eggs and starts its cooldown, a robot walking onto an egg gets stunned, swapping
to Bessie changes `CharacterBase.CharacterType`/`GridMovement.Speed`, Ground Slam stuns a robot
teleported into radius, `UnlockManager.CheckUnlocksOnLevelComplete(4)` unlocks Percy (asserts only
the post-call state, not `before == false`, since `SaveManager`'s real `PlayerPrefs` persist
across repeated verification runs — same caveat as `Phase1Test`'s coin round-trip), and a
Cluck → Woolly swap triggers Feather Storm, consumed by the next Triple Clone activation (2 clones
spawned, buff flag cleared). The only real-time wait is ~2.3s for Harvester's `spawnDelay`.

**Phase 5 verification specifics:** `Phase5Test` drives the flow via each controller's public
entry points and button `onClick.Invoke()` calls rather than simulating real clicks/taps,
verifying: only Main Menu active at startup; Main Menu → Level Select → Gameplay activates the
right screen at each step and reaches `GameState.Playing` (since both the Matchup screen's and
World Map's removal, this reproduces the "tap an unlocked tile" effect — `GameManager.LoadLevel` +
`SceneTransitionManager.ShowOnly` — directly rather than driving Level Select's own tile Button
deep inside its `ScrollRect`); the 7 required HUD elements exist (score/level/timer/portrait/
pause/sound/home — updated post-Phase-5 when Swap/Ability were removed from the HUD, see
"Landing/Gameplay-HUD cleanup" above; no on-screen ability-cooldown-ring assertion remains, since Space
still activates the ability directly but there's no HUD element left to watch); Pause freezes
`Time.timeScale` and shows/hides its overlay correctly; `GameManager.EndLevel(true)` produces a
well-formed `LevelResult` (1-3 stars, positive score/coins) and `LevelCompleteScreen` activates
automatically via `GameplayHUD`'s state-watch; `SaveManager.GetLevelBestScore` persists the result;
and toggling `SaveManager.MusicOn` round-trips correctly. See the batch-mode timing gotcha above
for why every wait is a poll, not a fixed duration.

## Art status

There's still no Kling AI / Suno / asset-import pipeline wired into this Claude Code session — art
gets dropped into `Assets/_Project/Sprites/{Characters,Robots,Environment,UI}` by hand and then
wired into existing prefabs/UI via `Editor/ArtWiringBuilder.cs` (`Farm Fury Arcade > Wire Uploaded
Art`), not generated by the tooling itself. Anything not yet wired still falls back to a
solid-color placeholder square generated at runtime by `Utilities/PlaceholderSprite`, using hex
values from the GDD's color palette where one exists (e.g. walls = Wall Brown `#4A2C1A`).

**Wired so far:**
- **Characters** — Cluck, Bessie, Woolly, Percy, and Gerald each have front/back/left art wired
  into their `CharacterData.walkAnimationFrames` (fixed order `[Up0,Up1,Down0,Down1,Left0,Left1,
  Right0,Right1]`). Cluck has real, correctly-wired Left AND Right art on both frames:
  `Cluck_left.png`/`Cluck_LeftWalk.png` for Left0/Left1, `Cluck_right.png`/`Cluck_rightwalk2.png`
  for Right0/Right1 (`Cluck_back.png` covers Up/Down, which have no dedicated art). **A previous
  pass wired only `Cluck_back.png` for every direction except Left1** despite the real per-direction
  files already existing on disk — she visibly faced backward while walking left/right until that
  was caught and fixed. `Cluck_rightwalk.png` (a third right-facing frame beyond the 2-frame
  Right0/Right1 slots this system supports) stays unreferenced, same "extra art, no slot for it yet"
  convention as elsewhere. `CharacterData.hasDedicatedRightArt = true` for her tells
  `CharacterAnimator` to skip its usual `flipX` mirroring of the Left sprite for Right-facing (every
  other character still has no dedicated Right art, so still gets mirrored). Every other character
  has only one pose per direction (no walk-cycle frames yet), so each direction's two slots repeat
  the same sprite — harmless no-op frame-toggle until second frames land for them too. Ducky has
  front/back only (no left/right art uploaded for her); her Left/Right slots fall back to the front
  sprite, so Left/Right facing won't read correctly for her until profile art exists — documented
  inline in `ArtWiringBuilder.SetWalkFrames`. Horace and Billy still have no art and remain
  solid-colour placeholders. `Gerald_effect.png` was uploaded but is unwired — `PuffUpAbility` has
  no spawned effect object (it just scales Gerald's own sprite 3x), unlike Bessie/Percy/Woolly's
  abilities, each of which spawns a dedicated effect prefab; wiring it in would mean adding a new
  prefab + a spawn call in `PuffUpAbility`, a gameplay change, not just art.
- **Robots** — `RobotVisual.SetDirectionalSprites` takes optional `left`/`right` sprites in addition
  to `front`/`back`. Patrol has a full 4-direction set. **Harvester, Scout, and Drifter now all have
  full 4-direction art** — Harvester gained real Left/Right (`HarvestorRobot_left/right.png`,
  previously front/back only), Scout gained a real Back (`ScoutRobot_back.png`, previously
  front/left/right only), and Drifter gained a real Back (`DriftRobot_back.png`, previously
  front/left/right only, with Up falling back to front). Heavy still has front/back only (no
  left/right — Left/Right both fall back to front, no mirroring). Drone has no art at all yet and keeps the
  colour-tint-only placeholder behaviour for its normal states.
- **Robot Defeated state** — all 6 robot prefabs (including Drone) have `RobotEyes.png` wired via
  `RobotVisual.SetDefeatedSprite`; while Defeated, `RobotVisual.Update`
  swaps to this sprite directly (skipping the old pale-tint placeholder) regardless of whether
  that robot has directional art. Vulnerable state is still colour-tint-only — no dedicated
  vulnerable sprite has been uploaded.
- **Tint-vs-real-art fix:** `RobotVisual` used to apply `normalColor` (the placeholder tint) as a
  multiply-colour on top of *any* sprite, including real uploaded art — harmless for Harvester
  (already reddish) but would have washed out Scout's pink/Patrol's cyan/etc. to whatever solid
  colour the placeholder used. Fixed via `BaseTintColor`: once a robot has real `frontSprite` art,
  the "normal" tint is `Color.white` instead of `normalColor`; robots with no art yet still tint
  the plain placeholder square as before. Same fix applies to the stun/knockback flash colour.
- **Pickups** — crop kernel uses `CornKernel.png`, the single vegetable pickup uses `carrot.png`
  (3 other vegetable sprites — cabbage/pumpkin/tomato — were uploaded but aren't wired in; the
  architecture only supports one vegetable sprite per maze right now).
- **Gameplay backdrop** — `World1_Cornfield.png` (swapped from `Wheatfield_background.png` per a
  gameplay review — `LevelData_01` is `MazeType.CornField`, so this ties the backdrop to the world
  it's actually set in; `Wheatfield_background.png` sat unused for a while as a result, until World
  4 (Wheat) got its own real levels and reused it as *its* `MazeArtSet.backdropSprite` instead —
  see the Orchard/Wheat bullet further down) is a
  `GameplayBackdrop` SpriteRenderer behind the maze (see "Camera" above) — fills the space around
  the 12×9 board, uniformly scaled (never stretched/zoomed) to cover whichever is bigger: the
  maze's own world footprint, or the camera's view width. Recenters/rescales itself off
  `LevelData_01`'s own `mazeWidth`/`mazeHeight` each time `ArtWiringBuilder.WireAll` runs, so it
  doesn't need manual retuning if the maze's dimensions ever change again.
- **Power pellets** — spawn with a real tier instead of always Sunflower. `TileMapRenderer.
  ConfigurePelletTier` rolls a weighted random tier per pellet (Sunflower 70% / GoldenWheat 20% /
  Rainbow 10%, matching the "RarePellets" art naming) and swaps in `sunflowerPelletSprite`
  (now `RarePellets_sunflower.png` — a dedicated sunflower sprite replaced the earlier placeholder
  use of `Power_1.png`, which is a Cluck power-up icon, not a pellet) / `goldenWheatPelletSprite`
  (`RarePellets_maize.png`) / `rainbowPelletSprite` (`RarePellets_apple.png`) accordingly. Only 1
  pellet per maze is ever allowed to roll a rare (non-Sunflower) tier — see the maze-generation note
  above. `Power_1.png`/`CluckPower_2.png`/`CluckPower_3.png` are no longer "Cluck power-up icons" —
  despite the naming (and despite being stored under `Sprites/Characters/`), they're actually
  Cluck's Egg Drop art: `Power_1.png` is the whole egg, `CluckPower_2.png`/`CluckPower_3.png` are
  its 2-frame crack/burst hit animation (see the Egg Drop bullet below). Collecting a GoldenWheat or Rainbow
  (i.e. non-Sunflower/"rare") pellet also spawns `PelletCollectBurst`
  (`Gameplay/PelletCollectBurst.cs`, wired via `PowerPelletPickup.collectEffectPrefab` and called
  from `CropCollector` right before the pellet is destroyed) — a procedural ring of
  placeholder-coloured squares that fly outward and fade, since no dedicated sparkle/particle art
  exists yet. Swap it for a real ParticleSystem/sprite-sheet burst once that art lands;
  `PelletCollectBurst.Configure(PowerPelletType)` is the only method a replacement needs to keep.
- **Ability effects** — `Shockwave` (Bessie's Ground Slam) uses `BessieSlam.png`. It used to play at
  a fixed placeholder duration/scale unrelated to the ability's real 2-tile radius/3s lingering
  killzone (see the "Every ability-created robot hazard" note further up); `ShockwaveEffect` now
  exposes `Configure(diameterWorldUnits, durationSeconds)`, called from `GroundSlamAbility.Execute`
  with the ability's actual radius (accounting for Double Slam's 4-tile buff) converted to world
  units and its real lingering duration, so the VFX genuinely represents what's happening
  underneath instead of a fixed placeholder unrelated to it. `BounceTrail`'s sprite
  (`Percy_effect.png`) is no longer instantiated as a separate trailing object for Percy's Bounce
  Roll — see the `BounceRollAbility` rework above; only its `SpriteRenderer.sprite` is read now, as
  the pose Percy's own sprite swaps to for the roll. `WoollyClone` (Woolly's Triple Clone) uses
  `Wooly_effect.png`. Cluck's Egg Drop (`Egg` prefab) went through several placeholder rounds before
  real art existed: a near-white/tan tint that blended into `CornTiles.png`'s warm ground art, a
  pure-white follow-up with still-too-little contrast, then a saturated hot-pink placeholder
  (`#FF1493`, `sortingOrder` raised above the character sprite's — the offset-0 egg spawns directly
  under Cluck's own feet, so it needs to draw on top of her to be visible there at all). **Now uses
  real art**: `Power_1.png` (the whole cracked egg) is the resting sprite, wired via
  `ArtWiringBuilder.WireEgg`. `EggHazard` also changed behaviour to match a request for a proper hit
  animation: it used to persist for its full `lifetimeSeconds` (15s) and be reusable — walked over
  by any number of robots without disappearing. Now it's one-shot: `OnTriggerEnter2D` disables its
  own collider immediately, plays `crackedSprite` (`CluckPower_2.png`) for 0.5s then `burstSprite`
  (`CluckPower_3.png`) for another 0.5s, then destroys itself (1s total) — `lifetimeSeconds` is now
  only a fallback auto-destroy for an egg nothing ever walks over, cut from 15s to 5s per feedback
  that an unhit egg was lingering far longer than it needed to. Stun duration on contact was
  cut from 3s to 1s, then raised to 5s per a later gameplay pass. `EggDropAbility` itself was
  simplified from a 3-egg trail behind Cluck (0/2/4 tiles) to a single egg at her current position
  the moment the ability activates.
- **UI backgrounds** — `MainMenuScreen` uses `landing.png` (which has "FARM FURY ARCADE" baked
  into the art). `LevelFailedScreen` was rebuilt to a 2026-08-01
  mockup: `Bg_LevelSelect.png` (night farm) root background instead of `LevelFailed.png` stretched
  full-screen, with `LevelFailed.png` moved onto an aspect-locked `PanelArt` child (the same
  square-art-on-landscape-overlay fix Pause/Level Complete already had — this was previously a known
  gap, now closed) and two real buttons, `Restart.png`/`Quit.png`, positioned in a fresh centred
  vertical stack (`LevelFailed.png` has no baked-in button rows to align to, unlike `Paused.png`).
  `Retry.png`/`Menu.png` (the old button art) are no longer referenced and have been deleted from
  disk. `LevelFailedController`'s fields were renamed to match (`retryButton`→`restartButton`,
  `menuButton`→`quitButton`, `mainMenuScreen`→`levelSelectScreen`) — Quit now returns to **Level
  Select**, not Main Menu, matching every other in-gameplay "back" action's "one step back to where
  you picked this level from" convention. `LevelCompleteScreen`/`PauseOverlay`/`ChooseCharacterScreen`
  all use `World1_Cornfield.png` as their root background now (per the 2026-07-31 mockups — see their own
  bullets above), with `LevelComplete.png`/`Paused.png` as an aspect-locked `PanelArt` child on top
  for the first two; `Wheatfield_background.png` sat unused after this (it briefly stood in for
  these before real dedicated panel art existed) until World 4 (Wheat) reused it as its own
  gameplay backdrop once it got real levels — see the note two bullets up. `matchup.png` is also
  unused — left on disk,
  not deleted — after the Matchup screen's removal (see "Removed: Matchup screen"). Because
  `landing.png`'s logo sits centred in the upper half, `MainMenuScreen/Content` (the button stack)
  was re-anchored to the bottom of the screen (`anchorMin/Max = (0.5, 0)`, `pivot = (0.5, 0)`,
  `anchoredPosition = (0, 30)`) instead of screen-center, so it no longer overlaps the art's logo.
  `Logo.png` is wired as a small top-left `LogoImage` on every 2026-07-31-mockup screen except Level
  Select (Settings, Pause, Choose Character, Level Complete) — Level Select already has its own
  top-left identity element (`CurrentWorldIndicator`) at the exact same anchor/inset, so adding a
  separate Logo there just clashed with it (see the Level Select section above). `landing.png` still bakes its
  own logo into Main Menu directly, so Main Menu has no separate `LogoImage`. `LoadingScreen
  Background.png` is still uploaded but unwired (`ChooseCharacterScreen` used it briefly before
  switching to `World1_Cornfield.png`) — there's no dedicated loading screen in the current screen
  flow for it to go to.
- **`Paused.png` is on its own aspect-locked child, not the overlay root.** `Paused.png` is a
  square (2048x2048) parchment/frame card with its 5 button rows (Resume/Swap Character/Restart/
  Settings/Quit) baked directly into the art. It used to be set as `PauseOverlay`'s own root
  `Image`, which stretches full-screen — on a real landscape aspect that non-uniformly stretched
  the square art, squashing its baked-in rows together and dragging the separately-wired button
  art (`Resume.png` etc., positioned by hand-tuned fractions) out of alignment with them.
  `Phase5ProjectBuilder.BuildPauseMenu` parents a `PanelArt` child under the root, locked to a
  1:1 aspect via `AspectRatioFitter` (`FitInParent`) so it stays centred and undistorted at any
  screen aspect. All 5 buttons sit under `PanelArt` so their fractions stay aligned with it.
  `LevelCompleteScreen` was later given the identical `PanelArt` treatment for `LevelComplete.png`
  (see its own bullet below) once it hit the same square-art-on-landscape-overlay problem.
  **The root's own `Image` is no longer a plain black dim** — per a 2026-07-31 Canva mockup it's
  now `World1_Cornfield.png` (an opaque cornfield/barn/moon backdrop), so Pause fully replaces the
  view instead of dimming the gameplay maze behind it; `LogoImage` (`Logo.png`, top-left, same
  size/inset as every other mockup-driven screen) was added to match. Choose Character and Level
  Complete got the same `World1_Cornfield.png` root + `LogoImage` treatment per their own mockups
  (same session) — see their bullets below.
- **Settings is a 2x3 grid of whole-plaque toggle cells, not a vertical stack of rows.** Rebuilt to
  a 2026-07-31 Canva mockup: root background is `Bg_LevelSelect.png` (moon/windmill/barn — see the
  Level Select bullet below for where else this art is used), `Logo.png` top-left, `SettingsSign.png`
  as the header (replacing the old TMP "SETTINGS" text), a round `Btn_home.png` back button
  bottom-right (`CreateRoundBackButton` — see its own bullet below), and a `GridLayoutGroup` of
  `Btn_plaque.png` cells (Music/SFX/Vibration/Left-Handed/Language filled, the grid's 6th cell left
  empty). Each cell **is** its Toggle (`Phase5ProjectBuilder.CreateTogglePlaqueCell` — `Toggle` and
  `Image` on the same GameObject, `targetGraphic` = its own `Image`), not a separate small checkbox
  floating on top of a plaque — the whole cell is the tap target. Plaques were later enlarged 2x
  (`grid.cellSize` `210x60` → `420x120`, `spacing` `24x20` → `48x40`) per feedback that they read as
  too small — but each label's own text box is pinned at the **original** `210x60` size, centred
  inside the now-bigger cell, rather than stretched to fill it; the ask was "scale the plaque only,"
  and a stretched auto-sizing label would have grown the font along with the artwork. Music/SFX volume sliders were
  dropped entirely (a grid cell isn't large enough to host both a tap target and a drag target
  cleanly) — Music/SFX are now simple mute toggles like Vibration/Left-Handed; `SaveManager.
  MusicVolume`/`SfxVolume` still exist for whenever a volume control gets a dedicated slot again,
  only the in-panel UI for it is gone. Restore Progress/Reset Progress (and their confirm
  sub-panel) were removed entirely per the same pass — Restore was Phase 6/cloud-save scope with
  no real action, and Reset's confirm sub-panel went with it. Settings text uses the same
  `Bangers SDF` cartoon font Gameplay HUD's score/timer use (`ArtWiringBuilder.WireSettingsFont`),
  not TMP's default `LiberationSans SDF`.
- **Round back buttons vs. the generic bottom-left one.** Every screen with a back button used to
  use `Phase5ProjectBuilder.CreateGenericBackButton` (rectangular `Btn_back.png`, bottom-left,
  160x160, safe-area inset). Settings, Level Select, and Choose Character now each have their own
  Canva mockup that places a round icon there instead — `CreateRoundBackButton(screenRoot,
  bottomRight)` builds that variant (`Btn_home.png`, 160x160, same inset, either bottom-right
  [Settings, Level Select] or bottom-left [Choose Character, whose mockup's triangle/mountain icon
  has no matching uploaded asset — substituted with `Btn_home.png` for consistency with the other
  two rather than left unwired]). Character Roster and Leaderboards remain the only
  `CreateGenericBackButton` users left.
- **Level Select** — root background is `Bg_LevelSelect.png` (moon top-right, windmill bottom-
  right, barn bottom-left, rolling hills — also reused as-is for `SettingsOverlay`, see above).
  `Header` has no background art at all (`Color.clear`) — it's just a layout strip for
  `TitleImage`/`StarCounter`, so the night sky shows straight through; `Header_LevelSelect.png`
  (a constant that used to point at a file which was never actually uploaded) was removed rather
  than kept as a dead reference. `SelectLevelSign.png` is `TitleImage`'s word-art (replacing old
  TMP "SELECT LEVEL" text). The 4 world badges (`CornfieldSign`/`VegetablePatchSign`/`OrchardSign`/
  `WheatfieldSign.png` — note `CornfieldSign.png`'s on-disk filename has a lowercase "f", unlike
  the other three, since `AssetDatabase.LoadAssetAtPath` is case-sensitive regardless of the OS
  filesystem) are wired straight onto `LevelSelectController.worldSignSprites` — see the "Level
  Select" architecture section above for how the carousel/tile-grid consume them. The tile grid is
  4 columns (was 5) to match the same mockup. `Divider_WorldBanner.png` is now only used by the
  kept-but-unlinked `WorldDivider` prefab (see the Level Select architecture section) — it's no
  longer the world badges' background, since the `*Sign.png` files already bake in the full badge
  art (shield + rope + name text) with nothing separate needed on top.
- **Level Complete is stars + score only, not a full breakdown.** Root is `World1_Cornfield.png` +
  `Logo.png` top-left (same as Pause/Choose Character; inset widened 40→100 to clear the yellow
  safe-area guide, matching the fix already applied elsewhere), `LevelComplete.png` is an
  aspect-locked `PanelArt` child (same square-art-on-landscape-overlay fix Pause already had). The
  score text + star row (`ShelfContent`, a vertical group) live in the card art's actual blank
  middle area — not the low band near the horseshoe/star-rating frame decoration, which is where
  an earlier pass placed them by mistake (band was 0.06–0.30 up the card, overlapping that
  decoration; moved to 0.28–0.56, centred in the genuinely blank zone). Score font enlarged twice
  (34→52→66pt) after repeated feedback that it read as too small/low — see the "Score breakdown
  categories" note above for what got dropped from display (not from the underlying computation).
  **Score renders ABOVE the real `StarDisplay` row, not below it** — the vertical group order was
  originally Stars-then-Score, which put the real (interactive-fill) star row directly beneath the
  3 decorative stars already baked into `LevelComplete.png` just under its "LEVEL COMPLETE!"
  banner, reading as two overlapping/clashing star rows. Swapping the order so score sits closest
  to the baked stars, with the real stars below it, reads as two clearly separate elements instead.
  A single `Btn_skip.png` button (Level Select) briefly replaced the old Replay/Next Level/Home
  row, then was itself replaced by a 3-button Play/Home/Settings row, which was later **split**:
  Play now stands alone bottom-left (`AnchorBottomLeft`, matching the safe-area inset every other
  screen's back button uses), Home/Settings stay paired in a bottom-right `ActionButtons` group —
  both rows share the same 110px bottom inset (they'd drifted to 110 vs 70 when Play's own inset
  was deepened for safe-area clearance without the change being carried over to `ActionButtons`,
  leaving the two rows visibly misaligned) — see `LevelCompleteController`'s doc comment for what
  each does:
  - **Play** — calls `LevelSelectController.OpenLevelSelectForLevel(nextLevelIndex)` then shows
    Level Select, jumping straight to the tile grid for the world containing the level that was
    just unlocked (the same reveal animation a normal world-badge tap uses), rather than landing on
    world select and making the player navigate there. This is what actually exercises the unlock
    chain end to end after finishing a level.
  - **Home** — shows Level Select with no pending target, landing on world select as usual.
  - **Settings** — opens the shared `SettingsPanel` overlay (same as Main Menu/Pause).

  **Gotcha hit when Play was split out of `ActionButtons`:** `ArtWiringBuilder.WireButtons` still
  targeted the old path `LevelCompleteScreen/ActionButtons/PlayButton` — since `SetImageSprite`
  fails silently (just a console warning) rather than throwing, Play kept rendering as its
  placeholder solid-color square with no error surfaced anywhere obvious. Fixed by updating the
  path to `LevelCompleteScreen/PlayButton`; worth checking for this same silent-path-mismatch
  pattern any time a button gets reparented.
  The star row itself was a "brown boxes" bug caught in a screenshot review: `StarDisplay` used to
  tint a `CreateImage`-baked solid-color square via its own `.color`, double-applying color (0.35
  grey × gold ≈ dark olive, not clean gold). Fixed by giving `StarDisplay` real
  `ScoreStar.png`/`ClearStar.png` sprites (`filledStarSprite`/`emptyStarSprite`, wired by
  `ArtWiringBuilder.WireLevelCompleteStars`) with a procedural `PlaceholderSprite.GetStar()`
  (white, star-shaped alpha) fallback if that art's ever missing — `UIBuilderHelpers.
  CreateStarDisplay` builds each star Image directly now instead of going through `CreateImage`,
  for the same double-tint reason.
- **New Character Unlock overlay was fully rebuilt** to a Canva mockup — full-screen
  `World1_Cornfield.png` backdrop, `Logo.png` top-left, a `NewCharacter.png` wood-sign banner
  top-centre (a shared "New Character" banner reused for every animal, replacing an earlier generic
  `unlocked.png` placeholder), and the character's own `selectCardArt` (the same per-character
  framed card `ChooseCharacterScreen` uses, e.g. `Percy_Pig.png`) large and centred. That art
  already has the character's name baked in (confirmed by opening the file directly — the frame,
  portrait circle, *and* a "Percy" nameplate are all one image), so this screen needs no separate
  name/title/stats text at all — the old version's `bannerText`/`titleText`/`statsText`/
  `goldenParticlesPlaceholder` fields and their GameObjects are gone entirely, not just hidden.
  **Dismisses on tap now, not a fixed auto-dismiss timer** — `autoDismissSeconds` was replaced by a
  `tapButton` (a full-screen invisible `Button` on the overlay root, same convention
  `NewWorldUnlockScreen`'s own tap-gate already used) per feedback that a timed fade-out didn't give
  the player enough time to actually look at the reveal. **The card's own size was silently broken
  from the moment this screen shipped**: it's built via `CreateImage`, whose width/height arguments
  only set a `LayoutElement.preferredWidth/Height` — meaningless on `unlockRoot`, a plain
  `CreatePanel` with no `LayoutGroup` to read that hint, so the card's real `RectTransform.
  sizeDelta` silently stayed at Unity's default 100x100 no matter what size was requested (an
  earlier "550→850, it read as too small" resize pass changed nothing on screen for exactly this
  reason). Now explicitly set to 340x360, stretched-to-fill (not `preserveAspect`) to match
  `ChooseCharacterScreen`'s own `CardArt` exactly — this reused the same underlying bug/fix pattern
  found on `NewWorldUnlockScreen`'s badge, see that screen's own bullet further down. The card still
  fades+scales in on reveal (`cardRevealStartScale` 0.4→1, same "pop into view" convention
  `CharacterSelectCard`'s selection animation uses) — an even earlier
  version of this reveal used a Y-axis RectTransform rotation ("card flip"), which read as broken
  rather than 3D: this Canvas renders in `RenderMode.ScreenSpaceOverlay` (no perspective camera at
  all), so a rotated RectTransform is drawn via a flat orthographic squash with zero depth cue —
  for most of the rotation sweep the card was a razor-thin, unreadable sliver overlapping
  neighbouring UI. Scale has no equivalent degenerate mid-state.
- **Device-frame screenshot review pass (2026-08-01)**, following up on the 2026-07-31 mockups
  above with actual on-device sizing/positioning corrections, screen by screen:
  - **Settings** — title banner enlarged (~1.23x, `TitleImage`) but kept at its original top
    position; the 2x3 plaque grid shrunk (cells 420x150 → 210x60) with larger auto-sizing label
    text (20–56pt) so longer labels ("Left-Handed") still fit; `Logo`/`Btn_home` insets widened
    (40→100) to clear the yellow safe-area guide. `TMP_Dropdown.template` was never assigned on
    the Language cell, throwing on tap — `Phase5ProjectBuilder.CreateDropdownTemplate` builds the
    minimal required Template/Viewport/Content/Item hierarchy now.
  - **Level Select (world-select state)** — all 4 world badges are now always instantiated
    (`ShowWorldSelect`, previously skipped locked ones entirely); a locked badge gets
    `LevelSelectController.LockedWorldTint` (grey) and `Button.interactable = false` instead of
    being omitted. Badge size ~2.6x (340x360 → 897x950, `itemSpacing` scaled to match), carousel
    container re-centred to the screen's true vertical midpoint (symmetric 200px top/bottom
    margin, was an asymmetric -100 offset). `TitleImage` moved off the `Header` strip onto the
    screen root directly (matching Settings' title treatment) — remember this if adding new
    `Find("LevelSelectScreen/Header/...")` lookups, the title is no longer under `Header`.
  - **Level Select (tile-grid state)** — tile spacing widened (20→45px, cell size unchanged per
    explicit "don't resize the tiles" instruction) inside the same already-scrollable
    `ScrollRect`/`ContentSizeFitter`. `CurrentWorldIndicator` enlarged (220→340) and inset further
    from the corner. `BackButton` now toggles between `Btn_home` (world-select — leaves the
    screen) and `Btn_back` (a world's tiles showing — `OnBackButtonClicked` calls
    `ShowWorldSelect()` instead, going back one step rather than exiting) via
    `LevelSelectController.SetBackButtonSprite`. `StarCounter` (the "0 ★" that read as a stray
    clipped number top-right) and each tile's level-number text overlay (redundant with the tile
    art's own "?"/lock icon) were both removed outright, not just hidden.
  - **Pause** — `Logo` inset widened (40→100); all 5 button rects (Resume/Swap/Restart/Settings/
    Quit) widened slightly (~0.015 horiz/~0.004 vert as anchor fractions) so the button art fully
    covers Paused.png's baked-in row shapes instead of leaving a sliver visible. Quit now goes to
    Level Select, not Main Menu — `GameManager.QuitToMainMenu()` renamed to `QuitToLevelSelect()`
    (sets the previously-unused `GameState.LevelSelect` instead of `GameState.MainMenu`),
    `PauseMenuController` holds a `levelSelectScreen` reference instead of `mainMenuScreen`.
    A later pass confirmed Resume/Swap/Restart/Settings were correctly aligned and left them alone;
    only Quit was nudged down another ~0.01 (its own anchor fractions only) per feedback that it
    alone was sitting slightly high of its row.
  - **Character portrait + Pause button cluster** (Gameplay HUD) — `clusterInsetX` reduced
    (130→90) per feedback that the stack sat too far in from the corner/safe-area edge. In a later
    pass this cluster and the directional pad swapped screen sides entirely — cluster now
    bottom-right (`AnchorBottomRight`, `clusterInsetX` negative since that anchor's pivot sits at
    the parent's right edge — positive would push it further right/off-screen, same convention
    `CreateRoundBackButton` uses), D-pad now bottom-left (`AnchorBottomLeft`, positive
    `dpadInsetX`). The D-pad's own up/down/left/right sub-offsets from its centre point didn't need
    to change sign — those are plain screen-space deltas independent of which edge the centre point
    itself is anchored to. **Its overall footprint was later shrunk again** (`dpadSpacing` 100→70,
    `dpadButtonSize` 110→90) per feedback that it still overlapped playable maze tiles on some
    device aspects — the maze's own rendered area fills nearly the entire device safe-area guide on
    some aspects (see "Camera" earlier), so shrinking the D-pad's own footprint is the only lever
    available to reduce overlap without changing camera zoom/backdrop sizing; some overlap on
    certain aspects may remain a known limitation rather than something fully solvable this way.
  - **Choose Character** — found and fixed the actual bug behind a large yellow block covering the
    active/centred card: `ActiveHighlight` was a *child* of the same GameObject holding the card's
    own `Image` — in uGUI a child always renders in front of its own parent's Image regardless of
    sibling order, so the highlight (larger, 85%-opaque gold) always drew on top of, not behind,
    the card art. Fixed by moving the card art onto its own child (`CardArt`), leaving the root
    Image invisible/raycast-only, so `ActiveHighlight` (added first) genuinely sat behind it —
    **`ActiveHighlight` was later removed entirely** per further feedback that a yellow background
    behind the active card was still distracting even correctly layered; `CharacterSelectCard.
    activeHighlight` is left unassigned (null-safe) rather than stripped from the script.
    `itemSpacing` tightened twice (380→300→220, per-instance override, same pattern as Level
    Select's world carousel) and the carousel now moves on a circular arc, not a flat line (see
    `CardCarouselController`'s own section above) — `arcRadius` overridden to `900` (vs. Level
    Select's `2800` default) so the curve reads clearly at these smaller cards' scale. Back button
    inset nudged from `60` (was `100`) — the mockup sits this further left than the generic
    bottom-left inset other screens use. Locked characters now grey-tint (`CharacterSelectCard.
    LockedTint`, matching `LevelSelectController.LockedWorldTint`) in addition to the existing
    "LOCKED" label. Back button icon changed `Btn_home`→`Btn_back` (it returns to Pause, not a
    "home" destination). Selecting a character now auto-resumes gameplay directly
    (`GameManager.ResumeGame()`) instead of landing back on the Pause menu — the Back button still
    correctly returns to Pause (`ChooseCharacterScreen.Close()` reactivates `pauseMenuScreen` only
    if `GameManager.CurrentState == GameState.Paused`).
  - **`CreateRoundBackButton` had a sign bug**, found while fixing the above: its `bottomRight`
    branch reused `AnchorBottomLeft`'s positive-X-is-inward offset convention, but for a
    right-pivoted anchor a *positive* X pushes the element further right/off-screen — negative X
    moves it inward. This left `Btn_home` (Settings/Level Select), `WorldMapScreen/HomeButton`
    (World Map itself is since removed — see "Removed: World Map screen"), and
    `LevelCompleteScreen/SkipButton` all mostly clipped off the right edge (only ~60 of 160px
    on-screen). Fixed at all three call sites; `CreateRoundBackButton`'s `bottomRight` inset is now
    `-150` (a bit more breathing room than a bare sign-fix `-100` would give).
  - **Landing music no longer cuts to silence when Play is tapped.** `MainMenuController.OnDisable`
    used to call `AudioManager.StopMusic()` unconditionally — since Play now opens Level Select
    (not gameplay directly), this left Level Select browsing with no music at all until a level
    was actually chosen. `OnDisable` no longer stops music; the landing track keeps playing until
    `GameManager.LoadLevel`'s own `ResumeBackgroundMusic()` crossfade takes over.
  - **Cluck/Bessie walk-frame wiring corrected** against the actual uploaded art (`ArtWiringBuilder.
    WireCluck`/`WireBessie`): Cluck's Up/Down/Right directions and her idle pose all use
    `Cluck_back.png`, with `Cluck_LeftWalk.png` as Left1 only (no dedicated Up/Right art exists
    yet). Bessie's Right-walk frame order was reversed (`right2` plays first, then `right`) — every
    other Bessie mapping already matched.
- **Maze wall/ground/warp-tunnel tiles** — `Wall_CornField`/`Ground_CornField`/`WarpTunnel`
  prefabs (each instantiated per-cell by `TileMapRenderer` at scale `TileMapRenderer.CellSize`,
  same convention crops/pellets already used) now use `CornTiles.png`/`FloorTile.png`/
  `WarpTile.png` respectively instead of `PlaceholderSprite` colour squares — each uploaded file is
  a single complete tile image, not a tileset, so no atlas-slicing was needed.
- **Card frames** — `Card.png` wired onto the `RosterCard` prefab's root `Image`. The New Character
  Unlock screen no longer has a generic card frame of its own to wire — see its own rebuilt-screen
  bullet further down.
- **Character/robot portrait sprites** — `CharacterData.portraitSprite`/`RobotData.portraitSprite`
  (fields that existed since Phase 4/3 but were never populated) now get each character/robot's
  front sprite, wired alongside their walk-cycle/directional art in
  `ArtWiringBuilder.SetWalkFrames`/`WireCluck`/`WireHarvester`/`WireNewRobots` (via the new
  `SetRobotPortrait` helper). `GameplayHUD.RefreshPortrait` reads `CharacterData.portraitSprite`
  (via `DataManager.GetCharacterData(CharacterManager.Instance.ActiveCharacter)`) instead of just
  tinting the placeholder square with the active character's `SpriteRenderer.color` — the old
  version left a plain gold placeholder block on screen even after portrait art existed, since
  color tinting was never going to substitute for an actual portrait sprite. (The Matchup screen
  was an earlier consumer of these fields too, before its removal — see "Removed: Matchup screen".)
- **Buttons** — `Btn_play/pause/settings/quit/home/skip/back/plaque` wired onto their matching
  buttons across every screen (Main Menu, Gameplay HUD, Pause, Settings, Level Select,
  Store, Level Complete/Failed, Roster, Leaderboards) via `ArtWiringBuilder.WireButtons` —
  buttons with no specific icon art (Restart, Replay, Retry, Store, Leaderboards, Roster, Daily
  Challenge) share the generic `Btn_plaque.png` background. `Btn_nosound.png`
  is wired now too, as `GameplayHUD`'s `soundOffSprite` (paired with `Btn_music.png` as
  `soundOnSprite`) — the HUD's new `SoundButton` is the first place either sprite swaps at
  runtime; Settings' Music/SFX toggles don't use `Btn_music.png`/`Btn_nosound.png` at all anymore
  — see the Settings grid bullet above, each toggle's own `Btn_plaque.png` cell is the whole tap
  target, with no separate checkbox/checkmark icon on top.
- **App icon** — `AppIcon.png` set as the Unity Player Settings icon (`PlayerSettings.
  SetIconsForTargetGroup`, Standalone + the default/`Unknown` group) — a project-settings change,
  not a scene/prefab one.

**Percy now has full directional art**: real Left (`Flat1.png`→`Flat2.png` flick) and Right
(`Right1.png`→`Right2.png` flick) walk-cycle frames (`ArtWiringBuilder.WirePercy`,
`hasDedicatedRightArt = true`) — his earlier "Left" reference (`Perccy_left.png`) was a misspelled
filename that never actually existed on disk, so he had zero real directional art before this and
silently fell back to front/back for every direction. His Bounce Roll ability effect
(`Percy_effect.png`, wired onto `BounceTrail.prefab`) is also in now — Percy's art is complete.

**Level Complete's star row uses real star art** (`ScoreStar.png` filled / `ClearStar.png` empty,
`ArtWiringBuilder.WireLevelCompleteStars` → `StarDisplay.filledStarSprite`/`emptyStarSprite`) —
see the Level Complete bullet above for the "brown boxes" double-tint bug this replaced.

**Ducky's art is now complete**: real Left/Right walk frames (`Ducky_left.png`/`Ducky_right.png`,
`hasDedicatedRightArt = true`, wired via a dedicated `WireDucky()` — she previously had only
front/back, so Right silently mirrored Left) plus a Skip Shot departure-splash effect
(`Ducky_ability_left.png`/`Ducky_ability_right.png` on a new `DuckySplash.prefab`/
`DuckySplashEffect` — mirrored per the actual skip direction, chosen at runtime by
`SkipShotAbility.Execute` from the sign of the horizontal distance to her destination).

**Horace has his first real art**: front + a real 2-frame Left walk cycle (`Horace_left1.png`/
`Horace_Left2.png` — `Horace_left.png`, no suffix, is an earlier superseded draft left
unreferenced, same "extra art, no slot for it yet" convention as `Cluck_rightwalk.png`), one Right
frame (`Horace_right2.png`, no "right1" yet — repeats for both Right0/Right1 slots, still real
dedicated art so `hasDedicatedRightArt = true`), and a Rear Kick landing-impact "buck" effect
(`Horace_ability_buckleft.png`/`Horace_ability_buckright.png` on a new `HoraceBuck.prefab`/
`HoraceBuckEffect` — mirrored per knockback direction). No Up/back art yet — Up falls back to
front.

**Gerald and Billy now have real art too, completing all 8 characters.** Gerald gets a real
2-frame Left walk cycle (`Gerald_left.png` → `Gerald_left1.png`) and a single dedicated Right frame
(`Gerald_right.png`, repeats for both Right0/Right1 slots, same "one real frame, no mirroring"
convention as Horace's Right — `hasDedicatedRightArt = true`); no Up/back-facing art, Up falls back
to front (`ArtWiringBuilder.WireGerald`). `Gerald_ability.png` is uploaded but stays unwired for
the same reason `Gerald_effect.png` (now deleted, replaced by this file) did — `PuffUpAbility` has
no spawned effect object to put a sprite on, only Gerald's own sprite scaling 3x; wiring it in
would be a gameplay change, not art wiring. Billy gets full 2-frame walk cycles on **both** Left
(`Billy_left.png` → `Billy_left1.png`) and Right (`Billy_right.png` → `Billy_right1.png`, no
mirroring either side — `hasDedicatedRightArt = true`), plus front/back (`ArtWiringBuilder.
WireBilly`).

**Water tiles have real art** (`Water_tile.png`, wired onto the `WaterTile` prefab via
`WireMazeTiles`) and **real placements**: every level from `LevelData_16` onward (the point
`LevelData_05`'s levelNumber-15 unlock threshold means Ducky is actually available — see the
character-unlock table) got a Ducky-only water tile pair added to its maze, except `LevelData_23`
(no interior redundancy at all to safely add one to — the one genuine placement gap; an earlier
pass of this doc mistakenly named `LevelData_24` as the gap instead, which turned out to be wrong
once actually checked — `LevelData_24` does have a pair). `TileMapRenderer.PairWaterTiles` requires
both tiles of a pair to share a row (paired by row, same convention warp tunnels use before falling
back to column), which is why "move the water pair" is a same-row column move, not a free
repositioning anywhere in the maze.

**Water pairs were repositioned to opposite sides of their row** in a later pass, per feedback that
they should read as a genuine shortcut across the maze rather than a 1-tile hop — most of the 34
pairs originally sat in adjacent columns (e.g. columns 3 and 4) because they'd each been converted
from a short run of wall cells next to each other, the easiest safe spot to find by hand. Moving
them required an offline script (same category of tool as the maze-generation verification below,
not committed — only its baked output is, per that convention): for each level, the existing pair
is reverted to wall, then every row is scanned for two wall cells (excluding the border columns)
each adjacent to a real floor cell, and the pair with the greatest column separation wins (ties
prefer the original row). Converting only between wall(1)/water(8) — both already impassable to
non-Ducky characters — means non-Ducky connectivity is provably unaffected by this regardless of
which row/columns get picked; only Ducky's own reachability to the new pair was re-verified (BFS
flood fill treating water as walkable). A few pairs (levels 28/37/40/44) were already correctly
spread (e.g. columns 1 and 9) and didn't move.

**World 3 (Orchard) and World 4 (Wheat) are both now fully wired and have all 25 real levels each**
(see "Development status" — `LevelData_51`-`75`/`levelNumber` 50-74 for Orchard,
`LevelData_76`-`100`/`levelNumber` 75-99 for Wheat, the last world). `ArtWiringBuilder.
WireOrchardAndWheat` adds both `TileMapRenderer.MazeArtSet` entries additively via
`TileMapRenderer.GetOrAddArtSet(MazeType)` (fetches-or-creates the entry, exposing its public
fields directly, since neither `Phase2ProjectBuilder.BuildAll`'s hardcoded `SetMazeArtSets` list
nor a single-field setter like `SetBackdropSprite` was a good fit for wiring several fields on a
brand-new world in one pass — note `SetMazeArtSets` **replaces** the whole list, so
`WireOrchardAndWheat` must always run *after* `Phase2ProjectBuilder.BuildAll` in the standard
rebuild chain, or its additive entries get wiped by the next `WireScene` call). Orchard has:
`Wall_Orchard`/`Ground_Orchard` prefabs (`Orchard_WallTile.png`, and `OrchardFloorTile.png` — the
latter deliberately split off a ground-art drop-in that originally landed on the **shared**
`FloorTile.png` CornField/VegPatch still use, which would have silently changed both worlds'
ground too; the original `FloorTile.png` was restored from git and the new art saved under its own
filename/prefab instead), a backdrop (`OrchardBackground.png`), a regular every-tile pellet
(`Red_Apple.png`), and a bonus pickup scattered ×10 (`Cherry.png`, reuses `CoinPickup`/
`BuildBonusPickupPrefab` — awards `SaveManager` coins directly, not maze score, same as CornField's
coin). Wheat has its own equally complete set now too: `Wall_Wheat`/`Ground_Wheat` prefabs
(`WheatWallTile.png`/`WheatFloorTile.png` — previously Wheat had no dedicated wall/ground art at
all), a backdrop (`Wheatfield_background.png` — a previously-dead const left over from an earlier
World 1 backdrop swap, reused here for its actual intended world), a regular every-tile pellet
(`MiniLoaf.png`), and a bonus pickup scattered ×10 (`RareGrainSack.png` — its count was originally
×1, "Rare" naming inferred to mean scarce; the ×1 turned out to just be an interim placeholder, not
an intentional scarcity choice, once the actual spec called for ×10 like Cherry). Both worlds' rare
-tier pellet now has a distinct look too, each repurposing a sprite originally uploaded for a
different role: Orchard's is `RarePellets_apple.png` (VegPatch's own regular pellet elsewhere),
Wheat's is `RarePellets_maize.png` (previously unwired dead weight left over from the old 3-tier
pellet visual system) — via `MazeArtSet.rarePelletSprite`, the single pellet that wins a maze's
one-rare-slot cap (`ConfigurePelletTier`'s `_rarePelletsSpawned` guard); every other pellet still
shows `pelletSprite`, and any world without a `rarePelletSprite` set (CornField/VegPatch) keeps the
older "every pellet, rare or not, looks the same" behaviour unchanged. Both worlds now have their
own dedicated warp-tunnel art (`WarpTunnel_Orchard`/`WarpTunnel_Wheat` prefabs, wired to
`OrchardWarpTile.png`/`WheatWarpTile.png`) instead of reusing CornField's generic `WarpTunnel`/
`WarpTile.png`.

**Orchard's and Wheat's regular crop tiles (id 2/3) used to reuse CornField's `Crop_Corn`/
`Crop_Vegetable` prefabs too** — meaning every ordinary collectible in an Orchard or Wheat maze
rendered `CornKernel.png`/`CornCob.png`, visibly wrong for either world. Caught and fixed on a
review pass, Orchard first then the same issue found on Wheat by the same check: both worlds now
have their own two crop prefabs (`Crop_Kernel_Orchard`/`Crop_Vegetable_Orchard` wired to
`Red_Apple.png`; `Crop_Kernel_Wheat`/`Crop_Vegetable_Wheat` wired to `MiniLoaf.png`, Wheat's own
"regular pellet" sprite reused the same way Orchard's `Red_Apple.png` already was for its id-4
role), same point values (10/50) as every other world's kernel/vegetable tier — only the art
changed. `Crop_Corn`/`Crop_Vegetable` are CornField/VegPatch-only again now. VegPatch was checked
too and was already correct (its own `Crop_Kernel_VegPatch`/`Crop_Vegetable_VegPatch` prefabs,
carrot/cabbage art, predate this issue).

**Both worlds' crop-kernel/vegetable prefabs render at scale 0.7, matching the power pellet — not
the usual 0.35/0.5 kernel/vegetable convention every other world uses.** `Red_Apple.png`/
`MiniLoaf.png` are each wired to THREE different tile roles at once (Orchard's kernel, vegetable,
AND power pellet all show `Red_Apple.png`; same for Wheat's `MiniLoaf.png`), so every apple/loaf in
a maze needs to render the same visual size regardless of which tile id painted it. The 0.35/0.5
kernel-vs-vegetable size split exists elsewhere specifically to visually distinguish two DIFFERENT
sprites (corn vs. carrot, say); it doesn't apply when both tiers share one sprite with the pellet —
at the old 0.35/0.5 scale, some apples/loaves in a maze visibly looked smaller than others purely
because of which tile id happened to paint that cell, not any real difference in the art.

**Drone now has real art** (`Drone.png` — a single symmetric hovering-quadcopter sprite with no
directional cues, so `RobotVisual` shows it for every facing via its own null-fallback rather than
needing per-direction frames) and its wall-phasing was tightened to match the design intent:
`DroneRobot.IsWalkableForThisRobot` used to be a bare `tileMap.IsInBounds` check, meaning it could
fly straight through the maze's outer border wall and off the playable board — it now only phases
through **interior** walls (any cell not on row/column 0 or `MazeWidth-1`/`MazeHeight-1`); normally
walkable cells (open floor, a border warp-tunnel opening) are unaffected either way.

**Gerald and Billy had a PPU (`spritePixelsPerUnit`) wiring bug**: `Gerald_left1.png`/
`Gerald_right.png` and all 6 of Billy's walk sprites were loaded and assigned in their `Wire*`
methods but missing from `ArtWiringBuilder.SpritesToConfigure` — the array that actually sets each
texture's `spritePixelsPerUnit` to its own pixel width. Without that, those textures kept Unity's
default PPU (100) instead, rendering up to ~5x oversized at the same `localScale` every other
correctly-configured frame uses — Gerald would visibly balloon on his second left-walk frame and
whenever facing right; Billy (whose art is also non-square, e.g. `Billy_Front.png` at 213×401) was
oversized in literally every direction, all the time. Both fixed by adding the missing consts to
`SpritesToConfigure`. **If a future character/world's art looks mis-sized only in some poses (or
always), check this array first** before suspecting the sprite itself or `localScale` — this is now
the second and third time this exact omission has caused it (see Orchard's wall/floor/backdrop/
pellet/bonus consts above, found the same way).

**Billy still has a real, unresolved sizing inconsistency between facings — a stopgap is in place,
not a fix.** Unlike every other character's art (uniformly 500x500 square in every direction), his
`Billy_Front.png`/`Billy_back.png` are tight portrait crops (213x401 / 234x408) while
`Billy_left/right(1).png` are a much more loosely-padded 500x500 square. Since `CharacterAnimator`
just swaps `_spriteRenderer.sprite` with no per-frame scale compensation, and PPU is normally set to
each texture's own width, this rendered him ~1.88 world units tall facing up/down but only ~1.0
tall facing left/right — a visible size pop on turning, caught via a direct pixel-dimension check
across all 8 characters' art (only Billy has a directional aspect-ratio mismatch; every other
character's directions are all 500x500). **Stopgap:** `ConfigureSpriteImporters` now overrides PPU
for just `Billy_left/right(1).png` to `500 * 213 / 401 ≈ 265.6` (matching Front's height ratio)
instead of the texture's own width, so his apparent height is now consistent across every facing —
at the cost of also rendering him wider than 1 grid cell while walking sideways, since a single PPU
scalar can't fix height independent of width on a square source. The real fix is a tighter crop of
the Left/Right art (matching Front/Back's framing) whenever that art lands; replace the override
with the standard `width > 0 ? width : 100` rule at that point, not before.

**Still missing / not wired:** a Vulnerable-state robot sprite and the Loading Screen background
(uploaded, unwired — see above). Cluck's Egg Drop effect art and the branding Logo are both wired
(see above) — do not re-list them here. Heavy's robot art (`HeavyRobot_front/back.png`) was
**deleted from the project** (not merely unwired) — `ArtWiringBuilder` still references the
filenames and logs "not found" warnings for them on every `WireAll` run; this is expected, not a
bug to fix, and Heavy is deliberately excluded from `Phase3ProjectBuilder`'s auto-assigned robot
roster now (see "Development status" → Phase 3) since it would otherwise appear as an untextured
placeholder square. `Map.png` and `Card.png` are
**not** gaps to close — `WorldMapScreen` (which would have used `Map.png`) was removed outright (see
"Removed: World Map screen"), and the New Character Unlock card / `RosterCard` were deliberately
decided to stay unframed rather than get a generic `Card.png` border; neither constant exists in
`ArtWiringBuilder` anymore, so don't reintroduce them speculatively. `Btn_music.png`/
`Btn_nosound.png`/`Btn_quit.png` are configured for import settings but never assigned to any
field — leftover from before `SoundButton` was removed from the HUD (see "Landing/Gameplay-HUD
cleanup") — currently dead, not a bug. `ArtWiringBuilder.Load()` logs a warning whenever a
referenced sprite path doesn't resolve on disk, so a future accidentally-dead reference like these
won't be silent.

**Texture import convention:** `ArtWiringBuilder.ConfigureSpriteImporters` sets every wired
texture's `spritePixelsPerUnit` to that texture's own pixel width (via `TextureImporter.
GetSourceTextureWidthAndHeight`), not a fixed value — this makes a sprite at `localScale = 1`
fill exactly one maze grid cell (1 world unit), matching `PlaceholderSprite`'s 1px==1unit@scale1
convention that every prefab's existing `localScale` (e.g. crop 0.35, pellet 0.7) was already
tuned around, so no prefab scale values needed to change when real art went in.

**Audio** — `Audio/Music/BackgroundMusic.mp3` is wired onto `AudioManager.backgroundMusicClip`
(`ArtWiringBuilder.WireAudio`). `AudioManager.Start()` calls `ResumeBackgroundMusic()` as a
safety-net auto-play, guarded by a `_musicStarted` flag set the first time `PlayMusic` runs from
anywhere — since Unity calls every active object's `OnEnable` before any object's `Start()`,
`MainMenuController.OnEnable`'s `PlayLandingMusic()` always claims `_musicStarted` first when Main
Menu is present, so this fallback can never override Main Menu's own landing track with gameplay
music; it only matters in a context with no `MainMenuController` (e.g. a test harness).
`SaveManager.MusicVolume`'s default was lowered from `1f` to `0.5f` so it plays soft/background-
level out of the box rather than at full volume; still fully overridable via the Settings slider.

**`GameManager.EndLevel`/`QuitToLevelSelect` call `AudioManager.PlayLandingMusic()` on the way out
of gameplay, not `StopMusic()`** — leaving a level (win, fail, or a deliberate Pause-menu quit) now
resumes the landing/menu track instead of cutting to silence. This matches the earlier fix to
`MainMenuController.OnDisable` (below) with the same intent: music should only ever be swapped
between tracks, never stopped outright, while any screen that has one is showing.

`AudioManager.musicSourceA`/`musicSourceB`/`sfxPool` are `AudioSource` children created by
`Phase5ProjectBuilder.WireAudioSources` (called from `AddManagers`, part of `BuildAll`) — **not**
something `ArtWiringBuilder` can fix on its own, since it only assigns `AudioClip`/`Sprite`
references, never creates GameObjects. If these fields are ever empty (check the `AudioManager`
component's serialized fields directly in `Game.unity` if in doubt), every `PlayMusic`/`PlaySFX`
call silently no-ops — re-run `Phase5ProjectBuilder.BuildAll` to fix it, not `ArtWiringBuilder`.

`Audio/SFX/EatRobot.mp3` (despite living in the SFX folder, it's used as a second **music** track,
not a one-shot) plays for the exact duration a power pellet is active — `PowerPelletManager`
crossfades to it via `AudioManager.PlayEatRobotMusic()` on the `false → true` activation edge, and
crossfades back to the regular background track via `ResumeBackgroundMusic()` when the countdown
reaches zero (`PowerPelletManager.CountDown`'s end). Both go through the same `PlayMusic`
crossfade `AudioManager` already had — no new fade logic needed, just two named entry points.

All 6 SFX clips under `Audio/SFX/` are wired to a specific gameplay trigger, each via a named
`AudioManager` method (`PlayXSfx()`, not a raw `PlaySFX(clip)` call, so call sites read as what
happened rather than which clip field to reach into):

| Clip | Method | Fires from |
|---|---|---|
| `Animal_death.mp3` | `PlayAnimalDeathSfx` | `PlayerHealth.DeathSequence` (start of the death sequence) |
| `CornPickup.mp3` | `PlayCornPickupSfx` | `CropCollector`, on every `CropPickup` collected — doubles as the generic crop/"pellet" pickup cue (corn kernels and vegetables alike, per feedback that carrots were silent), not corn-only despite the name |
| `CoinPickup.mp3` | `PlayCoinPickupSfx` | `CropCollector`, on every `CoinPickup` collected — previously reused `PlayCornPickupSfx` as a placeholder before this clip existed; now a distinct cue |
| `PowerReady.mp3` | `PlayPowerReadySfx` | `AbilityBase.UpdateCooldown`, the single frame a character's ability cooldown reaches exactly 0 (not power-pellet activation — that's a separate, unrelated event; see `PlayEatRobotMusic` below) |
| `RarePellet_pickup.mp3` | `PlayRarePelletPickupSfx` | `CropCollector`, only when `pellet.pelletType != PowerPelletType.Sunflower` — same "rare tier" gate `PelletCollectBurst` uses. Fires *before* `PowerPelletManager.ActivatePower` (which crossfades music to `EatRobot.mp3`), so the pickup cue is heard first rather than being stepped on by the music swap |
| `RobotSpawn.mp3` | `PlayRobotRespawnSfx` | `RobotSpawner.SpawnRobot` — every robot spawn, including level-start ones. Used to fire only from a defeated robot's mid-level walk back to the factory (`RobotBase.ArriveAtFactory`); that flow was removed (defeated robots now disappear permanently for the rest of the maze — see the Robot AI state-machine note above), so this was repointed to the only spawn event left, or it would have become dead code with no call site at all |

When more art lands, wire it into the existing prefabs (`Prefabs/Characters/`, `Prefabs/Robots/`,
`Prefabs/Blocks/`) via `ArtWiringBuilder` rather than creating new prefabs.

## Testing

Desktop: arrow keys or WASD. Mobile/Editor: swipe (or mouse-drag in Play mode) — 50px minimum
distance, dominant axis wins for diagonals. Tunable parameters if movement doesn't feel right:

- `GridMovement.speed` (comes from `CharacterData.movementSpeed` — **unified to `4.0` for all 8
  characters** as of a later gameplay pass, per feedback that they should all move identically.
  History: originals (Percy/Ducky/Horace at 6/5.5/5.5) were cut to ~0.76x, then ~0.6x again (down to
  1.9/1.8/2.8/2.3/2.5/2.5/2.0/2.0) when movement still read as too fast, then doubled back up
  per feedback that characters and robots moved at effectively the same speed (landing on a spread
  from 3.6 Bessie to 5.0 Percy/Ducky/Horace) — that spread itself later read as arbitrary (some
  characters weren't meant to be faster than others, that was just where the doubling pass happened
  to land after capping against `movementSpeed`'s `[Range(1,5)]` inspector hint), so
  `Phase4ProjectBuilder.UnifiedCharacterSpeed` now applies one shared value to every
  `BuildCharacterDataAsset` call instead of 8 separate literals. Robots' own `RobotData.movementSpeed`
  stayed at `2.0` (see Phase 3 below) — characters now clearly outrun a Chase/Scatter robot.
  A Vulnerable (killable) robot flees at `RobotBase.VulnerableSpeedMultiplier` (`0.85`) of its OWN
  normal speed (1.7 for a base-2.0 robot) — "slightly slower than its normal pace," per a later
  gameplay pass, rather than the old `0.5` (half speed). This is deliberately a fraction of the
  robot's own speed, not the character's: an earlier attempt keyed it off the (now-unified) active
  character's speed instead (4.0 × 0.85 = 3.4), which backfired — since robots chase at a much
  lower base speed than any character, that made a fleeing robot move FASTER than it does while
  hunting, the opposite of "so the character can catch them.")
- `GridMovement.AlignmentEpsilon` (0.02) — grid-center snap tolerance
- `InputController.minSwipeDistancePixels` (50)
- `CharacterAnimator.frameInterval` (0.15s baseline, scaled by speed)
- `WarpTunnel.reWarpCooldown` (0.1s)
- Ability cooldowns: `CharacterData.abilityCooldown` per character (also set on the ability
  component's `totalCooldown` by `Phase4ProjectBuilder` — keep both in sync if you tune one)

## Batch-mode verification (no Editor UI needed)

```bash
# Compile check
"C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe" -batchmode -quit \
  -projectPath "C:/Users/Personel/Desktop/FarmFury_Arcade" -logFile <path>.log

# Rebuild Phase 2 prefabs/LevelData/scene wiring (safe to re-run)
"C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe" -batchmode -quit \
  -projectPath "C:/Users/Personel/Desktop/FarmFury_Arcade" \
  -executeMethod FarmFuryArcade.EditorTools.Phase2ProjectBuilder.BuildAll -logFile <path>.log

# Rebuild Phase 3 robot prefabs/RobotData/LevelData_01+05/scene wiring (safe to re-run; run Phase 2 first)
"C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe" -batchmode -quit \
  -projectPath "C:/Users/Personel/Desktop/FarmFury_Arcade" \
  -executeMethod FarmFuryArcade.EditorTools.Phase3ProjectBuilder.BuildAll -logFile <path>.log

# Rebuild Phase 4 character prefabs/CharacterData/ability sub-prefabs/LevelData_01 water/scene wiring
# (safe to re-run; run Phase 2 first, Phase 3 not required at build time but expected at runtime)
"C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe" -batchmode -quit \
  -projectPath "C:/Users/Personel/Desktop/FarmFury_Arcade" \
  -executeMethod FarmFuryArcade.EditorTools.Phase4ProjectBuilder.BuildAll -logFile <path>.log

# Rebuild Phase 5 UI screens/managers/scene wiring (safe to re-run, rebuilds the whole UI
# hierarchy from scratch each time; run Phase 1+2 first)
"C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe" -batchmode -quit \
  -projectPath "C:/Users/Personel/Desktop/FarmFury_Arcade" \
  -executeMethod FarmFuryArcade.EditorTools.Phase5ProjectBuilder.BuildAll -logFile <path>.log

# Play mode verification (note: no -quit — the method calls EditorApplication.Exit itself)
"C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe" -batchmode \
  -projectPath "C:/Users/Personel/Desktop/FarmFury_Arcade" \
  -executeMethod FarmFuryArcade.EditorTools.Phase1ProjectBuilder.RunPlayModeVerification -logFile <path>.log
```

The play-mode run occasionally hangs *after* logging all results (something in Editor teardown,
not a project bug) — if the log already shows the expected `PASS`/`FAIL` lines and the process
doesn't exit within a minute or so, it's safe to kill (`taskkill /F /IM Unity.exe /T` on Windows)
and reopen the project normally to confirm nothing was corrupted.

## Development status

- **Phase 1** (foundation): single-scene architecture, ScriptableObject data pipeline, manager
  singletons — done.
- **Phase 2** (movement & maze): tile-id-driven maze rendering, grid movement with intersection/
  reversal rules, crop/vegetable/power-pellet pickup, warp tunnels, scoring, level completion —
  done. **All 100 real levels now exist**, filling out `UnlockProgression.TotalLevels` completely
  across all 4 worlds: `LevelData_01`-`25` (World 1, Corn Field, `levelNumber` 0-24), `LevelData_26`-
  `50` (World 2, Veg Patch, `levelNumber` 25-49), `LevelData_51`-`75` (World 3, Orchard, `levelNumber`
  50-74), and `LevelData_76`-`100` (World 4, Wheat Field, `levelNumber` 75-99 — the last world).
  World 3 and World 4's 25-level sets are each entirely algorithmically generated (no hand-authored
  ones, unlike World 1's `01`-`04`/`06`-`08`), each by its own one-shot offline generator
  (`OrchardMazeGeneratorTemp`/`WheatMazeGeneratorTemp` — not kept in the repo, same "generator not
  committed, only its baked output is" convention every algorithmically-generated level here
  follows; Wheat's used a different RNG seed and corner/factory offsets than Orchard's so its 25
  shapes don't mirror Orchard's) using a cleaner, explicitly-provable version of the same
  pillar/room scheme: interior room cells sit at ODD x in `{1,3,5,7,9}` and ODD y in `{1,3,5,7}`
  (5x4 = 20 rooms per maze), pillars at BOTH-even interior coordinates are never carved (same
  no-open-2x2-block guarantee as World 1/2's generator, just with the "never carved" set spelled
  out explicitly rather than inferred), and a recursive-backtracker spanning tree plus 5-8 extra
  loop edges connects them. Warp pairs are TOP/BOTTOM (same room column, y=0 and y=8) rather than
  LEFT/RIGHT, since the height (9) divides evenly into the room/pillar scheme with zero wasted
  margin while the width (12) doesn't quite (column `x=10` stays a permanent one-tile wall margin
  before the right border) — column pairing sidesteps that asymmetry entirely. All 50 (25 per
  world) were verified offline (full connectivity via flood fill from the player start, zero
  open-2x2 blocks, both warp tiles adjacent to an open cell) before being baked in. Phase 3's
  separate 20x20 multi-robot test maze lives at `LevelData_RobotTest.asset`
  (`levelNumber -1`, invisible to Level Select) — it used to sit at `LevelData_05.asset`/
  `levelNumber 4`, which meant `DataManager` (keyed purely by `levelNumber`) surfaced it as the
  real "Level 5" tile: tapping it loaded a mostly-open 20x20 test field instead of a designed 12x9
  maze, reading as "blank and without walls" next to every other level. `LevelData_05` is now a
  real, algorithmically-generated 12x9 "Corn Field - 05" level like `LevelData_09` onward.
  **The `x=10` dead-margin column above turned out to be a real, visible problem, not just a
  documented quirk:** since it's a permanent wall column sitting directly beside the border wall
  (`x=11`, also permanent), together they formed a genuinely 2-tile-thick wall running almost the
  full height of the maze — found on 93 of the 100 levels via a full offline audit (same
  not-committed-generator-script convention), reported as "wall tiles sitting double-sided next to
  each other." Fixed with a general, provably-safe algorithm rather than a hand patch per level: for
  every 2x2 all-wall block found, flip exactly one of its cells to plain floor, but only a
  non-border cell, and only if doing so doesn't create a NEW 2x2 all-open-floor block (checked
  immediately, not assumed) — then re-verify with a full connectivity flood-fill afterward that
  nothing became unreachable. This resolved 580 of 593 total violations on the first (simplest)
  candidate-ordering pass; the remainder needed a second candidate-cell ordering strategy (preferring
  the row shared with an adjacent violation, so a cell-flip resolves two overlapping blocks in one
  move instead of fighting a later flip for the same cell) to reach 99/100. **`LevelData_07`
  (hand-authored) is the one level left with its double-wall intact** — a genuinely interior double
  wall (not the border-margin pattern), in a maze packed tightly enough that every candidate
  single-cell flip creates a different open-floor violation elsewhere; left alone rather than risk a
  bad edit to hand-authored content. 295 individual wall cells were carved across the other 99
  levels — purely wall-to-floor conversions widening a room by one tile along the border, no new
  rooms, no crop/pellet count changes (`BuildLevel` recomputes `totalCropsRequired` from the grid
  automatically either way).

  World 1's `01`-`04`/`06`-`08` are hand-authored via `Tools/maze-designer.html` (a standalone
  click-to-paint web page, repo root — now has a World 1/World 2 toggle that reskins the wall/warp
  swatch colors to match each world's real art and tags the exported grid with
  `WORLD=... (mazeType=...)`, so a pasted-back export says which `MazeType` to build it with); the
  rest of both worlds are algorithmically
  generated (`Phase2ProjectBuilder`'s per-level `BuildLevelDataNN` methods, all delegating to the
  shared `BuildLevel(path, rows, levelNumber, levelName, mazeType)` helper — a recursive-
  backtracker maze on a half-density cell grid, provably unable to produce an open-2x2-block
  since every EVEN-EVEN grid coordinate is never carved).
  ⚠️ **`Phase2ProjectBuilder.BuildAll` rebuilds `Cluck.prefab` and every `LevelData` asset from
  scratch on every run — including resetting `robotSpawns` to empty and any hand-tuned Editor-only
  state.** Phase 3 gives `LevelData_01` its 2 robot spawns and Phase 4 adds `CharacterBase`/
  `EggDropAbility`/etc. to Cluck; re-running Phase 2 alone **silently wipes both** back out
  without erroring (this bit a real session: repeated Phase 2 reruns while iterating on maze/art
  wiring left `LevelData_01.robotSpawns` empty and Cluck missing `CharacterBase`/`PlayerHealth`,
  which broke robot spawning, character-swap, ability use, and — since `CharacterBase.Initialize`
  is what pushes `CharacterData` into `CharacterAnimator` — even looked like "Cluck's left/right
  walk changed"). **Always re-run the full `Phase2 → Phase3 → Phase4 → Phase5 → ArtWiringBuilder`
  chain in order after touching Phase 2**, never just Phase 2 + ArtWiringBuilder in isolation.
  **Every real level now has robot spawns**, not just `LevelData_01`/`_05` — until an earlier
  session `Phase2ProjectBuilder.BuildLevel` always stamped `robotSpawns = []` ("No robots yet —
  Phase 3") and nothing ever filled it in for the other levels, so no robots ever spawned outside
  of those two. `Phase3ProjectBuilder.AssignRobotSpawnsToRemainingLevels` now loops every
  `LevelData_01`-`100` (skipping `01`, hand-tuned separately) and applies a difficulty curve that
  resets per world (`levelNumber % 25`): 2 robots for a world's first 5 levels, 3 for the next 7, 4
  for the next 7, 5 for the last 6 — all spawned at that level's own `robotFactoryPosition`,
  staggered 4s apart starting at 2s. Purely a first-pass default (like the algorithmically
  generated mazes themselves); retune per-level via the same method if the curve plays wrong.
  **`DifficultyOrder` (Harvester, Scout, Patrol, Drifter, Drone) no longer includes Heavy** — its
  art (`HeavyRobot_front/back.png`) was deleted from the project, so it would otherwise render as
  an untextured placeholder square. Removing it from the 5-type roster means the "5 robots" tier
  (a world's last 6 levels) now brings Drone into the standard curve for the first time, rather
  than topping out at 4 real distinct types with Heavy as an unusable 5th. Heavy's `RobotData`/
  prefab/`HeavyRobot.cs` still exist and still build — just never auto-assigned by this curve
  anymore. Re-running this method (e.g. as part of the full Phase 2→5 chain) will revert any
  level's spawns back to the curve, including levels that previously had Heavy in their top tier.
- **Phase 3** (enemies & AI): 6 robot AI types (Harvester/Scout/Patrol/Drifter/Heavy/Drone) with
  distinct targeting behaviours, Chase/Scatter/Vulnerable/Defeated/Returning state machine,
  power-pellet-driven vulnerability (`PowerPelletManager`), chain scoring (`ChaseScoreManager`),
  player death/respawn on hostile contact (`PlayerHealth`), robot spawning with staggered delays
  (`RobotSpawner`) — done.
- **Phase 4** (characters & abilities): all 8 characters with unique `AbilityBase` subclasses,
  character swapping (`CharacterManager`, destroy/recreate at the same grid cell), the 8-combo
  `ComboSystem`, character unlocks on level complete (`UnlockManager`), a functional-not-polished
  swap UI (`CharacterSwapUI`, Tab to open) — done. (`CharacterSwapUI` was later replaced by the
  real uGUI `ChooseCharacterScreen` — see that entry above.)
- **Phase 5** (progression & UI): full screen flow (Main Menu → Level Select →
  Gameplay → Level Complete/Failed, plus Character Roster/Leaderboards/Settings/a Store placeholder)
  as real uGUI with TextMeshPro, `SceneTransitionManager`-driven fades, star rating + score on
  level complete (the fuller breakdown/coin display it originally had was simplified away in a
  later mockup pass — see "Art status"), automatic New Character Unlock celebration, `AudioManager`
  (real clips wired — see "Art status"), `DailyChallengeManager` foundation (5 challenge types,
  date-seeded, reuses `LevelData_01` rather than a distinct maze), local `LeaderboardManager` — done.
  An intermediate World Map screen existed here through much of Phase 5's history but was removed
  outright afterward — see "Removed: World Map screen".

## Known gaps / flagged for Phase 6
- **Character Roster and Leaderboards have no Main Menu entry point** — removed in the
  landing-page cleanup (see "Landing/Gameplay-HUD cleanup" above) in favour of just Play/Settings/
  Shop. Both screens still exist and build correctly; reaching them today requires calling
  `SceneTransitionManager.ShowOnly` directly, since nothing currently does. (Shop/`ShopController`
  regained a Main Menu entry point when Monetisation Phase 3's IAP plumbing was built — see "IAP
  plumbing" above — so it's no longer in this no-entry-point group.) Daily Challenge is
  different: it isn't a separate screen, just an objective overlaid on `LevelData_01` (index
  `DailyChallengeLevelIndex`, 0) — since that's the same level the normal Main Menu/Level Select
  flow already plays, `DailyChallengeManager.CheckCompletionOnLevelEnd` fires on any ordinary playthrough of
  level 0, no special entry point needed. (Before the Matchup screen's removal, that screen's
  `ShowForLevel` was one way to jump straight to a given level for testing; that shortcut is gone,
  but Daily Challenge completion never depended on it.)
- **Store is now a minimal, real IAP purchase surface (coin packs + Remove Ads), not a placeholder**
  — see "IAP plumbing" above. The GDD's full cosmetics Store vision (hats/skins/trails/themes) is
  still unbuilt and is Phase 4 scope, not Phase 6 as this section's own heading implies — that
  phase-number mismatch predates this note and hasn't been reconciled.
- **Settings' Restore/Reset Progress**: the original cloud-save Restore Progress and Reset Progress
  buttons were removed entirely (not just stubbed) in the 2026-07-31 mockup pass — Restore was
  Phase 6/cloud-save scope with no real action either way, and real cloud-save restore is still
  Phase 6 scope if it comes back. The Settings grid's 6th cell that Restore Progress used to occupy
  now hosts a *different* Restore — IAP's "Restore Purchases" (Monetisation Phase 3) — which is a
  distinct, now-real feature that happens to reuse the same empty slot; see "IAP plumbing" above.
- **Leaderboards has no cloud sync** — local-only, per spec.
- **`DailyChallengeManager.CharacterLocked` isn't enforced**, only checked after the fact — a
  player can freely swap characters during a Character-Locked daily challenge; the run just won't
  register as completed if more than one character was used. Real enforcement needs
  `CharacterManager.CanSwapTo` to know about the active challenge.
- **No ability icon sprites, and only partial portrait art** — the HUD portrait
  (`GameplayHUD.characterPortrait`, via `RefreshPortrait`) uses `CharacterData.portraitSprite`
  (front sprite) where a character has real art (see "Art status"); Roster cards still use
  solid-colour placeholders, and no dedicated ability icons exist anywhere.

## UX flow

```
Main Menu ──Play──▶ Level Select ──tap unlocked tile──▶ Gameplay HUD
    │▲                   │▲                                       │▲  │
    ││                    │└─ tap CurrentWorldIndicator (world select) │
    │└───────────────────┘                              Pause(P)──▶│└──┼─▶ Resume
    │                                                                  │
    │◀──────────────────── back (round icon) ─────────────────────────┘
    │
    │                                                      Level Complete
    │◀──────────────────── Skip ──────────────────────────────▲  (all crops
    │                                                          │   collected)
    │                                          Level Failed◀───┘
    │                                          (Retry loops back to Gameplay,
    │                                           or Pause ▸ Quit to Level Select)
    │
    ├──Settings (gear)────▶ modal overlay (2x3 plaque grid: music/sfx/vibration/left-handed/
    │                        language/restore purchases) ──back (round icon)──▶ wherever opened from
    └──Shop─────────────▶ modal overlay (5 coin packs + Remove Ads) ──Back──▶ Main Menu
```

(An intermediate "World Map" screen used to sit between Main Menu and Level Select, and a Matchup
"VS" card screen with a 3-2-1-GO countdown used to sit between that and Gameplay HUD — both removed
entirely; see "Removed: World Map screen" and "Removed: Matchup screen" above. Level Select itself
— world badge carousel → per-world tile grid — is a later addition; see the "Level Select"
architecture section.)

Pause and Settings are **overlays** (layer on top of whatever's showing, dim it, don't replace it)
— everything else in this diagram is a **screen swap** through `SceneTransitionManager.ShowOnly`.
New Character Unlock is a special case: not reachable by navigation, it's triggered automatically
by `LevelCompleteController` partway through its own celebration sequence, whenever
`UnlockManager.LastUnlockedBatch` isn't empty.

**Character Roster and Leaderboards are no longer reachable from Main Menu** (removed in the
landing-page cleanup — see "Landing/Gameplay-HUD cleanup" and the matching "Known gaps" entry).
Both screens still exist and still work exactly as described above if shown directly via
`SceneTransitionManager.ShowOnly` — there's just no button anywhere that does so today. Shop
regained a Main Menu entry point when Monetisation Phase 3's IAP plumbing was built (see "IAP
plumbing" above) — it's the overlay shown in the diagram, not one of the no-longer-reachable
screens. Daily Challenge is unaffected, since it isn't a separate screen — see the matching
"Known gaps" entry.
