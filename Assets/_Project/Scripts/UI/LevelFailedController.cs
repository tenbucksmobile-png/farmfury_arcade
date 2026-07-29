using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Shown by GameplayHUD when GameManager.CurrentState becomes LevelFailed. Per spec/GDD there
    /// is no lives or timer-fail system, so the only path here is a manual quit
    /// (PauseMenuController's "Quit to Menu" calls GameManager.EndLevel(false)) — title stays
    /// friendly rather than punishing, per spec. LevelFailed.png bakes in the "TRY AGAIN!" banner
    /// and "SCORE"/"BEST" labels itself, so this screen has no dynamic text of its own — just the
    /// two real button-art images (Retry.png/Menu.png) overlaid exactly on top of where the
    /// background art draws them.
    /// </summary>
    public class LevelFailedController : MonoBehaviour
    {
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private GameObject gameplayScreen;
        [SerializeField] private GameObject mainMenuScreen;

        private int _levelIndex;

        private void Awake()
        {
            retryButton.onClick.AddListener(Retry);
            menuButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(mainMenuScreen));
        }

        private void OnEnable()
        {
            _levelIndex = GameManager.Instance.CurrentLevel != null ? GameManager.Instance.CurrentLevel.levelNumber : 0;
        }

        private void Retry()
        {
            SceneTransitionManager.Instance.TransitionTo(() =>
            {
                gameObject.SetActive(false);
                gameplayScreen.SetActive(true);
            });
            GameManager.Instance.LoadLevel(_levelIndex);
        }
    }
}
