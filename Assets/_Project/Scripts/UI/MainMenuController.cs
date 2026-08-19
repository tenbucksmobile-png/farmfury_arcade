using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Top-level menu — Play/Settings/Shop only. Leaderboards moved to Settings (its own
    /// top-right button there) and Daily Challenge moved to Level Select (its own top-right button
    /// there) per feedback that the landing page should stay to just these three; both had briefly
    /// lived here as top-corner buttons before that move. Character Roster still has no entry
    /// point anywhere (see CLAUDE.md's "Known gaps").
    ///
    /// Play opens Level Select directly. An intermediate "World Map" screen (Map.png background,
    /// Play/Home nav buttons — `WorldMapController`) used to sit here; it was removed outright
    /// (deleted, not just unlinked — see CLAUDE.md's "Removed: World Map screen") once Level
    /// Select's scrollable tile grid (grouped into world sections with CORN FIELD/VEGETABLE
    /// PATCH/ORCHARD/WHEAT FIELD dividers) made it fully redundant.</summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button shopButton;

        [SerializeField] private GameObject levelSelectScreen;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private ShopController shopScreen;

        private void Awake()
        {
            playButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(levelSelectScreen));
            settingsButton.onClick.AddListener(() => settingsPanel.Show());
            if (shopButton != null && shopScreen != null)
            {
                shopButton.onClick.AddListener(() => shopScreen.Show());
            }
        }

        // Fires both at app launch (Main Menu starts active) and every time the player navigates
        // back here (LevelSelect's Back button, LevelFailed's Menu button, Pause's Quit button) —
        // a single hook point regardless of which path led back to Main Menu.
        private void OnEnable()
        {
            AudioManager.Instance?.PlayLandingMusic();
        }

        // Deliberately does NOT stop music on disable (tapping Play used to cut straight to
        // silence here, leaving Level Select browsing with no music at all) — the landing track
        // keeps playing until GameManager.LoadLevel's own ResumeBackgroundMusic crossfade takes
        // over once the player actually starts a level.
    }
}
