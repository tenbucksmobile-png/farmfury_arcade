using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FarmFuryArcade.Abilities;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;
using FarmFuryArcade.UI;
using FarmFuryArcade.Utilities;
using Object = UnityEngine.Object;

namespace FarmFuryArcade.EditorTools
{
    /// <summary>
    /// Phase 4 scaffolding: builds CharacterData for all 8 characters, adds CharacterBase +
    /// EggDropAbility to the existing Cluck prefab, builds the 7 remaining character prefabs plus
    /// every ability's sub-prefab (egg, shockwave, wool clone, water tile), adds water tiles to
    /// LevelData_01, and wires CharacterManager/ComboSystem/UnlockManager/CharacterSwapUI/
    /// CameraShake into Game.unity. Safe to re-run. Depends on Phase 2 (Cluck prefab, LevelData_01)
    /// and Phase 3 (robot types, for RearKick/PuffUp/GroundSlam to find at runtime — no direct
    /// build-time dependency).
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

            GameObject cluckPrefab = AddCharacterBaseAndAbilityToCluck(eggPrefab);

            GameObject bessiePrefab = BuildCharacterPrefab("Bessie", new Color(0.55f, 0.38f, 0.20f),
                typeof(GroundSlamAbility), 20f, ability =>
                {
                    var so = new SerializedObject(ability);
                    so.FindProperty("shockwavePrefab").objectReferenceValue = shockwavePrefab;
                    so.ApplyModifiedPropertiesWithoutUndo();
                });

            GameObject percyPrefab = BuildCharacterPrefab("Percy", new Color(0.95f, 0.55f, 0.65f),
                typeof(BounceRollAbility), 30f, ability =>
                {
                    var so = new SerializedObject(ability);
                    so.FindProperty("trailPrefab").objectReferenceValue = bounceTrailPrefab;
                    so.ApplyModifiedPropertiesWithoutUndo();
                });

            GameObject woollyPrefab = BuildCharacterPrefab("Woolly", new Color(0.92f, 0.90f, 0.80f),
                typeof(TripleCloneAbility), 25f, ability =>
                {
                    var so = new SerializedObject(ability);
                    so.FindProperty("clonePrefab").objectReferenceValue = woolClonePrefab;
                    so.FindProperty("eggPrefab").objectReferenceValue = eggPrefab;
                    so.ApplyModifiedPropertiesWithoutUndo();
                });

            GameObject duckyPrefab = BuildCharacterPrefab("Ducky", new Color(0.25f, 0.65f, 0.75f),
                typeof(SkipShotAbility), 2f, ability =>
                {
                    var so = new SerializedObject(ability);
                    so.FindProperty("woolClonePrefab").objectReferenceValue = woolClonePrefab;
                    so.ApplyModifiedPropertiesWithoutUndo();
                });

            GameObject horacePrefab = BuildCharacterPrefab("Horace", new Color(0.45f, 0.30f, 0.15f),
                typeof(RearKickAbility), 18f, null);

            GameObject geraldPrefab = BuildCharacterPrefab("Gerald", new Color(0.65f, 0.60f, 0.50f),
                typeof(PuffUpAbility), 45f, null);

            GameObject billyPrefab = BuildCharacterPrefab("Billy", new Color(0.35f, 0.35f, 0.38f),
                typeof(HeadbuttThroughAbility), 40f, null);

            UpdateLevelData01Water();

            WireScene(cluckPrefab, bessiePrefab, percyPrefab, woollyPrefab, duckyPrefab,
                horacePrefab, geraldPrefab, billyPrefab, waterTilePrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Phase4ProjectBuilder] Phase 4 character prefabs, CharacterData, ability sub-prefabs, " +
                      "LevelData_01 water tiles, and Game.unity wiring complete.");
        }

        // ---- Character prefabs ----------------------------------------------------------

        private static GameObject BuildCharacterPrefab(string name, Color color, System.Type abilityType,
            float abilityCooldown, System.Action<Component> configureAbilityOnAsset)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(color);
            sr.sortingOrder = 5;

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
            abilitySO.FindProperty("totalCooldown").floatValue = 15f;
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
            // The old near-white/tan placeholder (0.92, 0.87, 0.72) blended straight into the real
            // CornTiles.png ground art once that landed (both warm off-white/gold tones) — the
            // ability worked (robots still got stunned on contact) but the egg itself read as
            // invisible. Pure white plus a bigger scale gives it contrast against the ground until
            // real egg art is wired.
            sr.sprite = PlaceholderSprite.Get(Color.white);
            sr.sortingOrder = 2;
            go.transform.localScale = Vector3.one * 0.55f;
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
            go.AddComponent<ShockwaveEffect>();
            return SaveAndDestroy(go, $"{AbilityPrefabFolder}/Shockwave.prefab");
        }

        private static GameObject BuildBounceTrailPrefab()
        {
            var go = new GameObject("BounceTrail");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(new Color(0.3f, 0.9f, 0.95f, 0.5f));
            sr.sortingOrder = 4;
            go.transform.localScale = Vector3.one * 0.6f;
            return SaveAndDestroy(go, $"{AbilityPrefabFolder}/BounceTrail.prefab");
        }

        private static GameObject BuildWoolClonePrefab()
        {
            var go = new GameObject("WoollyClone");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.Get(new Color(0.92f, 0.90f, 0.80f));
            sr.sortingOrder = 5;
            go.transform.localScale = Vector3.one * 0.7f; // "smaller Woolly" per spec

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
            sr.sortingOrder = -1;
            go.AddComponent<WaterTile>();
            return SaveAndDestroy(go, $"{AbilityPrefabFolder}/WaterTile.prefab");
        }

        private static GameObject SaveAndDestroy(GameObject go, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ---- CharacterData ------------------------------------------------------------------

        private static void BuildCharacterData()
        {
            BuildCharacterDataAsset(CharacterType.Cluck, "Cluck", 4.2f, AbilityType.EggDrop, 15f,
                "Drops 3 eggs in her current lane that stun any robot walking over them for 3s.", 0, false);
            BuildCharacterDataAsset(CharacterType.Bessie, "Bessie", 4f, AbilityType.GroundSlam, 20f,
                "Instant shockwave stuns every robot within 2 tiles.", 0, false);
            BuildCharacterDataAsset(CharacterType.Percy, "Percy", 6f, AbilityType.BounceRoll, 30f,
                "The next wall Percy hits becomes walkable for 2 seconds.", 5, false);
            BuildCharacterDataAsset(CharacterType.Woolly, "Woolly", 5f, AbilityType.TripleClone, 25f,
                "Spawns 2 AI-controlled clones that wander, collect crops, and fade after 10s.", 10, false);
            BuildCharacterDataAsset(CharacterType.Ducky, "Ducky", 5.5f, AbilityType.SkipShot, 2f,
                "Teleports across an adjacent water tile pair — once per pair per maze.", 15, true);
            BuildCharacterDataAsset(CharacterType.Horace, "Horace", 5.5f, AbilityType.RearKick, 18f,
                "Kicks the nearest robot within 3 tiles back 4 tiles and stuns it on landing.", 20, false);
            BuildCharacterDataAsset(CharacterType.Gerald, "Gerald", 4.5f, AbilityType.PuffUp, 45f,
                "Inflates to 3x size for 5s — any robot touched is instantly defeated. Half speed, no warp tunnels while puffed.", 30, false);
            BuildCharacterDataAsset(CharacterType.Billy, "Billy", 4.5f, AbilityType.HeadbuttThrough, 40f,
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

        /// <summary>Adds one water tile pair to L01 at a row/columns chosen to sit clear of the
        /// warp row (5) and the robot factory box (x5-8, y6-9) — Phase2ProjectBuilder.
        /// BuildLevelData01 reserves these exact cells with a -1 sentinel during maze generation so
        /// they're guaranteed to still be plain ground (id 0) when this runs, regardless of what the
        /// procedural corridor layout does elsewhere. Verifies both target cells are still plain
        /// ground before overwriting anyway, so a future L01 redesign can't silently corrupt into an
        /// unreachable water tile — logs a warning and skips instead.</summary>
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

            if (GameObject.Find("CharacterSwapUI") == null)
            {
                new GameObject("CharacterSwapUI").AddComponent<CharacterSwapUI>();
            }

            if (GameObject.Find("Phase4Test") == null)
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
