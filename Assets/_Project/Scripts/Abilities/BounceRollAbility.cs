using System.Collections;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Enemies;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Percy's ability. Rolls forward RollTilesBase tiles in his current facing direction
    /// (falling back to Down if he isn't currently moving) — any robot touched along the way is
    /// instantly defeated regardless of state (ForceDefeat, same "deployed hazard kills outright"
    /// convention as EggHazard/GroundSlamAbility; ForceDefeat itself already no-ops on an
    /// already-Defeated/Returning robot, so no extra state check is needed here). Stops early if a
    /// wall blocks the roll partway through (same "stop early at an obstacle" convention
    /// RobotBase.KnockBack uses). Normal hold-to-move control (GridMovement) is disabled for the
    /// duration and re-enabled the instant the roll ends, so the character resumes walking under
    /// whatever direction the player is currently holding — no separate "continue walking" logic
    /// needed, GridMovement.OnEnable already re-syncs to the live held input on its own.
    ///
    /// Replaces an earlier "arm a wall-phase" version (the next wall hit became temporarily
    /// walkable). Earthquake Roll (Bessie -> Percy) and Kick and Roll (Horace -> Percy) used to
    /// buff that to 3 walls instead of 1; now they buff roll DISTANCE instead (3 tiles -> 9) for the
    /// next activation, since there's no wall count to buff anymore — see ComboSystem.
    /// PendingTripleWallPhase's own doc comment.</summary>
    public class BounceRollAbility : AbilityBase
    {
        private const int RollTilesBase = 3;
        private const int RollTilesBuffed = 9;
        private const float RollSecondsPerTile = 0.12f;

        [SerializeField] private GameObject trailPrefab;

        private bool _isRolling;
        private GameObject _activeTrail;

        protected override void Execute()
        {
            bool extendedBuff = ComboSystem.Instance != null && ComboSystem.Instance.ConsumeTripleWallPhase();
            int tiles = extendedBuff ? RollTilesBuffed : RollTilesBase;
            Direction facing = Movement.CurrentDirection == Direction.None ? Direction.Down : Movement.CurrentDirection;

            StartCoroutine(RollRoutine(facing, tiles));
        }

        private IEnumerator RollRoutine(Direction direction, int tileCount)
        {
            _isRolling = true;
            Movement.enabled = false;

            if (trailPrefab != null)
            {
                if (_activeTrail != null)
                {
                    Destroy(_activeTrail);
                }
                _activeTrail = Instantiate(trailPrefab, transform);
            }

            Vector2Int dirVector = DirectionUtils.ToVector(direction);
            Vector2Int cell = TileMap.WorldToGrid(transform.position);

            for (int i = 0; i < tileCount; i++)
            {
                Vector2Int nextCell = cell + dirVector;
                if (!TileMap.IsWalkable(nextCell))
                {
                    break;
                }

                Vector3 from = TileMap.GridToWorld(cell);
                Vector3 to = TileMap.GridToWorld(nextCell);
                float t = 0f;
                while (t < RollSecondsPerTile)
                {
                    t += Time.deltaTime;
                    transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t / RollSecondsPerTile));
                    yield return null;
                }
                transform.position = to;
                cell = nextCell;
            }

            if (_activeTrail != null)
            {
                Destroy(_activeTrail);
                _activeTrail = null;
            }

            _isRolling = false;
            Movement.enabled = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isRolling)
            {
                return;
            }

            var robot = other.GetComponent<RobotBase>();
            if (robot != null)
            {
                robot.ForceDefeat();
            }
        }
    }
}
