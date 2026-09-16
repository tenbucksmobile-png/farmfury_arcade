using System.IO;
using UnityEditor;
using UnityEngine;
using FarmFuryArcade.Abilities;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.EditorTools
{
    /// <summary>
    /// Wires the "Machine Cosmetics" line (2026-09-15) — full character-drawn-into-vehicle Skin
    /// cosmetics, each exclusive to one character (unlike Hat/Trail, there's no per-character
    /// "set" — Clucky's tractor doesn't exist for any other character): Clucky's Tractor, Bessie's
    /// Milk Tanker, Horace's Hay Baler. Two halves:
    ///
    /// 1. WireMachineSkins — builds the 3 CosmeticData (Skin) assets from
    ///    Sprites/Cosmetics/Cosmetics_machine art and adds CharacterCosmeticRenderer to the 3
    ///    character prefabs (idempotent, safe alongside CosmeticWiringBuilder's own calls — that
    ///    component is already a no-op AddComponent guard).
    /// 2. WireMachineHazards — clones the existing Egg/Shockwave/Horseshoe ability-effect prefabs
    ///    into machine-themed reskins (oil spill / milk splash / hay bale) and wires them onto
    ///    EggDropAbility/GroundSlamAbility/HorseshoeThrowAbility's oilHazardPrefab/
    ///    milkShockwavePrefab/hayBaleProjectilePrefab fields — same power/timing as the un-skinned
    ///    ability, purely a themed visual swap (see EggDropAbility's own doc comment for why this
    ///    keeps the cosmetic non-gameplay-affecting despite being a real functional hazard).
    ///
    /// All 3 machines now have real 4-direction art (front/back/left/right) — Bessie's Milk Tanker
    /// and Horace's Hay Baler gained their front/back/right frames 2026-09-16 (pulled as stills from
    /// a Kling-generated turntable orbit video of each machine, then hand-touched-up/re-cropped),
    /// replacing the earlier interim fallback that repeated their one Left frame across all 8
    /// skinFrames slots.
    ///
    /// Milk's own third (dissipating) frame is a known duplicate of the resting puddle — per direct
    /// instruction, the resting sprite is reused for the impact frame's own resting art is separate
    /// (Milk1.png), so only ONE stage is short here, not two — see ConfigureMilkSplashShockwave's
    /// own doc comment.
    /// </summary>
    public static class MachineWiringBuilder
    {
        private const string MachineSpriteFolder = "Assets/_Project/Sprites/Cosmetics/Cosmetics_machine";
        private const string CosmeticDataFolder = "Assets/_Project/ScriptableObjects/Resources/Cosmetics";
        private const string CharacterPrefabFolder = "Assets/_Project/Prefabs/Characters";
        private const string AbilityPrefabFolder = "Assets/_Project/Prefabs/Abilities";

        [MenuItem("Farm Fury Arcade/Wire Cosmetic Art (Machines)")]
        public static void WireAll()
        {
            WireMachineSkins();
            WireMachineHazards();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MachineWiringBuilder] Wired Machine Cosmetics (Tractor/Milk Tanker/Hay Baler) — skins + hazard reskins.");
        }

        private static void WireMachineSkins()
        {
            WireSkin(CharacterType.Cluck, "MachineTractorClucky", IAPManager.MachineTractorCluckyProductId,
                front: "Clucky_tractor_front.png", back: "Clucky_tractor_back.png",
                left: "Cluck_Tractor_left.png", right: "Clucky_tractor_right.png",
                preview: "Price_Clucky_truck.png");

            // Full 4-direction art (2026-09-16) — see class doc comment.
            WireSkin(CharacterType.Bessie, "MachineTruckBessie", IAPManager.MachineTruckBessieProductId,
                front: "Bessie_Truck_front.png", back: "Bessie_Truck_back.png",
                left: "Bessie_Truck_Left.png", right: "Bessie_Truck_right.png",
                preview: "Price_Bessie_truck.png");

            WireSkin(CharacterType.Horace, "MachineHayHorace", IAPManager.MachineHayHoraceProductId,
                front: "Horace_hay_front.png", back: "Horace_hay_back.png",
                left: "Horace_hay_left.png", right: "Horace_hay_right.png",
                preview: "Price_Horace_truck.png");
        }

        private static void WireSkin(CharacterType character, string assetNamePrefix, string cosmeticId,
            string front, string back, string left, string right, string preview)
        {
            Sprite frontSprite = ConfigureAndLoadSprite($"{MachineSpriteFolder}/{front}");
            Sprite backSprite = ConfigureAndLoadSprite($"{MachineSpriteFolder}/{back}");
            Sprite leftSprite = ConfigureAndLoadSprite($"{MachineSpriteFolder}/{left}");
            Sprite rightSprite = ConfigureAndLoadSprite($"{MachineSpriteFolder}/{right}");
            Sprite previewSprite = ConfigureAndLoadSprite($"{MachineSpriteFolder}/{preview}");

            if (frontSprite == null || backSprite == null || leftSprite == null || rightSprite == null)
            {
                Debug.LogWarning($"[MachineWiringBuilder] Missing sprite(s) for {character} machine skin — skipping.");
                return;
            }

            string path = $"{CosmeticDataFolder}/CosmeticData_{assetNamePrefix}.asset";
            var data = AssetDatabase.LoadAssetAtPath<CosmeticData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CosmeticData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.cosmeticId = cosmeticId;
            data.setId = cosmeticId;
            data.displayName = assetNamePrefix;
            data.cosmeticType = CosmeticType.Skin;
            data.character = character;
            // Real-money only (see IAPManager.MachineTractorCluckyProductId's own doc comment) —
            // not coin-purchasable, so this is left at 0/unused, same convention every real-money
            // cosmetic's coinCost field already uses.
            data.coinCost = 0;
            // Puff-of-smoke movement flourish (2026-09-15) - every machine skin gets one, purely
            // cosmetic, independent of whatever Trail (if any) is separately equipped.
            data.spawnsMovementSmoke = true;
            data.previewSprite = previewSprite != null ? previewSprite : frontSprite;
            data.skinFrames = new[]
            {
                backSprite, backSprite,     // Up0, Up1
                frontSprite, frontSprite,   // Down0, Down1
                leftSprite, leftSprite,     // Left0, Left1
                rightSprite, rightSprite,   // Right0, Right1
            };
            EditorUtility.SetDirty(data);

            AddCosmeticRendererToPrefab(character);
        }

        private static void AddCosmeticRendererToPrefab(CharacterType character)
        {
            string path = $"{CharacterPrefabFolder}/{character}.prefab";
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[MachineWiringBuilder] Character prefab not found, skipping: {path}");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(path);
            if (contents.GetComponent<CharacterCosmeticRenderer>() == null)
            {
                contents.AddComponent<CharacterCosmeticRenderer>();
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            PrefabUtility.UnloadPrefabContents(contents);
        }

        // ---- Hazard reskins ----

        private static void WireMachineHazards()
        {
            string oilPath = CloneAbilityPrefab("Egg.prefab", "OilSpillHazard.prefab");
            ConfigureOilSpillHazard(oilPath);
            WireAbilityPrefabField(CharacterType.Cluck, typeof(EggDropAbility), "oilHazardPrefab", oilPath);

            string milkPath = CloneAbilityPrefab("Shockwave.prefab", "MilkSplashShockwave.prefab");
            ConfigureMilkSplashShockwave(milkPath);
            WireAbilityPrefabField(CharacterType.Bessie, typeof(GroundSlamAbility), "milkShockwavePrefab", milkPath);

            string hayPath = CloneAbilityPrefab("Horseshoe.prefab", "HayBaleEffect.prefab");
            ConfigureHayBaleEffect(hayPath);
            WireAbilityPrefabField(CharacterType.Horace, typeof(HorseshoeThrowAbility), "hayBaleProjectilePrefab", hayPath);
        }

        /// <summary>Always re-clones from the current source rather than only-if-missing (2026-09-16
        /// — previously left an already-existing clone stale even after its source prefab's own
        /// structure changed, e.g. HayBaleEffect.prefab kept missing the Rigidbody2D/Collider2D that
        /// Horseshoe.prefab gained when HorseshoeThrowAbility replaced RearKickAbility). Every
        /// Configure* method below already re-applies its sprite fields idempotently regardless, so
        /// a fresh delete-then-copy on every run costs nothing and can never drift out of structural
        /// sync with its source again.</summary>
        private static string CloneAbilityPrefab(string sourceName, string targetName)
        {
            string sourcePath = $"{AbilityPrefabFolder}/{sourceName}";
            string targetPath = $"{AbilityPrefabFolder}/{targetName}";
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"[MachineWiringBuilder] Source prefab not found, cannot clone: {sourcePath}");
                return null;
            }
            if (File.Exists(targetPath))
            {
                AssetDatabase.DeleteAsset(targetPath);
            }
            AssetDatabase.CopyAsset(sourcePath, targetPath);
            return targetPath;
        }

        private static void ConfigureOilSpillHazard(string path)
        {
            if (path == null)
            {
                return;
            }
            var resting = ConfigureAndLoadSprite($"{MachineSpriteFolder}/Oil.png");
            var impact = ConfigureAndLoadSprite($"{MachineSpriteFolder}/Oil1.png");
            var dissipate = ConfigureAndLoadSprite($"{MachineSpriteFolder}/Oil2.png");

            var contents = PrefabUtility.LoadPrefabContents(path);
            var sr = contents.GetComponent<SpriteRenderer>();
            if (sr != null && resting != null)
            {
                sr.sprite = resting;
                sr.color = Color.white;
            }
            var hazard = contents.GetComponent<EggHazard>();
            if (hazard != null)
            {
                var so = new SerializedObject(hazard);
                if (impact != null) so.FindProperty("crackedSprite").objectReferenceValue = impact;
                if (dissipate != null) so.FindProperty("burstSprite").objectReferenceValue = dissipate;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        /// <summary>ShockwaveEffect has only one sprite slot (it scales/fades a single image, no
        /// crack/burst animation the way EggHazard has) — Milk1.png (the splash frame) is the more
        /// dynamic-looking of the two milk images, so it's used here rather than the resting
        /// puddle. Milk - Copy.png/Milk2.png are duplicates of the same resting art (Kling AI
        /// struggled to render a distinct third stage) — not needed here since this effect only
        /// ever shows one image, scaled/faded over time by the existing ShockwaveEffect logic.
        ///
        /// Real, reported bug fixed 2026-09-16: the milk splash rendered noticeably oversized
        /// compared to the plain placeholder shockwave circle. GroundSlamAbility.Configure() sets
        /// BOTH variants' maxScale from the SAME real kill-radius diameter (this effect has no
        /// collider of its own — GroundSlamAbility.DefeatRobotsInRadius's own grid-distance sweep is
        /// what actually defeats robots, so the sprite's visual size is purely cosmetic and can be
        /// tuned per-prefab with zero gameplay effect). Milk1.png's splash shape reads visually
        /// larger than an equally-sized plain circle would — its jagged splash droplets extend
        /// further toward the canvas edges than a circle's smooth silhouette does, so the same
        /// bounding-box diameter looks more dominant. Fixed with ShockwaveEffect.
        /// visualScaleMultiplier (new field, defaults to 1 so the un-skinned Shockwave.prefab is
        /// unaffected) set to 0.55 here — a first-pass correction with no visual Editor access this
        /// session, nudge further if it still reads too big/small once actually seen in Play mode.</summary>
        private static void ConfigureMilkSplashShockwave(string path)
        {
            if (path == null)
            {
                return;
            }
            var splash = ConfigureAndLoadSprite($"{MachineSpriteFolder}/Milk1.png");

            var contents = PrefabUtility.LoadPrefabContents(path);
            var sr = contents.GetComponent<SpriteRenderer>();
            if (sr != null && splash != null)
            {
                sr.sprite = splash;
                sr.color = Color.white;
            }
            var effect = contents.GetComponent<ShockwaveEffect>();
            if (effect != null)
            {
                var so = new SerializedObject(effect);
                so.FindProperty("visualScaleMultiplier").floatValue = 0.55f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        /// <summary>Horseshoe.prefab/HayBaleEffect.prefab both carry ThrownProjectileEffect
        /// (2026-09-16, replacing HoraceBuckEffect's per-direction leftSprite/rightSprite pair) —
        /// a single SpriteRenderer.sprite swap is all the reskin needs now, since the projectile
        /// spins via code at runtime instead of picking a pre-drawn per-direction pose.
        ///
        /// Also wires Haybail_Damaged.png as this prefab's impactSprite (2026-09-16, per direct
        /// feedback) — the real starburst/impact art the hay bale swaps to and holds/fades out on
        /// for a visible moment when it hits a robot, instead of just vanishing (see
        /// ThrownProjectileEffect.ImpactAndDissipate). Horseshoe.prefab is left with no
        /// impactSprite — no dedicated horseshoe-impact art exists yet, so it keeps the plain
        /// instant destroy on hit until/unless that art lands too.</summary>
        private static void ConfigureHayBaleEffect(string path)
        {
            if (path == null)
            {
                return;
            }
            var hayBale = ConfigureAndLoadSprite($"{MachineSpriteFolder}/Haybail.png");
            var hayBaleDamaged = ConfigureAndLoadSprite($"{MachineSpriteFolder}/Haybail_Damaged.png");

            var contents = PrefabUtility.LoadPrefabContents(path);
            var sr = contents.GetComponent<SpriteRenderer>();
            if (sr != null && hayBale != null)
            {
                sr.sprite = hayBale;
                sr.color = Color.white;
            }
            var effect = contents.GetComponent<ThrownProjectileEffect>();
            if (effect != null && hayBaleDamaged != null)
            {
                var so = new SerializedObject(effect);
                so.FindProperty("impactSprite").objectReferenceValue = hayBaleDamaged;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        /// <summary>Sets a GameObject-typed serialized field on the named ability component living
        /// on the given character's prefab — same LoadPrefabContents/SaveAsPrefabAsset round-trip
        /// every prefab-field edit in this project must use (a plain SerializedObject edit against
        /// PrefabUtility.SaveAsPrefabAsset's own returned GameObject does not reliably persist in
        /// this Unity version — see Phase4ProjectBuilder's own doc comment on this exact gotcha).</summary>
        private static void WireAbilityPrefabField(CharacterType character, System.Type abilityType, string fieldName, string prefabAssetPath)
        {
            if (prefabAssetPath == null)
            {
                return;
            }
            string characterPrefabPath = $"{CharacterPrefabFolder}/{character}.prefab";
            if (!File.Exists(characterPrefabPath))
            {
                Debug.LogWarning($"[MachineWiringBuilder] Character prefab not found, skipping: {characterPrefabPath}");
                return;
            }

            var effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
            if (effectPrefab == null)
            {
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(characterPrefabPath);
            var ability = contents.GetComponent(abilityType);
            if (ability == null)
            {
                Debug.LogWarning($"[MachineWiringBuilder] {abilityType.Name} not found on {characterPrefabPath}, skipping.");
                PrefabUtility.UnloadPrefabContents(contents);
                return;
            }

            var so = new SerializedObject(ability);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[MachineWiringBuilder] Field '{fieldName}' not found on {abilityType.Name}.");
            }
            else
            {
                prop.objectReferenceValue = effectPrefab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(contents, characterPrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static Sprite ConfigureAndLoadSprite(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[MachineWiringBuilder] Expected sprite not found, skipping: {path}");
                return null;
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[MachineWiringBuilder] Could not get TextureImporter for: {path}");
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;

            // Same PPU-equals-texture-width convention every other sprite in this project uses.
            importer.GetSourceTextureWidthAndHeight(out int width, out int _);
            importer.spritePixelsPerUnit = width > 0 ? width : 100;

            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
