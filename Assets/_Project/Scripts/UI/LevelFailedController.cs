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
    /// Only 3 buttons now: Play (replay), Settings (opens the shared SettingsPanel overlay, same
    /// convention Pause's own Settings button uses), and Home (back to Level Select's world-select
    /// state, via LevelSelectController.ShowWorldSelect — the old Quit button, just relabelled/
    /// re-iconed to match the mockup's house icon). The previous 4th button, Skip (a lesser "back
    /// to Level Select" step than Quit/Home), has no equivalent in the new mockup and is gone.
    /// </summary>
    public class LevelFailedController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private GameObject gameplayScreen;
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
        }

        private void Play()
        {
            // Re-passes the current daily-challenge flag (rather than always defaulting to false)
            // so failing and restarting a Daily Challenge attempt stays a Daily Challenge attempt —
            // see GameManager.LoadLevel's isDailyChallenge doc comment.
            bool isDailyChallenge = DailyChallengeManager.Instance != null && DailyChallengeManager.Instance.IsPlayingDailyChallenge;
            SceneTransitionManager.Instance.TransitionTo(() =>
            {
                gameObject.SetActive(false);
                gameplayScreen.SetActive(true);
            });
            GameManager.Instance.LoadLevel(_levelIndex, isDailyChallenge);
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
