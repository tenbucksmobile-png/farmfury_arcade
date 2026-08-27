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
                leaderboardsButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(leaderboardsScreen));
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

        public void Show()
        {
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
