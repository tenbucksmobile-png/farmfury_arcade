using UnityEngine;

namespace FarmFuryArcade.Data
{
    [CreateAssetMenu(fileName = "CharacterData_XX", menuName = "Farm Fury Arcade/Character Data")]
    public class CharacterData : ScriptableObject
    {
        public CharacterType characterType;
        public string displayName;
        public Sprite portraitSprite;
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
    }
}
