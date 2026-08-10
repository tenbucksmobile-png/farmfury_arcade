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
        private const string CharacterDataPath = "Assets/_Project/ScriptableObjects/Resources/Characters/CharacterData_Cluck.asset";
        private const string CharacterPrefabFolder = "Assets/_Project/Prefabs/Characters";
        private const string BlockPrefabFolder = "Assets/_Project/Prefabs/Blocks";

        [MenuItem("Farm Fury Arcade/Phase 2/Build All")]
        public static void BuildAll()
        {
            GameObject wallPrefab = BuildWallPrefab();
            GameObject groundPrefab = BuildGroundPrefab();
            GameObject cropKernelPrefab = BuildCropPrefab("Crop_Corn", CropType.Corn, 10, new Color(0.96f, 0.78f, 0.26f), 0.35f);
            GameObject cropVegetablePrefab = BuildCropPrefab("Crop_Vegetable", CropType.Vegetable, 50, new Color(0.30f, 0.69f, 0.31f), 0.5f);
            GameObject pelletCollectEffectPrefab = BuildPelletCollectEffectPrefab();
            GameObject powerPelletPrefab = BuildPowerPelletPrefab(pelletCollectEffectPrefab);
            GameObject warpTunnelPrefab = BuildWarpTunnelPrefab();
            GameObject cluckPrefab = BuildCluckPrefab();

            BuildCharacterData();
            BuildLevelData01();

            WireScene(wallPrefab, groundPrefab, cropKernelPrefab, cropVegetablePrefab, powerPelletPrefab, warpTunnelPrefab, cluckPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Phase2ProjectBuilder] Phase 2 prefabs, LevelData_01, CharacterData_Cluck, and Game.unity wiring complete.");
        }

        private static GameObject BuildWallPrefab()
        {
            var go = new GameObject("Wall_CornField");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(new Color(0.29f, 0.17f, 0.10f)); // GDD Wall Brown #4A2C1A
            go.transform.localScale = Vector3.one * TileMapRenderer.CellSize;
            go.AddComponent<BoxCollider2D>();
            return SaveAndDestroy(go, BlockPrefabFolder + "/Wall_CornField.prefab");
        }

        private static GameObject BuildGroundPrefab()
        {
            var go = new GameObject("Ground_CornField");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(new Color(0.18f, 0.12f, 0.08f)); // dark soil, visual only
            sr.sortingOrder = -1;
            go.transform.localScale = Vector3.one * TileMapRenderer.CellSize;
            return SaveAndDestroy(go, BlockPrefabFolder + "/Ground_CornField.prefab");
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

        private static GameObject BuildWarpTunnelPrefab()
        {
            var go = new GameObject("WarpTunnel");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(new Color(0.55f, 0.27f, 0.68f)); // placeholder "barn door" purple
            go.transform.localScale = Vector3.one * TileMapRenderer.CellSize;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = Vector2.one * 0.9f;
            go.AddComponent<WarpTunnel>();
            return SaveAndDestroy(go, BlockPrefabFolder + "/WarpTunnel.prefab");
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

        private static void BuildLevelData01()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LevelDataPath)!);
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelDataPath);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(level, LevelDataPath);
            }

            const int width = 12;
            const int height = 9;
            var grid = ParseRows(Rows, width, height);

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

            level.levelNumber = 0;
            level.levelName = "The Corn Field - 01";
            level.mazeType = MazeType.CornField;
            level.SetMazeLayout(grid);
            level.playerStartPosition = playerStart;
            // Robots spawn from the middle of the maze — the factory box's own centre, derived from
            // whatever cells were painted id 6, not a hardcoded position. Keep
            // Phase3ProjectBuilder.UpdateLevelData01Robots's spawnPosition in sync with this if the
            // maze is redesigned again.
            level.robotFactoryPosition = new Vector2Int((factoryMinX + factoryMaxX) / 2, (factoryMinY + factoryMaxY) / 2);
            level.baseCharacterSpeed = 4.0f;
            level.baseRobotSpeed = 3.5f;
            level.robotSpawns = new RobotSpawnData[0]; // No robots yet — Phase 3
            level.warpTunnelRows = warpRows.ToArray();
            // Explicitly cleared, not just left untouched — UpdateLevelData01Water used to set
            // this to stamp a (now-removed, dead-code, never-called) water gate; without resetting
            // it here, a full regeneration would otherwise carry a stale value forward forever
            // (this method rebuilds the grid from scratch, but never used to touch this field).
            level.waterTeleportRows = new int[0];
            level.totalCropsRequired = kernels + vegetables + pellets;

            EditorUtility.SetDirty(level);
        }

        private static void WireScene(GameObject wallPrefab, GameObject groundPrefab, GameObject cropKernelPrefab,
            GameObject cropVegetablePrefab, GameObject powerPelletPrefab, GameObject warpTunnelPrefab, GameObject cluckPrefab)
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
            tileMapSO.FindProperty("wallPrefab").objectReferenceValue = wallPrefab;
            tileMapSO.FindProperty("groundPrefab").objectReferenceValue = groundPrefab;
            tileMapSO.FindProperty("cropKernelPrefab").objectReferenceValue = cropKernelPrefab;
            tileMapSO.FindProperty("cropVegetablePrefab").objectReferenceValue = cropVegetablePrefab;
            tileMapSO.FindProperty("powerPelletPrefab").objectReferenceValue = powerPelletPrefab;
            tileMapSO.FindProperty("warpTunnelPrefab").objectReferenceValue = warpTunnelPrefab;
            tileMapSO.ApplyModifiedPropertiesWithoutUndo();

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
