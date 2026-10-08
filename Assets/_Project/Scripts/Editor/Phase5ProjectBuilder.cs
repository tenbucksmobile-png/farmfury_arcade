using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
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

            // Web demo: shop, cosmetics, locker, legal, parental gate, menu hub, leaderboards,
            // character roster and world purchase screens are not built. See WEB_DEMO_PLAN.md.
            var characterSelectCardPrefab = BuildCharacterSelectCardPrefab();

            var fadeGroup = BuildFadeOverlay(canvas.transform);

            var mainMenu = BuildMainMenu(canvas.transform);
            var titleScreen = BuildTitleScreen(canvas.transform, mainMenu);
            var gameplay = BuildGameplayHUD(canvas.transform);
            // Always-active, alpha-driven combo callout - see BuildComboHypeScreen's doc comment.
            BuildComboHypeScreen(canvas.transform);
            var pause = BuildPauseMenu(canvas.transform);
            var settings = BuildSettingsPanel(canvas.transform);
            var characterStory = BuildCharacterStoryPlaceholder(canvas.transform, characterSelectCardPrefab);
            var (levelComplete, unlockScreen) = BuildLevelComplete(canvas.transform);
            var levelFailed = BuildLevelFailed(canvas.transform);

            var chooseCharacter = BuildChooseCharacterScreen(canvas.transform, characterSelectCardPrefab);

            var levelTilePrefab = BuildLevelTilePrefab();
            BuildWorldDividerPrefab(); // kept but unlinked - see that method's own doc comment
            var worldShieldPrefab = BuildWorldShieldPrefab();
            var levelSelect = BuildLevelSelect(canvas.transform, levelTilePrefab, worldShieldPrefab);

            WireCrossReferences(mainMenu, gameplay, pause, settings,
                levelComplete, unlockScreen, levelFailed, chooseCharacter, levelSelect, characterStory);

            // One Esc/back handler (also YouTube Playables' "Esc closes dialogs"). Destroy any
            // existing instances first - re-runs used to stack duplicates.
            foreach (var existingBackButtonHandler in managersGO.GetComponents<AndroidBackButtonHandler>())
            {
                Object.DestroyImmediate(existingBackButtonHandler);
            }
            var backButtonHandler = managersGO.AddComponent<AndroidBackButtonHandler>();
            SetRefs(backButtonHandler,
                ("settingsPanel", settings.GetComponent<SettingsPanel>()),
                ("chooseCharacterScreen", chooseCharacter),
                ("pauseMenuScreen", pause.GetComponent<PauseMenuController>()),
                ("levelSelectController", levelSelect.GetComponent<LevelSelectController>()),
                ("gameplayHud", gameplay.GetComponent<GameplayHUD>()));

            var transitionManager = managersGO.GetComponent<SceneTransitionManager>();
            var transitionSO = new SerializedObject(transitionManager);
            transitionSO.FindProperty("fadeGroup").objectReferenceValue = fadeGroup;
            var screenRootsProp = transitionSO.FindProperty("screenRoots");
            var screens = new[] { titleScreen, mainMenu, gameplay, levelComplete, levelFailed, levelSelect };
            screenRootsProp.arraySize = screens.Length;
            for (int i = 0; i < screens.Length; i++)
            {
                screenRootsProp.GetArrayElementAtIndex(i).objectReferenceValue = screens[i];
            }
            transitionSO.ApplyModifiedPropertiesWithoutUndo();

            // Title screen starts active (attract mode, tap to continue); everything else
            // (including Main Menu and every overlay) starts hidden.
            foreach (var screen in screens)
            {
                screen.SetActive(screen == titleScreen);
            }
            pause.SetActive(false);
            settings.SetActive(false);
            characterStory.SetActive(false);
            chooseCharacter.gameObject.SetActive(false);
            unlockScreen.gameObject.SetActive(false);

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
            // Web demo: AdManager/IAPManager/AnalyticsManager were deleted. Remove the
            // missing-script components they leave behind on GameManagers.
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(managersGO);
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

        /// <summary>In-gameplay combo callout shown only when ComboSystem.OnComboTriggered actually
        /// fires (see ComboHypeScreen.cs) — never at level start, never on an ordinary character
        /// swap. Shows the specific Combo_*.png banner for the combo that fired, centred, fading in
        /// and back out on top of live gameplay. Plays the Combo.mp3 stinger (AudioManager.
        /// PlayComboSfx, wired via ArtWiringBuilder.WireAudio). Self-contained, same "bake
        /// everything at construction time" convention MenuHubScreen/ShopController use — nothing
        /// here needs ArtWiringBuilder for the sprites themselves (only the SFX clip goes through
        /// ArtWiringBuilder, since that's the established convention for AudioManager fields).
        ///
        /// Reworked 2026-09-11 from a full-screen "page" (opaque black root, the current world's
        /// own dimmed backdrop, a multi-second Time.timeScale freeze) into a lightweight overlay —
        /// the root has NO Image/backing at all (a plain RectTransform, not CreatePanel's usual
        /// opaque-backed panel) so gameplay stays fully visible and playable behind the banner; only
        /// the banner Image itself is drawn. See ComboHypeScreen's own doc comment for the timing
        /// (brief real freeze right at the trigger instant, then a live 3-second fade in/out).
        ///
        /// Deliberately stays active for the app's whole lifetime (like BuildFadeOverlay's own
        /// FadeOverlay) rather than being SetActive(false)'d in BuildAll's usual overlay sweep —
        /// its CanvasGroup starts at alpha 0/non-interactable, so it's invisible at rest, but its
        /// OnEnable event subscription to ComboSystem.OnComboTriggered only fires once, at scene
        /// load, and must not be missed by starting the GameObject inactive.</summary>
        private static GameObject BuildComboHypeScreen(Transform canvasTransform)
        {
            var root = new GameObject("ComboHypeScreen", typeof(RectTransform));
            root.transform.SetParent(canvasTransform, false);
            StretchFull((RectTransform)root.transform);
            var canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            // Banner art, centred — a fixed, centred box (not edge-to-edge) so a wide/tall
            // Combo_*.png reads as a clean centred card over live gameplay instead of covering the
            // whole screen.
            var bannerGO = new GameObject("BannerImage", typeof(RectTransform), typeof(Image));
            bannerGO.transform.SetParent(root.transform, false);
            var bannerImage = bannerGO.GetComponent<Image>();
            bannerImage.preserveAspect = true; // each Combo_*.png can have its own aspect ratio
            bannerImage.raycastTarget = false; // purely a callout — never intercepts gameplay taps
            var bannerRect = (RectTransform)bannerGO.transform;
            bannerRect.anchorMin = new Vector2(0.5f, 0.5f);
            bannerRect.anchorMax = new Vector2(0.5f, 0.5f);
            bannerRect.pivot = new Vector2(0.5f, 0.5f);
            bannerRect.sizeDelta = new Vector2(900f, 900f);
            bannerRect.anchoredPosition = Vector2.zero;

            // Name -> banner sprite, keyed by ComboSystem's own trigger names (ComboSystem.
            // Trigger("Feather Storm", ...) etc.) so HandleComboTriggered can look up the exact
            // combo that fired instead of picking one at random.
            var comboBannerEntries = new (string comboName, Sprite banner)[]
            {
                ("Crossfire", LoadUiSprite("Combo_CrossFire.png")),
                ("Double Slam", LoadUiSprite("Combo_DoubleSlam.png")),
                ("Earthquake Roll", LoadUiSprite("Combo_EarthquakeRoll.png")),
                ("Feather Storm", LoadUiSprite("Combo_Featherstorm.png")),
                ("Full Fury", LoadUiSprite("Combo_FullFury.png")),
                ("Iron Stampede", LoadUiSprite("Combo_IronStampede.png")),
                ("Kick and Roll", LoadUiSprite("Combo_KicknRoll.png")),
                ("Skip Shatter", LoadUiSprite("Combo_SkipShatter.png")),
            };

            var hype = root.AddComponent<ComboHypeScreen>();
            var hypeSo = new SerializedObject(hype);
            hypeSo.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            hypeSo.FindProperty("bannerImage").objectReferenceValue = bannerImage;
            var bannersProp = hypeSo.FindProperty("comboBanners");
            bannersProp.arraySize = comboBannerEntries.Length;
            for (int i = 0; i < comboBannerEntries.Length; i++)
            {
                var element = bannersProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("comboName").stringValue = comboBannerEntries[i].comboName;
                element.FindPropertyRelative("banner").objectReferenceValue = comboBannerEntries[i].banner;
            }
            hypeSo.ApplyModifiedPropertiesWithoutUndo();

            return root;
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
            // Shrunk again (~28%, 810x855 -> 580x615, aspect preserved) per a gameplay-screenshot
            // review showing the centred badge's top edge still overlapping the SELECT LEVEL/world-
            // name header sign above it even after BuildLevelSelect's header/carousel repositioning
            // — that repositioning alone wasn't enough; the badge itself was still too tall for the
            // available vertical space.
            goRect.sizeDelta = new Vector2(580f, 615f);
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
            //
            // The Shop icon briefly lived here (top-left, world-select state only) before being
            // moved again to Settings' new 4x2 grid (2026-08-20) — see Phase5ProjectBuilder.
            // BuildSettingsPanel.

            // TitleImage replaces the old TMP "SELECT LEVEL" text — SelectLevelSign.png is the
            // word-art itself, wired by ArtWiringBuilder. preserveAspect so it never distorts.
            // Shrunk (320 -> 260) and moved up slightly (-40 -> -20) from the original
            // Settings-title-matching size per a gameplay-screenshot review: the world carousel's
            // badges (WorldShield, 810x855 — see BuildWorldShieldPrefab) were tall enough that their
            // top edge visibly overlapped this banner's bottom edge. Combined with the carousel's
            // own downward shift below, this is a first-pass gap increase, not a pixel-measured
            // fix (no visual Editor access this session) — re-check against the actual banner/badge
            // art once seen and nudge further if any overlap remains.
            var titleImage = CreateImage("TitleImage", root.transform, Color.clear, 860f, 260f);
            titleImage.preserveAspect = true;
            AnchorTopCenter((RectTransform)titleImage.transform, new Vector2(860f, 260f), new Vector2(0f, -20f));

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
            // TitleImage (above) is anchored at y=-20 with a 260px height, so its bottom edge sits
            // at y=-280 from the top of the screen. -320 leaves a clean ~40px gap under the banner.
            scrollViewRect.offsetMax = new Vector2(0f, -320f);

            // World-select carousel area — vertically centred on the screen, nudged down (see the
            // sizeDelta/anchoredPosition comment below for the current top/bottom split) so the
            // centred badge clears the header above it, and spanning most of the width so badges
            // have room to fan out. An invisible-but-
            // raycastable Image covers the whole area (not just the badges themselves) so a flick
            // started on empty space between badges still registers as a drag; CardCarouselController
            // then positions/scales each badge every frame instead of a LayoutGroup arranging them
            // in a static row/column.
            var worldShieldContainerGO = new GameObject("WorldShieldContainer", typeof(RectTransform), typeof(Image));
            var shieldContainerRect = (RectTransform)worldShieldContainerGO.transform;
            shieldContainerRect.anchorMin = new Vector2(0.5f, 0f);
            shieldContainerRect.anchorMax = new Vector2(0.5f, 1f);
            shieldContainerRect.pivot = new Vector2(0.5f, 0.5f);
            // Height/position widened and shifted down further (was -400/-16) per a gameplay-
            // screenshot review — the centred badge (810x855, see BuildWorldShieldPrefab) was tall
            // enough that its top edge visibly overlapped TitleImage's banner above. Combined with
            // shrinking/raising the banner itself (see TitleImage above), this trades some of the
            // carousel's own headroom for a real gap; re-check against the actual art once seen and
            // nudge further (or shrink WorldShield itself) if any overlap remains — first-pass,
            // no visual Editor access this session.
            shieldContainerRect.sizeDelta = new Vector2(1600f, -460f);
            shieldContainerRect.anchoredPosition = new Vector2(0f, -70f);
            worldShieldContainerGO.transform.SetParent(root.transform, false);
            var shieldContainerImage = worldShieldContainerGO.GetComponent<Image>();
            shieldContainerImage.sprite = PlaceholderSprite.Get(Color.clear);
            shieldContainerImage.color = Color.clear;
            shieldContainerImage.raycastTarget = true;
            var worldCarousel = worldShieldContainerGO.AddComponent<CardCarouselController>();
            // Tightened twice per feedback that badges still read as spaced too far apart —
            // 730 -> 600 (see CLAUDE.md for the original 730 sizing math), then scaled down to 430
            // (600 * 580/810, the same ~0.72 ratio BuildWorldShieldPrefab's badge size just shrunk
            // by) so the fan's relative overlap between adjacent badges stays visually consistent
            // now that the badges themselves are smaller — leaving itemSpacing at 600 against a
            // smaller badge would have opened up a much wider relative gap than before.
            // CardCarouselController arranges items along a true circular arc (see its own arcRadius
            // field) instead of a flat linear x-offset, so itemSpacing here is the arc-length step
            // between adjacent items, not a straight pixel offset — arcRadius is left at the
            // component's default (2800), which reads as a natural curve at this spacing.
            var worldCarouselSO = new SerializedObject(worldCarousel);
            worldCarouselSO.FindProperty("itemSpacing").floatValue = 430f;
            worldCarouselSO.ApplyModifiedPropertiesWithoutUndo();

            // Created after the ScrollView and WorldShieldContainer (both full-bleed raycastable
            // areas) so it's the later sibling and actually receives taps instead of having them
            // swallowed by whichever of those two draws on top of it.
            var backButton = CreateRoundBackButton(root.transform);

            // Daily Challenge no longer has its own standalone button here — it moved INTO the
            // world carousel itself as the first shield (DailyChallengeSentinel, ahead of Corn
            // Field), per feedback that it should live alongside the world badges rather than as a
            // separate top-right icon. See LevelSelectController.ShowWorldSelect/PlayDailyChallenge
            // and dailyChallengeSignSprite (wired by ArtWiringBuilder to DailyChallenge.png).

            var lockedHintPanel = BuildLockedHintPanel(root.transform);

            // "Visit Our Store" merch promo moved to MenuHubScreen (2026-09-14, per direct feedback)
            // — see BuildMenuHubScreen. No longer built here at all.

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
                ("backButtonImage", backButton.GetComponent<Image>()),
                ("titleImage", titleImage),
                ("titleWorldSelectSprite", LoadUiSprite("WorldUnlocked.png")),
                ("titleTileGridSprite", LoadUiSprite("SelectLevelSign.png")));

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
            // Inset matched to SettingsButton's own 150px edge inset below (was 130 — a 20px
            // asymmetry that read as Play sitting closer to the yellow safe-area guide than
            // Settings on the opposite corner, per a device-frame screenshot review).
            playRect.anchoredPosition = new Vector2(150f, 70f);

            var settingsButton = CreateButton("SettingsButton", root.transform, string.Empty, new Color(0.35f, 0.35f, 0.38f), 28f, 160f, out _);
            Object.DestroyImmediate(settingsButton.transform.Find("SettingsButton_Label").gameObject);
            var settingsRect = (RectTransform)settingsButton.transform;
            settingsRect.anchorMin = new Vector2(1f, 0f);
            settingsRect.anchorMax = new Vector2(1f, 0f);
            settingsRect.pivot = new Vector2(1f, 0f);
            settingsRect.sizeDelta = new Vector2(160f, 160f);
            settingsRect.anchoredPosition = new Vector2(-150f, 70f);

            // Web demo: no Exit button (YouTube Playables forbids one).

            // Shop icon moved off Main Menu entirely (2026-08-20) — relocated to Level Select's
            // world-select page, top-left inside the safe-area guide (see BuildLevelSelect). Main
            // Menu is back down to just Play/Settings.
            //
            // Daily Challenge and Leaderboards also no longer live on Main Menu — moved to Level
            // Select and Settings respectively (see LevelSelectController/SettingsPanel's own doc
            // comments) per feedback that the landing page should stay minimal.
            //
            // The "Visit Our Store" merch banner briefly lived here too (2026-09-08) but was moved
            // to Level Select's world-select state instead — per a gameplay screenshot review it
            // sat awkwardly in front of landing.png's baked-in character art. See BuildLevelSelect.

            var controller = root.AddComponent<MainMenuController>();
            var so = new SerializedObject(controller);
            so.FindProperty("playButton").objectReferenceValue = playButton;
            so.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ---- Title screen -----------------------------------------------------------------------

        /// <summary>Attract-mode title screen (2026-08-30) — shown once at launch, before Main
        /// Menu; tap anywhere to continue. landing.png (full brightness — same art Main Menu itself
        /// shows once you continue past this screen, so the hand-off doesn't jump) already bakes in
        /// its own "FARM FURY ARCADE" wordmark, so a separate FFArcade_Icon.png logo stacked on top
        /// of it read as redundant clutter (per a screenshot review) — removed, only the pulsating
        /// PressStart.png prompt remains on top of the backdrop.
        ///
        /// Self-contained, same "bake everything at construction time" convention MenuHubScreen/
        /// ShopController use — nothing here needs ArtWiringBuilder.</summary>
        private static GameObject BuildTitleScreen(Transform canvasTransform, GameObject mainMenuScreen)
        {
            var root = CreatePanel("TitleScreen", canvasTransform, Color.black);
            root.GetComponent<Image>().sprite = LoadUiSprite("landing.png");

            var pressStartGO = new GameObject("PressStart", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            pressStartGO.transform.SetParent(root.transform, false);
            var pressStartImage = pressStartGO.GetComponent<Image>();
            pressStartImage.sprite = LoadUiSprite("PressStart.png");
            pressStartImage.preserveAspect = true;
            AnchorBottomCenter((RectTransform)pressStartGO.transform, new Vector2(600f, 160f), new Vector2(0f, 130f));

            // Tap anywhere — the root's own Image (landing.png) is the raycast target, same
            // "Button directly on the root panel" convention NewCharacterUnlockScreen's own
            // full-screen tap-to-dismiss gate uses.
            var tapButton = root.AddComponent<Button>();
            tapButton.targetGraphic = root.GetComponent<Image>();

            var controller = root.AddComponent<TitleScreenController>();
            var so = new SerializedObject(controller);
            so.FindProperty("tapButton").objectReferenceValue = tapButton;
            so.FindProperty("pressStartGroup").objectReferenceValue = pressStartGO.GetComponent<CanvasGroup>();
            so.FindProperty("mainMenuScreen").objectReferenceValue = mainMenuScreen;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ---- Gameplay HUD -----------------------------------------------------------------------

        private static GameObject BuildGameplayHUD(Transform canvasTransform)
        {
            var root = CreateEmpty("GameplayScreen", canvasTransform);
            StretchFull((RectTransform)root.transform);

            // Audit finding C3.3: every HUD element below used to parent directly onto `root`,
            // which spans the raw physical screen edge to edge — every corner inset (Pause button,
            // ability icon, coin chip, score/timer) was tuned against one static reference overlay
            // an artist eyeballed once, never against the runtime Screen.safeArea API. On a device
            // with a notch/Dynamic Island/punch-hole camera/gesture bar that doesn't match that one
            // reference frame, those elements could be clipped or obscured — and Android's real-
            // world cutout shapes are far more varied than iOS's handful of known ones. `SafeArea`
            // (Utilities/SafeArea.cs) shrinks its own RectTransform's anchors to Screen.safeArea
            // live, every frame it changes; parenting every HUD element under it instead of `root`
            // means every existing AnchorTopLeft/AnchorBottomRight/etc. call below needs zero
            // changes — their insets are already relative to their immediate parent's edges, which
            // are now the safe-area edges instead of the raw screen edges automatically.
            var safeArea = CreateEmpty("SafeArea", root.transform);
            StretchFull((RectTransform)safeArea.transform);
            safeArea.AddComponent<SafeArea>();

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
            // Timer moved above Score (both top-left corner now) per feedback — it used to sit
            // top-right where the coin balance pill now lives (see CoinBalanceChip below). Its own
            // box sits directly above ScoreText's (same 90px height, top edge at -80 so its bottom
            // edge lands exactly on ScoreText's top edge at -170, no gap/overlap).
            // Dark brown fill (same tone LevelComplete/LevelFailed's score text already uses)
            // instead of CreateText's plain-white default — white was unreadable against the
            // bright sky/backdrop art behind it, per a gameplay screenshot review.
            var hudTextColor = new Color(0.25f, 0.15f, 0.06f);
            // Real bug found and fixed (2026-08-29): every inset in this whole method was tuned
            // back when these elements were parented straight onto the raw screen edge — SafeArea
            // (added 2026-08-28, right above) now shrinks ITS OWN rect to Screen.safeArea first, and
            // every AnchorTopLeft/AnchorBottomRight/etc. call below measures its inset from THAT
            // already-inset edge, not the physical screen edge. On any device that actually reports
            // a non-zero safe-area inset (a notch, Dynamic Island, gesture bar — which is most of
            // them), the two insets now stack: the deep "clear the maze" values tuned pre-SafeArea
            // over-inset every corner element a second time, pulling the whole HUD further inward
            // and deeper onto the maze's own rendered tiles instead of off them — the opposite of
            // what they were meant to do. Reduced across the board (previous values noted per call)
            // since SafeArea's own shrink now already does the "clear the device's real unsafe
            // edge" job on its own; these residual insets are just breathing room from the safe
            // rectangle's edge, not double duty. Previously 170 -> 110 (a same-day-earlier fix) ->
            // 60 now.
            var timerText = CreateText("TimerText", safeArea.transform, "00:00", 60f, TextAlignmentOptions.TopLeft, 90f, hudTextColor);
            AnchorTopLeft((RectTransform)timerText.transform, new Vector2(240f, 90f), new Vector2(60f, -40f));

            var scoreText = CreateText("ScoreText", safeArea.transform, "0", 72f, TextAlignmentOptions.TopLeft, 90f, hudTextColor);
            AnchorTopLeft((RectTransform)scoreText.transform, new Vector2(320f, 90f), new Vector2(60f, -130f));

            // Monetisation: coin balance display — previously SaveManager.CoinBalance had no
            // on-screen display anywhere at all (only surfaced indirectly via the Revive prompt's
            // cost text / skip-cooldown button's cost label), which is bad UX once the player is
            // actually being asked to spend coins on both of those.
            //
            // Reworked 2026-08-28 from the wood-frame Coin_Balance_Chip.png plaque to a plain coin
            // icon (Coin_UI.png) + number, per direct feedback: the fixed-size wood frame couldn't
            // grow to fit a large balance (a player who bought the 15,000-coin pack would overflow
            // the frame's own parchment band). Built as a self-sizing row (HorizontalLayoutGroup +
            // ContentSizeFitter, both self-baked here rather than going through
            // ArtWiringBuilder.SetImageSprite, so nothing forces a fixed box the number could
            // outgrow) anchored by its own top-right corner, so it grows LEFTWARD as the balance
            // gains digits instead of running off the right edge of the screen.
            var coinDisplayGO = new GameObject("CoinBalanceDisplay", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            coinDisplayGO.transform.SetParent(safeArea.transform, false);
            var coinDisplayRect = (RectTransform)coinDisplayGO.transform;
            coinDisplayRect.anchorMin = coinDisplayRect.anchorMax = new Vector2(1f, 1f);
            coinDisplayRect.pivot = new Vector2(1f, 1f);
            // Shifted right and down (-100,-40 -> -70,-70) per feedback ("shift the coin and
            // counter slightly to the right and slightly downward") — a device screenshot showed it
            // sitting too close to the top-right corner of the yellow safe-area guide.
            coinDisplayRect.anchoredPosition = new Vector2(-70f, -70f);
            var coinDisplayHlg = coinDisplayGO.GetComponent<HorizontalLayoutGroup>();
            coinDisplayHlg.spacing = 14f;
            coinDisplayHlg.childAlignment = TextAnchor.MiddleRight;
            coinDisplayHlg.childControlWidth = true;
            coinDisplayHlg.childControlHeight = true;
            coinDisplayHlg.childForceExpandWidth = false;
            coinDisplayHlg.childForceExpandHeight = false;
            var coinDisplayFitter = coinDisplayGO.GetComponent<ContentSizeFitter>();
            coinDisplayFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            coinDisplayFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            const float coinIconSize = 72f;
            var coinIconGO = new GameObject("CoinIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            coinIconGO.transform.SetParent(coinDisplayGO.transform, false);
            var coinIconImage = coinIconGO.GetComponent<Image>();
            coinIconImage.sprite = LoadUiSprite("Coin_UI.png");
            coinIconImage.preserveAspect = true;
            var coinIconLayoutElement = coinIconGO.GetComponent<LayoutElement>();
            coinIconLayoutElement.preferredWidth = coinIconSize;
            coinIconLayoutElement.preferredHeight = coinIconSize;

            var coinBalanceText = CreateText("CoinBalanceText", coinDisplayGO.transform, "0", 56f, TextAlignmentOptions.Left, coinIconSize, hudTextColor);
            // Word-wrap off, no fixed preferredWidth override on its LayoutElement (CreateText's
            // default -1/unset) — TMP reports its own natural preferred width for whatever the
            // current balance renders as, so the HorizontalLayoutGroup reflows the whole row's width
            // automatically every time RefreshCoinBalanceText updates the number, growing/shrinking
            // with digit count rather than ever clipping or wrapping.
            coinBalanceText.enableWordWrapping = false;
            coinBalanceText.overflowMode = TextOverflowModes.Overflow;

            // Power pellet timer bar + chain counter (upper area, under the level text)
            var powerBarGO = CreatePanel("PowerPelletTimerBar", safeArea.transform, new Color(0.2f, 0.2f, 0.22f));
            var powerBarRect = (RectTransform)powerBarGO.transform;
            AnchorTopCenter(powerBarRect, new Vector2(400f, 24f), new Vector2(0f, -80f));
            var powerFillGO = CreatePanel("Fill", powerBarGO.transform, new Color(0.85f, 0.2f, 0.85f));
            var powerFillImage = powerFillGO.GetComponent<Image>();
            powerFillImage.type = Image.Type.Filled;
            powerFillImage.fillMethod = Image.FillMethod.Horizontal;
            powerFillImage.fillAmount = 1f;

            var chainRoot = CreateEmpty("ChainCounterRoot", safeArea.transform);
            var chainText = CreateText("ChainCounterText", chainRoot.transform, string.Empty, 26f, TextAlignmentOptions.Center, 34f);
            StretchFull((RectTransform)chainText.transform);
            AnchorTopCenter((RectTransform)chainRoot.transform, new Vector2(200f, 34f), new Vector2(0f, -112f));

            // The in-maze "COMBO! {name}" text toast (ComboNotificationBanner) was removed —
            // ComboHypeScreen's own full-screen page (real banner art + world backdrop) now covers
            // this moment, so no separate HUD text container is built here anymore.

            // Character portrait / ability icon cluster (bottom-right). Pause used to sit directly
            // above it here, forming a two-button stack — it's since moved to sit above the D-pad
            // instead, sized to match the D-pad's own buttons rather than a cluster size of its own
            // (see the "Pause button" block after the D-pad, below), per feedback, so this corner is
            // now just the ability icon on its own. clusterSpacing/clusterInsetX/clusterInsetY are
            // kept (not renamed) since the skip/watch-ad buttons below still reference them.
            // Bumped 20 -> 30 per feedback ("make sure all spacing is equal and neat") — this one
            // constant now drives every gap in this corner (Pause-above-D-pad, ability-icon-to-
            // WatchAd, skip-cooldown-to-icon) so they all read as the same consistent spacing
            // instead of several different hand-tuned values.
            const float clusterSpacing = 30f;
            // Pulled back out again (-160 -> -100 -> -60) — see the real root cause documented on
            // TimerText/ScoreText above: SafeArea's own shrink (added 2026-08-28) now already
            // clears the device's real unsafe edge, so keeping this corner's insets as deep as they
            // were tuned pre-SafeArea double-inset it, still crowding the ability/swap icons in
            // over the maze even after the first -100 pass.
            const float clusterInsetX = -60f;
            // Dropped slightly (50 -> 25, 2026-09-14 third iPhone 11 screenshot: "drop slightly
            // downward") — lowers the whole right-side stack (WatchAd -> ability icon -> Swap
            // Character -> Locker all key off this one value via abilityBottomY below) by the same
            // amount together.
            const float clusterInsetY = 25f;

            // Ability icon enlarged (120 -> 150 -> 210) and shifted further left (its own inset, not
            // Pause's) per direct feedback — it's the on-screen ability button (see below) and the
            // biggest/most-tapped element in this cluster, so it gets its own size distinct from
            // Pause's. abilityInsetX is more negative than
            // clusterInsetX (AnchorBottomRight: negative X moves an element further left/inward),
            // shifting it left of where Pause's own X still sits. The AnchorBottomRight pivot means
            // this growth extends the box up and left from its fixed bottom-right corner, so
            // enlarging it doesn't need any inset retuning to avoid clipping the screen edge.
            // Shrunk slightly (210 -> 180) and shifted right (abilityShiftLeft 30 -> 10, i.e. less
            // inward from clusterInsetX) per feedback ("slightly reduce the size... and shift
            // slightly right, keep inside the yellow border") — a device screenshot showed both
            // icons crowding right up against the yellow safe-area guide.
            //
            // Enlarged again (2026-09-14, direct iPhone 11 feedback: "enlarge it without encroaching
            // on surrounding areas") — 180 -> 220, back above the earlier 210 this was shrunk from.
            // Safe with respect to the right-edge crowding the 210->180 shrink above was reacting
            // to: this button is AnchorBottomRight-pivoted, so its fixed corner is
            // (abilityInsetX, abilityBottomY) regardless of size — growing the box only extends it
            // further UP and LEFT from that corner, never further right toward the safe-area edge.
            const float abilityButtonSize = 220f;
            const float abilityShiftLeft = 10f;
            const float abilityInsetX = clusterInsetX - abilityShiftLeft;

            // Real bug found the same session (second device screenshot): the FIRST version of the
            // ability-icon enlargement above also grew Swap Character/Locker, since both used to
            // share abilityButtonSize directly for their own box size — per direct correction
            // ("you increased the other icons as well - only the ability needed to be re sized,
            // both are overlaying the counters"), those two are unrelated buttons and should have
            // stayed at their prior size; growing all three pushed the whole stack tall enough for
            // Locker (topmost) to overlap the CoinBalanceDisplay in the top-right corner. Swap
            // Character/Locker now use their own clusterIconSize (180, their pre-enlargement value)
            // — only the ability/portrait button itself uses the bigger abilityButtonSize. Their
            // stacking offsets below are computed from each button's own real height rather than
            // assuming every entry in the stack is the same size.
            const float clusterIconSize = 180f;

            // Real bug found the same session (third device screenshot): "re-align the icons
            // underneath each other" — Swap Character/Locker still used abilityInsetX directly for
            // their own right edge, which right-edge-aligns them with the (wider) ability icon
            // rather than CENTRE-aligning them. Since AnchorBottomRight's offset is a box's own
            // right edge, two boxes of different widths sharing that same offset have different
            // centres — the narrower clusterIconSize boxes' centres sat
            // (abilityButtonSize-clusterIconSize)/2 = 20 units to the right of the ability icon's
            // centre, reading as visibly "off" underneath it. clusterIconInsetX shifts Swap
            // Character/Locker left by exactly that half-difference so all three share one true
            // vertical centreline regardless of either size being retuned again later.
            const float clusterIconInsetX = abilityInsetX - (abilityButtonSize - clusterIconSize) / 2f;

            const float skipButtonSize = 64f;

            // WatchAdSkipCooldownButton now shows the real WatchAd.png art instead of a plain "AD"
            // text label (per feedback: "enlarge and remove the ad text"). WatchAd.png is a wide
            // 512x214 banner — sizing the box to that exact aspect (170 x ~71) means the Sliced
            // stretch SetImageSprite always applies ends up uniform instead of squashing the art,
            // same "box aspect must match the art" fix used throughout this project (Coin Balance
            // Chip, Revive Prompt panel, Level Complete panel, etc.). Declared here (rather than
            // down by its own button below) since its height feeds into how far the icon is raised.
            const float watchAdButtonWidth = 170f;
            const float watchAdButtonHeight = watchAdButtonWidth * 214f / 512f;

            // The icon used to sit at clusterInsetY directly; it's now raised by WatchAd's own
            // height + a gap, since WatchAd moved from beside the icon to underneath it (per
            // feedback) and needs that space at the bottom of this corner instead. Gap tightened
            // to its own smaller value (was the shared clusterSpacing, 30) per feedback that this
            // specific gap read as too wide — clusterSpacing itself is left untouched since it still
            // governs the (still-uniform) Pause-above-D-pad gap elsewhere.
            const float abilityToWatchAdSpacing = 12f;
            float abilityBottomY = clusterInsetY + watchAdButtonHeight + abilityToWatchAdSpacing;

            // Character portrait sits at the bottom of the cluster (closer to the corner). Doubles
            // as the on-screen ability button (Space has no touch equivalent, so without this the
            // ability was completely unreachable on a device with no keyboard): tapping it raises
            // the same InputController event Space does, and GameplayHUD dims it while the active
            // character's ability is on cooldown.
            var portraitButton = CreateButton("CharacterPortrait", safeArea.transform, string.Empty, Color.clear, 26f, abilityButtonSize, out _);
            Object.DestroyImmediate(portraitButton.transform.Find("CharacterPortrait_Label").gameObject);
            AnchorBottomRight((RectTransform)portraitButton.transform, new Vector2(abilityButtonSize, abilityButtonSize),
                new Vector2(abilityInsetX, abilityBottomY));
            // onClick wiring happens in GameplayHUD.Awake() (via the abilityButton field below),
            // not here — a listener added directly from editor-script code doesn't survive a scene
            // save/reload (UnityEvent's non-persistent listeners aren't serialized), same pitfall
            // SimpleClosePanel exists to work around elsewhere in this builder.
            //
            // The button's own Image is transparent (CreateButton's PlaceholderSprite.Get(Color.
            // clear) above), raycast-target only — same "invisible root, art on a child" convention
            // CharacterSelectCard's root Image uses. It used to be a solid gold circle
            // (PlaceholderSprite.GetCircle) behind the character sprite; removed per direct feedback
            // that the circle backdrop read as clutter once a real ability icon existed. A separate
            // non-interactive "PortraitArt" child holds the actual character sprite;
            // GameplayHUD.characterPortrait points at this child (and is what StartReadyFlash scales/
            // tints when the ability is ready).
            var portraitArtGO = new GameObject("PortraitArt", typeof(RectTransform), typeof(Image));
            portraitArtGO.transform.SetParent(portraitButton.transform, false);
            var portraitArtRect = (RectTransform)portraitArtGO.transform;
            portraitArtRect.anchorMin = Vector2.zero;
            portraitArtRect.anchorMax = Vector2.one;
            float portraitArtInset = abilityButtonSize * 0.12f;
            portraitArtRect.offsetMin = new Vector2(portraitArtInset, portraitArtInset);
            portraitArtRect.offsetMax = new Vector2(-portraitArtInset, -portraitArtInset);
            var portrait = portraitArtGO.GetComponent<Image>();
            portrait.sprite = PlaceholderSprite.Get(new Color(1f, 0.84f, 0f));
            portrait.raycastTarget = false;

            // Monetisation: "skip cooldown for 3 coins" — reworked 2026-08-28 from a separate
            // "-3" button beside the ability icon into a coin badge overlaid on the icon itself
            // (per direct feedback), since Btn_skipcooldown.png's art slot was reassigned to the
            // Swap Character button. Sits in the icon's own top-right corner, poking out slightly
            // like a badge; shown only while the ability is on cooldown (see
            // GameplayHUD.HandleAbilityCooldownChanged) and disappears the instant it isn't. Its own
            // Button sits ABOVE PortraitArt in the hierarchy (later sibling = drawn on top) so a tap
            // on the badge is caught by IT, not the ability button underneath — tapping anywhere
            // else on the icon still just tries a normal activation (no-op while on cooldown, same
            // as Space), tapping the coin badge specifically pays to skip the wait instead.
            // Enlarged 0.4x -> 0.55x per direct feedback (2026-09-18) — it also spins continuously
            // while shown (GameplayHUD.Update) to draw the eye as "tappable," not just static.
            //
            // Spins a CHILD icon, not this root Button's own RectTransform (2026-09-18 fix, real
            // bug) — this root's pivot is (1,1) (its own top-right corner), needed so
            // anchoredPosition (10,10) pokes it out from the ability icon's corner correctly.
            // RectTransform rotation always happens around its own pivot, so rotating THIS
            // RectTransform swung the whole badge in an arc around that off-centre corner ("rotates
            // in circles," per direct feedback) instead of spinning in place like a coin. The root
            // stays a plain transparent raycast target (same "invisible root, art on a child"
            // convention `portraitButton`/`PortraitArt` already use above) sized/positioned via its
            // own pivot as before; a child `Icon` with a centred (0.5,0.5) pivot carries the actual
            // sprite and is what `GameplayHUD.RotateSkipCooldownCoinBadge` now spins.
            const float skipCoinBadgeSize = abilityButtonSize * 0.55f;
            var skipCooldownCoinButton = CreateIconButton("SkipCooldownCoinBadge", portraitButton.transform,
                null, skipCoinBadgeSize);
            var skipCoinBadgeRect = (RectTransform)skipCooldownCoinButton.transform;
            skipCoinBadgeRect.anchorMin = skipCoinBadgeRect.anchorMax = new Vector2(1f, 1f);
            skipCoinBadgeRect.pivot = new Vector2(1f, 1f);
            skipCoinBadgeRect.anchoredPosition = new Vector2(10f, 10f);
            skipCooldownCoinButton.GetComponent<Image>().color = Color.clear;

            var skipCoinBadgeIconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            skipCoinBadgeIconGO.transform.SetParent(skipCooldownCoinButton.transform, false);
            var skipCoinBadgeIconRect = (RectTransform)skipCoinBadgeIconGO.transform;
            skipCoinBadgeIconRect.anchorMin = Vector2.zero;
            skipCoinBadgeIconRect.anchorMax = Vector2.one;
            skipCoinBadgeIconRect.pivot = new Vector2(0.5f, 0.5f);
            skipCoinBadgeIconRect.offsetMin = Vector2.zero;
            skipCoinBadgeIconRect.offsetMax = Vector2.zero;
            var skipCoinBadgeIconImage = skipCoinBadgeIconGO.GetComponent<Image>();
            skipCoinBadgeIconImage.sprite = LoadUiSprite("Coin_UI.png");
            skipCoinBadgeIconImage.preserveAspect = true;
            skipCoinBadgeIconImage.raycastTarget = false;

            skipCooldownCoinButton.gameObject.SetActive(false);

            // Swap Character button (2026-08-27) — moved here from Pause (see GameplayHUD's own doc
            // comment) so it's reachable with a thumb mid-run on mobile without opening Pause first.
            // Directly above the ability icon, same X inset, own clusterIconSize (see that
            // constant's own doc comment for why this no longer shares abilityButtonSize), raised
            // by the ability icon's own real height + clusterSpacing.
            float swapBottomY = abilityBottomY + abilityButtonSize + clusterSpacing;
            var swapCharacterButton = CreateIconButton("SwapCharacterButton", safeArea.transform,
                LoadUiSprite("SwapCharacterIcon.png"), clusterIconSize);
            AnchorBottomRight((RectTransform)swapCharacterButton.transform, new Vector2(clusterIconSize, clusterIconSize),
                new Vector2(clusterIconInsetX, swapBottomY));

            // Web demo: no Locker button (no cosmetics).

            // Note: the coin-cost skip-cooldown button used to live here as its own "-3" button
            // beside the icon — replaced 2026-08-28 by the coin badge overlaid directly on the
            // ability icon itself (see its own comment further up, right after PortraitArt).
            float abilityCenterX = -abilityInsetX + abilityButtonSize / 2f; // distance from screen's right edge to the icon's horizontal centre

            // Web demo: no Watch Ad skip-cooldown button (no ads).

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
            // Shifted down and to the left again (inset 260/240 -> 235/210) per feedback, while
            // staying inside the yellow safe-area guide — this also opens up the headroom the new
            // Pause button (below) needs to sit above the diamond without crowding it.
            // Pulled back out again (235/210 -> 150/140) — same SafeArea double-inset root cause
            // documented on TimerText/ScoreText/clusterInsetX above: this diamond's centre inset was
            // tuned pre-SafeArea against the raw screen edge, and SafeArea's own shrink (added
            // 2026-08-28) now applies a second, redundant inward pull on top of it on any device
            // that reports a real notch/gesture-bar safe-area inset.
            // Shifted slightly further left (150 -> 115) per feedback ("shift the direction buttons
            // slightly left, include the Btn_pause") — Pause needs no separate change, its own
            // position is computed off the D-pad's Up button below and follows automatically.
            // Enlarged again (2026-09-14, per direct iPhone 11 playtest feedback — see
            // project_testing_device memory) from 98/82 to match StandardIconButtonSize (160), the
            // same size as Pause/Main Menu Play&Settings/every icon button family — "resize to the
            // same size as the buttons on pause, landing page etc, space out accordingly (there is
            // space)".
            //
            // First pass after that set dpadSpacing = dpadButtonSize (160/160), reasoned as "no
            // overlap at all" from the bounding-box math — WRONG in practice, confirmed by a device
            // screenshot showing a clearly visible gap between every arm, not the near-touching look
            // that math implied. Each button is only offset along ONE axis from the shared centre
            // (Up is (0,+spacing), Left is (-spacing,0), etc.), so two adjacent arms (e.g. Up/Left)
            // are DIAGONAL neighbours whose square bounding boxes only share a single point when
            // spacing==buttonSize — the round button ART inside each square never gets anywhere near
            // its neighbour's art at that ratio, regardless of the boxes touching. Per direct
            // correction ("bring the buttons closer, almost touching"), spacing dropped to 100
            // (well below buttonSize=160) so the boxes genuinely overlap (overlap = buttonSize -
            // spacing = 60, both axes at once for a diagonal pair) and the visible art reads as
            // close/almost touching.
            //
            // Fourth pass (2026-09-14, third device screenshot): that 100 overlapped MORE than
            // wanted ("space out slightly so the edges touch," i.e. back off toward a true touch,
            // not a deep overlap) — spacing raised 100->120 (overlap now 20, a light touch rather
            // than a 60-unit overlap). Separately, the diamond was still sitting on top of the maze
            // itself ("move the whole Dpad to the left so it is off the maze") — dpadInsetX pulled
            // in hard, 145->70, and per direct instruction that crossing the yellow safe-area guide
            // slightly is acceptable on mobile, dpadButtonSize was also shrunk a bit (160->140) to
            // help it clear the board without needing an even more extreme inset.
            //
            // Fifth pass (2026-09-14, fourth device screenshot — confirmed both fixes above landed
            // correctly: D-pad clear of the maze, ability cluster centred/dropped): "bring the
            // buttons slightly closer together; the corners can slightly touch. the rest is fine."
            // Only dpadSpacing needed a small nudge, 120->110 (overlap now 30, up from the light
            // 20-unit touch) — buttonSize/insetX untouched since position/size were explicitly
            // confirmed correct this round. dpadInsetY still kept at the same relative clearance
            // from dpadSpacing every pass has preserved (insetY-spacing=70): 110+70=180.
            const float dpadButtonSize = 140f;
            const float dpadSpacing = 110f;
            // Shared by iOS and Android. The Android test phone (1612x720) clips the Left button at
            // this value, but iOS is correct, so the Android nudge is applied at runtime instead —
            // see DirectionalPadController.AndroidShiftRight.
            const float dpadInsetX = 70f;
            const float dpadInsetY = 180f;
            Vector2 dpadCenter = new Vector2(dpadInsetX, dpadInsetY);

            var upButton = CreateButton("DPadUpButton", safeArea.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(upButton.transform.Find("DPadUpButton_Label").gameObject);
            AnchorBottomLeft((RectTransform)upButton.transform, new Vector2(dpadButtonSize, dpadButtonSize),
                dpadCenter + new Vector2(0f, dpadSpacing));

            var downButton = CreateButton("DPadDownButton", safeArea.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(downButton.transform.Find("DPadDownButton_Label").gameObject);
            AnchorBottomLeft((RectTransform)downButton.transform, new Vector2(dpadButtonSize, dpadButtonSize),
                dpadCenter + new Vector2(0f, -dpadSpacing));

            var leftButton = CreateButton("DPadLeftButton", safeArea.transform, string.Empty, Color.clear, out _);
            Object.DestroyImmediate(leftButton.transform.Find("DPadLeftButton_Label").gameObject);
            AnchorBottomLeft((RectTransform)leftButton.transform, new Vector2(dpadButtonSize, dpadButtonSize),
                dpadCenter + new Vector2(-dpadSpacing, 0f));

            var rightButton = CreateButton("DPadRightButton", safeArea.transform, string.Empty, Color.clear, out _);
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

            // Pause button — moved here from the right-side ability cluster (see that block's own
            // comment above) per feedback: "move the btn_pause to above the directional buttons."
            // Centred over the Up button specifically (not the diamond's overall centre — Left/Right
            // pull that centre off to the side), with clusterSpacing of clear padding above it.
            //
            // BUG FIX: AnchorBottomLeft's offset is the button's BOTTOM-LEFT CORNER, not its centre
            // — the first version of this code treated dpadCenter as if it were Up's own centre
            // point and used dpadButtonSize/2 for the top-edge math, which is only correct if the
            // offset were a centre. Since it's a corner, Up's real centre is offset by a FULL
            // dpadButtonSize/2 further right than dpadCenter.x, and Up's real top edge is a FULL
            // dpadButtonSize above its own anchor point, not half of one. That put Pause roughly
            // half a button-width too far left and overlapping Up instead of sitting cleanly above
            // it (caught via a gameplay screenshot). upButtonCenterX/upButtonTopEdge below compute
            // Up's true centre/top edge the same way its own AnchorBottomLeft call does.
            float upButtonCenterX = dpadCenter.x + dpadButtonSize / 2f;
            float upButtonTopEdge = dpadCenter.y + dpadSpacing + dpadButtonSize;
            // Sized to match the D-pad's own buttons (dpadButtonSize) rather than clusterButtonSize
            // — per feedback, Pause should read as the same size as Up/Down/Left/Right now that it
            // sits directly above them, not its old larger ability-cluster size.
            //
            // Gap above Up widened from the shared clusterSpacing (30) to its own dedicated
            // pauseAboveDpadGap (2026-09-14, direct iPhone 11 feedback: "lift the pause button
            // higher above the Dpad, because I accidentally kept hitting it when wanting to hit the
            // up direction") — clusterSpacing itself is untouched since it still governs unrelated
            // gaps elsewhere in this corner/the ability cluster. Only Pause's own vertical offset
            // needed to change; upButtonCenterX/upButtonTopEdge and Pause's size (dpadButtonSize)
            // already follow the D-pad's own enlargement above automatically.
            const float pauseAboveDpadGap = 110f;
            var pauseButton = CreateButton("PauseButton", safeArea.transform, string.Empty, new Color(0.35f, 0.35f, 0.38f), 28f, dpadButtonSize, out _);
            Object.DestroyImmediate(pauseButton.transform.Find("PauseButton_Label").gameObject);
            AnchorBottomLeft((RectTransform)pauseButton.transform, new Vector2(dpadButtonSize, dpadButtonSize),
                new Vector2(upButtonCenterX - dpadButtonSize / 2f, upButtonTopEdge + pauseAboveDpadGap));

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
            var reviveRoot = CreatePanel("RevivePromptOverlay", safeArea.transform, new Color(0f, 0f, 0f, 0.85f));

            var revivePanelArtGO = new GameObject("PanelArt", typeof(RectTransform), typeof(Image));
            revivePanelArtGO.transform.SetParent(reviveRoot.transform, false);
            var revivePanelArtRect = (RectTransform)revivePanelArtGO.transform;
            revivePanelArtRect.anchorMin = revivePanelArtRect.anchorMax = new Vector2(0.5f, 0.5f);
            revivePanelArtRect.sizeDelta = new Vector2(1300f, 731f); // 666x375 aspect, enlarged
            revivePanelArtRect.anchoredPosition = Vector2.zero;
            var revivePanelArtImage = revivePanelArtGO.GetComponent<Image>();
            revivePanelArtImage.sprite = PlaceholderSprite.Get(Color.clear);
            revivePanelArtImage.preserveAspect = true;

            // Sized and centred against the actual pixel-measured blank parchment area inside
            // "Revive Prompt panel background.png" (666x375 source): the readable interior runs
            // roughly x=[200,475]/y=[100,265], well short of the 666x375 art's own outer wood-sign
            // bounds — a plain visual read of the full sign (as earlier passes used) overestimated
            // how much of it is actually usable, leaving the buttons undersized with large dead
            // wood margins above Yes and below No. At the panel's current 1300x731 display scale
            // (x1.952 vs the 666x375 source) that interior maps to world x=[-435,+434]/
            // y=[+171,-151] — a 500-wide, ~324-tall content box centred at (0, +10) fills it with
            // the padding/spacing/button-height combo below (20 + 84 + 16 + 84 + 16 + 84 + 20 =
            // 324), matching the interior's real ~324-unit height instead of shrink-wrapping to a
            // much smaller auto-sized block floating in the middle of it.
            var reviveGroup = CreateVerticalGroup("Content", revivePanelArtGO.transform, 16f, 20);
            var reviveGroupRect = (RectTransform)reviveGroup.transform;
            reviveGroupRect.sizeDelta = new Vector2(500f, reviveGroupRect.sizeDelta.y);
            reviveGroupRect.anchoredPosition = new Vector2(0f, 10f);

            // No separate coin-icon/cost-text row anymore — the replacement panel art (see
            // RevivePromptPanel's own doc comment) bakes "Revive for 5 coins?" directly into its
            // bottom slot, so a duplicate runtime text row would just repeat it. costText is left
            // unwired below; RevivePromptController.Show() already null-checks it.
            var reviveButton = CreateButton("ReviveButton", reviveGroup.transform, string.Empty, new Color(0.2f, 0.65f, 0.3f), out _);
            Object.DestroyImmediate(reviveButton.transform.Find("ReviveButton_Label").gameObject);
            // Web demo: no Watch Ad revive option (no ads).
            var declineButton = CreateButton("DeclineButton", reviveGroup.transform, string.Empty, new Color(0.35f, 0.35f, 0.38f), out _);
            Object.DestroyImmediate(declineButton.transform.Find("DeclineButton_Label").gameObject);
            // reviveGroup's VerticalLayoutGroup has childControlHeight=false (see CreateVerticalGroup),
            // so a button's LayoutElement.preferredHeight (set inside CreateButton) is never actually
            // applied — the same CreateImage-args-are-inert pattern found elsewhere in this file.
            // Height set explicitly here instead; width still comes from the layout group
            // (childControlWidth=true), so only .y needs overriding.
            const float reviveButtonHeight = 84f; // was 130 -> 90 -> 62 -> 84 — 62 undershot the sign's real ~165px-tall (image-space) interior badly enough to leave large dead wood margins above Yes and below No; 84 (paired with the 500-wide/16-spacing/20-padding group above) fills that measured interior evenly instead
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
            so.FindProperty("swapCharacterButton").objectReferenceValue = swapCharacterButton;
            so.FindProperty("pauseButton").objectReferenceValue = pauseButton;
            so.FindProperty("powerPelletTimerBar").objectReferenceValue = powerBarGO;
            so.FindProperty("powerPelletTimerFill").objectReferenceValue = powerFillImage;
            so.FindProperty("chainCounterRoot").objectReferenceValue = chainRoot;
            so.FindProperty("chainCounterText").objectReferenceValue = chainText;
            so.FindProperty("revivePrompt").objectReferenceValue = revivePrompt;
            so.FindProperty("skipCooldownCoinButton").objectReferenceValue = skipCooldownCoinButton;
            so.FindProperty("skipCooldownCoinIcon").objectReferenceValue = skipCoinBadgeIconRect;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ---- Pause Menu -------------------------------------------------------------------------

        /// <summary>Rebuilt (2026-08-27) to match a new mockup exactly — same self-contained,
        /// bake-everything-at-construction-time convention BuildLevelFailed uses (nothing left for
        /// ArtWiringBuilder to wire), and in fact an almost identical layout: Bg_LevelSelect.png
        /// background, Logo.png top-left, a wood-sign banner (Pause.png — not the old square
        /// Paused.png card, which is no longer referenced anywhere in this project), and the exact
        /// same 4-button Play/Skip/Settings/Quit row BuildLevelFailed uses, at the exact same
        /// positions. See PauseMenuController's own doc comment for what each button does and how
        /// it differs from Level Failed's Play (resume, not restart) — Skip/Settings/Quit are wired
        /// identically. The old 5-button Resume/SwapCharacter/Restart/Settings/Quit design (built on
        /// Paused.png's baked-in rows) is discarded entirely, not patched.</summary>
        private static GameObject BuildPauseMenu(Transform canvasTransform)
        {
            var root = CreatePanel("PauseOverlay", canvasTransform, Color.black);
            root.GetComponent<Image>().sprite = LoadUiSprite("Bg_LevelSelect.png");

            var logoImageGO = new GameObject("LogoImage", typeof(RectTransform), typeof(Image));
            logoImageGO.transform.SetParent(root.transform, false);
            var logoImage = logoImageGO.GetComponent<Image>();
            logoImage.sprite = LoadUiSprite("Logo.png");
            logoImage.preserveAspect = true;
            AnchorTopLeft((RectTransform)logoImageGO.transform, new Vector2(LogoImageSize, LogoImageSize), new Vector2(100f, -40f));

            // Pause.png was reworked by the artist (2026-08-31) to drop its wood-sign frame/
            // background entirely — it's now bare "Pause" lettering on a transparent 418x124
            // canvas (~3.37:1), so it's rendered directly with no backboard/frame composited
            // behind it. Sized off a shared banner height (HeaderBannerHeight) with GameOver.png
            // below (BuildLevelFailed) so both text-only screen banners read at a consistent scale.
            var signGO = new GameObject("TitleImage", typeof(RectTransform), typeof(Image));
            signGO.transform.SetParent(root.transform, false);
            var signImage = signGO.GetComponent<Image>();
            signImage.sprite = LoadUiSprite("Pause.png");
            signImage.preserveAspect = true;
            AnchorTopCenter((RectTransform)signGO.transform, new Vector2(HeaderBannerHeight * (418f / 124f), HeaderBannerHeight), new Vector2(0f, -300f));

            // Same 4-button layout LevelFailedController used to have — Play/Skip bottom-left pair,
            // Settings/Quit bottom-right pair, identical StandardIconButtonSize + insets. Pause
            // keeps all 4 (unaffected by LevelFailed's own 2026-08-30 "GAME OVER" redesign, which
            // dropped Skip and re-iconed Quit to Home — see BuildLevelFailed's own doc comment).
            // Bottom inset raised from 110 to 110+BannerAdBottomClearance (2026-09-11) to leave
            // clear room below for AdManager's banner ad (see PauseMenuController's own doc comment
            // — the banner is a native overlay, not something this Canvas can lay out around, so
            // this row just needs to stay clear of the strip it'll occupy).
            const float pauseButtonBottomInset = 110f + BannerAdBottomClearance;
            var playButton = CreateIconButton("PlayButton", root.transform, LoadUiSprite("Btn_play.png"), StandardIconButtonSize);
            AnchorBottomLeft((RectTransform)playButton.transform, new Vector2(StandardIconButtonSize, StandardIconButtonSize), new Vector2(150f, pauseButtonBottomInset));

            var skipButton = CreateIconButton("SkipButton", root.transform, LoadUiSprite("Btn_skip.png"), StandardIconButtonSize);
            AnchorBottomLeft((RectTransform)skipButton.transform, new Vector2(StandardIconButtonSize, StandardIconButtonSize), new Vector2(150f + StandardIconButtonSize + 30f, pauseButtonBottomInset));

            var quitButton = CreateIconButton("QuitButton", root.transform, LoadUiSprite("Btn_quit.png"), StandardIconButtonSize);
            AnchorBottomRight((RectTransform)quitButton.transform, new Vector2(StandardIconButtonSize, StandardIconButtonSize), new Vector2(-150f, pauseButtonBottomInset));

            var settingsButton = CreateIconButton("SettingsButton", root.transform, LoadUiSprite("Btn_settings.png"), StandardIconButtonSize);
            AnchorBottomRight((RectTransform)settingsButton.transform, new Vector2(StandardIconButtonSize, StandardIconButtonSize), new Vector2(-150f - StandardIconButtonSize - 30f, pauseButtonBottomInset));

            var controller = root.AddComponent<PauseMenuController>();
            var so = new SerializedObject(controller);
            so.FindProperty("playButton").objectReferenceValue = playButton;
            so.FindProperty("skipButton").objectReferenceValue = skipButton;
            so.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            so.FindProperty("quitButton").objectReferenceValue = quitButton;
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

        /// <summary>Rebuilt (2026-08-27) to a new mockup — a single row of 4 icons (Music,
        /// Leaderboards, Character Story, Policies) instead of the earlier 4x2 grid. Shop/Worlds/
        /// RemoveAds moved to the new Shop hub (ShopController) and Restore Purchases moved to
        /// CoinPurchaseScreen — see SettingsPanel.cs's own doc comment for the full breakdown.
        /// Discards the old grid layout entirely, not just the 4 dropped icons.</summary>
        private static GameObject BuildSettingsPanel(Transform canvasTransform)
        {
            var root = CreatePanel("SettingsOverlay", canvasTransform, Color.black);
            StretchFull((RectTransform)root.transform);
            ApplyDimmedLandingBackground(root);

            // No LogoImage on this screen (removed per explicit request) — landing.png's own
            // baked-in "FARM FURY ARCADE" wordmark already reads through the dimmed backdrop, and
            // the separate Logo.png badge duplicated it in the same top-left corner.

            CreateHeaderSign(root.transform, LoadUiSprite("SettingsSign.png"));

            var closeButton = CreateRoundBackButton(root.transform);
            closeButton.GetComponent<Image>().sprite = LoadUiSprite("Btn_back.png");

            // Single row of 4 columns — matches the new mockup exactly (was a 4x2 grid with 4 more
            // icons that have since moved elsewhere). Icons enlarged 1.5x off the shared
            // StandardIconButtonSize (160 -> 240) per direct feedback that they read as too small
            // — a local IconSize rather than changing StandardIconButtonSize itself, since that
            // constant is shared by many unrelated screens (Level Complete's DoubleCoinsButton,
            // Cosmetics hub, Hat/Trail purchase, etc.) this change shouldn't touch. Same IconSize
            // used on the Shop hub's own row (BuildShopOverlay) so the two stay uniform with each
            // other, matching the "equally sized and spaced" requirement.
            float iconSize = StandardIconButtonSize * 1.5f;
            var gridGO = new GameObject("SettingsGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGO.transform.SetParent(root.transform, false);
            var gridRect = (RectTransform)gridGO.transform;
            gridRect.anchorMin = gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            float cellSpacing = 77f;
            // Web demo: 2 icons (Music, Character Story) - Leaderboards and Policies removed.
            float gridWidth = 2 * iconSize + 1 * cellSpacing;
            gridRect.sizeDelta = new Vector2(gridWidth + 100f, iconSize + 60f);
            // Centered in the vertical space between the header's bottom edge (365px from screen
            // top, from StandardHeaderSignOffset/Size above) and the screen's own bottom edge —
            // same "sit nicely middle aligned" convention the old 2-row grid used, recomputed for
            // a single row's own (shorter) height.
            gridRect.anchoredPosition = new Vector2(0f, -183f);
            var grid = gridGO.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(iconSize, iconSize);
            grid.spacing = new Vector2(cellSpacing, cellSpacing);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            var musicButton = CreateIconButton("MusicCell", gridGO.transform, LoadUiSprite("Btn_music-remove.png"), iconSize);
            var characterStoryButton = CreateIconButton("CharacterStoryCell", gridGO.transform, LoadUiSprite("Btn_CharacterStory.png"), iconSize);

            var controller = root.AddComponent<SettingsPanel>();
            var so = new SerializedObject(controller);
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("musicButton").objectReferenceValue = musicButton;
            so.FindProperty("musicButtonIcon").objectReferenceValue = musicButton.GetComponent<Image>();
            so.FindProperty("characterStoryButton").objectReferenceValue = characterStoryButton;
            // leaderboardsScreen/characterStoryScreen/policiesScreen are wired later in BuildAll's
            // WireCrossReferences, once those screens actually exist.
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // Warm gold (active) / brown (inactive) tab-tint convention — matches
        // CharacterStoryScreen's own copies of these same values, which SelectTab re-applies on
        // every tap at runtime; this pair only sets each tab's initial (Story-selected) tint at
        // build time.
        private static readonly Color TabActiveColor = new Color(0.85f, 0.65f, 0.2f);
        private static readonly Color TabInactiveColor = new Color(0.35f, 0.28f, 0.18f);

        /// <summary>Real content screen for "Btn_CharacterStory" (2026-08-21 follow-up — was a
        /// "Coming Soon" placeholder until the actual narrative/character copy was written). Matches
        /// the rest of the Settings-family redesign: dimmed Landing_Opacity.png backdrop, no
        /// top-left LogoImage (removed per request — every other screen in this family still has
        /// one, this is the one deliberate exception), and Btn_back kept at its existing
        /// bottom-right position (CreateRoundBackButton's default).
        ///
        /// Rebuilt (2026-09-09, per direct feedback) from one long continuous scrollable list into 3
        /// tabs — Story / How to Play / Characters — each its own independent ScrollRect, switched
        /// by CharacterStoryScreen.SelectTab. The single-list version read as "a very long scrolling
        /// list" once the How to Play section (GameplayTopics) was added on top of the narrative
        /// intro and all 8 character rows. Both the intro copy and the per-character blurbs are
        /// populated at runtime by CharacterStoryScreen (DataManager isn't available in Edit mode);
        /// this method only builds the empty layout and wires the tab/content references.</summary>
        private static GameObject BuildCharacterStoryPlaceholder(Transform canvasTransform, GameObject characterSelectCardPrefab)
        {
            var root = CreatePanel("CharacterStoryScreen", canvasTransform, Color.black);
            ApplyDimmedLandingBackground(root);

            const float tabBarTopMargin = 40f;
            const float tabBarHeight = 70f;
            const float tabBarToScrollGap = 20f;
            const float scrollTopMargin = tabBarTopMargin + tabBarHeight + tabBarToScrollGap;
            const float scrollBottomMargin = 140f; // clears the back button

            var tabBar = CreateHorizontalGroup("TabBar", root.transform, 12f);
            AnchorTopCenter((RectTransform)tabBar.transform, new Vector2(1700f, tabBarHeight), new Vector2(0f, -tabBarTopMargin));

            var storyTabButton = CreateButton("StoryTabButton", tabBar.transform, "Story", TabActiveColor, 28f, tabBarHeight, out var storyTabLabel);
            var howToPlayTabButton = CreateButton("HowToPlayTabButton", tabBar.transform, "How to Play", TabInactiveColor, 28f, tabBarHeight, out var howToPlayTabLabel);
            // Combos tab (2026-09-11) — inserted between How to Play and Characters, matching
            // CharacterStoryScreen's own tab-index order (0 Story / 1 How to Play / 2 Combos /
            // 3 Characters / 4 Cosmetics). TabBar's HorizontalLayoutGroup (childControlWidth +
            // childForceExpandWidth, see CreateHorizontalGroup) auto-divides its fixed 1700px width
            // across however many buttons it holds, so adding a 5th here needs no width retuning.
            var combosTabButton = CreateButton("CombosTabButton", tabBar.transform, "Combos", TabInactiveColor, 28f, tabBarHeight, out var combosTabLabel);
            var charactersTabButton = CreateButton("CharactersTabButton", tabBar.transform, "Characters", TabInactiveColor, 28f, tabBarHeight, out var charactersTabLabel);
            var cosmeticsTabButton = CreateButton("CosmeticsTabButton", tabBar.transform, "Cosmetics", TabInactiveColor, 28f, tabBarHeight, out var cosmeticsTabLabel);
            // Robots tab (2026-09-18) — appended after Cosmetics rather than renumbered in among the
            // existing 5, so every earlier tab's index (and any existing wiring referencing it)
            // stays unchanged. A literal duplicate of CharactersTabButton's own pill, per direct
            // feedback ("duplicate the character pill").
            var robotsTabButton = CreateButton("RobotsTabButton", tabBar.transform, "Robots", TabInactiveColor, 28f, tabBarHeight, out var robotsTabLabel);

            // Font sizing fix (2026-09-16, per direct feedback: "on a mobile device the writing is
            // very small" for these 5 tab pills) — CreateButton's fixed 28pt was small relative to
            // the 70px-tall plaque button, especially the shorter labels ("Story", "Combos") which
            // had plenty of unused vertical space above/below the glyphs. Auto-sizing (below,
            // fontSizeMax raised well past the old flat 28) lets each label grow to fill its own
            // plaque as much as its own text length allows, while fontSizeMin keeps the longest
            // label ("How to Play") shrinking to fit rather than ever overflowing the plaque's
            // rounded border or the side padding set below it.

            // Real Btn_plaque.png background (2026-09-10), replacing the flat solid-colour "pill"
            // squares CreateButton's placeholder sprite drew — same Image.Type.Sliced + border
            // technique StyleLegalPlaqueButton/CoinPurchaseScreen's Restore Purchases button already
            // use on this exact source file, so the plaque's rounded ends stay undistorted at
            // whatever width TabBar's HorizontalLayoutGroup stretches each of the 3 equal-width tabs
            // to. Border proportioned smaller than the taller Legal/Restore buttons' (90,70,90,70)
            // since this row is only tabBarHeight (70) tall — an unscaled copy of that border would
            // overlap itself vertically. Labels are still tinted via SelectTab's existing
            // TabActiveColor/TabInactiveColor .color assignment (unchanged runtime code — it targets
            // whatever Image is on button.targetGraphic regardless of sprite) and locked to a
            // single centred, non-wrapping line with side padding so "How to Play" can never
            // overlap the plaque's own rounded edges.
            var tabPlaqueBorder = new Vector4(40f, 24f, 40f, 24f);
            foreach (var (tabButton, tabLabel) in new[]
                     {
                         (storyTabButton, storyTabLabel), (howToPlayTabButton, howToPlayTabLabel),
                         (combosTabButton, combosTabLabel),
                         (charactersTabButton, charactersTabLabel), (cosmeticsTabButton, cosmeticsTabLabel),
                         (robotsTabButton, robotsTabLabel),
                     })
            {
                var tabImage = tabButton.GetComponent<Image>();
                tabImage.sprite = LoadUiSprite("Btn_plaque.png", tabPlaqueBorder);
                tabImage.type = Image.Type.Sliced;

                tabLabel.alignment = TextAlignmentOptions.Center; // horizontally AND vertically centred
                tabLabel.enableWordWrapping = false;
                // Shrink-to-fit within [28,44]pt instead of a flat 28pt — lets "Story"/"Combos" grow
                // well past the old size while "How to Play" (the longest label) shrinks just enough
                // to stay inside its own padded box. Truncate (not Overflow) plus the unchanged 28px
                // side padding below guarantees text can never spill past the plaque's rounded edge
                // or into a neighbouring tab's art.
                tabLabel.enableAutoSizing = true;
                tabLabel.fontSizeMin = 28f;
                tabLabel.fontSizeMax = 44f;
                tabLabel.overflowMode = TextOverflowModes.Truncate;
                var labelRect = (RectTransform)tabLabel.transform;
                labelRect.offsetMin = new Vector2(28f, labelRect.offsetMin.y);
                labelRect.offsetMax = new Vector2(-28f, labelRect.offsetMax.y);
            }

            GameObject BuildTabScrollView(string name, out Transform content, bool centerContentVertically = false)
            {
                var scrollRect = CreateVerticalScrollView(name, root.transform, out content);
                var scrollRT = (RectTransform)scrollRect.transform;
                scrollRT.anchorMin = new Vector2(0f, 0f);
                scrollRT.anchorMax = new Vector2(0f, 1f);
                scrollRT.pivot = new Vector2(0f, 0.5f);
                scrollRT.anchoredPosition = new Vector2(100f, (scrollBottomMargin - scrollTopMargin) / 2f);
                scrollRT.sizeDelta = new Vector2(1700f, -(scrollTopMargin + scrollBottomMargin));

                if (centerContentVertically)
                {
                    // CreateVerticalScrollView anchors Content to the TOP of its viewport (pivot
                    // (0.5,1)) — correct for a genuinely scrollable list, but for the Story tab's
                    // single short intro box it left the whole lower half of the tab visibly empty
                    // (ContentSizeFitter only grows Content to fit its own ~300px of children, which
                    // then sits pinned to the top of a much taller viewport). Re-anchoring Content to
                    // the viewport's vertical middle instead centres that box in the available page
                    // area; ContentSizeFitter still drives its height the same way either way, this
                    // only changes where that sized rect sits.
                    var contentRect = (RectTransform)content;
                    contentRect.anchorMin = new Vector2(0f, 0.5f);
                    contentRect.anchorMax = new Vector2(1f, 0.5f);
                    contentRect.pivot = new Vector2(0.5f, 0.5f);
                    contentRect.anchoredPosition = Vector2.zero;
                }

                return scrollRect.gameObject;
            }

            var storyScrollView = BuildTabScrollView("StoryScrollView", out var storyContainer, centerContentVertically: true);

            // Framed intro box — sole content of the Story tab now (used to be the first item in one
            // shared list with everything else below it). No dedicated wood-sign art exists for a
            // box this shape/size, so the "border" is a plain two-layer Image composition (an outer
            // gold border colour with a slightly inset, darker semi-transparent inner panel) rather
            // than uploaded art — same PlaceholderSprite.Get(color) convention used everywhere else
            // in this project a visual is needed before real art exists. Its own sizeDelta is set
            // explicitly (not left to a LayoutElement) since storyContainer's VerticalLayoutGroup has
            // childControlHeight/Width = false (CreateVerticalScrollView's convention) and reads
            // each child's raw sizeDelta directly.
            var introBorder = CreateImage("IntroBorder", storyContainer, new Color(0.70f, 0.55f, 0.20f), CharacterStoryScreen.RowWidth, 260f);
            ((RectTransform)introBorder.transform).sizeDelta = new Vector2(CharacterStoryScreen.RowWidth, 260f);

            var introBackground = CreateImage("IntroBackground", introBorder.transform, new Color(0.08f, 0.06f, 0.03f, 0.82f), CharacterStoryScreen.RowWidth - 12f, 248f);
            var introBgRect = (RectTransform)introBackground.transform;
            introBgRect.anchorMin = introBgRect.anchorMax = new Vector2(0.5f, 0.5f);
            introBgRect.pivot = new Vector2(0.5f, 0.5f);
            introBgRect.sizeDelta = new Vector2(CharacterStoryScreen.RowWidth - 12f, 248f);
            introBgRect.anchoredPosition = Vector2.zero;

            var introText = CreateText("IntroText", introBackground.transform, string.Empty, 26f, TextAlignmentOptions.Center, 220f, new Color(0.97f, 0.93f, 0.82f));
            var introTextRect = (RectTransform)introText.transform;
            introTextRect.anchorMin = introTextRect.anchorMax = new Vector2(0.5f, 0.5f);
            introTextRect.pivot = new Vector2(0.5f, 0.5f);
            introTextRect.sizeDelta = new Vector2(CharacterStoryScreen.RowWidth - 12f - 68f, 220f);
            introTextRect.anchoredPosition = Vector2.zero;

            var howToPlayScrollView = BuildTabScrollView("HowToPlayScrollView", out var howToPlayContainer);
            var combosScrollView = BuildTabScrollView("CombosScrollView", out var combosContainer);
            var charactersScrollView = BuildTabScrollView("CharactersScrollView", out var charactersContainer);
            var cosmeticsScrollView = BuildTabScrollView("CosmeticsScrollView", out var cosmeticsContainer);
            var robotsScrollView = BuildTabScrollView("RobotsScrollView", out var robotsContainer);

            var closeButton = CreateRoundBackButton(root.transform);
            closeButton.GetComponent<Image>().sprite = LoadUiSprite("Btn_back.png");

            var story = root.AddComponent<CharacterStoryScreen>();
            SetRefs(story,
                ("charactersContainer", charactersContainer),
                ("cosmeticsContainer", cosmeticsContainer),
                ("robotsContainer", robotsContainer),
                ("cardPrefab", characterSelectCardPrefab),
                ("closeButton", closeButton),
                ("introText", introText),
                ("introBorderRect", introBorder.transform),
                ("introBackgroundRect", introBackground.transform),
                ("howToPlayContainer", howToPlayContainer),
                ("combosContainer", combosContainer),
                ("storyTabButton", storyTabButton), ("storyTabContent", storyScrollView),
                ("howToPlayTabButton", howToPlayTabButton), ("howToPlayTabContent", howToPlayScrollView),
                ("combosTabButton", combosTabButton), ("combosTabContent", combosScrollView),
                ("charactersTabButton", charactersTabButton), ("charactersTabContent", charactersScrollView),
                ("cosmeticsTabButton", cosmeticsTabButton), ("cosmeticsTabContent", cosmeticsScrollView),
                ("robotsTabButton", robotsTabButton), ("robotsTabContent", robotsScrollView));

            // How to Play icons — one per GameplayTopics entry, same order (Coins/Scoring & Stars/
            // Power Crops & Robot Chains). Reuses existing art rather than commissioning anything
            // new: the coin pickup icon, a filled score star, and a real rare power-pellet sprite.
            // The old 4th entry (a combo banner standing in for "abilities/combos" as a concept) is
            // gone along with GameplayTopics' own 4th row — the Combos tab below now covers that
            // ground for real, one real icon per actual combo instead of one icon standing in for
            // all of them.
            var gameplayTopicIcons = new[]
            {
                LoadUiSprite("Coin_UI.png"),
                LoadUiSprite("ScoreStar.png"),
                ConfigureAndLoadCosmeticChromeSprite("Assets/_Project/Sprites/Environment/RarePellets_sunflower.png"),
            };
            var iconsSO = new SerializedObject(story);
            var iconsProp = iconsSO.FindProperty("gameplayTopicIcons");
            iconsProp.arraySize = gameplayTopicIcons.Length;
            for (int i = 0; i < gameplayTopicIcons.Length; i++)
            {
                iconsProp.GetArrayElementAtIndex(i).objectReferenceValue = gameplayTopicIcons[i];
            }

            // Combos tab icons (2026-09-11) — one real Combo_*.png banner per CharacterStoryScreen.
            // ComboEntries entry, same order and same art ComboHypeScreen's own comboBannerEntries
            // table uses (see BuildComboHypeScreen further up) — reusing that exact art means a
            // player who's seen a combo trigger in-maze recognises the same picture here.
            var comboIcons = new[]
            {
                LoadUiSprite("Combo_Featherstorm.png"),
                LoadUiSprite("Combo_EarthquakeRoll.png"),
                LoadUiSprite("Combo_SkipShatter.png"),
                LoadUiSprite("Combo_DoubleSlam.png"),
                LoadUiSprite("Combo_CrossFire.png"),
                LoadUiSprite("Combo_IronStampede.png"),
                LoadUiSprite("Combo_KicknRoll.png"),
                LoadUiSprite("Combo_FullFury.png"),
            };
            var comboIconsProp = iconsSO.FindProperty("comboIcons");
            comboIconsProp.arraySize = comboIcons.Length;
            for (int i = 0; i < comboIcons.Length; i++)
            {
                comboIconsProp.GetArrayElementAtIndex(i).objectReferenceValue = comboIcons[i];
            }

            // Cosmetics tab entries — 2026-09-11: swapped from the Shop's price-baked purchase art
            // (sombrero_price.png etc.) to new plain icon-only art dropped under Sprites/UI/ with no
            // price baked in, since this tab is informational, not a purchase surface. Blurb text
            // lives in CharacterStoryScreen itself (CosmeticBlurbs, keyed by this same displayName).
            var cosmeticEntryData = new (string displayName, Sprite icon)[]
            {
                ("Sombrero", LoadUiSprite("Sombrero.png")),
                ("Baseball Cap", LoadUiSprite("BaseballHat.png")),
                ("Cowboy Hat", LoadUiSprite("CowboyHat.png")),
                ("Chef Hat", LoadUiSprite("ChefHat.png")),
                ("Crown", LoadUiSprite("CrownHat.png")),
                ("Rainbow Ribbon", LoadUiSprite("Ribbon.png")),
                ("Sparkle Dust", LoadUiSprite("SparkleDust.png")),
                ("Corn Husk Trail", LoadUiSprite("CornHusk.png")),
                ("Ember Trail", LoadUiSprite("Ember.png")),
                // Confetti / Bubbles (2026-09-11) — note the on-disk filename is lowercase
                // "bubbles.png", unlike every other icon-only UI sprite here.
                ("Confetti Trail", LoadUiSprite("Confetti.png")),
                ("Bubbles Trail", LoadUiSprite("bubbles.png")),
                // Machine Cosmetics (2026-09-16) — corrected to their real dedicated icon-only art
                // (CluckyTruck/BessieTruck/HoraceTruck.png, Sprites/UI/ — wood-sign badges, same
                // style as the World Purchase shields) after an earlier pass wrongly reused the
                // Shop's price-baked preview art here. Same "plain icon-only art, no price baked in"
                // convention every other entry on this informational tab already uses.
                ("Clucky's Tractor", LoadUiSprite("CluckyTruck.png")),
                ("Bessie's Milk Tanker", LoadUiSprite("BessieTruck.png")),
                ("Horace's Hay Baler", LoadUiSprite("HoraceTruck.png")),
            };
            var cosmeticEntriesProp = iconsSO.FindProperty("cosmeticEntries");
            cosmeticEntriesProp.arraySize = cosmeticEntryData.Length;
            for (int i = 0; i < cosmeticEntryData.Length; i++)
            {
                var element = cosmeticEntriesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("displayName").stringValue = cosmeticEntryData[i].displayName;
                element.FindPropertyRelative("icon").objectReferenceValue = cosmeticEntryData[i].icon;
            }
            iconsSO.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        /// <summary>Swaps a plain CreateButton's placeholder background for the real Btn_plaque.png
        /// art (Sliced + border, so its rounded ends survive whatever width the parent layout group
        /// stretches it to) and locks its label to a single centred, non-wrapping line so it can
        /// never overlap the plaque's own rounded edges regardless of button width. Also fixes the
        /// button's real height explicitly — buttonGroup (CreateVerticalGroup) has
        /// childControlHeight=false, so the height passed to CreateButton's own LayoutElement is
        /// otherwise silently ignored (same gotcha documented throughout this project, e.g.
        /// CharacterStoryScreen.BuildRow) and the button would render at Unity's 100-tall
        /// GameObject default instead of the requested height.</summary>
        private static void StyleLegalPlaqueButton(Button button, TextMeshProUGUI label, Vector4 border, float height)
        {
            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);

            var image = button.GetComponent<Image>();
            image.sprite = LoadUiSprite("Btn_plaque.png", border);
            image.type = Image.Type.Sliced;

            label.alignment = TextAlignmentOptions.Center; // horizontally AND vertically centred
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
        }

        // ---- Shop / Cosmetics (2026-08-20 redesign — matches the new Shop/Cosmetics/Hats/Trails
        // mockups exactly; replaces the old plain-text-button coin grid and the tab-based
        // CosmeticStoreScreen entirely) ----------------------------------------------------------

        private const string UISpriteFolder = "Assets/_Project/Sprites/UI";
        private const string CosmeticsChromeFolder = "Assets/_Project/Sprites/Cosmetics";
        private const string CharacterSpriteFolder = "Assets/_Project/Sprites/Characters";
        private const string MazeThemeArtFolder = "Assets/_Project/Sprites/Cosmetics/CosmeticType_MazeTheme";

        /// <summary>Configures a texture as a Sprite (PPU = its own width, same convention every
        /// other sprite-importer pass in this project uses) and loads it. Self-contained rather
        /// than depending on ArtWiringBuilder having already run, since these screens are built
        /// earlier in the standard Phase2->Phase5->ArtWiringBuilder rebuild chain — mirrors
        /// CosmeticWiringBuilder.ConfigureAndLoadSprite's same self-contained approach.</summary>
        private static Sprite ConfigureAndLoadCosmeticChromeSprite(string path) => ConfigureAndLoadCosmeticChromeSprite(path, Vector4.zero);

        /// <summary>Border overload — sets the sprite's 9-slice border (Sprite.border, (left,bottom,
        /// right,top) in source pixels) so a caller can use Image.Type.Sliced to stretch just the
        /// straight middle section of a pill/plaque shape to any width while keeping its rounded
        /// end caps undistorted, instead of being locked to the source file's own aspect ratio the
        /// way Image.Type.Simple + preserveAspect requires. Used by the Restore Purchases plaque,
        /// which needs to size itself to whatever width its label text actually measures at
        /// runtime (see CoinPurchaseScreen.ResizeRestoreButtonToFitLabel) — a fixed-aspect box could
        /// never do that without either squashing the art or leaving dead space.</summary>
        private static Sprite ConfigureAndLoadCosmeticChromeSprite(string path, Vector4 border)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[Phase5ProjectBuilder] Expected sprite not found, skipping: {path}");
                return null;
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.GetSourceTextureWidthAndHeight(out int width, out int _);
            importer.spritePixelsPerUnit = width > 0 ? width : 100;
            importer.spriteBorder = border;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---- Cross-screen layout standards (2026-08-20 consistency pass) ----------------------
        // Applies to the family of screens rebuilt in this redesign wave (Main Menu, Level Select,
        // Settings, Shop, Cosmetics hub/purchase, Level Complete, Leaderboards, Character Story) —
        // every square icon-style button in that family is the same size, every text/wood-sign
        // header is the same size/position (Level Select's "SELECT LEVEL" sign is the benchmark),
        // and every screen using the landing.png ("farmfuryposter") backdrop shows it dimmed to
        // 50% opacity rather than full strength. Screens with bespoke non-square button art (Pause,
        // Choose Character, Level Failed, Character Roster, Gameplay HUD) predate this pass and
        // aren't touched — forcing their wide banner-style buttons into a square box would squash
        // them, the exact bug this same session already fixed once for DoubleCoins.png.
        private const float StandardIconButtonSize = 160f;

        // Reserved bottom strip (2026-09-11 monetisation pass) for AdManager's banner ad on Pause/
        // Level Failed (see PauseMenuController/LevelFailedController's own OnEnable/OnDisable) —
        // a LevelPlay banner renders as a native overlay anchored to the screen's bottom edge, NOT
        // a Unity UI element inside this Canvas, so nothing here can visually contain it; this
        // constant only exists to push this project's own button row (and Level Failed's Insert
        // Coin row) up far enough that neither one visually clashes with it. Sized generously above
        // a standard/adaptive banner's real height on any device (typically 50-100dp) since exact
        // on-device dp-to-canvas-unit conversion varies by device and isn't worth chasing precisely
        // here — err on the side of more clearance.
        private const float BannerAdBottomClearance = 160f;

        // Shared top-left LogoImage box (Pause, Level Complete, New Character Unlock, Choose
        // Character) — every call site used a non-square 300x170 box despite Logo.png itself being
        // a square 500x500 source. ArtWiringBuilder.SetImageSprite always sets Image.Type.Sliced,
        // which ignores preserveAspect (the same box-aspect-must-match-the-art bug documented
        // throughout this project — Coin Balance Chip, Revive Prompt panel, Level Complete panel,
        // etc.), so the square logo was actually being non-uniformly squashed into that wide box
        // every time it rendered. 170 (the box's constrained/height dimension, i.e. the size
        // preserveAspect WOULD have fit the image to if it weren't being ignored) is the true prior
        // visual size; enlarged 1.5x per feedback per a gameplay screenshot review.
        private const float LogoImageSize = 170f * 1.5f;

        // Shared height for the two now-backboard-less text-only wood-sign banners (Pause.png,
        // GameOver.png) — both lost their frame/background art on 2026-08-31, leaving bare
        // differently-proportioned lettering (Pause ~3.37:1, GameOver ~4.80:1). Sizing each off
        // one common height (rather than each screen's own hand-picked box) is what makes the two
        // screens' banners actually read as the same visual scale despite the differing aspects.
        private const float HeaderBannerHeight = 130f;
        // Sized to guarantee real clearance above BuildSettingsPanel's icon grid, not just eyeballed
        // against the benchmark: with the grid's own layout math (StandardIconButtonSize=160,
        // cellSpacing=50 -> gridHeight=370, container sizeDelta.y=gridHeight+60=430, anchoredPosition
        // Y=-60 on a (0.5,0.5)-pivoted rect in a 1920x1080 canvas), the grid's own top edge sits
        // exactly 385px below the screen top. The header box's top offset is 55px below the screen
        // top (AnchorTopCenter, pivot.y=1), so its bottom edge sits (55 + height)px down — this
        // height (310) keeps that bottom edge at 365px, a real verified 20px gap above the grid's
        // 385px top edge (comfortably over the requested 8px minimum). The previous 692x390 box put
        // the bottom edge at 445px, 60px PAST the grid's top edge — a real, computed overlap, not a
        // rendering glitch (confirmed against a screenshot showing the sign covering row 1's icons).
        // Width (550) keeps the box's aspect (550/310 ~= 1.77) matching the sign art's own aspect
        // (666x375 ~= 1.776), so preserveAspect doesn't waste any of the box on empty space.
        private static readonly Vector2 StandardHeaderSignSize = new Vector2(550f, 310f);
        private static readonly Vector2 StandardHeaderSignOffset = new Vector2(0f, -55f);

        /// <summary>Adds landing.png at the standard dimmed opacity as a CHILD layer on top of the
        /// root's own opaque black Image — same "poster" background every screen in this redesign
        /// wave shares, faded so it reads as a backdrop rather than competing with the content on
        /// top of it.
        ///
        /// Deliberately does NOT set the sprite/color directly on the root's own Image (an earlier
        /// version did, and the dimming silently read as a no-op): every one of these 6 screens is
        /// an overlay shown via plain SetActive on top of whatever's already showing (Main Menu or
        /// Pause), not through SceneTransitionManager.ShowOnly, so the screen behind stays active
        /// and visible underneath. CreatePanel already gives the root an opaque black Image — that
        /// was the only thing standing between the overlay and whatever's behind it. Overwriting
        /// that Image's own sprite/color to a 50%-alpha landing.png removed the opaque backing
        /// entirely, so the "dimmed" poster ended up alpha-blending against whatever was actually
        /// rendered underneath instead of against black — for Settings opened from Main Menu,
        /// that's the exact same landing.png at full opacity, and blending an image at 50% over an
        /// identical copy of itself reproduces that same image unchanged, so no dimming was ever
        /// visible. Keeping the root's own opaque black Image intact and layering the dimmed poster
        /// as a separate stretched child on top of it composites against a real black backing
        /// instead, so the dim is genuine no matter what's behind the overlay.
        ///
        /// Root's own backing is forced to sprite=null/color=Color.black here rather than trusting
        /// CreatePanel's PlaceholderSprite.Get(Color.black) call to still be intact — diagnostic
        /// logging (Farm Fury Arcade > Debug > Diagnose Dimmed Backdrops) on a freshly reloaded scene
        /// showed the root Image's sprite reference had come back NULL with color stuck at Unity's
        /// default white, on every one of these 6 screens. PlaceholderSprite.Get creates its Sprite
        /// from a plain `new Texture2D(...)` that's never saved as a real AssetDatabase asset (no
        /// .meta, no GUID) — Unity does not reliably re-serialize that kind of purely-in-memory
        /// Sprite reference across a scene save/reload the way an embedded prefab sub-asset survives.
        /// The result: a null sprite + default white color renders as an opaque WHITE rect via
        /// Image's own null-sprite fallback, not opaque black — so the "dimmed" poster on top of it
        /// was blending toward white/washed-out the whole time, never toward black. A null sprite
        /// with color explicitly set to black sidesteps the fragile reference entirely: Image already
        /// renders a solid filled rect when sprite is null, tinted by color, with no asset reference
        /// to lose on serialization.</summary>
        /// <param name="posterFileName">Defaults to Landing_Opacity.png at full (1f) alpha — a
        /// pre-faded PNG with the dim baked directly into the pixels, used uniformly across all 6
        /// screens for a consistent look. This replaced an earlier runtime-alpha-blend approach
        /// (landing.png shown at StandardBackdropOpacity/0.5 via Image.color.a) once that runtime
        /// blend was confirmed working via a Settings-only test — baking the fade into the art
        /// instead removes any dependency on runtime alpha compositing behaving consistently across
        /// screens, and matches "uniform across pages" per explicit request.</param>
        private static void ApplyDimmedLandingBackground(GameObject root, string posterFileName = "Landing_Opacity.png", float opacity = 1f)
        {
            var rootImage = root.GetComponent<Image>();
            rootImage.sprite = null;
            rootImage.color = Color.black;

            var posterGO = new GameObject("PosterBackdrop", typeof(RectTransform), typeof(Image));
            posterGO.transform.SetParent(root.transform, false);
            StretchFull((RectTransform)posterGO.transform);
            var image = posterGO.GetComponent<Image>();
            image.sprite = LoadUiSprite(posterFileName);
            image.color = new Color(1f, 1f, 1f, opacity);
        }

        /// <summary>Standardized top-center wood-sign header — same size/position on every screen
        /// in this redesign wave, benchmarked against Level Select's own "SELECT LEVEL" sign
        /// (SetAnchorRect/AnchorTopCenter, 860x320, (0,-40) offset).</summary>
        private static Image CreateHeaderSign(Transform screenRoot, Sprite sprite)
        {
            var go = new GameObject("TitleImage", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(screenRoot, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            AnchorTopCenter((RectTransform)go.transform, StandardHeaderSignSize, StandardHeaderSignOffset);
            return image;
        }

        private static Sprite LoadUiSprite(string fileName) => ConfigureAndLoadCosmeticChromeSprite($"{UISpriteFolder}/{fileName}");
        private static Sprite LoadUiSprite(string fileName, Vector4 border) => ConfigureAndLoadCosmeticChromeSprite($"{UISpriteFolder}/{fileName}", border);
        private static Sprite LoadCosmeticsSprite(string fileName) => ConfigureAndLoadCosmeticChromeSprite($"{CosmeticsChromeFolder}/{fileName}");
        private static Sprite LoadCharacterSprite(string fileName) => ConfigureAndLoadCosmeticChromeSprite($"{CharacterSpriteFolder}/{fileName}");
        private static Sprite LoadMazeThemeArtSprite(string fileName) => ConfigureAndLoadCosmeticChromeSprite($"{MazeThemeArtFolder}/{fileName}");

        /// <summary>Icon-only button — no label, art's own aspect preserved, explicit sizeDelta set
        /// directly rather than relying on LayoutElement (the parent HorizontalLayoutGroup rows this
        /// is used in all set childControlWidth/Height = false, which reads each child's raw
        /// RectTransform size directly and never consults LayoutElement — same gotcha CLAUDE.md
        /// documents for the D-pad/Level Select scroll-range bugs).</summary>
        private static Button CreateIconButton(string name, Transform parent, Sprite sprite, float size)
        {
            var button = CreateButton(name, parent, string.Empty, Color.white, 20f, size, out _);
            Object.DestroyImmediate(button.transform.Find(name + "_Label").gameObject);
            var image = button.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            ((RectTransform)button.transform).sizeDelta = new Vector2(size, size);
            return button;
        }

        /// <summary>Shared "same banner for all new pages" sizing (2026-09-12) — Hats&Caps.png and
        /// Trails.png are both 619x246 (aspect ~2.516), sized/positioned identically wherever either
        /// appears (the chooser's two stacked buttons, and each destination page's own plain header)
        /// so the banner a player tapped keeps reading as "this is where I am" on the page it opens.
        /// Height is derived from the art's own real aspect ratio rather than guessed, so neither
        /// banner is stretched.
        ///
        /// Re-tuned 2026-09-12 per direct screenshot feedback: the top offset originally matched
        /// MenuHubScreen's own stacked-sign convention (-320, i.e. well below the screen's top edge)
        /// — a screenshot showed this read as noticeably lower/smaller than every OTHER screen's own
        /// header, which all sit close to the top via CreateHeaderSign's StandardHeaderSignOffset.y
        /// (-55). Switched to that exact same offset for true top-middle consistency, and width
        /// enlarged 550 -> 700 (~+27%) so the banners read bigger on screen, per the same feedback.
        /// BuildCosmeticsHatsScreen/BuildCosmeticsTrailsScreen's own item-row Y was shifted up by the
        /// same ~206 the header's own bottom edge moved up by, to keep the same relative gap below it
        /// rather than opening a big dead zone where the header used to sit.</summary>
        private const float CosmeticsBannerWidth = 700f;
        private const float CosmeticsBannerAspect = 619f / 246f;
        private const float CosmeticsBannerHeight = CosmeticsBannerWidth / CosmeticsBannerAspect;
        private const float CosmeticsBannerGap = 30f;
        // Matches CreateHeaderSign's own StandardHeaderSignOffset.y exactly (-55) — not a
        // coincidence, this is the literal "top-middle alignment, same as every other header" fix.
        private const float CosmeticsBannerTopOffset = -55f;

        /// <summary>Item icon sizing (2026-09-12) — matches BuildCoinPurchaseScreen's own coin-pack
        /// icons exactly (StandardIconButtonSize * 1.5 * 1.4 = 336 tall), per direct feedback that
        /// the cosmetics items should read the same size as the coin icons. Not a coincidence that
        /// this also matches: every price-baked cosmetic item sprite (sombrero_price.png,
        /// baseball_price.png, ChefHat_price.png, all 6 trail price plaques, Crown_price.png) is the
        /// exact same 500x669 source resolution the coin plaques (100/500/5000/15000.png) use, so
        /// reusing the identical height+aspect math produces an identical on-screen size with zero
        /// distortion on either family.</summary>
        private const float CosmeticsItemHeight = StandardIconButtonSize * 1.5f * 1.4f;
        private const float CosmeticsItemAspect = 500f / 669f;
        private const float CosmeticsItemWidth = CosmeticsItemHeight * CosmeticsItemAspect;

        /// <summary>Plain (non-interactive) header banner shared by both cosmetics destination pages
        /// — same size/position CosmeticsChooserScreen's own buttons use, so the page a player lands
        /// on shows the identical banner they just tapped.</summary>
        private static void CreateCosmeticsPageHeaderBanner(Transform parent, Sprite sprite)
        {
            var headerGO = new GameObject("TitleImage", typeof(RectTransform), typeof(Image));
            headerGO.transform.SetParent(parent, false);
            var headerImage = headerGO.GetComponent<Image>();
            headerImage.sprite = sprite;
            headerImage.preserveAspect = true;
            AnchorTopCenter((RectTransform)headerGO.transform, new Vector2(CosmeticsBannerWidth, CosmeticsBannerHeight), new Vector2(0f, CosmeticsBannerTopOffset));
        }


        /// <summary>Non-square counterpart to CreateIconButton — for art like the cosmetics price
        /// plaques, which are taller than wide (icon + hanging price sign baked into one image), so
        /// forcing a square box would waste space either letterboxing or (worse, since these are
        /// built directly rather than through SetImageSprite) never actually distorting the art but
        /// leaving a mismatched box around it.</summary>
        private static Button CreateItemButton(string name, Transform parent, Sprite sprite, float width, float height)
        {
            var button = CreateButton(name, parent, string.Empty, Color.white, 20f, height, out _);
            Object.DestroyImmediate(button.transform.Find(name + "_Label").gameObject);
            var image = button.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            ((RectTransform)button.transform).sizeDelta = new Vector2(width, height);
            return button;
        }

        /// <summary>In-maze cosmetics "Locker" (2026-09-09) — opened from Gameplay HUD's Locker
        /// button. Backdrop/Logo/round-back-button follow the exact same opaque-overlay convention
        /// Pause uses (Bg_LevelSelect.png, no gameplay visible behind it while browsing). Everything
        /// below the header (the owned-items grid, the equip/unequip logic) is deliberately NOT
        /// baked here — LockerScreen builds its own tiles at runtime (same "component builds its
        /// own list at runtime" convention CharacterStoryScreen's BuildRow uses), since which items
        /// are actually owned/equipped, and the Baseball Cap item's real cosmeticId (resolved per
        /// active character), can only be known live, not at Editor-build time. This method only
        /// builds the scrollable tile region (tileContainer, a GridLayoutGroup + ContentSizeFitter
        /// inside a ScrollRect, so LockerScreen doesn't need to hand-position or size anything as
        /// the owned-item count varies — see its own comment further down for why this needs to
        /// scroll rather than use a fixed-size box) plus the header/backdrop/close button, and wires
        /// purchaseScreen to the existing Cosmetics purchase screen (built earlier in BuildAll) —
        /// currently unused by anything built here, kept wired for when the "You may like" banner
        /// (see its own removal note below) comes back. LockerScreen only ever builds a tile for an
        /// item the player actually OWNS — an unowned cosmetic gets no tile here at all right now
        /// (the suggestion banner that used to mention it elsewhere is temporarily removed).</summary>
        /// <summary>Category banner row size/position for BuildLockerScreen — the three
        /// Hats&amp;Caps.png/Trails.png/machine.png banners CosmeticsChooserScreen uses (619x246,
        /// aspect from CosmeticsBannerAspect), side by side rather than stacked since this screen
        /// has far less vertical room to spare than a dedicated chooser page.
        ///
        /// Reworked 2026-09-17 (per direct feedback) from a two-banner, empty-state-ONLY row into a
        /// permanent three-banner row in its own bottom-aligned band: the row used to vanish the
        /// instant the player owned a single cosmetic, taking the only in-maze shortcut to the
        /// purchase pages with it. Every number below is derived top-down so the row can never
        /// overlap an owned tile (1920x1080 reference canvas, D = distance from the screen's top
        /// edge): close button top edge D=850 (160 tall, 70 bottom inset) -> 20px margin -> row
        /// bottom D=830 -> row top D=830-height -> 20px margin -> tile scroll bottom -> tile scroll
        /// top D=385 (its own 20px gap below the header's D=365 bottom edge). Banner width dropped
        /// 500 -> 330 to fit three across (330*3 + 30*2 = 1050, inside the tile band's own 1200
        /// width) AND to keep the row short enough (~131 tall) that the tile scroll region above it
        /// still clears a full 280-tall tile row.</summary>
        private const float LockerBannerWidth = 330f;
        private const float LockerBannerHeight = LockerBannerWidth / CosmeticsBannerAspect;
        private const float LockerBannerGap = 30f;
        private const float LockerBannerRowBottomD = 830f;
        private const float LockerBannerRowTopD = LockerBannerRowBottomD - LockerBannerHeight;

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
            AnchorTopLeft((RectTransform)logoImageGO.transform, new Vector2(LogoImageSize, LogoImageSize), new Vector2(100f, -40f));

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

            // Web demo: no Double Coins (Watch Ad) button.

            var playButton = CreateButton("PlayButton", root.transform, string.Empty, new Color(0.85f, 0.55f, 0.1f), 28f, StandardIconButtonSize, out _);
            Object.DestroyImmediate(playButton.transform.Find("PlayButton_Label").gameObject);
            AnchorBottomLeft((RectTransform)playButton.transform, new Vector2(StandardIconButtonSize, StandardIconButtonSize), new Vector2(150f, 110f));

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
            AnchorTopLeft((RectTransform)unlockLogoGO.transform, new Vector2(LogoImageSize, LogoImageSize), new Vector2(100f, -50f));

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

            // Confetti layer (2026-09-11, per direct feedback: "more animated - exciting - maybe
            // with confetti") — a plain full-screen, non-raycasting RectTransform particles spawn
            // under; built LAST so it's the last sibling and therefore draws on top of the card
            // reveal, letting confetti visibly rain down over the card rather than behind it.
            var confettiLayerGO = new GameObject("ConfettiLayer", typeof(RectTransform));
            confettiLayerGO.transform.SetParent(unlockRoot.transform, false);
            StretchFull((RectTransform)confettiLayerGO.transform);
            var confettiBurst = confettiLayerGO.AddComponent<ConfettiBurst>();

            var unlockScreen = unlockRoot.AddComponent<NewCharacterUnlockScreen>();
            var unlockSO = new SerializedObject(unlockScreen);
            unlockSO.FindProperty("characterCardImage").objectReferenceValue = unlockCard;
            unlockSO.FindProperty("tapButton").objectReferenceValue = unlockTapButton;
            unlockSO.FindProperty("confettiBurst").objectReferenceValue = confettiBurst;
            var confettiSO = new SerializedObject(confettiBurst);
            confettiSO.FindProperty("particlesRoot").objectReferenceValue = (RectTransform)confettiLayerGO.transform;
            confettiSO.ApplyModifiedPropertiesWithoutUndo();
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

            // Shrunk again 2026-09-17 (600x600 -> 420x420), per a direct screenshot showing the
            // badge overlapping WorldUnlockedBanner above it: the banner occupies canvas-space
            // y=[220,480] (AnchorTopCenter, offset (0,-60), height 260, in this 1920x1080
            // reference canvas measured from the vertical centre), and at 600x600 anchored at
            // y-fraction 0.55 the badge's own top edge (y=354) landed 134 units INSIDE that band.
            // Re-centred in the real free space between the banner's bottom edge (y=220) and
            // TapHint's own top edge (y=-330, from its AnchorBottomCenter offset) instead of
            // guessing a new anchor fraction — midpoint y=-55 with a 420 diameter leaves a genuine
            // 65-unit gap on both sides (banner: 220-(-55+210)=65; hint: (-55-210)-(-330)=65), not
            // just "smaller so it probably doesn't touch."
            var worldBadge = CreateImage("WorldBadge", worldUnlockRoot.transform, new Color(1f, 0.84f, 0f), 420f, 420f);
            var worldBadgeRect = (RectTransform)worldBadge.transform;
            worldBadgeRect.anchorMin = worldBadgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            // CreateImage's width/height args only set a LayoutElement's preferredWidth/Height,
            // which worldUnlockRoot (a plain CreatePanel, no LayoutGroup) never reads — sizeDelta
            // must be set explicitly or the rect silently stays at Unity's default 100x100
            // regardless of what was passed in. This is why the badge rendered tiny even after its
            // burst-in/pulse animation "finished" — the animation itself was correct, it was just
            // animating up to a 100x100 target instead of the intended size.
            worldBadgeRect.sizeDelta = new Vector2(420f, 420f);
            worldBadgeRect.anchoredPosition = new Vector2(0f, -55f);
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
            so.FindProperty("unlockScreen").objectReferenceValue = unlockScreen;
            so.FindProperty("worldUnlockScreen").objectReferenceValue = worldUnlockScreen;
            so.ApplyModifiedPropertiesWithoutUndo();

            return (root, unlockScreen);
        }

        // ---- Level Failed -----------------------------------------------------------------------

        /// <summary>Rebuilt again (2026-08-30) to match a new "GAME OVER" mockup exactly —
        /// Bg_LevelSelect.png root background and Logo.png top-left are unchanged, but the old
        /// "TRY AGAIN!" card (LevelFailed.png) + StarDisplay/score readout are gone entirely: the
        /// new mockup shows just a wood-sign "GAME OVER" banner and 3 buttons, no stars/score at
        /// all. GameOver.png originally landed as bare text with no frame, so a placeholder wood-
        /// sign composition was built around it — the artist has since replaced the file in place
        /// with a real framed hanging sign (666x375, same template as Pause.png/Leaderboard.png:
        /// rope-tied corners, parchment insert, "GAME OVER" baked directly onto it), so that
        /// placeholder is gone — this is now a single real image, same convention every other
        /// wood-sign header in this project uses.
        ///
        /// Only 3 buttons now, matching the mockup exactly (was 4: Play/Skip/Settings/Quit) — Play
        /// (bottom-left, restarts, unchanged), Settings (opens the shared SettingsPanel overlay,
        /// unchanged) and Home (bottom-right outermost, Btn_home.png — the old Quit button, same
        /// QuitToWorldSelect behaviour, just relabelled/re-iconed to match the mockup's house icon;
        /// the standalone Skip button is gone, since the mockup has no 4th icon for it).
        ///
        /// GameOver.png was reworked again (2026-08-31) — the framed hanging sign described above
        /// is gone too, replaced with bare transparent lettering, same as Pause.png (BuildPauseMenu)
        /// — see HeaderBannerHeight's own doc comment for how the two are now sized consistently.
        /// A purely decorative "Insert Coin" + coin-icon row (InsertCoin.png/Coin_UI.png, no
        /// gameplay hookup) sits directly beneath it at a smaller scale, per direct request.</summary>
        private static GameObject BuildLevelFailed(Transform canvasTransform)
        {
            var root = CreatePanel("LevelFailedScreen", canvasTransform, Color.black);
            root.GetComponent<Image>().sprite = LoadUiSprite("Bg_LevelSelect.png");

            var logoImageGO = new GameObject("LogoImage", typeof(RectTransform), typeof(Image));
            logoImageGO.transform.SetParent(root.transform, false);
            var logoImage = logoImageGO.GetComponent<Image>();
            logoImage.sprite = LoadUiSprite("Logo.png");
            logoImage.preserveAspect = true;
            AnchorTopLeft((RectTransform)logoImageGO.transform, new Vector2(LogoImageSize, LogoImageSize), new Vector2(100f, -40f));

            // "GAME OVER" sign was reworked by the artist (2026-08-31) to drop its wood-sign
            // frame/background too — bare "GAME OVER" lettering on a transparent 418x87 canvas
            // (~4.80:1), rendered directly with no backboard/frame. Sized off the same
            // HeaderBannerHeight as Pause.png (BuildPauseMenu) so the two text-only screen banners
            // read at a consistent scale despite their differing aspect ratios (both PNGs' opaque
            // lettering fills essentially their whole canvas — measured near-zero transparent
            // padding on both — so matching canvas height genuinely matches rendered letter
            // height, not just bounding-box height). Anchored identically to Pause.png too
            // (AnchorTopCenter, same -300 offset) — this used to be centre-anchored at a
            // different offset (+150), which put the two banners in different screen positions
            // despite the shared-height sizing; found via direct comparison against Pause's own
            // anchor/offset after a report that the two didn't look placed the same.
            var gameOverGO = new GameObject("GameOverSign", typeof(RectTransform), typeof(Image));
            gameOverGO.transform.SetParent(root.transform, false);
            var gameOverImage = gameOverGO.GetComponent<Image>();
            gameOverImage.sprite = LoadUiSprite("GameOver.png");
            gameOverImage.preserveAspect = true;
            AnchorTopCenter((RectTransform)gameOverGO.transform, new Vector2(HeaderBannerHeight * (418f / 87f), HeaderBannerHeight), new Vector2(0f, -300f));

            // "Insert Coin" flavour row — purely decorative (no gameplay/purchase hookup).
            // InsertCoin.png (500x85 text) + Coin_UI.png (512x512, square) are laid out as a
            // simple fixed pair rather than a LayoutGroup, matching this method's existing
            // manual-anchor convention. Grouped under one container so a single CanvasGroup can
            // drive both — moved to sit near the bottom of the screen (was near vertical centre,
            // sitting awkwardly close to the character card row) and given the same pulsing
            // "flash" TitleScreenController's PRESS START prompt uses on the landing page, per
            // direct request. Bottom inset (130, +BannerAdBottomClearance since 2026-09-11 —
            // see the Play/Home/Settings row below) sits just above that row's own top edge
            // (its bottom inset + 160 size is its top; this row's own 70-tall box top-caps
            // comfortably below that) and matches the safe-area inset convention
            // CreateRoundBackButton/CreateGenericBackButton already use for bottom elements
            // (70-110), so it stays inside the yellow safe-area guide.
            const float insertCoinHeight = 70f; // smaller than HeaderBannerHeight (130) on purpose
            float insertCoinWidth = insertCoinHeight * (500f / 85f);
            const float insertCoinCoinSpacing = 15f;
            float insertCoinRowWidth = insertCoinWidth + insertCoinCoinSpacing + insertCoinHeight;
            const float insertCoinBottomInset = 130f + BannerAdBottomClearance;

            var insertCoinRowGO = new GameObject("InsertCoinRow", typeof(RectTransform), typeof(CanvasGroup));
            insertCoinRowGO.transform.SetParent(root.transform, false);
            AnchorBottomCenter((RectTransform)insertCoinRowGO.transform, new Vector2(insertCoinRowWidth, insertCoinHeight), new Vector2(0f, insertCoinBottomInset));
            var insertCoinRowGroup = insertCoinRowGO.GetComponent<CanvasGroup>();
            var insertCoinPulse = insertCoinRowGO.AddComponent<PulsingCanvasGroup>();
            var insertCoinPulseSO = new SerializedObject(insertCoinPulse);
            insertCoinPulseSO.FindProperty("canvasGroup").objectReferenceValue = insertCoinRowGroup;
            insertCoinPulseSO.ApplyModifiedPropertiesWithoutUndo();

            var insertCoinGO = new GameObject("InsertCoinText", typeof(RectTransform), typeof(Image));
            insertCoinGO.transform.SetParent(insertCoinRowGO.transform, false);
            var insertCoinImage = insertCoinGO.GetComponent<Image>();
            insertCoinImage.sprite = LoadUiSprite("InsertCoin.png");
            insertCoinImage.preserveAspect = true;
            var insertCoinRect = (RectTransform)insertCoinGO.transform;
            insertCoinRect.anchorMin = insertCoinRect.anchorMax = new Vector2(0.5f, 0.5f);
            insertCoinRect.pivot = new Vector2(0.5f, 0.5f);
            insertCoinRect.sizeDelta = new Vector2(insertCoinWidth, insertCoinHeight);
            insertCoinRect.anchoredPosition = new Vector2(-insertCoinRowWidth / 2f + insertCoinWidth / 2f, 0f);

            var insertCoinIconGO = new GameObject("InsertCoinIcon", typeof(RectTransform), typeof(Image));
            insertCoinIconGO.transform.SetParent(insertCoinRowGO.transform, false);
            var insertCoinIconImage = insertCoinIconGO.GetComponent<Image>();
            insertCoinIconImage.sprite = LoadUiSprite("Coin_UI.png");
            insertCoinIconImage.preserveAspect = true;
            var insertCoinIconRect = (RectTransform)insertCoinIconGO.transform;
            insertCoinIconRect.anchorMin = insertCoinIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            insertCoinIconRect.pivot = new Vector2(0.5f, 0.5f);
            insertCoinIconRect.sizeDelta = new Vector2(insertCoinHeight, insertCoinHeight);
            insertCoinIconRect.anchoredPosition = new Vector2(insertCoinRowWidth / 2f - insertCoinHeight / 2f, 0f);

            // Bottom inset raised from 110 to 110+BannerAdBottomClearance (2026-09-11), same reason
            // and same shared constant as PauseMenuController's identical row — see BuildPauseMenu's
            // own comment.
            const float levelFailedButtonBottomInset = 110f + BannerAdBottomClearance;
            var playButton = CreateIconButton("PlayButton", root.transform, LoadUiSprite("Btn_play.png"), StandardIconButtonSize);
            AnchorBottomLeft((RectTransform)playButton.transform, new Vector2(StandardIconButtonSize, StandardIconButtonSize), new Vector2(150f, levelFailedButtonBottomInset));

            var homeButton = CreateIconButton("HomeButton", root.transform, LoadUiSprite("Btn_home.png"), StandardIconButtonSize);
            AnchorBottomRight((RectTransform)homeButton.transform, new Vector2(StandardIconButtonSize, StandardIconButtonSize), new Vector2(-150f, levelFailedButtonBottomInset));

            var settingsButton = CreateIconButton("SettingsButton", root.transform, LoadUiSprite("Btn_settings.png"), StandardIconButtonSize);
            AnchorBottomRight((RectTransform)settingsButton.transform, new Vector2(StandardIconButtonSize, StandardIconButtonSize), new Vector2(-150f - StandardIconButtonSize - 30f, levelFailedButtonBottomInset));

            var controller = root.AddComponent<LevelFailedController>();
            var so = new SerializedObject(controller);
            so.FindProperty("playButton").objectReferenceValue = playButton;
            so.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            so.FindProperty("homeButton").objectReferenceValue = homeButton;
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

        // ---- Leaderboards -----------------------------------------------------------------------

        // Index-aligned with UnlockProgression's own world numbering (0=Corn Field .. 6=Harvest
        // Moon) - each file is a standalone bubble-text world-name banner, distinct from the
        // shield-shaped worldSignSprites Level Select uses. On-disk "Wheatfiled.png" keeps its real
        // typo (missing an 'l') - AssetDatabase.LoadAssetAtPath is case/spelling-sensitive
        // regardless of OS filesystem, same gotcha this project has hit before for sprite filenames.
        private static readonly string[] WorldBannerFiles =
        {
            "Cornfield.png", "VegetablePatch.png", "Orchard.png", "Wheatfiled.png",
            "FrozenGarden.png", "GoldenSunset.png", "HarvestMoon.png",
        };

        // Index-aligned with CharacterType's own declaration order (Cluck, Bessie, Percy, Woolly,
        // Ducky, Horace, Gerald, Billy) - "BestFarmFury"'s per-world portrait art. On-disk
        // "PercyThumbsup.png" keeps its real casing (lowercase "up", unlike every other character's
        // "ThumbsUp") - same case-sensitivity gotcha as the world banners above.
        private static readonly string[] CharacterThumbsUpFiles =
        {
            "CluckThumbsUp.png", "BessieThumbsUp.png", "PercyThumbsup.png", "WoollyThumbsUp.png",
            "DuckyThumbsUp.png", "HoraceThumbsUp.png", "GeraldThumbsUp.png", "BillyThumbsUp.png",
        };

        /// <summary>Shared row sizing for WorldLeaderboardDetailScreen (2026-09-14 enlarge pass) —
        /// declared once here rather than duplicated as local consts inside
        /// BuildLeaderboardStatRow/BuildLeaderboardStarRow/BuildLeaderboardPlaque, specifically so
        /// the row-height used for vertical spacing math in BuildWorldLeaderboardDetailScreen can
        /// never drift out of sync with the row-height those helpers actually build each row at —
        /// a mismatch there would silently reintroduce overlap. Enlarged from bestRowHeight 100/
        /// rowHeight 64/rowGap 16/labelWidth 280/plaqueWidth 220/innerGap 40/starSize 48/
        /// valueFontSize 36, per direct feedback the line items read too small.
        ///
        /// Re-tuned again 2026-09-14 (second pass, per a direct screenshot showing the first pass's
        /// results): rowGap 20->8 (padding between rows was still too loose) and starSize 56->70
        /// (stars read too small next to the now-larger plaques). bestRowHeight/rowHeight/
        /// plaqueWidth/innerGap/valueFontSize are unchanged from the first enlarge pass — this round
        /// was about spacing/stars, not the rows' own footprint.
        ///
        /// Third pass (2026-09-14): labelWidth 320->380 — no longer the width each label image is
        /// forced into (see BuildLeaderboardStatRow/BestFarmFury label's own doc comments for the
        /// real "renders shorter than rowHeight" bug that fix addresses), now purely the fixed
        /// column-alignment offset the value plaques sit at. 380 comfortably clears FastestTime.png's
        /// own natural width at rowHeight (~354, the widest of the two stat-row labels).</summary>
        private const float WorldDetailBestRowHeight = 120f;
        private const float WorldDetailRowHeight = 78f;
        private const float WorldDetailRowGap = 8f;
        private const float WorldDetailLabelWidth = 380f;
        private const float WorldDetailPlaqueWidth = 260f;
        private const float WorldDetailInnerGap = 46f;
        private const float WorldDetailStarSize = 70f;
        private const float WorldDetailValueFontSize = 42f;


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
        /// Pause), Logo.png top-left, a round back button bottom-right (moved here 2026-08-28 to
        /// match every other screen in this family — Settings/Level Select/Shop/Cosmetics/etc. all
        /// use CreateRoundBackButton's default bottom-right corner; this screen was previously the
        /// one outlier still on bottom-left, per an earlier mockup pass), and a CardCarouselController
        /// (same component Level Select's world picker uses) instead of the old static GridLayoutGroup
        /// — one CharacterSelectCard per CharacterData, flick to cycle which is centred/full-scale,
        /// tap the centred card to swap into it. Not part of screenRoots — like Pause/Settings, it's
        /// an overlay shown/hidden directly rather than routed through SceneTransitionManager.ShowOnly.</summary>
        private static ChooseCharacterScreen BuildChooseCharacterScreen(Transform canvasTransform, GameObject cardPrefab)
        {
            var root = CreatePanel("ChooseCharacterScreen", canvasTransform, Color.black);

            // Backdrop sits on its own child with an AspectRatioFitter (EnvelopeParent = uniformly
            // scale to fully cover the screen, cropping overflow top/bottom or left/right, never
            // distorting) instead of the root panel's own Image, which stretches non-uniformly to
            // exactly fill the screen rect whenever the device aspect doesn't match the art's own
            // ~1.77:1 — the cause of a real device screenshot showing World1_Cornfield.png's moon
            // rendering as an oval instead of a circle. root's own Image stays a plain black
            // fallback behind this (unaffected — CreatePanel already sets it, nothing to change).
            var backdropGO = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            backdropGO.transform.SetParent(root.transform, false);
            var backdropRect = (RectTransform)backdropGO.transform;
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            var backdropFitter = backdropGO.GetComponent<AspectRatioFitter>();
            backdropFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            // aspectRatio is set for real in ArtWiringBuilder once the actual sprite is wired,
            // computed from that sprite's own pixel dimensions rather than hardcoded here — this
            // placeholder value only matters before art is wired at all.
            backdropFitter.aspectRatio = 16f / 9f;
            var backdropImage = backdropGO.GetComponent<Image>();
            backdropImage.raycastTarget = false;

            // Logo now sits over the moon baked into World1_Cornfield.png (swapped with the
            // power-play showcase below, per direct request) — a plain fixed-size/fixed-position
            // icon rather than anything backdrop-relative, since a decorative logo doesn't need to
            // precisely match the moon's own silhouette the way the combo-icon showcase used to
            // try to (see PowerPlayShowcase's own comment below for why that was dropped). Position
            // measured from the same moon-centre fraction (0.7945, 0.1960 of the image, top-left
            // origin) found earlier this session, converted to a screen-centre-relative anchored
            // position in the 1920x1080 reference canvas — comfortably inside the yellow safe-area
            // guide (well clear of both the top and right edges).
            var logoImageGO = new GameObject("LogoImage", typeof(RectTransform), typeof(Image));
            logoImageGO.transform.SetParent(root.transform, false);
            var logoImage = logoImageGO.GetComponent<Image>();
            logoImage.sprite = PlaceholderSprite.Get(Color.clear);
            logoImage.preserveAspect = true;
            var logoRect = (RectTransform)logoImageGO.transform;
            logoRect.anchorMin = new Vector2(0.5f, 0.5f);
            logoRect.anchorMax = new Vector2(0.5f, 0.5f);
            logoRect.pivot = new Vector2(0.5f, 0.5f);
            logoRect.sizeDelta = new Vector2(260f, 260f);
            // Shifted right from the original moon-centre measurement (565->645) per a device
            // screenshot showing it sitting visibly left of the moon's actual centre.
            logoRect.anchoredPosition = new Vector2(645f, 328f);

            var backButton = CreateRoundBackButton(root.transform, bottomRight: true);

            // Power-play (combo icon) showcase — auto-cycles through the combo icon art
            // (CrossFire/DoubleSlam/IronStampede/KicknRoll/SkipShatter), a passive "pairing
            // characters unlocks power plays" cue. Moved to the top-left corner (swapped with
            // Logo above, per direct request) — this is deliberately the exact same plain
            // top-left anchor/size/inset Logo used to have, since sitting in open sky here means
            // it no longer needs to match any specific backdrop feature's shape (unlike its old
            // "sit precisely on the moon" spot, which fought the backdrop's own aspect-fill
            // scaling for no real benefit once it was just going to be replaced by a plain logo
            // anyway).
            var powerPlayShowcaseGO = new GameObject("PowerPlayShowcase", typeof(RectTransform), typeof(Image));
            powerPlayShowcaseGO.transform.SetParent(root.transform, false);
            var powerPlayShowcaseImage = powerPlayShowcaseGO.GetComponent<Image>();
            // X inset widened from 100 (Logo's own old value) to 175 per a device screenshot
            // showing it sitting right at/past the screen's rounded-corner edge, outside the
            // yellow safe-area guide.
            AnchorTopLeft((RectTransform)powerPlayShowcaseGO.transform, new Vector2(LogoImageSize, LogoImageSize), new Vector2(175f, -50f));
            powerPlayShowcaseImage.preserveAspect = true;
            powerPlayShowcaseImage.raycastTarget = false;
            Color powerPlayShowcaseStartColor = Color.white;
            powerPlayShowcaseStartColor.a = 0f;
            powerPlayShowcaseImage.color = powerPlayShowcaseStartColor;
            var powerPlayShowcase = powerPlayShowcaseGO.AddComponent<PowerPlayMoonShowcase>();
            var powerPlayIcons = new[]
            {
                LoadUiSprite("CrossFire.png"),
                LoadUiSprite("DoubleSlam.png"),
                LoadUiSprite("IronStampede.png"),
                LoadUiSprite("KicknRoll.png"),
                LoadUiSprite("SkipShatter.png"),
            };
            var powerPlayShowcaseSO = new SerializedObject(powerPlayShowcase);
            powerPlayShowcaseSO.FindProperty("targetImage").objectReferenceValue = powerPlayShowcaseImage;
            var powerPlayIconsProp = powerPlayShowcaseSO.FindProperty("icons");
            powerPlayIconsProp.arraySize = powerPlayIcons.Length;
            for (int i = 0; i < powerPlayIcons.Length; i++)
            {
                powerPlayIconsProp.GetArrayElementAtIndex(i).objectReferenceValue = powerPlayIcons[i];
            }
            powerPlayShowcaseSO.ApplyModifiedPropertiesWithoutUndo();

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
            // Re-centred again (2026-08-29 device screenshot) — plain (0,0) read as off-centre in
            // practice for two compounding reasons: (1) the arc's dip is one-directional
            // (y = -arcRadius*(1-cos(angle)), see CardCarouselController below — every off-centre
            // card sags DOWN, never up, so the row's actual visual mass sits below the centred
            // card's own y), and (2) with 8 cards and an odd centred index, the fan of visible
            // cards trails further off-frame to one side than the other, reading as left-clipped
            // with a gap on the right. Nudged right (+50) and up (+20) to compensate — first-pass,
            // no visual Editor access this session, expect to nudge further if it still reads off.
            containerRect.anchoredPosition = new Vector2(50f, 20f);
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
            ChooseCharacterScreen chooseCharacterScreen, GameObject levelSelect, GameObject characterStory)
        {
            var settingsPanel = settings.GetComponent<SettingsPanel>();
            SetRefs(settingsPanel, ("characterStoryScreen", characterStory));

            SetRefs(mainMenu.GetComponent<MainMenuController>(),
                ("levelSelectScreen", levelSelect), ("settingsPanel", settingsPanel));

            SetRefs(levelSelect.GetComponent<LevelSelectController>(),
                ("mainMenuScreen", mainMenu), ("gameplayScreen", gameplay));

            var hud = gameplay.GetComponent<GameplayHUD>();
            SetRefs(hud,
                ("pauseMenu", pause.GetComponent<PauseMenuController>()),
                ("levelCompleteScreen", levelComplete), ("levelFailedScreen", levelFailed),
                ("chooseCharacterScreen", chooseCharacterScreen));

            SetRefs(pause.GetComponent<PauseMenuController>(),
                ("settingsPanel", settingsPanel), ("levelSelectScreen", levelSelect),
                ("levelSelectController", levelSelect.GetComponent<LevelSelectController>()));

            SetRefs(chooseCharacterScreen, ("pauseMenuScreen", pause));

            SetRefs(levelComplete.GetComponent<LevelCompleteController>(),
                ("levelSelectScreen", levelSelect), ("levelSelectController", levelSelect.GetComponent<LevelSelectController>()),
                ("unlockScreen", unlockScreen));

            SetRefs(levelFailed.GetComponent<LevelFailedController>(),
                ("levelSelectScreen", levelSelect),
                ("levelSelectController", levelSelect.GetComponent<LevelSelectController>()),
                ("settingsPanel", settingsPanel));
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
