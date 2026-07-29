using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Overlay on top of GameplayHUD (not routed through SceneTransitionManager.ShowOnly,
    /// which would hide Gameplay underneath). GameManager.PauseGame/ResumeGame own
    /// Time.timeScale — this just shows/hides the panel and calls them. Paused.png bakes in the
    /// "PAUSED" title and all 5 button-row backgrounds itself, so this screen has no dynamic text
    /// of its own — just the 5 real button-art images (Resume/SwapCharacter/Restart/Settings/
    /// Quit.png) overlaid exactly on top of where the background art draws them. "Quit" returns
    /// straight to Main Menu (GameManager.QuitToMainMenu) rather than going through the Level
    /// Failed screen — see that method's doc comment for why.</summary>
    public class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button swapButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitToMenuButton;
        [SerializeField] private ChooseCharacterScreen chooseCharacterScreen;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private GameObject mainMenuScreen;

        private void Awake()
        {
            resumeButton.onClick.AddListener(Resume);
            swapButton.onClick.AddListener(() => chooseCharacterScreen.Show());
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
            GameManager.Instance.QuitToMainMenu();
            SceneTransitionManager.Instance.ShowOnly(mainMenuScreen);
        }
    }
}
