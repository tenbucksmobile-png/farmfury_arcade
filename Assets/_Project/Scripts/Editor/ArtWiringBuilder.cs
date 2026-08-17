using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Abilities;
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
        // World 2 (VegPatch) — built by Phase2ProjectBuilder.BuildAll alongside the CornField ones.
        private const string WallVegPatchPrefabPath = BlockPrefabFolder + "/Wall_VegPatch.prefab";
        private const string WarpTunnelVegPatchPrefabPath = BlockPrefabFolder + "/WarpTunnel_VegPatch.prefab";
        private const string CropKernelVegPatchPrefabPath = BlockPrefabFolder + "/Crop_Kernel_VegPatch.prefab";
        private const string CropVegetableVegPatchPrefabPath = BlockPrefabFolder + "/Crop_Vegetable_VegPatch.prefab";
        private const string CoinPrefabPath = BlockPrefabFolder + "/Pickup_Coin.prefab";

        private const string ShockwavePrefabPath = AbilityPrefabFolder + "/Shockwave.prefab";
        private const string BounceTrailPrefabPath = AbilityPrefabFolder + "/BounceTrail.prefab";
        private const string WoollyClonePrefabPath = AbilityPrefabFolder + "/WoollyClone.prefab";
        private const string WaterTilePrefabPath = AbilityPrefabFolder + "/WaterTile.prefab";
        private const string DuckySplashPrefabPath = AbilityPrefabFolder + "/DuckySplash.prefab";
        private const string HoraceBuckPrefabPath = AbilityPrefabFolder + "/HoraceBuck.prefab";
        private const string WaterTileSprite = "Assets/_Project/Sprites/UI/Water_tile.png";

        private const string RosterCardPrefabPath = UIPrefabFolder + "/RosterCard.prefab";

        // ---- Sprite paths (existing, Cluck/Harvester/crops/pellets/UI backgrounds) -------------
        private const string CluckFront = "Assets/_Project/Sprites/Characters/Cluck_front.png";
        private const string CluckBack = "Assets/_Project/Sprites/Characters/Cluck_back.png";
        private const string CluckLeft = "Assets/_Project/Sprites/Characters/Cluck_left.png";
        private const string HarvesterFront = "Assets/_Project/Sprites/Robots/HarvestorRobot_front.png";
        private const string HarvesterBack = "Assets/_Project/Sprites/Robots/HarvestorRobot_back.png";
        private const string HarvesterLeft = "Assets/_Project/Sprites/Robots/HarvestorRobot_left.png";
        private const string HarvesterRight = "Assets/_Project/Sprites/Robots/HarvestorRobot_right.png";
        private const string CornKernel = "Assets/_Project/Sprites/Environment/CornKernel.png";
        private const string Carrot = "Assets/_Project/Sprites/Environment/carrot.png";
        private const string CoinIcon = "Assets/_Project/Sprites/Environment/Collectable Coin.png";
        private const string SunflowerPellet = "Assets/_Project/Sprites/Environment/RarePellets_sunflower.png";
        // GoldenWheatPellet ("maize") sat unwired for a while after the 3-tier pellet visual
        // (Sunflower/GoldenWheat/Rainbow) was replaced by one themed sprite per world (see
        // TileMapRenderer.MazeArtSet.pelletSprite's doc comment) — now reused as Wheat's
        // MazeArtSet.rarePelletSprite (WireOrchardAndWheat), the same "repurposed for a different
        // world's rare-tier slot" trick Orchard's rarePelletSprite already does with RainbowPellet.
        private const string GoldenWheatPellet = "Assets/_Project/Sprites/Environment/RarePellets_maize.png";
        private const string RainbowPellet = "Assets/_Project/Sprites/Environment/RarePellets_apple.png";
        // ---- World theme crops (CornField's vegetable-tier + all of VegPatch's crops) ----------
        private const string CornCob = "Assets/_Project/Sprites/Environment/CornCob.png";
        private const string Cabbage = "Assets/_Project/Sprites/Environment/cabbage.png";
        private const string LandingBackground = "Assets/_Project/Sprites/UI/landing.png";

        // ---- Sprite paths (this batch) ----------------------------------------------------------
        private const string BessieFront = "Assets/_Project/Sprites/Characters/Bessie_front.png";
        private const string BessieBack = "Assets/_Project/Sprites/Characters/Bessie_back.png";
        private const string BessieLeft = "Assets/_Project/Sprites/Characters/Bessie_left.png";
        private const string BessieLeft2 = "Assets/_Project/Sprites/Characters/Bessie_left2.png";
        private const string BessieRight = "Assets/_Project/Sprites/Characters/Bessie_right.png";
        private const string BessieRight2 = "Assets/_Project/Sprites/Characters/Bessie_right2.png";
        private const string WoollyFront = "Assets/_Project/Sprites/Characters/Wooly_front.png";
        private const string WoollyBack = "Assets/_Project/Sprites/Characters/Wooly_back.png";
        private const string WoollyLeft = "Assets/_Project/Sprites/Characters/Wooly_left.png";
        private const string WoollyRight = "Assets/_Project/Sprites/Characters/Wooly_right.png";
        private const string WoollyEffect = "Assets/_Project/Sprites/Characters/Wooly_effect.png";
        private const string PercyFront = "Assets/_Project/Sprites/Characters/Percy_front.png";
        private const string PercyBack = "Assets/_Project/Sprites/Characters/Percy_back.png";
        // PercyLeft ("Perccy_left.png", uploaded filename typo) never actually existed on disk —
        // WireNewCharacters' SetWalkFrames("Percy", ...) call always fell back to front/back for
        // Left, so Percy had no real directional art at all until this real left/right walk-cycle
        // pair landed (matching Cluck/Bessie's 2-frame flick convention).
        private const string PercyLeftWalk0 = "Assets/_Project/Sprites/Characters/Flat1.png";
        private const string PercyLeftWalk1 = "Assets/_Project/Sprites/Characters/Flat2.png";
        private const string PercyRightWalk0 = "Assets/_Project/Sprites/Characters/Right1.png";
        private const string PercyRightWalk1 = "Assets/_Project/Sprites/Characters/Right2.png";
        private const string PercyEffect = "Assets/_Project/Sprites/Characters/Percy_effect.png";
        private const string DuckyFront = "Assets/_Project/Sprites/Characters/Ducky_front.png";
        private const string DuckyBack = "Assets/_Project/Sprites/Characters/Ducky_back.png";
        private const string DuckyLeft = "Assets/_Project/Sprites/Characters/Ducky_left.png";
        private const string DuckyRight = "Assets/_Project/Sprites/Characters/Ducky_right.png";
        // SkipShot's departure splash — mirrored per direction, chosen at runtime by
        // SkipShotAbility based on which way she's actually skipping.
        private const string DuckyAbilityLeft = "Assets/_Project/Sprites/Characters/Ducky_ability_left.png";
        private const string DuckyAbilityRight = "Assets/_Project/Sprites/Characters/Ducky_ability_right.png";
        private const string BessieSlam = "Assets/_Project/Sprites/Characters/BessieSlam.png";
        private const string GeraldFront = "Assets/_Project/Sprites/Characters/Gerald_front.png";
        private const string GeraldBack = "Assets/_Project/Sprites/Characters/Gerald_back.png";
        private const string GeraldLeft = "Assets/_Project/Sprites/Characters/Gerald_left.png";
        // Real 2-frame Left walk cycle (Gerald_left.png/Gerald_left1.png) plus a single Right frame
        // (no "right1" yet — same one-frame-repeats-both-slots convention as Horace's Right).
        // Gerald_ability.png is uploaded but stays unwired here — PuffUpAbility has no spawned
        // effect object (it just scales Gerald's own sprite 3x, see WireNewCharacters' old comment),
        // so wiring it in would mean adding a new effect prefab + a spawn call, a gameplay change
        // rather than pure art wiring.
        private const string GeraldLeft1 = "Assets/_Project/Sprites/Characters/Gerald_left1.png";
        private const string GeraldRight = "Assets/_Project/Sprites/Characters/Gerald_right.png";
        private const string HoraceFront = "Assets/_Project/Sprites/Characters/Horace_front.png";
        // Horace_left.png (no suffix) is an earlier/superseded draft — Horace_left1/Left2.png are
        // the real 2-frame walk-cycle pair (same "extra art, no slot for it yet" convention as
        // Cluck_rightwalk.png). No back art yet — Up falls back to front. Only one Right frame
        // (right2, no "right1") exists so far — used for both Right0/Right1 slots.
        private const string HoraceLeft1 = "Assets/_Project/Sprites/Characters/Horace_left1.png";
        private const string HoraceLeft2 = "Assets/_Project/Sprites/Characters/Horace_Left2.png";
        private const string HoraceRight2 = "Assets/_Project/Sprites/Characters/Horace_right2.png";
        // Rear Kick's landing-impact "buck" — mirrored per knockback direction, same convention as
        // Ducky's splash.
        private const string HoraceAbilityBuckLeft = "Assets/_Project/Sprites/Characters/Horace_ability_buckleft.png";
        private const string HoraceAbilityBuckRight = "Assets/_Project/Sprites/Characters/Horace_ability_buckright.png";

        // Billy's first real art — full 2-frame walk cycle on both Left and Right (same flick
        // convention as Cluck/Bessie/Percy), plus front/back.
        private const string BillyFront = "Assets/_Project/Sprites/Characters/Billy_Front.png";
        private const string BillyBack = "Assets/_Project/Sprites/Characters/Billy_back.png";
        private const string BillyLeft0 = "Assets/_Project/Sprites/Characters/Billy_left.png";
        private const string BillyLeft1 = "Assets/_Project/Sprites/Characters/Billy_left1.png";
        private const string BillyRight0 = "Assets/_Project/Sprites/Characters/Billy_right.png";
        private const string BillyRight1 = "Assets/_Project/Sprites/Characters/Billy_right1.png";

        // ---- Sprite paths (this batch: Cluck 2nd-frame walk cycle + real Right art) ------------
        private const string CluckRight = "Assets/_Project/Sprites/Characters/Cluck_right.png";
        private const string CluckRightWalk2 = "Assets/_Project/Sprites/Characters/Cluck_rightwalk2.png";
        private const string CluckLeftWalk = "Assets/_Project/Sprites/Characters/Cluck_LeftWalk.png";

        // Egg Drop's egg — despite living in Sprites/Characters and originally named for a Cluck
        // power-up icon, this is actually the egg art (confirmed against the uploaded image). Was
        // never wired because of that naming mismatch, leaving Egg.prefab on its hot-pink
        // PlaceholderSprite square (see Phase4ProjectBuilder.BuildEggPrefab's doc comment for why
        // pink specifically).
        private const string CluckEggIcon = "Assets/_Project/Sprites/Characters/Power_1.png";
        // EggHazard's hit animation: cracked egg mid-burst, then the full sun-burst flash, before
        // the egg disappears — same "named for a Cluck power-up, actually egg art" naming quirk as
        // Power_1.png/CluckEggIcon above.
        private const string CluckEggCracked = "Assets/_Project/Sprites/Characters/CluckPower_2.png";
        private const string CluckEggBurst = "Assets/_Project/Sprites/Characters/CluckPower_3.png";
        private const string EggPrefabPath = AbilityPrefabFolder + "/Egg.prefab";

        // ---- Sprite paths (this batch: maze wall/floor/warp tunnel art) ------------------------
        private const string WallCornTiles = "Assets/_Project/Sprites/UI/CornTiles.png";
        private const string FloorTile = "Assets/_Project/Sprites/UI/FloorTile.png";
        private const string WarpTile = "Assets/_Project/Sprites/UI/WarpTile.png";

        // ---- World 2 (VegPatch) maze/backdrop art ------------------------------------------------
        private const string VegTile = "Assets/_Project/Sprites/UI/VegTile.png";
        private const string VeggiePatchWarp = "Assets/_Project/Sprites/UI/VeggiePatchWarp.png";
        private const string VegetableGardenBackdrop = "Assets/_Project/Sprites/UI/VegatableGarden.png";

        // ---- Level Complete star row (filled/empty), replacing StarDisplay's tinted placeholder --
        private const string FilledStar = "Assets/_Project/Sprites/UI/ScoreStar.png";
        private const string EmptyStar = "Assets/_Project/Sprites/UI/ClearStar.png";

        private const string ScoutFront = "Assets/_Project/Sprites/Robots/ScoutRobot_front.png";
        private const string ScoutBack = "Assets/_Project/Sprites/Robots/ScoutRobot_back.png";
        private const string ScoutLeft = "Assets/_Project/Sprites/Robots/ScoutRobot_left.png";
        private const string ScoutRight = "Assets/_Project/Sprites/Robots/ScoutRobot_right.png";
        private const string PatrolFront = "Assets/_Project/Sprites/Robots/PatrolRobot_Front.png";
        private const string PatrolBack = "Assets/_Project/Sprites/Robots/PatrolRobot_back.png";
        private const string PatrolLeft = "Assets/_Project/Sprites/Robots/PatrolRobot_left.png";
        private const string PatrolRight = "Assets/_Project/Sprites/Robots/PatrolRobot_right.png";
        private const string HeavyFront = "Assets/_Project/Sprites/Robots/HeavyRobot_front.png";
        private const string HeavyBack = "Assets/_Project/Sprites/Robots/HeavyRobot_back.png";
        private const string DrifterFront = "Assets/_Project/Sprites/Robots/DriftRobot_front.png";
        private const string DrifterBack = "Assets/_Project/Sprites/Robots/DriftRobot_back.png";
        private const string DrifterLeft = "Assets/_Project/Sprites/Robots/DriftRobot_left.png";
        private const string DrifterRight = "Assets/_Project/Sprites/Robots/DriftRobot_right.png";
        private const string RobotEyes = "Assets/_Project/Sprites/Robots/RobotEyes.png";
        private const string DroneFront = "Assets/_Project/Sprites/Robots/Drone.png";

        private const string LogoImage = "Assets/_Project/Sprites/UI/Logo.png";
        private const string AppIconImage = "Assets/_Project/Sprites/UI/AppIcon.png";
        private const string WheatfieldBackdrop = "Assets/_Project/Sprites/UI/Wheatfield_background.png";
        private const string SettingsBackground = "Assets/_Project/Sprites/UI/LoadingScreen Background.png";
        private const string LevelCompletePanel = "Assets/_Project/Sprites/UI/LevelComplete.png";
        private const string LevelFailedPanel = "Assets/_Project/Sprites/UI/LevelFailed.png";
        private const string PausedPanel = "Assets/_Project/Sprites/UI/Paused.png";
        // Pause's own backdrop per the 2026-07-31 Canva mockup — same cornfield/barn/moon night
        // scene as Bg_LevelSelect.png's composition but a different piece of art (previously unused
        // — see CLAUDE.md's Art status section prior to this).
        private const string PauseBackground = "Assets/_Project/Sprites/UI/World1_Cornfield.png";
        // New Character Unlock overlay's wood-sign banner. Was unlocked.png (a generic "Unlocked"
        // mockup placeholder); replaced with NewCharacter.png, a dedicated "New Character" sign
        // used for every animal character's unlock celebration regardless of which one unlocked
        // (same "one shared header art, per-character content is the card below it" convention
        // WorldUnlockedBannerSprite uses for worlds). unlocked.png is left on disk unreferenced,
        // same "old art stays, just unused" convention as every other art swap in this project.
        private const string UnlockedBannerSprite = "Assets/_Project/Sprites/UI/NewCharacter.png";
        // New World Unlock overlay's own "World Unlocked" wood-sign banner — shared across every
        // world (not a per-world sprite swap like worldBadge), same generic-header intent as
        // UnlockedBannerSprite just above but a distinct asset/screen.
        private const string WorldUnlockedBannerSprite = "Assets/_Project/Sprites/UI/WorldUnlocked.png";

        // ---- Level Select (filenames as actually uploaded — note LevelTile_unlocked-notplayed.png
        // and LevelTile_1Star.png differ slightly from the originally-specced
        // "LevelTile_Unlocked.png"/"LevelTile_1Stars.png") -------------------------------------
        private const string LevelTileLocked = "Assets/_Project/Sprites/UI/LevelTile_Locked.png";
        private const string LevelTileUnlocked = "Assets/_Project/Sprites/UI/LevelTile_unlocked-notplayed.png";
        private const string LevelTile1Star = "Assets/_Project/Sprites/UI/LevelTile_1Star.png";
        private const string LevelTile2Stars = "Assets/_Project/Sprites/UI/LevelTile_2Stars.png";
        private const string LevelTile3Stars = "Assets/_Project/Sprites/UI/LevelTile_3Stars.png";
        private const string BgLevelSelect = "Assets/_Project/Sprites/UI/Bg_LevelSelect.png";
        private const string DividerWorldBanner = "Assets/_Project/Sprites/UI/Divider_WorldBanner.png";
        // Word-art replacing the old TMP "SELECT LEVEL"/world-name text (per-request — see
        // WireLevelSelect). Renamed to the "*Sign.png" convention when re-uploaded — the originals
        // (SelectLevel.png/CornField.png/VegetablePatch.png/Orchard.png/Wheatfield.png) were
        // deleted. All 4 world signs + SelectLevelSign are now present on disk.
        private const string SelectLevelText = "Assets/_Project/Sprites/UI/SelectLevelSign.png";
        // Note: on-disk filename is "CornfieldSign.png" (lowercase "f"), unlike the other three
        // *Sign.png world badges (VegetablePatchSign/OrchardSign/WheatfieldSign) — matched exactly
        // since AssetDatabase.LoadAssetAtPath is case-sensitive regardless of the OS filesystem.
        private const string CornFieldText = "Assets/_Project/Sprites/UI/CornfieldSign.png";
        private const string VegetablePatchText = "Assets/_Project/Sprites/UI/VegetablePatchSign.png";
        private const string OrchardText = "Assets/_Project/Sprites/UI/OrchardSign.png";
        private const string WheatfieldText = "Assets/_Project/Sprites/UI/WheatfieldSign.png";
        private const string SettingsSignText = "Assets/_Project/Sprites/UI/SettingsSign.png";
        private const string LevelTilePrefabPath = UIPrefabFolder + "/LevelTile.prefab";
        private const string WorldDividerPrefabPath = UIPrefabFolder + "/WorldDivider.prefab";
        private const string BackgroundMusicClip = "Assets/_Project/Audio/Music/BackgroundMusic.mp3";
        private const string AnimalDeathSfx = "Assets/_Project/Audio/SFX/Animal_death.mp3";
        private const string CornPickupSfx = "Assets/_Project/Audio/SFX/CornPickup.mp3";
        private const string CoinPickupSfx = "Assets/_Project/Audio/SFX/CoinPickup.mp3";
        private const string PowerReadySfx = "Assets/_Project/Audio/SFX/PowerReady.mp3";
        private const string RarePelletPickupSfx = "Assets/_Project/Audio/SFX/RarePellet_pickup.mp3";
        private const string RobotSpawnSfx = "Assets/_Project/Audio/SFX/RobotSpawn.mp3";
        private const string EatRobotMusicClip = "Assets/_Project/Audio/SFX/EatRobot.mp3";
        private const string LandingMusicClip = "Assets/_Project/Audio/Music/LandingPage.mp3";

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

        // ---- Monetisation art (Revive prompt, skip-cooldown button, coin balance chip) ----------
        // Filename has spaces (the uploaded file, not a renamed convention like every other UI
        // asset here) — kept as-is rather than renamed on disk, since AssetDatabase.LoadAssetAtPath
        // works fine with spaces in a path.
        // Replacement panel art (same filename, content swapped by the user) now bakes "Revive for
        // 5 coins?" directly into its bottom slot — see BuildLevelComplete's RevivePromptOverlay
        // comment for why the runtime coin-icon/cost-text row was removed as a result.
        private const string RevivePromptPanel = "Assets/_Project/Sprites/UI/Revive Prompt panel background.png";
        // Yes.png/No.png replaced the original Btn_revive.png/Btn_decline.png wood-plank buttons —
        // both already bake in their own "Yes"/"No" label, so BuildLevelComplete destroys
        // ReviveButton/DeclineButton's auto-generated text labels now. Old Btn_revive.png/
        // Btn_decline.png are left on disk unreferenced, same "old art stays, just unused"
        // convention as every other art swap in this project.
        private const string BtnRevive = "Assets/_Project/Sprites/UI/Yes.png";
        private const string BtnDecline = "Assets/_Project/Sprites/UI/No.png";
        private const string BtnSkipCooldown = "Assets/_Project/Sprites/UI/Btn_skipcooldown.png";
        private const string BtnWatchAd = "Assets/_Project/Sprites/UI/WatchAd.png";
        // Currently unused — was wired onto the Revive prompt's cost-text row, removed alongside it
        // (see RevivePromptPanel's comment above). Left configured/imported in case a future screen
        // wants a standalone coin glyph.
        private const string CoinUI = "Assets/_Project/Sprites/UI/Coin_UI.png";
        private const string CoinBalanceChip = "Assets/_Project/Sprites/UI/Coin_Balance_Chip.png";
        private const string RetryButtonArt = "Assets/_Project/Sprites/UI/Retry.png";
        private const string MenuButtonArt = "Assets/_Project/Sprites/UI/Menu.png";
        private const string ResumeButtonArt = "Assets/_Project/Sprites/UI/Resume.png";
        private const string SwapCharacterButtonArt = "Assets/_Project/Sprites/UI/SwapCharacter.png";
        private const string RestartButtonArt = "Assets/_Project/Sprites/UI/Restart.png";
        private const string SettingsButtonArt = "Assets/_Project/Sprites/UI/Settings.png";
        private const string QuitButtonArt = "Assets/_Project/Sprites/UI/Quit.png";

        // ---- World 3 (Orchard) + World 4 (Wheat) maze/pickup art --------------------------------
        private const string OrchardWallTile = "Assets/_Project/Sprites/UI/Orchard_WallTile.png";
        private const string OrchardBackgroundSprite = "Assets/_Project/Sprites/UI/OrchardBackground.png";
        // Orchard's own warp-tunnel art (a tree-trunk hollow) — replaces the CornField WarpTile.png
        // reuse both Orchard and Wheat had before this.
        private const string OrchardWarpTile = "Assets/_Project/Sprites/UI/OrchardWarpTile.png";
        // Wheat's own dedicated warp-tunnel art (a wheat-sheaf swirl portal).
        private const string WheatWarpTile = "Assets/_Project/Sprites/UI/WheatWarpTile.png";
        // Split off of a shared FloorTile.png drop-in that would otherwise have also changed
        // CornField/VegPatch's ground (see WireOrchardAndWheat's doc comment) — copied to its own
        // file before the original FloorTile.png was restored to CornField's original soil art.
        private const string OrchardFloorTileSprite = "Assets/_Project/Sprites/UI/OrchardFloorTile.png";
        // Orchard's regular (every-tile) power pellet look. A distinct rare-tier sprite is still
        // pending a future upload — see WireOrchardAndWheat's doc comment for why there's no slot
        // for one yet in the current MazeArtSet architecture.
        private const string RedApplePellet = "Assets/_Project/Sprites/Environment/Red_Apple.png";
        // Orchard's bonus pickup — scattered x10 per level, not a power pellet at all (matches the
        // CornField coin's "extra pickups scattered on random walkable cells" role, not part of
        // LevelData.totalCropsRequired).
        private const string CherryBonus = "Assets/_Project/Sprites/Environment/Cherry.png";
        // Wheat's regular (every-tile) pellet and bonus pickup — MiniLoaf as the regular per-tile
        // pellet, RareGrainSack as the bonus pickup (now x10 like Cherry, not x1 — "Rare" turned
        // out to just be flavour naming, not an actual scarcity request).
        private const string MiniLoafPellet = "Assets/_Project/Sprites/Environment/MiniLoaf.png";
        private const string RareGrainSackBonus = "Assets/_Project/Sprites/Environment/RareGrainSack.png";
        // Wheat's wall/ground tile art (landed this session — previously Wheat had neither).
        private const string WheatWallTile = "Assets/_Project/Sprites/UI/WheatWallTile.png";
        private const string WheatFloorTileSprite = "Assets/_Project/Sprites/UI/WheatFloorTile.png";
        private const string WallOrchardPrefabPath = BlockPrefabFolder + "/Wall_Orchard.prefab";
        private const string GroundOrchardPrefabPath = BlockPrefabFolder + "/Ground_Orchard.prefab";
        private const string WarpTunnelOrchardPrefabPath = BlockPrefabFolder + "/WarpTunnel_Orchard.prefab";
        private const string WarpTunnelWheatPrefabPath = BlockPrefabFolder + "/WarpTunnel_Wheat.prefab";
        private const string PickupCherryPrefabPath = BlockPrefabFolder + "/Pickup_Cherry.prefab";
        private const string CropKernelOrchardPrefabPath = BlockPrefabFolder + "/Crop_Kernel_Orchard.prefab";
        private const string CropVegetableOrchardPrefabPath = BlockPrefabFolder + "/Crop_Vegetable_Orchard.prefab";
        private const string WallWheatPrefabPath = BlockPrefabFolder + "/Wall_Wheat.prefab";
        private const string GroundWheatPrefabPath = BlockPrefabFolder + "/Ground_Wheat.prefab";
        private const string PickupGrainSackPrefabPath = BlockPrefabFolder + "/Pickup_GrainSack.prefab";
        private const string CropKernelWheatPrefabPath = BlockPrefabFolder + "/Crop_Kernel_Wheat.prefab";
        private const string CropVegetableWheatPrefabPath = BlockPrefabFolder + "/Crop_Vegetable_Wheat.prefab";

        // ---- ChooseCharacterScreen card art (framed "animal card" portraits) -------------------
        private const string CluckCard = "Assets/_Project/Sprites/UI/Cluck_Chicken.png";
        private const string BessieCard = "Assets/_Project/Sprites/UI/Bessie_Cow.png";
        private const string PercyCard = "Assets/_Project/Sprites/UI/Percy_Pig.png";
        // Was "Woolly_Sheep.png" (double-L) — the actual uploaded filename is single-L "Wooly_Sheep.png"
        // (matching the same "Wooly" spelling every other Woolly asset uses), so this never resolved
        // and Woolly's select-card art silently stayed unwired since the card system was added.
        private const string WoollyCard = "Assets/_Project/Sprites/UI/Wooly_Sheep.png";
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
            WireEgg();
            WireHarvester();
            WireCropsAndPellets();
            WireMazeTiles();
            WireOrchardAndWheat();
            WireGameplayBackdrop();
            WireBackgrounds();
            WireLevelCompleteStars();

            WireNewCharacters();
            WireCharacterSelectCards();
            WireNewRobots();
            WireAbilityEffects();
            WireCardsAndPanels();
            WireButtons();
            WireLevelSelect();
            WireAppIcon();
            WireAudio();
            WireGameplayFont();
            WireSettingsFont();
            WireSettingsSign();
            WireMonetisationArt();

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
            CluckFront, CluckBack, CluckLeft, HarvesterFront, HarvesterBack, HarvesterLeft, HarvesterRight, CornKernel, Carrot,
            CoinIcon, SunflowerPellet, GoldenWheatPellet, RainbowPellet, LandingBackground,
            BessieFront, BessieBack, BessieLeft, BessieLeft2, BessieRight, BessieRight2,
            WoollyFront, WoollyBack, WoollyLeft, WoollyRight, WoollyEffect,
            PercyFront, PercyBack, PercyLeftWalk0, PercyLeftWalk1, PercyRightWalk0, PercyRightWalk1, PercyEffect, DuckyFront, DuckyBack, DuckyLeft, DuckyRight, BessieSlam,
            DuckyAbilityLeft, DuckyAbilityRight,
            HoraceFront, HoraceLeft1, HoraceLeft2, HoraceRight2, HoraceAbilityBuckLeft, HoraceAbilityBuckRight,
            ScoutFront, ScoutBack, ScoutLeft, ScoutRight, PatrolFront, PatrolBack, PatrolLeft, PatrolRight,
            HeavyFront, HeavyBack, DrifterFront, DrifterLeft, DrifterRight, DrifterBack, RobotEyes, DroneFront,
            LevelCompletePanel, LevelFailedPanel, PausedPanel,
            BtnPlay, BtnPause, BtnSettings, BtnQuit, BtnMusic, BtnNoSound, BtnHome, BtnSkip, BtnBack, BtnPlaque,
            RevivePromptPanel, BtnRevive, BtnDecline, BtnWatchAd, BtnSkipCooldown, CoinUI, CoinBalanceChip,
            RetryButtonArt, MenuButtonArt, ResumeButtonArt, SwapCharacterButtonArt, RestartButtonArt,
            SettingsButtonArt, QuitButtonArt,
            CluckCard, BessieCard, PercyCard, WoollyCard, DuckyCard, HoraceCard, GeraldCard, BillyCard,
            DPadUp, DPadDown, DPadLeft, DPadRight,
            GeraldFront, GeraldBack, GeraldLeft, GeraldLeft1, GeraldRight,
            BillyFront, BillyBack, BillyLeft0, BillyLeft1, BillyRight0, BillyRight1,
            CluckRight, CluckRightWalk2, CluckLeftWalk,
            WallCornTiles, FloorTile, WarpTile, WheatfieldBackdrop, SettingsBackground, PauseBackground,
            VegTile, VeggiePatchWarp, VegetableGardenBackdrop, FilledStar, EmptyStar, CornCob, Cabbage,
            LevelTileLocked, LevelTileUnlocked, LevelTile1Star, LevelTile2Stars, LevelTile3Stars,
            BgLevelSelect, DividerWorldBanner, WaterTileSprite, UnlockedBannerSprite, WorldUnlockedBannerSprite,
            OrchardWallTile, OrchardFloorTileSprite, OrchardBackgroundSprite, OrchardWarpTile, WheatWarpTile, RedApplePellet, CherryBonus,
            WheatWallTile, WheatFloorTileSprite, MiniLoafPellet, RareGrainSackBonus,
            SelectLevelText, CornFieldText, VegetablePatchText, OrchardText, WheatfieldText, SettingsSignText,
            LogoImage, CluckEggIcon, CluckEggCracked, CluckEggBurst
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
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                importer.spritePixelsPerUnit = width > 0 ? width : 100;

                // Stopgap for a real sizing bug: Billy_Front.png/Billy_back.png are tight portrait
                // crops (213x401 / 234x408) but Billy_left/right(1).png are a much more loosely
                // padded 500x500 square — under the standard "PPU = this texture's own width" rule
                // above, that made him render only ~1.0 world unit tall facing left/right versus
                // ~1.88 tall facing up/down (CharacterAnimator swaps sprites with no per-frame scale
                // compensation), a visible size pop every time he turns sideways. Overriding PPU
                // here to match Front's height ratio (401/213) instead of this texture's own width
                // keeps his apparent height consistent across every facing — at the cost of also
                // rendering him wider than 1 grid cell while walking sideways, since a single PPU
                // scalar can't correct height without scaling width too on a square source. Replace
                // with a tighter crop of the Left/Right art (matching Front/Back's framing) instead
                // of this override whenever that art lands.
                if (path == BillyLeft0 || path == BillyLeft1 || path == BillyRight0 || path == BillyRight1)
                {
                    importer.spritePixelsPerUnit = 500f * 213f / 401f;
                }

                // Btn_plaque.png is a wide rounded-pill button graphic reused as a per-row
                // background on the Settings screen (see Phase5ProjectBuilder.WrapInPlaqueRow),
                // where rows are much wider/shorter than the source art's own aspect. A plain
                // Simple-type stretch would smear its rounded caps into straight corners; a 9-slice
                // border (computed as a fraction of its own pixel size, since exact dimensions
                // aren't hardcoded anywhere else) keeps the caps crisp when SetImageSprite applies
                // Image.Type.Sliced.
                if (path == BtnPlaque && width > 0 && height > 0)
                {
                    float borderX = width * 0.30f;
                    float borderY = height * 0.42f;
                    importer.spriteBorder = new Vector4(borderX, borderY, borderX, borderY);
                }

                importer.SaveAndReimport();
            }
        }

        private static Sprite Load(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[ArtWiringBuilder] No sprite found at {path} — the referencing field will be left unset.");
            }
            return sprite;
        }

        /// <summary>Cluck_front.png (facing the camera) and Cluck_back.png (facing away, no face
        /// visible) both exist as real, distinct art — Up (walking away from camera, toward the top
        /// of the screen) uses back; Down (walking toward the camera, toward the bottom of the
        /// screen) uses front. A previous pass wired Cluck_back.png for BOTH Up and Down, so she
        /// showed her back the whole time she walked downward (and while idle, since
        /// CharacterAnimator shows Down0 when not moving — see its own doc comment) — confirmed by
        /// comparing Cluck_front.png against Cluck_back.png directly, not guessed. Left and Right
        /// each DO have real dedicated 2-frame walk art on disk (Cluck_left.png + Cluck_LeftWalk.png;
        /// Cluck_right.png + Cluck_rightwalk2.png) — a previous pass wired only Cluck_back.png for
        /// every direction except Left1, leaving her facing backward while visibly walking left/right.
        /// Wired properly here. Cluck_rightwalk.png (a third right-facing frame beyond the 2-frame
        /// Right0/Right1 slots this system supports) stays unreferenced — kept on disk, same "extra
        /// art, no slot for it yet" convention as everywhere else. Right0/Right1 are set explicitly
        /// (not left to CharacterAnimator's flipX-mirror-Left fallback) so hasDedicatedRightArt stays
        /// true and they render un-mirrored.</summary>
        private static void WireCluck()
        {
            var front = Load(CluckFront);
            var back = Load(CluckBack);
            var left = Load(CluckLeft);
            var leftWalk = Load(CluckLeftWalk);
            var right = Load(CluckRight);
            var rightWalk2 = Load(CluckRightWalk2);

            string path = $"{CharacterDataFolder}/CharacterData_Cluck.asset";
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data != null)
            {
                data.walkAnimationFrames = new[]
                {
                    back, back,                                  // Up0, Up1 (facing away from camera)
                    front, front,                                // Down0, Down1 (facing camera; also her idle pose)
                    left != null ? left : back, leftWalk != null ? leftWalk : back,    // Left0, Left1
                    right != null ? right : back, rightWalk2 != null ? rightWalk2 : back // Right0, Right1
                };
                data.hasDedicatedRightArt = true;
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

        /// <summary>Replaces Egg.prefab's hot-pink PlaceholderSprite square with the real egg art —
        /// prefab scale/sortingOrder (set in Phase4ProjectBuilder.BuildEggPrefab) are left untouched,
        /// since ConfigureSpriteImporters' spritePixelsPerUnit-equals-texture-width convention already
        /// makes any wired sprite fill the same footprint the placeholder did at the same localScale.</summary>
        private static void WireEgg()
        {
            var eggSprite = Load(CluckEggIcon);
            if (eggSprite == null)
            {
                Debug.LogWarning($"[ArtWiringBuilder] {CluckEggIcon} not found — skipping egg wiring.");
                return;
            }

            var cracked = Load(CluckEggCracked);
            var burst = Load(CluckEggBurst);

            var contents = PrefabUtility.LoadPrefabContents(EggPrefabPath);
            var sr = contents.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = eggSprite;
                sr.color = Color.white;
            }
            var hazard = contents.GetComponent<EggHazard>();
            if (hazard != null)
            {
                var hazardSO = new SerializedObject(hazard);
                if (cracked != null) hazardSO.FindProperty("crackedSprite").objectReferenceValue = cracked;
                if (burst != null) hazardSO.FindProperty("burstSprite").objectReferenceValue = burst;
                hazardSO.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(contents, EggPrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        /// <summary>Bessie is the second character (after Cluck) with real 2nd-walk-frame and
        /// dedicated Right art — same pattern WireCluck established, duplicated rather than
        /// generalised into SetWalkFrames since only these two characters have it so far and a
        /// 6-sprite parameter list would make every other (single-pose) SetWalkFrames call site
        /// harder to read for no benefit yet.</summary>
        private static void WireBessie()
        {
            var front = Load(BessieFront);
            var back = Load(BessieBack);
            var left = Load(BessieLeft);
            var left2 = Load(BessieLeft2);
            var right = Load(BessieRight);
            var right2 = Load(BessieRight2);

            string path = $"{CharacterDataFolder}/CharacterData_Bessie.asset";
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data != null)
            {
                // Right frame order confirmed against the actual uploaded art (2026 review pass):
                // right2 plays first, then right — the reverse of Left's left-then-left2 order.
                data.walkAnimationFrames = new[]
                {
                    back, back,                                    // Up0, Up1
                    front, front,                                   // Down0, Down1
                    left, left2 != null ? left2 : left,             // Left0, Left1
                    right2 != null ? right2 : (right != null ? right : left), // Right0
                    right != null ? right : left                    // Right1
                };
                data.hasDedicatedRightArt = right != null;
                data.portraitSprite = front;
                EditorUtility.SetDirty(data);
            }
            else
            {
                Debug.LogWarning($"[ArtWiringBuilder] CharacterData_Bessie not found at {path}");
            }

            WireCharacterPrefabSprite($"{CharacterPrefabFolder}/Bessie.prefab", front);
        }

        /// <summary>Woolly's real Right art (Wooly_right.png) — she previously had only
        /// front/back/left, so Right silently fell back to a mirrored Left via SetWalkFrames'
        /// leftOrFront convention. Wired explicitly (not through SetWalkFrames) so
        /// hasDedicatedRightArt is set and Right renders un-mirrored, same convention as
        /// WireCluck/WirePercy.</summary>
        private static void WireWoolly()
        {
            var front = Load(WoollyFront);
            var back = Load(WoollyBack);
            var left = Load(WoollyLeft);
            var right = Load(WoollyRight);

            string path = $"{CharacterDataFolder}/CharacterData_Woolly.asset";
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data != null)
            {
                Sprite leftOrFront = left != null ? left : front;
                data.walkAnimationFrames = new[]
                {
                    back, back,                              // Up0, Up1
                    front, front,                            // Down0, Down1
                    leftOrFront, leftOrFront,                 // Left0, Left1
                    right != null ? right : leftOrFront, right != null ? right : leftOrFront // Right0, Right1
                };
                data.hasDedicatedRightArt = right != null;
                data.portraitSprite = front;
                EditorUtility.SetDirty(data);
            }
            else
            {
                Debug.LogWarning($"[ArtWiringBuilder] CharacterData_Woolly not found at {path}");
            }

            WireCharacterPrefabSprite($"{CharacterPrefabFolder}/Woolly.prefab", front);
        }

        /// <summary>Ducky's real Left/Right art (Ducky_left.png/Ducky_right.png) — she previously had
        /// only front/back, so SetWalkFrames' leftOrFront fallback used front for every side-facing
        /// direction (see its own doc comment: "Ducky has no profile art"). Wired explicitly (not
        /// through SetWalkFrames) so hasDedicatedRightArt is set and Right renders un-mirrored, same
        /// convention as WireCluck/WirePercy/WireWoolly.</summary>
        private static void WireDucky()
        {
            var front = Load(DuckyFront);
            var back = Load(DuckyBack);
            var left = Load(DuckyLeft);
            var right = Load(DuckyRight);

            string path = $"{CharacterDataFolder}/CharacterData_Ducky.asset";
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data != null)
            {
                Sprite leftOrFront = left != null ? left : front;
                data.walkAnimationFrames = new[]
                {
                    back, back,                              // Up0, Up1
                    front, front,                            // Down0, Down1
                    leftOrFront, leftOrFront,                 // Left0, Left1
                    right != null ? right : leftOrFront, right != null ? right : leftOrFront // Right0, Right1
                };
                data.hasDedicatedRightArt = right != null;
                data.portraitSprite = front;
                EditorUtility.SetDirty(data);
            }
            else
            {
                Debug.LogWarning($"[ArtWiringBuilder] CharacterData_Ducky not found at {path}");
            }

            WireCharacterPrefabSprite($"{CharacterPrefabFolder}/Ducky.prefab", front);
        }

        /// <summary>Horace's first real art — previously a solid-colour placeholder square in every
        /// direction. Left gets a real 2-frame walk cycle (Horace_left1/Left2.png — Horace_left.png,
        /// no suffix, is an earlier superseded draft left unreferenced, same "extra art, no slot for
        /// it yet" convention as Cluck_rightwalk.png); Right has only one uploaded frame
        /// (Horace_right2.png, no "right1") so it repeats for both Right0/Right1, same as any
        /// single-pose direction elsewhere — it's still real, dedicated art, not a Left mirror, so
        /// hasDedicatedRightArt is still set. No Up/back art yet — Up falls back to front.</summary>
        private static void WireHorace()
        {
            var front = Load(HoraceFront);
            var left1 = Load(HoraceLeft1);
            var left2 = Load(HoraceLeft2);
            var right2 = Load(HoraceRight2);

            string path = $"{CharacterDataFolder}/CharacterData_Horace.asset";
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data != null)
            {
                Sprite left1OrFront = left1 != null ? left1 : front;
                Sprite left2OrFront = left2 != null ? left2 : left1OrFront;
                data.walkAnimationFrames = new[]
                {
                    front, front,                             // Up0, Up1 (no back art yet)
                    front, front,                              // Down0, Down1
                    left1OrFront, left2OrFront,                // Left0, Left1
                    right2 != null ? right2 : left1OrFront, right2 != null ? right2 : left2OrFront // Right0, Right1
                };
                data.hasDedicatedRightArt = right2 != null;
                data.portraitSprite = front;
                EditorUtility.SetDirty(data);
            }
            else
            {
                Debug.LogWarning($"[ArtWiringBuilder] CharacterData_Horace not found at {path}");
            }

            WireCharacterPrefabSprite($"{CharacterPrefabFolder}/Horace.prefab", front);
        }

        /// <summary>Percy's real directional art — same 2-frame flick convention as Cluck/Bessie.
        /// He had no genuine Left/Right art before this (PercyLeft pointed at a misspelled,
        /// never-uploaded filename, so SetWalkFrames always fell back to front/back for every
        /// direction).</summary>
        private static void WirePercy()
        {
            var front = Load(PercyFront);
            var back = Load(PercyBack);
            var left0 = Load(PercyLeftWalk0);
            var left1 = Load(PercyLeftWalk1);
            var right0 = Load(PercyRightWalk0);
            var right1 = Load(PercyRightWalk1);

            string path = $"{CharacterDataFolder}/CharacterData_Percy.asset";
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data != null)
            {
                data.walkAnimationFrames = new[]
                {
                    back, back,                                          // Up0, Up1
                    front, front,                                        // Down0, Down1
                    left0 != null ? left0 : back, left1 != null ? left1 : (left0 != null ? left0 : back),    // Left0, Left1
                    right0 != null ? right0 : back, right1 != null ? right1 : (right0 != null ? right0 : back) // Right0, Right1
                };
                data.hasDedicatedRightArt = right0 != null;
                data.portraitSprite = front;
                EditorUtility.SetDirty(data);
            }
            else
            {
                Debug.LogWarning($"[ArtWiringBuilder] CharacterData_Percy not found at {path}");
            }

            WireCharacterPrefabSprite($"{CharacterPrefabFolder}/Percy.prefab", front);
        }

        /// <summary>Gerald's real directional art — a 2-frame Left walk cycle (Gerald_left.png →
        /// Gerald_left1.png) and a single-frame Right (Gerald_right.png, repeated for both Right0/
        /// Right1 slots, same convention as Horace's Right). Replaces the old front/back/left-only
        /// SetWalkFrames call, which mirrored Left for Right.</summary>
        private static void WireGerald()
        {
            var front = Load(GeraldFront);
            var back = Load(GeraldBack);
            var left0 = Load(GeraldLeft);
            var left1 = Load(GeraldLeft1);
            var right = Load(GeraldRight);

            string path = $"{CharacterDataFolder}/CharacterData_Gerald.asset";
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data != null)
            {
                Sprite left0OrFront = left0 != null ? left0 : front;
                Sprite left1OrFront = left1 != null ? left1 : left0OrFront;
                data.walkAnimationFrames = new[]
                {
                    back, back,                               // Up0, Up1
                    front, front,                              // Down0, Down1
                    left0OrFront, left1OrFront,                 // Left0, Left1
                    right != null ? right : left0OrFront, right != null ? right : left1OrFront // Right0, Right1
                };
                data.hasDedicatedRightArt = right != null;
                data.portraitSprite = front;
                EditorUtility.SetDirty(data);
            }
            else
            {
                Debug.LogWarning($"[ArtWiringBuilder] CharacterData_Gerald not found at {path}");
            }

            WireCharacterPrefabSprite($"{CharacterPrefabFolder}/Gerald.prefab", front);
        }

        /// <summary>Billy's first real art — full 2-frame walk cycles on both Left and Right
        /// (Billy_left.png → Billy_left1.png, Billy_right.png → Billy_right1.png), same flick
        /// convention as Cluck/Bessie/Percy. Previously a solid-colour placeholder in every
        /// direction.</summary>
        private static void WireBilly()
        {
            var front = Load(BillyFront);
            var back = Load(BillyBack);
            var left0 = Load(BillyLeft0);
            var left1 = Load(BillyLeft1);
            var right0 = Load(BillyRight0);
            var right1 = Load(BillyRight1);

            string path = $"{CharacterDataFolder}/CharacterData_Billy.asset";
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data != null)
            {
                Sprite left0OrFront = left0 != null ? left0 : front;
                Sprite left1OrFront = left1 != null ? left1 : left0OrFront;
                data.walkAnimationFrames = new[]
                {
                    back, back,                                          // Up0, Up1
                    front, front,                                        // Down0, Down1
                    left0OrFront, left1OrFront,                          // Left0, Left1
                    right0 != null ? right0 : left0OrFront, right1 != null ? right1 : (right0 != null ? right0 : left1OrFront) // Right0, Right1
                };
                data.hasDedicatedRightArt = right0 != null;
                data.portraitSprite = front;
                EditorUtility.SetDirty(data);
            }
            else
            {
                Debug.LogWarning($"[ArtWiringBuilder] CharacterData_Billy not found at {path}");
            }

            WireCharacterPrefabSprite($"{CharacterPrefabFolder}/Billy.prefab", front);
        }

        private static void WireHarvester()
        {
            var front = Load(HarvesterFront);
            var back = Load(HarvesterBack);
            var left = Load(HarvesterLeft);
            var right = Load(HarvesterRight);

            var contents = PrefabUtility.LoadPrefabContents(HarvesterPrefabPath);
            var sr = contents.GetComponent<SpriteRenderer>();
            if (sr != null && front != null)
            {
                sr.sprite = front;
            }
            var visual = contents.GetComponent<RobotVisual>();
            if (visual != null)
            {
                // Harvester now has real Left/Right art too (previously front/back only).
                visual.SetDirectionalSprites(front, back, left, right);
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

        /// <summary>Per-world crop/pellet reskin: CornField keeps CornKernel.png for its
        /// kernel-tier crop but now uses CornCob.png (not carrot.png) for its vegetable-tier crop;
        /// VegPatch's kernel-tier crop takes over carrot.png, its vegetable-tier crop uses
        /// cabbage.png. Every pellet in a maze now shows a single world-themed sprite (sunflower
        /// glow for CornField, apple for VegPatch) via TileMapRenderer.SetPelletSprite — see
        /// MazeArtSet.pelletSprite's doc comment for why the old 3-tier visual (Sunflower/
        /// GoldenWheat/Rainbow) was dropped. Also wires World 1's bonus coin pickup.</summary>
        private static void WireCropsAndPellets()
        {
            SetPrefabSprite(CropCornPrefabPath, Load(CornKernel));
            SetPrefabSprite(CropVegetablePrefabPath, Load(CornCob));
            SetPrefabSprite(PowerPelletPrefabPath, Load(SunflowerPellet));
            SetPrefabSprite(CropKernelVegPatchPrefabPath, Load(Carrot));
            SetPrefabSprite(CropVegetableVegPatchPrefabPath, Load(Cabbage));
            SetPrefabSprite(CoinPrefabPath, Load(CoinIcon));

            EditorSceneManager.OpenScene(ScenePath);
            var managersGO = GameObject.Find("GameManagers");
            var tileMapRenderer = managersGO != null ? managersGO.GetComponent<TileMapRenderer>() : null;
            if (tileMapRenderer != null)
            {
                tileMapRenderer.SetPelletSprite(MazeType.CornField, Load(SunflowerPellet));
                tileMapRenderer.SetPelletSprite(MazeType.VegPatch, Load(RainbowPellet));
                EditorUtility.SetDirty(tileMapRenderer);
            }
            else
            {
                Debug.LogWarning("[ArtWiringBuilder] Could not find TileMapRenderer on GameManagers to wire pellet sprites.");
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        /// <summary>Wires the maze wall/ground/warp-tunnel tile art. All prefabs are instantiated
        /// per-cell by TileMapRenderer at scale 1 (same convention crops/pellets already use), so a
        /// straight SpriteRenderer swap is enough — no tiling/atlas slicing, since each uploaded
        /// file is a single complete tile image, not a tileset. Also wires World 2 (VegPatch)'s
        /// wall/warp art (Wall_VegPatch/WarpTunnel_VegPatch, built by Phase2ProjectBuilder alongside
        /// the CornField ones) — ground has no dedicated VegPatch art yet, so GroundPrefabPath (the
        /// same Ground_CornField prefab) stays the only ground sprite wired, matching
        /// Phase2ProjectBuilder.WireScene's MazeArtSets, where both worlds' entries already share
        /// that one groundPrefab reference.</summary>
        private static void WireMazeTiles()
        {
            SetPrefabSprite(WallPrefabPath, Load(WallCornTiles));
            SetPrefabSprite(GroundPrefabPath, Load(FloorTile));
            SetPrefabSprite(WarpTunnelPrefabPath, Load(WarpTile));
            SetPrefabSprite(WallVegPatchPrefabPath, Load(VegTile));
            SetPrefabSprite(WarpTunnelVegPatchPrefabPath, Load(VeggiePatchWarp));
            SetPrefabSprite(WaterTilePrefabPath, Load(WaterTileSprite));
        }

        /// <summary>Wires World 3 (Orchard)'s and World 4 (Wheat)'s wall/ground/backdrop/regular-
        /// pellet/rare-pellet/crop art and bonus pickups (both x10 now, scattered like CornField's
        /// coin). These MazeArtSet entries are added/updated additively via TileMapRenderer.
        /// GetOrAddArtSet rather than through Phase2ProjectBuilder's WireScene (which only rebuilds
        /// the CornField/VegPatch entries it already knows about) — both worlds now have real
        /// LevelData (Phase2ProjectBuilder's BuildLevelData51-75 for Orchard, BuildLevelData76-100
        /// for Wheat), but their art still lands through this separate pass since that's where
        /// every other field on their MazeArtSet already lives. Both worlds' warp-tunnel prefabs
        /// still reuse CornField's (no dedicated art for those yet). Both worlds now have their own
        /// crop prefabs, though: Orchard's (Crop_Kernel_Orchard/Crop_Vegetable_Orchard) are wired to
        /// Red_Apple.png, Wheat's (Crop_Kernel_Wheat/Crop_Vegetable_Wheat) to MiniLoaf.png (Wheat's
        /// own "regular pellet" sprite, reused here the same way Orchard's Red_Apple.png was) —
        /// both used to share CornField's Crop_Corn/Crop_Vegetable, which meant every id-2/id-3
        /// crop tile in an Orchard or Wheat maze rendered CornKernel.png/CornCob.png; caught and
        /// fixed per feedback, Orchard first, Wheat as the same issue found on a follow-up check.
        /// Both worlds' warp tunnels now have their own dedicated art too (OrchardWarpTile.png, a
        /// tree-trunk hollow; WheatWarpTile.png, a wheat-sheaf swirl portal) via new
        /// WarpTunnel_Orchard/WarpTunnel_Wheat prefabs (Phase2ProjectBuilder.BuildAll) — neither
        /// reuses CornField's WarpTunnel prefab/WarpTile.png anymore.
        /// Each world's rare-tier pellet (MazeArtSet.rarePelletSprite — the single pellet that wins
        /// the maze's one-rare-slot cap, ConfigurePelletTier) reuses a sprite originally uploaded
        /// for a different purpose elsewhere: Orchard's is RarePellets_apple.png (loaded via the
        /// RainbowPellet constant, a legacy name from the old 3-tier pellet system — the file itself
        /// is apple-themed), Wheat's is RarePellets_maize.png (previously unwired dead weight left
        /// over from that same old system) — MazeArtSet entries are independent per world, so
        /// reusing a sprite across two unrelated roles is harmless.</summary>
        private static void WireOrchardAndWheat()
        {
            SetPrefabSprite(WallOrchardPrefabPath, Load(OrchardWallTile));
            SetPrefabSprite(GroundOrchardPrefabPath, Load(OrchardFloorTileSprite));
            SetPrefabSprite(WarpTunnelOrchardPrefabPath, Load(OrchardWarpTile));
            SetPrefabSprite(PickupCherryPrefabPath, Load(CherryBonus));
            SetPrefabSprite(CropKernelOrchardPrefabPath, Load(RedApplePellet));
            SetPrefabSprite(CropVegetableOrchardPrefabPath, Load(RedApplePellet));
            SetPrefabSprite(WallWheatPrefabPath, Load(WheatWallTile));
            SetPrefabSprite(GroundWheatPrefabPath, Load(WheatFloorTileSprite));
            SetPrefabSprite(WarpTunnelWheatPrefabPath, Load(WheatWarpTile));
            SetPrefabSprite(PickupGrainSackPrefabPath, Load(RareGrainSackBonus));
            SetPrefabSprite(CropKernelWheatPrefabPath, Load(MiniLoafPellet));
            SetPrefabSprite(CropVegetableWheatPrefabPath, Load(MiniLoafPellet));

            EditorSceneManager.OpenScene(ScenePath);
            var tileMapRenderer = GameObject.Find("GameManagers")?.GetComponent<TileMapRenderer>();
            if (tileMapRenderer == null)
            {
                Debug.LogWarning("[ArtWiringBuilder] Could not find TileMapRenderer on GameManagers to wire Orchard/Wheat art.");
                return;
            }

            var orchard = tileMapRenderer.GetOrAddArtSet(MazeType.Orchard);
            orchard.wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallOrchardPrefabPath);
            orchard.groundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GroundOrchardPrefabPath);
            orchard.warpTunnelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WarpTunnelOrchardPrefabPath);
            orchard.cropKernelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CropKernelOrchardPrefabPath);
            orchard.cropVegetablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CropVegetableOrchardPrefabPath);
            orchard.backdropSprite = Load(OrchardBackgroundSprite);
            orchard.pelletSprite = Load(RedApplePellet);
            orchard.rarePelletSprite = Load(RainbowPellet);
            orchard.bonusPickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupCherryPrefabPath);
            orchard.bonusPickupCount = 10;

            var wheat = tileMapRenderer.GetOrAddArtSet(MazeType.Wheat);
            wheat.wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallWheatPrefabPath);
            wheat.groundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GroundWheatPrefabPath);
            wheat.warpTunnelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WarpTunnelWheatPrefabPath);
            wheat.cropKernelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CropKernelWheatPrefabPath);
            wheat.cropVegetablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CropVegetableWheatPrefabPath);
            wheat.backdropSprite = Load(WheatfieldBackdrop);
            wheat.pelletSprite = Load(MiniLoafPellet);
            wheat.rarePelletSprite = Load(GoldenWheatPellet);
            wheat.bonusPickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupGrainSackPrefabPath);
            wheat.bonusPickupCount = 10;

            EditorUtility.SetDirty(tileMapRenderer);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        /// <summary>Wires real filled/empty star art onto LevelCompleteScreen's StarDisplay —
        /// see StarDisplay's doc comment for why it previously read as flat "brown boxes" instead of
        /// stars (a baked-color placeholder square being double-tinted at runtime).</summary>
        private static void WireLevelCompleteStars()
        {
            var filled = Load(FilledStar);
            var empty = Load(EmptyStar);
            if (filled == null || empty == null)
            {
                Debug.LogWarning($"[ArtWiringBuilder] {FilledStar} or {EmptyStar} not found — StarDisplay keeps its placeholder star shape.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);
            var canvasTransform = GameObject.Find("Canvas")?.transform;
            var starDisplay = canvasTransform?.Find("LevelCompleteScreen/PanelArt/ShelfContent/Stars")
                ?.GetComponent<StarDisplay>();
            if (starDisplay == null)
            {
                Debug.LogWarning("[ArtWiringBuilder] Could not find LevelCompleteScreen's StarDisplay to wire star art.");
                return;
            }

            var so = new SerializedObject(starDisplay);
            so.FindProperty("filledStarSprite").objectReferenceValue = filled;
            so.FindProperty("emptyStarSprite").objectReferenceValue = empty;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        /// <summary>Gameplay had no art behind the maze grid at all — just the camera's clear
        /// color, a documented gap. World1_Cornfield.png (already used as Pause/Choose Character's
        /// backdrop) replaced Wheatfield_background.png here per a gameplay review — LevelData_01
        /// is MazeType.CornField, so this specifically ties the backdrop to the world it's actually
        /// set in, rather than a generic wheatfield. One SpriteRenderer, sorting-ordered below
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
            var sprite = Load(PauseBackground); // World1_Cornfield.png — see doc comment above
            if (sprite == null)
            {
                Debug.LogWarning($"[ArtWiringBuilder] {PauseBackground} not found — skipping gameplay backdrop.");
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
            // camera's view width. CameraFollow now derives orthographicSize purely from
            // CellScreenHeightFraction (aspect-independent — see that script's comment), so the
            // camera's view width scales directly with the device's aspect ratio; MaxSupportedAspect
            // is the widest landscape aspect worth planning for (e.g. an ultra-wide phone). Bumped
            // from 1.3x to 1.6x after a playtest screenshot still showed a sliver of the camera's
            // blue clear colour past the backdrop's edge — the maze's own world bounds also run a
            // half-cell (CellSize/2) past mazeWidth/Height*CellSize on every side (GridToWorld has
            // no offset, so a tile's sprite extends CellSize/2 beyond its own grid coordinate in
            // each direction), which the old margin didn't explicitly account for.
            const float safetyMargin = 1.6f;
            float orthoSize = TileMapRenderer.CellSize / (2f * CameraFollow.CellScreenHeightFraction);
            float targetCameraViewWidth = 2f * orthoSize * CameraFollow.MaxSupportedAspect;
            float requiredWidth = (Mathf.Max(mazeWorldWidth, targetCameraViewWidth) + TileMapRenderer.CellSize) * safetyMargin;
            float requiredHeight = (mazeWorldHeight + TileMapRenderer.CellSize) * safetyMargin;

            float imageAspect = sprite.rect.width / sprite.rect.height; // width/height, e.g. ~1.77
            float widthUnits = Mathf.Max(requiredWidth, requiredHeight * imageAspect);
            float heightUnits = widthUnits / imageAspect;
            backdropGO.transform.localScale = new Vector3(widthUnits, heightUnits, 1f);
            backdropGO.transform.position = new Vector3(centerX, centerY, 0f);

            // The above only sets a static Editor-time preview (sized against LevelData_01, which
            // is always loaded in the Editor at wiring time) — TileMapRenderer.ApplyBackdrop is
            // what actually swaps this sprite per-level at runtime, keyed by MazeType, so it also
            // needs both worlds' backdrop sprites registered on its MazeArtSets. VegatableGarden.png
            // (World 2/VegPatch) is wired here alongside World1_Cornfield.png (World 1/CornField)
            // rather than needing its own method, since both are simple "look up TileMapRenderer,
            // call SetBackdropSprite" calls.
            var tileMapRenderer = GameObject.Find("GameManagers")?.GetComponent<TileMapRenderer>();
            if (tileMapRenderer != null)
            {
                tileMapRenderer.SetBackdropSprite(MazeType.CornField, sprite);
                var vegPatchBackdrop = Load(VegetableGardenBackdrop);
                if (vegPatchBackdrop != null)
                {
                    tileMapRenderer.SetBackdropSprite(MazeType.VegPatch, vegPatchBackdrop);
                }
                else
                {
                    Debug.LogWarning($"[ArtWiringBuilder] {VegetableGardenBackdrop} not found — VegPatch levels will fall back to CornField's backdrop at runtime.");
                }
                EditorUtility.SetDirty(tileMapRenderer);
            }
            else
            {
                Debug.LogWarning("[ArtWiringBuilder] Could not find TileMapRenderer on GameManagers to register per-world backdrop sprites.");
            }

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

        private static void WireBackgrounds()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var canvasTransform = GameObject.Find("Canvas")?.transform;
            if (canvasTransform == null)
            {
                Debug.LogWarning("[ArtWiringBuilder] Could not find Canvas — skipping background/coin wiring.");
                return;
            }

            SetScreenBackground(canvasTransform, "MainMenuScreen", Load(LandingBackground));

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

        // ---- New characters (Bessie, Woolly, Percy, Ducky) -------------------------------------

        /// <summary>Fixed order [Up0,Up1,Down0,Down1,Left0,Left1,Right0,Right1] per
        /// CharacterAnimator. Only one pose per direction exists (no walk-cycle frames yet), so
        /// each direction's two slots repeat the same sprite. Right reuses Left; CharacterAnimator
        /// flips it horizontally at runtime since there's no dedicated Right art. If left is null
        /// (e.g. Gerald has no profile art yet), front is used for Left/Right too — direction won't
        /// read correctly for those two facings until profile art is added, but the character is
        /// never left as a bare colour square.</summary>
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
            WireBessie();
            WireWoolly();
            WirePercy();

            WireDucky();
            WireHorace();
            WireGerald();
            WireBilly();
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
            // Scout now has real Back art too (previously front/left/right only, Up fell back to front).
            WireRobotVisual(ScoutPrefabPath, scoutFront, Load(ScoutBack), Load(ScoutLeft), Load(ScoutRight));
            SetRobotPortrait("Scout", scoutFront);

            var patrolFront = Load(PatrolFront);
            WireRobotVisual(PatrolPrefabPath, patrolFront, Load(PatrolBack), Load(PatrolLeft), Load(PatrolRight));
            SetRobotPortrait("Patrol", patrolFront);

            var heavyFront = Load(HeavyFront);
            WireRobotVisual(HeavyPrefabPath, heavyFront, Load(HeavyBack), null, null);
            SetRobotPortrait("Heavy", heavyFront);

            var drifterFront = Load(DrifterFront);
            // Drifter now has real Back art too (previously front/left/right only, Up fell back to front).
            WireRobotVisual(DrifterPrefabPath, drifterFront, Load(DrifterBack), Load(DrifterLeft), Load(DrifterRight));
            SetRobotPortrait("Drifter", drifterFront);

            // Drone's art is a single symmetric hovering-quadcopter face with no directional
            // cues to speak of — one sprite covers every facing (front/back/left/right all fall
            // back to it via RobotVisual's own null-handling), unlike every other robot's
            // per-direction art.
            var droneFront = Load(DroneFront);
            WireRobotVisual(DronePrefabPath, droneFront, null, null, null);
            SetRobotPortrait("Drone", droneFront);
        }

        // ---- Ability effects ---------------------------------------------------------------

        private static void WireAbilityEffects()
        {
            SetPrefabSprite(ShockwavePrefabPath, Load(BessieSlam));
            SetPrefabSprite(BounceTrailPrefabPath, Load(PercyEffect));
            SetPrefabSprite(WoollyClonePrefabPath, Load(WoollyEffect));
            SetPrefabLeftRightSprites(DuckySplashPrefabPath, Load(DuckyAbilityLeft), Load(DuckyAbilityRight));
            SetPrefabLeftRightSprites(HoraceBuckPrefabPath, Load(HoraceAbilityBuckLeft), Load(HoraceAbilityBuckRight));
        }

        /// <summary>Like SetPrefabSprite, but for DuckySplashEffect/HoraceBuckEffect's own
        /// leftSprite/rightSprite fields (chosen at runtime by direction) rather than a single
        /// SpriteRenderer.sprite — the prefab's SpriteRenderer itself is left without a sprite
        /// until PlayForDirection sets one at spawn time.</summary>
        private static void SetPrefabLeftRightSprites(string prefabPath, Sprite left, Sprite right)
        {
            if ((left == null && right == null) || !File.Exists(prefabPath))
            {
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            Component effect = contents.GetComponent<DuckySplashEffect>();
            if (effect == null)
            {
                effect = contents.GetComponent<HoraceBuckEffect>();
            }
            if (effect == null)
            {
                Debug.LogWarning($"[ArtWiringBuilder] {prefabPath} has neither DuckySplashEffect nor HoraceBuckEffect — skipping.");
                PrefabUtility.UnloadPrefabContents(contents);
                return;
            }

            var so = new SerializedObject(effect);
            if (left != null) so.FindProperty("leftSprite").objectReferenceValue = left;
            if (right != null) so.FindProperty("rightSprite").objectReferenceValue = right;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
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

            // Root is now World1_Cornfield.png (same backdrop as Pause/Choose Character/Settings'
            // sibling screens); PanelArt (a child, not the root) carries LevelComplete.png as an
            // aspect-locked square — see BuildLevelComplete's doc comment, same reasoning as Pause's
            // own PanelArt fix (a square card stretched full-screen distorts).
            SetScreenBackground(canvasTransform, "LevelCompleteScreen", Load(PauseBackground));
            SetImageSprite(canvasTransform, "LevelCompleteScreen/PanelArt", Load(LevelCompletePanel));
            SetImageSprite(canvasTransform, "LevelCompleteScreen/LogoImage", Load(LogoImage));
            // New Character Unlock overlay — rebuilt to its own Canva mockup (full-screen backdrop,
            // Logo, "Unlocked" wood-sign banner, large centred character card — see
            // BuildLevelComplete's doc comment for why no separate name/title/stats text exists).
            SetScreenBackground(canvasTransform, "LevelCompleteScreen/NewCharacterUnlockOverlay", Load(PauseBackground));
            SetImageSprite(canvasTransform, "LevelCompleteScreen/NewCharacterUnlockOverlay/LogoImage", Load(LogoImage));
            SetImageSprite(canvasTransform, "LevelCompleteScreen/NewCharacterUnlockOverlay/UnlockedBanner", Load(UnlockedBannerSprite));
            SetImageSprite(canvasTransform, "LevelCompleteScreen/NewWorldUnlockOverlay/WorldUnlockedBanner", Load(WorldUnlockedBannerSprite));
            // Rebuilt to a 2026-08-01 mockup — Bg_LevelSelect.png (night farm) root background, with
            // LevelFailed.png moved onto an aspect-locked PanelArt child (see BuildLevelFailed's own
            // doc comment for why the old full-screen stretch distorted the square card art).
            SetScreenBackground(canvasTransform, "LevelFailedScreen", Load(BgLevelSelect));
            SetImageSprite(canvasTransform, "LevelFailedScreen/PanelArt", Load(LevelFailedPanel));
            // Root Image is now World1_Cornfield.png (opaque backdrop), not a plain black dim — see
            // BuildPauseMenu's doc comment on the 2026-07-31 mockup dropping "dim gameplay behind
            // it" in favour of a dedicated background, same as Settings/Level Select got.
            SetScreenBackground(canvasTransform, "PauseOverlay", Load(PauseBackground));
            // PanelArt (a child of the root, not the root itself) carries Paused.png — see
            // BuildPauseMenu's comment on why the square art needs its own aspect-locked child
            // instead of stretching to fill the full-screen overlay.
            SetImageSprite(canvasTransform, "PauseOverlay/PanelArt", Load(PausedPanel));
            SetImageSprite(canvasTransform, "PauseOverlay/LogoImage", Load(LogoImage));
            // SettingsOverlay switched to Bg_LevelSelect.png (moon/windmill/barn) to match its own
            // 2026-07-31 Canva mockup exactly. ChooseCharacterScreen switched to World1_Cornfield.png
            // (same backdrop as Pause) per its own mockup — LoadingScreen Background.png is no longer
            // used by either.
            SetScreenBackground(canvasTransform, "SettingsOverlay", Load(BgLevelSelect));
            SetScreenBackground(canvasTransform, "ChooseCharacterScreen", Load(PauseBackground));
            SetImageSprite(canvasTransform, "SettingsOverlay/LogoImage", Load(LogoImage));
            SetImageSprite(canvasTransform, "ChooseCharacterScreen/LogoImage", Load(LogoImage));
            // LevelSelectScreen deliberately has no LogoImage — see BuildLevelSelect's doc comment;
            // it clashes with CurrentWorldIndicator at the same top-left inset.

            // New Character Unlock's card and RosterCard are deliberately unframed — there is no
            // dedicated card-frame art for them (a generic "Card.png" concept was considered and
            // dropped; do not reintroduce a CardFrame constant/wiring call here).
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
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
            // Sliced is a no-op visually for sprites with no configured border (behaves exactly
            // like Simple), so this is safe to apply blanket rather than special-casing it only
            // for Btn_plaque — see the border comment in ConfigureSpriteImporters.
            image.type = Image.Type.Sliced;
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
            var home = Load(BtnHome);
            var back = Load(BtnBack);
            var plaque = Load(BtnPlaque);

            SetImageSprite(canvasTransform, "MainMenuScreen/PlayButton", play);
            SetImageSprite(canvasTransform, "MainMenuScreen/SettingsButton", settings);

            SetImageSprite(canvasTransform, "GameplayScreen/PauseButton", pause);
            SetImageSprite(canvasTransform, "GameplayScreen/DPadUpButton", Load(DPadUp));
            SetImageSprite(canvasTransform, "GameplayScreen/DPadDownButton", Load(DPadDown));
            SetImageSprite(canvasTransform, "GameplayScreen/DPadLeftButton", Load(DPadLeft));
            SetImageSprite(canvasTransform, "GameplayScreen/DPadRightButton", Load(DPadRight));

            SetImageSprite(canvasTransform, "PauseOverlay/PanelArt/ResumeButton", Load(ResumeButtonArt));
            SetImageSprite(canvasTransform, "PauseOverlay/PanelArt/SwapButton", Load(SwapCharacterButtonArt));
            SetImageSprite(canvasTransform, "PauseOverlay/PanelArt/RestartButton", Load(RestartButtonArt));
            SetImageSprite(canvasTransform, "PauseOverlay/PanelArt/SettingsButton", Load(SettingsButtonArt));
            SetImageSprite(canvasTransform, "PauseOverlay/PanelArt/QuitButton", Load(QuitButtonArt));

            // Round Btn_home.png, not Btn_back.png — the mockup's back button on this screen is
            // the round barn/home icon, bottom-right, unlike every other screen's rectangular
            // Btn_back.png bottom-left (CreateGenericBackButton).
            SetImageSprite(canvasTransform, "SettingsOverlay/BackButton", home);

            // One Btn_plaque.png per grid cell (Phase5ProjectBuilder.CreateTogglePlaqueCell) —
            // matches the mockup's identical plaques exactly, 5 of the 6 grid slots filled.
            SetImageSprite(canvasTransform, "SettingsOverlay/SettingsGrid/MusicCell", plaque);
            SetImageSprite(canvasTransform, "SettingsOverlay/SettingsGrid/SfxCell", plaque);
            SetImageSprite(canvasTransform, "SettingsOverlay/SettingsGrid/VibrationCell", plaque);
            SetImageSprite(canvasTransform, "SettingsOverlay/SettingsGrid/LeftHandedCell", plaque);
            SetImageSprite(canvasTransform, "SettingsOverlay/SettingsGrid/LanguageCell", plaque);

            SetImageSprite(canvasTransform, "StoreComingSoonOverlay/Content/CloseButton", back);

            // Replay/Next Level/Home were removed per the 2026-07-31 mockup in favor of a single
            // Skip button, later replaced again with this Play/Home/Settings row — see
            // LevelCompleteController's doc comment for what each does. PlayButton was later split
            // out of ActionButtons to stand alone bottom-left (a device-frame review moved it
            // off the Home/Settings pair) — this path wasn't updated at the time, leaving it on
            // its placeholder color despite the button existing and working functionally.
            SetImageSprite(canvasTransform, "LevelCompleteScreen/PlayButton", play);
            SetImageSprite(canvasTransform, "LevelCompleteScreen/ActionButtons/HomeButton", home);
            SetImageSprite(canvasTransform, "LevelCompleteScreen/ActionButtons/SettingsButton", settings);
            SetImageSprite(canvasTransform, "LevelCompleteScreen/NewCharacterUnlockOverlay/UnlockContent/ContinueButton", play);

            SetImageSprite(canvasTransform, "LevelFailedScreen/PanelArt/RestartButton", Load(RestartButtonArt));
            SetImageSprite(canvasTransform, "LevelFailedScreen/PanelArt/QuitButton", Load(QuitButtonArt));

            SetImageSprite(canvasTransform, "CharacterRosterScreen/BackButton", back);
            SetImageSprite(canvasTransform, "LeaderboardsScreen/BackButton", back);
            // The Choose Character mockup's round back icon is a triangle/mountain glyph with no
            // matching uploaded asset — substituted with Btn_back.png (it navigates back to Pause,
            // not to a home/landing destination, so Btn_back reads correctly here, unlike Settings/
            // Level Select's Btn_home, which actually is a "go home" action).
            SetImageSprite(canvasTransform, "ChooseCharacterScreen/BackButton", back);
            // Round Btn_home.png, not Btn_back.png — same mockup-driven deviation as Settings (see
            // CreateRoundBackButton's doc comment); Level Select's back button is bottom-right.
            SetImageSprite(canvasTransform, "LevelSelectScreen/BackButton", home);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        /// <summary>Wires the Revive prompt's panel art + Yes/No buttons, the skip-cooldown
        /// button's icon, and the Gameplay HUD's coin balance chip — the monetisation surface
        /// reviewed with the user. Revive prompt art was replaced once already (see RevivePromptPanel/
        /// BtnRevive/BtnDecline's own comments for what changed and why).</summary>
        private static void WireMonetisationArt()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var canvasTransform = GameObject.Find("Canvas")?.transform;
            if (canvasTransform == null)
            {
                Debug.LogWarning("[ArtWiringBuilder] Could not find Canvas — skipping monetisation art wiring.");
                return;
            }

            SetImageSprite(canvasTransform, "GameplayScreen/RevivePromptOverlay/PanelArt", Load(RevivePromptPanel));
            SetImageSprite(canvasTransform, "GameplayScreen/RevivePromptOverlay/PanelArt/Content/ReviveButton", Load(BtnRevive));
            SetImageSprite(canvasTransform, "GameplayScreen/RevivePromptOverlay/PanelArt/Content/WatchAdButton", Load(BtnWatchAd));
            SetImageSprite(canvasTransform, "GameplayScreen/RevivePromptOverlay/PanelArt/Content/DeclineButton", Load(BtnDecline));

            SetImageSprite(canvasTransform, "GameplayScreen/SkipCooldownButton", Load(BtnSkipCooldown));

            SetImageSprite(canvasTransform, "GameplayScreen/CoinBalanceChip", Load(CoinBalanceChip));

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        // ---- Level Select ---------------------------------------------------------------------

        /// <summary>Wires the LevelSelectScreen background, the (unused, kept-built) WorldDivider
        /// prefab's banner, the 5 LevelTile.prefab state sprites, the SELECT LEVEL title sign, the 4
        /// world badge sprites (LevelSelectController.worldSignSprites), and the back button's two
        /// toggle sprites (Btn_home for world-select, Btn_back for a world's tile grid — see
        /// LevelSelectController.SetBackButtonSprite). The button's initial sprite (Btn_home, not
        /// Btn_back — see CreateRoundBackButton) is still set by WireButtons, same as every other
        /// screen's back button.</summary>
        private static void WireLevelSelect()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var canvasTransform = GameObject.Find("Canvas")?.transform;
            if (canvasTransform == null)
            {
                Debug.LogWarning("[ArtWiringBuilder] Could not find Canvas — skipping Level Select wiring.");
                return;
            }

            SetScreenBackground(canvasTransform, "LevelSelectScreen", Load(BgLevelSelect));

            // Header has no background art at all per the 2026-07-31 Canva mockup — it's Color.clear
            // in Phase5ProjectBuilder.BuildLevelSelect, just a layout strip for TitleImage/
            // StarCounter, so the night-sky Bg_LevelSelect.png shows straight through it.

            // Divider goes through Simple + preserveAspect here rather than the shared
            // SetImageSprite helper (which forces Type.Sliced) — Sliced ignores preserveAspect
            // entirely, so a banner whose own aspect ratio doesn't match its fixed-size container
            // (WorldDivider: 1920x250) got squashed/stretched into that shape instead of keeping its
            // own proportions. Simple + preserveAspect letterboxes instead of distorting, regardless
            // of the art's actual resolution.
            var dividerBanner = Load(DividerWorldBanner);
            if (dividerBanner != null && File.Exists(WorldDividerPrefabPath))
            {
                var dividerContents = PrefabUtility.LoadPrefabContents(WorldDividerPrefabPath);
                var dividerImage = dividerContents.GetComponent<Image>();
                if (dividerImage != null)
                {
                    dividerImage.sprite = dividerBanner;
                    dividerImage.type = Image.Type.Simple;
                    dividerImage.preserveAspect = true;
                }
                PrefabUtility.SaveAsPrefabAsset(dividerContents, WorldDividerPrefabPath);
                PrefabUtility.UnloadPrefabContents(dividerContents);
            }

            if (File.Exists(LevelTilePrefabPath))
            {
                var tileContents = PrefabUtility.LoadPrefabContents(LevelTilePrefabPath);
                var tileController = tileContents.GetComponent<LevelTileController>();
                if (tileController != null)
                {
                    var so = new SerializedObject(tileController);
                    so.FindProperty("spriteLocked").objectReferenceValue = Load(LevelTileLocked);
                    so.FindProperty("spriteUnlocked").objectReferenceValue = Load(LevelTileUnlocked);
                    so.FindProperty("sprite1Star").objectReferenceValue = Load(LevelTile1Star);
                    so.FindProperty("sprite2Stars").objectReferenceValue = Load(LevelTile2Stars);
                    so.FindProperty("sprite3Stars").objectReferenceValue = Load(LevelTile3Stars);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(tileContents, LevelTilePrefabPath);
                PrefabUtility.UnloadPrefabContents(tileContents);
            }

            // "SELECT LEVEL" word-art replacing the old TMP title text. TitleImage moved out of
            // Header onto LevelSelectScreen's root directly (to match Settings' title sizing/
            // position) — this path must track that or the sprite silently fails to wire.
            var titleImage = canvasTransform.Find("LevelSelectScreen/TitleImage")?.GetComponent<Image>();
            var titleSprite = Load(SelectLevelText);
            if (titleImage != null && titleSprite != null)
            {
                titleImage.sprite = titleSprite;
                titleImage.type = Image.Type.Simple;
                titleImage.preserveAspect = true;
            }

            // Complete world badges (shield + rope + name text already baked into one sprite each),
            // indexed to match UnlockProgression.GetWorldNameForLevel's own world-number mapping —
            // used directly as both the world-select carousel's WorldShield instances and the small
            // CurrentWorldIndicator badge (see LevelSelectController.ShowWorldSelect/RevealWorld/
            // SetWorldSignSprite). No separate background sprite needed anymore — the old
            // Divider_WorldBanner.png composition this replaced is gone.
            var levelSelectController = canvasTransform.Find("LevelSelectScreen")?.GetComponent<LevelSelectController>();
            if (levelSelectController != null)
            {
                var worldSigns = new[] { Load(CornFieldText), Load(VegetablePatchText), Load(OrchardText), Load(WheatfieldText) };
                var lsSo = new SerializedObject(levelSelectController);
                var arrayProp = lsSo.FindProperty("worldSignSprites");
                arrayProp.arraySize = worldSigns.Length;
                for (int i = 0; i < worldSigns.Length; i++)
                {
                    arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = worldSigns[i];
                }

                // BackButton icon swaps between these two depending on state (world-select vs a
                // world's tile grid) — see LevelSelectController.SetBackButtonSprite. WireButtons
                // sets the Image's initial sprite (home, matching the world-select state Level
                // Select opens into); these are the two states it can toggle between afterward.
                lsSo.FindProperty("backButtonWorldSelectSprite").objectReferenceValue = Load(BtnHome);
                lsSo.FindProperty("backButtonTileGridSprite").objectReferenceValue = Load(BtnBack);
                lsSo.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        // ---- Audio --------------------------------------------------------------------------------

        /// <summary>Wires background music + the power-active music swap + all 5 SFX clips onto
        /// AudioManager (on GameManagers). AudioManager.Start() plays the background music clip
        /// immediately and loops it for the life of the app; PowerPelletManager crossfades to
        /// eatRobotMusicClip for the duration of a power pellet's effect and back afterward
        /// (PlayEatRobotMusic/ResumeBackgroundMusic). The SFX clips are played by their respective
        /// gameplay triggers via AudioManager.PlayAnimalDeathSfx/PlayCornPickupSfx/
        /// PlayCoinPickupSfx/PlayPowerReadySfx/PlayRarePelletPickupSfx/PlayRobotRespawnSfx (see
        /// PlayerHealth, CropCollector, PowerPelletManager, RobotBase respectively). AudioClip import settings
        /// are left at Unity's defaults (no ConfigureSpriteImporters-style pass needed here, unlike
        /// sprites) since the default compressed-in-memory settings are already reasonable for
        /// these short clips and the two looping music tracks.</summary>
        private static void WireAudio()
        {
            var musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(BackgroundMusicClip);
            var animalDeath = AssetDatabase.LoadAssetAtPath<AudioClip>(AnimalDeathSfx);
            var cornPickup = AssetDatabase.LoadAssetAtPath<AudioClip>(CornPickupSfx);
            var coinPickup = AssetDatabase.LoadAssetAtPath<AudioClip>(CoinPickupSfx);
            var powerReady = AssetDatabase.LoadAssetAtPath<AudioClip>(PowerReadySfx);
            var rarePelletPickup = AssetDatabase.LoadAssetAtPath<AudioClip>(RarePelletPickupSfx);
            var robotSpawn = AssetDatabase.LoadAssetAtPath<AudioClip>(RobotSpawnSfx);
            var eatRobotMusic = AssetDatabase.LoadAssetAtPath<AudioClip>(EatRobotMusicClip);
            var landingMusic = AssetDatabase.LoadAssetAtPath<AudioClip>(LandingMusicClip);

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
            if (coinPickup != null) so.FindProperty("coinPickupClip").objectReferenceValue = coinPickup;
            if (powerReady != null) so.FindProperty("powerReadyClip").objectReferenceValue = powerReady;
            if (rarePelletPickup != null) so.FindProperty("rarePelletPickupClip").objectReferenceValue = rarePelletPickup;
            if (robotSpawn != null) so.FindProperty("robotRespawnClip").objectReferenceValue = robotSpawn;
            if (eatRobotMusic != null) so.FindProperty("eatRobotMusicClip").objectReferenceValue = eatRobotMusic;
            if (landingMusic != null) so.FindProperty("landingMusicClip").objectReferenceValue = landingMusic;
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

        /// <summary>Settings text was left on TMP's default LiberationSans SDF font (see the TMP
        /// bootstrap section in CLAUDE.md) — every other screen's headline/button text uses the
        /// same Bangers cartoon font as Gameplay HUD's score/timer, so Settings read as visually
        /// inconsistent with the rest of the game.</summary>
        private static void WireSettingsFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BangersFontPath);
            if (font == null)
            {
                Debug.LogWarning($"[ArtWiringBuilder] {BangersFontPath} not found — skipping Settings font wiring.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);
            var canvasTransform = GameObject.Find("Canvas")?.transform;
            if (canvasTransform == null)
            {
                Debug.LogWarning("[ArtWiringBuilder] Could not find Canvas — skipping Settings font wiring.");
                return;
            }

            // Title is SettingsSign.png word-art now (see WireSettingsSign), not TMP text, so it's
            // no longer in this font list. Rebuilt as a 2x3 grid of whole-plaque toggle cells
            // (Phase5ProjectBuilder.CreateTogglePlaqueCell) per the 2026-07-31 Canva mockup —
            // paths below match that hierarchy, not the old Content/*Row_Plaque stack.
            SetFont(canvasTransform, "SettingsOverlay/SettingsGrid/MusicCell/MusicCell_Label", font);
            SetFont(canvasTransform, "SettingsOverlay/SettingsGrid/SfxCell/SfxCell_Label", font);
            SetFont(canvasTransform, "SettingsOverlay/SettingsGrid/VibrationCell/VibrationCell_Label", font);
            SetFont(canvasTransform, "SettingsOverlay/SettingsGrid/LeftHandedCell/LeftHandedCell_Label", font);
            SetFont(canvasTransform, "SettingsOverlay/SettingsGrid/LanguageCell/Label", font);
            SetFont(canvasTransform, "SettingsOverlay/VersionText", font);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        /// <summary>SettingsSign.png replaces the old TMP "SETTINGS" title text — same pattern as
        /// Level Select's SelectLevelSign.png (see WireLevelSelect).</summary>
        private static void WireSettingsSign()
        {
            var sprite = Load(SettingsSignText);
            if (sprite == null)
            {
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);
            var canvasTransform = GameObject.Find("Canvas")?.transform;
            var titleImage = canvasTransform != null ? canvasTransform.Find("SettingsOverlay/TitleImage")?.GetComponent<Image>() : null;
            if (titleImage == null)
            {
                Debug.LogWarning("[ArtWiringBuilder] Could not find SettingsOverlay/TitleImage — skipping SettingsSign wiring.");
                return;
            }

            titleImage.sprite = sprite;
            titleImage.type = Image.Type.Simple;
            titleImage.preserveAspect = true;

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
