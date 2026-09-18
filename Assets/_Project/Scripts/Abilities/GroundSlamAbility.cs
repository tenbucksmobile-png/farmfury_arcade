using System.Collections;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;
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
        // Machine Cosmetics (2026-09-15) - Milk Tanker skin reskins the shockwave visual to a milk
        // splash; same radius/timing/defeat rule, purely a themed swap of which sprite the effect
        // shows (see EggDropAbility's own doc comment for why this is safe as a paid cosmetic).
        [SerializeField] private GameObject milkShockwavePrefab;
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

        private Vector3 _lingeringOriginWorld;
        private float _lingeringRadiusWorldUnits;

        protected override void Execute()
        {
            AudioManager.Instance?.PlayGroundSlamSfx();

            bool doubled = ComboSystem.Instance != null && ComboSystem.Instance.ConsumeDoubleSlamRadius();
            float radius = doubled ? ComboRadiusTiles : BaseRadiusTiles;
            // Cast from Bessie's own continuous position (transform.position), not her quantized
            // CurrentGridPosition — see DefeatRobotsInRadius's own doc comment for why the whole
            // sweep now works in continuous world space instead of grid cells.
            Vector3 originWorld = transform.position;
            float radiusWorldUnits = radius * TileMapRenderer.CellSize;

            DefeatRobotsInRadius(originWorld, radiusWorldUnits);

            bool milkTankerSkinEquipped = SaveManager.Instance != null &&
                SaveManager.Instance.GetEquippedCosmetic(CosmeticType.Skin, CharacterType.Bessie) == IAPManager.MachineTruckBessieProductId;
            GameObject shockwaveToSpawn = milkTankerSkinEquipped && milkShockwavePrefab != null ? milkShockwavePrefab : shockwavePrefab;
            if (shockwaveToSpawn != null)
            {
                var shockwaveGO = Instantiate(shockwaveToSpawn, transform.position, Quaternion.identity);
                // Diameter in world units = 2 * radius(tiles) * CellSize — see ShockwaveEffect.Configure's
                // doc comment for why this makes the VFX's footprint match the real kill radius instead
                // of a fixed placeholder size unrelated to it.
                float diameterWorldUnits = 2f * radius * TileMapRenderer.CellSize;
                shockwaveGO.GetComponent<ShockwaveEffect>()?.Configure(diameterWorldUnits, KillzoneDurationSeconds);
            }

            CameraShake.Instance?.Shake(shakeDuration, shakeMagnitude);

            _lingeringOriginWorld = originWorld;
            _lingeringRadiusWorldUnits = radiusWorldUnits;
            StartCoroutine(LingeringKillzone());
        }

        private IEnumerator LingeringKillzone()
        {
            IsActive = true;
            float elapsed = 0f;
            while (elapsed < KillzoneDurationSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            IsActive = false;
        }

        /// <summary>Real bug found and fixed (2026-09-15): "a robot walks straight through it and
        /// then kills Bessie even though she's ground-slammed." The lingering sweep used to run
        /// from inside LingeringKillzone's own coroutine, which resumes during Unity's Update phase
        /// — Unity gives NO ordering guarantee between that coroutine resumption and any individual
        /// robot's own Update()-driven RobotBase.UpdateMovement in the same frame. If the coroutine's
        /// step happened to run before a given robot's movement update that frame, the sweep checked
        /// a one-frame-stale CurrentGridPosition; a robot moving fast enough (or a frame drop letting
        /// UpdateMovement's own multi-cell-per-Update guard loop advance it several tiles at once)
        /// could cross the whole radius between two samples and never once register as "in range,"
        /// despite the sweep genuinely running every single frame. Moving the sweep into LateUpdate
        /// fixes this deterministically — Unity guarantees every GameObject's own Update() (which is
        /// what drives robot movement) completes before ANY GameObject's LateUpdate() runs in the
        /// same frame, so this now always sees each robot's fully up-to-date position.</summary>
        private void LateUpdate()
        {
            if (IsActive)
            {
                DefeatRobotsInRadius(_lingeringOriginWorld, _lingeringRadiusWorldUnits, verbose: false);
            }
        }

        /// <summary>Real bug found and fixed (2026-09-18): "robots move straight through, sometimes
        /// are not affected." This used to compare grid CELLS (Vector2Int.Distance against
        /// robot.CurrentGridPosition) — but RobotBase.CurrentGridPosition only updates once a robot
        /// fully ARRIVES at a new cell (see RobotBase.UpdateMovement), not continuously while it's
        /// travelling between cells. A robot could visually cross all the way through the shockwave's
        /// circle — which is drawn in continuous world space, sized to this exact radius (see
        /// Execute's diameterWorldUnits) — for up to a full tile's worth of travel time while its
        /// CurrentGridPosition still read the OLD, out-of-radius cell, letting it slip through
        /// untouched or only register right at the tail end of its pass. Fixed by comparing continuous
        /// world positions (robot.transform.position vs. the shockwave's own origin) against the
        /// radius in world units instead of tile counts — a robot is now defeated the instant it
        /// actually touches the visual shockwave, matching what the player sees on screen.</summary>
        private static void DefeatRobotsInRadius(Vector3 originWorld, float radiusWorldUnits, bool verbose = true)
        {
            var allRobots = FindObjectsByType<RobotBase>(FindObjectsSortMode.None);
            if (verbose)
            {
                Debug.Log($"[GroundSlamAbility] origin={originWorld} radiusWorldUnits={radiusWorldUnits} robotsInScene={allRobots.Length}");
            }
            foreach (var robot in allRobots)
            {
                float dist = Vector2.Distance(originWorld, robot.transform.position);
                bool inRadius = dist <= radiusWorldUnits;
                if (verbose)
                {
                    Debug.Log($"[GroundSlamAbility]  - {robot.name}: worldPos={robot.transform.position} dist={dist:F2} inRadius={inRadius} stateBefore={robot.CurrentState}");
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
