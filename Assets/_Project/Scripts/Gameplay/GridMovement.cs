using UnityEngine;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>
    /// Continuous grid-based movement: the character keeps moving in CurrentDirection until it
    /// reaches the next cell centre, where a queued direction (if walkable) is applied. Direction
    /// reversal is only allowed at intersections (3+ walkable neighbours) or dead ends (<=1
    /// walkable neighbour) — a straight corridor or simple turn (exactly 2 walkable neighbours)
    /// ignores a queued 180-degree reversal, per the "cannot reverse mid-corridor" rule.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class GridMovement : MonoBehaviour
    {
        [SerializeField] private float speed = 4f;

        private TileMapRenderer _tileMap;
        private Direction _queuedDirection = Direction.None;
        private bool _canCrossWater;

        public Vector2Int CurrentGridPosition { get; private set; }
        public Direction CurrentDirection { get; private set; } = Direction.None;
        public float Speed => speed;

        private void OnEnable()
        {
            InputController.OnDirectionInput += QueueInputDirection;
        }

        private void OnDisable()
        {
            InputController.OnDirectionInput -= QueueInputDirection;
        }

        private void Start()
        {
            _tileMap = FindFirstObjectByType<TileMapRenderer>();
            if (_tileMap != null)
            {
                CurrentGridPosition = _tileMap.WorldToGrid(transform.position);
            }
        }

        public void QueueInputDirection(Direction dir)
        {
            _queuedDirection = dir;
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

        /// <summary>Advances up to a full frame's worth of movement, clamping exactly on any cell
        /// boundary it crosses rather than sampling a small epsilon window once per frame. The old
        /// approach compared distance-to-center against a fixed epsilon (0.02) that per-frame
        /// movement (speed * deltaTime, easily 0.05-0.15 world units) could jump straight past —
        /// intermittently skipping the walkability re-check entirely, which read as either the
        /// character clipping through a wall (the check that should have stopped it never ran) or
        /// freezing unresponsive to input (it stopped mid-frame off-center, so the "am I at a cell
        /// center, can I accept a new queued direction" branch never became true again). Snapping
        /// to the exact cell center the instant it's reached — and carrying over any leftover
        /// distance into the next segment — makes this correct at any speed/frame-rate/cell size.</summary>
        private void Update()
        {
            if (_tileMap == null)
            {
                return;
            }

            float remaining = speed * TileMapRenderer.CellSize * Time.deltaTime;
            int guard = 0;
            while (remaining > 0f && guard++ < 8)
            {
                if (CurrentDirection == Direction.None)
                {
                    Vector2Int cell = _tileMap.WorldToGrid(transform.position);
                    transform.position = _tileMap.GridToWorld(cell);
                    CurrentGridPosition = cell;

                    if (CanApplyDirection(cell, _queuedDirection))
                    {
                        CurrentDirection = _queuedDirection;
                        continue;
                    }
                    break;
                }

                Vector2Int fromCell = _tileMap.WorldToGrid(transform.position);
                Vector2Int dirVector = DirectionUtils.ToVector(CurrentDirection);
                Vector2Int nextCell = fromCell + dirVector;

                if (!_tileMap.IsWalkable(nextCell, _canCrossWater))
                {
                    transform.position = _tileMap.GridToWorld(fromCell);
                    CurrentGridPosition = fromCell;
                    CurrentDirection = Direction.None;
                    break;
                }

                Vector3 targetCenter = _tileMap.GridToWorld(nextCell);
                float distToTarget = Vector3.Distance(transform.position, targetCenter);

                if (remaining >= distToTarget)
                {
                    transform.position = targetCenter;
                    CurrentGridPosition = nextCell;
                    remaining -= distToTarget;

                    if (CanApplyDirection(nextCell, _queuedDirection))
                    {
                        CurrentDirection = _queuedDirection;
                    }
                    else if (!_tileMap.IsWalkable(nextCell + DirectionUtils.ToVector(CurrentDirection), _canCrossWater))
                    {
                        CurrentDirection = Direction.None;
                    }
                }
                else
                {
                    transform.position += new Vector3(dirVector.x, dirVector.y, 0f) * remaining;
                    remaining = 0f;
                }
            }
        }

        private bool CanApplyDirection(Vector2Int cell, Direction queued)
        {
            if (queued == Direction.None)
            {
                return false;
            }

            if (!_tileMap.IsWalkable(cell + DirectionUtils.ToVector(queued), _canCrossWater))
            {
                return false;
            }

            if (CurrentDirection != Direction.None && queued == DirectionUtils.Opposite(CurrentDirection))
            {
                int walkableNeighbours = CountWalkableNeighbours(cell);
                return walkableNeighbours != 2;
            }

            return true;
        }

        private int CountWalkableNeighbours(Vector2Int cell)
        {
            int count = 0;
            if (_tileMap.IsWalkable(cell + DirectionUtils.ToVector(Direction.Up), _canCrossWater)) count++;
            if (_tileMap.IsWalkable(cell + DirectionUtils.ToVector(Direction.Down), _canCrossWater)) count++;
            if (_tileMap.IsWalkable(cell + DirectionUtils.ToVector(Direction.Left), _canCrossWater)) count++;
            if (_tileMap.IsWalkable(cell + DirectionUtils.ToVector(Direction.Right), _canCrossWater)) count++;
            return count;
        }
    }
}
