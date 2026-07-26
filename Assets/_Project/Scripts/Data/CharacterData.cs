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

        [Tooltip("Which level completing unlocks this character (0 = available from start).")]
        public int unlockLevel;
    }
}
