using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;
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

            // Still built (available for future multi-level content) even though WorldMapScreen no
            // longer wires a marker strip — see BuildWorldMap.
            BuildLevelMarkerPrefab();
            GameObject rosterCardPrefab = BuildRosterCardPrefab();

            var fadeGroup = BuildFadeOverlay(canvas.transform);

            var mainMenu = BuildMainMenu(canvas.transform);
            var worldMap = BuildWorldMap(canvas.transform);
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

            WireCrossReferences(mainMenu, worldMap, gameplay, pause, settings,
                levelComplete, unlockScreen, levelFailed, roster, leaderboards, chooseCharacter, comboBanner);

            var transitionManager = managersGO.GetComponent<SceneTransitionManager>();
            var transitionSO = new SerializedObject(transitionManager);
            transitionSO.FindProperty("fadeGroup").objectReferenceValue = fadeGroup;
            var screenRootsProp = transitionSO.FindProperty("screenRoots");
            var screens = new[] { mainMenu, worldMap, gameplay, levelComplete, levelFailed, roster, leaderboards };
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

        private static GameObject BuildLevelMarkerPrefab()
        {
            var go = new GameObject("LevelMarker", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(140f, 140f);
            var background = go.GetComponent<Image>();
            background.sprite = PlaceholderSprite.Get(new Color(0.4f, 0.4f, 0.4f));
            var button = go.GetComponent<Button>();
            button.targetGraphic = background;

            var numberText = CreateText("Number", go.transform, "1", 48f, TextAlignmentOptions.Center, 140f);
            StretchFull((RectTransform)numberText.transform);

            var lockIcon = CreateImage("LockIcon", go.transform, new Color(0.15f, 0.15f, 0.15f), 40f, 40f);
            var lockRect = (RectTransform)lockIcon.transform;
            lockRect.anchorMin = new Vector2(0.5f, 0.15f);
            lockRect.anchorMax = new Vector2(0.5f, 0.15f);

            var starDisplayGO = CreateStarDisplay("Stars", go.transform, 18);
            var starRect = (RectTransform)starDisplayGO.transform;
            starRect.anchorMin = new Vector2(0.5f, 0.08f);
            starRect.anchorMax = new Vector2(0.5f, 0.08f);
            starRect.sizeDelta = new Vector2(90f, 24f);

            var marker = go.AddComponent<LevelMarker>();
            var so = new SerializedObject(marker);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("background").objectReferenceValue = background;
            so.FindProperty("numberText").objectReferenceValue = numberText;
            so.FindProperty("lockIcon").objectReferenceValue = lockIcon.gameObject;
            so.FindProperty("starDisplay").objectReferenceValue = starDisplayGO.GetComponent<StarDisplay>();
            so.ApplyModifiedPropertiesWithoutUndo();

            return SaveAndDestroy(go, $"{UIPrefabFolder}/LevelMarker.prefab");
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

        // ---- World Map --------------------------------------------------------------------------

        private static GameObject BuildWorldMap(Transform canvasTransform)
        {
            var root = CreatePanel("WorldMapScreen", canvasTransform, new Color(0.10f, 0.16f, 0.10f));

            // Bottom-left Play / bottom-right Home — same 160x160 safe-area-inset icon-button
            // convention as Main Menu's Play/Settings and Gameplay HUD's PauseButton. Replaces an
            // earlier top-left HomeButton + horizontally-scrolling level-marker strip: with only a
            // couple of LevelData assets authored so far, that strip rendered as an unstyled green
            // swatch overlapping Map.png's own baked-in "THE FARM" title (see the World Map "known
            // gap" note in CLAUDE.md — markers were never aligned to the background art's path
            // either). Play jumps straight into whichever level the player would naturally
            // continue on, the same target CenterOnLevel used to just scroll to.
            const float navButtonSize = 160f;
            const float navInsetX = 100f;
            const float navInsetY = 70f;

            var playButton = CreateButton("PlayButton", root.transform, string.Empty, new Color(0.3f, 0.75f, 0.35f), 28f, navButtonSize, out _);
            Object.DestroyImmediate(playButton.transform.Find("PlayButton_Label").gameObject);
            AnchorBottomLeft((RectTransform)playButton.transform, new Vector2(navButtonSize, navButtonSize), new Vector2(navInsetX, navInsetY));

            var homeButton = CreateButton("HomeButton", root.transform, string.Empty, new Color(0.35f, 0.35f, 0.38f), 28f, navButtonSize, out _);
            Object.DestroyImmediate(homeButton.transform.Find("HomeButton_Label").gameObject);
            AnchorBottomRight((RectTransform)homeButton.transform, new Vector2(navButtonSize, navButtonSize), new Vector2(navInsetX, navInsetY));

            var controller = root.AddComponent<WorldMapController>();
            var so = new SerializedObject(controller);
            so.FindProperty("playButton").objectReferenceValue = playButton;
            so.FindProperty("homeButton").objectReferenceValue = homeButton;
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
            var scoreText = CreateText("ScoreText", root.transform, "0", 56f, TextAlignmentOptions.TopLeft, 70f);
            AnchorTopLeft((RectTransform)scoreText.transform, new Vector2(320f, 70f), new Vector2(140f, -140f));

            var timerText = CreateText("TimerText", root.transform, "00:00", 46f, TextAlignmentOptions.TopRight, 70f);
            AnchorTopRight((RectTransform)timerText.transform, new Vector2(240f, 70f), new Vector2(-140f, -140f));

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

            // Pause icon (bottom-left) — Sound and Home were removed from this cluster per
            // playtest feedback; both are still reachable via the Pause menu itself (Settings'
            // music/SFX toggles, Pause's own Quit button), so a single Pause button is all this
            // needs now. Sized/inset to match the Main Menu's Play/Settings buttons (160x160,
            // safe-area inset) — an original 80x80 at a 20px inset both read as too small and sat
            // outside the safe-area guide.
            const float clusterButtonSize = 160f;
            const float clusterSpacing = 24f;
            const float clusterInsetX = 100f;
            const float clusterInsetY = 70f;

            var pauseButton = CreateButton("PauseButton", root.transform, string.Empty, new Color(0.35f, 0.35f, 0.38f), 28f, clusterButtonSize, out _);
            Object.DestroyImmediate(pauseButton.transform.Find("PauseButton_Label").gameObject);
            AnchorBottomLeft((RectTransform)pauseButton.transform, new Vector2(clusterButtonSize, clusterButtonSize), new Vector2(clusterInsetX, clusterInsetY));

            // Character portrait — above the button, left-aligned with it.
            var portrait = CreateImage("CharacterPortrait", root.transform, new Color(1f, 0.84f, 0f), 90f, 90f);
            AnchorBottomLeft((RectTransform)portrait.transform, new Vector2(90f, 90f),
                new Vector2(clusterInsetX, clusterInsetY + clusterButtonSize + clusterSpacing));

            // Directional pad (right side, diamond/D-pad layout) — up.png/down.png/left.png/
            // right.png (wired by ArtWiringBuilder) already look like complete rounded buttons on
            // their own, so each is just a plain Image+Button, no separate background needed.
            // Positioned around a shared centre point rather than each anchored independently, so
            // the diamond shape (Up above centre, Down below, Left/Right to the sides) is easy to
            // read and re-tune as one unit.
            const float dpadButtonSize = 120f;
            const float dpadSpacing = 130f;
            const float dpadInsetX = 200f;
            const float dpadInsetY = 220f;
            Vector2 dpadCenter = new Vector2(-dpadInsetX, dpadInsetY);

            var upButton = CreateButton("DPadUpButton", root.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(upButton.transform.Find("DPadUpButton_Label").gameObject);
            AnchorBottomRight((RectTransform)upButton.transform, new Vector2(dpadButtonSize, dpadButtonSize),
                dpadCenter + new Vector2(0f, dpadSpacing));

            var downButton = CreateButton("DPadDownButton", root.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(downButton.transform.Find("DPadDownButton_Label").gameObject);
            AnchorBottomRight((RectTransform)downButton.transform, new Vector2(dpadButtonSize, dpadButtonSize),
                dpadCenter + new Vector2(0f, -dpadSpacing));

            var leftButton = CreateButton("DPadLeftButton", root.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(leftButton.transform.Find("DPadLeftButton_Label").gameObject);
            AnchorBottomRight((RectTransform)leftButton.transform, new Vector2(dpadButtonSize, dpadButtonSize),
                dpadCenter + new Vector2(-dpadSpacing, 0f));

            var rightButton = CreateButton("DPadRightButton", root.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(rightButton.transform.Find("DPadRightButton_Label").gameObject);
            AnchorBottomRight((RectTransform)rightButton.transform, new Vector2(dpadButtonSize, dpadButtonSize),
                dpadCenter + new Vector2(dpadSpacing, 0f));

            var dpad = root.AddComponent<DirectionalPadController>();
            var dpadSO = new SerializedObject(dpad);
            dpadSO.FindProperty("upButton").objectReferenceValue = upButton;
            dpadSO.FindProperty("downButton").objectReferenceValue = downButton;
            dpadSO.FindProperty("leftButton").objectReferenceValue = leftButton;
            dpadSO.FindProperty("rightButton").objectReferenceValue = rightButton;
            dpadSO.ApplyModifiedPropertiesWithoutUndo();

            var hud = root.AddComponent<GameplayHUD>();
            var so = new SerializedObject(hud);
            so.FindProperty("scoreText").objectReferenceValue = scoreText;
            so.FindProperty("timerText").objectReferenceValue = timerText;
            so.FindProperty("characterPortrait").objectReferenceValue = portrait;
            so.FindProperty("pauseButton").objectReferenceValue = pauseButton;
            so.FindProperty("powerPelletTimerBar").objectReferenceValue = powerBarGO;
            so.FindProperty("powerPelletTimerFill").objectReferenceValue = powerFillImage;
            so.FindProperty("chainCounterRoot").objectReferenceValue = chainRoot;
            so.FindProperty("chainCounterText").objectReferenceValue = chainText;
            so.ApplyModifiedPropertiesWithoutUndo();

            return (root, banner);
        }

        // ---- Pause Menu -------------------------------------------------------------------------

        /// <summary>Paused.png bakes in the "PAUSED" title and all 5 button-row backgrounds at
        /// fixed positions — Resume/SwapCharacter/Restart/Settings/Quit.png (real button art wired
        /// by ArtWiringBuilder) sit exactly on top of those five baked-in rows instead of generic
        /// CreateButton rectangles + a redundant "PAUSED" text. Anchor fractions below were
        /// measured directly off Paused.png (a 2048x2048 square image, stretched to fill this
        /// full-screen overlay) — same reasoning as BuildLevelFailed's SetAnchorRect comment.</summary>
        private static GameObject BuildPauseMenu(Transform canvasTransform)
        {
            var root = CreatePanel("PauseOverlay", canvasTransform, new Color(0f, 0f, 0f, 0.75f));

            // Paused.png is a SQUARE (2048x2048) parchment/frame card with its 5 button rows baked
            // into the art. Its old wiring set it directly as the root panel's own Image — the root
            // stretches full-screen (StretchFull), so on a real landscape device aspect the square
            // art got non-uniformly stretched, squashing the baked-in button rows together and
            // making the separately-wired button art (Resume.png etc, positioned by the fractions
            // below) drift out of alignment with them / off the visible card entirely. "PanelArt" is
            // a child that stays centred and square via AspectRatioFitter (FitInParent), so it never
            // exceeds the overlay's bounds regardless of device aspect; the root's own Image stays
            // the plain black dim behind it (matching every other overlay's "dim gameplay, don't
            // replace it" behaviour). The 5 buttons move under PanelArt so their fractions — tuned
            // against the art's own baked button positions — line up with it at any aspect.
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

            var resumeButton = CreateButton("ResumeButton", panelArtGO.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(resumeButton.transform.Find("ResumeButton_Label").gameObject);
            SetAnchorRect((RectTransform)resumeButton.transform, 0.325f, 0.6f, 0.675f, 0.6825f);

            var swapButton = CreateButton("SwapButton", panelArtGO.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(swapButton.transform.Find("SwapButton_Label").gameObject);
            SetAnchorRect((RectTransform)swapButton.transform, 0.2625f, 0.495f, 0.735f, 0.5775f);

            var restartButton = CreateButton("RestartButton", panelArtGO.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(restartButton.transform.Find("RestartButton_Label").gameObject);
            SetAnchorRect((RectTransform)restartButton.transform, 0.325f, 0.3925f, 0.675f, 0.4725f);

            var settingsButton = CreateButton("SettingsButton", panelArtGO.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(settingsButton.transform.Find("SettingsButton_Label").gameObject);
            SetAnchorRect((RectTransform)settingsButton.transform, 0.325f, 0.29f, 0.675f, 0.37f);

            var quitButton = CreateButton("QuitButton", panelArtGO.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(quitButton.transform.Find("QuitButton_Label").gameObject);
            SetAnchorRect((RectTransform)quitButton.transform, 0.3675f, 0.195f, 0.635f, 0.27f);

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

        /// <summary>Root background is "LoadingScreen Background.png" (a night barn/starfield
        /// scene, wired by ArtWiringBuilder), shown behind everything whenever the Settings
        /// (gear) button is pressed from Main Menu or Pause. Each control row sits on its own
        /// Btn_plaque.png background (wired by ArtWiringBuilder via WrapInPlaqueRow's predictable
        /// naming) — an earlier version stretched one single Btn_plaque.png (a small wide pill
        /// shape) behind the *entire* control stack at once, which distorted it into an unreadable
        /// blob and left every toggle/slider/dropdown floating over it with no framing of its own.</summary>
        private static GameObject BuildSettingsPanel(Transform canvasTransform)
        {
            var root = CreatePanel("SettingsOverlay", canvasTransform, new Color(0f, 0f, 0f, 0.8f));

            var group = CreateVerticalGroup("Content", root.transform, 14f, 30);
            ((RectTransform)group.transform).sizeDelta = new Vector2(640f, 0f);

            CreateText("Title", group.transform, "SETTINGS", 44f, TextAlignmentOptions.Center, 60f);

            // Back button lives outside "Content" (screen-corner anchored, same convention as
            // WorldMap's Home / CharacterRosterScreen's Back) rather than the old inline "X" —
            // wired to the same closeButton field/Hide() behaviour, just reskinned with Btn_back.png
            // and moved somewhere a player expects a back button to be.
            var closeButton = CreateButton("BackButton", root.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(closeButton.transform.Find("BackButton_Label").gameObject);
            AnchorTopLeft((RectTransform)closeButton.transform, new Vector2(120f, 120f), new Vector2(20f, -20f));

            const float rowHeight = 68f;

            var musicToggle = CreateToggle("MusicToggle", group.transform, "Music", out _);
            var musicSlider = CreateSlider("MusicVolumeSlider", group.transform, 1f);
            WrapInPlaqueRow(CombineRow("MusicRow", group.transform, musicToggle.transform.parent.gameObject, musicSlider.gameObject), rowHeight);

            var sfxToggle = CreateToggle("SfxToggle", group.transform, "SFX", out _);
            var sfxSlider = CreateSlider("SfxVolumeSlider", group.transform, 1f);
            WrapInPlaqueRow(CombineRow("SfxRow", group.transform, sfxToggle.transform.parent.gameObject, sfxSlider.gameObject), rowHeight);

            var vibrationToggle = CreateToggle("VibrationToggle", group.transform, "Vibration", out _);
            WrapInPlaqueRow(vibrationToggle.transform.parent.gameObject, rowHeight);

            var leftHandedToggle = CreateToggle("LeftHandedToggle", group.transform, "Left-Handed", out _);
            WrapInPlaqueRow(leftHandedToggle.transform.parent.gameObject, rowHeight);

            var languageDropdownGO = new GameObject("LanguageDropdown", typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
            languageDropdownGO.transform.SetParent(group.transform, false);
            languageDropdownGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(Color.clear);
            var languageDropdown = languageDropdownGO.GetComponent<TMP_Dropdown>();
            languageDropdown.options.Clear();
            languageDropdown.options.Add(new TMP_Dropdown.OptionData("English"));
            var langLabel = CreateText("Label", languageDropdownGO.transform, "English", 24f, TextAlignmentOptions.Left, 44f);
            StretchFull((RectTransform)langLabel.transform);
            languageDropdown.captionText = langLabel;
            WrapInPlaqueRow(languageDropdownGO, rowHeight);

            var restoreButton = CreateButton("RestoreProgressButton", group.transform, "Restore Progress (Phase 6)", new Color(0.35f, 0.35f, 0.38f), 22f, rowHeight, out _);
            var resetButton = CreateButton("ResetProgressButton", group.transform, "Reset Progress", new Color(0.75f, 0.25f, 0.25f), 24f, rowHeight, out _);

            var versionText = CreateText("VersionText", group.transform, "v0.1", 16f, TextAlignmentOptions.Center, 24f);

            // Reset confirmation sub-panel (overlay on top of Settings itself)
            var confirmPanel = CreatePanel("ResetConfirmPanel", root.transform, new Color(0f, 0f, 0f, 0.9f));
            var confirmGroup = CreateVerticalGroup("ConfirmContent", confirmPanel.transform, 14f, 20);
            CreateText("ConfirmLabel", confirmGroup.transform, "Reset all progress? This cannot be undone.", 24f, TextAlignmentOptions.Center, 60f);
            var confirmRow = CreateHorizontalGroup("ConfirmRow", confirmGroup.transform, 14f);
            var confirmButton = CreateButton("ConfirmButton", confirmRow.transform, "Reset", new Color(0.75f, 0.25f, 0.25f), out _);
            var cancelButton = CreateButton("CancelButton", confirmRow.transform, "Cancel", new Color(0.35f, 0.35f, 0.38f), out _);
            confirmPanel.SetActive(false);

            var controller = root.AddComponent<SettingsPanel>();
            var so = new SerializedObject(controller);
            so.FindProperty("musicToggle").objectReferenceValue = musicToggle;
            so.FindProperty("sfxToggle").objectReferenceValue = sfxToggle;
            so.FindProperty("musicVolumeSlider").objectReferenceValue = musicSlider;
            so.FindProperty("sfxVolumeSlider").objectReferenceValue = sfxSlider;
            so.FindProperty("vibrationToggle").objectReferenceValue = vibrationToggle;
            so.FindProperty("languageDropdown").objectReferenceValue = languageDropdown;
            so.FindProperty("leftHandedToggle").objectReferenceValue = leftHandedToggle;
            so.FindProperty("restoreProgressButton").objectReferenceValue = restoreButton;
            so.FindProperty("resetProgressButton").objectReferenceValue = resetButton;
            so.FindProperty("resetConfirmPanel").objectReferenceValue = confirmPanel;
            so.FindProperty("confirmResetButton").objectReferenceValue = confirmButton;
            so.FindProperty("cancelResetButton").objectReferenceValue = cancelButton;
            so.FindProperty("versionText").objectReferenceValue = versionText;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
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

        private static (GameObject root, NewCharacterUnlockScreen unlockScreen) BuildLevelComplete(Transform canvasTransform)
        {
            var root = CreatePanel("LevelCompleteScreen", canvasTransform, new Color(0.10f, 0.16f, 0.12f));
            var group = CreateVerticalGroup("Content", root.transform, 8f, 30);

            CreateText("Title", group.transform, "LEVEL COMPLETE!", 48f, TextAlignmentOptions.Center, 70f);
            var starDisplayGO = CreateStarDisplay("Stars", group.transform, 50);
            var scoreText = CreateText("ScoreText", group.transform, "0", 44f, TextAlignmentOptions.Center, 60f);

            var cropText = CreateText("CropBreakdown", group.transform, "Crops: 0", 20f, TextAlignmentOptions.Center, 28f);
            var robotText = CreateText("RobotBreakdown", group.transform, "Robots: 0", 20f, TextAlignmentOptions.Center, 28f);
            var timeText = CreateText("TimeBonus", group.transform, "Time Bonus: 0", 20f, TextAlignmentOptions.Center, 28f);
            var perfectText = CreateText("PerfectBonus", group.transform, "Perfect Run: --", 20f, TextAlignmentOptions.Center, 28f);
            var coinsText = CreateText("CoinsEarned", group.transform, "+0 coins", 24f, TextAlignmentOptions.Center, 34f);
            var comboText = CreateText("ComboAchievements", group.transform, string.Empty, 18f, TextAlignmentOptions.Center, 40f);

            var newBestBadge = CreateText("NewBestBadge", group.transform, "NEW BEST!", 22f, TextAlignmentOptions.Center, 30f, new Color(1f, 0.84f, 0f));
            newBestBadge.gameObject.SetActive(false);

            var buttonRow = CreateHorizontalGroup("Buttons", group.transform, 12f);
            var replayButton = CreateButton("ReplayButton", buttonRow.transform, "Replay", new Color(0.35f, 0.35f, 0.38f), out _);
            var nextButton = CreateButton("NextLevelButton", buttonRow.transform, "Next Level", new Color(0.3f, 0.75f, 0.35f), out _);
            var homeButton = CreateButton("HomeButton", buttonRow.transform, "Home", new Color(0.35f, 0.45f, 0.75f), out _);

            // New Character Unlock overlay, layered on top of Level Complete
            var unlockRoot = CreatePanel("NewCharacterUnlockOverlay", root.transform, new Color(0.05f, 0.05f, 0.02f, 0.95f));
            var unlockGroup = CreateVerticalGroup("UnlockContent", unlockRoot.transform, 10f, 30);
            var particles = CreateImage("GoldenParticles", unlockRoot.transform, new Color(1f, 0.84f, 0f, 0.3f), 400f, 400f);
            var particlesRect = (RectTransform)particles.transform;
            particlesRect.anchorMin = particlesRect.anchorMax = new Vector2(0.5f, 0.5f);
            var bannerText = CreateText("Banner", unlockGroup.transform, "NEW SQUAD MEMBER!", 30f, TextAlignmentOptions.Center, 44f, new Color(1f, 0.84f, 0f));
            var unlockCard = CreateImage("CharacterCard", unlockGroup.transform, new Color(1f, 0.84f, 0f), 160f, 160f);
            var unlockTitle = CreateText("UnlockTitle", unlockGroup.transform, string.Empty, 34f, TextAlignmentOptions.Center, 50f);
            var unlockStats = CreateText("UnlockStats", unlockGroup.transform, string.Empty, 20f, TextAlignmentOptions.Center, 120f);
            var continueButton = CreateButton("ContinueButton", unlockGroup.transform, "Continue", new Color(0.3f, 0.75f, 0.35f), out _);

            var unlockScreen = unlockRoot.AddComponent<NewCharacterUnlockScreen>();
            var unlockSO = new SerializedObject(unlockScreen);
            unlockSO.FindProperty("bannerText").objectReferenceValue = bannerText;
            unlockSO.FindProperty("titleText").objectReferenceValue = unlockTitle;
            unlockSO.FindProperty("characterCardImage").objectReferenceValue = unlockCard;
            unlockSO.FindProperty("statsText").objectReferenceValue = unlockStats;
            unlockSO.FindProperty("continueButton").objectReferenceValue = continueButton;
            unlockSO.FindProperty("goldenParticlesPlaceholder").objectReferenceValue = particles;
            unlockSO.ApplyModifiedPropertiesWithoutUndo();
            unlockRoot.SetActive(false);

            var controller = root.AddComponent<LevelCompleteController>();
            var so = new SerializedObject(controller);
            so.FindProperty("starDisplay").objectReferenceValue = starDisplayGO.GetComponent<StarDisplay>();
            so.FindProperty("scoreText").objectReferenceValue = scoreText;
            so.FindProperty("cropBreakdownText").objectReferenceValue = cropText;
            so.FindProperty("robotBreakdownText").objectReferenceValue = robotText;
            so.FindProperty("timeBonusText").objectReferenceValue = timeText;
            so.FindProperty("perfectBonusText").objectReferenceValue = perfectText;
            so.FindProperty("coinsEarnedText").objectReferenceValue = coinsText;
            so.FindProperty("newBestBadge").objectReferenceValue = newBestBadge.gameObject;
            so.FindProperty("comboAchievementsText").objectReferenceValue = comboText;
            so.FindProperty("replayButton").objectReferenceValue = replayButton;
            so.FindProperty("nextLevelButton").objectReferenceValue = nextButton;
            so.FindProperty("homeButton").objectReferenceValue = homeButton;
            so.FindProperty("unlockScreen").objectReferenceValue = unlockScreen;
            so.ApplyModifiedPropertiesWithoutUndo();

            return (root, unlockScreen);
        }

        // ---- Level Failed -----------------------------------------------------------------------

        /// <summary>LevelFailed.png bakes in the "TRY AGAIN!" banner, "SCORE"/"BEST" labels, and
        /// its own RETRY/MENU button graphics at fixed positions — Retry.png/Menu.png (real button
        /// art wired by ArtWiringBuilder) sit exactly on top of those two baked-in positions
        /// instead of generic CreateButton rectangles, and there's no dynamic Title/Score/Tip text
        /// anymore since the art already reads as a complete screen on its own. Anchor fractions
        /// below were measured directly off LevelFailed.png (a 2048x2048 square image, stretched
        /// to fill this full-screen panel) — anchoring as a fractional sub-rect (rather than a
        /// fixed pixel size/position) keeps the buttons aligned with the art regardless of how
        /// non-uniformly that stretch scales the square source into the canvas's own aspect.</summary>
        private static GameObject BuildLevelFailed(Transform canvasTransform)
        {
            var root = CreatePanel("LevelFailedScreen", canvasTransform, new Color(0.16f, 0.12f, 0.10f));

            var retryButton = CreateButton("RetryButton", root.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(retryButton.transform.Find("RetryButton_Label").gameObject);
            SetAnchorRect((RectTransform)retryButton.transform, 0.215f, 0.27f, 0.4275f, 0.355f);

            var menuButton = CreateButton("MenuButton", root.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(menuButton.transform.Find("MenuButton_Label").gameObject);
            SetAnchorRect((RectTransform)menuButton.transform, 0.5325f, 0.27f, 0.7475f, 0.355f);

            var controller = root.AddComponent<LevelFailedController>();
            var so = new SerializedObject(controller);
            so.FindProperty("retryButton").objectReferenceValue = retryButton;
            so.FindProperty("menuButton").objectReferenceValue = menuButton;
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

            var homeButton = CreateButton("BackButton", root.transform, "Back", new Color(0.35f, 0.35f, 0.38f), 22f, 50f, out _);
            AnchorTopLeft((RectTransform)homeButton.transform, new Vector2(160f, 50f), new Vector2(20f, -20f));

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
            var backButton = CreateButton("BackButton", group.transform, "Back", new Color(0.35f, 0.35f, 0.38f), out _);

            var controller = root.AddComponent<LeaderboardsScreen>();
            var so = new SerializedObject(controller);
            so.FindProperty("statsText").objectReferenceValue = statsText;
            so.FindProperty("backButton").objectReferenceValue = backButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ---- Choose Character (Phase 5 replacement for the old OnGUI CharacterSwapUI) ---------

        /// <summary>One card's tappable area: an Image for the card art/placeholder, a lock-icon
        /// overlay, and an active-highlight glow (a slightly larger Image behind the card, first
        /// sibling so it peeks out around the edges rather than covering the art) — all driven by
        /// CharacterSelectCard.Initialize.</summary>
        private static GameObject BuildCharacterSelectCardPrefab()
        {
            var go = new GameObject("CharacterSelectCard", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(380f, 380f);
            go.GetComponent<Image>().sprite = PlaceholderSprite.Get(Color.white);

            var activeHighlight = CreateImage("ActiveHighlight", go.transform, new Color(1f, 0.84f, 0f, 0.85f), 410f, 410f);
            var highlightRect = (RectTransform)activeHighlight.transform;
            highlightRect.anchorMin = highlightRect.anchorMax = new Vector2(0.5f, 0.5f);
            highlightRect.pivot = new Vector2(0.5f, 0.5f);
            highlightRect.sizeDelta = new Vector2(410f, 410f);
            highlightRect.anchoredPosition = Vector2.zero;
            activeHighlight.transform.SetAsFirstSibling();

            var lockIcon = CreateImage("LockIcon", go.transform, new Color(0f, 0f, 0f, 0.8f), 140f, 60f);
            var lockRect = (RectTransform)lockIcon.transform;
            lockRect.anchorMin = lockRect.anchorMax = new Vector2(0.5f, 0.5f);
            lockRect.pivot = new Vector2(0.5f, 0.5f);
            lockRect.sizeDelta = new Vector2(140f, 60f);
            lockRect.anchoredPosition = Vector2.zero;
            var lockLabel = CreateText("LockLabel", lockIcon.transform, "LOCKED", 22f, TextAlignmentOptions.Center, 60f);
            StretchFull((RectTransform)lockLabel.transform);

            var card = go.AddComponent<CharacterSelectCard>();
            var cardSO = new SerializedObject(card);
            cardSO.FindProperty("cardImage").objectReferenceValue = go.GetComponent<Image>();
            cardSO.FindProperty("lockIcon").objectReferenceValue = lockIcon.gameObject;
            cardSO.FindProperty("activeHighlight").objectReferenceValue = activeHighlight.gameObject;
            cardSO.FindProperty("button").objectReferenceValue = go.GetComponent<Button>();
            cardSO.ApplyModifiedPropertiesWithoutUndo();

            return SaveAndDestroy(go, $"{UIPrefabFolder}/CharacterSelectCard.prefab");
        }

        /// <summary>LoadingScreen Background.png (the same barn/night art used behind Settings)
        /// fills the screen; a GridLayoutGroup lays out one CharacterSelectCard per CharacterData,
        /// evenly spaced in a fixed 4-column grid regardless of unlock state. Not part of
        /// screenRoots — like Pause/Settings, it's an overlay shown/hidden directly rather than
        /// routed through SceneTransitionManager.ShowOnly.</summary>
        private static ChooseCharacterScreen BuildChooseCharacterScreen(Transform canvasTransform, GameObject cardPrefab)
        {
            var root = CreatePanel("ChooseCharacterScreen", canvasTransform, new Color(0.08f, 0.08f, 0.12f));

            var backButton = CreateButton("BackButton", root.transform, "Back", new Color(0.35f, 0.35f, 0.38f), 22f, 50f, out _);
            AnchorTopLeft((RectTransform)backButton.transform, new Vector2(160f, 50f), new Vector2(20f, -20f));

            var gridGO = new GameObject("CardGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGO.transform.SetParent(root.transform, false);
            var gridRect = (RectTransform)gridGO.transform;
            gridRect.anchorMin = new Vector2(0.5f, 0.5f);
            gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            gridRect.sizeDelta = new Vector2(1700f, 820f);
            gridRect.anchoredPosition = new Vector2(0f, -20f);

            var grid = gridGO.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(380f, 380f);
            grid.spacing = new Vector2(30f, 30f);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            var controller = root.AddComponent<ChooseCharacterScreen>();
            var so = new SerializedObject(controller);
            so.FindProperty("cardContainer").objectReferenceValue = gridGO.transform;
            so.FindProperty("cardPrefab").objectReferenceValue = cardPrefab;
            so.FindProperty("gridLayoutGroup").objectReferenceValue = grid;
            so.FindProperty("backButton").objectReferenceValue = backButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        // ---- Cross-references (built after every screen exists) -------------------------------

        private static void WireCrossReferences(GameObject mainMenu, GameObject worldMap,
            GameObject gameplay, GameObject pause, GameObject settings,
            GameObject levelComplete, NewCharacterUnlockScreen unlockScreen, GameObject levelFailed,
            GameObject roster, GameObject leaderboards, ChooseCharacterScreen chooseCharacterScreen, ComboNotificationBanner comboBanner)
        {
            var settingsPanel = settings.GetComponent<SettingsPanel>();

            SetRefs(mainMenu.GetComponent<MainMenuController>(),
                ("worldMapScreen", worldMap), ("settingsPanel", settingsPanel));

            SetRefs(worldMap.GetComponent<WorldMapController>(),
                ("mainMenuScreen", mainMenu), ("gameplayScreen", gameplay));

            var hud = gameplay.GetComponent<GameplayHUD>();
            SetRefs(hud,
                ("pauseMenu", pause.GetComponent<PauseMenuController>()),
                ("levelCompleteScreen", levelComplete), ("levelFailedScreen", levelFailed));

            SetRefs(pause.GetComponent<PauseMenuController>(),
                ("chooseCharacterScreen", chooseCharacterScreen), ("settingsPanel", settingsPanel), ("mainMenuScreen", mainMenu));

            SetRefs(chooseCharacterScreen, ("pauseMenuScreen", pause));

            SetRefs(levelComplete.GetComponent<LevelCompleteController>(),
                ("worldMapScreen", worldMap), ("gameplayScreen", gameplay), ("unlockScreen", unlockScreen));

            SetRefs(levelFailed.GetComponent<LevelFailedController>(),
                ("gameplayScreen", gameplay), ("mainMenuScreen", mainMenu));

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
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
