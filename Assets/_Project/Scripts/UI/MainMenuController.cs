using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.UI
{
    /// <summary>Top-level menu. Store is explicitly Phase 6 scope per spec ("cosmetics store
    /// (Phase 6)") — its button shows a small "coming soon" overlay rather than a real screen.
    /// Leaderboards is real (local, per spec — cloud sync is Phase 6).</summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button characterRosterButton;
        [SerializeField] private Button dailyChallengeButton;
        [SerializeField] private GameObject dailyChallengeBadge;
        [SerializeField] private Button storeButton;
        [SerializeField] private GameObject storeComingSoonPanel;
        [SerializeField] private Button leaderboardsButton;
        [SerializeField] private Button settingsButton;

        [SerializeField] private GameObject worldMapScreen;
        [SerializeField] private GameObject characterRosterScreen;
        [SerializeField] private GameObject leaderboardsScreen;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private MatchupScreenController matchupScreen;

        private void Awake()
        {
            playButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(worldMapScreen));
            characterRosterButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(characterRosterScreen));
            dailyChallengeButton.onClick.AddListener(OpenDailyChallenge);
            storeButton.onClick.AddListener(() => storeComingSoonPanel.SetActive(true));
            leaderboardsButton.onClick.AddListener(() => SceneTransitionManager.Instance.ShowOnly(leaderboardsScreen));
            settingsButton.onClick.AddListener(() => settingsPanel.Show());
        }

        private void OnEnable()
        {
            RefreshDailyBadge();
        }

        private void RefreshDailyBadge()
        {
            bool showBadge = DailyChallengeManager.Instance != null && !DailyChallengeManager.Instance.IsCompletedToday;
            if (dailyChallengeBadge != null)
            {
                dailyChallengeBadge.SetActive(showBadge);
            }
        }

        private void OpenDailyChallenge()
        {
            matchupScreen.ShowForLevel(DailyChallengeManager.DailyChallengeLevelIndex, isDailyChallenge: true);
        }
    }
}
