using UnityEngine;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Placeholder AoE visual: scales up and fades out, then destroys itself.</summary>
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
