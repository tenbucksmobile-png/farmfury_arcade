using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Star/score celebration + breakdown, shown by GameplayHUD when GameManager.CurrentState
    /// becomes LevelComplete. Reads GameManager.LastLevelResult (computed in GameManager.EndLevel)
    /// rather than recomputing anything. If UnlockManager unlocked a character this level, shows
    /// NewCharacterUnlockScreen as an overlay once the celebration sequence finishes.
    /// </summary>
    public class LevelCompleteController : MonoBehaviour
    {
        [SerializeField] private StarDisplay starDisplay;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI cropBreakdownText;
        [SerializeField] private TextMeshProUGUI robotBreakdownText;
        [SerializeField] private TextMeshProUGUI timeBonusText;
        [SerializeField] private TextMeshProUGUI perfectBonusText;
        [SerializeField] private TextMeshProUGUI coinsEarnedText;
        [SerializeField] private GameObject newBestBadge;
        [SerializeField] private TextMeshProUGUI comboAchievementsText;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private GameObject worldMapScreen;
        [SerializeField] private GameObject gameplayScreen;
        [SerializeField] private NewCharacterUnlockScreen unlockScreen;

        private const float StarStepSeconds = 0.35f;
        private const float PreStarDelaySeconds = 0.3f;
        private const float PreUnlockDelaySeconds = 0.3f;

        private int _levelIndex;

        private void Awake()
        {
            replayButton.onClick.AddListener(Replay);
            nextLevelButton.onClick.AddListener(PlayNext);
            homeButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(worldMapScreen));
        }

        private void OnEnable()
        {
            StartCoroutine(CelebrationSequence());
        }

        private IEnumerator CelebrationSequence()
        {
            var result = GameManager.Instance.LastLevelResult;
            _levelIndex = GameManager.Instance.CurrentLevel.levelNumber;

            starDisplay.SetStars(0);
            scoreText.text = "0";
            cropBreakdownText.text = $"Crops: {result.cropScore}";
            robotBreakdownText.text = $"Robots: {result.robotScore}";
            timeBonusText.text = $"Time Bonus: {result.timeBonus}";
            perfectBonusText.text = result.perfectBonus > 0 ? "Perfect Run: +500" : "Perfect Run: --";
            coinsEarnedText.text = $"+{result.coinsEarned} coins";
            if (newBestBadge != null) newBestBadge.SetActive(result.isNewBestScore);

            comboAchievementsText.text = ComboSystem.Instance != null && ComboSystem.Instance.CombosTriggeredThisMaze.Count > 0
                ? "Combos: " + string.Join(", ", ComboSystem.Instance.CombosTriggeredThisMaze)
                : "Combos: none this run";

            nextLevelButton.interactable = false;

            yield return new WaitForSecondsRealtime(PreStarDelaySeconds);
            for (int i = 1; i <= result.stars; i++)
            {
                starDisplay.SetStars(i);
                yield return new WaitForSecondsRealtime(StarStepSeconds);
            }

            yield return CountUpScore(result.totalScore);

            nextLevelButton.interactable = DataManager.Instance.GetLevelData(_levelIndex + 1) != null;

            if (UnlockManager.Instance != null && UnlockManager.Instance.LastUnlockedBatch.Count > 0)
            {
                yield return new WaitForSecondsRealtime(PreUnlockDelaySeconds);
                unlockScreen.Show(UnlockManager.Instance.LastUnlockedBatch[0]);
            }
        }

        private IEnumerator CountUpScore(int target)
        {
            int shown = 0;
            while (shown < target)
            {
                shown = Mathf.Min(target, shown + Mathf.Max(20, target / 30));
                scoreText.text = shown.ToString("N0");
                yield return null;
            }
            scoreText.text = target.ToString("N0");
        }

        private void Replay() => PlayLevel(_levelIndex);

        private void PlayNext()
        {
            int next = _levelIndex + 1;
            if (DataManager.Instance.GetLevelData(next) == null)
            {
                SceneTransitionManager.Instance.ShowOnly(worldMapScreen);
                return;
            }
            PlayLevel(next);
        }

        private void PlayLevel(int levelIndex)
        {
            SceneTransitionManager.Instance.TransitionTo(() =>
            {
                gameObject.SetActive(false);
                gameplayScreen.SetActive(true);
            });
            GameManager.Instance.LoadLevel(levelIndex);
        }
    }
}
