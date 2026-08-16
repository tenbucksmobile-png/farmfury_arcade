using UnityEngine;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>
    /// Hold-to-move grid movement: the character advances only while InputController reports a
    /// direction currently held (keyboard key down, D-pad finger down, or an active swipe — see
    /// InputController's own doc comment). Releasing the held direction stops her immediately,
    /// wherever she is. Switching direction — including a full 180-degree reversal, to escape a
    /// robot — takes effect the instant it's pressed, with no cooldown and no "must be at an
    /// intersection" restriction; this replaced an earlier auto-run-until-blocked model (queue a
    /// direction once, keep moving until a wall or an explicit new queue, reversal blocked mid-
    /// corridor) that read as unresponsive — turns only registered at the next full cell crossed,
    /// which could be several tiles away, and felt like input was being ignored.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class GridMovement : MonoBehaviour
    {
        [SerializeField] private float speed = 4f;

        private TileMapRenderer _tileMap;
        private Direction _heldDirection = Direction.None;
        private Direction _appliedDirection = Direction.None;
        private bool _canCrossWater;

        public Vector2Int CurrentGridPosition { get; private set; }
        public Direction CurrentDirection { get; private set; } = Direction.None;

        /// <summary>The last non-None value CurrentDirection held — unlike CurrentDirection itself
        /// (which resets to None the instant a held direction is released or blocked, so it can be
        /// read while genuinely stationary), this persists so a facing-direction ability (e.g.
        /// BounceRollAbility) can fire correctly even when activated while standing still, in
        /// whichever of the 4 directions the character actually last faced — not always defaulting
        /// to Down. Starts at Down to match every character's idle sprite (CharacterAnimator falls
        /// back to Down for the same reason).</summary>
        public Direction LastFacingDirection { get; private set; } = Direction.Down;

        public float Speed => speed;

        private void OnEnable()
        {
            InputController.OnHeldDirectionChanged += SetHeldDirection;
            _heldDirection = InputController.CurrentHeldDirection;
        }

        private void OnDisable()
        {
            InputController.OnHeldDirectionChanged -= SetHeldDirection;
        }

        private void Start()
        {
            _tileMap = FindFirstObjectByType<TileMapRenderer>();
            if (_tileMap != null)
            {
                CurrentGridPosition = _tileMap.WorldToGrid(transform.position);
            }
        }

        private void SetHeldDirection(Direction dir)
        {
            _heldDirection = dir;
        }

        /// <summary>Directly commands a direction, bypassing InputController — used by
        /// CharacterManager to carry facing into a freshly-spawned/swapped character and by the
        /// Phase 2 debug harness. Under hold-to-move rules this only matters until the next real
        /// input change (OnEnable already re-syncs to whatever's actually held), so it's a
        /// starting nudge, not a persistent override.</summary>
        public void QueueInputDirection(Direction dir)
        {
            _heldDirection = dir;
        }

        public void SetSpeed(float newSpeed)
        {
            speed = newSpeed;
        }

        /// <summary>Set true only for Ducky (via CharacterData.canCrossWater) — lets this
        /// instance treat water tiles as walkable instead of a soft wall.</summary>
        public void SetCanCrossWater(bool canCross)
        {
            _canCrossWater = canCross;
        }

        private void Update()
        {
            if (_tileMap == null)
            {
                return;
            }

            if (_heldDirection == Direction.None)
            {
                CurrentDirection = Direction.None;
                _appliedDirection = Direction.None;
                return;
            }

            // A direction change (including a full reversal) snaps her to the nearest cell centre
            // instantly, then continues from there — this is what makes turning immediate rather
            // than waiting to reach a cell boundary under her own momentum. The snap is at most
            // half a tile and happens in the same frame the new direction is pressed, so it reads
            // as instant, not delayed.
            if (_heldDirection != _appliedDirection)
            {
                Vector2Int nearest = _tileMap.WorldToGrid(transform.position);
                transform.position = _tileMap.GridToWorld(nearest);
                CurrentGridPosition = nearest;
                _appliedDirection = _heldDirection;
            }

            Vector2Int fromCell = _tileMap.WorldToGrid(transform.position);
            CurrentGridPosition = fromCell;
            Vector2Int dirVector = DirectionUtils.ToVector(_heldDirection);
            Vector2Int nextCell = fromCell + dirVector;

            if (!_tileMap.IsWalkable(nextCell, _canCrossWater))
            {
                // Blocked — hold position at the current cell centre. Still "holding" the input,
                // so the moment it becomes walkable (a destroyed wall) or the player picks a
                // different direction, it's re-evaluated next frame with no extra state to reset.
                transform.position = _tileMap.GridToWorld(fromCell);
                CurrentDirection = Direction.None;
                return;
            }

            CurrentDirection = _heldDirection;
            LastFacingDirection = _heldDirection;

            float remaining = speed * TileMapRenderer.CellSize * Time.deltaTime;
            Vector3 targetCenter = _tileMap.GridToWorld(nextCell);
            float distToTarget = Vector3.Distance(transform.position, targetCenter);

            if (remaining >= distToTarget)
            {
                transform.position = targetCenter;
                CurrentGridPosition = nextCell;
            }
            else
            {
                transform.position += new Vector3(dirVector.x, dirVector.y, 0f) * remaining;
            }
        }
    }
}
