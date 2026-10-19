using System.Collections;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Billy's ability: Mud Sling. Flings a glob of mud MudSlingTiles in his facing
    /// direction (a ThrownProjectileEffect, same distance/speed/one-target-defeat rule as Horace's
    /// horseshoe and Gerald's feathers). If it reaches the end of its flight (or a wall) without
    /// hitting anything, it leaves a mud pile on that tile (MudPileHazard) that defeats the next robot
    /// to walk into it. Billy holds a slinging pose (slingSprite, drawn facing right, mirrored for
    /// left) for SlingPoseSeconds on a Left/Right throw; Up/Down throws keep his current frame.
    ///
    /// Was "Headbutt Through" (a 3-tile charge that defeated robots on contact) until 2026-10-19,
    /// when it was replaced by the Mud Sling from Farm Fury: Stampede because the ram wasn't working
    /// in play. The class name, AbilityType.HeadbuttThrough and PlayBillyChargeSfx/billyChargeClip
    /// are kept so the Billy prefab's script reference and serialized fields stay intact.</summary>
    public class HeadbuttThroughAbility : AbilityBase
    {
        private const int MudSlingTiles = 3;
        private const float MudSlingSecondsPerTile = 0.4f;
        private const float SlingPoseSeconds = 0.3f;

        [SerializeField] private GameObject mudBallPrefab;
        [SerializeField] private Sprite slingSprite;

        private SpriteRenderer _spriteRenderer;
        private CharacterAnimator _characterAnimator;
        private bool _inPose;

        protected override void Awake()
        {
            base.Awake();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _characterAnimator = GetComponent<CharacterAnimator>();
        }

        protected override void Execute()
        {
            AudioManager.Instance?.PlayBillyChargeSfx();

            Direction facing = Movement.LastFacingDirection;
            ThrownProjectileEffect.SpawnAhead(mudBallPrefab, TileMap, Movement.CurrentGridPosition,
                facing, MudSlingTiles, MudSlingSecondsPerTile);

            // The guard covers the skip-cooldown double activation: a second throw is fine, but a
            // second pose coroutine would restore the wrong "previous" sprite.
            if (!_inPose && slingSprite != null && (facing == Direction.Left || facing == Direction.Right))
            {
                StartCoroutine(SlingPoseRoutine(facing == Direction.Left));
            }
        }

        private IEnumerator SlingPoseRoutine(bool mirror)
        {
            if (_spriteRenderer == null)
            {
                yield break;
            }

            _inPose = true;
            Sprite previousSprite = _spriteRenderer.sprite;
            bool previousFlip = _spriteRenderer.flipX;
            if (_characterAnimator != null)
            {
                _characterAnimator.enabled = false;
            }
            _spriteRenderer.sprite = slingSprite;
            _spriteRenderer.flipX = mirror;

            yield return new WaitForSeconds(SlingPoseSeconds);

            _spriteRenderer.sprite = previousSprite;
            _spriteRenderer.flipX = previousFlip;
            if (_characterAnimator != null)
            {
                _characterAnimator.enabled = true;
            }
            _inPose = false;
        }

        private void OnDisable()
        {
            // A disable mid-pose (e.g. the death/respawn sequence) stops the coroutine, so put the
            // animator back in charge rather than leaving the pose stuck on screen.
            if (_inPose && _characterAnimator != null)
            {
                _characterAnimator.enabled = true;
            }
            _inPose = false;
        }
    }
}
