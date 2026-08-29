using System.Collections;
using UnityEngine;
using FarmFuryArcade.Enemies;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Billy's ability. Speeds up and charges forward ChargeTilesBase tiles in his
    /// current facing direction (Movement.LastFacingDirection, not CurrentDirection, so it fires
    /// correctly even while he's completely stationary — same convention BounceRollAbility uses
    /// and for the same reason: CurrentDirection resets to None the instant no direction is held).
    /// Any robot touched along the way is instantly defeated regardless of state (ForceDefeat, same
    /// "deployed hazard kills outright" convention as EggHazard/GroundSlamAbility/BounceRollAbility
    /// — ForceDefeat itself already no-ops on an already-Defeated/Returning robot, so no extra state
    /// check is needed here). Stops early if a wall blocks the charge partway through, same
    /// "stop early at an obstacle" convention RobotBase.KnockBack/BounceRollAbility use.
    ///
    /// Replaces an earlier "arm a 3-wall-destroy window" version — per explicit feedback the ability
    /// should be a robot charge instead ("speed up and headbutt the robot in any direction"), not a
    /// wall-breaking gimmick; TileMapRenderer.DestroyWallAt is no longer called from here at all.
    ///
    /// Swaps Billy's own sprite to a real charging-ram pose for the charge's duration (Billy_left_
    /// ram1.png / Billy_right_ram1.png — both dedicated art, not mirrored from one), same
    /// "own SpriteRenderer swaps to the roll pose" convention BounceRollAbility uses for Percy, and
    /// replaces the earlier yellow-tint placeholder now that real art exists. Only Left/Right have
    /// dedicated ram art; an Up/Down charge leaves his sprite on whatever frame it was on when
    /// activated (CharacterAnimator is disabled for the whole charge regardless of direction, same
    /// as BounceRollAbility, so nothing overwrites it mid-charge) rather than showing a mismatched
    /// left/right ram pose facing the wrong way.</summary>
    public class HeadbuttThroughAbility : AbilityBase
    {
        private const int ChargeTilesBase = 3;
        private const float ChargeSecondsPerTile = 0.12f;

        [SerializeField] private Sprite ramSpriteLeft;
        [SerializeField] private Sprite ramSpriteRight;

        private SpriteRenderer _spriteRenderer;
        private CharacterAnimator _characterAnimator;
        private Sprite _preChargeSprite;
        private bool _isCharging;

        /// <summary>Read by PlayerHealth (same GameObject) — same race-condition fix as
        /// BounceRollAbility.IsRolling, see its own doc comment. Without this, Billy could die on
        /// the exact contact his charge was meant to ForceDefeat instead, depending on which
        /// sibling MonoBehaviour's OnTriggerEnter2D Unity happened to run first.</summary>
        public bool IsCharging => _isCharging;

        protected override void Awake()
        {
            base.Awake();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _characterAnimator = GetComponent<CharacterAnimator>();
        }

        protected override void Execute()
        {
            Direction facing = Movement.LastFacingDirection;
            StartCoroutine(ChargeRoutine(facing, ChargeTilesBase));
        }

        private IEnumerator ChargeRoutine(Direction direction, int tileCount)
        {
            _isCharging = true;
            Movement.enabled = false;

            Sprite ramSprite = direction switch
            {
                Direction.Left => ramSpriteLeft,
                Direction.Right => ramSpriteRight,
                _ => null
            };

            if (_characterAnimator != null)
            {
                _characterAnimator.enabled = false;
            }
            if (_spriteRenderer != null && ramSprite != null)
            {
                _preChargeSprite = _spriteRenderer.sprite;
                _spriteRenderer.sprite = ramSprite;
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
                while (t < ChargeSecondsPerTile)
                {
                    t += Time.deltaTime;
                    transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t / ChargeSecondsPerTile));
                    yield return null;
                }
                transform.position = to;
                cell = nextCell;
            }

            if (_spriteRenderer != null && _preChargeSprite != null)
            {
                _spriteRenderer.sprite = _preChargeSprite;
            }
            if (_characterAnimator != null)
            {
                _characterAnimator.enabled = true;
            }

            _isCharging = false;
            Movement.enabled = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isCharging)
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
