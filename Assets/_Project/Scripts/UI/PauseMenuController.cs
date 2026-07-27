using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Overlay on top of GameplayHUD (not routed through SceneTransitionManager.ShowOnly,
    /// which would hide Gameplay underneath). GameManager.PauseGame/ResumeGame own
    /// Time.timeScale — this just shows/hides the panel and calls them. "Quit to Menu" ends the
    /// level as a failure (EndLevel(false) — per spec, LevelFailedController's own trigger is
    /// exactly "player quits during a run"); GameplayHUD's state-change watcher then shows
    /// LevelFailedController, so this class doesn't need a direct reference to it.</summary>
    public class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button swapButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitToMenuButton;
        [SerializeField] private CharacterSwapUI characterSwapUI;
        [SerializeField] private SettingsPanel settingsPanel;

        private void Awake()
        {
            resumeButton.onClick.AddListener(Resume);
            swapButton.onClick.AddListener(() => characterSwapUI.ToggleOpen());
            restartButton.onClick.AddListener(RestartLevel);
            settingsButton.onClick.AddListener(() => settingsPanel.Show());
            quitToMenuButton.onClick.AddListener(QuitToMenu);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        private void Resume()
        {
            gameObject.SetActive(false);
            GameManager.Instance.ResumeGame();
        }

        private void RestartLevel()
        {
            int levelIndex = GameManager.Instance.CurrentLevel.levelNumber;
            gameObject.SetActive(false);
            GameManager.Instance.ResumeGame(); // clears Paused/timeScale before the reload
            GameManager.Instance.LoadLevel(levelIndex);
        }

        private void QuitToMenu()
        {
            gameObject.SetActive(false);
            Time.timeScale = 1f;
            GameManager.Instance.EndLevel(false);
        }
    }
}
