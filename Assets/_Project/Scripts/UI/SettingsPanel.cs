using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Rebuilt from scratch (2026-08-20) to match a new Canva mockup exactly — see
    /// Phase5ProjectBuilder.BuildSettingsPanel for the full layout rationale. Discards the entire
    /// old 2x3 toggle/dropdown/Restore-Purchases grid; this is now a 4x2 icon grid, row 1 wired
    /// (Shop, Music mute, Leaderboards, Character Story placeholder), row 2 deliberately blank.
    ///
    /// Overlay convention, same as ShopController/CosmeticsHubScreen — shown/hidden directly via
    /// Show()/SetActive, not through SceneTransitionManager. The close button now matches the same
    /// plain "back" icon (Btn_back.png) those screens use rather than the old distinct "go home"
    /// icon, so it simply closes this overlay (revealing whatever was underneath — Main Menu or
    /// Pause) instead of always forcing navigation back to Main Menu.</summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private Button closeButton;

        [SerializeField] private Button shopButton;
        [SerializeField] private ShopController shopScreen;

        [SerializeField] private Button musicButton;
        [SerializeField] private Image musicButtonIcon;

        [SerializeField] private Button leaderboardsButton;
        [SerializeField] private GameObject leaderboardsScreen;

        // "This is where we will tell a story about each character" — placeholder destination,
        // no real content yet (see Phase5ProjectBuilder.BuildCharacterStoryPlaceholder).
        [SerializeField] private Button characterStoryButton;
        [SerializeField] private GameObject characterStoryScreen;

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
            if (shopButton != null && shopScreen != null)
            {
                shopButton.onClick.AddListener(() => shopScreen.Show());
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
                characterStoryButton.onClick.AddListener(() => characterStoryScreen.SetActive(true));
            }
        }

        public void Show()
        {
            RefreshMusicIcon();
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
