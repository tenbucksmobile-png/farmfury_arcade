using UnityEngine;

namespace FarmFuryArcade.Data
{
    /// <summary>Per-character positioning override for a universal (one-asset-fits-all-characters)
    /// hat — see CosmeticData.characterHatOverrides.</summary>
    [System.Serializable]
    public struct CharacterHatOverride
    {
        public CharacterType character;
        public Vector2 hatOffset;
        public float hatScale;
    }

    /// <summary>
    /// One purchasable/equippable cosmetic (Monetisation Build Plan Phase 4). Loaded via
    /// DataManager.GetAllCosmetics() from ScriptableObjects/Resources/Cosmetics, same
    /// Resources.LoadAll convention as LevelData/CharacterData/RobotData — see DataManager's own
    /// doc comment for why that folder name is load-bearing, not cosmetic.
    ///
    /// Hats and Skins are per-character (a hat/skin design needs its own art fitted to each
    /// character's silhouette — see CLAUDE.md's Phase 4 art-scope note), so one CosmeticData asset
    /// covers exactly one (style, character) pair for those two types — e.g. "Cowboy Hat, Cluck"
    /// and "Cowboy Hat, Bessie" are two separate assets sharing setId "cowboy_hat" so the Store can
    /// group/price them together while SaveManager still tracks ownership per exact asset.
    /// Trails are character-agnostic (equippable on whichever character is active).
    /// </summary>
    [CreateAssetMenu(fileName = "CosmeticData_XX", menuName = "Farm Fury Arcade/Cosmetic Data")]
    public class CosmeticData : ScriptableObject
    {
        [Tooltip("Unique persistence key (e.g. \"cowboy_hat_cluck\") — used as the PlayerPrefs " +
                 "ownership key and the Resources asset name. Never rename after players may have " +
                 "already purchased it; that would orphan their save data.")]
        public string cosmeticId;

        [Tooltip("Groups per-character variants of the same design for Store display/pricing " +
                 "(e.g. every character's \"cowboy_hat\" asset shares this). Ignored for Trail " +
                 "(already character-agnostic) and MazeTheme (already per-world).")]
        public string setId;

        public string displayName;
        public CosmeticType cosmeticType;

        [Tooltip("Hat/Skin only — which character this variant's art was made for. Ignored for " +
                 "Trail.")]
        public CharacterType character;

        [Tooltip("Coin price. 0 = free/starter cosmetic, not purchasable via IAP-priced real money " +
                 "(the coin economy is the only cosmetic currency for now — see the Phase 4 plan " +
                 "note; revisit if a premium-currency cosmetic tier is ever added).")]
        public int coinCost;

        [Tooltip("Store purchase-card thumbnail. Falls back to a placeholder square if null, same " +
                 "convention as portraitSprite/selectCardArt on CharacterData.")]
        public Sprite previewSprite;

        [Tooltip("Hat only. Same fixed [Up0,Up1,Down0,Down1,Left0,Left1,Right0,Right1] order as " +
                 "CharacterData.walkAnimationFrames, so CharacterCosmeticRenderer can pick the same " +
                 "index CharacterAnimator is currently showing and the hat tracks the walk cycle " +
                 "frame-for-frame instead of drifting out of sync.")]
        public Sprite[] hatFrames;

        [Tooltip("Hat only. When true, hatFrames[6]/[7] (Right) deliberately hold the same sprite " +
                 "reference as hatFrames[4]/[5] (Left) as a flip-to-fake-Right substitute — " +
                 "CharacterCosmeticRenderer mirrors it horizontally when the character faces Right, " +
                 "same convention CharacterAnimator already uses for the base character sprite (see " +
                 "CharacterData.hasDedicatedRightArt) but scoped to the hat specifically, since a " +
                 "character can have real dedicated Right body art while its equipped hat still " +
                 "only has Left art. Defaults false so every hat wired before this field existed " +
                 "(a single sprite reused across all 8 slots, nothing directional to mirror) keeps " +
                 "rendering exactly as before. Set true only where WireBaseballCaps actually " +
                 "duplicates a LeftSpriteFileName into the Right slots too (Ducky/Woolly).")]
        public bool mirrorLeftHatForRight;

        [Tooltip("Hat only, local offset (world units) from the character's own sprite origin. " +
                 "Per-cosmetic (not per-CharacterCosmeticRenderer) because a sombrero and a party " +
                 "hat don't sit at the same height on the same character, let alone across " +
                 "characters with very different head shapes/sizes. Tune per (style, character) " +
                 "pair once real art is in — see CLAUDE.md's positioning-convention note.")]
        public Vector2 hatOffset = new Vector2(0f, 0.35f);

        [Tooltip("Hat only, uniform scale applied on top of the sprite's own PPU-derived size. " +
                 "1 = rendered at the same world size the sprite's pixel dimensions/PPU imply. Art " +
                 "generated as a big standalone prop (not pre-scaled to a character's head) will " +
                 "usually need this well below 1.")]
        public float hatScale = 1f;

        [Tooltip("Hat only. Optional per-character overrides of hatOffset/hatScale above, for a " +
                 "universal hat (e.g. Sombrero) shared by one CosmeticData asset across " +
                 "every character instead of one asset per character (unlike the baseball caps, " +
                 "which already get their own per-character offset/scale via a dedicated asset " +
                 "each). A character with no entry here falls back to the shared hatOffset/hatScale " +
                 "above. See CharacterCosmeticRenderer.ResolveHatOffsetAndScale.")]
        public CharacterHatOverride[] characterHatOverrides;

        [Tooltip("Hat only, optional. When set (2+ entries), CharacterCosmeticRenderer shows one of " +
                 "these instead of hatFrames — picked deterministically from the current level's " +
                 "own levelNumber (levelNumber % hatVariantSprites.Length), re-resolved every time " +
                 "the character (re)spawns, so the shown variant changes as the player progresses " +
                 "through levels. Every entry replaces ALL 8 hatFrames slots (no per-direction art " +
                 "for these variants) — built for Sombrero's 4 alternate designs (Sombrero_1..4.png), " +
                 "but works for any hat with several interchangeable single-pose looks. Leave empty " +
                 "for a normal hat that always shows the same hatFrames.")]
        public Sprite[] hatVariantSprites;

        [Tooltip("Skin only. A full replacement walk-cycle set, same 8-entry order as " +
                 "CharacterData.walkAnimationFrames — CharacterCosmeticRenderer feeds this into " +
                 "CharacterAnimator.SetCosmeticFrameOverride when this skin is equipped, in place " +
                 "of (not layered on top of) the character's base art.")]
        public Sprite[] skinFrames;

        [Tooltip("Trail only. A particle/effect prefab spawned behind the active character while " +
                 "equipped. Leave null to fall back to a procedural placeholder effect (same " +
                 "\"dedicated art with a procedural fallback\" convention PelletCollectBurst uses).")]
        public GameObject trailEffectPrefab;
    }
}
