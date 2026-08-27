using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Overlay on top of GameplayHUD (not routed through SceneTransitionManager.ShowOnly,
    /// which would hide Gameplay underneath). GameManager.PauseGame/ResumeGame own Time.timeScale —
    /// this just shows/hides the panel and calls them.
    ///
    /// Rebuilt (2026-08-27) to match a new mockup exactly, discarding the old 5-button
    /// Resume/SwapCharacter/Restart/Settings/Quit design built on Paused.png's baked-in rows —
    /// Bg_LevelSelect.png background, Logo.png top-left, a "Pause" wood-sign banner (Pause.png,
    /// not the old square Paused.png card, which is no longer referenced anywhere), and the exact
    /// same 4-button Play/Skip/Settings/Quit layout LevelFailedController uses (same art, same
    /// positions). Skip/Settings/Quit are wired identically to LevelFailedController's — Play is
    /// the one deliberate difference: on Level Failed it replays the level (there's nothing to
    /// resume, the run already ended), but Pause is reached mid-successful-run, so its Play button
    /// resumes gameplay instead — the universal play/resume meaning of that icon, and the only way
    /// left to simply un-pause now that the standalone Resume row is gone.
    ///
    /// The Swap Character button is gone from this screen (no room for it in the new 4-button
    /// mockup) — it's moving to the Gameplay HUD itself in a follow-up pass, not being removed
    /// outright. ChooseCharacterScreen itself, its own Tab-key shortcut
    /// (InputController.OnSwapMenuToggleInput), and its pauseMenuScreen back-reference (wired in
    /// Phase5ProjectBuilder.WireCrossReferences, entirely independent of this class) are all still
    /// fully intact and untouched — only this screen's own button pointing at it is gone.</summary>
    public class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private LevelSelectController levelSelectController;
        [SerializeField] private GameObject levelSelectScreen;

        private void Awake()
        {
            playButton.onClick.AddListener(Resume);
            skipButton.onClick.AddListener(Skip);
            if (settingsButton != null && settingsPanel != null)
            {
                settingsButton.onClick.AddListener(() => settingsPanel.Show());
            }
            quitButton.onClick.AddListener(QuitToWorldSelect);
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

        private void Skip()
        {
            gameObject.SetActive(false);
            GameManager.Instance.QuitToLevelSelect();
            SceneTransitionManager.Instance.ShowOnly(levelSelectScreen);
        }

        private void QuitToWorldSelect()
        {
            gameObject.SetActive(false);
            GameManager.Instance.QuitToLevelSelect();
            if (levelSelectController != null)
            {
                levelSelectController.ShowWorldSelect();
            }
            SceneTransitionManager.Instance.ShowOnly(levelSelectScreen);
        }
    }
}
