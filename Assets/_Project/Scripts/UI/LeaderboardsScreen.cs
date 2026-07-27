using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Local overall stats (per spec: "local for now, cloud in Phase 6"). Per-level bests
    /// are available via SaveManager.GetLevelBestScore/GetLevelBestTime once a real level list UI
    /// is worth building — kept to overall rollups here to match WorldMapController's own
    /// per-level star display rather than duplicating it.</summary>
    public class LeaderboardsScreen : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private Button backButton;
        [SerializeField] private GameObject mainMenuScreen;

        private void Awake()
        {
            backButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(mainMenuScreen));
        }

        private void OnEnable()
        {
            var lb = LeaderboardManager.Instance;
            if (lb == null || statsText == null)
            {
                return;
            }

            statsText.text =
                $"Highest Level Reached: {lb.GetHighestLevelReached() + 1}\n" +
                $"Total Lifetime Score: {lb.GetTotalLifetimeScore():N0}\n" +
                $"Total Combos Triggered: {lb.GetTotalCombosTriggered()}\n" +
                $"Characters Mastered: {lb.GetCharactersMasteredCount()}/8";
        }
    }
}
