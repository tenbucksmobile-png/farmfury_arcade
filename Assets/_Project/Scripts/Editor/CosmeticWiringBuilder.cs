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
            // reusing the single front sprite every other slot falls back to.
            public string LeftSpriteFileName;
            // Optional dedicated Right-facing art (hatFrames[6]/[7]) — added 2026-09-11 once real
            // per-character Right art landed for all 8 characters. When set, this is used directly
            // (mirrorLeftHatForRight stays false) instead of the old "duplicate Left, flip at
            // render time" fallback — a real Right-facing render reads better than a mirrored Left
            // one, and every character now has one.
            public string RightSpriteFileName;

            // Optional (see CosmeticData.hasSideOffset) — a character whose Left/Right walk pose
            // puts its head somewhere very different than its Front pose (e.g. a galloping horse)
            // needs its own offset for the side-facing directions, or the cap floats disconnected
            // from the head as the character moves. Authored as though always facing Left; Right
            // mirrors the X component automatically at runtime.
            public bool HasSideOffset;
            public Vector2 HatOffsetSide;
            public float HatScaleSide;

            public BaseballCapEntry(CharacterType character, string spriteFileName, Vector2 hatOffset, float hatScale, string leftSpriteFileName = null, string rightSpriteFileName = null, Vector2? hatOffsetSide = null, float hatScaleSide = 0f)
            {
                Character = character;
                SpriteFileName = spriteFileName;
                HatOffset = hatOffset;
                HatScale = hatScale;
                LeftSpriteFileName = leftSpriteFileName;
                RightSpriteFileName = rightSpriteFileName;
                HasSideOffset = hatOffsetSide.HasValue;
                HatOffsetSide = hatOffsetSide ?? Vector2.zero;
                HatScaleSide = hatOffsetSide.HasValue ? hatScaleSide : 0f;
            }
        }

        // Head-width/head-height eyeballed against each character's own *_front.png. hatOffset.y is
        // roughly (canvas-center-to-head-top)/500 in world units; hatScale targets the cap's
        // rendered width landing close to that character's own head width.
        // 2026-08-18 playtest feedback: caps sat too low (sunk toward the face) on every character.
        // Raised hatOffset.y by +0.15 world units across the board as a first-pass correction —
        // still eyeballed (no live render access), re-tune further if it overshoots.
        // 2026-09-11: real per-character Left AND Right art now exists for all 8 characters (a
        // much bigger art drop than the earlier Ducky/Woolly-only Left art) — every entry below now
        // wires a genuine 3-pose set (Front for Up/Down, real Left, real Right), so the cap actually
        // turns with the character in every direction instead of only Left (or nothing at all).
        // Filenames match on-disk casing exactly (AssetDatabase.LoadAssetAtPath is case-sensitive
        // regardless of OS filesystem, same gotcha CLAUDE.md documents elsewhere) — note
        // "Baseball_horace_right.png" has a lowercase 'h', unlike every other Horace file.
        private static readonly BaseballCapEntry[] Caps =
        {
            new BaseballCapEntry(CharacterType.Cluck, "Baseball_Clucky.png", new Vector2(0f, 0.55f), 0.64f, "Baseball_Clucky_left.png", "Baseball_Clucky_right.png"),
            // Scale corrected 2026-09-12 (0.58 -> 0.32) — the Cosmetic Preview Renderer's latest
            // render showed it wildly oversized, covering her whole head/horns/ears like a full
            // helmet rather than sitting on top of it (same class of bug Woolly's own cap had before
            // its 2026-09-11 fix below). Offset lowered to match the smaller cap (0.55 -> 0.46) — a
            // cap this much smaller needs less headroom above her to still sit ON her head rather
            // than floating.
            new BaseballCapEntry(CharacterType.Bessie, "Baseball_Bessie.png", new Vector2(0f, 0.46f), 0.32f, "Baseball_Bessie_left.png", "Baseball_Bessie_right.png"),
            new BaseballCapEntry(CharacterType.Percy, "Baseball_Percy.png", new Vector2(0f, 0.51f), 0.77f, "Baseball_Percy_left.png", "Baseball_Percy_right.png"),
            // Woolly's scale corrected 2026-09-11 (0.96 -> 0.50) — the Cosmetic Preview Renderer's
            // first real render showed it comically oversized, covering her whole head like a
            // helmet rather than sitting on top of it. Offset nudged down slightly (0.47 -> 0.40)
            // since a smaller cap needs less headroom above her.
            new BaseballCapEntry(CharacterType.Woolly, "Baseball_Woolly.png", new Vector2(0f, 0.40f), 0.50f, "Baseball_Woolly_left.png", "Baseball_Woolly_right.png"),
            new BaseballCapEntry(CharacterType.Ducky, "Baseball_Ducky.png", new Vector2(0f, 0.53f), 0.70f, "Baseball_Ducky_left.png", "Baseball_Ducky_right.png"),
            // Side offset added 2026-09-12 — despite having real dedicated Left/Right art, the
            // preview still showed the cap floating well above and behind Horace's actual head in
            // his galloping Left/Right pose (his stride/head-drop is far more pronounced than any
            // other character's, so even real per-direction art isn't enough on its own — the cap's
            // POSITION is still driven by one fixed offset regardless of pose). Authored as though
            // facing Left (head drops and swings forward-left); mirrored automatically for Right.
            new BaseballCapEntry(CharacterType.Horace, "Baseball_Horace.png", new Vector2(0f, 0.47f), 0.70f, "Baseball_Horace_left.png", "Baseball_horace_right.png",
                hatOffsetSide: new Vector2(-0.35f, 0.30f), hatScaleSide: 0.70f),
            // Scale corrected 2026-09-11 (0.45 -> 0.70) — the opposite problem from Woolly: the
            // preview showed his cap tiny and floating well above his head, barely visible. Offset
            // lowered too (0.53 -> 0.40) so the larger cap actually sits close to his head instead of
            // opening an even bigger gap now that it's bigger. Corrected AGAIN 2026-09-12 — the
            // latest render showed it had overshot the other way, the enlarged cap now sinking down
            // and covering his whole short neck/head. Gerald's head/neck is much smaller than most
            // other characters', so a "normal" cap scale still reads as oversized on him
            // specifically — scaled down further (0.70 -> 0.55) and raised (0.40 -> 0.50).
            new BaseballCapEntry(CharacterType.Gerald, "Baseball_Gerald.png", new Vector2(0f, 0.50f), 0.55f, "Baseball_Gerald_left.png", "Baseball_Gerald_right.png"),
            new BaseballCapEntry(CharacterType.Billy, "Baseball_Billy.png", new Vector2(0f, 0.47f), 0.58f, "Baseball_Billy_left.png", "Baseball_Billy_right.png"),
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
            // Confetti / Bubbles (2026-09-11) — 5th/6th trails, real art dropped under
            // CosmeticType.Trail/ as ConfettiTrail.png/BubblesTrail.png. coinCost is unused dead
            // weight on every trail here now (all 6 are sold via real-money IAP, see IAPManager's
            // TrailConfettiProductId/TrailBubblesProductId) — kept populated only for consistency
            // with the other 4 entries, same "field still exists, unused for these" note CLAUDE.md
            // already carries for cosmetics.
            new TrailEntry("trail_confetti", "Confetti Trail", "ConfettiTrail.png", 120),
            new TrailEntry("trail_bubbles", "Bubbles Trail", "BubblesTrail.png", 120),
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
            Debug.Log($"[CosmeticWiringBuilder] Wired {Trails.Length} trail cosmetics ($1.99 real-money IAP each) and imported {StoreChromeFiles.Length} Store UI chrome sprites.");
        }

        private struct UniversalHatEntry
        {
            public string Id;
            public string DisplayName;
            public string SpriteFileName;
            public Vector2 HatOffset;
            public float HatScale;
            public CharacterHatOverride[] CharacterOverrides;

            // Optional alternate single-pose designs (e.g. Sombrero's 4 variants) — see
            // CosmeticData.hatVariantSprites. Null/empty means this hat always shows SpriteFileName.
            public string[] VariantSpriteFileNames;

            public UniversalHatEntry(string id, string displayName, string spriteFileName, Vector2 hatOffset, float hatScale, CharacterHatOverride[] characterOverrides = null, string[] variantSpriteFileNames = null)
            {
                Id = id;
                DisplayName = displayName;
                SpriteFileName = spriteFileName;
                HatOffset = hatOffset;
                HatScale = hatScale;
                CharacterOverrides = characterOverrides;
                VariantSpriteFileNames = variantSpriteFileNames;
            }
        }

        // Per-character overrides of the shared Sombrero offset/scale below — added once real
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
        //
        // MOVED HERE 2026-09-12 (was declared AFTER UniversalHats, which references it) — this was
        // a real, previously-undiscovered bug: C# initializes static fields in the textual order
        // they're declared in the class, so UniversalHats' own initializer was reading this field
        // (and ChefHatCharacterOverrides below) BEFORE either had been assigned, always seeing
        // `null` regardless of what either array actually contained. Verified directly against a
        // standalone repro (a class with the same forward-reference shape prints the default value,
        // not the later initializer's value). Every per-character Sombrero override tuned across
        // every session documented above (Percy/Woolly/Bessie/Cluck) has therefore NEVER actually
        // applied at runtime — every character has only ever rendered the shared default the whole
        // time, which is the real reason "still too low" kept being reported even after repeatedly
        // "fixing" a specific character's own override: raising the SHARED default was the only
        // edit that was ever doing anything, and every override-only edit was silently a no-op.
        // Reordering these two arrays to be declared BEFORE UniversalHats (which now genuinely reads
        // fully-initialized arrays) is the actual fix — re-run Wire Cosmetic Art (Universal Hats)
        // and Render Cosmetic Preview Sheet to see the per-character values finally take effect.
        private static readonly CharacterHatOverride[] SombreroCharacterOverrides =
        {
            // Percy's baseball cap asset (see Caps above, tuned for his actual head) uses
            // offset.y 0.51 / scale 0.77 for a snug-fitting cap; the sombrero's wide brim still
            // needs to render smaller than the universal default.
            // Rescaled 2026-09-11 alongside the shared default above (x0.565 scale, -0.20 offset)
            // — see that entry's own comment.
            new CharacterHatOverride
            {
                character = CharacterType.Percy,
                hatOffset = new Vector2(0f, 0.55f),
                hatScale = 0.48f,
            },
            // Woolly's own baseball cap scale (now 0.50, corrected the same session — see Caps
            // above) means the sombrero's width was already roughly right; only its height needed
            // tuning, same as Percy. Rescaled 2026-09-11 alongside the shared default.
            new CharacterHatOverride
            {
                character = CharacterType.Woolly,
                hatOffset = new Vector2(0f, 0.40f),
                hatScale = 0.57f,
            },
            // Bessie's baseball cap is the smallest of the 8 — the universal sombrero scale
            // rendered wildly oversized and floating well off to the side of her head. Rescaled
            // 2026-09-11 alongside the shared default. Corrected AGAIN 2026-09-12 — the latest
            // preview showed this was still sitting far too low, covering her eyes/ears/whole face
            // rather than sitting on top of her head. Raised substantially (0.60 -> 0.88) and
            // shrunk slightly further (0.36 -> 0.30). (This is the first time this override will
            // actually render at all — see the field-ordering bug note above.)
            new CharacterHatOverride
            {
                character = CharacterType.Bessie,
                hatOffset = new Vector2(0f, 0.88f),
                hatScale = 0.30f,
            },
            // Cluck was fine at the shared default for every other character but read as
            // "slightly too high" on her specifically — a small nudge down, not the large
            // corrections the other 3 overrides above needed. Rescaled 2026-09-11 alongside the
            // shared default (this puts her back at exactly the new shared value, same as before
            // this rescale — she was already tracking the shared default 1:1).
            new CharacterHatOverride
            {
                character = CharacterType.Cluck,
                hatOffset = new Vector2(0f, 0.30f),
                hatScale = 0.65f,
            },
            // Side offset added 2026-09-12 — Horace's Front pose was fine at the shared default
            // (front values here just duplicate it), but his galloping Left/Right pose drops and
            // swings his head forward-left/right far more dramatically than any other character
            // (confirmed across every hat type in the preview render, not unique to the sombrero),
            // so a single fixed offset leaves the hat floating well above/behind wherever his head
            // actually ends up. Authored as though facing Left; mirrored for Right.
            new CharacterHatOverride
            {
                character = CharacterType.Horace,
                hatOffset = new Vector2(0f, 0.45f),
                hatScale = 0.65f,
                hasSideOffset = true,
                hatOffsetSide = new Vector2(-0.35f, 0.35f),
                hatScaleSide = 0.65f,
            },
        };

        // Per-character overrides of Chef Hat's shared offset/scale — added 2026-09-12 once the
        // Cosmetic Preview Renderer showed Gerald's own short neck/small head (already documented
        // on his Baseball Cap fix above) left a visible gap between the hat's own bottom rim and the
        // top of his head, unlike every other character which fit reasonably close. Declared before
        // UniversalHats for the same real ordering-bug reason SombreroCharacterOverrides was moved
        // above — see that field's own doc comment.
        private static readonly CharacterHatOverride[] ChefHatCharacterOverrides =
        {
            new CharacterHatOverride
            {
                character = CharacterType.Gerald,
                hatOffset = new Vector2(0f, 0.72f),
                hatScale = 0.42f,
            },
        };

        // Sombrero, Chef Hat, Crown — genuinely universal, single-piece-of-art-fits-every-character
        // cosmetics. Cowboy Hat used to live here too (its original art, kling_20260818_IMAGE_
        // Prop_shots_3923_0.png, was a single pig-face design shared by every character) but was
        // moved out 2026-09-11 once a full set of real PER-CHARACTER Cowboy Hat art landed —
        // see CowboyHats/WireCowboyHats below, same per-character shape as the baseball caps.
        private static readonly UniversalHatEntry[] UniversalHats =
        {
            // Sombrero_1.png (classic red/green/tan) is the DEFAULT/fallback single-pose sprite
            // (data.hatFrames, used if hatVariantSprites is ever empty) — but the real 4-variant
            // array below (VariantSpriteFileNames) is what actually shows in-game: CosmeticData.
            // hatVariantSprites, read by CharacterCosmeticRenderer.ResolveActiveHatFrames, picks one
            // of the 4 deterministically from the current level's own levelNumber every time the
            // character (re)spawns — so an equipped Sombrero visibly cycles through all 4 designs
            // (classic, cow-print, pink/green floral, orange-with-pom-poms) as the player progresses
            // through levels, rather than always showing the same one. Offset/scale (shared below,
            // plus SombreroCharacterOverrides) apply identically to all 4, since they're the same
            // general shape/size — only the surface pattern differs between them.
            //
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
            //
            // Scale corrected 2026-09-11 (1.15 -> 0.65, offset 0.65 -> 0.45) — the Cosmetic Preview
            // Renderer's first real render showed it badly oversized on every character, the brim
            // clipping the top of frame. All 4 SombreroCharacterOverrides below were rescaled by the
            // same ratio (0.65/1.15) and shifted by the same offset delta (-0.20) to keep their
            // relative sizing to each other and to the new shared default intact, rather than
            // leaving them huge while only the fallback shrank.
            new UniversalHatEntry(IAPManagerHatSombreroId, "Sombrero", "Sombrero_1.png", new Vector2(0f, 0.45f), 0.65f, SombreroCharacterOverrides,
                new[] { "Sombrero_1.png", "Sombrero_2.png", "Sombrero_3.png", "Sombrero_4.png" }),
            // Chef Hat / Crown (2026-09-11) — 4th/5th universal hats, brings the Shop's hat row to
            // parity with the 4 already-shipped trails. Both source files (ChefHat.png/Crown.png)
            // fill nearly their whole 500x500 canvas edge-to-edge (measured: ~98% both dimensions),
            // same "large standalone prop render, not pre-scaled to a head" situation the baseball
            // caps and Cowboy Hat's own pig-face art were in — so both start at a scale well below 1,
            // closer to Cowboy Hat's 0.62 than Sombrero_1's tighter-cropped 1.15. Offset.y 0.55
            // matches Cowboy Hat's own starting value. First-pass eyeballed (no visual Editor/Play
            // mode access this session) — same "expect to nudge later" convention as every other hat
            // here; use Farm Fury Arcade > Debug > Render Cosmetic Preview Sheet to check/tune
            // without needing Play mode at all.
            new UniversalHatEntry(IAPManagerHatChefId, "Chef Hat", "ChefHat.png", new Vector2(0f, 0.55f), 0.55f, ChefHatCharacterOverrides),
            new UniversalHatEntry(IAPManagerHatCrownId, "Crown", "Crown.png", new Vector2(0f, 0.55f), 0.55f),
        };

        // Local copies of IAPManager's cosmeticId constants — CosmeticWiringBuilder is an Editor-
        // only assembly and shouldn't need a runtime-assembly reference just for two string
        // literals; keep these in sync with IAPManager.CowboyHatCosmeticId/SombreroCosmeticId by
        // hand if either ever changes (both are load-bearing PlayerPrefs ownership keys, so neither
        // should change without a save-data migration anyway — see CosmeticData.cosmeticId's own
        // doc comment).
        private const string IAPManagerHatCowboyId = "cowboy_hat";
        private const string IAPManagerHatSombreroId = "sombrero_hat";
        private const string IAPManagerHatChefId = "chef_hat";
        private const string IAPManagerHatCrownId = "crown";

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

                if (entry.VariantSpriteFileNames != null && entry.VariantSpriteFileNames.Length > 0)
                {
                    var variants = new Sprite[entry.VariantSpriteFileNames.Length];
                    for (int i = 0; i < entry.VariantSpriteFileNames.Length; i++)
                    {
                        variants[i] = ConfigureAndLoadSprite($"{CosmeticSpriteFolder}/{entry.VariantSpriteFileNames[i]}");
                    }
                    data.hatVariantSprites = variants;
                }
                else
                {
                    data.hatVariantSprites = null;
                }
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
            Debug.Log("[CosmeticWiringBuilder] Wired universal Sombrero (4 cycling variants) / Chef Hat / Crown cosmetics ($1.99 IAP each, applies to whichever character is active on purchase).");
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
                // Front sprite covers Up/Down (no dedicated back/up art exists for any cap) — every
                // slot starts here, then Left/Right below override their own 2 slots when real art
                // exists for them.
                var hatFrames = new[] { sprite, sprite, sprite, sprite, sprite, sprite, sprite, sprite };
                data.mirrorLeftHatForRight = false;

                Sprite leftSprite = entry.LeftSpriteFileName != null
                    ? ConfigureAndLoadSprite($"{CosmeticSpriteFolder}/{entry.LeftSpriteFileName}")
                    : null;
                if (leftSprite != null)
                {
                    // [Up0,Up1,Down0,Down1,Left0,Left1,Right0,Right1] — indices 4/5 are Left.
                    hatFrames[4] = leftSprite;
                    hatFrames[5] = leftSprite;
                }

                Sprite rightSprite = entry.RightSpriteFileName != null
                    ? ConfigureAndLoadSprite($"{CosmeticSpriteFolder}/{entry.RightSpriteFileName}")
                    : null;
                if (rightSprite != null)
                {
                    // Real dedicated Right art — use it directly, no mirroring needed.
                    hatFrames[6] = rightSprite;
                    hatFrames[7] = rightSprite;
                }
                else if (leftSprite != null)
                {
                    // No real Right art for this character — fall back to the old "duplicate Left,
                    // flip at render time" trick (mirrorLeftHatForRight, read by
                    // CharacterCosmeticRenderer), same as every hat used before real Right art
                    // existed for anyone.
                    hatFrames[6] = leftSprite;
                    hatFrames[7] = leftSprite;
                    data.mirrorLeftHatForRight = true;
                }
                data.hatFrames = hatFrames;
                data.hatOffset = entry.HatOffset;
                data.hatScale = entry.HatScale;
                data.hasSideOffset = entry.HasSideOffset;
                data.hatOffsetSide = entry.HatOffsetSide;
                data.hatScaleSide = entry.HatScaleSide;
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

        private struct CowboyHatEntry
        {
            public CharacterType Character;
            // Right-facing art — the "default" pose, also reused for Up/Down (see WireCowboyHats'
            // own doc comment for why: this art has no true front/back pose at all, only Left and
            // Right). Null for Gerald/Percy, who currently only have Left art.
            public string RightSpriteFileName;
            public string LeftSpriteFileName;
            public Vector2 HatOffset;
            public float HatScale;

            // Optional (see CosmeticData.hasSideOffset) — same purpose as BaseballCapEntry's own
            // side-offset fields.
            public bool HasSideOffset;
            public Vector2 HatOffsetSide;
            public float HatScaleSide;

            public CowboyHatEntry(CharacterType character, string rightSpriteFileName, string leftSpriteFileName, Vector2 hatOffset, float hatScale, Vector2? hatOffsetSide = null, float hatScaleSide = 0f)
            {
                Character = character;
                RightSpriteFileName = rightSpriteFileName;
                LeftSpriteFileName = leftSpriteFileName;
                HatOffset = hatOffset;
                HatScale = hatScale;
                HasSideOffset = hatOffsetSide.HasValue;
                HatOffsetSide = hatOffsetSide ?? Vector2.zero;
                HatScaleSide = hatOffsetSide.HasValue ? hatScaleSide : 0f;
            }
        }

        // Cowboy Hat (2026-09-11) — converted from one universal single-pose asset (see
        // UniversalHats' own doc comment) to a per-character set once real directional art landed,
        // same shape as the baseball caps: one CosmeticData asset per character, cosmeticId
        // "cowboy_hat_<character>", granted as a full set on one $1.99 IAP purchase (see
        // IAPManager.GrantCowboyHatSet, mirroring GrantBaseballCapSet).
        //
        // Unlike baseball caps, this art has NO true front-facing pose at all — only Left and
        // Right (per direct instruction: "either one can be used for up and down") — so Up/Down
        // both reuse whichever of Right/Left is available (Right preferred, since it's the "default"
        // slot every character but Gerald has — Percy's own Right art landed 2026-09-11).
        //
        // Filenames match on-disk casing exactly (case-sensitive regardless of OS filesystem, same
        // gotcha CLAUDE.md documents elsewhere) — note "Cowboy_horace.png" (Right) is lowercase
        // while "Cowboy_Horace_left.png" (Left) is capitalized, and every character's own files use
        // "Clucky" not "Cluck" in the filename despite the CharacterType enum being Cluck.
        //
        // Bessie's art landed 2026-09-11 (Cowboy_bessie.png — lowercase 'b', unlike her own
        // Cowboy_Bessie_left.png which IS capitalized; same per-file casing inconsistency every
        // other character's Cowboy Hat art already has) — the "no Bessie art" gap flagged when this
        // array was first built is closed.
        //
        // hatOffset/hatScale below are first-pass estimates carried over from each character's own
        // baseball cap values (a reasonable starting point — the head size driving the fit is the
        // same character either way) — expect to nudge via the Cosmetic Preview Renderer tool.
        private static readonly CowboyHatEntry[] CowboyHats =
        {
            new CowboyHatEntry(CharacterType.Cluck, "Cowboy_Clucky.png", "Cowboy_Clucky_left.png", new Vector2(0f, 0.55f), 0.64f),
            new CowboyHatEntry(CharacterType.Bessie, "Cowboy_bessie.png", "Cowboy_Bessie_left.png", new Vector2(0f, 0.55f), 0.58f),
            // Cowboy_Percy.png (Right) landed 2026-09-11, closing the last "Left-only" gap — Gerald
            // remains the only character still missing a Right/default pose. Offset raised (0.51 ->
            // 0.80) after the preview showed the brim sunk down over his eyes — overshot, floating
            // almost entirely off the top of frame, so pulled back down to 0.62 (between the two).
            new CowboyHatEntry(CharacterType.Percy, "Cowboy_Percy.png", "Cowboy_Percy_left.png", new Vector2(0f, 0.62f), 0.77f),
            // Scale corrected 2026-09-11 (0.96 -> 0.50), same fix and same reason as her Baseball
            // Cap entry above — the preview showed it comically oversized, covering her whole head.
            // Offset needed a SEPARATE correction from the Baseball Cap fix, though (0.40 -> 0.65)
            // — the same offset.y that sat a baseball cap correctly on her wool poof left this
            // hat's own art (a taller cowboy-hat silhouette with a different internal vertical
            // anchor point) sunk down over her whole face instead. Confirms hatOffset/hatScale
            // don't transfer 1:1 between different hat STYLES on the same character, only within
            // the same style.
            // Side offset added 2026-09-12 — the Front pose (offset above) looked fine, but the
            // latest preview render showed the hat floating well above/behind her head in the
            // Left/Right walking pose (her walk cycle lowers/shifts her head forward, this fixed
            // offset didn't follow). Authored as though facing Left; mirrored for Right.
            new CowboyHatEntry(CharacterType.Woolly, "Cowboy_Woolly.png", "Cowboy_Woolly_left.png", new Vector2(0f, 0.55f), 0.50f,
                hatOffsetSide: new Vector2(-0.18f, 0.40f), hatScaleSide: 0.50f),
            // Offset nudged up slightly (0.53 -> 0.60) — preview showed the brim sitting a touch
            // low, partly covering one eye.
            new CowboyHatEntry(CharacterType.Ducky, "Cowboy_Ducky.png", "Cowboy_Ducky_left.png", new Vector2(0f, 0.60f), 0.70f),
            // Side offset added 2026-09-12 — Horace's galloping Left/Right pose drops and swings his
            // head forward far more dramatically than any other character (confirmed across every
            // hat type in the preview, not just this one), so the hat floats disconnected above/
            // behind the actual head whenever he's moving. Authored as though facing Left; mirrored
            // for Right.
            new CowboyHatEntry(CharacterType.Horace, "Cowboy_horace.png", "Cowboy_Horace_left.png", new Vector2(0f, 0.47f), 0.70f,
                hatOffsetSide: new Vector2(-0.35f, 0.30f), hatScaleSide: 0.70f),
            new CowboyHatEntry(CharacterType.Gerald, null, "Cowboy_Gerald_left.png", new Vector2(0f, 0.53f), 0.45f),
            new CowboyHatEntry(CharacterType.Billy, "Cowboy_Billy.png", "Cowboy_Billy_left.png", new Vector2(0f, 0.47f), 0.58f),
        };

        [MenuItem("Farm Fury Arcade/Wire Cosmetic Art (Cowboy Hats)")]
        public static void WireCowboyHats()
        {
            Directory.CreateDirectory(CosmeticDataFolder);

            foreach (var entry in CowboyHats)
            {
                Sprite rightSprite = entry.RightSpriteFileName != null
                    ? ConfigureAndLoadSprite($"{CosmeticSpriteFolder}/{entry.RightSpriteFileName}")
                    : null;
                Sprite leftSprite = entry.LeftSpriteFileName != null
                    ? ConfigureAndLoadSprite($"{CosmeticSpriteFolder}/{entry.LeftSpriteFileName}")
                    : null;

                if (rightSprite == null && leftSprite == null)
                {
                    // No art at all for this character (or it failed to load) — nothing to wire.
                    continue;
                }

                CosmeticData data = CreateOrLoadCosmeticData(entry.Character, "CowboyHat");
                string cosmeticId = $"cowboy_hat_{entry.Character}".ToLowerInvariant();

                data.cosmeticId = cosmeticId;
                data.setId = "cowboy_hat";
                data.displayName = "Cowboy Hat";
                data.cosmeticType = CosmeticType.Hat;
                data.character = entry.Character;
                data.coinCost = 0; // sold via real-money IAP, not coins — see IAPManager.

                data.mirrorLeftHatForRight = false;
                Sprite defaultSprite; // used for Up/Down and previewSprite
                Sprite[] hatFrames;
                if (rightSprite != null && leftSprite != null)
                {
                    defaultSprite = rightSprite;
                    hatFrames = new[] { rightSprite, rightSprite, rightSprite, rightSprite, leftSprite, leftSprite, rightSprite, rightSprite };
                }
                else if (rightSprite != null)
                {
                    // Right-only (shouldn't happen given the current art, but handled defensively) —
                    // every slot falls back to it.
                    defaultSprite = rightSprite;
                    hatFrames = new[] { rightSprite, rightSprite, rightSprite, rightSprite, rightSprite, rightSprite, rightSprite, rightSprite };
                }
                else
                {
                    // Left-only (Gerald) — Up/Down/Right all fall back to Left, mirrored for
                    // Right via mirrorLeftHatForRight so it at least visually turns with the
                    // character instead of always showing the Left-facing art unmirrored.
                    defaultSprite = leftSprite;
                    hatFrames = new[] { leftSprite, leftSprite, leftSprite, leftSprite, leftSprite, leftSprite, leftSprite, leftSprite };
                    data.mirrorLeftHatForRight = true;
                }

                data.previewSprite = defaultSprite;
                data.hatFrames = hatFrames;
                data.hatOffset = entry.HatOffset;
                data.hatScale = entry.HatScale;
                data.hasSideOffset = entry.HasSideOffset;
                data.hatOffsetSide = entry.HatOffsetSide;
                data.hatScaleSide = entry.HatScaleSide;
                data.characterHatOverrides = null;
                data.hatVariantSprites = null;
                EditorUtility.SetDirty(data);

                AddCosmeticRendererToPrefab(entry.Character);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CosmeticWiringBuilder] Wired Cowboy Hat for all 8 characters. $1.99 IAP, one purchase grants every character's variant.");
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

        private static CosmeticData CreateOrLoadCosmeticData(CharacterType character) =>
            CreateOrLoadCosmeticData(character, "BaseballCap");

        /// <summary>Generic per-character CosmeticData asset load-or-create, keyed by an asset-name
        /// prefix (e.g. "BaseballCap" -&gt; CosmeticData_BaseballCap_&lt;character&gt;.asset,
        /// "CowboyHat" -&gt; CosmeticData_CowboyHat_&lt;character&gt;.asset) — any per-character hat
        /// style shares this instead of each getting its own bespoke load-or-create method.</summary>
        private static CosmeticData CreateOrLoadCosmeticData(CharacterType character, string assetNamePrefix)
        {
            string path = $"{CosmeticDataFolder}/CosmeticData_{assetNamePrefix}_{character}.asset";
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
