using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Overlay shown by LevelCompleteController right after a level completion crosses a world's
    /// 2-star gate for the first time (GameManager.JustUnlockedWorldIndex) — same "celebration
    /// overlay layered on LevelComplete" convention as NewCharacterUnlockScreen, but for worlds
    /// instead of characters, and tap-gated rather than timer-dismissed. The world's own badge
    /// sprite (LevelSelectController.worldSignSprites — already bakes in the world's name/art, same
    /// as a character's selectCardArt) bursts in with an overshoot pop, then pulses
    /// (enlarges/shrinks) a couple of times to read as a celebratory beat rather than a static
    /// reveal.
    ///
    /// Originally auto-advanced on a fixed hold timer, same as NewCharacterUnlockScreen — but
    /// playtesting found the whole burst+pulse+hold beat (~2s) read as "nothing happened, it was
    /// very fast": by the time a player's eye caught the badge, the screen had already moved on to
    /// Level Select. Replaced the timer with a tap gate instead: after the pulse settles, a
    /// "Tap to continue" hint fades in and tapButton (a full-screen invisible Button) waits for
    /// player input before invoking onComplete — the caller (LevelCompleteController) uses that to
    /// navigate to Level Select's world-select state, where the badge now renders unlocked/coloured
    /// since save data was already updated before this overlay was shown.
    /// </summary>
    public class NewWorldUnlockScreen : MonoBehaviour
    {
        [SerializeField] private Image worldBadgeImage;
        [SerializeField] private Button tapButton;
        [SerializeField] private TextMeshProUGUI tapHintText;

        [Tooltip("Seconds the initial overshoot pop-in takes.")]
        [SerializeField] private float burstInSeconds = 0.4f;

        [Tooltip("Total seconds spent pulsing (enlarging/shrinking) after the burst-in.")]
        [SerializeField] private float pulseDurationSeconds = 1.2f;

        [Tooltip("How many full enlarge-then-shrink cycles happen during pulseDurationSeconds.")]
        [SerializeField] private int pulseCycleCount = 2;

        [Tooltip("Peak scale offset during a pulse, e.g. 0.12 = swells to 112% and shrinks to 88%.")]
        [SerializeField] private float pulseAmplitude = 0.12f;

        [Tooltip("Seconds the \"Tap to continue\" hint takes to fade in once the pulse settles.")]
        [SerializeField] private float hintFadeInSeconds = 0.4f;

        private Coroutine _routine;
        private bool _tapped;

        private void Awake()
        {
            if (tapButton != null)
            {
                tapButton.onClick.AddListener(() => _tapped = true);
            }
        }

        public void Show(Sprite badgeSprite, Action onComplete)
        {
            if (worldBadgeImage != null && badgeSprite != null)
            {
                worldBadgeImage.sprite = badgeSprite;
            }

            _tapped = false;
            if (tapButton != null)
            {
                tapButton.interactable = false;
            }
            if (tapHintText != null)
            {
                var c = tapHintText.color;
                tapHintText.color = new Color(c.r, c.g, c.b, 0f);
            }

            gameObject.SetActive(true);

            if (_routine != null)
            {
                StopCoroutine(_routine);
            }
            _routine = StartCoroutine(BurstPulseThenWaitForTap(onComplete));
        }

        /// <summary>Standard "ease out back" overshoot curve — rises past 1 around 70-90% through
        /// the tween, then settles to exactly 1 at t=1, giving the pop-in its "burst" feel without
        /// needing a separate two-phase lerp.</summary>
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        private IEnumerator BurstPulseThenWaitForTap(Action onComplete)
        {
            if (worldBadgeImage != null)
            {
                var rect = worldBadgeImage.rectTransform;
                var canvasGroup = worldBadgeImage.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = worldBadgeImage.gameObject.AddComponent<CanvasGroup>();
                }
                Vector3 baseScale = rect.localScale;

                rect.localScale = Vector3.zero;
                canvasGroup.alpha = 0f;

                float t = 0f;
                while (t < burstInSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    float p = Mathf.Clamp01(t / burstInSeconds);
                    rect.localScale = baseScale * EaseOutBack(p);
                    canvasGroup.alpha = Mathf.Clamp01(p * 2f);
                    yield return null;
                }
                rect.localScale = baseScale;
                canvasGroup.alpha = 1f;

                t = 0f;
                while (t < pulseDurationSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    float wave = Mathf.Sin(t / pulseDurationSeconds * pulseCycleCount * Mathf.PI * 2f);
                    rect.localScale = baseScale * (1f + wave * pulseAmplitude);
                    yield return null;
                }
                rect.localScale = baseScale;
            }

            // Only start accepting taps once the pulse has visibly settled — the button was
            // non-interactable up to this point so a tap thrown during the burst/pulse (impatient
            // mashing, or the same tap that dismissed a preceding character-unlock card) can't
            // instantly skip past the celebration before the player has even seen the badge.
            if (tapButton != null)
            {
                tapButton.interactable = true;
            }

            if (tapHintText != null)
            {
                float t = 0f;
                var baseColor = tapHintText.color;
                while (t < hintFadeInSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    float a = Mathf.Clamp01(t / hintFadeInSeconds);
                    tapHintText.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                    yield return null;
                }
                tapHintText.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
            }

            yield return new WaitUntil(() => _tapped);

            _routine = null;
            gameObject.SetActive(false);
            onComplete?.Invoke();
        }
    }
}
