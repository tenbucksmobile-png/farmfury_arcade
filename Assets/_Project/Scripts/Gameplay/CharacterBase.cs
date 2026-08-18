using UnityEngine;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>Identity/hub component every playable character prefab carries alongside
    /// GridMovement/CropCollector/CharacterAnimator/PlayerHealth and its unique AbilityBase
    /// subclass. CharacterManager calls Initialize(data) right after spawning/swapping to apply
    /// CharacterData's stats (speed, water-crossing) and animator data.</summary>
    [RequireComponent(typeof(GridMovement))]
    public class CharacterBase : MonoBehaviour
    {
        [SerializeField] private CharacterType characterType;
        public CharacterType CharacterType => characterType;
        public CharacterData Data { get; private set; }

        private GridMovement _movement;

        private void Awake()
        {
            _movement = GetComponent<GridMovement>();
        }

        public void Initialize(CharacterData data)
        {
            Data = data;
            if (data == null)
            {
                return;
            }

            characterType = data.characterType;
            _movement.SetSpeed(data.movementSpeed);
            _movement.SetCanCrossWater(data.canCrossWater);
            GetComponent<CharacterAnimator>()?.SetCharacterData(data);
            // CharacterType must be set (above) before this reads it via GetEquippedCosmetic.
            GetComponent<CharacterCosmeticRenderer>()?.Refresh();
        }
    }
}
