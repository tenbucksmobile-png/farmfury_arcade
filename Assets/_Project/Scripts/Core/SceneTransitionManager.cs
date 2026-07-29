using System;
using System.Collections;
using UnityEngine;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Central fade-transition helper for the screen flow (Main Menu -> World Map -> Gameplay ->
    /// Level Complete -> World Map). This project stays single-scene (see CLAUDE.md
    /// architecture note) — "scene transitions" here means fading a full-screen black Image to
    /// opaque, running the caller's show/hide swap (each screen controller owns its own
    /// GameObject's active state), then fading back. Every screen controller's Play/Back/Home
    /// button calls TransitionTo(() => { hide current; show next; }) instead of directly toggling
    /// SetActive, so the swap is never visible as an instant cut.
    /// </summary>
    public class SceneTransitionManager : Singleton<SceneTransitionManager>
    {
        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField] private float fadeSeconds = 0.25f;

        /// <summary>Every top-level, mutually-exclusive screen (Main Menu, World Map,
        /// Gameplay HUD, Level Complete, Level Failed, Character Roster, Leaderboards) — wired by
        /// Phase5ProjectBuilder. Overlays that layer on top of Gameplay instead of replacing it
        /// (Pause, Settings, New Character Unlock, the combo banner) manage their own visibility
        /// directly and are NOT in this list.</summary>
        [SerializeField] private GameObject[] screenRoots;

        private bool _isTransitioning;
        public bool IsTransitioning => _isTransitioning;

        /// <summary>Deactivates every registered screen and activates only screenToShow, faded.</summary>
        public void ShowOnly(GameObject screenToShow)
        {
            TransitionTo(() =>
            {
                foreach (var screen in screenRoots)
                {
                    if (screen != null)
                    {
                        screen.SetActive(screen == screenToShow);
                    }
                }
            });
        }

        public void TransitionTo(Action swapScreens)
        {
            if (_isTransitioning)
            {
                return;
            }
            StartCoroutine(TransitionRoutine(swapScreens));
        }

        private IEnumerator TransitionRoutine(Action swapScreens)
        {
            _isTransitioning = true;

            if (fadeGroup != null)
            {
                yield return Fade(0f, 1f);
            }

            swapScreens?.Invoke();

            if (fadeGroup != null)
            {
                yield return Fade(1f, 0f);
            }

            _isTransitioning = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            fadeGroup.blocksRaycasts = true;
            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.unscaledDeltaTime; // unscaled so it still fades while Time.timeScale == 0 (pause)
                fadeGroup.alpha = Mathf.Lerp(from, to, t / fadeSeconds);
                yield return null;
            }
            fadeGroup.alpha = to;
            fadeGroup.blocksRaycasts = to > 0.99f;
        }
    }
}
