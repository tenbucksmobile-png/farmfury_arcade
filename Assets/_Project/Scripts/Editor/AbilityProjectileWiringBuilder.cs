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

        [MenuItem("Farm Fury Arcade/Wire Ability Projectiles (Water Spout + Feather Burst)")]
        public static void WireAll()
        {
            string spoutPath = BuildProjectilePrefab("WaterSpout", $"{SpriteFolder}/WaterSpout.png");
            string featherPath = BuildProjectilePrefab("FeatherBurst", $"{SpriteFolder}/FeatherBurst.png");

            WirePrefabField("Ducky", typeof(SkipShotAbility), "waterSpoutPrefab", spoutPath);
            WirePrefabField("Gerald", typeof(PuffUpAbility), "featherBurstPrefab", featherPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AbilityProjectileWiringBuilder] Wired Water Spout (Ducky) and Feather Burst (Gerald).");
        }

        /// <summary>Same structure as Phase4ProjectBuilder.BuildHorseshoeProjectilePrefab: a Kinematic
        /// Rigidbody2D with useFullKinematicContacts (needed for trigger contact with robots) and a
        /// trigger CircleCollider2D. Rebuilt from scratch every run.</summary>
        private static string BuildProjectilePrefab(string name, string spritePath)
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
            go.transform.localScale = Vector3.one * ProjectileScale * TileMapRenderer.CellSize;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.useFullKinematicContacts = true;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.4f;

            go.AddComponent<ThrownProjectileEffect>();

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
            string characterPrefabPath = $"{CharacterPrefabFolder}/{characterName}.prefab";
            if (!File.Exists(characterPrefabPath))
            {
                Debug.LogWarning($"[AbilityProjectileWiringBuilder] Character prefab not found, skipping: {characterPrefabPath}");
                return;
            }

            var effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
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
                prop.objectReferenceValue = effectPrefab;
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
