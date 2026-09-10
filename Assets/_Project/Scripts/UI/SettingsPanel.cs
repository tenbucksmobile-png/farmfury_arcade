using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Rebuilt (2026-08-27) to match a new mockup — a single row of 4 icons (Music mute,
    /// Leaderboards, Character Story, Policies) instead of the earlier 4x2 grid. Shop, New Worlds,
    /// Remove Ads, and Restore Purchases all moved off this screen entirely in the same pass:
    /// Shop/Worlds/RemoveAds now live on the new Shop hub (see ShopController), and Restore
    /// Purchases moved to CoinPurchaseScreen (the actual IAP purchase surface). This screen is no
    /// longer reached directly from Main Menu either — Main Menu's Settings button now opens
    /// MenuHubScreen first, whose own "SETTINGS" sign opens this.
    ///
    /// Overlay convention, same as ShopController/CosmeticsHubScreen — shown/hidden directly via
    /// Show()/SetActive, not through SceneTransitionManager. The close button matches the plain
    /// "back" icon (Btn_back.png) those screens use, so it simply closes this overlay (revealing
    /// whatever was underneath — MenuHubScreen or Pause) instead of forcing navigation anywhere.</summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private Button closeButton;

        [SerializeField] private Button musicButton;
        [SerializeField] private Image musicButtonIcon;

        [SerializeField] private Button leaderboardsButton;
        [SerializeField] private GameObject leaderboardsScreen;

        // "This is where we will tell a story about each character" — placeholder destination,
        // no real content yet (see Phase5ProjectBuilder.BuildCharacterStoryPlaceholder).
        [SerializeField] private Button characterStoryButton;
        [SerializeField] private GameObject characterStoryScreen;

        // Policies — opens the legal hub (LegalScreen): Privacy Policy, Terms of Use, and any
        // other required legal copy.
        [SerializeField] private Button policiesButton;
        [SerializeField] private GameObject policiesScreen;

        /// <summary>Dims the music icon when muted — same tint-based on/off feedback convention
        /// LockedTint/InactiveTabTint use elsewhere, since no dedicated "muted" art variant exists
        /// for this icon.</summary>
        private static readonly Color MutedTint = new Color(0.5f, 0.5f, 0.5f, 1f);

        /// <summary>Whichever overlay called Show(opener), if any — MenuHubScreen or
        /// PauseMenuController, neither of which is a SceneTransitionManager screenRoot, so neither
        /// gets closed automatically by ShowOnly. Closed alongside this panel itself before the
        /// Leaderboards button navigates to a real screenRoot; otherwise it stays active on top
        /// (opaque backdrop) and visually blocks the screen ShowOnly just activated underneath it —
        /// this was the actual cause of "the leaderboards icon does not go into the leaderboards
        /// page" (2026-09-09): the navigation itself worked, the opener overlay was just still
        /// covering it. LevelFailedController's own Show() call needs no opener — LevelFailedScreen
        /// IS a screenRoot, so ShowOnly already deactivates it correctly on its own.</summary>
        private GameObject _opener;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            }
            if (musicButton != null)
            {
                musicButton.onClick.AddListener(HandleMusicButtonTapped);
            }
            if (leaderboardsButton != null && leaderboardsScreen != null)
            {
                leaderboardsButton.onClick.AddListener(() =>
                {
                    // Real bug found and fixed (2026-09-11): this used to hide Settings/_opener
                    // synchronously, THEN call ShowOnly — but ShowOnly's own fade only reaches full
                    // opaque black partway through its ramp (fadeSeconds, not instant), so for that
                    // whole ramp-up window Main Menu (the screenRoot now exposed underneath the
                    // just-hidden overlays) was visible through the still-transparent fade before
                    // Leaderboards actually swapped in — read as "the landing page flashes for a
                    // split second before Leaderboards opens." Fixed by passing the hide logic in
                    // as ShowOnly's new beforeSwap callback instead, which only runs once the fade
                    // has already reached full opaque black, same as every other screenRoot swap.
                    SceneTransitionManager.Instance.ShowOnly(leaderboardsScreen, () =>
                    {
                        gameObject.SetActive(false);
                        if (_opener != null)
                        {
                            // Pause needs its own GameState.Paused/Time.timeScale=0 reset too, not
                            // just hiding — see CloseForNavigation's own doc comment. MenuHubScreen
                            // (Main Menu's opener) has no such state and just needs to be hidden.
                            var pause = _opener.GetComponent<PauseMenuController>();
                            if (pause != null)
                            {
                                pause.CloseForNavigation();
                            }
                            else
                            {
                                _opener.SetActive(false);
                            }
                        }
                    });
                });
            }
            if (characterStoryButton != null && characterStoryScreen != null)
            {
                characterStoryButton.onClick.AddListener(() =>
                {
                    characterStoryScreen.transform.SetAsLastSibling();
                    characterStoryScreen.SetActive(true);
                });
            }
            if (policiesButton != null && policiesScreen != null)
            {
                policiesButton.onClick.AddListener(() =>
                {
                    policiesScreen.transform.SetAsLastSibling();
                    policiesScreen.SetActive(true);
                });
            }
        }

        public void Show(GameObject opener = null)
        {
            _opener = opener;
            RefreshMusicIcon();
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        private void HandleMusicButtonTapped()
        {
            bool on = !SaveManager.Instance.MusicOn;
            SaveManager.Instance.MusicOn = on;
            AudioManager.Instance?.SetMusicMuted(!on);
            RefreshMusicIcon();
        }

        private void RefreshMusicIcon()
        {
            if (musicButtonIcon != null && SaveManager.Instance != null)
            {
                musicButtonIcon.color = SaveManager.Instance.MusicOn ? Color.white : MutedTint;
            }
        }
    }
}
