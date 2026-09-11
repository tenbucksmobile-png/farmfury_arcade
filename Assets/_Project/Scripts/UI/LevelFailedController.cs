using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Shown by GameplayHUD when GameManager.CurrentState becomes LevelFailed (a timer expiry or
    /// exhausting the respawn cap — see GameManager.MaxRespawns/LevelTimeLimitSeconds). Rebuilt
    /// (2026-08-30) to match a new "GAME OVER" mockup: Bg_LevelSelect.png (night farm) root
    /// background, Logo.png top-left, and a wood-sign "GAME OVER" banner (see
    /// Phase5ProjectBuilder.BuildLevelFailed for the placeholder frame around GameOver.png's bare
    /// text) — no star/score readout at all, unlike the previous "TRY AGAIN!" card design.
    ///
    /// Only 3 buttons now: Play, Settings (opens the shared SettingsPanel overlay, same
    /// convention Pause's own Settings button uses), and Home (back to Level Select's world-select
    /// state, via LevelSelectController.ShowWorldSelect — the old Quit button, just relabelled/
    /// re-iconed to match the mockup's house icon). The previous 4th button, Skip (a lesser "back
    /// to Level Select" step than Quit/Home), has no equivalent in the new mockup and is gone.
    ///
    /// Play used to restart the failed level directly — changed per direct feedback that it
    /// should instead return to Level Select (the tile grid for the world containing the level
    /// just failed, same "jump straight to the relevant world" convention LevelCompleteController's
    /// own Play button uses), so the player can choose to retry it or pick a different level rather
    /// than being dropped straight back into another attempt with no choice.
    ///
    /// Shows AdManager's banner ad while this screen is open (2026-09-11 monetisation pass), same
    /// OnEnable/OnDisable convention as PauseMenuController's own banner — see that class' doc
    /// comment for why it can't be baked into this screen's own art (native overlay, not a Unity UI
    /// element). This screen IS a real SceneTransitionManager screenRoot (unlike Pause), so
    /// ShowOnly's own deactivation already guarantees OnDisable fires on every exit path (Play,
    /// GoHome) with nothing extra needed here. The 3-button row was shifted up to leave clear room
    /// for it — see BuildLevelFailed's own layout comment.
    /// </summary>
    public class LevelFailedController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private GameObject levelSelectScreen;
        [SerializeField] private LevelSelectController levelSelectController;
        [SerializeField] private SettingsPanel settingsPanel;

        private int _levelIndex;

        private void Awake()
        {
            playButton.onClick.AddListener(Play);
            if (settingsButton != null && settingsPanel != null)
            {
                settingsButton.onClick.AddListener(() => settingsPanel.Show());
            }
            homeButton.onClick.AddListener(GoHome);
        }

        private void OnEnable()
        {
            _levelIndex = GameManager.Instance.CurrentLevel != null ? GameManager.Instance.CurrentLevel.levelNumber : 0;
            AdManager.Instance?.ShowBanner();
        }

        private void OnDisable()
        {
            AdManager.Instance?.HideBanner();
        }

        private void Play()
        {
            if (levelSelectController != null)
            {
                levelSelectController.OpenLevelSelectForLevel(_levelIndex);
            }
            SceneTransitionManager.Instance.ShowOnly(levelSelectScreen);
        }

        private void GoHome()
        {
            GameManager.Instance.QuitToLevelSelect();
            if (levelSelectController != null)
            {
                levelSelectController.ShowWorldSelect();
            }
            SceneTransitionManager.Instance.ShowOnly(levelSelectScreen);
        }
    }
}
