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

            AddManagers(managersGO);

            GameObject levelMarkerPrefab = BuildLevelMarkerPrefab();
            GameObject rosterCardPrefab = BuildRosterCardPrefab();

            var fadeGroup = BuildFadeOverlay(canvas.transform);

            var mainMenu = BuildMainMenu(canvas.transform);
            var worldMap = BuildWorldMap(canvas.transform, levelMarkerPrefab);
            var matchup = BuildMatchup(canvas.transform);
            var (gameplay, comboBanner) = BuildGameplayHUD(canvas.transform);
            var pause = BuildPauseMenu(canvas.transform);
            var settings = BuildSettingsPanel(canvas.transform);
            var storeComingSoon = BuildStoreComingSoonPanel(canvas.transform);
            var (levelComplete, unlockScreen) = BuildLevelComplete(canvas.transform);
            var levelFailed = BuildLevelFailed(canvas.transform);
            var roster = BuildCharacterRoster(canvas.transform, rosterCardPrefab);
            var leaderboards = BuildLeaderboards(canvas.transform);

            var characterSwapUIGO = GameObject.Find("CharacterSwapUI");
            var characterSwapUI = characterSwapUIGO != null ? characterSwapUIGO.GetComponent<CharacterSwapUI>() : null;

            WireCrossReferences(mainMenu, worldMap, matchup, gameplay, pause, settings,
                levelComplete, unlockScreen, levelFailed, roster, leaderboards, characterSwapUI, comboBanner);

            var transitionManager = managersGO.GetComponent<SceneTransitionManager>();
            var transitionSO = new SerializedObject(transitionManager);
            transitionSO.FindProperty("fadeGroup").objectReferenceValue = fadeGroup;
            var screenRootsProp = transitionSO.FindProperty("screenRoots");
            var screens = new[] { mainMenu, worldMap, matchup.gameObject, gameplay, levelComplete, levelFailed, roster, leaderboards };
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

        private static void AddManagers(GameObject managersGO)
        {
            if (managersGO.GetComponent<SceneTransitionManager>() == null) managersGO.AddComponent<SceneTransitionManager>();
            if (managersGO.GetComponent<AudioManager>() == null) managersGO.AddComponent<AudioManager>();
            if (managersGO.GetComponent<DailyChallengeManager>() == null) managersGO.AddComponent<DailyChallengeManager>();
            if (managersGO.GetComponent<LeaderboardManager>() == null) managersGO.AddComponent<LeaderboardManager>();
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

        private static GameObject BuildWorldMap(Transform canvasTransform, GameObject levelMarkerPrefab)
        {
            var root = CreatePanel("WorldMapScreen", canvasTransform, new Color(0.10f, 0.16f, 0.10f));

            var homeButton = CreateButton("HomeButton", root.transform, "Home", new Color(0.35f, 0.35f, 0.38f), 22f, 50f, out _);
            var homeRect = (RectTransform)homeButton.transform;
            homeRect.anchorMin = new Vector2(0f, 1f);
            homeRect.anchorMax = new Vector2(0f, 1f);
            homeRect.pivot = new Vector2(0f, 1f);
            homeRect.sizeDelta = new Vector2(160f, 50f);
            homeRect.anchoredPosition = new Vector2(20f, -20f);

            var scrollRect = CreateHorizontalScrollView("MarkerScrollView", root.transform, out var content);
            var scrollGO = ((Component)scrollRect).gameObject;
            var scrollRectTransform = (RectTransform)scrollGO.transform;
            scrollRectTransform.offsetMin = new Vector2(0f, 0f);
            scrollRectTransform.offsetMax = new Vector2(0f, -100f);

            var controller = root.AddComponent<WorldMapController>();
            var so = new SerializedObject(controller);
            so.FindProperty("markerContainer").objectReferenceValue = content;
            so.FindProperty("levelMarkerPrefab").objectReferenceValue = levelMarkerPrefab;
            so.FindProperty("scrollRect").objectReferenceValue = scrollRect;
            so.FindProperty("homeButton").objectReferenceValue = homeButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ---- Matchup ----------------------------------------------------------------------------

        /// <summary>matchup.png (wired as this screen's background by ArtWiringBuilder) already
        /// bakes in the two wood-frame card slots and a "VS" graphic — CharacterCard/RobotCards
        /// sit directly on top of those two frames instead of the old abstract vertical stack of
        /// Level/Name/Objective text + a duplicate "VS" text + colour-square cards. RobotCards is
        /// still a HorizontalLayoutGroup so 1-3 active robot cards split the right frame's width
        /// evenly regardless of how many distinct robot types this level has (inactive slots are
        /// skipped by the layout). Play/Home are plain icon buttons at the bottom corners, same
        /// convention as the Main Menu cleanup.</summary>
        private static MatchupScreenController BuildMatchup(Transform canvasTransform)
        {
            var root = CreatePanel("MatchupScreen", canvasTransform, new Color(0.14f, 0.12f, 0.16f));

            var characterCard = CreateImage("CharacterCard", root.transform, Color.clear, 370f, 430f);
            var characterCardRect = characterCard.rectTransform;
            characterCardRect.anchorMin = new Vector2(0.5f, 0.5f);
            characterCardRect.anchorMax = new Vector2(0.5f, 0.5f);
            characterCardRect.pivot = new Vector2(0.5f, 0.5f);
            characterCardRect.sizeDelta = new Vector2(370f, 430f);
            characterCardRect.anchoredPosition = new Vector2(-640f, 70f);

            var robotCardsRow = CreateHorizontalGroup("RobotCards", root.transform, 6f);
            var robotRowRect = (RectTransform)robotCardsRow.transform;
            robotRowRect.anchorMin = new Vector2(0.5f, 0.5f);
            robotRowRect.anchorMax = new Vector2(0.5f, 0.5f);
            robotRowRect.pivot = new Vector2(0.5f, 0.5f);
            robotRowRect.sizeDelta = new Vector2(370f, 430f);
            robotRowRect.anchoredPosition = new Vector2(640f, 70f);
            var robotCards = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                robotCards[i] = CreateImage($"RobotCard{i}", robotCardsRow.transform, Color.clear, 110f, 150f);
            }

            var starDisplayGO = CreateStarDisplay("Stars", root.transform, 24);
            var starsRect = (RectTransform)starDisplayGO.transform;
            starsRect.anchorMin = new Vector2(0.5f, 1f);
            starsRect.anchorMax = new Vector2(0.5f, 1f);
            starsRect.pivot = new Vector2(0.5f, 1f);
            starsRect.anchoredPosition = new Vector2(0f, -24f);

            var countdownText = CreateText("Countdown", root.transform, string.Empty, 72f, TextAlignmentOptions.Center, 90f);
            var countdownRect = (RectTransform)countdownText.transform;
            countdownRect.anchorMin = new Vector2(0.5f, 0.5f);
            countdownRect.anchorMax = new Vector2(0.5f, 0.5f);
            countdownRect.pivot = new Vector2(0.5f, 0.5f);
            countdownRect.sizeDelta = new Vector2(400f, 120f);
            countdownRect.anchoredPosition = Vector2.zero;

            var playButton = CreateButton("PlayButton", root.transform, string.Empty, new Color(0.3f, 0.75f, 0.35f), 32f, 120f, out _);
            Object.DestroyImmediate(playButton.transform.Find("PlayButton_Label").gameObject);
            var playRect = (RectTransform)playButton.transform;
            playRect.anchorMin = new Vector2(0f, 0f);
            playRect.anchorMax = new Vector2(0f, 0f);
            playRect.pivot = new Vector2(0f, 0f);
            playRect.sizeDelta = new Vector2(120f, 120f);
            playRect.anchoredPosition = new Vector2(40f, 40f);

            var homeButton = CreateButton("HomeButton", root.transform, string.Empty, new Color(0.35f, 0.35f, 0.38f), 32f, 120f, out _);
            Object.DestroyImmediate(homeButton.transform.Find("HomeButton_Label").gameObject);
            var homeRect = (RectTransform)homeButton.transform;
            homeRect.anchorMin = new Vector2(1f, 0f);
            homeRect.anchorMax = new Vector2(1f, 0f);
            homeRect.pivot = new Vector2(1f, 0f);
            homeRect.sizeDelta = new Vector2(120f, 120f);
            homeRect.anchoredPosition = new Vector2(-40f, 40f);

            var controller = root.AddComponent<MatchupScreenController>();
            var so = new SerializedObject(controller);
            so.FindProperty("starDisplay").objectReferenceValue = starDisplayGO.GetComponent<StarDisplay>();
            so.FindProperty("characterCardImage").objectReferenceValue = characterCard;
            var robotCardsProp = so.FindProperty("robotCardImages");
            robotCardsProp.arraySize = 3;
            for (int i = 0; i < 3; i++) robotCardsProp.GetArrayElementAtIndex(i).objectReferenceValue = robotCards[i];
            so.FindProperty("countdownText").objectReferenceValue = countdownText;
            so.FindProperty("playButton").objectReferenceValue = playButton;
            so.FindProperty("backButton").objectReferenceValue = homeButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
            return controller;
        }

        // ---- Gameplay HUD -----------------------------------------------------------------------

        private static (GameObject root, ComboNotificationBanner banner) BuildGameplayHUD(Transform canvasTransform)
        {
            var root = CreateEmpty("GameplayScreen", canvasTransform);
            StretchFull((RectTransform)root.transform);

            // Score (top-left) — inset well clear of the rounded-corner safe area (20px used to
            // clip under the yellow safe-area guide, same class of fix as the Main Menu buttons).
            var scoreText = CreateText("ScoreText", root.transform, "0", 40f, TextAlignmentOptions.TopLeft, 60f);
            AnchorTopLeft((RectTransform)scoreText.transform, new Vector2(300f, 60f), new Vector2(100f, -90f));

            // Level (top-centre)
            var levelText = CreateText("LevelText", root.transform, string.Empty, 28f, TextAlignmentOptions.Top, 50f);
            AnchorTopCenter((RectTransform)levelText.transform, new Vector2(500f, 50f), new Vector2(0f, -20f));

            // Timer (top-right) — same safe-area inset as the score.
            var timerText = CreateText("TimerText", root.transform, "00:00", 32f, TextAlignmentOptions.TopRight, 50f);
            AnchorTopRight((RectTransform)timerText.transform, new Vector2(200f, 50f), new Vector2(-100f, -90f));

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

            // Pause/Sound/Home icon cluster (bottom-left) — replaces the old scattered
            // Swap(bottom-left)/Ability(bottom-centre)/Pause(bottom-right) buttons. Tab
            // (CharacterSwapUI) and Space (ability activation) still work as direct keyboard
            // input; this cluster is just pause, mute toggle, and quit-to-home. Sized/inset to
            // match the Main Menu's Play/Settings buttons (160x160, safe-area inset) — the
            // original 80x80 at a 20px inset both read as too small and sat outside the
            // safe-area guide.
            const float clusterButtonSize = 160f;
            const float clusterSpacing = 24f;
            const float clusterInsetX = 100f;
            const float clusterInsetY = 70f;

            var pauseButton = CreateButton("PauseButton", root.transform, string.Empty, new Color(0.35f, 0.35f, 0.38f), 28f, clusterButtonSize, out _);
            Object.DestroyImmediate(pauseButton.transform.Find("PauseButton_Label").gameObject);
            AnchorBottomLeft((RectTransform)pauseButton.transform, new Vector2(clusterButtonSize, clusterButtonSize), new Vector2(clusterInsetX, clusterInsetY));

            var soundButton = CreateButton("SoundButton", root.transform, string.Empty, new Color(0.35f, 0.35f, 0.38f), 28f, clusterButtonSize, out _);
            Object.DestroyImmediate(soundButton.transform.Find("SoundButton_Label").gameObject);
            AnchorBottomLeft((RectTransform)soundButton.transform, new Vector2(clusterButtonSize, clusterButtonSize),
                new Vector2(clusterInsetX + (clusterButtonSize + clusterSpacing), clusterInsetY));
            var soundIcon = soundButton.GetComponent<Image>();

            var homeButton = CreateButton("HomeButton", root.transform, string.Empty, new Color(0.35f, 0.35f, 0.38f), 28f, clusterButtonSize, out _);
            Object.DestroyImmediate(homeButton.transform.Find("HomeButton_Label").gameObject);
            AnchorBottomLeft((RectTransform)homeButton.transform, new Vector2(clusterButtonSize, clusterButtonSize),
                new Vector2(clusterInsetX + 2 * (clusterButtonSize + clusterSpacing), clusterInsetY));

            // Character portrait — above the (now much taller) button cluster, left-aligned with it.
            var portrait = CreateImage("CharacterPortrait", root.transform, new Color(1f, 0.84f, 0f), 90f, 90f);
            AnchorBottomLeft((RectTransform)portrait.transform, new Vector2(90f, 90f),
                new Vector2(clusterInsetX, clusterInsetY + clusterButtonSize + clusterSpacing));

            var hud = root.AddComponent<GameplayHUD>();
            var so = new SerializedObject(hud);
            so.FindProperty("scoreText").objectReferenceValue = scoreText;
            so.FindProperty("levelText").objectReferenceValue = levelText;
            so.FindProperty("timerText").objectReferenceValue = timerText;
            so.FindProperty("characterPortrait").objectReferenceValue = portrait;
            so.FindProperty("pauseButton").objectReferenceValue = pauseButton;
            so.FindProperty("soundButton").objectReferenceValue = soundButton;
            so.FindProperty("soundButtonIcon").objectReferenceValue = soundIcon;
            so.FindProperty("homeButton").objectReferenceValue = homeButton;
            so.FindProperty("powerPelletTimerBar").objectReferenceValue = powerBarGO;
            so.FindProperty("powerPelletTimerFill").objectReferenceValue = powerFillImage;
            so.FindProperty("chainCounterRoot").objectReferenceValue = chainRoot;
            so.FindProperty("chainCounterText").objectReferenceValue = chainText;
            so.ApplyModifiedPropertiesWithoutUndo();

            return (root, banner);
        }

        // ---- Pause Menu -------------------------------------------------------------------------

        private static GameObject BuildPauseMenu(Transform canvasTransform)
        {
            var root = CreatePanel("PauseOverlay", canvasTransform, new Color(0f, 0f, 0f, 0.75f));
            var group = CreateVerticalGroup("Content", root.transform, 14f, 30);

            CreateText("Title", group.transform, "PAUSED", 44f, TextAlignmentOptions.Center, 60f);
            var resumeButton = CreateButton("ResumeButton", group.transform, "Resume", new Color(0.3f, 0.75f, 0.35f), 26f, 70f, out _);
            var swapButton = CreateButton("SwapButton", group.transform, "Swap Character", new Color(0.35f, 0.45f, 0.75f), out _);
            var restartButton = CreateButton("RestartButton", group.transform, "Restart Level", new Color(0.75f, 0.55f, 0.2f), out _);
            var settingsButton = CreateButton("SettingsButton", group.transform, "Settings", new Color(0.35f, 0.35f, 0.38f), out _);
            var quitButton = CreateButton("QuitButton", group.transform, "Quit to Menu", new Color(0.75f, 0.25f, 0.25f), out _);

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

        private static GameObject BuildSettingsPanel(Transform canvasTransform)
        {
            var root = CreatePanel("SettingsOverlay", canvasTransform, new Color(0f, 0f, 0f, 0.8f));
            var group = CreateVerticalGroup("Content", root.transform, 10f, 30);

            var titleRow = CreateHorizontalGroup("TitleRow", group.transform, 10f);
            CreateText("Title", titleRow.transform, "SETTINGS", 36f, TextAlignmentOptions.Left, 50f);
            var closeButton = CreateButton("CloseButton", titleRow.transform, "X", new Color(0.6f, 0.2f, 0.2f), 22f, 50f, out _);
            closeButton.GetComponent<LayoutElement>().preferredWidth = 50f;

            var musicToggle = CreateToggle("MusicToggle", group.transform, "Music", out _);
            var musicSlider = CreateSlider("MusicVolumeSlider", group.transform, 1f);
            var sfxToggle = CreateToggle("SfxToggle", group.transform, "SFX", out _);
            var sfxSlider = CreateSlider("SfxVolumeSlider", group.transform, 1f);
            var vibrationToggle = CreateToggle("VibrationToggle", group.transform, "Vibration", out _);
            var leftHandedToggle = CreateToggle("LeftHandedToggle", group.transform, "Left-Handed", out _);

            var languageDropdownGO = new GameObject("LanguageDropdown", typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown), typeof(LayoutElement));
            languageDropdownGO.transform.SetParent(group.transform, false);
            languageDropdownGO.GetComponent<Image>().sprite = PlaceholderSprite.Get(new Color(0.25f, 0.25f, 0.28f));
            languageDropdownGO.GetComponent<LayoutElement>().preferredHeight = 44f;
            var languageDropdown = languageDropdownGO.GetComponent<TMP_Dropdown>();
            languageDropdown.options.Clear();
            languageDropdown.options.Add(new TMP_Dropdown.OptionData("English"));
            var langLabel = CreateText("Label", languageDropdownGO.transform, "English", 20f, TextAlignmentOptions.Left, 44f);
            StretchFull((RectTransform)langLabel.transform);
            languageDropdown.captionText = langLabel;

            var restoreButton = CreateButton("RestoreProgressButton", group.transform, "Restore Progress (Phase 6)", new Color(0.35f, 0.35f, 0.38f), out _);
            var resetButton = CreateButton("ResetProgressButton", group.transform, "Reset Progress", new Color(0.75f, 0.25f, 0.25f), out _);

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

        private static GameObject BuildLevelFailed(Transform canvasTransform)
        {
            var root = CreatePanel("LevelFailedScreen", canvasTransform, new Color(0.16f, 0.12f, 0.10f));
            var group = CreateVerticalGroup("Content", root.transform, 14f, 30);

            CreateText("Title", group.transform, "Try Again!", 44f, TextAlignmentOptions.Center, 60f);
            var scoreText = CreateText("ScoreText", group.transform, "Score: 0", 26f, TextAlignmentOptions.Center, 40f);
            var tipText = CreateText("TipText", group.transform, string.Empty, 20f, TextAlignmentOptions.Center, 60f);

            var buttonRow = CreateHorizontalGroup("Buttons", group.transform, 14f);
            var retryButton = CreateButton("RetryButton", buttonRow.transform, "Retry", new Color(0.3f, 0.75f, 0.35f), out _);
            var homeButton = CreateButton("HomeButton", buttonRow.transform, "Home", new Color(0.35f, 0.45f, 0.75f), out _);

            var controller = root.AddComponent<LevelFailedController>();
            var so = new SerializedObject(controller);
            so.FindProperty("scoreText").objectReferenceValue = scoreText;
            so.FindProperty("tipText").objectReferenceValue = tipText;
            so.FindProperty("retryButton").objectReferenceValue = retryButton;
            so.FindProperty("homeButton").objectReferenceValue = homeButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
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

        // ---- Cross-references (built after every screen exists) -------------------------------

        private static void WireCrossReferences(GameObject mainMenu, GameObject worldMap, MatchupScreenController matchup,
            GameObject gameplay, GameObject pause, GameObject settings,
            GameObject levelComplete, NewCharacterUnlockScreen unlockScreen, GameObject levelFailed,
            GameObject roster, GameObject leaderboards, CharacterSwapUI characterSwapUI, ComboNotificationBanner comboBanner)
        {
            var settingsPanel = settings.GetComponent<SettingsPanel>();

            SetRefs(mainMenu.GetComponent<MainMenuController>(),
                ("worldMapScreen", worldMap), ("settingsPanel", settingsPanel));

            SetRefs(worldMap.GetComponent<WorldMapController>(),
                ("mainMenuScreen", mainMenu), ("matchupScreen", matchup));

            SetRefs(matchup,
                ("gameplayScreen", gameplay), ("worldMapScreen", worldMap));

            var hud = gameplay.GetComponent<GameplayHUD>();
            SetRefs(hud,
                ("pauseMenu", pause.GetComponent<PauseMenuController>()),
                ("levelCompleteScreen", levelComplete), ("levelFailedScreen", levelFailed));

            SetRefs(pause.GetComponent<PauseMenuController>(),
                ("characterSwapUI", characterSwapUI), ("settingsPanel", settingsPanel));

            SetRefs(levelComplete.GetComponent<LevelCompleteController>(),
                ("worldMapScreen", worldMap), ("gameplayScreen", gameplay), ("unlockScreen", unlockScreen));

            SetRefs(levelFailed.GetComponent<LevelFailedController>(),
                ("gameplayScreen", gameplay), ("worldMapScreen", worldMap));

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
