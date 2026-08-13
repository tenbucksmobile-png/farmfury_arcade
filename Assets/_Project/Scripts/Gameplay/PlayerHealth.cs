using System.Collections;
using UnityEngine;
using FarmFuryArcade.Core;
using FarmFuryArcade.Enemies;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>
    /// Detects Cluck's contact with robots via the same trigger-overlap pattern CropCollector
    /// uses. A robot in Chase or Scatter is hostile (both are "solid" states in the classic
    /// arcade convention — only Vulnerable is safe to touch); a Vulnerable robot is defeated on
    /// contact. Defeated/Returning robots are already harmless "eyes" and are ignored. No lives
    /// system per the GDD — death just resets position, never score. The one exception is the
    /// death that exceeds GameManager.MaxRespawns: that no longer ends the run unconditionally,
    /// it offers a coin-spend revive first (see DeathSequence's GameManager.ReviveDecisionPending
    /// wait and GameManager.RequestRevivePrompt/AcceptRevive/DeclineRevive).
    /// </summary>
    [RequireComponent(typeof(GridMovement))]
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private float inputLockSeconds = 1.5f;
        [SerializeField] private float fadeOutSeconds = 0.6f;

        private GridMovement _movement;
        private SpriteRenderer _spriteRenderer;
        private Vector3 _spawnWorldPosition;

        public bool IsRespawning { get; private set; }

        private void Awake()
        {
            _movement = GetComponent<GridMovement>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            _spawnWorldPosition = transform.position;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsRespawning)
            {
                return;
            }

            var robot = other.GetComponent<RobotBase>();
            if (robot == null)
            {
                return;
            }

            if (robot.CurrentState == RobotState.Vulnerable)
            {
                robot.RegisterHit();
                return;
            }

            // A Stunned/KnockedBack robot is still technically Chase/Scatter (Stun only freezes its
            // AI/movement, not its state) but is meant to read as incapacitated — a robot an
            // ability just hit shouldn't be able to kill the player it was used to protect against.
            bool incapacitated = robot.IsStunned || robot.IsKnockedBack;
            if ((robot.CurrentState == RobotState.Chase || robot.CurrentState == RobotState.Scatter) && !incapacitated)
            {
                StartCoroutine(DeathSequence());
            }
        }

        private IEnumerator DeathSequence()
        {
            IsRespawning = true;
            bool hasRespawnLeft = GameManager.Instance == null || GameManager.Instance.NotifyPlayerDeath();
            AudioManager.Instance?.PlayAnimalDeathSfx();
            _movement.enabled = false;
            _movement.QueueInputDirection(Direction.None);

            if (_spriteRenderer != null)
            {
                yield return FadeOut(fadeOutSeconds);
            }

            if (!hasRespawnLeft)
            {
                // Respawn cap exhausted — GameManager.NotifyPlayerDeath raised a revive-for-coins
                // offer (RequestRevivePrompt) instead of ending the run outright. Wait for that
                // decision (or its own no-listener safety net, which auto-declines) before deciding
                // whether to fall through to a normal respawn or stay faded out.
                yield return new WaitUntil(() => GameManager.Instance == null || !GameManager.Instance.ReviveDecisionPending);

                if (GameManager.Instance == null || !GameManager.Instance.ConsumeRevived())
                {
                    // Declined, couldn't afford it, or no GameManager at all — the run has already
                    // ended (DeclineRevive calls EndLevel(false)); GameplayHUD's state-watcher will
                    // swap to the Level Failed screen. Stay faded out rather than respawning back
                    // into a maze that's over.
                    yield break;
                }
                // Revived — fall through to the normal respawn logic below.
            }

            float remaining = inputLockSeconds - fadeOutSeconds;
            if (remaining > 0f)
            {
                yield return new WaitForSeconds(remaining);
            }

            // Only the character respawns here — robots stay wherever they currently are rather
            // than being reset back to the factory (that used to happen on every player death).
            transform.position = _spawnWorldPosition;
            if (_spriteRenderer != null)
            {
                SetAlpha(1f);
            }

            _movement.enabled = true;
            IsRespawning = false;
        }

        private IEnumerator FadeOut(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                SetAlpha(Mathf.Lerp(1f, 0f, elapsed / duration));
                yield return null;
            }
            SetAlpha(0f);
        }

        private void SetAlpha(float alpha)
        {
            Color c = _spriteRenderer.color;
            c.a = alpha;
            _spriteRenderer.color = c;
        }
    }
}
