using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Top-level menu — Play/Settings only. Leaderboards live on Settings (its own button
    /// there) and Daily Challenge lives on Level Select (the first shield in its world carousel)
    /// per feedback that the landing page should stay minimal — both briefly lived here as
    /// top-corner buttons before those moves. Character Roster still has no entry point anywhere
    /// (see CLAUDE.md's "Known gaps").
    ///
    /// Play opens Level Select directly. An intermediate "World Map" screen (Map.png background,
    /// Play/Home nav buttons — `WorldMapController`) used to sit here; it was removed outright
    /// (deleted, not just unlinked — see CLAUDE.md's "Removed: World Map screen") once Level
    /// Select's scrollable tile grid (grouped into world sections with CORN FIELD/VEGETABLE
    /// PATCH/ORCHARD/WHEAT FIELD dividers) made it fully redundant.
    ///
    /// Settings (2026-08-27) no longer opens SettingsPanel directly — it opens MenuHubScreen
    /// first, a new intermediate overlay with two sign buttons ("SETTINGS"/"Shop") matching a new
    /// mockup, giving Shop a discoverable entry point from Main Menu it didn't have before (Shop
    /// had no entry point on this screen at all prior to this change).</summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;

        [SerializeField] private GameObject levelSelectScreen;
        [SerializeField] private MenuHubScreen menuHubScreen;

        private void Awake()
        {
            playButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(levelSelectScreen));
            settingsButton.onClick.AddListener(() => menuHubScreen.Show());
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
        // keeps playing until GameManager.LoadLevel's own PlayWorldMusic crossfade takes over
        // once the player actually starts a level.
    }
}
