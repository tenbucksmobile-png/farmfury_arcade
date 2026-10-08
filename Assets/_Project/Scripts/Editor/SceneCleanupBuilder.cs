using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;
using FarmFuryArcade.UI;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.EditorTools
{
    /// <summary>One-off scene-hygiene fixes that aren't "wire uploaded art" (ArtWiringBuilder) or
    /// "rebuild a whole phase's content" (PhaseNProjectBuilder). Each entry point here is a small,
    /// targeted, idempotent edit to the existing Game.unity scene.</summary>
    public static class SceneCleanupBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";

        /// <summary>Fixes a real, confirmed bug found via a 2026-08-27 playtest report ("game
        /// didn't end when all pellets consumed") on an Orchard level: TileMapRenderer caps every
        /// maze to at most ONE real spawned PowerPelletPickup (_powerPelletsSpawned) — any tile-id-4
        /// cell after the first renders as plain ground with no pickup at all. Phase2ProjectBuilder.
        /// BuildLevel's totalCropsRequired used to count every id-4 cell in a level's grid
        /// uncapped, so any maze authored/generated with more than one power-pellet spawn point
        /// (confirmed present on LevelData_51 "The Orchard - 01" itself, and likely scattered across
        /// every world — the generators never guaranteed exactly one) required collecting a pickup
        /// that was never actually spawned. GameManager._cropsRemaining could then never reach zero,
        /// so NotifyCropCollected's EndLevel(true) call never fired no matter how much of the board
        /// was actually cleared.
        ///
        /// BuildLevel itself is fixed to cap pellets at 1 going forward (see its own comment), but
        /// re-running Phase2ProjectBuilder.BuildAll to apply that fix to the 175 already-baked
        /// LevelData assets would also wipe robotSpawns/etc. back to empty on every one of them (a
        /// separately documented gotcha — BuildAll rebuilds every LevelData from scratch). This
        /// method instead walks every LevelData asset directly and recomputes ONLY
        /// totalCropsRequired from its own already-baked grid (kernels + vegetables +
        /// Mathf.Min(pellets, 1)), leaving every other field — robotSpawns, warpTunnelRows, art,
        /// mazeLayoutFlat itself — untouched. Safe to re-run; a level whose count was already
        /// correct is simply left alone (SetDirty/logging only fire for assets that actually
        /// changed).</summary>
        [MenuItem("Farm Fury Arcade/Debug/Fix Level Crop Counts (Power Pellet Cap)")]
        public static void FixLevelCropCounts()
        {
            const int tileCropKernel = 2;
            const int tileCropVegetable = 3;
            const int tilePowerPellet = 4;

            string[] guids = AssetDatabase.FindAssets("t:LevelData", new[] { "Assets/_Project/ScriptableObjects" });
            int fixedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level == null)
                {
                    continue;
                }

                var layout = level.MazeLayout;
                int kernels = 0, vegetables = 0, pellets = 0;
                for (int x = 0; x < level.mazeWidth; x++)
                {
                    for (int y = 0; y < level.mazeHeight; y++)
                    {
                        switch (layout[x, y])
                        {
                            case tileCropKernel: kernels++; break;
                            case tileCropVegetable: vegetables++; break;
                            case tilePowerPellet: pellets++; break;
                        }
                    }
                }

                int correctTotal = kernels + vegetables + Mathf.Min(pellets, 1);
                if (level.totalCropsRequired != correctTotal)
                {
                    Debug.Log($"[SceneCleanupBuilder] {level.name}: totalCropsRequired {level.totalCropsRequired} -> {correctTotal} " +
                              $"(grid has {pellets} power-pellet spawn point(s), only 1 ever actually spawns).");
                    level.totalCropsRequired = correctTotal;
                    EditorUtility.SetDirty(level);
                    fixedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SceneCleanupBuilder] Fixed totalCropsRequired on {fixedCount} of {guids.Length} LevelData assets.");
        }

        /// <summary>Web demo: the Phase1-5Test/LevelSelectTest scripts were deleted, which leaves
        /// their GameObjects in Game.unity with a missing script. Removes them by name.</summary>
        [MenuItem("Farm Fury Arcade/Remove Deleted Test Objects")]
        public static void RemoveDeletedTestObjects()
        {
            EditorSceneManager.OpenScene(ScenePath);
            int removed = 0;
            foreach (var name in new[] { "Phase1Test", "Phase2Test", "Phase3Test", "Phase4Test", "Phase5Test", "LevelSelectTest" })
            {
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>()
                             .Where(g => g.name == name && g.scene.IsValid() && !EditorUtility.IsPersistent(g)).ToList())
                {
                    Object.DestroyImmediate(go);
                    removed++;
                }
            }
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[SceneCleanupBuilder] Removed {removed} deleted test GameObject(s).");
        }

        /// <summary>The Main Camera was static (no follow script) at orthographic size 16 — sized
        /// to keep the whole original 28x31 maze on screen at once, classic-arcade style — then
        /// zoomed in to size 5 with CameraFollow tracking the active character. That close a
        /// follow-cam read as "zoomed in too much" per feedback (only ~10 of the maze's 31 rows
        /// visible at once, board not readable as a whole), so it went back to fitting the whole
        /// board. But at 28x31 tiles, "the whole board" was itself too small on screen — the fix
        /// for that was shrinking the maze to 14x16 (Phase2ProjectBuilder.BuildLevelData01) rather
        /// than zooming in again (zooming in would crop the board instead of enlarging it relative
        /// to the screen). Orthographic size 8 is sized the same way 16 was for the original board:
        /// `2 * size >= mazeHeight - 1` (16 - 1 = 15, needs size >= 7.5). CameraFollow stays
        /// attached rather than being removed — with the board fully in view,
        /// ClampToMazeBounds' "camera FOV bigger than maze" branch (see that method) pins it
        /// dead-center every frame, which is equivalent to a static camera but needs no separate
        /// code path, and keeps the option open to zoom in again later without re-adding the
        /// component. Safe to re-run — just resets orthographicSize and ensures exactly one
        /// CameraFollow component.</summary>
        [MenuItem("Farm Fury Arcade/Fit Gameplay Camera To Maze")]
        public static void FitGameplayCameraToMaze()
        {
            EditorSceneManager.OpenScene(ScenePath);

            var cameraGO = GameObject.Find("Main Camera");
            var camera = cameraGO != null ? cameraGO.GetComponent<Camera>() : null;
            if (camera == null)
            {
                Debug.LogWarning("[SceneCleanupBuilder] Could not find Main Camera in the scene.");
                return;
            }

            camera.orthographicSize = 8f;

            if (cameraGO.GetComponent<CameraFollow>() == null)
            {
                cameraGO.AddComponent<CameraFollow>();
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[SceneCleanupBuilder] Gameplay camera fit to the whole maze (orthographic size 8).");
        }

        // Matches SaveManager's own private LevelStarsKeyPrefix exactly — duplicated here rather
        // than exposed publicly on SaveManager, since this is a PlayerPrefs write, not something
        // that needs a live SaveManager instance (works in Edit mode, before ever entering Play).
        private const string LevelStarsKeyPrefix = "FFA_LevelStars_";

        /// <summary>Testing shortcut — grants 3 stars on every level slot (0 through
        /// UnlockProgression.TotalLevels-1) via PlayerPrefs directly, so every real level (both
        /// World 1's 25 and World 2's 25, per Phase2ProjectBuilder) shows unlocked in Level Select,
        /// including the World 2 badge itself (its gate is Level 25 at 2+ stars — see
        /// UnlockProgression.IsWorldUnlocked). Slots beyond the real 50 LevelData assets are
        /// harmlessly starred too; UnlockProgression.IsLevelUnlocked still requires
        /// DataManager.GetLevelData(index) != null regardless of stars, so an unauthored slot stays
        /// locked either way. Doesn't touch SaveManager.ResetAllProgress or any other save state —
        /// only ever raises stars (SetLevelStars' own semantics), never lowers them, so re-running
        /// this is always safe.</summary>
        [MenuItem("Farm Fury Arcade/Debug/Unlock All Levels (Testing)")]
        public static void UnlockAllLevelsForTesting()
        {
            for (int i = 0; i < UnlockProgression.TotalLevels; i++)
            {
                PlayerPrefs.SetInt(LevelStarsKeyPrefix + i, 3);
            }
            PlayerPrefs.Save();
            Debug.Log($"[SceneCleanupBuilder] Set 3 stars on levels 0-{UnlockProgression.TotalLevels - 1} " +
                      "so every real level (World 1 + World 2) is unlocked and tappable in Level Select. " +
                      "Press Play and open Level Select — World 2's badge is now selectable.");
        }

        /// <summary>Opposite of UnlockAllLevelsForTesting — wipes every level/world/character-unlock
        /// PlayerPrefs key via SaveManager.ResetAllProgressKeys (the static, no-instance-required
        /// half of SaveManager.ResetAllProgress — see its own doc comment) so play can restart from
        /// Level 1 with only the starter characters (Cluck/Bessie, re-applied automatically the next
        /// time SaveManager.Awake runs — see SaveManager.LoadProgress). Does not touch settings
        /// (music/sfx/language/etc.) — those aren't "progress".</summary>
        [MenuItem("Farm Fury Arcade/Debug/Reset All Progress (Testing)")]
        public static void ResetAllProgressForTesting()
        {
            SaveManager.ResetAllProgressKeys();
            Debug.Log("[SceneCleanupBuilder] Cleared all level/world/character-unlock progress. " +
                      "Press Play — only Cluck and Bessie will be unlocked, and Level Select will start at Level 1.");
        }

        /// <summary>Grants 5000 test coins (SaveManager.DebugAddCoinsForTesting) — enough to trigger
        /// the "Use Coins?" popup on any of the 11 cosmetics (all currently priced at 2500) without
        /// grinding levels or touching real IAP at all. Safe in both Edit mode (writes straight to
        /// the checksum-protected PlayerPrefs value, applied the next time Play mode loads) and
        /// Play mode (adds to the live balance immediately, so the HUD's coin display updates
        /// on the spot). Re-run any time more test coins are needed — it always adds, never
        /// resets, so running it repeatedly just stacks up.</summary>
        [MenuItem("Farm Fury Arcade/Debug/Add 5000 Test Coins")]
        public static void AddTestCoins()
        {
            SaveManager.DebugAddCoinsForTesting(5000);
            Debug.Log("[SceneCleanupBuilder] Added 5000 test coins. If already in Play mode, the " +
                      "balance updates immediately; otherwise it takes effect next time Play starts.");
        }

        private static int _sfxDiagFrame;

        /// <summary>Minimal, self-contained Play Mode check for "SFX doesn't play" reports — opens
        /// the scene, enters Play mode, waits a few frames for AudioManager/SaveManager to Awake,
        /// fires PlayCornPickupSfx() directly, then checks AudioManager.IsAnySfxPlaying(). Exits
        /// after ~8 frames rather than running any Phase*Test battery — RunPlayModeVerification
        /// (which runs the full Phase1-5Test chain over up to 30 real seconds) repeatedly hung/
        /// crashed in this environment before ever reaching a result; this avoids that path
        /// entirely so the SFX question actually gets answered.</summary>
        [MenuItem("Farm Fury Arcade/Debug/Run SFX Playback Diagnostic")]
        public static void RunSfxPlaybackDiagnostic()
        {
            EditorSceneManager.OpenScene(ScenePath);
            _sfxDiagFrame = 0;
            EditorApplication.update += OnSfxDiagUpdate;
            EditorApplication.isPlaying = true;
        }

        private static void OnSfxDiagUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            _sfxDiagFrame++;

            if (_sfxDiagFrame == 5)
            {
                var audioManager = AudioManager.Instance;
                if (audioManager == null)
                {
                    Debug.LogError("[SfxDiagnostic] FAIL: AudioManager.Instance is null 5 frames into Play mode.");
                }
                else
                {
                    Debug.Log("[SfxDiagnostic] AudioManager found — calling PlayCornPickupSfx().");
                    audioManager.PlayCornPickupSfx();
                }
            }
            else if (_sfxDiagFrame == 8)
            {
                var audioManager = AudioManager.Instance;
                bool playing = audioManager != null && audioManager.IsAnySfxPlaying();
                Debug.Log(playing
                    ? "[SfxDiagnostic] PASS: PlayCornPickupSfx started audio on a pooled AudioSource."
                    : "[SfxDiagnostic] FAIL: PlayCornPickupSfx did NOT start any pooled AudioSource playing.");

                EditorApplication.update -= OnSfxDiagUpdate;
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += () => EditorApplication.Exit(0);
            }
        }

        /// <summary>Diagnoses "Level Select won't scroll down to new levels" reports without
        /// entering Play mode at all (RunPlayModeVerification/RunSfxPlaybackDiagnostic both hang
        /// reliably at Play-mode entry in this environment, before any game code runs — an
        /// environment-level issue this sidesteps entirely). Calls LevelSelectController's private
        /// PopulateLevelGrid(0) directly via reflection on the Editor-mode scene instance, forces a
        /// layout rebuild, then logs the resulting Content vs Viewport heights — if Content isn't
        /// meaningfully taller than Viewport, that's the bug (ScrollRect has nothing real to scroll,
        /// so every drag springs back immediately, reading as "shoots back, can't reach lower
        /// levels"). DataManager/SaveManager singletons aren't available in Edit mode, so this
        /// can't call ScrollToCurrentLevel (which needs them) — measuring the grid's own size is
        /// enough to answer the actual question.</summary>
        [MenuItem("Farm Fury Arcade/Debug/Diagnose Level Select Scroll Range")]
        public static void DiagnoseLevelSelectScrollRange()
        {
            EditorSceneManager.OpenScene(ScenePath);

            var screenGO = GameObject.Find("Canvas")?.transform.Find("LevelSelectScreen")?.gameObject;
            var controller = screenGO != null ? screenGO.GetComponent<LevelSelectController>() : null;
            if (controller == null)
            {
                Debug.LogError("[LevelSelectDiag] Could not find LevelSelectScreen/LevelSelectController.");
                return;
            }

            var type = typeof(LevelSelectController);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;

            var populateMethod = type.GetMethod("PopulateLevelGrid", flags);
            var contentField = type.GetField("contentParent", flags);
            var scrollRectField = type.GetField("scrollRect", flags);

            if (populateMethod == null || contentField == null || scrollRectField == null)
            {
                Debug.LogError("[LevelSelectDiag] Reflection lookup failed — field/method names may have changed.");
                return;
            }

            // LevelSelectScreen (and everything under it) starts inactive in a freshly-opened,
            // never-Played scene — SceneTransitionManager.ShowOnly leaves only MainMenuScreen
            // active at rest. Unity's layout system skips inactive hierarchies entirely
            // (ILayoutElement lookups check isActiveAndEnabled, which reflects activeInHierarchy,
            // not just activeSelf), so every layout computation below would silently return 0
            // without this — not a runtime bug, an artifact of measuring an inactive screen.
            screenGO.SetActive(true);

            populateMethod.Invoke(controller, new object[] { 0 });

            var contentParent = (RectTransform)contentField.GetValue(controller);
            var scrollRect = (ScrollRect)scrollRectField.GetValue(controller);
            scrollRect.gameObject.SetActive(true);

            var section = contentParent.childCount > 0 ? contentParent.GetChild(0) as RectTransform : null;
            var sectionLayoutElement = section != null ? section.GetComponent<LayoutElement>() : null;
            var sectionGrid = section != null ? section.GetComponent<GridLayoutGroup>() : null;

            Debug.Log($"[LevelSelectDiag] contentParent childCount after populate: {contentParent.childCount}, " +
                      $"section childCount (tiles): {(section != null ? section.childCount : -1)}, " +
                      $"section.LayoutElement.preferredHeight: {(sectionLayoutElement != null ? sectionLayoutElement.preferredHeight : -999f)}, " +
                      $"section GridLayoutGroup found: {sectionGrid != null}.");

            // Rebuild bottom-up explicitly (section first, then its parent) rather than relying on
            // a single ForceRebuildLayoutImmediate(contentParent) call to recurse correctly, to
            // isolate whether propagation itself is the problem.
            if (section != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(section);
                Debug.Log($"[LevelSelectDiag] After rebuilding section alone: section.rect.height = {section.rect.height:F1}.");
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent);

            float contentHeight = contentParent.rect.height;
            float viewportHeight = scrollRect.viewport != null ? scrollRect.viewport.rect.height : -1f;
            float scrollRange = contentHeight - viewportHeight;

            Debug.Log($"[LevelSelectDiag] World 1 grid populated. Content height: {contentHeight:F1}, " +
                      $"Viewport height: {viewportHeight:F1}, scrollable range: {scrollRange:F1}px.");
            Debug.Log(scrollRange > 100f
                ? "[LevelSelectDiag] PASS: plenty of real scroll range — content is genuinely taller than the viewport."
                : "[LevelSelectDiag] FAIL: little or no scroll range — this is why drags spring back immediately.");
        }

        /// <summary>Logs the actual serialized state of every screen built via
        /// Phase5ProjectBuilder.ApplyDimmedLandingBackground (Settings/Shop/CosmeticsHub/Hat+Trail
        /// purchase/Leaderboards) — root Image sprite+color+sibling index, and the PosterBackdrop
        /// child's own sprite+color+sibling index+active state. Exists because a screenshot alone
        /// couldn't settle whether the 50%-opacity dim was actually reaching the built scene or
        /// failing at some other point (stale build, wrong sibling order, disabled GameObject,
        /// sprite import failure) — this reads the real component values directly out of the
        /// Editor-mode scene, no Play mode and no visual interpretation required.</summary>
        [MenuItem("Farm Fury Arcade/Debug/Diagnose Dimmed Backdrops")]
        public static void DiagnoseDimmedBackdrops()
        {
            EditorSceneManager.OpenScene(ScenePath);

            var canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError("[BackdropDiag] Could not find Canvas in the scene.");
                return;
            }

            string[] screenNames =
            {
                "SettingsOverlay", "StoreComingSoonOverlay", "CosmeticsHubScreen",
                "LeaderboardsScreen"
            };

            foreach (var name in screenNames)
            {
                var screenTransform = canvas.transform.Find(name);
                if (screenTransform == null)
                {
                    Debug.LogWarning($"[BackdropDiag] {name}: NOT FOUND under Canvas.");
                    continue;
                }

                var rootImage = screenTransform.GetComponent<Image>();
                string rootDesc = rootImage != null
                    ? $"sprite={(rootImage.sprite != null ? rootImage.sprite.name : "null")} color={rootImage.color} siblingIndex={screenTransform.GetSiblingIndex()}"
                    : "NO Image component on root";

                var poster = screenTransform.Find("PosterBackdrop");
                string posterDesc;
                if (poster == null)
                {
                    posterDesc = "MISSING — ApplyDimmedLandingBackground never ran, or this scene predates that fix";
                }
                else
                {
                    var posterImage = poster.GetComponent<Image>();
                    posterDesc = posterImage != null
                        ? $"active={poster.gameObject.activeSelf} sprite={(posterImage.sprite != null ? posterImage.sprite.name : "null")} color={posterImage.color} siblingIndex={poster.GetSiblingIndex()} (root has {screenTransform.childCount} children)"
                        : "child exists but has NO Image component";
                }

                Debug.Log($"[BackdropDiag] {name} -> root: {rootDesc} | PosterBackdrop: {posterDesc}");
            }
        }

        /// <summary>Diagnoses "music never plays / doesn't come back when toggled" reports —
        /// Edit-mode read of the CURRENT saved scene's AudioManager component: whether
        /// musicSourceA/musicSourceB/sfxPool exist (WireAudioSources — Phase5ProjectBuilder), and
        /// whether landingMusicClip/backgroundMusicClip/worldMusicClips actually have real
        /// AudioClip references (WireAudio — ArtWiringBuilder). Either being empty means PlayMusic
        /// silently no-ops (`if (clip == null) return` / `if (incoming == null) return`) with no
        /// console error at all — this catches that without needing a live Play session.</summary>
        [MenuItem("Farm Fury Arcade/Debug/Diagnose Audio Wiring")]
        public static void DiagnoseAudioWiring()
        {
            EditorSceneManager.OpenScene(ScenePath);

            var audioManager = Object.FindFirstObjectByType<FarmFuryArcade.Core.AudioManager>(FindObjectsInactive.Include);
            if (audioManager == null)
            {
                Debug.LogError("[AudioDiag] No AudioManager component found anywhere in the scene.");
                return;
            }

            var so = new SerializedObject(audioManager);
            var musicA = so.FindProperty("musicSourceA").objectReferenceValue;
            var musicB = so.FindProperty("musicSourceB").objectReferenceValue;
            var sfxPoolProp = so.FindProperty("sfxPool");
            int sfxPoolCount = sfxPoolProp != null ? sfxPoolProp.arraySize : 0;
            var landingClip = so.FindProperty("landingMusicClip").objectReferenceValue;
            var backgroundClip = so.FindProperty("backgroundMusicClip").objectReferenceValue;

            Debug.Log($"[AudioDiag] musicSourceA={(musicA != null ? "OK" : "NULL")} musicSourceB={(musicB != null ? "OK" : "NULL")} sfxPool.Length={sfxPoolCount}");
            Debug.Log($"[AudioDiag] landingMusicClip={(landingClip != null ? landingClip.name : "NULL")} backgroundMusicClip={(backgroundClip != null ? backgroundClip.name : "NULL")}");

            var worldClipsProp = so.FindProperty("worldMusicClips");
            if (worldClipsProp != null && worldClipsProp.isArray)
            {
                for (int i = 0; i < worldClipsProp.arraySize; i++)
                {
                    var element = worldClipsProp.GetArrayElementAtIndex(i);
                    var clipProp = element.FindPropertyRelative("clip");
                    var mazeTypeProp = element.FindPropertyRelative("mazeType");
                    string clipName = clipProp != null && clipProp.objectReferenceValue != null ? clipProp.objectReferenceValue.name : "NULL";
                    string mazeTypeName = mazeTypeProp != null ? mazeTypeProp.enumDisplayNames[mazeTypeProp.enumValueIndex] : "?";
                    Debug.Log($"[AudioDiag] worldMusicClips[{i}] mazeType={mazeTypeName} clip={clipName}");
                }
            }
            else
            {
                Debug.LogWarning("[AudioDiag] worldMusicClips property not found or not an array — check the field name/type in AudioManager.cs.");
            }

            var audioListener = Object.FindFirstObjectByType<AudioListener>(FindObjectsInactive.Include);
            Debug.Log(audioListener == null
                ? "[AudioDiag] No AudioListener found in the scene at all — NO audio of any kind can play."
                : $"[AudioDiag] AudioListener found on '{audioListener.gameObject.name}', enabled={audioListener.enabled}, GameObject.activeInHierarchy={audioListener.gameObject.activeInHierarchy}.");
        }

        /// <summary>Diagnoses "on-screen D-pad taps don't move the character (keyboard does)"
        /// reports without needing a live Play session — reads the CURRENT saved scene's
        /// GameplayScreen/SafeArea/DPad*Button hierarchy directly (Edit mode, same
        /// reflection-free-log convention as DiagnoseDimmedBackdrops) and reports everything that
        /// could silently break a tap: whether each button GameObject/Button/EventTrigger exists,
        /// Button.interactable, Image.raycastTarget, how many EventTrigger entries are wired (should
        /// be 3: PointerDown/PointerUp/PointerExit), and whether DirectionalPadController's own
        /// serialized button references on GameplayScreen actually point at these exact
        /// GameObjects (a stale/null reference here would silently no-op every tap while the
        /// buttons themselves still render and look correct).</summary>
        [MenuItem("Farm Fury Arcade/Debug/Diagnose DPad Wiring")]
        public static void DiagnoseDPadWiring()
        {
            EditorSceneManager.OpenScene(ScenePath);

            var canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError("[DPadDiag] Could not find Canvas in the scene.");
                return;
            }

            var gameplayScreen = canvas.transform.Find("GameplayScreen");
            if (gameplayScreen == null)
            {
                Debug.LogError("[DPadDiag] Could not find Canvas/GameplayScreen.");
                return;
            }

            var controller = gameplayScreen.GetComponent<DirectionalPadController>();
            if (controller == null)
            {
                Debug.LogError("[DPadDiag] GameplayScreen has NO DirectionalPadController component at all — every tap is a guaranteed no-op.");
            }

            var so = controller != null ? new SerializedObject(controller) : null;

            string[] buttonNames = { "DPadUpButton", "DPadDownButton", "DPadLeftButton", "DPadRightButton" };
            string[] fieldNames = { "upButton", "downButton", "leftButton", "rightButton" };

            for (int i = 0; i < buttonNames.Length; i++)
            {
                var buttonTransform = gameplayScreen.Find($"SafeArea/{buttonNames[i]}");
                if (buttonTransform == null)
                {
                    Debug.LogError($"[DPadDiag] {buttonNames[i]}: NOT FOUND under GameplayScreen/SafeArea.");
                    continue;
                }

                var button = buttonTransform.GetComponent<Button>();
                var image = buttonTransform.GetComponent<Image>();
                var trigger = buttonTransform.GetComponent<EventTrigger>();
                var rect = buttonTransform.GetComponent<RectTransform>();

                string buttonDesc = button != null
                    ? $"interactable={button.interactable}"
                    : "MISSING Button component";
                string imageDesc = image != null
                    ? $"raycastTarget={image.raycastTarget}"
                    : "MISSING Image component";
                string triggerDesc = trigger != null
                    ? $"entries={trigger.triggers.Count} (expect 3)"
                    : "no EventTrigger yet (EXPECTED in the saved Edit-mode scene — DirectionalPadController.Awake() adds it at runtime, not construction time)";
                string rectDesc = rect != null
                    ? $"size={rect.sizeDelta} activeInHierarchy={buttonTransform.gameObject.activeInHierarchy}"
                    : "MISSING RectTransform";

                string refDesc = "n/a (no controller)";
                if (so != null)
                {
                    var fieldProp = so.FindProperty(fieldNames[i]);
                    var referenced = fieldProp?.objectReferenceValue as Button;
                    refDesc = referenced == null
                        ? "DirectionalPadController field is NULL — guaranteed no-op tap"
                        : (referenced.gameObject == buttonTransform.gameObject
                            ? "OK, points at this exact button"
                            : $"MISMATCH — points at a different GameObject ({referenced.gameObject.name}, path={GetPath(referenced.transform)})");
                }

                Debug.Log($"[DPadDiag] {buttonNames[i]}: {buttonDesc} | {imageDesc} | {triggerDesc} | {rectDesc} | controllerField[{fieldNames[i]}]={refDesc}");
            }

            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            var uiModule = eventSystem != null ? eventSystem.GetComponent<BaseInputModule>() : null;
            Debug.Log(eventSystem == null
                ? "[DPadDiag] No EventSystem found in the scene at all — NO UI element anywhere can receive clicks/taps."
                : $"[DPadDiag] EventSystem found, input module = {(uiModule != null ? uiModule.GetType().Name : "MISSING — no BaseInputModule component, UI clicks will not be processed")}.");

            if (uiModule is UnityEngine.InputSystem.UI.InputSystemUIInputModule inputSystemUiModule)
            {
                var pointRef = inputSystemUiModule.point;
                var clickRef = inputSystemUiModule.leftClick;
                string pointDesc = DescribeActionRef(pointRef);
                string clickDesc = DescribeActionRef(clickRef);
                Debug.Log($"[DPadDiag] InputSystemUIInputModule.point = {pointDesc} | .leftClick = {clickDesc} — if either is 'null/unbound', NO on-screen UI (not just the D-pad) can register pointer input at all.");
            }
        }

        private static string DescribeActionRef(UnityEngine.InputSystem.InputActionReference actionRef)
        {
            if (actionRef == null)
            {
                return "NULL reference";
            }
            var action = actionRef.action;
            if (action == null)
            {
                return $"reference '{actionRef.name}' resolves to a NULL action";
            }
            return $"'{action.name}' bindings={action.bindings.Count} enabled={action.enabled}";
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        private static void DedupeAndDisable<T>() where T : MonoBehaviour
        {
            var instances = Resources.FindObjectsOfTypeAll<T>()
                .Where(t => !EditorUtility.IsPersistent(t.gameObject) && t.gameObject.scene.IsValid())
                .ToList();

            if (instances.Count == 0)
            {
                Debug.LogWarning($"[SceneCleanupBuilder] Could not find any {typeof(T).Name} in the scene.");
                return;
            }

            // Keep the first, destroy any extras (duplicates from the Find()-only-finds-active bug).
            for (int i = 1; i < instances.Count; i++)
            {
                Object.DestroyImmediate(instances[i].gameObject);
            }

            instances[0].gameObject.SetActive(false);
        }
    }
}
