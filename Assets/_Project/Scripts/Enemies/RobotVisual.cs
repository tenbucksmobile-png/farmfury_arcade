using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Gameplay;

namespace FarmFuryArcade.Enemies
{
    /// <summary>
    /// No art yet for some robots (see CLAUDE.md "Art status"), so Vulnerable/Defeated states are
    /// shown by swapping the placeholder SpriteRenderer's colour instead of swapping sprite sheets
    /// for those — same convention as PlaceholderSprite everywhere else. Once a robot has real
    /// front/back(/left/right) art (via SetDirectionalSprites), Update also swaps the base sprite
    /// by facing direction — robots with no art assigned keep the colour-only placeholder
    /// behaviour. Vulnerable flashes white during the last 2 seconds of the power state (spec's
    /// "warning flash").
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class RobotVisual : MonoBehaviour
    {
        private static readonly Color VulnerableColor = new Color(0.16f, 0.32f, 0.85f); // placeholder "vulnerable blue"
        private static readonly Color DefeatedColor = new Color(0.9f, 0.9f, 0.95f); // placeholder "eyes" pale

        private const float FlashThresholdSeconds = 2f;
        private const float FlashSpeed = 6f;

        [SerializeField] private Color normalColor = Color.red;
        [SerializeField] private Sprite frontSprite;
        [SerializeField] private Sprite backSprite;
        [SerializeField] private Sprite leftSprite;
        [SerializeField] private Sprite rightSprite;
        [SerializeField] private Sprite defeatedSprite;

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

        /// <summary>Left/right are optional — robots with only front/back art (e.g. Harvester,
        /// Heavy) pass null and keep the old "Up shows back, everything else shows front"
        /// behaviour. When only a left sprite is supplied, Right mirrors it horizontally (same
        /// convention CharacterAnimator uses for Cluck).</summary>
        public void SetDirectionalSprites(Sprite front, Sprite back, Sprite left = null, Sprite right = null)
        {
            frontSprite = front;
            backSprite = back;
            leftSprite = left;
            rightSprite = right;
        }

        /// <summary>Universal "eyes only" sprite shown while Defeated/Returning (spec 3.8) —
        /// replaces the pale colour-tint placeholder for any robot this is assigned to.</summary>
        public void SetDefeatedSprite(Sprite sprite)
        {
            defeatedSprite = sprite;
        }

        private void Update()
        {
            if (_robot == null)
            {
                return;
            }

            bool eyesOnly = defeatedSprite != null &&
                (_robot.CurrentState == RobotState.Defeated || _robot.CurrentState == RobotState.Returning);

            if (eyesOnly)
            {
                _spriteRenderer.sprite = defeatedSprite;
                _spriteRenderer.flipX = false;
                _spriteRenderer.color = Color.white;
                return;
            }

            if (frontSprite != null)
            {
                bool flip = false;
                Sprite chosen;
                switch (_robot.CurrentDirection)
                {
                    case Direction.Up:
                        chosen = backSprite != null ? backSprite : frontSprite;
                        break;
                    case Direction.Left:
                        chosen = leftSprite != null ? leftSprite : frontSprite;
                        break;
                    case Direction.Right:
                        if (rightSprite != null)
                        {
                            chosen = rightSprite;
                        }
                        else if (leftSprite != null)
                        {
                            chosen = leftSprite;
                            flip = true;
                        }
                        else
                        {
                            chosen = frontSprite;
                        }
                        break;
                    default:
                        chosen = frontSprite;
                        break;
                }

                _spriteRenderer.sprite = chosen;
                _spriteRenderer.flipX = flip;
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
                _ => BaseTintColor
            };
        }

        /// <summary>Once real art is assigned, the "normal" tint must be white — multiplying a
        /// real sprite (e.g. Scout's pink, Patrol's cyan) by the old placeholder-square colour
        /// would wash it out to that solid colour instead of showing the art's real palette.
        /// Robots with no art yet keep tinting the plain placeholder square as before.</summary>
        private Color BaseTintColor => frontSprite != null ? Color.white : normalColor;

        /// <summary>Placeholder "shake" for Stun/KnockBack (Phase 4) — flickers between the base
        /// tint and a darkened version rather than jittering transform.position, which would
        /// desync RobotBase's own grid-position tracking.</summary>
        private Color GetStunShakeColor()
        {
            bool flashOn = Mathf.PingPong(Time.time * FlashSpeed, 1f) > 0.5f;
            Color baseColor = BaseTintColor;
            return flashOn ? baseColor : Color.Lerp(baseColor, Color.black, 0.5f);
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
