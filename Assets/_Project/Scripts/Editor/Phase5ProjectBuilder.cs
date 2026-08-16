using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;
using FarmFuryArcade.Gameplay;
using FarmFuryArcade.UI;
using FarmFuryArcade.Utilities;
using static FarmFuryArcade.EditorTools.UIBuilderHelpers;
using Object = UnityEngine.Object;

namespace FarmFuryArcade.EditorTools
{
    /// <summary>
    /// Phase 5 scaffolding: bootstraps TextMeshPro (no com.unity.textmeshpro package reference
    /// exists — TMP ships bundled inside com.unity.ugui 2.5.0 in this Unity version, but its
    /// essential font/settings were never imported), builds every UI screen as real uGUI under
    /// the existing Canvas (programmatically — no visual Editor access in this session, same
    /// constraint every earlier phase worked under for prefabs), wires SceneTransitionManager/
    /// AudioManager/DailyChallengeManager/LeaderboardManager, and disables Phase4Test's runOnStart.
    /// Safe to re-run — rebuilds the whole UI hierarchy from scratch each time rather than trying
    /// to patch an existing one, since diffing hand-built vs previous-run hierarchies isn't
    /// practical.
    /// </summary>
    public static class Phase5ProjectBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string UIPrefabFolder = "Assets/_Project/Prefabs/UI";

        [MenuItem("Farm Fury Arcade/Phase 5/Build All")]
        public static void BuildAll()
        {
            EnsureTMPEssentials();

            EditorSceneManager.OpenScene(ScenePath);

            var canvas = GameObject.Find("Canvas");
            var managersGO = GameObject.Find("GameManagers");
            if (canvas == null || managersGO == null)
            {
                Debug.LogError("[Phase5ProjectBuilder] Canvas or GameManagers not found — run Phase 1-4 builders first.");
                return;
            }

            ConfigureCanvasScaler(canvas);
            RemoveExistingUIScreens(canvas.transform);
            RemoveObsoleteCharacterSwapUI();

            AddManagers(managersGO);

            GameObject rosterCardPrefab = BuildRosterCardPrefab();

            var fadeGroup = BuildFadeOverlay(canvas.transform);

            var mainMenu = BuildMainMenu(canvas.transform);
            var (gameplay, comboBanner) = BuildGameplayHUD(canvas.transform);
            var pause = BuildPauseMenu(canvas.transform);
            var settings = BuildSettingsPanel(canvas.transform);
            var storeComingSoon = BuildStoreComingSoonPanel(canvas.transform);
            var (levelComplete, unlockScreen) = BuildLevelComplete(canvas.transform);
            var levelFailed = BuildLevelFailed(canvas.transform);
            var roster = BuildCharacterRoster(canvas.transform, rosterCardPrefab);
            var leaderboards = BuildLeaderboards(canvas.transform);

            var characterSelectCardPrefab = BuildCharacterSelectCardPrefab();
            var chooseCharacter = BuildChooseCharacterScreen(canvas.transform, characterSelectCardPrefab);

            var levelTilePrefab = BuildLevelTilePrefab();
            BuildWorldDividerPrefab(); // kept but unlinked — see that method's own doc comment
            var worldShieldPrefab = BuildWorldShieldPrefab();
            var levelSelect = BuildLevelSelect(canvas.transform, levelTilePrefab, worldShieldPrefab);

            WireCrossReferences(mainMenu, gameplay, pause, settings,
                levelComplete, unlockScreen, levelFailed, roster, leaderboards, chooseCharacter, comboBanner, levelSelect);

            var transitionManager = managersGO.GetComponent<SceneTransitionManager>();
            var transitionSO = new SerializedObject(transitionManager);
            transitionSO.FindProperty("fadeGroup").objectReferenceValue = fadeGroup;
            var screenRootsProp = transitionSO.FindProperty("screenRoots");
            var screens = new[] { mainMenu, gameplay, levelComplete, levelFailed, roster, leaderboards, levelSelect };
            screenRootsProp.arraySize = screens.Length;
            for (int i = 0; i < screens.Length; i++)
            {
                screenRootsProp.GetArrayElementAtIndex(i).objectReferenceValue = screens[i];
            }
            transitionSO.ApplyModifiedPropertiesWithoutUndo();

            // Only Main Menu starts active; everything else (including overlays) starts hidden.
            foreach (var screen in screens)
            {
                screen.SetActive(screen == mainMenu);
            }
            pause.SetActive(false);
            settings.SetActive(false);
            storeComingSoon.SetActive(false);
            chooseCharacter.gameObject.SetActive(false);
            unlockScreen.gameObject.SetActive(false); // BuildLevelComplete already does this too; explicit for clarity

            // GameObject.Find only searches active objects (same gotcha Phase5Test itself works
            // around when looking up screens) — SceneCleanupBuilder.DisableDebugTestOverlays can
            // leave this GameObject inactive, and a plain Find-or-create here would silently spawn
            // a second active Phase5Test every re-run instead of recognizing the existing one.
            var existingPhase5Test = Resources.FindObjectsOfTypeAll<Phase5Test>()
                .FirstOrDefault(t => !EditorUtility.IsPersistent(t.gameObject));
            if (existingPhase5Test == null)
            {
                new GameObject("Phase5Test").AddComponent<Phase5Test>();
            }
            DisableRunOnStart("Phase4Test");

            // LevelSelectTest is the newest verification harness — same one-active-test-at-a-time
            // convention every earlier phase followed (see the doc comment on DisableRunOnStart's
            // call sites), so Phase5Test's runOnStart gets disabled here too.
            var existingLevelSelectTest = Resources.FindObjectsOfTypeAll<LevelSelectTest>()
                .FirstOrDefault(t => !EditorUtility.IsPersistent(t.gameObject));
            if (existingLevelSelectTest == null)
            {
                new GameObject("LevelSelectTest").AddComponent<LevelSelectTest>();
            }
            DisableRunOnStart("Phase5Test");

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Phase5ProjectBuilder] Phase 5 UI screens, managers, and Game.unity wiring complete.");
        }

        // ---- TMP bootstrap ------------------------------------------------------------------

        /// <summary>No com.unity.textmeshpro package reference exists in manifest.json — TMP is
        /// bundled directly inside com.unity.ugui 2.5.0 in this Unity version, but its essential
        /// font/settings (normally imported via Window > TextMeshPro > Import TMP Essential
        /// Resources) were never brought into Assets. Without them, TextMeshProUGUI has no default
        /// font asset to render with. Copies the one SDF font asset + TMP Settings this project
        /// actually has available (bundled as URP samples under render-pipelines.core's
        /// Samples~/Common/TextMesh Pro) into Assets/Resources, where TMP_Settings.Load looks for
        /// them by convention (Resources.Load&lt;TMP_Settings&gt;("TMP Settings")).</summary>
        private static void EnsureTMPEssentials()
        {
            const string destSettingsPath = "Assets/Resources/TMP Settings.asset";
            if (AssetDatabase.LoadAssetAtPath<Object>(destSettingsPath) != null)
            {
                return; // already bootstrapped
            }

            string[] settingsMatches = Directory.GetFiles("Library/PackageCache", "TMP Settings.asset", SearchOption.AllDirectories);
            if (settingsMatches.Length == 0)
            {
                Debug.LogWarning("[Phase5ProjectBuilder] Could not find a bundled 'TMP Settings.asset' under Library/PackageCache — " +
                                  "TextMeshProUGUI text may render without a font. Import TMP Essential Resources manually if so.");
                return;
            }

            string sourceResourcesDir = Path.GetDirectoryName(settingsMatches[0])!;
            Directory.CreateDirectory("Assets/Resources");
            Directory.CreateDirectory("Assets/TextMesh Pro/Resources");

            CopyFileAndMeta(Path.Combine(sourceResourcesDir, "TMP Settings.asset"), destSettingsPath);

            string sourceFontsDir = Path.Combine(sourceResourcesDir, "Fonts & Materials");
            string destFontsDir = "Assets/TextMesh Pro/Resources/Fonts & Materials";
            Directory.CreateDirectory(destFontsDir);

            if (Directory.Exists(sourceFontsDir))
            {
                foreach (var file in Directory.GetFiles(sourceFontsDir))
                {
                    if (file.EndsWith(".meta"))
                    {
                        continue;
                    }
                    CopyFileAndMeta(file, Path.Combine(destFontsDir, Path.GetFileName(file)));
                }
            }

            AssetDatabase.Refresh();

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(destSettingsPath);
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{destFontsDir}/Inter-Regular SDF.asset");
            if (settings != null && fontAsset != null)
            {
                var so = new SerializedObject(settings);
                var prop = so.FindProperty("m_defaultFontAsset");
                if (prop != null)
                {
                    prop.objectReferenceValue = fontAsset;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(settings);
            }
            else
            {
                Debug.LogWarning("[Phase5ProjectBuilder] TMP Settings or Inter-Regular SDF font asset not found after copy — " +
                                  "TextMeshProUGUI may fall back to no visible glyphs.");
            }

            AssetDatabase.SaveAssets();
        }

        private static void CopyFileAndMeta(string sourceFile, string destFile)
        {
            if (!File.Exists(sourceFile))
            {
                return;
            }
            File.Copy(sourceFile, destFile, overwrite: true);
            string sourceMeta = sourceFile + ".meta";
            if (File.Exists(sourceMeta))
            {
                File.Copy(sourceMeta, destFile + ".meta", overwrite: true);
            }
        }

        // ---- Scene-level setup ----------------------------------------------------------------

        private static void ConfigureCanvasScaler(GameObject canvas)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                return;
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void RemoveExistingUIScreens(Transform canvasTransform)
        {
            // Safe-to-re-run: destroy anything this builder previously created under Canvas.
            for (int i = canvasTransform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(canvasTransform.GetChild(i).gameObject);
            }
        }

        /// <summary>One-time migration cleanup: Phase4ProjectBuilder used to create a standalone
        /// "CharacterSwapUI" GameObject (the old OnGUI debug panel, now replaced by
        /// ChooseCharacterScreen). Older scenes built before that removal still have it lying
        /// around as a dangling GameObject with a Missing Script reference, since deleting the .cs
        /// file doesn't retroactively clean up scenes that already referenced it. Safe to call
        /// even if it's already gone.</summary>
        private static void RemoveObsoleteCharacterSwapUI()
        {
            var go = GameObject.Find("CharacterSwapUI");
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        private const int SfxPoolSize = 6;

        private static void AddManagers(GameObject managersGO)
        {
            if (managersGO.GetComponent<SceneTransitionManager>() == null) managersGO.AddComponent<SceneTransitionManager>();
            var audioManager = managersGO.GetComponent<AudioManager>();
            if (audioManager == null) audioManager = managersGO.AddComponent<AudioManager>();
            WireAudioSources(managersGO, audioManager);
            if (managersGO.GetComponent<DailyChallengeManager>() == null) managersGO.AddComponent<DailyChallengeManager>();
            if (managersGO.GetComponent<LeaderboardManager>() == null) managersGO.AddComponent<LeaderboardManager>();
            // AdManager's app-key/ad-unit-ID fields are left empty here — no real IDs exist yet
            // (see CLAUDE.md's monetisation plan). Fill them in via the Inspector once the LevelPlay
            // dashboard has real values; AdManager itself no-ops gracefully with a warning until
            // then, same "missing config just no-ops" convention AudioManager's missing-clip
            // handling already uses.
            if (managersGO.GetComponent<AdManager>() == null) managersGO.AddComponent<AdManager>();
        }

        /// <summary>AudioManager's musicSourceA/musicSourceB/sfxPool were never actually assigned
        /// anywhere — the component existed with a full playback API, but with those fields null,
        /// PlayMusic/PlaySFX silently no-op (`if (incoming == null) return;` / an empty pool), so no
        /// audio played at all despite clips being correctly wired. Find-or-create two dedicated
        /// AudioSource children for music (PlayMusic swaps between them to crossfade) and a pool of
        /// AudioSources for overlapping SFX one-shots. Safe to re-run — reuses existing children by
        /// name instead of duplicating them.</summary>
        private static void WireAudioSources(GameObject managersGO, AudioManager audioManager)
        {
            var musicA = FindOrCreateAudioSourceChild(managersGO.transform, "MusicSourceA");
            var musicB = FindOrCreateAudioSourceChild(managersGO.transform, "MusicSourceB");

            var sfxSources = new AudioSource[SfxPoolSize];
            for (int i = 0; i < SfxPoolSize; i++)
            {
                sfxSources[i] = FindOrCreateAudioSourceChild(managersGO.transform, $"SfxSource{i}");
            }

            var so = new SerializedObject(audioManager);
            so.FindProperty("musicSourceA").objectReferenceValue = musicA;
            so.FindProperty("musicSourceB").objectReferenceValue = musicB;
            var poolProp = so.FindProperty("sfxPool");
            poolProp.arraySize = sfxSources.Length;
            for (int i = 0; i < sfxSources.Length; i++)
            {
                poolProp.GetArrayElementAtIndex(i).objectReferenceValue = sfxSources[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AudioSource FindOrCreateAudioSourceChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name);
            if (existing == null)
            {
                go.transform.SetParent(parent, false);
            }

            var source = go.GetComponent<AudioSource>();
            if (source == null)
            {
                source = go.AddComponent<AudioSource>();
            }
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f; // 2D — UI/music shouldn't attenuate with listener distance
            return source;
        }

        // ---- Fade overlay + reusable small prefabs -------------------------------------------

        private static CanvasGroup BuildFadeOverlay(Transform canvasTransform)
        {
            var go = CreatePanel("FadeOverlay", canvasTransform, Color.black);
            go.transform.SetAsLastSibling(); // always renders on top of every screen
            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        private static GameObject BuildRosterCardPrefab()
        {
            var go = new GameObject("RosterCard", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(200f, 220f);
            go.GetComponent<Image>().sprite = PlaceholderSprite.Get(new Color(0.2f, 0.2f, 0.22f));
            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childAlignment = TextAnchor.UpperCenter;
            go.GetComponent<LayoutElement>().preferredWidth = 200f;

            var portrait = CreateImage("Portrait", go.transform, Color.white, 100f, 100f);
            var nameText = CreateText("Name", go.transform, "Character", 24f, TextAlignmentOptions.Center, 34f);
            var statusText = CreateText("Status", go.transform, "Status", 18f, TextAlignmentOptions.Center, 50f);

            var card = go.AddComponent<RosterCard>();
            var so = new SerializedObject(card);
            so.FindProperty("portrait").objectReferenceValue = portrait;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.ApplyModifiedPropertiesWithoutUndo();

            return SaveAndDestroy(go, $"{UIPrefabFolder}/RosterCard.prefab");
        }

        // ---- Level Select -------------------------------------------------------------------------

        /// <summary>150x150, matching the spec's tile dimensions. Built with plain placeholder
        /// colours only — real LevelTile_Locked/unlocked-notplayed/1Star/2Stars/3Stars.png art is
        /// wired separately by ArtWiringBuilder.WireLevelSelect, same two-pass convention every
        /// other screen in this builder follows.</summary>
        private static GameObject BuildLevelTilePrefab()
        {
            var go = new GameObject("LevelTile", typeof(RectTransform), typeof(Button));
            ((RectTransform)go.transform).sizeDelta = new Vector2(150f, 150f);

            var background = CreateImage("TileBackground", go.transform, new Color(0.3f, 0.55f, 0.3f), 150f, 150f);
            StretchFull((RectTransform)background.transform);

            var button = go.GetComponent<Button>();
            button.targetGraphic = background;

            // No separate LockedIcon overlay — LevelTile_Locked.png already bakes the padlock into
            // the tile background art itself. An earlier version had an unwired placeholder square
            // here, which sat on top of the correctly-rendering background and was the actual cause
            // of the "black tiles" bug (confirmed via LevelSelectTest's runtime diagnostic).
            var tile = go.AddComponent<LevelTileController>();
            var so = new SerializedObject(tile);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("tileBackground").objectReferenceValue = background;
            // spriteLocked/spriteUnlocked/sprite1Star/sprite2Stars/sprite3Stars: left null here,
            // wired by ArtWiringBuilder — see LevelTileController.SetBackground for the fallback
            // behaviour while they're empty.
            so.ApplyModifiedPropertiesWithoutUndo();

            return SaveAndDestroy(go, $"{UIPrefabFolder}/LevelTile.prefab");
        }

        /// <summary>1920x250. No longer used by LevelSelectController — Level Select now shows one
        /// world's tiles at a time (picked via a WorldShield in its world-select state) instead of
        /// a single continuous 100-tile scroll with a divider banner between each world's section.
        /// Kept built (same "kept but unlinked" treatment as Store/Roster/Leaderboards) in case a
        /// future redesign wants a continuous multi-world scroll again.</summary>
        private static GameObject BuildWorldDividerPrefab()
        {
            var go = new GameObject("WorldDivider", typeof(RectTransform), typeof(Image));
            ((RectTransform)go.transform).sizeDelta = new Vector2(1920f, 250f);
            go.GetComponent<Image>().sprite = PlaceholderSprite.Get(new Color(0.55f, 0.4f, 0.2f));

            var nameImage = CreateImage("WorldNameImage", go.transform, new Color(0.2f, 0.15f, 0.1f), 900f, 150f);
            var nameRect = (RectTransform)nameImage.transform;
            nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 0.5f);
            nameRect.pivot = new Vector2(0.5f, 0.5f);
            nameRect.anchoredPosition = Vector2.zero;
            nameRect.sizeDelta = new Vector2(900f, 150f);
            nameImage.preserveAspect = true;

            return SaveAndDestroy(go, $"{UIPrefabFolder}/WorldDivider.prefab");
        }

        /// <summary>A single tappable world badge for Level Select's world-select carousel
        /// (CardCarouselController). CornFieldSign/VegetablePatchSign/OrchardSign/WheatfieldSign.png
        /// each already bake the full shield-shape + rope + name-text art into one sprite (set at
        /// runtime by LevelSelectController.SetWorldSignSprite from worldSignSprites), so this is
        /// just one Image + Button — no separate background/name-overlay composition needed anymore.
        /// CanvasGroup is added at runtime by LevelSelectController.RevealWorld for the shrink-and-
        /// fade transition, not built in here. CardCarouselController repositions/rescales instances
        /// of this prefab every frame, so its own sizeDelta only matters as the "full scale" size.</summary>
        private static GameObject BuildWorldShieldPrefab()
        {
            var go = new GameObject("WorldShield", typeof(RectTransform), typeof(Image), typeof(Button));
            var goRect = (RectTransform)go.transform;
            // Explicit centre anchor/pivot — CardCarouselController positions instances via
            // anchoredPosition assuming (0,0) is the container's own centre (no LayoutGroup governs
            // this anymore, unlike the old VerticalLayoutGroup-driven layout), so this can't be left
            // at whatever a freshly-created RectTransform defaults to.
            goRect.anchorMin = goRect.anchorMax = goRect.pivot = new Vector2(0.5f, 0.5f);
            // ~2.6x the original 340x360 (aspect preserved) — as large as the badge art can go
            // while still centred in the space between the header and the bottom of the screen.
            // Shrunk ~10% (897x950 -> 810x855, aspect preserved) per a follow-up mockup review —
            // the badges were reading as slightly oversized against the header/bottom margins.
            goRect.sizeDelta = new Vector2(810f, 855f);
            var background = go.GetComponent<Image>();
            background.sprite = PlaceholderSprite.Get(new Color(0.55f, 0.4f, 0.2f));
            background.preserveAspect = true;
            go.GetComponent<Button>().targetGraphic = background;

            return SaveAndDestroy(go, $"{UIPrefabFolder}/WorldShield.prefab");
        }

        /// <summary>Small auto-dismissing toast, not a SceneTransitionManager screen — built once as
        /// a scene child of LevelSelectScreen (not a reusable prefab like LevelTile/WorldDivider,
        /// since exactly one instance is ever needed) and starts inactive.</summary>
        private static LockedHintPanel BuildLockedHintPanel(Transform parent)
        {
            var panelGO = CreatePanel("LockedHintPanel", parent, new Color(0.1f, 0.1f, 0.12f, 0.92f));
            var rt = (RectTransform)panelGO.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900f, 120f);
            rt.anchoredPosition = Vector2.zero;

            var messageText = CreateText("Message", panelGO.transform, string.Empty, 32f, TextAlignmentOptions.Center, 120f);
            StretchFull((RectTransform)messageText.transform);

            var hint = panelGO.AddComponent<LockedHintPanel>();
            SetRefs(hint, ("messageText", messageText));
            panelGO.SetActive(false);
            return hint;
        }

        /// <summary>Built to the 2026-07-31 Canva mockup pair (world-select carousel + level tile
        /// grid). No separate Header container — a 200px band at the top is still reserved (for the
        /// SelectLevelSign.png title and to keep the tile grid/carousel clear of it) but there's no
        /// StarCounter to hold there anymore (removed — it read as a stray, half-clipped number in
        /// the far top-right corner rather than useful information). A round Btn_home.png back
        /// button bottom-right (CreateRoundBackButton, matching Settings' same mockup-driven
        /// deviation from the generic bottom-left back button); a standalone top-left
        /// CurrentWorldIndicator badge (shown only once a world is selected); a vertical ScrollView
        /// filling the rest of the screen (4-column tile grid, one world at a time); a
        /// WorldShieldContainer carrying CardCarouselController for the world-select state; and the
        /// LockedHintPanel toast. mainMenuScreen/gameplayScreen are cross-screen references resolved
        /// later by WireCrossReferences, same as every other screen built here.</summary>
        private static GameObject BuildLevelSelect(Transform canvasTransform, GameObject levelTilePrefab, GameObject worldShieldPrefab)
        {
            var root = CreatePanel("LevelSelectScreen", canvasTransform, new Color(0.35f, 0.55f, 0.75f));

            // No top-left LogoImage here (unlike Settings/Pause/Choose Character/Level Complete) —
            // this screen already has its own top-left identity element, CurrentWorldIndicator (built
            // below), anchored at the exact same inset. A LogoImage was added in an earlier pass
            // without noticing that clash; removed again per feedback that the two badges overlapped.

            // TitleImage replaces the old TMP "SELECT LEVEL" text — SelectLevelSign.png is the
            // word-art itself, wired by ArtWiringBuilder. preserveAspect so it never distorts.
            // Sized/positioned to match Settings' title banner (same AnchorTopCenter treatment).
            var titleImage = CreateImage("TitleImage", root.transform, Color.clear, 860f, 320f);
            titleImage.preserveAspect = true;
            AnchorTopCenter((RectTransform)titleImage.transform, new Vector2(860f, 320f), new Vector2(0f, -40f));

            // Small persistent "which world am I in" badge, top-left of the screen (not the header
            // strip) — hidden until a world is selected (see LevelSelectController.RevealWorld),
            // tapping it returns to world select. Single Image now (no separate name overlay) since
            // *Sign.png already bakes the full badge art — see LevelSelectController.worldSignSprites.
            var currentWorldIndicatorBtn = CreateButton("CurrentWorldIndicator", root.transform, string.Empty, Color.clear, 20f, 220f, out _);
            Object.DestroyImmediate(currentWorldIndicatorBtn.transform.Find("CurrentWorldIndicator_Label").gameObject);
            var indicatorImage = currentWorldIndicatorBtn.GetComponent<Image>();
            indicatorImage.preserveAspect = true;
            // Enlarged (220 -> 340) and inset further from the corner (40 -> 100, matching the
            // safe-area inset every other corner element on these mockups uses) — it was small
            // enough, and close enough to the edge, to read as clipped by the yellow safe-area guide.
            AnchorTopLeft((RectTransform)currentWorldIndicatorBtn.transform, new Vector2(340f, 340f), new Vector2(100f, -50f));
            currentWorldIndicatorBtn.gameObject.SetActive(false);

            var scrollRect = CreateVerticalScrollView("ScrollView", root.transform, out var content);
            var scrollViewRect = (RectTransform)scrollRect.transform;
            scrollViewRect.anchorMin = new Vector2(0f, 0f);
            scrollViewRect.anchorMax = new Vector2(1f, 1f);
            scrollViewRect.offsetMin = Vector2.zero;
            // TitleImage (above) is anchored at y=-40 with a 320px height, so its bottom edge sits
            // at y=-360 from the top of the screen. 320px leaves a clean ~100px gap under the
            // banner (tightened from 420px, which read as too much dead space above the tiles).
            scrollViewRect.offsetMax = new Vector2(0f, -320f);

            // World-select carousel area — vertically centred on the screen (a symmetric 200px
            // margin reserved top and bottom, matching the header's own height, so the centred
            // badge sits at the screen's true midpoint rather than skewed toward one edge) and
            // spanning most of the width so badges have room to fan out. An invisible-but-
            // raycastable Image covers the whole area (not just the badges themselves) so a flick
            // started on empty space between badges still registers as a drag; CardCarouselController
            // then positions/scales each badge every frame instead of a LayoutGroup arranging them
            // in a static row/column.
            var worldShieldContainerGO = new GameObject("WorldShieldContainer", typeof(RectTransform), typeof(Image));
            var shieldContainerRect = (RectTransform)worldShieldContainerGO.transform;
            shieldContainerRect.anchorMin = new Vector2(0.5f, 0f);
            shieldContainerRect.anchorMax = new Vector2(0.5f, 1f);
            shieldContainerRect.pivot = new Vector2(0.5f, 0.5f);
            shieldContainerRect.sizeDelta = new Vector2(1600f, -400f); // 200px margin top and bottom
            // Nudged down 16px from the screen's true vertical centre, per a follow-up mockup
            // review — the carousel read as sitting slightly too close under the SELECT LEVEL banner.
            shieldContainerRect.anchoredPosition = new Vector2(0f, -16f);
            worldShieldContainerGO.transform.SetParent(root.transform, false);
            var shieldContainerImage = worldShieldContainerGO.GetComponent<Image>();
            shieldContainerImage.sprite = PlaceholderSprite.Get(Color.clear);
            shieldContainerImage.color = Color.clear;
            shieldContainerImage.raycastTarget = true;
            var worldCarousel = worldShieldContainerGO.AddComponent<CardCarouselController>();
            // Tightened twice per feedback that badges still read as spaced too far apart —
            // 730 -> 600 (see CLAUDE.md for the original 730 sizing math). At 600, adjacent badges'
            // edges overlap by roughly 97px (badge width 810 at full scale, ~583 at the 0.72
            // side-scale falloff: 810/2 + 583/2 - 600 ~= 97px overlap), reading as a closer,
            // tighter fan than the previous non-overlapping gap. CardCarouselController arranges
            // items along a true circular arc (see its own arcRadius field) instead of a flat
            // linear x-offset, so itemSpacing here is the arc-length step between adjacent items,
            // not a straight pixel offset — arcRadius is left at the component's default (2800),
            // which reads as a natural curve at this spacing.
            var worldCarouselSO = new SerializedObject(worldCarousel);
            worldCarouselSO.FindProperty("itemSpacing").floatValue = 600f;
            worldCarouselSO.ApplyModifiedPropertiesWithoutUndo();

            // Created after the ScrollView and WorldShieldContainer (both full-bleed raycastable
            // areas) so it's the later sibling and actually receives taps instead of having them
            // swallowed by whichever of those two draws on top of it.
            var backButton = CreateRoundBackButton(root.transform);

            var lockedHintPanel = BuildLockedHintPanel(root.transform);

            var controller = root.AddComponent<LevelSelectController>();
            SetRefs(controller,
                ("levelTilePrefab", levelTilePrefab),
                ("worldShieldPrefab", worldShieldPrefab),
                ("worldShieldContainer", shieldContainerRect),
                ("worldCarousel", worldCarousel),
                ("currentWorldIndicator", (RectTransform)currentWorldIndicatorBtn.transform),
                ("currentWorldIndicatorImage", indicatorImage),
                ("currentWorldIndicatorButton", currentWorldIndicatorBtn),
                ("contentParent", (RectTransform)content),
                ("scrollRect", scrollRect),
                ("lockedHintPanel", lockedHintPanel),
                ("backButton", backButton),
                ("backButtonImage", backButton.GetComponent<Image>()));

            return root;
        }

        // ---- Main Menu --------------------------------------------------------------------------

        /// <summary>Just two icon buttons directly on the landing art — no title text (landing.png
        /// already bakes "FARM FURY ARCADE" into the art) and no vertical button stack. Character
        /// Roster/Daily Challenge/Store/Leaderboards entry points were removed from Main Menu
        /// entirely per the landing-page cleanup; those screens are still built elsewhere in
        /// BuildAll, just no longer linked from here.</summary>
        private static GameObject BuildMainMenu(Transform canvasTransform)
        {
            var root = CreatePanel("MainMenuScreen", canvasTransform, new Color(0.12f, 0.14f, 0.10f));

            // 160x160 — thumb-sized tap target (240 read as oversized once it was actually laid
            // out; 120 was the original, flagged as sitting on/outside the safe-area guide at a
            // tight 40px inset). Insets keep both buttons clear of the rounded-corner /
            // camera-cutout safe area on real devices — see the safe-area review screenshots
            // (90/-110 inset still clipped the yellow guide slightly; pulled in further).
            var playButton = CreateButton("PlayButton", root.transform, string.Empty, new Color(0.3f, 0.75f, 0.35f), 28f, 160f, out _);
            Object.DestroyImmediate(playButton.transform.Find("PlayButton_Label").gameObject);
            var playRect = (RectTransform)playButton.transform;
            playRect.anchorMin = new Vector2(0f, 0f);
            playRect.anchorMax = new Vector2(0f, 0f);
            playRect.pivot = new Vector2(0f, 0f);
            playRect.sizeDelta = new Vector2(160f, 160f);
            playRect.anchoredPosition = new Vector2(130f, 70f);

            var settingsButton = CreateButton("SettingsButton", root.transform, string.Empty, new Color(0.35f, 0.35f, 0.38f), 28f, 160f, out _);
            Object.DestroyImmediate(settingsButton.transform.Find("SettingsButton_Label").gameObject);
            var settingsRect = (RectTransform)settingsButton.transform;
            settingsRect.anchorMin = new Vector2(1f, 0f);
            settingsRect.anchorMax = new Vector2(1f, 0f);
            settingsRect.pivot = new Vector2(1f, 0f);
            settingsRect.sizeDelta = new Vector2(160f, 160f);
            settingsRect.anchoredPosition = new Vector2(-150f, 70f);

            var controller = root.AddComponent<MainMenuController>();
            var so = new SerializedObject(controller);
            so.FindProperty("playButton").objectReferenceValue = playButton;
            so.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ---- Gameplay HUD -----------------------------------------------------------------------

        private static (GameObject root, ComboNotificationBanner banner) BuildGameplayHUD(Transform canvasTransform)
        {
            var root = CreateEmpty("GameplayScreen", canvasTransform);
            StretchFull((RectTransform)root.transform);

            // Score (top-left) and Timer (top-right) — no more "LevelText" header (the level name
            // duplicated what the World Map marker the player just tapped already established, and
            // read as a redundant white text banner over the maze art). Pulled further in from an
            // original (100,-90) inset — that sat above/outside the backdrop art's own safe-area
            // guide once actually viewed on a device frame — and enlarged (40->56 / 32->46) per
            // feedback that they were too small. Font is wired to Bangers SDF (a cartoon/comic
            // bundled with TMP's Examples & Extras) by ArtWiringBuilder.WireGameplayFont, matching
            // the "same cartoon font" as the rest of the game's title/button art.
            // Enlarged (56->72 / 46->60) and pulled further in (140->170) so both sit clearly
            // inside the device safe-area guide instead of grazing its edge.
            var scoreText = CreateText("ScoreText", root.transform, "0", 72f, TextAlignmentOptions.TopLeft, 90f);
            AnchorTopLeft((RectTransform)scoreText.transform, new Vector2(320f, 90f), new Vector2(170f, -170f));

            var timerText = CreateText("TimerText", root.transform, "00:00", 60f, TextAlignmentOptions.TopRight, 90f);
            AnchorTopRight((RectTransform)timerText.transform, new Vector2(240f, 90f), new Vector2(-170f, -170f));

            // Monetisation: coin balance chip, just below ScoreText — previously SaveManager.
            // CoinBalance had no on-screen display anywhere at all (only surfaced indirectly via the
            // Revive prompt's cost text / skip-cooldown button's cost label), which is bad UX once
            // the player is actually being asked to spend coins on both of those. Coin_Balance_Chip.png
            // has its own left (icon) / right (number) halves baked in, so the icon needs no separate
            // child Image — just the number text positioned over the right half.
            const float coinChipWidth = 220f;
            const float coinChipHeight = 70f;
            var coinChipImage = CreateImage("CoinBalanceChip", root.transform, new Color(0.85f, 0.65f, 0.2f), coinChipWidth, coinChipHeight);
            var coinChipRect = (RectTransform)coinChipImage.transform;
            AnchorTopLeft(coinChipRect, new Vector2(coinChipWidth, coinChipHeight), new Vector2(170f, -276f));
            coinChipImage.preserveAspect = false;

            var coinBalanceText = CreateText("CoinBalanceText", coinChipImage.transform, "0", 32f, TextAlignmentOptions.Center, coinChipHeight);
            var coinBalanceTextRect = (RectTransform)coinBalanceText.transform;
            // Right half of the chip only — the left half is the coin icon baked into the art.
            coinBalanceTextRect.anchorMin = new Vector2(0.5f, 0f);
            coinBalanceTextRect.anchorMax = new Vector2(1f, 1f);
            coinBalanceTextRect.offsetMin = Vector2.zero;
            coinBalanceTextRect.offsetMax = Vector2.zero;
            coinBalanceText.color = new Color(0.3f, 0.2f, 0.1f);

            // Power pellet timer bar + chain counter (upper area, under the level text)
            var powerBarGO = CreatePanel("PowerPelletTimerBar", root.transform, new Color(0.2f, 0.2f, 0.22f));
            var powerBarRect = (RectTransform)powerBarGO.transform;
            AnchorTopCenter(powerBarRect, new Vector2(400f, 24f), new Vector2(0f, -80f));
            var powerFillGO = CreatePanel("Fill", powerBarGO.transform, new Color(0.85f, 0.2f, 0.85f));
            var powerFillImage = powerFillGO.GetComponent<Image>();
            powerFillImage.type = Image.Type.Filled;
            powerFillImage.fillMethod = Image.FillMethod.Horizontal;
            powerFillImage.fillAmount = 1f;

            var chainRoot = CreateEmpty("ChainCounterRoot", root.transform);
            var chainText = CreateText("ChainCounterText", chainRoot.transform, string.Empty, 26f, TextAlignmentOptions.Center, 34f);
            StretchFull((RectTransform)chainText.transform);
            AnchorTopCenter((RectTransform)chainRoot.transform, new Vector2(200f, 34f), new Vector2(0f, -112f));

            // Combo notification banner
            var bannerGO = CreatePanel("ComboBanner", root.transform, new Color(0.75f, 0.55f, 0.15f));
            var bannerRect = (RectTransform)bannerGO.transform;
            AnchorTopCenter(bannerRect, new Vector2(600f, 60f), new Vector2(0f, -150f));
            var bannerGroup = bannerGO.AddComponent<CanvasGroup>();
            var bannerText = CreateText("Text", bannerGO.transform, string.Empty, 26f, TextAlignmentOptions.Center, 60f);
            StretchFull((RectTransform)bannerText.transform);
            var banner = bannerGO.AddComponent<ComboNotificationBanner>();
            var bannerSO = new SerializedObject(banner);
            bannerSO.FindProperty("bannerText").objectReferenceValue = bannerText;
            bannerSO.FindProperty("canvasGroup").objectReferenceValue = bannerGroup;
            bannerSO.ApplyModifiedPropertiesWithoutUndo();

            // Pause icon (bottom-right), directly above the character portrait — moved inward and
            // shrunk (160->120, inset 100/70->130/90) per playtest feedback that the cluster sat
            // too close to the corner/safe-area edge. Sound and Home were removed from this
            // cluster earlier (both are still reachable via the Pause menu itself), so a single
            // Pause button is all this needs.
            // Swapped from bottom-left to bottom-right (and the D-pad from bottom-right to
            // bottom-left, below) per feedback — AnchorBottomRight needs a NEGATIVE X offset to
            // move inward (positive pushes further right/off-screen — see CreateRoundBackButton's
            // own doc comment for the same convention), unlike AnchorBottomLeft's positive-is-inward.
            const float clusterButtonSize = 120f;
            const float clusterSpacing = 20f;
            // Deepened (-90 -> -160) per a gameplay-screen review — the cluster (Pause button in
            // particular) was still crossing the yellow safe-area guide's right edge.
            const float clusterInsetX = -160f;
            const float clusterInsetY = 90f;

            // Character portrait sits at the bottom of the cluster (closer to the corner), enlarged
            // to match the Pause button's own size (was a much smaller 90x90 floating above Pause —
            // now the two read as one deliberate stack, largest/most-tappable element lowest).
            // Doubles as the on-screen ability button (Space has no touch equivalent, so without
            // this the ability was completely unreachable on a device with no keyboard): tapping it
            // raises the same InputController event Space does, and GameplayHUD dims it while the
            // active character's ability is on cooldown.
            // Cooldown ring — a radial-filled Image sitting behind (created before, so it draws
            // first/underneath) and slightly larger than the portrait button, so it reads as a ring
            // peeking out around the edges. fillAmount is driven every cooldown tick by
            // GameplayHUD.HandleAbilityCooldownChanged: empty the instant the ability is used,
            // filling back up to full as the cooldown completes. No dedicated ring art exists yet —
            // PlaceholderSprite's plain square still shows the radial fill/sweep correctly (Image.
            // Type.Filled applies regardless of the sprite's shape), it just won't look like a ring
            // until real art replaces it.
            const float ringSize = 140f;
            var ringImage = CreateImage("AbilityCooldownRing", root.transform, new Color(1f, 0.95f, 0.6f, 0.9f), ringSize, ringSize);
            var ringRect = (RectTransform)ringImage.transform;
            AnchorBottomRight(ringRect, new Vector2(ringSize, ringSize),
                new Vector2(clusterInsetX + (ringSize - clusterButtonSize) / 2f, clusterInsetY - (ringSize - clusterButtonSize) / 2f));
            ringImage.type = Image.Type.Filled;
            ringImage.fillMethod = Image.FillMethod.Radial360;
            ringImage.fillOrigin = (int)Image.Origin360.Top;
            ringImage.fillClockwise = true;
            ringImage.fillAmount = 1f;

            var portraitButton = CreateButton("CharacterPortrait", root.transform, string.Empty, new Color(1f, 0.84f, 0f), 26f, clusterButtonSize, out _);
            Object.DestroyImmediate(portraitButton.transform.Find("CharacterPortrait_Label").gameObject);
            AnchorBottomRight((RectTransform)portraitButton.transform, new Vector2(clusterButtonSize, clusterButtonSize),
                new Vector2(clusterInsetX, clusterInsetY));
            // onClick wiring happens in GameplayHUD.Awake() (via the abilityButton field below),
            // not here — a listener added directly from editor-script code doesn't survive a scene
            // save/reload (UnityEvent's non-persistent listeners aren't serialized), same pitfall
            // SimpleClosePanel exists to work around elsewhere in this builder.
            //
            // The button's own Image is now a round background (PlaceholderSprite.GetCircle) rather
            // than the square GameplayHUD swaps the character's actual portrait sprite onto — those
            // were the same Image before, so showing a real (rectangular) character sprite there
            // would have overwritten the round shape entirely. A separate non-interactive "PortraitArt"
            // child (inset slightly so the round edge stays visible around it) holds the actual
            // character sprite instead; GameplayHUD.characterPortrait now points at this child.
            var portraitBg = portraitButton.GetComponent<Image>();
            portraitBg.sprite = PlaceholderSprite.GetCircle(new Color(1f, 0.84f, 0f));

            var portraitArtGO = new GameObject("PortraitArt", typeof(RectTransform), typeof(Image));
            portraitArtGO.transform.SetParent(portraitButton.transform, false);
            var portraitArtRect = (RectTransform)portraitArtGO.transform;
            portraitArtRect.anchorMin = Vector2.zero;
            portraitArtRect.anchorMax = Vector2.one;
            float portraitArtInset = clusterButtonSize * 0.12f;
            portraitArtRect.offsetMin = new Vector2(portraitArtInset, portraitArtInset);
            portraitArtRect.offsetMax = new Vector2(-portraitArtInset, -portraitArtInset);
            var portrait = portraitArtGO.GetComponent<Image>();
            portrait.sprite = PlaceholderSprite.Get(new Color(1f, 0.84f, 0f));
            portrait.raycastTarget = false;

            var pauseButton = CreateButton("PauseButton", root.transform, string.Empty, new Color(0.35f, 0.35f, 0.38f), 28f, clusterButtonSize, out _);
            Object.DestroyImmediate(pauseButton.transform.Find("PauseButton_Label").gameObject);
            AnchorBottomRight((RectTransform)pauseButton.transform, new Vector2(clusterButtonSize, clusterButtonSize),
                new Vector2(clusterInsetX, clusterInsetY + clusterButtonSize + clusterSpacing));

            // Monetisation: "skip cooldown for 3 coins" button — sits just left of the cooldown
            // ring, vertically centred against it. Hidden by default; GameplayHUD.
            // HandleAbilityCooldownChanged shows/hides and enables/disables it every tick while an
            // ability is on cooldown (see that method's own comment for why it re-checks
            // affordability every tick rather than once). No dedicated icon art exists yet, so the
            // button keeps its auto-generated "-3" text label instead of the usual icon-only style.
            const float skipButtonSize = 64f;
            const float skipButtonGap = 16f;
            float ringRightOffsetX = clusterInsetX + (ringSize - clusterButtonSize) / 2f;
            float ringBottomOffsetY = clusterInsetY - (ringSize - clusterButtonSize) / 2f;
            float ringCenterY = ringBottomOffsetY + ringSize / 2f;
            var skipCooldownButton = CreateButton("SkipCooldownButton", root.transform, "-3",
                new Color(0.85f, 0.55f, 0.1f), 24f, skipButtonSize, out _);
            AnchorBottomRight((RectTransform)skipCooldownButton.transform, new Vector2(skipButtonSize, skipButtonSize),
                new Vector2(ringRightOffsetX - ringSize - skipButtonGap, ringCenterY - skipButtonSize / 2f));
            skipCooldownButton.gameObject.SetActive(false);

            // Directional pad (left side, diamond/D-pad layout) — up.png/down.png/left.png/
            // right.png (wired by ArtWiringBuilder) already look like complete rounded buttons on
            // their own, so each is just a plain Image+Button, no separate background needed.
            // Positioned around a shared centre point rather than each anchored independently, so
            // the diamond shape (Up above centre, Down below, Left/Right to the sides) is easy to
            // read and re-tune as one unit.
            // Tightened repeatedly (spacing 130->100->70, size 120->110->90) and pulled further in
            // from the edge (inset 200->260) — the diamond previously crossed the device safe-area
            // guide. Swapped from bottom-right to bottom-left (and the Pause/portrait cluster from
            // bottom-left to bottom-right, above) per feedback — the sub-button offsets (dpadSpacing
            // terms below) are plain screen-space deltas and don't need to change sign, only
            // dpadCenter's own X (now positive, measured inward from the left edge via
            // AnchorBottomLeft). Latest pass (100->70 / 110->90) is a further shrink per feedback
            // that the diamond's overall footprint still overlapped playable maze tiles — the maze's
            // own rendered area fills nearly the entire device safe-area guide on some aspects, so a
            // genuinely large D-pad can't avoid overlapping SOME tiles there; shrinking the diamond's
            // footprint is the only lever available without changing camera zoom/backdrop sizing.
            const float dpadButtonSize = 90f;
            const float dpadSpacing = 70f;
            const float dpadInsetX = 260f;
            const float dpadInsetY = 240f;
            Vector2 dpadCenter = new Vector2(dpadInsetX, dpadInsetY);

            var upButton = CreateButton("DPadUpButton", root.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(upButton.transform.Find("DPadUpButton_Label").gameObject);
            AnchorBottomLeft((RectTransform)upButton.transform, new Vector2(dpadButtonSize, dpadButtonSize),
                dpadCenter + new Vector2(0f, dpadSpacing));

            var downButton = CreateButton("DPadDownButton", root.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(downButton.transform.Find("DPadDownButton_Label").gameObject);
            AnchorBottomLeft((RectTransform)downButton.transform, new Vector2(dpadButtonSize, dpadButtonSize),
                dpadCenter + new Vector2(0f, -dpadSpacing));

            var leftButton = CreateButton("DPadLeftButton", root.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(leftButton.transform.Find("DPadLeftButton_Label").gameObject);
            AnchorBottomLeft((RectTransform)leftButton.transform, new Vector2(dpadButtonSize, dpadButtonSize),
                dpadCenter + new Vector2(-dpadSpacing, 0f));

            var rightButton = CreateButton("DPadRightButton", root.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(rightButton.transform.Find("DPadRightButton_Label").gameObject);
            AnchorBottomLeft((RectTransform)rightButton.transform, new Vector2(dpadButtonSize, dpadButtonSize),
                dpadCenter + new Vector2(dpadSpacing, 0f));

            var dpad = root.AddComponent<DirectionalPadController>();
            var dpadSO = new SerializedObject(dpad);
            dpadSO.FindProperty("upButton").objectReferenceValue = upButton;
            dpadSO.FindProperty("downButton").objectReferenceValue = downButton;
            dpadSO.FindProperty("leftButton").objectReferenceValue = leftButton;
            dpadSO.FindProperty("rightButton").objectReferenceValue = rightButton;
            dpadSO.ApplyModifiedPropertiesWithoutUndo();

            // Monetisation: "revive for 5 coins?" overlay, shown by GameplayHUD in response to
            // GameManager.OnReviveOffered (the 4th death this maze). Dim backdrop + a hanging-sign
            // PanelArt (same aspect-locked-child-over-dim convention as every other overlay's card
            // art, e.g. LevelComplete's PanelArt) + message/buttons content on top.
            //
            // sizeDelta matches the CURRENT art's actual 666x375 (~1.776:1, a wide banner) pixel
            // aspect — the art was replaced with a differently-shaped asset after this was first
            // tuned for an earlier ~2048x1940 near-square version, and the box size was never
            // updated to match. That mismatch mattered more than it should have: SetImageSprite (in
            // ArtWiringBuilder, which is what actually assigns this Image's sprite) always sets
            // Image.Type.Sliced, and Sliced IGNORES preserveAspect entirely — so the wide banner art
            // was being force-stretched into the old near-square box's proportions, visibly squashed
            // and reading as "too small," with Yes/No overflowing past its now-narrower rendered
            // edges. Getting the box's own aspect right makes the forced stretch uniform (so it's
            // exactly as if preserveAspect worked correctly) regardless of that Sliced quirk. Also
            // enlarged overall (was 900 wide) per feedback that the backdrop read as too small.
            var reviveRoot = CreatePanel("RevivePromptOverlay", root.transform, new Color(0f, 0f, 0f, 0.85f));

            var revivePanelArtGO = new GameObject("PanelArt", typeof(RectTransform), typeof(Image));
            revivePanelArtGO.transform.SetParent(reviveRoot.transform, false);
            var revivePanelArtRect = (RectTransform)revivePanelArtGO.transform;
            revivePanelArtRect.anchorMin = revivePanelArtRect.anchorMax = new Vector2(0.5f, 0.5f);
            revivePanelArtRect.sizeDelta = new Vector2(1300f, 731f); // 666x375 aspect, enlarged
            revivePanelArtRect.anchoredPosition = Vector2.zero;
            var revivePanelArtImage = revivePanelArtGO.GetComponent<Image>();
            revivePanelArtImage.sprite = PlaceholderSprite.Get(Color.clear);
            revivePanelArtImage.preserveAspect = true;

            // Kept notably narrower than the backdrop's own 1300 width — per feedback the buttons
            // themselves should read smaller relative to the now-larger backdrop, not stretch to
            // fill it.
            var reviveGroup = CreateVerticalGroup("Content", revivePanelArtGO.transform, 14f, 30);
            var reviveGroupRect = (RectTransform)reviveGroup.transform;
            reviveGroupRect.sizeDelta = new Vector2(750f, reviveGroupRect.sizeDelta.y);
            reviveGroupRect.anchoredPosition = new Vector2(0f, -20f);

            // No separate coin-icon/cost-text row anymore — the replacement panel art (see
            // RevivePromptPanel's own doc comment) bakes "Revive for 5 coins?" directly into its
            // bottom slot, so a duplicate runtime text row would just repeat it. costText is left
            // unwired below; RevivePromptController.Show() already null-checks it.
            var reviveButton = CreateButton("ReviveButton", reviveGroup.transform, string.Empty, new Color(0.2f, 0.65f, 0.3f), out _);
            Object.DestroyImmediate(reviveButton.transform.Find("ReviveButton_Label").gameObject);
            var declineButton = CreateButton("DeclineButton", reviveGroup.transform, string.Empty, new Color(0.35f, 0.35f, 0.38f), out _);
            Object.DestroyImmediate(declineButton.transform.Find("DeclineButton_Label").gameObject);
            // reviveGroup's VerticalLayoutGroup has childControlHeight=false (see CreateVerticalGroup),
            // so a button's LayoutElement.preferredHeight (set inside CreateButton) is never actually
            // applied — the same CreateImage-args-are-inert pattern found elsewhere in this file.
            // Height set explicitly here instead; width still comes from the layout group
            // (childControlWidth=true), so only .y needs overriding.
            const float reviveButtonHeight = 90f; // was 130 — reduced per feedback, buttons read too large
            var reviveButtonRect = (RectTransform)reviveButton.transform;
            reviveButtonRect.sizeDelta = new Vector2(reviveButtonRect.sizeDelta.x, reviveButtonHeight);
            var declineButtonRect = (RectTransform)declineButton.transform;
            declineButtonRect.sizeDelta = new Vector2(declineButtonRect.sizeDelta.x, reviveButtonHeight);
            reviveRoot.SetActive(false);

            var revivePrompt = reviveRoot.AddComponent<RevivePromptController>();
            var reviveSO = new SerializedObject(revivePrompt);
            reviveSO.FindProperty("reviveButton").objectReferenceValue = reviveButton;
            reviveSO.FindProperty("declineButton").objectReferenceValue = declineButton;
            reviveSO.ApplyModifiedPropertiesWithoutUndo();

            var hud = root.AddComponent<GameplayHUD>();
            var so = new SerializedObject(hud);
            so.FindProperty("scoreText").objectReferenceValue = scoreText;
            so.FindProperty("timerText").objectReferenceValue = timerText;
            so.FindProperty("coinBalanceText").objectReferenceValue = coinBalanceText;
            so.FindProperty("characterPortrait").objectReferenceValue = portrait;
            so.FindProperty("abilityButton").objectReferenceValue = portraitButton;
            so.FindProperty("abilityCooldownRing").objectReferenceValue = ringImage;
            so.FindProperty("pauseButton").objectReferenceValue = pauseButton;
            so.FindProperty("powerPelletTimerBar").objectReferenceValue = powerBarGO;
            so.FindProperty("powerPelletTimerFill").objectReferenceValue = powerFillImage;
            so.FindProperty("chainCounterRoot").objectReferenceValue = chainRoot;
            so.FindProperty("chainCounterText").objectReferenceValue = chainText;
            so.FindProperty("revivePrompt").objectReferenceValue = revivePrompt;
            so.FindProperty("skipCooldownButton").objectReferenceValue = skipCooldownButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return (root, banner);
        }

        // ---- Pause Menu -------------------------------------------------------------------------

        /// <summary>Paused.png bakes in the "PAUSED" title and all 5 button-row backgrounds at
        /// fixed positions — Resume/SwapCharacter/Restart/Settings/Quit.png (real button art wired
        /// by ArtWiringBuilder) sit exactly on top of those five baked-in rows instead of generic
        /// CreateButton rectangles + a redundant "PAUSED" text. Anchor fractions below were
        /// measured directly off Paused.png (a 2048x2048 square image, stretched to fill this
        /// full-screen overlay) — same reasoning as BuildLevelFailed's SetAnchorRect comment.
        /// Rebuilt to a Canva mockup (2026-07-31): the root's own Image is now World1_Cornfield.png
        /// (an opaque farm backdrop, wired by ArtWiringBuilder), not the previous plain black dim —
        /// Pause now fully replaces the view rather than dimming the gameplay maze behind it, same
        /// "own dedicated background" treatment the mockup already gave Settings/Level Select.
        /// LogoImage (top-left, same as Settings/Level Select) was added to match.</summary>
        private static GameObject BuildPauseMenu(Transform canvasTransform)
        {
            var root = CreatePanel("PauseOverlay", canvasTransform, Color.black);

            var logoImageGO = new GameObject("LogoImage", typeof(RectTransform), typeof(Image));
            logoImageGO.transform.SetParent(root.transform, false);
            var logoImage = logoImageGO.GetComponent<Image>();
            logoImage.sprite = PlaceholderSprite.Get(Color.clear);
            logoImage.preserveAspect = true;
            // Inset further than the other 3 screens sharing this logo convention (40 -> 100) —
            // it was close enough to the corner to read as clipped by the yellow safe-area guide.
            AnchorTopLeft((RectTransform)logoImageGO.transform, new Vector2(300f, 170f), new Vector2(100f, -50f));

            // Paused.png is a SQUARE (2048x2048) parchment/frame card with its 5 button rows baked
            // into the art. Its old wiring set it directly as the root panel's own Image — the root
            // stretches full-screen (StretchFull), so on a real landscape device aspect the square
            // art got non-uniformly stretched, squashing the baked-in button rows together and
            // making the separately-wired button art (Resume.png etc, positioned by the fractions
            // below) drift out of alignment with them / off the visible card entirely. "PanelArt" is
            // a child that stays centred and square via AspectRatioFitter (FitInParent), so it never
            // exceeds the overlay's bounds regardless of device aspect. The 5 buttons move under
            // PanelArt so their fractions — tuned against the art's own baked button positions —
            // line up with it at any aspect.
            var panelArtGO = new GameObject("PanelArt", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            panelArtGO.transform.SetParent(root.transform, false);
            var panelArtRect = (RectTransform)panelArtGO.transform;
            panelArtRect.anchorMin = Vector2.zero;
            panelArtRect.anchorMax = Vector2.one;
            panelArtRect.offsetMin = Vector2.zero;
            panelArtRect.offsetMax = Vector2.zero;
            panelArtGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(Color.clear);
            var panelArtFitter = panelArtGO.GetComponent<AspectRatioFitter>();
            panelArtFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            panelArtFitter.aspectRatio = 1f;

            // Fractions widened slightly (~0.015 horizontally, ~0.004 vertically) from the values
            // originally measured off Paused.png — the button art was sitting a hair inside its
            // row's baked-in outline, leaving a thin sliver of the parchment's own button shape
            // visible around each one instead of being fully covered. Nudged down another ~0.012
            // (all 5, uniformly) per a follow-up review — "almost perfectly aligned" but sitting a
            // touch high of the baked-in rows.
            var resumeButton = CreateButton("ResumeButton", panelArtGO.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(resumeButton.transform.Find("ResumeButton_Label").gameObject);
            SetAnchorRect((RectTransform)resumeButton.transform, 0.31f, 0.584f, 0.69f, 0.6745f);

            var swapButton = CreateButton("SwapButton", panelArtGO.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(swapButton.transform.Find("SwapButton_Label").gameObject);
            SetAnchorRect((RectTransform)swapButton.transform, 0.2475f, 0.479f, 0.75f, 0.5695f);

            var restartButton = CreateButton("RestartButton", panelArtGO.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(restartButton.transform.Find("RestartButton_Label").gameObject);
            SetAnchorRect((RectTransform)restartButton.transform, 0.31f, 0.3765f, 0.69f, 0.4645f);

            var settingsButton = CreateButton("SettingsButton", panelArtGO.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(settingsButton.transform.Find("SettingsButton_Label").gameObject);
            SetAnchorRect((RectTransform)settingsButton.transform, 0.31f, 0.274f, 0.69f, 0.362f);

            var quitButton = CreateButton("QuitButton", panelArtGO.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(quitButton.transform.Find("QuitButton_Label").gameObject);
            // Quit alone nudged down another ~0.01 per feedback — the other 4 buttons are confirmed
            // correctly aligned now and were left untouched.
            SetAnchorRect((RectTransform)quitButton.transform, 0.3525f, 0.169f, 0.65f, 0.252f);

            var controller = root.AddComponent<PauseMenuController>();
            var so = new SerializedObject(controller);
            so.FindProperty("resumeButton").objectReferenceValue = resumeButton;
            so.FindProperty("swapButton").objectReferenceValue = swapButton;
            so.FindProperty("restartButton").objectReferenceValue = restartButton;
            so.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            so.FindProperty("quitToMenuButton").objectReferenceValue = quitButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ---- Anchor helpers (corner-pinned, non-stretching UI elements) -----------------------

        private static void AnchorTopLeft(RectTransform rt, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
        }

        private static void AnchorTopRight(RectTransform rt, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
        }

        private static void AnchorTopCenter(RectTransform rt, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
        }

        private static void AnchorBottomLeft(RectTransform rt, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
        }

        /// <summary>Generic back-button placement, used by every screen that has one (Character
        /// Roster, Leaderboards, Choose Character) — bottom-left, 160x160, safe-area inset (100,70).
        /// Matches Gameplay's PauseButton and Main Menu's Play/Settings buttons exactly, so a back
        /// button always lands in the same place regardless of which screen it's on, instead of each
        /// screen picking its own ad-hoc corner/size. Settings and Level Select deliberately deviate
        /// from this — see CreateRoundBackButton, used by both per their own Canva mockups.</summary>
        private static Button CreateGenericBackButton(Transform screenRoot)
        {
            var backButton = CreateButton("BackButton", screenRoot, string.Empty, new Color(0.35f, 0.35f, 0.38f), 28f, 160f, out _);
            Object.DestroyImmediate(backButton.transform.Find("BackButton_Label").gameObject);
            AnchorBottomLeft((RectTransform)backButton.transform, new Vector2(160f, 160f), new Vector2(100f, 70f));
            return backButton;
        }

        /// <summary>Round back button — used by Settings and Level Select (bottom-right, Btn_home.png)
        /// and Choose Character (bottom-left, per its own mockup — see ArtWiringBuilder.WireButtons
        /// for which icon each ends up with), all built to Canva mockups (2026-07-31) that place a
        /// round icon there instead of the rectangular Btn_back.png every other screen uses
        /// (CreateGenericBackButton). 160x160, safe-area inset either way — bottomRight's X inset
        /// must be negative (AnchorBottomRight's pivot sits at the parent's right edge, so a
        /// positive X pushes the button further right/off-screen instead of inward; only
        /// AnchorBottomLeft's positive-X-is-inward convention matches a plain (100,70) offset). A
        /// stray copy-paste of the bottom-left offset here previously left Btn_home mostly clipped
        /// off the right edge of the screen (only ~60 of its 160px width on-screen) — confirmed via
        /// a device-frame screenshot review. -150 (rather than just -100) gives it a bit more
        /// breathing room inside the safe-area guide.</summary>
        private static Button CreateRoundBackButton(Transform screenRoot, bool bottomRight = true)
        {
            var backButton = CreateButton("BackButton", screenRoot, string.Empty, new Color(0.6f, 0.4f, 0.15f), 28f, 160f, out _);
            Object.DestroyImmediate(backButton.transform.Find("BackButton_Label").gameObject);
            if (bottomRight)
            {
                AnchorBottomRight((RectTransform)backButton.transform, new Vector2(160f, 160f), new Vector2(-150f, 70f));
            }
            else
            {
                // Was 60 — a device-frame check showed it sitting outside the yellow safe-area
                // guide, not inside it as previously assumed. Raised to 110, matching the generic
                // bottom-left inset every other screen uses.
                AnchorBottomLeft((RectTransform)backButton.transform, new Vector2(160f, 160f), new Vector2(110f, 70f));
            }
            return backButton;
        }

        private static void AnchorBottomCenter(RectTransform rt, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
        }

        private static void AnchorBottomRight(RectTransform rt, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
        }

        // ---- Settings ---------------------------------------------------------------------------

        /// <summary>Packs 2 LayoutElement-bearing GameObjects (e.g. a toggle root + a slider root)
        /// into one horizontal row — the first item at a fixed width, the rest sharing whatever
        /// width remains. Used to fit a "Music"/"SFX" toggle and its volume slider on one plaque
        /// row instead of two.</summary>
        private static GameObject CombineRow(string name, Transform parent, GameObject fixedWidthItem, GameObject flexibleItem)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            row.GetComponent<LayoutElement>().preferredHeight = 40f;
            ((RectTransform)row.transform).sizeDelta = new Vector2(0f, 40f);

            var fixedLE = fixedWidthItem.GetComponent<LayoutElement>();
            fixedLE.preferredWidth = 220f;
            fixedLE.flexibleWidth = 0f;
            var flexLE = flexibleItem.GetComponent<LayoutElement>();
            flexLE.flexibleWidth = 1f;

            fixedWidthItem.transform.SetParent(row.transform, false);
            flexibleItem.transform.SetParent(row.transform, false);
            return row;
        }

        /// <summary>Sits a control (toggle/slider row, dropdown, ...) centred on its own
        /// Btn_plaque.png-backed row instead of floating with no framing — see BuildSettingsPanel's
        /// doc comment for why one giant stretched plaque behind everything doesn't work. The
        /// plaque GameObject is named "&lt;content.name&gt;_Plaque" so ArtWiringBuilder.WireButtons
        /// can address it by a predictable path.</summary>
        private static GameObject WrapInPlaqueRow(GameObject content, float height)
        {
            var parent = content.transform.parent;
            int siblingIndex = content.transform.GetSiblingIndex();

            var plaqueGO = new GameObject(content.name + "_Plaque", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            plaqueGO.transform.SetParent(parent, false);
            plaqueGO.transform.SetSiblingIndex(siblingIndex);
            plaqueGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(new Color(0.55f, 0.35f, 0.15f));
            var le = plaqueGO.GetComponent<LayoutElement>();
            le.preferredHeight = height;
            ((RectTransform)plaqueGO.transform).sizeDelta = new Vector2(0f, height);

            content.transform.SetParent(plaqueGO.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 0.5f);
            contentRect.anchorMax = new Vector2(1f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(-100f, height - 24f);
            contentRect.anchoredPosition = Vector2.zero;

            return plaqueGO;
        }

        /// <summary>Built to a specific Canva mockup (2026-07-31), not freehand: full-screen
        /// Bg_LevelSelect.png (moon/windmill/barn night farm, already includes all decorative
        /// elements — nothing else needs to draw them), Logo.png top-left, SettingsSign.png as the
        /// header (replacing the old TMP "SETTINGS" text), a 2-column x 3-row grid of Btn_plaque.png
        /// cells for the actual controls, and a round Btn_home.png back button bottom-right — the
        /// one screen-specific deviation from CreateGenericBackButton's bottom-left convention,
        /// because the mockup places it there explicitly. Only 5 of the 6 grid cells are filled
        /// (Music/SFX/Vibration/Left-Handed/Language); the 6th (bottom-right of the grid) is left
        /// empty per spec rather than inventing a 6th setting. Volume sliders were dropped — Music/
        /// SFX are now simple whole-plaque mute toggles like Vibration/Left-Handed, since a plaque
        /// this size can't cleanly host both a tap target and a drag target without gesture
        /// conflicts, and the mockup's plaques are all the same plain shape with no slider drawn.</summary>
        private static GameObject BuildSettingsPanel(Transform canvasTransform)
        {
            var root = CreatePanel("SettingsOverlay", canvasTransform, Color.black);
            StretchFull((RectTransform)root.transform);

            // Logo/BackButton positions come from the 2026-07-31 device-frame screenshot review —
            // both were confirmed correct and are left alone. Title and the plaque grid went through
            // a second pass after that review: the first attempt repositioned both to avoid overlap,
            // but the ask was resize-only, not reposition — so both are back at their original
            // anchored positions here. Title's size is capped at ~1.23x (not the full ~1.86x tried
            // before) specifically because anything bigger, at its original position, overlaps the
            // enlarged Logo (which keeps its new size/position per that review) — a geometric limit,
            // not a design choice. The grid's anchoredPosition is nudged down slightly (-60 -> -90)
            // from its original value to clear the modestly-bigger title; its own box (sizeDelta,
            // cellSize) is otherwise unchanged from its original spacious layout except for the
            // smaller cellSize/spacing, which is the one dimension actually meant to shrink.
            var logoImageGO = new GameObject("LogoImage", typeof(RectTransform), typeof(Image));
            logoImageGO.transform.SetParent(root.transform, false);
            var logoImage = logoImageGO.GetComponent<Image>();
            logoImage.sprite = PlaceholderSprite.Get(Color.clear);
            logoImage.preserveAspect = true;
            AnchorTopLeft((RectTransform)logoImageGO.transform, new Vector2(420f, 238f), new Vector2(100f, -50f));

            var titleImageGO = new GameObject("TitleImage", typeof(RectTransform), typeof(Image));
            titleImageGO.transform.SetParent(root.transform, false);
            var titleImage = titleImageGO.GetComponent<Image>();
            titleImage.sprite = PlaceholderSprite.Get(Color.clear);
            titleImage.preserveAspect = true;
            AnchorTopCenter((RectTransform)titleImageGO.transform, new Vector2(860f, 320f), new Vector2(0f, -40f));

            var closeButton = CreateRoundBackButton(root.transform);

            var gridGO = new GameObject("SettingsGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGO.transform.SetParent(root.transform, false);
            var gridRect = (RectTransform)gridGO.transform;
            gridRect.anchorMin = gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            gridRect.sizeDelta = new Vector2(900f, 460f);
            gridRect.anchoredPosition = new Vector2(0f, -90f);
            var grid = gridGO.GetComponent<GridLayoutGroup>();
            // Plaques enlarged 2x (210x60 -> 420x120) per request; spacing scaled to match so the
            // now-bigger cells stay evenly spaced rather than crowding together. CreateTogglePlaqueCell
            // keeps each label's own text box pinned at the ORIGINAL 210x60 size (see its own comment)
            // so only the plaque artwork grows, not the text.
            grid.cellSize = new Vector2(420f, 120f);
            grid.spacing = new Vector2(48f, 40f);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            var musicToggle = CreateTogglePlaqueCell("MusicCell", gridGO.transform, "Music");
            var sfxToggle = CreateTogglePlaqueCell("SfxCell", gridGO.transform, "SFX");
            var vibrationToggle = CreateTogglePlaqueCell("VibrationCell", gridGO.transform, "Vibration");
            var leftHandedToggle = CreateTogglePlaqueCell("LeftHandedCell", gridGO.transform, "Left-Handed");

            var languageCellGO = new GameObject("LanguageCell", typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
            languageCellGO.transform.SetParent(gridGO.transform, false);
            languageCellGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(new Color(0.55f, 0.35f, 0.15f));
            var languageDropdown = languageCellGO.GetComponent<TMP_Dropdown>();
            languageDropdown.options.Clear();
            languageDropdown.options.Add(new TMP_Dropdown.OptionData("English"));
            var langLabel = CreateText("Label", languageCellGO.transform, "English", 52f, TextAlignmentOptions.Center, 40f);
            // Same fixed-size-label-inside-a-bigger-plaque treatment as CreateTogglePlaqueCell.
            var langLabelRect = (RectTransform)langLabel.transform;
            langLabelRect.anchorMin = langLabelRect.anchorMax = new Vector2(0.5f, 0.5f);
            langLabelRect.pivot = new Vector2(0.5f, 0.5f);
            langLabelRect.anchoredPosition = Vector2.zero;
            langLabelRect.sizeDelta = new Vector2(210f, 60f);
            langLabel.enableAutoSizing = true;
            langLabel.fontSizeMin = 20f;
            langLabel.fontSizeMax = 52f;
            languageDropdown.captionText = langLabel;
            languageDropdown.targetGraphic = languageCellGO.GetComponent<Image>();
            languageDropdown.template = CreateDropdownTemplate(languageCellGO.transform, languageDropdown);
            // 6th grid cell (bottom-right) intentionally left empty — no GameObject created for it.

            var versionText = CreateText("VersionText", root.transform, "v0.1", 16f, TextAlignmentOptions.Center, 24f);
            var versionRect = (RectTransform)versionText.transform;
            AnchorBottomCenter(versionRect, new Vector2(200f, 24f), new Vector2(0f, 20f));

            var controller = root.AddComponent<SettingsPanel>();
            var so = new SerializedObject(controller);
            so.FindProperty("musicToggle").objectReferenceValue = musicToggle;
            so.FindProperty("sfxToggle").objectReferenceValue = sfxToggle;
            so.FindProperty("vibrationToggle").objectReferenceValue = vibrationToggle;
            so.FindProperty("languageDropdown").objectReferenceValue = languageDropdown;
            so.FindProperty("leftHandedToggle").objectReferenceValue = leftHandedToggle;
            so.FindProperty("versionText").objectReferenceValue = versionText;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        /// <summary>TMP_Dropdown.Show() throws ("The template needs to be assigned and must have a
        /// child GameObject with a Toggle component") if template is left null — it was never
        /// wired here, so tapping the Language plaque errored instead of opening. Builds the
        /// minimal Template/Viewport/Content/Item hierarchy TMP_Dropdown requires (single "English"
        /// option, so the dropdown list itself is mostly vestigial today, but it still needs to be
        /// functional rather than throwing).</summary>
        private static RectTransform CreateDropdownTemplate(Transform dropdownParent, TMP_Dropdown dropdown)
        {
            var templateGO = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            templateGO.transform.SetParent(dropdownParent, false);
            var templateRect = (RectTransform)templateGO.transform;
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, 2f);
            templateRect.sizeDelta = new Vector2(0f, 150f);
            templateGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(new Color(0.2f, 0.12f, 0.05f, 0.97f));

            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGO.transform.SetParent(templateGO.transform, false);
            StretchFull((RectTransform)viewportGO.transform);
            viewportGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(Color.white);
            viewportGO.GetComponent<Mask>().showMaskGraphic = false;

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = (RectTransform)contentGO.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 40f);

            var itemGO = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            itemGO.transform.SetParent(contentGO.transform, false);
            var itemRect = (RectTransform)itemGO.transform;
            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(1f, 1f);
            itemRect.pivot = new Vector2(0.5f, 1f);
            itemRect.anchoredPosition = Vector2.zero;
            itemRect.sizeDelta = new Vector2(0f, 40f);

            var itemBgGO = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            itemBgGO.transform.SetParent(itemGO.transform, false);
            StretchFull((RectTransform)itemBgGO.transform);
            var itemBgImage = itemBgGO.GetComponent<Image>();
            itemBgImage.sprite = PlaceholderSprite.Get(new Color(0.55f, 0.35f, 0.15f));

            var itemCheckGO = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
            itemCheckGO.transform.SetParent(itemGO.transform, false);
            var itemCheckRect = (RectTransform)itemCheckGO.transform;
            itemCheckRect.anchorMin = itemCheckRect.anchorMax = new Vector2(0f, 0.5f);
            itemCheckRect.pivot = new Vector2(0.5f, 0.5f);
            itemCheckRect.sizeDelta = new Vector2(20f, 20f);
            itemCheckRect.anchoredPosition = new Vector2(20f, 0f);
            var itemCheckImage = itemCheckGO.GetComponent<Image>();
            itemCheckImage.sprite = PlaceholderSprite.Get(Color.white);

            var itemLabelGO = new GameObject("Item Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            itemLabelGO.transform.SetParent(itemGO.transform, false);
            var itemLabelRect = (RectTransform)itemLabelGO.transform;
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(40f, 2f);
            itemLabelRect.offsetMax = new Vector2(-10f, -2f);
            var itemLabel = itemLabelGO.GetComponent<TextMeshProUGUI>();
            itemLabel.text = "Option A";
            itemLabel.fontSize = 32f;
            itemLabel.alignment = TextAlignmentOptions.Left;
            itemLabel.color = Color.black;

            var itemToggle = itemGO.GetComponent<Toggle>();
            itemToggle.targetGraphic = itemBgImage;
            itemToggle.graphic = itemCheckImage;
            itemToggle.isOn = true;

            var scrollRect = templateGO.GetComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = (RectTransform)viewportGO.transform;
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            dropdown.itemText = itemLabel;
            dropdown.itemImage = itemCheckImage;

            templateGO.SetActive(false);
            return templateRect;
        }

        /// <summary>One Btn_plaque.png grid cell that IS the toggle — Toggle lives on the same
        /// GameObject as the Image (targetGraphic = its own Image), so the entire plaque is the
        /// click target, not just a small child checkbox. A centred label sits on top; no separate
        /// checkbox/checkmark graphic is created at all, matching the mockup's plain plaques.</summary>
        private static Toggle CreateTogglePlaqueCell(string name, Transform parent, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Toggle));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = PlaceholderSprite.Get(new Color(0.55f, 0.35f, 0.15f));

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = image;
            toggle.graphic = null;
            toggle.isOn = true;

            var labelText = CreateText(name + "_Label", go.transform, label, 56f, TextAlignmentOptions.Center, 40f);
            // Fixed at the plaque's ORIGINAL size (210x60), centred, rather than stretched to fill
            // the cell — the cell itself was enlarged 2x (see BuildSettingsPanel) so the plaque
            // artwork reads bigger, but keeping the label's own box at its old size means its
            // auto-sizing font computes the exact same size as before instead of growing with it.
            var labelRect = (RectTransform)labelText.transform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(210f, 60f);
            // Auto-shrink rather than clip/overflow — the doubled font size (28 -> 56) doesn't
            // reliably fit every label ("Left-Handed", "Vibration") at every plaque size.
            labelText.enableAutoSizing = true;
            labelText.fontSizeMin = 20f;
            labelText.fontSizeMax = 56f;

            return toggle;
        }

        // ---- Store "coming soon" (Store itself is Phase 6 scope) -------------------------------

        private static GameObject BuildStoreComingSoonPanel(Transform canvasTransform)
        {
            var root = CreatePanel("StoreComingSoonOverlay", canvasTransform, new Color(0f, 0f, 0f, 0.85f));
            var group = CreateVerticalGroup("Content", root.transform, 14f, 30);
            CreateText("Message", group.transform, "The Store is coming in Phase 6!", 32f, TextAlignmentOptions.Center, 60f);
            var closeButton = CreateButton("CloseButton", group.transform, "Back", new Color(0.35f, 0.35f, 0.38f), out _);

            var simpleClose = root.AddComponent<SimpleClosePanel>();
            var closeSO = new SerializedObject(simpleClose);
            closeSO.FindProperty("closeButton").objectReferenceValue = closeButton;
            closeSO.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ---- Level Complete + New Character Unlock ---------------------------------------------

        /// <summary>Built to a Canva mockup (2026-07-31): World1_Cornfield.png backdrop (same as
        /// Pause/Choose Character) with Logo.png top-left, LevelComplete.png as an aspect-locked
        /// PanelArt child (same square-art-on-landscape-overlay pattern BuildPauseMenu uses, for the
        /// same reason — stretching a square card full-screen distorts it), a small star row +
        /// score readout positioned on the art's own wooden shelf (SetAnchorRect fractions measured
        /// off the art, same convention as Pause's button fractions), and a 3-button row (Play/Home/
        /// Settings — see LevelCompleteController's doc comment for what each does) near the bottom,
        /// replacing an earlier single Btn_skip.png button in the same spot. The previous crop/
        /// robot/time/perfect-bonus breakdown, combo achievements, and "new best" badge are gone.
        /// LogoImage's inset matches the 100px fix already applied to Settings/Pause/Level Select
        /// (see CLAUDE.md's device-frame-review notes) — this screen hadn't gotten that pass yet and
        /// was still clipping against the yellow safe-area guide at the old 40px inset.</summary>
        private static (GameObject root, NewCharacterUnlockScreen unlockScreen) BuildLevelComplete(Transform canvasTransform)
        {
            var root = CreatePanel("LevelCompleteScreen", canvasTransform, Color.black);

            var logoImageGO = new GameObject("LogoImage", typeof(RectTransform), typeof(Image));
            logoImageGO.transform.SetParent(root.transform, false);
            var logoImage = logoImageGO.GetComponent<Image>();
            logoImage.sprite = PlaceholderSprite.Get(Color.clear);
            logoImage.preserveAspect = true;
            AnchorTopLeft((RectTransform)logoImageGO.transform, new Vector2(300f, 170f), new Vector2(100f, -40f));

            var panelArtGO = new GameObject("PanelArt", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            panelArtGO.transform.SetParent(root.transform, false);
            var panelArtRect = (RectTransform)panelArtGO.transform;
            panelArtRect.anchorMin = Vector2.zero;
            panelArtRect.anchorMax = Vector2.one;
            panelArtRect.offsetMin = Vector2.zero;
            panelArtRect.offsetMax = Vector2.zero;
            panelArtGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(Color.clear);
            var panelArtFitter = panelArtGO.GetComponent<AspectRatioFitter>();
            panelArtFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            panelArtFitter.aspectRatio = 1f;

            // Score + star row sit in the art's own blank middle area, below the 3 decorative
            // always-filled stars baked into the card art just under the "LEVEL COMPLETE!" banner
            // (that banner and those decorative stars are art, not these). Score now comes FIRST
            // (closest to the baked stars) with the real StarDisplay row moved below it — the two
            // used to be reversed (Stars on top, right under the baked-in stars), which visually
            // clashed since a second row of stars sat almost directly beneath the art's own first
            // row. Band also nudged down (0.36-0.62 -> 0.28-0.56) for clearance from the baked
            // stars. Font enlarged again (52 -> 66) and spacing increased (16 -> 20) per repeated
            // feedback that it still read as too small/low.
            var shelfGO = CreateVerticalGroup("ShelfContent", panelArtGO.transform, 20f, 0);
            SetAnchorRect((RectTransform)shelfGO.transform, 0.27f, 0.28f, 0.73f, 0.56f);
            var scoreText = CreateText("ScoreText", shelfGO.transform, "0", 66f, TextAlignmentOptions.Center, 80f, new Color(0.3f, 0.2f, 0.1f));
            var starDisplayGO = CreateStarDisplay("Stars", shelfGO.transform, 28);

            // Play/Home/Settings used to be one bottom-right row; split per feedback — Play now
            // sits alone bottom-left (matching the same bottom-left safe-area inset every other
            // screen's back button uses), Home/Settings stay paired bottom-right. Both rows share
            // the same 110px bottom inset so their button centres land on the same horizontal line
            // (matching the horseshoe art baked into both bottom corners of the card) — they'd
            // drifted out of alignment when Play's own inset was deepened (70 -> 110) for
            // safe-area clearance without carrying the same change over to ActionButtons.
            var playButton = CreateButton("PlayButton", root.transform, string.Empty, new Color(0.85f, 0.55f, 0.1f), 28f, 130f, out _);
            Object.DestroyImmediate(playButton.transform.Find("PlayButton_Label").gameObject);
            AnchorBottomLeft((RectTransform)playButton.transform, new Vector2(130f, 130f), new Vector2(150f, 110f));

            var actionRow = CreateHorizontalGroup("ActionButtons", root.transform, 24f);
            actionRow.GetComponent<LayoutElement>().preferredHeight = 130f;
            // Bottom-right (was bottom-center) per a device-frame review — bottom-center sat
            // outside the yellow safe-area guide on a real aspect. Inset deepened again (-100 ->
            // -220) per a follow-up review — the row (Play, when it was still part of this group)
            // still crossed the yellow guide's right edge at -100. Negative X moves inward for a
            // right-pivoted anchor (see CreateRoundBackButton's doc comment). Width shrunk to match
            // now that it's only 2 buttons instead of 3. Y inset raised (70 -> 110) to match
            // PlayButton's own bottom inset — see the comment above.
            AnchorBottomRight((RectTransform)actionRow.transform, new Vector2(320f, 130f), new Vector2(-220f, 110f));

            var homeButton = CreateButton("HomeButton", actionRow.transform, string.Empty, new Color(0.85f, 0.55f, 0.1f), 28f, 130f, out _);
            Object.DestroyImmediate(homeButton.transform.Find("HomeButton_Label").gameObject);
            var settingsButton = CreateButton("SettingsButton", actionRow.transform, string.Empty, new Color(0.85f, 0.55f, 0.1f), 28f, 130f, out _);
            Object.DestroyImmediate(settingsButton.transform.Find("SettingsButton_Label").gameObject);

            // New Character Unlock overlay, layered on top of Level Complete — rebuilt to match a
            // Canva mockup: full-screen night-farm backdrop (same World1_Cornfield.png convention
            // as Pause/Choose Character/this screen's own root), Logo top-left, a wood-sign
            // "Unlocked" banner top-centre, and the character's own selectCardArt large and
            // centred (that art already has the character's name baked in — see
            // NewCharacterUnlockScreen's doc comment — so no separate name/title/stats text is
            // needed at all). Tapping anywhere dismisses it (tapButton, wired below) instead of a
            // fixed auto-dismiss timer.
            var unlockRoot = CreatePanel("NewCharacterUnlockOverlay", root.transform, Color.black);
            var unlockTapButton = unlockRoot.AddComponent<Button>();
            unlockTapButton.targetGraphic = unlockRoot.GetComponent<Image>();

            var unlockLogoGO = new GameObject("LogoImage", typeof(RectTransform), typeof(Image));
            unlockLogoGO.transform.SetParent(unlockRoot.transform, false);
            var unlockLogoImage = unlockLogoGO.GetComponent<Image>();
            unlockLogoImage.sprite = PlaceholderSprite.Get(Color.clear);
            unlockLogoImage.preserveAspect = true;
            unlockLogoImage.raycastTarget = false;
            AnchorTopLeft((RectTransform)unlockLogoGO.transform, new Vector2(300f, 170f), new Vector2(100f, -50f));

            var unlockBannerGO = new GameObject("UnlockedBanner", typeof(RectTransform), typeof(Image));
            unlockBannerGO.transform.SetParent(unlockRoot.transform, false);
            var unlockBannerImage = unlockBannerGO.GetComponent<Image>();
            unlockBannerImage.sprite = PlaceholderSprite.Get(Color.clear);
            unlockBannerImage.preserveAspect = true;
            unlockBannerImage.raycastTarget = false;
            AnchorTopCenter((RectTransform)unlockBannerGO.transform, new Vector2(700f, 220f), new Vector2(0f, -60f));

            // Sized (and un-preserveAspect'd) to match ChooseCharacterScreen's own CardArt exactly
            // (BuildCharacterSelectCardPrefab: 340x360, stretched-to-fill rather than preserveAspect)
            // per feedback that this card should be "the same size as the swap character scene" —
            // was 850x850 with preserveAspect, which (depending on each character's selectCardArt
            // native aspect ratio) could read noticeably smaller than the Choose Character card it's
            // showing the exact same art as.
            var unlockCard = CreateImage("CharacterCard", unlockRoot.transform, new Color(1f, 0.84f, 0f), 340f, 360f);
            var unlockCardRect = (RectTransform)unlockCard.transform;
            unlockCardRect.anchorMin = unlockCardRect.anchorMax = new Vector2(0.5f, 0.5f);
            // CreateImage's width/height args only set a LayoutElement's preferredWidth/Height,
            // which a plain (non-LayoutGroup) parent like unlockRoot never reads — sizeDelta must be
            // set explicitly or the rect silently stays at Unity's default 100x100 regardless of
            // what was passed in. This was the real reason an earlier "match Choose Character's
            // card size" pass didn't actually change anything on screen.
            unlockCardRect.sizeDelta = new Vector2(340f, 360f);
            unlockCardRect.anchoredPosition = new Vector2(0f, -60f);
            unlockCard.preserveAspect = false;
            // Shouldn't swallow the tap before it reaches unlockTapButton on the root underneath —
            // same convention NewWorldUnlockScreen's worldBadge uses.
            unlockCard.raycastTarget = false;

            var unlockScreen = unlockRoot.AddComponent<NewCharacterUnlockScreen>();
            var unlockSO = new SerializedObject(unlockScreen);
            unlockSO.FindProperty("characterCardImage").objectReferenceValue = unlockCard;
            unlockSO.FindProperty("tapButton").objectReferenceValue = unlockTapButton;
            unlockSO.ApplyModifiedPropertiesWithoutUndo();
            unlockRoot.SetActive(false);

            // New World Unlock overlay — same "celebration layered on top of Level Complete"
            // convention as NewCharacterUnlockOverlay above, but for a world's badge
            // (LevelSelectController.worldSignSprites) instead of a character card, and tap-gated
            // rather than timer-dismissed (see NewWorldUnlockScreen's doc comment: a fixed-timer
            // auto-advance read as "nothing happened, it was very fast" in testing). The root panel
            // itself doubles as the tap target — CreatePanel already stretches it full-screen with
            // an Image, so adding a Button directly to it needs no separate invisible overlay
            // GameObject.
            var worldUnlockRoot = CreatePanel("NewWorldUnlockOverlay", root.transform, Color.black);
            var worldUnlockTapButton = worldUnlockRoot.AddComponent<Button>();
            worldUnlockTapButton.targetGraphic = worldUnlockRoot.GetComponent<Image>();

            // Just-unlocked world's own gameplay backdrop, faded — first child so everything else
            // (banner/badge/hint) draws on top of it. Sprite/alpha set at runtime by
            // NewWorldUnlockScreen.Show (per world, via TileMapRenderer.MazeArtSet.backdropSprite),
            // not wired here — starts fully transparent so the root's own solid black shows through
            // until Show() runs.
            var worldUnlockBackgroundGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            worldUnlockBackgroundGO.transform.SetParent(worldUnlockRoot.transform, false);
            StretchFull((RectTransform)worldUnlockBackgroundGO.transform);
            var worldUnlockBackgroundImage = worldUnlockBackgroundGO.GetComponent<Image>();
            worldUnlockBackgroundImage.sprite = PlaceholderSprite.Get(Color.white);
            worldUnlockBackgroundImage.color = new Color(0f, 0f, 0f, 0f);
            worldUnlockBackgroundImage.raycastTarget = false;

            // "World Unlocked" wood-sign banner, top-centre — same element/position convention as
            // NewCharacterUnlockOverlay's UnlockedBanner just above, but its own dedicated art
            // (WorldUnlocked.png) since "reused for all worlds" was the explicit ask, not a
            // per-world sprite swap like worldBadge below.
            var worldUnlockBannerGO = new GameObject("WorldUnlockedBanner", typeof(RectTransform), typeof(Image));
            worldUnlockBannerGO.transform.SetParent(worldUnlockRoot.transform, false);
            var worldUnlockBannerImage = worldUnlockBannerGO.GetComponent<Image>();
            worldUnlockBannerImage.sprite = PlaceholderSprite.Get(Color.clear);
            worldUnlockBannerImage.preserveAspect = true;
            worldUnlockBannerImage.raycastTarget = false;
            AnchorTopCenter((RectTransform)worldUnlockBannerGO.transform, new Vector2(900f, 260f), new Vector2(0f, -60f));

            var worldBadge = CreateImage("WorldBadge", worldUnlockRoot.transform, new Color(1f, 0.84f, 0f), 850f, 850f);
            var worldBadgeRect = (RectTransform)worldBadge.transform;
            worldBadgeRect.anchorMin = worldBadgeRect.anchorMax = new Vector2(0.5f, 0.6f);
            // CreateImage's width/height args only set a LayoutElement's preferredWidth/Height,
            // which worldUnlockRoot (a plain CreatePanel, no LayoutGroup) never reads — sizeDelta
            // must be set explicitly or the rect silently stays at Unity's default 100x100
            // regardless of what was passed in. This is why the badge rendered tiny even after its
            // burst-in/pulse animation "finished" — the animation itself was correct, it was just
            // animating up to a 100x100 target instead of the intended 850x850.
            worldBadgeRect.sizeDelta = new Vector2(850f, 850f);
            worldBadgeRect.anchoredPosition = Vector2.zero;
            worldBadge.preserveAspect = true;
            // Badge itself shouldn't swallow the tap before it reaches the root Button underneath.
            worldBadge.raycastTarget = false;

            var tapHintText = CreateText("TapHint", worldUnlockRoot.transform, "Tap to continue", 36f,
                TextAlignmentOptions.Center, 60f, Color.white);
            AnchorBottomCenter((RectTransform)tapHintText.transform, new Vector2(600f, 60f), new Vector2(0f, 150f));
            tapHintText.raycastTarget = false;

            var worldUnlockScreen = worldUnlockRoot.AddComponent<NewWorldUnlockScreen>();
            var worldUnlockSO = new SerializedObject(worldUnlockScreen);
            worldUnlockSO.FindProperty("worldBadgeImage").objectReferenceValue = worldBadge;
            worldUnlockSO.FindProperty("backgroundImage").objectReferenceValue = worldUnlockBackgroundImage;
            worldUnlockSO.FindProperty("tapButton").objectReferenceValue = worldUnlockTapButton;
            worldUnlockSO.FindProperty("tapHintText").objectReferenceValue = tapHintText;
            worldUnlockSO.ApplyModifiedPropertiesWithoutUndo();
            worldUnlockRoot.SetActive(false);

            var controller = root.AddComponent<LevelCompleteController>();
            var so = new SerializedObject(controller);
            so.FindProperty("starDisplay").objectReferenceValue = starDisplayGO.GetComponent<StarDisplay>();
            so.FindProperty("scoreText").objectReferenceValue = scoreText;
            so.FindProperty("playButton").objectReferenceValue = playButton;
            so.FindProperty("homeButton").objectReferenceValue = homeButton;
            so.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            so.FindProperty("unlockScreen").objectReferenceValue = unlockScreen;
            so.FindProperty("worldUnlockScreen").objectReferenceValue = worldUnlockScreen;
            so.ApplyModifiedPropertiesWithoutUndo();

            return (root, unlockScreen);
        }

        // ---- Level Failed -----------------------------------------------------------------------

        /// <summary>Rebuilt to a 2026-08-01 Canva mockup: Bg_LevelSelect.png (night farm) root
        /// background instead of LevelFailed.png stretched full-screen — that stretch used to
        /// non-uniformly distort the square "TRY AGAIN!" card art on a landscape aspect (the same
        /// square-art-on-landscape-overlay problem Pause/Level Complete already had fixed). PanelArt
        /// is now a child locked to a 1:1 aspect via AspectRatioFitter, carrying LevelFailed.png,
        /// with Restart.png/Quit.png positioned inside its own parchment area (LevelFailed.png has
        /// no baked-in button rows to align to, unlike Paused.png, so these fractions are a fresh
        /// centred vertical stack rather than measured off pre-existing art positions).</summary>
        private static GameObject BuildLevelFailed(Transform canvasTransform)
        {
            var root = CreatePanel("LevelFailedScreen", canvasTransform, Color.black);

            var panelArtGO = new GameObject("PanelArt", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            panelArtGO.transform.SetParent(root.transform, false);
            var panelArtRect = (RectTransform)panelArtGO.transform;
            panelArtRect.anchorMin = Vector2.zero;
            panelArtRect.anchorMax = Vector2.one;
            // Inset (was a bare 0/0 full-stretch) — the square card's top ribbon banner and bottom
            // rope corners were touching/crossing the yellow safe-area guide's top and bottom edges
            // on a real landscape aspect, since FitInParent at 100% of the screen height leaves no
            // margin at all in the dimension that's actually the constraint. 80px top/bottom, 40px
            // left/right shrinks the box AspectRatioFitter fits into, not just the visible art.
            panelArtRect.offsetMin = new Vector2(40f, 80f);
            panelArtRect.offsetMax = new Vector2(-40f, -80f);
            panelArtGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(Color.clear);
            var panelArtFitter = panelArtGO.GetComponent<AspectRatioFitter>();
            panelArtFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            panelArtFitter.aspectRatio = 1f;

            var restartButton = CreateButton("RestartButton", panelArtGO.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(restartButton.transform.Find("RestartButton_Label").gameObject);
            SetAnchorRect((RectTransform)restartButton.transform, 0.28f, 0.44f, 0.72f, 0.54f);

            var quitButton = CreateButton("QuitButton", panelArtGO.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(quitButton.transform.Find("QuitButton_Label").gameObject);
            SetAnchorRect((RectTransform)quitButton.transform, 0.28f, 0.28f, 0.72f, 0.38f);

            var controller = root.AddComponent<LevelFailedController>();
            var so = new SerializedObject(controller);
            so.FindProperty("restartButton").objectReferenceValue = restartButton;
            so.FindProperty("quitButton").objectReferenceValue = quitButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        /// <summary>Anchors a RectTransform to an exact fractional sub-rect of its parent (stretch
        /// to fill, no fixed offset) — used for overlaying button art onto specific positions baked
        /// into a background image, where the parent panel may itself be stretched to a different
        /// aspect ratio than the source art.</summary>
        private static void SetAnchorRect(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ---- Character Roster ---------------------------------------------------------------

        private static GameObject BuildCharacterRoster(Transform canvasTransform, GameObject rosterCardPrefab)
        {
            var root = CreatePanel("CharacterRosterScreen", canvasTransform, new Color(0.12f, 0.10f, 0.16f));

            var homeButton = CreateGenericBackButton(root.transform);

            var scrollRect = CreateHorizontalScrollView("CardScrollView", root.transform, out var content);
            var scrollRectTransform = (RectTransform)((Component)scrollRect).transform;
            scrollRectTransform.offsetMin = new Vector2(0f, 0f);
            scrollRectTransform.offsetMax = new Vector2(0f, -100f);

            var controller = root.AddComponent<CharacterRosterScreen>();
            var so = new SerializedObject(controller);
            so.FindProperty("cardContainer").objectReferenceValue = content;
            so.FindProperty("cardPrefab").objectReferenceValue = rosterCardPrefab;
            so.FindProperty("backButton").objectReferenceValue = homeButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ---- Leaderboards -----------------------------------------------------------------------

        private static GameObject BuildLeaderboards(Transform canvasTransform)
        {
            var root = CreatePanel("LeaderboardsScreen", canvasTransform, new Color(0.10f, 0.12f, 0.16f));
            var group = CreateVerticalGroup("Content", root.transform, 14f, 30);

            CreateText("Title", group.transform, "LEADERBOARDS", 40f, TextAlignmentOptions.Center, 60f);
            var statsText = CreateText("StatsText", group.transform, string.Empty, 24f, TextAlignmentOptions.Left, 200f);
            // Pulled out of "Content" so it lands in the generic bottom-left screen-corner spot
            // instead of stacking inline under the stats text.
            var backButton = CreateGenericBackButton(root.transform);

            var controller = root.AddComponent<LeaderboardsScreen>();
            var so = new SerializedObject(controller);
            so.FindProperty("statsText").objectReferenceValue = statsText;
            so.FindProperty("backButton").objectReferenceValue = backButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ---- Choose Character (Phase 5 replacement for the old OnGUI CharacterSwapUI) ---------

        /// <summary>One card's tappable area: an invisible root Image (raycast target only — see
        /// below), a child "CardArt" Image for the actual card art/placeholder, a lock-icon overlay,
        /// and an active-highlight glow behind the card art — all driven by
        /// CharacterSelectCard.Initialize.</summary>
        private static GameObject BuildCharacterSelectCardPrefab()
        {
            var go = new GameObject("CharacterSelectCard", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            // Explicit centre anchor/pivot — CardCarouselController positions instances via
            // anchoredPosition assuming (0,0) is the container's own centre (no LayoutGroup governs
            // this anymore, per the 2026-07-31 Canva mockup), so this can't be left at whatever a
            // freshly-created RectTransform defaults to. Same fix as WorldShield's own prefab.
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(340f, 360f);
            // The card's own Image used to hold the character art directly on this root
            // GameObject, with ActiveHighlight as a child "first sibling" intended to peek out
            // from behind it. That never actually worked: a child GameObject always renders in
            // front of its own parent's Image in uGUI, regardless of sibling index — sibling order
            // only reorders children relative to each other. So the (larger, 85%-opaque yellow)
            // highlight rendered ON TOP of the card art instead of behind it, blotting the active
            // character's card out entirely (the "yellow cover over Cluck" bug). Fixed by moving
            // the card art onto its own child ("CardArt"), leaving this root Image invisible and
            // used only as the Button's raycast target — now ActiveHighlight (added first) genuinely
            // sits behind CardArt (added after) in the same sibling list.
            var rootImage = go.GetComponent<Image>();
            rootImage.sprite = PlaceholderSprite.Get(Color.clear);
            rootImage.color = Color.clear;

            // ActiveHighlight (an 85%-opaque yellow square behind the centred card) removed per
            // feedback — it read as a distracting yellow background block behind the active
            // character rather than a subtle highlight. CharacterSelectCard.activeHighlight is left
            // null-safe (its SetActive call is already guarded), so no script change was needed.
            var cardArt = CreateImage("CardArt", go.transform, Color.white, 340f, 360f);
            var cardArtRect = (RectTransform)cardArt.transform;
            cardArtRect.anchorMin = cardArtRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardArtRect.pivot = new Vector2(0.5f, 0.5f);
            cardArtRect.sizeDelta = new Vector2(340f, 360f);
            cardArtRect.anchoredPosition = Vector2.zero;
            // Not preserveAspect — the per-character selectCardArt sprites have inconsistent native
            // dimensions (each is a hand-authored framed card image, not a shared template), so
            // preserving aspect within a fixed box made cards read as visibly different sizes
            // instead of a uniform deck. Stretching to fill guarantees every card is the same size;
            // the trade-off is a slight aspect distortion on any card whose source art isn't
            // already close to 340:360.
            cardArt.preserveAspect = false;

            var lockIcon = CreateImage("LockIcon", go.transform, new Color(0f, 0f, 0f, 0.8f), 140f, 60f);
            var lockRect = (RectTransform)lockIcon.transform;
            lockRect.anchorMin = lockRect.anchorMax = new Vector2(0.5f, 0.5f);
            lockRect.pivot = new Vector2(0.5f, 0.5f);
            lockRect.sizeDelta = new Vector2(140f, 60f);
            lockRect.anchoredPosition = Vector2.zero;
            var lockLabel = CreateText("LockLabel", lockIcon.transform, "LOCKED", 22f, TextAlignmentOptions.Center, 60f);
            StretchFull((RectTransform)lockLabel.transform);

            var button = go.GetComponent<Button>();
            button.targetGraphic = rootImage;

            var card = go.AddComponent<CharacterSelectCard>();
            var cardSO = new SerializedObject(card);
            cardSO.FindProperty("cardImage").objectReferenceValue = cardArt;
            cardSO.FindProperty("lockIcon").objectReferenceValue = lockIcon.gameObject;
            cardSO.FindProperty("button").objectReferenceValue = button;
            cardSO.ApplyModifiedPropertiesWithoutUndo();

            return SaveAndDestroy(go, $"{UIPrefabFolder}/CharacterSelectCard.prefab");
        }

        /// <summary>Built to a Canva mockup (2026-07-31): World1_Cornfield.png backdrop (same as
        /// Pause), Logo.png top-left, a round back button bottom-LEFT (CreateRoundBackButton's
        /// non-default corner — this mockup's icon isn't Btn_home.png like Settings/Level Select's,
        /// see ArtWiringBuilder.WireButtons for the substitution note), and a CardCarouselController
        /// (same component Level Select's world picker uses) instead of the old static GridLayoutGroup
        /// — one CharacterSelectCard per CharacterData, flick to cycle which is centred/full-scale,
        /// tap the centred card to swap into it. Not part of screenRoots — like Pause/Settings, it's
        /// an overlay shown/hidden directly rather than routed through SceneTransitionManager.ShowOnly.</summary>
        private static ChooseCharacterScreen BuildChooseCharacterScreen(Transform canvasTransform, GameObject cardPrefab)
        {
            var root = CreatePanel("ChooseCharacterScreen", canvasTransform, Color.black);

            var logoImageGO = new GameObject("LogoImage", typeof(RectTransform), typeof(Image));
            logoImageGO.transform.SetParent(root.transform, false);
            var logoImage = logoImageGO.GetComponent<Image>();
            logoImage.sprite = PlaceholderSprite.Get(Color.clear);
            logoImage.preserveAspect = true;
            // Inset further than the original mockup value (40 -> 100) — it was close enough to the
            // corner to read as clipped by the yellow safe-area guide, same fix as Settings/Pause.
            AnchorTopLeft((RectTransform)logoImageGO.transform, new Vector2(300f, 170f), new Vector2(100f, -50f));

            var backButton = CreateRoundBackButton(root.transform, bottomRight: false);

            // Carousel area — an invisible-but-raycastable Image covers the whole area (not just the
            // cards themselves) so a flick started on empty space between cards still registers as a
            // drag; CardCarouselController then positions/scales each card every frame instead of a
            // GridLayoutGroup arranging them in a static grid.
            var cardContainerGO = new GameObject("CardContainer", typeof(RectTransform), typeof(Image));
            var containerRect = (RectTransform)cardContainerGO.transform;
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.sizeDelta = new Vector2(1700f, 500f);
            containerRect.anchoredPosition = new Vector2(0f, -20f);
            cardContainerGO.transform.SetParent(root.transform, false);
            var containerImage = cardContainerGO.GetComponent<Image>();
            containerImage.sprite = PlaceholderSprite.Get(Color.clear);
            containerImage.color = Color.clear;
            containerImage.raycastTarget = true;
            var carousel = cardContainerGO.AddComponent<CardCarouselController>();
            // Default itemSpacing (380) left visibly large gaps between cards — tightened further
            // (300 -> 220) per feedback that cards still read as too far apart. arcRadius was
            // originally scaled down to 900 (from Level Select's 2800 default) so the circular
            // motion would read clearly at these smaller cards' size — but per later feedback this
            // dipped noticeably ("drafting down") rather than reading as side-to-side motion like
            // Level Select's own carousel. Matched to Level Select's 2800 instead: at itemSpacing
            // 220, dip for the nearest card drops from ~27px to ~9px (y = arcRadius*(1-cos(spacing/
            // radius))), while horizontal spread stays effectively unchanged (x ≈ itemSpacing for
            // small angles regardless of radius) — same side-to-side character, just flatter.
            var carouselSO = new SerializedObject(carousel);
            carouselSO.FindProperty("itemSpacing").floatValue = 220f;
            carouselSO.FindProperty("arcRadius").floatValue = 2800f;
            carouselSO.ApplyModifiedPropertiesWithoutUndo();

            var controller = root.AddComponent<ChooseCharacterScreen>();
            var so = new SerializedObject(controller);
            so.FindProperty("cardContainer").objectReferenceValue = cardContainerGO.transform;
            so.FindProperty("cardPrefab").objectReferenceValue = cardPrefab;
            so.FindProperty("carousel").objectReferenceValue = carousel;
            so.FindProperty("backButton").objectReferenceValue = backButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        // ---- Cross-references (built after every screen exists) -------------------------------

        private static void WireCrossReferences(GameObject mainMenu,
            GameObject gameplay, GameObject pause, GameObject settings,
            GameObject levelComplete, NewCharacterUnlockScreen unlockScreen, GameObject levelFailed,
            GameObject roster, GameObject leaderboards, ChooseCharacterScreen chooseCharacterScreen,
            ComboNotificationBanner comboBanner, GameObject levelSelect)
        {
            var settingsPanel = settings.GetComponent<SettingsPanel>();
            SetRefs(settingsPanel, ("mainMenuScreen", mainMenu));

            SetRefs(mainMenu.GetComponent<MainMenuController>(),
                ("levelSelectScreen", levelSelect), ("settingsPanel", settingsPanel));

            SetRefs(levelSelect.GetComponent<LevelSelectController>(),
                ("mainMenuScreen", mainMenu), ("gameplayScreen", gameplay));

            var hud = gameplay.GetComponent<GameplayHUD>();
            SetRefs(hud,
                ("pauseMenu", pause.GetComponent<PauseMenuController>()),
                ("levelCompleteScreen", levelComplete), ("levelFailedScreen", levelFailed));

            SetRefs(pause.GetComponent<PauseMenuController>(),
                ("chooseCharacterScreen", chooseCharacterScreen), ("settingsPanel", settingsPanel), ("levelSelectScreen", levelSelect));

            SetRefs(chooseCharacterScreen, ("pauseMenuScreen", pause));

            SetRefs(levelComplete.GetComponent<LevelCompleteController>(),
                ("levelSelectScreen", levelSelect), ("levelSelectController", levelSelect.GetComponent<LevelSelectController>()),
                ("settingsPanel", settingsPanel), ("unlockScreen", unlockScreen));

            SetRefs(levelFailed.GetComponent<LevelFailedController>(),
                ("gameplayScreen", gameplay), ("levelSelectScreen", levelSelect));

            SetRefs(roster.GetComponent<CharacterRosterScreen>(),
                ("mainMenuScreen", mainMenu));

            SetRefs(leaderboards.GetComponent<LeaderboardsScreen>(),
                ("mainMenuScreen", mainMenu));
        }

        /// <summary>Sets one or more [SerializeField] object references on a component by name in
        /// a single SerializedObject pass — every screen has 2-6 cross-references to other
        /// screens/controllers that can only be resolved once all screens exist, so this keeps
        /// WireCrossReferences from being 40 lines of repeated SerializedObject boilerplate.</summary>
        private static void SetRefs(Component target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs)
            {
                var prop = so.FindProperty(field);
                if (prop == null)
                {
                    Debug.LogWarning($"[Phase5ProjectBuilder] {target.GetType().Name} has no serialized field '{field}'.");
                    continue;
                }
                prop.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DisableRunOnStart(string gameObjectName)
        {
            var go = GameObject.Find(gameObjectName);
            if (go == null) return;
            var components = go.GetComponents<MonoBehaviour>();
            if (components.Length == 0) return;
            var so = new SerializedObject(components[0]);
            var prop = so.FindProperty("runOnStart");
            if (prop != null)
            {
                prop.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static GameObject SaveAndDestroy(GameObject go, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // See Phase4ProjectBuilder.EmbedRuntimePlaceholderSprites — PlaceholderSprite.Get()
            // sprites (used throughout UIBuilderHelpers for Image.sprite too) are runtime-only and
            // get silently nulled out by SaveAsPrefabAsset unless embedded as a real sub-asset
            // first. This builder's saved UI prefabs (RosterCard, CharacterSelectCard, LevelTile,
            // WorldDivider, WorldShield) use Image, not SpriteRenderer, so both are checked here.
            var placeholderSprites = new List<(string transformPath, Sprite sprite, bool isImage)>();
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.sprite != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sr.sprite)))
                {
                    placeholderSprites.Add((AnimationUtility.CalculateTransformPath(sr.transform, go.transform), sr.sprite, false));
                }
            }
            foreach (var img in go.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(img.sprite)))
                {
                    placeholderSprites.Add((AnimationUtility.CalculateTransformPath(img.transform, go.transform), img.sprite, true));
                }
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            if (placeholderSprites.Count > 0)
            {
                EmbedRuntimePlaceholderSprites(path, placeholderSprites);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return prefab;
        }

        private static void EmbedRuntimePlaceholderSprites(string prefabPath, List<(string transformPath, Sprite sprite, bool isImage)> placeholders)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            foreach (var (transformPath, sprite, isImage) in placeholders)
            {
                var target = string.IsNullOrEmpty(transformPath) ? contents.transform : contents.transform.Find(transformPath);
                if (target == null)
                {
                    continue;
                }

                if (sprite.texture != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sprite.texture)))
                {
                    AssetDatabase.AddObjectToAsset(sprite.texture, prefabPath);
                }
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sprite)))
                {
                    AssetDatabase.AddObjectToAsset(sprite, prefabPath);
                }

                if (isImage)
                {
                    var img = target.GetComponent<Image>();
                    if (img != null) img.sprite = sprite;
                }
                else
                {
                    var sr = target.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.sprite = sprite;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
        }
    }
}
