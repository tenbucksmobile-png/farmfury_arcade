using UnityEngine;

namespace FarmFuryArcade.UI
{
    /// <summary>A slow scale "breath" for the menu hub's cross-promo cards (unscaled time, so it runs while paused).</summary>
    public sealed class PromoBob : MonoBehaviour
    {
        public float amount = 0.035f;   // +/- fraction of the base scale
        public float period = 2.4f;     // seconds per full breath

        private Vector3 _base;

        private void Awake() => _base = transform.localScale;

        private void Update()
        {
            float s = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(0.1f, period));
            transform.localScale = _base * (1f + amount * s);
        }
    }
}
