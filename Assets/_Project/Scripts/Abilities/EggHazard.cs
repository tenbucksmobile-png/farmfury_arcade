using System.Collections;
using UnityEngine;
using FarmFuryArcade.Enemies;

namespace FarmFuryArcade.Abilities
{
    /// <summary>Sits as a hazard until a robot walks over it, then plays a 2-frame crack/burst
    /// animation (crackedSprite -> burstSprite, half a second each) and disappears — one-shot per
    /// egg now, not reusable within its lifetime like the original "eggs persist for 15 seconds"
    /// version. lifetimeSeconds is still a fallback auto-destroy for an egg nothing ever walks over.
    /// A robot that walks over it is now instantly defeated (ForceDefeat, bypassing the Vulnerable
    /// requirement — same convention as PuffUpAbility) rather than just stunned, per a gameplay
    /// rule change: a deployed ability hazard a robot runs through should kill it, not just
    /// incapacitate it. Applies to every character's ability-created robot hazard except Percy
    /// (BounceRoll) and Ducky (SkipShot), neither of which has one.</summary>
    public class EggHazard : MonoBehaviour
    {
        private const float BurstFrameSeconds = 0.5f;

        [SerializeField] private float lifetimeSeconds = 15f;
        [SerializeField] private Sprite crackedSprite;
        [SerializeField] private Sprite burstSprite;

        private SpriteRenderer _spriteRenderer;
        private Collider2D _collider;
        private bool _triggered;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<Collider2D>();
        }

        private void Start()
        {
            Destroy(gameObject, lifetimeSeconds);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_triggered)
            {
                return;
            }

            var robot = other.GetComponent<RobotBase>();
            if (robot == null)
            {
                return;
            }

            robot.ForceDefeat();
            StartCoroutine(BurstThenDisappear());
        }

        private IEnumerator BurstThenDisappear()
        {
            _triggered = true;
            if (_collider != null)
            {
                _collider.enabled = false;
            }

            if (_spriteRenderer != null && crackedSprite != null)
            {
                _spriteRenderer.sprite = crackedSprite;
            }
            yield return new WaitForSeconds(BurstFrameSeconds);

            if (_spriteRenderer != null && burstSprite != null)
            {
                _spriteRenderer.sprite = burstSprite;
            }
            yield return new WaitForSeconds(BurstFrameSeconds);

            Destroy(gameObject);
        }
    }
}
