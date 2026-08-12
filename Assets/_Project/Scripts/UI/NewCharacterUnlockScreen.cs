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

        [Tooltip("Seconds for the card's fade-in + rotate-to-front reveal.")]
        [SerializeField] private float cardRevealDuration = 0.6f;

        [Tooltip("Starting Y rotation (degrees) the card reveals from — 90 reads as edge-on, about to face the viewer.")]
        [SerializeField] private float cardRevealStartAngle = 90f;

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

            gameObject.SetActive(true);

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

        /// <summary>Fades the card in (alpha 0->1) while rotating it around Y from
        /// cardRevealStartAngle down to 0 (facing the viewer) — a cheap "card flip reveal" that
        /// needs no 3D camera setup, since an orthographic Canvas already foreshortens a Y-rotated
        /// RectTransform the same way a true perspective flip would. Eased with an ease-out curve
        /// (Mathf.Sin) so the reveal settles rather than snapping to rest.</summary>
        private IEnumerator RevealCardRoutine()
        {
            var cardTransform = characterCardImage.rectTransform;
            var baseColor = characterCardImage.color;

            float t = 0f;
            while (t < cardRevealDuration)
            {
                t += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(t / cardRevealDuration);
                float eased = Mathf.Sin(progress * Mathf.PI * 0.5f);

                cardTransform.localRotation = Quaternion.Euler(0f, Mathf.Lerp(cardRevealStartAngle, 0f, eased), 0f);
                characterCardImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, eased);

                yield return null;
            }

            cardTransform.localRotation = Quaternion.identity;
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
