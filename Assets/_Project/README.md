# Farm Fury: Arcade — Project Architecture

Phase 1 foundation. Single-scene, data-driven maze arcade game (Unity 6000.5.0f1, Universal
Render Pipeline, 2D Renderer). See the GDD (`FarmFury_Arcade_GDD_v1.docx`) for design context.

## Unity version note

The GDD specifies Unity 2022 LTS. Only Unity 6000.5.0f1 (Unity 6) was installed on this
machine, so the project was built against Unity 6 instead (user-approved). Package versions
(URP 17.5.0, com.unity.ugui 2.0.0, com.unity.2d.sprite/tilemap 1.0.0) were taken from Unity's
own bundled 2D template for this exact editor build, so they're guaranteed compatible.

## Single-scene architecture

There is exactly one scene: `Assets/_Project/Scenes/Game.unity`. Levels are never separate
scenes — `GameManager.LoadLevel(int)` looks up a `LevelData` asset and hands it to
`SceneController`, which destroys the previous level's instantiated content and builds the new
level's tiles/items/spawns as children of four persistent parent transforms
(`MazeParent`, `CharacterParent`, `RobotParent`, `ItemParent`). This avoids scene-load hitches
and keeps all managers alive across level transitions.

## Data-driven levels

`LevelData`, `CharacterData`, and `RobotData` are ScriptableObjects
(`Assets/_Project/ScriptableObjects/Resources/{Levels,Characters,Robots}`). `DataManager` loads
all of them at startup via `Resources.LoadAll<T>` and exposes lookup-by-ID methods. This mirrors
the lesson from the main Farm Fury game: level content should be authored as data assets, not
hardcoded in level-specific scripts.

**Why a `Resources` subfolder:** Unity can only load arbitrary project assets by name at
runtime (in a build, not just the Editor) if they live under a folder literally named
`Resources`. The GDD's folder list didn't call this out, so the ScriptableObject folders were
nested one level deeper than specified (`ScriptableObjects/Resources/Levels` instead of
`ScriptableObjects/Levels`) to make `DataManager` actually work outside the Editor. If the
project later needs to keep the Resources folder lean (build size), swap `Resources.LoadAll`
for Addressables inside `DataManager` — the public API (`GetLevelData`, `GetCharacterData`,
`GetRobotData`) doesn't need to change.

**Why `mazeLayoutFlat` instead of a raw `int[,]`:** Unity's serializer does not support
multi-dimensional arrays — a field declared `int[,]` silently fails to persist to disk. `LevelData`
stores the grid as a flat `int[]` (`mazeWidth * mazeHeight`, row-major) and exposes it as the
`int[,]` the GDD describes through the `MazeLayout` get-only property (and `SetMazeLayout` for
writing). Everything downstream (`SceneController`) reads `level.MazeLayout` and never touches
the flat array directly.

## Managers (`Assets/_Project/Scripts/Core`)

All four managers live on one `GameManagers` GameObject in `Game.unity`, so they persist for
the life of the app and can reference each other via `Instance` without null-checks for
scene-load ordering.

- **GameManager** — current `GameState`, current level/character, score. Delegates actual
  content spawning to `SceneController` (same GameObject, fetched via `GetComponent` in `Awake`).
- **DataManager** — loads and indexes all ScriptableObject data (see above).
- **SaveManager** — PlayerPrefs-backed persistence: highest level reached, coin balance,
  per-level star ratings, character unlocks. Cluck and Bessie auto-unlock on first run since
  they're starter characters per the GDD.
- **SceneController** — not a singleton (only `GameManager` needs to reach it, via
  `GetComponent`). Builds/tears down placeholder maze content for the currently loaded
  `LevelData`.

`Singleton<T>` (`Assets/_Project/Scripts/Utilities/Singleton.cs`) is a small generic base class
used by the three managers that need one; `SceneController` doesn't inherit it since nothing
else needs to reach it by static reference.

## Phase 1 scope — what's deliberately NOT here

- No player/robot movement, input handling, or AI — `SceneController` places static coloured
  square markers (yellow = player start, red = robot, green = crop, cyan = power pellet, grey =
  wall) purely to prove the data pipeline renders something. Real prefabs/sprites/animation
  and gameplay come in Phase 2+.
- No UI beyond a debug `OnGUI` overlay in `Phase1Test.cs` (two buttons: load level 0, run
  verification). Phase 5 replaces this with the real Canvas-based HUD/menus described in the GDD.
- No robot AI — `spawnDelay` on `RobotSpawnData` is stored but intentionally unused until
  Phase 3 implements the six AI behaviours.

## Editor tooling (`Assets/_Project/Scripts/Editor/Phase1ProjectBuilder.cs`)

Editor-only script (auto-excluded from player builds by living under a folder named `Editor`)
that programmatically builds `Game.unity` and `LevelData_01.asset` rather than hand-authoring
scene YAML. Menu items under **Farm Fury Arcade > Phase 1**:

- **Build Game Scene + Level 01** — (re)creates the scene hierarchy and the placeholder level
  asset. Safe to re-run; it overwrites both.
- **Run Play Mode Verification** — opens `Game.unity`, enters Play mode for ~20 frames (enough
  for `Awake`/`Start` to run and `Phase1Test` to log PASS/FAIL for each check), then exits Play
  mode and the Editor. Intended for `-executeMethod` batch-mode runs (do **not** pass `-quit` —
  the method calls `EditorApplication.Exit` itself once verification finishes).

## Placeholder LevelData_01

28×31 grid, border walls, a simple square ring in the middle, player start at (14, 5), robot
factory at (14, 25), one `Harvester` robot spawn (2s delay, unused until Phase 3), two Sunflower
power pellets in opposite corners, 100 Corn crop placements on a grid pattern, base speeds
character 4.0 / robot 3.5 — matches the Phase 1 spec exactly. `levelNumber = 0` so
`GameManager.LoadLevel(0)` resolves it.

## Folder structure

```
Assets/_Project/
  Scripts/
    Core/          GameManager, DataManager, SaveManager, SceneController, GameState, Phase1Test
    Data/           LevelData, CharacterData, RobotData + supporting structs/enums
    Gameplay/       (empty — Phase 2)
    Enemies/        (empty — Phase 3)
    UI/             (empty — Phase 5)
    Utilities/      Singleton<T>, PlaceholderSprite
    Editor/         Phase1ProjectBuilder
  ScriptableObjects/Resources/{Levels,Characters,Robots}
  Prefabs/{Characters,Robots,Blocks,UI}   (empty — later phases)
  Sprites/{Characters,Robots,Environment,UI}  (empty — later phases)
  Audio/{Music,SFX}   (empty — later phases)
  Scenes/Game.unity
```
