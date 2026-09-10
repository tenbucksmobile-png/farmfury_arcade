using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Overlay shown by LevelCompleteController right after a character unlock. Rebuilt to match a
    /// Canva mockup: full-screen night-farm backdrop, Logo top-left, a wood-sign "Unlocked" banner
    /// top-centre, and the character's own framed card art (CharacterData.selectCardArt — the same
    /// per-character asset ChooseCharacterScreen uses, and now sized to match — see
    /// Phase5ProjectBuilder's unlockCard) large and centred. That card art already has the
    /// character's name baked in (confirmed against the uploaded files, e.g. Percy_Pig.png), so this
    /// screen needs no separate name/title/stats text at all — it's purely the card reveal.
    /// Dismisses on tap (tapButton, a full-screen invisible Button on the root panel — same
    /// convention NewWorldUnlockScreen uses) rather than auto-dismissing on a fixed timer — per
    /// feedback that a timed fade-out didn't give the player enough control/time to actually look at
    /// the reveal. Progress is already saved by UnlockManager at the moment of unlock; this screen
    /// is purely presentational.
    ///
    /// Made more animated/exciting (2026-09-11, per direct feedback) two ways: the card's reveal
    /// now overshoots past full size before settling (EaseOutBack, same "pop" curve
    /// NewWorldUnlockScreen's own badge burst-in already uses) instead of a plain ease-in, and a
    /// ConfettiBurst fires a short beat after the reveal starts (ConfettiRevealDelaySeconds — tuned
    /// again the same day, see that constant's own doc comment: firing at t=0 meant the burst's
    /// own short life was already ticking down while the card was still nearly invisible), bigger
    /// and slower than ConfettiBurst's own defaults so it stays on screen well past the card
    /// settling rather than racing to finish during the reveal.
    /// </summary>
    public class NewCharacterUnlockScreen : MonoBehaviour
    {
        [SerializeField] private Image characterCardImage;
        [SerializeField] private Button tapButton;
        [SerializeField] private ConfettiBurst confettiBurst;

        [Tooltip("Seconds the card's pop-in reveal takes.")]
        [SerializeField] private float cardRevealDuration = 0.6f;

        [Tooltip("Starting scale (as a fraction of full size) the card reveals from.")]
        [SerializeField] private float cardRevealStartScale = 0.4f;

        // Confetti timing/scale tuned 2026-09-11 per direct feedback ("it currently fires so
        // quickly its over before the character card is seen — perhaps slow it down as well —
        // create more"). Firing Burst() at t=0 (the old behaviour) meant the confetti's own life
        // was already ticking down while the card was still nearly invisible (alpha only reaches
        // ~24% by t=0.15s under the *1.6 fade-in curve below) — by the time the card actually
        // read as "there," a chunk of a short 2s burst had already played out unseen behind it.
        // ConfettiRevealDelaySeconds holds the burst until the card is meaningfully visible;
        // ConfettiParticleCount/ConfettiDurationSeconds replace ConfettiBurst.Burst()'s own
        // defaults (70 / 2f) with a bigger, slower burst that stays on screen well past the card's
        // own reveal instead of racing to finish during it.
        private const float ConfettiRevealDelaySeconds = 0.15f;
        private const int ConfettiParticleCount = 110;
        private const float ConfettiDurationSeconds = 3.5f;

        private Coroutine _showRoutine;
        private System.Action _onDismissed;
        private bool _tapped;

        private void Awake()
        {
            if (tapButton != null)
            {
                tapButton.onClick.AddListener(() => _tapped = true);
            }
        }

        /// <summary>onDismissed (optional) fires once, right after the card's own auto-dismiss —
        /// lets a caller chain a follow-up celebration (e.g. a new-world-unlock burst) without it
        /// visually overlapping this card's own reveal/hold.</summary>
        public void Show(CharacterType type, System.Action onDismissed = null)
        {
            _onDismissed = onDismissed;
            var data = DataManager.Instance.GetCharacterData(type);

            if (characterCardImage != null)
            {
                var cardArt = data != null ? data.selectCardArt : null;
                if (cardArt != null)
                {
                    characterCardImage.sprite = cardArt;
                    characterCardImage.color = Color.white;
                }
                else
                {
                    characterCardImage.color = new Color(1f, 0.84f, 0f);
                }
            }

            _tapped = false;
            if (tapButton != null)
            {
                tapButton.interactable = false;
            }

            gameObject.SetActive(true);

            if (_showRoutine != null)
            {
                StopCoroutine(_showRoutine);
            }
            _showRoutine = StartCoroutine(RevealThenWaitForTap());
        }

        private void Hide()
        {
            if (_showRoutine != null)
            {
                StopCoroutine(_showRoutine);
                _showRoutine = null;
            }
            gameObject.SetActive(false);
        }

        /// <summary>Standard "ease out back" overshoot curve — rises past 1 around 70-90% through
        /// the tween, then settles to exactly 1 at t=1, giving the pop-in its "burst" feel without
        /// needing a separate two-phase lerp. Same curve NewWorldUnlockScreen's own badge burst-in
        /// uses.</summary>
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        /// <summary>Scales the card up from cardRevealStartScale to full size (overshooting past
        /// full size before settling — EaseOutBack, not a plain ease-in) while fading it in, firing
        /// a ConfettiBurst a short beat after the reveal starts — see ConfettiRevealDelaySeconds'
        /// own doc comment for why not instantly — holds at full reveal until tapped, then hides
        /// the whole overlay.
        ///
        /// Scale, not rotation: an earlier attempt animated the card via a Y-axis RectTransform
        /// rotation (a cheap "card flip"), but this Canvas renders in RenderMode.ScreenSpaceOverlay,
        /// which has no perspective camera at all — a rotated RectTransform is drawn via a flat
        /// orthographic squash with zero depth cue, so for most of the rotation sweep the card was
        /// a razor-thin, unreadable sliver overlapping neighbouring UI. Scale has no equivalent
        /// degenerate mid-state.</summary>
        private IEnumerator RevealThenWaitForTap()
        {
            StartCoroutine(FireConfettiDelayed());

            if (characterCardImage != null)
            {
                var cardTransform = characterCardImage.rectTransform;
                var baseColor = characterCardImage.color;
                var targetScale = cardTransform.localScale;

                float t = 0f;
                while (t < cardRevealDuration)
                {
                    t += Time.unscaledDeltaTime;
                    float progress = Mathf.Clamp01(t / cardRevealDuration);
                    float eased = EaseOutBack(progress);
                    // Fade uses a simple clamp (not the overshooting eased value directly) so alpha
                    // never overshoots past 1 or dips below 0 during the bounce.
                    float alpha = Mathf.Clamp01(progress * 1.6f);

                    cardTransform.localScale = Vector3.LerpUnclamped(targetScale * cardRevealStartScale, targetScale, eased);
                    characterCardImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

                    yield return null;
                }

                cardTransform.localScale = targetScale;
                characterCardImage.color = baseColor;
            }

            // Only start accepting taps once the reveal has finished — same "don't let an
            // impatient/leftover tap instantly skip the reveal" guard NewWorldUnlockScreen uses.
            if (tapButton != null)
            {
                tapButton.interactable = true;
            }

            yield return new WaitUntil(() => _tapped);

            _showRoutine = null;
            Hide();

            var callback = _onDismissed;
            _onDismissed = null;
            callback?.Invoke();
        }

        /// <summary>Waits ConfettiRevealDelaySeconds (unscaled, matching the reveal tween's own
        /// Time.unscaledDeltaTime timing) before firing the burst, then requests the bigger/slower
        /// burst directly — see the constants' own doc comment above for why. Runs as its own
        /// coroutine, in parallel with the card's scale/fade tween, rather than inline in
        /// RevealThenWaitForTap, so a short wait here never delays the card's own reveal start.</summary>
        private IEnumerator FireConfettiDelayed()
        {
            yield return new WaitForSecondsRealtime(ConfettiRevealDelaySeconds);
            confettiBurst?.Burst(ConfettiParticleCount, ConfettiDurationSeconds);
        }
    }
}
