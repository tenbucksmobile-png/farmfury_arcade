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
        private const float AlignmentEpsilon = 0.02f;

        private TileMapRenderer _tileMap;
        private Direction _queuedDirection = Direction.None;

        public Vector2Int CurrentGridPosition { get; private set; }
        public Direction CurrentDirection { get; private set; } = Direction.None;

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

        private void Update()
        {
            if (_tileMap == null)
            {
                return;
            }

            Vector2Int cell = _tileMap.WorldToGrid(transform.position);
            Vector3 cellCenter = _tileMap.GridToWorld(cell);
            bool atCenter = Vector3.Distance(transform.position, cellCenter) < AlignmentEpsilon;

            if (atCenter)
            {
                transform.position = cellCenter;
                CurrentGridPosition = cell;

                if (CanApplyDirection(cell, _queuedDirection))
                {
                    CurrentDirection = _queuedDirection;
                }

                if (CurrentDirection != Direction.None && !_tileMap.IsWalkable(cell + DirectionUtils.ToVector(CurrentDirection)))
                {
                    CurrentDirection = Direction.None;
                }
            }

            if (CurrentDirection != Direction.None)
            {
                Vector2Int dirVector = DirectionUtils.ToVector(CurrentDirection);
                transform.position += new Vector3(dirVector.x, dirVector.y, 0f) * speed * Time.deltaTime;
            }
        }

        private bool CanApplyDirection(Vector2Int cell, Direction queued)
        {
            if (queued == Direction.None)
            {
                return false;
            }

            if (!_tileMap.IsWalkable(cell + DirectionUtils.ToVector(queued)))
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
            if (_tileMap.IsWalkable(cell + DirectionUtils.ToVector(Direction.Up))) count++;
            if (_tileMap.IsWalkable(cell + DirectionUtils.ToVector(Direction.Down))) count++;
            if (_tileMap.IsWalkable(cell + DirectionUtils.ToVector(Direction.Left))) count++;
            if (_tileMap.IsWalkable(cell + DirectionUtils.ToVector(Direction.Right))) count++;
            return count;
        }
    }
}
