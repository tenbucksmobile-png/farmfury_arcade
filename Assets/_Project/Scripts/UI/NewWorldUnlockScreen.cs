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
    /// as a character's selectCardArt) bursts in with an overshoot pop, then pulses continuously
    /// (enlarges/shrinks on a steady sine wave) for as long as it's shown — through the "Tap to
    /// continue" hint fading in and the wait for the actual tap — so it reads as a genuinely
    /// "alive" celebratory badge rather than a static reveal that briefly wiggles then goes still.
    /// (2026-09-17, per direct feedback: the original version only pulsed for a couple of cycles
    /// right after the burst-in, then settled and sat motionless while waiting for the tap.)
    ///
    /// Originally auto-advanced on a fixed hold timer, same as NewCharacterUnlockScreen — but
    /// playtesting found the whole burst+pulse+hold beat (~2s) read as "nothing happened, it was
    /// very fast": by the time a player's eye caught the badge, the screen had already moved on to
    /// Level Select. Replaced the timer with a tap gate instead: after the pulse settles, a
    /// "Tap to continue" hint fades in and tapButton (a full-screen invisible Button) waits for
    /// player input before invoking onComplete — the caller (LevelCompleteController) uses that to
    /// navigate to Level Select's world-select state, where the badge now renders unlocked/coloured
    /// since save data was already updated before this overlay was shown.
    ///
    /// Background was a flat solid black behind the badge/banner — per a benchmark mockup, it now
    /// shows the just-unlocked world's own gameplay backdrop sprite (TileMapRenderer.MazeArtSet.
    /// backdropSprite, looked up by LevelCompleteController via GetOrAddArtSet) at BackgroundAlpha
    /// so the scenery reads as a faded/washed-out version of the world rather than pure black.
    /// </summary>
    public class NewWorldUnlockScreen : MonoBehaviour
    {
        [SerializeField] private Image worldBadgeImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Button tapButton;
        [SerializeField] private TextMeshProUGUI tapHintText;

        /// <summary>Alpha the unlocked world's own gameplay backdrop is shown at behind the badge —
        /// per a benchmark mockup, the celebration should show the world's own scenery faded/washed
        /// out rather than a flat solid-black backdrop (which is what this screen used before).</summary>
        private const float BackgroundAlpha = 0.55f;

        [Tooltip("Seconds the initial overshoot pop-in takes.")]
        [SerializeField] private float burstInSeconds = 0.4f;

        [Tooltip("Seconds one full enlarge-then-shrink pulse cycle takes, once continuous " +
            "pulsing starts (after the burst-in).")]
        [SerializeField] private float pulseCycleSeconds = 1.4f;

        [Tooltip("Peak scale offset during a pulse, e.g. 0.12 = swells to 112% and shrinks to 88%.")]
        [SerializeField] private float pulseAmplitude = 0.1f;

        [Tooltip("Seconds after the burst-in before the \"Tap to continue\" hint starts fading in " +
            "- the badge keeps pulsing underneath this pause.")]
        [SerializeField] private float pulseSettleSeconds = 0.6f;

        [Tooltip("Seconds the \"Tap to continue\" hint takes to fade in.")]
        [SerializeField] private float hintFadeInSeconds = 0.4f;

        private Coroutine _routine;
        private Coroutine _pulseRoutine;
        private bool _tapped;

        private void Awake()
        {
            if (tapButton != null)
            {
                tapButton.onClick.AddListener(() => _tapped = true);
            }
        }

        public void Show(Sprite badgeSprite, Sprite worldBackdropSprite, Action onComplete)
        {
            // Draw on top of whatever else is currently showing on LevelComplete (its own star/
            // score UI, or a character-unlock card that just finished) — same "always draw on top"
            // convention ComboHypeScreen already uses. This screen's build-time sibling order
            // already happens to be last, but that's a build-order coincidence, not a runtime
            // guarantee; a future reorder of BuildLevelComplete's element creation could silently
            // put something else on top of this overlay without this call.
            transform.SetAsLastSibling();

            if (worldBadgeImage != null && badgeSprite != null)
            {
                worldBadgeImage.sprite = badgeSprite;
            }

            if (backgroundImage != null)
            {
                if (worldBackdropSprite != null)
                {
                    backgroundImage.sprite = worldBackdropSprite;
                    var c = backgroundImage.color;
                    backgroundImage.color = new Color(c.r, c.g, c.b, BackgroundAlpha);
                }
                else
                {
                    // No backdrop registered for this world (e.g. art not wired yet) — stay
                    // invisible so the root panel's own solid black shows through, same as this
                    // screen's behaviour before backgroundImage existed.
                    backgroundImage.color = new Color(0f, 0f, 0f, 0f);
                }
            }

            _tapped = false;
            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }
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
            RectTransform rect = null;
            Vector3 baseScale = Vector3.one;

            if (worldBadgeImage != null)
            {
                rect = worldBadgeImage.rectTransform;
                var canvasGroup = worldBadgeImage.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = worldBadgeImage.gameObject.AddComponent<CanvasGroup>();
                }
                baseScale = rect.localScale;

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

                // Continuous pulsate - keeps running for as long as the badge is on screen (through
                // the settle pause below, the hint fading in, and the wait for the actual tap)
                // instead of stopping after a couple of cycles. Stopped explicitly once tapped, below.
                _pulseRoutine = StartCoroutine(PulseLoop(rect, baseScale));
            }

            // Small pause after the burst so the celebratory pop reads clearly before the hint
            // appears - the badge keeps pulsing underneath this wait, it doesn't go still for it.
            yield return new WaitForSecondsRealtime(pulseSettleSeconds);

            // Only start accepting taps once the burst/settle pause is done - the button was
            // non-interactable up to this point so a tap thrown during the burst (impatient
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

            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }
            if (rect != null)
            {
                rect.localScale = baseScale;
            }

            _routine = null;
            gameObject.SetActive(false);
            onComplete?.Invoke();
        }

        /// <summary>Runs indefinitely (stopped explicitly by the caller once the player taps, or by
        /// Show() re-invoking mid-cycle) - a steady sine-wave scale pulse, same "pulse" convention
        /// GameplayHUD's ability-ready flash already uses elsewhere in this project.</summary>
        private IEnumerator PulseLoop(RectTransform rect, Vector3 baseScale)
        {
            float t = 0f;
            while (true)
            {
                t += Time.unscaledDeltaTime;
                float wave = Mathf.Sin(t / pulseCycleSeconds * Mathf.PI * 2f);
                rect.localScale = baseScale * (1f + wave * pulseAmplitude);
                yield return null;
            }
        }
    }
}
