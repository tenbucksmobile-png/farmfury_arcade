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
                  CharacterSwapUI is still OnGUI (a Phase 4 leftover, not rebuilt this phase —
                  see that section for why).
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
static `OnDirectionInput` event from keyboard (WASD/arrows, via Input System) and swipe/pointer
gestures (works with mouse drag in the Editor too); `OnAbilityActivateInput` (Space) and
`OnSwapMenuToggleInput` (Tab) were added in Phase 4. `GridMovement`/`AbilityBase` both subscribe
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

**Camera:** there is no camera-follow script — the Main Camera is static, orthographic size `16`,
positioned at `(14, 15, -10)`, sized to keep the entire 28×31 `LevelData_01` maze on screen at
once (classic-arcade Pac-Man framing, not a scrolling/following camera). Orthographic size was
originally `8` (only showing about half the maze's height), which made Cluck's spawn near the
bottom of the maze (`playerStartPosition (14, 3)`) render clean off-screen — looked exactly like a
missing-sprite bug but was actually a framing bug. If a future level's `mazeWidth`/`mazeHeight`
is taller than `LevelData_01`'s, the camera won't automatically reframe to fit it (nothing recomputes
orthographic size from `LevelData` today) — check this first if a new level's edges get clipped.

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

**No art yet:** `RobotVisual` swaps the placeholder `SpriteRenderer` colour for
Vulnerable (blue, flashing white in the last 2s) / Defeated-or-Returning (pale) instead of
swapping sprite sheets — same placeholder convention as everywhere else. Replace with real
`Robot_Vulnerable_Walk`/`Robot_Defeated_Eyes` sprite swaps when art lands, then delete this
component.

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
`CharacterSwapUI` on confirm, so affordability never actually blocks a swap. A short fade-in
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

**`CharacterSwapUI`** is deliberately `OnGUI`, not real uGUI — "functional even if not polished"
per spec, matching every other test/debug overlay in this codebase; Phase 5 replaces it. Toggled
by Tab.

### Screens & scene flow (`Scripts/UI`, Phase 5)

**Still single-scene** (see the architecture note at the top) — "scene transitions" are Canvas
panels being shown/hidden, not `SceneManager.LoadScene`. Every top-level screen is a direct child
of the existing `Canvas` GameObject (built by `Phase1ProjectBuilder`, upgraded by
`Phase5ProjectBuilder` to `CanvasScaler.ScaleWithScreenSize`, 1920×1080 reference, 0.5 match —
the "scale properly for different screen sizes" requirement) and is mutually exclusive with every
other top-level screen.

**Flow:** Main Menu → World Map → Matchup → Gameplay HUD → (Level Complete | Level Failed) → World
Map. **`SceneTransitionManager`** (`Core`) is the single place this is orchestrated: `ShowOnly
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

**`CharacterSwapUI` was not rebuilt as real uGUI this phase** — it's the one piece of Phase 4 that
stays `OnGUI`, reused as-is by both `GameplayHUD`'s Swap button and `PauseMenuController`'s Swap
button (both just call its now-public `ToggleOpen()`). Flagged as a real gap, not an oversight —
see "Known gaps" below.

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
  procedural 28×31 maze, creates `CharacterData_Cluck`, and rewires `Game.unity` with
  `ScoreManager`/`TileMapRenderer`/`InputController` plus the Cluck prefab reference.
  **Idempotent** — safe to re-run after any prefab/data change instead of touching the scene by
  hand.
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
  true), and wires `CharacterManager`/`ComboSystem`/`UnlockManager`/`CharacterSwapUI`/
  `CameraShake` into `Game.unity`. Also disables `Phase3Test`'s `runOnStart` (see below).
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
  (Main Menu, World Map + `LevelMarker` prefab, Matchup, Gameplay HUD + combo banner, Pause,
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
  (Cluck, Harvester, crop/pellet prefabs, `TileMapRenderer`'s 3 pellet-tier sprites, UI screen
  backgrounds, the Level Complete coin icon) — it does not regenerate prefabs or screens from
  scratch, only sets sprite references (and configures each new texture's import settings —
  Sprite type, `spritePixelsPerUnit` = texture width, alpha transparency — the first time it sees
  a path). Safe to re-run; re-running with the same art already wired is a no-op except where it
  intentionally always re-applies (e.g. sprite references). Also has a narrower
  `Farm Fury Arcade > Reposition Main Menu Buttons` entry (`RepositionMainMenuContent`) that only
  re-anchors `MainMenuScreen/Content`, for when just the layout needs adjusting without touching
  sprite wiring.

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
entry points (`MatchupScreenController.ShowForLevel`, button `onClick.Invoke()` calls) rather than
simulating real clicks/taps, verifying: only Main Menu active at startup; Main Menu → World Map →
Matchup → (countdown) → Gameplay activates the right screen at each step and reaches
`GameState.Playing`; all 7 required HUD elements exist and the ability cooldown ring's
`fillAmount` reflects an active cooldown; Pause freezes `Time.timeScale` and shows/hides its
overlay correctly; `GameManager.EndLevel(true)` produces a well-formed `LevelResult` (1-3 stars,
positive score/coins) and `LevelCompleteScreen` activates automatically via `GameplayHUD`'s
state-watch; `SaveManager.GetLevelBestScore` persists the result; and toggling
`SaveManager.MusicOn` round-trips correctly. See the batch-mode timing gotcha above for why every
wait is a poll, not a fixed duration.

## Art status

There's still no Kling AI / Suno / asset-import pipeline wired into this Claude Code session — art
gets dropped into `Assets/_Project/Sprites/{Characters,Robots,Environment,UI}` by hand and then
wired into existing prefabs/UI via `Editor/ArtWiringBuilder.cs` (`Farm Fury Arcade > Wire Uploaded
Art`), not generated by the tooling itself. Anything not yet wired still falls back to a
solid-color placeholder square generated at runtime by `Utilities/PlaceholderSprite`, using hex
values from the GDD's color palette where one exists (e.g. walls = Wall Brown `#4A2C1A`).

**Wired so far:**
- **Cluck** — `Cluck_front/back/left` cover Down/Up/Left; there's no Right art, so
  `CharacterAnimator` flips the Left sprite horizontally (`spriteRenderer.flipX`) when facing
  Right instead. Only one pose per direction exists (no walk-cycle frames yet), so
  `CharacterData_Cluck.walkAnimationFrames`' 8 slots just repeat each direction's single sprite
  twice — `CharacterAnimator`'s frame-toggle becomes a harmless no-op until a second walk frame
  is added per direction.
- **Harvester robot** — `HarvestorRobot_front/back` wired via `RobotVisual.SetDirectionalSprites`
  (new, optional `frontSprite`/`backSprite` fields). Facing Up shows the back sprite, everything
  else (including idle) shows front — there's no left/right robot art. Every other robot type
  (Scout/Patrol/Drifter/Heavy/Drone) has no art yet and keeps the colour-tint-only placeholder
  behaviour, since those fields stay null for them.
- **Pickups** — crop kernel uses `CornKernel.png`, the single vegetable pickup uses `carrot.png`
  (3 other vegetable sprites — cabbage/pumpkin/tomato — were uploaded but aren't wired in; the
  architecture only supports one vegetable sprite per maze right now).
- **Power pellets** — now spawn with a real tier instead of always Sunflower. `TileMapRenderer.
  ConfigurePelletTier` rolls a weighted random tier per pellet (Sunflower 70% / GoldenWheat 20% /
  Rainbow 10%, matching the "RarePellets" art naming) and swaps in `sunflowerPelletSprite`
  (`Power_1.png`, plain egg) / `goldenWheatPelletSprite` (`RarePellets_maize.png`) /
  `rainbowPelletSprite` (`RarePellets_apple.png`) accordingly. `Power_1.png` is also the prefab's
  static default sprite. Two other uploaded sprites — `CluckPower_2`/`CluckPower_3` (a cracking
  egg / golden sunburst) — are intentionally **not** wired to anything; there's no "Cluck looks
  different while powered up" feature in the code for them to hook into.
- **UI backgrounds** — `MainMenuScreen` uses `landing.png` (which has "FARM FURY ARCADE" baked
  into the art), `WorldMapScreen` uses `Map.png`, `MatchupScreen` uses `World1_Cornfield.png`,
  `LevelCompleteScreen`/`LevelFailedScreen` share `Wheatfield_background.png`. Because
  `landing.png`'s logo sits centred in the upper half, `MainMenuScreen/Content` (the button stack)
  was re-anchored to the bottom of the screen (`anchorMin/Max = (0.5, 0)`, `pivot = (0.5, 0)`,
  `anchoredPosition = (0, 30)`) instead of screen-center, so it no longer overlaps the art's logo.
  `LoadingScreen Background.png` was uploaded but isn't wired anywhere — there's no dedicated
  loading screen in the current screen flow (see "Screens & scene flow").
- **Coin icon** — `Collectable Coin.png` was added as a new `CoinIcon` Image next to
  `LevelCompleteScreen`'s existing "+N coins" text (that field never had an icon slot before, so
  `ArtWiringBuilder` wraps it in a new `CoinsRow` horizontal group rather than adding a dedicated
  serialized field for one icon).

**Texture import convention:** `ArtWiringBuilder.ConfigureSpriteImporters` sets every wired
texture's `spritePixelsPerUnit` to that texture's own pixel width (via `TextureImporter.
GetSourceTextureWidthAndHeight`), not a fixed value — this makes a sprite at `localScale = 1`
fill exactly one maze grid cell (1 world unit), matching `PlaceholderSprite`'s 1px==1unit@scale1
convention that every prefab's existing `localScale` (e.g. crop 0.35, pellet 0.7) was already
tuned around, so no prefab scale values needed to change when real art went in.

`Audio/` is still empty — no audio pipeline yet. When more art lands, wire it into the existing
prefabs (`Prefabs/Characters/`, `Prefabs/Robots/`, `Prefabs/Blocks/`) via `ArtWiringBuilder` rather
than creating new prefabs.

## Testing

Desktop: arrow keys or WASD. Mobile/Editor: swipe (or mouse-drag in Play mode) — 50px minimum
distance, dominant axis wins for diagonals. Tunable parameters if movement doesn't feel right:

- `GridMovement.speed` (comes from `CharacterData.movementSpeed` — 5 Cluck, 4 Bessie, 6 Percy,
  5 Woolly, 5.5 Ducky, 5.5 Horace, 4.5 Gerald, 4.5 Billy)
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
  swap UI (`CharacterSwapUI`, Tab to open) — done.
- **Phase 5** (progression & UI): full screen flow (Main Menu → World Map → Matchup → Gameplay →
  Level Complete/Failed, plus Character Roster/Leaderboards/Settings/a Store placeholder) as real
  uGUI with TextMeshPro, `SceneTransitionManager`-driven fades, score breakdown + star rating +
  coin reward on level complete, automatic New Character Unlock celebration, `AudioManager`
  (API-complete, no clips yet), `DailyChallengeManager` foundation (5 challenge types, date-seeded,
  reuses `LevelData_01` rather than a distinct maze), local `LeaderboardManager` — done.

## Known gaps / flagged for Phase 6

- **`CharacterSwapUI` is still `OnGUI`**, not rebuilt as uGUI this phase — it works (both the HUD
  and Pause swap buttons call its `ToggleOpen()`), but visually it's the debug-style panel from
  Phase 4, not styled to match the rest of Phase 5's screens. Real fix: rebuild it as a proper
  panel via `Phase5ProjectBuilder`/`UIBuilderHelpers` and delete the `OnGUI` version.
- **Store is a placeholder** ("coming in Phase 6" panel) per spec's own scope note — no cosmetics
  UI or IAP hookup exists.
- **Restore Progress** (Settings) logs and does nothing — real implementation needs cloud save
  (Phase 6).
- **Leaderboards has no cloud sync** — local-only, per spec.
- **`DailyChallengeManager.CharacterLocked` isn't enforced**, only checked after the fact — a
  player can freely swap characters during a Character-Locked daily challenge; the run just won't
  register as completed if more than one character was used. Real enforcement needs
  `CharacterManager.CanSwapTo` to know about the active challenge.
- **World Map is still a horizontal scroll strip of numbered markers layered on top of real
  background art** (`Map.png`, an isometric winding-path farm illustration, wired as
  `WorldMapScreen`'s background) — the markers themselves are **not** yet repositioned to align
  with the path drawn in that art; they still lay out via `WorldMapController`/`LevelMarker`'s
  horizontal scroll, independent of the background. There's no hand-placed path-node data yet
  (only 2 `LevelData` assets exist against the GDD's 100-level target). The underlying system is
  level-count-agnostic, so hand-placing marker positions along the art's path later is a content
  change, not a rewrite.
- **No character portrait/ability icon sprites** — HUD portrait, Matchup character/robot cards, and
  Roster cards all use solid-colour placeholders (see "Art status").
- **AudioManager has zero real clips wired** — full API, fully wired to `SaveManager` settings, but
  silent until `AudioClip`s are imported and assigned.

## UX flow

```
Main Menu ──Play──▶ World Map ──tap unlocked level──▶ Matchup ──Play (3,2,1,GO!)──▶ Gameplay HUD
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
    ├──Character Roster──▶ (unlock progress, 8 cards) ──Back──▶ Main Menu
    ├──Daily Challenge────▶ Matchup (LevelData_01, objective banner) ──▶ … same as above
    ├──Store──────────────▶ "Coming in Phase 6" overlay ──Back──▶ Main Menu
    ├──Leaderboards───────▶ (overall stats) ──Back──▶ Main Menu
    └──Settings (gear)────▶ modal overlay (music/sfx/volume/vibration/language/handedness,
                             reset progress w/ confirm) ──X──▶ wherever it was opened from
```

Pause and Settings are **overlays** (layer on top of whatever's showing, dim it, don't replace it)
— everything else in this diagram is a **screen swap** through `SceneTransitionManager.ShowOnly`.
New Character Unlock is a special case: not reachable by navigation, it's triggered automatically
by `LevelCompleteController` partway through its own celebration sequence, whenever
`UnlockManager.LastUnlockedBatch` isn't empty.
