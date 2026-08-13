using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;
using FarmFuryArcade.Utilities;
using Object = UnityEngine.Object;

namespace FarmFuryArcade.EditorTools
{
    /// <summary>
    /// Phase 2 scaffolding: builds all placeholder prefabs, regenerates LevelData_01 as a full
    /// procedural 28x31 maze (tile-id driven per the GDD's convention), creates CharacterData_Cluck,
    /// and rewires the existing Game.unity (built by Phase1ProjectBuilder) with the new
    /// TileMapRenderer/ScoreManager/InputController components. Safe to re-run.
    /// </summary>
    public static class Phase2ProjectBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string LevelDataPath = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_01.asset";
        private const string LevelData02Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_02.asset";
        private const string LevelData03Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_03.asset";
        private const string LevelData04Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_04.asset";
        // LevelData_05.asset / levelNumber 4 used to be permanently occupied by Phase3ProjectBuilder's
        // 20x20 multi-robot test maze, which was never meant to be player-reachable ("not part of the
        // level-select flow yet" per its own doc comment) but leaked into World 1's real 25-level
        // sequence anyway — DataManager keys LevelData purely by levelNumber, and
        // UnlockProgression/LevelSelectController have no separate "is this a real level" concept, so
        // any LevelData occupying a 0-24/25-49 slot is player-reachable by construction. Tapping tile
        // 5 loaded that mostly-open 20x20 test field instead of a real "Corn Field - 05" maze — read
        // as "blank and without walls" compared to every other level. Fixed by giving the test maze
        // its own file (Phase3ProjectBuilder.LevelDataRobotTestPath -> LevelData_RobotTest.asset) and an
        // out-of-range levelNumber (-1, invisible to DataManager.GetAllLevelData's 0-99 consumers),
        // freeing LevelData_05.asset/levelNumber 4 for BuildLevelData05 below — a real, verified
        // (connected, no open-2x2-block) 12x9 maze, algorithmically generated the same way as
        // LevelData_09 onward. LevelData_09 onward are algorithmically generated (recursive-
        // backtracker + extra loop edges on a half-density cell grid, which provably can't produce
        // the open-2x2-block failure mode two earlier hand-tuned procedural attempts hit — see
        // BuildLevel's doc comment) to fill out the full 25-level World 1 set
        // (UnlockProgression.LevelsPerWorld) without hand-authoring every one via the maze designer.
        private const string LevelData05Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_05.asset";
        private const string LevelData06Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_06.asset";
        private const string LevelData07Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_07.asset";
        private const string LevelData08Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_08.asset";
        private const string LevelData09Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_09.asset";
        private const string LevelData10Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_10.asset";
        private const string LevelData11Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_11.asset";
        private const string LevelData12Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_12.asset";
        private const string LevelData13Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_13.asset";
        private const string LevelData14Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_14.asset";
        private const string LevelData15Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_15.asset";
        private const string LevelData16Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_16.asset";
        private const string LevelData17Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_17.asset";
        private const string LevelData18Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_18.asset";
        private const string LevelData19Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_19.asset";
        private const string LevelData20Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_20.asset";
        private const string LevelData21Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_21.asset";
        private const string LevelData22Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_22.asset";
        private const string LevelData23Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_23.asset";
        private const string LevelData24Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_24.asset";
        private const string LevelData25Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_25.asset";
        // World 2 (VegPatch) — continues levelNumber sequentially after World 1's 25 (0-24), so
        // World 2 occupies levelNumber 25-49 / LevelData_26 through LevelData_50, matching
        // UnlockProgression.LevelsPerWorld's 25-per-world convention. Algorithmically generated
        // the same way as World 1's LevelData_09-25 — see BuildLevel's doc comment.
        private const string LevelData26Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_26.asset";
        private const string LevelData27Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_27.asset";
        private const string LevelData28Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_28.asset";
        private const string LevelData29Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_29.asset";
        private const string LevelData30Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_30.asset";
        private const string LevelData31Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_31.asset";
        private const string LevelData32Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_32.asset";
        private const string LevelData33Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_33.asset";
        private const string LevelData34Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_34.asset";
        private const string LevelData35Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_35.asset";
        private const string LevelData36Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_36.asset";
        private const string LevelData37Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_37.asset";
        private const string LevelData38Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_38.asset";
        private const string LevelData39Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_39.asset";
        private const string LevelData40Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_40.asset";
        private const string LevelData41Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_41.asset";
        private const string LevelData42Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_42.asset";
        private const string LevelData43Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_43.asset";
        private const string LevelData44Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_44.asset";
        private const string LevelData45Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_45.asset";
        private const string LevelData46Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_46.asset";
        private const string LevelData47Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_47.asset";
        private const string LevelData48Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_48.asset";
        private const string LevelData49Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_49.asset";
        private const string LevelData50Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_50.asset";
        // World 3 (Orchard) — continues levelNumber sequentially after World 2's 50 (25-49), so
        // World 3 occupies levelNumber 50-74 / LevelData_51 through LevelData_75, matching
        // UnlockProgression.LevelsPerWorld's 25-per-world convention. See BuildLevelData51's doc
        // comment (near BuildLevelData50) for how these were generated.
        private const string LevelData51Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_51.asset";
        private const string LevelData52Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_52.asset";
        private const string LevelData53Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_53.asset";
        private const string LevelData54Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_54.asset";
        private const string LevelData55Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_55.asset";
        private const string LevelData56Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_56.asset";
        private const string LevelData57Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_57.asset";
        private const string LevelData58Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_58.asset";
        private const string LevelData59Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_59.asset";
        private const string LevelData60Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_60.asset";
        private const string LevelData61Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_61.asset";
        private const string LevelData62Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_62.asset";
        private const string LevelData63Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_63.asset";
        private const string LevelData64Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_64.asset";
        private const string LevelData65Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_65.asset";
        private const string LevelData66Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_66.asset";
        private const string LevelData67Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_67.asset";
        private const string LevelData68Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_68.asset";
        private const string LevelData69Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_69.asset";
        private const string LevelData70Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_70.asset";
        private const string LevelData71Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_71.asset";
        private const string LevelData72Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_72.asset";
        private const string LevelData73Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_73.asset";
        private const string LevelData74Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_74.asset";
        private const string LevelData75Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_75.asset";
        // World 4 (Wheat) — continues levelNumber sequentially after World 3's 75 (50-74), so
        // World 4 occupies levelNumber 75-99 / LevelData_76 through LevelData_100 — the last world,
        // matching UnlockProgression.TotalLevels' 100-level cap exactly.
        private const string LevelData76Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_76.asset";
        private const string LevelData77Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_77.asset";
        private const string LevelData78Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_78.asset";
        private const string LevelData79Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_79.asset";
        private const string LevelData80Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_80.asset";
        private const string LevelData81Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_81.asset";
        private const string LevelData82Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_82.asset";
        private const string LevelData83Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_83.asset";
        private const string LevelData84Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_84.asset";
        private const string LevelData85Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_85.asset";
        private const string LevelData86Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_86.asset";
        private const string LevelData87Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_87.asset";
        private const string LevelData88Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_88.asset";
        private const string LevelData89Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_89.asset";
        private const string LevelData90Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_90.asset";
        private const string LevelData91Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_91.asset";
        private const string LevelData92Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_92.asset";
        private const string LevelData93Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_93.asset";
        private const string LevelData94Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_94.asset";
        private const string LevelData95Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_95.asset";
        private const string LevelData96Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_96.asset";
        private const string LevelData97Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_97.asset";
        private const string LevelData98Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_98.asset";
        private const string LevelData99Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_99.asset";
        private const string LevelData100Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_100.asset";
        private const string CharacterDataPath = "Assets/_Project/ScriptableObjects/Resources/Characters/CharacterData_Cluck.asset";
        private const string CharacterPrefabFolder = "Assets/_Project/Prefabs/Characters";
        private const string BlockPrefabFolder = "Assets/_Project/Prefabs/Blocks";

        [MenuItem("Farm Fury Arcade/Phase 2/Build All")]
        public static void BuildAll()
        {
            GameObject wallPrefab = BuildWallPrefab("Wall_CornField", new Color(0.29f, 0.17f, 0.10f)); // GDD Wall Brown #4A2C1A
            GameObject groundPrefab = BuildGroundPrefab("Ground_CornField", new Color(0.18f, 0.12f, 0.08f)); // dark soil, visual only
            GameObject cropKernelPrefab = BuildCropPrefab("Crop_Corn", CropType.Corn, 10, new Color(0.96f, 0.78f, 0.26f), 0.35f);
            GameObject cropVegetablePrefab = BuildCropPrefab("Crop_Vegetable", CropType.Vegetable, 50, new Color(0.30f, 0.69f, 0.31f), 0.5f);
            GameObject pelletCollectEffectPrefab = BuildPelletCollectEffectPrefab();
            GameObject powerPelletPrefab = BuildPowerPelletPrefab(pelletCollectEffectPrefab);
            GameObject warpTunnelPrefab = BuildWarpTunnelPrefab("WarpTunnel", new Color(0.55f, 0.27f, 0.68f)); // placeholder "barn door" purple
            GameObject cluckPrefab = BuildCluckPrefab();

            // World 2 (VegPatch) wall/warp-tunnel prefabs — ground reuses Ground_CornField (no
            // dedicated VegPatch ground art has been uploaded yet; soil reads fine for a vegetable
            // patch too) per TileMapRenderer.MazeArtSet's doc comment. Placeholder colors only
            // until ArtWiringBuilder.WireMazeTiles sets the real VegTile.png/VeggiePatchWarp.png
            // sprites.
            GameObject wallPrefabVegPatch = BuildWallPrefab("Wall_VegPatch", new Color(0.24f, 0.42f, 0.20f));
            GameObject warpTunnelPrefabVegPatch = BuildWarpTunnelPrefab("WarpTunnel_VegPatch", new Color(0.55f, 0.27f, 0.68f));

            // World 2 (VegPatch) crop prefabs — carrot.png takes over as the kernel-tier crop
            // (World 1's kernel-tier prefab, Crop_Corn, keeps CornKernel.png), cabbage.png as the
            // vegetable-tier crop. Placeholder colors only until ArtWiringBuilder sets the real
            // sprites.
            GameObject cropKernelPrefabVegPatch = BuildCropPrefab("Crop_Kernel_VegPatch", CropType.Corn, 10, new Color(0.85f, 0.45f, 0.15f), 0.35f);
            GameObject cropVegetablePrefabVegPatch = BuildCropPrefab("Crop_Vegetable_VegPatch", CropType.Vegetable, 50, new Color(0.35f, 0.6f, 0.25f), 0.5f);

            // World 1's bonus coin pickup — scattered on top of already-rendered tiles by
            // TileMapRenderer.SpawnBonusPickups, not part of the maze grid itself. See
            // TileMapRenderer.MazeArtSet.bonusPickupPrefab's doc comment for why it's excluded from
            // LevelData.totalCropsRequired.
            GameObject coinPrefab = BuildCoinPrefab();

            // World 3 (Orchard) wall/ground prefabs + bonus cherry pickup, and World 4 (Wheat)'s
            // own wall/ground prefabs + bonus grain-sack pickup — Wheat's wall/ground art landed
            // this session (WheatWallTile.png/WheatFloorTile.png), same "own dedicated tile
            // prefabs" treatment Orchard already got rather than sharing Ground_CornField.
            // Placeholder colors only until ArtWiringBuilder sets the real sprites; the MazeArtSet
            // entries themselves are added additively by ArtWiringBuilder (TileMapRenderer.
            // GetOrAddArtSet) via these prefabs' own asset paths, not threaded through this
            // method's WireScene call, since neither world has dedicated warp/crop art of its own
            // yet (both reuse CornField's).
            BuildWallPrefab("Wall_Orchard", new Color(0.55f, 0.16f, 0.16f));
            BuildGroundPrefab("Ground_Orchard", new Color(0.42f, 0.27f, 0.14f));
            BuildBonusPickupPrefab("Pickup_Cherry", new Color(0.72f, 0.05f, 0.15f));
            BuildWallPrefab("Wall_Wheat", new Color(0.62f, 0.48f, 0.18f));
            BuildGroundPrefab("Ground_Wheat", new Color(0.35f, 0.24f, 0.12f));
            BuildBonusPickupPrefab("Pickup_GrainSack", new Color(0.68f, 0.52f, 0.25f));

            BuildCharacterData();
            BuildLevelData01();
            BuildLevelData02();
            BuildLevelData03();
            BuildLevelData04();
            BuildLevelData05();
            BuildLevelData06();
            BuildLevelData07();
            BuildLevelData08();
            BuildLevelData09();
            BuildLevelData10();
            BuildLevelData11();
            BuildLevelData12();
            BuildLevelData13();
            BuildLevelData14();
            BuildLevelData15();
            BuildLevelData16();
            BuildLevelData17();
            BuildLevelData18();
            BuildLevelData19();
            BuildLevelData20();
            BuildLevelData21();
            BuildLevelData22();
            BuildLevelData23();
            BuildLevelData24();
            BuildLevelData25();
            BuildLevelData26();
            BuildLevelData27();
            BuildLevelData28();
            BuildLevelData29();
            BuildLevelData30();
            BuildLevelData31();
            BuildLevelData32();
            BuildLevelData33();
            BuildLevelData34();
            BuildLevelData35();
            BuildLevelData36();
            BuildLevelData37();
            BuildLevelData38();
            BuildLevelData39();
            BuildLevelData40();
            BuildLevelData41();
            BuildLevelData42();
            BuildLevelData43();
            BuildLevelData44();
            BuildLevelData45();
            BuildLevelData46();
            BuildLevelData47();
            BuildLevelData48();
            BuildLevelData49();
            BuildLevelData50();
            BuildLevelData51();
            BuildLevelData52();
            BuildLevelData53();
            BuildLevelData54();
            BuildLevelData55();
            BuildLevelData56();
            BuildLevelData57();
            BuildLevelData58();
            BuildLevelData59();
            BuildLevelData60();
            BuildLevelData61();
            BuildLevelData62();
            BuildLevelData63();
            BuildLevelData64();
            BuildLevelData65();
            BuildLevelData66();
            BuildLevelData67();
            BuildLevelData68();
            BuildLevelData69();
            BuildLevelData70();
            BuildLevelData71();
            BuildLevelData72();
            BuildLevelData73();
            BuildLevelData74();
            BuildLevelData75();
            BuildLevelData76();
            BuildLevelData77();
            BuildLevelData78();
            BuildLevelData79();
            BuildLevelData80();
            BuildLevelData81();
            BuildLevelData82();
            BuildLevelData83();
            BuildLevelData84();
            BuildLevelData85();
            BuildLevelData86();
            BuildLevelData87();
            BuildLevelData88();
            BuildLevelData89();
            BuildLevelData90();
            BuildLevelData91();
            BuildLevelData92();
            BuildLevelData93();
            BuildLevelData94();
            BuildLevelData95();
            BuildLevelData96();
            BuildLevelData97();
            BuildLevelData98();
            BuildLevelData99();
            BuildLevelData100();

            WireScene(wallPrefab, groundPrefab, cropKernelPrefab, cropVegetablePrefab, powerPelletPrefab, warpTunnelPrefab, cluckPrefab,
                wallPrefabVegPatch, warpTunnelPrefabVegPatch, cropKernelPrefabVegPatch, cropVegetablePrefabVegPatch, coinPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Phase2ProjectBuilder] Phase 2 prefabs, LevelData_01 through LevelData_100 (all 4 worlds' full 25-level sets), CharacterData_Cluck, and Game.unity wiring complete.");
        }

        /// <summary>Generalized from a hardcoded "Wall_CornField" so World 2's Wall_VegPatch could
        /// reuse it — see BuildAll's VegPatch wiring below.</summary>
        private static GameObject BuildWallPrefab(string name, Color placeholderColor)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(placeholderColor);
            go.transform.localScale = Vector3.one * TileMapRenderer.CellSize;
            go.AddComponent<BoxCollider2D>();
            return SaveAndDestroy(go, BlockPrefabFolder + "/" + name + ".prefab");
        }

        /// <summary>Generalized from a hardcoded "Ground_CornField" so World 3's Ground_Orchard
        /// could reuse it — see BuildAll's Orchard wiring below. CornField/VegPatch keep sharing
        /// Ground_CornField (see TileMapRenderer.MazeArtSet's doc comment); Orchard gets its own
        /// dedicated ground prefab since real Orchard-specific ground art exists.</summary>
        private static GameObject BuildGroundPrefab(string name, Color placeholderColor)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(placeholderColor);
            sr.sortingOrder = -1;
            go.transform.localScale = Vector3.one * TileMapRenderer.CellSize;
            return SaveAndDestroy(go, BlockPrefabFolder + "/" + name + ".prefab");
        }

        private static GameObject BuildCropPrefab(string name, CropType cropType, int points, Color color, float scale)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(color);
            go.transform.localScale = Vector3.one * scale * TileMapRenderer.CellSize;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;
            var pickup = go.AddComponent<CropPickup>();
            pickup.cropType = cropType;
            pickup.points = points;
            return SaveAndDestroy(go, BlockPrefabFolder + "/" + name + ".prefab");
        }

        /// <summary>World 1's bonus coin — scattered by TileMapRenderer.SpawnBonusPickups on top of
        /// already-rendered tiles (not tied to any grid tile id), sortingOrder 3 so it renders above
        /// ground(-1)/crops(default 0) but below characters(5).</summary>
        private static GameObject BuildCoinPrefab()
        {
            var go = new GameObject("Pickup_Coin");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(new Color(1f, 0.85f, 0.2f)); // placeholder gold
            sr.sortingOrder = 3;
            go.transform.localScale = Vector3.one * 0.5f * TileMapRenderer.CellSize;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;
            go.AddComponent<CoinPickup>();
            return SaveAndDestroy(go, BlockPrefabFolder + "/Pickup_Coin.prefab");
        }

        /// <summary>Generalized from the hardcoded "Pickup_Coin" (BuildCoinPrefab) so World 3/4's
        /// bonus pickups (Pickup_Cherry, Pickup_GrainSack) can reuse it — same CoinPickup component
        /// (awards SaveManager coins directly, not maze score, and stays out of
        /// LevelData.totalCropsRequired) regardless of which world's bonus item it actually is.</summary>
        private static GameObject BuildBonusPickupPrefab(string name, Color placeholderColor)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(placeholderColor);
            sr.sortingOrder = 3;
            go.transform.localScale = Vector3.one * 0.5f * TileMapRenderer.CellSize;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;
            go.AddComponent<CoinPickup>();
            return SaveAndDestroy(go, BlockPrefabFolder + "/" + name + ".prefab");
        }

        private static GameObject BuildPowerPelletPrefab(GameObject collectEffectPrefab)
        {
            var go = new GameObject("Power_Sunflower");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(new Color(1f, 0.765f, 0f)); // GDD Power Sunflower #FFC300
            go.transform.localScale = Vector3.one * 0.7f * TileMapRenderer.CellSize;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;
            var pickup = go.AddComponent<PowerPelletPickup>();
            pickup.pelletType = PowerPelletType.Sunflower;
            pickup.points = 500;
            pickup.SetCollectEffectPrefab(collectEffectPrefab);
            return SaveAndDestroy(go, BlockPrefabFolder + "/Power_Sunflower.prefab");
        }

        /// <summary>No dedicated sparkle/particle art exists yet for rare-pellet collection (see
        /// CLAUDE.md "Art status") — PelletCollectBurst procedurally animates a small ring of
        /// placeholder-coloured squares instead. This prefab just carries that component; the
        /// visual rays are spawned as children at runtime by Configure().</summary>
        private static GameObject BuildPelletCollectEffectPrefab()
        {
            var go = new GameObject("PelletCollectBurst");
            go.AddComponent<PelletCollectBurst>();
            return SaveAndDestroy(go, BlockPrefabFolder + "/PelletCollectBurst.prefab");
        }

        /// <summary>Generalized from a hardcoded "WarpTunnel" so World 2's WarpTunnel_VegPatch
        /// could reuse it — see BuildAll's VegPatch wiring below.</summary>
        private static GameObject BuildWarpTunnelPrefab(string name, Color placeholderColor)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(placeholderColor);
            go.transform.localScale = Vector3.one * TileMapRenderer.CellSize;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = Vector2.one * 0.9f;
            go.AddComponent<WarpTunnel>();
            return SaveAndDestroy(go, BlockPrefabFolder + "/" + name + ".prefab");
        }

        private static GameObject BuildCluckPrefab()
        {
            var go = new GameObject("Cluck");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(new Color(1f, 0.843f, 0f)); // GDD Accent Gold #FFD700
            sr.sortingOrder = 5;
            go.transform.localScale = Vector3.one * TileMapRenderer.CellSize;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            // Kinematic bodies don't generate trigger callbacks against plain static colliders
            // (crops, power pellets, warp tunnels — none of which have a Rigidbody2D) unless
            // this is enabled.
            rb.useFullKinematicContacts = true;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.4f;
            go.AddComponent<GridMovement>();
            go.AddComponent<CropCollector>();
            go.AddComponent<CharacterAnimator>();
            return SaveAndDestroy(go, CharacterPrefabFolder + "/Cluck.prefab");
        }

        private static GameObject SaveAndDestroy(GameObject go, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // PlaceholderSprite.Get() creates a Sprite from an in-memory Texture2D that was never
            // written to disk as an asset — SaveAsPrefabAsset can't serialize a reference to a
            // non-asset object, so any SpriteRenderer still using one ends up with a NULL sprite
            // in the saved .prefab (invisible in-game). See Phase4ProjectBuilder's
            // EmbedRuntimePlaceholderSprites for the full story and the confirmed evidence
            // (Egg.prefab/WaterTile.prefab/Horace.prefab all shipped with m_Sprite: {fileID: 0}).
            var placeholderSprites = new List<(string transformPath, Sprite sprite)>();
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.sprite != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sr.sprite)))
                {
                    placeholderSprites.Add((AnimationUtility.CalculateTransformPath(sr.transform, go.transform), sr.sprite));
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

        private static void EmbedRuntimePlaceholderSprites(string prefabPath, List<(string transformPath, Sprite sprite)> placeholders)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            foreach (var (transformPath, sprite) in placeholders)
            {
                var target = string.IsNullOrEmpty(transformPath) ? contents.transform : contents.transform.Find(transformPath);
                var sr = target != null ? target.GetComponent<SpriteRenderer>() : null;
                if (sr == null)
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
                sr.sprite = sprite;
            }

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
        }

        private static void BuildCharacterData()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CharacterDataPath)!);
            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(CharacterDataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CharacterData>();
                AssetDatabase.CreateAsset(data, CharacterDataPath);
            }

            data.characterType = CharacterType.Cluck;
            data.displayName = "Cluck";
            data.movementSpeed = 5f;
            data.specialAbility = AbilityType.EggDrop;
            data.abilityCooldown = 15f;
            data.abilityDescription = "Drops 3 eggs on the maze that damage any robot passing over them.";
            data.unlockLevel = 0;

            EditorUtility.SetDirty(data);
        }

        /// <summary>LevelData_01's maze is now a fixed, hand-authored layout (not procedurally
        /// generated) — designed by the user via a purpose-built maze-designer web tool and pasted
        /// back verbatim as a row-major tile-id grid. Two earlier procedural approaches were tried
        /// and both produced technically-valid-but-open-reading mazes (a mirrored half-board with a
        /// seam corridor, then a full-width recursive-backtracker whose fixed seed happened to
        /// connect entire rows); hand authorship sidesteps that whole failure class since every
        /// tile is a deliberate choice. `Rows` below is ordered top-of-screen first (highest y) to
        /// match how the maze reads on screen and how the design tool exports it; `ParseRows`
        /// converts to the `grid[x,y]` convention `LevelData.SetMazeLayout` expects (y=0 at the
        /// bottom, since GridToWorld maps grid y directly to world Y with no flip).</summary>
        private static readonly string[] Rows =
        {
            "111111111111", // y=8 (top)
            "172222322231", // y=7
            "121111111211", // y=6
            "532221622235", // y=5
            "111121112121", // y=4
            "122232223121", // y=3
            "112121131121", // y=2
            "532232242235", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static int[,] ParseRows(string[] rows, int width, int height)
        {
            var grid = new int[width, height];
            for (int editorRow = 0; editorRow < height; editorRow++)
            {
                int y = height - 1 - editorRow;
                string row = rows[editorRow];
                for (int x = 0; x < width; x++)
                {
                    grid[x, y] = row[x] - '0';
                }
            }
            return grid;
        }

        /// <summary>Shared body for every BuildLevelDataNN() method — extracted once LevelData_09
        /// onward made repeating this ~50-line block by hand impractical. Scans the parsed grid for
        /// tile ids 2/3/4 (crop/vegetable/pellet counts), 5 (warp rows), 6 (factory box centre), and
        /// 7 (player start) — none of those are hand-maintained coordinates, so a maze can be edited
        /// or regenerated freely without touching this method. LevelData_09..LevelData_25 are
        /// algorithmically generated (see the gen script this was ported from): a recursive
        /// backtracker carves a spanning tree over cell positions on ODD-ODD grid coordinates only
        /// (connectors between adjacent cells sit on exactly-one-even coordinates), with extra random
        /// loop edges added afterward for multiple routes. Every EVEN-EVEN grid point is therefore
        /// never carved by construction, and any 2x2 all-open square necessarily contains exactly one
        /// EVEN-EVEN point — so the open-2x2-block failure mode documented above (from the two
        /// earlier hand-tuned procedural attempts) can't occur here regardless of how many loop edges
        /// get added. Warp portals/factory/player-start/pellet placements are applied as later
        /// overwrites of already-open path tiles, never opening new tiles, so this invariant holds
        /// for the finished maze too.</summary>
        private static void BuildLevel(string path, string[] rows, int levelNumber, string levelName, MazeType mazeType = MazeType.CornField)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(level, path);
            }

            const int width = 12;
            const int height = 9;
            var grid = ParseRows(rows, width, height);

            Vector2Int playerStart = default;
            var warpRows = new List<int>();
            int factoryMinX = int.MaxValue, factoryMaxX = int.MinValue, factoryMinY = int.MaxValue, factoryMaxY = int.MinValue;
            int kernels = 0, vegetables = 0, pellets = 0;

            for (int y = 0; y < height; y++)
            {
                bool rowHasWarp = false;
                for (int x = 0; x < width; x++)
                {
                    switch (grid[x, y])
                    {
                        case 2: kernels++; break;
                        case 3: vegetables++; break;
                        case 4: pellets++; break;
                        case 5: rowHasWarp = true; break;
                        case 6:
                            factoryMinX = Mathf.Min(factoryMinX, x);
                            factoryMaxX = Mathf.Max(factoryMaxX, x);
                            factoryMinY = Mathf.Min(factoryMinY, y);
                            factoryMaxY = Mathf.Max(factoryMaxY, y);
                            break;
                        case 7: playerStart = new Vector2Int(x, y); break;
                    }
                }
                if (rowHasWarp)
                {
                    warpRows.Add(y);
                }
            }

            level.levelNumber = levelNumber;
            level.levelName = levelName;
            level.mazeType = mazeType;
            level.SetMazeLayout(grid);
            level.playerStartPosition = playerStart;
            // Robots spawn from the middle of the maze — the factory box's own centre, derived from
            // whatever cells were painted id 6, not a hardcoded position. Keep
            // Phase3ProjectBuilder.UpdateLevelData01Robots's spawnPosition in sync with this if
            // LevelData_01 specifically is redesigned again.
            level.robotFactoryPosition = new Vector2Int((factoryMinX + factoryMaxX) / 2, (factoryMinY + factoryMaxY) / 2);
            level.baseCharacterSpeed = 4.0f;
            level.baseRobotSpeed = 3.5f;
            level.robotSpawns = new RobotSpawnData[0]; // No robots yet — Phase 3
            level.warpTunnelRows = warpRows.ToArray();
            level.waterTeleportRows = new int[0];
            level.totalCropsRequired = kernels + vegetables + pellets;

            EditorUtility.SetDirty(level);
        }

        private static void BuildLevelData01() => BuildLevel(LevelDataPath, Rows, 0, "The Corn Field - 01");

        /// <summary>LevelData_02, same maze-designer-tool-sourced/hand-authored convention as
        /// LevelData_01's `Rows` above.</summary>
        private static readonly string[] Rows02 =
        {
            "111111115111", // y=8 (top)
            "132222342271", // y=7
            "121211212131", // y=6
            "121261311121", // y=5
            "521132213225", // y=4
            "132221222121", // y=3
            "121111212121", // y=2
            "123122322321", // y=1
            "111111115111", // y=0 (bottom)
        };

        private static void BuildLevelData02() => BuildLevel(LevelData02Path, Rows02, 1, "The Corn Field - 02");

        /// <summary>LevelData_03, same maze-designer-tool-sourced/hand-authored convention as
        /// LevelData_01's `Rows` above.</summary>
        private static readonly string[] Rows03 =
        {
            "111511111111", // y=8 (top)
            "522232322125", // y=7
            "121112112221", // y=6
            "121362211131", // y=5
            "121111322121", // y=4
            "132221213131", // y=3
            "121111212121", // y=2
            "172322312241", // y=1
            "111511111111", // y=0 (bottom)
        };

        private static void BuildLevelData03() => BuildLevel(LevelData03Path, Rows03, 2, "The Corn Field - 03");

        /// <summary>LevelData_04, same maze-designer-tool-sourced/hand-authored convention as
        /// LevelData_01's `Rows` above.</summary>
        private static readonly string[] Rows04 =
        {
            "111111111111", // y=8 (top)
            "522322232225", // y=7
            "112111211111", // y=6
            "121161312231", // y=5
            "123221211141", // y=4
            "121132223131", // y=3
            "112121212121", // y=2
            "522321317225", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData04() => BuildLevel(LevelData04Path, Rows04, 3, "The Corn Field - 04");

        /// <summary>LevelData_05, algorithmically generated (recursive backtracker on the 5x4
        /// odd-odd cell grid + loop edges, same convention as LevelData_09 onward — see BuildLevel's
        /// doc comment) rather than hand-authored via the maze designer. Verified offline before
        /// being baked in here: fully connected (every non-wall cell reachable from the player
        /// start), no open-2x2 block anywhere, and the two warp tiles (0,5)/(11,5) both have an
        /// open interior neighbor so the tunnel is actually usable in both directions. Replaces
        /// what used to be a permanent gap at levelNumber 4 (see the LevelData05Path comment above
        /// for why that gap existed and how it leaked Phase3's test maze into Level Select).</summary>
        private static readonly string[] Rows05 =
        {
            "111111111111", // y=8 (top)
            "142222222411", // y=7
            "121112111211", // y=6
            "522222621235", // y=5
            "121211121211", // y=4
            "122222122211", // y=3
            "111112121111", // y=2
            "122227122211", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData05() => BuildLevel(LevelData05Path, Rows05, 4, "The Corn Field - 05");

        /// <summary>LevelData_06 (the 6th designed 12x9 progression level), same maze-designer-tool-
        /// sourced/hand-authored convention as LevelData_01's `Rows` above.</summary>
        private static readonly string[] Rows06 =
        {
            "111111111151", // y=8 (top)
            "122222326131", // y=7
            "121111121221", // y=6
            "132237121121", // y=5
            "121111123131", // y=4
            "522223112125", // y=3
            "121112132211", // y=2
            "141322221321", // y=1
            "111111111151", // y=0 (bottom)
        };

        private static void BuildLevelData06() => BuildLevel(LevelData06Path, Rows06, 5, "The Corn Field - 06");

        /// <summary>LevelData_07, same convention as LevelData_06 above.</summary>
        private static readonly string[] Rows07 =
        {
            "111111151111", // y=8 (top)
            "531232221135", // y=7
            "121711121121", // y=6
            "121116121131", // y=5
            "132223123221", // y=4
            "121131121121", // y=3
            "121222121121", // y=2
            "131312222241", // y=1
            "111111151111", // y=0 (bottom)
        };

        private static void BuildLevelData07() => BuildLevel(LevelData07Path, Rows07, 6, "The Corn Field - 07");

        /// <summary>LevelData_08, same convention as LevelData_06 above.</summary>
        // Two of this maze's 4 warp tiles ((0,2) and (10,8)) originally had no row-mate or
        // column-mate at all — a hand-authoring slip that predates TileMapRenderer's row-then-
        // column pairing fix (see its own doc comment) and would have left both silently dead
        // (touching them did nothing). (0,2) is fixed by giving it a real, reachable partner at
        // (11,2) — (10,2) needed opening from wall to floor first so the new warp destination
        // isn't a walled-in dead end (verified this adds no 2x2 open block and only ever adds
        // connectivity, never removes it). (10,8) has no such clean fix: its only valid vertical
        // partner row (y=0) already holds this maze's OTHER pair's tile at (7,0) — adding a second
        // y=0 tile at (10,0) would make the row-first pairing pass greedily pair (7,0) with (10,0)
        // instead of each with its real vertical partner ((7,6)/(10,8)), breaking both pairs
        // instead of fixing one. Reverted to a plain wall instead, matching the rest of this
        // border row — it never worked before this fix either way, so removing it changes nothing
        // a player would notice, just cleans up the dead stub.
        private static readonly string[] Rows08 =
        {
            "111111111111", // y=8 (top)
            "132222227121", // y=7
            "121113151131", // y=6
            "121416122321", // y=5
            "131211121121", // y=4
            "121232231121", // y=3
            "521111111251", // y=2
            "122232232221", // y=1
            "111111151111", // y=0 (bottom)
        };

        private static void BuildLevelData08() => BuildLevel(LevelData08Path, Rows08, 7, "The Corn Field - 08");

        // ---- LevelData_09 through LevelData_25: algorithmically generated (see BuildLevel's doc
        // comment for the generation/validation approach). ----

        private static readonly string[] Rows09 =
        {
            "111111111111", // y=8 (top)
            "522222122325", // y=7
            "121613121211", // y=6
            "521212121225", // y=5
            "111313121211", // y=4
            "132222133211", // y=3
            "131117111211", // y=2
            "521233322345", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData09() => BuildLevel(LevelData09Path, Rows09, 8, "The Corn Field - 09");

        private static readonly string[] Rows10 =
        {
            "151111111511", // y=8 (top)
            "131633222311", // y=7
            "121211111411", // y=6
            "531212123235", // y=5
            "121212121311", // y=4
            "173322121211", // y=3
            "111311121311", // y=2
            "122223221211", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData10() => BuildLevel(LevelData10Path, Rows10, 9, "The Corn Field - 10");

        private static readonly string[] Rows11 =
        {
            "151111111511", // y=8 (top)
            "522232221325", // y=7
            "121112161211", // y=6
            "522212321325", // y=5
            "121213121211", // y=4
            "522212221725", // y=3
            "121212121411", // y=2
            "131323323211", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData11() => BuildLevel(LevelData11Path, Rows11, 10, "The Corn Field - 11");

        private static readonly string[] Rows12 =
        {
            "111111111111", // y=8 (top)
            "522732322235", // y=7
            "131116111211", // y=6
            "122222232211", // y=5
            "131211121211", // y=4
            "531323131225", // y=3
            "131214121211", // y=2
            "531212223325", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData12() => BuildLevel(LevelData12Path, Rows12, 11, "The Corn Field - 12");

        private static readonly string[] Rows13 =
        {
            "111111111111", // y=8 (top)
            "132212221311", // y=7
            "121212121311", // y=6
            "522326131225", // y=5
            "121111121211", // y=4
            "521233124225", // y=3
            "131212111211", // y=2
            "121317223311", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData13() => BuildLevel(LevelData13Path, Rows13, 12, "The Corn Field - 13");

        private static readonly string[] Rows14 =
        {
            "151111111511", // y=8 (top)
            "522322332235", // y=7
            "121112121111", // y=6
            "132332222311", // y=5
            "121213161211", // y=4
            "122413272311", // y=3
            "121211111211", // y=2
            "523232223225", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData14() => BuildLevel(LevelData14Path, Rows14, 13, "The Corn Field - 14");

        private static readonly string[] Rows15 =
        {
            "111111111111", // y=8 (top)
            "121222332211", // y=7
            "121212121211", // y=6
            "522312623225", // y=5
            "121112111311", // y=4
            "121222122211", // y=3
            "111212131111", // y=2
            "132374232211", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData15() => BuildLevel(LevelData15Path, Rows15, 14, "The Corn Field - 15");

        private static readonly string[] Rows16 =
        {
            "151111111511", // y=8 (top)
            "123236222311", // y=7
            "131211111211", // y=6
            "171422322311", // y=5
            "111112111111", // y=4
            "532322222225", // y=3
            "121212111211", // y=2
            "531883222225", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData16() => BuildLevel(LevelData16Path, Rows16, 15, "The Corn Field - 16");

        private static readonly string[] Rows17 =
        {
            "151111111511", // y=8 (top)
            "123222222411", // y=7
            "131111161211", // y=6
            "131223321311", // y=5
            "121711121311", // y=4
            "532212231325", // y=3
            "111212111211", // y=2
            "532318822235", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData17() => BuildLevel(LevelData17Path, Rows17, 16, "The Corn Field - 17");

        private static readonly string[] Rows18 =
        {
            "151111111511", // y=8 (top)
            "522723222225", // y=7
            "121312121111", // y=6
            "121232163311", // y=5
            "121212111211", // y=4
            "121212123311", // y=3
            "131212121211", // y=2
            "521882241225", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData18() => BuildLevel(LevelData18Path, Rows18, 17, "The Corn Field - 18");

        private static readonly string[] Rows19 =
        {
            "111111111111", // y=8 (top)
            "132622331211", // y=7
            "121112121311", // y=6
            "532312121245", // y=5
            "111112131211", // y=4
            "532332122235", // y=3
            "131111111211", // y=2
            "131882332711", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData19() => BuildLevel(LevelData19Path, Rows19, 18, "The Corn Field - 19");

        private static readonly string[] Rows20 =
        {
            "111111111111", // y=8 (top)
            "121342232211", // y=7
            "131211121211", // y=6
            "532622131325", // y=5
            "121212121211", // y=4
            "522223121225", // y=3
            "131313121211", // y=2
            "527883221325", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData20() => BuildLevel(LevelData20Path, Rows20, 19, "The Corn Field - 20");

        private static readonly string[] Rows21 =
        {
            "111111111111", // y=8 (top)
            "133222321211", // y=7
            "131311121211", // y=6
            "521632221225", // y=5
            "121211121211", // y=4
            "521322121225", // y=3
            "121112171211", // y=2
            "148822123311", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData21() => BuildLevel(LevelData21Path, Rows21, 20, "The Corn Field - 21");

        private static readonly string[] Rows22 =
        {
            "111111111111", // y=8 (top)
            "522226232235", // y=7
            "121211111311", // y=6
            "122212221211", // y=5
            "121117111211", // y=4
            "132312221211", // y=3
            "111212131311", // y=2
            "122488322211", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData22() => BuildLevel(LevelData22Path, Rows22, 21, "The Corn Field - 22");

        private static readonly string[] Rows23 =
        {
            "111111111111", // y=8 (top)
            "132436233311", // y=7
            "121113111211", // y=6
            "531322122225", // y=5
            "121211121111", // y=4
            "521312221225", // y=3
            "111212111211", // y=2
            "522312722325", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData23() => BuildLevel(LevelData23Path, Rows23, 22, "The Corn Field - 23");

        private static readonly string[] Rows24 =
        {
            "111111111111", // y=8 (top)
            "522622223235", // y=7
            "121211111211", // y=6
            "131883422311", // y=5
            "131111111311", // y=4
            "122322321711", // y=3
            "111111121211", // y=2
            "132222231211", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData24() => BuildLevel(LevelData24Path, Rows24, 23, "The Corn Field - 24");

        private static readonly string[] Rows25 =
        {
            "151111111511", // y=8 (top)
            "122316223711", // y=7
            "131112111311", // y=6
            "122333221211", // y=5
            "121111141211", // y=4
            "532222321225", // y=3
            "111111111311", // y=2
            "188222232211", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData25() => BuildLevel(LevelData25Path, Rows25, 24, "The Corn Field - 25");

        // ---- LevelData_26 through LevelData_50: World 2 (VegPatch), algorithmically generated
        // the same way as World 1's LevelData_09-25. ----

        private static readonly string[] Rows26 =
        {
            "151111111511", // y=8 (top)
            "132232622211", // y=7
            "121112111211", // y=6
            "121327221311", // y=5
            "121211111211", // y=4
            "132313222211", // y=3
            "111112121211", // y=2
            "134232882211", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData26() => BuildLevel(LevelData26Path, Rows26, 25, "The Veggie Patch - 01", MazeType.VegPatch);

        private static readonly string[] Rows27 =
        {
            "151111111511", // y=8 (top)
            "121226272311", // y=7
            "121211121211", // y=6
            "121312221211", // y=5
            "121212111211", // y=4
            "122322131311", // y=3
            "131212131211", // y=2
            "138483123211", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData27() => BuildLevel(LevelData27Path, Rows27, 26, "The Veggie Patch - 02", MazeType.VegPatch);

        private static readonly string[] Rows28 =
        {
            "151111111511", // y=8 (top)
            "133222227211", // y=7
            "121112121211", // y=6
            "521224221325", // y=5
            "131612121311", // y=4
            "121212232311", // y=3
            "111212121211", // y=2
            "182212222811", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData28() => BuildLevel(LevelData28Path, Rows28, 27, "The Veggie Patch - 03", MazeType.VegPatch);

        private static readonly string[] Rows29 =
        {
            "111111111111", // y=8 (top)
            "522326223225", // y=7
            "121111111311", // y=6
            "171223221211", // y=5
            "131112111211", // y=4
            "188322222211", // y=3
            "111111121211", // y=2
            "123223241211", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData29() => BuildLevel(LevelData29Path, Rows29, 28, "The Veggie Patch - 04", MazeType.VegPatch);

        private static readonly string[] Rows30 =
        {
            "151111111511", // y=8 (top)
            "122223633211", // y=7
            "131211111311", // y=6
            "521322232225", // y=5
            "111113131111", // y=4
            "132222173311", // y=3
            "121212111311", // y=2
            "541882222235", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData30() => BuildLevel(LevelData30Path, Rows30, 29, "The Veggie Patch - 05", MazeType.VegPatch);

        private static readonly string[] Rows31 =
        {
            "151111111511", // y=8 (top)
            "522223226225", // y=7
            "121111131211", // y=6
            "521233231235", // y=5
            "121211121311", // y=4
            "121242322711", // y=3
            "111212131111", // y=2
            "122318822211", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData31() => BuildLevel(LevelData31Path, Rows31, 30, "The Veggie Patch - 06", MazeType.VegPatch);

        private static readonly string[] Rows32 =
        {
            "151111111511", // y=8 (top)
            "522322322225", // y=7
            "111211111311", // y=6
            "122226234211", // y=5
            "121211111211", // y=4
            "528218132235", // y=3
            "111212131211", // y=2
            "522212322735", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData32() => BuildLevel(LevelData32Path, Rows32, 31, "The Veggie Patch - 07", MazeType.VegPatch);

        private static readonly string[] Rows33 =
        {
            "111111111111", // y=8 (top)
            "122622222211", // y=7
            "121111111211", // y=6
            "522222222235", // y=5
            "111111121211", // y=4
            "522213221225", // y=3
            "141212111711", // y=2
            "531222882225", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData33() => BuildLevel(LevelData33Path, Rows33, 32, "The Veggie Patch - 08", MazeType.VegPatch);

        private static readonly string[] Rows34 =
        {
            "111111111111", // y=8 (top)
            "124322223211", // y=7
            "121111121211", // y=6
            "522327221225", // y=5
            "111211161211", // y=4
            "522212232325", // y=3
            "121212111211", // y=2
            "128822122311", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData34() => BuildLevel(LevelData34Path, Rows34, 33, "The Veggie Patch - 09", MazeType.VegPatch);

        private static readonly string[] Rows35 =
        {
            "111111111111", // y=8 (top)
            "522222162225", // y=7
            "121213121211", // y=6
            "521212122245", // y=5
            "121313111211", // y=4
            "521312232235", // y=3
            "121311121211", // y=2
            "521882272225", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData35() => BuildLevel(LevelData35Path, Rows35, 34, "The Veggie Patch - 10", MazeType.VegPatch);

        private static readonly string[] Rows36 =
        {
            "111111111111", // y=8 (top)
            "132222222411", // y=7
            "121112121311", // y=6
            "522213226225", // y=5
            "121111121211", // y=4
            "122213221311", // y=3
            "131212121211", // y=2
            "531883127225", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData36() => BuildLevel(LevelData36Path, Rows36, 35, "The Veggie Patch - 11", MazeType.VegPatch);

        private static readonly string[] Rows37 =
        {
            "151111111511", // y=8 (top)
            "123223221211", // y=7
            "141111121211", // y=6
            "521322123735", // y=5
            "121216131211", // y=4
            "122212121311", // y=3
            "111113121311", // y=2
            "182222122811", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData37() => BuildLevel(LevelData37Path, Rows37, 36, "The Veggie Patch - 12", MazeType.VegPatch);

        private static readonly string[] Rows38 =
        {
            "151111111511", // y=8 (top)
            "132223222211", // y=7
            "121211161211", // y=6
            "132212222211", // y=5
            "121112111311", // y=4
            "532222131375", // y=3
            "121411121211", // y=2
            "531882232235", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData38() => BuildLevel(LevelData38Path, Rows38, 37, "The Veggie Patch - 13", MazeType.VegPatch);

        private static readonly string[] Rows39 =
        {
            "151111111511", // y=8 (top)
            "522222262225", // y=7
            "111112111211", // y=6
            "132212221211", // y=5
            "121211121211", // y=4
            "121422221211", // y=3
            "121211111211", // y=2
            "128823732211", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData39() => BuildLevel(LevelData39Path, Rows39, 38, "The Veggie Patch - 14", MazeType.VegPatch);

        private static readonly string[] Rows40 =
        {
            "151111111511", // y=8 (top)
            "531236222245", // y=7
            "131212111111", // y=6
            "123212222211", // y=5
            "131111111211", // y=4
            "523222122235", // y=3
            "121112171211", // y=2
            "183322321811", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData40() => BuildLevel(LevelData40Path, Rows40, 39, "The Veggie Patch - 15", MazeType.VegPatch);

        private static readonly string[] Rows41 =
        {
            "111111111111", // y=8 (top)
            "521326322225", // y=7
            "121212121211", // y=6
            "527222141335", // y=5
            "121111131211", // y=4
            "121223232211", // y=3
            "111211131311", // y=2
            "522218822325", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData41() => BuildLevel(LevelData41Path, Rows41, 40, "The Veggie Patch - 16", MazeType.VegPatch);

        private static readonly string[] Rows42 =
        {
            "111111111111", // y=8 (top)
            "522232322225", // y=7
            "121112131111", // y=6
            "121362132211", // y=5
            "131112111311", // y=4
            "122232122211", // y=3
            "121212121311", // y=2
            "138418721211", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData42() => BuildLevel(LevelData42Path, Rows42, 41, "The Veggie Patch - 17", MazeType.VegPatch);

        private static readonly string[] Rows43 =
        {
            "151111111511", // y=8 (top)
            "122222321311", // y=7
            "121111121211", // y=6
            "131222221311", // y=5
            "141216121311", // y=4
            "122313121311", // y=3
            "111212121211", // y=2
            "132278832211", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData43() => BuildLevel(LevelData43Path, Rows43, 42, "The Veggie Patch - 18", MazeType.VegPatch);

        private static readonly string[] Rows44 =
        {
            "151111111511", // y=8 (top)
            "122222227211", // y=7
            "131111121311", // y=6
            "122223261211", // y=5
            "121211111211", // y=4
            "181324123811", // y=3
            "111112121111", // y=2
            "122222222211", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData44() => BuildLevel(LevelData44Path, Rows44, 43, "The Veggie Patch - 19", MazeType.VegPatch);

        private static readonly string[] Rows45 =
        {
            "111111111111", // y=8 (top)
            "123226123211", // y=7
            "121213131711", // y=6
            "522212231325", // y=5
            "121211111211", // y=4
            "131222121211", // y=3
            "111212121211", // y=2
            "122488322211", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData45() => BuildLevel(LevelData45Path, Rows45, 44, "The Veggie Patch - 20", MazeType.VegPatch);

        private static readonly string[] Rows46 =
        {
            "111111111111", // y=8 (top)
            "122216233211", // y=7
            "121312121111", // y=6
            "132322222211", // y=5
            "121112111211", // y=4
            "132212231211", // y=3
            "111112121211", // y=2
            "534322887225", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData46() => BuildLevel(LevelData46Path, Rows46, 45, "The Veggie Patch - 21", MazeType.VegPatch);

        private static readonly string[] Rows47 =
        {
            "151111111511", // y=8 (top)
            "122262232211", // y=7
            "141212111311", // y=6
            "532232232225", // y=5
            "121113121111", // y=4
            "522327322325", // y=3
            "111213111211", // y=2
            "523318822225", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData47() => BuildLevel(LevelData47Path, Rows47, 46, "The Veggie Patch - 22", MazeType.VegPatch);

        private static readonly string[] Rows48 =
        {
            "111111111111", // y=8 (top)
            "534672323325", // y=7
            "121111111211", // y=6
            "532212222225", // y=5
            "111113121211", // y=4
            "523222121325", // y=3
            "131111121211", // y=2
            "521882232225", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData48() => BuildLevel(LevelData48Path, Rows48, 47, "The Veggie Patch - 23", MazeType.VegPatch);

        private static readonly string[] Rows49 =
        {
            "151111111511", // y=8 (top)
            "121322243211", // y=7
            "131316121211", // y=6
            "532212221325", // y=5
            "111111111211", // y=4
            "533212322225", // y=3
            "121213121211", // y=2
            "128372182211", // y=1
            "151111111511", // y=0 (bottom)
        };

        private static void BuildLevelData49() => BuildLevel(LevelData49Path, Rows49, 48, "The Veggie Patch - 24", MazeType.VegPatch);

        private static readonly string[] Rows50 =
        {
            "111111111111", // y=8 (top)
            "123232222311", // y=7
            "121211111711", // y=6
            "521216222235", // y=5
            "121312111211", // y=4
            "188232231211", // y=3
            "111111121211", // y=2
            "522432221235", // y=1
            "111111111111", // y=0 (bottom)
        };

        private static void BuildLevelData50() => BuildLevel(LevelData50Path, Rows50, 49, "The Veggie Patch - 25", MazeType.VegPatch);

        // World 3 (Orchard) — continues levelNumber sequentially after World 2's 50 (25-49), so
        // World 3 occupies levelNumber 50-74 / LevelData_51 through LevelData_75, matching
        // UnlockProgression.LevelsPerWorld's 25-per-world convention. Algorithmically generated the
        // same recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels (see
        // BuildLevel's doc comment) — offline generator (OrchardMazeGeneratorTemp, not kept in the
        // repo, same "generator not committed, only its baked output is" convention as World 1/2's
        // generated levels) verified every maze's full connectivity, absence of any open 2x2 block,
        // and that both warp tiles have a usable open neighbor before this was baked in.
        /// <summary>LevelData_51 (The Orchard - 01), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows51 =
        {
            "151111111111", // y=8 (top)
            "122423322211", // y=7
            "121212131211", // y=6
            "133322123311", // y=5
            "121212121211", // y=4
            "121216231211", // y=3
            "121212121211", // y=2
            "172242132211", // y=1
            "151111111111", // y=0 (bottom)
        };

        private static void BuildLevelData51() => BuildLevel(LevelData51Path, Rows51, 50, "The Orchard - 01", MazeType.Orchard);

        /// <summary>LevelData_52 (The Orchard - 02), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows52 =
        {
            "111511111111", // y=8 (top)
            "122222232211", // y=7
            "121212121311", // y=6
            "133316222211", // y=5
            "121111121111", // y=4
            "144233122211", // y=3
            "121212131311", // y=2
            "122322222711", // y=1
            "111511111111", // y=0 (bottom)
        };

        private static void BuildLevelData52() => BuildLevel(LevelData52Path, Rows52, 51, "The Orchard - 02", MazeType.Orchard);

        /// <summary>LevelData_53 (The Orchard - 03), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows53 =
        {
            "111115111111", // y=8 (top)
            "173422222311", // y=7
            "121112121211", // y=6
            "122232231211", // y=5
            "131113121211", // y=4
            "123226223211", // y=3
            "121411111211", // y=2
            "132232222211", // y=1
            "111115111111", // y=0 (bottom)
        };

        private static void BuildLevelData53() => BuildLevel(LevelData53Path, Rows53, 52, "The Orchard - 03", MazeType.Orchard);

        /// <summary>LevelData_54 (The Orchard - 04), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows54 =
        {
            "111111151111", // y=8 (top)
            "133223231711", // y=7
            "121212121311", // y=6
            "123236241211", // y=5
            "121312111211", // y=4
            "122222242211", // y=3
            "111212111311", // y=2
            "122232222211", // y=1
            "111111151111", // y=0 (bottom)
        };

        private static void BuildLevelData54() => BuildLevel(LevelData54Path, Rows54, 53, "The Orchard - 04", MazeType.Orchard);

        /// <summary>LevelData_55 (The Orchard - 05), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows55 =
        {
            "111111111511", // y=8 (top)
            "122322122211", // y=7
            "121211131311", // y=6
            "121222222311", // y=5
            "131212121211", // y=4
            "121226222211", // y=3
            "121313121311", // y=2
            "172324142311", // y=1
            "111111111511", // y=0 (bottom)
        };

        private static void BuildLevelData55() => BuildLevel(LevelData55Path, Rows55, 54, "The Orchard - 05", MazeType.Orchard);

        /// <summary>LevelData_56 (The Orchard - 06), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows56 =
        {
            "151111111111", // y=8 (top)
            "132222222211", // y=7
            "141213121311", // y=6
            "123236123211", // y=5
            "121212131211", // y=4
            "121222321311", // y=3
            "141212111211", // y=2
            "122212223711", // y=1
            "151111111111", // y=0 (bottom)
        };

        private static void BuildLevelData56() => BuildLevel(LevelData56Path, Rows56, 55, "The Orchard - 06", MazeType.Orchard);

        /// <summary>LevelData_57 (The Orchard - 07), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows57 =
        {
            "111511111111", // y=8 (top)
            "172222321211", // y=7
            "121312131311", // y=6
            "133222421211", // y=5
            "121211121211", // y=4
            "122226143211", // y=3
            "121212121211", // y=2
            "122232232311", // y=1
            "111511111111", // y=0 (bottom)
        };

        private static void BuildLevelData57() => BuildLevel(LevelData57Path, Rows57, 56, "The Orchard - 07", MazeType.Orchard);

        /// <summary>LevelData_58 (The Orchard - 08), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows58 =
        {
            "111115111111", // y=8 (top)
            "123222222711", // y=7
            "121114131211", // y=6
            "122216331311", // y=5
            "121212121311", // y=4
            "123222222311", // y=3
            "131213111211", // y=2
            "122222422211", // y=1
            "111115111111", // y=0 (bottom)
        };

        private static void BuildLevelData58() => BuildLevel(LevelData58Path, Rows58, 57, "The Orchard - 08", MazeType.Orchard);

        /// <summary>LevelData_59 (The Orchard - 09), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows59 =
        {
            "111111151111", // y=8 (top)
            "133222222311", // y=7
            "121213131211", // y=6
            "124223222211", // y=5
            "121412111211", // y=4
            "131226321211", // y=3
            "121112121211", // y=2
            "173222232211", // y=1
            "111111151111", // y=0 (bottom)
        };

        private static void BuildLevelData59() => BuildLevel(LevelData59Path, Rows59, 58, "The Orchard - 09", MazeType.Orchard);

        /// <summary>LevelData_60 (The Orchard - 10), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows60 =
        {
            "111111111511", // y=8 (top)
            "132322332211", // y=7
            "121112121211", // y=6
            "123226122211", // y=5
            "141111121211", // y=4
            "122322242311", // y=3
            "121312131211", // y=2
            "122232222711", // y=1
            "111111111511", // y=0 (bottom)
        };

        private static void BuildLevelData60() => BuildLevel(LevelData60Path, Rows60, 59, "The Orchard - 10", MazeType.Orchard);

        /// <summary>LevelData_61 (The Orchard - 11), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows61 =
        {
            "151111111111", // y=8 (top)
            "171322223211", // y=7
            "121212121211", // y=6
            "122234221211", // y=5
            "121112121311", // y=4
            "121226222311", // y=3
            "121312131211", // y=2
            "122342233211", // y=1
            "151111111111", // y=0 (bottom)
        };

        private static void BuildLevelData61() => BuildLevel(LevelData61Path, Rows61, 60, "The Orchard - 11", MazeType.Orchard);

        /// <summary>LevelData_62 (The Orchard - 12), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows62 =
        {
            "111511111111", // y=8 (top)
            "122222222711", // y=7
            "121212121311", // y=6
            "122236223211", // y=5
            "131212111211", // y=4
            "132422132311", // y=3
            "121112131211", // y=2
            "143312222211", // y=1
            "111511111111", // y=0 (bottom)
        };

        private static void BuildLevelData62() => BuildLevel(LevelData62Path, Rows62, 61, "The Orchard - 12", MazeType.Orchard);

        /// <summary>LevelData_63 (The Orchard - 13), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows63 =
        {
            "111115111111", // y=8 (top)
            "122222222211", // y=7
            "121112121211", // y=6
            "122322243311", // y=5
            "121212141211", // y=4
            "121326232311", // y=3
            "121112111211", // y=2
            "172322233311", // y=1
            "111115111111", // y=0 (bottom)
        };

        private static void BuildLevelData63() => BuildLevel(LevelData63Path, Rows63, 62, "The Orchard - 13", MazeType.Orchard);

        /// <summary>LevelData_64 (The Orchard - 14), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows64 =
        {
            "111111151111", // y=8 (top)
            "132222332211", // y=7
            "121111131211", // y=6
            "122226221311", // y=5
            "121213131211", // y=4
            "123222222311", // y=3
            "121212121311", // y=2
            "122412422711", // y=1
            "111111151111", // y=0 (bottom)
        };

        private static void BuildLevelData64() => BuildLevel(LevelData64Path, Rows64, 63, "The Orchard - 14", MazeType.Orchard);

        /// <summary>LevelData_65 (The Orchard - 15), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows65 =
        {
            "111111111511", // y=8 (top)
            "172232222211", // y=7
            "131214121211", // y=6
            "142322221211", // y=5
            "121312121311", // y=4
            "122226131311", // y=3
            "131211121211", // y=2
            "122223223211", // y=1
            "111111111511", // y=0 (bottom)
        };

        private static void BuildLevelData65() => BuildLevel(LevelData65Path, Rows65, 64, "The Orchard - 15", MazeType.Orchard);

        /// <summary>LevelData_66 (The Orchard - 16), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows66 =
        {
            "151111111111", // y=8 (top)
            "122212222711", // y=7
            "131112111311", // y=6
            "122326222211", // y=5
            "111313121211", // y=4
            "123322122411", // y=3
            "121113121211", // y=2
            "132322124211", // y=1
            "151111111111", // y=0 (bottom)
        };

        private static void BuildLevelData66() => BuildLevel(LevelData66Path, Rows66, 65, "The Orchard - 16", MazeType.Orchard);

        /// <summary>LevelData_67 (The Orchard - 17), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows67 =
        {
            "111511111111", // y=8 (top)
            "122222324311", // y=7
            "121311121211", // y=6
            "121232423211", // y=5
            "121312121211", // y=4
            "121326121211", // y=3
            "121213121111", // y=2
            "172312322211", // y=1
            "111511111111", // y=0 (bottom)
        };

        private static void BuildLevelData67() => BuildLevel(LevelData67Path, Rows67, 66, "The Orchard - 17", MazeType.Orchard);

        /// <summary>LevelData_68 (The Orchard - 18), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows68 =
        {
            "111115111111", // y=8 (top)
            "121232322311", // y=7
            "121412121211", // y=6
            "122236121411", // y=5
            "121112121111", // y=4
            "122332122211", // y=3
            "131211121311", // y=2
            "123232222711", // y=1
            "111115111111", // y=0 (bottom)
        };

        private static void BuildLevelData68() => BuildLevel(LevelData68Path, Rows68, 67, "The Orchard - 18", MazeType.Orchard);

        /// <summary>LevelData_69 (The Orchard - 19), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows69 =
        {
            "111111151111", // y=8 (top)
            "172222223211", // y=7
            "121213111111", // y=6
            "122312332211", // y=5
            "121113131211", // y=4
            "142336221211", // y=3
            "131112111211", // y=2
            "122222422211", // y=1
            "111111151111", // y=0 (bottom)
        };

        private static void BuildLevelData69() => BuildLevel(LevelData69Path, Rows69, 68, "The Orchard - 19", MazeType.Orchard);

        /// <summary>LevelData_70 (The Orchard - 20), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows70 =
        {
            "111111111511", // y=8 (top)
            "122232122711", // y=7
            "121212121211", // y=6
            "122226222211", // y=5
            "121311121311", // y=4
            "131312222211", // y=3
            "121314111211", // y=2
            "133412133211", // y=1
            "111111111511", // y=0 (bottom)
        };

        private static void BuildLevelData70() => BuildLevel(LevelData70Path, Rows70, 69, "The Orchard - 20", MazeType.Orchard);

        /// <summary>LevelData_71 (The Orchard - 21), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows71 =
        {
            "151111111111", // y=8 (top)
            "122332332211", // y=7
            "121212111211", // y=6
            "133222232211", // y=5
            "111211131211", // y=4
            "121226122211", // y=3
            "121312121211", // y=2
            "172413221411", // y=1
            "151111111111", // y=0 (bottom)
        };

        private static void BuildLevelData71() => BuildLevel(LevelData71Path, Rows71, 70, "The Orchard - 21", MazeType.Orchard);

        /// <summary>LevelData_72 (The Orchard - 22), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows72 =
        {
            "111511111111", // y=8 (top)
            "123222233311", // y=7
            "111213111111", // y=6
            "122216222211", // y=5
            "121211111311", // y=4
            "132222232311", // y=3
            "121411131211", // y=2
            "122422222711", // y=1
            "111511111111", // y=0 (bottom)
        };

        private static void BuildLevelData72() => BuildLevel(LevelData72Path, Rows72, 71, "The Orchard - 22", MazeType.Orchard);

        /// <summary>LevelData_73 (The Orchard - 23), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows73 =
        {
            "111115111111", // y=8 (top)
            "172222222211", // y=7
            "131212111311", // y=6
            "121232322311", // y=5
            "121312121411", // y=4
            "134216121311", // y=3
            "121112121211", // y=2
            "122323122211", // y=1
            "111115111111", // y=0 (bottom)
        };

        private static void BuildLevelData73() => BuildLevel(LevelData73Path, Rows73, 72, "The Orchard - 23", MazeType.Orchard);

        /// <summary>LevelData_74 (The Orchard - 24), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows74 =
        {
            "111111151111", // y=8 (top)
            "122222132711", // y=7
            "121213121211", // y=6
            "122316221211", // y=5
            "121211121311", // y=4
            "131234324211", // y=3
            "121312121211", // y=2
            "121223123211", // y=1
            "111111151111", // y=0 (bottom)
        };

        private static void BuildLevelData74() => BuildLevel(LevelData74Path, Rows74, 73, "The Orchard - 24", MazeType.Orchard);

        /// <summary>LevelData_75 (The Orchard - 25), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows75 =
        {
            "111111111511", // y=8 (top)
            "122213222311", // y=7
            "111313131211", // y=6
            "123222122411", // y=5
            "131112121211", // y=4
            "141326221211", // y=3
            "121212121311", // y=2
            "172322221211", // y=1
            "111111111511", // y=0 (bottom)
        };

        private static void BuildLevelData75() => BuildLevel(LevelData75Path, Rows75, 74, "The Orchard - 25", MazeType.Orchard);

        // World 4 (Wheat) — continues levelNumber sequentially after World 3's 75 (50-74), so
        // World 4 occupies levelNumber 75-99 / LevelData_76 through LevelData_100, matching
        // UnlockProgression.LevelsPerWorld's 25-per-world convention and TotalLevels' 100-level cap
        // exactly (this is the last world). Generated the same way as World 3's own levels (see
        // BuildLevelData51's doc comment above) via a separate offline generator
        // (WheatMazeGeneratorTemp, not kept in the repo, same convention) — different RNG seed and
        // corner/factory offsets so its 25 mazes don't mirror Orchard's shapes.
        /// <summary>LevelData_76 (The Wheat Field - 01), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows76 =
        {
            "111115111111", // y=8 (top)
            "122222222211", // y=7
            "121212141411", // y=6
            "132236222211", // y=5
            "121113121311", // y=4
            "132212221211", // y=3
            "121311131211", // y=2
            "132322223711", // y=1
            "111115111111", // y=0 (bottom)
        };

        private static void BuildLevelData76() => BuildLevel(LevelData76Path, Rows76, 75, "The Wheat Field - 01", MazeType.Wheat);

        /// <summary>LevelData_77 (The Wheat Field - 02), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows77 =
        {
            "111111151111", // y=8 (top)
            "172213223211", // y=7
            "121111121211", // y=6
            "122223222311", // y=5
            "111211121211", // y=4
            "122336233211", // y=3
            "131211111211", // y=2
            "124243222211", // y=1
            "111111151111", // y=0 (bottom)
        };

        private static void BuildLevelData77() => BuildLevel(LevelData77Path, Rows77, 76, "The Wheat Field - 02", MazeType.Wheat);

        /// <summary>LevelData_78 (The Wheat Field - 03), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows78 =
        {
            "111111111511", // y=8 (top)
            "122222223711", // y=7
            "121213111211", // y=6
            "123216231311", // y=5
            "121212131211", // y=4
            "122332121311", // y=3
            "121112111411", // y=2
            "132222222411", // y=1
            "111111111511", // y=0 (bottom)
        };

        private static void BuildLevelData78() => BuildLevel(LevelData78Path, Rows78, 77, "The Wheat Field - 03", MazeType.Wheat);

        /// <summary>LevelData_79 (The Wheat Field - 04), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows79 =
        {
            "151111111111", // y=8 (top)
            "132233222211", // y=7
            "121212121211", // y=6
            "121223121211", // y=5
            "121211121211", // y=4
            "121226234211", // y=3
            "121314111211", // y=2
            "172312233311", // y=1
            "151111111111", // y=0 (bottom)
        };

        private static void BuildLevelData79() => BuildLevel(LevelData79Path, Rows79, 78, "The Wheat Field - 04", MazeType.Wheat);

        /// <summary>LevelData_80 (The Wheat Field - 05), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows80 =
        {
            "111511111111", // y=8 (top)
            "123222132211", // y=7
            "111313111411", // y=6
            "122216222311", // y=5
            "121311121211", // y=4
            "121222322211", // y=3
            "121212111211", // y=2
            "123432223711", // y=1
            "111511111111", // y=0 (bottom)
        };

        private static void BuildLevelData80() => BuildLevel(LevelData80Path, Rows80, 79, "The Wheat Field - 05", MazeType.Wheat);

        /// <summary>LevelData_81 (The Wheat Field - 06), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows81 =
        {
            "111115111111", // y=8 (top)
            "171222223211", // y=7
            "131212121211", // y=6
            "122212231411", // y=5
            "111213121411", // y=4
            "123226131311", // y=3
            "121312121211", // y=2
            "122212322311", // y=1
            "111115111111", // y=0 (bottom)
        };

        private static void BuildLevelData81() => BuildLevel(LevelData81Path, Rows81, 80, "The Wheat Field - 06", MazeType.Wheat);

        /// <summary>LevelData_82 (The Wheat Field - 07), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows82 =
        {
            "111111151111", // y=8 (top)
            "122222223711", // y=7
            "121112121211", // y=6
            "132226132211", // y=5
            "121313111211", // y=4
            "122312221311", // y=3
            "121211121111", // y=2
            "122423334211", // y=1
            "111111151111", // y=0 (bottom)
        };

        private static void BuildLevelData82() => BuildLevel(LevelData82Path, Rows82, 81, "The Wheat Field - 07", MazeType.Wheat);

        /// <summary>LevelData_83 (The Wheat Field - 08), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows83 =
        {
            "111111111511", // y=8 (top)
            "122223233311", // y=7
            "121113111111", // y=6
            "142232223211", // y=5
            "131111121211", // y=4
            "122226221211", // y=3
            "121214121311", // y=2
            "173222122211", // y=1
            "111111111511", // y=0 (bottom)
        };

        private static void BuildLevelData83() => BuildLevel(LevelData83Path, Rows83, 82, "The Wheat Field - 08", MazeType.Wheat);

        /// <summary>LevelData_84 (The Wheat Field - 09), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows84 =
        {
            "151111111111", // y=8 (top)
            "133242222311", // y=7
            "111211121211", // y=6
            "122226221211", // y=5
            "121113121311", // y=4
            "122323122311", // y=3
            "121114111211", // y=2
            "122322223711", // y=1
            "151111111111", // y=0 (bottom)
        };

        private static void BuildLevelData84() => BuildLevel(LevelData84Path, Rows84, 83, "The Wheat Field - 09", MazeType.Wheat);

        /// <summary>LevelData_85 (The Wheat Field - 10), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows85 =
        {
            "111511111111", // y=8 (top)
            "173222232311", // y=7
            "121212131311", // y=6
            "121212231211", // y=5
            "121212121311", // y=4
            "121216324211", // y=3
            "121211111211", // y=2
            "123322422211", // y=1
            "111511111111", // y=0 (bottom)
        };

        private static void BuildLevelData85() => BuildLevel(LevelData85Path, Rows85, 84, "The Wheat Field - 10", MazeType.Wheat);

        /// <summary>LevelData_86 (The Wheat Field - 11), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows86 =
        {
            "111115111111", // y=8 (top)
            "122222222711", // y=7
            "121213121211", // y=6
            "132316321211", // y=5
            "121111111311", // y=4
            "122322422211", // y=3
            "121113121211", // y=2
            "124233122311", // y=1
            "111115111111", // y=0 (bottom)
        };

        private static void BuildLevelData86() => BuildLevel(LevelData86Path, Rows86, 85, "The Wheat Field - 11", MazeType.Wheat);

        /// <summary>LevelData_87 (The Wheat Field - 12), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows87 =
        {
            "111111151111", // y=8 (top)
            "122222122311", // y=7
            "131211121211", // y=6
            "121223322311", // y=5
            "121212131211", // y=4
            "142216222211", // y=3
            "121112111211", // y=2
            "173433232211", // y=1
            "111111151111", // y=0 (bottom)
        };

        private static void BuildLevelData87() => BuildLevel(LevelData87Path, Rows87, 86, "The Wheat Field - 12", MazeType.Wheat);

        /// <summary>LevelData_88 (The Wheat Field - 13), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows88 =
        {
            "111111111511", // y=8 (top)
            "122222233211", // y=7
            "141111121311", // y=6
            "122226322211", // y=5
            "131213121111", // y=4
            "122232132211", // y=3
            "141211121211", // y=2
            "122322231711", // y=1
            "111111111511", // y=0 (bottom)
        };

        private static void BuildLevelData88() => BuildLevel(LevelData88Path, Rows88, 87, "The Wheat Field - 13", MazeType.Wheat);

        /// <summary>LevelData_89 (The Wheat Field - 14), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows89 =
        {
            "151111111111", // y=8 (top)
            "172232123211", // y=7
            "131312121211", // y=6
            "131212222211", // y=5
            "131312111211", // y=4
            "122316222211", // y=3
            "121214121311", // y=2
            "122232241211", // y=1
            "151111111111", // y=0 (bottom)
        };

        private static void BuildLevelData89() => BuildLevel(LevelData89Path, Rows89, 88, "The Wheat Field - 14", MazeType.Wheat);

        /// <summary>LevelData_90 (The Wheat Field - 15), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows90 =
        {
            "111511111111", // y=8 (top)
            "122222232711", // y=7
            "121211131211", // y=6
            "121236231211", // y=5
            "121313111211", // y=4
            "123422122211", // y=3
            "121213121311", // y=2
            "141232222211", // y=1
            "111511111111", // y=0 (bottom)
        };

        private static void BuildLevelData90() => BuildLevel(LevelData90Path, Rows90, 89, "The Wheat Field - 15", MazeType.Wheat);

        /// <summary>LevelData_91 (The Wheat Field - 16), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows91 =
        {
            "111115111111", // y=8 (top)
            "122232332211", // y=7
            "121113121211", // y=6
            "133212123311", // y=5
            "131413121211", // y=4
            "121226422211", // y=3
            "121211121211", // y=2
            "172222122211", // y=1
            "111115111111", // y=0 (bottom)
        };

        private static void BuildLevelData91() => BuildLevel(LevelData91Path, Rows91, 90, "The Wheat Field - 16", MazeType.Wheat);

        /// <summary>LevelData_92 (The Wheat Field - 17), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows92 =
        {
            "111111151111", // y=8 (top)
            "122222423311", // y=7
            "111412121211", // y=6
            "122226232211", // y=5
            "131211111211", // y=4
            "121322232211", // y=3
            "111312121111", // y=2
            "132222323711", // y=1
            "111111151111", // y=0 (bottom)
        };

        private static void BuildLevelData92() => BuildLevel(LevelData92Path, Rows92, 91, "The Wheat Field - 17", MazeType.Wheat);

        /// <summary>LevelData_93 (The Wheat Field - 18), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows93 =
        {
            "111111111511", // y=8 (top)
            "171232222211", // y=7
            "121411111311", // y=6
            "121223232311", // y=5
            "131212121311", // y=4
            "141236221211", // y=3
            "121212121211", // y=2
            "122223222311", // y=1
            "111111111511", // y=0 (bottom)
        };

        private static void BuildLevelData93() => BuildLevel(LevelData93Path, Rows93, 92, "The Wheat Field - 18", MazeType.Wheat);

        /// <summary>LevelData_94 (The Wheat Field - 19), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows94 =
        {
            "151111111111", // y=8 (top)
            "123332222711", // y=7
            "121212121211", // y=6
            "122216132311", // y=5
            "121112111211", // y=4
            "121232122311", // y=3
            "121214121211", // y=2
            "132242322311", // y=1
            "151111111111", // y=0 (bottom)
        };

        private static void BuildLevelData94() => BuildLevel(LevelData94Path, Rows94, 93, "The Wheat Field - 19", MazeType.Wheat);

        /// <summary>LevelData_95 (The Wheat Field - 20), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows95 =
        {
            "111511111111", // y=8 (top)
            "122232332311", // y=7
            "121312121211", // y=6
            "122232121211", // y=5
            "121111121411", // y=4
            "122246122311", // y=3
            "121213111211", // y=2
            "172223223211", // y=1
            "111511111111", // y=0 (bottom)
        };

        private static void BuildLevelData95() => BuildLevel(LevelData95Path, Rows95, 94, "The Wheat Field - 20", MazeType.Wheat);

        /// <summary>LevelData_96 (The Wheat Field - 21), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows96 =
        {
            "111115111111", // y=8 (top)
            "123232233211", // y=7
            "111213141211", // y=6
            "122216222211", // y=5
            "121112121311", // y=4
            "132322122211", // y=3
            "111112141211", // y=2
            "122233222711", // y=1
            "111115111111", // y=0 (bottom)
        };

        private static void BuildLevelData96() => BuildLevel(LevelData96Path, Rows96, 95, "The Wheat Field - 21", MazeType.Wheat);

        /// <summary>LevelData_97 (The Wheat Field - 22), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows97 =
        {
            "111111151111", // y=8 (top)
            "172222323211", // y=7
            "121212111211", // y=6
            "122314324211", // y=5
            "111212131211", // y=4
            "131226122211", // y=3
            "121213121311", // y=2
            "132222322211", // y=1
            "111111151111", // y=0 (bottom)
        };

        private static void BuildLevelData97() => BuildLevel(LevelData97Path, Rows97, 96, "The Wheat Field - 22", MazeType.Wheat);

        /// <summary>LevelData_98 (The Wheat Field - 23), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows98 =
        {
            "111111111511", // y=8 (top)
            "134222232711", // y=7
            "121411121211", // y=6
            "132236321311", // y=5
            "121212131211", // y=4
            "123222231211", // y=3
            "121112121211", // y=2
            "121322222211", // y=1
            "111111111511", // y=0 (bottom)
        };

        private static void BuildLevelData98() => BuildLevel(LevelData98Path, Rows98, 97, "The Wheat Field - 23", MazeType.Wheat);

        /// <summary>LevelData_99 (The Wheat Field - 24), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows99 =
        {
            "151111111111", // y=8 (top)
            "123322223211", // y=7
            "121111121211", // y=6
            "123222123211", // y=5
            "111212121211", // y=4
            "122216333211", // y=3
            "121414131211", // y=2
            "173222222211", // y=1
            "151111111111", // y=0 (bottom)
        };

        private static void BuildLevelData99() => BuildLevel(LevelData99Path, Rows99, 98, "The Wheat Field - 24", MazeType.Wheat);

        /// <summary>LevelData_100 (The Wheat Field - 25), algorithmically generated the same
        /// recursive-backtracker-plus-loop-edges way as World 1/2/3's own generated levels — verified
        /// offline (full connectivity, no open 2x2 block, both warp tiles reachable) before being
        /// baked in here.</summary>
        private static readonly string[] Rows100 =
        {
            "111511111111", // y=8 (top)
            "133332422211", // y=7
            "131112111311", // y=6
            "121226242211", // y=5
            "121212131111", // y=4
            "122222322211", // y=3
            "121212111211", // y=2
            "132223222711", // y=1
            "111511111111", // y=0 (bottom)
        };

        private static void BuildLevelData100() => BuildLevel(LevelData100Path, Rows100, 99, "The Wheat Field - 25", MazeType.Wheat);

        private static void WireScene(GameObject wallPrefab, GameObject groundPrefab, GameObject cropKernelPrefab,
            GameObject cropVegetablePrefab, GameObject powerPelletPrefab, GameObject warpTunnelPrefab, GameObject cluckPrefab,
            GameObject wallPrefabVegPatch, GameObject warpTunnelPrefabVegPatch,
            GameObject cropKernelPrefabVegPatch, GameObject cropVegetablePrefabVegPatch, GameObject coinPrefab)
        {
            EditorSceneManager.OpenScene(ScenePath);

            var managersGO = GameObject.Find("GameManagers");
            var mazeParent = GameObject.Find("MazeParent")?.transform;
            var characterParent = GameObject.Find("CharacterParent")?.transform;
            var robotParent = GameObject.Find("RobotParent")?.transform;

            // Superseded by TileMapRenderer instantiating everything under MazeParent.
            var itemParentGO = GameObject.Find("ItemParent");
            if (itemParentGO != null)
            {
                Object.DestroyImmediate(itemParentGO);
            }

            var tileMapRenderer = managersGO.GetComponent<TileMapRenderer>();
            if (tileMapRenderer == null)
            {
                tileMapRenderer = managersGO.AddComponent<TileMapRenderer>();
            }
            if (managersGO.GetComponent<ScoreManager>() == null)
            {
                managersGO.AddComponent<ScoreManager>();
            }
            if (managersGO.GetComponent<InputController>() == null)
            {
                managersGO.AddComponent<InputController>();
            }

            var tileMapSO = new SerializedObject(tileMapRenderer);
            tileMapSO.FindProperty("mazeParent").objectReferenceValue = mazeParent;
            tileMapSO.FindProperty("powerPelletPrefab").objectReferenceValue = powerPelletPrefab;
            tileMapSO.ApplyModifiedPropertiesWithoutUndo();

            // Per-world wall/ground/warp-tunnel/crop prefabs, keyed by MazeType — CornField's entry
            // points at the exact same prefabs every level up to now has always used, so World 1's
            // rendering is unchanged; VegPatch's entry is new (World 2). Ground has no dedicated
            // per-world art yet, so both entries share groundPrefab (Ground_CornField's soil look
            // reads fine for a vegetable patch too — see TileMapRenderer.MazeArtSet's doc comment).
            // Set directly (not via SerializedObject) since List<T> fields don't need
            // FindProperty's array plumbing here and this runs in a batch-mode Editor script, not
            // an Inspector session that needs Undo support.
            tileMapRenderer.SetMazeArtSets(new List<TileMapRenderer.MazeArtSet>
            {
                new TileMapRenderer.MazeArtSet
                {
                    mazeType = MazeType.CornField,
                    wallPrefab = wallPrefab,
                    groundPrefab = groundPrefab,
                    warpTunnelPrefab = warpTunnelPrefab,
                    cropKernelPrefab = cropKernelPrefab,
                    cropVegetablePrefab = cropVegetablePrefab,
                    bonusPickupPrefab = coinPrefab,
                    bonusPickupCount = 1,
                },
                new TileMapRenderer.MazeArtSet
                {
                    mazeType = MazeType.VegPatch,
                    wallPrefab = wallPrefabVegPatch,
                    groundPrefab = groundPrefab,
                    warpTunnelPrefab = warpTunnelPrefabVegPatch,
                    cropKernelPrefab = cropKernelPrefabVegPatch,
                    cropVegetablePrefab = cropVegetablePrefabVegPatch,
                    useRandomVegetableQuota = true,
                    vegetableQuota = 10,
                },
            });
            EditorUtility.SetDirty(tileMapRenderer);

            // characterParent/cluckPrefab moved off SceneController onto CharacterManager in
            // Phase 4 (which now owns all player spawning, including character swapping) — only
            // robotParent is still SceneController's to wire. Run Phase 4 > Build All afterward
            // to wire CharacterManager's prefab references.
            var sceneController = managersGO.GetComponent<SceneController>();
            var scSO = new SerializedObject(sceneController);
            scSO.FindProperty("robotParent").objectReferenceValue = robotParent;
            scSO.ApplyModifiedPropertiesWithoutUndo();

            // GameObject.Find only matches ACTIVE objects — once Phase2Test is disabled (by a
            // later phase's builder, or SceneCleanupBuilder), a re-run of this method couldn't find
            // it and spawned a second active instance every time (see the "black tiles" duplicate-
            // debug-overlay bug). Resources.FindObjectsOfTypeAll also matches inactive instances —
            // same fix Phase5ProjectBuilder already applies to its own Phase5Test/LevelSelectTest.
            var existingPhase2Test = Resources.FindObjectsOfTypeAll<Phase2Test>()
                .FirstOrDefault(t => !EditorUtility.IsPersistent(t.gameObject));
            if (existingPhase2Test == null)
            {
                new GameObject("Phase2Test").AddComponent<Phase2Test>();
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
    }
}
