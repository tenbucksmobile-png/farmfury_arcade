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
  Sprites/{Characters,Robots,Environment,UI}, Audio/        first real art landed here (see
                                                              "Art status" below); Audio/ still
                                                              empty
  Resources/TMP Settings.asset, TextMesh Pro/Resources/     TMP essentials (see "TextMeshPro
                                                              bootstrap" below)
  Scenes/Game.unity
```

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
  also triggers `UnlockManager.CheckUnlocksOnLevelComplete`.
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

Crop/vegetable/pellet/warp-tunnel positions are **not** stored as separate arrays — an earlier
pass had `CropPlacement[]`/`PowerPelletPlacement[]` fields on `LevelData`, but these were removed
in favor of scanning the grid for tile ids 2–5, since the GDD's own convention table already
encodes this and keeping both would mean two sources of truth. `LevelData.warpTunnelRows` lists
which rows wrap between `x=0` and `x=mazeWidth-1`; `TileMapRenderer` pairs the two tile-id-5
tiles per row automatically.

**Why `LevelData.mazeLayoutFlat` instead of a raw `int[,]`:** Unity's serializer doesn't support
multi-dimensional arrays — a field declared `int[,]` silently fails to persist. The grid is
stored as a flat `int[]` (row-major) and exposed as `int[,]` through the `MazeLayout` property
(and written via `SetMazeLayout`). Always go through `MazeLayout`/`SetMazeLayout`, never touch
`mazeLayoutFlat` directly.

### Movement (`Scripts/Gameplay`)

`GridMovement` implements continuous grid-based movement: the character keeps moving in
`CurrentDirection` until the next cell center, where a queued direction (from `InputController`)
is applied if walkable (`IsWalkable(cell, canCrossWater)` — see the water tile note above).
Direction reversal is only allowed at intersections (3+ walkable neighbours) or dead ends (≤1) —
a straight corridor/turn (exactly 2) ignores a queued 180° reversal. `InputController` raises a
static `OnDirectionInput` event from keyboard (WASD/arrows, via Input System), swipe/pointer
gestures (works with mouse drag in the Editor too), and — the same event, via
`InputController.RaiseDirectionInput` — an on-screen directional pad (`UI/DirectionalPadController`,
Gameplay HUD's `up`/`down`/`left`/`right.png` buttons, diamond-laid-out on the right side). None of
these three input sources know about each other; `GridMovement` just listens to whichever raises
the event. `OnAbilityActivateInput` (Space) and `OnSwapMenuToggleInput` (Tab) were added in Phase 4.
`GridMovement`/`AbilityBase` both subscribe
directly to their static events rather than routing through a per-frame lookup — safe because
`CharacterManager` guarantees only one character GameObject (and so only one subscriber of each)
exists at a time, destroying the old one before creating the new one on every swap.

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
`LateUpdate`, before the follow logic) derives it purely from the camera's own live `aspect`, so
`CameraFollow.TargetVisibleColumns` (10, of the maze's 14) always fills
`CameraFollow.WidthFillFraction` (0.7) of the screen width regardless of the actual device/window
aspect ratio. A fixed orthographicSize tuned by eye for one aspect (e.g. the Editor Game View's
often near-square default) looked completely different once actually run at a true mobile
landscape aspect — this was the direct cause of a "the maze isn't scaled/cropped like I asked"
playtest report. Recomputed every frame (one division) rather than once in `Awake`, so a runtime
window resize/orientation change stays correct too. `LevelData_01` itself is 14×16 (halved from an
original 28×31 — see `Phase2ProjectBuilder` under "Editor tooling" below); `CameraFollow` adapts
automatically to a different level's `mazeWidth`/`mazeHeight` since the clamp is computed from the
loaded level's own dimensions each frame, and the orthographicSize formula doesn't depend on level
data at all (only the camera's own aspect). `Utilities/CameraShake` (Bessie's Ground Slam
feedback) runs in `LateUpdate` with `[DefaultExecutionOrder(100)]` so it executes *after*
`CameraFollow` and adds its jitter on top of that frame's follow position via
`transform.position +=`, rather than caching an absolute "resting" position — a stale resting
position would snap the camera back to wherever it was at scene load every time a shake ended.

A `GameplayBackdrop` SpriteRenderer (`Wheatfield_background.png`, sorting order `-5`, centered on
the maze) fills the space around the board — see "Art status" below. Sized in
`ArtWiringBuilder.WireGameplayBackdrop` to cover whichever is bigger: the maze's own world
footprint, or `CameraFollow`'s target view width (constant regardless of aspect, by the same
formula above) — plus a 1.3x safety margin for aspect extremes — since the camera deliberately
shows more width than the maze has (that's the point of `WidthFillFraction`: extra screen margin
at the edges for this backdrop art to show through). An earlier version sized this backdrop off
only the maze's own bounds, which under-covered the camera's actual view once the 70%-width
framing was added, showing the camera's clear (blue) background color at the screen edges.

### Robot AI (`Scripts/Enemies`)

`RobotBase` is an AI-driven analogue of `GridMovement` — same continuous move-to-next-cell-centre
algorithm, but the next direction comes from `RobotAI.GetNextDirection`/`ComputeDesiredDirection`
instead of a queued player input. It deliberately does **not** reuse `GridMovement`, since that
component subscribes to `InputController`'s static `OnDirectionInput` event; giving robots that
component too would make every robot obey player input.

**State machine:** `Chase` ↔ `Scatter` alternate on a 20s/5s cycle (paused while Vulnerable/
Defeated/Returning, resumed from where it left off). `PowerPelletManager.OnPowerStateChanged`
flips every listening robot to/from `Vulnerable`; a hit while Vulnerable (`RegisterHit()`)
decrements health, and health reaching zero triggers a brief `Defeated` pause → `Returning`
(fast pathfind to the factory cell) → respawn to `Chase` on arrival. `PlayerHealth` calls
`RegisterHit()` on contact with a Vulnerable robot, and starts its own death sequence on contact
with a Chase/Scatter robot (Defeated/Returning "eyes" are harmless).

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
`IsWalkableForThisRobot`/`ComputeDesiredDirection` to use `TileMapRenderer.IsInBounds` instead,
so it picks the closest-to-target direction among all 4 regardless of walls — "through walls" is
really just "walls don't factor into its direction choice."

**Scatter corners:** `RobotSpawner.GetScatterCorner` assigns each `RobotType` one of the four maze
corners (inset 1 tile from the border), classic-arcade style. `DrifterRobot`'s "retreat" target
when close to the player reuses the same field (`scatterCornerPosition`).

**Art status:** `RobotVisual` now swaps in a real `RobotEyes.png` sprite for Defeated/Returning on
all 6 robots (see "Art status" below), but Vulnerable still swaps the placeholder `SpriteRenderer`
colour (blue, flashing white in the last 2s) since no dedicated vulnerable sprite has been
uploaded yet. Replace with a real `Robot_Vulnerable_Walk` sprite swap when that art lands.

**Chain scoring & power state:** `PowerPelletManager` (Core) owns the single global "frightened"
countdown (`IsPowerActive`, `TimeRemaining`, `ActivatePower(duration)`, `OnPowerStateChanged`)
and duration-per-tier lookup (`GetDuration`: Sunflower 8s / Golden Wheat 15s / Rainbow 30s).
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
| Cluck | EggDrop | 3 eggs behind her (0/2/4 tiles); any robot walking over one is stunned 3s for 15s | 15s |
| Bessie | GroundSlam | Stuns every robot within 2 tiles instantly; shockwave + camera shake | 20s |
| Percy | BounceRoll | Next wall he hits becomes walkable 2s (glows while phaseable) | 30s |
| Woolly | TripleClone | Spawns 2 AI clones (`WoollyClone`) that wander/collect crops for 10s | 25s |
| Ducky | SkipShot | Teleports across an adjacent unused water tile pair — once per pair per maze | 2s (debounce only — the real gate is per-pair, see `WaterTile.Used`) |
| Horace | RearKick | Nearest robot within 3 tiles (Manhattan) knocked back 4 tiles, stunned 2s on landing | 18s |
| Gerald | PuffUp | 3x scale, 5s, instantly defeats any robot touched, half speed, can't use warp tunnels | 45s |
| Billy | HeadbuttThrough | Permanently destroys the next 3 walls he hits | 40s |

**Robot mechanics abilities lean on** (`RobotBase`, additive Phase 4): `Stun(duration)`/
`IsStunned` (freezes state-cycle + movement, ignored by Defeated/Returning "eyes"),
`KnockBack(direction, tiles, stunAfter)`/`IsKnockedBack` (coroutine slide, stops early at a wall,
then stuns), `ForceDefeat()` (bypasses the Vulnerable-state requirement `RegisterHit` has — used
by PuffUp only). `RobotVisual` tints stunned/knocked-back robots with a dark flicker, same
placeholder-colour convention as Vulnerable/Defeated.

**Wall mutation** lives on `TileMapRenderer`, not `LevelData` — `SetTemporaryWalkable(cell, bool)`
overrides a single cell's walkability without touching the maze asset (Percy calls it, then
reverts after 2s); `DestroyWallAt(cell)` also removes the spawned wall GameObject and never
reverts (Billy, and Gerald's Iron Stampede buff). `GetWallAt(cell)` returns the wall GameObject
for tinting.

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
| Earthquake Roll | Bessie → Percy | Percy phases 3 walls instead of 1 |
| Skip Shatter | Ducky → Woolly | Ducky's next SkipShot spawns 2 wool clones at the destination |
| Double Slam | Bessie → Bessie (2nd+ activation via swap) | Ground Slam radius doubles to 4 tiles |
| Crossfire | Billy → Horace | Rear Kick knockback doubles to 8 tiles |
| Iron Stampede | Bessie → Gerald | Puff Up also destroys walls Gerald is adjacent to |
| Kick and Roll | Horace → Percy | Same buff as Earthquake Roll (identical effect per GDD) |
| Full Fury | 5+ distinct characters used this maze | Immediate: every robot stunned 5s (not a "next use" buff) |

Buffs are stored as one-shot `Pending*` flags **on `ComboSystem` itself**, not on the ability
instance — a flag on e.g. `BounceRollAbility` would be lost the moment Percy is swapped away
(his GameObject is destroyed). Each affected ability calls the matching `Consume*` method at the
top of its `Execute()`.

**`ChooseCharacterScreen`** (`Scripts/UI`) is the real uGUI character-swap panel — see "Screens &
scene flow" below for its full description. The original Phase 4 `CharacterSwapUI` (`OnGUI`,
"functional even if not polished" per spec) was retired once this replaced it. Toggled by Tab
(via the same `InputController.OnSwapMenuToggleInput` event) or Pause's Swap Character button.

### Screens & scene flow (`Scripts/UI`, Phase 5)

**Still single-scene** (see the architecture note at the top) — "scene transitions" are Canvas
panels being shown/hidden, not `SceneManager.LoadScene`. Every top-level screen is a direct child
of the existing `Canvas` GameObject (built by `Phase1ProjectBuilder`, upgraded by
`Phase5ProjectBuilder` to `CanvasScaler.ScaleWithScreenSize`, 1920×1080 reference, 0.5 match —
the "scale properly for different screen sizes" requirement) and is mutually exclusive with every
other top-level screen.

**Flow:** Main Menu → World Map → Gameplay HUD → (Level Complete | Level Failed) → World
Map. World Map itself is just Map.png with two bottom-corner icon buttons (Play/Home, same
convention as Main Menu's own Play/Settings — see the World Map bullet under "Landing/
Gameplay-HUD cleanup" below); tapping Play (`WorldMapController.OnPlayTapped`) calls `GameManager.LoadLevel` and
`SceneTransitionManager.ShowOnly` directly — there is no intermediate "VS" matchup screen and no
countdown; gameplay starts immediately. (A `MatchupScreenController` screen existed here through
Phase 5 but was removed later — see "Removed: Matchup screen" below.)
**`SceneTransitionManager`** (`Core`) is the single place this is orchestrated: `ShowOnly
(GameObject)` deactivates every screen in its `screenRoots` array and activates just the target,
wrapped in a black-`CanvasGroup` fade (`TransitionTo(Action swapScreens)` is the more general form
`ShowOnly` is built on, for cases — none currently — that need a custom swap instead of "hide all,
show one"). Screen controllers never call `SetActive` on each other directly; they call
`SceneTransitionManager.Instance.ShowOnly(targetScreen)`.

**Overlays are NOT in `screenRoots`** — Pause, Settings, the Store "coming soon" placeholder, and
New Character Unlock layer on top of whatever's currently showing (almost always Gameplay or
Level Complete) rather than replacing it, and manage their own `SetActive` directly. This is why
`PauseMenuController`/`SettingsPanel` don't fade — they're instant show/hide, matching "semi-
transparent overlay dims gameplay" from the spec more literally than a full-screen fade would.

**Removed: Matchup screen.** The Phase 5 "VS" card screen (`MatchupScreenController`, shown between
World Map and Gameplay — character card vs. up to 3 robot cards, plus a 3-2-1-GO countdown) was
deleted entirely after playtesting — it read as tonally mismatched with the rest of the game.
`WorldMapController.OnPlayTapped` now calls `GameManager.LoadLevel` + `SceneTransitionManager
.ShowOnly(gameplayScreen)` directly; there is no countdown replacement. `Phase5ProjectBuilder` no
longer builds a `MatchupScreen` (removed from `screenRoots` and `WireCrossReferences`),
`ArtWiringBuilder` no longer wires `matchup.png` or its buttons (the file itself is unused now —
left on disk, not deleted), and `Phase5Test`'s World-Map-to-gameplay check calls
`GameManager.LoadLevel`/`ShowOnly` directly instead of driving a Matchup Play button. If you're
reading older commit history or design notes that mention "Matchup," they predate this removal.

**Landing/Gameplay-HUD cleanup (post-Phase-5):** once real art landed, screens got stripped down
from their original Phase 5 layouts:

- **Main Menu** (`MainMenuController`) is now just two icon buttons directly on `landing.png`
  (which already bakes in the "FARM FURY ARCADE" logo) — `PlayButton` bottom-left → World Map,
  `SettingsButton` bottom-right → the Settings overlay. The old vertical button stack (Character
  Roster/Daily Challenge/Store/Leaderboards) and its duplicate "Title" text are gone, along with
  the `MainMenuScreen/Content` vertical group they lived in. Those four screens/systems still get
  built by `Phase5ProjectBuilder.BuildAll` and still work — they just have no entry point from Main
  Menu anymore, so reaching them today means calling `SceneTransitionManager.ShowOnly` on them
  directly (nothing currently does). `CharacterRosterScreen`/`LeaderboardsScreen` keep their own
  `mainMenuScreen` back-reference for their "Back" buttons regardless.
- **World Map** (`WorldMapController`) similarly lost its top-left `HomeButton` + horizontally-
  scrolling level-marker strip (`LevelMarker`/`StarDisplay`, built via `CreateHorizontalScrollView`)
  — with only 2 `LevelData` assets authored so far, that strip rendered as an unstyled green
  swatch overlapping `Map.png`'s own baked-in "THE FARM" title, with no real per-level layout to
  speak of yet (see "Known gaps" below on the marker-to-path-art alignment that was never
  finished). Replaced with the same bottom-left/right icon-button convention as Main Menu — `Play`
  (bottom-left) calls `OnPlayTapped`, which picks the same "next available" level `CenterOnLevel`
  used to just scroll to (first unlocked level with 0 stars, falling back to the highest level
  reached) and jumps straight into it; `Home` (bottom-right) returns to Main Menu.
  `Phase5ProjectBuilder.BuildLevelMarkerPrefab`/`LevelMarker.cs`/`StarDisplay.cs` are still built —
  same "kept for future re-wiring" treatment as Roster/Store/Leaderboards — for whenever the
  100-level target from the GDD needs a real level-select screen again.
- **Gameplay HUD** (`GameplayHUD`) lost its `SwapButton`/`AbilityButton` (+cooldown ring) —
  Tab (`ChooseCharacterScreen.ToggleOpen`) and Space (`AbilityBase.OnAbilityActivateInput`) still
  trigger both directly via `InputController`, so removing the buttons didn't remove the features.
  `SoundButton`/`HomeButton` were later removed too (per playtest feedback) — both are reachable via
  Pause instead (Settings' music/SFX toggles, Pause's own Quit button) — leaving just a single
  `160x160` `PauseButton`, bottom-left (matching the Main Menu's Play/Settings buttons, safe-area
  inset). A vacant `Btn_plaque.png` backdrop ("SideBackdrop") used to run down the right side as a
  placeholder for future writing/navigation — removed entirely after review, since it had no
  behaviour and read as an oversized, unexplained button. `ScoreText`/`TimerText` were later pulled
  further in from the screen edges (an original inset sat above/outside the backdrop art's own
  safe-area guide once viewed on a device frame), enlarged, and given the `Bangers SDF` cartoon font
  (`ArtWiringBuilder.WireGameplayFont` — bundled with TMP's own Examples & Extras, already has a
  correctly-generated SDF material unlike `Inter-Regular SDF`'s broken shader, so no
  import/generation step needed). `LevelText` (the level name header) was removed outright — it
  duplicated what the World Map marker the player just tapped already established. An on-screen
  **directional pad** (`UI/DirectionalPadController`, right side, diamond layout — `up`/`down`/
  `left`/`right.png`, each already a complete rounded button with no separate background needed)
  was added as a touch-friendly alternative to keyboard/swipe; each button calls
  `InputController.RaiseDirectionInput`, the exact same static event keyboard/swipe already raise,
  so `GridMovement` needs no awareness that a third input source exists.
- Three always-on `OnGUI` debug overlays (`Phase1Test`/`Phase2Test`/`Phase3Test`/`Phase4Test` manual
  test buttons, independent of their `runOnStart` flag) used to render on top of every screen in
  every Play session. `Editor/SceneCleanupBuilder.DisableDebugTestOverlays` (`Farm Fury Arcade >
  Disable Debug Test Overlays`) deactivates all 5 `Phase*Test` GameObjects — safe to re-run, and
  also de-duplicates them (see that file's doc comment for the `GameObject.Find`-only-finds-active
  bug this uncovered in `Phase5ProjectBuilder`'s own `Phase5Test` idempotency check, now fixed to
  look up inactive instances too via `Resources.FindObjectsOfTypeAll`). Re-enable a specific one
  (Inspector checkbox, or its `ContextMenu`) to run its manual test battery again.

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

**`ComboSystem.CombosTriggeredThisMaze`** (added this phase) is what `LevelCompleteController`'s
"combo achievements this run" line reads — a simple ordered name list, reset with everything else
in `ComboSystem.ResetForNewMaze()`.

**`UnlockManager.LastUnlockedBatch`** is how `LevelCompleteController` knows to show
`NewCharacterUnlockScreen` automatically after the star/score celebration finishes, without
`UnlockManager` needing to know anything about UI — it just records what
`CheckUnlocksOnLevelComplete` unlocked on its most recent call.

**`ChooseCharacterScreen`** (real uGUI, `Scripts/UI/ChooseCharacterScreen.cs` +
`CharacterSelectCard.cs`) replaced the Phase 4 `CharacterSwapUI` `OnGUI` panel. Not a
`SceneTransitionManager` screen — like Pause/Settings, it's an overlay shown/hidden directly
(`Show()`/`ToggleOpen()`), temporarily taking Pause's place on top of Gameplay and handing back to
it afterward (`pauseMenuScreen` back-reference). Background is `LoadingScreen Background.png` (the
same barn/night art used behind Settings). One `CharacterSelectCard` per `CharacterData.GetAllCharacterData()`
entry lays out in a fixed 4-column `GridLayoutGroup` (`ChooseCharacterScreen.BuildChooseCharacterScreen`
in `Phase5ProjectBuilder`), each showing `CharacterData.selectCardArt` — a dedicated framed "animal
card" image per character (`Sprites/UI/{Name}_{Species}.png`, own wood-frame border baked in,
distinct from the plain `portraitSprite` front sprite used elsewhere) — or a placeholder square for
any character without one. Locked cards show a "LOCKED" overlay and their `Button.interactable` is
`false`; the active character's card gets a gold glow (`activeHighlight`, a slightly larger Image
behind the card so it peeks out around the edges) and is also non-interactable (can't "swap" to the
character already active). Tapping an eligible card pops it (scale + `SetAsLastSibling`, avoiding a
fight with the `GridLayoutGroup` repositioning it), deducts the same 1-coin cost `CharacterSwapUI`
used to (free if the player has 0), calls `CharacterManager.SwapCharacter`, then closes back to
Pause. Tab still toggles it too, via the same `InputController.OnSwapMenuToggleInput` event
`CharacterSwapUI` used — nothing else needed to change to preserve that shortcut.

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

**`AudioManager`** has no real clips to play yet (see "Art status") — `PlayMusic`
crossfades between two looping `AudioSource`s, `PlaySFX` round-robins a pooled array via
`PlayOneShot`, both respect `SaveManager.MusicOn/SfxOn/MusicVolume/SfxVolume`. Wire real
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
  procedural 14×16 maze, creates `CharacterData_Cluck`, and rewires `Game.unity` with
  `ScoreManager`/`TileMapRenderer`/`InputController` plus the Cluck prefab reference.
  **Idempotent** — safe to re-run after any prefab/data change instead of touching the scene by
  hand. `BuildLevelData01`'s maze is a real Pac-Man-style corridor layout (1-tile-wide paths + wall
  blocks), not the original sparse-2x2-walls-on-open-floor version — a deterministic (seeded)
  randomized recursive backtracker carves the left half (x = 1..`leftHalfMax`, where
  `leftHalfMax = (width - 2) / 2`) as a cell lattice, mirrors it onto the right half for a symmetric
  arcade-maze look, then reopens ~22% of the remaining connector walls so the board has loops
  instead of being a single spanning tree. The warp row (`y=5`), robot factory box (`x=5..8,
  y=6..9`), and player-start clearing (around `(7,2)`) are stamped on top afterward at fixed
  coordinates that Phase 3/4 also hardcode (robot spawn `(7,7)`, water tile cells `(3,11)`/`(10,11)`)
  — those two builders must be updated together with this one if `width`/`height` or the feature
  coordinates ever change again, since none of it is derived automatically across files. The water
  cells specifically are reserved with a `-1` sentinel during generation so the "every remaining
  floor tile becomes a crop kernel" pass doesn't consume them before Phase 4 gets to stamp water
  tiles there. 4 power pellets (one per corner, found via nearest-open-floor search from each
  target corner and mirrored) replace an original fixed 2; 2 small BFS-clumped vegetable patches
  replace scattering single vegetables board-wide.
  `width`/`height` were halved from an original 28×31 — fitting that entire board on screen (see
  "Camera" above) still left individual tiles too small to read comfortably, and zooming in further
  would have cropped the board rather than enlarging it, so the maze's own cell count was shrunk
  instead (fewer, bigger tiles, whole board still fits on screen). `SceneCleanupBuilder.
  FitGameplayCameraToMaze`'s orthographic size (`8`) is tuned to this 14×16 board specifically.
- **`Phase3ProjectBuilder`** (`Phase 3 > Build All`) — the one you actually want day to day now.
  Builds the 6 robot prefabs + `RobotData` assets, adds `PlayerHealth` to the existing Cluck
  prefab, gives `LevelData_01` its 2 spec'd robot spawns (Harvester@2s, Scout@6s, both at (14,15)
  in the factory box), creates `LevelData_05` (levelNumber 4 — a smaller 20×20 maze with 3 robots:
  Harvester/Scout/Patrol) for isolated multi-robot testing, and wires `RobotSpawner`/
  `PowerPelletManager`/`ChaseScoreManager` onto `GameManagers`. Also disables `Phase1Test`'s and
  `Phase2Test`'s `runOnStart` (see below for why). **Idempotent** — safe to re-run. Depends on
  Phase 2's prefabs/`LevelData_01` already existing.
- **`Phase4ProjectBuilder`** (`Phase 4 > Build All`) — the one you actually want day to day now.
  Builds `CharacterData` for all 8 characters, adds `CharacterBase`+`EggDropAbility` to the
  existing Cluck prefab, builds the 7 remaining character prefabs plus every ability's sub-prefab
  (`Egg`, `Shockwave`, `BounceTrail`, `WoollyClone`, `WaterTile`), adds one water tile pair to
  `LevelData_01` (row 25, verified clear of the warp row/factory box/wall-block pattern before
  overwriting — logs a warning and skips instead of corrupting the maze if that ever stops being
  true), and wires `CharacterManager`/`ComboSystem`/`UnlockManager`/`CameraShake` into `Game.unity`
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
  (Main Menu, World Map + `LevelMarker` prefab, Gameplay HUD + combo banner, Pause,
  Settings, Store "coming soon", Level Complete + New Character Unlock, Level Failed, Character
  Roster + `RosterCard` prefab, Leaderboards), wires `SceneTransitionManager`/`AudioManager`/
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
  `Farm Fury Arcade > Fit Gameplay Camera To Maze`) — small targeted scene-hygiene fixes that are
  neither "wire art" nor "rebuild a phase's content." `DisableDebugTestOverlays` deactivates (and
  de-duplicates) the 5 `Phase*Test` GameObjects; `FitGameplayCameraToMaze` (renamed from
  `ZoomInGameplayCamera` — it now does the opposite) sets the Main Camera's `orthographicSize` to
  `8` so the whole board fits on screen and ensures a `CameraFollow` component exists. Both safe
  to re-run.

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
verifying: only Main Menu active at startup; Main Menu → World Map → Gameplay activates the right
screen at each step and reaches `GameState.Playing` (since the Matchup screen's removal, this
reproduces `WorldMapController.OnPlayTapped`'s effect — `GameManager.LoadLevel` +
`SceneTransitionManager.ShowOnly` — directly rather than driving the World Map's own Play button);
the 7 required HUD elements exist (score/level/timer/portrait/
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
  Right0,Right1]`). Cluck is now the one exception with real second-frame walk-cycle art and a
  genuine dedicated Right pose (`Cluck_right.png`/`Cluck_rightwalk2.png` for Right0/Right1,
  `Cluck_LeftWalk.png` for Left1) — `CharacterData.hasDedicatedRightArt` (new field) is `true` for
  her, which tells `CharacterAnimator` to skip its usual `flipX` mirroring of the Left sprite for
  Right-facing (every other character still has no dedicated Right art, so still gets mirrored).
  Every other character has only one pose per direction (no walk-cycle frames yet), so each
  direction's two slots repeat the same sprite — harmless no-op frame-toggle until second frames
  land for them too. Ducky has front/back only (no left/right art uploaded for her); her Left/Right
  slots fall back to the front sprite, so Left/Right facing won't read correctly for her until
  profile art exists — documented inline in `ArtWiringBuilder.SetWalkFrames`. Horace and Billy still
  have no art and remain solid-colour placeholders. `Gerald_effect.png` was uploaded but is unwired
  — `PuffUpAbility` has no spawned effect object (it just scales Gerald's own sprite 3x), unlike
  Bessie/Percy/Woolly's abilities, each of which spawns a dedicated effect prefab; wiring it in
  would mean adding a new prefab + a spawn call in `PuffUpAbility`, a gameplay change, not just art.
- **Robots** — `RobotVisual.SetDirectionalSprites` now takes optional `left`/`right` sprites in
  addition to `front`/`back` (extended from the Harvester-only front/back version). Patrol has a
  full 4-direction set; Scout and Drifter have front/left/right (no back — Up falls back to
  front); Heavy has front/back only (same pattern Harvester originally used); Harvester and Drone
  still have no left/right art. Drone has no art at all yet and keeps the colour-tint-only
  placeholder behaviour for its normal states.
- **Robot Defeated state** — all 6 robot prefabs (including Drone) now have `RobotEyes.png` wired
  via the new `RobotVisual.SetDefeatedSprite`; while Defeated/Returning, `RobotVisual.Update`
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
- **Gameplay backdrop** — `Wheatfield_background.png` (uploaded early on, previously unused once
  LevelComplete/Failed/Pause got their own dedicated panel art) is now a `GameplayBackdrop`
  SpriteRenderer behind the maze (see "Camera" above) — fills the space the fit-to-maze camera
  shows around the 14×16 board. Recenters/rescales itself off `LevelData_01`'s own
  `mazeWidth`/`mazeHeight` each time `ArtWiringBuilder.WireAll` runs, so it doesn't need manual
  retuning if the maze's dimensions ever change again.
- **Power pellets** — spawn with a real tier instead of always Sunflower. `TileMapRenderer.
  ConfigurePelletTier` rolls a weighted random tier per pellet (Sunflower 70% / GoldenWheat 20% /
  Rainbow 10%, matching the "RarePellets" art naming) and swaps in `sunflowerPelletSprite`
  (now `RarePellets_sunflower.png` — a dedicated sunflower sprite replaced the earlier placeholder
  use of `Power_1.png`, which is a Cluck power-up icon, not a pellet) / `goldenWheatPelletSprite`
  (`RarePellets_maize.png`) / `rainbowPelletSprite` (`RarePellets_apple.png`) accordingly.
  `Power_1.png`/`CluckPower_2`/`CluckPower_3` remain unwired — there's no "Cluck looks different
  while powered up" feature in the code for them to hook into. Collecting a GoldenWheat or Rainbow
  (i.e. non-Sunflower/"rare") pellet also spawns `PelletCollectBurst`
  (`Gameplay/PelletCollectBurst.cs`, wired via `PowerPelletPickup.collectEffectPrefab` and called
  from `CropCollector` right before the pellet is destroyed) — a procedural ring of
  placeholder-coloured squares that fly outward and fade, since no dedicated sparkle/particle art
  exists yet. Swap it for a real ParticleSystem/sprite-sheet burst once that art lands;
  `PelletCollectBurst.Configure(PowerPelletType)` is the only method a replacement needs to keep.
- **Ability effects** — `Shockwave` (Bessie's Ground Slam) uses `BessieSlam.png`, `BounceTrail`
  (Percy's Bounce Roll) uses `Percy_effect.png`, `WoollyClone` (Woolly's Triple Clone) uses
  `Wooly_effect.png`. Cluck's Egg Drop (`Egg` prefab) still has no dedicated art — its placeholder
  went through two rounds of "still not visible" playtest reports: a near-white/tan tint blended
  into `CornTiles.png`'s similarly warm-toned ground art, and the pure-white follow-up fix still
  had too little contrast against the real ground/corn art. It's now a saturated hot pink
  (`#FF1493`), and its `sortingOrder` was raised above the character sprite's — the egg dropped at
  offset 0 (see `EggDropAbility.TileOffsetsBehind`) spawns directly under Cluck's own feet, so it
  needs to draw on top of her, not just the ground, to be visible there at all.
- **UI backgrounds** — `MainMenuScreen` uses `landing.png` (which has "FARM FURY ARCADE" baked
  into the art), `WorldMapScreen` uses `Map.png`,
  `LevelCompleteScreen`/`LevelFailedScreen`/`PauseOverlay` use dedicated `LevelComplete.png`/
  `LevelFailed.png`/`Paused.png` panel art (previously these 3 all reused `World1_Cornfield.png`/
  `Wheatfield_background.png` as stand-ins; those two files are now unused). `matchup.png` is also
  now unused — left on disk, not deleted — after the Matchup screen's removal (see "Removed:
  Matchup screen"). Because
  `landing.png`'s logo sits centred in the upper half, `MainMenuScreen/Content` (the button stack)
  was re-anchored to the bottom of the screen (`anchorMin/Max = (0.5, 0)`, `pivot = (0.5, 0)`,
  `anchoredPosition = (0, 30)`) instead of screen-center, so it no longer overlaps the art's logo.
  `LoadingScreen Background.png` and `Logo.png` were uploaded but aren't wired anywhere — there's
  no dedicated loading screen in the current screen flow, and `Logo.png` has no obvious slot since
  `landing.png` already bakes the logo into Main Menu.
- **`Paused.png` is on its own aspect-locked child, not the overlay root.** `Paused.png` is a
  square (2048x2048) parchment/frame card with its 5 button rows (Resume/Swap Character/Restart/
  Settings/Quit) baked directly into the art. It used to be set as `PauseOverlay`'s own root
  `Image`, which stretches full-screen — on a real landscape aspect that non-uniformly stretched
  the square art, squashing its baked-in rows together and dragging the separately-wired button
  art (`Resume.png` etc., positioned by hand-tuned fractions) out of alignment with them.
  `Phase5ProjectBuilder.BuildPauseMenu` now parents a `PanelArt` child under the root, locked to a
  1:1 aspect via `AspectRatioFitter` (`FitInParent`) so it stays centred and undistorted at any
  screen aspect; the root's own `Image` goes back to being the plain full-screen black dim every
  other overlay uses. All 5 buttons moved under `PanelArt` so their fractions stay aligned with it.
- **Settings uses one `Btn_plaque.png` per control row, not one stretched behind everything.**
  `Btn_plaque.png` is a small wide rounded-pill button graphic; an earlier version stretched a
  single instance of it behind the *entire* control stack (toggles, sliders, dropdown, buttons),
  which distorted it into an unreadable blob with every control floating over it unframed.
  `Phase5ProjectBuilder.WrapInPlaqueRow` now gives each row (Music, SFX, Vibration, Left-Handed,
  Language) its own plaque, 9-sliced (`ArtWiringBuilder` computes `spriteBorder` for `Btn_plaque.png`
  as a fraction of its own pixel size, and `SetImageSprite` applies `Image.Type.Sliced` to every
  wired button sprite — a no-op for sprites with no configured border, so this didn't need to be
  special-cased per call site) so the pill's rounded caps survive being stretched much wider than
  the source art's own aspect. Settings text was also left on TMP's default `LiberationSans SDF`
  font (see "TextMeshPro bootstrap" below) — `ArtWiringBuilder.WireSettingsFont` now wires the same
  `Bangers SDF` cartoon font Gameplay HUD's score/timer use, so Settings reads consistently with
  the rest of the UI.
- **Maze wall/ground/warp-tunnel tiles** — `Wall_CornField`/`Ground_CornField`/`WarpTunnel`
  prefabs (each instantiated per-cell by `TileMapRenderer` at scale `TileMapRenderer.CellSize`,
  same convention crops/pellets already used) now use `CornTiles.png`/`FloorTile.png`/
  `WarpTile.png` respectively instead of `PlaceholderSprite` colour squares — each uploaded file is
  a single complete tile image, not a tileset, so no atlas-slicing was needed.
- **Card frames** — `Card.png` wired onto the New Character Unlock screen's card `Image` and the
  `RosterCard` prefab's root `Image`.
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
  buttons across every screen (Main Menu, World Map, Gameplay HUD, Pause, Settings,
  Store, Level Complete/Failed, Roster, Leaderboards) via `ArtWiringBuilder.WireButtons` —
  buttons with no specific icon art (Restart, Replay, Retry, Store, Leaderboards, Roster, Daily
  Challenge) share the generic `Btn_plaque.png` background. `Btn_nosound.png`
  is wired now too, as `GameplayHUD`'s `soundOffSprite` (paired with `Btn_music.png` as
  `soundOnSprite`) — the HUD's new `SoundButton` is the first place either sprite swaps at
  runtime; `SettingsPanel`'s separate Music toggle still only has one icon slot and doesn't swap
  art on/off, just its checkmark.
- **Coin icon** — `Collectable Coin.png` was added as a new `CoinIcon` Image next to
  `LevelCompleteScreen`'s existing "+N coins" text (that field never had an icon slot before, so
  `ArtWiringBuilder` wraps it in a new `CoinsRow` horizontal group rather than adding a dedicated
  serialized field for one icon).
- **App icon** — `AppIcon.png` set as the Unity Player Settings icon (`PlayerSettings.
  SetIconsForTargetGroup`, Standalone + the default/`Unknown` group) — a project-settings change,
  not a scene/prefab one.

**Still missing / not wired:** Horace/Billy character art (Gerald now has art — see above), Drone
robot art, a Vulnerable-state robot sprite, Cluck's Egg Drop effect art, Gerald's Puff Up effect
art (`Gerald_effect.png`, uploaded but unwired — see above), and the branding Logo/Loading Screen
background (both uploaded, neither wired — see above).

**Texture import convention:** `ArtWiringBuilder.ConfigureSpriteImporters` sets every wired
texture's `spritePixelsPerUnit` to that texture's own pixel width (via `TextureImporter.
GetSourceTextureWidthAndHeight`), not a fixed value — this makes a sprite at `localScale = 1`
fill exactly one maze grid cell (1 world unit), matching `PlaceholderSprite`'s 1px==1unit@scale1
convention that every prefab's existing `localScale` (e.g. crop 0.35, pellet 0.7) was already
tuned around, so no prefab scale values needed to change when real art went in.

**Audio** — `Audio/Music/BackgroundMusic.mp3` is wired onto `AudioManager.backgroundMusicClip`
(`ArtWiringBuilder.WireAudio`) and starts looping automatically in `AudioManager.Start()` as soon
as the app launches (no fade-in — that's reserved for switching tracks later, not the initial
start). `SaveManager.MusicVolume`'s default was lowered from `1f` to `0.5f` so it plays soft/
background-level out of the box rather than at full volume; still fully overridable via the
Settings slider.

`Audio/SFX/EatRobot.mp3` (despite living in the SFX folder, it's used as a second **music** track,
not a one-shot) plays for the exact duration a power pellet is active — `PowerPelletManager`
crossfades to it via `AudioManager.PlayEatRobotMusic()` on the `false → true` activation edge, and
crossfades back to the regular background track via `ResumeBackgroundMusic()` when the countdown
reaches zero (`PowerPelletManager.CountDown`'s end). Both go through the same `PlayMusic`
crossfade `AudioManager` already had — no new fade logic needed, just two named entry points.

All 5 SFX clips under `Audio/SFX/` are wired to a specific gameplay trigger, each via a named
`AudioManager` method (`PlayXSfx()`, not a raw `PlaySFX(clip)` call, so call sites read as what
happened rather than which clip field to reach into):

| Clip | Method | Fires from |
|---|---|---|
| `Animal_death.mp3` | `PlayAnimalDeathSfx` | `PlayerHealth.DeathSequence` (start of the death sequence) |
| `CornPickup.mp3` | `PlayCornPickupSfx` | `CropCollector`, only when `CropPickup.cropType == CropType.Corn` (not Vegetable) |
| `PowerReady.mp3` | `PlayPowerReadySfx` | `PowerPelletManager.ActivatePower`, only on the `false → true` edge (a pellet eaten while power is already active just refreshes the duration, doesn't replay the cue) |
| `RarePellet_pickup.mp3` | `PlayRarePelletPickupSfx` | `CropCollector`, only when `pellet.pelletType != PowerPelletType.Sunflower` — same "rare tier" gate `PelletCollectBurst` uses |
| `RobotSpawn.mp3` | `PlayRobotRespawnSfx` | `RobotBase.ArriveAtFactory` — a defeated robot's respawn back to Chase, not `RobotSpawner`'s initial level-start spawns |

When more art lands, wire it into the existing prefabs (`Prefabs/Characters/`, `Prefabs/Robots/`,
`Prefabs/Blocks/`) via `ArtWiringBuilder` rather than creating new prefabs.

## Testing

Desktop: arrow keys or WASD. Mobile/Editor: swipe (or mouse-drag in Play mode) — 50px minimum
distance, dominant axis wins for diagonals. Tunable parameters if movement doesn't feel right:

- `GridMovement.speed` (comes from `CharacterData.movementSpeed` — 3.2 Cluck, 3.0 Bessie, 4.6 Percy,
  3.8 Woolly, 4.2 Ducky, 4.2 Horace, 3.4 Gerald, 3.4 Billy; all ~0.76x their original values —
  movement still read as too fast after `CellSize` scaling even accounting for its effect on
  perceived speed. The originals for Percy/Ducky/Horace (6, 5.5, 5.5) also exceeded
  `movementSpeed`'s own `[Range(1,5)]` inspector hint; the scaled-down values now fit inside it)
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
  done.
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
- **Phase 5** (progression & UI): full screen flow (Main Menu → World Map → Gameplay →
  Level Complete/Failed, plus Character Roster/Leaderboards/Settings/a Store placeholder) as real
  uGUI with TextMeshPro, `SceneTransitionManager`-driven fades, score breakdown + star rating +
  coin reward on level complete, automatic New Character Unlock celebration, `AudioManager`
  (API-complete, no clips yet), `DailyChallengeManager` foundation (5 challenge types, date-seeded,
  reuses `LevelData_01` rather than a distinct maze), local `LeaderboardManager` — done.

## Known gaps / flagged for Phase 6
- **Store, Character Roster, and Leaderboards have no Main Menu entry point** — removed in the
  landing-page cleanup (see "Landing/Gameplay-HUD cleanup" above) in favour of just Play/Settings.
  All 3 screens still exist and build correctly; reaching them today requires calling
  `SceneTransitionManager.ShowOnly` directly, since nothing currently does. Daily Challenge is
  different: it isn't a separate screen, just an objective overlaid on `LevelData_01` (index
  `DailyChallengeLevelIndex`, 0) — since that's the same level the normal World Map flow already
  plays, `DailyChallengeManager.CheckCompletionOnLevelEnd` fires on any ordinary playthrough of
  level 0, no special entry point needed. (Before the Matchup screen's removal, that screen's
  `ShowForLevel` was one way to jump straight to a given level for testing; that shortcut is gone,
  but Daily Challenge completion never depended on it.)
- **Store is a placeholder** ("coming in Phase 6" panel) per spec's own scope note — no cosmetics
  UI or IAP hookup exists.
- **Restore Progress** (Settings) logs and does nothing — real implementation needs cloud save
  (Phase 6).
- **Leaderboards has no cloud sync** — local-only, per spec.
- **`DailyChallengeManager.CharacterLocked` isn't enforced**, only checked after the fact — a
  player can freely swap characters during a Character-Locked daily challenge; the run just won't
  register as completed if more than one character was used. Real enforcement needs
  `CharacterManager.CanSwapTo` to know about the active challenge.
- **World Map has no visible level-select UI at all** — it dropped the horizontal scroll strip of
  numbered markers entirely (see the World Map bullet under "Landing/Gameplay-HUD cleanup") in
  favor of a single Play button that jumps straight to the next available level, since with only
  2 `LevelData` assets against the GDD's 100-level target there was no real per-level layout for
  markers to align with anyway. `Map.png` (an isometric winding-path farm illustration) is still
  `WorldMapScreen`'s background purely as decoration now. `LevelMarker`/`StarDisplay`/
  `Phase5ProjectBuilder.BuildLevelMarkerPrefab` are kept built but unwired — reconnecting a real
  level-select screen (with markers hand-placed along the art's path) is content-authoring scope
  once there are enough levels to need one, not a rewrite.
- **No ability icon sprites, and only partial portrait art** — the HUD portrait
  (`GameplayHUD.characterPortrait`, via `RefreshPortrait`) uses `CharacterData.portraitSprite`
  (front sprite) where a character has real art (see "Art status"); Roster cards still use
  solid-colour placeholders, and no dedicated ability icons exist anywhere.

## UX flow

```
Main Menu ──Play──▶ World Map ──Play (next available level, no countdown)──▶ Gameplay HUD
    │                    │▲                                                            │▲  │
    │                    ││ Home                                          Pause(P)────▶│└──┼─▶ Resume
    │                    │└─────────────────────────────────────────────────────────────┘  │
    │                    │                                                                  │
    │                    │◀────────────────────── Home ─────────────────────┐               │
    │                    │                                                  │               │
    │                    │◀── Replay / Next Level (loops back to Gameplay) ─┤               │
    │                    │                                          Level Complete◀─────────┤
    │                    └──────────────────────────────────────────────────┘   (all crops   │
    │                                                                            collected)   │
    │                                                                                         │
    │                                                            Level Failed◀────────────────┘
    │                                                             (Pause ▸ Quit to Menu)
    │
    └──Settings (gear)────▶ modal overlay (music/sfx/volume/vibration/language/handedness,
                             reset progress w/ confirm) ──X──▶ wherever it was opened from
```

(A Matchup "VS" card screen with a 3-2-1-GO countdown used to sit between World Map and Gameplay
HUD — removed entirely; see "Removed: Matchup screen" above.)

Pause and Settings are **overlays** (layer on top of whatever's showing, dim it, don't replace it)
— everything else in this diagram is a **screen swap** through `SceneTransitionManager.ShowOnly`.
New Character Unlock is a special case: not reachable by navigation, it's triggered automatically
by `LevelCompleteController` partway through its own celebration sequence, whenever
`UnlockManager.LastUnlockedBatch` isn't empty.

**Character Roster, Store, and Leaderboards are no longer reachable from Main
Menu** (removed in the landing-page cleanup — see "Landing/Gameplay-HUD cleanup" and the
matching "Known gaps" entry). Each screen still exists and still works exactly as described
above if shown directly via `SceneTransitionManager.ShowOnly` — there's just no button anywhere
that does so today. Daily Challenge is unaffected, since it isn't a separate screen — see the
matching "Known gaps" entry.
