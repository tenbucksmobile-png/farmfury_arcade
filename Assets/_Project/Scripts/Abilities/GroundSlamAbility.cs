using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Enemies;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Bessie's ability. Instant AoE: every robot within radiusTiles is instantly
    /// defeated (ForceDefeat, bypassing the Vulnerable requirement — same convention as
    /// PuffUpAbility). Was a stun; changed per a gameplay rule that a deployed ability hazard
    /// should kill a robot that runs through it, not just incapacitate it — applies to every
    /// character's ability-created robot hazard except Percy (BounceRoll) and Ducky (SkipShot).
    /// Double Slam combo (Bessie -> Bessie via swap) doubles the radius to 4 tiles for this use.</summary>
    public class GroundSlamAbility : AbilityBase
    {
        private const float BaseRadiusTiles = 2f;
        private const float ComboRadiusTiles = 4f;

        [SerializeField] private GameObject shockwavePrefab;
        [SerializeField] private float shakeDuration = 0.3f;
        [SerializeField] private float shakeMagnitude = 0.15f;

        protected override void Execute()
        {
            bool doubled = ComboSystem.Instance != null && ComboSystem.Instance.ConsumeDoubleSlamRadius();
            float radius = doubled ? ComboRadiusTiles : BaseRadiusTiles;

            Vector2Int origin = Movement.CurrentGridPosition;
            foreach (var robot in FindObjectsByType<RobotBase>(FindObjectsSortMode.None))
            {
                if (Vector2Int.Distance(origin, robot.CurrentGridPosition) <= radius)
                {
                    robot.ForceDefeat();
                }
            }

            if (shockwavePrefab != null)
            {
                Instantiate(shockwavePrefab, transform.position, Quaternion.identity);
            }

            CameraShake.Instance?.Shake(shakeDuration, shakeMagnitude);
        }
    }
}
