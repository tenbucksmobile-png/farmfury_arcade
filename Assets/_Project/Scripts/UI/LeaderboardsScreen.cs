using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Leaderboards screen — per direct feedback (2026-09-09), stripped down to just the
    /// header (Leaderboard.png) and a single stat-icon banner (HighScore.png) with no text anywhere
    /// on the screen at all, not even the numeric value. Combo.png was removed 2026-09-10 (this
    /// page is being built out with more real content) and HighScore.png now sits directly above a
    /// real Btn_plaque.png + number showing LeaderboardManager.GetTotalLifetimeScore() — the first
    /// of this screen's stats to get an actual on-screen value again. The other underlying stats
    /// (GetTotalCombosTriggered/GetHighestLevelReached/GetCharactersMasteredCount) are still live
    /// and queryable on LeaderboardManager if a future pass wants to add more entries below this
    /// one the same way.
    ///
    /// Back button (2026-09-09): this screen is only ever reached via SettingsPanel's own
    /// Leaderboards icon (SettingsPanel.leaderboardsButton), which — since LeaderboardsScreen is a
    /// real SceneTransitionManager screenRoot, not an overlay — has to swap the active screenRoot
    /// away to get here, closing Settings (and whatever opened Settings) in the process. Back used
    /// to just call ShowOnly(mainMenuScreen), which left the player on a bare landing page with
    /// Settings closed rather than actually returning them to "the Settings page" as it reads to
    /// the player. Fixed by restoring the mainMenu screenRoot AND reopening Settings on top of it,
    /// same "screenRoot swap plus overlay reopen" shape SettingsPanel's own leaderboardsButton
    /// handler uses in the other direction.
    ///
    /// Real bug found and fixed (2026-09-11): reopening Settings here used to call
    /// settingsPanel.Show() with no opener argument — Settings is only ever reached from Main Menu
    /// via MenuHubScreen ("SETTINGS" sign), so Settings' own back button expects to reveal that hub
    /// underneath it, not Main Menu directly. Passing no opener left Settings' _opener null, so a
    /// player who opened Leaderboards, came back here, then tapped Settings' own back button landed
    /// straight on Main Menu instead of the settings/shop hub they actually came from — reported as
    /// "settings back button navigates to the landing page, should go back to the settings/shop
    /// hub." Fixed by reopening MenuHubScreen first and passing IT as Settings' opener, same as
    /// MainMenuController's own settingsButton does. Both hide calls also moved into ShowOnly's
    /// beforeSwap callback (not run eagerly beforehand) so nothing flashes visible mid-fade, same
    /// fix SettingsPanel.leaderboardsButton needed for the reverse direction.</summary>
    public class LeaderboardsScreen : MonoBehaviour
    {
        [SerializeField] private Button backButton;
        [SerializeField] private GameObject mainMenuScreen;
        [SerializeField] private MenuHubScreen menuHubScreen;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private TextMeshProUGUI scoreText;

        // The plaque behind scoreText is built at a compact default width (light padding around a
        // few digits) — a real lifetime score can run well past that, so it's widened here to the
        // text's own real measured width, same GetPreferredValues technique
        // CoinPurchaseScreen.ResizeRestoreButtonToFitLabel uses for its own plaque.
        [SerializeField] private RectTransform scorePlaqueRect;
        [SerializeField] private float scorePlaqueMinWidth = 260f;
        private const float ScorePlaqueHorizontalPadding = 72f; // matches scoreText's own 36px-per-side inset

        private void Awake()
        {
            backButton.onClick.AddListener(HandleBack);
        }

        private void OnEnable()
        {
            RefreshScoreText();
        }

        private void RefreshScoreText()
        {
            if (scoreText == null)
            {
                return;
            }
            int score = LeaderboardManager.Instance != null ? LeaderboardManager.Instance.GetTotalLifetimeScore() : 0;
            scoreText.text = score.ToString("N0");

            if (scorePlaqueRect != null)
            {
                float labelWidth = scoreText.GetPreferredValues(scoreText.text, 0f, 0f).x;
                float desiredWidth = Mathf.Max(scorePlaqueMinWidth, labelWidth + ScorePlaqueHorizontalPadding);
                scorePlaqueRect.sizeDelta = new Vector2(desiredWidth, scorePlaqueRect.sizeDelta.y);
            }
        }

        private void HandleBack()
        {
            SceneTransitionManager.Instance.ShowOnly(mainMenuScreen, () =>
            {
                menuHubScreen?.Show();
                settingsPanel?.Show(menuHubScreen != null ? menuHubScreen.gameObject : null);
            });
        }
    }
}
