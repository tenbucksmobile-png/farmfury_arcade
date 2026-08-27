using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Shown by GameplayHUD when GameManager.CurrentState becomes LevelFailed (a timer expiry or
    /// exhausting the respawn cap — see GameManager.MaxRespawns/LevelTimeLimitSeconds). Rebuilt to a
    /// 2026-08-27 mockup: Bg_LevelSelect.png (night farm) root background, Logo.png top-left, and
    /// the "TRY AGAIN!" card (LevelFailed.png) as an aspect-locked PanelArt child (same
    /// square-art-on-landscape-overlay fix Pause/Level Complete already have) — carrying a real
    /// StarDisplay + score readout in its blank parchment interior (score sits BELOW the stars here,
    /// the opposite order from LevelCompleteController's own ShelfContent, per the mockup and per
    /// explicit direction — a failed run never earns stars, so the star row leads and the score
    /// earned before failing sits underneath it).
    ///
    /// 4 real buttons now instead of 2: Play (replay), Skip (back to Level Select, same "one step
    /// back to where the player picked this level from" convention the old single Quit button had),
    /// Settings (opens the shared SettingsPanel overlay, same convention Pause's own Settings button
    /// uses), and Quit (back to Level Select's world-select state specifically, via
    /// LevelSelectController.ShowWorldSelect — a bigger step back than Skip).
    /// </summary>
    public class LevelFailedController : MonoBehaviour
    {
        [SerializeField] private StarDisplay starDisplay;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private Button playButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject gameplayScreen;
        [SerializeField] private GameObject levelSelectScreen;
        [SerializeField] private LevelSelectController levelSelectController;
        [SerializeField] private SettingsPanel settingsPanel;

        private int _levelIndex;

        private void Awake()
        {
            playButton.onClick.AddListener(Play);
            skipButton.onClick.AddListener(Skip);
            if (settingsButton != null && settingsPanel != null)
            {
                settingsButton.onClick.AddListener(() => settingsPanel.Show());
            }
            quitButton.onClick.AddListener(QuitToWorldSelect);
        }

        private void OnEnable()
        {
            _levelIndex = GameManager.Instance.CurrentLevel != null ? GameManager.Instance.CurrentLevel.levelNumber : 0;

            // A failed run never earns stars — shown as an all-empty row, same StarDisplay
            // component LevelCompleteController uses, just always passed 0. The score readout still
            // reflects whatever was actually scored this attempt: GameManager.LastLevelResult isn't
            // populated on a failure (see EndLevel's success/failure branch), so this reads the live
            // running total directly off ScoreManager instead, which EndLevel(false) never resets.
            if (starDisplay != null)
            {
                starDisplay.SetStars(0);
            }
            if (scoreText != null)
            {
                int score = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentMazeScore : 0;
                scoreText.text = score.ToString("N0");
            }
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

        private void Skip()
        {
            GameManager.Instance.QuitToLevelSelect();
            SceneTransitionManager.Instance.ShowOnly(levelSelectScreen);
        }

        private void QuitToWorldSelect()
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
