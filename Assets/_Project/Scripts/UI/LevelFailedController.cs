using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Shown by GameplayHUD when GameManager.CurrentState becomes LevelFailed. Per spec/GDD there
    /// is no lives or timer-fail system, so the only path here is a manual quit
    /// (PauseMenuController's "Quit to Menu" calls GameManager.EndLevel(false)) — title stays
    /// friendly rather than punishing, per spec.
    /// </summary>
    public class LevelFailedController : MonoBehaviour
    {
        private static readonly string[] Tips =
        {
            "Try swapping characters to find the right ability for the moment.",
            "Power pellets turn the tables — save them for when robots are close.",
            "Corners and intersections are your best escape routes.",
        };

        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI tipText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private GameObject gameplayScreen;
        [SerializeField] private GameObject worldMapScreen;

        private int _levelIndex;

        private void Awake()
        {
            retryButton.onClick.AddListener(Retry);
            homeButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(worldMapScreen));
        }

        private void OnEnable()
        {
            _levelIndex = GameManager.Instance.CurrentLevel != null ? GameManager.Instance.CurrentLevel.levelNumber : 0;
            if (scoreText != null && ScoreManager.Instance != null)
            {
                scoreText.text = $"Score: {ScoreManager.Instance.CurrentMazeScore:N0}";
            }
            if (tipText != null)
            {
                tipText.text = Tips[Random.Range(0, Tips.Length)];
            }
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
