using UnityEngine;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Ducky's Skip Shot effect — a one-shot splash sprite at her departure point, mirrored
    /// left/right to match which way she skipped (SkipShotAbility picks via PlayForDirection based
    /// on the sign of the horizontal distance to her destination). Fades out and destroys itself,
    /// same scale-free fade convention as ShockwaveEffect.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class DuckySplashEffect : MonoBehaviour
    {
        [SerializeField] private Sprite leftSprite;
        [SerializeField] private Sprite rightSprite;
        [SerializeField] private float duration = 0.5f;

        private SpriteRenderer _spriteRenderer;
        private float _elapsed;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void PlayForDirection(bool movingRight)
        {
            var sprite = movingRight ? rightSprite : leftSprite;
            if (sprite != null)
            {
                _spriteRenderer.sprite = sprite;
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(_elapsed / duration);

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
