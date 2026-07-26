using UnityEngine;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>
    /// Picks the current direction's walk frames from CharacterData.walkAnimationFrames, which is
    /// expected in the fixed order [Up0,Up1,Down0,Down1,Left0,Left1,Right0,Right1] (8 entries).
    /// If a CharacterData has fewer than 8 frames (true for every character until real art is
    /// imported), the SpriteRenderer's placeholder sprite from the prefab is left untouched —
    /// direction/frame selection logic still runs so it's ready the moment art is assigned.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CharacterAnimator : MonoBehaviour
    {
        [SerializeField] private CharacterData characterData;
        [SerializeField] private float frameInterval = 0.15f;

        private SpriteRenderer _spriteRenderer;
        private GridMovement _gridMovement;
        private float _frameTimer;
        private int _frameIndex;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _gridMovement = GetComponent<GridMovement>();
        }

        public void SetCharacterData(CharacterData data)
        {
            characterData = data;
        }

        private void Update()
        {
            Direction facing = _gridMovement != null ? _gridMovement.CurrentDirection : Direction.None;
            Sprite[] frames = GetFramesForDirection(facing == Direction.None ? Direction.Down : facing);
            if (frames == null)
            {
                return;
            }

            bool moving = facing != Direction.None;
            if (!moving)
            {
                _spriteRenderer.sprite = frames[0];
                _frameTimer = 0f;
                _frameIndex = 0;
                return;
            }

            float speedScale = characterData != null ? Mathf.Max(characterData.movementSpeed, 1f) / 4f : 1f;
            _frameTimer += Time.deltaTime * speedScale;
            if (_frameTimer >= frameInterval)
            {
                _frameTimer = 0f;
                _frameIndex = (_frameIndex + 1) % frames.Length;
            }

            _spriteRenderer.sprite = frames[_frameIndex];
        }

        private Sprite[] GetFramesForDirection(Direction dir)
        {
            if (characterData == null || characterData.walkAnimationFrames == null || characterData.walkAnimationFrames.Length < 8)
            {
                return null;
            }

            int baseIndex;
            switch (dir)
            {
                case Direction.Up: baseIndex = 0; break;
                case Direction.Left: baseIndex = 4; break;
                case Direction.Right: baseIndex = 6; break;
                default: baseIndex = 2; break; // Down
            }

            return new[] { characterData.walkAnimationFrames[baseIndex], characterData.walkAnimationFrames[baseIndex + 1] };
        }
    }
}
