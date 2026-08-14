using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FarmFuryArcade.Abilities;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;
using FarmFuryArcade.Utilities;
using Object = UnityEngine.Object;

namespace FarmFuryArcade.EditorTools
{
    /// <summary>
    /// Phase 4 scaffolding: builds CharacterData for all 8 characters, adds CharacterBase +
    /// EggDropAbility to the existing Cluck prefab, builds the 7 remaining character prefabs plus
    /// every ability's sub-prefab (egg, shockwave, wool clone, water tile — the water tile prefab
    /// is built but no longer stamped onto LevelData_01, see UpdateLevelData01Water's own doc
    /// comment), and wires CharacterManager/ComboSystem/UnlockManager/CameraShake into Game.unity
    /// (ChooseCharacterScreen, its Phase 5 replacement, is wired by Phase5ProjectBuilder instead).
    /// Safe to re-run. Depends on Phase 2 (Cluck prefab, LevelData_01) and Phase 3 (robot types,
    /// for RearKick/PuffUp/GroundSlam to find at runtime — no direct build-time dependency).
    /// </summary>
    public static class Phase4ProjectBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string CharacterPrefabFolder = "Assets/_Project/Prefabs/Characters";
        private const string AbilityPrefabFolder = "Assets/_Project/Prefabs/Abilities";
        private const string CharacterDataFolder = "Assets/_Project/ScriptableObjects/Resources/Characters";
        private const string CluckPrefabPath = "Assets/_Project/Prefabs/Characters/Cluck.prefab";
        private const string LevelData01Path = "Assets/_Project/ScriptableObjects/Resources/Levels/LevelData_01.asset";

        [MenuItem("Farm Fury Arcade/Phase 4/Build All")]
        public static void BuildAll()
        {
            BuildCharacterData();

            GameObject eggPrefab = BuildEggPrefab();
            GameObject shockwavePrefab = BuildShockwavePrefab();
            GameObject bounceTrailPrefab = BuildBounceTrailPrefab();
            GameObject woolClonePrefab = BuildWoolClonePrefab();
            GameObject waterTilePrefab = BuildWaterTilePrefab();
            GameObject duckySplashPrefab = BuildDuckySplashPrefab();
            GameObject horaceBuckPrefab = BuildHoraceBuckPrefab();

            GameObject cluckPrefab = AddCharacterBaseAndAbilityToCluck(eggPrefab);

            GameObject bessiePrefab = BuildCharacterPrefab("Bessie", new Color(0.55f, 0.38f, 0.20f),
                typeof(GroundSlamAbility), 10f, ability =>
                {
                    var so = new SerializedObject(ability);
                    so.FindProperty("shockwavePrefab").objectReferenceValue = shockwavePrefab;
                    so.ApplyModifiedPropertiesWithoutUndo();
                });

            GameObject percyPrefab = BuildCharacterPrefab("Percy", new Color(0.95f, 0.55f, 0.65f),
                typeof(BounceRollAbility), 10f, ability =>
                {
                    var so = new SerializedObject(ability);
                    so.FindProperty("trailPrefab").objectReferenceValue = bounceTrailPrefab;
                    so.ApplyModifiedPropertiesWithoutUndo();
                });

            GameObject woollyPrefab = BuildCharacterPrefab("Woolly", new Color(0.92f, 0.90f, 0.80f),
                typeof(TripleCloneAbility), 10f, ability =>
                {
                    var so = new SerializedObject(ability);
                    so.FindProperty("clonePrefab").objectReferenceValue = woolClonePrefab;
                    so.FindProperty("eggPrefab").objectReferenceValue = eggPrefab;
                    so.ApplyModifiedPropertiesWithoutUndo();
                });

            GameObject duckyPrefab = BuildCharacterPrefab("Ducky", new Color(0.25f, 0.65f, 0.75f),
                typeof(SkipShotAbility), 10f, ability =>
                {
                    var so = new SerializedObject(ability);
                    so.FindProperty("woolClonePrefab").objectReferenceValue = woolClonePrefab;
                    so.FindProperty("splashEffectPrefab").objectReferenceValue = duckySplashPrefab;
                    so.ApplyModifiedPropertiesWithoutUndo();
                });

            GameObject horacePrefab = BuildCharacterPrefab("Horace", new Color(0.45f, 0.30f, 0.15f),
                typeof(RearKickAbility), 10f, ability =>
                {
                    var so = new SerializedObject(ability);
                    so.FindProperty("buckEffectPrefab").objectReferenceValue = horaceBuckPrefab;
                    so.ApplyModifiedPropertiesWithoutUndo();
                });

            GameObject geraldPrefab = BuildCharacterPrefab("Gerald", new Color(0.65f, 0.60f, 0.50f),
                typeof(PuffUpAbility), 10f, null);

            GameObject billyPrefab = BuildCharacterPrefab("Billy", new Color(0.35f, 0.35f, 0.38f),
                typeof(HeadbuttThroughAbility), 10f, null);

            // UpdateLevelData01Water() is no longer called — the water gate at row 11 (cells
            // (3,11)/(10,11)) rendered as a plain blue placeholder square (no real water art was
            // ever uploaded) and read as an invisible wall/bug rather than a Ducky-only crossing
            // mechanic. Removed per feedback rather than left half-implemented; the method, the
            // WaterTile prefab, and SkipShotAbility are all kept (same "built but unlinked"
            // treatment as Store/Roster/Leaderboards) in case real water art lands later and this
            // gets reinstated.

            WireScene(cluckPrefab, bessiePrefab, percyPrefab, woollyPrefab, duckyPrefab,
                horacePrefab, geraldPrefab, billyPrefab, waterTilePrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Phase4ProjectBuilder] Phase 4 character prefabs, CharacterData, ability sub-prefabs, " +
                      "and Game.unity wiring complete.");
        }

        // ---- Character prefabs ----------------------------------------------------------

        private static GameObject BuildCharacterPrefab(string name, Color color, System.Type abilityType,
            float abilityCooldown, System.Action<Component> configureAbilityOnAsset)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(color);
            sr.sortingOrder = 5;
            go.transform.localScale = Vector3.one * TileMapRenderer.CellSize;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.useFullKinematicContacts = true;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.4f;

            go.AddComponent<GridMovement>();
            go.AddComponent<CropCollector>();
            go.AddComponent<CharacterAnimator>();
            go.AddComponent<PlayerHealth>();
            go.AddComponent<CharacterBase>();
            go.AddComponent(abilityType);

            string path = $"{CharacterPrefabFolder}/{name}.prefab";
            SaveAndDestroy(go, path);

            // Setting fields directly on the GameObject SaveAsPrefabAsset just returned does NOT
            // reliably persist in this Unity version (verified: Bessie/Percy/Horace all came out
            // with unset ability fields and the default totalCooldown despite this exact code
            // running against them). Round-trip through LoadPrefabContents/SaveAsPrefabAsset
            // instead — the same pattern AddCharacterBaseAndAbilityToCluck already uses
            // successfully for Cluck's eggPrefab.
            var contents = PrefabUtility.LoadPrefabContents(path);
            var abilityOnContents = contents.GetComponent(abilityType);
            var abilitySO = new SerializedObject(abilityOnContents);
            abilitySO.FindProperty("totalCooldown").floatValue = abilityCooldown;
            abilitySO.ApplyModifiedPropertiesWithoutUndo();
            configureAbilityOnAsset?.Invoke(abilityOnContents);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>Cluck's prefab already exists from Phase 2/3 — this adds the two components
        /// Phase 4 needs (CharacterBase, EggDropAbility) without touching anything else, same
        /// LoadPrefabContents pattern Phase3ProjectBuilder used to add PlayerHealth.</summary>
        private static GameObject AddCharacterBaseAndAbilityToCluck(GameObject eggPrefab)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CluckPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Phase4ProjectBuilder] Cluck prefab not found — run Phase 2 > Build All first.");
                return null;
            }

            var contents = PrefabUtility.LoadPrefabContents(CluckPrefabPath);
            if (contents.GetComponent<CharacterBase>() == null)
            {
                contents.AddComponent<CharacterBase>();
            }

            var ability = contents.GetComponent<EggDropAbility>();
            if (ability == null)
            {
                ability = contents.AddComponent<EggDropAbility>();
            }

            var abilitySO = new SerializedObject(ability);
            abilitySO.FindProperty("totalCooldown").floatValue = 10f;
            abilitySO.FindProperty("eggPrefab").objectReferenceValue = eggPrefab;
            abilitySO.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, CluckPrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            return AssetDatabase.LoadAssetAtPath<GameObject>(CluckPrefabPath);
        }

        // ---- Ability sub-prefabs ----------------------------------------------------------

        private static GameObject BuildEggPrefab()
        {
            var go = new GameObject("Egg");
            var sr = go.AddComponent<SpriteRenderer>();
            // Pure white at sortingOrder 2 (Phase 4 original) still wasn't visible once real
            // ground/corn art landed — those textures read light/warm enough that a plain white
            // square has poor contrast, the same failure mode as the earlier tan placeholder just
            // with real art instead of a placeholder ground colour. Switched to a saturated hot
            // pink no farm-themed ground art is likely to ever match, and raised sortingOrder above
            // the character sprite (5) rather than just above ground — the offset-0 egg spawns
            // directly under Cluck's own feet (see EggDropAbility's TileOffsetsBehind including 0),
            // so it needs to draw on TOP of her sprite to be visible at all at that position.
            sr.sprite = PlaceholderSprite.Get(new Color(1f, 0.08f, 0.58f));
            sr.sortingOrder = 6;
            go.transform.localScale = Vector3.one * 0.55f * TileMapRenderer.CellSize;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;
            go.AddComponent<EggHazard>();
            return SaveAndDestroy(go, $"{AbilityPrefabFolder}/Egg.prefab");
        }

        private static GameObject BuildShockwavePrefab()
        {
            var go = new GameObject("Shockwave");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(new Color(1f, 1f, 1f, 0.6f));
            sr.sortingOrder = 6;
            go.transform.localScale = Vector3.one * TileMapRenderer.CellSize;
            go.AddComponent<ShockwaveEffect>();
            return SaveAndDestroy(go, $"{AbilityPrefabFolder}/Shockwave.prefab");
        }

        private static GameObject BuildBounceTrailPrefab()
        {
            var go = new GameObject("BounceTrail");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(new Color(0.3f, 0.9f, 0.95f, 0.5f));
            sr.sortingOrder = 4;
            go.transform.localScale = Vector3.one * 0.6f * TileMapRenderer.CellSize;
            return SaveAndDestroy(go, $"{AbilityPrefabFolder}/BounceTrail.prefab");
        }

        private static GameObject BuildDuckySplashPrefab()
        {
            var go = new GameObject("DuckySplash");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(new Color(0.4f, 0.75f, 0.95f, 0.7f));
            sr.sortingOrder = 5;
            go.transform.localScale = Vector3.one * TileMapRenderer.CellSize;
            go.AddComponent<DuckySplashEffect>();
            return SaveAndDestroy(go, $"{AbilityPrefabFolder}/DuckySplash.prefab");
        }

        private static GameObject BuildHoraceBuckPrefab()
        {
            var go = new GameObject("HoraceBuck");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(new Color(0.75f, 0.5f, 0.25f, 0.7f));
            sr.sortingOrder = 5;
            go.transform.localScale = Vector3.one * TileMapRenderer.CellSize;
            go.AddComponent<HoraceBuckEffect>();
            return SaveAndDestroy(go, $"{AbilityPrefabFolder}/HoraceBuck.prefab");
        }

        private static GameObject BuildWoolClonePrefab()
        {
            var go = new GameObject("WoollyClone");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(new Color(0.92f, 0.90f, 0.80f));
            sr.sortingOrder = 5;
            go.transform.localScale = Vector3.one * 0.7f * TileMapRenderer.CellSize; // "smaller Woolly" per spec

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.useFullKinematicContacts = true;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.3f;

            go.AddComponent<CropCollector>();
            go.AddComponent<WoollyClone>();

            return SaveAndDestroy(go, $"{AbilityPrefabFolder}/WoollyClone.prefab");
        }

        private static GameObject BuildWaterTilePrefab()
        {
            var go = new GameObject("WaterTile");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(new Color(0.20f, 0.50f, 0.90f));
            // Must render ABOVE Ground_CornField (sortingOrder -1), which is instantiated under
            // every non-wall cell including water ones (TileMapRenderer.RenderMaze). This used to
            // also be -1 — identical to the ground tile beneath it, at the same Z — leaving draw
            // order between the two undefined. Depending on sprite-batching order that could render
            // the water tile invisibly under the ground tile, so a water cell looked like ordinary
            // walkable floor while still silently blocking any character without canCrossWater
            // (everyone except Ducky) — reads exactly like "hit an invisible wall, can't go
            // further." Left at the SpriteRenderer default (0), matching every other tile that
            // sits on top of ground (crops, pellets, warp tunnel).
            go.transform.localScale = Vector3.one * TileMapRenderer.CellSize;
            go.AddComponent<WaterTile>();
            return SaveAndDestroy(go, $"{AbilityPrefabFolder}/WaterTile.prefab");
        }

        private static GameObject SaveAndDestroy(GameObject go, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // PlaceholderSprite.Get() creates a Sprite from an in-memory Texture2D that was never
            // written to disk as an asset. PrefabUtility.SaveAsPrefabAsset can't serialize a
            // reference to an object that isn't a real asset, so any SpriteRenderer still using a
            // placeholder ends up with a NULL sprite in the saved .prefab — invisible in-game even
            // though it looked correct in the Editor session that built it. Confirmed directly:
            // Egg.prefab, WaterTile.prefab and Horace.prefab all shipped with
            // "m_Sprite: {fileID: 0}". Real wired art (ArtWiringBuilder) doesn't trip this since
            // it assigns an actual on-disk texture asset, which masked the bug everywhere real art
            // already landed. Capture any still-placeholder sprites BEFORE go is destroyed, then
            // embed them as real sub-assets of the prefab (see EmbedRuntimePlaceholderSprites).
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

        /// <summary>Embeds each captured placeholder Sprite (and its Texture2D) as a sub-asset of
        /// the just-saved prefab file, then round-trips the prefab (LoadPrefabContents -> re-point
        /// the SpriteRenderer at the now-persisted sprite -> SaveAsPrefabAsset ->
        /// UnloadPrefabContents) so the reference that was saved as null actually points at
        /// something real. Same round-trip shape as the "SerializedObject fields don't stick"
        /// gotcha noted elsewhere in this file — Unity needs the object saved and reloaded, not
        /// just mutated in place, for prefab-asset changes like this to persist.</summary>
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

        // ---- CharacterData ------------------------------------------------------------------

        // Unified to a single shared speed (4.0, the mid-point of the old 3.6-5.0 spread) per
        // feedback that characters should all move identically — the old per-character spread
        // (Cluck 3.8, Bessie 3.6, Percy/Ducky/Horace 5.0, Woolly 4.6, Gerald/Billy 4.0) made some
        // characters feel sluggish/quick relative to others for no gameplay reason (Percy/Ducky/
        // Horace weren't intentionally "fast" characters, that was just where an earlier
        // speed-doubling pass happened to land after capping against the [Range(1,5)] inspector
        // hint). Robots stayed at 2.0 base (Phase3ProjectBuilder) so characters still clearly
        // outrun a Chase/Scatter robot. A Vulnerable robot's flee speed is no longer a fraction of
        // its OWN base speed either — RobotBase.CurrentSpeed now keys it off the active
        // character's speed directly (VulnerableSpeedFraction, 0.85 — "slightly slower than the
        // character" so catching a fleeing robot is a real but short chase), which only stays
        // consistent across every robot type because every character now shares this one speed.
        private const float UnifiedCharacterSpeed = 4.0f;

        private static void BuildCharacterData()
        {
            BuildCharacterDataAsset(CharacterType.Cluck, "Cluck", UnifiedCharacterSpeed, AbilityType.EggDrop, 10f,
                "Drops 3 eggs in her current lane that instantly defeat any robot walking over them.", 0, false);
            BuildCharacterDataAsset(CharacterType.Bessie, "Bessie", UnifiedCharacterSpeed, AbilityType.GroundSlam, 10f,
                "Instant shockwave instantly defeats every robot within 2 tiles.", 0, false);
            BuildCharacterDataAsset(CharacterType.Percy, "Percy", UnifiedCharacterSpeed, AbilityType.BounceRoll, 10f,
                "The next wall Percy hits becomes walkable for 2 seconds.", 5, false);
            BuildCharacterDataAsset(CharacterType.Woolly, "Woolly", UnifiedCharacterSpeed, AbilityType.TripleClone, 10f,
                "Spawns 2 AI-controlled clones that wander, collect crops, and fade after 10s.", 10, false);
            BuildCharacterDataAsset(CharacterType.Ducky, "Ducky", UnifiedCharacterSpeed, AbilityType.SkipShot, 10f,
                "Teleports across an adjacent water tile pair — once per pair per maze.", 15, true);
            BuildCharacterDataAsset(CharacterType.Horace, "Horace", UnifiedCharacterSpeed, AbilityType.RearKick, 10f,
                "Kicks the nearest robot within 3 tiles back 4 tiles and defeats it on landing.", 20, false);
            BuildCharacterDataAsset(CharacterType.Gerald, "Gerald", UnifiedCharacterSpeed, AbilityType.PuffUp, 10f,
                "Inflates to 3x size for 5s — any robot touched is instantly defeated. Half speed, no warp tunnels while puffed.", 30, false);
            BuildCharacterDataAsset(CharacterType.Billy, "Billy", UnifiedCharacterSpeed, AbilityType.HeadbuttThrough, 10f,
                "Permanently destroys the next 3 walls he headbutts.", 40, false);
        }

        private static void BuildCharacterDataAsset(CharacterType type, string displayName, float speed,
            AbilityType ability, float cooldown, string description, int unlockLevel, bool canCrossWater)
        {
            string path = $"{CharacterDataFolder}/CharacterData_{type}.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CharacterData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.characterType = type;
            data.displayName = displayName;
            data.movementSpeed = speed;
            data.specialAbility = ability;
            data.abilityCooldown = cooldown;
            data.abilityDescription = description;
            data.unlockLevel = unlockLevel;
            data.canCrossWater = canCrossWater;

            EditorUtility.SetDirty(data);
        }

        // ---- LevelData_01 water tiles -------------------------------------------------------

        /// <summary>No longer called from BuildAll — the water tile prefab was never given real
        /// art, so it rendered as a plain blue placeholder square that read as an invisible wall/
        /// bug rather than the intended Ducky-only crossing mechanic (confirmed via playtest
        /// feedback). Left here, unused, in case real water art lands later and this gets wired
        /// back in — everything below still works exactly as before if re-added to BuildAll.
        ///
        /// Adds one water tile pair to L01 at a row/columns chosen to sit clear of the warp row (5)
        /// and the robot factory box (x5-8, y6-9) — Phase2ProjectBuilder.BuildLevelData01 reserves
        /// these exact cells with a -1 sentinel during maze generation so they're guaranteed to
        /// still be plain ground (id 0) when this runs, regardless of what the procedural corridor
        /// layout does elsewhere. Verifies both target cells are still plain ground before
        /// overwriting anyway, so a future L01 redesign can't silently corrupt into an unreachable
        /// water tile — logs a warning and skips instead.</summary>
        private static void UpdateLevelData01Water()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelData01Path);
            if (level == null)
            {
                Debug.LogWarning("[Phase4ProjectBuilder] LevelData_01 not found — run Phase 2 > Build All first.");
                return;
            }

            const int waterRow = 11;
            var waterA = new Vector2Int(3, waterRow);
            var waterB = new Vector2Int(10, waterRow);

            var grid = level.MazeLayout;
            if (grid[waterA.x, waterA.y] != 0 || grid[waterB.x, waterB.y] != 0)
            {
                Debug.LogWarning("[Phase4ProjectBuilder] LevelData_01's chosen water tile cells " +
                                  "aren't plain ground (maze layout changed) — skipping water tile placement.");
                return;
            }

            grid[waterA.x, waterA.y] = 8;
            grid[waterB.x, waterB.y] = 8;
            level.SetMazeLayout(grid);
            level.waterTeleportRows = new[] { waterRow };

            EditorUtility.SetDirty(level);
        }

        // ---- Scene wiring ---------------------------------------------------------------------

        private static void WireScene(GameObject cluckPrefab, GameObject bessiePrefab, GameObject percyPrefab,
            GameObject woollyPrefab, GameObject duckyPrefab, GameObject horacePrefab, GameObject geraldPrefab,
            GameObject billyPrefab, GameObject waterTilePrefab)
        {
            EditorSceneManager.OpenScene(ScenePath);

            var managersGO = GameObject.Find("GameManagers");
            var characterParent = GameObject.Find("CharacterParent")?.transform;
            var tileMapRenderer = managersGO.GetComponent<TileMapRenderer>();

            var tileMapSO = new SerializedObject(tileMapRenderer);
            tileMapSO.FindProperty("waterTilePrefab").objectReferenceValue = waterTilePrefab;
            tileMapSO.ApplyModifiedPropertiesWithoutUndo();

            if (managersGO.GetComponent<ComboSystem>() == null)
            {
                managersGO.AddComponent<ComboSystem>();
            }
            if (managersGO.GetComponent<UnlockManager>() == null)
            {
                managersGO.AddComponent<UnlockManager>();
            }

            var characterManager = managersGO.GetComponent<CharacterManager>();
            if (characterManager == null)
            {
                characterManager = managersGO.AddComponent<CharacterManager>();
            }

            var cmSO = new SerializedObject(characterManager);
            cmSO.FindProperty("characterParent").objectReferenceValue = characterParent;
            cmSO.FindProperty("cluckPrefab").objectReferenceValue = cluckPrefab;
            cmSO.FindProperty("bessiePrefab").objectReferenceValue = bessiePrefab;
            cmSO.FindProperty("percyPrefab").objectReferenceValue = percyPrefab;
            cmSO.FindProperty("woollyPrefab").objectReferenceValue = woollyPrefab;
            cmSO.FindProperty("duckyPrefab").objectReferenceValue = duckyPrefab;
            cmSO.FindProperty("horacePrefab").objectReferenceValue = horacePrefab;
            cmSO.FindProperty("geraldPrefab").objectReferenceValue = geraldPrefab;
            cmSO.FindProperty("billyPrefab").objectReferenceValue = billyPrefab;
            cmSO.ApplyModifiedPropertiesWithoutUndo();

            var mainCameraGO = GameObject.Find("Main Camera");
            if (mainCameraGO != null && mainCameraGO.GetComponent<CameraShake>() == null)
            {
                mainCameraGO.AddComponent<CameraShake>();
            }

            // See Phase2ProjectBuilder's matching comment — GameObject.Find only matches active
            // objects, so once Phase4Test is disabled a plain Find-or-create re-spawns a duplicate.
            var existingPhase4Test = Resources.FindObjectsOfTypeAll<Phase4Test>()
                .FirstOrDefault(t => !EditorUtility.IsPersistent(t.gameObject));
            if (existingPhase4Test == null)
            {
                new GameObject("Phase4Test").AddComponent<Phase4Test>();
            }

            // Same reasoning as Phase3ProjectBuilder disabling Phase1Test/Phase2Test: only the
            // newest test harness's runOnStart should auto-fire, since they all independently
            // call GameManager.Instance.LoadLevel(0) and race on the resulting player reload.
            DisableRunOnStart("Phase3Test");

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        private static void DisableRunOnStart(string gameObjectName)
        {
            var go = GameObject.Find(gameObjectName);
            if (go == null)
            {
                return;
            }

            var components = go.GetComponents<MonoBehaviour>();
            if (components.Length == 0)
            {
                return;
            }

            var so = new SerializedObject(components[0]);
            var prop = so.FindProperty("runOnStart");
            if (prop != null)
            {
                prop.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
