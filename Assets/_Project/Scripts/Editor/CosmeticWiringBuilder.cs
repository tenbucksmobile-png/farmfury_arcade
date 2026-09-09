using System;
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
            // Optional dedicated Left-facing art (hatFrames[4]/[5]) — null means Left keeps
            // reusing the single front sprite every other slot falls back to. Both Ducky and
            // Woolly already have hasDedicatedRightArt=true on their own CharacterData (real
            // Wooly_right.png/Ducky_right.png — see ArtWiringBuilder.WireWoolly/WireDucky), so
            // their own base sprite's Right-facing pose is NEVER the flipX-mirrored-Left trick —
            // meaning a Left-only hat sprite here has no mirroring implications for Right at all;
            // Right simply keeps falling back to the single default sprite until dedicated Right
            // hat art exists too.
            public string LeftSpriteFileName;

            public BaseballCapEntry(CharacterType character, string spriteFileName, Vector2 hatOffset, float hatScale, string leftSpriteFileName = null)
            {
                Character = character;
                SpriteFileName = spriteFileName;
                HatOffset = hatOffset;
                HatScale = hatScale;
                LeftSpriteFileName = leftSpriteFileName;
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
            // Baseball_Woolly_left.png (2026-09-09) — real dedicated Left-facing cap art, applied
            // to hatFrames[4]/[5] only; every other slot (Up/Down/Right) still falls back to the
            // single front sprite as before.
            new BaseballCapEntry(CharacterType.Woolly, "Baseball_Woolly.png", new Vector2(0f, 0.47f), 0.96f, "Baseball_Woolly_left.png"),
            // Baseball_Ducky_left.png (2026-09-09) — same treatment.
            new BaseballCapEntry(CharacterType.Ducky, "Baseball_Ducky.png", new Vector2(0f, 0.53f), 0.70f, "Baseball_Ducky_left.png"),
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
                // trailEffectPrefab intentionally left null — reserved for a future dedicated
                // particle/VFX prefab. CharacterCosmeticRenderer's Trail path (added 2026-08-25,
                // extended 2026-09-08) already renders real in-gameplay art without one: it spawns
                // fading CosmeticTrailGhost afterimages of this asset's own previewSprite (the
                // CornHuskTrail.png/etc. wired just above) as the character moves, and only falls
                // back to a flat procedural TrailRenderer colour line if BOTH trailEffectPrefab and
                // previewSprite are unset.
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

        private struct UniversalHatEntry
        {
            public string Id;
            public string DisplayName;
            public string SpriteFileName;
            public Vector2 HatOffset;
            public float HatScale;
            public CharacterHatOverride[] CharacterOverrides;

            public UniversalHatEntry(string id, string displayName, string spriteFileName, Vector2 hatOffset, float hatScale, CharacterHatOverride[] characterOverrides = null)
            {
                Id = id;
                DisplayName = displayName;
                SpriteFileName = spriteFileName;
                HatOffset = hatOffset;
                HatScale = hatScale;
                CharacterOverrides = characterOverrides;
            }
        }

        // Cowboy Hat / Sombrero (2026-08-20 Shop redesign) — unlike the 8 per-character baseball
        // caps above, only one piece of art exists for each of these two styles (not fitted per
        // character), so each is a single universal CosmeticData asset instead of one per
        // character. The unframed source art (no wood-frame border) is used here for the in-game
        /// overlay sprite — the framed versions (FrameCowboy.png/FrameSombrero.png) are used only
        // as the Shop's own purchase-button icon, wired directly in Phase5ProjectBuilder. Offset/
        // scale are first-pass eyeballed against Cluck's own head size (this session has no visual
        // Editor/Play mode access) — same "expect to nudge later" convention as the baseball caps.
        private static readonly UniversalHatEntry[] UniversalHats =
        {
            new UniversalHatEntry(IAPManagerHatCowboyId, "Cowboy Hat", "kling_20260818_IMAGE_Prop_shots_3923_0.png", new Vector2(0f, 0.55f), 0.62f),
            // Original source file (kling_20260818_IMAGE_isolated_g_4590_0.png) was deleted from
            // disk and replaced with 4 new re-generated variants (2026-09-08) — Sombrero_1.png
            // (classic red/green/tan) picked as the most immediately recognisable of the 4 for a
            // universal asset shown across all 8 characters; Sombrero_2/3/4.png (cow-print, pink/
            // green floral, orange with pom-poms) are real, usable art too if a different look or a
            // future second sombrero style is ever wanted — just swap the filename here.
            // Sized/positioned against several direct gameplay screenshots (2026-09-08), across
            // multiple rounds — the shared default alone went 0.55 -> 0.35 -> 0.45 -> 0.65 as
            // "too low" feedback kept coming in for characters with no per-character override
            // (Cluck/Ducky/Horace/Gerald/Billy) even after the ones WITH an override (Percy/
            // Woolly/Bessie, below) were separately raised twice each. This shared value is this
            // asset's fallback for any character with no entry in SombreroCharacterOverrides — a
            // single universal number was never going to fit every character's head equally
            // (confirmed once a screenshot showed it specifically mis-fit on Percy), but the whole
            // group was ALSO reading too low at the same time, hence raising both independently
            // here rather than assuming the override characters were the only ones affected.
            new UniversalHatEntry(IAPManagerHatSombreroId, "Sombrero", "Sombrero_1.png", new Vector2(0f, 0.65f), 1.15f, SombreroCharacterOverrides),
        };

        // Per-character overrides of the shared Sombrero offset/scale above — added once real
        // gameplay screenshots showed it badly mis-fit on specific characters. First-pass
        // eyeballed corrections (no visual Editor/Play mode access this session) — nudge further
        // once seen. "We will reposition for every character" per direct feedback — add further
        // entries here as each remaining character gets checked in Play mode.
        //
        // Offset.y history, all per direct "too low"/"too high" feedback rounds: Percy 0.45(shared)
        // -> 0.30 -> 0.43 -> 0.55 -> 0.75; Woolly 0.45(shared) -> 0.28 -> 0.40 -> 0.60; Bessie
        // 0.45(shared) -> 0.47 -> 0.60 -> 0.80. Each raise moved every character (shared default
        // included) up together once "still too low" feedback covered the whole group rather than
        // just the ones with their own override entry.
        private static readonly CharacterHatOverride[] SombreroCharacterOverrides =
        {
            // Percy's baseball cap asset (see Caps above, tuned for his actual head) uses
            // offset.y 0.51 / scale 0.77 for a snug-fitting cap; the sombrero's wide brim still
            // needs to render smaller than the universal 1.15 default.
            new CharacterHatOverride
            {
                character = CharacterType.Percy,
                hatOffset = new Vector2(0f, 0.75f),
                hatScale = 0.85f,
            },
            // Woolly's own baseball cap scale (0.96, the largest of the 8 — his fluffy head reads
            // wide) means the sombrero's width was already roughly right; only its height needed
            // tuning, same as Percy.
            new CharacterHatOverride
            {
                character = CharacterType.Woolly,
                hatOffset = new Vector2(0f, 0.60f),
                hatScale = 1.0f,
            },
            // Bessie's baseball cap is the smallest of the 8 (scale 0.58, offset.y 0.55) — the
            // universal 1.15 sombrero scale rendered wildly oversized and floating well off to the
            // side of her head. Scale kept from the first correction; offset raised per the same
            // "still too low" feedback Percy got.
            new CharacterHatOverride
            {
                character = CharacterType.Bessie,
                hatOffset = new Vector2(0f, 0.80f),
                hatScale = 0.64f,
            },
            // Cluck was fine at the shared 0.65 default for every other character but read as
            // "slightly too high" on her specifically — a small nudge down, not the large
            // corrections the other 3 overrides above needed.
            new CharacterHatOverride
            {
                character = CharacterType.Cluck,
                hatOffset = new Vector2(0f, 0.58f),
                hatScale = 1.15f,
            },
        };

        // Local copies of IAPManager's cosmeticId constants — CosmeticWiringBuilder is an Editor-
        // only assembly and shouldn't need a runtime-assembly reference just for two string
        // literals; keep these in sync with IAPManager.CowboyHatCosmeticId/SombreroCosmeticId by
        // hand if either ever changes (both are load-bearing PlayerPrefs ownership keys, so neither
        // should change without a save-data migration anyway — see CosmeticData.cosmeticId's own
        // doc comment).
        private const string IAPManagerHatCowboyId = "cowboy_hat";
        private const string IAPManagerHatSombreroId = "sombrero_hat";

        [MenuItem("Farm Fury Arcade/Wire Cosmetic Art (Universal Hats)")]
        public static void WireUniversalHats()
        {
            Directory.CreateDirectory(CosmeticDataFolder);

            foreach (var entry in UniversalHats)
            {
                string spritePath = $"{CosmeticSpriteFolder}/{entry.SpriteFileName}";
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
                data.cosmeticType = CosmeticType.Hat;
                // character is left at its enum default — this asset is equipped identically
                // regardless of which character is active (see IAPManager.GrantAndEquipHat), so
                // the per-character field GetCosmeticsForCharacter would normally filter on is
                // simply unused for this asset.
                data.coinCost = 0; // sold via real-money IAP now, not coins — see IAPManager.
                data.previewSprite = sprite;
                data.hatFrames = new[] { sprite, sprite, sprite, sprite, sprite, sprite, sprite, sprite };
                data.hatOffset = entry.HatOffset;
                data.hatScale = entry.HatScale;
                data.characterHatOverrides = entry.CharacterOverrides;
                EditorUtility.SetDirty(data);
            }

            // Ensures CharacterCosmeticRenderer exists on every character prefab even if this menu
            // item is run before "Wire Cosmetic Art (Baseball Caps)" — both hat sources need the
            // same component, so whichever runs first should leave every prefab ready for either.
            foreach (CharacterType character in Enum.GetValues(typeof(CharacterType)))
            {
                AddCosmeticRendererToPrefab(character);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CosmeticWiringBuilder] Wired universal Cowboy Hat / Sombrero cosmetics ($3.99 IAP each, applies to whichever character is active on purchase).");
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
                var hatFrames = new[] { sprite, sprite, sprite, sprite, sprite, sprite, sprite, sprite };
                data.mirrorLeftHatForRight = false;
                if (entry.LeftSpriteFileName != null)
                {
                    Sprite leftSprite = ConfigureAndLoadSprite($"{CosmeticSpriteFolder}/{entry.LeftSpriteFileName}");
                    if (leftSprite != null)
                    {
                        // [Up0,Up1,Down0,Down1,Left0,Left1,Right0,Right1] — indices 4/5 are Left.
                        // Right (6/7) gets the SAME sprite reference — this character already has
                        // real dedicated Right art for its own body (Ducky/Woolly both do), so a
                        // Left-only hat would otherwise show the plain default sprite while facing
                        // right, visibly out of sync with the body's real turn. Duplicating it here
                        // and flipping at render time (mirrorLeftHatForRight, read by
                        // CharacterCosmeticRenderer) makes the hat track the body's facing
                        // direction correctly, same "flip Left to fake Right" trick
                        // CharacterAnimator itself uses for a character with no dedicated Right art.
                        hatFrames[4] = leftSprite;
                        hatFrames[5] = leftSprite;
                        hatFrames[6] = leftSprite;
                        hatFrames[7] = leftSprite;
                        data.mirrorLeftHatForRight = true;
                    }
                }
                data.hatFrames = hatFrames;
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
