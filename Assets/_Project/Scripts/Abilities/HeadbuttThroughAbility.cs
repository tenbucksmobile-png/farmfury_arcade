using UnityEngine;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Billy's ability. Arms a window that permanently destroys the next 3 walls he hits
    /// in his current direction (TileMapRenderer.DestroyWallAt — visually removed and permanently
    /// walkable, unlike Percy's temporary phase). Tints Billy's own sprite while armed as a "horn
    /// glow" placeholder cue.</summary>
    public class HeadbuttThroughAbility : AbilityBase
    {
        private const int WallsToDestroy = 3;
        private static readonly Color GlowColor = Color.yellow;

        private bool _armed;
        private int _wallsRemaining;
        private SpriteRenderer _spriteRenderer;
        private Color _originalColor;

        protected override void Awake()
        {
            base.Awake();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        protected override void Execute()
        {
            _wallsRemaining = WallsToDestroy;
            _armed = true;

            if (_spriteRenderer != null)
            {
                _originalColor = _spriteRenderer.color;
                _spriteRenderer.color = GlowColor;
            }
        }

        protected override void Update()
        {
            base.Update();
            if (!_armed || TileMap == null)
            {
                return;
            }

            Direction facing = Movement.CurrentDirection == Direction.None ? Direction.Down : Movement.CurrentDirection;
            Vector2Int aheadCell = Movement.CurrentGridPosition + DirectionUtils.ToVector(facing);

            if (!TileMap.IsWalkable(aheadCell))
            {
                TileMap.DestroyWallAt(aheadCell);
                _wallsRemaining--;
                if (_wallsRemaining <= 0)
                {
                    Disarm();
                }
            }
        }

        private void Disarm()
        {
            _armed = false;
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = _originalColor;
            }
        }
    }
}
