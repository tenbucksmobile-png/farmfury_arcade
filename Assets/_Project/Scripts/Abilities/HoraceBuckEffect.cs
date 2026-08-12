using UnityEngine;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Horace's Rear Kick effect — a one-shot "buck" sprite at the target robot's landing
    /// spot, mirrored left/right to match the knockback direction (RearKickAbility picks via
    /// PlayForDirection based on the sign of the knockback vector's X). Same fade convention as
    /// DuckySplashEffect/ShockwaveEffect.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class HoraceBuckEffect : MonoBehaviour
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
