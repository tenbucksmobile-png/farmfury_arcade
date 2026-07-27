using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Enemies;

namespace FarmFuryArcade.EditorTools
{
    /// <summary>
    /// One-off wiring of the first batch of uploaded art (Assets/_Project/Sprites/...) into the
    /// prefabs/UI that Phase2-5's builders left as solid-colour placeholders. Unlike the
    /// PhaseNProjectBuilders this isn't meant to be re-run as part of a "rebuild everything"
    /// workflow — it's idempotent (safe to re-run, e.g. after adding more art under the same
    /// paths) but only touches the specific fields listed below rather than regenerating prefabs
    /// or screens from scratch.
    /// </summary>
    public static class ArtWiringBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string CharacterDataPath = "Assets/_Project/ScriptableObjects/Resources/Characters/CharacterData_Cluck.asset";
        private const string CluckPrefabPath = "Assets/_Project/Prefabs/Characters/Cluck.prefab";
        private const string HarvesterPrefabPath = "Assets/_Project/Prefabs/Robots/Robot_Harvester.prefab";
        private const string CropCornPrefabPath = "Assets/_Project/Prefabs/Blocks/Crop_Corn.prefab";
        private const string CropVegetablePrefabPath = "Assets/_Project/Prefabs/Blocks/Crop_Vegetable.prefab";
        private const string PowerPelletPrefabPath = "Assets/_Project/Prefabs/Blocks/Power_Sunflower.prefab";

        private const string CluckFront = "Assets/_Project/Sprites/Characters/Cluck_front.png";
        private const string CluckBack = "Assets/_Project/Sprites/Characters/Cluck_back.png";
        private const string CluckLeft = "Assets/_Project/Sprites/Characters/Cluck_left.png";
        private const string HarvesterFront = "Assets/_Project/Sprites/Robots/HarvestorRobot_front.png";
        private const string HarvesterBack = "Assets/_Project/Sprites/Robots/HarvestorRobot_back.png";
        private const string CornKernel = "Assets/_Project/Sprites/Environment/CornKernel.png";
        private const string Carrot = "Assets/_Project/Sprites/Environment/carrot.png";
        private const string CoinIcon = "Assets/_Project/Sprites/Environment/Collectable Coin.png";
        private const string SunflowerPellet = "Assets/_Project/Sprites/Characters/Power_1.png";
        private const string GoldenWheatPellet = "Assets/_Project/Sprites/Environment/RarePellets_maize.png";
        private const string RainbowPellet = "Assets/_Project/Sprites/Environment/RarePellets_apple.png";
        private const string LandingBackground = "Assets/_Project/Sprites/UI/landing.png";
        private const string MapBackground = "Assets/_Project/Sprites/UI/Map.png";
        private const string CornfieldBackground = "Assets/_Project/Sprites/UI/World1_Cornfield.png";
        private const string WheatfieldBackground = "Assets/_Project/Sprites/UI/Wheatfield_background.png";

        [MenuItem("Farm Fury Arcade/Wire Uploaded Art")]
        public static void WireAll()
        {
            ConfigureSpriteImporters();

            WireCluck();
            WireHarvester();
            WireCropsAndPellets();
            WireBackgroundsAndCoinIcon();
            RepositionMainMenuContent();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ArtWiringBuilder] Uploaded art wired into Cluck, Harvester, crops/pellets, and UI backgrounds.");
        }

        /// <summary>landing.png has "FARM FURY ARCADE" baked into the art itself, centred in the
        /// upper half — the button stack (and its own duplicate "Title" text) used to be centred
        /// on screen too, directly on top of it. Re-anchors the whole Content group to the lower
        /// portion of the screen, clear of the logo, without changing its internal layout.</summary>
        [MenuItem("Farm Fury Arcade/Reposition Main Menu Buttons")]
        public static void RepositionMainMenuContent()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var content = GameObject.Find("Canvas")?.transform.Find("MainMenuScreen/Content");
            if (content == null)
            {
                Debug.LogWarning("[ArtWiringBuilder] Could not find MainMenuScreen/Content to reposition.");
                return;
            }

            var rt = (RectTransform)content;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 30f);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        private static readonly string[] SpritesToConfigure =
        {
            CluckFront, CluckBack, CluckLeft, HarvesterFront, HarvesterBack, CornKernel, Carrot,
            CoinIcon, SunflowerPellet, GoldenWheatPellet, RainbowPellet, LandingBackground,
            MapBackground, CornfieldBackground, WheatfieldBackground
        };

        private static void ConfigureSpriteImporters()
        {
            foreach (var path in SpritesToConfigure)
            {
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[ArtWiringBuilder] Expected sprite not found, skipping: {path}");
                    continue;
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"[ArtWiringBuilder] Could not get TextureImporter for: {path}");
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;

                // Pixels-per-unit = texture width so a sprite at localScale 1 fills exactly one
                // maze grid cell (1 world unit), matching PlaceholderSprite's 1px==1unit@scale1
                // convention that every existing prefab's localScale was already tuned around.
                importer.GetSourceTextureWidthAndHeight(out int width, out _);
                importer.spritePixelsPerUnit = width > 0 ? width : 100;

                importer.SaveAndReimport();
            }
        }

        private static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static void WireCluck()
        {
            var front = Load(CluckFront);
            var back = Load(CluckBack);
            var left = Load(CluckLeft);

            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(CharacterDataPath);
            if (data != null)
            {
                // Fixed order [Up0,Up1,Down0,Down1,Left0,Left1,Right0,Right1] per CharacterAnimator.
                // Only one pose per direction exists (no walk-cycle frames yet), so each direction's
                // two slots repeat the same sprite — CharacterAnimator's frame toggle just becomes a
                // no-op until a second walk frame is added. Right reuses Left; CharacterAnimator
                // flips it horizontally at runtime since there's no dedicated Right art.
                data.walkAnimationFrames = new[] { back, back, front, front, left, left, left, left };
                EditorUtility.SetDirty(data);
            }
            else
            {
                Debug.LogWarning($"[ArtWiringBuilder] CharacterData_Cluck not found at {CharacterDataPath}");
            }

            var contents = PrefabUtility.LoadPrefabContents(CluckPrefabPath);
            var sr = contents.GetComponent<SpriteRenderer>();
            if (sr != null && front != null)
            {
                sr.sprite = front;
            }
            PrefabUtility.SaveAsPrefabAsset(contents, CluckPrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static void WireHarvester()
        {
            var front = Load(HarvesterFront);
            var back = Load(HarvesterBack);

            var contents = PrefabUtility.LoadPrefabContents(HarvesterPrefabPath);
            var sr = contents.GetComponent<SpriteRenderer>();
            if (sr != null && front != null)
            {
                sr.sprite = front;
            }
            var visual = contents.GetComponent<RobotVisual>();
            if (visual != null)
            {
                visual.SetDirectionalSprites(front, back);
            }
            PrefabUtility.SaveAsPrefabAsset(contents, HarvesterPrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static void WireCropsAndPellets()
        {
            SetPrefabSprite(CropCornPrefabPath, Load(CornKernel));
            SetPrefabSprite(CropVegetablePrefabPath, Load(Carrot));
            SetPrefabSprite(PowerPelletPrefabPath, Load(SunflowerPellet));

            // Wire the 3 pellet-tier sprites onto the scene's TileMapRenderer (RollPelletTier
            // picks the tier at spawn time; see TileMapRenderer.ConfigurePelletTier).
            EditorSceneManager.OpenScene(ScenePath);
            var managersGO = GameObject.Find("GameManagers");
            var tileMapRenderer = managersGO != null ? managersGO.GetComponent<TileMapRenderer>() : null;
            if (tileMapRenderer != null)
            {
                var so = new SerializedObject(tileMapRenderer);
                so.FindProperty("sunflowerPelletSprite").objectReferenceValue = Load(SunflowerPellet);
                so.FindProperty("goldenWheatPelletSprite").objectReferenceValue = Load(GoldenWheatPellet);
                so.FindProperty("rainbowPelletSprite").objectReferenceValue = Load(RainbowPellet);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[ArtWiringBuilder] Could not find TileMapRenderer on GameManagers to wire pellet tier sprites.");
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        private static void SetPrefabSprite(string prefabPath, Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            var sr = contents.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = sprite;
            }
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static void WireBackgroundsAndCoinIcon()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var canvasTransform = GameObject.Find("Canvas")?.transform;
            if (canvasTransform == null)
            {
                Debug.LogWarning("[ArtWiringBuilder] Could not find Canvas — skipping background/coin wiring.");
                return;
            }

            SetScreenBackground(canvasTransform, "MainMenuScreen", Load(LandingBackground));
            SetScreenBackground(canvasTransform, "WorldMapScreen", Load(MapBackground));
            SetScreenBackground(canvasTransform, "MatchupScreen", Load(CornfieldBackground));
            SetScreenBackground(canvasTransform, "LevelCompleteScreen", Load(WheatfieldBackground));
            SetScreenBackground(canvasTransform, "LevelFailedScreen", Load(WheatfieldBackground));

            AddCoinIcon(canvasTransform);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        private static void SetScreenBackground(Transform canvasTransform, string screenName, Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            var screen = canvasTransform.Find(screenName);
            var image = screen != null ? screen.GetComponent<Image>() : null;
            if (image == null)
            {
                Debug.LogWarning($"[ArtWiringBuilder] Could not find Image on {screenName} to set background.");
                return;
            }

            image.sprite = sprite;
        }

        /// <summary>Adds a small coin icon in front of LevelCompleteScreen's existing "+N coins"
        /// text — that field never had an icon slot, so this wraps it in a new horizontal row
        /// rather than inventing a new serialized field for one icon.</summary>
        private static void AddCoinIcon(Transform canvasTransform)
        {
            var coinSprite = Load(CoinIcon);
            if (coinSprite == null)
            {
                return;
            }

            var coinsText = canvasTransform.Find("LevelCompleteScreen/Content/CoinsEarned");
            if (coinsText == null)
            {
                Debug.LogWarning("[ArtWiringBuilder] Could not find LevelCompleteScreen/Content/CoinsEarned to attach coin icon.");
                return;
            }

            var contentParent = coinsText.parent;
            if (contentParent.Find("CoinsRow") != null)
            {
                return; // already wired by a previous run
            }

            int siblingIndex = coinsText.GetSiblingIndex();
            var row = UIBuilderHelpers.CreateHorizontalGroup("CoinsRow", contentParent, 6f);
            row.transform.SetSiblingIndex(siblingIndex);

            var icon = UIBuilderHelpers.CreateImage("CoinIcon", row.transform, Color.white, 28f, 28f);
            icon.sprite = coinSprite;
            icon.transform.SetSiblingIndex(0);

            coinsText.SetParent(row.transform, false);
            coinsText.SetSiblingIndex(1);
        }
    }
}
