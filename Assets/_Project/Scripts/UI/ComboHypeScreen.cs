using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Full-screen "hype" flash shown ONLY when ComboSystem.OnComboTriggered actually
    /// fires — i.e. a real combo (two specific characters swapped-to in the right order) was just
    /// triggered, never on an ordinary character swap and never at level start. Shows the exact
    /// banner art for the combo that fired (looked up by name against comboBanners) over a dimmed
    /// version of the current world's own gameplay backdrop, plays the Combo.mp3 stinger, holds
    /// for totalSeconds, then fades out and hands control back to gameplay. This is now the only
    /// on-screen cue for a triggered combo — the older in-maze "COMBO! {name}" text toast
    /// (ComboNotificationBanner) was removed once this full page replaced it.
    ///
    /// This replaced an earlier version tied to GameManager.OnLevelHypeRequested, which fired once
    /// per level load regardless of whether any combo had ever been triggered — that read as a
    /// combo celebration for something that hadn't happened yet. See GameManager.LoadLevel's own
    /// doc comment for the pre-gameplay ad-gate logic this screen used to also drive (now
    /// independent of it).
    ///
    /// The root GameObject stays active for the app's whole lifetime (same convention
    /// SceneTransitionManager's own FadeOverlay uses) — visibility is driven purely by the
    /// CanvasGroup, never SetActive, so OnEnable's event subscription fires exactly once at scene
    /// load rather than needing to re-subscribe every time the banner shows. Time.timeScale is
    /// frozen for the duration (same convention Pause/Revive/the old pre-gameplay gate all use) so
    /// the celebration isn't fought over by robots/timer still running behind it; fades run on
    /// unscaled time accordingly.</summary>
    public class ComboHypeScreen : MonoBehaviour
    {
        [Serializable]
        private struct ComboBannerEntry
        {
            public string comboName;
            public Sprite banner;
        }

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backdropImage;
        [SerializeField] private Image bannerImage;
        [SerializeField] private ComboBannerEntry[] comboBanners;
        [SerializeField] private float totalSeconds = 5f;
        [SerializeField] private float fadeSeconds = 0.4f;

        /// <summary>Alpha the current world's gameplay backdrop is shown at behind the combo
        /// banner — same "faded scenery, not flat black" convention NewWorldUnlockScreen uses.</summary>
        private const float BackdropAlpha = 0.55f;

        private Coroutine _routine;

        /// <summary>True only while THIS screen is the one holding Time.timeScale at 0 — persists
        /// across an overlapping combo trigger cutting the previous ShowRoutine off mid-flight
        /// (HandleComboTriggered's StopCoroutine below), unlike a plain local "did I freeze it"
        /// bool scoped to one coroutine run, which would lose that fact the instant the routine
        /// holding it gets stopped and leave Time.timeScale stuck at 0 forever — the next combo's
        /// fresh routine would see time already frozen, correctly skip re-freezing, but then also
        /// skip un-freezing at ITS OWN end since (from its own local view) it never froze anything.</summary>
        private bool _isFrozenByThisScreen;

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
                // yet) — skip the celebration entirely rather than showing a random/wrong banner.
                return;
            }

            bannerImage.sprite = banner;

            if (backdropImage != null)
            {
                var backdropSprite = ResolveCurrentWorldBackdrop();
                if (backdropSprite != null)
                {
                    backdropImage.sprite = backdropSprite;
                    backdropImage.color = new Color(1f, 1f, 1f, BackdropAlpha);
                }
                else
                {
                    // No backdrop registered for this world yet — stay invisible so the root
                    // panel's own solid black shows through.
                    backdropImage.color = new Color(0f, 0f, 0f, 0f);
                }
            }

            if (_routine != null)
            {
                StopCoroutine(_routine);
            }
            _routine = StartCoroutine(ShowRoutine());
        }

        private static Sprite ResolveCurrentWorldBackdrop()
        {
            var level = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : null;
            if (level == null)
            {
                return null;
            }
            var tileMapRenderer = FindFirstObjectByType<TileMapRenderer>();
            if (tileMapRenderer == null)
            {
                return null;
            }
            return tileMapRenderer.GetOrAddArtSet(level.mazeType).backdropSprite;
        }

        private IEnumerator ShowRoutine()
        {
            transform.SetAsLastSibling(); // always draw on top of whatever's currently showing
            canvasGroup.blocksRaycasts = true;

            if (!_isFrozenByThisScreen && Time.timeScale != 0f)
            {
                Time.timeScale = 0f;
                _isFrozenByThisScreen = true;
            }

            AudioManager.Instance?.PlayComboSfx();

            yield return Fade(0f, 1f);
            float holdSeconds = Mathf.Max(0f, totalSeconds - fadeSeconds * 2f);
            yield return new WaitForSecondsRealtime(holdSeconds);
            yield return Fade(1f, 0f);

            canvasGroup.blocksRaycasts = false;
            if (_isFrozenByThisScreen && GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameState.Paused)
            {
                Time.timeScale = 1f;
                _isFrozenByThisScreen = false;
            }
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
