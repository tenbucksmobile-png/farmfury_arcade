using System.Collections;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Enemies;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Gerald's ability. Pulsates (breathes) between normal size and 2x scale for 3s,
    /// rather than jumping straight to a fixed size — per feedback that a flat 3x scale for 5s read
    /// as "too big." Any robot touched while puffed is instantly defeated regardless of state
    /// (bypasses the power-pellet requirement via RobotBase.ForceDefeat) — this isn't gated to the
    /// pulse's peak, so a touch anywhere in the cycle still counts, matching the original "puffed up
    /// at all" behaviour. Movement speed halves for the duration, and IsPuffed blocks warp tunnels
    /// (see WarpTunnel.OnTriggerEnter2D — "too big" to fit). Iron Stampede combo (Bessie -> Gerald)
    /// also destroys any wall Gerald is adjacent to while puffed.</summary>
    public class PuffUpAbility : AbilityBase
    {
        private const float PuffDurationSeconds = 3f;
        private const float ScaleMultiplier = 2f;
        private const float SpeedMultiplier = 0.5f;

        /// <summary>Full swell-then-shrink cycles spread across PuffDurationSeconds — 3 gives a
        /// clearly visible "breathing" rhythm (once per second) rather than either a single slow
        /// swell or an overly frantic flutter.</summary>
        private const float PulseCyclesOverDuration = 3f;

        public bool IsPuffed { get; private set; }

        protected override void Execute()
        {
            StartCoroutine(PuffRoutine());
        }

        private IEnumerator PuffRoutine()
        {
            IsPuffed = true;
            bool wallBuff = ComboSystem.Instance != null && ComboSystem.Instance.ConsumeWallDestroyPuff();

            Vector3 originalScale = transform.localScale;

            float originalSpeed = Movement.Speed;
            Movement.SetSpeed(originalSpeed * SpeedMultiplier);

            float elapsed = 0f;
            while (elapsed < PuffDurationSeconds)
            {
                elapsed += Time.deltaTime;

                float pulse = (Mathf.Sin(elapsed / PuffDurationSeconds * PulseCyclesOverDuration * Mathf.PI * 2f) + 1f) * 0.5f;
                transform.localScale = Vector3.Lerp(originalScale, originalScale * ScaleMultiplier, pulse);

                if (wallBuff && TileMap != null)
                {
                    DestroyAdjacentWalls();
                }

                yield return null;
            }

            transform.localScale = originalScale;
            Movement.SetSpeed(originalSpeed);
            IsPuffed = false;
        }

        private void DestroyAdjacentWalls()
        {
            Vector2Int pos = Movement.CurrentGridPosition;
            foreach (var dir in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
            {
                Vector2Int cell = pos + DirectionUtils.ToVector(dir);
                if (!TileMap.IsWalkable(cell))
                {
                    TileMap.DestroyWallAt(cell);
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPuffed)
            {
                return;
            }

            var robot = other.GetComponent<RobotBase>();
            if (robot == null)
            {
                return;
            }

            if (robot.CurrentState is RobotState.Chase or RobotState.Scatter or RobotState.Vulnerable)
            {
                robot.ForceDefeat();
            }
        }
    }
}
