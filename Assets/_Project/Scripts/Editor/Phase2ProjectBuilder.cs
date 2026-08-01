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

        /// <summary>Deterministic seed for the recursive-backtracker maze generator below — keeps
        /// LevelData_01 identical across re-runs of Build All, matching every other builder
        /// method's "safe to re-run" idempotency.</summary>
        private const int MazeSeed = 20260728;

        /// <summary>Regenerates LevelData_01 as an actual Pac-Man-style corridor maze (1-tile-wide
        /// paths + wall blocks) instead of the old sparse-2x2-blocks-on-open-floor layout, which
        /// read as barely a maze at all once real corn-tile floor/wall art landed. Built via a
        /// randomized recursive backtracker over the LEFT half only (x = 1..leftHalfMax), then
        /// mirrored onto the right half (width-1-x) for a classic symmetric arcade-maze look — an
        /// even `width` splits cleanly into two equal halves with no leftover center column. A
        /// handful of extra connector walls are re-opened afterward so the board has loops
        /// (multiple routes around robots) rather than being a single spanning tree with only one
        /// path anywhere. The two warp rows, robot factory box, and player-start clearing are then
        /// stamped on top, exactly where Phase3ProjectBuilder.UpdateLevelData01Robots's hardcoded
        /// robot spawn position expects them, so that builder doesn't need touching when only this
        /// method's own constants change — but it DOES need to change together whenever
        /// `width`/`height` change, since that coordinate isn't derived from these constants
        /// automatically.
        ///
        /// `width`/`height` are 12x9, this game's fixed maze size for every level (hardcoded here,
        /// not a per-level choice) — reduced from 14x16 without compensating by enlarging each
        /// tile, unlike the earlier 28x31 -> 14x16 halving, which doubled tile size specifically to
        /// keep the board's total footprint the same. This time the board is deliberately smaller on
        /// screen, showing more of GameplayBackdrop's art around it. Tile size itself doesn't
        /// actually depend on these constants at all — CameraFollow.ApplyOrthographicSizeForAspect
        /// derives orthographicSize purely from CellScreenHeightFraction, a fixed tile-size-to-
        /// screen-height ratio independent of maze dimensions — so no camera-side changes were
        /// needed for this resize.</summary>
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
            var grid = new int[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    grid[x, y] = 1; // fully walled; corridors carved below
                }
            }

            // Full-width carve, not "carve one half and mirror" — the mirror approach (left half
            // only, leftHalfMax=(width-2)/2, then grid[width-1-x,y]=grid[x,y]) put the seam between
            // the two halves at two ADJACENT room columns (x=5 and its mirror x=6 at this board's
            // width) with no wall ever separating them, since every room cell is unconditionally
            // open (that's what "every cell is part of the spanning tree" means). The result was a
            // permanent 2-tile-wide corridor straight down the middle, and — because a 12-wide board
            // only fit 3 room columns per half — most of the board read as wide-open rather than
            // single-tile corridors. A full-width backtracker (room columns at every odd x from 1 to
            // width-2, no mirroring) gives 5 room columns with normal single-tile spacing between
            // them and no seam duplication, at the cost of the left/right symmetry the mirror used
            // to guarantee — correctness over cosmetics per playtest feedback.
            CarveMazeCorridors(grid, width, height);

            // GridToWorld maps grid.y directly to world Y with no flip, so higher y is higher on
            // screen — row N counting from the top is y = height - N. Rows 3 and 7 from the top
            // (spec'd directly, not derived) land on y=6 and y=2: one above the robot factory box,
            // one below it, each with a warp-tunnel edge (id 5) at both x=0 and x=width-1.
            // WarpTunnel teleports on trigger overlap (instant, see WarpTunnel.OnTriggerEnter2D) —
            // it never required a walkable lane spanning the whole row. This used to force the
            // ENTIRE row open (x=1..width-2), bulldozing whatever 1-tile-wide corridors
            // CarveMazeCorridors had already carved there and leaving two full-width open bands
            // across the board — exactly the "completely open in the middle" maze look flagged in
            // playtest. Now it only opens the single interior tile next to each border edge (just
            // enough for the tunnel tile itself to be reachable); everything else in the row keeps
            // whatever wall/corridor pattern the maze generator produced.
            int warpRowTop = height - 3;
            int warpRowBottom = height - 7;
            foreach (int warpRow in new[] { warpRowTop, warpRowBottom })
            {
                grid[1, warpRow] = 0;
                grid[width - 2, warpRow] = 0;
                grid[0, warpRow] = 5;
                grid[width - 1, warpRow] = 5;
            }

            // Sits strictly between the two warp rows (y=3..5, vs warp rows at y=2/y=6) so stamping
            // it doesn't overwrite either tunnel edge.
            const int fx0 = 4, fx1 = 7, fy0 = 3, fy1 = 5;
            for (int x = fx0; x <= fx1; x++)
            {
                for (int y = fy0; y <= fy1; y++)
                {
                    grid[x, y] = 6;
                }
            }

            // Bottommost interior row (y=1, just above the y=0 wall row) — the only row left once
            // the factory box and both warp rows claim the rest of this much shorter 7-interior-row
            // board.
            var playerStart = new Vector2Int(6, 1);
            for (int x = 5; x <= 7; x++)
            {
                grid[x, 1] = 0;
            }
            grid[playerStart.x, playerStart.y] = 7;

            // Power pellets and vegetables are scattered randomly across every open floor tile
            // (deterministically — same MazeSeed-derived RNG as corridor carving, so rebuilds stay
            // reproducible) rather than fixed one-per-corner pellets / two hand-anchored BFS
            // vegetable clusters. Every remaining open tile becomes a corn kernel in the fill pass
            // below regardless, so this is really just "re-roll some of those kernel tiles as
            // something rarer, picked at random."
            var openFloor = new List<Vector2Int>();
            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    if (grid[x, y] == 0)
                    {
                        openFloor.Add(new Vector2Int(x, y));
                    }
                }
            }

            var scatterRng = new System.Random(MazeSeed + 1);
            for (int i = openFloor.Count - 1; i > 0; i--)
            {
                int j = scatterRng.Next(i + 1);
                (openFloor[i], openFloor[j]) = (openFloor[j], openFloor[i]);
            }

            const int pelletCount = 4;
            const int vegetableCount = 10;
            int scatterIndex = 0;
            for (int i = 0; i < pelletCount && scatterIndex < openFloor.Count; i++, scatterIndex++)
            {
                var c = openFloor[scatterIndex];
                grid[c.x, c.y] = 4;
            }
            for (int i = 0; i < vegetableCount && scatterIndex < openFloor.Count; i++, scatterIndex++)
            {
                var c = openFloor[scatterIndex];
                grid[c.x, c.y] = 3;
            }

            // Every remaining open corridor tile gets a crop kernel — the classic "dot in every
            // lane" Pac-Man look, and simplest way to guarantee no leftover unused floor tiles.
            int kernels = 0, vegetables = 0, pellets = 0;
            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    switch (grid[x, y])
                    {
                        case 0:
                            grid[x, y] = 2;
                            kernels++;
                            break;
                        case 2:
                            kernels++;
                            break;
                        case 3:
                            vegetables++;
                            break;
                        case 4:
                            pellets++;
                            break;
                    }
                }
            }

            level.levelNumber = 0;
            level.levelName = "The Corn Field - 01";
            level.mazeType = MazeType.CornField;
            level.SetMazeLayout(grid);
            level.playerStartPosition = playerStart;
            level.robotFactoryPosition = new Vector2Int((fx0 + fx1) / 2, (fy0 + fy1) / 2);
            level.baseCharacterSpeed = 4.0f;
            level.baseRobotSpeed = 3.5f;
            level.robotSpawns = new RobotSpawnData[0]; // No robots yet — Phase 3
            level.warpTunnelRows = new[] { warpRowTop, warpRowBottom };
            // Explicitly cleared, not just left untouched — UpdateLevelData01Water used to set
            // this to stamp a (now-removed, dead-code, never-called) water gate; without resetting
            // it here, a full regeneration would otherwise carry a stale value forward forever
            // (this method rebuilds the grid from scratch, but never used to touch this field).
            level.waterTeleportRows = new int[0];
            level.totalCropsRequired = kernels + vegetables + pellets;

            EditorUtility.SetDirty(level);
        }

        /// <summary>Fraction of remaining connector walls reopened after the spanning tree is
        /// carved, adding loops back into what would otherwise be a maze with exactly one path
        /// between any two points. Dropped from 0.22 to 0.05 per playtest feedback: robots read as
        /// "falling into a loop of going in one line" — the wide multi-path open areas the higher
        /// value produced gave robot AI long straight runs with no real branching decision to make.
        /// Kept slightly above zero (a pure spanning tree) rather than 0, so a few loops still exist
        /// for the player to escape into — "only single paths" as the dominant character of the
        /// board, not literally every last connector sealed.</summary>
        private const double LoopReopenChance = 0.05;

        /// <summary>Randomized recursive backtracker over the FULL-width cell lattice (odd x in
        /// 1..width-2, odd y in 1..height-2 are "room" cells; the even coordinate between two
        /// adjacent visited cells is the connector carved open to join them). Produces a spanning
        /// tree of 1-tile-wide corridors, then reopens LoopReopenChance of the remaining connector
        /// walls so the maze has a few loops instead of exactly one path between any two points.
        /// Previously ran over the left half only and mirrored the result — see BuildLevelData01's
        /// call site for why that produced a permanent 2-tile-wide seam down the middle instead of
        /// genuine single-tile corridors.</summary>
        private static void CarveMazeCorridors(int[,] grid, int width, int height)
        {
            var rng = new System.Random(MazeSeed);
            var visited = new bool[width, height];
            var cellStack = new Stack<Vector2Int>();
            var start = new Vector2Int(1, 1);
            grid[start.x, start.y] = 0;
            visited[start.x, start.y] = true;
            cellStack.Push(start);

            Vector2Int[] cellDirs = { new Vector2Int(2, 0), new Vector2Int(-2, 0), new Vector2Int(0, 2), new Vector2Int(0, -2) };
            var neighbors = new List<Vector2Int>();

            while (cellStack.Count > 0)
            {
                var current = cellStack.Peek();
                neighbors.Clear();
                foreach (var d in cellDirs)
                {
                    var next = current + d;
                    if (next.x >= 1 && next.x <= width - 2 && next.y >= 1 && next.y <= height - 2 && !visited[next.x, next.y])
                    {
                        neighbors.Add(next);
                    }
                }

                if (neighbors.Count == 0)
                {
                    cellStack.Pop();
                    continue;
                }

                var chosen = neighbors[rng.Next(neighbors.Count)];
                var between = new Vector2Int((current.x + chosen.x) / 2, (current.y + chosen.y) / 2);
                grid[chosen.x, chosen.y] = 0;
                grid[between.x, between.y] = 0;
                visited[chosen.x, chosen.y] = true;
                cellStack.Push(chosen);
            }

            // Horizontal connectors sit at even x, odd y (between two horizontally-adjacent cells).
            for (int x = 2; x < width - 2; x += 2)
            {
                for (int y = 1; y <= height - 2; y += 2)
                {
                    if (grid[x, y] == 1 && grid[x - 1, y] == 0 && grid[x + 1, y] == 0 && rng.NextDouble() < LoopReopenChance)
                    {
                        grid[x, y] = 0;
                    }
                }
            }
            // Vertical connectors sit at odd x, even y (between two vertically-adjacent cells).
            for (int x = 1; x <= width - 2; x += 2)
            {
                for (int y = 2; y <= height - 3; y += 2)
                {
                    if (grid[x, y] == 1 && grid[x, y - 1] == 0 && grid[x, y + 1] == 0 && rng.NextDouble() < LoopReopenChance)
                    {
                        grid[x, y] = 0;
                    }
                }
            }
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
