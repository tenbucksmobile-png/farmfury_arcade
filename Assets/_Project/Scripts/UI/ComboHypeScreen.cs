using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>In-gameplay combo callout, shown ONLY when ComboSystem.OnComboTriggered actually
    /// fires — i.e. a real combo (two specific characters swapped-to in the right order) was just
    /// triggered, never on an ordinary character swap and never at level start. Shows the exact
    /// banner art for the combo that fired (looked up by name against comboBanners), plays the
    /// Combo.mp3 stinger, then fades in and back out over TotalSeconds (3s).
    ///
    /// Reworked 2026-09-11 (per direct feedback) from a full-screen "page" — opaque black
    /// background, the current world's own dimmed backdrop, Time.timeScale frozen for the whole
    /// duration — into a lightweight overlay ON TOP of live gameplay instead: no background at
    /// all (the maze/HUD stay fully visible and playable behind the banner), no backdrop lookup,
    /// and only a brief real-time freeze right at the trigger instant (GameStartDelaySeconds) so
    /// the moment registers before gameplay keeps moving underneath the fading banner, rather than
    /// gating the whole celebration behind a multi-second freeze like the old page did. This is now
    /// purely a callout, not a scene transition.
    ///
    /// The root GameObject stays active for the app's whole lifetime (same convention
    /// SceneTransitionManager's own FadeOverlay uses) — visibility is driven purely by the
    /// CanvasGroup, never SetActive, so OnEnable's event subscription fires exactly once at scene
    /// load rather than needing to re-subscribe every time the banner shows.</summary>
    public class ComboHypeScreen : MonoBehaviour
    {
        [Serializable]
        private struct ComboBannerEntry
        {
            public string comboName;
            public Sprite banner;
        }

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image bannerImage;
        [SerializeField] private ComboBannerEntry[] comboBanners;

        [Tooltip("Total seconds the banner is visible for, fade-in and fade-out included.")]
        [SerializeField] private float totalSeconds = 3f;

        [Tooltip("Seconds each fade (in, then out) takes — the remainder of totalSeconds is a full-opacity hold.")]
        [SerializeField] private float fadeSeconds = 0.75f;

        [Tooltip("Brief real-time freeze right at the trigger instant, so the combo actually " +
                 "registers before gameplay resumes moving underneath the fading banner.")]
        [SerializeField] private float gameStartDelaySeconds = 0.25f;

        private Coroutine _routine;

        private void OnEnable()
        {
            if (ComboSystem.Instance != null)
            {
                ComboSystem.Instance.OnComboTriggered += HandleComboTriggered;
            }
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private void OnDisable()
        {
            if (ComboSystem.Instance != null)
            {
                ComboSystem.Instance.OnComboTriggered -= HandleComboTriggered;
            }
        }

        private Sprite FindBanner(string comboName)
        {
            if (comboBanners == null)
            {
                return null;
            }
            foreach (var entry in comboBanners)
            {
                if (entry.comboName == comboName)
                {
                    return entry.banner;
                }
            }
            return null;
        }

        private void HandleComboTriggered(string comboName)
        {
            var banner = FindBanner(comboName);
            if (banner == null || bannerImage == null || canvasGroup == null)
            {
                // No matching banner art wired for this combo (or the screen isn't fully wired
                // yet) — skip the callout entirely rather than showing a random/wrong banner.
                return;
            }

            bannerImage.sprite = banner;

            if (_routine != null)
            {
                StopCoroutine(_routine);
            }
            _routine = StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            transform.SetAsLastSibling(); // always draw on top of the live gameplay HUD

            AudioManager.Instance?.PlayComboSfx();

            // Brief real freeze right at the trigger instant — long enough for the moment to
            // register, short enough that it reads as a beat, not a pause. Real-time based
            // (WaitForSecondsRealtime) so it elapses correctly even while Time.timeScale is 0.
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(gameStartDelaySeconds);
            Time.timeScale = previousTimeScale;

            yield return Fade(0f, 1f);
            float holdSeconds = Mathf.Max(0f, totalSeconds - fadeSeconds * 2f);
            yield return new WaitForSecondsRealtime(holdSeconds);
            yield return Fade(1f, 0f);

            _routine = null;
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
