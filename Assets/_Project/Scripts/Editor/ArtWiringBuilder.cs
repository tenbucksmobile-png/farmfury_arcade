using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Enemies;
using FarmFuryArcade.UI;

namespace FarmFuryArcade.EditorTools
{
    /// <summary>
    /// One-off wiring of uploaded art (Assets/_Project/Sprites/...) into the prefabs/UI that
    /// Phase2-5's builders left as solid-colour placeholders. Unlike the PhaseNProjectBuilders
    /// this isn't meant to be re-run as part of a "rebuild everything" workflow — it's idempotent
    /// (safe to re-run, e.g. after adding more art under the same paths) but only touches the
    /// specific fields listed below rather than regenerating prefabs or screens from scratch.
    /// </summary>
    public static class ArtWiringBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";

        // ---- Data / prefab paths --------------------------------------------------------------
        private const string CharacterDataFolder = "Assets/_Project/ScriptableObjects/Resources/Characters";
        private const string RobotDataFolder = "Assets/_Project/ScriptableObjects/Resources/Robots";
        private const string CharacterPrefabFolder = "Assets/_Project/Prefabs/Characters";
        private const string RobotPrefabFolder = "Assets/_Project/Prefabs/Robots";
        private const string AbilityPrefabFolder = "Assets/_Project/Prefabs/Abilities";
        private const string BlockPrefabFolder = "Assets/_Project/Prefabs/Blocks";
        private const string UIPrefabFolder = "Assets/_Project/Prefabs/UI";

        private const string CluckPrefabPath = CharacterPrefabFolder + "/Cluck.prefab";
        private const string HarvesterPrefabPath = RobotPrefabFolder + "/Robot_Harvester.prefab";
        private const string ScoutPrefabPath = RobotPrefabFolder + "/Robot_Scout.prefab";
        private const string PatrolPrefabPath = RobotPrefabFolder + "/Robot_Patrol.prefab";
        private const string DrifterPrefabPath = RobotPrefabFolder + "/Robot_Drifter.prefab";
        private const string HeavyPrefabPath = RobotPrefabFolder + "/Robot_Heavy.prefab";
        private const string DronePrefabPath = RobotPrefabFolder + "/Robot_Drone.prefab";

        private const string CropCornPrefabPath = BlockPrefabFolder + "/Crop_Corn.prefab";
        private const string CropVegetablePrefabPath = BlockPrefabFolder + "/Crop_Vegetable.prefab";
        private const string PowerPelletPrefabPath = BlockPrefabFolder + "/Power_Sunflower.prefab";
        private const string WallPrefabPath = BlockPrefabFolder + "/Wall_CornField.prefab";
        private const string GroundPrefabPath = BlockPrefabFolder + "/Ground_CornField.prefab";
        private const string WarpTunnelPrefabPath = BlockPrefabFolder + "/WarpTunnel.prefab";

        private const string ShockwavePrefabPath = AbilityPrefabFolder + "/Shockwave.prefab";
        private const string BounceTrailPrefabPath = AbilityPrefabFolder + "/BounceTrail.prefab";
        private const string WoollyClonePrefabPath = AbilityPrefabFolder + "/WoollyClone.prefab";

        private const string RosterCardPrefabPath = UIPrefabFolder + "/RosterCard.prefab";

        // ---- Sprite paths (existing, Cluck/Harvester/crops/pellets/UI backgrounds) -------------
        private const string CluckFront = "Assets/_Project/Sprites/Characters/Cluck_front.png";
        private const string CluckBack = "Assets/_Project/Sprites/Characters/Cluck_back.png";
        private const string CluckLeft = "Assets/_Project/Sprites/Characters/Cluck_left.png";
        private const string HarvesterFront = "Assets/_Project/Sprites/Robots/HarvestorRobot_front.png";
        private const string HarvesterBack = "Assets/_Project/Sprites/Robots/HarvestorRobot_back.png";
        private const string CornKernel = "Assets/_Project/Sprites/Environment/CornKernel.png";
        private const string Carrot = "Assets/_Project/Sprites/Environment/carrot.png";
        private const string CoinIcon = "Assets/_Project/Sprites/Environment/Collectable Coin.png";
        private const string SunflowerPellet = "Assets/_Project/Sprites/Environment/RarePellets_sunflower.png";
        private const string GoldenWheatPellet = "Assets/_Project/Sprites/Environment/RarePellets_maize.png";
        private const string RainbowPellet = "Assets/_Project/Sprites/Environment/RarePellets_apple.png";
        private const string LandingBackground = "Assets/_Project/Sprites/UI/landing.png";
        private const string MapBackground = "Assets/_Project/Sprites/UI/Map.png";

        // ---- Sprite paths (this batch) ----------------------------------------------------------
        private const string BessieFront = "Assets/_Project/Sprites/Characters/Bessie_front.png";
        private const string BessieBack = "Assets/_Project/Sprites/Characters/Bessie_back.png";
        private const string BessieLeft = "Assets/_Project/Sprites/Characters/Bessie_left.png";
        private const string WoollyFront = "Assets/_Project/Sprites/Characters/Wooly_front.png";
        private const string WoollyBack = "Assets/_Project/Sprites/Characters/Wooly_back.png";
        private const string WoollyLeft = "Assets/_Project/Sprites/Characters/Wooly_left.png";
        private const string WoollyEffect = "Assets/_Project/Sprites/Characters/Wooly_effect.png";
        private const string PercyFront = "Assets/_Project/Sprites/Characters/Percy_front.png";
        private const string PercyBack = "Assets/_Project/Sprites/Characters/Percy_back.png";
        private const string PercyLeft = "Assets/_Project/Sprites/Characters/Perccy_left.png"; // uploaded filename typo
        private const string PercyEffect = "Assets/_Project/Sprites/Characters/Percy_effect.png";
        private const string DuckyFront = "Assets/_Project/Sprites/Characters/Ducky_front.png";
        private const string DuckyBack = "Assets/_Project/Sprites/Characters/Ducky_back.png";
        private const string BessieSlam = "Assets/_Project/Sprites/Characters/BessieSlam.png";
        private const string GeraldFront = "Assets/_Project/Sprites/Characters/Gerald_front.png";
        private const string GeraldBack = "Assets/_Project/Sprites/Characters/Gerald_back.png";
        private const string GeraldLeft = "Assets/_Project/Sprites/Characters/Gerald_left.png";

        // ---- Sprite paths (this batch: Cluck 2nd-frame walk cycle + real Right art) ------------
        private const string CluckRight = "Assets/_Project/Sprites/Characters/Cluck_right.png";
        private const string CluckRightWalk2 = "Assets/_Project/Sprites/Characters/Cluck_rightwalk2.png";
        private const string CluckLeftWalk = "Assets/_Project/Sprites/Characters/Cluck_LeftWalk.png";

        // ---- Sprite paths (this batch: maze wall/floor/warp tunnel art) ------------------------
        private const string WallCornTiles = "Assets/_Project/Sprites/UI/CornTiles.png";
        private const string FloorTile = "Assets/_Project/Sprites/UI/FloorTile.png";
        private const string WarpTile = "Assets/_Project/Sprites/UI/WarpTile.png";

        private const string ScoutFront = "Assets/_Project/Sprites/Robots/ScoutRobot_front.png";
        private const string ScoutLeft = "Assets/_Project/Sprites/Robots/ScoutRobot_left.png";
        private const string ScoutRight = "Assets/_Project/Sprites/Robots/ScoutRobot_right.png";
        private const string PatrolFront = "Assets/_Project/Sprites/Robots/PatrolRobot_Front.png";
        private const string PatrolBack = "Assets/_Project/Sprites/Robots/PatrolRobot_back.png";
        private const string PatrolLeft = "Assets/_Project/Sprites/Robots/PatrolRobot_left.png";
        private const string PatrolRight = "Assets/_Project/Sprites/Robots/PatrolRobot_right.png";
        private const string HeavyFront = "Assets/_Project/Sprites/Robots/HeavyRobot_front.png";
        private const string HeavyBack = "Assets/_Project/Sprites/Robots/HeavyRobot_back.png";
        private const string DrifterFront = "Assets/_Project/Sprites/Robots/DriftRobot_front.png";
        private const string DrifterLeft = "Assets/_Project/Sprites/Robots/DriftRobot_left.png";
        private const string DrifterRight = "Assets/_Project/Sprites/Robots/DriftRobot_right.png";
        private const string RobotEyes = "Assets/_Project/Sprites/Robots/RobotEyes.png";

        private const string LogoImage = "Assets/_Project/Sprites/UI/Logo.png";
        private const string AppIconImage = "Assets/_Project/Sprites/UI/AppIcon.png";
        private const string MatchupBackground = "Assets/_Project/Sprites/UI/matchup.png";
        private const string LevelCompletePanel = "Assets/_Project/Sprites/UI/LevelComplete.png";
        private const string LevelFailedPanel = "Assets/_Project/Sprites/UI/LevelFailed.png";
        private const string PausedPanel = "Assets/_Project/Sprites/UI/Paused.png";
        private const string CardFrame = "Assets/_Project/Sprites/UI/Card.png";
        private const string BtnPlay = "Assets/_Project/Sprites/UI/Btn_play.png";
        private const string BtnPause = "Assets/_Project/Sprites/UI/Btn_pause.png";
        private const string BtnSettings = "Assets/_Project/Sprites/UI/Btn_settings.png";
        private const string BtnQuit = "Assets/_Project/Sprites/UI/Btn_quit.png";
        private const string BtnMusic = "Assets/_Project/Sprites/UI/Btn_music.png";
        private const string BtnNoSound = "Assets/_Project/Sprites/UI/Btn_nosound.png";
        private const string BtnHome = "Assets/_Project/Sprites/UI/Btn_home.png";
        private const string BtnSkip = "Assets/_Project/Sprites/UI/Btn_skip.png";
        private const string BtnBack = "Assets/_Project/Sprites/UI/Btn_back.png";
        private const string BtnPlaque = "Assets/_Project/Sprites/UI/Btn_plaque.png";

        [MenuItem("Farm Fury Arcade/Wire Uploaded Art")]
        public static void WireAll()
        {
            ConfigureSpriteImporters();

            WireCluck();
            WireHarvester();
            WireCropsAndPellets();
            WireMazeTiles();
            WireBackgroundsAndCoinIcon();

            WireNewCharacters();
            WireNewRobots();
            WireAbilityEffects();
            WireCardsAndPanels();
            WireButtons();
            WireAppIcon();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ArtWiringBuilder] Uploaded art wired into characters, robots, maze tiles, ability effects, cards/panels, and buttons.");
        }

        // Main Menu no longer has a "Content" vertical stack or duplicate Title text to
        // reposition around landing.png's baked-in logo — Phase5ProjectBuilder.BuildMainMenu now
        // places just PlayButton/SettingsButton directly at the bottom corners. The
        // "Reposition Main Menu Buttons" menu item this used to be is gone along with it; rerun
        // Phase5ProjectBuilder.BuildAll if the Main Menu ever needs rebuilding from scratch.

        private static readonly string[] SpritesToConfigure =
        {
            CluckFront, CluckBack, CluckLeft, HarvesterFront, HarvesterBack, CornKernel, Carrot,
            CoinIcon, SunflowerPellet, GoldenWheatPellet, RainbowPellet, LandingBackground, MapBackground,
            BessieFront, BessieBack, BessieLeft, WoollyFront, WoollyBack, WoollyLeft, WoollyEffect,
            PercyFront, PercyBack, PercyLeft, PercyEffect, DuckyFront, DuckyBack, BessieSlam,
            ScoutFront, ScoutLeft, ScoutRight, PatrolFront, PatrolBack, PatrolLeft, PatrolRight,
            HeavyFront, HeavyBack, DrifterFront, DrifterLeft, DrifterRight, RobotEyes,
            MatchupBackground, LevelCompletePanel, LevelFailedPanel, PausedPanel, CardFrame,
            BtnPlay, BtnPause, BtnSettings, BtnQuit, BtnMusic, BtnNoSound, BtnHome, BtnSkip, BtnBack, BtnPlaque,
            GeraldFront, GeraldBack, GeraldLeft,
            CluckRight, CluckRightWalk2, CluckLeftWalk,
            WallCornTiles, FloorTile, WarpTile
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
            var leftWalk = Load(CluckLeftWalk);
            var right = Load(CluckRight);
            var rightWalk = Load(CluckRightWalk2);

            // Cluck is the only character with a real 2nd walk frame per direction and dedicated
            // Right art so far — everyone else still repeats their single pose in both slots via
            // SetWalkFrames. Cluck_rightwalk.png (the other uploaded right-walk frame) is left
            // unused; rightwalk2 reads as a clearer mid-stride contrast against the idle Cluck_right.
            string path = $"{CharacterDataFolder}/CharacterData_Cluck.asset";
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data != null)
            {
                data.walkAnimationFrames = new[]
                {
                    back, back,                                   // Up0, Up1
                    front, front,                                  // Down0, Down1
                    left, leftWalk != null ? leftWalk : left,       // Left0, Left1
                    right != null ? right : left,                   // Right0
                    rightWalk != null ? rightWalk : (right != null ? right : left) // Right1
                };
                data.hasDedicatedRightArt = right != null;
                data.portraitSprite = front;
                EditorUtility.SetDirty(data);
            }
            else
            {
                Debug.LogWarning($"[ArtWiringBuilder] CharacterData_Cluck not found at {path}");
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
                visual.SetDefeatedSprite(Load(RobotEyes));
            }
            PrefabUtility.SaveAsPrefabAsset(contents, HarvesterPrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            SetRobotPortrait("Harvester", front);
        }

        /// <summary>RobotData.portraitSprite (used by MatchupScreenController's robot cards) was
        /// unwired until now — every robot's front sprite doubles as its portrait, same convention
        /// as CharacterData.portraitSprite in SetWalkFrames/WireCluck.</summary>
        private static void SetRobotPortrait(string robotTypeName, Sprite front)
        {
            if (front == null)
            {
                return;
            }

            string path = $"{RobotDataFolder}/RobotData_{robotTypeName}.asset";
            var data = AssetDatabase.LoadAssetAtPath<RobotData>(path);
            if (data == null)
            {
                Debug.LogWarning($"[ArtWiringBuilder] RobotData_{robotTypeName} not found at {path}");
                return;
            }

            data.portraitSprite = front;
            EditorUtility.SetDirty(data);
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

        /// <summary>Wires the maze wall/ground/warp-tunnel tile art. All 3 prefabs are
        /// instantiated per-cell by TileMapRenderer at scale 1 (same convention crops/pellets
        /// already use), so a straight SpriteRenderer swap is enough — no tiling/atlas slicing,
        /// since each uploaded file is a single complete tile image, not a tileset.</summary>
        private static void WireMazeTiles()
        {
            SetPrefabSprite(WallPrefabPath, Load(WallCornTiles));
            SetPrefabSprite(GroundPrefabPath, Load(FloorTile));
            SetPrefabSprite(WarpTunnelPrefabPath, Load(WarpTile));
        }

        private static void SetPrefabSprite(string prefabPath, Sprite sprite)
        {
            if (sprite == null || !File.Exists(prefabPath))
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

        // ---- New characters (Bessie, Woolly, Percy, Ducky) -------------------------------------

        /// <summary>Fixed order [Up0,Up1,Down0,Down1,Left0,Left1,Right0,Right1] per
        /// CharacterAnimator. Only one pose per direction exists (no walk-cycle frames yet), so
        /// each direction's two slots repeat the same sprite. Right reuses Left; CharacterAnimator
        /// flips it horizontally at runtime since there's no dedicated Right art. If left is null
        /// (Ducky has no profile art), front is used for Left/Right too — direction won't read
        /// correctly for those two facings until profile art is added, but the character is never
        /// left as a bare colour square.</summary>
        private static void SetWalkFrames(string characterTypeName, Sprite front, Sprite back, Sprite left)
        {
            string path = $"{CharacterDataFolder}/CharacterData_{characterTypeName}.asset";
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data == null)
            {
                Debug.LogWarning($"[ArtWiringBuilder] CharacterData_{characterTypeName} not found at {path}");
                return;
            }

            Sprite leftOrFront = left != null ? left : front;
            data.walkAnimationFrames = new[] { back, back, front, front, leftOrFront, leftOrFront, leftOrFront, leftOrFront };
            data.portraitSprite = front;
            EditorUtility.SetDirty(data);
        }

        private static void WireCharacterPrefabSprite(string prefabPath, Sprite front)
        {
            if (front == null || !File.Exists(prefabPath))
            {
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            var sr = contents.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = front;
            }
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static void WireNewCharacters()
        {
            var bessieFront = Load(BessieFront);
            SetWalkFrames("Bessie", bessieFront, Load(BessieBack), Load(BessieLeft));
            WireCharacterPrefabSprite($"{CharacterPrefabFolder}/Bessie.prefab", bessieFront);

            var woollyFront = Load(WoollyFront);
            SetWalkFrames("Woolly", woollyFront, Load(WoollyBack), Load(WoollyLeft));
            WireCharacterPrefabSprite($"{CharacterPrefabFolder}/Woolly.prefab", woollyFront);

            var percyFront = Load(PercyFront);
            SetWalkFrames("Percy", percyFront, Load(PercyBack), Load(PercyLeft));
            WireCharacterPrefabSprite($"{CharacterPrefabFolder}/Percy.prefab", percyFront);

            var duckyFront = Load(DuckyFront);
            SetWalkFrames("Ducky", duckyFront, Load(DuckyBack), null);
            WireCharacterPrefabSprite($"{CharacterPrefabFolder}/Ducky.prefab", duckyFront);

            var geraldFront = Load(GeraldFront);
            SetWalkFrames("Gerald", geraldFront, Load(GeraldBack), Load(GeraldLeft));
            WireCharacterPrefabSprite($"{CharacterPrefabFolder}/Gerald.prefab", geraldFront);

            // Gerald_effect.png is uploaded but unwired — PuffUpAbility has no spawned effect
            // object (it just scales Gerald's own sprite 3x), unlike Bessie/Percy/Woolly's
            // abilities which each spawn a dedicated effect prefab. Adding one is a gameplay
            // change (a new prefab + a spawn call in PuffUpAbility), not just art wiring.

            // Horace and Billy still have no uploaded art yet — left as placeholder squares.
        }

        // ---- New robots (Scout, Patrol, Heavy, Drifter) + universal defeated eyes -------------

        private static void WireRobotVisual(string prefabPath, Sprite front, Sprite back, Sprite left, Sprite right)
        {
            if (!File.Exists(prefabPath))
            {
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            var sr = contents.GetComponent<SpriteRenderer>();
            if (sr != null && front != null)
            {
                sr.sprite = front;
            }
            var visual = contents.GetComponent<RobotVisual>();
            if (visual != null)
            {
                if (front != null)
                {
                    visual.SetDirectionalSprites(front, back, left, right);
                }
                visual.SetDefeatedSprite(Load(RobotEyes));
            }
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static void WireNewRobots()
        {
            var scoutFront = Load(ScoutFront);
            WireRobotVisual(ScoutPrefabPath, scoutFront, null, Load(ScoutLeft), Load(ScoutRight));
            SetRobotPortrait("Scout", scoutFront);

            var patrolFront = Load(PatrolFront);
            WireRobotVisual(PatrolPrefabPath, patrolFront, Load(PatrolBack), Load(PatrolLeft), Load(PatrolRight));
            SetRobotPortrait("Patrol", patrolFront);

            var heavyFront = Load(HeavyFront);
            WireRobotVisual(HeavyPrefabPath, heavyFront, Load(HeavyBack), null, null);
            SetRobotPortrait("Heavy", heavyFront);

            var drifterFront = Load(DrifterFront);
            WireRobotVisual(DrifterPrefabPath, drifterFront, null, Load(DrifterLeft), Load(DrifterRight));
            SetRobotPortrait("Drifter", drifterFront);

            // Drone has no uploaded art yet — still gets the universal defeated-eyes sprite and
            // no portraitSprite, so its Matchup/robot-card slot stays a colour-tint placeholder.
            WireRobotVisual(DronePrefabPath, null, null, null, null);
        }

        // ---- Ability effects ---------------------------------------------------------------

        private static void WireAbilityEffects()
        {
            SetPrefabSprite(ShockwavePrefabPath, Load(BessieSlam));
            SetPrefabSprite(BounceTrailPrefabPath, Load(PercyEffect));
            SetPrefabSprite(WoollyClonePrefabPath, Load(WoollyEffect));
        }

        // ---- Cards and full-screen panels ----------------------------------------------------

        private static void WireCardsAndPanels()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var canvasTransform = GameObject.Find("Canvas")?.transform;
            if (canvasTransform == null)
            {
                Debug.LogWarning("[ArtWiringBuilder] Could not find Canvas — skipping card/panel wiring.");
                return;
            }

            var matchup = Load(MatchupBackground);
            SetScreenBackground(canvasTransform, "MatchupScreen", matchup);
            SetScreenBackground(canvasTransform, "LevelCompleteScreen", Load(LevelCompletePanel));
            SetScreenBackground(canvasTransform, "LevelFailedScreen", Load(LevelFailedPanel));
            SetScreenBackground(canvasTransform, "PauseOverlay", Load(PausedPanel));

            // Matchup's CharacterCard/RobotCards intentionally do NOT get the Card.png frame —
            // matchup.png's background art already bakes in two wood-frame slots at those exact
            // positions (see Phase5ProjectBuilder.BuildMatchup); adding Card.png on top would
            // double up the framing. Card.png is still used for New Character Unlock and RosterCard,
            // neither of which has a frame baked into their background.
            var card = Load(CardFrame);
            if (card != null)
            {
                SetImageSprite(canvasTransform, "LevelCompleteScreen/NewCharacterUnlockOverlay/UnlockContent/CharacterCard", card);
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            // RosterCard is a prefab, not part of the scene hierarchy above.
            if (card != null && File.Exists(RosterCardPrefabPath))
            {
                var contents = PrefabUtility.LoadPrefabContents(RosterCardPrefabPath);
                var rootImage = contents.GetComponent<Image>();
                if (rootImage != null)
                {
                    rootImage.sprite = card;
                }
                PrefabUtility.SaveAsPrefabAsset(contents, RosterCardPrefabPath);
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void SetImageSprite(Transform canvasTransform, string path, Sprite sprite)
        {
            var target = canvasTransform.Find(path);
            var image = target != null ? target.GetComponent<Image>() : null;
            if (image == null)
            {
                Debug.LogWarning($"[ArtWiringBuilder] Could not find Image at Canvas/{path} to wire.");
                return;
            }
            image.sprite = sprite;
        }

        /// <summary>GameplayHUD.soundOnSprite/soundOffSprite are plain Sprite fields (not an
        /// Image on the button itself, which SetImageSprite already handles), swapped at runtime
        /// by RefreshSoundIcon — the first uses of Btn_nosound.png, which was uploaded a while ago
        /// but had no icon-swap feature to hook into until now.</summary>
        private static void WireSoundIconSprites(Transform canvasTransform, Sprite music, Sprite noSound)
        {
            var hud = canvasTransform.Find("GameplayScreen")?.GetComponent<GameplayHUD>();
            if (hud == null)
            {
                Debug.LogWarning("[ArtWiringBuilder] Could not find GameplayHUD to wire sound icon sprites.");
                return;
            }

            var so = new SerializedObject(hud);
            so.FindProperty("soundOnSprite").objectReferenceValue = music;
            so.FindProperty("soundOffSprite").objectReferenceValue = noSound;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Buttons --------------------------------------------------------------------------

        private static void WireButtons()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var canvasTransform = GameObject.Find("Canvas")?.transform;
            if (canvasTransform == null)
            {
                Debug.LogWarning("[ArtWiringBuilder] Could not find Canvas — skipping button wiring.");
                return;
            }

            var play = Load(BtnPlay);
            var pause = Load(BtnPause);
            var settings = Load(BtnSettings);
            var quit = Load(BtnQuit);
            var music = Load(BtnMusic);
            var noSound = Load(BtnNoSound);
            var home = Load(BtnHome);
            var skip = Load(BtnSkip);
            var back = Load(BtnBack);
            var plaque = Load(BtnPlaque);

            SetImageSprite(canvasTransform, "MainMenuScreen/PlayButton", play);
            SetImageSprite(canvasTransform, "MainMenuScreen/SettingsButton", settings);

            SetImageSprite(canvasTransform, "WorldMapScreen/HomeButton", home);

            SetImageSprite(canvasTransform, "MatchupScreen/PlayButton", play);
            SetImageSprite(canvasTransform, "MatchupScreen/HomeButton", home);

            SetImageSprite(canvasTransform, "GameplayScreen/PauseButton", pause);
            SetImageSprite(canvasTransform, "GameplayScreen/SoundButton", music);
            SetImageSprite(canvasTransform, "GameplayScreen/HomeButton", home);
            SetImageSprite(canvasTransform, "GameplayScreen/SideBackdrop", plaque);
            WireSoundIconSprites(canvasTransform, music, noSound);

            SetImageSprite(canvasTransform, "PauseOverlay/Content/ResumeButton", play);
            SetImageSprite(canvasTransform, "PauseOverlay/Content/SwapButton", plaque);
            SetImageSprite(canvasTransform, "PauseOverlay/Content/RestartButton", plaque);
            SetImageSprite(canvasTransform, "PauseOverlay/Content/SettingsButton", settings);
            SetImageSprite(canvasTransform, "PauseOverlay/Content/QuitButton", quit);

            SetImageSprite(canvasTransform, "SettingsOverlay/Content/TitleRow/CloseButton", back);
            SetImageSprite(canvasTransform, "SettingsOverlay/Content/MusicToggle/MusicToggle_Box", music);

            SetImageSprite(canvasTransform, "StoreComingSoonOverlay/Content/CloseButton", back);

            SetImageSprite(canvasTransform, "LevelCompleteScreen/Content/Buttons/ReplayButton", plaque);
            SetImageSprite(canvasTransform, "LevelCompleteScreen/Content/Buttons/NextLevelButton", skip);
            SetImageSprite(canvasTransform, "LevelCompleteScreen/Content/Buttons/HomeButton", home);
            SetImageSprite(canvasTransform, "LevelCompleteScreen/NewCharacterUnlockOverlay/UnlockContent/ContinueButton", play);

            SetImageSprite(canvasTransform, "LevelFailedScreen/Content/Buttons/RetryButton", plaque);
            SetImageSprite(canvasTransform, "LevelFailedScreen/Content/Buttons/HomeButton", home);

            SetImageSprite(canvasTransform, "CharacterRosterScreen/BackButton", back);
            SetImageSprite(canvasTransform, "LeaderboardsScreen/Content/BackButton", back);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        // ---- App icon ---------------------------------------------------------------------------

        /// <summary>Sets the Unity Player Settings app icon (shown on iOS/Android home screens and
        /// in the Standalone build's .exe) directly from the uploaded 1024x1024 icon — this is a
        /// project-settings change, not a scene/prefab one, so it isn't gated behind ConfigureSpriteImporters.</summary>
        private static void WireAppIcon()
        {
            if (!File.Exists(AppIconImage))
            {
                return;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconImage);
            if (texture == null)
            {
                Debug.LogWarning($"[ArtWiringBuilder] Could not load {AppIconImage} as a Texture2D for the app icon.");
                return;
            }

            var textures = new[] { texture };
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, textures);
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, textures);
        }
    }
}
