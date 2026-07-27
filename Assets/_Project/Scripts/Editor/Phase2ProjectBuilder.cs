using System.Collections.Generic;
using System.IO;
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
            GameObject powerPelletPrefab = BuildPowerPelletPrefab();
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
            go.AddComponent<BoxCollider2D>();
            return SaveAndDestroy(go, BlockPrefabFolder + "/Wall_CornField.prefab");
        }

        private static GameObject BuildGroundPrefab()
        {
            var go = new GameObject("Ground_CornField");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(new Color(0.18f, 0.12f, 0.08f)); // dark soil, visual only
            sr.sortingOrder = -1;
            return SaveAndDestroy(go, BlockPrefabFolder + "/Ground_CornField.prefab");
        }

        private static GameObject BuildCropPrefab(string name, CropType cropType, int points, Color color, float scale)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(color);
            go.transform.localScale = Vector3.one * scale;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;
            var pickup = go.AddComponent<CropPickup>();
            pickup.cropType = cropType;
            pickup.points = points;
            return SaveAndDestroy(go, BlockPrefabFolder + "/" + name + ".prefab");
        }

        private static GameObject BuildPowerPelletPrefab()
        {
            var go = new GameObject("Power_Sunflower");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(new Color(1f, 0.765f, 0f)); // GDD Power Sunflower #FFC300
            go.transform.localScale = Vector3.one * 0.7f;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;
            var pickup = go.AddComponent<PowerPelletPickup>();
            pickup.pelletType = PowerPelletType.Sunflower;
            pickup.points = 500;
            return SaveAndDestroy(go, BlockPrefabFolder + "/Power_Sunflower.prefab");
        }

        private static GameObject BuildWarpTunnelPrefab()
        {
            var go = new GameObject("WarpTunnel");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(new Color(0.55f, 0.27f, 0.68f)); // placeholder "barn door" purple
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
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
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

        private static void BuildLevelData01()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LevelDataPath)!);
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelDataPath);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(level, LevelDataPath);
            }

            const int width = 28;
            const int height = 31;
            var grid = new int[width, height];

            for (int x = 0; x < width; x++)
            {
                grid[x, 0] = 1;
                grid[x, height - 1] = 1;
            }
            for (int y = 0; y < height; y++)
            {
                grid[0, y] = 1;
                grid[width - 1, y] = 1;
            }

            const int warpRow = 9;
            for (int x = 1; x < width - 1; x++)
            {
                grid[x, warpRow] = 0;
            }
            grid[0, warpRow] = 5;
            grid[width - 1, warpRow] = 5;

            const int fx0 = 10, fx1 = 17, fy0 = 13, fy1 = 18;
            for (int x = fx0; x <= fx1; x++)
            {
                for (int y = fy0; y <= fy1; y++)
                {
                    grid[x, y] = 6;
                }
            }

            for (int bx = 3; bx <= width - 5; bx += 5)
            {
                for (int by = 3; by <= height - 5; by += 6)
                {
                    if (by == warpRow || by + 1 == warpRow)
                    {
                        continue;
                    }
                    if (grid[bx, by] != 0 || grid[bx + 1, by] != 0 || grid[bx, by + 1] != 0 || grid[bx + 1, by + 1] != 0)
                    {
                        continue;
                    }
                    grid[bx, by] = 1;
                    grid[bx + 1, by] = 1;
                    grid[bx, by + 1] = 1;
                    grid[bx + 1, by + 1] = 1;
                }
            }

            var playerStart = new Vector2Int(14, 3);
            for (int x = 13; x <= 15; x++)
            {
                for (int y = 2; y <= 4; y++)
                {
                    grid[x, y] = 0;
                }
            }
            grid[playerStart.x, playerStart.y] = 7;

            var pelletA = new Vector2Int(2, 2);
            var pelletB = new Vector2Int(width - 3, height - 3);

            var floorCells = new List<Vector2Int>();
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    if (grid[x, y] != 0)
                    {
                        continue;
                    }
                    var cell = new Vector2Int(x, y);
                    if (cell == playerStart || cell == pelletA || cell == pelletB)
                    {
                        continue;
                    }
                    floorCells.Add(cell);
                }
            }

            int kernelCount = Mathf.Min(200, floorCells.Count);
            int vegetableCount = Mathf.Min(20, Mathf.Max(0, floorCells.Count - kernelCount));

            for (int i = 0; i < kernelCount; i++)
            {
                var c = floorCells[i];
                grid[c.x, c.y] = 2;
            }
            for (int i = kernelCount; i < kernelCount + vegetableCount; i++)
            {
                var c = floorCells[i];
                grid[c.x, c.y] = 3;
            }

            grid[pelletA.x, pelletA.y] = 4;
            grid[pelletB.x, pelletB.y] = 4;

            level.levelNumber = 0;
            level.levelName = "The Corn Field - 01";
            level.mazeType = MazeType.CornField;
            level.SetMazeLayout(grid);
            level.playerStartPosition = playerStart;
            level.robotFactoryPosition = new Vector2Int((fx0 + fx1) / 2, (fy0 + fy1) / 2);
            level.baseCharacterSpeed = 4.0f;
            level.baseRobotSpeed = 3.5f;
            level.robotSpawns = new RobotSpawnData[0]; // No robots yet — Phase 3
            level.warpTunnelRows = new[] { warpRow };
            level.totalCropsRequired = kernelCount + vegetableCount + 2;

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

            if (GameObject.Find("Phase2Test") == null)
            {
                new GameObject("Phase2Test").AddComponent<Phase2Test>();
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
    }
}
