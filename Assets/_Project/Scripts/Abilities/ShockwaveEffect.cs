using UnityEngine;

namespace FarmFuryArcade.Abilities
{
    /// <summary>AoE visual: scales up and fades out, then destroys itself. Defaults (duration 0.4s,
    /// maxScale 4) are a placeholder fallback for callers that never call Configure — GroundSlamAbility
    /// does call it, passing the ability's own real radius/lingering-duration so the effect actually
    /// represents what's happening underneath (previously it used these fixed defaults regardless of
    /// the ability's real 2-tile radius/3s lingering killzone, which read as "the ability doesn't
    /// last as long / doesn't reach as far as it should" even though the underlying kill logic was
    /// already correct).</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ShockwaveEffect : MonoBehaviour
    {
        [SerializeField] private float duration = 0.4f;
        [SerializeField] private float maxScale = 4f;

        private SpriteRenderer _spriteRenderer;
        private float _elapsed;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>diameterWorldUnits should match the real effect's footprint (2 * radius in
        /// world units) so the sprite's growth genuinely covers the area being affected — assumes
        /// the sprite fills exactly 1 world unit in diameter at localScale 1, the same PPU
        /// convention every other wired sprite in this project uses (see ArtWiringBuilder's
        /// texture-import doc comment), so maxScale == diameterWorldUnits directly.</summary>
        public void Configure(float diameterWorldUnits, float durationSeconds)
        {
            maxScale = diameterWorldUnits;
            duration = durationSeconds;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(_elapsed / duration);

            transform.localScale = Vector3.one * Mathf.Lerp(0.2f, maxScale, p);
            Color c = _spriteRenderer.color;
            c.a = 1f - p;
            _spriteRenderer.color = c;

            if (p >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
