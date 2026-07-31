using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// Star/score celebration, shown by GameplayHUD when GameManager.CurrentState becomes
    /// LevelComplete. Reads GameManager.LastLevelResult (computed in GameManager.EndLevel) rather
    /// than recomputing anything. If UnlockManager unlocked a character this level, shows
    /// NewCharacterUnlockScreen as an overlay once the celebration sequence finishes.
    ///
    /// Rebuilt to a Canva mockup (2026-07-31): LevelComplete.png's panel only has room for the
    /// "LEVEL COMPLETE!" banner (baked into the art), 3 stars, and a score readout on its wooden
    /// shelf — the previous crop/robot/time/perfect-bonus breakdown, combo-achievements line, "new
    /// best" badge, and the Replay/Next Level/Home button row are gone. A single Skip button
    /// (bottom-right, per the mockup) is the only way off this screen now — it returns to Level
    /// Select rather than jumping straight into another level, since the coin/star/unlock data this
    /// screen used to summarize piecemeal is already visible there (world badges reflect newly
    /// unlocked worlds automatically on open — see LevelSelectController.IsWorldAvailable — and the
    /// tile grid reflects the just-earned stars the same way).
    /// </summary>
    public class LevelCompleteController : MonoBehaviour
    {
        [SerializeField] private StarDisplay starDisplay;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private Button skipButton;
        [SerializeField] private GameObject levelSelectScreen;
        [SerializeField] private NewCharacterUnlockScreen unlockScreen;

        private const float StarStepSeconds = 0.35f;
        private const float PreStarDelaySeconds = 0.3f;
        private const float PreUnlockDelaySeconds = 0.3f;

        private void Awake()
        {
            skipButton.onClick.AddListener(Skip);
        }

        private void OnEnable()
        {
            StartCoroutine(CelebrationSequence());
        }

        private IEnumerator CelebrationSequence()
        {
            var result = GameManager.Instance.LastLevelResult;

            starDisplay.SetStars(0);
            scoreText.text = "0";

            yield return new WaitForSecondsRealtime(PreStarDelaySeconds);
            for (int i = 1; i <= result.stars; i++)
            {
                starDisplay.SetStars(i);
                yield return new WaitForSecondsRealtime(StarStepSeconds);
            }

            yield return CountUpScore(result.totalScore);

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

        private void Skip() => SceneTransitionManager.Instance.ShowOnly(levelSelectScreen);
    }
}
