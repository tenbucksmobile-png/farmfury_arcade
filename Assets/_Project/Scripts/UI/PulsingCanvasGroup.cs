using UnityEngine;

namespace FarmFuryArcade.UI
{
    /// <summary>Pulses a CanvasGroup's alpha between 0.35 and 1 on a plain sine-adjacent
    /// PingPong lerp — the exact same "flash" convention TitleScreenController's PRESS START
    /// prompt uses, extracted here so any other screen (e.g. Level Failed's "Insert Coin" row)
    /// can reuse it without duplicating the lerp.</summary>
    public class PulsingCanvasGroup : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float pulseSecondsPerCycle = 1.2f;

        private void Update()
        {
            if (canvasGroup == null)
            {
                return;
            }
            float t = Mathf.PingPong(Time.unscaledTime, pulseSecondsPerCycle) / pulseSecondsPerCycle;
            canvasGroup.alpha = Mathf.Lerp(0.35f, 1f, t);
        }
    }
}
