using System.IO;
using UnityEditor;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.EditorTools
{
    /// <summary>
    /// Wires dropped-in cosmetic art (Assets/_Project/Sprites/Cosmetics/...) into real CosmeticData
    /// assets and the character prefabs, same "art wiring is separate from prefab building" split
    /// ArtWiringBuilder already established for the base cast. Idempotent — safe to re-run after
    /// adding more cosmetic art under the same folder convention.
    ///
    /// hatOffset/hatScale values below are first-pass eyeballed estimates (this session has no
    /// visual Editor/Play mode access to tune them against a live render) — the source art fills
    /// most of its own 500x500 canvas rather than being pre-scaled to sit on a specific character's
    /// head (unlike the "trace against the character's real sprite" convention CLAUDE.md's
    /// Cosmetics section describes for future art), so every cap here needs a real scale-down.
    /// Expect to nudge these per character once actually seen in Play mode.
    /// </summary>
    public static class CosmeticWiringBuilder
    {
        private const string CosmeticSpriteFolder = "Assets/_Project/Sprites/Cosmetics/Cosmetics_Type_Hat";
        private const string CosmeticDataFolder = "Assets/_Project/ScriptableObjects/Resources/Cosmetics";
        private const string CharacterPrefabFolder = "Assets/_Project/Prefabs/Characters";

        private struct BaseballCapEntry
        {
            public CharacterType Character;
            public string SpriteFileName;
            public Vector2 HatOffset;
            public float HatScale;

            public BaseballCapEntry(CharacterType character, string spriteFileName, Vector2 hatOffset, float hatScale)
            {
                Character = character;
                SpriteFileName = spriteFileName;
                HatOffset = hatOffset;
                HatScale = hatScale;
            }
        }

        // Head-width/head-height eyeballed against each character's own *_front.png. hatOffset.y is
        // roughly (canvas-center-to-head-top)/500 in world units; hatScale targets the cap's
        // rendered width landing close to that character's own head width.
        // 2026-08-18 playtest feedback: caps sat too low (sunk toward the face) on every character.
        // Raised hatOffset.y by +0.15 world units across the board as a first-pass correction —
        // still eyeballed (no live render access), re-tune further if it overshoots.
        private static readonly BaseballCapEntry[] Caps =
        {
            new BaseballCapEntry(CharacterType.Cluck, "Baseball_Clucky.png", new Vector2(0f, 0.55f), 0.64f),
            new BaseballCapEntry(CharacterType.Bessie, "Baseball_Bessie.png", new Vector2(0f, 0.55f), 0.58f),
            new BaseballCapEntry(CharacterType.Percy, "Baseball_Percy.png", new Vector2(0f, 0.51f), 0.77f),
            new BaseballCapEntry(CharacterType.Woolly, "Baseball_Woolly.png", new Vector2(0f, 0.47f), 0.96f),
            new BaseballCapEntry(CharacterType.Ducky, "Baseball_Ducky.png", new Vector2(0f, 0.53f), 0.70f),
            new BaseballCapEntry(CharacterType.Horace, "Baseball_Horace.png", new Vector2(0f, 0.47f), 0.70f),
            new BaseballCapEntry(CharacterType.Gerald, "Baseball_Gerald.png", new Vector2(0f, 0.53f), 0.45f),
            new BaseballCapEntry(CharacterType.Billy, "Baseball_Billy.png", new Vector2(0f, 0.47f), 0.58f),
        };

        private const string TrailSpriteFolder = "Assets/_Project/Sprites/Cosmetics/CosmeticType.Trail";
        private const string StoreChromeFolder = "Assets/_Project/Sprites/Cosmetics";

        // Store UI chrome (tab icons, purchase card frame, equipped badge) dropped in alongside the
        // trails. No cosmetics Store screen exists yet to consume these (ShopController today only
        // sells Phase 3's coin packs + Remove Ads — see CLAUDE.md's Phase 4 "Not built yet" list),
        // so this just imports/configures them (PPU-equals-texture-width, same convention as every
        // other sprite) ready for whenever that screen gets built.
        private static readonly string[] StoreChromeFiles =
        {
            "Hat_Icon.png",
            "Trails_Tab_Icon.png",
            "MazeThemeTab.png",
            "PurchaseCardFrame.png",
            "EquippedBadge_Icon.png",
        };

        private struct TrailEntry
        {
            public string Id;
            public string DisplayName;
            public string SpriteFileName;
            public int CoinCost;

            public TrailEntry(string id, string displayName, string spriteFileName, int coinCost)
            {
                Id = id;
                DisplayName = displayName;
                SpriteFileName = spriteFileName;
                CoinCost = coinCost;
            }
        }

        // Coin pricing follows this project's existing rarity convention: "Rainbow" already means
        // the rarest/most valuable tier elsewhere (PowerPelletManager's Sunflower/GoldenWheat/
        // Rainbow pellet tiers), so RainbowRibbon is priced highest. CornHuskTrail is the plainest,
        // most thematically "common" of the four (matches the Corn Field starter world) and priced
        // lowest, just above a baseball cap's 50. Ember/SparkleDust sit in the middle as the two
        // visually flashier-but-not-flagship options. First-pass pricing, easy to retune per-asset
        // in the Inspector afterward — no design doc specifies these values.
        private static readonly TrailEntry[] Trails =
        {
            new TrailEntry("trail_cornhusk", "Corn Husk Trail", "CornHuskTrail.png", 60),
            new TrailEntry("trail_ember", "Ember Trail", "EmberTrail.png", 100),
            new TrailEntry("trail_sparkledust", "Sparkle Dust Trail", "SparkleDust.png", 100),
            new TrailEntry("trail_rainbowribbon", "Rainbow Ribbon Trail", "RainbowRibbon.png", 150),
        };

        [MenuItem("Farm Fury Arcade/Wire Cosmetic Art (Trails)")]
        public static void WireTrails()
        {
            Directory.CreateDirectory(CosmeticDataFolder);

            foreach (var entry in Trails)
            {
                string spritePath = $"{TrailSpriteFolder}/{entry.SpriteFileName}";
                Sprite sprite = ConfigureAndLoadSprite(spritePath);
                if (sprite == null)
                {
                    continue;
                }

                string dataPath = $"{CosmeticDataFolder}/CosmeticData_{entry.Id}.asset";
                var data = AssetDatabase.LoadAssetAtPath<CosmeticData>(dataPath);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<CosmeticData>();
                    AssetDatabase.CreateAsset(data, dataPath);
                }

                data.cosmeticId = entry.Id;
                data.setId = entry.Id;
                data.displayName = entry.DisplayName;
                data.cosmeticType = CosmeticType.Trail;
                data.coinCost = entry.CoinCost;
                data.previewSprite = sprite;
                // trailEffectPrefab intentionally left null — CosmeticData's own doc comment says
                // this falls back to a procedural placeholder effect, same "dedicated art with a
                // procedural fallback" convention PelletCollectBurst uses, since no trail-rendering
                // hook exists yet (CharacterCosmeticRenderer only handles Hat/Skin today — equipping
                // a Trail persists correctly via SaveManager but has no visible effect in gameplay
                // until that hook is built).
                EditorUtility.SetDirty(data);
            }

            foreach (string fileName in StoreChromeFiles)
            {
                ConfigureAndLoadSprite($"{StoreChromeFolder}/{fileName}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CosmeticWiringBuilder] Wired {Trails.Length} trail cosmetics with coin pricing (60/100/100/150) and imported {StoreChromeFiles.Length} Store UI chrome sprites (not yet wired into any screen — no cosmetics Store UI exists yet).");
        }

        [MenuItem("Farm Fury Arcade/Wire Cosmetic Art (Baseball Caps)")]
        public static void WireBaseballCaps()
        {
            Directory.CreateDirectory(CosmeticDataFolder);

            foreach (var entry in Caps)
            {
                string spritePath = $"{CosmeticSpriteFolder}/{entry.SpriteFileName}";
                Sprite sprite = ConfigureAndLoadSprite(spritePath);
                if (sprite == null)
                {
                    continue;
                }

                CosmeticData data = CreateOrLoadCosmeticData(entry.Character);
                string cosmeticId = $"baseball_cap_{entry.Character}".ToLowerInvariant();

                data.cosmeticId = cosmeticId;
                data.setId = "baseball_cap";
                data.displayName = "Baseball Cap";
                data.cosmeticType = CosmeticType.Hat;
                data.character = entry.Character;
                data.coinCost = 50;
                data.previewSprite = sprite;
                // Only one orientation exists yet — every slot reuses it, same convention
                // CharacterAnimator already falls back to for characters missing per-direction art.
                data.hatFrames = new[] { sprite, sprite, sprite, sprite, sprite, sprite, sprite, sprite };
                data.hatOffset = entry.HatOffset;
                data.hatScale = entry.HatScale;
                EditorUtility.SetDirty(data);

                AddCosmeticRendererToPrefab(entry.Character);
                // No longer force-equipped via SaveManager.DebugForceEquipForTesting — now that
                // CosmeticStoreScreen exists (Monetisation Build Plan Phase 4's real purchase
                // surface), caps go through the real 50-coin purchase flow like every other
                // cosmetic instead of being pre-owned for testing convenience.
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CosmeticWiringBuilder] Wired baseball caps for all 8 characters (50 coins each, purchasable via the Cosmetics Store).");
        }

        private static Sprite ConfigureAndLoadSprite(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[CosmeticWiringBuilder] Expected sprite not found, skipping: {path}");
                return null;
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[CosmeticWiringBuilder] Could not get TextureImporter for: {path}");
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;

            // Same PPU-equals-texture-width convention ArtWiringBuilder.ConfigureSpriteImporters
            // uses for every other sprite in the project.
            importer.GetSourceTextureWidthAndHeight(out int width, out int _);
            importer.spritePixelsPerUnit = width > 0 ? width : 100;

            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static CosmeticData CreateOrLoadCosmeticData(CharacterType character)
        {
            string path = $"{CosmeticDataFolder}/CosmeticData_BaseballCap_{character}.asset";
            var data = AssetDatabase.LoadAssetAtPath<CosmeticData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CosmeticData>();
                AssetDatabase.CreateAsset(data, path);
            }
            return data;
        }

        private static void AddCosmeticRendererToPrefab(CharacterType character)
        {
            string path = $"{CharacterPrefabFolder}/{character}.prefab";
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[CosmeticWiringBuilder] Character prefab not found, skipping: {path}");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(path);
            if (contents.GetComponent<CharacterCosmeticRenderer>() == null)
            {
                contents.AddComponent<CharacterCosmeticRenderer>();
            }
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
