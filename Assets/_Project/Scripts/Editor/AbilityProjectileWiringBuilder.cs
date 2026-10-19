using System.IO;
using UnityEditor;
using UnityEngine;
using FarmFuryArcade.Abilities;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.EditorTools
{
    /// <summary>
    /// Builds the two projectiles added 2026-10-15 — Ducky's Water Spout (WaterSpout.png) and
    /// Gerald's Feather Burst (FeatherBurst.png) — and wires them onto SkipShotAbility.
    /// waterSpoutPrefab / PuffUpAbility.featherBurstPrefab on the existing Ducky/Gerald prefabs.
    /// Since 2026-10-19 it also builds Billy's Mud Sling (MudBall + MudPile prefabs) and wires
    /// HeadbuttThroughAbility.mudBallPrefab/slingSprite on Billy.
    ///
    /// Self-contained and idempotent, same shape as MachineWiringBuilder, so it can be run on its own
    /// without a full Phase 4 rebuild (which wipes other wiring). Phase4ProjectBuilder.BuildAll also
    /// calls it at the end, since that rebuilds Ducky/Gerald from scratch and would otherwise drop
    /// these two fields.
    /// </summary>
    public static class AbilityProjectileWiringBuilder
    {
        private const string SpriteFolder = "Assets/_Project/Sprites/Characters";
        private const string AbilityPrefabFolder = "Assets/_Project/Prefabs/Abilities";
        private const string CharacterPrefabFolder = "Assets/_Project/Prefabs/Characters";

        /// <summary>Visual size relative to one grid cell. Horseshoe uses 0.6; these two are busier
        /// shapes (a ring, a scatter of feathers) that read too small at that size.</summary>
        private const float ProjectileScale = 0.8f;

        /// <summary>The mud glob is a small blob (the horseshoe's 0.6); the pile it leaves is a wide
        /// puddle that should nearly fill its tile.</summary>
        private const float MudBallScale = 0.6f;
        private const float MudPileScale = 0.9f;

        [MenuItem("Farm Fury Arcade/Wire Ability Projectiles (Water Spout + Feather Burst + Mud Sling)")]
        public static void WireAll()
        {
            string spoutPath = BuildProjectilePrefab("WaterSpout", $"{SpriteFolder}/WaterSpout.png", ProjectileScale);
            string featherPath = BuildProjectilePrefab("FeatherBurst", $"{SpriteFolder}/FeatherBurst.png", ProjectileScale);

            WirePrefabField("Ducky", typeof(SkipShotAbility), "waterSpoutPrefab", spoutPath);
            WirePrefabField("Gerald", typeof(PuffUpAbility), "featherBurstPrefab", featherPath);

            // Billy's Mud Sling (2026-10-19, ported from Farm Fury: Stampede). MudSplat.png is used for
            // both the hit splat and the pile left on a miss: Stampede's own MudPile.png is drawn
            // side-on (a flat strip), which reads wrong from Arcade's top-down view.
            string splatSpritePath = $"{SpriteFolder}/MudSplat.png";
            string pilePath = BuildMudPilePrefab(splatSpritePath);
            Sprite splat = ConfigureAndLoadSprite(splatSpritePath);
            var pilePrefab = pilePath != null ? AssetDatabase.LoadAssetAtPath<GameObject>(pilePath) : null;
            string mudBallPath = BuildProjectilePrefab("MudBall", $"{SpriteFolder}/MudBall.png", MudBallScale, splat, pilePrefab);
            WirePrefabField("Billy", typeof(HeadbuttThroughAbility), "mudBallPrefab", mudBallPath);
            WirePrefabSpriteField("Billy", typeof(HeadbuttThroughAbility), "slingSprite",
                ConfigureAndLoadSprite($"{SpriteFolder}/Billy_Sling.png"));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AbilityProjectileWiringBuilder] Wired Water Spout (Ducky), Feather Burst (Gerald) and Mud Sling (Billy).");
        }

        /// <summary>Same shape as Phase4ProjectBuilder.BuildEggPrefab: a static trigger collider (robots
        /// carry their own Kinematic Rigidbody2D with useFullKinematicContacts, so contact still fires)
        /// plus the hazard component. Sorted below characters/robots (5) so they walk over it.</summary>
        private static string BuildMudPilePrefab(string spritePath)
        {
            Sprite sprite = ConfigureAndLoadSprite(spritePath);
            if (sprite == null)
            {
                return null;
            }

            var go = new GameObject("MudPile");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 3;
            go.transform.localScale = Vector3.one * MudPileScale * TileMapRenderer.CellSize;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.35f;

            go.AddComponent<MudPileHazard>();

            string path = $"{AbilityPrefabFolder}/MudPile.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return path;
        }

        /// <summary>Same structure as Phase4ProjectBuilder.BuildHorseshoeProjectilePrefab: a Kinematic
        /// Rigidbody2D with useFullKinematicContacts (needed for trigger contact with robots) and a
        /// trigger CircleCollider2D. Rebuilt from scratch every run. impactSprite/landingPrefab are
        /// ThrownProjectileEffect's optional on-hit art and on-miss leftover (Mud Sling only).</summary>
        private static string BuildProjectilePrefab(string name, string spritePath, float scale,
            Sprite impactSprite = null, GameObject landingPrefab = null)
        {
            Sprite sprite = ConfigureAndLoadSprite(spritePath);
            if (sprite == null)
            {
                return null;
            }

            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 5;
            go.transform.localScale = Vector3.one * scale * TileMapRenderer.CellSize;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.useFullKinematicContacts = true;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.4f;

            var effect = go.AddComponent<ThrownProjectileEffect>();
            if (impactSprite != null || landingPrefab != null)
            {
                var so = new SerializedObject(effect);
                so.FindProperty("impactSprite").objectReferenceValue = impactSprite;
                so.FindProperty("landingPrefab").objectReferenceValue = landingPrefab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            string path = $"{AbilityPrefabFolder}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return path;
        }

        private static void WirePrefabField(string characterName, System.Type abilityType, string fieldName, string prefabAssetPath)
        {
            if (prefabAssetPath == null)
            {
                return;
            }
            SetPrefabFieldReference(characterName, abilityType, fieldName,
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath));
        }

        private static void WirePrefabSpriteField(string characterName, System.Type abilityType, string fieldName, Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }
            SetPrefabFieldReference(characterName, abilityType, fieldName, sprite);
        }

        private static void SetPrefabFieldReference(string characterName, System.Type abilityType, string fieldName, Object value)
        {
            string characterPrefabPath = $"{CharacterPrefabFolder}/{characterName}.prefab";
            if (!File.Exists(characterPrefabPath))
            {
                Debug.LogWarning($"[AbilityProjectileWiringBuilder] Character prefab not found, skipping: {characterPrefabPath}");
                return;
            }

            // Round-trip through LoadPrefabContents — setting fields on a just-saved prefab asset
            // directly doesn't reliably persist (see CLAUDE.md, Phase 4 gotcha).
            var contents = PrefabUtility.LoadPrefabContents(characterPrefabPath);
            var ability = contents.GetComponent(abilityType);
            if (ability == null)
            {
                Debug.LogWarning($"[AbilityProjectileWiringBuilder] {abilityType.Name} not found on {characterPrefabPath}, skipping.");
                PrefabUtility.UnloadPrefabContents(contents);
                return;
            }

            var so = new SerializedObject(ability);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[AbilityProjectileWiringBuilder] Field '{fieldName}' not found on {abilityType.Name}.");
            }
            else
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(contents, characterPrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        /// <summary>Same importer setup as MachineWiringBuilder.ConfigureAndLoadSprite: Sprite type,
        /// PPU = texture width so the sprite fills exactly one world unit at scale 1.</summary>
        private static Sprite ConfigureAndLoadSprite(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[AbilityProjectileWiringBuilder] Expected sprite not found, skipping: {path}");
                return null;
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[AbilityProjectileWiringBuilder] Could not get TextureImporter for: {path}");
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.GetSourceTextureWidthAndHeight(out int width, out int _);
            importer.spritePixelsPerUnit = width > 0 ? width : 100;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
