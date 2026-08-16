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
    /// </summary>
    public class NewCharacterUnlockScreen : MonoBehaviour
    {
        [SerializeField] private Image characterCardImage;
        [SerializeField] private Button tapButton;

        [Tooltip("Seconds the card's fade-in + scale-up reveal takes.")]
        [SerializeField] private float cardRevealDuration = 0.6f;

        [Tooltip("Starting scale (as a fraction of full size) the card reveals from.")]
        [SerializeField] private float cardRevealStartScale = 0.4f;

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

        /// <summary>Scales the card up from cardRevealStartScale to full size while fading it in
        /// (a "pop into view" reveal, same convention CharacterSelectCard's selection animation
        /// uses), holds at full reveal for autoDismissSeconds, then hides the whole overlay.
        ///
        /// Scale, not rotation: an earlier attempt animated the card via a Y-axis RectTransform
        /// rotation (a cheap "card flip"), but this Canvas renders in RenderMode.ScreenSpaceOverlay,
        /// which has no perspective camera at all — a rotated RectTransform is drawn via a flat
        /// orthographic squash with zero depth cue, so for most of the rotation sweep the card was
        /// a razor-thin, unreadable sliver overlapping neighbouring UI. Scale has no equivalent
        /// degenerate mid-state.</summary>
        private IEnumerator RevealThenWaitForTap()
        {
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
                    float eased = Mathf.Sin(progress * Mathf.PI * 0.5f);

                    cardTransform.localScale = Vector3.Lerp(targetScale * cardRevealStartScale, targetScale, eased);
                    characterCardImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, eased);

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
    }
}
