using UnityEngine;

namespace FarmFuryArcade.Data
{
    [CreateAssetMenu(fileName = "CharacterData_XX", menuName = "Farm Fury Arcade/Character Data")]
    public class CharacterData : ScriptableObject
    {
        public CharacterType characterType;
        public string displayName;
        public Sprite portraitSprite;

        [Tooltip("Framed 'animal card' art (Sprites/UI/{Name}_{Species}.png) shown by " +
                 "ChooseCharacterScreen — distinct from portraitSprite, which is a plain front " +
                 "sprite reused for the HUD/Matchup-era cards. Null falls back to a placeholder " +
                 "square for any character without dedicated card art yet.")]
        public Sprite selectCardArt;

        public Sprite[] walkAnimationFrames;

        [Range(1, 5)]
        public float movementSpeed;

        public AbilityType specialAbility;
        public float abilityCooldown;
        [TextArea]
        public string abilityDescription;

        [Tooltip("Mazes-completed count that unlocks this character (0 = available from start). " +
                 "Compared against SaveManager.HighestLevelReached + 1 by UnlockManager.")]
        public int unlockLevel;

        [Tooltip("Only Ducky sets this true — lets GridMovement treat water tiles (id 8) as " +
                 "walkable instead of a soft wall.")]
        public bool canCrossWater;

        [Tooltip("True once dedicated Right-facing art exists in walkAnimationFrames[6]/[7]. " +
                 "When false (the default), CharacterAnimator mirrors the Left frames instead.")]
        public bool hasDedicatedRightArt;
    }
}
