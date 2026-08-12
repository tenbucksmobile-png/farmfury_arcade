using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Enemies;
using FarmFuryArcade.Gameplay;
using FarmFuryArcade.Utilities;
using Object = UnityEngine.Object;

namespace FarmFuryArcade.EditorTools
{
    /// <summary>
    /// Phase 3 scaffolding: builds the 6 robot prefabs + RobotData assets, adds PlayerHealth to
    /// the existing Cluck prefab, wires RobotSpawner/PowerPelletManager/ChaseScoreManager onto
    /// Game.unity's GameManagers, gives LevelData_01 its 2 spec'd robot spawns, and creates
    /// LevelData_RobotTest (3 robots) for later testing. Safe to re-run, same convention as
    /// Phase1ProjectBuilder/Phase2ProjectBuilder.
    /// </summary>
    public static class Phase3ProjectBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string RobotPrefabFolder = "Assets/_Project/Prefabs/Robots";
        private const string RobotDataFolder = "Assets/_Project/ScriptableObjects/Resources/Robots";
        private const string CluckPrefabPath = "Assets/_Project/Prefabs/Characters/Cluck.prefab";
        private const string LevelData01Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_01.asset";
        // Was LevelData_05.asset/levelNumber 4 — that slot is now Phase2ProjectBuilder's real
        // "Corn Field - 05" maze (see its own doc comment for the bug this used to cause: this test
        // maze was silently player-reachable as "Level 5" in Level Select, showing as a mostly-open
        // field with no interior walls). Renamed to its own file with an out-of-range levelNumber
        // (-1) so DataManager still loads it (for the Phase3Test debug button below) but
        // LevelSelectController's 0-99 tile range never sees it.
        private const string LevelDataRobotTestPath = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_RobotTest.asset";

        [MenuItem("Farm Fury Arcade/Phase 3/Build All")]
        public static void BuildAll()
        {
            BuildRobotData();

            GameObject harvesterPrefab = BuildRobotPrefab("Robot_Harvester", typeof(HarvesterRobot), new Color(0.86f, 0.16f, 0.16f));
            GameObject scoutPrefab = BuildRobotPrefab("Robot_Scout", typeof(ScoutRobot), new Color(0.98f, 0.55f, 0.75f));
            GameObject patrolPrefab = BuildRobotPrefab("Robot_Patrol", typeof(PatrolRobot), new Color(0.20f, 0.80f, 0.85f));
            GameObject drifterPrefab = BuildRobotPrefab("Robot_Drifter", typeof(DrifterRobot), new Color(0.95f, 0.55f, 0.15f));
            GameObject heavyPrefab = BuildRobotPrefab("Robot_Heavy", typeof(HeavyRobot), new Color(0.55f, 0.55f, 0.58f));
            GameObject dronePrefab = BuildRobotPrefab("Robot_Drone", typeof(DroneRobot), new Color(0.62f, 0.20f, 0.86f));

            AddPlayerHealthToCluck();
            UpdateLevelData01Robots();
            BuildLevelData05();
            AssignRobotSpawnsToRemainingLevels();

            WireScene(harvesterPrefab, scoutPrefab, patrolPrefab, drifterPrefab, heavyPrefab, dronePrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Phase3ProjectBuilder] Phase 3 robot prefabs, RobotData, LevelData_01 plus robot spawns for every other real level, LevelData_RobotTest, and Game.unity wiring complete.");
        }

        private static GameObject BuildRobotPrefab(string name, System.Type robotComponentType, Color color)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(color);
            sr.sortingOrder = 5;
            go.transform.localScale = Vector3.one * TileMapRenderer.CellSize;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            // Same kinematic-trigger gotcha as Cluck (see CLAUDE.md) — needed for robots to fire
            // OnTriggerEnter2D against static colliders like WarpTunnel that have no Rigidbody2D.
            rb.useFullKinematicContacts = true;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.4f;

            go.AddComponent(robotComponentType);
            var visual = go.AddComponent<RobotVisual>();
            visual.SetNormalColor(color);

            return SaveAndDestroy(go, RobotPrefabFolder + "/" + name + ".prefab");
        }

        private static GameObject SaveAndDestroy(GameObject go, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // See Phase4ProjectBuilder.EmbedRuntimePlaceholderSprites — PlaceholderSprite.Get()
            // sprites are runtime-only and get silently nulled out by SaveAsPrefabAsset unless
            // embedded as a real sub-asset first.
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

        // Reduced from a uniform 3.5 (~0.57x) to match the character speed cut in
        // Phase4ProjectBuilder.BuildCharacterData — robots were substantially outrunning the
        // player at the old value once characters were slowed down.
        private static void BuildRobotData()
        {
            BuildRobotDataAsset(RobotType.Harvester, "Harvester", 2.0f, AIBehaviourType.Chase, 1);
            BuildRobotDataAsset(RobotType.Scout, "Scout", 2.0f, AIBehaviourType.Predict, 1);
            BuildRobotDataAsset(RobotType.Patrol, "Patrol", 2.0f, AIBehaviourType.Coordinate, 1);
            BuildRobotDataAsset(RobotType.Drifter, "Drifter", 2.0f, AIBehaviourType.Random, 1);
            BuildRobotDataAsset(RobotType.Heavy, "Heavy", 2.0f, AIBehaviourType.Tank, 2);
            BuildRobotDataAsset(RobotType.Drone, "Drone", 2.0f, AIBehaviourType.Fly, 1);
        }

        private static void BuildRobotDataAsset(RobotType type, string displayName, float speed, AIBehaviourType behaviour, int healthPoints)
        {
            string path = $"{RobotDataFolder}/RobotData_{type}.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var data = AssetDatabase.LoadAssetAtPath<RobotData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<RobotData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.robotType = type;
            data.displayName = displayName;
            data.movementSpeed = speed;
            data.behaviour = behaviour;
            data.healthPoints = healthPoints;

            EditorUtility.SetDirty(data);
        }

        private static void AddPlayerHealthToCluck()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CluckPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Phase3ProjectBuilder] Cluck prefab not found — run Phase 2 > Build All first.");
                return;
            }

            if (prefab.GetComponent<PlayerHealth>() != null)
            {
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(CluckPrefabPath);
            if (contents.GetComponent<PlayerHealth>() == null)
            {
                contents.AddComponent<PlayerHealth>();
            }
            PrefabUtility.SaveAsPrefabAsset(contents, CluckPrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        /// <summary>Adds the 2 spawns spec'd for Phase 3 verification: Harvester after 2s, Scout
        /// after 6s, both at LevelData_01's own robotFactoryPosition (the maze's factory box
        /// centre — Phase2ProjectBuilder.BuildLevelData01 now derives this from wherever the
        /// hand-authored maze painted tile id 6, rather than a hardcoded fx0/fx1/fy0/fy1 box) so
        /// this stays correct automatically if the maze is redesigned again. Only touches the
        /// robotSpawns field, leaving the rest of the hand-authored L01 maze untouched.</summary>
        private static void UpdateLevelData01Robots()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelData01Path);
            if (level == null)
            {
                Debug.LogWarning("[Phase3ProjectBuilder] LevelData_01 not found — run Phase 2 > Build All first.");
                return;
            }

            var factoryCenter = level.robotFactoryPosition;
            level.robotSpawns = new[]
            {
                new RobotSpawnData { robotType = RobotType.Harvester, spawnDelay = 2f, spawnPosition = factoryCenter },
                new RobotSpawnData { robotType = RobotType.Scout, spawnDelay = 6f, spawnPosition = factoryCenter }
            };

            EditorUtility.SetDirty(level);
        }

        /// <summary>A second, smaller level purely for exercising 3 robots together (Harvester,
        /// Scout, Patrol) — deliberately kept out of the level-select flow via an out-of-range
        /// levelNumber (see LevelDataRobotTestPath's doc comment above); just a DataManager-loadable
        /// asset for manual/automated Phase 3 testing.</summary>
        private static void BuildLevelData05()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelDataRobotTestPath);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(level, LevelDataRobotTestPath);
            }

            const int width = 20;
            const int height = 20;
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

            const int warpRow = 6;
            for (int x = 1; x < width - 1; x++)
            {
                grid[x, warpRow] = 0;
            }
            grid[0, warpRow] = 5;
            grid[width - 1, warpRow] = 5;

            const int fx0 = 7, fx1 = 12, fy0 = 9, fy1 = 13;
            for (int x = fx0; x <= fx1; x++)
            {
                for (int y = fy0; y <= fy1; y++)
                {
                    grid[x, y] = 6;
                }
            }

            var playerStart = new Vector2Int(10, 2);
            for (int x = 9; x <= 11; x++)
            {
                for (int y = 1; y <= 3; y++)
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

            int kernelCount = Mathf.Min(80, floorCells.Count);
            for (int i = 0; i < kernelCount; i++)
            {
                var c = floorCells[i];
                grid[c.x, c.y] = 2;
            }

            grid[pelletA.x, pelletA.y] = 4;
            grid[pelletB.x, pelletB.y] = 4;

            var factoryCenter = new Vector2Int((fx0 + fx1) / 2, (fy0 + fy1) / 2);

            level.levelNumber = -1;
            level.levelName = "Robot Test Field";
            level.mazeType = MazeType.CornField;
            level.SetMazeLayout(grid);
            level.playerStartPosition = playerStart;
            level.robotFactoryPosition = factoryCenter;
            level.baseCharacterSpeed = 4.0f;
            level.baseRobotSpeed = 3.5f;
            level.warpTunnelRows = new[] { warpRow };
            level.totalCropsRequired = kernelCount + 2;
            level.robotSpawns = new[]
            {
                new RobotSpawnData { robotType = RobotType.Harvester, spawnDelay = 0f, spawnPosition = factoryCenter },
                new RobotSpawnData { robotType = RobotType.Scout, spawnDelay = 3f, spawnPosition = factoryCenter },
                new RobotSpawnData { robotType = RobotType.Patrol, spawnDelay = 6f, spawnPosition = factoryCenter }
            };

            EditorUtility.SetDirty(level);
        }

        private const string LevelsFolder = "Assets/_Project/ScriptableObjects/Resources/Levels";

        /// <summary>Robot type mix in escalating difficulty order — used to pick the first N types
        /// for a level's robot count. Heavy/Drone (tankier/wall-ignoring) are held back for a
        /// world's own later levels rather than appearing from level 1.</summary>
        private static readonly RobotType[] DifficultyOrder =
        {
            RobotType.Harvester, RobotType.Scout, RobotType.Patrol, RobotType.Drifter, RobotType.Heavy, RobotType.Drone
        };

        /// <summary>Every real level except LevelData_01 (hand-tuned by UpdateLevelData01Robots)
        /// used to ship with `robotSpawns = new RobotSpawnData[0]` — Phase2ProjectBuilder.BuildLevel
        /// always sets that as a placeholder ("No robots yet — Phase 3") and nothing ever filled it
        /// in for levels 02-04/06-50, so no robots ever spawned there. Applies a difficulty curve
        /// that resets per world (position-in-world = levelNumber % 25, matching
        /// UnlockProgression.LevelsPerWorld's own convention): 2 robots for a world's first 5
        /// levels, 3 for the next 7, 4 for the next 7, 5 for the last 6 — all spawned at the level's
        /// own robotFactoryPosition (derived from the maze, never hardcoded), staggered 4s apart
        /// starting at 2s, matching LevelData_01's own 2s/6s Harvester/Scout timing.</summary>
        private static void AssignRobotSpawnsToRemainingLevels()
        {
            for (int n = 1; n <= 50; n++)
            {
                // LevelData_01 is hand-tuned separately by UpdateLevelData01Robots. LevelData_05
                // used to be skipped here too (it held the reserved robot-test maze, not a real
                // progression level) — it's now a real Phase2ProjectBuilder-generated maze like
                // every other level and needs the same spawn-curve treatment.
                if (n == 1)
                {
                    continue;
                }

                string path = $"{LevelsFolder}/LevelData_{n:00}.asset";
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level == null)
                {
                    continue;
                }

                int positionInWorld = level.levelNumber % 25;
                int robotCount = positionInWorld switch
                {
                    < 5 => 2,
                    < 12 => 3,
                    < 19 => 4,
                    _ => 5
                };

                var factory = level.robotFactoryPosition;
                var spawns = new RobotSpawnData[robotCount];
                for (int i = 0; i < robotCount; i++)
                {
                    spawns[i] = new RobotSpawnData
                    {
                        robotType = DifficultyOrder[i],
                        spawnDelay = 2f + i * 4f,
                        spawnPosition = factory
                    };
                }

                level.robotSpawns = spawns;
                EditorUtility.SetDirty(level);
            }
        }

        private static void WireScene(GameObject harvesterPrefab, GameObject scoutPrefab, GameObject patrolPrefab,
            GameObject drifterPrefab, GameObject heavyPrefab, GameObject dronePrefab)
        {
            EditorSceneManager.OpenScene(ScenePath);

            var managersGO = GameObject.Find("GameManagers");
            var robotParent = GameObject.Find("RobotParent")?.transform;
            var tileMapRenderer = managersGO.GetComponent<TileMapRenderer>();

            if (managersGO.GetComponent<PowerPelletManager>() == null)
            {
                managersGO.AddComponent<PowerPelletManager>();
            }
            if (managersGO.GetComponent<ChaseScoreManager>() == null)
            {
                managersGO.AddComponent<ChaseScoreManager>();
            }

            var spawner = managersGO.GetComponent<RobotSpawner>();
            if (spawner == null)
            {
                spawner = managersGO.AddComponent<RobotSpawner>();
            }

            var spawnerSO = new SerializedObject(spawner);
            spawnerSO.FindProperty("robotParent").objectReferenceValue = robotParent;
            spawnerSO.FindProperty("tileMap").objectReferenceValue = tileMapRenderer;
            spawnerSO.FindProperty("harvesterPrefab").objectReferenceValue = harvesterPrefab;
            spawnerSO.FindProperty("scoutPrefab").objectReferenceValue = scoutPrefab;
            spawnerSO.FindProperty("patrolPrefab").objectReferenceValue = patrolPrefab;
            spawnerSO.FindProperty("drifterPrefab").objectReferenceValue = drifterPrefab;
            spawnerSO.FindProperty("heavyPrefab").objectReferenceValue = heavyPrefab;
            spawnerSO.FindProperty("dronePrefab").objectReferenceValue = dronePrefab;
            spawnerSO.ApplyModifiedPropertiesWithoutUndo();

            var sceneController = managersGO.GetComponent<SceneController>();
            var scSO = new SerializedObject(sceneController);
            scSO.FindProperty("robotSpawner").objectReferenceValue = spawner;
            scSO.ApplyModifiedPropertiesWithoutUndo();

            // See Phase2ProjectBuilder's matching comment — GameObject.Find only matches active
            // objects, so once Phase3Test is disabled a plain Find-or-create re-spawns a duplicate.
            var existingPhase3Test = Resources.FindObjectsOfTypeAll<Phase3Test>()
                .FirstOrDefault(t => !EditorUtility.IsPersistent(t.gameObject));
            if (existingPhase3Test == null)
            {
                new GameObject("Phase3Test").AddComponent<Phase3Test>();
            }

            // Phase1Test/Phase2Test/Phase3Test all auto-run on Start() and each independently
            // calls GameManager.Instance.LoadLevel(0), which destroys and recreates the Cluck
            // GameObject. With all three coexisting, their coroutines race on that reload and can
            // end up sharing/losing track of the player instance mid-test (observed as flaky
            // wrong-position failures in Phase3Test's death/respawn check). Phase3Test's own
            // battery already exercises Phase 1/2 functionality (LoadLevel, movement, crops,
            // warp) as a side effect, so disable the older harnesses' auto-run rather than
            // deleting them — they're still reachable via their ContextMenu/OnGUI for manual
            // regression checks.
            DisableRunOnStart("Phase1Test");
            DisableRunOnStart("Phase2Test");

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        private static void DisableRunOnStart(string gameObjectName)
        {
            var go = GameObject.Find(gameObjectName);
            if (go == null)
            {
                return;
            }

            var component = go.GetComponents<MonoBehaviour>().Length > 0 ? go.GetComponents<MonoBehaviour>()[0] : null;
            if (component == null)
            {
                return;
            }

            var so = new SerializedObject(component);
            var prop = so.FindProperty("runOnStart");
            if (prop != null)
            {
                prop.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
