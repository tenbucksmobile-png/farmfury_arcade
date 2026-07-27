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
    /// system per the GDD — death just resets position, never score.
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

            if (robot.CurrentState == RobotState.Chase || robot.CurrentState == RobotState.Scatter)
            {
                StartCoroutine(DeathSequence());
            }
        }

        private IEnumerator DeathSequence()
        {
            IsRespawning = true;
            GameManager.Instance?.NotifyPlayerDeath();
            _movement.enabled = false;
            _movement.QueueInputDirection(Direction.None);

            if (_spriteRenderer != null)
            {
                yield return FadeOut(fadeOutSeconds);
            }

            float remaining = inputLockSeconds - fadeOutSeconds;
            if (remaining > 0f)
            {
                yield return new WaitForSeconds(remaining);
            }

            transform.position = _spawnWorldPosition;
            if (_spriteRenderer != null)
            {
                SetAlpha(1f);
            }

            var spawner = FindFirstObjectByType<Enemies.RobotSpawner>();
            spawner?.ResetAllRobotsToFactory();

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
