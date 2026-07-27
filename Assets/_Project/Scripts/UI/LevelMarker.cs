using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FarmFuryArcade.UI
{
    /// <summary>One World Map button: locked (padlock colour + shake on tap), unlocked-not-played
    /// (bright, no stars), or completed (star rating). Spawned/refreshed by WorldMapController.</summary>
    public class LevelMarker : MonoBehaviour
    {
        private static readonly Color UnlockedColor = new Color(0.30f, 0.75f, 0.35f);
        private static readonly Color LockedColor = new Color(0.40f, 0.40f, 0.40f);
        private const int ShakeSteps = 6;
        private const float ShakeStepSeconds = 0.03f;
        private const float ShakeMagnitudePixels = 6f;

        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI numberText;
        [SerializeField] private GameObject lockIcon;
        [SerializeField] private StarDisplay starDisplay;

        public int LevelIndex { get; private set; }

        private Action<int, bool> _onTapped;
        private bool _unlocked;

        public void Initialize(int levelIndex, bool unlocked, int stars, Action<int, bool> onTapped)
        {
            LevelIndex = levelIndex;
            _unlocked = unlocked;
            _onTapped = onTapped;

            numberText.text = (levelIndex + 1).ToString();
            background.color = unlocked ? UnlockedColor : LockedColor;
            lockIcon.SetActive(!unlocked);

            bool hasStars = unlocked && stars > 0;
            starDisplay.gameObject.SetActive(hasStars);
            if (hasStars)
            {
                starDisplay.SetStars(stars);
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => _onTapped?.Invoke(LevelIndex, _unlocked));
        }

        public void PlayLockedShake()
        {
            StopAllCoroutines();
            StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            var rect = (RectTransform)transform;
            Vector2 original = rect.anchoredPosition;

            for (int i = 0; i < ShakeSteps; i++)
            {
                float offset = (i % 2 == 0 ? 1f : -1f) * ShakeMagnitudePixels;
                rect.anchoredPosition = original + new Vector2(offset, 0f);
                yield return new WaitForSecondsRealtime(ShakeStepSeconds);
            }

            rect.anchoredPosition = original;
        }
    }
}
