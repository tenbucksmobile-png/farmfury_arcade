using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Enemies;
using FarmFuryArcade.UI;
using FarmFuryArcade.Utilities;

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
        private const string WheatfieldBackdrop = "Assets/_Project/Sprites/UI/Wheatfield_background.png";
        private const string SettingsBackground = "Assets/_Project/Sprites/UI/LoadingScreen Background.png";
        private const string LevelCompletePanel = "Assets/_Project/Sprites/UI/LevelComplete.png";
        private const string LevelFailedPanel = "Assets/_Project/Sprites/UI/LevelFailed.png";
        private const string PausedPanel = "Assets/_Project/Sprites/UI/Paused.png";
        private const string CardFrame = "Assets/_Project/Sprites/UI/Card.png";
        private const string BackgroundMusicClip = "Assets/_Project/Audio/Music/BackgroundMusic.mp3";
        private const string AnimalDeathSfx = "Assets/_Project/Audio/SFX/Animal_death.mp3";
        private const string CornPickupSfx = "Assets/_Project/Audio/SFX/CornPickup.mp3";
        private const string PowerReadySfx = "Assets/_Project/Audio/SFX/PowerReady.mp3";
        private const string RarePelletPickupSfx = "Assets/_Project/Audio/SFX/RarePellet_pickup.mp3";
        private const string RobotSpawnSfx = "Assets/_Project/Audio/SFX/RobotSpawn.mp3";
        private const string EatRobotMusicClip = "Assets/_Project/Audio/SFX/EatRobot.mp3";

        /// <summary>Bundled with TMP's own "Examples & Extras" (Assets/TextMesh Pro/Examples &
        /// Extras/...) — a bold comic/cartoon-style display face, already has its own correctly
        /// generated SDF material (unlike Inter-Regular SDF's broken shader — see CLAUDE.md's TMP
        /// bootstrap section), so no import/generation step is needed, just point .font at it.</summary>
        private const string BangersFontPath = "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF.asset";
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
        private const string RetryButtonArt = "Assets/_Project/Sprites/UI/Retry.png";
        private const string MenuButtonArt = "Assets/_Project/Sprites/UI/Menu.png";
        private const string ResumeButtonArt = "Assets/_Project/Sprites/UI/Resume.png";
        private const string SwapCharacterButtonArt = "Assets/_Project/Sprites/UI/SwapCharacter.png";
        private const string RestartButtonArt = "Assets/_Project/Sprites/UI/Restart.png";
        private const string SettingsButtonArt = "Assets/_Project/Sprites/UI/Settings.png";
        private const string QuitButtonArt = "Assets/_Project/Sprites/UI/Quit.png";

        // ---- ChooseCharacterScreen card art (framed "animal card" portraits) -------------------
        private const string CluckCard = "Assets/_Project/Sprites/UI/Cluck_Chicken.png";
        private const string BessieCard = "Assets/_Project/Sprites/UI/Bessie_Cow.png";
        private const string PercyCard = "Assets/_Project/Sprites/UI/Percy_Pig.png";
        private const string WoollyCard = "Assets/_Project/Sprites/UI/Woolly_Sheep.png";
        private const string DuckyCard = "Assets/_Project/Sprites/UI/Ducky_Duck.png";
        private const string HoraceCard = "Assets/_Project/Sprites/UI/Horace_Horse.png";
        private const string GeraldCard = "Assets/_Project/Sprites/UI/Gerald_Turkey.png";
        private const string BillyCard = "Assets/_Project/Sprites/UI/Billy_Goat.png";

        // ---- On-screen directional pad -----------------------------------------------------
        private const string DPadUp = "Assets/_Project/Sprites/UI/up.png";
        private const string DPadDown = "Assets/_Project/Sprites/UI/down.png";
        private const string DPadLeft = "Assets/_Project/Sprites/UI/left.png";
        private const string DPadRight = "Assets/_Project/Sprites/UI/right.png";

        [MenuItem("Farm Fury Arcade/Wire Uploaded Art")]
        public static void WireAll()
        {
            ConfigureSpriteImporters();

            WireCluck();
            WireHarvester();
            WireCropsAndPellets();
            WireMazeTiles();
            WireGameplayBackdrop();
            WireBackgroundsAndCoinIcon();

            WireNewCharacters();
            WireCharacterSelectCards();
            WireNewRobots();
            WireAbilityEffects();
            WireCardsAndPanels();
            WireButtons();
            WireAppIcon();
            WireAudio();
            WireGameplayFont();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ArtWiringBuilder] Uploaded art wired into characters, robots, maze tiles, gameplay backdrop, ability effects, cards/panels, and buttons.");
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
            LevelCompletePanel, LevelFailedPanel, PausedPanel, CardFrame,
            BtnPlay, BtnPause, BtnSettings, BtnQuit, BtnMusic, BtnNoSound, BtnHome, BtnSkip, BtnBack, BtnPlaque,
            RetryButtonArt, MenuButtonArt, ResumeButtonArt, SwapCharacterButtonArt, RestartButtonArt,
            SettingsButtonArt, QuitButtonArt,
            CluckCard, BessieCard, PercyCard, WoollyCard, DuckyCard, HoraceCard, GeraldCard, BillyCard,
            DPadUp, DPadDown, DPadLeft, DPadRight,
            GeraldFront, GeraldBack, GeraldLeft,
            CluckRight, CluckRightWalk2, CluckLeftWalk,
            WallCornTiles, FloorTile, WarpTile, WheatfieldBackdrop, SettingsBackground
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

        /// <summary>RobotData.portraitSprite was unwired until now — every robot's front sprite
        /// doubles as its portrait, same convention as CharacterData.portraitSprite in
        /// SetWalkFrames/WireCluck.</summary>
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

        /// <summary>Gameplay had no art behind the maze grid at all — just the camera's clear
        /// color, a documented gap. Wheatfield_background.png was uploaded early on but went
        /// unused once LevelComplete/Failed/Pause got their own dedicated panel art — reused here
        /// instead of uploading something new. One SpriteRenderer, sorting-ordered below
        /// Ground_CornField's -1 so tile art still draws over it everywhere inside the maze.
        ///
        /// Sized as a "cover" fit against the maze's own world bounds (mazeWidth/Height *
        /// TileMapRenderer.CellSize) rather than a fixed hardcoded width: scaled uniformly (so the
        /// art's own aspect ratio is always preserved — never non-uniformly stretched) to just
        /// barely cover the full area CameraFollow can ever pan across, picking whichever axis
        /// needs more scale to do so. A fixed "90 units wide" constant (this image's previous
        /// sizing, tuned back when 1 grid cell was 1 world unit) badly overshot that once
        /// TileMapRenderer.CellSize made cells bigger — the same fixed pixel-to-world ratio now
        /// spanned far more of the image than fits in view, reading as a zoomed-in crop instead of
        /// the intended whole-picture backdrop. Deriving the size from the maze's actual current
        /// world footprint keeps this correct if the maze or CellSize ever change again, and this
        /// image's own aspect ratio (2720x1536, ~16:9) already closely matches the camera's, so in
        /// practice this reads as "the whole picture, undistorted" exactly as intended rather than
        /// an arbitrary zoom level. Centered on LevelData_01's own dimensions rather than
        /// TileMapRenderer.MazeWidth/Height, since those are runtime-only (0 until a level is
        /// loaded in Play mode) and this runs in the Editor at wiring time.</summary>
        private static void WireGameplayBackdrop()
        {
            var sprite = Load(WheatfieldBackdrop);
            if (sprite == null)
            {
                Debug.LogWarning($"[ArtWiringBuilder] {WheatfieldBackdrop} not found — skipping gameplay backdrop.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);

            var level = AssetDatabase.LoadAssetAtPath<LevelData>(
                "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_01.asset");
            float mazeWorldWidth = level != null ? (level.mazeWidth - 1) * TileMapRenderer.CellSize : 26f;
            float mazeWorldHeight = level != null ? (level.mazeHeight - 1) * TileMapRenderer.CellSize : 30f;
            float centerX = mazeWorldWidth / 2f;
            float centerY = mazeWorldHeight / 2f;

            var mazeParent = GameObject.Find("MazeParent")?.transform;
            var backdropGO = GameObject.Find("GameplayBackdrop");
            if (backdropGO == null)
            {
                backdropGO = new GameObject("GameplayBackdrop");
                backdropGO.AddComponent<SpriteRenderer>();
            }
            if (mazeParent != null)
            {
                backdropGO.transform.SetParent(mazeParent, false);
            }

            var sr = backdropGO.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -5;

            // The backdrop must cover whichever is bigger: the maze's own world footprint, or the
            // camera's view width (CameraFollow deliberately shows more width than the maze itself
            // has — that's the whole point of WidthFillFraction, extra screen margin at the edges
            // for backdrop art to show through). CameraFollow.TargetVisibleColumns/WidthFillFraction
            // give a camera view width that's constant regardless of the runtime aspect ratio (only
            // view HEIGHT varies with aspect, and that's bounded by mazeWorldHeight in all realistic
            // landscape aspects) — see that script's own comment. A 1.3x safety margin on top covers
            // any aspect extreme enough to break that assumption, without needing to know the actual
            // runtime aspect at Editor-wiring time.
            const float safetyMargin = 1.3f;
            float targetCameraViewWidth = CameraFollow.TargetVisibleColumns * TileMapRenderer.CellSize / CameraFollow.WidthFillFraction;
            float requiredWidth = Mathf.Max(mazeWorldWidth, targetCameraViewWidth) * safetyMargin;
            float requiredHeight = mazeWorldHeight * safetyMargin;

            float imageAspect = sprite.rect.width / sprite.rect.height; // width/height, e.g. ~1.77
            float widthUnits = Mathf.Max(requiredWidth, requiredHeight * imageAspect);
            float heightUnits = widthUnits / imageAspect;
            backdropGO.transform.localScale = new Vector3(widthUnits, heightUnits, 1f);
            backdropGO.transform.position = new Vector3(centerX, centerY, 0f);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
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

        /// <summary>ChooseCharacterScreen's card art — separate from portraitSprite/
        /// walkAnimationFrames since these are dedicated framed "animal card" images (a wood-frame
        /// border baked directly into each file), not a plain front-facing sprite. Covers all 8
        /// characters in one pass regardless of which walk-cycle art they have (Horace/Billy have
        /// dedicated card art despite still being solid-colour placeholders in actual gameplay).</summary>
        private static void WireCharacterSelectCards()
        {
            SetSelectCardArt("Cluck", Load(CluckCard));
            SetSelectCardArt("Bessie", Load(BessieCard));
            SetSelectCardArt("Percy", Load(PercyCard));
            SetSelectCardArt("Woolly", Load(WoollyCard));
            SetSelectCardArt("Ducky", Load(DuckyCard));
            SetSelectCardArt("Horace", Load(HoraceCard));
            SetSelectCardArt("Gerald", Load(GeraldCard));
            SetSelectCardArt("Billy", Load(BillyCard));
        }

        private static void SetSelectCardArt(string characterTypeName, Sprite cardArt)
        {
            if (cardArt == null)
            {
                return;
            }

            string path = $"{CharacterDataFolder}/CharacterData_{characterTypeName}.asset";
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data == null)
            {
                Debug.LogWarning($"[ArtWiringBuilder] CharacterData_{characterTypeName} not found at {path}");
                return;
            }

            data.selectCardArt = cardArt;
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

            // Drone has no uploaded art yet — still gets the universal defeated-eyes sprite, but
            // no portraitSprite.
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

            SetScreenBackground(canvasTransform, "LevelCompleteScreen", Load(LevelCompletePanel));
            SetScreenBackground(canvasTransform, "LevelFailedScreen", Load(LevelFailedPanel));
            SetScreenBackground(canvasTransform, "PauseOverlay", Load(PausedPanel));
            SetScreenBackground(canvasTransform, "SettingsOverlay", Load(SettingsBackground));
            SetImageSprite(canvasTransform, "SettingsOverlay/ContentBackdrop", Load(BtnPlaque));
            SetScreenBackground(canvasTransform, "ChooseCharacterScreen", Load(SettingsBackground));

            // Card.png is used for New Character Unlock and RosterCard, neither of which has a
            // frame baked into their background.
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
            var music = Load(BtnMusic);
            var home = Load(BtnHome);
            var skip = Load(BtnSkip);
            var back = Load(BtnBack);
            var plaque = Load(BtnPlaque);

            SetImageSprite(canvasTransform, "MainMenuScreen/PlayButton", play);
            SetImageSprite(canvasTransform, "MainMenuScreen/SettingsButton", settings);

            SetImageSprite(canvasTransform, "WorldMapScreen/HomeButton", home);

            SetImageSprite(canvasTransform, "GameplayScreen/PauseButton", pause);
            SetImageSprite(canvasTransform, "GameplayScreen/DPadUpButton", Load(DPadUp));
            SetImageSprite(canvasTransform, "GameplayScreen/DPadDownButton", Load(DPadDown));
            SetImageSprite(canvasTransform, "GameplayScreen/DPadLeftButton", Load(DPadLeft));
            SetImageSprite(canvasTransform, "GameplayScreen/DPadRightButton", Load(DPadRight));

            SetImageSprite(canvasTransform, "PauseOverlay/ResumeButton", Load(ResumeButtonArt));
            SetImageSprite(canvasTransform, "PauseOverlay/SwapButton", Load(SwapCharacterButtonArt));
            SetImageSprite(canvasTransform, "PauseOverlay/RestartButton", Load(RestartButtonArt));
            SetImageSprite(canvasTransform, "PauseOverlay/SettingsButton", Load(SettingsButtonArt));
            SetImageSprite(canvasTransform, "PauseOverlay/QuitButton", Load(QuitButtonArt));

            SetImageSprite(canvasTransform, "SettingsOverlay/BackButton", back);
            SetImageSprite(canvasTransform, "SettingsOverlay/Content/MusicToggle/MusicToggle_Box", music);

            SetImageSprite(canvasTransform, "StoreComingSoonOverlay/Content/CloseButton", back);

            SetImageSprite(canvasTransform, "LevelCompleteScreen/Content/Buttons/ReplayButton", plaque);
            SetImageSprite(canvasTransform, "LevelCompleteScreen/Content/Buttons/NextLevelButton", skip);
            SetImageSprite(canvasTransform, "LevelCompleteScreen/Content/Buttons/HomeButton", home);
            SetImageSprite(canvasTransform, "LevelCompleteScreen/NewCharacterUnlockOverlay/UnlockContent/ContinueButton", play);

            SetImageSprite(canvasTransform, "LevelFailedScreen/RetryButton", Load(RetryButtonArt));
            SetImageSprite(canvasTransform, "LevelFailedScreen/MenuButton", Load(MenuButtonArt));

            SetImageSprite(canvasTransform, "CharacterRosterScreen/BackButton", back);
            SetImageSprite(canvasTransform, "LeaderboardsScreen/Content/BackButton", back);
            SetImageSprite(canvasTransform, "ChooseCharacterScreen/BackButton", back);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        // ---- Audio --------------------------------------------------------------------------------

        /// <summary>Wires background music + the power-active music swap + all 5 SFX clips onto
        /// AudioManager (on GameManagers). AudioManager.Start() plays the background music clip
        /// immediately and loops it for the life of the app; PowerPelletManager crossfades to
        /// eatRobotMusicClip for the duration of a power pellet's effect and back afterward
        /// (PlayEatRobotMusic/ResumeBackgroundMusic). The SFX clips are played by their respective
        /// gameplay triggers via AudioManager.PlayAnimalDeathSfx/PlayCornPickupSfx/
        /// PlayPowerReadySfx/PlayRarePelletPickupSfx/PlayRobotRespawnSfx (see PlayerHealth,
        /// CropCollector, PowerPelletManager, RobotBase respectively). AudioClip import settings
        /// are left at Unity's defaults (no ConfigureSpriteImporters-style pass needed here, unlike
        /// sprites) since the default compressed-in-memory settings are already reasonable for
        /// these short clips and the two looping music tracks.</summary>
        private static void WireAudio()
        {
            var musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(BackgroundMusicClip);
            var animalDeath = AssetDatabase.LoadAssetAtPath<AudioClip>(AnimalDeathSfx);
            var cornPickup = AssetDatabase.LoadAssetAtPath<AudioClip>(CornPickupSfx);
            var powerReady = AssetDatabase.LoadAssetAtPath<AudioClip>(PowerReadySfx);
            var rarePelletPickup = AssetDatabase.LoadAssetAtPath<AudioClip>(RarePelletPickupSfx);
            var robotSpawn = AssetDatabase.LoadAssetAtPath<AudioClip>(RobotSpawnSfx);
            var eatRobotMusic = AssetDatabase.LoadAssetAtPath<AudioClip>(EatRobotMusicClip);

            if (musicClip == null)
            {
                Debug.LogWarning($"[ArtWiringBuilder] {BackgroundMusicClip} not found — skipping background music wiring.");
            }

            EditorSceneManager.OpenScene(ScenePath);

            var managersGO = GameObject.Find("GameManagers");
            var audioManager = managersGO != null ? managersGO.GetComponent<AudioManager>() : null;
            if (audioManager == null)
            {
                Debug.LogWarning("[ArtWiringBuilder] Could not find AudioManager on GameManagers — skipping audio wiring.");
                return;
            }

            var so = new SerializedObject(audioManager);
            if (musicClip != null) so.FindProperty("backgroundMusicClip").objectReferenceValue = musicClip;
            if (animalDeath != null) so.FindProperty("animalDeathClip").objectReferenceValue = animalDeath;
            if (cornPickup != null) so.FindProperty("cornPickupClip").objectReferenceValue = cornPickup;
            if (powerReady != null) so.FindProperty("powerReadyClip").objectReferenceValue = powerReady;
            if (rarePelletPickup != null) so.FindProperty("rarePelletPickupClip").objectReferenceValue = rarePelletPickup;
            if (robotSpawn != null) so.FindProperty("robotRespawnClip").objectReferenceValue = robotSpawn;
            if (eatRobotMusic != null) so.FindProperty("eatRobotMusicClip").objectReferenceValue = eatRobotMusic;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        // ---- Gameplay HUD font ----------------------------------------------------------------

        /// <summary>Score/Timer use Bangers SDF instead of the default LiberationSans SDF — a
        /// cartoon/comic display face matching the rest of the game's title and button art (per
        /// playtest feedback that plain default-font score/timer text looked out of place).</summary>
        private static void WireGameplayFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BangersFontPath);
            if (font == null)
            {
                Debug.LogWarning($"[ArtWiringBuilder] {BangersFontPath} not found — skipping cartoon font wiring.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);
            var canvasTransform = GameObject.Find("Canvas")?.transform;
            if (canvasTransform == null)
            {
                Debug.LogWarning("[ArtWiringBuilder] Could not find Canvas — skipping cartoon font wiring.");
                return;
            }

            SetFont(canvasTransform, "GameplayScreen/ScoreText", font);
            SetFont(canvasTransform, "GameplayScreen/TimerText", font);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        private static void SetFont(Transform canvasTransform, string path, TMP_FontAsset font)
        {
            var text = canvasTransform.Find(path)?.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                Debug.LogWarning($"[ArtWiringBuilder] Could not find TextMeshProUGUI at Canvas/{path} to wire font.");
                return;
            }
            text.font = font;
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
