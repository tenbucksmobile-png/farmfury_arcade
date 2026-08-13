using System.Collections;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Enemies;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Bessie's ability. AoE at a fixed origin (her position when cast): every robot
    /// within radiusTiles is instantly defeated (ForceDefeat, bypassing the Vulnerable
    /// requirement — same convention as PuffUpAbility), and the zone then lingers for
    /// KillzoneDurationSeconds, defeating any robot that wanders into it afterward too — same
    /// "deployed hazard keeps killing while live" rule every other ability hazard follows
    /// (EggHazard, PuffUp), rather than a one-shot check. Double Slam combo (Bessie -> Bessie via
    /// swap) doubles the radius to 4 tiles for this use, applied to both the instant hit and the
    /// lingering zone.</summary>
    public class GroundSlamAbility : AbilityBase
    {
        private const float BaseRadiusTiles = 2f;
        private const float ComboRadiusTiles = 4f;
        private const float KillzoneDurationSeconds = 3f;

        [SerializeField] private GameObject shockwavePrefab;
        [SerializeField] private float shakeDuration = 0.3f;
        [SerializeField] private float shakeMagnitude = 0.15f;

        protected override void Execute()
        {
            bool doubled = ComboSystem.Instance != null && ComboSystem.Instance.ConsumeDoubleSlamRadius();
            float radius = doubled ? ComboRadiusTiles : BaseRadiusTiles;
            Vector2Int origin = Movement.CurrentGridPosition;

            DefeatRobotsInRadius(origin, radius);

            if (shockwavePrefab != null)
            {
                Instantiate(shockwavePrefab, transform.position, Quaternion.identity);
            }

            CameraShake.Instance?.Shake(shakeDuration, shakeMagnitude);

            StartCoroutine(LingeringKillzone(origin, radius));
        }

        private IEnumerator LingeringKillzone(Vector2Int origin, float radius)
        {
            float elapsed = 0f;
            while (elapsed < KillzoneDurationSeconds)
            {
                DefeatRobotsInRadius(origin, radius);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private static void DefeatRobotsInRadius(Vector2Int origin, float radius)
        {
            foreach (var robot in FindObjectsByType<RobotBase>(FindObjectsSortMode.None))
            {
                if (Vector2Int.Distance(origin, robot.CurrentGridPosition) <= radius)
                {
                    robot.ForceDefeat();
                }
            }
        }
    }
}
