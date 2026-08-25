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

            GameObject rosterCardPrefab = BuildRosterCardPrefab();
            // Built early (was further down, after BuildCharacterStoryPlaceholder) so Character
            // Story's own card column can reuse the exact same prefab/sizing ChooseCharacterScreen
            // uses instead of duplicating it.
            var characterSelectCardPrefab = BuildCharacterSelectCardPrefab();

            var fadeGroup = BuildFadeOverlay(canvas.transform);

            var mainMenu = BuildMainMenu(canvas.transform);
            var (gameplay, comboBanner) = BuildGameplayHUD(canvas.transform);
            var pause = BuildPauseMenu(canvas.transform);
            var settings = BuildSettingsPanel(canvas.transform);
            var characterStory = BuildCharacterStoryPlaceholder(canvas.transform, characterSelectCardPrefab);
            var storeComingSoon = BuildShopOverlay(canvas.transform);
            var cosmeticsHub = BuildCosmeticsHubScreen(canvas.transform);
            var hatPurchase = BuildCosmeticPurchaseScreen(canvas.transform, "HatPurchaseScreen", LoadCosmeticsSprite("Hat_Icon.png"),
                new (string productId, Sprite frameSprite)[]
                {
                    (IAPManager.HatBaseballCapProductId, LoadHatArtSprite("FrameBaseballCap.png")),
                    (IAPManager.HatCowboyHatProductId, LoadHatArtSprite("FrameCowboy.png")),
                    (IAPManager.HatSombreroProductId, LoadHatArtSprite("FrameSombrero.png")),
                },
                LoadUiSprite("3.99.png"));
            var trailPurchase = BuildCosmeticPurchaseScreen(canvas.transform, "TrailPurchaseScreen", LoadCosmeticsSprite("Trails_Tab_Icon.png"),
                new (string productId, Sprite frameSprite)[]
                {
                    (IAPManager.TrailCornHuskProductId, LoadTrailArtSprite("FrameCornHuskTrail.png")),
                    (IAPManager.TrailRainbowRibbonProductId, LoadTrailArtSprite("FrameRainbowRibbon.png")),
                    (IAPManager.TrailSparkleDustProductId, LoadTrailArtSprite("FrameSparkleDust(1).png")),
                    (IAPManager.TrailEmberProductId, LoadTrailArtSprite("FrameEmberTrail.png")),
                },
                LoadUiSprite("3.99.png"));
            // World Purchase — a whole new 25-level world ($3.99), not a cosmetic, so it's a
            // sibling to Cosmetics on the Shop screen rather than living under CosmeticsHubScreen.
            // Built to a real design mockup (WorldPurchaseBackground.png, a single baked composite
            // — background, logo, 3 shields, price plaque all one image) rather than assembled
            // from primitives — see BuildWorldPurchaseScreen's own doc comment.
            var worldPurchase = BuildWorldPurchaseScreen(canvas.transform);
            SetRefs(storeComingSoon.GetComponent<ShopController>(),
                ("cosmeticsHubScreen", cosmeticsHub.GetComponent<CosmeticsHubScreen>()));
            SetRefs(cosmeticsHub.GetComponent<CosmeticsHubScreen>(),
                ("hatPurchaseScreen", hatPurchase.GetComponent<CosmeticPurchaseScreen>()),
                ("trailPurchaseScreen", trailPurchase.GetComponent<CosmeticPurchaseScreen>()));
            var (levelComplete, unlockScreen) = BuildLevelComplete(canvas.transform);
            var levelFailed = BuildLevelFailed(canvas.transform);
            var roster = BuildCharacterRoster(canvas.transform, rosterCardPrefab);
            var leaderboards = BuildLeaderboards(canvas.transform);

            var chooseCharacter = BuildChooseCharacterScreen(canvas.transform, characterSelectCardPrefab);

            var levelTilePrefab = BuildLevelTilePrefab();
            BuildWorldDividerPrefab(); // kept but unlinked — see that method's own doc comment
            var worldShieldPrefab = BuildWorldShieldPrefab();
            var levelSelect = BuildLevelSelect(canvas.transform, levelTilePrefab, worldShieldPrefab);

            WireCrossReferences(mainMenu, gameplay, pause, settings,
                levelComplete, unlockScreen, levelFailed, roster, leaderboards, chooseCharacter, comboBanner, levelSelect, storeComingSoon, characterStory, worldPurchase);

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
            characterStory.SetActive(false);
            storeComingSoon.SetActive(false);
            cosmeticsHub.SetActive(false);
            hatPurchase.SetActive(false);
            trailPurchase.SetActive(false);
            worldPurchase.SetActive(false);
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
            // Monetisation (Phase 3): IAPManager connects to the store and fetches product
            // metadata on Start() — no real store products are registered in App Store Connect/
            // Play Console yet, so Connect() is expected to fail gracefully with a logged warning
            // until that manual store-side setup happens (same "infrastructure ready, real config
            // later" convention AdManager already established).
            if (managersGO.GetComponent<IAPManager>() == null) managersGO.AddComponent<IAPManager>();
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

            // Shop icon moved off Main Menu entirely (2026-08-20) — relocated to Level Select's
            // world-select page, top-left inside the safe-area guide (see BuildLevelSelect). Main
            // Menu is back down to just Play/Settings.
            //
            // Daily Challenge and Leaderboards also no longer live on Main Menu — moved to Level
            // Select and Settings respectively (see LevelSelectController/SettingsPanel's own doc
            // comments) per feedback that the landing page should stay minimal.

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
            // Timer moved above Score (both top-left corner now) per feedback — it used to sit
            // top-right where the coin balance pill now lives (see CoinBalanceChip below). Its own
            // box sits directly above ScoreText's (same 90px height, top edge at -80 so its bottom
            // edge lands exactly on ScoreText's top edge at -170, no gap/overlap).
            var timerText = CreateText("TimerText", root.transform, "00:00", 60f, TextAlignmentOptions.TopLeft, 90f);
            AnchorTopLeft((RectTransform)timerText.transform, new Vector2(240f, 90f), new Vector2(170f, -80f));

            var scoreText = CreateText("ScoreText", root.transform, "0", 72f, TextAlignmentOptions.TopLeft, 90f);
            AnchorTopLeft((RectTransform)scoreText.transform, new Vector2(320f, 90f), new Vector2(170f, -170f));

            // Monetisation: coin balance chip — previously SaveManager.CoinBalance had no on-screen
            // display anywhere at all (only surfaced indirectly via the Revive prompt's cost text /
            // skip-cooldown button's cost label), which is bad UX once the player is actually being
            // asked to spend coins on both of those.
            //
            // Coin_Balance_Chip.png (SQUARE, 500x500) was reworked by the artist (2026-08-23) — the
            // old art was a horizontal pill (coin icon left half / blank right half), which forced
            // the balance number to squeeze into a narrow right-side strip; it kept wrapping onto
            // two lines and spilling past the frame (caught via a gameplay screenshot review). The
            // new art is a wood picture-frame with the coin icon centred near the TOP of the inner
            // parchment and a wide blank parchment band below it, purpose-built for the number to
            // sit underneath the coin instead of squeezed beside it.
            // Sized square to match the art's real aspect, same "box aspect must match the art" fix
            // already applied to the Revive Prompt panel and Level Complete panel.
            // Enlarged repeatedly (110 -> 170 -> 340) per feedback it kept reading as too small to
            // actually see — moved to the top-right corner (TimerText's old spot, now that Timer
            // lives above ScoreText on the left — see above). 340 then turned out too large in
            // practice (a device-frame screenshot showed it overlapping the maze's own rendered
            // tiles), so it's pulled back down to 230 — still notably bigger than the original 170,
            // just no longer eating into playable maze space.
            const float coinChipSize = 230f;
            var coinChipImage = CreateImage("CoinBalanceChip", root.transform, new Color(0.85f, 0.65f, 0.2f), coinChipSize, coinChipSize);
            var coinChipRect = (RectTransform)coinChipImage.transform;
            // Lifted to -80 (was -170) so its top edge lines up with TimerText's own top edge
            // (AnchorTopLeft offset y=-80, above) instead of sitting noticeably lower than it.
            AnchorTopRight(coinChipRect, new Vector2(coinChipSize, coinChipSize), new Vector2(-170f, -80f));
            coinChipImage.preserveAspect = false;

            var coinBalanceText = CreateText("CoinBalanceText", coinChipImage.transform, "0", 64f, TextAlignmentOptions.Center, coinChipSize);
            var coinBalanceTextRect = (RectTransform)coinBalanceText.transform;
            // Positioned in the blank parchment band directly below the coin icon (roughly
            // x:20%-80%, y:18%-55% of the full 500x500 art, measured against the new artwork) — the
            // coin icon occupies the parchment's upper third, the wood frame border runs from about
            // 0-15% and 85-100% on every edge, so this stays inset from both without touching either.
            coinBalanceTextRect.anchorMin = new Vector2(0.20f, 0.18f);
            coinBalanceTextRect.anchorMax = new Vector2(0.80f, 0.55f);
            coinBalanceTextRect.offsetMin = Vector2.zero;
            coinBalanceTextRect.offsetMax = Vector2.zero;
            coinBalanceText.color = new Color(0.3f, 0.2f, 0.1f);
            // A flat 64pt was wrapping onto two lines for anything beyond ~3 digits ("1,2" over "7"
            // — caught via a gameplay screenshot), since CreateText's default word-wrapping tried to
            // fit the number within this band's width at a fixed size. Shrink-to-fit (same "keep
            // text inside its container" convention used elsewhere, e.g. CharacterStoryScreen's row
            // text) with wrapping off guarantees the whole balance always renders on one line,
            // shrinking the font instead of breaking the number across rows.
            coinBalanceText.enableWordWrapping = false;
            coinBalanceText.enableAutoSizing = true;
            coinBalanceText.fontSizeMin = 24f;
            coinBalanceText.fontSizeMax = 64f;
            coinBalanceText.overflowMode = TextOverflowModes.Truncate;

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
            // Deepened (-90 -> -160) per a gameplay-screen review — the cluster (Pause button in
            // particular) was still crossing the yellow safe-area guide's right edge.
            const float clusterInsetX = -160f;
            const float clusterInsetY = 90f;

            // Ability icon enlarged (120 -> 150 -> 210) and shifted further left (its own inset, not
            // Pause's) per direct feedback — it's the on-screen ability button (see below) and the
            // biggest/most-tapped element in this cluster, so it gets its own size distinct from
            // Pause's. abilityInsetX is more negative than
            // clusterInsetX (AnchorBottomRight: negative X moves an element further left/inward),
            // shifting it left of where Pause's own X still sits. The AnchorBottomRight pivot means
            // this growth extends the box up and left from its fixed bottom-right corner, so
            // enlarging it doesn't need any inset retuning to avoid clipping the screen edge.
            const float abilityButtonSize = 210f;
            const float abilityShiftLeft = 30f;
            const float abilityInsetX = clusterInsetX - abilityShiftLeft;

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
            // feedback) and needs that space at the bottom of this corner instead.
            float abilityBottomY = clusterInsetY + watchAdButtonHeight + clusterSpacing;

            // Character portrait sits at the bottom of the cluster (closer to the corner). Doubles
            // as the on-screen ability button (Space has no touch equivalent, so without this the
            // ability was completely unreachable on a device with no keyboard): tapping it raises
            // the same InputController event Space does, and GameplayHUD dims it while the active
            // character's ability is on cooldown.
            var portraitButton = CreateButton("CharacterPortrait", root.transform, string.Empty, Color.clear, 26f, abilityButtonSize, out _);
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

            // Monetisation: "skip cooldown for 3 coins" button — sits just left of the ability icon,
            // vertically centred against it (computed from the icon's own inset/size, now raised to
            // abilityBottomY — see above). Hidden by default; GameplayHUD.HandleAbilityCooldownChanged
            // shows/hides and enables/disables it every tick while an ability is on cooldown (see
            // that method's own comment for why it re-checks affordability every tick rather than
            // once). No dedicated icon art exists yet, so the button keeps its auto-generated "-3"
            // text label instead of the usual icon-only style.
            float abilityLeftEdgeInset = -abilityInsetX + abilityButtonSize; // distance from screen's right edge to the icon's LEFT edge
            float abilityCenterX = -abilityInsetX + abilityButtonSize / 2f; // distance from screen's right edge to the icon's horizontal centre
            float abilityCenterY = abilityBottomY + abilityButtonSize / 2f;
            var skipCooldownButton = CreateButton("SkipCooldownButton", root.transform, "-3",
                new Color(0.85f, 0.55f, 0.1f), 24f, skipButtonSize, out _);
            AnchorBottomRight((RectTransform)skipCooldownButton.transform, new Vector2(skipButtonSize, skipButtonSize),
                new Vector2(-(abilityLeftEdgeInset + clusterSpacing), abilityCenterY - skipButtonSize / 2f));
            skipCooldownButton.gameObject.SetActive(false);

            // Monetisation (Phase 2, "extra ability charge"/"skip cooldown via ad" — per the
            // Monetisation Build Plan doc these are literally the same button): a Watch Ad
            // alternative to spending coins, free instead of 3 coins. This is the "watch an ad to
            // shorten the cooldown" button. GameplayHUD.HandleAbilityCooldownChanged shows/hides it
            // every tick, gated on both "on cooldown" AND AdManager.IsRewardedAdReady (never a dead
            // button) — so it's only actually visible while the active ability is on cooldown and a
            // rewarded ad is loaded; it's invisible the rest of the time by design, not missing.
            // Moved from beside the ability icon to directly BELOW it (per feedback — the icon was
            // raised by abilityBottomY above specifically to make room here), horizontally centred
            // under the icon rather than sharing its left edge. Enlarged (64 -> 170x71) and now
            // shows the real WatchAd.png icon (wired in ArtWiringBuilder.WireMonetisationArt) instead
            // of the auto-generated "AD" text label — that label is destroyed below, same "icon art
            // replaces auto-label" convention every other icon-only button in this project uses.
            var watchAdSkipCooldownButton = CreateButton("WatchAdSkipCooldownButton", root.transform, string.Empty,
                new Color(0.85f, 0.55f, 0.1f), 24f, watchAdButtonHeight, out _);
            Object.DestroyImmediate(watchAdSkipCooldownButton.transform.Find("WatchAdSkipCooldownButton_Label").gameObject);
            AnchorBottomRight((RectTransform)watchAdSkipCooldownButton.transform, new Vector2(watchAdButtonWidth, watchAdButtonHeight),
                new Vector2(-(abilityCenterX - watchAdButtonWidth / 2f), clusterInsetY));
            watchAdSkipCooldownButton.gameObject.SetActive(false);

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
            const float dpadButtonSize = 90f;
            const float dpadSpacing = 70f;
            const float dpadInsetX = 235f;
            const float dpadInsetY = 210f;
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
            var pauseButton = CreateButton("PauseButton", root.transform, string.Empty, new Color(0.35f, 0.35f, 0.38f), 28f, dpadButtonSize, out _);
            Object.DestroyImmediate(pauseButton.transform.Find("PauseButton_Label").gameObject);
            AnchorBottomLeft((RectTransform)pauseButton.transform, new Vector2(dpadButtonSize, dpadButtonSize),
                new Vector2(upButtonCenterX - dpadButtonSize / 2f, upButtonTopEdge + clusterSpacing));

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
            // Monetisation: rewarded-ad alternative to spending coins (Phase 2's "continue after
            // death" placement — see CLAUDE.md). WatchAd.png bakes its own "Watch Ad" label in,
            // same convention as Yes.png/No.png — the auto-generated TMP label is destroyed here
            // and the real sprite is wired by ArtWiringBuilder (BtnWatchAd), not set inline.
            var watchAdButton = CreateButton("WatchAdButton", reviveGroup.transform, string.Empty, new Color(0.85f, 0.55f, 0.1f), out _);
            Object.DestroyImmediate(watchAdButton.transform.Find("WatchAdButton_Label").gameObject);
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
            var watchAdButtonRect = (RectTransform)watchAdButton.transform;
            watchAdButtonRect.sizeDelta = new Vector2(watchAdButtonRect.sizeDelta.x, reviveButtonHeight);
            var declineButtonRect = (RectTransform)declineButton.transform;
            declineButtonRect.sizeDelta = new Vector2(declineButtonRect.sizeDelta.x, reviveButtonHeight);
            reviveRoot.SetActive(false);

            var revivePrompt = reviveRoot.AddComponent<RevivePromptController>();
            var reviveSO = new SerializedObject(revivePrompt);
            reviveSO.FindProperty("reviveButton").objectReferenceValue = reviveButton;
            reviveSO.FindProperty("declineButton").objectReferenceValue = declineButton;
            reviveSO.FindProperty("watchAdButton").objectReferenceValue = watchAdButton;
            reviveSO.ApplyModifiedPropertiesWithoutUndo();

            var hud = root.AddComponent<GameplayHUD>();
            var so = new SerializedObject(hud);
            so.FindProperty("scoreText").objectReferenceValue = scoreText;
            so.FindProperty("timerText").objectReferenceValue = timerText;
            so.FindProperty("coinBalanceText").objectReferenceValue = coinBalanceText;
            so.FindProperty("characterPortrait").objectReferenceValue = portrait;
            so.FindProperty("abilityButton").objectReferenceValue = portraitButton;
            so.FindProperty("pauseButton").objectReferenceValue = pauseButton;
            so.FindProperty("powerPelletTimerBar").objectReferenceValue = powerBarGO;
            so.FindProperty("powerPelletTimerFill").objectReferenceValue = powerFillImage;
            so.FindProperty("chainCounterRoot").objectReferenceValue = chainRoot;
            so.FindProperty("chainCounterText").objectReferenceValue = chainText;
            so.FindProperty("revivePrompt").objectReferenceValue = revivePrompt;
            so.FindProperty("skipCooldownButton").objectReferenceValue = skipCooldownButton;
            so.FindProperty("watchAdSkipCooldownButton").objectReferenceValue = watchAdSkipCooldownButton;
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

        /// <summary>Rebuilt from scratch to match a new Canva mockup (2026-08-20) exactly, discarding
        /// the entire old 2x3 toggle/dropdown/Restore-Purchases grid. landing.png at 50% opacity
        /// (a new "dimmed backdrop" treatment — every other overlay/screen in this project uses its
        /// background art at full opacity) sits behind Logo.png top-left and SettingsSign.png as the
        /// header, same insets those already used. A 4x2 GridLayoutGroup of plaque cells follows:
        /// row 1 is wired (Shop.png — relocated here from Level Select, Btn_music-remove.png,
        /// Btn_LeaderBoard.png, Btn_CharacterStory.png), row 2 is 4 deliberately blank Btn_plaque.png
        /// cells — same "leave it empty rather than invent a feature" convention this grid has used
        /// before (e.g. its old 6th cell), reserved for whatever gets planned into them next. All
        /// art is baked directly at construction time (self-contained, same convention Shop/
        /// Cosmetics use) rather than looked up by ArtWiringBuilder afterward.
        ///
        /// Music/SFX/Vibration/Left-Handed/Language and IAP Restore Purchases are no longer in the
        /// visible UI at all — SaveManager still has the underlying MusicOn/SfxOn/VibrationOn/
        /// LeftHanded/Language state and IAPManager.RestorePurchases still exists, just nothing
        /// here calls them anymore. Restore Purchases in particular has no Settings surface right
        /// now (Apple requires one for non-consumables) — flagged as a gap, not silently dropped;
        /// revisit once one of the 4 blank cells gets planned for it.</summary>
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

            // 4 columns x StandardIconButtonSize cells, evenly spaced — icons and blank cells alike
            // are all the same size (see the cross-screen layout standards note above).
            var gridGO = new GameObject("SettingsGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGO.transform.SetParent(root.transform, false);
            var gridRect = (RectTransform)gridGO.transform;
            gridRect.anchorMin = gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            // Measured directly off the Settings benchmark mockup (gridline analysis): adjacent
            // icons sit ~4% of screen width apart (icon width ~11%, gap ~4%, of a 1280px-wide
            // reference), not the ~2.6% gap the old 50px spacing produced in this 1920-wide canvas
            // (50/1920 = 2.6%). 0.04*1920 = 76.8, rounded to 77 — matches the benchmark's actual
            // icon-to-icon spacing in both axes (GridLayoutGroup.spacing applies this uniformly).
            float cellSpacing = 77f;
            float gridWidth = 4 * StandardIconButtonSize + 3 * cellSpacing;
            float gridHeight = 2 * StandardIconButtonSize + cellSpacing;
            gridRect.sizeDelta = new Vector2(gridWidth + 100f, gridHeight + 60f);
            // Centered in the vertical space between the header's bottom edge (365px from screen
            // top, from StandardHeaderSignOffset/Size above) and the screen's own bottom edge
            // (1080px) rather than sitting bunched just under the header — per feedback that the
            // grid needs to "sit nicely middle aligned" with the empty space below it. Midpoint of
            // that 365-1080 zone is 722.5px from top regardless of the grid's own height (centering
            // targets the midpoint, not an edge), so this offset doesn't need to change when
            // cellSpacing above changes gridHeight. Verified clear of CreateRoundBackButton's
            // bottom-right corner button by X position alone: gridWidth=4*160+3*77=871, container
            // sizeDelta.x=971, so the grid's own edge maxes out at canvas X=+485.5 even at this
            // wider spacing; the back button's left edge sits at X=+650 — a ~164-unit gap — so this
            // shift can't crowd it regardless of any Y-range overlap.
            gridRect.anchoredPosition = new Vector2(0f, -183f);
            var grid = gridGO.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(StandardIconButtonSize, StandardIconButtonSize);
            grid.spacing = new Vector2(cellSpacing, cellSpacing);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            var shopButton = CreateIconButton("ShopCell", gridGO.transform, LoadUiSprite("Shop.png"), StandardIconButtonSize);
            var musicButton = CreateIconButton("MusicCell", gridGO.transform, LoadUiSprite("Btn_music-remove.png"), StandardIconButtonSize);
            var leaderboardsButton = CreateIconButton("LeaderboardCell", gridGO.transform, LoadUiSprite("Btn_LeaderBoard.png"), StandardIconButtonSize);
            var characterStoryButton = CreateIconButton("CharacterStoryCell", gridGO.transform, LoadUiSprite("Btn_CharacterStory.png"), StandardIconButtonSize);

            // Row 2, cell 0 — Restore Purchases (2026-08-23; was blank). Apple requires a restore
            // entry point somewhere for non-consumable IAPs (Remove Ads, hats, trails); no dedicated
            // icon art exists for it, so it reuses Icon_bare.png as a backing plaque with a plain
            // text label on top (same "text label until art lands" convention SkipCooldownButton's
            // "-3" used before Btn_skipcooldown.png existed) — the only row-2 cell that isn't just a
            // raycast-disabled placeholder Image, since this one needs to actually be tappable.
            var restorePurchasesButton = CreateButton("RestorePurchasesCell", gridGO.transform, "Restore",
                Color.white, 22f, StandardIconButtonSize, out var restorePurchasesLabel);
            var restorePurchasesImage = restorePurchasesButton.GetComponent<Image>();
            restorePurchasesImage.sprite = LoadUiSprite("Icon_bare.png");
            restorePurchasesImage.preserveAspect = true;
            ((RectTransform)restorePurchasesButton.transform).sizeDelta = new Vector2(StandardIconButtonSize, StandardIconButtonSize);
            restorePurchasesLabel.color = new Color(0.3f, 0.2f, 0.1f); // dark brown, readable against Icon_bare's light parchment tone
            // Shrink-to-fit so "Restore" (and Icon_bare's own real aspect once wired) never spills
            // past this small icon-sized slot — same convention used elsewhere for text-in-a-box.
            restorePurchasesLabel.enableAutoSizing = true;
            restorePurchasesLabel.fontSizeMin = 14f;
            restorePurchasesLabel.fontSizeMax = 22f;
            restorePurchasesLabel.overflowMode = TextOverflowModes.Truncate;

            // Row 2, cell 1 (2026-08-25) — "New Worlds" (WorldMaze.png), opens the World Purchase
            // screen; was blank. Cells 2-3 still deliberately blank, reserved for whatever's next.
            var worldsButton = CreateIconButton("WorldsCell", gridGO.transform, LoadUiSprite("WorldMaze.png"), StandardIconButtonSize);

            for (int i = 2; i < 4; i++)
            {
                var blankImage = CreateImage($"BlankCell{i}", gridGO.transform, Color.white, StandardIconButtonSize, StandardIconButtonSize);
                blankImage.sprite = LoadUiSprite("Icon_bare.png");
                blankImage.preserveAspect = true;
                blankImage.raycastTarget = false;
                ((RectTransform)blankImage.transform).sizeDelta = new Vector2(StandardIconButtonSize, StandardIconButtonSize);
            }

            // Small feedback row (Restoring.../Purchases restored!/Restore failed.), bottom-centre —
            // same convention Shop/Cosmetic purchase screens' own statusText already use, positioned
            // clear of the bottom-right back button.
            var restoreStatusText = CreateText("RestoreStatusText", root.transform, string.Empty, 24f, TextAlignmentOptions.Center, 40f);
            AnchorBottomCenter((RectTransform)restoreStatusText.transform, new Vector2(700f, 40f), new Vector2(0f, 40f));

            var controller = root.AddComponent<SettingsPanel>();
            var so = new SerializedObject(controller);
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("shopButton").objectReferenceValue = shopButton;
            so.FindProperty("musicButton").objectReferenceValue = musicButton;
            so.FindProperty("musicButtonIcon").objectReferenceValue = musicButton.GetComponent<Image>();
            so.FindProperty("leaderboardsButton").objectReferenceValue = leaderboardsButton;
            so.FindProperty("characterStoryButton").objectReferenceValue = characterStoryButton;
            so.FindProperty("restorePurchasesButton").objectReferenceValue = restorePurchasesButton;
            so.FindProperty("restoreStatusText").objectReferenceValue = restoreStatusText;
            so.FindProperty("worldsButton").objectReferenceValue = worldsButton;
            // shopScreen/leaderboardsScreen/characterStoryScreen/mainMenuScreen/worldPurchaseScreen
            // are wired later in BuildAll's WireCrossReferences, once those screens actually exist.
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        /// <summary>Real content screen for "Btn_CharacterStory" (2026-08-21 follow-up — was a
        /// "Coming Soon" placeholder until the actual narrative/character copy was written). Matches
        /// the rest of the Settings-family redesign: dimmed Landing_Opacity.png backdrop, no
        /// top-left LogoImage (removed per request — every other screen in this family still has
        /// one, this is the one deliberate exception), and Btn_back kept at its existing
        /// bottom-right position (CreateRoundBackButton's default).
        ///
        /// The WHOLE screen scrolls as one list now (per feedback: a separate fixed-position intro
        /// box above a fixed-height row area meant a tall intro — it grows at runtime to fit its own
        /// copy, see CharacterStoryScreen.ResizeIntroContainerToFitText — ate directly into the space
        /// left for character cards). The framed intro box (IntroBorder/IntroBackground/IntroText)
        /// is now the FIRST item inside the same vertical ScrollRect/Content the character rows live
        /// in, sized to the same RowWidth so it lines up with every row beneath it, rather than a
        /// separate element positioned above a second, independently-sized scroll view. Both the
        /// intro copy and the per-character blurbs are populated at runtime by CharacterStoryScreen
        /// (DataManager isn't available in Edit mode); this method only builds the empty layout and
        /// wires the introText/cardContainer references.</summary>
        private static GameObject BuildCharacterStoryPlaceholder(Transform canvasTransform, GameObject characterSelectCardPrefab)
        {
            var root = CreatePanel("CharacterStoryScreen", canvasTransform, Color.black);
            ApplyDimmedLandingBackground(root);

            // Scroll view now spans nearly the full screen (40px top margin, 140px bottom margin for
            // the back button) — everything below, including the intro box, lives inside its Content
            // and scrolls together as one list.
            var scrollRect = CreateVerticalScrollView("CharacterScrollView", root.transform, out var cardContainer);
            var scrollRT = (RectTransform)scrollRect.transform;
            scrollRT.anchorMin = new Vector2(0f, 0f);
            scrollRT.anchorMax = new Vector2(0f, 1f);
            scrollRT.pivot = new Vector2(0f, 0.5f);
            const float scrollTopMargin = 40f;
            const float scrollBottomMargin = 140f; // clears the back button
            scrollRT.anchoredPosition = new Vector2(100f, (scrollBottomMargin - scrollTopMargin) / 2f);
            scrollRT.sizeDelta = new Vector2(1700f, -(scrollTopMargin + scrollBottomMargin));

            // Framed intro box — now the first child inside cardContainer (same list the character
            // rows are appended to at runtime, see CharacterStoryScreen.PopulateIfNeeded), sized to
            // CharacterStoryScreen.RowWidth so it shares the same left/right edges as every row below
            // it. No dedicated wood-sign art exists for a box this shape/size, so the "border" is a
            // plain two-layer Image composition (an outer gold border colour with a slightly inset,
            // darker semi-transparent inner panel) rather than uploaded art — same
            // PlaceholderSprite.Get(color) convention used everywhere else in this project a visual
            // is needed before real art exists. Its own sizeDelta is set explicitly (not left to a
            // LayoutElement) since cardContainer's VerticalLayoutGroup has childControlHeight/Width =
            // false (CreateVerticalScrollView's convention) and reads each child's raw sizeDelta
            // directly — same reason every character row below sets its own sizeDelta too.
            var introBorder = CreateImage("IntroBorder", cardContainer, new Color(0.70f, 0.55f, 0.20f), CharacterStoryScreen.RowWidth, 260f);
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

            var closeButton = CreateRoundBackButton(root.transform);
            closeButton.GetComponent<Image>().sprite = LoadUiSprite("Btn_back.png");

            var story = root.AddComponent<CharacterStoryScreen>();
            SetRefs(story,
                ("cardContainer", cardContainer),
                ("cardPrefab", characterSelectCardPrefab),
                ("closeButton", closeButton),
                ("introText", introText),
                ("introBorderRect", introBorder.transform),
                ("introBackgroundRect", introBackground.transform));

            return root;
        }

        // ---- Shop / Cosmetics (2026-08-20 redesign — matches the new Shop/Cosmetics/Hats/Trails
        // mockups exactly; replaces the old plain-text-button coin grid and the tab-based
        // CosmeticStoreScreen entirely) ----------------------------------------------------------

        private const string UISpriteFolder = "Assets/_Project/Sprites/UI";
        private const string CosmeticsChromeFolder = "Assets/_Project/Sprites/Cosmetics";
        private const string HatArtFolder = "Assets/_Project/Sprites/Cosmetics/Cosmetics_Type_Hat";
        private const string TrailArtFolder = "Assets/_Project/Sprites/Cosmetics/CosmeticType.Trail";
        private const string MazeThemeArtFolder = "Assets/_Project/Sprites/Cosmetics/CosmeticType_MazeTheme";

        /// <summary>Configures a texture as a Sprite (PPU = its own width, same convention every
        /// other sprite-importer pass in this project uses) and loads it. Self-contained rather
        /// than depending on ArtWiringBuilder having already run, since these screens are built
        /// earlier in the standard Phase2->Phase5->ArtWiringBuilder rebuild chain — mirrors
        /// CosmeticWiringBuilder.ConfigureAndLoadSprite's same self-contained approach.</summary>
        private static Sprite ConfigureAndLoadCosmeticChromeSprite(string path)
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
        private static Sprite LoadCosmeticsSprite(string fileName) => ConfigureAndLoadCosmeticChromeSprite($"{CosmeticsChromeFolder}/{fileName}");
        private static Sprite LoadHatArtSprite(string fileName) => ConfigureAndLoadCosmeticChromeSprite($"{HatArtFolder}/{fileName}");
        private static Sprite LoadTrailArtSprite(string fileName) => ConfigureAndLoadCosmeticChromeSprite($"{TrailArtFolder}/{fileName}");
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

        /// <summary>Coin-pack purchase screen — landing.png background (matching every mockup in
        /// this redesign), ShopBanner.png "Shop" sign, 4 self-contained coin-pack plaques
        /// (100/500/5000/15000.png each bake in their own coin count + $ price, so no separate
        /// label text is built on top the way the old plain-text-button version needed), a
        /// Btn_Cosmetics.png button opening the new cosmetics hub, and a round back button. No
        /// dedicated triangle/mountain back icon was uploaded for this mockup (same substitution
        /// convention CreateRoundBackButton's own doc comment describes for Choose Character) —
        /// Btn_back.png stands in. Root/overlay name kept as "StoreComingSoonOverlay" for
        /// scene-path stability (ArtWiringBuilder and any future screenshot/test tooling that looks
        /// it up by path) even though its content has been rebuilt from scratch twice now.</summary>
        private static GameObject BuildShopOverlay(Transform canvasTransform)
        {
            var root = CreatePanel("StoreComingSoonOverlay", canvasTransform, Color.black);
            ApplyDimmedLandingBackground(root);

            CreateHeaderSign(root.transform, LoadUiSprite("ShopBanner.png"));

            // 4-column single-row grid (same construction as Settings' icon grid) instead of a
            // HorizontalLayoutGroup — a layout group with childControlWidth=true stretches each
            // child to fill whatever width remains, which doesn't reliably produce an exact
            // StandardIconButtonSize square; GridLayoutGroup guarantees it.
            // Coin plaque boards enlarged (160 -> 200) to more closely match how big Settings' own
            // icons READ on screen — the coin-pack art (100.png etc.) bakes in more of its own
            // transparent margin than Settings' tightly-cropped icons, so even at the identical
            // StandardIconButtonSize box it rendered visibly smaller; a bigger box compensates.
            float coinIconSize = StandardIconButtonSize * 1.25f;
            var coinRowGO = new GameObject("CoinRow", typeof(RectTransform), typeof(GridLayoutGroup));
            coinRowGO.transform.SetParent(root.transform, false);
            var coinRowRect = (RectTransform)coinRowGO.transform;
            coinRowRect.anchorMin = coinRowRect.anchorMax = new Vector2(0.5f, 0.5f);
            coinRowRect.pivot = new Vector2(0.5f, 0.5f);
            float coinRowSpacing = 50f;
            coinRowRect.sizeDelta = new Vector2(4 * coinIconSize + 3 * coinRowSpacing + 100f, coinIconSize + 40f);
            coinRowRect.anchoredPosition = new Vector2(0f, 20f);
            var coinGrid = coinRowGO.GetComponent<GridLayoutGroup>();
            coinGrid.cellSize = new Vector2(coinIconSize, coinIconSize);
            coinGrid.spacing = new Vector2(coinRowSpacing, 0f);
            coinGrid.childAlignment = TextAnchor.MiddleCenter;
            coinGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            coinGrid.constraintCount = 4;

            var coinDefs = new (string productId, string fileName)[]
            {
                (IAPManager.Coins100ProductId, "100.png"),
                (IAPManager.Coins500ProductId, "500.png"),
                (IAPManager.Coins5000ProductId, "5000.png"),
                (IAPManager.Coins15000ProductId, "15000.png"),
            };

            var coinButtonsData = new (string id, Button button)[coinDefs.Length];
            for (int i = 0; i < coinDefs.Length; i++)
            {
                var (id, fileName) = coinDefs[i];
                coinButtonsData[i] = (id, CreateIconButton(id + "Button", coinRowGO.transform, LoadUiSprite(fileName), coinIconSize));
            }

            // Btn_Cosmetics doubled in size (160 -> 320) and given more clearance off the bottom
            // edge (70 -> 100 padding) per request. Shifted left of centre (was plain (0,100)) to
            // make room for RemoveAdsButton beside it — see that button's own comment below for why
            // it needed a home on this screen at all.
            float cosmeticsButtonSize = StandardIconButtonSize * 2f;
            const float removeAdsButtonWidth = 300f;
            const float removeAdsButtonHeight = 90f;
            const float shopBottomRowGap = 40f;
            float shopBottomRowPairWidth = cosmeticsButtonSize + shopBottomRowGap + removeAdsButtonWidth;
            float cosmeticsButtonCenterX = -shopBottomRowPairWidth / 2f + cosmeticsButtonSize / 2f;
            float removeAdsButtonCenterX = shopBottomRowPairWidth / 2f - removeAdsButtonWidth / 2f;
            var cosmeticsButton = CreateIconButton("CosmeticsButton", root.transform, LoadUiSprite("Btn_Cosmetics.png"), cosmeticsButtonSize);
            var cosmeticsButtonRect = (RectTransform)cosmeticsButton.transform;
            AnchorBottomCenter(cosmeticsButtonRect, new Vector2(cosmeticsButtonSize, cosmeticsButtonSize), new Vector2(cosmeticsButtonCenterX, 100f));

            // Remove Ads — the Shop mockup this screen was built to only showed the 4 coin packs,
            // so this IAP product (registered in IAPManager since Phase 3) had no purchase surface
            // at all until now. No dedicated square icon art exists for it yet, so it keeps a plain
            // text label (same "text label until art lands" convention SkipCooldownButton's "-3"
            // used before Btn_skipcooldown.png existed) — a rectangular plaque rather than forcing
            // it into the same square icon shape as the coin packs/Cosmetics, since a text label
            // needs the extra width to read clearly. Vertically centred against Cosmetics' own
            // centre (100 + cosmeticsButtonSize/2), not sharing its bottom edge, since the two
            // buttons are very different heights.
            float cosmeticsButtonCenterY = 100f + cosmeticsButtonSize / 2f;
            var removeAdsButton = CreateButton("RemoveAdsButton", root.transform, "Remove Ads",
                new Color(0.85f, 0.55f, 0.1f), 30f, removeAdsButtonHeight, out var removeAdsLabel);
            AnchorBottomCenter((RectTransform)removeAdsButton.transform, new Vector2(removeAdsButtonWidth, removeAdsButtonHeight),
                new Vector2(removeAdsButtonCenterX, cosmeticsButtonCenterY - removeAdsButtonHeight / 2f));

            // New Worlds' top-right icon was removed from this screen (2026-08-25 review) — the
            // World Purchase screen is still reachable from Settings (row-2 WorldsCell) and from
            // tapping a locked shield directly in Level Select, so the feature itself is unaffected.
            var closeButton = CreateRoundBackButton(root.transform, bottomRight: true);
            closeButton.GetComponent<Image>().sprite = LoadUiSprite("Btn_back.png");

            // Raised (250 -> cosmeticsButton's own top edge + 40) so it clears the now much taller
            // CosmeticsButton beneath it instead of landing inside its bounds.
            var statusText = CreateText("StatusText", root.transform, string.Empty, 26f, TextAlignmentOptions.Center, 40f);
            AnchorBottomCenter((RectTransform)statusText.transform, new Vector2(860f, 40f), new Vector2(0f, 100f + cosmeticsButtonSize + 40f));

            var shop = root.AddComponent<ShopController>();
            var shopSO = new SerializedObject(shop);
            var purchaseDefs = new (string id, Button button)[coinButtonsData.Length + 1];
            for (int i = 0; i < coinButtonsData.Length; i++)
            {
                purchaseDefs[i] = coinButtonsData[i];
            }
            purchaseDefs[coinButtonsData.Length] = (IAPManager.RemoveAdsProductId, removeAdsButton);
            var arrayProp = shopSO.FindProperty("purchaseButtons");
            arrayProp.arraySize = purchaseDefs.Length;
            for (int i = 0; i < purchaseDefs.Length; i++)
            {
                var element = arrayProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("productId").stringValue = purchaseDefs[i].id;
                element.FindPropertyRelative("button").objectReferenceValue = purchaseDefs[i].button;
            }
            shopSO.FindProperty("statusText").objectReferenceValue = statusText;
            shopSO.FindProperty("closeButton").objectReferenceValue = closeButton;
            shopSO.FindProperty("cosmeticsButton").objectReferenceValue = cosmeticsButton;
            shopSO.FindProperty("removeAdsButton").objectReferenceValue = removeAdsButton;
            shopSO.FindProperty("removeAdsLabel").objectReferenceValue = removeAdsLabel;
            // cosmeticsHubScreen is wired later in BuildAll, once that screen has actually been
            // created (cross-screen reference, same deferred-wiring
            // pattern WireCrossReferences uses for every other screen-to-screen link).
            shopSO.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        /// <summary>Cosmetics category hub — landing.png background, Cosmetics.png "Cosmetics"
        /// sign, and 3 icons (Hat_Icon.png / Trails_Tab_Icon.png "comet" / MazeThemeTab.png "map").
        /// The map icon is shown (matching the mockup) but deliberately non-interactive — maze
        /// reskinning as a cosmetic was tried and dropped in favor of purchasable whole new worlds
        /// instead (see BuildShopOverlay's "New Worlds" section), so this icon has no destination
        /// screen to open.</summary>
        private static GameObject BuildCosmeticsHubScreen(Transform canvasTransform)
        {
            var root = CreatePanel("CosmeticsHubScreen", canvasTransform, Color.black);
            ApplyDimmedLandingBackground(root);

            CreateHeaderSign(root.transform, LoadUiSprite("Cosmetics.png"));

            var iconRow = CreateHorizontalGroup("IconRow", root.transform, 50f);
            var iconRowRect = (RectTransform)iconRow.transform;
            iconRowRect.anchorMin = iconRowRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRowRect.pivot = new Vector2(0.5f, 0.5f);
            iconRowRect.sizeDelta = new Vector2(2 * StandardIconButtonSize + 50f + 60f, StandardIconButtonSize + 20f);
            iconRowRect.anchoredPosition = Vector2.zero;
            iconRow.GetComponent<LayoutElement>().preferredHeight = StandardIconButtonSize;
            var iconRowHlg = iconRow.GetComponent<HorizontalLayoutGroup>();
            iconRowHlg.childControlWidth = false;
            iconRowHlg.childForceExpandWidth = false;
            iconRowHlg.childControlHeight = false;
            iconRowHlg.childForceExpandHeight = false;
            iconRowHlg.childAlignment = TextAnchor.MiddleCenter;

            var hatButton = CreateIconButton("HatButton", iconRow.transform, LoadCosmeticsSprite("Hat_Icon.png"), StandardIconButtonSize);
            var trailButton = CreateIconButton("TrailButton", iconRow.transform, LoadCosmeticsSprite("Trails_Tab_Icon.png"), StandardIconButtonSize);

            var closeButton = CreateRoundBackButton(root.transform, bottomRight: true);
            closeButton.GetComponent<Image>().sprite = LoadUiSprite("Btn_back.png");

            var hub = root.AddComponent<CosmeticsHubScreen>();
            var so = new SerializedObject(hub);
            so.FindProperty("hatButton").objectReferenceValue = hatButton;
            so.FindProperty("trailButton").objectReferenceValue = trailButton;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            // hatPurchaseScreen/trailPurchaseScreen are wired later in BuildAll, once those screens
            // have actually been created (same deferred cross-screen-reference pattern as Shop's
            // own cosmeticsHubScreen field above).
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        /// <summary>Generic cosmetic-style purchase screen — reused for both Hats (3 items:
        /// Baseball Cap / Cowboy Hat / Sombrero) and Trails (4 items: Corn Husk / Rainbow Ribbon /
        /// Sparkle Dust / Ember), since both mockups share the same layout: a breadcrumb icon, a
        /// row of framed purchase items (each item's own wood-frame art is baked directly into the
        /// button, same "art baked at construction time" convention the old CosmeticCardController
        /// used for PurchaseCardFrame.png), a single price plaque (every item on a given screen is
        /// $3.99), and a round back button. Every item purchases via IAPManager directly on tap —
        /// see CosmeticPurchaseScreen's own doc comment for the purchase/grant flow.</summary>
        private static GameObject BuildCosmeticPurchaseScreen(Transform canvasTransform, string screenName,
            Sprite breadcrumbSprite, (string productId, Sprite frameSprite)[] items, Sprite priceSprite)
        {
            var root = CreatePanel(screenName, canvasTransform, Color.black);
            ApplyDimmedLandingBackground(root);

            var breadcrumbGO = new GameObject("BreadcrumbIcon", typeof(RectTransform), typeof(Image));
            breadcrumbGO.transform.SetParent(root.transform, false);
            var breadcrumbImage = breadcrumbGO.GetComponent<Image>();
            breadcrumbImage.sprite = breadcrumbSprite;
            breadcrumbImage.preserveAspect = true;
            // Top padding increased 30 -> 62 (+32px) — the icon was overlapping the dimmed poster's
            // baked-in "FARM FURY" wordmark at the original offset (per a screenshot review).
            AnchorTopCenter((RectTransform)breadcrumbGO.transform, new Vector2(StandardIconButtonSize, StandardIconButtonSize), new Vector2(0f, -62f));

            // Item icons doubled (160 -> 320) and the row's own spacing scaled up to match (50 ->
            // 100), so the gaps between icons stay visually proportional to the larger art instead
            // of reading cramped — same "pad the row" request. Row itself nudged up (20 -> 40) to
            // tighten the gap to the breadcrumb icon above now that both moved.
            const float itemIconSize = StandardIconButtonSize * 2f;
            const float itemSpacing = 100f;
            var itemRow = CreateHorizontalGroup("ItemRow", root.transform, itemSpacing);
            var itemRowRect = (RectTransform)itemRow.transform;
            itemRowRect.anchorMin = itemRowRect.anchorMax = new Vector2(0.5f, 0.5f);
            itemRowRect.pivot = new Vector2(0.5f, 0.5f);
            itemRowRect.sizeDelta = new Vector2(items.Length * itemIconSize + (items.Length - 1) * itemSpacing + 60f, itemIconSize + 20f);
            itemRowRect.anchoredPosition = new Vector2(0f, 40f);
            itemRow.GetComponent<LayoutElement>().preferredHeight = itemIconSize;
            var itemRowHlg = itemRow.GetComponent<HorizontalLayoutGroup>();
            itemRowHlg.childControlWidth = false;
            itemRowHlg.childForceExpandWidth = false;
            itemRowHlg.childControlHeight = false;
            itemRowHlg.childForceExpandHeight = false;
            itemRowHlg.childAlignment = TextAnchor.MiddleCenter;

            var itemButtonsData = new (string id, Button button)[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                itemButtonsData[i] = (items[i].productId, CreateIconButton($"Item{i}Button", itemRow.transform, items[i].frameSprite, itemIconSize));
            }

            var priceGO = new GameObject("PriceSign", typeof(RectTransform), typeof(Image));
            priceGO.transform.SetParent(root.transform, false);
            var priceImage = priceGO.GetComponent<Image>();
            priceImage.sprite = priceSprite;
            priceImage.preserveAspect = true;
            // Enlarged ~1.4x (360x190 -> 500x266, same aspect) to read clearly against the now much
            // bigger item icons above it; bottom offset kept at 90 rather than scaled the same 2x
            // the icons got, tightening its gap to the row instead of letting it drift apart.
            AnchorBottomCenter((RectTransform)priceGO.transform, new Vector2(500f, 266f), new Vector2(0f, 90f));

            var closeButton = CreateRoundBackButton(root.transform, bottomRight: true);
            closeButton.GetComponent<Image>().sprite = LoadUiSprite("Btn_back.png");

            var statusText = CreateText("StatusText", root.transform, string.Empty, 24f, TextAlignmentOptions.Center, 40f);
            AnchorBottomCenter((RectTransform)statusText.transform, new Vector2(860f, 40f), new Vector2(0f, 20f));

            var screen = root.AddComponent<CosmeticPurchaseScreen>();
            var so = new SerializedObject(screen);
            var arrayProp = so.FindProperty("itemButtons");
            arrayProp.arraySize = itemButtonsData.Length;
            for (int i = 0; i < itemButtonsData.Length; i++)
            {
                var element = arrayProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("productId").stringValue = itemButtonsData[i].id;
                element.FindPropertyRelative("button").objectReferenceValue = itemButtonsData[i].button;
            }
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        /// <summary>World Purchase screen — rebuilt (2026-08-25 review) to use real per-world
        /// shield art (FrozenGarden_shield.png/GoldenSunset_shield.png/HarvestMoon_shield.png,
        /// dropped in the same session) as actual positioned/sized Buttons, replacing an earlier
        /// version that stretched the raw supplied mockup image full-screen as one flat background
        /// with invisible hotspots on top — that approach couldn't be tightened (the shields were
        /// baked pixels in one picture) and visibly spilled past the safe-area guide on a real
        /// device aspect. landing.png (full brightness, not the dimmed-poster convention other
        /// overlays use — matches the mockup's own vivid look) is the background now instead.
        ///
        /// All 3 worlds have real IAP products and real, verified 25-level sets behind them
        /// (IAPManager.WorldFrostbiteGardenProductId/WorldGoldenSunsetProductId/
        /// WorldHarvestMoonProductId — see UnlockProgression's purchase-gated world handling), so
        /// all 3 shields are wired into itemButtons. comingSoonButtons stays empty for now but is
        /// left wired on the component (empty array, harmless) in case a future 4th shield needs
        /// the same "shown but not purchasable yet" treatment before its own content is ready.</summary>
        private static GameObject BuildWorldPurchaseScreen(Transform canvasTransform)
        {
            var root = CreatePanel("WorldPurchaseScreen", canvasTransform, Color.black);
            var backgroundImage = root.GetComponent<Image>();
            backgroundImage.sprite = LoadUiSprite("landing.png");
            backgroundImage.color = Color.white;

            var badgeGO = new GameObject("WorldMazeBadge", typeof(RectTransform), typeof(Image));
            badgeGO.transform.SetParent(root.transform, false);
            var badgeImage = badgeGO.GetComponent<Image>();
            badgeImage.sprite = LoadUiSprite("WorldMaze.png");
            badgeImage.preserveAspect = true;
            AnchorTopCenter((RectTransform)badgeGO.transform, new Vector2(220f, 220f), new Vector2(0f, -70f));

            // Tighter than a normal CreateIconButton row elsewhere in this project (per feedback:
            // "bring the shields slightly closer together"), and sized to comfortably clear the
            // safe-area guide on every side — total row width (3*380 + 2*30 + 40 = 1240) leaves a
            // generous ~340px margin on each side of the 1920-wide canvas.
            const float shieldSize = 380f;
            const float shieldSpacing = 30f;
            var shieldRow = CreateHorizontalGroup("ShieldRow", root.transform, shieldSpacing);
            var shieldRowRect = (RectTransform)shieldRow.transform;
            shieldRowRect.anchorMin = shieldRowRect.anchorMax = new Vector2(0.5f, 0.5f);
            shieldRowRect.pivot = new Vector2(0.5f, 0.5f);
            shieldRowRect.sizeDelta = new Vector2(3 * shieldSize + 2 * shieldSpacing + 40f, shieldSize + 20f);
            shieldRowRect.anchoredPosition = new Vector2(0f, -30f);
            shieldRow.GetComponent<LayoutElement>().preferredHeight = shieldSize;
            var shieldRowHlg = shieldRow.GetComponent<HorizontalLayoutGroup>();
            shieldRowHlg.childControlWidth = false;
            shieldRowHlg.childForceExpandWidth = false;
            shieldRowHlg.childControlHeight = false;
            shieldRowHlg.childForceExpandHeight = false;
            shieldRowHlg.childAlignment = TextAnchor.MiddleCenter;

            var goldenSunsetButton = CreateIconButton("GoldenSunsetButton", shieldRow.transform, LoadMazeThemeArtSprite("GoldenSunset_shield.png"), shieldSize);
            var frozenGardenButton = CreateIconButton("FrozenGardenButton", shieldRow.transform, LoadMazeThemeArtSprite("FrozenGarden_shield.png"), shieldSize);
            var harvestMoonButton = CreateIconButton("HarvestMoonButton", shieldRow.transform, LoadMazeThemeArtSprite("HarvestMoon_shield.png"), shieldSize);

            var priceGO = new GameObject("PriceSign", typeof(RectTransform), typeof(Image));
            priceGO.transform.SetParent(root.transform, false);
            var priceImage = priceGO.GetComponent<Image>();
            priceImage.sprite = LoadUiSprite("3.99.png");
            priceImage.preserveAspect = true;
            AnchorBottomCenter((RectTransform)priceGO.transform, new Vector2(340f, 180f), new Vector2(0f, 130f));

            var closeButton = CreateRoundBackButton(root.transform, bottomRight: true);
            closeButton.GetComponent<Image>().sprite = LoadUiSprite("Btn_back.png");

            var statusText = CreateText("StatusText", root.transform, string.Empty, 26f, TextAlignmentOptions.Center, 40f);
            AnchorBottomCenter((RectTransform)statusText.transform, new Vector2(860f, 40f), new Vector2(0f, 60f));

            var screen = root.AddComponent<CosmeticPurchaseScreen>();
            var so = new SerializedObject(screen);
            var items = new (string productId, Button button)[]
            {
                (IAPManager.WorldGoldenSunsetProductId, goldenSunsetButton),
                (IAPManager.WorldFrostbiteGardenProductId, frozenGardenButton),
                (IAPManager.WorldHarvestMoonProductId, harvestMoonButton),
            };
            var arrayProp = so.FindProperty("itemButtons");
            arrayProp.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                arrayProp.GetArrayElementAtIndex(i).FindPropertyRelative("productId").stringValue = items[i].productId;
                arrayProp.GetArrayElementAtIndex(i).FindPropertyRelative("button").objectReferenceValue = items[i].button;
            }

            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.ApplyModifiedPropertiesWithoutUndo();

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

            // Monetisation rewarded-ad placement #2 ("double coins" — see CLAUDE.md's Monetisation
            // section and LevelCompleteController's own doc comment). Bottom-right, in the space
            // Home/Settings used to share (both removed 2026-08-20 per a screenshot review — Play
            // is the only real navigation left on this screen) — mirrors PlayButton's own
            // bottom-left inset/size exactly (StandardIconButtonSize, 150/110 inset) for symmetry.
            // Previously a wide 240x90 text-label button; DoubleCoins.png is actually square
            // (501x500), so that box squashed it badly (the same Sliced-ignores-preserveAspect
            // box-aspect-must-match-the-art bug this project has hit repeatedly elsewhere) — square
            // box + real art + preserveAspect fixes it, and the auto-created text label is
            // destroyed (icon only now, matching the mockup review that called out "remove the
            // white text overlay").
            var doubleCoinsButton = CreateIconButton("DoubleCoinsButton", root.transform, LoadUiSprite("DoubleCoins.png"), StandardIconButtonSize);
            AnchorBottomRight((RectTransform)doubleCoinsButton.transform, new Vector2(StandardIconButtonSize, StandardIconButtonSize), new Vector2(-150f, 110f));

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
            so.FindProperty("doubleCoinsButton").objectReferenceValue = doubleCoinsButton;
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

        /// <summary>Rebuilt (2026-08-20) to match the rest of this redesign wave — dimmed
        /// landing.png backdrop, Leaderboard.png as the standardized header sign (reused from its
        /// old corner-button role — same wood-sign convention as ShopBanner.png/Cosmetics.png/
        /// SettingsSign.png), stats centred below it, and a round back button matching every other
        /// screen in this suite (was CreateGenericBackButton's rectangular Btn_back before).</summary>
        private static GameObject BuildLeaderboards(Transform canvasTransform)
        {
            var root = CreatePanel("LeaderboardsScreen", canvasTransform, Color.black);
            ApplyDimmedLandingBackground(root);

            CreateHeaderSign(root.transform, LoadUiSprite("Leaderboard.png"));

            var statsText = CreateText("StatsText", root.transform, string.Empty, 32f, TextAlignmentOptions.Center, 260f, Color.white);
            var statsRect = (RectTransform)statsText.transform;
            statsRect.anchorMin = statsRect.anchorMax = new Vector2(0.5f, 0.5f);
            statsRect.pivot = new Vector2(0.5f, 0.5f);
            statsRect.sizeDelta = new Vector2(900f, 260f);
            statsRect.anchoredPosition = new Vector2(0f, -40f);

            // Moved bottom-left -> bottom-right (per a screenshot review) to match every other
            // screen in this redesign wave — Settings, Shop, Cosmetics hub, Hat/Trail purchase all
            // use bottomRight: true; Leaderboards was the one outlier still sitting on the left.
            var backButton = CreateRoundBackButton(root.transform, bottomRight: true);
            backButton.GetComponent<Image>().sprite = LoadUiSprite("Btn_back.png");

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
            ComboNotificationBanner comboBanner, GameObject levelSelect, GameObject shop, GameObject characterStory, GameObject worldPurchase)
        {
            var settingsPanel = settings.GetComponent<SettingsPanel>();
            SetRefs(settingsPanel,
                ("leaderboardsScreen", leaderboards),
                ("shopScreen", shop.GetComponent<ShopController>()), ("characterStoryScreen", characterStory),
                ("worldPurchaseScreen", worldPurchase.GetComponent<CosmeticPurchaseScreen>()));

            SetRefs(mainMenu.GetComponent<MainMenuController>(),
                ("levelSelectScreen", levelSelect), ("settingsPanel", settingsPanel));

            SetRefs(levelSelect.GetComponent<LevelSelectController>(),
                ("mainMenuScreen", mainMenu), ("gameplayScreen", gameplay),
                ("worldPurchaseScreen", worldPurchase.GetComponent<CosmeticPurchaseScreen>()));

            var hud = gameplay.GetComponent<GameplayHUD>();
            SetRefs(hud,
                ("pauseMenu", pause.GetComponent<PauseMenuController>()),
                ("levelCompleteScreen", levelComplete), ("levelFailedScreen", levelFailed));

            SetRefs(pause.GetComponent<PauseMenuController>(),
                ("chooseCharacterScreen", chooseCharacterScreen), ("settingsPanel", settingsPanel), ("levelSelectScreen", levelSelect));

            SetRefs(chooseCharacterScreen, ("pauseMenuScreen", pause));

            SetRefs(levelComplete.GetComponent<LevelCompleteController>(),
                ("levelSelectScreen", levelSelect), ("levelSelectController", levelSelect.GetComponent<LevelSelectController>()),
                ("unlockScreen", unlockScreen));

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
