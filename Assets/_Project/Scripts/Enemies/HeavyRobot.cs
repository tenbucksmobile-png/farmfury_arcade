using System.Collections;
using UnityEngine;

namespace FarmFuryArcade.Enemies
{
    /// <summary>Same direct-pursuit targeting as HarvesterRobot, but requires 2 power pellet hits
    /// to defeat (RobotData_Heavy.healthPoints = 2) and moves at 0.7x speed. RobotBase.RegisterHit
    /// already implements "decrement, defeat at zero" generically; this override just layers the
    /// brief visual glitch on a non-lethal hit.</summary>
    public class HeavyRobot : RobotBase
    {
        [SerializeField] private float glitchDuration = 0.3f;
        [SerializeField] private float glitchFlickerInterval = 0.05f;

        protected override float SpeedMultiplier => 0.7f;

        protected override Vector2Int GetTargetPosition()
        {
            return playerMovement != null ? playerMovement.CurrentGridPosition : CurrentGridPosition;
        }

        public override void RegisterHit()
        {
            if (CurrentState != RobotState.Vulnerable)
            {
                return;
            }

            base.RegisterHit();

            if (CurrentState == RobotState.Vulnerable)
            {
                // Still alive after this hit (health > 0) — glitch flicker, stays Vulnerable.
                StartCoroutine(GlitchFlicker());
            }
        }

        private IEnumerator GlitchFlicker()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < glitchDuration)
            {
                sr.enabled = !sr.enabled;
                yield return new WaitForSeconds(glitchFlickerInterval);
                elapsed += glitchFlickerInterval;
            }
            sr.enabled = true;
        }
    }
}
