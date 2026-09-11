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

        /// <summary>True for the ability's whole active hazard window (cast through the end of the
        /// lingering killzone) — same "PlayerHealth checks this to skip its own death check, since
        /// the ability's own contact is already handled" convention BounceRollAbility.IsRolling/
        /// HeadbuttThroughAbility.IsCharging/PuffUpAbility.IsPuffed use. Gap found and fixed
        /// 2026-09-08: those three were added to PlayerHealth.IsProtectedByActiveAbility on
        /// 2026-08-29 (see that commit's own doc comment), but Ground Slam was missed even though
        /// it defeats robots the same way — Bessie could die on the exact contact her own slam was
        /// about to instantly defeat, especially right at the moment of deployment (Execute()'s
        /// initial radius sweep and PlayerHealth's own trigger-contact death check run via two
        /// different mechanisms with no ordering guarantee between them).</summary>
        public bool IsActive { get; private set; }

        protected override void Execute()
        {
            AudioManager.Instance?.PlayGroundSlamSfx();

            bool doubled = ComboSystem.Instance != null && ComboSystem.Instance.ConsumeDoubleSlamRadius();
            float radius = doubled ? ComboRadiusTiles : BaseRadiusTiles;
            Vector2Int origin = Movement.CurrentGridPosition;

            DefeatRobotsInRadius(origin, radius);

            if (shockwavePrefab != null)
            {
                var shockwaveGO = Instantiate(shockwavePrefab, transform.position, Quaternion.identity);
                // Diameter in world units = 2 * radius(tiles) * CellSize — see ShockwaveEffect.Configure's
                // doc comment for why this makes the VFX's footprint match the real kill radius instead
                // of a fixed placeholder size unrelated to it.
                float diameterWorldUnits = 2f * radius * TileMapRenderer.CellSize;
                shockwaveGO.GetComponent<ShockwaveEffect>()?.Configure(diameterWorldUnits, KillzoneDurationSeconds);
            }

            CameraShake.Instance?.Shake(shakeDuration, shakeMagnitude);

            StartCoroutine(LingeringKillzone(origin, radius));
        }

        private IEnumerator LingeringKillzone(Vector2Int origin, float radius)
        {
            IsActive = true;
            float elapsed = 0f;
            while (elapsed < KillzoneDurationSeconds)
            {
                DefeatRobotsInRadius(origin, radius, verbose: false);
                elapsed += Time.deltaTime;
                yield return null;
            }
            IsActive = false;
        }

        // TEMP diagnostic (remove once the "Ground Slam does nothing" report is root-caused) —
        // verbose=true (the initial cast only, not the per-frame lingering-zone re-checks, which
        // would otherwise flood the console for 3s every cast) logs every robot's grid
        // position/distance/state, so a failing case shows exactly where the chain breaks: no
        // robots found at all vs. found-but-out-of-radius vs. in-radius-but-ForceDefeat no-op'd
        // because it was already Defeated.
        private static void DefeatRobotsInRadius(Vector2Int origin, float radius, bool verbose = true)
        {
            var allRobots = FindObjectsByType<RobotBase>(FindObjectsSortMode.None);
            if (verbose)
            {
                Debug.Log($"[GroundSlamAbility] origin={origin} radius={radius} robotsInScene={allRobots.Length}");
            }
            foreach (var robot in allRobots)
            {
                float dist = Vector2Int.Distance(origin, robot.CurrentGridPosition);
                bool inRadius = dist <= radius;
                if (verbose)
                {
                    Debug.Log($"[GroundSlamAbility]  - {robot.name}: gridPos={robot.CurrentGridPosition} dist={dist:F2} inRadius={inRadius} stateBefore={robot.CurrentState}");
                }
                if (inRadius)
                {
                    robot.ForceDefeat();
                    if (verbose)
                    {
                        Debug.Log($"[GroundSlamAbility]    -> ForceDefeat() called, stateAfter={robot.CurrentState}");
                    }
                }
            }
        }
    }
}
