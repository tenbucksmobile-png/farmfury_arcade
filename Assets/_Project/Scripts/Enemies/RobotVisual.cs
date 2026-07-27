using UnityEngine;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.Enemies
{
    /// <summary>
    /// No art yet (see CLAUDE.md "No art or audio"), so Vulnerable/Defeated states are shown by
    /// swapping the placeholder SpriteRenderer's colour instead of swapping sprite sheets — same
    /// convention as PlaceholderSprite everywhere else. Vulnerable flashes white during the last 2
    /// seconds of the power state (spec's "warning flash"). When real art lands, replace the
    /// colour swap with sprite swaps from RobotData's Robot_Vulnerable_Walk / Robot_Defeated_Eyes
    /// sheets and delete this component.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class RobotVisual : MonoBehaviour
    {
        private static readonly Color VulnerableColor = new Color(0.16f, 0.32f, 0.85f); // placeholder "vulnerable blue"
        private static readonly Color DefeatedColor = new Color(0.9f, 0.9f, 0.95f); // placeholder "eyes" pale

        private const float FlashThresholdSeconds = 2f;
        private const float FlashSpeed = 6f;

        [SerializeField] private Color normalColor = Color.red;

        private SpriteRenderer _spriteRenderer;
        private RobotBase _robot;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _robot = GetComponent<RobotBase>();
        }

        public void SetNormalColor(Color color)
        {
            normalColor = color;
        }

        private void Update()
        {
            if (_robot == null)
            {
                return;
            }

            if (_robot.IsStunned || _robot.IsKnockedBack)
            {
                _spriteRenderer.color = GetStunShakeColor();
                return;
            }

            _spriteRenderer.color = _robot.CurrentState switch
            {
                RobotState.Vulnerable => GetVulnerableColor(),
                RobotState.Defeated or RobotState.Returning => DefeatedColor,
                _ => normalColor
            };
        }

        /// <summary>Placeholder "shake" for Stun/KnockBack (Phase 4) — flickers between normal
        /// colour and a darkened tint rather than jittering transform.position, which would
        /// desync RobotBase's own grid-position tracking.</summary>
        private Color GetStunShakeColor()
        {
            bool flashOn = Mathf.PingPong(Time.time * FlashSpeed, 1f) > 0.5f;
            return flashOn ? normalColor : Color.Lerp(normalColor, Color.black, 0.5f);
        }

        private Color GetVulnerableColor()
        {
            var powerManager = PowerPelletManager.Instance;
            if (powerManager != null && powerManager.TimeRemaining <= FlashThresholdSeconds)
            {
                bool flashOn = Mathf.PingPong(Time.time * FlashSpeed, 1f) > 0.5f;
                return flashOn ? Color.white : VulnerableColor;
            }

            return VulnerableColor;
        }
    }
}
