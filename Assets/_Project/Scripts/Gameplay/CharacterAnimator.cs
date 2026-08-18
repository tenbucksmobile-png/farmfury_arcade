using UnityEngine;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>
    /// Picks the current direction's walk frames from CharacterData.walkAnimationFrames, which is
    /// expected in the fixed order [Up0,Up1,Down0,Down1,Left0,Left1,Right0,Right1] (8 entries).
    /// If a CharacterData has fewer than 8 frames (still true for Horace/Billy, which have no
    /// uploaded art yet), the SpriteRenderer's placeholder sprite from the prefab is left
    /// untouched — direction/frame selection logic still runs so it's ready the moment art lands.
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

        /// <summary>Equipped-skin frames (CosmeticData.skinFrames, same 8-entry order as
        /// CharacterData.walkAnimationFrames) set by CharacterCosmeticRenderer. When non-null this
        /// REPLACES characterData's own frames entirely rather than layering on top — a skin is a
        /// full recolor, not an overlay. Null means "no skin equipped," fall back to base art.</summary>
        private Sprite[] _cosmeticFrameOverride;

        /// <summary>Direction/frame-index actually being displayed this frame, read by
        /// CharacterCosmeticRenderer so an equipped hat can track the exact same walk-cycle frame
        /// instead of running its own independent (and easily desynced) timer.</summary>
        public Direction CurrentDisplayDirection { get; private set; } = Direction.Down;
        public int CurrentFrameIndex => _frameIndex;
        public bool IsFlippedX => _spriteRenderer != null && _spriteRenderer.flipX;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _gridMovement = GetComponent<GridMovement>();
        }

        public void SetCharacterData(CharacterData data)
        {
            characterData = data;
        }

        /// <summary>Pass null to unequip (revert to characterData's own base art).</summary>
        public void SetCosmeticFrameOverride(Sprite[] skinFrames)
        {
            _cosmeticFrameOverride = skinFrames != null && skinFrames.Length >= 8 ? skinFrames : null;
        }

        private void Update()
        {
            Direction facing = _gridMovement != null ? _gridMovement.CurrentDirection : Direction.None;
            Direction dir = facing == Direction.None ? Direction.Down : facing;
            CurrentDisplayDirection = dir;
            Sprite[] frames = GetFramesForDirection(dir);
            if (frames == null)
            {
                return;
            }

            // Right reuses the Left frames mirrored horizontally unless this character has its
            // own Right art (walkAnimationFrames[6]/[7]) — see CharacterData.hasDedicatedRightArt.
            _spriteRenderer.flipX = dir == Direction.Right &&
                (characterData == null || !characterData.hasDedicatedRightArt);

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
            Sprite[] source = _cosmeticFrameOverride;
            if (source == null)
            {
                if (characterData == null || characterData.walkAnimationFrames == null || characterData.walkAnimationFrames.Length < 8)
                {
                    return null;
                }
                source = characterData.walkAnimationFrames;
            }

            int baseIndex;
            switch (dir)
            {
                case Direction.Up: baseIndex = 0; break;
                case Direction.Left: baseIndex = 4; break;
                case Direction.Right: baseIndex = 6; break;
                default: baseIndex = 2; break; // Down
            }

            return new[] { source[baseIndex], source[baseIndex + 1] };
        }
    }
}
