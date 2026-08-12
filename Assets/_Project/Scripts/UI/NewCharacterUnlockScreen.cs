using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Overlay shown by LevelCompleteController right after a character unlock. Uses the same
    /// framed "animal card" art as ChooseCharacterScreen (CharacterData.selectCardArt) — falls back
    /// to a plain gold-tinted placeholder square for any character without one yet. "Golden
    /// particles" is still approximated with a pulsing placeholder Image (no dedicated VFX art
    /// exists) — replace once that art lands without changing Show()'s signature. Progress is
    /// already saved by UnlockManager at the moment of unlock; this screen is purely presentational.
    /// </summary>
    public class NewCharacterUnlockScreen : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI bannerText;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Image characterCardImage;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private Button continueButton;
        [SerializeField] private Image goldenParticlesPlaceholder;

        [Tooltip("Seconds for the card's fade-in + scale-up reveal.")]
        [SerializeField] private float cardRevealDuration = 0.6f;

        [Tooltip("Starting scale (as a fraction of full size) the card reveals from — 0.4 reads as a clear but not too-dramatic pop-in.")]
        [SerializeField] private float cardRevealStartScale = 0.4f;

        private Coroutine _particleRoutine;
        private Coroutine _cardRevealRoutine;

        private void Awake()
        {
            continueButton.onClick.AddListener(Hide);
        }

        public void Show(CharacterType type)
        {
            var data = DataManager.Instance.GetCharacterData(type);

            bannerText.text = "NEW SQUAD MEMBER!";
            titleText.text = data != null
                ? $"{data.displayName.ToUpperInvariant()} JOINS THE SQUAD!"
                : $"{type} JOINS THE SQUAD!";
            statsText.text = data != null
                ? $"Speed: {data.movementSpeed:F1}\nAbility: {data.specialAbility}\nCooldown: {data.abilityCooldown:F0}s\n{data.abilityDescription}"
                : string.Empty;

            // Must activate before starting any coroutine — this overlay starts inactive
            // (Phase5ProjectBuilder.BuildLevelComplete), and StartCoroutine throws on an inactive
            // GameObject.
            gameObject.SetActive(true);

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

                if (_cardRevealRoutine != null) StopCoroutine(_cardRevealRoutine);
                _cardRevealRoutine = StartCoroutine(RevealCardRoutine());
            }

            if (goldenParticlesPlaceholder != null)
            {
                if (_particleRoutine != null) StopCoroutine(_particleRoutine);
                _particleRoutine = StartCoroutine(PulseParticles());
            }
        }

        private void Hide()
        {
            if (_particleRoutine != null)
            {
                StopCoroutine(_particleRoutine);
                _particleRoutine = null;
            }
            if (_cardRevealRoutine != null)
            {
                StopCoroutine(_cardRevealRoutine);
                _cardRevealRoutine = null;
            }
            gameObject.SetActive(false);
        }

        /// <summary>Fades the card in (alpha 0->1) while scaling it up from cardRevealStartScale to
        /// full size — a "pop into view" reveal, same convention CharacterSelectCard's selection
        /// animation already uses. Eased with an ease-out curve (Mathf.Sin) so the reveal settles
        /// rather than snapping to rest.
        ///
        /// This replaced an earlier attempt at a Y-axis "card flip" (rotating the RectTransform
        /// from 90 degrees down to 0) — that looked broken rather than 3D: this Canvas renders in
        /// RenderMode.ScreenSpaceOverlay, which has no perspective camera at all, so a rotated
        /// RectTransform is drawn via a flat orthographic squash with zero depth cue. For most of
        /// the 90->0 degree sweep the card was a razor-thin, unreadable sliver overlapping
        /// neighboring UI — only the final ~30% of the animation (roughly below 40-50 degrees)
        /// looked like an actual card. A screenshot caught mid-animation read as a rendering bug,
        /// but it was the rotation working exactly as coded — the effect itself just doesn't work
        /// in this render mode. Scale has no equivalent degenerate mid-state.</summary>
        private IEnumerator RevealCardRoutine()
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
            _cardRevealRoutine = null;
        }

        private IEnumerator PulseParticles()
        {
            float t = 0f;
            while (gameObject.activeInHierarchy)
            {
                t += Time.unscaledDeltaTime;
                Color c = goldenParticlesPlaceholder.color;
                c.a = 0.35f + 0.25f * Mathf.Sin(t * 3f);
                goldenParticlesPlaceholder.color = c;
                yield return null;
            }
        }
    }
}
