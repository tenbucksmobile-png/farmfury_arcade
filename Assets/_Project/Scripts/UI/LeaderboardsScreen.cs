using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Leaderboards screen — per direct feedback (2026-09-09), stripped down to just the
    /// header (Leaderboard.png) and the two stat-icon banners (HighScore.png/Combo.png) with no
    /// text anywhere on the screen at all, not even the numeric values the icons used to sit
    /// beside. The underlying stats (LeaderboardManager.GetTotalLifetimeScore/
    /// GetTotalCombosTriggered/GetHighestLevelReached/GetCharactersMasteredCount) are all still
    /// live and queryable — only this screen's own display of them was removed; if numeric values
    /// (or the Highest-Level-Reached/Characters-Mastered lines this screen used to show) come back
    /// later, wire them back onto LeaderboardManager the same way this class used to.
    ///
    /// Back button (2026-09-09): this screen is only ever reached via SettingsPanel's own
    /// Leaderboards icon (SettingsPanel.leaderboardsButton), which — since LeaderboardsScreen is a
    /// real SceneTransitionManager screenRoot, not an overlay — has to swap the active screenRoot
    /// away to get here, closing Settings (and whatever opened Settings) in the process. Back used
    /// to just call ShowOnly(mainMenuScreen), which left the player on a bare landing page with
    /// Settings closed rather than actually returning them to "the Settings page" as it reads to
    /// the player. Fixed by restoring the mainMenu screenRoot AND reopening Settings on top of it,
    /// same "screenRoot swap plus overlay reopen" shape SettingsPanel's own leaderboardsButton
    /// handler uses in the other direction.</summary>
    public class LeaderboardsScreen : MonoBehaviour
    {
        [SerializeField] private Button backButton;
        [SerializeField] private GameObject mainMenuScreen;
        [SerializeField] private SettingsPanel settingsPanel;

        private void Awake()
        {
            backButton.onClick.AddListener(HandleBack);
        }

        private void HandleBack()
        {
            SceneTransitionManager.Instance.ShowOnly(mainMenuScreen);
            settingsPanel?.Show();
        }
    }
}
