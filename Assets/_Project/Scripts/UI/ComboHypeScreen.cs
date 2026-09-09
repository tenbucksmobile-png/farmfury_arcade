using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Full-screen "hype" flash shown automatically right before a level actually begins —
    /// subscribes to GameManager.OnLevelHypeRequested (fired from LoadLevel, which freezes
    /// Time.timeScale for the whole gate so the level timer/robots don't burn away behind it — see
    /// that method's own doc comment). Picks one of the 8 big Combo_*.png banners at random, plays
    /// the Combo.mp3 stinger, holds fullscreen for totalSeconds, fades out, then invokes the
    /// callback GameManager handed it so LoadLevel can proceed to the interstitial-ad gate (or
    /// unfreeze directly).
    ///
    /// The root GameObject stays active for the app's whole lifetime (same convention
    /// SceneTransitionManager's own FadeOverlay uses) — visibility is driven purely by the
    /// CanvasGroup, never SetActive, so OnEnable's event subscription fires exactly once at scene
    /// load rather than needing to re-subscribe every time the banner shows. Fades run on unscaled
    /// time since Time.timeScale is 0 for the whole sequence.</summary>
    public class ComboHypeScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image bannerImage;
        [SerializeField] private Sprite[] comboBanners;
        [SerializeField] private float totalSeconds = 5f;
        [SerializeField] private float fadeSeconds = 0.4f;

        private Coroutine _routine;

        private void OnEnable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLevelHypeRequested += HandleHypeRequested;
            }
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLevelHypeRequested -= HandleHypeRequested;
            }
        }

        /// <summary>Falls back to firing onComplete immediately if no banner art is wired (e.g.
        /// before Phase5ProjectBuilder wires comboBanners), so a missing-art gap never blocks a
        /// level from actually starting.</summary>
        private void HandleHypeRequested(Action onComplete)
        {
            if (comboBanners == null || comboBanners.Length == 0 || bannerImage == null || canvasGroup == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (_routine != null)
            {
                StopCoroutine(_routine);
            }
            _routine = StartCoroutine(ShowRoutine(onComplete));
        }

        private IEnumerator ShowRoutine(Action onComplete)
        {
            bannerImage.sprite = comboBanners[UnityEngine.Random.Range(0, comboBanners.Length)];
            transform.SetAsLastSibling(); // always draw on top of whatever's currently showing
            canvasGroup.blocksRaycasts = true;

            AudioManager.Instance?.PlayComboSfx();

            yield return Fade(0f, 1f);
            float holdSeconds = Mathf.Max(0f, totalSeconds - fadeSeconds * 2f);
            yield return new WaitForSecondsRealtime(holdSeconds);
            yield return Fade(1f, 0f);

            canvasGroup.blocksRaycasts = false;
            _routine = null;
            onComplete?.Invoke();
        }

        private IEnumerator Fade(float from, float to)
        {
            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, t / fadeSeconds);
                yield return null;
            }
            canvasGroup.alpha = to;
        }
    }
}
