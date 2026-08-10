using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Top-level menu. Stripped to just Play/Settings per the landing-page cleanup —
    /// Character Roster, Daily Challenge, Store, and Leaderboards no longer have an entry point
    /// here. Their screens/scripts are untouched (still built, still reachable if something else
    /// calls SceneTransitionManager.ShowOnly on them directly) — only the Main Menu buttons and
    /// this controller's references to them were removed.
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

        [SerializeField] private GameObject levelSelectScreen;
        [SerializeField] private SettingsPanel settingsPanel;

        private void Awake()
        {
            playButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(levelSelectScreen));
            settingsButton.onClick.AddListener(() => settingsPanel.Show());
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
