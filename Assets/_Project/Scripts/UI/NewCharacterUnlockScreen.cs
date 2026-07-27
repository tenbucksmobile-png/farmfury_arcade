using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Overlay shown by LevelCompleteController right after a character unlock. No sprite/particle
    /// art exists yet, so "golden particles" and "signature animation" are approximated with a
    /// pulsing placeholder Image and a card fade-in — replace with real VFX/animation once art
    /// lands without changing Show()'s signature. Progress is already saved by UnlockManager at
    /// the moment of unlock; this screen is purely presentational.
    /// </summary>
    public class NewCharacterUnlockScreen : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI bannerText;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Image characterCardImage;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private Button continueButton;
        [SerializeField] private Image goldenParticlesPlaceholder;

        private Coroutine _particleRoutine;

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
                characterCardImage.color = new Color(1f, 0.84f, 0f);
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
            gameObject.SetActive(false);
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
