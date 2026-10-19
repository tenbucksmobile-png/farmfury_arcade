using UnityEngine;
using FarmFuryArcade.Enemies;

namespace FarmFuryArcade.Abilities
{
    /// <summary>The mud Billy's Mud Sling leaves on the ground when it doesn't hit a robot in flight
    /// (spawned by ThrownProjectileEffect.landingPrefab). The first robot to walk into it is defeated
    /// (ForceDefeat, same "deployed hazard kills outright" convention as EggHazard), then the pile
    /// fades away; unused, it dries up and fades after lifetimeSeconds. Harmless to the player.
    /// Ported from Farm Fury: Stampede's MudPile (squash-in on landing, slight wobble, fade).</summary>
    public class MudPileHazard : MonoBehaviour
    {
        private const float SettleSeconds = 0.15f;

        [SerializeField] private float lifetimeSeconds = 3f;
        [SerializeField] private float fadeSeconds = 0.5f;

        private SpriteRenderer _spriteRenderer;
        private Collider2D _collider;
        private Vector3 _baseScale;
        private float _time;
        private bool _spent;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<Collider2D>();
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            _time += Time.deltaTime;

            float settle = Mathf.Clamp01(_time / SettleSeconds);
            float wobble = 1f + 0.04f * Mathf.Sin(_time * 9f) * (1f - settle * 0.5f);
            transform.localScale = new Vector3(
                _baseScale.x * Mathf.Lerp(1.3f, 1f, settle) * wobble,
                _baseScale.y * Mathf.Lerp(0.6f, 1f, settle) / wobble,
                _baseScale.z);

            if (_spriteRenderer != null)
            {
                Color c = _spriteRenderer.color;
                c.a = Mathf.Clamp01((lifetimeSeconds - _time) / fadeSeconds);
                _spriteRenderer.color = c;
            }

            if (_time >= lifetimeSeconds)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_spent)
            {
                return;
            }

            var robot = other.GetComponent<RobotBase>();
            if (robot == null)
            {
                return;
            }

            robot.ForceDefeat();
            _spent = true;
            if (_collider != null)
            {
                _collider.enabled = false;
            }
            _time = Mathf.Max(_time, lifetimeSeconds - fadeSeconds); // splashes away: fade out from here
        }
    }
}
