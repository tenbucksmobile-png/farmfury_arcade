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
    /// Percy's own sprite swaps to trailPrefab's art (Percy_effect.png, a curled-up rolling pose)
    /// for the roll's duration instead of a separate semi-transparent trail object trailing behind
    /// him — the earlier version instantiated trailPrefab as a child while Percy kept his normal
    /// walk-cycle sprite, so the rolling pose was barely visible underneath him; per feedback this
    /// needed to actually replace what Percy looks like. CharacterAnimator (which otherwise drives
    /// the same SpriteRenderer every frame from GridMovement's direction) is disabled for the
    /// duration too, or it would overwrite the swapped sprite on the very next frame.
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

        /// <summary>Kept its original field name/serialized reference (BounceTrail.prefab, already
        /// wired to Percy_effect.png) even though it's no longer instantiated as a separate trail
        /// object — only its SpriteRenderer's sprite is read now, as the pose Percy's own
        /// SpriteRenderer swaps to for the roll. Renaming the field would drop the existing wired
        /// reference on Percy's prefab (Unity matches serialized fields by name).</summary>
        [SerializeField] private GameObject trailPrefab;

        private SpriteRenderer _spriteRenderer;
        private CharacterAnimator _characterAnimator;
        private Sprite _preRollSprite;
        private bool _isRolling;

        protected override void Awake()
        {
            base.Awake();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _characterAnimator = GetComponent<CharacterAnimator>();
        }

        protected override void Execute()
        {
            bool extendedBuff = ComboSystem.Instance != null && ComboSystem.Instance.ConsumeTripleWallPhase();
            int tiles = extendedBuff ? RollTilesBuffed : RollTilesBase;
            // LastFacingDirection (not CurrentDirection, which resets to None the instant no
            // direction is held) so the roll fires correctly in whichever of the 4 directions Percy
            // actually last faced, including while completely stationary — CurrentDirection would
            // always fall back to Down in that case, silently ignoring Up/Left/Right activations.
            Direction facing = Movement.LastFacingDirection;

            StartCoroutine(RollRoutine(facing, tiles));
        }

        private IEnumerator RollRoutine(Direction direction, int tileCount)
        {
            _isRolling = true;
            Movement.enabled = false;

            Sprite rollSprite = trailPrefab != null ? trailPrefab.GetComponent<SpriteRenderer>()?.sprite : null;
            if (_characterAnimator != null)
            {
                _characterAnimator.enabled = false;
            }
            if (_spriteRenderer != null && rollSprite != null)
            {
                _preRollSprite = _spriteRenderer.sprite;
                _spriteRenderer.sprite = rollSprite;
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

            if (_spriteRenderer != null && _preRollSprite != null)
            {
                _spriteRenderer.sprite = _preRollSprite;
            }
            if (_characterAnimator != null)
            {
                _characterAnimator.enabled = true;
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
